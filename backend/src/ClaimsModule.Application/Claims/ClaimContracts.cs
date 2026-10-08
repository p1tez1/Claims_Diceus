using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims;

public sealed record PartyInput
{
    public PartyRole Role { get; init; }
    public PartyType Type { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
}

public sealed record RiskObjectInput
{
    public AssetType AssetType { get; init; }
    public string AssetDescription { get; init; } = string.Empty;
    public string? DamageDescription { get; init; }
    public bool IsPrimary { get; init; }
    public string? AssetReference { get; init; }
}

public sealed record InitialReserveInput(ReserveComponentType Component, decimal Amount, string? ChangeReason);

// Returning the identity lets the API fetch the persisted detail after the transaction and audit dispatch commit.
public sealed record CreateClaimCommand : ICommand<Guid>
{
    public DateTimeOffset LossDate { get; init; }
    public string LossDescription { get; init; } = string.Empty;
    public string CauseOfLossCode { get; init; } = string.Empty;
    public string? LossLocation { get; init; }
    public decimal? EstimatedLossAmount { get; init; }
    public Guid? PolicyId { get; init; }
    public ClaimSeverity? Severity { get; init; }
    public IReadOnlyList<PartyInput> Parties { get; init; } = [];
    public IReadOnlyList<RiskObjectInput> RiskObjects { get; init; } = [];
    public InitialReserveInput? InitialReserve { get; init; }
}

public sealed record IntakeIssueDto(string Field, string Code, string Message);
public sealed record ClaimValidationDto(IReadOnlyList<IntakeIssueDto> CriticalIssues, IReadOnlyList<IntakeIssueDto> Warnings);
public sealed record ValidateClaimQuery(CreateClaimCommand Intake) : IQuery<ClaimValidationDto>;
public sealed record GetClaimDetailQuery(Guid Id) : IQuery<ClaimDetailDto>;
public sealed record ListClaimsQuery(int Page = 1, int PageSize = 20, IReadOnlyList<ClaimStatus>? Status = null,
    DateTimeOffset? DateFrom = null, DateTimeOffset? DateTo = null, Guid? AssignedHandlerId = null,
    string? CauseOfLossCode = null, Guid? PolicyId = null, string? Search = null) : IQuery<PageDto<ClaimListItemDto>>;
public sealed record GetClaimAuditQuery(Guid Id, int Page = 1, int PageSize = 20) : IQuery<PageDto<AuditLogDto>>;
public sealed record AddPartyCommand(Guid Id, PartyInput Party) : ICommand<PartyDto>;
public sealed record RemovePartyCommand(Guid Id, Guid PartyId) : ICommand<Unit>;
public sealed record UpdateClaimNotesCommand(Guid Id, string? Notes) : ICommand<Unit>;
public sealed record LinkPolicyCommand(Guid Id, Guid PolicyId) : ICommand<Unit>;
public sealed record AssignHandlerCommand(Guid Id, Guid AssignedHandlerId) : ICommand<Unit>;
public sealed record DetermineLiabilityCommand(Guid Id, LiabilityDetermination? Determination, string? Notes) : ICommand<Unit>;
