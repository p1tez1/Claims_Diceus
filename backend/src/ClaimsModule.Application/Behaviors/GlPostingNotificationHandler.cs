using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Events;
using MediatR;

namespace ClaimsModule.Application.Behaviors;

public sealed class GlPostingNotificationHandler(IGlPostingScheduler scheduler) : INotificationHandler<DomainEventNotification>
{
    public Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        switch (notification.Event)
        {
            case ReserveAutoApproved e: scheduler.Enqueue(e.TransactionId, e.ClaimId, e.IdempotencyKey); break;
            case ReserveApproved e: scheduler.Enqueue(e.TransactionId, e.ClaimId, e.IdempotencyKey); break;
        }
        return Task.CompletedTask;
    }
}
