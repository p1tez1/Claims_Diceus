using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Storage;

namespace ClaimsModule.Infrastructure.Storage;

public sealed class LocalFileSystemStorageService : IStorageService
{
    private readonly string _root;

    public LocalFileSystemStorageService(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _root = Path.GetFullPath(options.Local.RootPath, Directory.GetCurrentDirectory());
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> UploadAsync(Guid organisationId, Guid claimId, string fileName, string contentType,
        Stream content, CancellationToken cancellationToken = default)
    {
        var directory = ResolvePath($"{organisationId:D}/{claimId:D}");
        Directory.CreateDirectory(directory);
        var sanitized = DocumentFileRules.SanitizeFileName(fileName);
        for (var suffix = 0; suffix < int.MaxValue; suffix++)
        {
            var candidate = DocumentFileRules.WithNumericSuffix(sanitized, suffix);
            var storagePath = $"{organisationId:D}/{claimId:D}/{candidate}";
            var fullPath = ResolvePath(storagePath);
            FileStream target;
            try
            {
                target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
            }
            catch (IOException) when (File.Exists(fullPath)) { continue; }
            try
            {
                await using (target) await content.CopyToAsync(target, cancellationToken);
                return new(storagePath, candidate);
            }
            catch
            {
                File.Delete(fullPath);
                throw;
            }
        }
        throw new IOException("Could not allocate a unique document filename.");
    }

    public Task<string> GetDownloadUrlAsync(string path, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _ = ResolvePath(path);
        var encoded = string.Join('/', NormalizeSegments(path).Select(Uri.EscapeDataString));
        return Task.FromResult($"/api/storage/local/{encoded}");
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(path)));

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        File.Delete(ResolvePath(path));
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        Stream stream = new FileStream(ResolvePath(path), FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    private string ResolvePath(string path)
    {
        var segments = NormalizeSegments(path);
        var fullPath = Path.GetFullPath(Path.Combine([_root, .. segments]));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, _root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage path escapes the configured local root.");
        return fullPath;
    }

    private static string[] NormalizeSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new InvalidOperationException("Storage path must be relative.");
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(x => x is "." or ".." || x.Contains("..", StringComparison.Ordinal)
            || x.Any(char.IsControl) || x.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0))
            throw new InvalidOperationException("Storage path contains unsafe segments.");
        return segments;
    }
}
