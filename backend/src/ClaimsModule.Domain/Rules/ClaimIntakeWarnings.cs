using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Rules;

public sealed record IntakeWarning(string Field, string Code, string Message);

public static class ClaimIntakeWarnings
{
    public static IReadOnlyList<IntakeWarning> Evaluate(DateTimeOffset lossDate, PolicyAssociation? policy, bool hasRiskObjects)
    {
        var warnings = new List<IntakeWarning>();
        if (policy is null)
            warnings.Add(new("PolicyId", "BR-C-06", "No policy linked — claim requires policy association before financial actions are permitted."));
        else if (OutsidePolicyWindow(lossDate, policy))
            warnings.Add(new("LossDate", "BR-C-02", "Loss date is outside the policy effective period."));
        if (!hasRiskObjects)
            warnings.Add(new("RiskObjects", "NO_RISK_OBJECTS", "No risk objects are linked to the claim."));
        return warnings;
    }

    public static bool OutsidePolicyWindow(DateTimeOffset lossDate, PolicyAssociation policy)
    {
        var date = DateOnly.FromDateTime(lossDate.UtcDateTime);
        return date < DateOnly.FromDateTime(policy.EffectiveDate.UtcDateTime)
            || date > DateOnly.FromDateTime(policy.ExpirationDate.UtcDateTime);
    }
}
