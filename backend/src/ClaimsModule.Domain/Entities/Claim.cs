using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.Rules;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Entities;

public sealed record ClaimTransitionOptions(string? Reason = null, bool AcknowledgeWarnings = false,
    string? ClosureJustification = null, bool LiabilityDetermined = false);

public sealed class Claim : AggregateRoot
{
    private readonly List<ClaimParty> _parties = [];
    private readonly List<ClaimRiskObject> _riskObjects = [];
    private readonly List<ClaimValidationIssue> _validationIssues = [];

    private Claim(Guid id) : base(id) { }

    public ClaimNumber ClaimNumber { get; private set; } = null!;
    public Guid? PolicyId { get; private set; }
    public string? PolicyNumber { get; private set; }
    public string? ClientName { get; private set; }
    public ClaimStatus Status { get; private set; }
    public ClaimSeverity Severity { get; private set; }
    public DateTimeOffset ReportedDate { get; private set; }
    public Guid? AssignedHandlerId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? ClosureReason { get; private set; }
    public string? Notes { get; private set; }
    public LiabilityDetermination? LiabilityDetermination { get; private set; }
    public DateTimeOffset? LiabilityDeterminedAt { get; private set; }
    public bool ManagerOverride { get; private set; }
    public DateTimeOffset? SlaBreachedAt { get; private set; }
    public ActivityMarker? LastActivity { get; private set; }
    public LossEvent LossEvent { get; private set; } = null!;
    public IReadOnlyList<ClaimParty> Parties => _parties.AsReadOnly();
    public IReadOnlyList<ClaimRiskObject> RiskObjects => _riskObjects.AsReadOnly();
    public IReadOnlyList<ClaimValidationIssue> ValidationIssues => _validationIssues.AsReadOnly();

    public static Claim Create(Guid id, ClaimNumber number, Guid createdBy, LossEvent lossEvent,
        IEnumerable<ClaimParty> parties, IClock clock, PolicyAssociation? policy = null,
        IEnumerable<ClaimRiskObject>? riskObjects = null, IEnumerable<ClaimValidationIssue>? validationIssues = null,
        ClaimSeverity severity = ClaimSeverity.Standard, string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(lossEvent);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Id(createdBy, nameof(createdBy));
        var partyRows = parties.ToArray();
        var riskRows = riskObjects?.ToArray() ?? [];
        var issueRows = validationIssues?.ToArray() ?? [];
        if (!partyRows.Any(party => party.IsActive && party.Role == PartyRole.Claimant))
            throw new DomainRuleViolationException("BR-C-03", "At least one active Claimant is required at creation.");
        if (issueRows.Any(issue => issue.Severity == ValidationSeverity.Critical && issue.Status != ValidationIssueStatus.Resolved))
            throw new DomainRuleViolationException("DD-10", "Critical validation issues reject claim creation.");
        if (partyRows.DistinctBy(row => row.Id).Count() != partyRows.Length
            || riskRows.DistinctBy(row => row.Id).Count() != riskRows.Length
            || issueRows.DistinctBy(row => row.Id).Count() != issueRows.Length)
            throw new DomainRuleViolationException("DUPLICATE_CHILD", "Child identifiers must be unique within a claim.");

        var claim = new Claim(id)
        {
            ClaimNumber = number,
            Status = ClaimStatus.Draft,
            Severity = RuleGuard.Defined(severity),
            ReportedDate = clock.UtcNow,
            AssignedHandlerId = createdBy,
            LossEvent = lossEvent,
            Notes = notes,
        };
        claim.EnsureChildClaim(lossEvent.ClaimId);
        foreach (var party in partyRows) claim.EnsureChildClaim(party.ClaimId);
        foreach (var risk in riskRows) claim.EnsureChildClaim(risk.ClaimId);
        foreach (var issue in issueRows) claim.EnsureChildClaim(issue.ClaimId);
        if (policy is not null) claim.ApplyPolicy(policy);
        claim._parties.AddRange(partyRows);
        claim._riskObjects.AddRange(riskRows);
        claim.Raise(new ClaimCreated(id, number.Value, createdBy, clock.UtcNow));
        foreach (var party in partyRows)
            claim.Raise(new ClaimPartyAdded(id, party.Id, party.Role, clock.UtcNow));
        foreach (var issue in issueRows) claim.AddValidationIssue(issue, clock);
        foreach (var warning in ClaimIntakeWarnings.Evaluate(lossEvent.LossDate, policy, riskRows.Length > 0))
            claim.AddWarning(warning.Code, warning.Message, clock);
        claim.Touch(clock);
        return claim;
    }

