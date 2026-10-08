using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Jobs;

public sealed class SlaMonitoringJob(IBackgroundJobStore store)
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        foreach (var work in await store.FindSlaCandidatesAsync(cancellationToken))
            await store.FlagSlaAsync(work, cancellationToken);
    }
}
