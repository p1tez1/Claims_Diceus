using System.Data;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClaimsModule.Persistence.Services;

public sealed class ClaimNumberGenerator(ClaimsDbContext db, IIdentifierGenerator ids) : IClaimNumberGenerator
{
    public async Task<ClaimNumber> NextAsync(int year, CancellationToken cancellationToken = default)
    {
        _ = ClaimNumber.Create(year, 1);
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Claim numbers must be allocated in the caller's transaction.");
        if (db.TenantId == Guid.Empty) throw new InvalidOperationException("A tenant is required to allocate claim numbers.");
        const string savepoint = "ClaimNumberAllocation";
        for (var retry = 0; ; retry++)
        {
            await transaction.CreateSavepointAsync(savepoint, cancellationToken);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                DECLARE @allocated TABLE ([Value] int NOT NULL);
                UPDATE [ClaimNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                SET [LastValue] = [LastValue] + 1, [UpdatedAt] = @now, [UserModified] = @actor
                OUTPUT inserted.[LastValue] INTO @allocated
                WHERE [OrganisationId] = @organisation AND [Year] = @year AND [LastValue] < 9999999;
                IF NOT EXISTS (SELECT 1 FROM @allocated)
                BEGIN
                    IF EXISTS (SELECT 1 FROM [ClaimNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [OrganisationId] = @organisation AND [Year] = @year)
                        THROW 50001, 'The seven-digit claim-number range is exhausted.', 1;
                    INSERT INTO [ClaimNumberSequences]
                        ([ClaimNumberSequenceId], [OrganisationId], [Year], [LastValue], [CreatedAt], [UserCreated])
                    OUTPUT inserted.[LastValue] INTO @allocated
                    VALUES (@id, @organisation, @year, 1, @now, @actor);
                END;
                SELECT [Value] FROM @allocated;
                """;
            Add("@id", DbType.Guid, ids.NewId());
            Add("@organisation", DbType.Guid, db.TenantId);
            Add("@year", DbType.Int32, year);
            Add("@now", DbType.DateTimeOffset, db.Clock.UtcNow.ToUniversalTime());
            Add("@actor", DbType.Guid, db.CurrentUser.UserId);
            try
            {
                var number = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
                return ClaimNumber.Create(year, number);
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                db.TransactionAbortedByServer = true;
                // SQL Server has destroyed the entire transaction. A statement-only retry would lose earlier writes.
                throw new TransactionRetryRequiredException("Counter deadlock: retry the complete owning transaction (DD-35).", exception);
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627 && retry < 3)
            {
                await transaction.RollbackToSavepointAsync(savepoint, cancellationToken);
            }

            void Add(string name, DbType type, object? value)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.DbType = type;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }
    }
}
