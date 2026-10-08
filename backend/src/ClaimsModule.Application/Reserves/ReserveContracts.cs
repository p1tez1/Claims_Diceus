using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;
using MediatR;

namespace ClaimsModule.Application.Reserves;

public sealed record SubmitReserveCommand(Guid ClaimId, ReserveComponentType Component, decimal Amount,
    string ChangeReason, ReserveTransactionType TransactionType, Guid? ResubmissionOfId = null) : ICommand<ReserveTransactionResult>;
public sealed record AdjustReserveCommand(Guid ClaimId, Guid ComponentId, decimal Amount, string ChangeReason,
    Guid? ResubmissionOfId = null) : ICommand<ReserveTransactionResult>;
public sealed record ApproveReserveCommand(Guid ClaimId, Guid TransactionId) : ICommand<Unit>;
public sealed record RejectReserveCommand(Guid ClaimId, Guid TransactionId, string RejectionReason) : ICommand<Unit>;
public sealed record RetractReserveCommand(Guid ClaimId, Guid TransactionId) : ICommand<Unit>;
public sealed record RetryGlPostingCommand(Guid ClaimId, Guid TransactionId) : ICommand<Unit>;
public sealed record SetReserveOverrideCommand(Guid ClaimId, bool Enabled = true) : ICommand<Unit>;
public sealed record GetClaimReservesQuery(Guid ClaimId) : IQuery<IReadOnlyList<ReserveComponentDto>>;
public sealed record ReserveTransactionResult(ReserveHistoryDto Transaction, ReserveApprovalStatus ApprovalStatus,
    IReadOnlyList<BlockingCondition> Warnings);
