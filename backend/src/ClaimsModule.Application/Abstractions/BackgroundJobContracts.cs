namespace ClaimsModule.Application.Abstractions;

public interface IGlPostingScheduler
{
    void Enqueue(Guid reserveHistoryId, Guid claimId, string idempotencyKey);
}

public sealed record GlPostingWork(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey);
public sealed record SlaWork(Guid ClaimId, Guid OrganisationId);

/// <summary>Each operation owns a transaction and derives an explicit background tenant from stored data.</summary>
public interface IBackgroundJobStore
{
    Task<bool> PostAsync(GlPostingWork work, string jobId, CancellationToken cancellationToken = default);
    Task<bool> FailAsync(GlPostingWork work, string jobId, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPostingWork>> FindUnscheduledAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlaWork>> FindSlaCandidatesAsync(CancellationToken cancellationToken = default);
    Task<bool> FlagSlaAsync(SlaWork work, CancellationToken cancellationToken = default);
}
