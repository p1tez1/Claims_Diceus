using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Reserves;

public sealed class SubmitReserveCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    ReserveSubmission submission) : IRequestHandler<SubmitReserveCommand, ReserveTransactionResult>
{
    public async Task<ReserveTransactionResult> Handle(SubmitReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.ClaimId, cancellationToken);
        return submission.Submit(claim, await reserves.GetForClaimAsync(claim.Id, cancellationToken), request);
    }
}

public sealed class AdjustReserveCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    ReserveSubmission submission) : IRequestHandler<AdjustReserveCommand, ReserveTransactionResult>
{
    public async Task<ReserveTransactionResult> Handle(AdjustReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.ClaimId, cancellationToken);
        var components = await reserves.GetForClaimAsync(claim.Id, cancellationToken);
        var component = components.SingleOrDefault(x => x.Id == request.ComponentId)
            ?? throw new NotFoundException("Reserve component was not found on this claim.");
        return submission.Submit(claim, components, new(claim.Id, component.Component, request.Amount,
            request.ChangeReason, ReserveTransactionType.Adjust, request.ResubmissionOfId));
    }
}

public sealed class ApproveReserveCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    ICurrentUser user, IClock clock) : IRequestHandler<ApproveReserveCommand, Unit>
{
    public async Task<Unit> Handle(ApproveReserveCommand request, CancellationToken cancellationToken)
    {
        ReserveHandlerSupport.EnsureReviewer(user);
        var (component, context) = await ReserveHandlerSupport.Load(claims, reserves, request.ClaimId, request.TransactionId, cancellationToken);
        component.Approve(request.TransactionId, user.UserId!.Value, user.Role!.Value, context, clock);
        return Unit.Value;
    }
}

public sealed class RejectReserveCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    ICurrentUser user, IClock clock) : IRequestHandler<RejectReserveCommand, Unit>
{
    public async Task<Unit> Handle(RejectReserveCommand request, CancellationToken cancellationToken)
    {
        ReserveHandlerSupport.EnsureReviewer(user);
        var (component, context) = await ReserveHandlerSupport.Load(claims, reserves, request.ClaimId, request.TransactionId, cancellationToken);
        component.Reject(request.TransactionId, user.UserId!.Value, user.Role!.Value, request.RejectionReason, context, clock);
        return Unit.Value;
    }
}

public sealed class RetractReserveCommandHandler(IClaimRepository claims, IReserveComponentRepository reserves,
    ICurrentUser user, IClock clock) : IRequestHandler<RetractReserveCommand, Unit>
{
    public async Task<Unit> Handle(RetractReserveCommand request, CancellationToken cancellationToken)
    {
        var (component, context) = await ReserveHandlerSupport.Load(claims, reserves, request.ClaimId, request.TransactionId, cancellationToken);
        component.Retract(request.TransactionId, user.UserId ?? throw new InsufficientAuthorityException(), context, clock);
        return Unit.Value;
    }
}

public sealed class RetryGlPostingCommandHandler(ReservePostingRetry retry) : IRequestHandler<RetryGlPostingCommand, Unit>
{
    // All authenticated claims roles may retry; tenant ownership is enforced by the existing repositories.
    public async Task<Unit> Handle(RetryGlPostingCommand request, CancellationToken cancellationToken)
    {
        await retry.ExecuteAsync(new(request.ClaimId, request.TransactionId), cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetReserveOverrideCommandHandler(IClaimRepository claims, ICurrentUser user, IClock clock)
    : IRequestHandler<SetReserveOverrideCommand, Unit>
{
    public async Task<Unit> Handle(SetReserveOverrideCommand request, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Manager || user.UserId is null) throw new InsufficientAuthorityException("Only a Manager may set the reserve override.");
        var claim = await ClaimHandlerSupport.Load(claims, request.ClaimId, cancellationToken);
        claim.SetManagerOverride(user.UserId.Value, user.Role.Value, request.Enabled, clock);
        return Unit.Value;
    }
}

public sealed class GetClaimReservesQueryHandler(IClaimRepository claims, IReserveQueries queries)
    : IRequestHandler<GetClaimReservesQuery, IReadOnlyList<ReserveComponentDto>>
{
    public async Task<IReadOnlyList<ReserveComponentDto>> Handle(GetClaimReservesQuery request, CancellationToken cancellationToken)
    {
        await ClaimHandlerSupport.Load(claims, request.ClaimId, cancellationToken);
        return await queries.ListAsync(request.ClaimId, cancellationToken);
    }
}

internal static class ReserveHandlerSupport
{
    internal static void EnsureReviewer(ICurrentUser user)
    {
        if (user.UserId is null || user.Role is null or < UserRole.Supervisor) throw new InsufficientAuthorityException();
    }

    internal static async Task<(ClaimReserveComponent Component, ReserveOperationContext Context)> Load(
        IClaimRepository claims, IReserveComponentRepository reserves, Guid claimId, Guid transactionId, CancellationToken token)
    {
        var claim = await ClaimHandlerSupport.Load(claims, claimId, token);
        var components = await reserves.GetForClaimAsync(claimId, token);
        var component = components.SingleOrDefault(x => x.ReserveHistory.Any(h => h.Id == transactionId))
            ?? throw new NotFoundException("Reserve transaction was not found on this claim.");
        return (component, new(claim, components));
    }
}
