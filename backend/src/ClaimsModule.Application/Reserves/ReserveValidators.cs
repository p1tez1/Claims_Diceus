using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using FluentValidation;

namespace ClaimsModule.Application.Reserves;

public sealed class SubmitReserveCommandValidator : AbstractValidator<SubmitReserveCommand>
{
    public SubmitReserveCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.Component).IsInEnum();
        RuleFor(x => x.TransactionType).IsInEnum();
        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ResubmissionOfId).NotEqual(Guid.Empty).When(x => x.ResubmissionOfId.HasValue);
        RuleFor(x => x).Custom((request, context) =>
        {
            if (!Enum.IsDefined(request.Component) || !Enum.IsDefined(request.TransactionType)) return;
            try { ClaimReserveComponent.ValidateSubmissionAmount(request.Component, request.Amount, request.TransactionType); }
            catch (DomainRuleViolationException e)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure("Amount", e.Message) { ErrorCode = e.Code });
            }
        });
    }
}

public sealed class AdjustReserveCommandValidator : AbstractValidator<AdjustReserveCommand>
{
    public AdjustReserveCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.ComponentId).NotEmpty();
        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ResubmissionOfId).NotEqual(Guid.Empty).When(x => x.ResubmissionOfId.HasValue);
        RuleFor(x => x.Amount).Custom((amount, context) =>
        {
            try { ClaimReserveComponent.ValidateSubmissionAmount(ReserveComponentType.Indemnity, amount, ReserveTransactionType.Adjust); }
            catch (DomainRuleViolationException e) { context.AddFailure(e.Message); }
        });
    }
}

public sealed class RejectReserveCommandValidator : AbstractValidator<RejectReserveCommand>
{
    public RejectReserveCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ApproveReserveCommandValidator : AbstractValidator<ApproveReserveCommand>
{
    public ApproveReserveCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class RetractReserveCommandValidator : AbstractValidator<RetractReserveCommand>
{
    public RetractReserveCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class RetryGlPostingCommandValidator : AbstractValidator<RetryGlPostingCommand>
{
    public RetryGlPostingCommandValidator()
    {
        RuleFor(x => x.ClaimId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class SetReserveOverrideCommandValidator : AbstractValidator<SetReserveOverrideCommand>
{
    public SetReserveOverrideCommandValidator() => RuleFor(x => x.ClaimId).NotEmpty();
}
