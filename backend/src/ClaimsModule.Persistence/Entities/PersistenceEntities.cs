using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Persistence.Entities;

// Storage/reference records have no domain behavior. All tenant and audit state lives in EF shadow properties.
public sealed class Policy
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }
    public string[] CoverageTypes { get; set; } = [];
}
public enum PolicyStatus { Active, Expired, Cancelled }
public enum IdempotencyStatus { InProgress, Completed }

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public sealed class CauseOfLossCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PerilCategory { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ClaimStatusTransitionRow
{
    public Guid Id { get; set; }
    public ClaimStatus From { get; set; }
    public ClaimStatus To { get; set; }
    public UserRole RequiredRole { get; set; }
    public string RuleCode { get; set; } = string.Empty;
}

public sealed class ClaimDocument
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? Notes { get; set; }
}

public sealed class ClaimAuditLog
{
    public Guid Id { get; internal set; }
    public Guid ClaimId { get; internal set; }
    public string EventType { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public string? OldValue { get; internal set; }
    public string? NewValue { get; internal set; }
    public Guid? RelatedEntityId { get; internal set; }
    public string? RelatedEntityType { get; internal set; }
    public Guid? CorrelationId { get; internal set; }
    public Guid? CreatedByUserId { get; internal set; }
}

public sealed class ClaimNumberSequence
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int LastValue { get; set; }
}

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RouteTemplate { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public IdempotencyStatus Status { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
}
