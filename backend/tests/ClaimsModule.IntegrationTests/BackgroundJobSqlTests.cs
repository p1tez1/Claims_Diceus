using System.Net;
using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Behaviors;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Entities;
using ClaimsModule.Persistence.Seeding;
using ClaimsModule.Persistence.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.SqlServer;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class BackgroundJobSqlTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private readonly SequentialIdentifierGenerator _ids = new();
    private BackgroundJobStore Store() => new(new DbContextOptionsBuilder<ClaimsDbContext>()
        .UseSqlServer(fixture.ConnectionString).Options, fixture.Clock, _ids);
    private PostGLReserveChangeJob Posting() => new(Store(), NullLogger<PostGLReserveChangeJob>.Instance);

    [SqlServerFact]
    public async Task SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate()
    {
        foreach (var parallel in new[] { false, true })
        {
            var work = await SeedReserve();
            var start = fixture.Clock.UtcNow;
            fixture.Clock.UtcNow = start.AddMinutes(1);
            if (parallel) await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Posting().Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey)));
            else await Posting().Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey);
            await using var first = fixture.Create();
            var claim = await first.Claims.SingleAsync(c => c.Id == work.ClaimId);
            var version = claim.RowVersion.ToArray();
            var updated = first.Entry(claim).Property<DateTimeOffset?>("UpdatedAt").CurrentValue;
            updated.ShouldBe(fixture.Clock.UtcNow);
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
            await Posting().Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey);
            await using var check = fixture.Create();
            var history = await check.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId);
            history.PostingStatus.ShouldBe(PostingStatus.Posted);
            history.PostingJobId.ShouldBe($"direct:{history.Id:D}");
            history.PostedAt.ShouldBe(updated);
            var audit = await check.ClaimAuditLog.Where(a => a.ClaimId == work.ClaimId && a.EventType == "GL_POSTING_SIMULATED").ToListAsync();
            audit.ShouldHaveSingleItem();
            audit[0].CreatedByUserId.ShouldBeNull();
            var json = JsonSerializer.Deserialize<JsonElement>(audit[0].NewValue!);
            json.GetProperty("debit").GetString().ShouldBe("Change in Outstanding Reserves");
            json.GetProperty("credit").GetString().ShouldBe("Outstanding Loss Reserves");
            json.GetProperty("amount").GetDecimal().ShouldBe(100);
            json.GetProperty("idempotencyKey").GetString().ShouldBe(work.IdempotencyKey);
            var unchanged = await check.Claims.SingleAsync(c => c.Id == work.ClaimId);
            unchanged.RowVersion.ShouldBe(version);
            check.Entry(unchanged).Property<DateTimeOffset?>("UpdatedAt").CurrentValue.ShouldBe(updated);
        }
    }

    [SqlServerFact]
    public async Task NonApprovedAndMismatchedDeliveriesProduceNoAudit()
    {
        foreach (var status in new[] { ReserveApprovalStatus.PendingApproval, ReserveApprovalStatus.Rejected, ReserveApprovalStatus.Cancelled })
        {
            var work = await SeedReserve(status);
            await Posting().Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey);
            (await Store().FailAsync(work, "failure", "no posting")).ShouldBeFalse();
            await using var db = fixture.Create();
            (await db.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId)).ShouldBe(0);
        }
        var approved = await SeedReserve(ReserveApprovalStatus.Approved);
        await Posting().Execute(approved.ReserveHistoryId, Guid.NewGuid(), approved.IdempotencyKey);
        await Posting().Execute(approved.ReserveHistoryId, approved.ClaimId, "wrong key");
        await using var check = fixture.Create();
        (await check.ClaimAuditLog.CountAsync(a => a.ClaimId == approved.ClaimId)).ShouldBe(0);
        await Posting().Execute(approved.ReserveHistoryId, approved.ClaimId, approved.IdempotencyKey);
        (await check.ClaimAuditLog.CountAsync(a => a.ClaimId == approved.ClaimId && a.EventType == "GL_POSTING_SIMULATED")).ShouldBe(1);
    }

    [SqlServerFact]
    public async Task StatePipelineOnlyRecordsFailureAfterDefaultRetriesAreExhausted()
    {
        var work = await SeedReserve();
        using var services = new ServiceCollection().AddScoped<IBackgroundJobStore>(_ => Store()).BuildServiceProvider();
        var storage = new SqlServerStorage(fixture.ConnectionString, new SqlServerStorageOptions { SchemaName = "HangFire" });
        var filters = new JobFilterCollection();
        filters.Add(new AutomaticRetryAttribute());
        filters.Add(new GlPostingFailureFilter(services.GetRequiredService<IServiceScopeFactory>()));
        var client = new BackgroundJobClient(storage, filters);
        var jobId = client.Enqueue<PostGLReserveChangeJob>(job => job.Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey, null!, CancellationToken.None));
        // The retry filter substitutes Scheduled, so the requested Failed state was not applied.
        client.ChangeState(jobId, new FailedState(new InvalidOperationException("first attempt")), EnqueuedState.StateName).ShouldBeFalse();
        using var connection = storage.GetConnection();
        connection.GetStateData(jobId).Name.ShouldBe(ScheduledState.StateName);
        await using (var pending = fixture.Create())
        {
            (await pending.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Pending);
            (await pending.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId)).ShouldBe(0);
        }
        connection.SetJobParameter(jobId, "RetryCount", "10");
        client.ChangeState(jobId, new FailedState(new InvalidOperationException("exhausted")), ScheduledState.StateName).ShouldBeTrue();
        connection.GetStateData(jobId).Name.ShouldBe(FailedState.StateName);
        client.ChangeState(jobId, new FailedState(new InvalidOperationException("repeated filter")), FailedState.StateName).ShouldBeTrue();
        await using var db = fixture.Create();
        (await db.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Failed);
        (await db.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId && a.EventType == "GL_POSTING_FAILED")).ShouldBe(1);
    }

    [SqlServerFact]
    public async Task AuditFailureRollsBackPostingSoDeliveryCanBeRetried()
    {
        var work = await SeedReserve();
        var store = new BackgroundJobStore(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options,
            fixture.Clock, new FailingIdentifierGenerator());
        await Should.ThrowAsync<InvalidOperationException>(() => store.PostAsync(work, "failed-audit"));
        await using (var db = fixture.Create())
        {
            (await db.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Pending);
            (await db.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId)).ShouldBe(0);
        }
        (await Store().PostAsync(work, "retried")).ShouldBeTrue();
    }

    [SqlServerFact]
    public async Task TerminalFailureFilterIsIdempotentAndRetryRecoversWithSameKey()
    {
        var work = await SeedReserve();
        using var services = new ServiceCollection().AddScoped<IBackgroundJobStore>(_ => Store()).BuildServiceProvider();
        var filter = new GlPostingFailureFilter(services.GetRequiredService<IServiceScopeFactory>());
        await Task.WhenAll(filter.HandleAsync(work, "failed-1", "exhausted"), filter.HandleAsync(work, "failed-1", "exhausted"));
        await filter.HandleAsync(work, "failed-1", "exhausted");
        await using (var db = fixture.Create())
        {
            (await db.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Failed);
            (await db.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId && a.EventType == "GL_POSTING_FAILED")).ShouldBe(1);
        }
        var client = new RecordingClient();
        await using (var db = fixture.Create())
        await using (var unit = new UnitOfWork(db))
        {
            await unit.BeginAsync();
            var retry = new ReservePostingRetry(new ClaimRepository(db), new ReserveComponentRepository(db),
                new GlPostingScheduler(unit, client), fixture.Clock);
            await retry.ExecuteAsync(new(work.ClaimId, work.ReserveHistoryId));
            client.Jobs.ShouldBeEmpty();
            await unit.CommitAsync(new TestDispatcher());
            client.Jobs.ShouldHaveSingleItem().Args[2].ShouldBe(work.IdempotencyKey);
        }
        await Posting().Execute(work.ReserveHistoryId, work.ClaimId, work.IdempotencyKey);
        await filter.HandleAsync(work, "failed-1", "late failure");
        await using var check = fixture.Create();
        (await check.ReserveHistory.SingleAsync(h => h.Id == work.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Posted);
        (await check.ClaimAuditLog.CountAsync(a => a.ClaimId == work.ClaimId && a.EventType == "GL_POSTING_FAILED")).ShouldBe(1);
    }

    [SqlServerFact]
    public async Task ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt()
    {
        foreach (var commit in new[] { false, true })
        {
            var client = new RecordingClient();
            await using var db = fixture.Create();
            await using var unit = new UnitOfWork(db);
            await unit.BeginAsync();
            var claim = await NewClaim(db);
            var component = AddReserve(db, claim, ReserveApprovalStatus.AutoApproved);
            var notification = new GlPostingNotificationHandler(new GlPostingScheduler(unit, client));
            foreach (var e in component.DomainEvents) await notification.Handle(new DomainEventNotification(e), default);
            client.Jobs.ShouldBeEmpty();
            if (!commit)
            {
                await unit.RollbackAsync();
                client.Jobs.ShouldBeEmpty();
                continue;
            }
            client.OnCreate = () =>
            {
                using var visible = fixture.Create();
                visible.ReserveHistory.Any(h => h.Id == component.ReserveHistory.Single().Id).ShouldBeTrue();
                unit.HasActiveTransaction.ShouldBeFalse();
            };
            await unit.CommitAsync(new TestDispatcher());
            client.Jobs.ShouldHaveSingleItem();
        }
    }

    [SqlServerFact]
    public async Task RecoveryFindsOnlyOldApprovedPendingUnscheduledRowsIncludingFailedRetryEnqueue()
    {
        var eligible = await SeedReserve();
        var pending = await SeedReserve(ReserveApprovalStatus.PendingApproval);
        var posted = await SeedReserve();
        await Posting().Execute(posted.ReserveHistoryId, posted.ClaimId, posted.IdempotencyKey);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(3);
        var fresh = await SeedReserve();
        var works = await Store().FindUnscheduledAsync();
        works.ShouldContain(eligible);
        works.ShouldNotContain(pending);
        works.ShouldNotContain(posted);
        works.ShouldNotContain(fresh);
        var client = new RecordingClient();
        await new GlPostingRecoveryJob(Store(), client).Execute();
        client.Jobs.ShouldContain(j => (Guid)j.Args[0] == eligible.ReserveHistoryId && (string)j.Args[2] == eligible.IdempotencyKey);
        (await Store().FailAsync(eligible, "failed", "test")).ShouldBeTrue();
        await using (var db = fixture.Create())
        await using (var unit = new UnitOfWork(db))
        {
            await unit.BeginAsync();
            var failing = new RecordingClient { ThrowOnCreate = true };
            await new ReservePostingRetry(new ClaimRepository(db), new ReserveComponentRepository(db),
                new GlPostingScheduler(unit, failing), fixture.Clock).ExecuteAsync(new(eligible.ClaimId, eligible.ReserveHistoryId));
            (await unit.CommitAsync(new TestDispatcher())).PostCommitErrors.ShouldHaveSingleItem();
        }
        (await Store().FindUnscheduledAsync()).ShouldContain(eligible);
    }

    [SqlServerFact]
    public async Task SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus()
    {
        var cases = new List<(Guid Id, ClaimStatus Status, bool Flag)>();
        foreach (var status in new[] { ClaimStatus.Draft, ClaimStatus.Open, ClaimStatus.Closed, ClaimStatus.UnderInvestigation })
            cases.Add((await SeedClaim(status, fixture.Clock.UtcNow.AddHours(-49)), status, status is ClaimStatus.Draft or ClaimStatus.Open));
        cases.Add((await SeedClaim(ClaimStatus.Draft, fixture.Clock.UtcNow.AddHours(-47)), ClaimStatus.Draft, false));
        cases.Add((await SeedClaim(ClaimStatus.Open, fixture.Clock.UtcNow.AddHours(-48)), ClaimStatus.Open, false));
        cases.Add((await SeedClaim(ClaimStatus.Draft, fixture.Clock.UtcNow.AddDays(-3), deleted: true), ClaimStatus.Draft, false));
        var otherTenant = Guid.NewGuid();
        cases.Add((await SeedClaim(ClaimStatus.Draft, fixture.Clock.UtcNow.AddDays(-3), tenant: otherTenant), ClaimStatus.Draft, true));
        cases.Add((await SeedClaim(ClaimStatus.Draft, null, fixture.Clock.UtcNow.AddDays(-3)), ClaimStatus.Draft, true));
        cases.Add((await SeedClaim(ClaimStatus.Draft, null), ClaimStatus.Draft, false));
        await using var before = fixture.Create();
        var original = await before.Claims.IgnoreQueryFilters().Where(c => cases.Select(x => x.Id).Contains(c.Id))
            .Select(c => new { c.Id, Updated = EF.Property<DateTimeOffset?>(c, "UpdatedAt") }).ToDictionaryAsync(c => c.Id);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => new SlaMonitoringJob(Store()).Execute()));
        await new SlaMonitoringJob(Store()).Execute();
        await Check(1);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(23);
        await new SlaMonitoringJob(Store()).Execute();
        // Fresh cases have now aged into eligibility; previously breached claims remain deduped.
        await using (var db = fixture.Create())
            foreach (var c in cases.Where(c => c.Flag))
                (await db.ClaimAuditLog.IgnoreQueryFilters().CountAsync(a => a.ClaimId == c.Id && a.EventType == "SLA_BREACH_DETECTED")).ShouldBe(1);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(1);
        await Task.WhenAll(new SlaMonitoringJob(Store()).Execute(), new SlaMonitoringJob(Store()).Execute());
        await using var last = fixture.Create();
        foreach (var c in cases.Where(c => c.Flag))
            (await last.ClaimAuditLog.IgnoreQueryFilters().CountAsync(a => a.ClaimId == c.Id && a.EventType == "SLA_BREACH_DETECTED")).ShouldBe(2);

        async Task Check(int count)
        {
            await using var db = fixture.Create();
            foreach (var c in cases)
            {
                var row = await db.Claims.IgnoreQueryFilters().SingleAsync(x => x.Id == c.Id);
                row.Status.ShouldBe(c.Status);
                db.Entry(row).Property<DateTimeOffset?>("UpdatedAt").CurrentValue.ShouldBe(original[c.Id].Updated);
                (row.SlaBreachedAt is not null).ShouldBe(c.Flag);
                (await db.ClaimAuditLog.IgnoreQueryFilters().CountAsync(a => a.ClaimId == c.Id && a.EventType == "SLA_BREACH_DETECTED")).ShouldBe(c.Flag ? count : 0);
            }
        }
    }

    [SqlServerFact]
    public async Task GlUsesStoredTenantAndFailureCountsAsClaimActivity()
    {
        var tenant = Guid.NewGuid();
        GlPostingWork foreign;
        await using (var db = fixture.Create(fixture.User with { OrganisationId = tenant, UserId = null }))
        await using (var unit = new UnitOfWork(db))
        {
            await unit.BeginAsync();
            db.Users.Add(new User { Id = _ids.NewId(), Name = "Other actor", Username = "other", Role = UserRole.Handler });
            db.CauseOfLossCodes.Add(new CauseOfLossCode { Id = _ids.NewId(), Code = "GL-" + tenant.ToString("N"), Name = "Other cause", IsActive = true });
            db.Policies.Add(new Policy
            {
                Id = _ids.NewId(),
                PolicyNumber = "OTHER-POLICY",
                ClientName = "Other tenant",
                EffectiveDate = new DateOnly(2026, 1, 1),
                ExpirationDate = new DateOnly(2026, 12, 31)
            });
            await unit.SaveChangesAsync();
            var claim = await NewClaim(db);
            var component = ClaimReserveComponent.Create(_ids.NewId(), claim, ReserveComponentType.Indemnity, []);
            var history = component.Submit(_ids.NewId(), 100, ReserveTransactionType.Add, "Other reserve", claim.AssignedHandlerId!.Value,
                UserRole.Handler, new(claim, new[] { component }), fixture.Clock).Transaction;
            db.ClaimReserveComponents.Add(component);
            await unit.CommitAsync(new TestDispatcher());
            foreign = new(history.Id, claim.Id, history.IdempotencyKey);
        }
        await Posting().Execute(foreign.ReserveHistoryId, foreign.ClaimId, foreign.IdempotencyKey);
        await using (var db = fixture.Create(fixture.User with { OrganisationId = tenant }))
        {
            (await db.ClaimAuditLog.CountAsync(a => a.ClaimId == foreign.ClaimId && a.EventType == "GL_POSTING_SIMULATED")).ShouldBe(1);
            (await db.ReserveHistory.SingleAsync(h => h.Id == foreign.ReserveHistoryId)).PostingStatus.ShouldBe(PostingStatus.Posted);
        }
        var work = await SeedReserve();
        await using (var db = fixture.Create())
            await db.Claims.Where(c => c.Id == work.ClaimId).ExecuteUpdateAsync(s => s.SetProperty(c => c.SlaBreachedAt, fixture.Clock.UtcNow));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        (await Store().FailAsync(work, "failed", "exhausted")).ShouldBeTrue();
        await using var check = fixture.Create();
        var changed = await check.Claims.SingleAsync(c => c.Id == work.ClaimId);
        changed.SlaBreachedAt.ShouldBeNull();
        check.Entry(changed).Property<DateTimeOffset?>("UpdatedAt").CurrentValue.ShouldBe(fixture.Clock.UtcNow);
        var rowVersion = changed.RowVersion.ToArray();
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        (await Store().FailAsync(work, "failed", "duplicate")).ShouldBeFalse();
        await check.Entry(changed).ReloadAsync();
        changed.RowVersion.ShouldBe(rowVersion);
    }

    [SqlServerFact]
    public async Task RealApiStartupInstallsStorageStartsServerAndRegistersRecurringJobs()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = fixture.ConnectionString,
                ["Storage:Provider"] = "LocalFileSystem",
                ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
            }));
        });
        using var client = factory.CreateClient();
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/hangfire")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var storage = factory.Services.GetRequiredService<JobStorage>();
        using var connection = storage.GetConnection();
        var limit = DateTime.UtcNow.AddSeconds(10);
        var recurring = connection.GetRecurringJobs();
        while (recurring.Count < 2 && DateTime.UtcNow < limit)
        {
            await Task.Delay(100);
            recurring = connection.GetRecurringJobs();
        }
        recurring.Single(j => j.Id == nameof(SlaMonitoringJob)).Cron.ShouldBe("*/15 * * * *");
        recurring.Single(j => j.Id == nameof(GlPostingRecoveryJob)).Cron.ShouldBe("*/5 * * * *");
        limit = DateTime.UtcNow.AddSeconds(10);
        while (storage.GetMonitoringApi().Servers().Count == 0 && DateTime.UtcNow < limit) await Task.Delay(100);
        storage.GetMonitoringApi().Servers().ShouldNotBeEmpty();
    }

    private async Task<GlPostingWork> SeedReserve(ReserveApprovalStatus status = ReserveApprovalStatus.AutoApproved)
    {
        await using var db = fixture.Create();
        await using var unit = new UnitOfWork(db);
        await unit.BeginAsync();
        var claim = await NewClaim(db);
        var component = AddReserve(db, claim, status);
        var row = component.ReserveHistory.Single();
        await unit.CommitAsync(new TestDispatcher());
        return new(row.Id, claim.Id, row.IdempotencyKey);
    }

    private ClaimReserveComponent AddReserve(ClaimsDbContext db, Claim claim, ReserveApprovalStatus status)
    {
        var component = ClaimReserveComponent.Create(_ids.NewId(), claim, ReserveComponentType.Indemnity, []);
        var row = component.Submit(_ids.NewId(), status == ReserveApprovalStatus.AutoApproved ? 100 : 20000,
            ReserveTransactionType.Add, "Initial estimate", fixture.User.UserId!.Value, UserRole.Handler,
            new(claim, new[] { component }), fixture.Clock).Transaction;
        var context = new ReserveOperationContext(claim, new[] { component });
        if (status == ReserveApprovalStatus.Approved) component.Approve(row.Id, SeedConstants.SupervisorId, UserRole.Supervisor, context, fixture.Clock);
        if (status == ReserveApprovalStatus.Rejected) component.Reject(row.Id, SeedConstants.SupervisorId, UserRole.Supervisor, "Rejected", context, fixture.Clock);
        if (status == ReserveApprovalStatus.Cancelled) component.Retract(row.Id, fixture.User.UserId.Value, context, fixture.Clock);
        db.ClaimReserveComponents.Add(component);
        return component;
    }

    private async Task<Claim> NewClaim(ClaimsDbContext db)
    {
        var id = _ids.NewId();
        var user = await db.Users.FirstAsync();
        var cause = await db.CauseOfLossCodes.FirstAsync();
        var policy = await db.Policies.FirstOrDefaultAsync();
        var loss = LossEvent.Create(_ids.NewId(), id, fixture.Clock.UtcNow.AddDays(-1),
            "The insured vehicle was damaged in a road collision.", cause.Code, true, fixture.Clock);
        var claim = Claim.Create(id, await new ClaimNumberGenerator(db, _ids).NextAsync(2026), user.Id,
            loss, [ClaimParty.Create(_ids.NewId(), id, PartyRole.Claimant, PartyType.Person, "Test", "Claimant")], fixture.Clock,
            policy is null ? null : new PolicyAssociation(policy.Id, policy.PolicyNumber, policy.ClientName,
                fixture.Clock.UtcNow.AddYears(-1), fixture.Clock.UtcNow.AddYears(1)));
        db.Claims.Add(claim);
        return claim;
    }

    private async Task<Guid> SeedClaim(ClaimStatus status, DateTimeOffset? updated,
        DateTimeOffset? created = null, bool deleted = false, Guid? tenant = null)
    {
        await using var db = fixture.Create(fixture.User with { OrganisationId = tenant ?? fixture.User.OrganisationId });
        await using var unit = new UnitOfWork(db);
        await unit.BeginAsync();
        if (tenant.HasValue)
        {
            db.Users.Add(new User { Id = _ids.NewId(), Name = "Other tenant", Username = "other", Role = UserRole.Handler });
            db.CauseOfLossCodes.Add(new CauseOfLossCode { Id = _ids.NewId(), Code = "OTHER", Name = "Other cause", IsActive = true });
            await unit.SaveChangesAsync();
        }
        var claim = await NewClaim(db);
        if (status != ClaimStatus.Draft)
        {
            claim.TransitionTo(ClaimStatus.Open, fixture.User.UserId!.Value, UserRole.Handler, fixture.Clock, [],
                new ClaimTransitionOptions(AcknowledgeWarnings: true));
            if (status != ClaimStatus.Open)
                claim.TransitionTo(status, fixture.User.UserId.Value, UserRole.Handler, fixture.Clock, []);
        }
        await unit.CommitAsync(new TestDispatcher());
        await db.Claims.Where(c => c.Id == claim.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(c => EF.Property<DateTimeOffset?>(c, "UpdatedAt"), updated)
            .SetProperty(c => EF.Property<DateTimeOffset>(c, "CreatedAt"), created ?? fixture.Clock.UtcNow)
            .SetProperty(c => EF.Property<bool>(c, "IsDeleted"), deleted));
        return claim.Id;
    }

    private sealed class RecordingClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = [];
        public Action? OnCreate { get; set; }
        public bool ThrowOnCreate { get; init; }
        public string Create(Job job, IState state)
        {
            OnCreate?.Invoke();
            if (ThrowOnCreate) throw new InvalidOperationException("Simulated enqueue outage");
            Jobs.Add(job);
            return $"job-{Jobs.Count}";
        }
        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();
    }

    private sealed class FailingIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => throw new InvalidOperationException("Audit write failed");
    }
}
