using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Domain.ValueObjects;
using FluentValidation;

namespace ClaimsModule.Application.Claims;

public sealed class PartyInputValidator : AbstractValidator<PartyInput>
{
    public PartyInputValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
        When(x => x.Type == PartyType.Person, () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.CompanyName).Must(string.IsNullOrWhiteSpace).WithMessage("A Person cannot have a company name.");
        });
        When(x => x.Type == PartyType.Company, () =>
        {
            RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(255);
            RuleFor(x => x.FirstName).Must(string.IsNullOrWhiteSpace).WithMessage("A Company cannot have person names.");
            RuleFor(x => x.LastName).Must(string.IsNullOrWhiteSpace).WithMessage("A Company cannot have person names.");
        });
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public sealed class RiskObjectInputValidator : AbstractValidator<RiskObjectInput>
{
    public RiskObjectInputValidator()
    {
        RuleFor(x => x.AssetType).IsInEnum();
        RuleFor(x => x.AssetDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AssetReference).MaximumLength(255);
    }
}

public sealed class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(IClock clock, IReferenceDataQueries reference, IPolicyQueries policies, IMapper mapper)
    {
        RuleFor(x => x.LossDate).NotEmpty().WithMessage("Loss date is required.").WithErrorCode("BR-C-01")
            .LessThanOrEqualTo(_ => clock.UtcNow).WithMessage("Loss date cannot be in the future.").WithErrorCode("BR-C-01");
        RuleFor(x => x.LossDescription).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length >= 20)
            .WithMessage("Loss description is required and must be at least 20 characters.").WithErrorCode("BR-C-07");
        RuleFor(x => x.CauseOfLossCode).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(50)
            .MustAsync(async (code, token) => (await reference.GetCausesAsync(cancellationToken: token)).Any(x => x.Code == code))
            .WithMessage("Cause of loss code is not recognised or is inactive.").WithErrorCode("BR-C-05");
        RuleFor(x => x.LossLocation).MaximumLength(500);
        RuleFor(x => x.EstimatedLossAmount).Must(x => !x.HasValue || (x.Value >= 0 && x.Value <= 999_999_999_999_999.9999m
            && decimal.Round(x.Value, 4) == x.Value)).WithMessage("Estimated loss amount must be non-negative and fit decimal(19,4) without rounding.")
            .WithErrorCode("DD-30");
        RuleFor(x => x.Severity).Must(x => x is null || Enum.IsDefined(x.Value)).WithMessage("Invalid claim severity.");
        RuleFor(x => x.Parties).NotNull().Must(x => x != null && x.Any(p => p != null && p.Role == PartyRole.Claimant))
            .WithMessage("At least one Claimant party is required.").WithErrorCode("BR-C-03");
        RuleForEach(x => x.Parties).NotNull().SetValidator(new PartyInputValidator());
        RuleFor(x => x.RiskObjects).NotNull();
        RuleForEach(x => x.RiskObjects).NotNull().SetValidator(new RiskObjectInputValidator());
        RuleFor(x => x.InitialReserve).Must((request, _) => request.PolicyId.HasValue)
            .When(x => x.InitialReserve is not null)
            .WithMessage("A policy must be linked before reserves can be submitted.").WithErrorCode("BR-C-06");
        RuleFor(x => x.InitialReserve).SetValidator(new InitialReserveInputValidator()!).When(x => x.InitialReserve is not null);
        RuleFor(x => x).CustomAsync(async (request, context, token) =>
        {
            PolicyAssociation? association = null;
            if (request.PolicyId.HasValue)
            {
                var policy = await policies.GetAsync(request.PolicyId.Value, token);
                if (policy is null)
                {
                    context.AddFailure("PolicyId", "Policy was not found.");
                    return;
                }
                association = mapper.Map<PolicyAssociation>(policy);
            }
            foreach (var warning in ClaimIntakeWarnings.Evaluate(request.LossDate, association, request.RiskObjects is { Count: > 0 }))
                context.AddFailure(new FluentValidation.Results.ValidationFailure(warning.Field, warning.Message)
                { ErrorCode = warning.Code, Severity = FluentValidation.Severity.Warning });
        });
    }
}

public sealed class InitialReserveInputValidator : AbstractValidator<InitialReserveInput>
{
    public InitialReserveInputValidator()
    {
        RuleFor(x => x.Component).IsInEnum();
        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x).Custom((request, context) =>
        {
            if (!Enum.IsDefined(request.Component)) return;
            try { ClaimReserveComponent.ValidateSubmissionAmount(request.Component, request.Amount, ReserveTransactionType.Add); }
            catch (DomainRuleViolationException e)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure("Amount", e.Message) { ErrorCode = e.Code });
            }
        });
    }
}

public sealed class AddPartyCommandValidator : AbstractValidator<AddPartyCommand>
{
    public AddPartyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Party).NotNull().SetValidator(new PartyInputValidator());
    }
}

public sealed class LinkPolicyCommandValidator : AbstractValidator<LinkPolicyCommand>
{
    public LinkPolicyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}

public sealed class AssignHandlerCommandValidator : AbstractValidator<AssignHandlerCommand>
{
    public AssignHandlerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AssignedHandlerId).NotEmpty();
    }
}

public sealed class DetermineLiabilityCommandValidator : AbstractValidator<DetermineLiabilityCommand>
{
    public DetermineLiabilityCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Determination).Must(x => x.HasValue && Enum.IsDefined(x.Value)).WithMessage("A valid liability determination is required.");
    }
}

public sealed class RemovePartyCommandValidator : AbstractValidator<RemovePartyCommand>
{
    public RemovePartyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
    }
}

public sealed class UpdateClaimNotesCommandValidator : AbstractValidator<UpdateClaimNotesCommand>
{
    public UpdateClaimNotesCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleForEach(x => x.Status).IsInEnum();
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).When(x => x.DateFrom.HasValue && x.DateTo.HasValue);
        RuleFor(x => x.Search).MaximumLength(255);
        RuleFor(x => x.CauseOfLossCode).MaximumLength(50);
    }
}

public sealed class GetClaimAuditQueryValidator : AbstractValidator<GetClaimAuditQuery>
{
    public GetClaimAuditQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
