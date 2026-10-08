using System.Globalization;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;

namespace ClaimsModule.Domain.Entities;

// Application supplies all component aggregates loaded in the owning transaction.
public sealed record ReserveOperationContext(Claim Claim, IReadOnlyCollection<ClaimReserveComponent> Components);
public sealed record ReserveSubmissionResult(ReserveHistory Transaction, IReadOnlyList<BlockingCondition> Warnings);

public sealed class ClaimReserveComponent : AggregateRoot
{
    private readonly List<ReserveHistory> _history = [];

    private ClaimReserveComponent(Guid id, Guid claimId, ReserveComponentType component) : base(id)
    {
        ClaimId = claimId;
        Component = component;
        Status = ReserveComponentStatus.Active;
    }

    public Guid ClaimId { get; private set; }
    public ReserveComponentType Component { get; private set; }
    public decimal CurrentAmount { get; private set; }
    public ReserveComponentStatus Status { get; private set; }
    public IReadOnlyList<ReserveHistory> ReserveHistory => _history.AsReadOnly();
    public bool HasPendingTransactions => _history.Any(row => row.ApprovalStatus == ReserveApprovalStatus.PendingApproval);
    public bool HasApprovedTransactions => _history.Any(row => row.IsApproved);
    public decimal PendingAmount => _history.Where(row => row.ApprovalStatus == ReserveApprovalStatus.PendingApproval).Sum(row => row.Amount);

