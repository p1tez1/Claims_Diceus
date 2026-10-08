using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimsModule.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ReferencePolicySqlApiTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ReferenceAndPolicyEndpointsReturnSeededAndFilteredData()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        var codes = await GetJsonAsync<List<CauseDto>>(client, "/api/reference/cause-of-loss-codes");
        codes.Count.ShouldBe(10);
        codes.Select(x => x.SortOrder).ShouldBe(codes.Select(x => x.SortOrder).Order());

        var autoCodes = await GetJsonAsync<List<CauseDto>>(client, "/api/reference/cause-of-loss-codes?perilCategory=Auto");
        autoCodes.Select(x => x.Code).ShouldBe(["COL-VEH-COL", "COL-VEH-COMP"]);

        var statuses = await GetJsonAsync<List<StatusDto>>(client, "/api/reference/claim-statuses");
        statuses.ShouldNotBeEmpty();
        statuses.SelectMany(x => x.Transitions).Select(x => x.RequiredRole).ShouldContain("Handler");

        var users = await GetJsonAsync<List<UserDto>>(client, "/api/reference/users?role=Handler");
        users.ShouldHaveSingleItem().Name.ShouldBe("Claims Handler");

        var allPolicies = await GetJsonAsync<List<PolicyDto>>(client, "/api/policies/search?q=");
        allPolicies.Count.ShouldBe(5);
        var numberMatch = await GetJsonAsync<List<PolicyDto>>(client, "/api/policies/search?q=2025-002001");
        numberMatch.ShouldHaveSingleItem().PolicyNumber.ShouldBe("POL-2025-002001");
        var nameMatch = await GetJsonAsync<List<PolicyDto>>(client, "/api/policies/search?q=meridian");
        nameMatch.ShouldHaveSingleItem().ClientName.ShouldBe("Meridian Transport LLC");

        var coverage = await GetJsonAsync<List<string>>(client, $"/api/policies/{nameMatch.Single().Id}/coverage");
        coverage.ShouldBe(["Vehicle", "Cargo"]);
        (await client.GetAsync($"/api/policies/{Guid.NewGuid()}/coverage")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ReferenceAndPolicyEndpointsRequireAuthentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/reference/cause-of-loss-codes")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/policies/search?q=POL")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginDto>())!.AccessToken);
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url))!;

    private sealed record LoginDto(string AccessToken);
    private sealed record CauseDto(string Code, int SortOrder);
    private sealed record StatusDto(List<TransitionDto> Transitions);
    private sealed record TransitionDto(string? RequiredRole);
    private sealed record UserDto(string Name);
    private sealed record PolicyDto(Guid Id, string PolicyNumber, string ClientName);
}
