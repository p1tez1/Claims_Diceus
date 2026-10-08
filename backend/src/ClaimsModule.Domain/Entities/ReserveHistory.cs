using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

public sealed class ReserveHistory
{
    private ReserveHistory() { }

    internal ReserveHistory(Guid id, Guid componentId, Guid claimId, ReserveTransactionType type,
        decimal amount, int sequence, string idempotencyKey, string reason, Guid submittedBy,
        DateTimeOffset createdAt, Guid? resubmissionOfId)
    {
        Id = id;
        ReserveComponentId = componentId;
        ClaimId = claimId;
        TransactionType = type;
        Amount = amount;
        ChangeSequence = sequence;
        IdempotencyKey = idempotencyKey;
        ChangeReason = reason;
        SubmittedByUserId = submittedBy;
        CreatedAt = createdAt;
        ResubmissionOfId = resubmissionOfId;
        ApprovalStatus = ReserveApprovalStatus.PendingApproval;
        PostingStatus = PostingStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ReserveComponentId { get; private set; }
    public Guid ClaimId { get; private set; }
    public ReserveTransactionType TransactionType { get; private set; }
    public decimal Amount { get; private set; }
    // Pending/rejected/cancelled transactions have no approved balances.
    public decimal? PreviousBalance { get; private set; }
    public decimal? NewBalance { get; private set; }
    public int ChangeSequence { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string ChangeReason { get; private set; } = string.Empty;
    public Guid SubmittedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ResubmissionOfId { get; private set; }
    public ReserveApprovalStatus ApprovalStatus { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? RetractedByUserId { get; private set; }
    public DateTimeOffset? RetractedAt { get; private set; }
    public PostingStatus PostingStatus { get; private set; }
    public string? PostingJobId { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public DateTimeOffset? PostingFailedAt { get; private set; }
    public string? PostingFailureReason { get; private set; }

    public bool IsApproved => ApprovalStatus is ReserveApprovalStatus.AutoApproved or ReserveApprovalStatus.Approved;

    internal void Approve(Guid? userId, DateTimeOffset at, decimal previousBalance, bool automatic)
    {
        ApprovalStatus = automatic ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAt = at;
        PreviousBalance = previousBalance;
        NewBalance = previousBalance + Amount;
    }

    internal void Reject(Guid userId, string reason, DateTimeOffset at)
    {
        ApprovalStatus = ReserveApprovalStatus.Rejected;
        RejectedByUserId = userId;
        RejectedAt = at;
        RejectionReason = reason;
        PostingStatus = PostingStatus.Cancelled;
    }

    internal void Retract(Guid userId, DateTimeOffset at)
    {
        ApprovalStatus = ReserveApprovalStatus.Cancelled;
        RetractedByUserId = userId;
        RetractedAt = at;
        PostingStatus = PostingStatus.Cancelled;
    }

    internal void MarkPosted(string jobId, DateTimeOffset at)
    {
        PostingStatus = PostingStatus.Posted;
        PostingJobId = jobId;
        PostedAt = at;
    }

    internal void MarkPostingFailed(string jobId, string reason, DateTimeOffset at)
    {
        PostingStatus = PostingStatus.Failed;
        PostingJobId = jobId;
        PostingFailedAt = at;
        PostingFailureReason = reason;
    }

    internal void RetryPosting()
    {
        PostingStatus = PostingStatus.Pending;
        PostingJobId = null; // DD-33 must recover a retry whose post-commit enqueue failed.
    }
}
