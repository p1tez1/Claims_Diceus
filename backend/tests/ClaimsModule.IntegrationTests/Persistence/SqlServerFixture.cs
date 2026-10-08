using System.Diagnostics;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ClaimsModule.IntegrationTests.Persistence;

public sealed class SqlServerFactAttribute : FactAttribute
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

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLAIMS_TEST_SQLSERVER")) && !DockerAvailable.Value)
            Skip = "Real SQL Server required: set CLAIMS_TEST_SQLSERVER or start Docker for Testcontainers.MsSql.";
    }
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public string ConnectionString { get; private set; } = string.Empty;
    public TestClock Clock { get; } = new();
    public TestUser User { get; } = new();
    private readonly string _databaseName = "ClaimsModule_Tests_" + Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("CLAIMS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connection))
        {
            if (new SqlServerFactAttribute().Skip is not null) return;
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            connection = _container.GetConnectionString();
        }
        ConnectionString = new SqlConnectionStringBuilder(connection) { InitialCatalog = _databaseName }.ConnectionString;
        await using var db = Create();
        await db.Database.MigrateAsync();
    }

    public ClaimsDbContext Create(TestUser? user = null, TestClock? clock = null) => new(
        new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(ConnectionString).Options, user ?? User, clock ?? Clock);

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(ConnectionString))
        {
            var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            if (database != _databaseName || !database.StartsWith("ClaimsModule_Tests_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a database not owned by this test fixture.");
            await using var db = Create();
            await db.Database.EnsureDeletedAsync();
        }
        if (_container is not null) await _container.DisposeAsync();
    }
}

public sealed record TestUser : ICurrentUser
{
    public Guid? UserId { get; init; } = SeedConstants.HandlerId;
    public string Name => "Test handler";
    public UserRole? Role => UserRole.Handler;
    public Guid OrganisationId { get; init; } = SeedConstants.OrganisationId;
    public Guid? CorrelationId { get; init; } = Guid.NewGuid();
}

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

internal sealed class TestDispatcher(Func<IReadOnlyList<IDomainEvent>, CancellationToken, Task>? dispatch = null) : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) =>
        dispatch?.Invoke(events, cancellationToken) ?? Task.CompletedTask;
}
