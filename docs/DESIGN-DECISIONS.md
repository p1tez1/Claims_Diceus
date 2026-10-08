# Design decisions

D1 is [the functional specification](spec/spec-1-functional.md). D2 is [the assessment specification](spec/spec-2-assessment.md). The following 31 decisions are supplied by the user and take precedence over both specifications where explicit. The source references show traceability; "Derived" marks details supplied by these decisions rather than directly prescribed by a spec. DISAGREEMENT notes preserve departures without changing the supplied decisions. Open questions are not decisions.

## DD-01

Precedence: D1 over D2 for functional behavior; D2 for technology, structure, deliverables, deployment.

Source / derived: D1 §§1.3, 2; D2 §§2.3–2.4, 3.8, 4. Derived: the user establishes source-of-truth precedence and explicit recorded overrides.

## DD-02

Reserve components: Indemnity, Expense, ALAE, SubrogationRecoverable (D1). D2's Recovery/Litigation names are not used.

Source / derived: D1 §§6.2, 9.5; D2 Appendix A.2.

DISAGREEMENT: D2 Appendix A.2 names RecoveryReserve/LitigationReserve and differently named indemnity/expense reserves. This decision retains D1's exact component names.

## DD-03

Reserve API (D1 canonical): POST /api/claims/{id}/reserves {component, amount, changeReason, transactionType: Add|Adjust|Reverse}; GET /api/claims/{id}/reserves; POST .../reserves/{txnId}/approve | reject {rejectionReason} | retract; POST .../reserves/{txnId}/retry-posting (derived). Also PUT /api/claims/{id}/reserves/{componentId} as a thin adapter that creates an Adjust transaction (D2 compatibility).

Source / derived: D1 §10.2; D2 §3.3.3. Derived: retry-posting from D1 §11.3 and the PUT adapter with componentId-to-Adjust semantics.

DISAGREEMENT: D2 §3.3.3 describes reserveId approval/rejection targets and a separate adjustment PUT. This decision uses transaction targets canonically and keeps PUT only as a component adapter.

## DD-04

Ledger: ReserveHistory row = one transaction; Amount is a SIGNED DELTA. Request semantics: Add -> amount > 0 (SubrogationRecoverable: amount < 0); Adjust -> signed non-zero delta; Reverse -> positive magnitude stored as negative delta. Authority is evaluated on |delta| per transaction (never the running total). Non-subrogation balance must never go below 0 once approved. Zero amounts rejected (BR-R-01).

Source / derived: D1 §§6.2–6.3, 7.2 BR-R-01/02, 9.6; D2 §3.4.2. Derived: Add/Adjust/Reverse request signs, absolute-delta authority, zero rejection, and the approval-time balance invariant.

DISAGREEMENT: D1 §7.2 BR-R-01 and §8 require positive non-subrogation transaction amounts, while D1 §9.6 permits negative deltas. D2 §3.4.2 BR-R-01 says amounts must be positive. This decision explicitly permits negative Adjust deltas and stores Reverse as a negative delta; absolute-value authority is an explicit extension.

## DD-05

Balance = sum of deltas where ApprovalStatus in (AutoApproved, Approved). PendingApproval is shown as "pending amount". PostingStatus does not affect balance. PreviousBalance/NewBalance are recomputed at approval time inside the transaction.

Source / derived: D1 §§6.6, 9.5–9.6, 11.3. Derived: AutoApproved/Approved membership independent of posting, and approval-time balance recomputation.

DISAGREEMENT: D1 §§6.6/9.5 say posted/approved without defining whether posting matters; §6.3 calls automatic status Approved while §9.6 distinguishes AutoApproved. This decision selects approval membership alone.

## DD-06

Immutable once written: ReserveComponentId, TransactionType, Amount, ChangeSequence, IdempotencyKey. Workflow fields (ApprovalStatus, approver/rejector fields, PostingStatus, PostingJobId, balances at approval) change only via domain methods. ClaimAuditLog and ReserveHistory financial fields are never updated through any other path.

Source / derived: D1 §§6.4–6.6, 9.6, 12.1, 14.2. Derived: the precise immutable/workflow field partition and domain-method enforcement.

