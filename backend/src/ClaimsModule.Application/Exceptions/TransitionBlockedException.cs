using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;

namespace ClaimsModule.Application.Exceptions;

public sealed class TransitionBlockedException : Exception
{
    public TransitionBlockedException(ClaimStatus from, ClaimStatus to, ClaimTransitionEvaluation evaluation)
        : base(evaluation.ValidNextStatuses.Contains(to)
            ? "The requested status transition is blocked."
            : $"Transition from {from} to {to} is not permitted.")
    {
        BlockingConditions = evaluation.BlockingConditions;
        ValidNextStatuses = evaluation.ValidNextStatuses;
    }

    public IReadOnlyList<BlockingCondition> BlockingConditions { get; }
    public IReadOnlyList<ClaimStatus> ValidNextStatuses { get; }
}
