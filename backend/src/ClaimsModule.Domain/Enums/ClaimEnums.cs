namespace ClaimsModule.Domain.Enums;

public enum ClaimStatus { Draft, Open, UnderInvestigation, PendingPayment, Closed, Reopened, Withdrawn }
public enum ClaimSeverity { Standard, Catastrophic, Critical, Minor }
public enum PartyRole { Claimant, Insured, ThirdParty, Witness, Attorney }
public enum PartyType { Person, Company }
public enum AssetType { Vehicle, Property, Person, Equipment, Other }
public enum ReserveComponentType { Indemnity, Expense, ALAE, SubrogationRecoverable }
public enum ReserveComponentStatus { Active, Closed }
public enum ReserveTransactionType { Add, Adjust, Reverse }
public enum ReserveApprovalStatus { AutoApproved, PendingApproval, Approved, Rejected, Cancelled }
public enum PostingStatus { Pending, Posted, Failed, Cancelled }
public enum UserRole { Handler, Supervisor, Manager }
public enum ValidationSeverity { Critical, Warning }
public enum ValidationIssueStatus { Open, Acknowledged, Resolved }
public enum LiabilityDetermination { Accepted, PartiallyAccepted, Denied }
