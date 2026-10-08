using System.Globalization;
using System.Web;
using Azure.Storage;
using Azure.Storage.Sas;
using ClaimsModule.Application.Storage;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class BlobDownloadOriginTests
{
    private const string Account = "claimsdevstore";
    private const string Key = "Y2xhaW1zLW1vZHVsZS1henVyaXRlLWRldi1rZXktMjAyNg==";

    [Fact]
    public async Task F15BrowserDownloadOriginPreservesBlobPathAndValidReadOnlySignature()
    {
        var options = new StorageOptions
        {
            Azure = new()
            {
                ConnectionString = $"DefaultEndpointsProtocol=http;AccountName={Account};AccountKey={Key};BlobEndpoint=http://azurite:10000/{Account}",
            }
        };
        // Use configuration binding to exercise the optional setting even before the property exists.
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Azure:PublicDownloadOrigin"] = "http://localhost:10000",
        }).Build().Bind(options);
        var path = $"{Guid.NewGuid():D}/{Guid.NewGuid():D}/proof (1).pdf";
        var uri = new Uri(await new AzureBlobStorageService(options).GetDownloadUrlAsync(path, TimeSpan.FromHours(1)));
        uri.GetLeftPart(UriPartial.Authority).ShouldBe("http://localhost:10000");
        Uri.UnescapeDataString(uri.AbsolutePath).ShouldBe($"/{Account}/claim-documents/{path}");
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["sp"].ShouldBe("r");
        var expiry = DateTimeOffset.Parse(query["se"]!, CultureInfo.InvariantCulture);
        expiry.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
        var sas = new BlobSasBuilder { BlobContainerName = "claim-documents", BlobName = path, Resource = "b", ExpiresOn = expiry };
        sas.SetPermissions(BlobSasPermissions.Read);
        query["sig"].ShouldBe(sas.ToSasQueryParameters(new StorageSharedKeyCredential(Account, Key)).Signature);
    }
}