    public void AddParty(ClaimParty party, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(party);
        EnsureChildClaim(party.ClaimId);
        if (!party.IsActive || _parties.Any(row => row.Id == party.Id))
            throw new DomainRuleViolationException("DUPLICATE_PARTY", "Party must be active and not already present.");
        _parties.Add(party);
        Raise(new ClaimPartyAdded(Id, party.Id, party.Role, clock.UtcNow));
        Touch(clock);
    }

    public void RemoveParty(Guid partyId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var party = _parties.SingleOrDefault(row => row.Id == partyId && row.IsActive)
            ?? throw new DomainRuleViolationException("PARTY_NOT_FOUND", "Active party was not found.");
        if (party.Role == PartyRole.Claimant && _parties.Count(row => row.IsActive && row.Role == PartyRole.Claimant) == 1)
            throw new DomainRuleViolationException("BR-P-01", "The last active Claimant cannot be removed.");
        party.Deactivate();
        Raise(new ClaimPartyRemoved(Id, party.Id, party.Role, clock.UtcNow));
        Touch(clock);
    }

    public void AddRiskObject(ClaimRiskObject riskObject, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(riskObject);
        EnsureChildClaim(riskObject.ClaimId);
        if (_riskObjects.Any(row => row.Id == riskObject.Id))
            throw new DomainRuleViolationException("DUPLICATE_RISK_OBJECT", "Risk object is already present.");
        _riskObjects.Add(riskObject);
        ResolveMatchingWarnings("NO_RISK_OBJECTS", clock);
        Touch(clock);
    }

    public void AddValidationIssue(ClaimValidationIssue issue, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(issue);
        EnsureChildClaim(issue.ClaimId);
        if (_validationIssues.Any(row => row.Id == issue.Id))
            throw new DomainRuleViolationException("DUPLICATE_VALIDATION_ISSUE", "Validation issue is already present.");
        _validationIssues.Add(issue);
        Raise(new ValidationIssueAdded(Id, issue.Id, issue.Code, issue.Severity, clock.UtcNow));
        Touch(clock);
    }

    public void AcknowledgeValidationIssue(Guid issueId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        FindIssue(issueId).Acknowledge(clock.UtcNow);
        Touch(clock);
    }

    public void ResolveValidationIssue(Guid issueId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        FindIssue(issueId).Resolve(clock.UtcNow);
        Touch(clock);
    }

    public void LinkPolicy(PolicyAssociation policy, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(policy);
        ApplyPolicy(policy);
        ResolveMatchingWarnings("BR-C-06", clock);
        ResolveMatchingWarnings("BR-C-02", clock);
        if (ClaimIntakeWarnings.OutsidePolicyWindow(LossEvent.LossDate, policy))
            AddWarning("BR-C-02", "Loss date is outside the policy effective period.", clock);
        Raise(new PolicyLinked(Id, policy.Id, clock.UtcNow));
        Touch(clock);
    }

    public ClaimTransitionEvaluation EvaluateTransition(ClaimStatus target, UserRole role,
        IReadOnlyCollection<ClaimReserveComponent> reserves, ClaimTransitionOptions? options = null,
        ClaimStateMachine? stateMachine = null)
    {
        ArgumentNullException.ThrowIfNull(reserves);
        ValidateReserveSet(reserves);
        var machine = stateMachine ?? new ClaimStateMachine();
        var context = TransitionContext(role, reserves, options ?? new());
        var evaluation = machine.Evaluate(Status, target, context);
        if (target != ClaimStatus.Reopened || !evaluation.ValidNextStatuses.Contains(ClaimStatus.Reopened)) return evaluation;

        var openLeg = machine.Evaluate(ClaimStatus.Reopened, ClaimStatus.Open, context);
        return evaluation with
        {
            BlockingConditions = Array.AsReadOnly(evaluation.BlockingConditions.Concat(openLeg.BlockingConditions).ToArray()),
            Warnings = Array.AsReadOnly(evaluation.Warnings.Concat(openLeg.Warnings).ToArray()),
        };
    }

