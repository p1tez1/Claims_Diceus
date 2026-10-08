using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ReserveAuthorityPolicyTests
{
    public static TheoryData<decimal, UserRole?> BoundaryCases => new()
    {
        { 0.01m, null }, { 10_000m, null }, { 10_000.01m, UserRole.Supervisor },
        { 100_000m, UserRole.Supervisor }, { 100_000.01m, UserRole.Manager },
        { 10_000_000m, UserRole.Manager }, { 10_000_000.01m, UserRole.Manager },
        { -10_000m, null }, { -10_000.01m, UserRole.Supervisor },
        { -100_000m, UserRole.Supervisor }, { -100_000.01m, UserRole.Manager },
    };

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void BRR02ExactThresholdBoundariesUseAbsoluteDelta(decimal delta, UserRole? expected)
        => new ReserveAuthorityPolicy().RequiredApprover(delta).ShouldBe(expected);

    public static IEnumerable<object[]> AuthorityMatrix()
    {
        foreach (var role in Enum.GetValues<UserRole>())
            foreach (var amount in new[] { 1m, 10_000m, 10_000.01m, 100_000m, 100_000.01m, 10_000_000m, 10_000_000.01m })
                foreach (var sign in new[] { 1, -1 })
                {
                    var expected = role == UserRole.Supervisor && amount <= 100_000m
                        || role == UserRole.Manager && amount <= 10_000_000m;
                    yield return [amount * sign, role, expected];
                }
    }

    [Theory]
    [MemberData(nameof(AuthorityMatrix))]
    public void BRR02RoleAuthorityMatrix(decimal delta, UserRole role, bool expected)
    {
        var policy = new ReserveAuthorityPolicy();
        policy.CanApprove(delta, role).ShouldBe(expected);
        if (!expected) Should.Throw<InsufficientAuthorityException>(() => policy.EnsureCanApprove(delta, role));
    }

    [Fact]
    public void AuthorityLimitsCanBeOverriddenWithRecord()
    {
        var limits = new ReserveAuthorityLimits() with { AutoApproval = 50m, Supervisor = 100m, Manager = 200m };
        var policy = new ReserveAuthorityPolicy(limits);
        policy.RequiredApprover(50m).ShouldBeNull();
        policy.RequiredApprover(50.01m).ShouldBe(UserRole.Supervisor);
        policy.RequiredApprover(100.01m).ShouldBe(UserRole.Manager);
        policy.CanApprove(200.01m, UserRole.Manager).ShouldBeFalse();
    }

    [Fact]
    public void InvalidAuthorityLimitOrderingIsRejected()
        => Should.Throw<ArgumentException>(() => new ReserveAuthorityPolicy(new(100m, 50m, 200m)));
}
