using ClaimsModule.Application.Abstractions;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure.Jobs;

/// <summary>Applied Failed is terminal: AutomaticRetry has already elected Scheduled for retryable failures.</summary>
public sealed class GlPostingFailureFilter(IServiceScopeFactory scopes) : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var job = context.BackgroundJob.Job;
        if (context.NewState is not FailedState failed || job.Type != typeof(PostGLReserveChangeJob)
            || job.Method.Name != nameof(PostGLReserveChangeJob.Execute)) return;
        HandleAsync(new((Guid)job.Args[0], (Guid)job.Args[1], (string)job.Args[2]),
            context.BackgroundJob.Id, string.IsNullOrWhiteSpace(failed.Exception.Message)
                ? failed.Exception.GetType().Name : failed.Exception.Message).GetAwaiter().GetResult();
    }

    public async Task HandleAsync(GlPostingWork work, string jobId, string reason)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>().FailAsync(work, jobId, reason);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}
