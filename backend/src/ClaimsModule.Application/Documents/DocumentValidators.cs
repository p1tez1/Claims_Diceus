using ClaimsModule.Application.Storage;
using FluentValidation;

namespace ClaimsModule.Application.Documents;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator(StorageOptions options)
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().Must((request, name) => DocumentFileRules.IsAllowed(name, request.ContentType))
            .WithMessage("File extension and content type must be an allowed matching pair (PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV).");
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).WithMessage("File must not be empty.")
            .LessThanOrEqualTo(options.MaxFileSizeBytes).WithMessage($"File must not exceed {options.MaxFileSizeBytes} bytes.");
        RuleFor(x => x.Content).NotNull().Must(x => x is { CanRead: true, CanSeek: true })
            .WithMessage("File content must be readable and seekable for transaction retries.");
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
