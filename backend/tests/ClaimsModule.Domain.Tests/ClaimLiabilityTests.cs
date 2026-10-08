using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ClaimLiabilityTests
{
    [Theory]
    [InlineData(LiabilityDetermination.Accepted)]
    [InlineData(LiabilityDetermination.PartiallyAccepted)]
    [InlineData(LiabilityDetermination.Denied)]
    public void DeterminationRecordsTimeAuditAndActivity(LiabilityDetermination determination)
    {
        var fixture = new DomainFixture();
        fixture.Claim.FlagSlaBreach(fixture.Clock);
        fixture.Claim.DetermineLiability(determination, "Evidence reviewed", fixture.Clock);
        fixture.Claim.LiabilityDetermination.ShouldBe(determination);
        fixture.Claim.LiabilityDeterminedAt.ShouldBe(fixture.Clock.UtcNow);
        fixture.Claim.SlaBreachedAt.ShouldBeNull();
        fixture.Claim.DomainEvents.OfType<LiabilityDetermined>().Single().Notes.ShouldBe("Evidence reviewed");
    }

    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void TerminalClaimsRejectDeterminationWithoutMutation(ClaimStatus status)
    {
        var fixture = new DomainFixture();
        fixture.Open();
        fixture.Claim.TransitionTo(status, fixture.Submitter, UserRole.Handler, fixture.Clock, fixture.Components,
            new(Reason: "Completed"));
        var activity = fixture.Claim.LastActivity;
        Should.Throw<DomainRuleViolationException>(() => fixture.Claim.DetermineLiability(LiabilityDetermination.Accepted, null, fixture.Clock));
        fixture.Claim.LiabilityDetermination.ShouldBeNull();
        fixture.Claim.LastActivity.ShouldBe(activity);
    }
}