    public void TransitionTo(ClaimStatus target, Guid userId, UserRole role, IClock clock,
        IReadOnlyCollection<ClaimReserveComponent> reserves, ClaimTransitionOptions? options = null,
        ClaimStateMachine? stateMachine = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Id(userId, nameof(userId));
        options ??= new();
        if (target == ClaimStatus.Reopened)
        {
            Reopen(userId, role, options.Reason ?? string.Empty, clock, reserves, options, stateMachine);
            return;
        }

        EnsureAllowed(EvaluateTransition(target, role, reserves, options, stateMachine));
        if (target == ClaimStatus.Open && options.AcknowledgeWarnings)
            foreach (var issue in _validationIssues.Where(row => row.Severity == ValidationSeverity.Warning && row.Status == ValidationIssueStatus.Open))
                issue.Acknowledge(clock.UtcNow);
        ChangeStatus(target, userId, options.Reason, clock);
        if (target == ClaimStatus.Closed)
        {
            ClosedAt = clock.UtcNow;
            ClosureReason = string.IsNullOrWhiteSpace(options.Reason) ? options.ClosureJustification : options.Reason;
            Raise(new ClaimClosed(Id, userId, ClosureReason, options.ClosureJustification, clock.UtcNow));
        }
        Touch(clock);
    }

    public void Reopen(Guid userId, UserRole role, string reason, IClock clock,
        IReadOnlyCollection<ClaimReserveComponent> reserves, ClaimTransitionOptions? options = null,
        ClaimStateMachine? stateMachine = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Id(userId, nameof(userId));
        options = (options ?? new()) with { Reason = reason };
        var machine = stateMachine ?? new ClaimStateMachine();
        // Preflight validates both legs before emitting events or changing state.
        EnsureAllowed(EvaluateTransition(ClaimStatus.Reopened, role, reserves, options, machine));
        if (options.AcknowledgeWarnings)
            foreach (var issue in _validationIssues.Where(row => row.Severity == ValidationSeverity.Warning && row.Status == ValidationIssueStatus.Open))
                issue.Acknowledge(clock.UtcNow);
        ChangeStatus(ClaimStatus.Reopened, userId, reason, clock);
        Raise(new ClaimReopened(Id, userId, reason, clock.UtcNow));
        ChangeStatus(ClaimStatus.Open, userId, reason, clock);
        ClosedAt = null;
        ClosureReason = null;
        Touch(clock);
    }

    public void AssignHandler(Guid handlerId, UserRole role, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Defined(role);
        if (role < UserRole.Supervisor) throw new InsufficientAuthorityException("Supervisor or higher is required to assign a handler.");
        AssignedHandlerId = RuleGuard.Id(handlerId, nameof(handlerId));
        Touch(clock);
    }

    public void UpdateNotes(string? notes, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Notes = notes;
        Touch(clock);
    }

    public void DetermineLiability(LiabilityDetermination determination, string? notes, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Defined(determination);
        if (Status is ClaimStatus.Closed or ClaimStatus.Withdrawn)
            throw new DomainRuleViolationException("DD-37", "Liability cannot be determined on a Closed or Withdrawn claim.");
        var previous = LiabilityDetermination;
        LiabilityDetermination = determination;
        LiabilityDeterminedAt = clock.UtcNow;
        Raise(new LiabilityDetermined(Id, previous, determination, notes, clock.UtcNow));
        Touch(clock);
    }

    public void SetManagerOverride(Guid userId, UserRole role, bool enabled, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        RuleGuard.Id(userId, nameof(userId));
        RuleGuard.Defined(role);
        if (role != UserRole.Manager) throw new InsufficientAuthorityException("Only a Manager may set the reserve override.");
        ManagerOverride = enabled;
        Raise(new ReserveOverrideSet(Id, userId, enabled, clock.UtcNow));
        Touch(clock);
    }

    public void EnsureReserveSubmissionAllowed()
    {
        if (!PolicyId.HasValue)
            throw new DomainRuleViolationException("BR-C-06", "A policy must be linked before reserves can be submitted.");
        if (Status is ClaimStatus.Closed or ClaimStatus.Withdrawn)
            throw new DomainRuleViolationException("CLAIM_STATUS", "Reserves cannot be submitted on a Closed or Withdrawn claim.");
    }

