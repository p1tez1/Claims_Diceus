using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.API;
using ClaimsModule.API.Controllers;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Persistence;
using ClaimsModule.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ClaimsCoreSqlApiTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static CreateClaimCommand Valid() => new()
    {
        LossDate = DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription = "A collision damaged the insured vehicle.",
        CauseOfLossCode = "COL-VEH-COL",
        LossLocation = "Kyiv",
        EstimatedLossAmount = 1500,
        Parties = [new() { Role = PartyRole.Claimant, Type = PartyType.Person, FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" }],
        RiskObjects = [new() { AssetType = AssetType.Vehicle, AssetDescription = "Insured vehicle", IsPrimary = true }]
    };
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings()));
    });
    private Dictionary<string, string?> Settings() => new()
    {
        ["ConnectionStrings:Default"] = fixture.ConnectionString,
        ["Auth:Jwt:SigningKey"] = "test-only-signing-key-with-more-than-32-characters",
        ["Auth:Jwt:Issuer"] = "ClaimsModule.Tests",
        ["Auth:Jwt:Audience"] = "ClaimsModule.Tests.Client",
        ["Auth:DevLoginEnabled"] = "true"
    };
    private static async Task Login(HttpClient client, string username = "handler")
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new { username });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("accessToken").GetString());
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private static async Task<ClaimDetailDto> Create(HttpClient client, CreateClaimCommand? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/claims", request ?? Valid(), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location.ShouldNotBeNull();
        return await Read<ClaimDetailDto>(response);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var policy = (await Read<List<PolicyDto>>(await client.GetAsync("/api/policies/search?q=meridian"))).Single();
        var claim = await Create(client, Valid() with { PolicyId = policy.Id });
        var path = $"/api/claims/{claim.Id}";
        var invalid = await client.PutAsJsonAsync($"{path}/status", new TransitionStatusRequest(ClaimStatus.Closed, null, false, null), Json);
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var invalidBody = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        invalidBody.GetProperty("title").GetString().ShouldBe("Transition from Draft to Closed is not permitted.");
        invalidBody.GetProperty("errors").GetProperty("ValidNextStatuses")[0].GetString().ShouldBe("Open");

        async Task<ClaimStatus> Move(ClaimStatus target, string? reason = null, string? justification = null)
        {
            var response = await client.PutAsJsonAsync($"{path}/status", new TransitionStatusRequest(target, reason, false, justification), Json);
            var result = await Read<JsonElement>(response);
            return Enum.Parse<ClaimStatus>(result.GetProperty("status").GetString()!);
        }

        (await Move(ClaimStatus.Open)).ShouldBe(ClaimStatus.Open);
        (await Move(ClaimStatus.UnderInvestigation)).ShouldBe(ClaimStatus.UnderInvestigation);
        var reserve = await client.PostAsJsonAsync($"{path}/reserves",
            new SubmitReserveRequest(ReserveComponentType.Indemnity, 100, "Estimate", ReserveTransactionType.Add), Json);
        reserve.StatusCode.ShouldBe(HttpStatusCode.Created, await reserve.Content.ReadAsStringAsync());
        var liabilityBlocked = await Read<TransitionPreflightDto>(await client.GetAsync($"{path}/transitions/PendingPayment/preflight"));
        liabilityBlocked.Allowed.ShouldBeFalse();
        liabilityBlocked.BlockingConditions.Select(x => x.Message)
            .ShouldContain("Liability must be determined (Accepted or PartiallyAccepted) before PendingPayment.");
        (await client.PutAsJsonAsync($"{path}/liability", new LiabilityRequest(LiabilityDetermination.Accepted, "Reviewed"), Json))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Move(ClaimStatus.PendingPayment)).ShouldBe(ClaimStatus.PendingPayment);
        var closePreflight = await Read<TransitionPreflightDto>(await client.GetAsync($"{path}/transitions/Closed/preflight"));
        closePreflight.RequiresJustification.ShouldBeTrue();
        closePreflight.BlockingConditions.Select(x => x.Code).ShouldContain("CC-04");
        (await Move(ClaimStatus.Closed, justification: "Open balance accepted")).ShouldBe(ClaimStatus.Closed);
        await Login(client, "supervisor");
        (await Move(ClaimStatus.Reopened, reason: "New evidence")).ShouldBe(ClaimStatus.Open);
        var detail = await Read<ClaimDetailDto>(await client.GetAsync(path));
        detail.Status.ShouldBe(ClaimStatus.Open);
        detail.ClosedAt.ShouldBeNull();
        var audit = await Read<PageDto<AuditLogDto>>(await client.GetAsync($"{path}/audit?pageSize=100"));
        audit.Items.Count(x => x.EventType == "CLAIM_REOPENED").ShouldBe(1);
        audit.Items.Count(x => x.EventType == "STATUS_CHANGED").ShouldBe(6);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task CreateReturnsPersistedDetailWarningsAuditAndIdempotentReplay()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var correlation = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlation.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var request = Valid() with { RiskObjects = [] };
        var first = await client.PostAsJsonAsync("/api/claims", request, Json);
        var claim = await Read<ClaimDetailDto>(first);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        claim.ClaimNumber.ShouldMatch(@"^CLM-\d{4}-\d{7}$");
        claim.Status.ShouldBe(ClaimStatus.Draft);
        claim.Severity.ShouldBe(ClaimSeverity.Standard);
        claim.AssignedHandlerId.ShouldBe(SeedConstants.HandlerId);
        claim.LossEvent.LossLocation.ShouldBe("Kyiv");
        claim.LossEvent.ReportDate.ShouldBe(claim.ReportedDate);
        claim.Parties.ShouldHaveSingleItem().FirstName.ShouldBe("Ada");
        claim.Warnings.Select(x => x.Code).Order().ShouldBe(["BR-C-06", "NO_RISK_OBJECTS"]);
        claim.Warnings.Single(x => x.Code == "BR-C-06").Message.ShouldBe("No policy linked — claim requires policy association before financial actions are permitted.");
        claim.ValidationIssues.ShouldAllBe(x => x.Status == ValidationIssueStatus.Open);
        claim.Reserves.ShouldBeEmpty();
        claim.TotalReserves.ShouldBe(0);
        claim.PendingAmount.ShouldBe(0);
        claim.Documents.ShouldBeEmpty();
        claim.Audit.Single(x => x.EventType == "CLAIM_CREATED").CorrelationId.ShouldBe(correlation);
        claim.Audit.Count(x => x.EventType == "VALIDATION_ISSUE_ADDED").ShouldBe(2);
        var partyAudit = claim.Audit.Single(x => x.EventType == "PARTY_ADDED");
        partyAudit.RelatedEntityId.ShouldBe(claim.Parties.Single().Id);
        partyAudit.CorrelationId.ShouldBe(correlation);
        partyAudit.CreatedByUserId.ShouldBe(SeedConstants.HandlerId);
        var replay = await client.PostAsJsonAsync("/api/claims", request, Json);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync("/api/claims", request with { LossDescription = "A different loss description for the same key." }, Json))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var detail = await Read<ClaimDetailDto>(await client.GetAsync(first.Headers.Location));
        detail.Id.ShouldBe(claim.Id);
        await using var db = fixture.Create();
        (await db.Claims.CountAsync(x => x.Id == claim.Id)).ShouldBe(1);
        (await db.ClaimValidationIssues.CountAsync(x => x.ClaimId == claim.Id)).ShouldBe(2);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var valid = Valid();
        (CreateClaimCommand Request, string Field)[] cases =
        [
            (valid with { LossDate = default }, "LossDate"),
            (valid with { LossDate = new DateTimeOffset(1899, 12, 31, 23, 59, 59, TimeSpan.Zero) }, "LossDate"),
            (valid with { LossDate = DateTimeOffset.UtcNow.AddDays(1) }, "LossDate"),
            (valid with { LossDescription = "short" }, "LossDescription"),
            (valid with { CauseOfLossCode = "UNKNOWN" }, "CauseOfLossCode"),
            (valid with { CauseOfLossCode = "COL-FIRE" }, "CauseOfLossCode"),
            (valid with { Parties = [] }, "Parties"),
            (valid with { Parties = [valid.Parties[0] with { Role = PartyRole.Witness }] }, "Parties"),
            (valid with { Parties = [valid.Parties[0] with { FirstName = "" }] }, "Parties[0].FirstName"),
            (valid with { Parties = [valid.Parties[0] with { LastName = "" }] }, "Parties[0].LastName"),
            (valid with { Parties = [new() { Type = PartyType.Company }] }, "Parties[0].CompanyName"),
            (valid with { Parties = [valid.Parties[0] with { Email = "bad" }] }, "Parties[0].Email"),
            (valid with { Parties = [valid.Parties[0] with { Role = (PartyRole)99 }] }, "Parties[0].Role"),
            (valid with { Parties = [valid.Parties[0] with { Type = (PartyType)99 }] }, "Parties[0].Type"),
            (valid with { RiskObjects = [new() { AssetType = (AssetType)99, AssetDescription = "Asset" }] }, "RiskObjects[0].AssetType"),
            (valid with { RiskObjects = [new() { AssetDescription = "" }] }, "RiskObjects[0].AssetDescription"),
            (valid with { EstimatedLossAmount = -1 }, "EstimatedLossAmount"),
            (valid with { Severity = (ClaimSeverity)99 }, "Severity"),
            (valid with { PolicyId = Guid.NewGuid() }, "PolicyId"),
            (valid with { InitialReserve = new(ReserveComponentType.Indemnity, 100, "Initial") }, "InitialReserve")
        ];
        await using var db = fixture.Create();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            (await db.CauseOfLossCodes.SingleAsync(x => x.Code == "COL-FIRE")).IsActive = false;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var count = await db.Claims.CountAsync();
        foreach (var (request, field) in cases)
        {
            var response = await client.PostAsJsonAsync("/api/claims", request, Json);
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("type").GetString().ShouldBe("ValidationError");
            body.GetProperty("status").GetInt32().ShouldBe(422);
            body.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue(field);
        }
        (await db.Claims.CountAsync()).ShouldBe(count);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            (await db.CauseOfLossCodes.SingleAsync(x => x.Code == "COL-FIRE")).IsActive = true;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task DateBoundsReturnFieldKeyed422WithoutReachingTheClaimsQuery()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);

        foreach (var query in new[]
        {
            "dateFrom=1900-01-01T00:00:00Z", "dateTo=2100-12-31T23:59:59Z"
        })
            (await client.GetAsync("/api/claims?" + query)).StatusCode.ShouldBe(HttpStatusCode.OK);

        foreach (var (query, field) in new[]
        {
            ("dateFrom=1899-12-31", "DateFrom"),
            ("dateTo=2101-01-01", "DateTo"),
            ("dateFrom=222222-02-22", "DateFrom"),
            ("dateFrom=2026-02-23&dateTo=2026-02-22", "DateTo")
        })
        {
            var response = await client.GetAsync("/api/claims?" + query);
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("type").GetString().ShouldBe("ValidationError");
            body.GetProperty("status").GetInt32().ShouldBe(422);
            body.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
        }

        var oldClaim = Valid() with { LossDate = new DateTimeOffset(1899, 12, 31, 23, 59, 59, TimeSpan.Zero) };
        var preview = await Read<ClaimValidationDto>(await client.PostAsJsonAsync("/api/claims/validate", oldClaim, Json));
        preview.CriticalIssues.ShouldContain(x => x.Field == "LossDate" && x.Code == "DD-41");
        var create = await client.PostAsJsonAsync("/api/claims", oldClaim, Json);
        create.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>();
        createBody.GetProperty("errors").TryGetProperty("LossDate", out _).ShouldBeTrue();
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var policies = await Read<List<PolicyDto>>(await client.GetAsync("/api/policies/search?q=POL-2023-000099"));
        var request = Valid() with { PolicyId = policies.Single().Id, RiskObjects = [] };
        await using var db = fixture.Create();
        var before = await db.Claims.CountAsync();
        var records = await db.IdempotencyRecords.CountAsync();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var preview = await Read<ClaimValidationDto>(await client.PostAsJsonAsync("/api/claims/validate", request, Json));
        preview.CriticalIssues.ShouldBeEmpty();
        preview.Warnings.Select(x => x.Code).ShouldBe(["BR-C-02", "NO_RISK_OBJECTS"]);
        var invalidPreview = await Read<ClaimValidationDto>(await client.PostAsJsonAsync("/api/claims/validate", request with { Parties = [] }, Json));
        invalidPreview.CriticalIssues.Single().Code.ShouldBe("BR-C-03");
        (await db.Claims.CountAsync()).ShouldBe(before);
        (await db.IdempotencyRecords.CountAsync()).ShouldBe(records);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var claim = await Create(client, request);
        claim.PolicyNumber.ShouldBe("POL-2023-000099");
        claim.ClientName.ShouldBe("Archived Corp");
        claim.Warnings.Select(x => x.Code).Order().ShouldBe(preview.Warnings.Select(x => x.Code).Order());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ClaimNumbersAreUniqueUnderParallelCreates()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claims = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Create(client)));
        claims.Select(x => x.ClaimNumber).Distinct().Count().ShouldBe(20);
        claims.ShouldAllBe(x => System.Text.RegularExpressions.Regex.IsMatch(x.ClaimNumber, @"^CLM-\d{4}-\d{7}$"));
        var numbers = claims.Select(x => int.Parse(x.ClaimNumber[^7..], System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
        numbers.ShouldBe(Enumerable.Range(numbers[0], 20));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ListFiltersSearchPaginationAndSortAreAppliedInSql()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client, "supervisor");
        var policy = (await Read<List<PolicyDto>>(await client.GetAsync("/api/policies/search?q=meridian"))).Single();
        var date = new DateTimeOffset(2022, 7, 8, 12, 0, 0, TimeSpan.Zero);
        var claims = new List<ClaimDetailDto>();
        for (var i = 0; i < 5; i++)
            claims.Add(await Create(client, Valid() with { PolicyId = policy.Id, LossDate = date.AddHours(i), CauseOfLossCode = "COL-EQUIP" }));
        var filters = $"dateFrom=2022-07-08T00:00:00Z&dateTo=2022-07-09T00:00:00Z&policyId={policy.Id}&assignedHandlerId={SeedConstants.SupervisorId}&causeOfLossCode=COL-EQUIP&status=Draft&status=Open";
        var page = await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&page=1&pageSize=2"));
        page.TotalCount.ShouldBe(5);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(2);
        page.Items.Select(x => x.Id).ShouldBe(claims.TakeLast(2).Reverse().Select(x => x.Id));
        var page2 = await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&page=2&pageSize=2"));
        page2.Items.Select(x => x.Id).ShouldBe(claims.Skip(1).Take(2).Reverse().Select(x => x.Id));
        var numberSearch = await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&search={claims[0].ClaimNumber[^7..]}"));
        numberSearch.Items.ShouldHaveSingleItem().Id.ShouldBe(claims[0].Id);
        (await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&search=Meridian"))).TotalCount.ShouldBe(5);
        (await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&status=Closed"))).TotalCount.ShouldBe(5); // Draft/Open/Closed OR
        (await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?dateFrom=2022-07-08T00:00:00Z&dateTo=2022-07-09T00:00:00Z&status=Closed"))).TotalCount.ShouldBe(0);
        (await Read<PageDto<ClaimListItemDto>>(await client.GetAsync($"/api/claims?{filters}&page=4&pageSize=2"))).Items.ShouldBeEmpty();
        foreach (var query in new[] { "page=0", "pageSize=101", "dateFrom=2026-01-02&dateTo=2026-01-01" })
            (await client.GetAsync("/api/claims?" + query)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.GetAsync("/api/claims?status=99")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        var original = claim.Parties.Single().Id;
        (await client.DeleteAsync($"/api/claims/{claim.Id}/parties/{original}")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var party = new PartyInput { Type = PartyType.Company, Role = PartyRole.Claimant, CompanyName = "Acme" };
        var added = await Read<PartyDto>(await client.PostAsJsonAsync($"/api/claims/{claim.Id}/parties", party, Json));
        (await Read<PartyDto>(await client.PostAsJsonAsync($"/api/claims/{claim.Id}/parties", party, Json))).Id.ShouldBe(added.Id);
        (await client.DeleteAsync($"/api/claims/{claim.Id}/parties/{original}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/claims/{claim.Id}/parties/{original}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var detail = await Read<ClaimDetailDto>(await client.GetAsync($"/api/claims/{claim.Id}"));
        detail.Parties.Count.ShouldBe(2);
        detail.Parties.Single(x => x.Id == added.Id).IsActive.ShouldBeTrue();
        detail.Parties.Single(x => x.Id == original).IsActive.ShouldBeFalse();
        detail.Audit.Count(x => x.EventType == "PARTY_ADDED").ShouldBe(2);
        detail.Audit.Count(x => x.EventType == "PARTY_REMOVED").ShouldBe(1);
        await using var db = fixture.Create();
        (await db.ClaimParties.SingleAsync(x => x.Id == original)).IsActive.ShouldBeFalse();
        (await client.DeleteAsync($"/api/claims/{claim.Id}/parties/{added.Id}")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task NotesPolicyLiabilityAssigneeAndAuditFollowDecisionsAndTouch()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        await Login(client);
        var claim = await Create(client);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/assignee", new { assignedHandlerId = SeedConstants.ManagerId })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var users = await Read<List<UserDto>>(await client.GetAsync("/api/reference/users"));
        users.Count.ShouldBe(3);
        var policy = (await Read<List<PolicyDto>>(await client.GetAsync("/api/policies/search?q=meridian"))).Single();
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/policy", new { policyId = policy.Id })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/liability", new { determination = "Accepted", notes = "Evidence reviewed" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/liability", new { determination = 99 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/liability", new { notes = "Missing determination" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/policy", new { policyId = Guid.NewGuid() })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await Login(client, "supervisor");
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/assignee", new { assignedHandlerId = SeedConstants.ManagerId })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "ignored-on-put");
        for (var i = 0; i < 23; i++)
            (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/notes", new { notes = "Investigation note " + i })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var detail = await Read<ClaimDetailDto>(await client.GetAsync($"/api/claims/{claim.Id}"));
        detail.Notes.ShouldBe("Investigation note 22");
        detail.PolicyId.ShouldBe(policy.Id);
        detail.AssignedHandlerId.ShouldBe(SeedConstants.ManagerId);
        detail.LiabilityDetermination.ShouldBe("Accepted");
        detail.LiabilityDeterminedAt.ShouldNotBeNull();
        detail.UpdatedAt.ShouldNotBeNull();
        detail.RowVersion.ShouldNotBe(claim.RowVersion);
        detail.ValidationIssues.Single().Status.ShouldBe(ValidationIssueStatus.Resolved);
        detail.Audit.Count.ShouldBe(20);
        var audit = await Read<PageDto<AuditLogDto>>(await client.GetAsync($"/api/claims/{claim.Id}/audit?page=1&pageSize=100"));
        audit.TotalCount.ShouldBe(29); // Created, initial party, unknown policy warning, link, liability, assignment, 23 notes.
        audit.Items[0].NewValue!.ShouldContain("Investigation note 22");
        audit.Items.Select(x => x.CreatedAt).ShouldBe(audit.Items.Select(x => x.CreatedAt).OrderDescending());
        audit.Items.ShouldContain(x => x.EventType == "LIABILITY_DETERMINED" && x.NewValue!.Contains("Evidence reviewed"));
        audit.Items.ShouldContain(x => x.EventType == "POLICY_LINKED");
        var page2 = await Read<PageDto<AuditLogDto>>(await client.GetAsync($"/api/claims/{claim.Id}/audit?page=2&pageSize=5"));
        page2.Items.Select(x => x.Id).ShouldBe(audit.Items.Skip(5).Take(5).Select(x => x.Id));
        (await client.GetAsync($"/api/claims/{Guid.NewGuid()}/audit")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task AuthenticationTenantIsolationAndFailedCommandReplayAreEnforced()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/claims")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/claims", Valid(), Json)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await Login(client, "manager");
        var claim = await Create(client);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var missing = $"/api/claims/{Guid.NewGuid()}/parties";
        var first = await client.PostAsJsonAsync(missing, Valid().Parties[0], Json);
        first.StatusCode.ShouldBe(HttpStatusCode.NotFound, await first.Content.ReadAsStringAsync());
        var replay = await client.PostAsJsonAsync(missing, Valid().Parties[0], Json);
        replay.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await replay.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var config = new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Authentication.CreateToken(new(SeedConstants.ManagerId, "manager", "Other tenant", UserRole.Manager), Guid.NewGuid(), config));
        (await client.GetAsync($"/api/claims/{claim.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/claims/{claim.Id}/audit")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Read<PageDto<ClaimListItemDto>>(await client.GetAsync("/api/claims"))).TotalCount.ShouldBe(0);
        (await client.PutAsJsonAsync($"/api/claims/{claim.Id}/notes", new { notes = "Forbidden tenant" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
