using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Exceptions;

namespace ClaimsModule.Domain.Entities;

public sealed class LossEvent
{
    private LossEvent() { }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public DateTimeOffset LossDate { get; private set; }
    public string LossDescription { get; private set; } = string.Empty;
    public string? LossLocation { get; private set; }
    public string CauseOfLossCode { get; private set; } = string.Empty;
    public decimal? EstimatedLossAmount { get; private set; }
    public DateTimeOffset ReportDate { get; private set; }
    public string? PoliceReportNumber { get; private set; }

    // Reference-data existence/active state is supplied by the caller, without a repository dependency.
    public static LossEvent Create(Guid id, Guid claimId, DateTimeOffset lossDate, string description,
        string causeOfLossCode, bool causeOfLossIsActive, IClock clock, decimal? estimatedLossAmount = null,
        string? location = null, string? policeReportNumber = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (lossDate == default || lossDate > clock.UtcNow)
            throw new DomainRuleViolationException("BR-C-01", "Loss date is required and cannot be in the future.");
        if (lossDate.Date < new DateTime(1900, 1, 1))
            throw new DomainRuleViolationException("DD-41", "Loss date cannot be earlier than 1900-01-01.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 20)
            throw new DomainRuleViolationException("BR-C-07", "Loss description is required and must be at least 20 characters.");
        if (string.IsNullOrWhiteSpace(causeOfLossCode) || !causeOfLossIsActive)
            throw new DomainRuleViolationException("BR-C-05", "Cause of loss code is not recognised or is inactive.");
        if (estimatedLossAmount.HasValue)
        {
            RuleGuard.Money(estimatedLossAmount.Value);
            if (estimatedLossAmount < 0)
                throw new DomainRuleViolationException("DD-30", "Estimated loss amount must not be negative.");
        }

        return new LossEvent
        {
            Id = RuleGuard.Id(id, nameof(id)),
            ClaimId = RuleGuard.Id(claimId, nameof(claimId)),
            LossDate = lossDate,
            LossDescription = description.Trim(),
            CauseOfLossCode = RuleGuard.Text(causeOfLossCode, nameof(causeOfLossCode), 50),
            EstimatedLossAmount = estimatedLossAmount,
            ReportDate = clock.UtcNow,
            LossLocation = location,
            PoliceReportNumber = policeReportNumber,
        };
    }
}
