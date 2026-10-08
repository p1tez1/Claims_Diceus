namespace ClaimsModule.Application.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const long DefaultMaxFileSizeBytes = 50L * 1024 * 1024;

    public string Provider { get; set; } = "LocalFileSystem";
    public AzureStorageOptions Azure { get; set; } = new();
    public LocalStorageOptions Local { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;
}

public sealed class AzureStorageOptions
{
    public string? ConnectionString { get; set; }
    public string Container { get; set; } = "claim-documents";
    // Optional browser-reachable origin for an emulator/reverse proxy. Preserve the signed path/query.
    public string? PublicDownloadOrigin { get; set; }
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "uploads";
}
