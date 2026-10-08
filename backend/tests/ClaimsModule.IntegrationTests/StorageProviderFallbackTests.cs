using ClaimsModule.Application.Abstractions;
using ClaimsModule.Infrastructure;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class StorageProviderFallbackTests
{
    [Fact]
    [Trait("Category", "AzureBlob")]
    public void UnconfiguredAzureBlobProviderFallsBackToLocalFileSystemWithWarning()
    {
        var root = Path.Combine(Path.GetTempPath(), "claims-azure-fallback-tests", Guid.NewGuid().ToString("N"));
        var logs = new RecordingLoggerProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "AzureBlob",
            ["Storage:Local:RootPath"] = root,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddInfrastructure(configuration);

        using (var provider = services.BuildServiceProvider())
            provider.GetRequiredService<IStorageService>().ShouldBeOfType<LocalFileSystemStorageService>();

        logs.Messages.ShouldContain(x => x.Level == LogLevel.Warning
            && x.Message.Contains("using local filesystem storage", StringComparison.Ordinal));
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }
    }

    private sealed class RecordingLogger(List<(LogLevel Level, string Message)> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Add((logLevel, formatter(state, exception)));
    }
}
