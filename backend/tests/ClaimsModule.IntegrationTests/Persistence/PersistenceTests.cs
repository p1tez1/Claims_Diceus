using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Entities;
using ClaimsModule.Persistence.Queries;
using ClaimsModule.Persistence.Seeding;
using ClaimsModule.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace ClaimsModule.IntegrationTests.Persistence;

public sealed class PersistenceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private readonly SequentialIdentifierGenerator _ids = new();

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift()
    {
        await using var db = fixture.Create();
        (await db.Database.GetAppliedMigrationsAsync()).Single().ShouldEndWith("_InitialCreate");
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Policies.CountAsync()).ShouldBe(5);
        (await db.Policies.CountAsync(x => x.Status == PolicyStatus.Expired)).ShouldBe(2);
        (await db.CauseOfLossCodes.CountAsync()).ShouldBe(10);
        (await db.CauseOfLossCodes.CountAsync(x => x.IsActive)).ShouldBe(10);
        (await db.Users.CountAsync()).ShouldBe(3);
        (await db.ClaimStatusTransitions.CountAsync()).ShouldBe(12);
        (await db.ClaimStatusTransitions.SingleAsync(x => x.From == ClaimStatus.Closed)).RequiredRole.ShouldBe(UserRole.Supervisor);
        (await db.ClaimStatusTransitions.CountAsync(x => x.RequiredRole == UserRole.Handler)).ShouldBe(11);
        (await db.Policies.SingleAsync(x => x.PolicyNumber == "POL-2024-001001")).CoverageTypes.ShouldBe(["Vehicle", "Cargo"]);
    }

    [Fact]
    public void AllFifteenMappingsFollowD1AndDD39()
    {
        using var db = new ClaimsDbContextFactory().CreateDbContext([]);
        var model = db.GetService<IDesignTimeModel>().Model;
        model.GetEntityTypes().Count().ShouldBe(15);
        var softTables = new[] { "Claims", "ClaimParties", "ClaimRiskObjects", "ClaimDocuments", "ClaimValidationIssues", "LossEvents" };
        foreach (var type in model.GetEntityTypes())
        {
            type.FindProperty("OrganisationId")!.IsShadowProperty().ShouldBeTrue();
            type.GetQueryFilter().ShouldNotBeNull();
            type.FindPrimaryKey()!.Properties.Single().GetDefaultValueSql().ShouldBe("NEWSEQUENTIALID()");
            var createdAt = type.FindProperty("AuditCreatedAt") ?? type.FindProperty("CreatedAt");
            createdAt!.IsShadowProperty().ShouldBeTrue();
            createdAt.GetColumnName().ShouldBe("CreatedAt");
            type.FindProperty("UpdatedAt")!.IsShadowProperty().ShouldBeTrue();
            (type.FindProperty("IsDeleted") is not null).ShouldBe(softTables.Contains(type.GetTableName()));
            foreach (var property in type.GetProperties())
            {
                var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clr == typeof(decimal)) { property.GetPrecision().ShouldBe(19); property.GetScale().ShouldBe(4); }
                if (clr == typeof(DateTimeOffset)) property.GetPrecision().ShouldBe(7);
                if (clr == typeof(bool)) property.GetDefaultValue().ShouldBe(false);
            }
        }
        model.FindEntityType(typeof(ClaimParty))!.FindProperty(nameof(ClaimParty.Type))!.GetMaxLength().ShouldBe(20);
        foreach (var root in new[] { typeof(Claim), typeof(ClaimReserveComponent) })
        {
            var version = model.FindEntityType(root)!.FindProperty("RowVersion")!;
            version.IsConcurrencyToken.ShouldBeTrue();
            version.GetColumnName().ShouldBe("RowVer");
            version.GetColumnType().ShouldBe("rowversion");
        }
    }

    [SqlServerFact]
    public async Task ShadowsDomainTimestampBridgeAndDecimalPrecisionRoundTrip()
    {
        Guid id;
        DateTimeOffset historyCreatedAt;
        await using (var db = fixture.Create())
        await using (var uow = new UnitOfWork(db))
        {
            await uow.BeginAsync();
            var claim = await NewClaim(db, 123456789012345.6789m);
            var component = AddReserve(db, claim, 1234.5678m);
            historyCreatedAt = component.ReserveHistory.Single().CreatedAt;
            await uow.CommitAsync(new TestDispatcher());
            id = claim.Id;
            claim.RowVersion.Length.ShouldBe(8);
            component.RowVersion.Length.ShouldBe(8);
            foreach (var entry in db.ChangeTracker.Entries())
            {
                entry.Property("OrganisationId").CurrentValue.ShouldBe(SeedConstants.OrganisationId);
                entry.Property("UserCreated").CurrentValue.ShouldBe(SeedConstants.HandlerId);
                entry.Property(entry.Metadata.FindProperty("AuditCreatedAt") is null ? "CreatedAt" : "AuditCreatedAt")
                    .CurrentValue.ShouldBe(fixture.Clock.UtcNow);
            }
        }
        await using var loaded = fixture.Create();
        var read = await new ClaimRepository(loaded).GetAsync(id);
        read.ShouldNotBeNull();
        read.LossEvent.EstimatedLossAmount.ShouldBe(123456789012345.6789m);
        read.ValidationIssues.ShouldNotBeEmpty();
        read.ValidationIssues[0].CreatedAt.ShouldBe(fixture.Clock.UtcNow);
        var reserves = await new ReserveComponentRepository(loaded).GetForClaimAsync(id);
        reserves.Single().CurrentAmount.ShouldBe(1234.5678m);
        reserves.Single().ReserveHistory.Single().CreatedAt.ShouldBe(historyCreatedAt);
        reserves.Single().CurrentAmount.ShouldBe(reserves.Single().ReserveHistory.Where(x => x.IsApproved).Sum(x => x.Amount));
    }

    [SqlServerFact]
    public async Task TenantAndSoftDeleteFiltersHideRowsAndNumbersAreNeverReused()
    {
        var id = await SaveClaim();
        await using var otherTenant = fixture.Create(fixture.User with { OrganisationId = Guid.NewGuid() });
        (await otherTenant.Claims.AnyAsync(x => x.Id == id)).ShouldBeFalse();
        (await new ReadQueries(otherTenant).GetAsync(id)).ShouldBeNull();
        (await otherTenant.Policies.CountAsync()).ShouldBe(0);
        var hidden = await otherTenant.Claims.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        await using (var tx = new UnitOfWork(otherTenant))
        {
            await tx.BeginAsync();
            hidden.UpdateNotes("Cross-tenant mutation", fixture.Clock);
            await Should.ThrowAsync<InvalidOperationException>(() => tx.SaveChangesAsync());
        }
        await using var db = fixture.Create();
        await using (var uow = new UnitOfWork(db))
        {
            await uow.BeginAsync();
            var claim = (await new ClaimRepository(db).GetAsync(id))!;
            new ClaimRepository(db).Remove(claim);
            await uow.CommitAsync(new TestDispatcher());
        }
        db.ChangeTracker.Clear();
        (await db.Claims.AnyAsync(x => x.Id == id)).ShouldBeFalse();
        var deleted = await db.Claims.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        db.Entry(deleted).Property<bool>("IsDeleted").CurrentValue.ShouldBeTrue();
        db.Entry(deleted).Property<DateTimeOffset?>("DeletedAt").CurrentValue.ShouldNotBeNull();
        (await new ReadQueries(db).GetAsync(id)).ShouldBeNull();
        await using var insert = new UnitOfWork(db);
        await insert.BeginAsync();
        var replacement = await NewClaim(db);
        db.Entry(replacement).Property(x => x.ClaimNumber).CurrentValue = deleted.ClaimNumber;
        await Should.ThrowAsync<DbUpdateException>(() => insert.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task AuditIsAppendOnlyPropagatesActorAndCorrelationAndCannotBypassService()
    {
        var id = await SaveClaim();
        await using (var db = fixture.Create())
        await using (var uow = new UnitOfWork(db))
        {
            await uow.BeginAsync();
            new AuditLogService(db, _ids).Append(new(id, "TEST_EVENT", "Test audit", NewValue: "{\"ok\":true}"));
            await uow.CommitAsync(new TestDispatcher());
        }
        foreach (var delete in new[] { false, true })
        {
            await using var db = fixture.Create();
            await using var uow = new UnitOfWork(db);
            await uow.BeginAsync();
            var audit = await db.ClaimAuditLog.SingleAsync(x => x.ClaimId == id);
            audit.CorrelationId.ShouldBe(fixture.User.CorrelationId);
            audit.CreatedByUserId.ShouldBe(SeedConstants.HandlerId);
            if (delete) db.Remove(audit);
            else db.Entry(audit).Property(x => x.Description).CurrentValue = "Changed";
            await Should.ThrowAsync<InvalidOperationException>(() => uow.SaveChangesAsync());
        }
        await using var bypass = fixture.Create();
        await using var transaction = new UnitOfWork(bypass);
        await transaction.BeginAsync();
        bypass.ClaimAuditLog.Add(new ClaimAuditLog());
        await Should.ThrowAsync<InvalidOperationException>(() => transaction.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ClaimRowVersionConflictSurfacesTypedException()
    {
        var id = await SaveClaim();
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        var a = (await new ClaimRepository(first).GetAsync(id))!;
        var b = (await new ClaimRepository(second).GetAsync(id))!;
        await using (var tx = new UnitOfWork(first))
        {
            await tx.BeginAsync();
            a.UpdateNotes("Winner", fixture.Clock);
            await tx.CommitAsync(new TestDispatcher());
        }
        await using var conflict = new UnitOfWork(second);
        await conflict.BeginAsync();
        b.UpdateNotes("Stale", fixture.Clock);
        var exception = await Should.ThrowAsync<ConcurrencyException>(() => conflict.CommitAsync(new TestDispatcher()));
        exception.InnerException.ShouldBeOfType<DbUpdateConcurrencyException>();
        second.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task ReserveWorkflowAdvancesRowVersionEvenWhenBalanceIsUnchanged()
    {
        var id = await SaveClaim(withReserve: true);
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        var claimA = (await new ClaimRepository(first).GetAsync(id))!;
        var claimB = (await new ClaimRepository(second).GetAsync(id))!;
        var a = (await new ReserveComponentRepository(first).GetForClaimAsync(id)).Single();
        var b = (await new ReserveComponentRepository(second).GetForClaimAsync(id)).Single();
        var original = a.RowVersion;
        await using (var tx = new UnitOfWork(first))
        {
            await tx.BeginAsync();
            a.Submit(_ids.NewId(), 20000m, ReserveTransactionType.Add, "Pending estimate", SeedConstants.HandlerId,
                UserRole.Handler, new(claimA, new[] { a }), fixture.Clock);
            await tx.CommitAsync(new TestDispatcher());
            a.CurrentAmount.ShouldBe(100m);
            a.RowVersion.ShouldNotBe(original);
        }
        await using var conflict = new UnitOfWork(second);
        await conflict.BeginAsync();
        b.Submit(_ids.NewId(), 30000m, ReserveTransactionType.Add, "Concurrent pending estimate", SeedConstants.HandlerId,
            UserRole.Handler, new(claimB, new[] { b }), fixture.Clock);
        await Should.ThrowAsync<ConcurrencyException>(() => conflict.CommitAsync(new TestDispatcher()));
    }

    [SqlServerFact]
    public async Task FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap()
    {
        var allocations = Enumerable.Range(0, 50).Select(async _ =>
        {
            await using var db = fixture.Create();
            await using var uow = new UnitOfWork(db);
            var result = await uow.ExecuteAsync(ct => new ClaimNumberGenerator(db, _ids).NextAsync(2031, ct), new TestDispatcher());
            return result.Value.Sequence;
        });
        (await Task.WhenAll(allocations)).Order().ShouldBe(Enumerable.Range(1, 50));
        await using (var db = fixture.Create())
        await using (var rollback = new UnitOfWork(db))
        {
            await rollback.BeginAsync();
            (await new ClaimNumberGenerator(db, _ids).NextAsync(2032)).Sequence.ShouldBe(1);
            await rollback.RollbackAsync();
        }
        await using var next = fixture.Create();
        await using var commit = new UnitOfWork(next);
        await commit.BeginAsync();
        (await new ClaimNumberGenerator(next, _ids).NextAsync(2032)).Value.ShouldBe("CLM-2032-0000001");
        await commit.CommitAsync(new TestDispatcher());
    }

    [SqlServerFact]
    public async Task UniqueIndexesRejectDuplicateReserveKeysAndUserScopedRequestKeys()
    {
        var id = await SaveClaim(withReserve: true);
        await using (var db = fixture.Create())
        await using (var tx = new UnitOfWork(db))
        {
            await tx.BeginAsync();
            var claim = (await new ClaimRepository(db).GetAsync(id))!;
            var components = (await new ReserveComponentRepository(db).GetForClaimAsync(id)).ToList();
            var duplicate = components[0].Submit(_ids.NewId(), 10m, ReserveTransactionType.Add, "Duplicate key",
                SeedConstants.HandlerId, UserRole.Handler, new(claim, components), fixture.Clock).Transaction;
            db.ReserveHistory.Add(duplicate);
            db.Entry(duplicate).Property(x => x.IdempotencyKey).CurrentValue = components[0].ReserveHistory[0].IdempotencyKey;
            await Should.ThrowAsync<DbUpdateException>(() => tx.SaveChangesAsync());
        }
        await using var requests = fixture.Create();
        await using var transaction = new UnitOfWork(requests);
        await transaction.BeginAsync();
        var key = Guid.NewGuid().ToString();
        requests.IdempotencyRecords.AddRange(Request(key), Request(key));
        await Should.ThrowAsync<DbUpdateException>(() => transaction.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ReserveFinancialFieldsAndDeletionAreRejected()
    {
        var id = await SaveClaim(withReserve: true);
        foreach (var delete in new[] { false, true })
        {
            await using var db = fixture.Create();
            await using var uow = new UnitOfWork(db);
            await uow.BeginAsync();
            var entry = await db.ReserveHistory.SingleAsync(x => x.ClaimId == id);
            if (delete) db.Remove(entry);
            else db.Entry(entry).Property(x => x.Amount).CurrentValue = 1m;
            await Should.ThrowAsync<InvalidOperationException>(() => uow.SaveChangesAsync());
        }
    }

    [SqlServerFact]
    public async Task DomainEventsRunBeforeCommitAndPostCommitActionsSeeCommittedData()
    {
        await using var db = fixture.Create();
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        var claim = await NewClaim(db);
        var invoked = false;
        tx.EnqueuePostCommitAction(async token =>
        {
            db.Database.CurrentTransaction.ShouldBeNull();
            await using var reader = fixture.Create();
            (await reader.Claims.AnyAsync(x => x.Id == claim.Id, token)).ShouldBeTrue();
            (await reader.ClaimAuditLog.CountAsync(x => x.ClaimId == claim.Id, token)).ShouldBe(1);
            invoked = true;
        });
        var result = await tx.CommitAsync(new TestDispatcher((events, _) =>
        {
            db.Database.CurrentTransaction.ShouldNotBeNull();
            events.ShouldNotBeEmpty();
            new AuditLogService(db, _ids).Append(new(claim.Id, "CLAIM_CREATED", "Dispatched in owning transaction"));
            return Task.CompletedTask;
        }));
        result.PostCommitErrors.ShouldBeEmpty();
        invoked.ShouldBeTrue();
        claim.DomainEvents.ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task DispatchFailureRollsBackEarlierSavesAndCounterAndDiscardsPostCommitActions()
    {
        await using var db = fixture.Create();
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        var claim = await NewClaim(db, year: 2033);
        await tx.SaveChangesAsync();
        var invoked = false;
        tx.EnqueuePostCommitAction(_ => { invoked = true; return Task.CompletedTask; });
        await Should.ThrowAsync<InvalidOperationException>(() => tx.CommitAsync(new TestDispatcher((_, _) =>
            throw new InvalidOperationException("Handler failed"))));
        invoked.ShouldBeFalse();
        await using var reader = fixture.Create();
        (await reader.Claims.AnyAsync(x => x.Id == claim.Id)).ShouldBeFalse();
        await using var retry = new UnitOfWork(reader);
        await retry.BeginAsync();
        (await new ClaimNumberGenerator(reader, _ids).NextAsync(2033)).Sequence.ShouldBe(1);
    }

    [SqlServerFact]
    public async Task PostCommitFailureDoesNotRollbackOrReplayAndLaterActionsStillRun()
    {
        await using var db = fixture.Create();
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        var claim = await NewClaim(db);
        var secondRan = false;
        tx.EnqueuePostCommitAction(_ => throw new InvalidOperationException("Queue unavailable"));
        tx.EnqueuePostCommitAction(_ => { secondRan = true; return Task.CompletedTask; });
        var result = await tx.CommitAsync(new TestDispatcher());
        result.PostCommitErrors.Count.ShouldBe(1);
        secondRan.ShouldBeTrue();
        await using var reader = fixture.Create();
        (await reader.Claims.AnyAsync(x => x.Id == claim.Id)).ShouldBeTrue();
    }

    [SqlServerFact]
    public async Task ReadSideProjectsDtoGraphsFiltersAndExactRolesWithoutTracking()
    {
        var id = await SaveClaim(withReserve: true);
        await using var db = fixture.Create();
        var queries = new ReadQueries(db);
        var detail = await queries.GetAsync(id);
        detail.ShouldNotBeNull();
        detail.Parties.Count.ShouldBe(1);
        detail.Reserves.Single().History.Count.ShouldBe(1);
        detail.Reserves.Single().CurrentAmount.ShouldBe(100m);
        var page = await queries.ListAsync(new ClaimListRequest(new(1, 10), Search: detail.ClaimNumber));
        page.Items.Single().Id.ShouldBe(id);
        page.Items.Single().TotalReserves.ShouldBe(100m);
        (await queries.ListAsync(UserRole.Handler)).Single().Username.ShouldBe("handler");
        (await queries.ListAsync()).Count.ShouldBe(3);
        (await queries.GetCausesAsync("Auto")).Count.ShouldBe(2);
        (await queries.GetStatusesAsync()).Single(x => x.Status == ClaimStatus.Withdrawn).Transitions.ShouldBeEmpty();
        (await queries.SearchAsync("Meridian")).Single().CoverageTypes.ShouldBe(["Vehicle", "Cargo"]);
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task SlaBulkUpdateAndAuditInsertDoNotBumpClaimActivity()
    {
        var id = await SaveClaim();
        await using var db = fixture.Create();
        var before = await db.Claims.Where(x => x.Id == id).Select(x => EF.Property<DateTimeOffset?>(x, "UpdatedAt")).SingleAsync();
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        await db.Claims.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SlaBreachedAt, fixture.Clock.UtcNow));
        new AuditLogService(db, _ids).Append(new(id, "SLA_BREACH_DETECTED", "SLA detected"));
        await tx.CommitAsync(new TestDispatcher());
        var after = await db.Claims.Where(x => x.Id == id).Select(x => new { x.SlaBreachedAt, At = EF.Property<DateTimeOffset?>(x, "UpdatedAt") }).SingleAsync();
        after.At.ShouldBe(before);
        after.SlaBreachedAt.ShouldBe(fixture.Clock.UtcNow);
    }

    [SqlServerFact]
    public async Task AuditBulkMutationAlsoThrowsAndFailedAuditRollbackLeavesNoRows()
    {
        var id = await SaveClaim();
        await using var db = fixture.Create();
        await Should.ThrowAsync<InvalidOperationException>(() => db.ClaimAuditLog.Where(x => x.ClaimId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "Bypass")));
        await Should.ThrowAsync<InvalidOperationException>(() => db.ClaimAuditLog.Where(x => x.ClaimId == id).ExecuteDeleteAsync());
        await using (var tx = new UnitOfWork(db))
        {
            await tx.BeginAsync();
            new AuditLogService(db, _ids).Append(new(id, "ROLLBACK", "Must roll back"));
            await tx.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        (await db.ClaimAuditLog.CountAsync(x => x.ClaimId == id)).ShouldBe(0);
    }

    [SqlServerFact]
    public async Task ComponentRowVersionProtectsChangesWithoutAClaimUpdate()
    {
        var id = await SaveClaim(withReserve: true);
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        var a = await first.ClaimReserveComponents.SingleAsync(x => x.ClaimId == id);
        var b = await second.ClaimReserveComponents.SingleAsync(x => x.ClaimId == id);
        await using (var tx = new UnitOfWork(first))
        {
            await tx.BeginAsync();
            first.Entry(a).Property(x => x.Status).CurrentValue = ReserveComponentStatus.Closed;
            await tx.CommitAsync(new TestDispatcher());
        }
        await using var conflict = new UnitOfWork(second);
        await conflict.BeginAsync();
        second.Entry(b).Property(x => x.Status).CurrentValue = ReserveComponentStatus.Closed;
        var error = await Should.ThrowAsync<ConcurrencyException>(() => conflict.CommitAsync(new TestDispatcher()));
        ((DbUpdateConcurrencyException)error.InnerException!).Entries.Single().Entity.ShouldBeOfType<ClaimReserveComponent>();
    }

    [SqlServerFact]
    public async Task RetryReloadsAndReevaluatesOnceAndDoesNotRepeatPostCommitActions()
    {
        var id = await SaveClaim();
        await using var db = fixture.Create();
        await using var tx = new UnitOfWork(db);
        var attempts = 0;
        var posts = 0;
        await tx.ExecuteAsync(async token =>
        {
            attempts++;
            var claim = (await new ClaimRepository(db).GetAsync(id, token))!;
            if (attempts == 1)
            {
                await using var rival = fixture.Create();
                await using var rivalTx = new UnitOfWork(rival);
                await rivalTx.BeginAsync(token);
                var other = (await new ClaimRepository(rival).GetAsync(id, token))!;
                other.UpdateNotes("Rival", fixture.Clock);
                await rivalTx.CommitAsync(new TestDispatcher(), token);
            }
            else claim.Notes.ShouldBe("Rival");
            claim.UpdateNotes("Re-evaluated", fixture.Clock);
            tx.EnqueuePostCommitAction(_ => { posts++; return Task.CompletedTask; });
            return claim.Id;
        }, new TestDispatcher());
        attempts.ShouldBe(2);
        posts.ShouldBe(1);
    }

    [SqlServerFact]
    public async Task CounterDeadlockRetriesTheWholeTransactionWithoutLosingEitherAllocation()
    {
        await using (var setup = fixture.Create())
        await using (var tx = new UnitOfWork(setup))
        {
            await tx.BeginAsync();
            var generator = new ClaimNumberGenerator(setup, _ids);
            await generator.NextAsync(2038);
            await generator.NextAsync(2039);
            await tx.CommitAsync(new TestDispatcher());
        }
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var attempts = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        async Task Run(int firstYear, int secondYear)
        {
            await using var db = fixture.Create();
            await using var tx = new UnitOfWork(db);
            var localAttempts = 0;
            await tx.ExecuteAsync(async token =>
            {
                Interlocked.Increment(ref attempts);
                localAttempts++;
                var generator = new ClaimNumberGenerator(db, _ids);
                await generator.NextAsync(firstYear, token);
                if (localAttempts == 1)
                {
                    if (Interlocked.Increment(ref arrived) == 2) gate.SetResult();
                    await gate.Task.WaitAsync(token);
                }
                return await generator.NextAsync(secondYear, token);
            }, new TestDispatcher(), timeout.Token);
        }
        await Task.WhenAll(Run(2038, 2039), Run(2039, 2038));
        attempts.ShouldBe(3);
        await using var verify = fixture.Create();
        (await verify.ClaimNumberSequences.Where(x => x.Year == 2038 || x.Year == 2039).Select(x => x.LastValue).ToArrayAsync())
            .ShouldBe([3, 3]);
    }

    [SqlServerFact]
    public async Task TenantForeignKeysRejectCrossTenantRelationships()
    {
        Claim claim;
        await using (var original = fixture.Create())
        await using (var tx = new UnitOfWork(original))
        {
            await tx.BeginAsync();
            claim = await NewClaim(original);
            await tx.RollbackAsync();
        }
        await using var foreign = fixture.Create(fixture.User with { OrganisationId = Guid.NewGuid() });
        await using var foreignTx = new UnitOfWork(foreign);
        await foreignTx.BeginAsync();
        foreign.Claims.Add(claim);
        var error = await Should.ThrowAsync<DbUpdateException>(() => foreignTx.SaveChangesAsync());
        error.InnerException!.Message.ShouldContain("FOREIGN KEY");
    }

    [SqlServerFact]
    public async Task TouchPersistsChildOnlyActivityAndLiabilityColumnsRoundTrip()
    {
        var id = await SaveClaim();
        var clock = new TestClock { UtcNow = fixture.Clock.UtcNow.AddMinutes(5) };
        await using var db = fixture.Create(clock: clock);
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        var claim = (await new ClaimRepository(db).GetAsync(id))!;
        var version = claim.RowVersion;
        claim.AddParty(ClaimParty.Create(_ids.NewId(), id, PartyRole.Witness, PartyType.Person, "New", "Witness"), clock);
        db.Entry(claim).Property<LiabilityDetermination?>("LiabilityDetermination").CurrentValue = LiabilityDetermination.PartiallyAccepted;
        db.Entry(claim).Property<DateTimeOffset?>("LiabilityDeterminedAt").CurrentValue = clock.UtcNow;
        var activityAt = clock.UtcNow;
        clock.UtcNow = clock.UtcNow.AddMinutes(1); // Saving later must preserve the business activity's timestamp.
        await tx.CommitAsync(new TestDispatcher());
        claim.RowVersion.ShouldNotBe(version);
        db.Entry(claim).Property<DateTimeOffset?>("UpdatedAt").CurrentValue.ShouldBe(activityAt);
        db.Entry(claim).Property<Guid?>("UserModified").CurrentValue.ShouldBe(SeedConstants.HandlerId);
        var detail = (await new ReadQueries(db).GetAsync(id))!;
        detail.Parties.Count.ShouldBe(2);
        detail.LiabilityDetermination.ShouldBe("PartiallyAccepted");
        detail.LiabilityDeterminedAt.ShouldBe(activityAt);
    }

    [SqlServerFact]
    public async Task RequestKeysArePartitionedByUserAndTenant()
    {
        var key = Guid.NewGuid().ToString();
        await using (var db = fixture.Create())
        await using (var tx = new UnitOfWork(db))
        {
            await tx.BeginAsync();
            var second = Request(key);
            second.UserId = SeedConstants.SupervisorId;
            db.IdempotencyRecords.AddRange(Request(key), second);
            await tx.CommitAsync(new TestDispatcher());
        }
        await using var other = fixture.Create(fixture.User with { OrganisationId = Guid.NewGuid() });
        await using var otherTx = new UnitOfWork(other);
        await otherTx.BeginAsync();
        other.IdempotencyRecords.Add(Request(key));
        await otherTx.CommitAsync(new TestDispatcher());
        (await other.IdempotencyRecords.CountAsync(x => x.Key == key)).ShouldBe(1);
        (await other.IdempotencyRecords.IgnoreQueryFilters().CountAsync(x => x.Key == key)).ShouldBe(3);
    }

    private IdempotencyRecord Request(string key) => new()
    {
        Id = _ids.NewId(),
        UserId = SeedConstants.HandlerId,
        Key = key,
        HttpMethod = "POST",
        RouteTemplate = "/api/claims",
        RequestHash = new string('a', 64),
        Status = IdempotencyStatus.InProgress,
    };

    private async Task<Guid> SaveClaim(bool withReserve = false)
    {
        await using var db = fixture.Create();
        await using var tx = new UnitOfWork(db);
        await tx.BeginAsync();
        var claim = await NewClaim(db);
        if (withReserve) AddReserve(db, claim, 100m);
        await tx.CommitAsync(new TestDispatcher());
        return claim.Id;
    }

    private async Task<Claim> NewClaim(ClaimsDbContext db, decimal? estimate = null, int year = 2026)
    {
        var id = _ids.NewId();
        var policy = await db.Policies.AsNoTracking().SingleAsync(x => x.PolicyNumber == "POL-2024-001001");
        var loss = LossEvent.Create(_ids.NewId(), id, fixture.Clock.UtcNow.AddDays(-1),
            "The insured vehicle was damaged in a road collision.", "COL-VEH-COL", true, fixture.Clock, estimate);
        var claim = Claim.Create(id, await new ClaimNumberGenerator(db, _ids).NextAsync(year), SeedConstants.HandlerId,
            loss, [ClaimParty.Create(_ids.NewId(), id, PartyRole.Claimant, PartyType.Person, "Test", "Claimant")], fixture.Clock,
            new PolicyAssociation(policy.Id, policy.PolicyNumber, policy.ClientName,
                new DateTimeOffset(policy.EffectiveDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                new DateTimeOffset(policy.ExpirationDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)));
        db.Claims.Add(claim);
        return claim;
    }

    private ClaimReserveComponent AddReserve(ClaimsDbContext db, Claim claim, decimal amount)
    {
        var component = ClaimReserveComponent.Create(_ids.NewId(), claim, ReserveComponentType.Indemnity, []);
        component.Submit(_ids.NewId(), amount, ReserveTransactionType.Add, "Initial estimate", SeedConstants.HandlerId,
            UserRole.Handler, new(claim, new[] { component }), fixture.Clock);
        db.ClaimReserveComponents.Add(component);
        return component;
    }
}
