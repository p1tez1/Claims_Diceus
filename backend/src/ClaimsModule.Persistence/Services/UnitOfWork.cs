using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClaimsModule.Persistence.Services;

public sealed class UnitOfWork(ClaimsDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;
    private readonly List<Func<CancellationToken, Task>> _postCommit = [];
    private bool _committing;
    public bool HasActiveTransaction => _transaction is not null;
    public bool IsConcurrencyRetry { get; private set; }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (HasActiveTransaction || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A transaction is already active.");
        _transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        return db.SaveChangesAsync(cancellationToken);
    }

    public void EnqueuePostCommitAction(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        RequireTransaction();
        _postCommit.Add(action);
    }

    public async Task<CommitResult> CommitAsync(IDomainEventDispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        RequireTransaction();
        if (_committing) throw new InvalidOperationException("Commit cannot be re-entered by an event dispatcher.");
        _committing = true;
        try
        {
            var dispatched = new HashSet<IDomainEvent>(ReferenceEqualityComparer.Instance);
            while (true)
            {
                var pending = db.CollectDomainEvents().Where(e => !dispatched.Contains(e)).ToArray();
                if (pending.Length == 0) break;
                await dispatcher.DispatchAsync(pending, cancellationToken);
                foreach (var item in pending) dispatched.Add(item);
            }
            await db.SaveChangesAsync(cancellationToken);
            await _transaction!.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { _committing = false; }

        foreach (var aggregate in db.ChangeTracker.Entries<AggregateRoot>()) aggregate.Entity.ClearDomainEvents();
        await _transaction!.DisposeAsync();
        _transaction = null;
        var actions = _postCommit.ToArray();
        _postCommit.Clear();
        var errors = new List<Exception>();
        // Request cancellation after commit must not suppress the delivery attempt (DD-17/DD-33).
        foreach (var action in actions)
        {
            try { await action(CancellationToken.None); }
            catch (Exception exception) { errors.Add(exception); }
        }
        return new(errors.AsReadOnly());
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null && !db.TransactionAbortedByServer) await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            if (_transaction is not null) await _transaction.DisposeAsync();
            _transaction = null;
            db.TransactionAbortedByServer = false;
            _postCommit.Clear();
            db.ChangeTracker.Clear();
            db.ActivitySnapshots.Clear();
            db.AuthorizedAuditEntries.Clear();
        }
    }

    public async Task<TransactionResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation,
        IDomainEventDispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var concurrencyRetries = 0;
            var counterRetries = 0;
            IsConcurrencyRetry = false;
            while (true)
            {
                await BeginAsync(cancellationToken);
                try
                {
                    var result = await operation(cancellationToken);
                    return new TransactionResult<T>(result, await CommitAsync(dispatcher, cancellationToken));
                }
                catch (ConcurrencyException) when (concurrencyRetries++ < 1)
                {
                    await RollbackAsync(CancellationToken.None);
                    IsConcurrencyRetry = true;
                }
                catch (TransactionRetryRequiredException) when (counterRetries++ < 3)
                {
                    await RollbackAsync(CancellationToken.None);
                }
                catch (DomainRuleViolationException exception) when (concurrencyRetries > 0 && exception.Code == "RESERVE_TRANSACTION_STATE")
                {
                    await RollbackAsync(CancellationToken.None);
                    throw new ConcurrencyException("The reserve transaction was reviewed by another request. Reload its current state.", exception);
                }
                catch
                {
                    await RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await RollbackAsync(CancellationToken.None);
    }

    private void RequireTransaction()
    {
        if (!HasActiveTransaction) throw new InvalidOperationException("Begin a unit-of-work transaction first.");
    }
}
