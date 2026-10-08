using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Abstractions;

public interface IClock : Domain.Common.IClock;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string Name { get; }
    UserRole? Role { get; }
    Guid OrganisationId { get; }
    Guid? CorrelationId { get; }
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>A scope must be begun before allocating numbers, appending audit, or saving.
/// Commit dispatches events and saves their effects in the owning transaction. Actions run only after commit.
/// Dispose/rollback detaches tracked state; callers must reload before retrying a failed business operation.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    bool HasActiveTransaction { get; }
    bool IsConcurrencyRetry { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<CommitResult> CommitAsync(IDomainEventDispatcher dispatcher, CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    void EnqueuePostCommitAction(Func<CancellationToken, Task> action);
    // The callback must reload and re-evaluate domain rules on EVERY invocation (DD-35).
    Task<TransactionResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation,
        IDomainEventDispatcher dispatcher, CancellationToken cancellationToken = default);
}

/// <summary>The database has committed when this result is returned. Action errors must never trigger command replay.
/// DD-33 recovers eligible GL rows through its separate recovery job.</summary>
public sealed record CommitResult(IReadOnlyList<Exception> PostCommitErrors);
public sealed record TransactionResult<T>(T Value, CommitResult Commit);

public interface IIdentifierGenerator
{
    Guid NewId();
}

public interface IClaimRepository
{
    Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken = default);
    void Add(Claim claim);
    void Remove(Claim claim);
}

public interface IReserveComponentRepository
{
    // Load ALL components and history, in the transaction owning the Claim, for ReserveOperationContext.
    Task<IReadOnlyList<ClaimReserveComponent>> GetForClaimAsync(Guid claimId, CancellationToken cancellationToken = default);
    void Add(ClaimReserveComponent component);
}

public interface IClaimNumberGenerator
{
    Task<ClaimNumber> NextAsync(int year, CancellationToken cancellationToken = default);
}

public interface IAuditLogService
{
    void Append(AuditEntry entry);
}

public interface IClaimQueries
{
    Task<PageDto<ClaimListItemDto>> ListAsync(ClaimListRequest request, CancellationToken cancellationToken = default);
    Task<ClaimDetailDto?> GetAsync(Guid claimId, CancellationToken cancellationToken = default);
}

public interface IAuditQueries
{
    Task<PageDto<AuditLogDto>> ListAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken = default);
}

public interface IReserveQueries
{
    Task<IReadOnlyList<ReserveComponentDto>> ListAsync(Guid claimId, CancellationToken cancellationToken = default);
}

public interface IReferenceDataQueries
{
    Task<IReadOnlyList<CauseOfLossDto>> GetCausesAsync(string? perilCategory = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClaimStatusDto>> GetStatusesAsync(CancellationToken cancellationToken = default);
}

public interface IPolicyQueries
{
    Task<IReadOnlyList<PolicyDto>> SearchAsync(string search, CancellationToken cancellationToken = default);
    Task<PolicyDto?> GetAsync(Guid policyId, CancellationToken cancellationToken = default);
}

public interface IUserQueries
{
    // Exact stored role (DD-40). Omit the filter for the assignee picker.
    Task<IReadOnlyList<UserDto>> ListAsync(UserRole? role = null, CancellationToken cancellationToken = default);
    Task<UserDto?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public interface IDocumentQueries
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid claimId, CancellationToken cancellationToken = default);
}

public interface IDocumentRepository
{
    void Add(DocumentRecord document);
}

public interface IStorageService
{
    Task<StoredFile> UploadAsync(Guid organisationId, Guid claimId, string fileName, string contentType,
        Stream content, CancellationToken cancellationToken = default);
    Task<string> GetDownloadUrlAsync(string path, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
