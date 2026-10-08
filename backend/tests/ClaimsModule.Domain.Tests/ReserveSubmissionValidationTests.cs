using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ReserveSubmissionValidationTests
{
    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void TerminalClaimSubmissionCannotWriteHistoryOrEvents(ClaimStatus status)
    {
        var fixture = new DomainFixture();
        fixture.Open();
        fixture.Claim.TransitionTo(status, fixture.Submitter, UserRole.Handler, fixture.Clock, [], new(Reason: "Complete"));
        var component = fixture.Component();
        fixture.Claim.ClearDomainEvents();
        var before = fixture.Claim.LastActivity;
        Should.Throw<DomainRuleViolationException>(() => fixture.Submit(component, 100)).Code.ShouldBe("CLAIM_STATUS");
        component.ReserveHistory.ShouldBeEmpty();
        component.DomainEvents.ShouldBeEmpty();
        fixture.Claim.LastActivity.ShouldBe(before);
    }
}