    public void Touch(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        LastActivity = new(clock.UtcNow, checked((LastActivity?.Revision ?? 0) + 1));
        SlaBreachedAt = null;
    }

    public void FlagSlaBreach(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        SlaBreachedAt = clock.UtcNow;
    }

    private void ApplyPolicy(PolicyAssociation policy)
    {
        var id = RuleGuard.Id(policy.Id, nameof(policy.Id));
        var number = RuleGuard.Text(policy.PolicyNumber, nameof(policy.PolicyNumber), 50);
        var name = RuleGuard.Text(policy.ClientName, nameof(policy.ClientName), 255);
        if (policy.EffectiveDate > policy.ExpirationDate)
            throw new DomainRuleViolationException("INVALID_POLICY_PERIOD", "Policy effective date must not follow expiration date.");
        PolicyId = id;
        PolicyNumber = number;
        ClientName = name;
    }

    private void AddWarning(string code, string message, IClock clock)
    {
        if (!_validationIssues.Any(row => row.Code == code && row.Status != ValidationIssueStatus.Resolved))
            AddValidationIssue(ClaimValidationIssue.Create(Guid.CreateVersion7(clock.UtcNow), Id, code, message, ValidationSeverity.Warning, clock), clock);
    }

    private ClaimValidationIssue FindIssue(Guid id) => _validationIssues.SingleOrDefault(row => row.Id == id)
        ?? throw new DomainRuleViolationException("VALIDATION_ISSUE_NOT_FOUND", "Validation issue was not found.");

    private void ResolveMatchingWarnings(string code, IClock clock)
    {
        foreach (var issue in _validationIssues.Where(row => row.Code == code && row.Severity == ValidationSeverity.Warning && row.Status != ValidationIssueStatus.Resolved))
            issue.Resolve(clock.UtcNow);
    }

    private ClaimTransitionContext TransitionContext(UserRole role, IReadOnlyCollection<ClaimReserveComponent> reserves, ClaimTransitionOptions options) => new(
        role, _parties.Any(row => row.IsActive && row.Role == PartyRole.Claimant),
        _validationIssues.Any(row => row.Severity == ValidationSeverity.Critical && row.Status != ValidationIssueStatus.Resolved),
        _validationIssues.Any(row => row.Severity == ValidationSeverity.Warning && row.Status == ValidationIssueStatus.Open),
        AssignedHandlerId.HasValue, reserves.Any(row => row.HasApprovedTransactions),
        reserves.Any(row => row.HasPendingTransactions), reserves.Any(row => row.CurrentAmount != 0),
        LiabilityDetermination is ClaimsModule.Domain.Enums.LiabilityDetermination.Accepted
            or ClaimsModule.Domain.Enums.LiabilityDetermination.PartiallyAccepted,
        options.Reason, options.AcknowledgeWarnings, options.ClosureJustification);

    internal void ValidateReserveSet(IReadOnlyCollection<ClaimReserveComponent> reserves)
    {
        foreach (var reserve in reserves) EnsureChildClaim(reserve.ClaimId);
        if (reserves.DistinctBy(row => row.Component).Count() != reserves.Count)
            throw new DomainRuleViolationException("DUPLICATE_RESERVE_COMPONENT", "A claim has one reserve component per type.");
    }

    private void EnsureChildClaim(Guid claimId)
    {
        if (claimId != Id) throw new DomainRuleViolationException("CLAIM_MISMATCH", "The child belongs to another claim.");
    }

    private static void EnsureAllowed(ClaimTransitionEvaluation evaluation)
    {
        var roleBlock = evaluation.BlockingConditions.FirstOrDefault(row => row.Code == "INSUFFICIENT_AUTHORITY");
        if (roleBlock is not null) throw new InsufficientAuthorityException(roleBlock.Message);
        if (!evaluation.IsAllowed)
            throw new DomainRuleViolationException(evaluation.BlockingConditions[0].Code,
                string.Join(" ", evaluation.BlockingConditions.Select(row => $"{row.Code}: {row.Message}")));
    }

    private void ChangeStatus(ClaimStatus target, Guid userId, string? reason, IClock clock)
    {
        var from = Status;
        Status = target;
        Raise(new ClaimStatusChanged(Id, from, target, userId, reason, clock.UtcNow));
    }
}
