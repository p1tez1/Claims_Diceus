using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ClaimStateMachineTests
{
    private static readonly (ClaimStatus From, ClaimStatus To)[] ValidEdges =
    [
        (ClaimStatus.Draft, ClaimStatus.Open),
        (ClaimStatus.Open, ClaimStatus.UnderInvestigation), (ClaimStatus.Open, ClaimStatus.PendingPayment),
        (ClaimStatus.Open, ClaimStatus.Closed), (ClaimStatus.Open, ClaimStatus.Withdrawn),
        (ClaimStatus.UnderInvestigation, ClaimStatus.Open), (ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment),
        (ClaimStatus.UnderInvestigation, ClaimStatus.Closed), (ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn),
        (ClaimStatus.PendingPayment, ClaimStatus.Closed), (ClaimStatus.Closed, ClaimStatus.Reopened),
        (ClaimStatus.Reopened, ClaimStatus.Open),
    ];

    private static ClaimTransitionContext ValidContext => new(UserRole.Manager, true, false, false, true,
        true, false, false, LiabilityDetermined: true, Reason: "Documented reason", ClosureJustification: "Approved closure");

    public static IEnumerable<object[]> StateMatrix()
    {
        foreach (var from in Enum.GetValues<ClaimStatus>())
            foreach (var to in Enum.GetValues<ClaimStatus>())
                yield return [from, to, ValidEdges.Contains((from, to))];
    }

    [Theory]
    [MemberData(nameof(StateMatrix))]
    public void BRST01FullStateMachineMatrixMatchesD1(ClaimStatus from, ClaimStatus to, bool expected)
    {
        var evaluation = new ClaimStateMachine().Evaluate(from, to, ValidContext);
        evaluation.IsAllowed.ShouldBe(expected);
        if (!expected) evaluation.BlockingConditions.Single().Code.ShouldBe("BR-ST-01");
    }

    public static IEnumerable<object[]> ValidTransitionRoles() => ValidEdges.Select(edge => new object[]
    {
        edge.From, edge.To, edge.To == ClaimStatus.Reopened ? UserRole.Supervisor : UserRole.Handler,
    });

    [Theory]
    [MemberData(nameof(ValidTransitionRoles))]
    public void DD11EveryValidTransitionHasRequiredRole(ClaimStatus from, ClaimStatus to, UserRole expected)
    {
        var edge = ClaimStateMachine.DefaultTransitions.Single(row => row.From == from && row.To == to);
        edge.RequiredRole.ShouldBe(expected);
        new ClaimStateMachine().Evaluate(from, to, ValidContext with { Role = expected }).IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void BRST01InvalidTransitionListsValidNextStatuses()
    {
        var result = new ClaimStateMachine().Evaluate(ClaimStatus.Draft, ClaimStatus.Closed, ValidContext);
        result.ValidNextStatuses.ShouldBe([ClaimStatus.Open]);
        result.BlockingConditions[0].Message.ShouldContain("Valid next statuses: Open");
    }

    [Fact]
    public void BRST02DraftToOpenRequiresResolvedCriticalIssuesClaimantAndHandler()
    {
        var result = new ClaimStateMachine().Evaluate(ClaimStatus.Draft, ClaimStatus.Open, ValidContext with
        { HasUnresolvedCriticalIssues = true, HasActiveClaimant = false, HasAssignedHandler = false });
        result.BlockingConditions.Count.ShouldBe(3);
        result.BlockingConditions.Select(row => row.Code).ShouldContain("BR-P-01");
    }

    [Theory]
    [InlineData(ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.Reopened)]
    public void ReturningToOpenRequiresCriticalIssuesResolvedAndClaimant(ClaimStatus from)
    {
        var result = new ClaimStateMachine().Evaluate(from, ClaimStatus.Open, ValidContext with
        { HasUnresolvedCriticalIssues = true, HasActiveClaimant = false, HasAssignedHandler = false, HasOpenWarnings = true });
        result.IsAllowed.ShouldBeFalse();
        result.BlockingConditions.Select(x => x.Code).ShouldBe(["BR-ST-02", "BR-P-01"]);
        var satisfied = new ClaimStateMachine().Evaluate(from, ClaimStatus.Open, ValidContext with
        { HasAssignedHandler = false, HasOpenWarnings = true });
        satisfied.IsAllowed.ShouldBeTrue(); // Assignment/acknowledgement remain Draft intake conditions.
    }

    [Fact]
    public void BRST02OpenWarningsRequireExplicitAcknowledgement()
    {
        var machine = new ClaimStateMachine();
        var context = ValidContext with { HasOpenWarnings = true };
        machine.Evaluate(ClaimStatus.Draft, ClaimStatus.Open, context).IsAllowed.ShouldBeFalse();
        var confirmed = machine.Evaluate(ClaimStatus.Draft, ClaimStatus.Open, context with { AcknowledgeWarnings = true });
        confirmed.IsAllowed.ShouldBeTrue();
        confirmed.Warnings.Single().Code.ShouldBe("BR-ST-02");
    }

    [Theory]
    [InlineData(ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation)]
    public void PendingPaymentRequiresApprovedReserve(ClaimStatus from)
        => new ClaimStateMachine().Evaluate(from, ClaimStatus.PendingPayment,
            ValidContext with { HasApprovedReserve = false }).IsAllowed.ShouldBeFalse();

    [Fact]
    public void InvestigationToPendingPaymentRequiresLiabilityDetermination()
    {
        var context = ValidContext with { LiabilityDetermined = false };
        var machine = new ClaimStateMachine();
        machine.Evaluate(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, context).IsAllowed.ShouldBeFalse();
        machine.Evaluate(ClaimStatus.Open, ClaimStatus.PendingPayment, context).IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void CC01PendingApprovalBlocksClosure()
        => AssertClosureBlock(ValidContext with { HasPendingReserve = true }, "CC-01");

    [Fact]
    public void CC02UnresolvedCriticalIssuesBlockClosure()
        => AssertClosureBlock(ValidContext with { HasUnresolvedCriticalIssues = true }, "CC-02");

    [Fact]
    public void CC03ClosureRequiresActiveClaimant()
        => AssertClosureBlock(ValidContext with { HasActiveClaimant = false }, "CC-03");

    [Fact]
    public void CC04NonZeroReserveRequiresJustificationAndReturnsWarning()
    {
        var context = ValidContext with { HasNonZeroReserveBalance = true, ClosureJustification = null };
        AssertClosureBlock(context, "CC-04");
        var result = new ClaimStateMachine().Evaluate(ClaimStatus.Open, ClaimStatus.Closed,
            context with { ClosureJustification = "Accept outstanding exposure" });
        result.IsAllowed.ShouldBeTrue();
        result.Warnings.Single().Code.ShouldBe("CC-04");
    }

    [Theory]
    [InlineData(ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.PendingPayment)]
    public void BRST03ClosureReturnsAllBlockingConditions(ClaimStatus from)
    {
        var result = new ClaimStateMachine().Evaluate(from, ClaimStatus.Closed, ValidContext with
        {
            HasPendingReserve = true,
            HasUnresolvedCriticalIssues = true,
            HasActiveClaimant = false,
            HasNonZeroReserveBalance = true,
            ClosureJustification = " ",
        });
        result.BlockingConditions.Select(row => row.Code).ShouldBe(["CC-01", "CC-02", "CC-03", "CC-04"]);
    }

    [Fact]
    public void BRST04ReopenRequiresSupervisorAndReason()
    {
        var result = new ClaimStateMachine().Evaluate(ClaimStatus.Closed, ClaimStatus.Reopened,
            ValidContext with { Role = UserRole.Handler, Reason = " " });
        result.BlockingConditions.Select(row => row.Code).ShouldBe(["INSUFFICIENT_AUTHORITY", "BR-ST-04"]);
    }

    [Fact]
    public void WithdrawalRequiresReason()
        => new ClaimStateMachine().Evaluate(ClaimStatus.Open, ClaimStatus.Withdrawn,
            ValidContext with { Reason = null }).IsAllowed.ShouldBeFalse();

    [Fact]
    public void PersistedTransitionTableMaySupplyHigherRequiredRole()
    {
        var machine = new ClaimStateMachine([new(ClaimStatus.Open, ClaimStatus.UnderInvestigation, UserRole.Manager, "BR-ST-01")]);
        machine.Evaluate(ClaimStatus.Open, ClaimStatus.UnderInvestigation, ValidContext with { Role = UserRole.Handler })
            .BlockingConditions.Single().Code.ShouldBe("INSUFFICIENT_AUTHORITY");
        machine.Evaluate(ClaimStatus.Open, ClaimStatus.PendingPayment, ValidContext).IsAllowed.ShouldBeFalse();
    }

    private static void AssertClosureBlock(ClaimTransitionContext context, string code)
    {
        var result = new ClaimStateMachine().Evaluate(ClaimStatus.Open, ClaimStatus.Closed, context);
        result.IsAllowed.ShouldBeFalse();
        result.BlockingConditions.Select(row => row.Code).ShouldContain(code);
    }
}
