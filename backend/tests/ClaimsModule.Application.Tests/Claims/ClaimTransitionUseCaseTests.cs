using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Domain.ValueObjects;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests.Claims;

public sealed class ClaimTransitionUseCaseTests
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly Clock _clock = new();
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IReserveComponentRepository _reserves = Substitute.For<IReserveComponentRepository>();
    private readonly IReferenceDataQueries _reference = Substitute.For<IReferenceDataQueries>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unit = Substitute.For<IUnitOfWork>();
    private readonly List<ClaimReserveComponent> _components = [];
    private readonly Claim _claim;
    private readonly Guid _actor = Guid.NewGuid();

    public ClaimTransitionUseCaseTests()
    {
        var id = Guid.NewGuid();
        var loss = LossEvent.Create(Guid.NewGuid(), id, _clock.UtcNow.AddDays(-1),
            "Collision damaged the insured vehicle.", "COL-VEH-COL", true, _clock);
        var claimant = ClaimParty.Create(Guid.NewGuid(), id, PartyRole.Claimant, PartyType.Person, "Ada", "Lovelace");
        var risk = ClaimRiskObject.Create(Guid.NewGuid(), id, AssetType.Vehicle, "Car");
        var policy = new PolicyAssociation(Guid.NewGuid(), "POL-1", "Client", _clock.UtcNow.AddYears(-1), _clock.UtcNow.AddYears(1));
        _claim = Claim.Create(id, ClaimNumber.Create(2026, 1), _actor, loss, [claimant], _clock, policy, [risk]);
        _claims.GetAsync(id, Arg.Any<CancellationToken>()).Returns(_claim);
        _reserves.GetForClaimAsync(id, Arg.Any<CancellationToken>()).Returns(_ => _components);
        _user.UserId.Returns(_actor);
        _user.Role.Returns(UserRole.Handler);
        _reference.GetStatusesAsync(Arg.Any<CancellationToken>()).Returns(
            Enum.GetValues<ClaimStatus>().Select(status => new ClaimStatusDto(status,
                ClaimStateMachine.DefaultTransitions.Where(edge => edge.From == status)
                    .Select(edge => new StatusTransitionDto(edge.To, edge.RequiredRole, edge.RuleCode)).ToArray())).ToArray());
    }

    private TransitionClaimStatusCommandHandler Command() => new(_claims, _reserves, _reference, _user, _clock, _unit);
    private GetTransitionPreflightQueryHandler Preflight() => new(_claims, _reserves, _reference, _user);
    private Task<ClaimStatus> Move(ClaimStatus target, string? reason = null, bool acknowledge = false, string? justification = null) =>
        Command().Handle(new(_claim.Id, target, reason, acknowledge, justification), CancellationToken.None);
    private Task<TransitionPreflightDto> Check(ClaimStatus target, string? reason = null, bool acknowledge = false,
        string? justification = null) => Preflight().Handle(new(_claim.Id, target, reason, acknowledge, justification), CancellationToken.None);

    private void ApprovedReserve()
    {
        var component = ClaimReserveComponent.Create(Guid.NewGuid(), _claim, ReserveComponentType.Indemnity, _components);
        _components.Add(component);
        component.Submit(Guid.NewGuid(), 100, ReserveTransactionType.Add, "Estimate", _actor,
            UserRole.Handler, new(_claim, _components), _clock);
    }

    public static IEnumerable<object[]> ClientEdges() => ClaimStateMachine.DefaultTransitions
        .Where(edge => edge.From != ClaimStatus.Reopened)
        .Select(edge => new object[] { edge.From, edge.To });

    [Theory]
    [MemberData(nameof(ClientEdges))]
    public async Task EveryConfiguredClientEdgeSucceedsWithSatisfiedConditions(ClaimStatus from, ClaimStatus to)
    {
        if (from != ClaimStatus.Draft) await Move(ClaimStatus.Open);
        if (from == ClaimStatus.UnderInvestigation) await Move(ClaimStatus.UnderInvestigation);
        if (from == ClaimStatus.PendingPayment)
        {
            ApprovedReserve();
            await Move(ClaimStatus.PendingPayment);
        }
        if (from == ClaimStatus.Closed) await Move(ClaimStatus.Closed);
        if (to == ClaimStatus.PendingPayment && _components.Count == 0) ApprovedReserve();
        if (from == ClaimStatus.UnderInvestigation && to == ClaimStatus.PendingPayment)
            _claim.DetermineLiability(LiabilityDetermination.Accepted, null, _clock);
        if (to == ClaimStatus.Reopened) _user.Role.Returns(UserRole.Supervisor);
        var result = await Move(to, reason: to is ClaimStatus.Withdrawn or ClaimStatus.Reopened ? "Documented" : null,
            justification: to == ClaimStatus.Closed && _components.Count > 0 ? "Balance accepted" : null);
        result.ShouldBe(to == ClaimStatus.Reopened ? ClaimStatus.Open : to);
    }

    [Fact]
    public async Task WalkLifecycleAndAuditEventsIncludingAtomicReopen()
    {
        (await Move(ClaimStatus.Open)).ShouldBe(ClaimStatus.Open);
        (await Move(ClaimStatus.UnderInvestigation)).ShouldBe(ClaimStatus.UnderInvestigation);
        ApprovedReserve();
        (await Check(ClaimStatus.PendingPayment)).BlockingConditions.Select(x => x.Message)
            .ShouldContain("Liability must be determined (Accepted or PartiallyAccepted) before PendingPayment.");
        _claim.DetermineLiability(LiabilityDetermination.Denied, null, _clock);
        (await Check(ClaimStatus.PendingPayment)).Allowed.ShouldBeFalse();
        _claim.DetermineLiability(LiabilityDetermination.PartiallyAccepted, null, _clock);
        (await Move(ClaimStatus.PendingPayment)).ShouldBe(ClaimStatus.PendingPayment);
        var closing = await Check(ClaimStatus.Closed);
        closing.Allowed.ShouldBeFalse();
        closing.RequiresJustification.ShouldBeTrue();
        closing.Warnings.Select(x => x.Code).ShouldContain("CC-04");
        (await Move(ClaimStatus.Closed, justification: "Balance remains open")).ShouldBe(ClaimStatus.Closed);
        _claim.ClosedAt.ShouldBe(_clock.UtcNow);
        _claim.ClosureReason.ShouldBe("Balance remains open");
        _user.Role.Returns(UserRole.Supervisor);
        var before = _claim.LastActivity!.Revision;
        (await Move(ClaimStatus.Reopened, reason: "New evidence")).ShouldBe(ClaimStatus.Open);
        _claim.LastActivity!.Revision.ShouldBe(before + 1);
        _claim.ClosedAt.ShouldBeNull();
        _claim.DomainEvents.OfType<ClaimStatusChanged>().TakeLast(2).Select(x => (x.From, x.To))
            .ShouldBe([(ClaimStatus.Closed, ClaimStatus.Reopened), (ClaimStatus.Reopened, ClaimStatus.Open)]);
        _claim.DomainEvents.OfType<ClaimReopened>().Count().ShouldBe(1);
        _claim.DomainEvents.OfType<ClaimClosed>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task WarningsPreflightAndExecutionStayInParity()
    {
        var warning = ClaimValidationIssue.Create(Guid.NewGuid(), _claim.Id, "TEST", "Review", ValidationSeverity.Warning, _clock);
        _claim.AddValidationIssue(warning, _clock);
        (await Check(ClaimStatus.Open)).Allowed.ShouldBeFalse();
        var blocked = await Should.ThrowAsync<TransitionBlockedException>(() => Move(ClaimStatus.Open));
        blocked.BlockingConditions.Select(x => x.Code).ShouldContain("BR-ST-02");
        (await Check(ClaimStatus.Open, acknowledge: true)).Allowed.ShouldBeTrue();
        (await Move(ClaimStatus.Open, acknowledge: true)).ShouldBe(ClaimStatus.Open);
        warning.Status.ShouldBe(ValidationIssueStatus.Acknowledged);
    }

    [Fact]
    public async Task InvalidEdgesListValidNextStatusesAndReopenRequiresSupervisor()
    {
        var invalid = await Should.ThrowAsync<TransitionBlockedException>(() => Move(ClaimStatus.Closed));
        invalid.Message.ShouldBe("Transition from Draft to Closed is not permitted.");
        invalid.ValidNextStatuses.ShouldBe([ClaimStatus.Open]);
        (await Check(ClaimStatus.Closed)).Allowed.ShouldBeFalse();
        await Move(ClaimStatus.Open);
        (await Check(ClaimStatus.Withdrawn)).RequiresReason.ShouldBeTrue();
        (await Should.ThrowAsync<TransitionBlockedException>(() => Move(ClaimStatus.Withdrawn)))
            .BlockingConditions.Select(x => x.Message).ShouldContain("Withdrawal reason is required.");
        await Move(ClaimStatus.Closed);
        (await Check(ClaimStatus.Reopened, reason: "Evidence")).Allowed.ShouldBeFalse();
        await Should.ThrowAsync<InsufficientAuthorityException>(() => Move(ClaimStatus.Reopened, "Evidence"));
        _user.Role.Returns(UserRole.Supervisor);
        (await Check(ClaimStatus.Reopened)).RequiresReason.ShouldBeTrue();
        (await Should.ThrowAsync<TransitionBlockedException>(() => Move(ClaimStatus.Reopened)))
            .BlockingConditions.Select(x => x.Code).ShouldContain("BR-ST-04");
    }

    [Fact]
    public async Task RejectedTransitionAfterConcurrencyRetryIsConflict()
    {
        _unit.IsConcurrencyRetry.Returns(true);
        await Should.ThrowAsync<ConcurrencyException>(() => Move(ClaimStatus.Closed));
    }

    [Fact]
    public async Task CriticalIssueBlocksReturningToOpenInPreflightAndCommand()
    {
        await Move(ClaimStatus.Open);
        await Move(ClaimStatus.UnderInvestigation);
        var issue = ClaimValidationIssue.Create(Guid.NewGuid(), _claim.Id, "REVIEW", "Evidence requires review",
            ValidationSeverity.Critical, _clock);
        _claim.AddValidationIssue(issue, _clock);
        _claim.ClearDomainEvents();
        var preflight = await Check(ClaimStatus.Open);
        preflight.Allowed.ShouldBeFalse();
        preflight.BlockingConditions.ShouldContain(x => x.Code == "BR-ST-02");
        var error = await Should.ThrowAsync<TransitionBlockedException>(() => Move(ClaimStatus.Open));
        error.BlockingConditions.Select(x => x.Code).ShouldBe(preflight.BlockingConditions.Select(x => x.Code));
        _claim.Status.ShouldBe(ClaimStatus.UnderInvestigation);
        _claim.DomainEvents.ShouldBeEmpty();
        _claim.ResolveValidationIssue(issue.Id, _clock);
        (await Move(ClaimStatus.Open)).ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public async Task HandlerUsesPersistedRequiredRoleRatherThanDefaultRole()
    {
        await Move(ClaimStatus.Open);
        _reference.GetStatusesAsync(Arg.Any<CancellationToken>()).Returns([
            new ClaimStatusDto(ClaimStatus.Open,
                [new StatusTransitionDto(ClaimStatus.UnderInvestigation, UserRole.Manager, "CUSTOM")])
        ]);
        (await Check(ClaimStatus.UnderInvestigation)).Allowed.ShouldBeFalse();
        await Should.ThrowAsync<InsufficientAuthorityException>(() => Move(ClaimStatus.UnderInvestigation));
        _user.Role.Returns(UserRole.Manager);
        (await Move(ClaimStatus.UnderInvestigation)).ShouldBe(ClaimStatus.UnderInvestigation);
    }
}
