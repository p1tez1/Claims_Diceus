using ClaimsModule.IntegrationTests.Persistence;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class RuntimePermissionSqlTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task RuntimeRoleAllowsAuditAppendAndBlocksSqlUpdateAndDelete()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "backend", "ClaimsModule.sln"))) root = root.Parent;
        root.ShouldNotBeNull();
        var script = await File.ReadAllTextAsync(Path.Combine(root.FullName, "infra", "sql", "runtime-permissions.sql"));
        script = script.Replace("$(RuntimeUser)", "ClaimsCheckpointRuntime", StringComparison.Ordinal);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await Execute("CREATE USER [ClaimsCheckpointRuntime] WITHOUT LOGIN;");
        await Execute(script);
        await Execute(script); // Deployment permission setup is repeatable.
        await Execute("EXECUTE AS USER = 'ClaimsCheckpointRuntime';");
        try
        {
            await using var permissions = new SqlCommand("""
                SELECT HAS_PERMS_BY_NAME('dbo.ClaimAuditLog', 'OBJECT', 'INSERT'),
                       HAS_PERMS_BY_NAME('dbo.ClaimAuditLog', 'OBJECT', 'SELECT'),
                       HAS_PERMS_BY_NAME('dbo.ClaimAuditLog', 'OBJECT', 'UPDATE'),
                       HAS_PERMS_BY_NAME('dbo.ClaimAuditLog', 'OBJECT', 'DELETE'),
                       HAS_PERMS_BY_NAME('dbo.Claims', 'OBJECT', 'UPDATE'),
                       HAS_PERMS_BY_NAME('dbo.IdempotencyRecords', 'OBJECT', 'DELETE');
                """, connection);
            await using (var reader = await permissions.ExecuteReaderAsync())
            {
                (await reader.ReadAsync()).ShouldBeTrue();
                Enumerable.Range(0, 6).Select(reader.GetInt32).ShouldBe([1, 1, 0, 0, 1, 1]);
            }
            var update = await Should.ThrowAsync<SqlException>(() => Execute("UPDATE dbo.ClaimAuditLog SET Description = N'changed';"));
            update.Number.ShouldBe(229);
            var delete = await Should.ThrowAsync<SqlException>(() => Execute("DELETE FROM dbo.ClaimAuditLog;"));
            delete.Number.ShouldBe(229);
        }
        finally { await Execute("REVERT;"); }

        async Task Execute(string sql)
        {
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
