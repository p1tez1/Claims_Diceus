using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ReserveComponentTests
{
    public static IEnumerable<object[]> SignRules()
    {
        foreach (var component in Enum.GetValues<ReserveComponentType>())
            foreach (var type in Enum.GetValues<ReserveTransactionType>())
                foreach (var amount in new[] { -100m, 0m, 100m })
                {
                    var allowed = amount != 0 && (type == ReserveTransactionType.Adjust
                        || type == ReserveTransactionType.Reverse && amount > 0
                        || type == ReserveTransactionType.Add && (component == ReserveComponentType.SubrogationRecoverable ? amount < 0 : amount > 0));
                    yield return [component, type, amount, allowed];
                }
    }

    [Theory]
    [MemberData(nameof(SignRules))]
    public void BRR01DD04AllComponentTransactionSigns(ReserveComponentType componentType,
        ReserveTransactionType type, decimal amount, bool allowed)
    {
        var fixture = new DomainFixture();
        var component = fixture.Component(componentType);
        if (componentType != ReserveComponentType.SubrogationRecoverable) fixture.Submit(component, 1000m);
        if (allowed)
        {
            var entry = fixture.Submit(component, amount, type);
            entry.Amount.ShouldBe(type == ReserveTransactionType.Reverse ? -amount : amount);
        }
        else
        {
            var count = component.ReserveHistory.Count;
            Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, amount, type)).Code.ShouldBe("BR-R-01");
            component.ReserveHistory.Count.ShouldBe(count);
        }
        AssertLedger(component);
    }

    [Theory]
    [InlineData(ReserveComponentType.Indemnity)]
    [InlineData(ReserveComponentType.Expense)]
    [InlineData(ReserveComponentType.ALAE)]
    public void BRR01AutoApprovalCannotMakeBalanceNegative(ReserveComponentType type)
    {
        var fixture = new DomainFixture();
        var component = fixture.Component(type);
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, -1m, ReserveTransactionType.Adjust))
            .Code.ShouldBe("BR-R-01");
        component.ReserveHistory.ShouldBeEmpty();
        component.CurrentAmount.ShouldBe(0m);
    }

    [Fact]
    public void BRR01ApprovalRechecksBalanceAfterInterveningChanges()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var initial = fixture.Submit(component, 50_000m);
        component.Approve(initial.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        var pendingReduction = fixture.Submit(component, -40_000m, ReserveTransactionType.Adjust);
        var laterReduction = fixture.Submit(component, -20_000m, ReserveTransactionType.Adjust);
        component.Approve(laterReduction.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => component.Approve(pendingReduction.Id, fixture.Reviewer,
            UserRole.Supervisor, fixture.Context, fixture.Clock)).Code.ShouldBe("BR-R-01");
        pendingReduction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        component.CurrentAmount.ShouldBe(30_000m);
        AssertLedger(component);
    }

    [Theory]
    [InlineData(UserRole.Handler)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Manager)]
    public void BRR02AnyRoleMaySubmitAboveItsAuthority(UserRole role)
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 200_000m, role: role);
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        component.CurrentAmount.ShouldBe(0m);
        entry.PreviousBalance.ShouldBeNull();
        entry.NewBalance.ShouldBeNull();
        component.DomainEvents.OfType<ReserveAutoApproved>().ShouldBeEmpty();
    }

    [Fact]
    public void BRR02ThresholdUsesDeltaRatherThanRunningTotal()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        fixture.Submit(component, 10_000m);
        var second = fixture.Submit(component, 10_000m);
        second.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        component.CurrentAmount.ShouldBe(20_000m);
        second.PreviousBalance.ShouldBe(10_000m);
        second.NewBalance.ShouldBe(20_000m);
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Manager)]
    public void BRR03SelfApprovalThrowsDistinctException(UserRole role)
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m, role: role);
        var exception = Should.Throw<SelfApprovalException>(() => component.Approve(entry.Id, fixture.Submitter,
            role, fixture.Context, fixture.Clock));
        exception.Message.ShouldBe("Self-approval is not permitted.");
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        component.CurrentAmount.ShouldBe(0m);
    }

    [Fact]
    public void BRR02InsufficientAuthorityDoesNotChangePendingTransaction()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 100_000.01m);
        Should.Throw<InsufficientAuthorityException>(() => component.Approve(entry.Id, fixture.Reviewer,
            UserRole.Supervisor, fixture.Context, fixture.Clock));
        Should.Throw<InsufficientAuthorityException>(() => component.Reject(entry.Id, fixture.Reviewer,
            UserRole.Supervisor, "Not supported", fixture.Context, fixture.Clock));
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        component.Approve(entry.Id, fixture.Reviewer, UserRole.Manager, fixture.Context, fixture.Clock);
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
        entry.ApprovedByUserId.ShouldBe(fixture.Reviewer);
        entry.ApprovedAt.ShouldBe(fixture.Clock.UtcNow);
    }

    [Fact]
    public void BRR04ResubmissionLinksRetainedRejectedRowAndUsesNormalThreshold()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var original = fixture.Submit(component, 20_000m);
        component.Reject(original.Id, fixture.Reviewer, UserRole.Supervisor, "Estimate unsupported", fixture.Context, fixture.Clock);
        var revised = fixture.Submit(component, 500m, resubmissionOfId: original.Id);
        revised.ResubmissionOfId.ShouldBe(original.Id);
        revised.Id.ShouldNotBe(original.Id);
        revised.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        original.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        original.RejectedByUserId.ShouldBe(fixture.Reviewer);
        original.RejectedAt.ShouldBe(fixture.Clock.UtcNow);
        original.RejectionReason.ShouldBe("Estimate unsupported");
        original.Amount.ShouldBe(20_000m);
        original.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        component.ReserveHistory.Count.ShouldBe(2);
        AssertLedger(component);
    }

    [Fact]
    public void BRR04ResubmissionMustLinkSameComponentRejectedRowAndUseAddOrAdjust()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m);
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, 100m, resubmissionOfId: entry.Id));
        component.Reject(entry.Id, fixture.Reviewer, UserRole.Supervisor, "Revise", fixture.Context, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, 100m, ReserveTransactionType.Reverse, resubmissionOfId: entry.Id));
        var other = fixture.Component(ReserveComponentType.Expense);
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(other, 100m, resubmissionOfId: entry.Id));
    }

    [Fact]
    public void RejectRequiresNonEmptyReasonAndPreservesPendingOnFailure()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m);
        Should.Throw<DomainRuleViolationException>(() => component.Reject(entry.Id, fixture.Reviewer,
            UserRole.Supervisor, " ", fixture.Context, fixture.Clock));
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
    }

    [Fact]
    public void DD07RetractOnlyBySubmitterWhilePendingCancelsPosting()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m);
        Should.Throw<InsufficientAuthorityException>(() => component.Retract(entry.Id, fixture.Reviewer, fixture.Context, fixture.Clock));
        component.Retract(entry.Id, fixture.Submitter, fixture.Context, fixture.Clock);
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
        entry.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        entry.RetractedByUserId.ShouldBe(fixture.Submitter);
        Should.Throw<DomainRuleViolationException>(() => component.Retract(entry.Id, fixture.Submitter, fixture.Context, fixture.Clock));
        Should.Throw<DomainRuleViolationException>(() => component.Approve(entry.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock));
        var newEntry = fixture.Submit(component, 100m);
        newEntry.ChangeSequence.ShouldBe(2);
        AssertLedger(component);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DD07RetractCannotCancelAutoOrManuallyApprovedTransactions(bool manual)
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, manual ? 20_000m : 100m);
        if (manual) component.Approve(entry.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => component.Retract(entry.Id, fixture.Submitter, fixture.Context, fixture.Clock));
        AssertLedger(component);
    }

    [Fact]
    public void BRR05CapForcesSmallDeltaPendingAndBlocksUntilManagerOverride()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var initial = fixture.Submit(component, 10_000_000m);
        component.Approve(initial.Id, fixture.Reviewer, UserRole.Manager, fixture.Context, fixture.Clock);
        var result = component.Submit(Guid.NewGuid(), 0.01m, ReserveTransactionType.Add, "Extra expense",
            fixture.Submitter, UserRole.Handler, fixture.Context, fixture.Clock);
        result.Warnings.Single().Code.ShouldBe("BR-R-05");
        result.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        Should.Throw<DomainRuleViolationException>(() => component.Approve(result.Transaction.Id, fixture.Reviewer,
            UserRole.Manager, fixture.Context, fixture.Clock)).Code.ShouldBe("BR-R-05");
        Should.Throw<InsufficientAuthorityException>(() => fixture.Claim.SetManagerOverride(fixture.Reviewer, UserRole.Supervisor, true, fixture.Clock));
        fixture.Claim.SetManagerOverride(fixture.Reviewer, UserRole.Manager, true, fixture.Clock);
        component.Approve(result.Transaction.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        component.CurrentAmount.ShouldBe(10_000_000.01m);
        AssertLedger(component);
    }

    [Fact]
    public void BRR05CapUsesNetSignedBalancesAcrossAllComponentsAndIgnoresPending()
    {
        var fixture = new DomainFixture();
        var indemnity = fixture.Component();
        var initial = fixture.Submit(indemnity, 10_000_000m);
        indemnity.Approve(initial.Id, fixture.Reviewer, UserRole.Manager, fixture.Context, fixture.Clock);
        var recovery = fixture.Component(ReserveComponentType.SubrogationRecoverable);
        fixture.Submit(recovery, -100m);
        fixture.Submit(recovery, -20_000m); // Pending recovery must not reduce the cap calculation.
        var expense = fixture.Component(ReserveComponentType.Expense);
        fixture.Submit(expense, 100m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        fixture.Submit(expense, 0.01m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        fixture.Components.Sum(row => row.CurrentAmount).ShouldBe(10_000_000m);
    }

    [Fact]
    public void BRR05ApprovalRechecksAggregateCapAfterAnotherComponentIsApproved()
    {
        var fixture = new DomainFixture();
        var indemnity = fixture.Component();
        var large = fixture.Submit(indemnity, 10_000_000m);
        var expense = fixture.Component(ReserveComponentType.Expense);
        var pending = fixture.Submit(expense, 20_000m);
        indemnity.Approve(large.Id, fixture.Reviewer, UserRole.Manager, fixture.Context, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => expense.Approve(pending.Id, fixture.Reviewer,
            UserRole.Supervisor, fixture.Context, fixture.Clock)).Code.ShouldBe("BR-R-05");
    }

    [Fact]
    public void DD07OverrideDoesNotLiftIndividualTenMillionCeiling()
    {
        var fixture = new DomainFixture();
        fixture.Claim.SetManagerOverride(fixture.Reviewer, UserRole.Manager, true, fixture.Clock);
        var component = fixture.Component();
        var entry = fixture.Submit(component, 10_000_000.01m);
        Should.Throw<InsufficientAuthorityException>(() => component.Approve(entry.Id, fixture.Reviewer,
            UserRole.Manager, fixture.Context, fixture.Clock));
        entry.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
    }

    [Fact]
    public void BRR06IdempotencyKeyUsesComponentIdAndSequencesNeverReuseRejectedOrCancelledNumbers()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var first = fixture.Submit(component, 20_000m);
        component.Reject(first.Id, fixture.Reviewer, UserRole.Supervisor, "Revise", fixture.Context, fixture.Clock);
        var second = fixture.Submit(component, 20_000m);
        component.Retract(second.Id, fixture.Submitter, fixture.Context, fixture.Clock);
        var third = fixture.Submit(component, 1m);
        component.ReserveHistory.Select(row => row.ChangeSequence).ShouldBe([1, 2, 3]);
        foreach (var row in component.ReserveHistory)
            row.IdempotencyKey.ShouldBe($"Reserve:{component.Id:D}:Change:{row.ChangeSequence}");
        var other = fixture.Component(ReserveComponentType.Expense);
        fixture.Submit(other, 1m).ChangeSequence.ShouldBe(1);
        third.IdempotencyKey.ShouldNotBe(first.IdempotencyKey);
    }

    [Fact]
    public void DD05DD28ApprovedOnlyLedgerCacheAndApprovalOrderBalances()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var first = fixture.Submit(component, 20_000m);
        var second = fixture.Submit(component, 30_000m);
        var auto = fixture.Submit(component, 1m);
        component.PendingAmount.ShouldBe(50_000m);
        component.Approve(second.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        second.PreviousBalance.ShouldBe(1m);
        second.NewBalance.ShouldBe(30_001m);
        component.Approve(first.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        first.PreviousBalance.ShouldBe(30_001m);
        first.NewBalance.ShouldBe(50_001m);
        auto.PreviousBalance.ShouldBe(0m);
        component.RecomputeBalance().ShouldBe(50_001m);
        AssertLedger(component);
    }

    [Fact]
    public void BRR06PostingIsIdempotentAndPostingStateDoesNotAffectBalance()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 100m);
        var key = entry.IdempotencyKey;
        fixture.Claim.FlagSlaBreach(fixture.Clock);
        fixture.Clock.Advance();
        component.MarkPostingFailed(entry.Id, "job-1", "Retry exhausted", fixture.Context, fixture.Clock).ShouldBeTrue();
        entry.PostingStatus.ShouldBe(PostingStatus.Failed);
        fixture.Claim.LastActivity!.At.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.SlaBreachedAt.ShouldBeNull();
        var revision = fixture.Claim.LastActivity.Revision;
        component.MarkPostingFailed(entry.Id, "job-1", "Duplicate", fixture.Context, fixture.Clock).ShouldBeFalse();
        fixture.Claim.LastActivity.Revision.ShouldBe(revision);
        AssertLedger(component);
        component.RetryPosting(entry.Id, fixture.Context, fixture.Clock);
        entry.IdempotencyKey.ShouldBe(key);
        entry.PostingStatus.ShouldBe(PostingStatus.Pending);
        fixture.Claim.FlagSlaBreach(fixture.Clock);
        fixture.Clock.Advance();
        component.MarkPosted(entry.Id, "job-2", fixture.Context, fixture.Clock).ShouldBeTrue();
        fixture.Claim.SlaBreachedAt.ShouldBeNull();
        fixture.Claim.LastActivity.At.ShouldBe(fixture.Clock.UtcNow);
        revision = fixture.Claim.LastActivity.Revision;
        component.MarkPosted(entry.Id, "job-3", fixture.Context, fixture.Clock).ShouldBeFalse();
        component.MarkPostingFailed(entry.Id, "job-3", "Stale failure", fixture.Context, fixture.Clock).ShouldBeFalse();
        fixture.Claim.LastActivity.Revision.ShouldBe(revision);
        entry.PostingJobId.ShouldBe("job-2");
        entry.IdempotencyKey.ShouldBe(key);
        entry.Amount.ShouldBe(100m);
        AssertLedger(component);
    }

    [Fact]
    public void BRR02PendingAndCancelledTransactionsCannotBePosted()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m);
        Should.Throw<DomainRuleViolationException>(() => component.MarkPosted(entry.Id, "job", fixture.Context, fixture.Clock));
        component.Retract(entry.Id, fixture.Submitter, fixture.Context, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => component.MarkPostingFailed(entry.Id, "job", "Failure", fixture.Context, fixture.Clock));
    }

    [Fact]
    public void ReserveWorkflowEmitsAllRequestedEventsAndCanClearThem()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        fixture.Submit(component, 100m);
        var approved = fixture.Submit(component, 20_000m);
        component.Approve(approved.Id, fixture.Reviewer, UserRole.Supervisor, fixture.Context, fixture.Clock);
        var rejected = fixture.Submit(component, 20_000m);
        component.Reject(rejected.Id, fixture.Reviewer, UserRole.Supervisor, "Revise", fixture.Context, fixture.Clock);
        var retracted = fixture.Submit(component, 20_000m);
        component.Retract(retracted.Id, fixture.Submitter, fixture.Context, fixture.Clock);
        component.DomainEvents.OfType<ReserveSubmitted>().Count().ShouldBe(4);
        component.DomainEvents.OfType<ReserveAutoApproved>().Single().IdempotencyKey.ShouldBe(component.ReserveHistory[0].IdempotencyKey);
        component.DomainEvents.OfType<ReserveApproved>().Single().UserId.ShouldBe(fixture.Reviewer);
        component.DomainEvents.OfType<ReserveRejected>().Single().Reason.ShouldBe("Revise");
        component.DomainEvents.OfType<ReserveRetracted>().Single().TransactionId.ShouldBe(retracted.Id);
        component.ClearDomainEvents();
        component.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void DD28DuplicateComponentAndWrongClaimContextAreRejected()
    {
        var fixture = new DomainFixture();
        var component = fixture.Component();
        Should.Throw<DomainRuleViolationException>(() => fixture.Component());
        var otherClaim = new DomainFixture();
        Should.Throw<DomainRuleViolationException>(() => component.Submit(Guid.NewGuid(), 100m,
            ReserveTransactionType.Add, "Estimate", fixture.Submitter, UserRole.Handler, otherClaim.Context, fixture.Clock));
    }

    [Fact]
    public void BRC06UnknownPolicyBlocksSubmissionUntilLinked()
    {
        var fixture = new DomainFixture(withPolicy: false);
        var component = fixture.Component();
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, 100m)).Code.ShouldBe("BR-C-06");
        fixture.Claim.LinkPolicy(DomainFixture.Policy(fixture.Clock), fixture.Clock);
        fixture.Submit(component, 100m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
    }

    private static void AssertLedger(ClaimReserveComponent component)
        => component.CurrentAmount.ShouldBe(component.ReserveHistory.Where(row => row.IsApproved).Sum(row => row.Amount));
}
