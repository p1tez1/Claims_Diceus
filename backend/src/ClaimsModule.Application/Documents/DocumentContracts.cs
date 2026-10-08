using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;

namespace ClaimsModule.Application.Documents;

public sealed record UploadDocumentCommand(Guid ClaimId, string FileName, string ContentType, long FileSizeBytes,
    Stream Content, string DocumentType, string? Notes) : ICommand<DocumentDto>;

public sealed record ListDocumentsQuery(Guid ClaimId) : IQuery<IReadOnlyList<DocumentDto>>;
