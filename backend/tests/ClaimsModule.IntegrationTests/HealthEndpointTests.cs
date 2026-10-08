using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using ClaimsModule.IntegrationTests.Persistence;

namespace ClaimsModule.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters"
            }));
        }).CreateClient();
    }

    [Fact]
    public async Task GetHealthReturnsHealthyResponse()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        payload.ShouldNotBeNull();
        payload.Status.ShouldBe("Healthy");
    }

    private sealed record HealthResponse(string Status);
}

public sealed class ReadyHealthEndpointTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task GetReadyChecksDatabaseConnectivity()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = fixture.ConnectionString,
                ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters"
            }));
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<HealthResponse>())!.Status.ShouldBe("Ready");
    }

    private sealed record HealthResponse(string Status);
}
