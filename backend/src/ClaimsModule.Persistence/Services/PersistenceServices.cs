using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace ClaimsModule.Persistence.Services;

public sealed class SequentialIdentifierGenerator : IIdentifierGenerator
{
    private readonly SequentialGuidValueGenerator _generator = new();
    public Guid NewId() => _generator.Next(null!);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class AuditLogService(ClaimsDbContext db, IIdentifierGenerator ids) : IAuditLogService
{
    public void Append(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Audit writes require the caller's transaction.");
        var row = new ClaimAuditLog
        {
            Id = ids.NewId(),
            ClaimId = entry.ClaimId,
            EventType = entry.EventType,
            Description = entry.Description,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            RelatedEntityId = entry.RelatedEntityId,
            RelatedEntityType = entry.RelatedEntityType,
            CorrelationId = db.CurrentUser.CorrelationId,
            CreatedByUserId = db.CurrentUser.UserId,
        };
        db.AuthorizedAuditEntries.Add(row);
        db.ClaimAuditLog.Add(row);
    }
}

public sealed class ClaimRepository(ClaimsDbContext db) : IClaimRepository
{
    public Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken = default) => db.Claims
        .Include(x => x.LossEvent).Include(x => x.Parties).Include(x => x.RiskObjects).Include(x => x.ValidationIssues)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken);
    public void Add(Claim claim) => db.Claims.Add(claim);
    public void Remove(Claim claim) => db.Claims.Remove(claim);
}

public sealed class ReserveComponentRepository(ClaimsDbContext db) : IReserveComponentRepository
{
    public async Task<IReadOnlyList<ClaimReserveComponent>> GetForClaimAsync(Guid claimId,
        CancellationToken cancellationToken = default) => await db.ClaimReserveComponents
        .Where(x => x.ClaimId == claimId && db.Claims.Any(c => c.Id == x.ClaimId))
        .Include(x => x.ReserveHistory).AsSplitQuery().OrderBy(x => x.Component).ToListAsync(cancellationToken);
    public void Add(ClaimReserveComponent component) => db.ClaimReserveComponents.Add(component);
}

public sealed class DocumentRepository(ClaimsDbContext db) : IDocumentRepository
{
    public void Add(DocumentRecord document) => db.ClaimDocuments.Add(new ClaimDocument
    {
        Id = document.Id,
        ClaimId = document.ClaimId,
        DocumentType = document.DocumentType,
        DocumentName = document.DocumentName,
        BlobPath = document.BlobPath,
        ContentType = document.ContentType,
        FileSizeBytes = document.FileSizeBytes,
        UploadedAt = document.UploadedAt,
        UploadedByUserId = document.UploadedByUserId,
        Notes = document.Notes,
    });
}
