using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimParty
{
    private ClaimParty() { }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public PartyRole Role { get; private set; }
    public PartyType Type { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? CompanyName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    public static ClaimParty Create(Guid id, Guid claimId, PartyRole role, PartyType type,
        string? firstName = null, string? lastName = null, string? companyName = null,
        string? email = null, string? phone = null, string? notes = null)
    {
        RuleGuard.Defined(role);
        RuleGuard.Defined(type);
        if (type == PartyType.Person)
        {
            firstName = RuleGuard.Text(firstName, nameof(firstName), 100);
            lastName = RuleGuard.Text(lastName, nameof(lastName), 100);
            if (!string.IsNullOrWhiteSpace(companyName))
                throw new DomainRuleViolationException("PARTY_FIELDS", "A Person cannot have a company name.");
        }
        else
        {
            companyName = RuleGuard.Text(companyName, nameof(companyName), 255);
            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                throw new DomainRuleViolationException("PARTY_FIELDS", "A Company cannot have person names.");
        }

        return new ClaimParty
        {
            Id = RuleGuard.Id(id, nameof(id)),
            ClaimId = RuleGuard.Id(claimId, nameof(claimId)),
            Role = role,
            Type = type,
            FirstName = type == PartyType.Person ? firstName : null,
            LastName = type == PartyType.Person ? lastName : null,
            CompanyName = type == PartyType.Company ? companyName : null,
            Email = email,
            Phone = phone,
            Notes = notes,
            IsActive = true,
        };
    }

    internal void Deactivate() => IsActive = false;
}