DISAGREEMENT: D1 §§6.6/9.6 describe ReserveHistory as append-only/no reserve UPDATE, but §§10.2/12.1 require workflow updates. This decision permits only the listed workflow/balance updates and keeps financial identity fields immutable.

## DD-07

Any role may submit any amount. >10,000 -> PendingApproval. Approval: supervisor up to 100,000, manager up to 10,000,000, >100,000 manager only. The submitter can never approve own transaction (422 "Self-approval is not permitted."). Insufficient role/authority -> 403. Only the submitter may retract, only while PendingApproval (-> Cancelled, PostingStatus Cancelled). A PendingApproval transaction is never modified.

Source / derived: D1 §§3, 6.3–6.4, 7.2 BR-R-02/03, 10.2; D2 §3.4.2. Derived: any-role submission, 403 for insufficient authority, submitter-only retraction, and cancelled posting status.

DISAGREEMENT: D1 §3 describes handlers as opening reserves within $10,000; this decision permits submission of any amount with approval gating. D1 §8/§10 presents authority failures as validation errors (422); this decision uses 403. D1 §§6.3/7.2 do not state a $10M individual-transaction ceiling, whereas this decision retains the manager limit from §3.

## DD-08

Aggregate cap 10,000,000: total = net signed sum of approved component balances plus the new delta. If exceeded and Claim.ManagerOverride is false: return a Warning, force PendingApproval even if <=10,000, and block approval (422) until a manager sets the flag via POST /api/claims/{id}/reserve-override (manager only; audit event CLAIM_RESERVE_OVERRIDE_SET).

Source / derived: D1 §7.2 BR-R-05 and §8; D2 §3.4.2 BR-R-07. Derived: net-signed cap calculation, forcing PendingApproval below the auto threshold, ManagerOverride field, endpoint, and audit event.

## DD-09

BR-C-06: reserve creation (including an initial reserve on claim creation) is rejected (422) while PolicyId is null. Add PUT /api/claims/{id}/policy {policyId} to link a policy; it re-evaluates policy warnings and resolves the "policy unknown" issue; audit it.

Source / derived: D1 §§5.2, 7.1 BR-C-06, 8, 10.1. Derived: link-policy endpoint, issue resolution, re-evaluation, and audit integration.

## DD-10

Validation: every Critical rule rejects creation with 422 using D1 section 10.4 body. Warnings (no policy, outside policy period, no risk objects) are persisted as ClaimValidationIssues (Open -> Acknowledged | Resolved) with VALIDATION_ISSUE_ADDED audit. POST /api/claims/validate is a dry run returning issues. Draft->Open requires no Critical issue, >=1 Claimant, and acknowledgeWarnings=true when any Open warning exists.

Source / derived: D1 §§4.2, 5.4, 7.1–7.3, 7.5, 8, 10.4, 14.1; D2 §§3.4.1, 3.7.2. Derived: dry-run route, persisted issue state machine, and acknowledgeWarnings flag.

DISAGREEMENT: D1 §5.4 says Critical issues leave a created claim in Draft, and §§7.1 BR-C-03/7.5 allow claimant-free Drafts. This decision rejects creation for every Critical rule. D1 §4.2 allows waiving Critical issues; the chosen warning-only issue state machine provides no Critical-waiver path. D1 §5.4 describes warnings as non-blocking, whereas this decision requires acknowledgement of all Open warnings (consistent with the policy-period warning in §7.1 BR-C-02).

## DD-11

ClaimStatusTransitions is seeded (From, To, RequiredRole, RuleCode) and is the data source for validation and for GET /api/reference/claim-statuses. Minimum role is handler for all transitions except Reopen (supervisor). Add PUT /api/claims/{id}/assignee for handler assignment (supervisor+; creator is default assignee).

Source / derived: D1 §§3, 4.1–4.2, 9.1, 10.3, 11.1/11.3; D2 §§3.2, 3.3.1/3.3.4. Derived: RuleCode, minimum-role matrix, assignee endpoint and default assignment.

DISAGREEMENT: D1 §3 broadly places status management with supervisors, while §4.2 explicitly permits several handler transitions. This decision sets handler as the minimum for every transition except Reopen.

