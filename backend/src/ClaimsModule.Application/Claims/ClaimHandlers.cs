using System.Text.Json;
using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Documents;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims;

public sealed class CreateClaimCommandHandler(IClaimRepository claims, IClaimNumberGenerator numbers,
    IIdentifierGenerator ids, IClock clock, ICurrentUser user, IPolicyQueries policies, IMapper mapper,
    ReserveSubmission reserveSubmission)
    : IRequestHandler<CreateClaimCommand, Guid>
{
    public async Task<Guid> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var intakeClock = new IntakeClock(clock.UtcNow.ToUniversalTime());
        PolicyAssociation? association = null;
        if (request.PolicyId.HasValue)
        {
            var policy = await policies.GetAsync(request.PolicyId.Value, cancellationToken)
                ?? throw ClaimHandlerSupport.Invalid("PolicyId", "Policy was not found.");
            association = mapper.Map<PolicyAssociation>(policy);
        }
        var id = ids.NewId();
        var loss = LossEvent.Create(ids.NewId(), id, request.LossDate, request.LossDescription, request.CauseOfLossCode,
            true, intakeClock, request.EstimatedLossAmount, request.LossLocation);
        var claim = Claim.Create(id, await numbers.NextAsync(intakeClock.UtcNow.Year, cancellationToken),
            user.UserId ?? throw new InsufficientAuthorityException("An authenticated creator is required."), loss,
            request.Parties.Select(x => ClaimHandlerSupport.Party(x, ids.NewId(), id)), intakeClock, association,
            request.RiskObjects.Select(x => ClaimRiskObject.Create(ids.NewId(), id, x.AssetType, x.AssetDescription,
                x.DamageDescription, x.IsPrimary, x.AssetReference)), severity: request.Severity ?? ClaimSeverity.Standard);
        claims.Add(claim);
        if (request.InitialReserve is { } initial)
            reserveSubmission.Submit(claim, [], new(claim.Id, initial.Component, initial.Amount,
                initial.ChangeReason!, ReserveTransactionType.Add));
        return id;
    }

    private sealed record IntakeClock(DateTimeOffset UtcNow) : Domain.Common.IClock;
}

public sealed class ValidateClaimQueryHandler(IValidator<CreateClaimCommand> validator) : IRequestHandler<ValidateClaimQuery, ClaimValidationDto>
{
    public async Task<ClaimValidationDto> Handle(ValidateClaimQuery request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request.Intake, cancellationToken);
        return new(result.Errors.Where(x => x.Severity == FluentValidation.Severity.Error)
                .Select(x => new IntakeIssueDto(x.PropertyName, x.ErrorCode, x.ErrorMessage)).ToArray(),
            result.Errors.Where(x => x.Severity == FluentValidation.Severity.Warning)
                .Select(x => new IntakeIssueDto(x.PropertyName, x.ErrorCode, x.ErrorMessage)).ToArray());
    }
}

public sealed class GetClaimDetailQueryHandler(IClaimQueries queries, IStorageService storage) : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Claim '{request.Id}' was not found.");
        return result with { Documents = await ListDocumentsQueryHandler.AddUrls(result.Documents, storage, cancellationToken) };
    }
}

public sealed class ListClaimsQueryHandler(IClaimQueries queries) : IRequestHandler<ListClaimsQuery, PageDto<ClaimListItemDto>>
{
    public Task<PageDto<ClaimListItemDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken) =>
        queries.ListAsync(new(new(request.Page, request.PageSize), request.Status, request.DateFrom, request.DateTo,
            request.AssignedHandlerId, request.CauseOfLossCode, request.PolicyId, request.Search?.Trim()), cancellationToken);
}

public sealed class GetClaimAuditQueryHandler(IClaimRepository claims, IAuditQueries queries) : IRequestHandler<GetClaimAuditQuery, PageDto<AuditLogDto>>
{
    public async Task<PageDto<AuditLogDto>> Handle(GetClaimAuditQuery request, CancellationToken cancellationToken)
    {
        await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        return await queries.ListAsync(request.Id, new(request.Page, request.PageSize), cancellationToken);
    }
}

