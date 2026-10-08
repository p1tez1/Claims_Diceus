using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimValidationIssue
{
    private ClaimValidationIssue() { }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public ValidationSeverity Severity { get; private set; }
    public ValidationIssueStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static ClaimValidationIssue Create(Guid id, Guid claimId, string code, string message,
        ValidationSeverity severity, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new ClaimValidationIssue
        {
            Id = RuleGuard.Id(id, nameof(id)),
            ClaimId = RuleGuard.Id(claimId, nameof(claimId)),
            Code = RuleGuard.Text(code, nameof(code)),
            Message = RuleGuard.Text(message, nameof(message)),
            Severity = RuleGuard.Defined(severity),
            Status = ValidationIssueStatus.Open,
            CreatedAt = clock.UtcNow,
        };
    }

    internal void Acknowledge(DateTimeOffset at)
    {
        if (Severity != ValidationSeverity.Warning || Status != ValidationIssueStatus.Open)
            throw new DomainRuleViolationException("VALIDATION_ISSUE_STATE", "Only an Open warning may be acknowledged.");
        Status = ValidationIssueStatus.Acknowledged;
        AcknowledgedAt = at;
    }

    internal void Resolve(DateTimeOffset at)
    {
        if (Status == ValidationIssueStatus.Resolved)
            throw new DomainRuleViolationException("VALIDATION_ISSUE_STATE", "Validation issue is already resolved.");
        Status = ValidationIssueStatus.Resolved;
        ResolvedAt = at;
    }
}
