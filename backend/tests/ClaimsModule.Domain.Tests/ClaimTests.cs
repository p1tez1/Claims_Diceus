using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Domain.ValueObjects;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ClaimTests
{
    [Fact]
    public void CreateSetsDraftDefaultsAndEmitsClaimCreated()
    {
        var fixture = new DomainFixture();
        fixture.Claim.Status.ShouldBe(ClaimStatus.Draft);
        fixture.Claim.Severity.ShouldBe(ClaimSeverity.Standard);
        fixture.Claim.AssignedHandlerId.ShouldBe(fixture.Submitter);
        fixture.Claim.ReportedDate.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.DomainEvents.OfType<ClaimCreated>().Single().ClaimNumber.ShouldBe("CLM-2026-0000001");
        fixture.Claim.RowVersion.ShouldBeEmpty();
        fixture.Claim.DomainEvents.OfType<ClaimPartyAdded>().ShouldHaveSingleItem().PartyId
            .ShouldBe(fixture.Claim.Parties.Single().Id);
    }

    [Fact]
    public void DD10ClaimantIsMandatoryAtCreation()
    {
        var clock = new TestClock();
        var id = Guid.NewGuid();
        var loss = LossEvent.Create(Guid.NewGuid(), id, clock.UtcNow, "The insured vehicle was damaged.", "COL-VEH-COL", true, clock);
        Should.Throw<DomainRuleViolationException>(() => Claim.Create(id, ClaimNumber.Create(2026, 1), Guid.NewGuid(),
            loss, [DomainFixture.Person(id, PartyRole.Witness)], clock)).Code.ShouldBe("BR-C-03");
    }

    [Fact]
    public void DD10CriticalIssueRejectsCreation()
    {
        var clock = new TestClock();
        var id = Guid.NewGuid();
        var loss = LossEvent.Create(Guid.NewGuid(), id, clock.UtcNow, "The insured vehicle was damaged.", "COL-VEH-COL", true, clock);
        var issue = ClaimValidationIssue.Create(Guid.NewGuid(), id, "CRITICAL", "Critical validation issue", ValidationSeverity.Critical, clock);
        Should.Throw<DomainRuleViolationException>(() => Claim.Create(id, ClaimNumber.Create(2026, 1), Guid.NewGuid(),
            loss, [DomainFixture.Person(id, PartyRole.Claimant)], clock, validationIssues: [issue])).Code.ShouldBe("DD-10");
    }

    [Fact]
    public void DD10WarningsPersistAndOpenAcknowledgesThem()
    {
        var fixture = new DomainFixture(withPolicy: false, withRiskObject: false);
        fixture.Claim.ValidationIssues.Select(row => row.Code).ShouldBe(["BR-C-06", "NO_RISK_OBJECTS"]);
        fixture.Claim.ValidationIssues.ShouldAllBe(row => row.Status == ValidationIssueStatus.Open);
        fixture.Claim.DomainEvents.OfType<ValidationIssueAdded>().Count().ShouldBe(2);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Open,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components));
        fixture.Claim.Status.ShouldBe(ClaimStatus.Draft);
        fixture.Open();
        fixture.Claim.ValidationIssues.ShouldAllBe(row => row.Status == ValidationIssueStatus.Acknowledged);
        fixture.Claim.ValidationIssues.ShouldAllBe(row => row.AcknowledgedAt == fixture.Clock.UtcNow);
    }

    [Fact]
    public void BRST02LaterCriticalIssuesMustBeResolvedAndCannotBeAcknowledged()
    {
        var fixture = new DomainFixture();
        var issue = ClaimValidationIssue.Create(Guid.NewGuid(), fixture.Claim.Id, "CRITICAL", "Investigation discrepancy",
            ValidationSeverity.Critical, fixture.Clock);
        fixture.Claim.AddValidationIssue(issue, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.AcknowledgeValidationIssue(issue.Id, fixture.Clock));
        Should.Throw<DomainRuleViolationException>(() => fixture.Open()).Code.ShouldBe("BR-ST-02");
        fixture.Claim.ResolveValidationIssue(issue.Id, fixture.Clock);
        issue.Status.ShouldBe(ValidationIssueStatus.Resolved);
        issue.ResolvedAt.ShouldBe(fixture.Clock.UtcNow);
        fixture.Open();
        fixture.Claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void BRP01LastActiveClaimantCannotBeRemovedEvenInDraft()
    {
        var fixture = new DomainFixture();
        fixture.Claim.ClearDomainEvents();
        var first = fixture.Claim.Parties.Single();
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.RemoveParty(first.Id, fixture.Clock)).Code.ShouldBe("BR-P-01");
        var second = DomainFixture.Person(fixture.Claim.Id, PartyRole.Claimant);
        fixture.Claim.AddParty(second, fixture.Clock);
        fixture.Claim.RemoveParty(first.Id, fixture.Clock);
        first.IsActive.ShouldBeFalse();
        fixture.Claim.Parties.Count.ShouldBe(2); // Soft removal preserves history.
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.RemoveParty(second.Id, fixture.Clock));
        fixture.Claim.DomainEvents.OfType<ClaimPartyAdded>().Single().PartyId.ShouldBe(second.Id);
        fixture.Claim.DomainEvents.OfType<ClaimPartyRemoved>().Single().PartyId.ShouldBe(first.Id);
    }

    [Theory]
    [InlineData(PartyRole.Claimant)]
    [InlineData(PartyRole.Insured)]
    [InlineData(PartyRole.ThirdParty)]
    [InlineData(PartyRole.Witness)]
    [InlineData(PartyRole.Attorney)]
    public void BRP02AllRolesAndRepeatedRolesAreSupported(PartyRole role)
    {
        var fixture = new DomainFixture();
        fixture.Claim.AddParty(DomainFixture.Person(fixture.Claim.Id, role), fixture.Clock);
        fixture.Claim.AddParty(DomainFixture.Person(fixture.Claim.Id, role), fixture.Clock);
        fixture.Claim.Parties.Count(row => row.Role == role).ShouldBe(role == PartyRole.Claimant ? 3 : 2);
    }

    [Fact]
    public void BRC02LinkPolicyReevaluatesPeriodWarningsAndResolvesUnknownPolicy()
    {
        var fixture = new DomainFixture(withPolicy: false);
        var expired = DomainFixture.Policy(fixture.Clock) with { ExpirationDate = fixture.Clock.UtcNow.AddYears(-1) };
        fixture.Claim.LinkPolicy(expired, fixture.Clock);
        fixture.Claim.ValidationIssues.Single(row => row.Code == "BR-C-06").Status.ShouldBe(ValidationIssueStatus.Resolved);
        fixture.Claim.ValidationIssues.Single(row => row.Code == "BR-C-02").Status.ShouldBe(ValidationIssueStatus.Open);
        fixture.Claim.PolicyId.ShouldBe(expired.Id);
        fixture.Claim.PolicyNumber.ShouldBe(expired.PolicyNumber);
        fixture.Claim.ClientName.ShouldBe(expired.ClientName);
        var current = expired with { ExpirationDate = fixture.Clock.UtcNow.AddYears(1) };
        fixture.Claim.LinkPolicy(current, fixture.Clock);
        fixture.Claim.ValidationIssues.Single(row => row.Code == "BR-C-02").Status.ShouldBe(ValidationIssueStatus.Resolved);
        fixture.Claim.DomainEvents.OfType<PolicyLinked>().Count().ShouldBe(2);
    }

    [Fact]
    public void AddRiskObjectResolvesMissingRiskWarning()
    {
        var fixture = new DomainFixture(withRiskObject: false);
        fixture.Claim.AddRiskObject(ClaimRiskObject.Create(Guid.NewGuid(), fixture.Claim.Id, AssetType.Property, "Building"), fixture.Clock);
        fixture.Claim.RiskObjects.Count.ShouldBe(1);
        fixture.Claim.ValidationIssues.Single(row => row.Code == "NO_RISK_OBJECTS").Status.ShouldBe(ValidationIssueStatus.Resolved);
    }

    [Fact]
    public void BRST01InvalidTransitionDoesNotMutateClaimOrEmitEvents()
    {
        var fixture = new DomainFixture();
        fixture.Claim.ClearDomainEvents();
        var activity = fixture.Claim.LastActivity;
        var exception = Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Closed,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components));
        exception.Code.ShouldBe("BR-ST-01");
        exception.Message.ShouldContain("Valid next statuses: Open");
        fixture.Claim.Status.ShouldBe(ClaimStatus.Draft);
        fixture.Claim.LastActivity.ShouldBe(activity);
        fixture.Claim.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void BRST03ClosureBlocksPendingApprovalUntilItIsRetracted()
    {
        var fixture = new DomainFixture();
        fixture.Open();
        var component = fixture.Component();
        var entry = fixture.Submit(component, 20_000m);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Closed,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components)).Code.ShouldBe("CC-01");
        component.Retract(entry.Id, fixture.Submitter, fixture.Context, fixture.Clock);
        fixture.Claim.TransitionTo(ClaimStatus.Closed, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components);
        fixture.Claim.Status.ShouldBe(ClaimStatus.Closed);
    }

    [Fact]
    public void CC02ClaimClosureRemainsBlockedUntilLaterCriticalIssueIsResolved()
    {
        var fixture = new DomainFixture();
        fixture.Open();
        var issue = ClaimValidationIssue.Create(Guid.NewGuid(), fixture.Claim.Id, "CRITICAL", "Unresolved validation",
            ValidationSeverity.Critical, fixture.Clock);
        fixture.Claim.AddValidationIssue(issue, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Closed,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components)).Code.ShouldBe("CC-02");
        fixture.Claim.ResolveValidationIssue(issue.Id, fixture.Clock);
        fixture.Claim.TransitionTo(ClaimStatus.Closed, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components);
        fixture.Claim.Status.ShouldBe(ClaimStatus.Closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CC04ClosureRequiresJustificationForPositiveOrNegativeBalance(bool recovery)
    {
        var fixture = new DomainFixture();
        fixture.Open();
        var component = fixture.Component(recovery ? ReserveComponentType.SubrogationRecoverable : ReserveComponentType.Indemnity);
        fixture.Submit(component, recovery ? -100m : 100m);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Closed,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components)).Code.ShouldBe("CC-04");
        fixture.Claim.TransitionTo(ClaimStatus.Closed, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components,
            new(Reason: "Settled", ClosureJustification: "Outstanding amount confirmed"));
        fixture.Claim.ClosedAt.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.ClosureReason.ShouldBe("Settled");
        fixture.Claim.DomainEvents.OfType<ClaimClosed>().Single().Justification.ShouldBe("Outstanding amount confirmed");
    }

    [Fact]
    public void PendingPaymentAcceptsAutoApprovedReserveAndInvestigationRequiresAcceptedLiability()
    {
        var fixture = new DomainFixture();
        fixture.Open();
        var component = fixture.Component();
        fixture.Submit(component, 100m);
        fixture.Claim.TransitionTo(ClaimStatus.UnderInvestigation, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.PendingPayment,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components));
        fixture.Claim.DetermineLiability(LiabilityDetermination.Denied, null, fixture.Clock);
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.PendingPayment,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components));
        fixture.Claim.DetermineLiability(LiabilityDetermination.Accepted, null, fixture.Clock);
        fixture.Claim.TransitionTo(ClaimStatus.PendingPayment, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components);
        fixture.Claim.Status.ShouldBe(ClaimStatus.PendingPayment);
    }

    [Fact]
    public void BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened()
    {
        var fixture = ClosedClaim();
        fixture.Claim.ClearDomainEvents();
        Should.Throw<InsufficientAuthorityException>(() => fixture.Claim.Reopen(fixture.Reviewer, UserRole.Handler,
            "New evidence", fixture.Clock, fixture.Components));
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.Reopen(fixture.Reviewer, UserRole.Supervisor,
            " ", fixture.Clock, fixture.Components));
        fixture.Claim.Status.ShouldBe(ClaimStatus.Closed);
        fixture.Claim.DomainEvents.ShouldBeEmpty();
        fixture.Claim.TransitionTo(ClaimStatus.Reopened, fixture.Reviewer, UserRole.Supervisor, fixture.Clock, fixture.Components,
            new(Reason: "New evidence"));
        fixture.Claim.Status.ShouldBe(ClaimStatus.Open);
        fixture.Claim.ClosedAt.ShouldBeNull();
        fixture.Claim.ClosureReason.ShouldBeNull();
        var events = fixture.Claim.DomainEvents.OfType<ClaimStatusChanged>().ToArray();
        events.Length.ShouldBe(2);
        (events[0].From, events[0].To).ShouldBe((ClaimStatus.Closed, ClaimStatus.Reopened));
        (events[1].From, events[1].To).ShouldBe((ClaimStatus.Reopened, ClaimStatus.Open));
        fixture.Claim.DomainEvents.OfType<ClaimReopened>().Single().Reason.ShouldBe("New evidence");
    }

    [Fact]
    public void BRST04ReopenValidatesOpenLegBeforeAnyMutation()
    {
        var fixture = ClosedClaim();
        var machine = new ClaimStateMachine(ClaimStateMachine.DefaultTransitions
            .Where(edge => edge.From != ClaimStatus.Reopened));
        fixture.Claim.ClearDomainEvents();
        var preflight = fixture.Claim.EvaluateTransition(ClaimStatus.Reopened, UserRole.Supervisor, fixture.Components,
            new(Reason: "New evidence"), machine);
        preflight.IsAllowed.ShouldBeFalse();
        preflight.BlockingConditions.Single().Code.ShouldBe("BR-ST-01");
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.Reopen(fixture.Reviewer, UserRole.Supervisor,
            "New evidence", fixture.Clock, fixture.Components, stateMachine: machine));
        fixture.Claim.Status.ShouldBe(ClaimStatus.Closed);
        fixture.Claim.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ReopenWithLaterCriticalIssueIsBlockedAtomicallyUntilResolution()
    {
        var fixture = ClosedClaim();
        var issue = ClaimValidationIssue.Create(Guid.NewGuid(), fixture.Claim.Id, "LATER_CRITICAL",
            "New evidence requires review", ValidationSeverity.Critical, fixture.Clock);
        fixture.Claim.AddValidationIssue(issue, fixture.Clock);
        fixture.Claim.ClearDomainEvents();
        var before = fixture.Claim.LastActivity;
        var closedAt = fixture.Claim.ClosedAt;
        fixture.Claim.EvaluateTransition(ClaimStatus.Reopened, UserRole.Supervisor, fixture.Components,
            new(Reason: "New evidence")).BlockingConditions.ShouldContain(x => x.Code == "BR-ST-02");
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.Reopen(fixture.Reviewer,
            UserRole.Supervisor, "New evidence", fixture.Clock, fixture.Components));
        fixture.Claim.Status.ShouldBe(ClaimStatus.Closed);
        fixture.Claim.ClosedAt.ShouldBe(closedAt);
        fixture.Claim.LastActivity.ShouldBe(before);
        fixture.Claim.DomainEvents.ShouldBeEmpty();
        fixture.Claim.ResolveValidationIssue(issue.Id, fixture.Clock);
        fixture.Claim.Reopen(fixture.Reviewer, UserRole.Supervisor, "New evidence", fixture.Clock, fixture.Components);
        fixture.Claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void WithdrawnIsTerminalAndRequiresReason()
    {
        var fixture = new DomainFixture();
        fixture.Open();
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.TransitionTo(ClaimStatus.Withdrawn,
            fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components));
        fixture.Claim.TransitionTo(ClaimStatus.Withdrawn, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components,
            new(Reason: "Claimant withdrew"));
        foreach (var target in Enum.GetValues<ClaimStatus>())
            fixture.Claim.EvaluateTransition(target, UserRole.Manager, fixture.Components).IsAllowed.ShouldBeFalse();
    }

    [Fact]
    public void DD18TouchAndBusinessActionsUseClockAndClearSlaWithoutChangingStatus()
    {
        var fixture = new DomainFixture();
        var activity = fixture.Claim.LastActivity!;
        fixture.Clock.Advance();
        fixture.Claim.FlagSlaBreach(fixture.Clock);
        fixture.Claim.SlaBreachedAt.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.LastActivity.ShouldBe(activity);
        fixture.Claim.Status.ShouldBe(ClaimStatus.Draft);
        fixture.Clock.Advance();
        fixture.Claim.UpdateNotes("Handler review", fixture.Clock);
        fixture.Claim.Notes.ShouldBe("Handler review");
        fixture.Claim.LastActivity!.At.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.LastActivity.Revision.ShouldBe(activity.Revision + 1);
        fixture.Claim.SlaBreachedAt.ShouldBeNull();
        fixture.Claim.Touch(fixture.Clock);
        fixture.Claim.LastActivity.Revision.ShouldBe(activity.Revision + 2);
    }

    [Fact]
    public void ManagerOverrideEmitsEventAndAssignmentRequiresSupervisor()
    {
        var fixture = new DomainFixture();
        Should.Throw<InsufficientAuthorityException>(() => fixture.Claim.SetManagerOverride(fixture.Submitter,
            UserRole.Handler, true, fixture.Clock));
        fixture.Claim.SetManagerOverride(fixture.Reviewer, UserRole.Manager, true, fixture.Clock);
        fixture.Claim.ManagerOverride.ShouldBeTrue();
        fixture.Claim.DomainEvents.OfType<ReserveOverrideSet>().Single().Enabled.ShouldBeTrue();
        Should.Throw<InsufficientAuthorityException>(() => fixture.Claim.AssignHandler(fixture.Reviewer, UserRole.Handler, fixture.Clock));
        fixture.Claim.AssignHandler(fixture.Reviewer, UserRole.Supervisor, fixture.Clock);
        fixture.Claim.AssignedHandlerId.ShouldBe(fixture.Reviewer);
    }

    [Fact]
    public void CollectionsCannotBeMutatedAndChildrenCannotCrossClaims()
    {
        var fixture = new DomainFixture();
        var other = new DomainFixture();
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.AddParty(other.Claim.Parties[0], fixture.Clock));
        Should.Throw<NotSupportedException>(() => ((IList<ClaimParty>)fixture.Claim.Parties).Clear());
        fixture.Claim.ClearDomainEvents();
        fixture.Claim.DomainEvents.ShouldBeEmpty();
    }

    private static DomainFixture ClosedClaim()
    {
        var fixture = new DomainFixture();
        fixture.Open();
        fixture.Claim.TransitionTo(ClaimStatus.Closed, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components);
        return fixture;
    }
}
