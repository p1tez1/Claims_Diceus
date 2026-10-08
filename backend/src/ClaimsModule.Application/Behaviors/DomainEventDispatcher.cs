using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Behaviors;

public sealed record DomainEventNotification(IDomainEvent Event) : INotification;

public sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
            await publisher.Publish(new DomainEventNotification(domainEvent), cancellationToken);
    }
}

public sealed class AuditDomainNotificationHandler(IAuditLogService audit) : INotificationHandler<DomainEventNotification>
{
    public Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        audit.Append(ToAuditEntry(notification.Event));
        return Task.CompletedTask;
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static AuditEntry ToAuditEntry(IDomainEvent domainEvent) => domainEvent switch
    {
        ClaimCreated e => new(e.ClaimId, "CLAIM_CREATED", "Claim created", NewValue: Json(new { e.ClaimNumber, e.UserId })),
        ClaimStatusChanged e => new(e.ClaimId, "STATUS_CHANGED", "Claim status changed",
            Json(new { Status = e.From.ToString() }), Json(new { Status = e.To.ToString(), e.Reason })),
        ClaimPartyAdded e => new(e.ClaimId, "PARTY_ADDED", "Party added", NewValue: Json(new { e.PartyId, Role = e.Role.ToString() }),
            RelatedEntityId: e.PartyId, RelatedEntityType: "ClaimParty"),
        ClaimPartyRemoved e => new(e.ClaimId, "PARTY_REMOVED", "Party removed", OldValue: Json(new { e.PartyId, Role = e.Role.ToString() }),
            NewValue: Json(new { IsActive = false }), RelatedEntityId: e.PartyId, RelatedEntityType: "ClaimParty"),
        ClaimClosed e => new(e.ClaimId, "CLAIM_CLOSED", "Claim closed", NewValue: Json(new { Status = "Closed", e.Reason, e.Justification })),
        ClaimReopened e => new(e.ClaimId, "CLAIM_REOPENED", "Claim reopened", OldValue: Json(new { Status = "Closed" }),
            NewValue: Json(new { Status = "Open", e.Reason })),
        ValidationIssueAdded e => new(e.ClaimId, "VALIDATION_ISSUE_ADDED", "Validation issue added",
            NewValue: Json(new { e.Code, Severity = e.Severity.ToString() }), RelatedEntityId: e.IssueId,
            RelatedEntityType: "ClaimValidationIssue"),
        ReserveOverrideSet e => new(e.ClaimId, "CLAIM_RESERVE_OVERRIDE_SET", "Reserve override set",
            NewValue: Json(new { e.Enabled, e.UserId })),
        PolicyLinked e => new(e.ClaimId, "POLICY_LINKED", "Policy linked", NewValue: Json(new { e.PolicyId })),
        LiabilityDetermined e => new(e.ClaimId, "LIABILITY_DETERMINED", "Liability determined",
            OldValue: Json(new { Determination = e.Previous?.ToString() }),
            NewValue: Json(new { Determination = e.Determination.ToString(), e.Notes })),
        ReserveSubmitted e => new(e.ClaimId, "RESERVE_CREATED", "Reserve transaction submitted",
            NewValue: Json(new { e.ComponentId, e.TransactionId, e.Delta }), RelatedEntityId: e.TransactionId,
            RelatedEntityType: "ReserveHistory"),
        ReserveAutoApproved e => new(e.ClaimId, "RESERVE_AUTO_APPROVED", "Reserve auto-approved",
            NewValue: Json(new { ApprovalStatus = "AutoApproved", e.IdempotencyKey }), RelatedEntityId: e.TransactionId, RelatedEntityType: "ReserveHistory"),
        ReserveApproved e => new(e.ClaimId, "RESERVE_APPROVED", "Reserve approved",
            OldValue: Json(new { ApprovalStatus = "PendingApproval" }),
            NewValue: Json(new { ApprovalStatus = "Approved", e.UserId, e.IdempotencyKey }),
            RelatedEntityId: e.TransactionId, RelatedEntityType: "ReserveHistory"),
        ReserveRejected e => new(e.ClaimId, "RESERVE_REJECTED", "Reserve rejected",
            OldValue: Json(new { ApprovalStatus = "PendingApproval", e.Reason }),
            NewValue: Json(new { ApprovalStatus = "Rejected" }), RelatedEntityId: e.TransactionId, RelatedEntityType: "ReserveHistory"),
        ReserveRetracted e => new(e.ClaimId, "RESERVE_RETRACTED", "Reserve retracted",
            OldValue: Json(new { ApprovalStatus = "PendingApproval" }),
            NewValue: Json(new { ApprovalStatus = "Cancelled", e.UserId }),
            RelatedEntityId: e.TransactionId, RelatedEntityType: "ReserveHistory"),
        _ => throw new InvalidOperationException($"No audit mapping for {domainEvent.GetType().Name}.")
    };
}
