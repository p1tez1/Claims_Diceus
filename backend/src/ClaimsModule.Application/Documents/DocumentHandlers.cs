using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Storage;
using MediatR;

namespace ClaimsModule.Application.Documents;

public sealed class UploadDocumentCommandHandler(IClaimRepository claims, IDocumentRepository documents,
    IStorageService storage, IIdentifierGenerator ids, IClock clock, ICurrentUser user, IAuditLogService audit)
    : IRequestHandler<UploadDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException($"Claim '{request.ClaimId}' was not found.");
        // The unit of work can replay this handler after a RowVer conflict. IFormFile streams are seekable.
        request.Content.Position = 0;
        await DocumentFileRules.ValidateMagicBytesAsync(request.FileName, request.Content, cancellationToken);
        var stored = await storage.UploadAsync(user.OrganisationId, claim.Id, request.FileName, request.ContentType,
            request.Content, cancellationToken);
        var id = ids.NewId();
        var uploadedAt = clock.UtcNow.ToUniversalTime();
        var record = new DocumentRecord(id, claim.Id, request.DocumentType.Trim(), stored.FileName, stored.Path,
            request.ContentType, request.FileSizeBytes, uploadedAt, user.UserId, request.Notes?.Trim());
        documents.Add(record);
        claim.Touch(clock);
        audit.Append(new(claim.Id, "DOCUMENT_UPLOADED", "Document uploaded",
            NewValue: System.Text.Json.JsonSerializer.Serialize(new { record.DocumentName, record.DocumentType, record.FileSizeBytes }),
            RelatedEntityId: id, RelatedEntityType: "ClaimDocument"));
        var url = await storage.GetDownloadUrlAsync(stored.Path, TimeSpan.FromHours(1), cancellationToken);
        return ToDto(record, user.Name, url);
    }

    private static DocumentDto ToDto(DocumentRecord x, string? uploaderName, string url) => new(x.Id, x.ClaimId,
        x.DocumentType, x.DocumentName, x.BlobPath, x.ContentType, x.FileSizeBytes, x.UploadedAt,
        x.UploadedByUserId, x.Notes, uploaderName, url);
}

public sealed class ListDocumentsQueryHandler(IClaimRepository claims, IDocumentQueries documents, IStorageService storage)
    : IRequestHandler<ListDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(ListDocumentsQuery request, CancellationToken cancellationToken)
    {
        if (await claims.GetAsync(request.ClaimId, cancellationToken) is null)
            throw new NotFoundException($"Claim '{request.ClaimId}' was not found.");
        return await AddUrls(await documents.ListAsync(request.ClaimId, cancellationToken), storage, cancellationToken);
    }

    internal static async Task<IReadOnlyList<DocumentDto>> AddUrls(IReadOnlyList<DocumentDto> documents,
        IStorageService storage, CancellationToken cancellationToken)
    {
        var result = new DocumentDto[documents.Count];
        for (var index = 0; index < documents.Count; index++)
            result[index] = documents[index] with
            {
                DownloadUrl = await storage.GetDownloadUrlAsync(documents[index].BlobPath, TimeSpan.FromHours(1), cancellationToken)
            };
        return result;
    }
}