public sealed class AddPartyCommandHandler(IClaimRepository claims, IIdentifierGenerator ids, IClock clock, IMapper mapper)
    : IRequestHandler<AddPartyCommand, PartyDto>
{
    public async Task<PartyDto> Handle(AddPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var party = ClaimHandlerSupport.Party(request.Party, ids.NewId(), claim.Id);
        claim.AddParty(party, clock);
        return mapper.Map<PartyDto>(party);
    }
}

public sealed class RemovePartyCommandHandler(IClaimRepository claims, IClock clock) : IRequestHandler<RemovePartyCommand, Unit>
{
    public async Task<Unit> Handle(RemovePartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var party = claim.Parties.SingleOrDefault(x => x.Id == request.PartyId)
            ?? throw new NotFoundException($"Party '{request.PartyId}' was not found on this claim.");
        if (party.IsActive) claim.RemoveParty(party.Id, clock);
        return Unit.Value;
    }
}

public sealed class UpdateClaimNotesCommandHandler(IClaimRepository claims, IClock clock, IAuditLogService audit)
    : IRequestHandler<UpdateClaimNotesCommand, Unit>
{
    public async Task<Unit> Handle(UpdateClaimNotesCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var old = claim.Notes;
        claim.UpdateNotes(request.Notes, clock);
        audit.Append(new(claim.Id, "NOTES_UPDATED", "Claim notes updated",
            JsonSerializer.Serialize(new { Notes = old }), JsonSerializer.Serialize(new { request.Notes })));
        return Unit.Value;
    }
}

public sealed class LinkPolicyCommandHandler(IClaimRepository claims, IPolicyQueries policies, IClock clock, IMapper mapper)
    : IRequestHandler<LinkPolicyCommand, Unit>
{
    public async Task<Unit> Handle(LinkPolicyCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        var policy = await policies.GetAsync(request.PolicyId, cancellationToken)
            ?? throw ClaimHandlerSupport.Invalid("PolicyId", "Policy was not found.");
        claim.LinkPolicy(mapper.Map<PolicyAssociation>(policy), clock);
        return Unit.Value;
    }
}

public sealed class AssignHandlerCommandHandler(IClaimRepository claims, IUserQueries users, ICurrentUser user, IClock clock, IAuditLogService audit)
    : IRequestHandler<AssignHandlerCommand, Unit>
{
    public async Task<Unit> Handle(AssignHandlerCommand request, CancellationToken cancellationToken)
    {
        if (user.Role is null or < UserRole.Supervisor) throw new InsufficientAuthorityException("Supervisor or higher is required to assign a handler.");
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        if (!(await users.ListAsync(cancellationToken: cancellationToken)).Any(x => x.Id == request.AssignedHandlerId))
            throw ClaimHandlerSupport.Invalid("AssignedHandlerId", "Assignee was not found.");
        var old = claim.AssignedHandlerId;
        claim.AssignHandler(request.AssignedHandlerId, user.Role.Value, clock);
        audit.Append(new(claim.Id, "HANDLER_ASSIGNED", "Claim handler assigned", JsonSerializer.Serialize(new { AssignedHandlerId = old }),
            JsonSerializer.Serialize(new { request.AssignedHandlerId })));
        return Unit.Value;
    }
}

public sealed class DetermineLiabilityCommandHandler(IClaimRepository claims, IClock clock) : IRequestHandler<DetermineLiabilityCommand, Unit>
{
    public async Task<Unit> Handle(DetermineLiabilityCommand request, CancellationToken cancellationToken)
    {
        var claim = await ClaimHandlerSupport.Load(claims, request.Id, cancellationToken);
        claim.DetermineLiability(request.Determination ?? throw ClaimHandlerSupport.Invalid("Determination", "Liability determination is required."), request.Notes, clock);
        return Unit.Value;
    }
}

internal static class ClaimHandlerSupport
{
    internal static async Task<Claim> Load(IClaimRepository claims, Guid id, CancellationToken token) =>
        await claims.GetAsync(id, token) ?? throw new NotFoundException($"Claim '{id}' was not found.");
    internal static ClaimParty Party(PartyInput input, Guid id, Guid claimId) => ClaimParty.Create(id, claimId,
        input.Role, input.Type, input.FirstName, input.LastName, input.CompanyName, input.Email, input.Phone, input.Notes);
    internal static ValidationException Invalid(string field, string message) => new([new(field, message)]);
}
