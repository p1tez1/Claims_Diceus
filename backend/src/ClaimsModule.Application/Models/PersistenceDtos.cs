using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Models;

public sealed record PageRequest(int Page, int PageSize)
{
    public int Offset
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(Page, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 1);
            return checked((Page - 1) * PageSize);
        }
    }
}

public sealed record PageDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record ClaimListRequest(PageRequest Pagination, IReadOnlyList<ClaimStatus>? Statuses = null,
    DateTimeOffset? DateFrom = null, DateTimeOffset? DateTo = null, Guid? AssignedHandlerId = null,
    string? CauseOfLossCode = null, Guid? PolicyId = null, string? Search = null);
public sealed record ClaimListItemDto(Guid Id, string ClaimNumber, string? ClientName, string? PolicyNumber,
    DateTimeOffset LossDate, string CauseOfLossCode, ClaimStatus Status, decimal TotalReserves, Guid? AssignedHandlerId);
public sealed record ClaimDetailDto(Guid Id, string ClaimNumber, Guid? PolicyId, string? PolicyNumber,
    string? ClientName, ClaimStatus Status, ClaimSeverity Severity, DateTimeOffset ReportedDate,
    Guid? AssignedHandlerId, DateTimeOffset? ClosedAt, string? ClosureReason, string? Notes,
    bool ManagerOverride, DateTimeOffset? SlaBreachedAt, string? LiabilityDetermination, DateTimeOffset? LiabilityDeterminedAt,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, byte[] RowVersion, LossEventDto LossEvent,
    IReadOnlyList<PartyDto> Parties, IReadOnlyList<RiskObjectDto> RiskObjects, IReadOnlyList<ValidationIssueDto> ValidationIssues,
    IReadOnlyList<ReserveComponentDto> Reserves, IReadOnlyList<DocumentDto> Documents, IReadOnlyList<AuditLogDto> Audit)
{
    public IReadOnlyList<ValidationIssueDto> Warnings => ValidationIssues.Where(x => x.Severity == ValidationSeverity.Warning
        && x.Status != ValidationIssueStatus.Resolved).ToArray();
    public decimal TotalReserves => Reserves.Sum(x => x.CurrentAmount);
    public decimal PendingAmount => Reserves.Sum(x => x.PendingAmount);
}
public sealed record LossEventDto(Guid Id, DateTimeOffset LossDate, string LossDescription, string? LossLocation,
    string CauseOfLossCode, decimal? EstimatedLossAmount, DateTimeOffset ReportDate, string? PoliceReportNumber);
public sealed record PartyDto(Guid Id, PartyRole Role, PartyType Type, string? FirstName, string? LastName,
    string? CompanyName, string? Email, string? Phone, string? Notes, bool IsActive);
public sealed record RiskObjectDto(Guid Id, AssetType AssetType, string AssetDescription, string? DamageDescription,
    bool IsPrimary, string? AssetReference);
public sealed record ValidationIssueDto(Guid Id, string Code, string Message, ValidationSeverity Severity,
    ValidationIssueStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ResolvedAt);
public sealed record ReserveComponentDto(Guid Id, ReserveComponentType Component, ReserveComponentStatus Status,
    decimal CurrentAmount, decimal PendingAmount, byte[] RowVersion, IReadOnlyList<ReserveHistoryDto> History);
public sealed record ReserveHistoryDto(Guid Id, ReserveTransactionType TransactionType, decimal Amount,
    decimal? PreviousBalance, decimal? NewBalance, int ChangeSequence, string ChangeReason, string IdempotencyKey,
    ReserveApprovalStatus ApprovalStatus, PostingStatus PostingStatus, Guid SubmittedByUserId, DateTimeOffset CreatedAt,
    Guid? ApprovedByUserId, DateTimeOffset? ApprovedAt, Guid? RejectedByUserId, DateTimeOffset? RejectedAt,
    string? RejectionReason, Guid? ResubmissionOfId, string? PostingJobId,
    ReserveComponentType? Component = null, string? SubmittedByName = null, string? ApprovedByName = null);
public sealed record AuditEntry(Guid ClaimId, string EventType, string Description, string? OldValue = null,
    string? NewValue = null, Guid? RelatedEntityId = null, string? RelatedEntityType = null);
public sealed record AuditLogDto(Guid Id, Guid ClaimId, string EventType, string Description, string? OldValue,
    string? NewValue, Guid? RelatedEntityId, string? RelatedEntityType, Guid? CorrelationId,
    DateTimeOffset CreatedAt, Guid? CreatedByUserId, string? ActorName);
public sealed record CauseOfLossDto(Guid Id, string Code, string Name, string PerilCategory, int SortOrder);
public sealed record StatusTransitionDto(ClaimStatus To, UserRole RequiredRole, string RuleCode);
public sealed record ClaimStatusDto(ClaimStatus Status, IReadOnlyList<StatusTransitionDto> Transitions);
public sealed record PolicyDto(Guid Id, string PolicyNumber, string ClientName, DateOnly EffectiveDate,
    DateOnly ExpirationDate, string Status, IReadOnlyList<string> CoverageTypes);
public sealed record UserDto(Guid Id, string Username, string Name, UserRole Role);
// Storage implementations add SAS/download URLs; persistence returns metadata only.
public sealed record DocumentDto(Guid Id, Guid ClaimId, string DocumentType, string DocumentName,
    string BlobPath, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt, Guid? UploadedByUserId, string? Notes,
    string? UploaderName = null, string? DownloadUrl = null);
public sealed record DocumentRecord(Guid Id, Guid ClaimId, string DocumentType, string DocumentName,
    string BlobPath, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt, Guid? UploadedByUserId, string? Notes);
public sealed record StoredFile(string Path, string FileName);
