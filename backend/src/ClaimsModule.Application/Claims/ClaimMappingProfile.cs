using AutoMapper;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Claims;

public sealed class ClaimMappingProfile : Profile
{
    public ClaimMappingProfile()
    {
        CreateMap<PolicyDto, PolicyAssociation>().ConvertUsing(x => new(x.Id, x.PolicyNumber, x.ClientName,
            new DateTimeOffset(x.EffectiveDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(x.ExpirationDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
        // Keep permitted AutoMapper 14 mappings bounded (CVE-2026-32933); all current DTO graphs are flat.
        CreateMap<ClaimParty, PartyDto>().MaxDepth(32);
        CreateMap<ClaimRiskObject, RiskObjectDto>().MaxDepth(32);
        CreateMap<LossEvent, LossEventDto>().MaxDepth(32);
    }
}
