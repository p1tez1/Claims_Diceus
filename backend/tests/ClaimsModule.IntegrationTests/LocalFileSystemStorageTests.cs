using ClaimsModule.Application.Storage;
using ClaimsModule.Infrastructure.Storage;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class LocalFileSystemStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claims-local-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UploadUsesSafeTenantClaimPathCollisionSuffixAndLocalUrl()
    {
        var service = Create();
        var organisationId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        await using var firstContent = new MemoryStream("first"u8.ToArray());
        await using var secondContent = new MemoryStream("second"u8.ToArray());

        var first = await service.UploadAsync(organisationId, claimId, "../../evidence.txt", "text/plain", firstContent);
        var second = await service.UploadAsync(organisationId, claimId, "evidence.txt", "text/plain", secondContent);

        first.FileName.ShouldBe("evidence.txt");
        second.FileName.ShouldBe("evidence (1).txt");
        (await service.ExistsAsync(first.Path)).ShouldBeTrue();
        (await service.GetDownloadUrlAsync(first.Path, TimeSpan.FromHours(1)))
            .ShouldBe($"/api/storage/local/{organisationId:D}/{claimId:D}/evidence.txt");
        await using var read = await service.OpenReadAsync(first.Path);
        using var reader = new StreamReader(read);
        (await reader.ReadToEndAsync()).ShouldBe("first");
    }

    [Fact]
    public async Task PdfCanBeUploadedAndReadBackLocally()
    {
        var service = Create();
        var bytes = "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF"u8.ToArray();
        await using var content = new MemoryStream(bytes);
        var stored = await service.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), "manual-check.pdf", "application/pdf", content);
        stored.FileName.ShouldBe("manual-check.pdf");
        await using var read = await service.OpenReadAsync(stored.Path);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        copy.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task CopyFailureIsNotRetriedAsCollisionAndLeavesNoPartialFile()
    {
        var service = Create();
        await using var content = new FailingStream();
        var error = await Should.ThrowAsync<IOException>(() => service.UploadAsync(Guid.NewGuid(), Guid.NewGuid(),
            "partial.txt", "text/plain", content));
        error.Message.ShouldBe("Input failed");
        content.Reads.ShouldBe(1);
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    private sealed class FailingStream : MemoryStream
    {
        public int Reads { get; private set; }
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (++Reads == 1)
            {
                destination.WriteByte(1);
                throw new IOException("Input failed");
            }
            throw new InvalidOperationException("Copy failure was incorrectly retried");
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("safe/../../outside.txt")]
    [InlineData("C:\\outside.txt")]
    public async Task DownloadPathTraversalIsRejected(string path)
    {
        var service = Create();
        await Should.ThrowAsync<InvalidOperationException>(() => service.OpenReadAsync(path));
    }

    private LocalFileSystemStorageService Create() => new(new StorageOptions
    {
        Provider = "LocalFileSystem",
        Local = new() { RootPath = _root },
    });

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
