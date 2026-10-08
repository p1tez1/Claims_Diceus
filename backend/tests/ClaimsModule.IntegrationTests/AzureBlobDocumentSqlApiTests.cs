using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

[Collection(AzuriteTestGroup.Name)]
public sealed class AzureBlobDocumentSqlApiTests(AzuriteFixture azurite, SqlServerFixture sql)
    : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SqlServerAndAzuriteFact]
    [Trait("Category", "AzureBlob")]
    public async Task PostAndGetDocumentsUseAzuriteAndReturnWorkingDownloadUrl()
    {
        azurite.RequireAvailable();
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await CreateClaim(client);
        var expected = "end-to-end Azurite bytes"u8.ToArray();

        using var upload = await Upload(client, claim.Id, "../../evidence.txt", "text/plain", expected);
        upload.StatusCode.ShouldBe(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var created = (await upload.Content.ReadFromJsonAsync<DocumentDto>(Json))!;
        created.DocumentName.ShouldBe("evidence.txt");
        created.BlobPath.ShouldBe($"{SeedConstants.OrganisationId:D}/{claim.Id:D}/evidence.txt");

        var listed = (await client.GetFromJsonAsync<DocumentDto[]>($"/api/claims/{claim.Id}/documents", Json))!.ShouldHaveSingleItem();
        listed.DownloadUrl.ShouldNotBeNullOrWhiteSpace();
        using var download = await new HttpClient().GetAsync(listed.DownloadUrl);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(expected);
    }

    [SqlServerAndAzuriteFact]
    [Trait("Category", "AzureBlob")]
    public async Task WrongMimeAndOversizeAreRejectedWithoutCreatingDocuments()
    {
        azurite.RequireAvailable();
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await CreateClaim(client);

        using var wrongMime = await Upload(client, claim.Id, "evidence.pdf", "text/plain", "%PDF-1.4"u8.ToArray());
        wrongMime.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await wrongMime.Content.ReadAsStringAsync());
        using var oversize = await Upload(client, claim.Id, "notes.txt", "text/plain", new byte[33]);
        oversize.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await oversize.Content.ReadAsStringAsync());
        (await client.GetFromJsonAsync<DocumentDto[]>($"/api/claims/{claim.Id}/documents", Json)).ShouldBeEmpty();
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = sql.ConnectionString,
            ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
            ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
            ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
            ["Auth:DevLoginEnabled"] = "true",
            ["Storage:Provider"] = "AzureBlob",
            ["Storage:Azure:ConnectionString"] = azurite.ConnectionString,
            ["Storage:Azure:Container"] = "claim-documents",
            ["Storage:MaxFileSizeBytes"] = "32",
        }));
    });

    private static async Task Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new { username = "handler" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static async Task<ClaimDetailDto> CreateClaim(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand
        {
            LossDate = DateTimeOffset.UtcNow.AddDays(-1),
            LossDescription = "A collision damaged the insured vehicle.",
            CauseOfLossCode = "COL-VEH-COL",
            Parties = [new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Ada", LastName = "Lovelace" }],
            RiskObjects = [new() { AssetType = AssetType.Vehicle, AssetDescription = "Insured vehicle", IsPrimary = true }],
        }, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>(Json))!;
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, Guid claimId, string fileName, string contentType,
        byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Evidence"), "documentType");
        return client.PostAsync($"/api/claims/{claimId}/documents", form);
    }
}
