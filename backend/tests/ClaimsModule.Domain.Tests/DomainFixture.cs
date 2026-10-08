using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Tests;

internal sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    public void Advance() => UtcNow = UtcNow.AddMinutes(1);
}

internal sealed class DomainFixture
{
    public DomainFixture(bool withPolicy = true, bool withRiskObject = true)
    {
        var id = Guid.NewGuid();
        var loss = LossEvent.Create(Guid.NewGuid(), id, Clock.UtcNow.AddDays(-1),
            "Vehicle damaged in a collision at the intersection.", "COL-VEH-COL", true, Clock);
        var parties = new[] { Person(id, PartyRole.Claimant) };
        var risks = withRiskObject
            ? new[] { ClaimRiskObject.Create(Guid.NewGuid(), id, AssetType.Vehicle, "Delivery truck") } : [];
        Claim = Claim.Create(id, ClaimNumber.Create(2026, 1), Submitter, loss, parties, Clock,
            withPolicy ? Policy(Clock) : null, risks);
    }

    public TestClock Clock { get; } = new();
    public Guid Submitter { get; } = Guid.NewGuid();
    public Guid Reviewer { get; } = Guid.NewGuid();
    public Claim Claim { get; }
    public List<ClaimReserveComponent> Components { get; } = [];
    public ReserveOperationContext Context => new(Claim, Components.AsReadOnly());

    public ClaimReserveComponent Component(ReserveComponentType type = ReserveComponentType.Indemnity)
    {
        var component = ClaimReserveComponent.Create(Guid.NewGuid(), Claim, type, Components);
        Components.Add(component);
        return component;
    }

    public ReserveHistory Submit(ClaimReserveComponent component, decimal amount,
        ReserveTransactionType type = ReserveTransactionType.Add, Guid? userId = null, UserRole role = UserRole.Handler,
        Guid? resubmissionOfId = null) => component.Submit(Guid.NewGuid(), amount, type, "Updated estimate",
            userId ?? Submitter, role, Context, Clock, resubmissionOfId).Transaction;

    public void Open() => Claim.TransitionTo(ClaimStatus.Open, Submitter, UserRole.Handler, Clock, Components,
        new(AcknowledgeWarnings: true));

    public static ClaimParty Person(Guid claimId, PartyRole role) => ClaimParty.Create(Guid.NewGuid(), claimId,
        role, PartyType.Person, "Alex", "Smith");

    public static PolicyAssociation Policy(TestClock clock) => new(Guid.NewGuid(), "POL-2024-001001",
        "Meridian Transport LLC", clock.UtcNow.AddYears(-2), clock.UtcNow.AddYears(1));
}