    public static ClaimReserveComponent Create(Guid id, Claim claim, ReserveComponentType component,
        IReadOnlyCollection<ClaimReserveComponent> existingComponents)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(existingComponents);
        RuleGuard.Defined(component);
        claim.ValidateReserveSet(existingComponents);
        if (existingComponents.Any(row => row.Component == component || row.Id == id))
            throw new DomainRuleViolationException("DUPLICATE_RESERVE_COMPONENT", "A claim has one reserve component per type.");
        return new(id, claim.Id, component);
    }

    public ReserveSubmissionResult Submit(Guid transactionId, decimal amount, ReserveTransactionType type,
        string changeReason, Guid userId, UserRole role, ReserveOperationContext context, IClock clock,
        Guid? resubmissionOfId = null, ReserveAuthorityPolicy? authority = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        RuleGuard.Id(transactionId, nameof(transactionId));
        RuleGuard.Id(userId, nameof(userId));
        RuleGuard.Defined(role);
        changeReason = RuleGuard.Text(changeReason, nameof(changeReason), 500);
        context.Claim.EnsureReserveSubmissionAllowed();
        if (_history.Any(row => row.Id == transactionId))
            throw new DomainRuleViolationException("DUPLICATE_RESERVE_TRANSACTION", "Transaction identifier is already present.");
        var delta = ValidateSubmissionAmount(Component, amount, type);
        if (resubmissionOfId.HasValue)
        {
            var original = Find(resubmissionOfId.Value);
            if (original.ApprovalStatus != ReserveApprovalStatus.Rejected || type == ReserveTransactionType.Reverse)
                throw new DomainRuleViolationException("BR-R-04", "Resubmission must link a rejected transaction and use Add or Adjust.");
        }

        authority ??= new();
        var warnings = new List<BlockingCondition>();
        var needsOverride = ExceedsCap(delta, context) && !context.Claim.ManagerOverride;
        if (needsOverride) warnings.Add(new("BR-R-05", "Total reserves will exceed $10,000,000. Manager override required."));
        var automatic = authority.RequiredApprover(delta) is null && !needsOverride;
        var balance = LedgerBalance();
        if (automatic) EnsureBalance(balance + delta);
        var sequence = checked((_history.Count == 0 ? 0 : _history.Max(row => row.ChangeSequence)) + 1);
        var key = string.Create(CultureInfo.InvariantCulture, $"Reserve:{Id:D}:Change:{sequence}");
        var entry = new ReserveHistory(transactionId, Id, ClaimId, type, delta, sequence, key, changeReason,
            userId, clock.UtcNow, resubmissionOfId);
        if (automatic) entry.Approve(null, clock.UtcNow, balance, automatic: true);
        _history.Add(entry);
        CurrentAmount = LedgerBalance();
        Raise(new ReserveSubmitted(ClaimId, Id, entry.Id, delta, clock.UtcNow));
        if (automatic) Raise(new ReserveAutoApproved(ClaimId, Id, entry.Id, key, clock.UtcNow));
        context.Claim.Touch(clock);
        return new(entry, warnings.AsReadOnly());
    }

    public void Approve(Guid transactionId, Guid userId, UserRole role, ReserveOperationContext context,
        IClock clock, ReserveAuthorityPolicy? authority = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        RuleGuard.Id(userId, nameof(userId));
        var entry = Pending(transactionId);
        if (entry.SubmittedByUserId == userId) throw new SelfApprovalException();
        (authority ?? new()).EnsureCanApprove(entry.Amount, role);
        if (ExceedsCap(entry.Amount, context) && !context.Claim.ManagerOverride)
            throw new DomainRuleViolationException("BR-R-05", "Manager override is required before approval above the aggregate cap.");
        var previous = LedgerBalance();
        EnsureBalance(previous + entry.Amount);
        entry.Approve(userId, clock.UtcNow, previous, automatic: false);
        CurrentAmount = LedgerBalance();
        Raise(new ReserveApproved(ClaimId, Id, entry.Id, userId, entry.IdempotencyKey, clock.UtcNow));
        context.Claim.Touch(clock);
    }

    public void Reject(Guid transactionId, Guid userId, UserRole role, string reason,
        ReserveOperationContext context, IClock clock, ReserveAuthorityPolicy? authority = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        RuleGuard.Id(userId, nameof(userId));
        var entry = Pending(transactionId);
        (authority ?? new()).EnsureCanApprove(entry.Amount, role);
        reason = RuleGuard.Text(reason, nameof(reason), 500);
        entry.Reject(userId, reason, clock.UtcNow);
        Raise(new ReserveRejected(ClaimId, Id, entry.Id, userId, reason, clock.UtcNow));
        context.Claim.Touch(clock);
    }

    public void Retract(Guid transactionId, Guid userId, ReserveOperationContext context, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        RuleGuard.Id(userId, nameof(userId));
        var entry = Pending(transactionId);
        if (entry.SubmittedByUserId != userId)
            throw new InsufficientAuthorityException("Only the submitter may retract a pending transaction.");
        entry.Retract(userId, clock.UtcNow);
        Raise(new ReserveRetracted(ClaimId, Id, entry.Id, userId, clock.UtcNow));
        context.Claim.Touch(clock);
    }

    // Return false on duplicate/stale delivery so the caller can avoid duplicate audit rows.
    public bool MarkPosted(Guid transactionId, string jobId, ReserveOperationContext context, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        var entry = Approved(transactionId);
        if (entry.PostingStatus == PostingStatus.Posted) return false;
        jobId = RuleGuard.Text(jobId, nameof(jobId));
        entry.MarkPosted(jobId, clock.UtcNow);
        context.Claim.Touch(clock);
        return true;
    }

    public bool MarkPostingFailed(Guid transactionId, string jobId, string reason, ReserveOperationContext context, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        var entry = Approved(transactionId);
        if (entry.PostingStatus is PostingStatus.Posted or PostingStatus.Failed) return false;
        jobId = RuleGuard.Text(jobId, nameof(jobId));
        reason = RuleGuard.Text(reason, nameof(reason));
        entry.MarkPostingFailed(jobId, reason, clock.UtcNow);
        context.Claim.Touch(clock);
        return true;
    }

    public void RetryPosting(Guid transactionId, ReserveOperationContext context, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateContext(context);
        var entry = Approved(transactionId);
        if (entry.PostingStatus != PostingStatus.Failed)
            throw new DomainRuleViolationException("POSTING_STATE", "Only Failed posting may be retried.");
        entry.RetryPosting();
        context.Claim.Touch(clock);
    }

    public decimal RecomputeBalance()
    {
        var balance = LedgerBalance();
        EnsureBalance(balance);
        CurrentAmount = balance;
        return balance;
    }

    // Shared with request validators; financial sign/precision rules have one domain source.
    public static decimal ValidateSubmissionAmount(ReserveComponentType component, decimal amount, ReserveTransactionType type)
    {
        RuleGuard.Defined(component);
        RuleGuard.Defined(type);
        RuleGuard.Money(amount);
        if (amount == 0 || (type == ReserveTransactionType.Reverse && amount < 0)
            || (type == ReserveTransactionType.Add && (component == ReserveComponentType.SubrogationRecoverable ? amount > 0 : amount < 0)))
            throw new DomainRuleViolationException("BR-R-01", "Add requires the component's sign, Adjust a non-zero signed delta, and Reverse a positive magnitude.");
        return type == ReserveTransactionType.Reverse ? -amount : amount;
    }

    private decimal LedgerBalance() => _history.Where(row => row.IsApproved).Sum(row => row.Amount);

    private void EnsureBalance(decimal balance)
    {
        RuleGuard.Money(balance);
        if (Component != ReserveComponentType.SubrogationRecoverable && balance < 0)
            throw new DomainRuleViolationException("BR-R-01", "An approved non-subrogation balance cannot be negative.");
    }

    private static bool ExceedsCap(decimal delta, ReserveOperationContext context) =>
        context.Components.Sum(row => row.LedgerBalance()) + delta > ReserveAuthorityPolicy.AggregateCap;

    private ReserveHistory Find(Guid id) => _history.SingleOrDefault(row => row.Id == id)
        ?? throw new DomainRuleViolationException("RESERVE_TRANSACTION_NOT_FOUND", "Reserve transaction was not found in this component.");

    private ReserveHistory Pending(Guid id)
    {
        var entry = Find(id);
        if (entry.ApprovalStatus != ReserveApprovalStatus.PendingApproval)
            throw new DomainRuleViolationException("RESERVE_TRANSACTION_STATE", "Only a PendingApproval transaction may be reviewed or retracted.");
        return entry;
    }

    private ReserveHistory Approved(Guid id)
    {
        var entry = Find(id);
        if (!entry.IsApproved)
            throw new DomainRuleViolationException("POSTING_STATE", "Only an approved transaction may be posted.");
        return entry;
    }

    private void ValidateContext(ReserveOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Claim);
        ArgumentNullException.ThrowIfNull(context.Components);
        context.Claim.ValidateReserveSet(context.Components);
        if (context.Claim.Id != ClaimId || !context.Components.Any(row => ReferenceEquals(row, this)))
            throw new DomainRuleViolationException("CLAIM_MISMATCH", "Context must include this component and its owning claim.");
    }
}
