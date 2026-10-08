using ClaimsModule.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;

namespace ClaimsModule.API;

public static class BackgroundJobs
{
    public static IServiceCollection AddClaimsBackgroundJobs(this IServiceCollection services, bool testing)
    {
        services.AddHangfire((provider, configuration) => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"),
                new SqlServerStorageOptions
                {
                    SchemaName = "HangFire",
                    PrepareSchemaIfNecessary = !testing,
                    TryAutoDetectSchemaDependentOptions = !testing,
                    UseRecommendedIsolationLevel = true,
                })
            .UseFilter(provider.GetRequiredService<GlPostingFailureFilter>()));
        // Test hosts call Execute directly without starting a worker or requiring Hangfire storage.
        if (!testing)
        {
            services.AddHangfireServer();
            services.AddHostedService<RecurringJobsRegistrationService>();
        }
        return services;
    }

    public static IApplicationBuilder UseClaimsHangfireDashboard(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Hangfire:DashboardEnabled")) return app;
        return app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new ManagerDashboardAuthorizationFilter()],
        });
    }

    private sealed class RecurringJobsRegistrationService(
        IServiceProvider services, ILogger<RecurringJobsRegistrationService> logger) : BackgroundService
    {
        private static readonly Action<ILogger, Exception?> Registered = LoggerMessage.Define(
            LogLevel.Information, new EventId(2101, "HangfireRecurringJobsRegistered"),
            "Hangfire recurring jobs registered.");
        private static readonly Action<ILogger, Exception?> RegistrationFailed = LoggerMessage.Define(
            LogLevel.Warning, new EventId(2102, "HangfireRecurringJobsRegistrationFailed"),
            "Hangfire recurring-job registration failed; retrying in 15 seconds while the database becomes available.");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var manager = services.GetRequiredService<IRecurringJobManager>();
                    manager.AddOrUpdate<SlaMonitoringJob>(nameof(SlaMonitoringJob),
                        job => job.Execute(CancellationToken.None), "*/15 * * * *",
                        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
                    manager.AddOrUpdate<GlPostingRecoveryJob>(nameof(GlPostingRecoveryJob),
                        job => job.Execute(CancellationToken.None), "*/5 * * * *",
                        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
                    Registered(logger, null);
                    return;
                }
                catch (Exception exception)
                {
                    RegistrationFailed(logger, exception);
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
            }
        }
    }

    private sealed class ManagerDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) =>
            context.GetHttpContext().User.Identity?.IsAuthenticated == true
            && context.GetHttpContext().User.IsInRole("Manager");
    }
}
