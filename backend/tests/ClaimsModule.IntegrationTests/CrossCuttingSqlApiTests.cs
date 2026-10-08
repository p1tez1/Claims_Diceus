using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class CrossCuttingSqlApiTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task AuditReceivesCorrelationAndIdempotencyReplaysOrRejectsMismatch()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = fixture.ConnectionString,
                ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
                ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
                ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
                ["Auth:DevLoginEnabled"] = "true"
            }));
        });
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var correlation = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlation.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var first = await client.PostAsJsonAsync("/__test/claim-audit", new { alpha = 1, beta = 2 });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = (await first.Content.ReadFromJsonAsync<ClaimBody>())!.ClaimId;
        var replay = await client.PostAsJsonAsync("/__test/claim-audit", new { beta = 2, alpha = 1 });
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<ClaimBody>())!.ClaimId.ShouldBe(created);
        var mismatch = await client.PostAsJsonAsync("/__test/claim-audit", new { alpha = 3, beta = 2 });
        mismatch.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await using var db = fixture.Create();
        (await db.Claims.CountAsync(x => x.Id == created)).ShouldBe(1);
        var audit = await db.ClaimAuditLog.Where(x => x.ClaimId == created && x.EventType == "CLAIM_CREATED").SingleAsync();
        audit.CorrelationId.ShouldBe(correlation);
    }

    private sealed record LoginBody(string AccessToken);
    private sealed record ClaimBody(Guid ClaimId);
}
