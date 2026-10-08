using System.Diagnostics;
using Azure.Storage.Blobs;
using ClaimsModule.IntegrationTests.Persistence;
using Testcontainers.Azurite;

namespace ClaimsModule.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AzuriteTestGroup : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "Azurite storage";
}

public sealed class AzuriteFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(() =>
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null) return false;
            if (!process.WaitForExit(3000)) { process.Kill(); return false; }
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    });

    public AzuriteFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLAIMS_TEST_AZURE_BLOB")) && !DockerAvailable.Value)
            Skip = "Azurite required: set CLAIMS_TEST_AZURE_BLOB to a full Azurite connection string or start Docker for Testcontainers.Azurite.";
    }
}

public sealed class SqlServerAndAzuriteFactAttribute : FactAttribute
{
    public SqlServerAndAzuriteFactAttribute()
    {
        var sqlReason = new SqlServerFactAttribute().Skip;
        var azuriteReason = new AzuriteFactAttribute().Skip;
        Skip = sqlReason is null ? azuriteReason : azuriteReason is null ? sqlReason : $"{sqlReason} {azuriteReason}";
    }
}

public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/azure-storage/azurite:3.35.0";
    private AzuriteContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("CLAIMS_TEST_AZURE_BLOB");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            ValidateEmulatorConnection(configured);
            ConnectionString = configured;
            return;
        }

        if (new AzuriteFactAttribute().Skip is { } unavailable)
        {
            UnavailableReason = unavailable;
            return;
        }

        try
        {
            _container = new AzuriteBuilder(Image)
                .WithCommand("--skipApiVersionCheck")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            ValidateEmulatorConnection(ConnectionString);
        }
        catch (Exception)
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }

            UnavailableReason =
                "Azurite required: CLAIMS_TEST_AZURE_BLOB is not set and the Azurite Testcontainer could not start. Start Docker or supply a full Azurite connection string.";
        }
    }

    public void RequireAvailable()
    {
        if (UnavailableReason is not null) throw new InvalidOperationException(UnavailableReason);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private static void ValidateEmulatorConnection(string connectionString)
    {
        var client = new BlobServiceClient(connectionString);
        if (!connectionString.Contains("AccountName=devstoreaccount1", StringComparison.OrdinalIgnoreCase)
            || client.Uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CLAIMS_TEST_AZURE_BLOB must target Azurite with the public devstoreaccount1 development account; real Azure is not allowed in these tests.");
    }
}
