using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using Shouldly;

namespace ClaimsModule.Domain.Tests;

public sealed class ValueObjectAndEntityTests
{
    [Fact]
    public void BRC04ClaimNumberFormatsParsesAndComparesByValue()
    {
        var number = ClaimNumber.Create(2026, 142);
        number.Value.ShouldBe("CLM-2026-0000142");
        ClaimNumber.Parse(number.Value).ShouldBe(number);
        ClaimNumber.Create(2026, 9_999_999).Value.ShouldBe("CLM-2026-9999999");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10000, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 10000000)]
    public void BRC04ClaimNumberRejectsOutOfRangeYearOrSequence(int year, int sequence)
        => Should.Throw<DomainRuleViolationException>(() => ClaimNumber.Create(year, sequence)).Code.ShouldBe("BR-C-04");

    [Theory]
    [InlineData("CLM-2026-142")]
    [InlineData("clm-2026-0000142")]
    [InlineData("CLM-2026-0000000")]
    [InlineData("CLM-2026-00001A2")]
    [InlineData("CLM-2026-0000142 ")]
    [InlineData("CLM-２０２６-0000142")]
    public void BRC04ClaimNumberRejectsMalformedFormat(string value)
        => Should.Throw<DomainRuleViolationException>(() => ClaimNumber.Parse(value));

    [Fact]
    public void BRC01LossDateUsesInjectedClockAndRejectsFutureOrMissingDate()
    {
        var clock = new TestClock();
        var claimId = Guid.NewGuid();
        Should.Throw<DomainRuleViolationException>(() => LossEvent.Create(Guid.NewGuid(), claimId, clock.UtcNow.AddTicks(1),
            "Vehicle damaged in a collision.", "COL-VEH-COL", true, clock)).Code.ShouldBe("BR-C-01");
        Should.Throw<DomainRuleViolationException>(() => LossEvent.Create(Guid.NewGuid(), claimId, default,
            "Vehicle damaged in a collision.", "COL-VEH-COL", true, clock)).Code.ShouldBe("BR-C-01");
        LossEvent.Create(Guid.NewGuid(), claimId, clock.UtcNow, "Vehicle damaged in a collision.", "COL-VEH-COL", true, clock)
            .ReportDate.ShouldBe(clock.UtcNow);
    }

    [Fact]
    public void BRC05InactiveOrUnknownCauseRejectsLossEvent()
    {
        var clock = new TestClock();
        Should.Throw<DomainRuleViolationException>(() => LossEvent.Create(Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow,
            "Vehicle damaged in a collision.", "UNKNOWN", false, clock)).Code.ShouldBe("BR-C-05");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Short description")]
    [InlineData("                   ")]
    public void BRC07LossDescriptionRequiresTwentyCharacters(string description)
    {
        var clock = new TestClock();
        Should.Throw<DomainRuleViolationException>(() => LossEvent.Create(Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow,
            description, "COL-VEH-COL", true, clock)).Code.ShouldBe("BR-C-07");
    }

    [Fact]
    public void DD30EstimatedLossCannotBeNegativeButMayBeZeroOrAbsent()
    {
        var clock = new TestClock();
        Should.Throw<DomainRuleViolationException>(() => LossEvent.Create(Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow,
            "Vehicle damaged in a collision.", "COL-VEH-COL", true, clock, -0.01m)).Code.ShouldBe("DD-30");
        LossEvent.Create(Guid.NewGuid(), Guid.NewGuid(), clock.UtcNow, "Vehicle damaged in a collision.", "COL-VEH-COL", true, clock, 0m)
            .EstimatedLossAmount.ShouldBe(0m);
    }

    [Theory]
    [InlineData(PartyType.Person, null, "Smith", null)]
    [InlineData(PartyType.Person, "Alex", null, null)]
    [InlineData(PartyType.Person, "Alex", "Smith", "Company")]
    [InlineData(PartyType.Company, null, null, null)]
    [InlineData(PartyType.Company, "Alex", "Smith", "Company")]
    public void PartyTypeEnforcesPersonVersusCompanyFieldRules(PartyType type, string? first, string? last, string? company)
        => Should.Throw<DomainRuleViolationException>(() => ClaimParty.Create(Guid.NewGuid(), Guid.NewGuid(),
            PartyRole.Claimant, type, first, last, company));

    [Fact]
    public void CompanyRequiresOnlyCompanyName()
    {
        var party = ClaimParty.Create(Guid.NewGuid(), Guid.NewGuid(), PartyRole.Insured, PartyType.Company, companyName: "Acme Ltd");
        party.FirstName.ShouldBeNull();
        party.LastName.ShouldBeNull();
        party.CompanyName.ShouldBe("Acme Ltd");
    }

    [Theory]
    [InlineData(AssetType.Vehicle)]
    [InlineData(AssetType.Property)]
    [InlineData(AssetType.Person)]
    [InlineData(AssetType.Equipment)]
    [InlineData(AssetType.Other)]
    public void RiskObjectSupportsEveryAssetTypeAndRequiresDescription(AssetType type)
    {
        ClaimRiskObject.Create(Guid.NewGuid(), Guid.NewGuid(), type, "Affected asset").AssetType.ShouldBe(type);
        Should.Throw<DomainRuleViolationException>(() => ClaimRiskObject.Create(Guid.NewGuid(), Guid.NewGuid(), type, " "));
    }

    [Fact]
    public void DomainHasNoExternalOrOtherProjectAssemblyDependencies()
    {
        AssemblyReference.Assembly.GetReferencedAssemblies().ShouldAllBe(reference => reference.Name!.StartsWith("System", StringComparison.Ordinal));
    }
}
