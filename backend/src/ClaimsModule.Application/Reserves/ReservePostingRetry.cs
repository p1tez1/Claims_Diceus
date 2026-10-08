using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Entities;
using MediatR;

namespace ClaimsModule.Application.Reserves;

// Retained as the existing posting service contract; RetryGlPostingCommand supplies the API path.
public sealed record ReserveRetryPostingCommand(Guid ClaimId, Guid ReserveHistoryId);

public sealed class ReservePostingRetry(IClaimRepository claims, IReserveComponentRepository reserves,
    IGlPostingScheduler scheduler, IClock clock)
{
    // Called inside the command's owning unit-of-work transaction.
    public async Task ExecuteAsync(ReserveRetryPostingCommand command, CancellationToken cancellationToken = default)
    {
        var claim = await claims.GetAsync(command.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim was not found.");
        var components = await reserves.GetForClaimAsync(command.ClaimId, cancellationToken);
        var component = components.SingleOrDefault(x => x.ReserveHistory.Any(h => h.Id == command.ReserveHistoryId))
            ?? throw new NotFoundException("Reserve transaction was not found.");
        component.RetryPosting(command.ReserveHistoryId, new ReserveOperationContext(claim, components), clock);
        var history = component.ReserveHistory.Single(x => x.Id == command.ReserveHistoryId);
        scheduler.Enqueue(history.Id, claim.Id, history.IdempotencyKey);
    }
}
