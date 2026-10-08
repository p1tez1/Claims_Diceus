using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;

namespace ClaimsModule.Application.Reserves;

// Both FNOL and subsequent transactions call this inside their existing command transaction.
public sealed class ReserveSubmission(IReserveComponentRepository reserves, IIdentifierGenerator ids,
    IClock clock, ICurrentUser user, IValidationWarnings warnings)
{
    public ReserveTransactionResult Submit(Claim claim, IReadOnlyList<ClaimReserveComponent> existing, SubmitReserveCommand request)
    {
        claim.EnsureReserveSubmissionAllowed();
        var components = existing.ToList();
        var component = components.SingleOrDefault(x => x.Component == request.Component);
        var isNew = component is null;
        component ??= ClaimReserveComponent.Create(ids.NewId(), claim, request.Component, components);
        if (isNew) components.Add(component);
        var result = component.Submit(ids.NewId(), request.Amount, request.TransactionType, request.ChangeReason,
            user.UserId ?? throw new InsufficientAuthorityException("An authenticated submitter is required."),
            user.Role ?? throw new InsufficientAuthorityException(), new(claim, components), clock,
            request.ResubmissionOfId, new ReserveAuthorityPolicy());
        // Add only after Submit succeeds, so an invalid submission cannot create an empty row.
        if (isNew) reserves.Add(component);
        warnings.AddRange(result.Warnings.Select(x => new FluentValidation.Results.ValidationFailure("Amount", x.Message)
        { ErrorCode = x.Code, Severity = FluentValidation.Severity.Warning }));
        return new(ToDto(result.Transaction, component.Component, user.Name), result.Transaction.ApprovalStatus, result.Warnings);
    }

    private static ReserveHistoryDto ToDto(ReserveHistory h, Domain.Enums.ReserveComponentType component, string submitter) => new(
        h.Id, h.TransactionType, h.Amount, h.PreviousBalance, h.NewBalance, h.ChangeSequence, h.ChangeReason,
        h.IdempotencyKey, h.ApprovalStatus, h.PostingStatus, h.SubmittedByUserId, h.CreatedAt, h.ApprovedByUserId,
        h.ApprovedAt, h.RejectedByUserId, h.RejectedAt, h.RejectionReason, h.ResubmissionOfId, h.PostingJobId,
        component, submitter);
}
