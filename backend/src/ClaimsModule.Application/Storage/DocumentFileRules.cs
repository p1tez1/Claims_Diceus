using FluentValidation;

namespace ClaimsModule.Application.Storage;

public static class DocumentFileRules
{
    private const int MaxFileNameLength = 255;
    private static readonly char[] UnsafeFileNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
    };

    public static string SanitizeFileName(string fileName)
    {
        var leaf = (fileName ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        leaf = new string(leaf.Where(c => !char.IsControl(c) && !UnsafeFileNameCharacters.Contains(c)).ToArray());
        while (leaf.Contains("..", StringComparison.Ordinal)) leaf = leaf.Replace("..", ".", StringComparison.Ordinal);
        leaf = leaf.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(leaf)) leaf = "file";

        var extension = Path.GetExtension(leaf);
        var stem = Path.GetFileNameWithoutExtension(leaf);
        var available = Math.Max(1, MaxFileNameLength - extension.Length);
        if (stem.Length > available) stem = stem[..available];
        return stem + extension;
    }

    public static string WithNumericSuffix(string sanitizedFileName, int suffix)
    {
        if (suffix <= 0) return sanitizedFileName;
        var extension = Path.GetExtension(sanitizedFileName);
        var stem = Path.GetFileNameWithoutExtension(sanitizedFileName);
        var tail = $" ({suffix})";
        var available = Math.Max(1, MaxFileNameLength - extension.Length - tail.Length);
        if (stem.Length > available) stem = stem[..available];
        return stem + tail + extension;
    }

    public static bool IsAllowed(string fileName, string contentType) =>
        AllowedTypes.TryGetValue(Path.GetExtension(SanitizeFileName(fileName)), out var expected)
        && string.Equals(expected, contentType?.Split(';', 2)[0].Trim(), StringComparison.OrdinalIgnoreCase);

    public static async Task ValidateMagicBytesAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(SanitizeFileName(fileName)).ToLowerInvariant();
        byte[]? expected = extension switch
        {
            ".pdf" => "%PDF-"u8.ToArray(),
            ".png" => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
            ".jpg" or ".jpeg" => [0xff, 0xd8, 0xff],
            _ => null,
        };
        if (expected is null) return;
        if (!content.CanSeek) throw Invalid("File", "The uploaded stream must be seekable.");

        var original = content.Position;
        var actual = new byte[expected.Length];
        var read = await content.ReadAsync(actual, cancellationToken);
        content.Position = original;
        if (read != expected.Length || !actual.AsSpan().SequenceEqual(expected))
            throw Invalid("File", $"File content does not match the {extension.TrimStart('.').ToUpperInvariant()} extension.");
    }

    private static ValidationException Invalid(string field, string message) => new([new(field, message)]);
}
