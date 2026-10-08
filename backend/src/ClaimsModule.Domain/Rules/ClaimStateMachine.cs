using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Rules;

public sealed record ClaimStatusTransition(ClaimStatus From, ClaimStatus To, UserRole RequiredRole, string RuleCode);
public sealed record BlockingCondition(string Code, string Message);

public sealed record ClaimTransitionContext(
    UserRole Role,
    bool HasActiveClaimant,
    bool HasUnresolvedCriticalIssues,
    bool HasOpenWarnings,
    bool HasAssignedHandler,
    bool HasApprovedReserve,
    bool HasPendingReserve,
    bool HasNonZeroReserveBalance,
    bool LiabilityDetermined = false,
    string? Reason = null,
    bool AcknowledgeWarnings = false,
    string? ClosureJustification = null);

public sealed record ClaimTransitionEvaluation(
    IReadOnlyList<BlockingCondition> BlockingConditions,
    IReadOnlyList<BlockingCondition> Warnings,
    IReadOnlyList<ClaimStatus> ValidNextStatuses)
{
    public bool IsAllowed => BlockingConditions.Count == 0;
}

public sealed class ClaimStateMachine
{
    public static IReadOnlyList<ClaimStatusTransition> DefaultTransitions { get; } = Array.AsReadOnly<ClaimStatusTransition>(
    [
        new(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler, "BR-ST-02"),
        new(ClaimStatus.Open, ClaimStatus.UnderInvestigation, UserRole.Handler, "BR-ST-01"),
        new(ClaimStatus.Open, ClaimStatus.PendingPayment, UserRole.Handler, "BR-ST-01"),
        new(ClaimStatus.Open, ClaimStatus.Closed, UserRole.Handler, "BR-ST-03"),
        new(ClaimStatus.Open, ClaimStatus.Withdrawn, UserRole.Handler, "BR-ST-01"),
        new(ClaimStatus.UnderInvestigation, ClaimStatus.Open, UserRole.Handler, "BR-ST-02"),
        new(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, UserRole.Handler, "BR-ST-01"),
        new(ClaimStatus.UnderInvestigation, ClaimStatus.Closed, UserRole.Handler, "BR-ST-03"),
        new(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn, UserRole.Handler, "BR-ST-01"),
        new(ClaimStatus.PendingPayment, ClaimStatus.Closed, UserRole.Handler, "BR-ST-03"),
        new(ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor, "BR-ST-04"),
        new(ClaimStatus.Reopened, ClaimStatus.Open, UserRole.Handler, "BR-ST-02"),
    ]);

    // A caller may supply the persisted reference table; the defaults are the seed values.
    public ClaimStateMachine(IEnumerable<ClaimStatusTransition>? transitions = null)
    {
        var rows = (transitions ?? DefaultTransitions).ToArray();
        foreach (var row in rows)
        {
            RuleGuard.Defined(row.From);
            RuleGuard.Defined(row.To);
            RuleGuard.Defined(row.RequiredRole);
        }
        if (rows.DistinctBy(row => (row.From, row.To)).Count() != rows.Length)
            throw new ArgumentException("Duplicate transition edges are not permitted.", nameof(transitions));
        Transitions = Array.AsReadOnly(rows);
    }

    public IReadOnlyList<ClaimStatusTransition> Transitions { get; }

    public ClaimTransitionEvaluation Evaluate(ClaimStatus from, ClaimStatus to, ClaimTransitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RuleGuard.Defined(context.Role);
        var blockers = new List<BlockingCondition>();
        var warnings = new List<BlockingCondition>();
        var next = Transitions.Where(row => row.From == from).Select(row => row.To).ToArray();
        var edge = Transitions.SingleOrDefault(row => row.From == from && row.To == to);
        if (edge is null)
        {
            blockers.Add(new("BR-ST-01", $"Transition from {from} to {to} is not permitted. Valid next statuses: {string.Join(", ", next)}."));
            return new(blockers.AsReadOnly(), warnings.AsReadOnly(), Array.AsReadOnly(next));
        }

        if (context.Role < edge.RequiredRole)
            blockers.Add(new("INSUFFICIENT_AUTHORITY", $"{edge.RequiredRole} or higher is required for this transition."));
        if (to == ClaimStatus.Open)
        {
            if (context.HasUnresolvedCriticalIssues) blockers.Add(new("BR-ST-02", "All Critical issues must be resolved before Open."));
            if (!context.HasActiveClaimant) blockers.Add(new("BR-P-01", "At least one active Claimant is required before Open."));
        }
        if (from == ClaimStatus.Draft && to == ClaimStatus.Open)
        {
            if (!context.HasAssignedHandler) blockers.Add(new("BR-ST-02", "A handler must be assigned before Open."));
            if (context.HasOpenWarnings)
            {
                warnings.Add(new("BR-ST-02", "Open warnings require acknowledgement."));
                if (!context.AcknowledgeWarnings) blockers.Add(new("BR-ST-02", "Acknowledge all Open warnings before Open."));
            }
        }
        if (to == ClaimStatus.PendingPayment)
        {
            if (!context.HasApprovedReserve) blockers.Add(new("BR-ST-01", "PendingPayment requires at least one approved reserve."));
            if (from == ClaimStatus.UnderInvestigation && !context.LiabilityDetermined)
                blockers.Add(new("BR-ST-01", "Liability must be determined (Accepted or PartiallyAccepted) before PendingPayment."));
        }
        if (to == ClaimStatus.Closed)
        {
            if (context.HasPendingReserve) blockers.Add(new("CC-01", "Pending reserve approvals must be completed before closure."));
            if (context.HasUnresolvedCriticalIssues) blockers.Add(new("CC-02", "All Critical issues must be resolved before closure."));
            if (!context.HasActiveClaimant) blockers.Add(new("CC-03", "Closure requires an active Claimant."));
            if (context.HasNonZeroReserveBalance)
            {
                warnings.Add(new("CC-04", "Closure has a non-zero reserve balance."));
                if (string.IsNullOrWhiteSpace(context.ClosureJustification))
                    blockers.Add(new("CC-04", "A justification is required to close with non-zero reserves."));
            }
        }
        if (to == ClaimStatus.Withdrawn && string.IsNullOrWhiteSpace(context.Reason))
            blockers.Add(new("BR-ST-01", "Withdrawal reason is required."));
        if (to == ClaimStatus.Reopened && string.IsNullOrWhiteSpace(context.Reason))
            blockers.Add(new("BR-ST-04", "Reopen reason is required."));

        return new(blockers.AsReadOnly(), warnings.AsReadOnly(), Array.AsReadOnly(next));
    }
}
