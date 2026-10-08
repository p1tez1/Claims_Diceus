using System.Net;
using ClaimsModule.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ProductionEndpointSafetyTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public void InvalidProductionConfigurationFailsFast()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));

        var exception = Should.Throw<InvalidOperationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("Production configuration is invalid");
        exception.Message.ShouldContain("Auth:Jwt:SigningKey");
    }

    [SqlServerFact]
    public async Task ValidProductionConfigurationStartsAndTestEndpointsAreAbsent()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Default", fixture.ConnectionString);
            builder.UseSetting("Auth:Jwt:SigningKey", "test-only-signing-key-with-more-than-32-characters");
            builder.UseSetting("Storage:Provider", "LocalFileSystem");
            builder.UseSetting("Cors:Origins:0", "https://gray-water-0ee27680f.3.azurestaticapps.net");
            builder.UseSetting("Auth:DevLoginEnabled", "false");
        });
        using var client = factory.CreateClient();

        (await client.GetAsync("/__test/current")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
