using System.Linq.Expressions;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence.Entities;
using ClaimsModule.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Queries;

public sealed class ReadQueries(ClaimsDbContext db) : IClaimQueries, IAuditQueries, IReferenceDataQueries,
    IPolicyQueries, IUserQueries, IDocumentQueries, IReserveQueries
{
    public async Task<PageDto<ClaimListItemDto>> ListAsync(ClaimListRequest request, CancellationToken cancellationToken = default)
    {
        var offset = request.Pagination.Offset;
        var query = db.Claims.AsNoTracking();
        if (request.Statuses is { Count: > 0 }) query = query.Where(x => request.Statuses.Contains(x.Status));
        if (request.DateFrom.HasValue) query = query.Where(x => x.LossEvent.LossDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue) query = query.Where(x => x.LossEvent.LossDate <= request.DateTo.Value);
        if (request.AssignedHandlerId.HasValue) query = query.Where(x => x.AssignedHandlerId == request.AssignedHandlerId);
        if (request.PolicyId.HasValue) query = query.Where(x => x.PolicyId == request.PolicyId);
        if (!string.IsNullOrEmpty(request.CauseOfLossCode)) query = query.Where(x => x.LossEvent.CauseOfLossCode == request.CauseOfLossCode);
        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(x => ((string)(object)x.ClaimNumber).Contains(request.Search)
                || (x.ClientName != null && x.ClientName.Contains(request.Search)));
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => EF.Property<DateTimeOffset>(x, "CreatedAt")).ThenBy(x => x.Id)
            .Skip(offset).Take(request.Pagination.PageSize)
            .Select(x => new ClaimListItemDto(x.Id, x.ClaimNumber.Value, x.ClientName, x.PolicyNumber,
                x.LossEvent.LossDate, x.LossEvent.CauseOfLossCode, x.Status,
                db.ClaimReserveComponents.Where(r => r.ClaimId == x.Id).Sum(r => (decimal?)r.CurrentAmount) ?? 0,
                x.AssignedHandlerId)).ToListAsync(cancellationToken);
        return new(rows, count, request.Pagination.Page, request.Pagination.PageSize);
    }

    public async Task<ClaimDetailDto?> GetAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var result = await db.Claims.AsNoTracking().Where(x => x.Id == claimId).Select(x => new ClaimDetailDto(
            x.Id, x.ClaimNumber.Value, x.PolicyId, x.PolicyNumber, x.ClientName, x.Status, x.Severity, x.ReportedDate,
            x.AssignedHandlerId, x.ClosedAt, x.ClosureReason, x.Notes, x.ManagerOverride, x.SlaBreachedAt,
            EF.Property<LiabilityDetermination?>(x, "LiabilityDetermination") == null ? null
                : EF.Property<LiabilityDetermination?>(x, "LiabilityDetermination").ToString(),
            EF.Property<DateTimeOffset?>(x, "LiabilityDeterminedAt"), EF.Property<DateTimeOffset>(x, "CreatedAt"),
            EF.Property<DateTimeOffset?>(x, "UpdatedAt"), x.RowVersion,
            new LossEventDto(x.LossEvent.Id, x.LossEvent.LossDate, x.LossEvent.LossDescription, x.LossEvent.LossLocation,
                x.LossEvent.CauseOfLossCode, x.LossEvent.EstimatedLossAmount, x.LossEvent.ReportDate, x.LossEvent.PoliceReportNumber),
            new List<PartyDto>(), new List<RiskObjectDto>(), new List<ValidationIssueDto>(), new List<ReserveComponentDto>(),
            new List<DocumentDto>(), new List<AuditLogDto>())).SingleOrDefaultAsync(cancellationToken);
        if (result is null) return null;
        var parties = await db.ClaimParties.AsNoTracking().Where(x => x.ClaimId == claimId).OrderBy(x => x.Id)
            .Select(x => new PartyDto(x.Id, x.Role, x.Type, x.FirstName, x.LastName, x.CompanyName, x.Email, x.Phone, x.Notes, x.IsActive))
            .ToListAsync(cancellationToken);
        var risks = await db.ClaimRiskObjects.AsNoTracking().Where(x => x.ClaimId == claimId).OrderBy(x => x.Id)
            .Select(x => new RiskObjectDto(x.Id, x.AssetType, x.AssetDescription, x.DamageDescription, x.IsPrimary, x.AssetReference))
            .ToListAsync(cancellationToken);
        var issues = await db.ClaimValidationIssues.AsNoTracking().Where(x => x.ClaimId == claimId).OrderBy(x => x.Id)
            .Select(x => new ValidationIssueDto(x.Id, x.Code, x.Message, x.Severity, x.Status,
                EF.Property<DateTimeOffset>(x, "AuditCreatedAt"), x.AcknowledgedAt, x.ResolvedAt)).ToListAsync(cancellationToken);
        return result with
        {
            Parties = parties,
            RiskObjects = risks,
            ValidationIssues = issues,
            Reserves = await ((IReserveQueries)this).ListAsync(claimId, cancellationToken),
            Documents = await ((IDocumentQueries)this).ListAsync(claimId, cancellationToken),
            Audit = await AuditRows(claimId).OrderByDescending(x => EF.Property<DateTimeOffset>(x, "CreatedAt")).ThenByDescending(x => x.Id)
                .Take(20).Select(AuditProjection()).ToListAsync(cancellationToken),
        };
    }

    // DD-36: sequence is presentation order; approved balances reflect approval order.
    async Task<IReadOnlyList<ReserveComponentDto>> IReserveQueries.ListAsync(Guid claimId, CancellationToken cancellationToken) =>
        await db.ClaimReserveComponents.AsNoTracking()
            .Where(x => x.ClaimId == claimId && db.Claims.Any(c => c.Id == x.ClaimId)).OrderBy(x => x.Component)
            .Select(x => new ReserveComponentDto(x.Id, x.Component, x.Status, x.CurrentAmount,
                x.ReserveHistory.Where(h => h.ApprovalStatus == ReserveApprovalStatus.PendingApproval).Sum(h => (decimal?)h.Amount) ?? 0,
                x.RowVersion, x.ReserveHistory.OrderBy(h => h.ChangeSequence).Select(h => new ReserveHistoryDto(
                    h.Id, h.TransactionType, h.Amount, h.PreviousBalance, h.NewBalance, h.ChangeSequence, h.ChangeReason,
                    h.IdempotencyKey, h.ApprovalStatus, h.PostingStatus, h.SubmittedByUserId,
                    EF.Property<DateTimeOffset>(h, "AuditCreatedAt"), h.ApprovedByUserId, h.ApprovedAt,
                    h.RejectedByUserId, h.RejectedAt, h.RejectionReason, h.ResubmissionOfId, h.PostingJobId, x.Component,
                    db.Users.Where(u => u.Id == h.SubmittedByUserId).Select(u => u.Name).FirstOrDefault(),
                    db.Users.Where(u => u.Id == h.ApprovedByUserId).Select(u => u.Name).FirstOrDefault())).ToList()))
            .ToListAsync(cancellationToken);

    public async Task<PageDto<AuditLogDto>> ListAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var offset = page.Offset;
        var query = AuditRows(claimId);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => EF.Property<DateTimeOffset>(x, "CreatedAt")).ThenByDescending(x => x.Id)
            .Skip(offset).Take(page.PageSize).Select(AuditProjection()).ToListAsync(cancellationToken);
        return new(rows, count, page.Page, page.PageSize);
    }

    private IQueryable<ClaimAuditLog> AuditRows(Guid claimId) => db.ClaimAuditLog.AsNoTracking()
        .Where(x => x.ClaimId == claimId && db.Claims.Any(c => c.Id == x.ClaimId));

    private Expression<Func<ClaimAuditLog, AuditLogDto>> AuditProjection() => x => new AuditLogDto(
        x.Id, x.ClaimId, x.EventType, x.Description, x.OldValue, x.NewValue, x.RelatedEntityId,
        x.RelatedEntityType, x.CorrelationId, EF.Property<DateTimeOffset>(x, "CreatedAt"), x.CreatedByUserId,
        db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.Name).FirstOrDefault());

    public async Task<IReadOnlyList<CauseOfLossDto>> GetCausesAsync(string? perilCategory = null,
        CancellationToken cancellationToken = default) => await db.CauseOfLossCodes.AsNoTracking()
        .Where(x => x.IsActive && (perilCategory == null || x.PerilCategory == perilCategory)).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
        .Select(x => new CauseOfLossDto(x.Id, x.Code, x.Name, x.PerilCategory, x.SortOrder)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClaimStatusDto>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        var transitions = await db.ClaimStatusTransitions.AsNoTracking()
            .Select(x => new { x.From, x.To, x.RequiredRole, x.RuleCode }).ToListAsync(cancellationToken);
        return Enum.GetValues<ClaimStatus>().Select(status => new ClaimStatusDto(status, transitions.Where(x => x.From == status)
            .OrderBy(x => x.To).Select(x => new StatusTransitionDto(x.To, x.RequiredRole, x.RuleCode)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<PolicyDto>> SearchAsync(string search, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search.Trim().ToLower();
        return await db.Policies.AsNoTracking()
            .Where(x => x.PolicyNumber.ToLower().Contains(normalizedSearch) || x.ClientName.ToLower().Contains(normalizedSearch))
            .OrderBy(x => x.PolicyNumber).Take(20).Select(PolicyProjection).ToListAsync(cancellationToken);
    }

    async Task<PolicyDto?> IPolicyQueries.GetAsync(Guid policyId, CancellationToken cancellationToken) =>
        await db.Policies.AsNoTracking().Where(x => x.Id == policyId).Select(PolicyProjection).SingleOrDefaultAsync(cancellationToken);

    private static readonly Expression<Func<Policy, PolicyDto>> PolicyProjection = x => new(
        x.Id, x.PolicyNumber, x.ClientName, x.EffectiveDate, x.ExpirationDate, x.Status.ToString(), x.CoverageTypes);

    public async Task<IReadOnlyList<UserDto>> ListAsync(UserRole? role = null, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => role == null || x.Role == role).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new UserDto(x.Id, x.Username, x.Name, x.Role)).ToListAsync(cancellationToken);

    public Task<UserDto?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => EF.Property<Guid>(x, "OrganisationId") == SeedConstants.OrganisationId && x.Username == username)
            .Select(x => new UserDto(x.Id, x.Username, x.Name, x.Role)).SingleOrDefaultAsync(cancellationToken);

    async Task<IReadOnlyList<DocumentDto>> IDocumentQueries.ListAsync(Guid claimId, CancellationToken cancellationToken) =>
        await db.ClaimDocuments.AsNoTracking().Where(x => x.ClaimId == claimId && db.Claims.Any(c => c.Id == x.ClaimId))
            .OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id).Select(x => new DocumentDto(x.Id, x.ClaimId,
                x.DocumentType, x.DocumentName, x.BlobPath, x.ContentType, x.FileSizeBytes, x.UploadedAt, x.UploadedByUserId, x.Notes,
                db.Users.Where(user => user.Id == x.UploadedByUserId).Select(user => user.Name).FirstOrDefault(), null))
            .ToListAsync(cancellationToken);
}
