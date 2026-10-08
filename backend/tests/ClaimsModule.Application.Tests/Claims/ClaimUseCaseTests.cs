using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using FluentValidation;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests.Claims;

public sealed class ClaimUseCaseTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IClaimNumberGenerator _numbers = Substitute.For<IClaimNumberGenerator>();
    private readonly IIdentifierGenerator _ids = Substitute.For<IIdentifierGenerator>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IPolicyQueries _policies = Substitute.For<IPolicyQueries>();
    private readonly IReferenceDataQueries _reference = Substitute.For<IReferenceDataQueries>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUserQueries _users = Substitute.For<IUserQueries>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly IMapper _mapper = new MapperConfiguration(x => x.AddProfile<ClaimMappingProfile>()).CreateMapper();
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public ClaimUseCaseTests()
    {
        _clock.UtcNow.Returns(Now);
        _ids.NewId().Returns(_ => Guid.NewGuid());
        _user.UserId.Returns(Guid.NewGuid());
        _user.Role.Returns(UserRole.Supervisor);
        _numbers.NextAsync(2026, Arg.Any<CancellationToken>()).Returns(ClaimNumber.Create(2026, 123));
        _reference.GetCausesAsync(cancellationToken: Arg.Any<CancellationToken>()).Returns([new(Guid.NewGuid(), "COL-FIRE", "Fire", "Property", 1)]);
    }

    private static CreateClaimCommand Valid() => new()
    {
        LossDate = Now.AddDays(-1),
        LossDescription = "A fire damaged the insured property.",
        CauseOfLossCode = "COL-FIRE",
        Parties = [new() { FirstName = "Ada", LastName = "Lovelace", Role = PartyRole.Claimant, Type = PartyType.Person }],
        RiskObjects = [new() { AssetType = AssetType.Property, AssetDescription = "House" }],
    };
    private CreateClaimCommandValidator Validator() => new(_clock, _reference, _policies, _mapper);
    private CreateClaimCommandHandler Creator() => new(_claims, _numbers, _ids, _clock, _user, _policies, _mapper,
        new ReserveSubmission(Substitute.For<IReserveComponentRepository>(), _ids, _clock, _user, Substitute.For<IValidationWarnings>()));
    private async Task<Claim> Create(CreateClaimCommand? request = null)
    {
        Claim? result = null;
        _claims.When(x => x.Add(Arg.Any<Claim>())).Do(x => result = x.Arg<Claim>());
        await Creator().Handle(request ?? Valid(), CancellationToken.None);
        result.ShouldNotBeNull();
        _claims.GetAsync(result.Id, Arg.Any<CancellationToken>()).Returns(result);
        return result;
    }

    public static IEnumerable<object[]> CriticalInputs()
    {
        var valid = Valid();
        yield return [valid with { LossDate = default }, "LossDate"];
        yield return [valid with { LossDate = Now.AddSeconds(1) }, "LossDate"];
        yield return [valid with { LossDescription = "Too short" }, "LossDescription"];
        yield return [valid with { LossDescription = new string(' ', 30) }, "LossDescription"];
        yield return [valid with { CauseOfLossCode = "INACTIVE" }, "CauseOfLossCode"];
        yield return [valid with { CauseOfLossCode = "" }, "CauseOfLossCode"];
        yield return [valid with { Parties = [] }, "Parties"];
        yield return [valid with { Parties = null! }, "Parties"];
        yield return [valid with { Parties = [valid.Parties[0] with { Role = PartyRole.Witness }] }, "Parties"];
        yield return [valid with { Parties = [valid.Parties[0] with { FirstName = "" }] }, "Parties[0].FirstName"];
        yield return [valid with { Parties = [valid.Parties[0] with { LastName = "" }] }, "Parties[0].LastName"];
        yield return [valid with { Parties = [valid.Parties[0] with { Email = "bad-email" }] }, "Parties[0].Email"];
        yield return [valid with { Parties = [new() { Type = PartyType.Company }] }, "Parties[0].CompanyName"];
        yield return [valid with { Parties = [valid.Parties[0] with { CompanyName = "Acme" }] }, "Parties[0].CompanyName"];
        yield return [valid with { Parties = [valid.Parties[0] with { Role = (PartyRole)99 }] }, "Parties[0].Role"];
        yield return [valid with { Parties = [valid.Parties[0] with { Type = (PartyType)99 }] }, "Parties[0].Type"];
        yield return [valid with { Parties = [null!] }, "Parties[0]"];
        yield return [valid with { RiskObjects = [new() { AssetType = (AssetType)99, AssetDescription = "Asset" }] }, "RiskObjects[0].AssetType"];
        yield return [valid with { RiskObjects = [new() { AssetDescription = "" }] }, "RiskObjects[0].AssetDescription"];
        yield return [valid with { RiskObjects = null! }, "RiskObjects"];
        yield return [valid with { EstimatedLossAmount = -1 }, "EstimatedLossAmount"];
        yield return [valid with { EstimatedLossAmount = 1.12345m }, "EstimatedLossAmount"];
        yield return [valid with { EstimatedLossAmount = 1_000_000_000_000_000m }, "EstimatedLossAmount"];
        yield return [valid with { Severity = (ClaimSeverity)99 }, "Severity"];
        yield return [valid with { PolicyId = Guid.NewGuid() }, "PolicyId"];
        yield return [valid with { InitialReserve = new(ReserveComponentType.Indemnity, 100, "Initial") }, "InitialReserve"];
    }

    [Theory]
    [MemberData(nameof(CriticalInputs))]
    public async Task CriticalRulesAreFieldKeyedAndDryRunUsesTheSameResults(CreateClaimCommand request, string field)
    {
        var validation = await Validator().ValidateAsync(request);
        validation.Errors.ShouldContain(x => x.PropertyName == field && x.Severity == FluentValidation.Severity.Error);
        var preview = await new ValidateClaimQueryHandler(Validator()).Handle(new(request), CancellationToken.None);
        preview.CriticalIssues.ShouldContain(x => x.Field == field);
        _claims.DidNotReceive().Add(Arg.Any<Claim>());
        await _numbers.DidNotReceive().NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSetsDefaultsChildrenWarningsAndEvents()
    {
        var claim = await Create(Valid() with { RiskObjects = [], EstimatedLossAmount = 0 });
        claim.Status.ShouldBe(ClaimStatus.Draft);
        claim.Severity.ShouldBe(ClaimSeverity.Standard);
        claim.AssignedHandlerId.ShouldBe(_user.UserId);
        claim.ClaimNumber.Value.ShouldBe("CLM-2026-0000123");
        claim.ReportedDate.ShouldBe(Now);
        claim.LossEvent.ReportDate.ShouldBe(Now);
        claim.Parties.ShouldHaveSingleItem().ClaimId.ShouldBe(claim.Id);
        claim.ValidationIssues.Select(x => x.Code).ShouldBe(["BR-C-06", "NO_RISK_OBJECTS"]);
        claim.DomainEvents.OfType<ClaimCreated>().ShouldHaveSingleItem();
        claim.DomainEvents.OfType<ValidationIssueAdded>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task PolicyDenormalizationAndUtcDateBoundaryMatchDryRunWarnings()
    {
        var id = Guid.NewGuid();
        _policies.GetAsync(id, Arg.Any<CancellationToken>()).Returns(new PolicyDto(id, "POL", "Client", new(2026, 1, 1), new(2026, 10, 5), "Active", []));
        // Local date is Oct 6, UTC date is Oct 5: the expiration day is inclusive.
        var request = Valid() with { PolicyId = id, LossDate = new(2026, 10, 6, 1, 0, 0, TimeSpan.FromHours(3)) };
        var preview = await new ValidateClaimQueryHandler(Validator()).Handle(new(request), CancellationToken.None);
        preview.CriticalIssues.ShouldBeEmpty();
        preview.Warnings.ShouldBeEmpty();
        var claim = await Create(request);
        claim.ValidationIssues.ShouldBeEmpty();
        claim.PolicyNumber.ShouldBe("POL");
        claim.ClientName.ShouldBe("Client");
        request = request with { LossDate = Now, RiskObjects = [] };
        preview = await new ValidateClaimQueryHandler(Validator()).Handle(new(request), CancellationToken.None);
        claim = await Create(request);
        preview.Warnings.Select(x => x.Code).ShouldBe(claim.ValidationIssues.Select(x => x.Code));
    }

    [Fact]
    public async Task CompanyAndEveryAssetTypeAreAccepted()
    {
        var request = Valid() with
        {
            Parties = [new() { Type = PartyType.Company, CompanyName = "Acme", Email = "claims@example.com" }],
            RiskObjects = Enum.GetValues<AssetType>().Select(x => new RiskObjectInput { AssetType = x, AssetDescription = "Asset" }).ToArray()
        };
        (await Validator().ValidateAsync(request)).Errors.ShouldAllBe(x => x.Severity == FluentValidation.Severity.Warning);
        (await Create(request)).RiskObjects.Count.ShouldBe(5);
    }

    [Fact]
    public async Task PartyHandlersProtectLastClaimantAndRemoveOnlyOnce()
    {
        var claim = await Create();
        var original = claim.Parties[0];
        var remove = new RemovePartyCommandHandler(_claims, _clock);
        (await Should.ThrowAsync<DomainRuleViolationException>(() => remove.Handle(new(claim.Id, original.Id), CancellationToken.None))).Code.ShouldBe("BR-P-01");
        var added = await new AddPartyCommandHandler(_claims, _ids, _clock, _mapper).Handle(new(claim.Id, Valid().Parties[0]), CancellationToken.None);
        added.IsActive.ShouldBeTrue();
        await remove.Handle(new(claim.Id, original.Id), CancellationToken.None);
        await remove.Handle(new(claim.Id, original.Id), CancellationToken.None);
        original.IsActive.ShouldBeFalse();
        claim.DomainEvents.OfType<ClaimPartyRemoved>().ShouldHaveSingleItem();
        await Should.ThrowAsync<NotFoundException>(() => remove.Handle(new(claim.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task NotesLinkAssignmentAndLiabilityTouchClaimAndWriteAudits()
    {
        var claim = await Create();
        var before = claim.LastActivity!.Revision;
        var policy = new PolicyDto(Guid.NewGuid(), "POL", "Client", new(2026, 1, 1), new(2026, 12, 31), "Active", []);
        _policies.GetAsync(policy.Id, Arg.Any<CancellationToken>()).Returns(policy);
        var manager = new UserDto(Guid.NewGuid(), "manager", "Manager", UserRole.Manager);
        _users.ListAsync(cancellationToken: Arg.Any<CancellationToken>()).Returns([manager]);
        await new UpdateClaimNotesCommandHandler(_claims, _clock, _audit).Handle(new(claim.Id, "Notes"), CancellationToken.None);
        await new LinkPolicyCommandHandler(_claims, _policies, _clock, _mapper).Handle(new(claim.Id, policy.Id), CancellationToken.None);
        await new AssignHandlerCommandHandler(_claims, _users, _user, _clock, _audit).Handle(new(claim.Id, manager.Id), CancellationToken.None);
        await new DetermineLiabilityCommandHandler(_claims, _clock).Handle(new(claim.Id, LiabilityDetermination.PartiallyAccepted, "Evidence"), CancellationToken.None);
        claim.Notes.ShouldBe("Notes");
        claim.AssignedHandlerId.ShouldBe(manager.Id);
        claim.LiabilityDetermination.ShouldBe(LiabilityDetermination.PartiallyAccepted);
        claim.LiabilityDeterminedAt.ShouldBe(Now);
        claim.LastActivity!.Revision.ShouldBe(before + 4);
        claim.ValidationIssues.Single().Status.ShouldBe(ValidationIssueStatus.Resolved);
        claim.DomainEvents.OfType<LiabilityDetermined>().Single().Notes.ShouldBe("Evidence");
        _audit.Received().Append(Arg.Is<AuditEntry>(x => x.EventType == "NOTES_UPDATED" && x.NewValue!.Contains("Notes")));
        _audit.Received().Append(Arg.Is<AuditEntry>(x => x.EventType == "HANDLER_ASSIGNED"));
    }

    [Fact]
    public async Task AssignmentChecksAuthorityAndTenantUserAndLinkRejectsUnknownPolicy()
    {
        var claim = await Create();
        var assign = new AssignHandlerCommandHandler(_claims, _users, _user, _clock, _audit);
        _user.Role.Returns(UserRole.Handler);
        await Should.ThrowAsync<InsufficientAuthorityException>(() => assign.Handle(new(claim.Id, Guid.NewGuid()), CancellationToken.None));
        _user.Role.Returns(UserRole.Manager);
        await Should.ThrowAsync<ValidationException>(() => assign.Handle(new(claim.Id, Guid.NewGuid()), CancellationToken.None));
        await Should.ThrowAsync<ValidationException>(() => new LinkPolicyCommandHandler(_claims, _policies, _clock, _mapper)
            .Handle(new(claim.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ReadsForwardFiltersAndReturnNotFoundForMissingClaims()
    {
        var queries = Substitute.For<IClaimQueries>();
        var audit = Substitute.For<IAuditQueries>();
        var list = new ListClaimsQuery(2, 5, [ClaimStatus.Draft], Now.AddDays(-3), Now, Guid.NewGuid(), "COL-FIRE", Guid.NewGuid(), " Client ");
        queries.ListAsync(Arg.Any<ClaimListRequest>(), Arg.Any<CancellationToken>()).Returns(new PageDto<ClaimListItemDto>([], 0, 2, 5));
        (await new ListClaimsQueryHandler(queries).Handle(list, CancellationToken.None)).Page.ShouldBe(2);
        await queries.Received().ListAsync(Arg.Is<ClaimListRequest>(x => x.Pagination.Page == 2 && x.Pagination.PageSize == 5
            && x.Statuses!.Single() == ClaimStatus.Draft && x.Search == "Client" && x.DateFrom == list.DateFrom
            && x.DateTo == list.DateTo && x.AssignedHandlerId == list.AssignedHandlerId && x.CauseOfLossCode == list.CauseOfLossCode
            && x.PolicyId == list.PolicyId), Arg.Any<CancellationToken>());
        await Should.ThrowAsync<NotFoundException>(() => new GetClaimDetailQueryHandler(queries, _storage).Handle(new(Guid.NewGuid()), CancellationToken.None));
        await Should.ThrowAsync<NotFoundException>(() => new GetClaimAuditQueryHandler(_claims, audit).Handle(new(Guid.NewGuid()), CancellationToken.None));
        var claim = await Create();
        audit.ListAsync(claim.Id, Arg.Any<PageRequest>(), Arg.Any<CancellationToken>()).Returns(new PageDto<AuditLogDto>([], 0, 2, 3));
        (await new GetClaimAuditQueryHandler(_claims, audit).Handle(new(claim.Id, 2, 3), CancellationToken.None)).Page.ShouldBe(2);
    }

    [Fact]
    public void MappingConfigurationIsValid() => ((Mapper)_mapper).ConfigurationProvider.AssertConfigurationIsValid();
}