## DD-12

Transition request body: {targetStatus, reason?, acknowledgeWarnings?, closureJustification?}. Invalid transition -> 422 listing valid next statuses. GET /api/claims/{id}/transitions/{targetStatus}/preflight returns blocking conditions and warnings.

Source / derived: D1 §§4.3, 7.3, 10.1, 11.3. Derived: expanded request flags and the preflight endpoint.

## DD-13

Reopen: request targetStatus=Reopened; Closed->Reopened->Open occur in one transaction; two STATUS_CHANGED entries plus CLAIM_REOPENED; response shows final status Open. Reason mandatory.

Source / derived: D1 §§4.2, 7.3 BR-ST-04, 14.1. Derived: atomic two-step transition, exact audit entry count, and final Open response.

## DD-14

Closed requires CC-01..CC-03; CC-04 requires closureJustification when any component balance is non-zero. Open->Closed is always allowed per D1 (no "configured" switch). Withdrawn requires a reason. Withdrawn is terminal. Draft->Closed is invalid.

Source / derived: D1 §§4.2–4.3, 7.3; D2 §3.4.1 BR-C-06. Derived: explicit terminal Withdrawn semantics and non-zero closure test.

DISAGREEMENT: D1 §4.3 CC-04 requires justification for a positive balance; this decision requires it for any non-zero balance, including negative recoveries. D2 §3.4.1 BR-C-06 makes direct Open-to-Closed conditional on configuration; this decision follows D1's unconditional state-machine edge.

## DD-15

Reasons are stored in the audit entry (NewValue JSON) and Claims.ClosureReason where applicable.

Source / derived: D1 §§4.2–4.3, 9.1, 14.1. Derived: using NewValue JSON for all transition reasons, with ClosureReason where applicable.

## DD-16

Claim numbers: table ClaimNumberSequences(OrganisationId, Year, LastValue). Allocate with an atomic UPDATE ... OUTPUT inserted.LastValue inside the SAME transaction as the claim insert (gap-free, rolled back on failure). Unique index (OrganisationId, ClaimNumber). Soft-deleted claims keep their numbers. Do not use a SQL SEQUENCE (gaps).

Source / derived: D1 §§5.3, 7.1 BR-C-04; D2 §3.4.1 BR-C-04. Derived: counter schema, tenant/year scope, shared transaction, and unique index.

DISAGREEMENT: D1 §5.3 allows a database SEQUENCE despite its no-gap requirement; this decision disallows it and selects a transactional counter.

## DD-17

PostGLReserveChangeJob(reserveHistoryId, claimId, idempotencyKey). Key = Reserve:{ReserveComponentId}:Change:{ChangeSequence}; unique index on ReserveHistory.IdempotencyKey. Enqueue only AFTER the owning transaction commits (IGlPostingScheduler as a post-commit action). The job loads the row, no-ops if already Posted, otherwise in ONE transaction does a conditional update (PostingStatus<>'Posted') and writes the GL_POSTING_SIMULATED audit row only if that update affected a row. After all retries are exhausted (Hangfire state filter) set PostingStatus=Failed and write GL_POSTING_FAILED once. Retry endpoint resets Failed->Pending and enqueues a job with the same key.

Source / derived: D1 §§6.5, 7.2 BR-R-06, 9.6, 11.3, 12.1; D2 §3.5. Derived: component-based key identity, unique index, conditional transactional claim, post-commit scheduler, terminal-failure filter, and retry endpoint.

## DD-18

SlaMonitoringJob cron */15 * * * *. Candidates: Status in (Draft, Open) AND COALESCE(UpdatedAt, CreatedAt) < now-48h, across tenants (IgnoreQueryFilters but still excluding soft-deleted). Write SLA_BREACH_DETECTED only if no such entry exists in the last 24h. Set nullable Claims.SlaBreachedAt without touching UpdatedAt; never change Status. Every business action on a claim bumps Claims.UpdatedAt and clears SlaBreachedAt.

