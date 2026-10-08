using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class DocumentSqlApiTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "claims-document-api-tests", Guid.NewGuid().ToString("N"));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task UploadSanitizesNameAndListReturnsUploaderUrlAndAudit()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await CreateClaim(client);

        using var response = await Upload(client, claim.Id, "../../police-report.pdf", "application/pdf",
            "%PDF-1.4\n%%EOF"u8.ToArray(), "PoliceReport", "Incident evidence");
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var uploaded = (await response.Content.ReadFromJsonAsync<DocumentDto>(Json))!;
        uploaded.DocumentName.ShouldBe("police-report.pdf");
        uploaded.DownloadUrl.ShouldStartWith("/api/storage/local/");
        uploaded.UploaderName.ShouldBe("Claims Handler");

        var listed = await client.GetFromJsonAsync<DocumentDto[]>($"/api/claims/{claim.Id}/documents", Json);
        listed.ShouldHaveSingleItem().DownloadUrl.ShouldStartWith("/api/storage/local/");
        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.Id}", Json);
        detail!.Documents.ShouldHaveSingleItem().DownloadUrl.ShouldNotBeNullOrWhiteSpace();
        var audit = detail.Audit.Single(x => x.EventType == "DOCUMENT_UPLOADED");
        audit.RelatedEntityId.ShouldBe(uploaded.Id);
        audit.RelatedEntityType.ShouldBe("ClaimDocument");
        detail.UpdatedAt.ShouldNotBeNull();
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task WrongMimeOversizeAndMissingClaimReturnExpectedValidation()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await CreateClaim(client);

        using var wrongMime = await Upload(client, claim.Id, "evidence.pdf", "text/plain", "%PDF-1.4"u8.ToArray(), "Other");
        wrongMime.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await wrongMime.Content.ReadAsStringAsync());
        using var tooLarge = await Upload(client, claim.Id, "notes.txt", "text/plain", new byte[33], "Other");
        tooLarge.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await tooLarge.Content.ReadAsStringAsync());
        using var missing = await Upload(client, Guid.NewGuid(), "evidence.pdf", "application/pdf", "%PDF-1.4"u8.ToArray(), "Other");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound, await missing.Content.ReadAsStringAsync());
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = fixture.ConnectionString,
            ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
            ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
            ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
            ["Auth:DevLoginEnabled"] = "true",
            ["Storage:Provider"] = "LocalFileSystem",
            ["Storage:Local:RootPath"] = _storageRoot,
            ["Storage:MaxFileSizeBytes"] = "32",
        }));
    });

    private static async Task Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static async Task<ClaimDetailDto> CreateClaim(HttpClient client)
    {
        var request = new CreateClaimCommand
        {
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A collision damaged the insured vehicle.",
            CauseOfLossCode = "COL-VEH-COL",
            Parties = [new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Ada", LastName = "Lovelace" }],
            RiskObjects = [new() { AssetType = AssetType.Vehicle, AssetDescription = "Insured vehicle", IsPrimary = true }],
        };
        var response = await client.PostAsJsonAsync("/api/claims", request, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>(Json))!;
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, Guid claimId, string fileName, string contentType,
        byte[] bytes, string documentType, string? notes = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(documentType), "documentType");
        if (notes is not null) form.Add(new StringContent(notes), "notes");
        return client.PostAsync($"/api/claims/{claimId}/documents", form);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }
}
