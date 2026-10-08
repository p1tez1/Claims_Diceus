export type ClaimStatus = 'Draft' | 'Open' | 'UnderInvestigation' | 'PendingPayment' | 'Closed' | 'Reopened' | 'Withdrawn';
export type ClaimSeverity = 'Standard' | 'Catastrophic' | 'Critical' | 'Minor';
export type PartyRole = 'Claimant' | 'Insured' | 'ThirdParty' | 'Witness' | 'Attorney';
export type PartyType = 'Person' | 'Company';
export type AssetType = 'Vehicle' | 'Property' | 'Person' | 'Equipment' | 'Other';
export type ReserveComponentType = 'Indemnity' | 'Expense' | 'ALAE' | 'SubrogationRecoverable';
export type ReserveTransactionType = 'Add' | 'Adjust' | 'Reverse';
export type ReserveApprovalStatus = 'AutoApproved' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type PostingStatus = 'Pending' | 'Posted' | 'Failed' | 'Cancelled';
export type UserRole = 'Handler' | 'Supervisor' | 'Manager';
export type LiabilityDetermination = 'Accepted' | 'PartiallyAccepted' | 'Denied';

export interface Page<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
export interface ApiError { type: string; title: string; status: number; errors: Record<string, string[]>; correlationId?: string; }
export interface User { id: string; username?: string; name: string; role: UserRole; }
export interface LoginResponse { accessToken: string; user: User; }
export interface ClaimListItem { id: string; claimNumber: string; clientName?: string; policyNumber?: string; lossDate: string; causeOfLossCode: string; status: ClaimStatus; totalReserves: number; assignedHandlerId?: string; }
export interface ClaimListFilters { page?: number; pageSize?: number; status?: ClaimStatus[]; dateFrom?: string; dateTo?: string; assignedHandlerId?: string; causeOfLossCode?: string; policyId?: string; search?: string; }
export interface PartyInput { role: PartyRole; type: PartyType; firstName?: string; lastName?: string; companyName?: string; email?: string; phone?: string; notes?: string; }
export interface RiskObjectInput { assetType: AssetType; assetDescription: string; damageDescription?: string; isPrimary: boolean; assetReference?: string; }
export interface InitialReserveInput { component: ReserveComponentType; amount: number; changeReason?: string; }
export interface CreateClaimRequest { lossDate: string; lossDescription: string; causeOfLossCode: string; lossLocation?: string; estimatedLossAmount?: number; policyId?: string; severity?: ClaimSeverity; parties: PartyInput[]; riskObjects: RiskObjectInput[]; initialReserve?: InitialReserveInput; }
export interface IntakeIssue { field: string; code: string; message: string; }
export interface ClaimValidation { criticalIssues: IntakeIssue[]; warnings: IntakeIssue[]; }
export interface Party extends PartyInput { id: string; isActive: boolean; }
export interface RiskObject extends RiskObjectInput { id: string; }
export interface LossEvent { id: string; lossDate: string; lossDescription: string; lossLocation?: string; causeOfLossCode: string; estimatedLossAmount?: number; reportDate: string; policeReportNumber?: string; }
export interface ValidationIssue { id: string; code: string; message: string; severity: 'Critical' | 'Warning'; status: 'Open' | 'Acknowledged' | 'Resolved'; createdAt: string; acknowledgedAt?: string; resolvedAt?: string; }
export interface ReserveHistory { id: string; transactionType: ReserveTransactionType; amount: number; previousBalance?: number; newBalance?: number; changeSequence: number; changeReason: string; idempotencyKey: string; approvalStatus: ReserveApprovalStatus; postingStatus: PostingStatus; submittedByUserId: string; createdAt: string; approvedByUserId?: string; approvedAt?: string; rejectedByUserId?: string; rejectedAt?: string; rejectionReason?: string; resubmissionOfId?: string; postingJobId?: string; component?: ReserveComponentType; submittedByName?: string; approvedByName?: string; }
export interface ReserveComponent { id: string; component: ReserveComponentType; status: 'Active' | 'Closed'; currentAmount: number; pendingAmount: number; rowVersion: string; history: ReserveHistory[]; }
export interface DocumentInfo { id: string; claimId: string; documentType: string; documentName: string; blobPath: string; contentType: string; fileSizeBytes: number; uploadedAt: string; uploadedByUserId?: string; notes?: string; uploaderName?: string; downloadUrl?: string; }
export interface AuditLog { id: string; claimId: string; eventType: string; description: string; oldValue?: string; newValue?: string; relatedEntityId?: string; relatedEntityType?: string; correlationId?: string; createdAt: string; createdByUserId?: string; actorName?: string; }
export interface ClaimDetail { id: string; claimNumber: string; policyId?: string; policyNumber?: string; clientName?: string; status: ClaimStatus; severity: ClaimSeverity; reportedDate: string; assignedHandlerId?: string; closedAt?: string; closureReason?: string; notes?: string; managerOverride: boolean; slaBreachedAt?: string; liabilityDetermination?: string; liabilityDeterminedAt?: string; createdAt: string; updatedAt?: string; rowVersion: string; lossEvent: LossEvent; parties: Party[]; riskObjects: RiskObject[]; validationIssues: ValidationIssue[]; reserves: ReserveComponent[]; documents: DocumentInfo[]; audit: AuditLog[]; }
export interface SubmitReserveRequest { component: ReserveComponentType; amount: number; changeReason: string; transactionType: ReserveTransactionType; resubmissionOfId?: string; }
export interface ReserveTransactionResult { transaction: ReserveHistory; approvalStatus: ReserveApprovalStatus; warnings: { code: string; message: string }[]; }
export interface CauseOfLoss { id: string; code: string; name: string; perilCategory: string; sortOrder: number; }
export interface ClaimStatusReference { status: ClaimStatus; transitions: { to: ClaimStatus; requiredRole: UserRole; ruleCode: string }[]; }
export interface Policy { id: string; policyNumber: string; clientName: string; effectiveDate: string; expirationDate: string; status: string; coverageTypes: string[]; }
export interface BlockingCondition { code: string; message: string; }
export interface TransitionPreflight { allowed: boolean; blockingConditions: BlockingCondition[]; warnings: BlockingCondition[]; requiresJustification: boolean; requiresReason: boolean; }
export interface TransitionOptions { reason?: string; acknowledgeWarnings?: boolean; closureJustification?: string; }
