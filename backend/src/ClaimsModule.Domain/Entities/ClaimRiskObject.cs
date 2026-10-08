using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimRiskObject
{
    private ClaimRiskObject() { }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public AssetType AssetType { get; private set; }
    public string AssetDescription { get; private set; } = string.Empty;
    public string? DamageDescription { get; private set; }
    public bool IsPrimary { get; private set; }
    public string? AssetReference { get; private set; }

    public static ClaimRiskObject Create(Guid id, Guid claimId, AssetType assetType, string description,
        string? damageDescription = null, bool isPrimary = false, string? assetReference = null) => new()
        {
            Id = RuleGuard.Id(id, nameof(id)),
            ClaimId = RuleGuard.Id(claimId, nameof(claimId)),
            AssetType = RuleGuard.Defined(assetType),
            AssetDescription = RuleGuard.Text(description, nameof(description), 500),
            DamageDescription = damageDescription,
            IsPrimary = isPrimary,
            AssetReference = assetReference,
        };
}
