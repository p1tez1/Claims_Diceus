using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Storage;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    private static readonly Action<ILogger, Exception?> AzureFallbackWarning = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1001, "AzureStorageFallback"),
        "AzureBlob storage was selected but Storage:Azure:ConnectionString is not configured; using local filesystem storage.");

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGlPostingScheduler, Jobs.GlPostingScheduler>();
        services.AddScoped<Jobs.PostGLReserveChangeJob>();
        services.AddScoped<Jobs.SlaMonitoringJob>();
        services.AddScoped<Jobs.GlPostingRecoveryJob>();
        services.AddSingleton<Jobs.GlPostingFailureFilter>();
        // Read after the host has applied all configuration providers (including test/host overrides).
        services.AddSingleton(_ =>
        {
            var configuredMaximum = long.TryParse(configuration["Storage:MaxFileSizeBytes"], out var maximum) && maximum > 0
                ? maximum : StorageOptions.DefaultMaxFileSizeBytes;
            var options = new StorageOptions
            {
                Provider = configuration["Storage:Provider"] ?? "LocalFileSystem",
                MaxFileSizeBytes = configuredMaximum,
                Azure = new()
                {
                    ConnectionString = configuration["Storage:Azure:ConnectionString"],
                    Container = configuration["Storage:Azure:Container"] ?? "claim-documents",
                    PublicDownloadOrigin = configuration["Storage:Azure:PublicDownloadOrigin"],
                },
                Local = new() { RootPath = configuration["Storage:Local:RootPath"] ?? "uploads" },
            };
            return options;
        });
        services.AddSingleton<LocalFileSystemStorageService>();
        services.AddSingleton<AzureBlobStorageService>();
        services.AddSingleton<IStorageService>(provider =>
        {
            var options = provider.GetRequiredService<StorageOptions>();
            if (!options.Provider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
            {
                if (!options.Provider.Equals("LocalFileSystem", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Storage:Provider must be AzureBlob or LocalFileSystem.");
                return provider.GetRequiredService<LocalFileSystemStorageService>();
            }
            if (!string.IsNullOrWhiteSpace(options.Azure.ConnectionString)
                && !options.Azure.ConnectionString.StartsWith("__SET_", StringComparison.Ordinal))
                return provider.GetRequiredService<AzureBlobStorageService>();

            AzureFallbackWarning(provider.GetRequiredService<ILoggerFactory>().CreateLogger("Storage"), null);
            return provider.GetRequiredService<LocalFileSystemStorageService>();
        });
        return services;
    }
}
