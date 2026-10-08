using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.API;
using ClaimsModule.API.Controllers;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Seeding;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class BackendE2eScenarioTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SqlServerFact]
    public async Task FnolHappyPathPersistsPartiesAndRiskObjectsThroughDetailView()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var policy = await Policy(scenario.Client);
        var claim = await Create(scenario.Client, Intake(policy.Id) with
        {
            Parties =
            [
                new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Ada", LastName = "Lovelace" },
                new() { Role = PartyRole.Witness, Type = PartyType.Person, FirstName = "Grace", LastName = "Hopper" }
            ],
            RiskObjects =
            [
                new() { AssetType = AssetType.Vehicle, AssetDescription = "2024 sedan", IsPrimary = true },
                new() { AssetType = AssetType.Property, AssetDescription = "Roadside barrier" }
            ]
        });

        var detail = await Get<ClaimDetailDto>(scenario.Client, $"/api/claims/{claim.Id}");
        detail.Parties.Count.ShouldBe(2);
        detail.RiskObjects.Count.ShouldBe(2);
        detail.PolicyId.ShouldBe(policy.Id);
        detail.Audit.ShouldContain(x => x.EventType == "CLAIM_CREATED");
        detail.Audit.Where(x => x.EventType == "PARTY_ADDED").Select(x => x.RelatedEntityId).Order()
            .ShouldBe(detail.Parties.Select(x => (Guid?)x.Id).Order());
    }

    [SqlServerFact]
    public async Task UnknownPolicyWarningCanBeLinkedAcknowledgedAndOpened()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake(null) with { RiskObjects = [] });
        claim.Warnings.Select(x => x.Code).ShouldContain("BR-C-06");
        var policy = await Policy(scenario.Client);
        (await scenario.Client.PutAsJsonAsync($"/api/claims/{claim.Id}/policy", new { policyId = policy.Id })).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        var opened = await scenario.Client.PutAsJsonAsync($"/api/claims/{claim.Id}/status",
            new TransitionStatusRequest(ClaimStatus.Open, null, true, null), Json);
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());
        var detail = await Get<ClaimDetailDto>(scenario.Client, $"/api/claims/{claim.Id}");
        detail.Status.ShouldBe(ClaimStatus.Open);
        detail.ValidationIssues.Single(x => x.Code == "BR-C-06").Status.ShouldBe(ValidationIssueStatus.Resolved);
        detail.ValidationIssues.Single(x => x.Code == "NO_RISK_OBJECTS").Status.ShouldBe(ValidationIssueStatus.Acknowledged);
    }

    [SqlServerFact]
    public async Task AutoApprovedReserveRunsGlJobAndWritesBothAuditEvents()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake((await Policy(scenario.Client)).Id));
        var reserve = await Submit(scenario.Client, claim.Id, 5000);
        reserve.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        await scenario.ExecuteGl(reserve.Transaction, claim.Id);
        var audit = await Audit(scenario.Client, claim.Id);
        audit.Select(x => x.EventType).ShouldContain("RESERVE_AUTO_APPROVED");
        audit.Select(x => x.EventType).ShouldContain("GL_POSTING_SIMULATED");
    }

    [SqlServerFact]
    public async Task PendingReserveSupervisorApprovalRunsGlPosting()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake((await Policy(scenario.Client)).Id));
        var reserve = await Submit(scenario.Client, claim.Id, 25000);
        reserve.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        await Login(scenario.Client, "supervisor");
        (await scenario.Client.PostAsync($"/api/claims/{claim.Id}/reserves/{reserve.Transaction.Id}/approve", null)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        await scenario.ExecuteGl(reserve.Transaction, claim.Id);
        (await Get<List<ReserveComponentDto>>(scenario.Client, $"/api/claims/{claim.Id}/reserves"))
            .Single().History.Single().PostingStatus.ShouldBe(PostingStatus.Posted);
    }

    [SqlServerFact]
    public async Task RejectedReserveCanBeResubmittedAsNewRetainedTransaction()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake((await Policy(scenario.Client)).Id));
        var rejected = await Submit(scenario.Client, claim.Id, 30000);
        await Login(scenario.Client, "supervisor");
        (await scenario.Client.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves/{rejected.Transaction.Id}/reject",
            new RejectReserveRequest("Estimate requires revision"), Json)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Login(scenario.Client);
        var revised = await Submit(scenario.Client, claim.Id, 9000, rejected.Transaction.Id);
        revised.Transaction.ResubmissionOfId.ShouldBe(rejected.Transaction.Id);
        var history = (await Get<List<ReserveComponentDto>>(scenario.Client, $"/api/claims/{claim.Id}/reserves")).Single().History;
        history.Count.ShouldBe(2);
        history.Single(x => x.Id == rejected.Transaction.Id).ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        history.Single(x => x.Id == revised.Transaction.Id).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
    }

    [SqlServerFact]
    public async Task DocumentUploadAppearsInDocumentList()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake(null));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\n%%EOF"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "evidence.pdf");
        form.Add(new StringContent("Evidence"), "documentType");
        var upload = await scenario.Client.PostAsync($"/api/claims/{claim.Id}/documents", form);
        upload.StatusCode.ShouldBe(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var documents = await Get<List<DocumentDto>>(scenario.Client, $"/api/claims/{claim.Id}/documents");
        documents.ShouldHaveSingleItem().DocumentName.ShouldBe("evidence.pdf");
    }

    [SqlServerFact]
    public async Task FullLifecycleClosesAndReopensToOpen()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake((await Policy(scenario.Client)).Id));
        var reserve = await Submit(scenario.Client, claim.Id, 1000);
        await scenario.ExecuteGl(reserve.Transaction, claim.Id);
        await Move(ClaimStatus.Open);
        await Move(ClaimStatus.UnderInvestigation);
        (await scenario.Client.PutAsJsonAsync($"/api/claims/{claim.Id}/liability",
            new LiabilityRequest(LiabilityDetermination.Accepted, "Evidence reviewed"), Json)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Move(ClaimStatus.PendingPayment);
        await Move(ClaimStatus.Closed, justification: "Outstanding reserve accepted for closure");
        await Login(scenario.Client, "supervisor");
        await Move(ClaimStatus.Reopened, "New evidence received");
        (await Get<ClaimDetailDto>(scenario.Client, $"/api/claims/{claim.Id}")).Status.ShouldBe(ClaimStatus.Open);

        async Task Move(ClaimStatus status, string? reason = null, string? justification = null)
        {
            var response = await scenario.Client.PutAsJsonAsync($"/api/claims/{claim.Id}/status",
                new TransitionStatusRequest(status, reason, false, justification), Json);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }

    [SqlServerFact]
    public async Task SlaJobFlagsStaleClaimWithoutChangingStatus()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client);
        var claim = await Create(scenario.Client, Intake(null));
        await using (var db = scenario.CreateDb())
            await db.Claims.Where(x => x.Id == claim.Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => EF.Property<DateTimeOffset?>(x, "UpdatedAt"), scenario.Clock.UtcNow.AddHours(-49)));
        using (var scope = scenario.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>().Execute();
        var detail = await Get<ClaimDetailDto>(scenario.Client, $"/api/claims/{claim.Id}");
        detail.Status.ShouldBe(ClaimStatus.Draft);
        detail.SlaBreachedAt.ShouldBe(scenario.Clock.UtcNow);
        (await Audit(scenario.Client, claim.Id)).ShouldContain(x => x.EventType == "SLA_BREACH_DETECTED");
    }

    [SqlServerFact]
    public async Task SecondOrganisationTokenCannotSeeOrMutateFirstOrganisationClaim()
    {
        await using var scenario = await Scenario.Create(server.ConnectionString);
        await Login(scenario.Client, "manager");
        var claim = await Create(scenario.Client, Intake(null));
        scenario.UseToken(SeedConstants.ManagerId, UserRole.Manager, Guid.NewGuid());
        (await scenario.Client.GetAsync($"/api/claims/{claim.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Get<PageDto<ClaimListItemDto>>(scenario.Client, "/api/claims")).TotalCount.ShouldBe(0);
        (await scenario.Client.PutAsJsonAsync($"/api/claims/{claim.Id}/notes", new { notes = "cross tenant" })).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    private static CreateClaimCommand Intake(Guid? policyId) => new()
    {
        LossDate = new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero),
        LossDescription = "Rear-end collision damaged the insured vehicle.",
        CauseOfLossCode = "COL-VEH-COL",
        LossLocation = "Kyiv",
        PolicyId = policyId,
        Parties = [new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Ada", LastName = "Lovelace" }],
        RiskObjects = [new() { AssetType = AssetType.Vehicle, AssetDescription = "2024 sedan", IsPrimary = true }]
    };

    private static async Task Login(HttpClient client, string username = "handler")
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new { username });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static async Task<PolicyDto> Policy(HttpClient client) =>
        (await Get<List<PolicyDto>>(client, "/api/policies/search?q=meridian")).Single();

    private static async Task<ClaimDetailDto> Create(HttpClient client, CreateClaimCommand request)
    {
        var response = await client.PostAsJsonAsync("/api/claims", request, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>(Json))!;
    }

    private static async Task<ReserveTransactionResult> Submit(HttpClient client, Guid claimId, decimal amount,
        Guid? resubmissionOfId = null)
    {
        var response = await client.PostAsJsonAsync($"/api/claims/{claimId}/reserves",
            new SubmitReserveRequest(ReserveComponentType.Indemnity, amount, "Updated estimate", ReserveTransactionType.Add,
                resubmissionOfId), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ReserveTransactionResult>(Json))!;
    }

    private static async Task<List<AuditLogDto>> Audit(HttpClient client, Guid claimId) =>
        (await Get<PageDto<AuditLogDto>>(client, $"/api/claims/{claimId}/audit?pageSize=100")).Items.ToList();

    private static async Task<T> Get<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private sealed class Scenario : IAsyncDisposable
    {
        private const string SigningKey = "test-only-signing-key-with-more-than-32-characters";
        private readonly string _storage;
        public string ConnectionString { get; }
        public TestClock Clock { get; } = new() { UtcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero) };
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }

        private Scenario(string connectionString, string storage)
        {
            ConnectionString = connectionString;
            _storage = storage;
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings(connectionString, storage)));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IClock>();
                    services.AddSingleton<IClock>(Clock);
                    services.RemoveAll<IBackgroundJobClient>();
                    services.AddSingleton<IBackgroundJobClient, RecordingJobClient>();
                });
            });
            Client = Factory.CreateClient();
        }

        public static async Task<Scenario> Create(string serverConnection)
        {
            var database = "ClaimsModule_E2E_" + Guid.NewGuid().ToString("N");
            var connection = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = database }.ConnectionString;
            var storage = Path.Combine(Path.GetTempPath(), "claims-e2e", database);
            var scenario = new Scenario(connection, storage);
            await using var db = scenario.CreateDb();
            await db.Database.MigrateAsync();
            return scenario;
        }

        public ClaimsDbContext CreateDb() => new(new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(ConnectionString).Options, new TestUser(), Clock);

        public async Task ExecuteGl(ReserveHistoryDto transaction, Guid claimId)
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>()
                .Execute(transaction.Id, claimId, transaction.IdempotencyKey);
        }

        public void UseToken(Guid userId, UserRole role, Guid organisationId)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(Settings(ConnectionString, _storage)).Build();
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Authentication.CreateToken(new(userId, role.ToString().ToLowerInvariant(), "Other tenant", role), organisationId, configuration));
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            if (!database.StartsWith("ClaimsModule_E2E_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a database not owned by an E2E scenario.");
            await using var db = CreateDb();
            await db.Database.EnsureDeletedAsync();
            if (Directory.Exists(_storage)) Directory.Delete(_storage, recursive: true);
        }

        private static Dictionary<string, string?> Settings(string connection, string storage) => new()
        {
            ["ConnectionStrings:Default"] = connection,
            ["Auth:Jwt:SigningKey"] = SigningKey,
            ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
            ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
            ["Auth:DevLoginEnabled"] = "true",
            ["Storage:Provider"] = "LocalFileSystem",
            ["Storage:Local:RootPath"] = storage
        };
    }

    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString("N");
        public bool ChangeState(string jobId, IState state, string expectedState) => false;
    }
}
