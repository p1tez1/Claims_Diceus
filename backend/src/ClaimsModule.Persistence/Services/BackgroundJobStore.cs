using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Services;

public sealed class BackgroundJobStore(DbContextOptions<ClaimsDbContext> options, IClock clock,
    IIdentifierGenerator ids) : IBackgroundJobStore
{
    private ClaimsDbContext Create(Guid tenant) => new(options, new JobUser(tenant), clock);

    public Task<bool> PostAsync(GlPostingWork work, string jobId, CancellationToken cancellationToken = default) =>
        ChangePostingAsync(work, jobId, null, cancellationToken);

    public Task<bool> FailAsync(GlPostingWork work, string jobId, string reason, CancellationToken cancellationToken = default) =>
        ChangePostingAsync(work, jobId, reason, cancellationToken);

    private async Task<bool> ChangePostingAsync(GlPostingWork work, string jobId, string? failure,
        CancellationToken cancellationToken)
    {
        // Only discovery ignores tenant filters. All writes use the tenant found on the exact reserve identity.
        await using var discovery = Create(Guid.Empty);
        var tenant = await discovery.ReserveHistory.IgnoreQueryFilters().Where(h => h.Id == work.ReserveHistoryId
            && h.ClaimId == work.ClaimId && h.IdempotencyKey == work.IdempotencyKey)
            .Select(h => (Guid?)EF.Property<Guid>(h, "OrganisationId")).SingleOrDefaultAsync(cancellationToken);
        if (!tenant.HasValue) return false;
        await using var db = Create(tenant.Value);
        await using var unit = new UnitOfWork(db);
        var result = await unit.ExecuteAsync(async token =>
        {
            var claim = await LockedClaim(db, work.ClaimId, token);
            if (claim is null) return false;
            var components = await new ReserveComponentRepository(db).GetForClaimAsync(claim.Id, token);
            var component = components.SingleOrDefault(c => c.ReserveHistory.Any(h => h.Id == work.ReserveHistoryId));
            var row = component?.ReserveHistory.Single(h => h.Id == work.ReserveHistoryId);
            if (row is null || row.IdempotencyKey != work.IdempotencyKey || !row.IsApproved
                || row.PostingStatus == PostingStatus.Posted || (failure is not null && row.PostingStatus == PostingStatus.Failed))
                return false;
            var operation = new ReserveOperationContext(claim, components);
            var changed = failure is null
                ? component!.MarkPosted(row.Id, jobId, operation, clock)
                : component!.MarkPostingFailed(row.Id, jobId, failure, operation, clock);
            if (!changed) return false;

            // Domain methods select workflow values. The database predicate claims the delivery atomically.
            var eligible = db.ReserveHistory.Where(h => h.Id == row.Id && h.IdempotencyKey == work.IdempotencyKey
                && h.PostingStatus != PostingStatus.Posted
                && (h.ApprovalStatus == ReserveApprovalStatus.AutoApproved || h.ApprovalStatus == ReserveApprovalStatus.Approved));
            if (failure is not null) eligible = eligible.Where(h => h.PostingStatus != PostingStatus.Failed);
            var affected = await eligible.ExecuteUpdateAsync(setters => setters
                .SetProperty(h => h.PostingStatus, row.PostingStatus)
                .SetProperty(h => h.PostingJobId, row.PostingJobId)
                .SetProperty(h => h.PostedAt, row.PostedAt)
                .SetProperty(h => h.PostingFailedAt, row.PostingFailedAt)
                .SetProperty(h => h.PostingFailureReason, row.PostingFailureReason)
                .SetProperty(h => EF.Property<DateTimeOffset?>(h, "UpdatedAt"), clock.UtcNow)
                .SetProperty(h => EF.Property<Guid?>(h, "UserModified"), (Guid?)null), token);
            if (affected != 1) throw new InvalidOperationException("Posting ownership changed while the claim lock was held.");
            // Avoid a second tracked UPDATE of the reserve. Claim.Touch remains tracked and commits with the audit.
            db.Entry(row).State = EntityState.Unchanged;
            // Preserve the existing workflow concurrency convention even though the reserve write was bulk SQL.
            db.Entry(component!).Property(c => c.CurrentAmount).IsModified = true;
            var json = failure is null
                ? JsonSerializer.Serialize(new
                {
                    debit = "Change in Outstanding Reserves",
                    credit = "Outstanding Loss Reserves",
                    amount = row.Amount,
                    idempotencyKey = row.IdempotencyKey
                })
                : JsonSerializer.Serialize(new { idempotencyKey = row.IdempotencyKey, jobId, reason = failure });
            new AuditLogService(db, ids).Append(new AuditEntry(claim.Id,
                failure is null ? "GL_POSTING_SIMULATED" : "GL_POSTING_FAILED",
                failure is null ? "DR Change in Outstanding Reserves / CR Outstanding Loss Reserves" : "GL posting retries exhausted",
                NewValue: json, RelatedEntityId: row.Id, RelatedEntityType: "ReserveHistory"));
            return true;
        }, new NoEventsDispatcher(), cancellationToken);
        return result.Value;
    }

    public async Task<IReadOnlyList<GlPostingWork>> FindUnscheduledAsync(CancellationToken cancellationToken = default)
    {
        await using var db = Create(Guid.Empty);
        var cutoff = clock.UtcNow.AddMinutes(-2);
        return await db.ReserveHistory.IgnoreQueryFilters().AsNoTracking().Where(h =>
            (h.ApprovalStatus == ReserveApprovalStatus.AutoApproved || h.ApprovalStatus == ReserveApprovalStatus.Approved)
            && h.PostingStatus == PostingStatus.Pending && h.PostingJobId == null && h.ApprovedAt < cutoff
            && db.Claims.IgnoreQueryFilters().Any(c => c.Id == h.ClaimId && !EF.Property<bool>(c, "IsDeleted")
                && EF.Property<Guid>(c, "OrganisationId") == EF.Property<Guid>(h, "OrganisationId")))
            .Select(h => new GlPostingWork(h.Id, h.ClaimId, h.IdempotencyKey)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SlaWork>> FindSlaCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = Create(Guid.Empty);
        var cutoff = clock.UtcNow.AddHours(-48);
        return await db.Claims.IgnoreQueryFilters().AsNoTracking().Where(c => !EF.Property<bool>(c, "IsDeleted")
            && (c.Status == ClaimStatus.Draft || c.Status == ClaimStatus.Open)
            && (EF.Property<DateTimeOffset?>(c, "UpdatedAt") ?? EF.Property<DateTimeOffset>(c, "CreatedAt")) < cutoff)
            .Select(c => new SlaWork(c.Id, EF.Property<Guid>(c, "OrganisationId"))).ToListAsync(cancellationToken);
    }

    public async Task<bool> FlagSlaAsync(SlaWork work, CancellationToken cancellationToken = default)
    {
        await using var db = Create(work.OrganisationId);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Serialize parallel monitors and business actions before rechecking time/status and the audit window.
            if (await LockedClaim(db, work.ClaimId, cancellationToken) is null) return false;
            var now = clock.UtcNow;
            var cutoff = now.AddHours(-48);
            var dedupe = now.AddHours(-24);
            var affected = await db.Claims.Where(c => c.Id == work.ClaimId
                && (c.Status == ClaimStatus.Draft || c.Status == ClaimStatus.Open)
                && (EF.Property<DateTimeOffset?>(c, "UpdatedAt") ?? EF.Property<DateTimeOffset>(c, "CreatedAt")) < cutoff
                && !db.ClaimAuditLog.Any(a => a.ClaimId == c.Id && a.EventType == "SLA_BREACH_DETECTED"
                    && EF.Property<DateTimeOffset>(a, "CreatedAt") > dedupe))
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.SlaBreachedAt, now), cancellationToken);
            if (affected != 1) return false;
            new AuditLogService(db, ids).Append(new AuditEntry(work.ClaimId, "SLA_BREACH_DETECTED", "Claim has not been updated in 48 hours"));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private static Task<Claim?> LockedClaim(ClaimsDbContext db, Guid claimId, CancellationToken cancellationToken) =>
        db.Claims.FromSqlInterpolated($"SELECT * FROM [Claims] WITH (UPDLOCK, HOLDLOCK) WHERE [ClaimId] = {claimId}")
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record JobUser(Guid OrganisationId) : ICurrentUser
    {
        public Guid? UserId => null;
        public string Name => "Background job";
        public UserRole? Role => null;
        public Guid? CorrelationId { get; } = Guid.NewGuid();
    }

    private sealed class NoEventsDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (events.Count != 0) throw new InvalidOperationException("Posting unexpectedly raised domain events.");
            return Task.CompletedTask;
        }
    }
}
