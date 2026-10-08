using ClaimsModule.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/storage/local")]
public sealed class LocalStorageController(LocalFileSystemStorageService storage, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("{**path}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Download(string path, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        try
        {
            var stream = await storage.OpenReadAsync(path, cancellationToken);
            return File(stream, ContentType(path), enableRangeProcessing: true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
        {
            return NotFound();
        }
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        _ => "application/octet-stream",
    };
}
