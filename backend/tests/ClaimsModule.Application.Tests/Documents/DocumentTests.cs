using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Documents;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Storage;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using FluentValidation;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests.Documents;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> UnsafeNames()
    {
        yield return ["../../x.pdf", "x.pdf"];
        yield return [@"C:\\temp\\report.pdf", "report.pdf"];
        yield return ["/var/tmp/report.pdf", "report.pdf"];
        yield return ["r\u00e9sum\u00e9.pdf", "r\u00e9sum\u00e9.pdf"];
        yield return [new string('a', 300) + ".pdf", new string('a', 251) + ".pdf"];
        yield return ["bad\0..name.pdf", "bad.name.pdf"];
    }

    [Theory]
    [MemberData(nameof(UnsafeNames))]
    public void FileNamesAreSanitized(string input, string expected) =>
        DocumentFileRules.SanitizeFileName(input).ShouldBe(expected);

    [Theory]
    [InlineData("report.pdf", "text/plain")]
    [InlineData("report.exe", "application/pdf")]
    [InlineData("report.png", "image/jpeg")]
    public async Task ValidatorRejectsMismatchedOrDisallowedTypes(string name, string contentType)
    {
        var validator = new UploadDocumentCommandValidator(new() { MaxFileSizeBytes = 10 });
        var command = new UploadDocumentCommand(Guid.NewGuid(), name, contentType, 5,
            new MemoryStream(new byte[5]), "Other", null);
        var result = await validator.ValidateAsync(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == "FileName");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task ValidatorRejectsEmptyAndOversizeFiles(long length)
    {
        var validator = new UploadDocumentCommandValidator(new() { MaxFileSizeBytes = 10 });
        var command = new UploadDocumentCommand(Guid.NewGuid(), "report.pdf", "application/pdf", length,
            new MemoryStream(), "Other", null);
        var result = await validator.ValidateAsync(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == "FileSizeBytes");
    }

    [Fact]
    public async Task UploadStoresMetadataTouchesClaimAndAuditsDocumentId()
    {
        var claimId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claim = Claim.Create(claimId, ClaimNumber.Create(2026, 1), userId,
            LossEvent.Create(Guid.NewGuid(), claimId, Now.AddDays(-1), "A documented collision damaged the vehicle.",
                "COL-VEH-COL", true, new TestClock()),
            [ClaimParty.Create(Guid.NewGuid(), claimId, PartyRole.Claimant, PartyType.Person, "Ada", "Lovelace")], new TestClock());
        claim.ClearDomainEvents();
        var revision = claim.LastActivity!.Revision;
        var claims = Substitute.For<IClaimRepository>();
        claims.GetAsync(claimId, Arg.Any<CancellationToken>()).Returns(claim);
        var documents = Substitute.For<IDocumentRepository>();
        var storage = Substitute.For<IStorageService>();
        storage.UploadAsync(Arg.Any<Guid>(), claimId, "report.pdf", "application/pdf", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new StoredFile($"org/{claimId}/report.pdf", "report.pdf"));
        storage.GetDownloadUrlAsync(Arg.Any<string>(), TimeSpan.FromHours(1), Arg.Any<CancellationToken>()).Returns("https://download.test/report");
        var ids = Substitute.For<IIdentifierGenerator>();
        ids.NewId().Returns(documentId);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId);
        user.Name.Returns("Ada Handler");
        user.OrganisationId.Returns(Guid.NewGuid());
        var audit = Substitute.For<IAuditLogService>();
        await using var content = new MemoryStream("%PDF-1.4\n%%EOF"u8.ToArray());

        var result = await new UploadDocumentCommandHandler(claims, documents, storage, ids, new TestClock(), user, audit)
            .Handle(new(claimId, "report.pdf", "application/pdf", content.Length, content, "PoliceReport", " Evidence "), CancellationToken.None);

        result.Id.ShouldBe(documentId);
        result.DownloadUrl.ShouldBe("https://download.test/report");
        result.UploaderName.ShouldBe("Ada Handler");
        claim.LastActivity!.Revision.ShouldBe(revision + 1);
        documents.Received().Add(Arg.Is<DocumentRecord>(x => x.Id == documentId && x.Notes == "Evidence"));
        audit.Received().Append(Arg.Is<AuditEntry>(x => x.EventType == "DOCUMENT_UPLOADED"
            && x.RelatedEntityId == documentId && x.RelatedEntityType == "ClaimDocument"));
    }

    [Fact]
    public async Task MagicBytesAreValidated()
    {
        await using var content = new MemoryStream("not a pdf"u8.ToArray());
        await Should.ThrowAsync<ValidationException>(() =>
            DocumentFileRules.ValidateMagicBytesAsync("report.pdf", content, CancellationToken.None));
    }

    [Theory]
    [InlineData("evidence.txt", "text/plain", "Complete evidence content")]
    [InlineData("evidence.pdf", "application/pdf", "%PDF-1.4\nComplete evidence content")]
    public async Task TransactionReplayUploadsTheCompleteContentAgain(string fileName, string mime, string value)
    {
        var claimId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claim = Claim.Create(claimId, ClaimNumber.Create(2026, 1), userId,
            LossEvent.Create(Guid.NewGuid(), claimId, Now.AddDays(-1), "A documented collision damaged the vehicle.",
                "COL-VEH-COL", true, new TestClock()),
            [ClaimParty.Create(Guid.NewGuid(), claimId, PartyRole.Claimant, PartyType.Person, "Ada", "Lovelace")], new TestClock());
        var claims = Substitute.For<IClaimRepository>();
        claims.GetAsync(claimId, Arg.Any<CancellationToken>()).Returns(claim);
        var storage = Substitute.For<IStorageService>();
        var copies = new List<byte[]>();
        storage.UploadAsync(Arg.Any<Guid>(), claimId, fileName, mime, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await using var copy = new MemoryStream();
                await call.Arg<Stream>().CopyToAsync(copy);
                copies.Add(copy.ToArray());
                return new StoredFile("org/claim/evidence", fileName);
            });
        storage.GetDownloadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns("/download");
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId);
        var handler = new UploadDocumentCommandHandler(claims, Substitute.For<IDocumentRepository>(), storage,
            Substitute.For<IIdentifierGenerator>(), new TestClock(), user, Substitute.For<IAuditLogService>());
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        await using var content = new MemoryStream(bytes);
        var command = new UploadDocumentCommand(claimId, fileName, mime, bytes.Length, content, "Other", null);
        await handler.Handle(command, default);
        // Simulates the same command invoked again after the owning SQL transaction was rolled back.
        await handler.Handle(command, default);
        copies.Count.ShouldBe(2);
        copies.ShouldAllBe(x => x.SequenceEqual(bytes));
    }

    [Fact]
    public async Task NullContentIsAValidationFailureInsteadOfNullReferenceException()
    {
        var result = await new UploadDocumentCommandValidator(new()).ValidateAsync(
            new UploadDocumentCommand(Guid.NewGuid(), "evidence.txt", "text/plain", 1, null!, "Other", null));
        result.Errors.ShouldContain(x => x.PropertyName == "Content");
    }

    [Fact]
    public async Task ListRequiresClaimAndGeneratesDownloadUrlForEachCall()
    {
        var claimId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claim = Claim.Create(claimId, ClaimNumber.Create(2026, 2), userId,
            LossEvent.Create(Guid.NewGuid(), claimId, Now.AddDays(-1), "A documented collision damaged the vehicle.",
                "COL-VEH-COL", true, new TestClock()),
            [ClaimParty.Create(Guid.NewGuid(), claimId, PartyRole.Claimant, PartyType.Person, "Ada", "Lovelace")], new TestClock());
        var claims = Substitute.For<IClaimRepository>();
        claims.GetAsync(claimId, Arg.Any<CancellationToken>()).Returns(claim);
        var documents = Substitute.For<IDocumentQueries>();
        documents.ListAsync(claimId, Arg.Any<CancellationToken>()).Returns([
            new DocumentDto(Guid.NewGuid(), claimId, "Other", "notes.txt", "org/claim/notes.txt", "text/plain",
                5, Now, userId, null, "Ada Handler")]);
        var storage = Substitute.For<IStorageService>();
        storage.GetDownloadUrlAsync("org/claim/notes.txt", TimeSpan.FromHours(1), Arg.Any<CancellationToken>())
            .Returns("/download/one", "/download/two");
        var handler = new ListDocumentsQueryHandler(claims, documents, storage);

        (await handler.Handle(new(claimId), CancellationToken.None)).Single().DownloadUrl.ShouldBe("/download/one");
        (await handler.Handle(new(claimId), CancellationToken.None)).Single().DownloadUrl.ShouldBe("/download/two");
        await Should.ThrowAsync<ClaimsModule.Application.Exceptions.NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
