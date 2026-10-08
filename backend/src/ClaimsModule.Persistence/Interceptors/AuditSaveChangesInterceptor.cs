using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Configurations;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence.Interceptors;

internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    internal static readonly AuditSaveChangesInterceptor Instance = new();
    private static readonly string[] ImmutableFinancialFields = ["ReserveComponentId", "ClaimId", "TransactionType", "Amount",
        "ChangeSequence", "IdempotencyKey", "SubmittedByUserId", "ChangeReason", "ResubmissionOfId", Mapping.DomainCreatedAt];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Prepare((ClaimsDbContext)eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Prepare((ClaimsDbContext)eventData.Context!);
        return ValueTask.FromResult(result);
    }

    private static void Prepare(ClaimsDbContext db)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An explicit caller transaction is required before SaveChanges.");
        if (db.TenantId == Guid.Empty) throw new InvalidOperationException("A tenant is required for writes.");
        db.ChangeTracker.DetectChanges();
        var now = db.Clock.UtcNow.ToUniversalTime();

        foreach (var entry in db.ChangeTracker.Entries<Claim>())
        {
            db.ActivitySnapshots.TryGetValue(entry.Entity, out var original);
            if (entry.State != EntityState.Added && !Equals(entry.Entity.LastActivity, original))
            {
                entry.Property<DateTimeOffset?>("UpdatedAt").CurrentValue = entry.Entity.LastActivity!.At.ToUniversalTime();
                entry.Property("UpdatedAt").IsModified = true; // Also protects cross-component reserve cap decisions.
            }
        }

        // Workflow-only edits and pending submissions must also advance the component RowVer (DD-35).
        var changedComponents = db.ChangeTracker.Entries<ReserveHistory>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified).Select(e => e.Entity.ReserveComponentId).ToHashSet();
        foreach (var entry in db.ChangeTracker.Entries<ClaimReserveComponent>())
            if (entry.State != EntityState.Added && changedComponents.Contains(entry.Entity.Id))
                entry.Property(x => x.CurrentAmount).IsModified = true;

        foreach (var entry in db.ChangeTracker.Entries().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is ClaimAuditLog audit)
            {
                if (entry.State != EntityState.Added)
                    throw new InvalidOperationException("ClaimAuditLog is append-only; updates and deletes are forbidden.");
                if (!db.AuthorizedAuditEntries.Contains(audit))
                    throw new InvalidOperationException("ClaimAuditLog entries must be appended through IAuditLogService.");
            }
            if (entry.Entity is ReserveHistory && (entry.State == EntityState.Deleted
                || ImmutableFinancialFields.Any(name => entry.State == EntityState.Modified && entry.Property(name).IsModified)))
                throw new InvalidOperationException("ReserveHistory financial fields cannot be changed or deleted.");

            var tenant = entry.Property("OrganisationId");
            if (entry.State != EntityState.Added && (!Equals(tenant.OriginalValue, db.TenantId) || tenant.IsModified))
                throw new InvalidOperationException("Cross-tenant writes and tenant changes are forbidden.");
            if (entry.State == EntityState.Added && !Equals(tenant.CurrentValue, db.TenantId))
                throw new InvalidOperationException("Cross-tenant writes are forbidden.");

            if (entry.State == EntityState.Deleted && entry.Metadata.FindProperty("IsDeleted") is not null)
            {
                entry.State = EntityState.Unchanged;
                entry.Property("IsDeleted").CurrentValue = true;
                entry.Property("DeletedAt").CurrentValue = now;
            }
            if (entry.State == EntityState.Deleted) continue; // DD-32 permits deletion of failed idempotency records.
            var created = entry.Metadata.FindProperty(Mapping.DomainCreatedAt) is null ? "CreatedAt" : Mapping.DomainCreatedAt;
            if (entry.State == EntityState.Added)
            {
                var at = entry.Entity switch
                {
                    ReserveHistory history => history.CreatedAt,
                    ClaimValidationIssue issue => issue.CreatedAt,
                    _ => now,
                };
                entry.Property(created).CurrentValue = at.ToUniversalTime();
                entry.Property("UserCreated").CurrentValue = db.CurrentUser.UserId;
                tenant.CurrentValue = db.TenantId;
            }
            else
            {
                if (entry.Property(created).IsModified || entry.Property("UserCreated").IsModified)
                    throw new InvalidOperationException("Creation audit columns cannot be modified.");
                // DD-34's SLA job uses ExecuteUpdate; ordinary Modified claims are business activity.
                var activityAt = entry.Entity is Claim changedClaim
                    && !Equals(changedClaim.LastActivity, db.ActivitySnapshots.GetValueOrDefault(changedClaim))
                    ? changedClaim.LastActivity?.At.ToUniversalTime() : null;
                entry.Property("UpdatedAt").CurrentValue = activityAt ?? now;
                entry.Property("UserModified").CurrentValue = db.CurrentUser.UserId;
            }
            foreach (var property in entry.Properties)
                if (property.CurrentValue is DateTimeOffset timestamp && (entry.State == EntityState.Added || property.IsModified))
                    property.CurrentValue = timestamp.ToUniversalTime();
        }
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Snapshot((ClaimsDbContext)eventData.Context!);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Snapshot((ClaimsDbContext)eventData.Context!);
        return ValueTask.FromResult(result);
    }

    private static void Snapshot(ClaimsDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<Claim>()) db.ActivitySnapshots[entry.Entity] = entry.Entity.LastActivity;
        db.AuthorizedAuditEntries.Clear();
    }
}
