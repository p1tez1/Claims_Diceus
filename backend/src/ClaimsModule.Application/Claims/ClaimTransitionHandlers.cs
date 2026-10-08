using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims;

public sealed record TransitionClaimStatusCommand(Guid Id, ClaimStatus TargetStatus, string? Reason = null,
    bool AcknowledgeWarnings = false, string? ClosureJustification = null) : ICommand<ClaimStatus>;

public sealed record GetTransitionPreflightQuery(Guid Id, ClaimStatus TargetStatus, string? Reason = null,
    bool AcknowledgeWarnings = false, string? ClosureJustification = null) : IQuery<TransitionPreflightDto>;

public sealed record TransitionPreflightDto(bool Allowed, IReadOnlyList<BlockingCondition> BlockingConditions,
    IReadOnlyList<BlockingCondition> Warnings, bool RequiresJustification, bool RequiresReason);

public sealed class TransitionClaimStatusCommandValidator : AbstractValidator<TransitionClaimStatusCommand>
{
    public TransitionClaimStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetStatus).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(2000);
        RuleFor(x => x.ClosureJustification).MaximumLength(2000);
    }
}

public sealed class TransitionClaimStatusCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    IReferenceDataQueries reference, ICurrentUser user, IClock clock, IUnitOfWork unitOfWork)
    : IRequestHandler<TransitionClaimStatusCommand, ClaimStatus>
{
    public async Task<ClaimStatus> Handle(TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var components = await reserves.GetForClaimAsync(claim.Id, cancellationToken);
        var machine = await ClaimTransitionSupport.LoadMachine(reference, cancellationToken);
        var role = ClaimTransitionSupport.Role(user);
        var options = ClaimTransitionSupport.Options(request.Reason, request.AcknowledgeWarnings, request.ClosureJustification);
        var evaluation = claim.EvaluateTransition(request.TargetStatus, role, components, options, machine);
        if (!evaluation.IsAllowed)
        {
            if (unitOfWork.IsConcurrencyRetry)
                throw new ConcurrencyException("The claim status changed during this request. Reload its current state.");
            ClaimTransitionSupport.ThrowBlocked(claim.Status, request.TargetStatus, evaluation);
        }
        claim.TransitionTo(request.TargetStatus,
            user.UserId ?? throw new InsufficientAuthorityException("An authenticated user is required."),
            role, clock, components, options, machine);
        return claim.Status;
    }
}

public sealed class GetTransitionPreflightQueryHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    IReferenceDataQueries reference, ICurrentUser user) : IRequestHandler<GetTransitionPreflightQuery, TransitionPreflightDto>
{
    public async Task<TransitionPreflightDto> Handle(GetTransitionPreflightQuery request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TargetStatus))
            throw new ValidationException("A valid target status is required.");
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var components = await reserves.GetForClaimAsync(claim.Id, cancellationToken);
        var machine = await ClaimTransitionSupport.LoadMachine(reference, cancellationToken);
        var evaluation = claim.EvaluateTransition(request.TargetStatus, ClaimTransitionSupport.Role(user), components,
            ClaimTransitionSupport.Options(request.Reason, request.AcknowledgeWarnings, request.ClosureJustification), machine);
        var edgeExists = evaluation.ValidNextStatuses.Contains(request.TargetStatus);
        return new(evaluation.IsAllowed, evaluation.BlockingConditions, evaluation.Warnings,
            edgeExists && request.TargetStatus == ClaimStatus.Closed && components.Any(x => x.CurrentAmount != 0),
            edgeExists && request.TargetStatus is ClaimStatus.Reopened or ClaimStatus.Withdrawn);
    }
}

internal static class ClaimTransitionSupport
{
    internal static ClaimTransitionOptions Options(string? reason, bool acknowledgeWarnings, string? closureJustification) =>
        new(reason, acknowledgeWarnings, closureJustification);

    internal static UserRole Role(ICurrentUser user) => user.Role
        ?? throw new InsufficientAuthorityException("An authenticated role is required.");

    internal static async Task<ClaimStateMachine> LoadMachine(IReferenceDataQueries reference, CancellationToken token)
    {
        var statuses = await reference.GetStatusesAsync(token);
        return new(statuses.SelectMany(x => x.Transitions.Select(edge =>
            new ClaimStatusTransition(x.Status, edge.To, edge.RequiredRole, edge.RuleCode))));
    }

    internal static void ThrowBlocked(ClaimStatus from, ClaimStatus to, ClaimTransitionEvaluation evaluation)
    {
        var roleBlock = evaluation.BlockingConditions.FirstOrDefault(x => x.Code == "INSUFFICIENT_AUTHORITY");
        if (roleBlock is not null) throw new InsufficientAuthorityException(roleBlock.Message);
        throw new TransitionBlockedException(from, to, evaluation);
    }
}
