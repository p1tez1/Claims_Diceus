using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Storage;

namespace ClaimsModule.Infrastructure.Storage;

public sealed class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;
    private readonly Uri? _publicDownloadOrigin;

    public AzureBlobStorageService(StorageOptions options)
    {
        var connectionString = options.Azure.ConnectionString
            ?? throw new InvalidOperationException("Storage:Azure:ConnectionString is required.");
        _container = new BlobContainerClient(connectionString, options.Azure.Container);
        if (!string.IsNullOrWhiteSpace(options.Azure.PublicDownloadOrigin))
        {
            if (!Uri.TryCreate(options.Azure.PublicDownloadOrigin, UriKind.Absolute, out var origin) ||
                origin.Scheme is not ("http" or "https") || origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) || !string.IsNullOrEmpty(origin.UserInfo))
                throw new InvalidOperationException("Storage:Azure:PublicDownloadOrigin must be an HTTP(S) origin without a path, query, or credentials.");
            _publicDownloadOrigin = origin;
        }
    }

    public async Task<StoredFile> UploadAsync(Guid organisationId, Guid claimId, string fileName, string contentType,
        Stream content, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var sanitized = DocumentFileRules.SanitizeFileName(fileName);
        for (var suffix = 0; suffix < int.MaxValue; suffix++)
        {
            var candidate = DocumentFileRules.WithNumericSuffix(sanitized, suffix);
            var path = $"{organisationId:D}/{claimId:D}/{candidate}";
            var blob = _container.GetBlobClient(path);
            try
            {
                await blob.UploadAsync(content, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                }, cancellationToken);
                return new(path, candidate);
            }
            catch (RequestFailedException exception) when (exception.Status is 409 or 412)
            {
                if (content.CanSeek) content.Position = 0;
            }
        }
        throw new IOException("Could not allocate a unique document filename.");
    }

    public Task<string> GetDownloadUrlAsync(string path, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(NormalizePath(path));
        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("The configured Azure Blob credential cannot generate a shared-key SAS URL.");
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blob.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl),
        };
        sas.SetPermissions(BlobSasPermissions.Read);
        var url = blob.GenerateSasUri(sas);
        if (_publicDownloadOrigin is not null)
            url = new UriBuilder(url) { Scheme = _publicDownloadOrigin.Scheme, Host = _publicDownloadOrigin.Host, Port = _publicDownloadOrigin.Port }.Uri;
        return Task.FromResult(url.AbsoluteUri);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
        (await _container.GetBlobClient(NormalizePath(path)).ExistsAsync(cancellationToken)).Value;

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        await _container.GetBlobClient(NormalizePath(path)).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidOperationException("Blob path is unsafe.");
        return path;
    }
}
