using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests.Reserves;

public sealed class ReserveUseCaseTests
{
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IReserveComponentRepository _reserves = Substitute.For<IReserveComponentRepository>();
    private readonly IIdentifierGenerator _ids = Substitute.For<IIdentifierGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IValidationWarnings _warnings = Substitute.For<IValidationWarnings>();
    private readonly List<ClaimReserveComponent> _components = [];
    private readonly Guid _submitter = Guid.NewGuid();
    private readonly Guid _reviewer = Guid.NewGuid();
    private readonly Claim _claim;
    private ReserveSubmission Submission => new(_reserves, _ids, _clock, _user, _warnings);
    private SubmitReserveCommandHandler Submitter => new(_claims, _reserves, Submission);
    private ApproveReserveCommandHandler Approver => new(_claims, _reserves, _user, _clock);

    public ReserveUseCaseTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        _ids.NewId().Returns(_ => Guid.NewGuid());
        Actor(_submitter, UserRole.Handler);
        var id = Guid.NewGuid();
        _claim = Claim.Create(id, ClaimNumber.Create(2026, 1), _submitter,
            LossEvent.Create(Guid.NewGuid(), id, _clock.UtcNow.AddDays(-1), "Vehicle damaged in a collision.", "COL-VEH-COL", true, _clock),
            [ClaimParty.Create(Guid.NewGuid(), id, PartyRole.Claimant, PartyType.Person, "Alex", "Smith")], _clock,
            new(Guid.NewGuid(), "POL", "Client", _clock.UtcNow.AddYears(-1), _clock.UtcNow.AddYears(1)));
        _claims.GetAsync(id, Arg.Any<CancellationToken>()).Returns(_claim);
        _reserves.GetForClaimAsync(id, Arg.Any<CancellationToken>()).Returns(_components);
        _reserves.When(x => x.Add(Arg.Any<ClaimReserveComponent>())).Do(x => _components.Add(x.Arg<ClaimReserveComponent>()));
    }

    private void Actor(Guid id, UserRole role)
    {
        _user.UserId.Returns(id);
        _user.Role.Returns(role);
        _user.Name.Returns(role.ToString());
    }
    private Task<ReserveTransactionResult> Submit(decimal amount, ReserveTransactionType type = ReserveTransactionType.Add,
        ReserveComponentType component = ReserveComponentType.Indemnity, Guid? resubmission = null) =>
        Submitter.Handle(new(_claim.Id, component, amount, "Updated estimate", type, resubmission), default);

    [Theory]
    [InlineData(10000, ReserveApprovalStatus.AutoApproved)]
    [InlineData(10000.01, ReserveApprovalStatus.PendingApproval)]
    [InlineData(100000, ReserveApprovalStatus.PendingApproval)]
    [InlineData(100000.01, ReserveApprovalStatus.PendingApproval)]
    public async Task SubmissionUsesDomainThresholdsAndTouchesClaim(decimal amount, ReserveApprovalStatus expected)
    {
        var before = _claim.LastActivity!.Revision;
        var result = await Submit(amount);
        result.ApprovalStatus.ShouldBe(expected);
        result.Warnings.ShouldBeEmpty();
        _components.ShouldHaveSingleItem().CurrentAmount.ShouldBe(expected == ReserveApprovalStatus.AutoApproved ? amount : 0);
        _claim.LastActivity!.Revision.ShouldBe(before + 1);
        _components[0].DomainEvents.OfType<ReserveAutoApproved>().Count().ShouldBe(expected == ReserveApprovalStatus.AutoApproved ? 1 : 0);
    }

    [Theory]
    [InlineData(UserRole.Handler, 50000, false)]
    [InlineData(UserRole.Supervisor, 100000, true)]
    [InlineData(UserRole.Supervisor, 100000.01, false)]
    [InlineData(UserRole.Manager, 10000.01, true)]
    [InlineData(UserRole.Manager, 100000, true)]
    [InlineData(UserRole.Manager, 100000.01, true)]
    [InlineData(UserRole.Manager, 10000000, true)]
    [InlineData(UserRole.Manager, 10000000.01, false)]
    public async Task ApprovalAuthorityAndIndividualCeilingComeFromDomain(UserRole role, decimal amount, bool allowed)
    {
        var pending = await Submit(amount);
        Actor(_reviewer, role);
        if (allowed)
        {
            await Approver.Handle(new(_claim.Id, pending.Transaction.Id), default);
            _components[0].CurrentAmount.ShouldBe(amount);
            _components[0].DomainEvents.OfType<ReserveApproved>().ShouldHaveSingleItem();
        }
        else await Should.ThrowAsync<InsufficientAuthorityException>(() => Approver.Handle(new(_claim.Id, pending.Transaction.Id), default));
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Manager)]
    public async Task SelfApprovalIs422DomainRule(UserRole role)
    {
        Actor(_submitter, role);
        var pending = await Submit(50000);
        (await Should.ThrowAsync<SelfApprovalException>(() => Approver.Handle(new(_claim.Id, pending.Transaction.Id), default)))
            .Message.ShouldBe("Self-approval is not permitted.");
    }

    [Fact]
    public async Task RejectionResubmissionAndPutAppendWithoutEditingPendingOrRejectedAmounts()
    {
        var pending = await Submit(50000);
        Actor(_reviewer, UserRole.Supervisor);
        await new RejectReserveCommandHandler(_claims, _reserves, _user, _clock)
            .Handle(new(_claim.Id, pending.Transaction.Id, "Estimate too high"), default);
        Actor(_submitter, UserRole.Handler);
        var revised = await Submit(30000, resubmission: pending.Transaction.Id);
        var adjusted = await new AdjustReserveCommandHandler(_claims, _reserves, Submission)
            .Handle(new(_claim.Id, _components[0].Id, 1000, "Another delta"), default);
        var history = _components[0].ReserveHistory;
        history.Count.ShouldBe(3);
        history[0].Amount.ShouldBe(50000);
        history[0].ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        history[1].Amount.ShouldBe(30000);
        history[1].ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        revised.Transaction.ResubmissionOfId.ShouldBe(history[0].Id);
        adjusted.Transaction.TransactionType.ShouldBe(ReserveTransactionType.Adjust);
        _components[0].CurrentAmount.ShouldBe(1000);
        await Should.ThrowAsync<DomainRuleViolationException>(() => Submit(500, resubmission: history[1].Id));
        await Should.ThrowAsync<DomainRuleViolationException>(() => Submit(500, ReserveTransactionType.Reverse, resubmission: history[0].Id));
    }

    [Fact]
    public async Task RetractRequiresSubmitterAndPendingAndTouchesOnlyOnSuccess()
    {
        var pending = await Submit(50000);
        var retract = new RetractReserveCommandHandler(_claims, _reserves, _user, _clock);
        Actor(_reviewer, UserRole.Manager);
        await Should.ThrowAsync<InsufficientAuthorityException>(() => retract.Handle(new(_claim.Id, pending.Transaction.Id), default));
        Actor(_submitter, UserRole.Handler);
        var before = _claim.LastActivity!.Revision;
        await retract.Handle(new(_claim.Id, pending.Transaction.Id), default);
        _claim.LastActivity!.Revision.ShouldBe(before + 1);
        _components[0].ReserveHistory[0].PostingStatus.ShouldBe(PostingStatus.Cancelled);
        await Should.ThrowAsync<DomainRuleViolationException>(() => retract.Handle(new(_claim.Id, pending.Transaction.Id), default));
        var automatic = await Submit(10);
        await Should.ThrowAsync<DomainRuleViolationException>(() => retract.Handle(new(_claim.Id, automatic.Transaction.Id), default));
    }

    [Fact]
    public async Task CapWarningForcesPendingAndApprovalNeedsManagerOverride()
    {
        var initial = await Submit(10000000);
        Actor(_reviewer, UserRole.Manager);
        await Approver.Handle(new(_claim.Id, initial.Transaction.Id), default);
        Actor(_submitter, UserRole.Handler);
        var capped = await Submit(1, component: ReserveComponentType.Expense);
        capped.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        capped.Warnings.ShouldHaveSingleItem().Code.ShouldBe("BR-R-05");
        Actor(_reviewer, UserRole.Supervisor);
        await Should.ThrowAsync<DomainRuleViolationException>(() => Approver.Handle(new(_claim.Id, capped.Transaction.Id), default));
        var setter = new SetReserveOverrideCommandHandler(_claims, _user, _clock);
        await Should.ThrowAsync<InsufficientAuthorityException>(() => setter.Handle(new(_claim.Id), default));
        Actor(_reviewer, UserRole.Manager);
        await setter.Handle(new(_claim.Id), default);
        await Approver.Handle(new(_claim.Id, capped.Transaction.Id), default);
        _components.Sum(x => x.CurrentAmount).ShouldBe(10000001);
        _claim.DomainEvents.OfType<ReserveOverrideSet>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public async Task TerminalClaimsRejectNewReservesInDomain(ClaimStatus status)
    {
        _claim.TransitionTo(ClaimStatus.Open, _submitter, UserRole.Handler, _clock, [], new(AcknowledgeWarnings: true));
        _claim.TransitionTo(status, _submitter, UserRole.Handler, _clock, [], new(Reason: "Complete"));
        (await Should.ThrowAsync<DomainRuleViolationException>(() => Submit(100))).Code.ShouldBe("CLAIM_STATUS");
        _components.ShouldBeEmpty();
    }

    public static IEnumerable<object[]> Signs()
    {
        foreach (var component in Enum.GetValues<ReserveComponentType>())
            foreach (var type in Enum.GetValues<ReserveTransactionType>())
                foreach (var amount in new[] { -1m, 0m, 1m })
                    yield return [component, type, amount, amount != 0 && (type == ReserveTransactionType.Adjust
                || type == ReserveTransactionType.Reverse && amount > 0
                || type == ReserveTransactionType.Add && (component == ReserveComponentType.SubrogationRecoverable ? amount < 0 : amount > 0))];
    }

    [Theory]
    [MemberData(nameof(Signs))]
    public async Task ValidatorUsesAllDomainSignRules(ReserveComponentType component, ReserveTransactionType type, decimal amount, bool valid)
    {
        (await new SubmitReserveCommandValidator().ValidateAsync(new SubmitReserveCommand(_claim.Id, component, amount, "Estimate", type))).IsValid.ShouldBe(valid);
    }

    [Fact]
    public async Task ValidatorsRejectBadEnumsPrecisionReasonAndEmptyResubmission()
    {
        var valid = new SubmitReserveCommand(_claim.Id, ReserveComponentType.Indemnity, 10, "Estimate", ReserveTransactionType.Add);
        var validator = new SubmitReserveCommandValidator();
        foreach (var request in new[] { valid with { Component = (ReserveComponentType)99 }, valid with { TransactionType = (ReserveTransactionType)99 },
            valid with { Amount = 1.12345m }, valid with { Amount = 1000000000000000m }, valid with { ChangeReason = " " },
            valid with { ChangeReason = new string('x', 501) }, valid with { ResubmissionOfId = Guid.Empty } })
            (await validator.ValidateAsync(request)).IsValid.ShouldBeFalse();
        (await new RejectReserveCommandValidator().ValidateAsync(new RejectReserveCommand(_claim.Id, Guid.NewGuid(), " "))).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(10000, ReserveApprovalStatus.AutoApproved)]
    [InlineData(50000, ReserveApprovalStatus.PendingApproval)]
    public async Task InitialReserveSharesSubmissionPath(decimal amount, ReserveApprovalStatus expected)
    {
        var policies = Substitute.For<IPolicyQueries>();
        policies.GetAsync(_claim.PolicyId!.Value, Arg.Any<CancellationToken>()).Returns(
            new PolicyDto(_claim.PolicyId.Value, "POL", "Client", new(2026, 1, 1), new(2026, 12, 31), "Active", []));
        var numbers = Substitute.For<IClaimNumberGenerator>();
        numbers.NextAsync(2026, Arg.Any<CancellationToken>()).Returns(ClaimNumber.Create(2026, 2));
        var mapper = new MapperConfiguration(x => x.AddProfile<ClaimMappingProfile>()).CreateMapper();
        var request = new CreateClaimCommand
        {
            LossDate = _clock.UtcNow.AddDays(-1),
            LossDescription = "Vehicle damaged in a collision.",
            CauseOfLossCode = "COL-VEH-COL",
            PolicyId = _claim.PolicyId,
            Parties = [new() { FirstName = "Alex", LastName = "Smith" }],
            InitialReserve = new(ReserveComponentType.Indemnity, amount, "Initial estimate")
        };
        await new CreateClaimCommandHandler(_claims, numbers, _ids, _clock, _user, policies, mapper, Submission).Handle(request, default);
        var initial = _components.ShouldHaveSingleItem().ReserveHistory.ShouldHaveSingleItem();
        initial.ApprovalStatus.ShouldBe(expected);
        _reserves.Received(1).Add(Arg.Any<ClaimReserveComponent>());
        _claims.Received(1).Add(Arg.Any<Claim>());
    }

    [Fact]
    public async Task MissingAndCrossClaimTransactionsReturn404()
    {
        await Should.ThrowAsync<NotFoundException>(() => Submitter.Handle(new(Guid.NewGuid(), ReserveComponentType.Expense, 10, "Estimate", ReserveTransactionType.Add), default));
        Actor(_reviewer, UserRole.Supervisor);
        await Should.ThrowAsync<NotFoundException>(() => Approver.Handle(new(_claim.Id, Guid.NewGuid()), default));
    }
}
