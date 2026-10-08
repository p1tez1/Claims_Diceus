using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using ClaimsModule.API;

namespace ClaimsModule.IntegrationTests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void ProductionRejectsMissingJwtSigningKey()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(Configuration(("Auth:Jwt:SigningKey", null)), Production()));

        exception.Message.ShouldContain("Auth:Jwt:SigningKey");
    }

    [Fact]
    public void ProductionRejectsMissingSqlConnectionString()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(Configuration(("ConnectionStrings:Default", null)), Production()));

        exception.Message.ShouldContain("ConnectionStrings:Default");
    }

    [Fact]
    public void ProductionRejectsMissingCorsOrigin()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(Configuration(("Cors:Origins:0", null)), Production()));

        exception.Message.ShouldContain("Cors:Origins");
    }

    [Fact]
    public void CorsOriginsAreReadFromIndexedConfigurationAndNormalized()
    {
        var configuration = Configuration(("Cors:Origins:0", "https://gray-water-0ee27680f.3.azurestaticapps.net/"));

        ProductionConfiguration.GetCorsOrigins(configuration).ShouldBe(
            ["https://gray-water-0ee27680f.3.azurestaticapps.net"]);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Auth:Jwt:SigningKey"] = "test-signing-key-with-at-least-thirty-two-bytes",
            ["ConnectionStrings:Default"] = "Server=sql;Database=claims;Authentication=Active Directory Managed Identity",
            ["Storage:Provider"] = "LocalFileSystem",
            ["Cors:Origins:0"] = "https://gray-water-0ee27680f.3.azurestaticapps.net"
        };
        foreach (var (key, value) in overrides)
            if (value is null) values.Remove(key); else values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static TestHostEnvironment Production() => new();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "ClaimsModule.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