Source / derived: D1 §§9.1, 12.2, 15.1; D2 §3.5. Derived: CreatedAt fallback, cross-tenant scan with explicit soft-delete exclusion, SlaBreachedAt, and business-action reset.

DISAGREEMENT: D2 §3.5 specifies SlaBreached status, while D1 §12.2 forbids changing status. This decision follows D1 and adds a timestamp. COALESCE also includes never-updated claims excluded by D1's literal nullable UpdatedAt comparison.

## DD-19

Auth: JWT bearer with symmetric key from config; claims sub, name, role, org_id. POST /api/auth/dev-login {username} returns a token for seeded users (handler, supervisor, manager; flag Auth:DevLoginEnabled). Users table seeded; GET /api/reference/users?role= for names/assignee filter. Role hierarchy handler < supervisor < manager implemented as authorization policies.

Source / derived: D1 §§3, 10, 11.4; D2 §§3.7.4, 4.3. Derived: symmetric JWT configuration and claim names, dev-login flag/route, seeded Users, users query, and authorization policies.

## DD-20

Errors: D1 section 10.4 shape ({type,title,status,errors}); 400 malformed, 401, 403, 404, 409 (concurrency), 422 (validation/business). All produced by one middleware. X-Correlation-Id accepted/generated and propagated to audit entries.

Source / derived: D1 §§7.3, 10.4, 14.2; D2 §6.2. Derived: HTTP status taxonomy, centralized middleware, and X-Correlation-Id header.

## DD-21

Idempotency-Key header on POST: IdempotencyRecords(OrganisationId, Key, RequestHash, StatusCode, ResponseBody). Same key+same body replays the stored response; same key+different body -> 422.

Source / derived: D1 §10. Derived: IdempotencyRecords fields, request-hash comparison, replay, and different-body rejection.

DISAGREEMENT: D1 §10 requires header-based idempotency for all write operations. This decision explicitly scopes the recorded mechanism to POST; PUT/DELETE coverage remains an open question.

## DD-22

Storage: config Storage:Provider = AzureBlob | LocalFileSystem. Max 50 MB; MIME allowlist PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV; sanitize filename (strip paths/control chars); name collision -> numeric suffix; blob path {org}/{claim}/{filename} in container claim-documents; SAS 1h; bytes never proxied for Azure; local provider serves via a dev-only download endpoint with path-traversal protection.

Source / derived: D1 §§7.4, 13; D2 §3.6. Derived: Storage:Provider key, sanitization details, numeric collision suffix, and dev-only local download endpoint.

DISAGREEMENT: D1 §13 names StorageProvider and D2 §3.6 names Provider. This decision standardizes the configuration path as Storage:Provider.

## DD-23

Frontend: Angular 18+ standalone components with lazy-loaded feature routes (claims-list, claim-detail, fnol-intake), signals-based state, Angular Material custom palette, Reactive Forms, typed service layer only, mock AuthService calling dev-login with role switcher.

Source / derived: D1 §§11.2–11.4; D2 §§2.3, 3.7. Derived: standalone lazy feature routes, signals state, and dev-login-backed mock authentication.

DISAGREEMENT: D1 §11.4 and D2 §3.7.4 prescribe lazy-loaded feature modules. This decision uses standalone components with lazy feature routes.

## DD-24

EF Core: audit columns, soft-delete columns and OrganisationId as shadow properties set by a SaveChanges interceptor; one combined global query filter (!IsDeleted AND OrganisationId==tenant); enums stored as strings via value conversion; RowVer as rowversion on Claims and ClaimReserveComponents; client-side sequential GUIDs while the column keeps default NEWSEQUENTIALID(); domain events collected from aggregates and dispatched BEFORE commit inside the same transaction; Hangfire enqueue AFTER commit; audit rows only through IAuditLogService; an interceptor blocks any UPDATE/DELETE of ClaimAuditLog.

Source / derived: D1 §§14.2, 15.1–15.2; D2 §§2.4, 6.1. Derived: shadow properties/interceptor, combined tenant filter, client-generated sequential GUIDs, event timing, post-commit enqueue, and audit mutation guard.

