using System.Globalization;
using System.Net;
using System.Web;
using ClaimsModule.Application.Storage;
using ClaimsModule.Infrastructure.Storage;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

[Collection(AzuriteTestGroup.Name)]
public sealed class AzureBlobStorageTests(AzuriteFixture fixture)
{
    [AzuriteFact]
    [Trait("Category", "AzureBlob")]
    public async Task UploadUsesSafeExpectedPathAndWorkingOneHourReadOnlySas()
    {
        fixture.RequireAvailable();
        var service = CreateService();
        var organisationId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var expected = "same bytes from Azurite"u8.ToArray();
        await using var content = new MemoryStream(expected);
        var stored = await service.UploadAsync(organisationId, claimId, "../../evidence.txt", "text/plain", content);

        try
        {
            stored.FileName.ShouldBe("evidence.txt");
            stored.Path.ShouldBe($"{organisationId:D}/{claimId:D}/evidence.txt");
            (await service.ExistsAsync(stored.Path)).ShouldBeTrue();

            var before = DateTimeOffset.UtcNow;
            var url = new Uri(await service.GetDownloadUrlAsync(stored.Path, TimeSpan.FromHours(1)));
            var expiry = DateTimeOffset.Parse(HttpUtility.ParseQueryString(url.Query)["se"]!, CultureInfo.InvariantCulture);
            expiry.ShouldBeInRange(before.AddMinutes(55), before.AddMinutes(65));
            HttpUtility.ParseQueryString(url.Query)["sp"].ShouldBe("r");

            using var response = await new HttpClient().GetAsync(url);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).ShouldBe(expected);
        }
        finally
        {
            await service.DeleteAsync(stored.Path);
        }
    }

    [AzuriteFact]
    [Trait("Category", "AzureBlob")]
    public async Task FilenameCollisionGetsNumericSuffix()
    {
        fixture.RequireAvailable();
        var service = CreateService();
        var organisationId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        await using var firstContent = new MemoryStream("first"u8.ToArray());
        await using var secondContent = new MemoryStream("second"u8.ToArray());
        var first = await service.UploadAsync(organisationId, claimId, "evidence.txt", "text/plain", firstContent);
        var second = await service.UploadAsync(organisationId, claimId, "evidence.txt", "text/plain", secondContent);

        try
        {
            first.FileName.ShouldBe("evidence.txt");
            second.FileName.ShouldBe("evidence (1).txt");
            second.Path.ShouldBe($"{organisationId:D}/{claimId:D}/evidence (1).txt");
        }
        finally
        {
            await service.DeleteAsync(first.Path);
            await service.DeleteAsync(second.Path);
        }
    }

    [AzuriteFact]
    [Trait("Category", "AzureBlob")]
    public async Task StoredPathTraversalIsRejected()
    {
        fixture.RequireAvailable();
        var service = CreateService();

        await Should.ThrowAsync<InvalidOperationException>(() => service.GetDownloadUrlAsync("org/../secret.txt", TimeSpan.FromHours(1)));
        await Should.ThrowAsync<InvalidOperationException>(() => service.ExistsAsync("org\\claim\\secret.txt"));
    }

    private AzureBlobStorageService CreateService() => new(new StorageOptions
    {
        Provider = "AzureBlob",
        Azure = new() { ConnectionString = fixture.ConnectionString, Container = "claim-documents" },
    });
}
