using ClaimsModule.Application.Abstractions;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.Jobs;

public sealed class GlPostingScheduler(IUnitOfWork unit, IBackgroundJobClient client) : IGlPostingScheduler
{
    public void Enqueue(Guid reserveHistoryId, Guid claimId, string idempotencyKey) =>
        unit.EnqueuePostCommitAction(_ =>
        {
            client.Enqueue<PostGLReserveChangeJob>(job => job.Execute(reserveHistoryId, claimId, idempotencyKey, null!, CancellationToken.None));
            return Task.CompletedTask;
        });
}

public sealed class PostGLReserveChangeJob(IBackgroundJobStore store, ILogger<PostGLReserveChangeJob> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> Skipped = LoggerMessage.Define<Guid>(LogLevel.Information,
        new EventId(1, "GlPostingSkipped"), "GL posting skipped for reserve {ReserveHistoryId}: missing, mismatched, unapproved or already posted");
    public async Task Execute(Guid reserveHistoryId, Guid claimId, string idempotencyKey,
        PerformContext? context = null, CancellationToken cancellationToken = default)
    {
        var changed = await store.PostAsync(new(reserveHistoryId, claimId, idempotencyKey),
            context?.BackgroundJob.Id ?? $"direct:{reserveHistoryId:D}", cancellationToken);
        if (!changed) Skipped(logger, reserveHistoryId, null);
    }
}

public sealed class GlPostingRecoveryJob(IBackgroundJobStore store, IBackgroundJobClient client)
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        foreach (var work in await store.FindUnscheduledAsync(cancellationToken))
            client.Enqueue<PostGLReserveChangeJob>(job => job.Execute(work.ReserveHistoryId, work.ClaimId,
                work.IdempotencyKey, null!, CancellationToken.None));
    }
}
