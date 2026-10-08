using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Behaviors;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests;

public sealed class DomainEventAuditTests
{
    [Fact]
    public async Task EveryDomainEventHasAnAuditMappingWithJsonValues()
    {
        var audit = Substitute.For<IAuditLogService>();
        var entries = new List<AuditEntry>();
        audit.When(x => x.Append(Arg.Any<AuditEntry>())).Do(call => entries.Add(call.Arg<AuditEntry>()));
        var handler = new AuditDomainNotificationHandler(audit);
        var claim = Guid.NewGuid();
        var related = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        IDomainEvent[] events =
        [
            new ClaimCreated(claim, "CLM-1", actor, now),
            new ClaimStatusChanged(claim, ClaimStatus.Draft, ClaimStatus.Open, actor, "ready", now),
            new ClaimPartyAdded(claim, related, PartyRole.Claimant, now),
            new ClaimPartyRemoved(claim, related, PartyRole.Claimant, now),
            new ClaimClosed(claim, actor, "done", "paid", now),
            new ClaimReopened(claim, actor, "new evidence", now),
            new ValidationIssueAdded(claim, related, "WARN", ValidationSeverity.Warning, now),
            new ReserveOverrideSet(claim, actor, true, now),
            new PolicyLinked(claim, related, now),
            new ReserveSubmitted(claim, related, related, 10m, now),
            new ReserveAutoApproved(claim, related, related, "key", now),
            new ReserveApproved(claim, related, related, actor, "key", now),
            new ReserveRejected(claim, related, related, actor, "reason", now),
            new ReserveRetracted(claim, related, related, actor, now)
        ];
        foreach (var domainEvent in events)
            await handler.Handle(new DomainEventNotification(domainEvent), default);

        entries.Select(x => x.EventType).ShouldBe([
            "CLAIM_CREATED", "STATUS_CHANGED", "PARTY_ADDED", "PARTY_REMOVED", "CLAIM_CLOSED",
            "CLAIM_REOPENED", "VALIDATION_ISSUE_ADDED", "CLAIM_RESERVE_OVERRIDE_SET", "POLICY_LINKED",
            "RESERVE_CREATED", "RESERVE_AUTO_APPROVED", "RESERVE_APPROVED", "RESERVE_REJECTED", "RESERVE_RETRACTED"]);
        entries.All(x => x.ClaimId == claim && (x.OldValue is not null || x.NewValue is not null)).ShouldBeTrue();
        entries.Single(x => x.EventType == "STATUS_CHANGED").OldValue!.ShouldContain("Draft");
        entries.Single(x => x.EventType == "RESERVE_REJECTED").NewValue!.ShouldContain("Rejected");
    }
}