DISAGREEMENT: D2 §§3.2/A.4 require OrganizationEntityId; this decision follows D1's OrganisationId spelling. D1 §§15.1/15.2 specify NEWSEQUENTIALID() defaults; explicit client GUIDs retain that default but bypass it for normal inserts.

## DD-25

Fixed GUIDs exist only in Persistence seed constants. Application logic obtains OrganisationId/user from the token/config.

Source / derived: D1 §§15.1, 15.4; D2 §3.2. Derived: restricting fixed seed GUIDs to Persistence constants and token/config access.

## DD-26

Package pins per AGENTS.md package policy.

Source / derived: D2 §2.3; user-supplied AGENTS.md package policy. Derived: version/license constraints and the documented test-double choice.

## DD-27

A rejected reserve is resubmitted as a new Add/Adjust transaction with nullable ResubmissionOfId; the rejected row stays.

Source / derived: D1 §§6.4, 7.2 BR-R-04, 9.6; D2 §3.4.2 BR-R-06. Derived: ResubmissionOfId and selecting Add/Adjust for a new row.

DISAGREEMENT: D2 §3.4.2 BR-R-06 says a revised reserve creates a new pending record. This decision routes a new Add/Adjust through the normal authority path, which may auto-approve according to D1 §§6.3/7.2.

## DD-28

One ClaimReserveComponents row per (ClaimId, Component), created on first transaction. CurrentAmount is a persisted cache updated in the same transaction as each approval; a test must assert it equals the ledger sum.

Source / derived: D1 §§9.5–9.6, 15.1; D2 §3.2. Derived: one component row per claim/type, first-use creation, persisted cache, and ledger-equivalence test.

DISAGREEMENT: D1 §9.5 describes CurrentAmount as computed from history. This decision persists it as a cache while preserving the ledger as truth.

## DD-29

Initial reserve on claim creation uses the same reserve submission path in the same transaction.

Source / derived: D1 §§5.2, 10.1; D2 §3.7.2. Derived: reusing the same reserve submission path within claim creation.

## DD-30

ClaimType is omitted. Severity is optional (default Standard). EstimatedLossAmount >= 0 when provided.

Source / derived: D1 §§9.1–9.2; D2 §3.2. Derived: optional/default Standard severity and non-negative estimated loss validation.

DISAGREEMENT: D2 §3.2 lists ClaimType as a key field; this decision omits it in accordance with D1's entity list.

## DD-31

Hangfire uses SQL Server storage (schema HangFire, its own installer permitted). Dashboard disabled or restricted to the manager role in non-development environments.

Source / derived: D1 §§12, 15.4; D2 §§2.3, 3.2. Derived: HangFire schema, installer exception, and dashboard access restriction.

DISAGREEMENT: D2 §3.2 prohibits runtime schema creation and D1 §15.4 expects migrations alone to prepare the database. This decision makes an explicit exception for Hangfire's own installer.

## Derived endpoints and model additions

| Addition | Purpose and decision |
| --- | --- |
| `POST /api/claims/validate` | Preview validation without persistence (DD-10; D1 §5.4). |
| `PUT /api/claims/{id}/policy` | Link an unknown-policy claim before financial actions and resolve policy issues (DD-09; D1 BR-C-06). |
| `PUT /api/claims/{id}/assignee` | Manage the handler needed for Open and dashboard filtering (DD-11; D1 §§4.1, 11.1). |
| `POST /api/claims/{id}/reserve-override` | Let a manager authorize exceeding the aggregate cap and audit it (DD-08; D1 BR-R-05). |
| `POST /api/claims/{id}/reserves/{txnId}/retry-posting` | Support the failed-GL retry button using the original key (DD-03/17; D1 §11.3). |
| `GET /api/claims/{id}/transitions/{targetStatus}/preflight` | Populate the transition checklist with blockers and warnings (DD-12; D1 §11.3). |
| `POST /api/auth/dev-login` | Issue real bearer tokens for simulated users under a feature flag (DD-19; D1 §11.4). |
| `GET /api/reference/users?role=` and `Users` | Resolve actor names and offer handler choices (DD-19; D1 §§11.1, 11.3). |
| `ClaimValidationIssues` | Persist warnings and their Open/Acknowledged/Resolved lifecycle (DD-10). |
| `ClaimNumberSequences` | Allocate gap-free numbers atomically per organisation/year (DD-16). |
| `IdempotencyRecords` | Store request hashes and responses for write replay (DD-21). |
| `Claims.ManagerOverride` | Store the manager's aggregate-cap authorization (DD-08). |
| `Claims.SlaBreachedAt` | Track a breach without altering lifecycle status (DD-18). |
| `ReserveHistory.ResubmissionOfId` | Link a revised transaction to the retained rejected row (DD-27). |
| Local dev-only download endpoint (route unspecified) | Serve fallback files with path protection (DD-22; D1 §13). |

