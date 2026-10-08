using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ClaimsModule.API;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class CrossCuttingApiTests
{
    private const string SigningKey = "test-only-signing-key-with-more-than-32-characters";
    private static readonly string[] ExpectedSwaggerTags = ["Auth", "Claims", "Policies", "Reference", "Reserves", "System"];

    private static WebApplicationFactory<Program> Factory(bool devLogin) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = SigningKey,
                ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
                ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
                ["Auth:DevLoginEnabled"] = devLogin.ToString()
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserQueries>();
                services.AddSingleton<IUserQueries, FakeUsers>();
            });
        });

    [Theory]
    [InlineData("validation", HttpStatusCode.UnprocessableEntity, "ValidationError")]
    [InlineData("domain", HttpStatusCode.UnprocessableEntity, "ValidationError")]
    [InlineData("self", HttpStatusCode.UnprocessableEntity, "ValidationError")]
    [InlineData("authority", HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData("missing", HttpStatusCode.NotFound, "NotFound")]
    [InlineData("conflict", HttpStatusCode.Conflict, "ConcurrencyError")]
    [InlineData("unknown", HttpStatusCode.InternalServerError, "ServerError")]
    public async Task ErrorsUseSharedContract(string kind, HttpStatusCode status, string type)
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/__test/error/{kind}");
        response.StatusCode.ShouldBe(status);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        body.ShouldNotBeNull();
        body.Type.ShouldBe(type);
        body.Status.ShouldBe((int)status);
        body.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        if (kind == "validation") body.Errors["Field"].ShouldContain("Invalid value");
        if (kind == "unknown") body.Title.ShouldNotContain("Secret details");
    }

    [Fact]
    public async Task RolesAndCorrelationIdFlowFromToken()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var unauthenticated = await client.GetAsync("/__test/supervisor");
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await unauthenticated.Content.ReadFromJsonAsync<ErrorBody>())!.Type.ShouldBe("Unauthorized");
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var denied = await client.GetAsync("/__test/supervisor");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, denied.Headers.WwwAuthenticate.ToString());
        (await denied.Content.ReadFromJsonAsync<ErrorBody>())!.Type.ShouldBe("Forbidden");
        var correlation = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlation.ToString());
        var response = await client.GetAsync("/__test/current");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(correlation.ToString());
        (await response.Content.ReadFromJsonAsync<CurrentBody>())!.CorrelationId.ShouldBe(correlation);

        var supervisor = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "supervisor" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await supervisor.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        (await client.GetAsync("/__test/supervisor")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/__test/manager")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DevLoginCanBeDisabled()
    {
        using var factory = Factory(false);
        using var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MalformedJsonReturnsBadRequestContract()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/auth/dev-login", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Type.ShouldBe("BadRequest");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"lossDate\":\"invalid-date\"}")]
    [InlineData("{\"severity\":\"NotASeverity\"}")]
    public async Task ControllerBindingErrorsUseSharedContract(string json)
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        using var response = await client.PostAsync("/api/claims", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = (await response.Content.ReadFromJsonAsync<ErrorBody>())!;
        body.Type.ShouldBe("BadRequest");
        body.Status.ShouldBe(400);
        body.Errors.ShouldNotBeEmpty();
        body.CorrelationId.ShouldBe(response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task MissingMultipartFileUsesSharedBadRequestContract()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Other"), "documentType");
        var response = await client.PostAsync($"/api/claims/{Guid.NewGuid()}/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Type.ShouldBe("BadRequest");
    }

    [Theory]
    [InlineData("DELETE", HttpStatusCode.MethodNotAllowed, "MethodNotAllowed")]
    [InlineData("POST", HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType")]
    public async Task TransportErrorsUseSharedContract(string method, HttpStatusCode status, string type)
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginBody>())!.AccessToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/claims")
        {
            Content = new StringContent("plain text", System.Text.Encoding.UTF8, "text/plain")
        };
        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(status);
        var body = (await response.Content.ReadFromJsonAsync<ErrorBody>())!;
        body.Type.ShouldBe(type);
        body.Status.ShouldBe((int)status);
        body.Errors.ShouldNotBeNull();
        body.CorrelationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("                                ")]
    [InlineData("__SET_VIA_ENVIRONMENT_MINIMUM_32_CHARACTERS__")]
    [InlineData("__SET_VIA_USER_SECRETS_MINIMUM_32_CHARACTERS__")]
    public void BearerAndTokenCreationRejectUnsetSigningKeys(string? signingKey)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Jwt:SigningKey"] = signingKey,
            ["Auth:Jwt:Issuer"] = "test",
            ["Auth:Jwt:Audience"] = "test"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddClaimsAuthentication();
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme));
        Should.Throw<InvalidOperationException>(() => Authentication.CreateToken(
            new(Guid.NewGuid(), "handler", "Handler", UserRole.Handler), Guid.NewGuid(), config));
    }

    [Fact]
    public async Task SwaggerPublishesSchemasExamplesRolesMultipartTagsAndJwtResponses()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        (await client.GetAsync("/swagger/index.html")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var create = paths.GetProperty("/api/claims").GetProperty("post");
        create.GetProperty("summary").GetString()!.ShouldContain("FNOL");
        create.GetProperty("description").GetString()!.ShouldContain("HandlerOrAbove");
        create.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .TryGetProperty("example", out _).ShouldBeTrue();
        create.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json")
            .TryGetProperty("example", out _).ShouldBeTrue();
        foreach (var status in new[] { "201", "401", "403", "409", "422" })
            create.GetProperty("responses").TryGetProperty(status, out _).ShouldBeTrue(status);
        paths.GetProperty("/api/claims/{id}").GetProperty("get").GetProperty("responses")
            .TryGetProperty("404", out _).ShouldBeTrue();

        var submit = paths.GetProperty("/api/claims/{id}/reserves").GetProperty("post");
        submit.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .TryGetProperty("example", out _).ShouldBeTrue();
        submit.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json")
            .TryGetProperty("example", out _).ShouldBeTrue();
        var upload = paths.GetProperty("/api/claims/{id}/documents").GetProperty("post");
        upload.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _).ShouldBeTrue();
        root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer")
            .GetProperty("scheme").GetString().ShouldBe("bearer");
        root.GetProperty("security")[0].TryGetProperty("Bearer", out _).ShouldBeTrue();
        create.GetProperty("tags")[0].GetString().ShouldBe("Claims");
        submit.GetProperty("tags")[0].GetString().ShouldBe("Reserves");
        var tags = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject())
            .Where(operation => operation.Value.TryGetProperty("tags", out _))
            .SelectMany(operation => operation.Value.GetProperty("tags").EnumerateArray())
            .Select(tag => tag.GetString()).Where(tag => tag is not null).ToHashSet();
        ExpectedSwaggerTags.Except(tags).ShouldBeEmpty();
        root.GetProperty("components").GetProperty("schemas").TryGetProperty("CreateClaimCommand", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/api/policies/search", "get", "PolicyDto")]
    [InlineData("/api/policies/{id}/coverage", "get", "string")]
    [InlineData("/api/reference/cause-of-loss-codes", "get", "CauseOfLossDto")]
    [InlineData("/api/reference/claim-statuses", "get", "ClaimStatusDto")]
    [InlineData("/api/reference/users", "get", "UserDto")]
    [InlineData("/api/claims/{id}/status", "put", "TransitionStatusResponse")]
    public async Task F17SwaggerDescribesActualSuccessPayloads(string path, string method, string payload)
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        var success = operation.GetProperty("responses").GetProperty("200");
        success.TryGetProperty("content", out var content).ShouldBeTrue($"{method} {path} must describe its response body");
        var schema = content.GetProperty("application/json").GetProperty("schema");
        if (payload == "TransitionStatusResponse")
        {
            schema.GetProperty("$ref").GetString().ShouldEndWith("/" + payload);
            document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(payload)
                .GetProperty("properties").TryGetProperty("status", out _).ShouldBeTrue();
        }
        else
        {
            schema.GetProperty("type").GetString().ShouldBe("array");
            var item = schema.GetProperty("items");
            if (payload == "string") item.GetProperty("type").GetString().ShouldBe("string");
            else item.GetProperty("$ref").GetString().ShouldEndWith("/" + payload);
        }
        operation.GetProperty("responses").TryGetProperty("401", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task StructuredLogContextContainsCorrelationIdAndAuthenticatedUserId()
    {
        var sink = new CollectingSink();
        var original = Log.Logger;
        Log.Logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var correlation = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-Id"] = correlation.ToString("D");
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString("D"))], "test"));
            var userMiddleware = new UserLogContextMiddleware(_ =>
            {
                Log.Information("Structured request test");
                return Task.CompletedTask;
            });
            var correlationMiddleware = new CorrelationIdMiddleware(ctx => userMiddleware.InvokeAsync(ctx));
            await correlationMiddleware.InvokeAsync(context);

            var logEvent = sink.Events.ShouldHaveSingleItem();
            logEvent.Properties["CorrelationId"].ToString().ShouldContain(correlation.ToString("D"));
            logEvent.Properties["UserId"].ToString().ShouldContain(userId.ToString("D"));
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            Log.Logger = original;
        }
    }

    private sealed record ErrorBody(string Type, string Title, int Status, Dictionary<string, string[]> Errors, string CorrelationId);
    private sealed record LoginBody(string AccessToken);
    private sealed record CurrentBody(Guid? CorrelationId);
    private sealed class FakeUsers : IUserQueries
    {
        public Task<IReadOnlyList<UserDto>> ListAsync(UserRole? role = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserDto>>([]);
        public Task<UserDto?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            UserDto? user = username switch
            {
                "handler" => new(SeedConstants.HandlerId, "handler", "Claims Handler", UserRole.Handler),
                "supervisor" => new(SeedConstants.SupervisorId, "supervisor", "Claims Supervisor", UserRole.Supervisor),
                "manager" => new(SeedConstants.ManagerId, "manager", "Claims Manager", UserRole.Manager),
                _ => null
            };
            return Task.FromResult(user);
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
