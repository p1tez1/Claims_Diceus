using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Events;

public interface IDomainEvent;

public sealed record ClaimCreated(Guid ClaimId, string ClaimNumber, Guid UserId, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ClaimStatusChanged(Guid ClaimId, ClaimStatus From, ClaimStatus To, Guid UserId, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ClaimPartyAdded(Guid ClaimId, Guid PartyId, PartyRole Role, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ClaimPartyRemoved(Guid ClaimId, Guid PartyId, PartyRole Role, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ClaimClosed(Guid ClaimId, Guid UserId, string? Reason, string? Justification, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ClaimReopened(Guid ClaimId, Guid UserId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ValidationIssueAdded(Guid ClaimId, Guid IssueId, string Code, ValidationSeverity Severity, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveOverrideSet(Guid ClaimId, Guid UserId, bool Enabled, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record PolicyLinked(Guid ClaimId, Guid PolicyId, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record LiabilityDetermined(Guid ClaimId, LiabilityDetermination? Previous, LiabilityDetermination Determination,
    string? Notes, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveSubmitted(Guid ClaimId, Guid ComponentId, Guid TransactionId, decimal Delta, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveAutoApproved(Guid ClaimId, Guid ComponentId, Guid TransactionId, string IdempotencyKey, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveApproved(Guid ClaimId, Guid ComponentId, Guid TransactionId, Guid UserId, string IdempotencyKey, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveRejected(Guid ClaimId, Guid ComponentId, Guid TransactionId, Guid UserId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReserveRetracted(Guid ClaimId, Guid ComponentId, Guid TransactionId, Guid UserId, DateTimeOffset OccurredAt) : IDomainEvent;
