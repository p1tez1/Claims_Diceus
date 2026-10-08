using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.API;
using ClaimsModule.API.Controllers;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ReserveSqlApiTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly RecordingClient _jobs = new();
    private WebApplicationFactory<Program> Factory(Action<IServiceCollection>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = fixture.ConnectionString,
                ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
                ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
                ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
                ["Auth:DevLoginEnabled"] = "true"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBackgroundJobClient>();
                services.AddSingleton<IBackgroundJobClient>(_jobs);
                configure?.Invoke(services);
            });
        });

    private static async Task Login(HttpClient client, string username = "handler")
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new { username });
        var body = await Read<JsonElement>(response);
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("accessToken").GetString());
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private static CreateClaimCommand Intake(Guid? policyId, InitialReserveInput? initial = null) => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A collision damaged the insured vehicle.",
        CauseOfLossCode = "COL-VEH-COL",
        PolicyId = policyId,
        Parties = [new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Alex", LastName = "Smith" }],
        RiskObjects = [new() { AssetType = AssetType.Vehicle, AssetDescription = "Insured vehicle" }],
        InitialReserve = initial
    };
    private static async Task<Guid> Policy(HttpClient client) =>
        (await Read<List<PolicyDto>>(await client.GetAsync("/api/policies/search?q=meridian"))).Single().Id;
    private static async Task<ClaimDetailDto> Create(HttpClient client, InitialReserveInput? initial = null, bool withPolicy = true) =>
        await Read<ClaimDetailDto>(await client.PostAsJsonAsync("/api/claims", Intake(withPolicy ? await Policy(client) : null, initial), Json));
    private static Task<HttpResponseMessage> SubmitResponse(HttpClient client, Guid id, decimal amount,
        ReserveComponentType component = ReserveComponentType.Indemnity, ReserveTransactionType type = ReserveTransactionType.Add,
        Guid? resubmissionOfId = null) => client.PostAsJsonAsync($"/api/claims/{id}/reserves",
            new SubmitReserveRequest(component, amount, "Updated estimate", type, resubmissionOfId), Json);
    private static async Task<ReserveTransactionResult> Submit(HttpClient client, Guid id, decimal amount,
        ReserveComponentType component = ReserveComponentType.Indemnity, ReserveTransactionType type = ReserveTransactionType.Add,
        Guid? resubmissionOfId = null)
    {
        var response = await SubmitResponse(client, id, amount, component, type, resubmissionOfId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location.ShouldNotBeNull();
        return await Read<ReserveTransactionResult>(response);
    }
    private static Task<HttpResponseMessage> Action(HttpClient client, Guid claimId, Guid txnId, string action) =>
        client.PostAsync($"/api/claims/{claimId}/reserves/{txnId}/{action}", null);
    private static async Task<List<ReserveComponentDto>> Reserves(HttpClient client, Guid claimId) =>
        await Read<List<ReserveComponentDto>>(await client.GetAsync($"/api/claims/{claimId}/reserves"));
    private static async Task<List<AuditLogDto>> Audit(HttpClient client, Guid claimId) =>
        (await Read<PageDto<AuditLogDto>>(await client.GetAsync($"/api/claims/{claimId}/audit?pageSize=100"))).Items.ToList();
    private BackgroundJobStore Store() => new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options,
        fixture.Clock, new SequentialIdentifierGenerator());

    [SqlServerFact]
    public async Task ThresholdBoundariesAndRoleAuthorityThroughApi()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        foreach (var amount in new[] { 10000m, 10000.01m, 100000m, 100000.01m })
        {
            await Login(client);
            var claim = await Create(client);
            var before = _jobs.Jobs.Count;
            var submitted = await Submit(client, claim.Id, amount);
            submitted.ApprovalStatus.ShouldBe(amount == 10000 ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.PendingApproval);
            submitted.Transaction.PreviousBalance.ShouldBe(amount == 10000 ? 0 : null);
            _jobs.Jobs.Count.ShouldBe(before + (amount == 10000 ? 1 : 0));
            if (amount > 10000)
            {
                (await Action(client, claim.Id, submitted.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                await Login(client, "supervisor");
                var approval = await Action(client, claim.Id, submitted.Transaction.Id, "approve");
                approval.StatusCode.ShouldBe(amount <= 100000 ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, await approval.Content.ReadAsStringAsync());
                if (amount > 100000)
                {
                    await Login(client, "manager");
                    (await Action(client, claim.Id, submitted.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
                }
                _jobs.Jobs.Count.ShouldBe(before + 1);
            }
            var component = (await Reserves(client, claim.Id)).ShouldHaveSingleItem();
            component.CurrentAmount.ShouldBe(amount);
            component.PendingAmount.ShouldBe(0);
            component.History.Single().SubmittedByName.ShouldNotBeNullOrWhiteSpace();
            if (amount > 10000) component.History.Single().ApprovedByName.ShouldNotBeNullOrWhiteSpace();
            var audit = await Audit(client, claim.Id);
            audit.Count(x => x.EventType == "RESERVE_CREATED").ShouldBe(1);
            audit.Count(x => x.EventType == (amount == 10000 ? "RESERVE_AUTO_APPROVED" : "RESERVE_APPROVED")).ShouldBe(1);
            await AssertLedger(claim.Id);
        }
    }

    [SqlServerFact]
    public async Task SupervisorAndManagerCannotApproveOwnSubmission()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        foreach (var role in new[] { "supervisor", "manager" })
        {
            await Login(client, role);
            var claim = await Create(client);
            var reserve = await Submit(client, claim.Id, 50000);
            var response = await Action(client, claim.Id, reserve.Transaction.Id, "approve");
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.Content.ReadAsStringAsync()).ShouldContain("Self-approval is not permitted.");
            (await Reserves(client, claim.Id)).Single().History.Single().ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        }
        _jobs.Jobs.ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var pending = await Submit(client, claim.Id, 50000);
        var first = (await Reserves(client, claim.Id)).Single();
        (await Action(client, claim.Id, pending.Transaction.Id, "retract")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Action(client, claim.Id, pending.Transaction.Id, "retract")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var rejected = await Submit(client, claim.Id, 50000);
        await Login(client, "supervisor");
        (await Action(client, claim.Id, rejected.Transaction.Id, "retract")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves/{rejected.Transaction.Id}/reject", new { rejectionReason = " " }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves/{rejected.Transaction.Id}/reject", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves/{rejected.Transaction.Id}/reject", new { rejectionReason = "Estimate too high" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(client);
        var revised = await Submit(client, claim.Id, 30000, resubmissionOfId: rejected.Transaction.Id);
        var adjustment = await Read<ReserveTransactionResult>(await client.PutAsJsonAsync($"/api/claims/{claim.Id}/reserves/{first.Id}",
            new AdjustReserveRequest(1000, "Additional costs"), Json));
        adjustment.Transaction.TransactionType.ShouldBe(ReserveTransactionType.Adjust);
        var component = (await Reserves(client, claim.Id)).Single();
        component.History.Count.ShouldBe(4);
        component.History[0].ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
        component.History[1].ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        component.History[1].Amount.ShouldBe(50000);
        component.History[2].Amount.ShouldBe(30000);
        component.History[2].ResubmissionOfId.ShouldBe(rejected.Transaction.Id);
        component.History[2].PreviousBalance.ShouldBeNull();
        component.CurrentAmount.ShouldBe(1000);
        component.PendingAmount.ShouldBe(30000);
        component.RowVersion.ShouldNotBe(first.RowVersion);
        (await SubmitResponse(client, claim.Id, 20, resubmissionOfId: revised.Transaction.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await SubmitResponse(client, claim.Id, 20, type: ReserveTransactionType.Reverse, resubmissionOfId: rejected.Transaction.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Action(client, claim.Id, adjustment.Transaction.Id, "retract")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var audit = await Audit(client, claim.Id);
        audit.Count(x => x.EventType == "RESERVE_CREATED").ShouldBe(4);
        audit.Single(x => x.EventType == "RESERVE_REJECTED").OldValue!.ShouldContain("Estimate too high");
        audit.Count(x => x.EventType == "RESERVE_RETRACTED").ShouldBe(1);
        _jobs.Jobs.ShouldHaveSingleItem();
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task AllComponentSignsZeroPrecisionAndRequestValidationThroughApi()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        foreach (var component in Enum.GetValues<ReserveComponentType>())
        {
            if (component != ReserveComponentType.SubrogationRecoverable) await Submit(client, claim.Id, 100, component);
            foreach (var type in Enum.GetValues<ReserveTransactionType>())
                foreach (var amount in new[] { -1m, 0m, 1m })
                {
                    var allowed = amount != 0 && (type == ReserveTransactionType.Adjust || type == ReserveTransactionType.Reverse && amount > 0
                        || type == ReserveTransactionType.Add && (component == ReserveComponentType.SubrogationRecoverable ? amount < 0 : amount > 0));
                    var response = await SubmitResponse(client, claim.Id, amount, component, type);
                    response.StatusCode.ShouldBe(allowed ? HttpStatusCode.Created : HttpStatusCode.UnprocessableEntity);
                    if (allowed) (await Read<ReserveTransactionResult>(response)).Transaction.Amount.ShouldBe(type == ReserveTransactionType.Reverse ? -amount : amount);
                }
        }
        var request = new SubmitReserveRequest(ReserveComponentType.Expense, 10, "Estimate", ReserveTransactionType.Add);
        foreach (var invalid in new[] { request with { Amount = 1.12345m }, request with { Amount = 1000000000000000m },
            request with { ChangeReason = " " }, request with { ChangeReason = null }, request with { ChangeReason = new string('x', 501) },
            request with { ResubmissionOfId = Guid.Empty } })
            (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves", invalid, Json)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        foreach (var enumJson in new[] { "{\"component\":99,\"amount\":10,\"changeReason\":\"Estimate\",\"transactionType\":\"Add\"}",
            "{\"component\":\"Expense\",\"amount\":10,\"changeReason\":\"Estimate\",\"transactionType\":99}" })
            (await client.PostAsync($"/api/claims/{claim.Id}/reserves", new StringContent(enumJson, System.Text.Encoding.UTF8, "application/json")))
                .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var componentId = (await Reserves(client, claim.Id))[0].Id;
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/reserves/{componentId}", new { amount = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var initial = await Submit(client, claim.Id, 10000000);
        await Login(client, "manager");
        (await Action(client, claim.Id, initial.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(client);
        // Cap is the net signed sum, so a recovery offsets costs.
        await Submit(client, claim.Id, -1, ReserveComponentType.SubrogationRecoverable);
        (await Submit(client, claim.Id, 1, ReserveComponentType.Expense)).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        var capped = await Submit(client, claim.Id, 1, ReserveComponentType.Expense);
        capped.Warnings.ShouldHaveSingleItem().Code.ShouldBe("BR-R-05");
        capped.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        var jobs = _jobs.Jobs.Count;
        foreach (var role in new[] { "handler", "supervisor" })
        {
            await Login(client, role);
            (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserve-override", new { enabled = true })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        (await Action(client, claim.Id, capped.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        _jobs.Jobs.Count.ShouldBe(jobs);
        await Login(client, "manager");
        (await Action(client, claim.Id, capped.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserve-override", new { enabled = true })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(client, "supervisor");
        (await Action(client, claim.Id, capped.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(client);
        var aboveCeiling = await Submit(client, claim.Id, 10000000.01m);
        await Login(client, "manager");
        (await Action(client, claim.Id, aboveCeiling.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Audit(client, claim.Id)).Count(x => x.EventType == "CLAIM_RESERVE_OVERRIDE_SET").ShouldBe(1);
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        foreach (var amount in new[] { 10000m, 50000m, -200m })
        {
            var before = _jobs.Jobs.Count;
            var claim = await Create(client, new(amount < 0 ? ReserveComponentType.SubrogationRecoverable : ReserveComponentType.Indemnity, amount, "Initial estimate"));
            var component = claim.Reserves.ShouldHaveSingleItem();
            component.History.Single().ApprovalStatus.ShouldBe(amount == 50000 ? ReserveApprovalStatus.PendingApproval : ReserveApprovalStatus.AutoApproved);
            claim.Audit.Count(x => x.EventType == "RESERVE_CREATED").ShouldBe(1);
            _jobs.Jobs.Count.ShouldBe(before + (amount == 50000 ? 0 : 1));
            await AssertLedger(claim.Id);
        }
        var unknown = await Create(client, withPolicy: false);
        (await SubmitResponse(client, unknown.Id, 100)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/claims", Intake(null, new(ReserveComponentType.Indemnity, 100, "Initial")), Json))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/claims", Intake(Guid.NewGuid(), new(ReserveComponentType.Indemnity, 100, "Initial")), Json))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Reserves(client, unknown.Id)).ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task RetryOnlyFailedUsesOriginalKeyAndTouchesClaim()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var pending = await Submit(client, claim.Id, 50000);
        var automatic = await Submit(client, claim.Id, 100);
        (await Action(client, claim.Id, pending.Transaction.Id, "retry-posting")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Action(client, claim.Id, automatic.Transaction.Id, "retry-posting")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var work = new GlPostingWork(automatic.Transaction.Id, claim.Id, automatic.Transaction.IdempotencyKey);
        (await Store().FailAsync(work, "failed-job", "Exhausted retries")).ShouldBeTrue();
        var before = (await Reserves(client, claim.Id)).Single().RowVersion;
        (await Action(client, claim.Id, automatic.Transaction.Id, "retry-posting")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var component = (await Reserves(client, claim.Id)).Single();
        component.RowVersion.ShouldNotBe(before);
        component.History.Single(x => x.Id == automatic.Transaction.Id).PostingStatus.ShouldBe(PostingStatus.Pending);
        _jobs.Jobs.Count.ShouldBe(2);
        _jobs.Jobs.Last().Args[2].ShouldBe(automatic.Transaction.IdempotencyKey);
        (await Store().PostAsync(work, "successful-retry")).ShouldBeTrue();
        (await Action(client, claim.Id, automatic.Transaction.Id, "retry-posting")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task TerminalClaimsAndCrossClaimIdsAreRejectedAndPostReplayCreatesOneRow()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var first = await Submit(client, claim.Id, 100);
        (await Read<ReserveTransactionResult>(await SubmitResponse(client, claim.Id, 100))).Transaction.Id.ShouldBe(first.Transaction.Id);
        (await SubmitResponse(client, claim.Id, 101)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        _jobs.Jobs.ShouldHaveSingleItem();
        foreach (var status in new[] { ClaimStatus.Closed, ClaimStatus.Withdrawn })
        {
            var terminal = await Create(client);
            await using var db = fixture.Create();
            await using var unit = new UnitOfWork(db);
            await unit.BeginAsync();
            var domain = (await new ClaimRepository(db).GetAsync(terminal.Id))!;
            domain.TransitionTo(ClaimStatus.Open, fixture.User.UserId!.Value, UserRole.Handler, fixture.Clock, [], new(AcknowledgeWarnings: true));
            domain.TransitionTo(status, fixture.User.UserId!.Value, UserRole.Handler, fixture.Clock, [], new(Reason: "Complete"));
            await unit.CommitAsync(new TestDispatcher());
            (await SubmitResponse(client, terminal.Id, 100)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
        var other = await Create(client);
        await Login(client, "supervisor");
        (await Action(client, other.Id, first.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/claims/{Guid.NewGuid()}/reserves")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task TwoConcurrentApprovalsCommitExactlyOneAndEnqueueAfterCommit()
    {
        var gate = new ReadGate();
        using var factory = Factory(services =>
        {
            services.RemoveAll<IReserveComponentRepository>();
            services.AddScoped<IReserveComponentRepository>(p => new GatedRepository(p.GetRequiredService<ClaimsDbContext>(), gate));
        });
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var pending = await Submit(client, claim.Id, 50000);
        _jobs.OnCreate = job =>
        {
            using var db = fixture.Create();
            db.ReserveHistory.Single(x => x.Id == pending.Transaction.Id).ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
            db.ClaimAuditLog.Count(x => x.ClaimId == claim.Id && x.EventType == "RESERVE_APPROVED").ShouldBe(1);
        };
        await Login(client, "supervisor");
        gate.ClaimId = claim.Id;
        var results = await Task.WhenAll(Action(client, claim.Id, pending.Transaction.Id, "approve"), Action(client, claim.Id, pending.Transaction.Id, "approve"));
        results.Count(x => x.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        results.Count(x => x.StatusCode == HttpStatusCode.Conflict).ShouldBe(1, string.Join("; ", await Task.WhenAll(results.Select(x => x.Content.ReadAsStringAsync()))));
        _jobs.Jobs.ShouldHaveSingleItem();
        gate.Reads.ShouldBe(3); // Both initial reads plus exactly one reload/re-evaluation.
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task ConcurrentDifferentApprovalsReloadBalancesAndBothCommitOnce()
    {
        var gate = new ReadGate();
        using var factory = Factory(services =>
        {
            services.RemoveAll<IReserveComponentRepository>();
            services.AddScoped<IReserveComponentRepository>(p => new GatedRepository(p.GetRequiredService<ClaimsDbContext>(), gate));
        });
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var first = await Submit(client, claim.Id, 50000);
        var second = await Submit(client, claim.Id, 20000);
        await Login(client, "supervisor");
        gate.ClaimId = claim.Id;
        var responses = await Task.WhenAll(Action(client, claim.Id, first.Transaction.Id, "approve"), Action(client, claim.Id, second.Transaction.Id, "approve"));
        responses.ShouldAllBe(x => x.StatusCode == HttpStatusCode.NoContent);
        gate.Reads.ShouldBe(3);
        var component = (await Reserves(client, claim.Id)).Single();
        component.CurrentAmount.ShouldBe(70000);
        component.History.Count(x => x.PreviousBalance == 0).ShouldBe(1);
        component.History.Count(x => x.NewBalance == 70000).ShouldBe(1);
        _jobs.Jobs.Count.ShouldBe(2);
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task ConcurrentDifferentComponentsCannotBypassAggregateCap()
    {
        var gate = new ReadGate();
        using var factory = Factory(services =>
        {
            services.RemoveAll<IReserveComponentRepository>();
            services.AddScoped<IReserveComponentRepository>(p => new GatedRepository(p.GetRequiredService<ClaimsDbContext>(), gate));
        });
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var baseReserve = await Submit(client, claim.Id, 9950000);
        await Login(client, "manager");
        (await Action(client, claim.Id, baseReserve.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(client);
        var first = await Submit(client, claim.Id, 40000, ReserveComponentType.Expense);
        var second = await Submit(client, claim.Id, 40000, ReserveComponentType.ALAE);
        await Login(client, "supervisor");
        gate.ClaimId = claim.Id;
        var responses = await Task.WhenAll(Action(client, claim.Id, first.Transaction.Id, "approve"), Action(client, claim.Id, second.Transaction.Id, "approve"));
        responses.Count(x => x.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Count(x => x.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(1);
        _jobs.Jobs.Count.ShouldBe(2); // Base reserve and exactly one concurrent approval.
        (await Reserves(client, claim.Id)).Sum(x => x.CurrentAmount).ShouldBe(9990000);
        await AssertLedger(claim.Id);
    }

    [SqlServerFact]
    public async Task RepeatedConflictRetriesOnceThenReturns409WithoutJobsOrAudit()
    {
        Guid target = Guid.Empty;
        var reads = 0;
        using var factory = Factory(services =>
        {
            services.RemoveAll<IReserveComponentRepository>();
            services.AddScoped<IReserveComponentRepository>(p => new InterveningRepository(p.GetRequiredService<ClaimsDbContext>(), async id =>
            {
                if (id != target) return;
                Interlocked.Increment(ref reads);
                await using var rival = fixture.Create();
                await using var unit = new UnitOfWork(rival);
                await unit.BeginAsync();
                var claim = (await new ClaimRepository(rival).GetAsync(id))!;
                claim.UpdateNotes("Concurrent activity " + reads, fixture.Clock);
                await unit.CommitAsync(new TestDispatcher());
            }));
        });
        using var client = factory.CreateClient();
        await Login(client);
        var created = await Create(client);
        var pending = await Submit(client, created.Id, 50000);
        target = created.Id;
        await Login(client, "supervisor");
        (await Action(client, created.Id, pending.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        reads.ShouldBe(2);
        _jobs.Jobs.ShouldBeEmpty();
        (await Audit(client, created.Id)).ShouldNotContain(x => x.EventType == "RESERVE_APPROVED");
        (await Reserves(client, created.Id)).Single().History.Single().ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
    }

    [SqlServerFact]
    public async Task ConcurrentSubmissionsReuseOneComponentAndAppendDistinctSequences()
    {
        foreach (var firstUse in new[] { true, false })
        {
            var gate = new ReadGate();
            using var factory = Factory(services =>
            {
                services.RemoveAll<IReserveComponentRepository>();
                services.AddScoped<IReserveComponentRepository>(p => new GatedRepository(p.GetRequiredService<ClaimsDbContext>(), gate));
            });
            using var client = factory.CreateClient();
            await Login(client);
            var claim = await Create(client);
            if (!firstUse) await Submit(client, claim.Id, 40000);
            gate.ClaimId = claim.Id;
            var responses = await Task.WhenAll(SubmitResponse(client, claim.Id, 20000), SubmitResponse(client, claim.Id, 30000));
            responses.ShouldAllBe(x => x.StatusCode == HttpStatusCode.Created, string.Join("; ", await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))));
            var component = (await Reserves(client, claim.Id)).ShouldHaveSingleItem();
            component.History.Count.ShouldBe(firstUse ? 2 : 3);
            component.History.Select(x => x.ChangeSequence).ShouldBe(Enumerable.Range(1, component.History.Count));
            component.CurrentAmount.ShouldBe(0);
            component.PendingAmount.ShouldBe(firstUse ? 50000 : 90000);
            await AssertLedger(claim.Id);
        }
        _jobs.Jobs.ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task OtherTenantAndSoftDeletedClaimsAreInvisibleToReserveEndpoints()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var pending = await Submit(client, claim.Id, 50000);
        var settings = factory.Services.GetRequiredService<IConfiguration>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Authentication.CreateToken(
            new(Guid.NewGuid(), "other-manager", "Other manager", UserRole.Manager), Guid.NewGuid(), settings));
        (await client.GetAsync($"/api/claims/{claim.Id}/reserves")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SubmitResponse(client, claim.Id, 10)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Action(client, claim.Id, pending.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Action(client, claim.Id, pending.Transaction.Id, "retry-posting")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserve-override", new { enabled = true })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using (var db = fixture.Create())
        await using (var unit = new UnitOfWork(db))
        {
            await unit.BeginAsync();
            db.Claims.Remove((await new ClaimRepository(db).GetAsync(claim.Id))!);
            await unit.CommitAsync(new TestDispatcher());
        }
        await Login(client);
        (await client.GetAsync($"/api/claims/{claim.Id}/reserves")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SubmitResponse(client, claim.Id, 10)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _jobs.Jobs.ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task InitialReserveAuditFailureRollsBackClaimNumberHistoryAndJob()
    {
        using var factory = Factory(services =>
        {
            services.RemoveAll<IAuditLogService>();
            services.AddScoped<IAuditLogService>(p => new FailingReserveAudit(new AuditLogService(p.GetRequiredService<ClaimsDbContext>(), p.GetRequiredService<IIdentifierGenerator>())));
        });
        using var client = factory.CreateClient();
        await Login(client);
        await using var db = fixture.Create();
        var beforeClaims = await db.Claims.CountAsync();
        var beforeHistory = await db.ReserveHistory.CountAsync();
        var year = DateTimeOffset.UtcNow.Year;
        var beforeNumber = await db.ClaimNumberSequences.Where(x => x.Year == year).Select(x => (int?)x.LastValue).SingleOrDefaultAsync();
        (await client.PostAsJsonAsync("/api/claims", Intake(await Policy(client), new(ReserveComponentType.Indemnity, 100, "Initial")), Json))
            .StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await db.Claims.CountAsync()).ShouldBe(beforeClaims);
        (await db.ReserveHistory.CountAsync()).ShouldBe(beforeHistory);
        (await db.ClaimNumberSequences.Where(x => x.Year == year).Select(x => (int?)x.LastValue).SingleOrDefaultAsync()).ShouldBe(beforeNumber);
        _jobs.Jobs.ShouldBeEmpty();
    }

    [SqlServerFact]
    public async Task ApprovalOrderBalancesAndNegativeBalanceRecheckArePreserved()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var first = await Submit(client, claim.Id, 50000);
        var second = await Submit(client, claim.Id, 20000);
        await Login(client, "supervisor");
        (await Action(client, claim.Id, second.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Action(client, claim.Id, first.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var history = (await Reserves(client, claim.Id)).Single().History;
        history[0].PreviousBalance.ShouldBe(20000);
        history[0].NewBalance.ShouldBe(70000);
        history[1].PreviousBalance.ShouldBe(0);
        history[1].NewBalance.ShouldBe(20000);
        await Login(client);
        var reduction = await Submit(client, claim.Id, -60000, type: ReserveTransactionType.Adjust);
        var later = await Submit(client, claim.Id, 20000, type: ReserveTransactionType.Reverse);
        await Login(client, "supervisor");
        (await Action(client, claim.Id, later.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Action(client, claim.Id, reduction.Transaction.Id, "approve")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Reserves(client, claim.Id)).Single().CurrentAmount.ShouldBe(50000);
        await AssertLedger(claim.Id);
    }

    private async Task AssertLedger(Guid claimId)
    {
        await using var db = fixture.Create();
        foreach (var component in await db.ClaimReserveComponents.Include(x => x.ReserveHistory).Where(x => x.ClaimId == claimId).ToListAsync())
            component.CurrentAmount.ShouldBe(component.ReserveHistory.Where(x => x.IsApproved).Sum(x => x.Amount));
    }

    private sealed class RecordingClient : IBackgroundJobClient
    {
        public ConcurrentQueue<Job> Jobs { get; } = new();
        public Action<Job>? OnCreate { get; set; }
        public string Create(Job job, IState state)
        {
            OnCreate?.Invoke(job);
            job.Type.ShouldBe(typeof(PostGLReserveChangeJob));
            Jobs.Enqueue(job);
            return $"job-{Jobs.Count}";
        }
        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();
    }
    private sealed class ReadGate
    {
        public Guid ClaimId { get; set; }
        private int _reads;
        public int Reads => _reads;
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Wait(Guid id, CancellationToken token)
        {
            if (id != ClaimId) return;
            var count = Interlocked.Increment(ref _reads);
            if (count == 2) _bothRead.TrySetResult();
            if (count <= 2) await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
        }
    }
    private sealed class GatedRepository(ClaimsDbContext db, ReadGate gate) : IReserveComponentRepository
    {
        private readonly ReserveComponentRepository _inner = new(db);
        public void Add(ClaimReserveComponent component) => _inner.Add(component);
        public async Task<IReadOnlyList<ClaimReserveComponent>> GetForClaimAsync(Guid claimId, CancellationToken cancellationToken = default)
        {
            var result = await _inner.GetForClaimAsync(claimId, cancellationToken);
            await gate.Wait(claimId, cancellationToken);
            return result;
        }
    }
    private sealed class FailingReserveAudit(IAuditLogService inner) : IAuditLogService
    {
        public void Append(AuditEntry entry)
        {
            if (entry.EventType == "RESERVE_CREATED") throw new InvalidOperationException("Injected reserve audit failure");
            inner.Append(entry);
        }
    }
    private sealed class InterveningRepository(ClaimsDbContext db, Func<Guid, Task> intervene) : IReserveComponentRepository
    {
        private readonly ReserveComponentRepository _inner = new(db);
        public void Add(ClaimReserveComponent component) => _inner.Add(component);
        public async Task<IReadOnlyList<ClaimReserveComponent>> GetForClaimAsync(Guid claimId, CancellationToken cancellationToken = default)
        {
            var result = await _inner.GetForClaimAsync(claimId, cancellationToken);
            await intervene(claimId);
            return result;
        }
    }
}
