using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reference;
using ClaimsModule.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests.Reference;

public sealed class ReferenceQueryHandlerTests
{
    [Fact]
    public async Task CauseHandlerPassesTheOptionalPerilCategoryToReadQueries()
    {
        var queries = Substitute.For<IReferenceDataQueries>();
        var expected = new[] { new CauseOfLossDto(Guid.NewGuid(), "COL-FIRE", "Fire", "Property", 1) };
        queries.GetCausesAsync("Property", Arg.Any<CancellationToken>()).Returns(expected);

        var result = await new GetCauseOfLossCodesQueryHandler(queries).Handle(new("Property"), CancellationToken.None);

        result.ShouldBe(expected);
        await queries.Received(1).GetCausesAsync("Property", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatusAndUserHandlersDelegateToTheirReadInterfaces()
    {
        var referenceQueries = Substitute.For<IReferenceDataQueries>();
        var userQueries = Substitute.For<IUserQueries>();
        var statuses = new[] { new ClaimStatusDto(ClaimStatus.Open, []) };
        var users = new[] { new UserDto(Guid.NewGuid(), "handler", "Claims Handler", UserRole.Handler) };
        referenceQueries.GetStatusesAsync(Arg.Any<CancellationToken>()).Returns(statuses);
        userQueries.ListAsync(UserRole.Handler, Arg.Any<CancellationToken>()).Returns(users);

        (await new GetClaimStatusesQueryHandler(referenceQueries).Handle(new(), CancellationToken.None)).ShouldBe(statuses);
        (await new GetUsersQueryHandler(userQueries).Handle(new(UserRole.Handler), CancellationToken.None)).ShouldBe(users);
    }

    [Fact]
    public async Task SearchHandlerUsesEmptySearchForAnOmittedQuery()
    {
        var queries = Substitute.For<IPolicyQueries>();
        queries.SearchAsync(string.Empty, Arg.Any<CancellationToken>()).Returns([]);

        await new SearchPoliciesQueryHandler(queries).Handle(new(null), CancellationToken.None);

        await queries.Received(1).SearchAsync(string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CoverageHandlerReturnsCoveragesOrThrowsForAnUnknownPolicy()
    {
        var queries = Substitute.For<IPolicyQueries>();
        var id = Guid.NewGuid();
        queries.GetAsync(id, Arg.Any<CancellationToken>()).Returns(new PolicyDto(id, "POL-1", "Client", new(2026, 1, 1), new(2026, 12, 31), "Active", ["Vehicle"]));

        (await new GetPolicyCoverageQueryHandler(queries).Handle(new(id), CancellationToken.None)).ShouldBe(["Vehicle"]);

        var unknown = Guid.NewGuid();
        queries.GetAsync(unknown, Arg.Any<CancellationToken>()).Returns((PolicyDto?)null);
        await Should.ThrowAsync<NotFoundException>(() => new GetPolicyCoverageQueryHandler(queries).Handle(new(unknown), CancellationToken.None));
    }
}