The reserve adjustment PUT is a D2 compatibility adapter (DD-03), not another ledger mutation path. ClaimStatusTransitions is required by D2 §3.2; its RuleCode and role rules are specified by DD-11.

## Open questions

### Clarifications supplied for Prompt 4 (Domain layer)

The user resolved these points while implementing the Domain layer:

- DD-07/DD-08: ManagerOverride permits only aggregate totals above $10M. The individual transaction approval ceiling remains $10M, including when the override is set. Any role may still submit a larger transaction, but it cannot be approved under this authority policy.
- DD-10: Claim.Create requires at least one active Claimant. Critical issues arising later are represented as ClaimValidationIssues and must be resolved before Open/Closed; there is no acknowledgement or waiver path for Critical issues.
- D1 §4.2: Domain receives liability determination as an explicit transition-context flag. Selection of its persisted field and endpoint is deferred.
- DD-18: Background GL success/failure count as claim activity: MarkPosted/MarkPostingFailed bump the activity marker and clear SlaBreachedAt. Duplicate/stale deliveries which change nothing do not count as new activity. The SLA detection action itself retains DD-18's explicit no-UpdatedAt behavior.

### Still unresolved

These questions do not override DD-01 through DD-31. STOP and report them when they materially affect the current implementation task.

- DD-21 specifies POST idempotency, whereas D1 §10 covers all writes. How should PUT/DELETE use Idempotency-Key? What are the key's operation scope, canonical request hashing rules, retention, and treatment of concurrent/in-flight requests, multipart uploads, and dry-run validation?
- DD-17 schedules after commit. How should an enqueue failure or process crash between commit and enqueue be recovered? No durable delivery mechanism is selected here.
- DD-18: How must the interceptor preserve the SLA job's explicit no-UpdatedAt rule when the SLA audit action is persisted?
- How should concurrent approvals and new per-tenant/year counter rows be serialized or retried, and how should approval-time balances be presented when approval order differs from ChangeSequence order?
- D1 §4.2: Which persisted field and endpoint will supply the liability determination consumed by Domain's transition context?
- D1 §11.3 requires editable claim notes; no update endpoint is selected. D2 §3.3.2 requires a policy coverage endpoint; its relationship to search-returned coverageTypes is not resolved by this decision list.
- DD-24 makes OrganisationId a shadow property, while DD-16 uses it in a counter key. How should tenant identity be exposed to domain methods without duplicating persistence state? What do the universal audit/soft-delete conventions mean for immutable audit rows and Hangfire-owned tables?
- DD-19 defaults assignment to the creator, who may be a supervisor or manager. Does the role-filtered assignee list include these inherited handler roles?
- Which roles may retry failed posting and access the local development download route? How is tenant authorization enforced there, and how is dev-login configured for the public review deployment?
- Which exact authorized package versions satisfy DD-26, compatibility, and the package license policy? No new version numbers are selected in this documentation task.

## Decisions added before Prompt 5

The user-supplied [DD-32 through DD-40 addendum](DESIGN-DECISIONS-addendum.md) resolves the corresponding questions above and takes precedence where explicit. The four Prompt 4 clarifications remain accepted. Prompt 5 implementation details and verification are recorded in [PERSISTENCE.md](PERSISTENCE.md).

## DD-41

User-entered and API-filter dates are limited to 1900-01-01 through 2100-12-31, while FNOL loss dates also remain subject to BR-C-01's no-future rule, because explicit bounds prevent unsupported years from reaching date parsing or database operations.

