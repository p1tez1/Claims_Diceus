# Backend checkpoint — 2026-10-06

Scope: current working-tree backend, including existing uncommitted work. Both specifications were reread in full. D1 controls functionality, D2 technology/deliverables, and explicit DD-01–DD-40 decisions control their documented exceptions. The DD-32–DD-40 addendum resolves the older open-question list. No frontend exists yet. Severity: Critical = authentication bypass/data loss; High = business integrity or required contract failure; Medium = incomplete delivery/secondary workflow; Low = presentation/documentation. “None” means no gap found, not proof against every possible defect.

Evidence paths below are relative to `backend/`. `D/` = `src/ClaimsModule.Domain/`; `A/` = `src/ClaimsModule.Application/`; `P/` = `src/ClaimsModule.Persistence/`; `I/` = `src/ClaimsModule.Infrastructure/`; `API/` = `src/ClaimsModule.API/`; `DT/`, `AT/`, `IT/` = the Domain, Application, Integration test projects under `tests/`. Class/file and named test evidence is provided rather than unstable line numbers.

Inspection traced controllers → MediatR validation/transaction behaviors → domain operations → tenant-filtered repositories → events/audit → SQL commit → Hangfire scheduling, and document/job paths independently. Inspected the initial migration, designer, snapshot, all 15 configurations and seed data. Existing tests exercise real SQL concurrency and rollback, rather than only mocked behavior.

## Findings before fixes

| ID | Finding | Severity | Planned disposition |
| --- | --- | --- | --- |
| F01 | Public `__SET_...` JWT signing-key placeholders pass the byte-length check and can sign accepted tokens. | Critical | Reject missing, whitespace, short and placeholder signing keys in both bearer validation and token creation; regression tests. |
| F02 | UnderInvestigation→Open and the Reopened→Open leg skip unresolved Critical/claimant checks required by BR-ST-02; the existing test explicitly codifies this gap. | High | Apply Critical/claimant checks to every Open entry; retain Draft-only assignment/warning acknowledgement gating. Test preflight and atomic reopen. |
| F03 | Controller model-binding failures use ASP.NET ProblemDetails rather than DD-20's shared error body. | High | Route model-binding failures through the existing middleware; test actual controller malformed JSON, enum/date and multipart requests. |
| F04 | UoW reload/re-evaluation reuses an exhausted upload stream: TXT/CSV can store an empty retry; binary validation can fail on the retry. Local storage mistakes a copy IOException for a filename collision and may retry repeatedly. | High | Rewind HTTP upload content on each handler invocation; limit collision retry to file creation, remove partial files on copy failure; regression tests. |
| F05 | Parties supplied in FNOL are persisted without PARTY_ADDED events, despite D1 §14.1 requiring an event for a new party. | High | Raise existing party domain events during creation; domain and HTTP/SQL audit assertions. |
| F06 | Several simple commands have no FluentValidation validator (D2 §3.3). | Medium | Trivial ID validators using existing patterns; registration coverage and pipeline tests. |
| F07 | Detail query filters inactive parties, preventing D1 §11.3's active/inactive display. | Medium | Trivial projection correction with SQL/API regression. |
| F08 | AutoMapper 14 has NU1903 / CVE-2026-32933; patched versions 15.1.1/16.1.1 exceed AGENTS/DD-26's <=14 cap. | High, policy conflict | Current mappings are flat; record exposure assessment and permitted depth limits. Do not upgrade or suppress advisory. |
| F09 | Runtime SQL principal permissions are not provisioned; D1 §14.2 requires no UPDATE/DELETE permission on audit rows. EF guards alone do not enforce SQL permissions. | High, deployment | Supply repeatable runtime-role permission script and SQL permission test; deployment must bind its non-owner principal to that role. |

## Requirement matrix

This matrix was rechecked after fixes. Closed findings retain their original severity for traceability; the closure evidence below identifies the regression tests. “Yes” is an implementation finding; external deployment and emulator verification are reported separately.

| Requirement | Implemented | Evidence (file/test) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 BR-C-01: valid, nonfuture loss date | Yes | A/Claims/ClaimValidators.cs; D/Entities/LossEvent.cs; AT/Claims/ClaimUseCaseTests.cs; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | None | None |
| D1 BR-C-02: inclusive policy dates, persisted warning | Yes | D/Rules/ClaimIntakeWarnings.cs; D/Entities/Claim.cs; IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | DD-10 requires acknowledgement before Draft→Open | None |
| D1 BR-C-03: claimant before Open; DD-10 claimant at creation | Yes | Claim.Create; ClaimStateMachine; DT/ClaimTests.DD10ClaimantIsMandatoryAtCreation | Every Open entry checks Critical issues and claimant; Draft-only assignment/warning checks preserved; preflight and atomic reopen regressions pass. | High (closed F02) |
| D1 BR-C-04: unique, atomic, gap-free numbers; no reuse | Yes | P/Services/ClaimNumberGenerator.cs; IT/Persistence/PersistenceTests.FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap; IT/ReserveSqlApiTests.InitialReserveAuditFailureRollsBackClaimNumberHistoryAndJob | Same owning transaction, UPDLOCK/HOLDLOCK counter, no SQL SEQUENCE | None |
| D1 BR-C-05: active tenant cause code | Yes | CreateClaimCommandValidator; ReadQueries.GetCausesAsync; composite loss cause FK; ClaimsCoreSqlApiTests critical-rule test | None | None |
| D1 BR-C-06: unknown policy warning; block financial actions | Yes | Claim.EnsureReserveSubmissionAllowed; InitialReserve validator; IT/ReserveSqlApiTests.InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422 | D2 BR-C-06 instead denotes Draft→Closed; both enforced | None |
| D1 BR-C-07: required description >=20 characters | Yes | CreateClaimCommandValidator; LossEvent.Create; ClaimsCoreSqlApiTests critical-rule test | None | None |
| D1 BR-R-01: valid signs/nonzero and nonnegative ordinary balance | Yes | ClaimReserveComponent.ValidateSubmissionAmount/EnsureBalance; DT/ReserveSubmissionValidationTests; IT/ReserveSqlApiTests.AllComponentSignsZeroPrecisionAndRequestValidationThroughApi | DD-04 permits signed Adjust and stores Reverse negative | None |
| D1 BR-R-02: individual absolute-delta authority, inclusive thresholds | Yes | D/Rules/ReserveAuthorityPolicy.cs; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | 10,000 auto; 100,000 supervisor; 10,000,000 manager ceiling, independent of aggregate override | None |
| D1 BR-R-03: never approve own transaction, 422 | Yes | ClaimReserveComponent.Approve; IT/ReserveSqlApiTests.SupervisorAndManagerCannotApproveOwnSubmission | None | None |
| D1 BR-R-04: retained rejection and linked new revision | Yes | Submit/ResubmissionOfId; IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | DD-27 uses normal authority for revision, not forced pending | None |
| D1 BR-R-05: net signed aggregate cap and manager override | Yes | ClaimReserveComponent.ExceedsCap; Claim.SetManagerOverride; IT/ReserveSqlApiTests.AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains; ConcurrentDifferentComponentsCannotBypassAggregateCap | Exactly $10M permitted; small over-cap change forced pending; claim RowVer protects cross-component cap | None |
| D1 BR-R-06: GL key and re-entrant processing | Yes | ReserveHistory unique key; BackgroundJobStore conditional UPDATE + audit; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | None | None |
| D2 BR-C-01: nonfuture date | Yes | CreateClaimCommandValidator; LossEvent.Create; ClaimsCoreSqlApiTests critical-rule test | Same invariant as D1 BR-C-01 | None |
| D2 BR-C-02: outside-period warning recorded in audit | Yes | ClaimIntakeWarnings; Claim.AddValidationIssue; AuditDomainNotificationHandler; ClaimsCoreSqlApiTests dry-run/create tests | Same invariant as D1 BR-C-02 under DD-10 | None |
| D2 BR-C-03: claimant at creation | Yes | Claim.Create; CreateClaimCommandValidator; DT/ClaimTests.DD10ClaimantIsMandatoryAtCreation | DD-10 resolves D1 Draft tension in favor of rejecting creation | None |
| D2 BR-C-04: organisation-unique atomic formatted number | Yes | ClaimNumberGenerator; counter rollback/parallel tests | DD-16 selects transactional table counter, forbids SQL SEQUENCE | None |
| D2 BR-C-05: active organisation cause code | Yes | GetCausesAsync filter; CreateClaimCommandValidator; inactive cause HTTP test | Same invariant as D1 BR-C-05 | None |
| D2 BR-C-06: Draft cannot close directly | Yes | ClaimStateMachine; ClaimTransitionHttpTests full invalid matrix | D1/DD-14 allows unconditional Open→Closed after closure checks, overrides D2 configured edge | None |
| D2 BR-R-01: positive amount | Yes per DD-04 | ClaimReserveComponent.ValidateSubmissionAmount; ReserveSqlApiTests sign/precision test | DD-04 overrides positive-only with signed Adjust/Reverse and recovery rules | None |
| D2 BR-R-02: <=10,000 automatic + GL | Yes | ReserveAuthorityPolicy; ReserveAutoApproved; GlPostingNotificationHandler; threshold/post-commit tests | Includes exactly 10,000; actual enqueue after commit under DD-17 | None |
| D2 BR-R-03: >10,000 and <=100,000 supervisor approval | Yes | ReserveAuthorityPolicy; ApproveReserveCommandHandler; ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Manager inherits supervisor authority | None |
| D2 BR-R-04: >100,000 manager approval | Yes per DD-07 | ReserveAuthorityPolicy; threshold/cap/individual-ceiling API tests | Individual ceiling stays 10,000,000 even with aggregate override | None |
| D2 BR-R-05: GL idempotency key and re-entrancy | Yes | ReserveHistory.IdempotencyKey; BackgroundJobStore; BackgroundJobSqlTests sequential/parallel posting | Same invariant as D1 BR-R-06; key uses component ID | None |
| D2 BR-R-06: retain rejected transaction and revise | Yes per DD-27 | ClaimReserveComponent.Submit; ResubmissionOfId; ReserveSqlApiTests reject/resubmit test | Normal authority applies to new Add/Adjust; not always forced pending | None |
| D2 BR-R-07: aggregate cap override | Yes | ExceedsCap; SetReserveOverrideCommandHandler; cross-component concurrent cap tests | DD-08 net signed approved balances, manager-only override | None |
| D1 BR-ST-01: exact state matrix, invalid 422 lists valid next statuses | Yes | D/Rules/ClaimStateMachine.cs; A/Exceptions/TransitionBlockedException.cs; IT/ClaimTransitionHttpTests.EveryInvalidStatePairReturns422AndItsValidNextList | None | None |
| D1 BR-ST-02: no unresolved Critical + active claimant for Open | Yes | ClaimStateMachine.Evaluate; GetTransitionPreflightQueryHandler | Every Open entry checks Critical issues and claimant; Draft-only assignment/warning checks preserved; preflight and atomic reopen regressions pass. | High (closed F02) |
| D1 BR-ST-03: closure returns all blockers | Yes | ClaimStateMachine; IT/ClaimTransitionHttpTests.ClosureBlockersRemainIndividuallyKeyed | None | None |
| D1 BR-ST-04: supervisor reopen, reason, immediate Open | Yes | Claim.Reopen; DT/ClaimTests.BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened; IT/BackendE2eScenarioTests.FullLifecycleClosesAndReopensToOpen | Every Open entry checks Critical issues and claimant; Draft-only assignment/warning checks preserved; preflight and atomic reopen regressions pass. | High (closed F02) |
| CC-01: no PendingApproval on closure | Yes | ClaimStateMachine; DT/ClaimStateMachineTests.CC01PendingApprovalBlocksClosure | None | None |
| CC-02: no unresolved Critical on closure | Yes | ClaimStateMachine; DT/ClaimTests.CC02ClaimClosureRemainsBlockedUntilLaterCriticalIssueIsResolved | None | None |
| CC-03: active claimant on closure | Yes | ClaimStateMachine; DT/ClaimStateMachineTests.CC03ClosureRequiresActiveClaimant | None | None |
| CC-04: warn and require justification for any nonzero component | Yes | ClaimStateMachine; DT/ClaimTests.CC04ClosureRequiresJustificationForPositiveOrNegativeBalance | DD-14 extends positive to any nonzero, including recovery | None |
| D1 BR-D-01: Azure path and sanitized filename | Yes | I/Storage/AzureBlobStorageService.cs; A/Storage/DocumentFileRules.cs; IT/DocumentSqlApiTests.UploadSanitizesNameAndListReturnsUploaderUrlAndAudit | Upload replay rewinds content; copy failure propagates without collision retries or partial files; TXT/PDF replay and local failure regressions pass. | High (closed F04) |
| D1 BR-D-02: SAS 1h; no Azure byte proxy | Yes | AzureBlobStorageService.GetDownloadUrlAsync; DocumentHandlers.AddUrls; IT/AzureBlobStorageTests | External emulator check initially skipped; local dev route is DD-22 exception | None |
| D1 BR-D-03: configurable local fallback/interface | Yes | I/DependencyInjection.cs; both storage implementations; AT/Documents/DocumentTests; IT/LocalFileSystemStorageTests | Local URL available only in Development, deliberately anonymous; deployment should select Azure | Medium |
| D1 BR-P-01: claimant invariant, last claimant removal blocked | Yes | Claim.RemoveParty; IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | DD-10 disallows claimant-free creation | None |
| D1 BR-P-02: all five roles, repeats permitted | Yes | D/Enums/ClaimEnums.cs; PartyInputValidator; DT/ClaimTests.BRP02AllRolesAndRepeatedRolesAreSupported | None | None |
| D1 BR-A-01: immutable audit, no SQL UPDATE/DELETE permission | Yes | AuditSaveChangesInterceptor; AuditQueryInterceptor; IT/Persistence/PersistenceTests.AuditIsAppendOnlyPropagatesActorAndCorrelationAndCannotBypassService | infra/sql/runtime-permissions.sql supplies append-only runtime permissions; SQL impersonation test verifies UPDATE/DELETE denial. Must be applied to the deployment runtime principal. | High (closed F09) |
| D1 BR-A-02: all significant actions audit | Yes | AuditDomainNotificationHandler; document/notes/assignee handlers use IAuditLogService; IT/ArchitectureTests.ProductionAuditWritesAreCentralizedInAuditLogService | FNOL raises PARTY_ADDED for each initial party; domain and persisted HTTP audit/correlation regressions pass. | High (closed F05) |
| D1 §4: all seven statuses, Withdrawn terminal, Draft→Closed invalid | Yes | ClaimEnums; ClaimStateMachine; DT/ClaimStateMachineTests full matrix | D1 unconditional Open→Closed takes precedence over D2 configured edge | None |
| D1 §4: approved reserve for PendingPayment | Yes | ClaimStateMachine; DT/ClaimTests.PendingPaymentAcceptsAutoApprovedReserveAndInvestigationRequiresAcceptedLiability | AutoApproved counts under DD-05 | None |
| D1 §4/DD-37: liability for investigation→PendingPayment | Yes | Claim.DetermineLiability; nullable mapped fields; liability command/endpoint; DT/ClaimLiabilityTests | Accepted/PartiallyAccepted only, endpoint disallows terminal claims | None |
| D1 §5.2: atomic FNOL children and optional initial reserve | Yes | CreateClaimCommandHandler → ReserveSubmission → UoW; IT/ReserveSqlApiTests.InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422 | None | None |
| D1 §§5.4,8/DD-10: dry-run Critical errors; persisted warnings | Yes | ValidateClaimQueryHandler; ClaimIntakeWarnings; ClaimsCoreSqlApiTests dry-run test | Warning state Open→Acknowledged/Resolved; no Critical waiver | None |
| D1 §5.5: five exact policy seeds and coverages | Yes | P/Seeding/SeedData.cs; InitialCreate.InsertData; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | None | None |
| D1 §5.6: ten exact active cause-code seeds | Yes | SeedData/InitialCreate; ReferencePolicySqlApiTests.ReferenceAndPolicyEndpointsReturnSeededAndFilteredData | None | None |
| D1 §6.2/DD-02: four canonical components | Yes | ClaimEnums; DT/ReserveComponentTests; ReserveSqlApiTests sign test | D2 Recovery/Litigation naming explicitly overridden | None |
| D1 §6.4/DD-07: pending financial immutability and submitter-only retract | Yes | immutable history identity; Pending/Retract; ReserveSqlApiTests reject/retract test | Changed amount is a new transaction; no modification endpoint for pending row | None |
| D1 §6.6/DD-05/06: approved ledger; immutable financials, mutable workflow | Yes | ReserveHistory; AuditSaveChangesInterceptor; IT/Persistence/PersistenceTests.ReserveFinancialFieldsAndDeletionAreRejected | Posting status never determines balance | None |
| DD-28: one component + persisted cache equals ledger | Yes | unique ClaimId+Component; ClaimReserveComponent.LedgerBalance; ReserveSqlApiTests.ApprovalOrderBalancesAndNegativeBalanceRecheckArePreserved | None | None |
| DD-35/36: reload/re-evaluate once, then 409; approval-order balances | Yes | UnitOfWork.ExecuteAsync; Claim/Component RowVer; ReserveSqlApiTests concurrency tests and approval-order test | Pending balances null; history sequence order can differ from approval order | None |
| D1 §8: field-level validation in pipeline | Yes | A/Behaviors/PipelineBehaviors.cs; Claim/Reserve/Document validators; AT/PipelineBehaviorTests.cs | Controller binding failures now throw to the shared middleware; malformed JSON/date/enum/missing multipart file regressions pass. All production MediatR commands have registered validators and one handler; empty-ID pipeline regressions pass. | Medium (closed F03/F06) |
| D1 §9.1 Claims data | Yes | Claim; ClaimConfiguration; migration; ClaimDetailDto; PersistenceTests timestamp/convention tests | ClaimType omitted by DD-30; optional/default Standard severity; liability/override/SLA additions | None |
| D1 §9.2 LossEvents data | Yes | LossEvent; LossEventConfiguration; LossEventDto; migration | None | None |
| D1 §9.3 ClaimParties data | Yes | ClaimParty; ClaimPartyConfiguration; PartyDto; migration | Detail returns inactive as well as active non-soft-deleted parties; HTTP/SQL regression passes. | Medium (closed F07) |
| D1 §9.4 ClaimRiskObjects data | Yes | ClaimRiskObject; ClaimRiskObjectConfiguration; RiskObjectDto; migration | None | None |
| D1 §9.5 ClaimReserveComponents data | Yes | ClaimReserveComponent; configuration; ReserveComponentDto; migration | Notes exists as shadow field; no notes UI required here | None |
| D1 §9.6 ReserveHistory data | Yes | ReserveHistory; configuration; ReserveHistoryDto; migration | DD-06 allows only workflow updates; extra failure/retract/resubmission fields | None |
| D1 §9.7 ClaimDocuments data | Yes | P/Entities/PersistenceEntities.cs; ClaimDocumentConfiguration; DocumentDto | None | None |
| D1 §9.8 ClaimAuditLog data | Yes | ClaimAuditLog entity/configuration; AuditLogDto; AuditLogService | Actor/correlation/time/related entity/JSON projected | None |
| D1 §9.9 CauseOfLossCodes data | Yes | entity/configuration/seed/migration; GetCausesAsync | Global code uniqueness as prescribed; tenant filters still applied | None |
| D1 §9.10 Policies data | Yes | entity/configuration/seed/migration; PolicyProjection | Effective/expiration are DATE, coverage JSON array | None |
| D2 §3.2/DD-11 status-transition table | Yes | ClaimStatusTransitionRow; HasData from defaults; ClaimTransitionSupport.LoadMachine | Request validation reads table, not controller hardcoding | None |
| DD-19/40 Users table and names | Yes | Users seed/configuration; ReadQueries.ListAsync; UserDto | Omit role for assignee options; exact-role filter otherwise | None |
| DD-16 counter table | Yes | ClaimNumberSequence/configuration/migration; ClaimNumberGenerator | Tenant shadow property participates in unique Org+Year index | None |
| DD-21/32 idempotency table | Yes | IdempotencyRecord/configuration/migration; API/IdempotencyMiddleware.cs | Unique Org+User+Key; method/template checked in middleware | None |
| POST /api/claims (D1 10.1; D2 3.3.1) | Yes | ClaimsController.Create; CreateClaimCommandHandler; ClaimsCoreSqlApiTests.CreateReturnsPersistedDetailWarningsAuditAndIdempotentReplay | 201 persisted detail after commit | None |
| GET /api/claims (D1 10.1; D2 3.3.1) | Yes | ClaimsController.List; ListClaimsQueryHandler; ReadQueries.ListAsync; ClaimsCoreSqlApiTests.ListFiltersSearchPaginationAndSortAreAppliedInSql | All seven specified filters, multi-status, pagination/sort | None |
| GET /api/claims/{id} (D1/D2) | Yes | GetClaimDetailQueryHandler; ReadQueries.GetAsync; BackendE2eScenarioTests.FnolHappyPathPersistsPartiesAndRiskObjectsThroughDetailView | Detail returns inactive as well as active non-soft-deleted parties; HTTP/SQL regression passes. | Medium (closed F07) |
| PUT /api/claims/{id}/status (D1/D2) | Yes | ClaimsController.TransitionStatus; TransitionClaimStatusCommandHandler; ClaimTransitionHttpTests | Every Open entry checks Critical issues and claimant; Draft-only assignment/warning checks preserved; preflight and atomic reopen regressions pass. | High (closed F02) |
| GET /api/claims/{id}/audit (D1/D2) | Yes | GetClaimAuditQueryHandler; ReadQueries audit pagination; ClaimsCoreSqlApiTests | Descending timestamp and ID; names/related identity exposed | None |
| POST /api/claims/{id}/parties (D1) | Yes | AddPartyCommandHandler; Claim.AddParty; ClaimsCoreSqlApiTests party test | None | None |
| DELETE /api/claims/{id}/parties/{partyId} (D1) | Yes | RemovePartyCommandHandler; Claim.RemoveParty; ClaimsCoreSqlApiTests party test | IsActive=false; repeat no-op; last claimant 422 | None |
| POST /api/claims/{id}/documents (D1/D2) | Yes | ClaimsController.UploadDocument; UploadDocumentCommandHandler; DocumentSqlApiTests | Controller binding failures now throw to the shared middleware; malformed JSON/date/enum/missing multipart file regressions pass. Upload replay rewinds content; copy failure propagates without collision retries or partial files; TXT/PDF replay and local failure regressions pass. | High (closed F03/F04) |
| GET /api/claims/{id}/documents (D1/D2) | Yes | ListDocumentsQueryHandler; DocumentSqlApiTests upload/list test | Fresh 1h URL per list/detail read | None |
| POST /api/claims/{id}/reserves (D1/D2) | Yes | ReservesController.Submit; SubmitReserveCommandHandler; ReserveSqlApiTests | 201 includes transaction/status/warnings | None |
| PUT /api/claims/{id}/reserves/{componentId} (D2/DD-03) | Yes | AdjustReserveCommandHandler; ReserveSubmission; ReserveSqlApiTests reject/PUT test | Thin Adjust adapter; header ignored per DD-32; repeats append a delta (documented decision tension) | Medium |
| GET /api/claims/{id}/reserves (D1/D2) | Yes | GetClaimReservesQueryHandler; ReadQueries; ReserveSqlApiTests | Cache/pending amounts and full transaction history | None |
| POST .../reserves/{txnId}/approve (D1/D2/DD-03) | Yes | ApproveReserveCommandHandler; ClaimReserveComponent.Approve; ReserveSqlApiTests threshold/self/concurrency tests | Transaction ID canonical, 204 | None |
| POST .../reserves/{txnId}/reject (D1/D2/DD-03) | Yes | RejectReserveCommandHandler; Reject; ReserveSqlApiTests rejection test | Requires authority and reason | None |
| POST .../reserves/{txnId}/retract (D1) | Yes | RetractReserveCommandHandler; Retract; ReserveSqlApiTests rejection test | Submitter only, PendingApproval only | None |
| GET /api/reference/cause-of-loss-codes (D1/D2) | Yes | ReferenceController; GetCauseOfLossCodesQueryHandler; ReferencePolicySqlApiTests | Active-only, peril category filter, stable sort | None |
| GET /api/reference/claim-statuses (D1/D2) | Yes | GetClaimStatusesQueryHandler; GetStatusesAsync; ReferencePolicySqlApiTests | All statuses including terminal, transitions + roles | None |
| GET /api/policies/search (D1/D2) | Yes | PoliciesController.Search; SearchPoliciesQueryHandler; ReferencePolicySqlApiTests | Number/client search; dates/status/coverage array | None |
| GET /api/policies/{id}/coverage (D2/DD-38) | Yes | GetPolicyCoverageQueryHandler; ReferencePolicySqlApiTests | Same policy-row coverage array; unknown/other tenant 404 | None |
| POST /api/claims/validate (DD-10) | Yes | ValidateClaimQueryHandler; ClaimsCoreSqlApiTests dry-run test | Query, no persistence/idempotency reservation | None |
| PUT /api/claims/{id}/policy (DD-09) | Yes | LinkPolicyCommandHandler; Claim.LinkPolicy; ClaimsCoreSqlApiTests.NotesPolicyLiabilityAssigneeAndAuditFollowDecisionsAndTouch | Re-evaluates period; resolves old policy issues; POLICY_LINKED | None |
| PUT /api/claims/{id}/assignee (DD-11) | Yes | AssignHandlerCommandHandler; SupervisorOrAbove; ClaimsCoreSqlApiTests notes/policy test | Creator default; tenant user existence check | None |
| PUT /api/claims/{id}/liability (DD-37) | Yes | DetermineLiabilityCommandHandler; Claim.DetermineLiability; BackendE2eScenarioTests lifecycle | Nullable state/time, JSON audit notes | None |
| PUT /api/claims/{id}/notes (DD-38) | Yes | UpdateClaimNotesCommandHandler; ClaimsCoreSqlApiTests notes/policy test | Editable; touch + NOTES_UPDATED via service | None |
| POST /api/claims/{id}/reserve-override (DD-08) | Yes | SetReserveOverrideCommandHandler; ManagerOnly; ReserveSqlApiTests cap test | Audit + touch; aggregate only | None |
| POST .../reserves/{txnId}/retry-posting (DD-17) | Yes | RetryGlPostingCommandHandler; ReservePostingRetry; ReserveSqlApiTests.RetryOnlyFailedUsesOriginalKeyAndTouchesClaim | Failed→Pending; original key; post-commit action | None |
| GET .../transitions/{targetStatus}/preflight (DD-12) | Yes | GetTransitionPreflightQueryHandler; AT/Claims/ClaimTransitionUseCaseTests | Every Open entry checks Critical issues and claimant; Draft-only assignment/warning checks preserved; preflight and atomic reopen regressions pass. | High (closed F02) |
| POST /api/auth/dev-login (DD-19) | Yes | Program.cs; Authentication.CreateToken; CrossCuttingApiTests.DevLoginCanBeDisabled/RolesAndCorrelationIdFlowFromToken | Signing-key placeholders, whitespace and missing/short secrets rejected in bearer configuration and token issuance; regression tests pass. | Critical (closed F01) |
| GET /api/reference/users?role= (DD-19/40) | Yes | GetUsersQueryHandler; ReadQueries.ListAsync; ReferencePolicySqlApiTests | Exact role or all users, display names | None |
| Local download route (DD-22) | Yes | LocalStorageController; LocalFileSystemStorageService.ResolvePath | Development only; anonymous URL; path protection; no tenant authorization | Medium |
| D1 10.4/DD-20: {type,title,status,errors}, statuses 400/401/403/404/409/422 | Yes | ApiExceptionMiddleware; CrossCuttingApiTests.ErrorsUseSharedContract | Controller binding failures now throw to the shared middleware; malformed JSON/date/enum/missing multipart file regressions pass. | High (closed F03) |
| DD-20 correlation accepted/generated/returned/audited | Yes | CorrelationIdMiddleware; HttpCurrentUser; AuditLogService; CrossCuttingSqlApiTests.AuditReceivesCorrelationAndIdempotencyReplaysOrRejectsMismatch | GUID accepted; invalid header gets generated GUID | None |
| D1 §3/DD-19: JWT symmetric key + sub/name/role/org_id | Yes | API/Authentication.cs; HttpCurrentUser; CrossCuttingApiTests | Signing-key placeholders, whitespace and missing/short secrets rejected in bearer configuration and token issuance; regression tests pass. | Critical (closed F01) |
| D1 §3/DD-19: handler<supervisor<manager policies and server role checks | Yes | Authentication.AddClaimsAuthentication; controller attributes + domain reviewer/override checks; ReserveSqlApiTests threshold tests | None | None |
| DD-32 POST replay/hash/scope/TTL/concurrency | Yes | IdempotencyMiddleware; ClaimsCoreSqlApiTests auth/replay; ReserveSqlApiTests terminal/replay; CrossCuttingSqlApiTests | Sorted compact JSON SHA-256; 24h overwrite; InProgress 409+Retry-After; store 2xx/4xx; release 5xx | None |
| DD-32 exclusions and key semantics | Yes, with decision tension | IdempotencyMiddleware; controller XML comments; DESIGN-DECISIONS-addendum.md | Multipart/dry-run excluded; PUT/DELETE ignored. Template does not distinguish resource IDs; repeating PUT Adjust is not naturally idempotent. Do not silently alter decided semantics | Medium |
| D1 §11.1 list causeOfLossCode for UI | Yes | ClaimListItemDto; ReadQueries list projection; ClaimsCoreSqlApiTests list test | None | None |
| D1 §11.3 detail: preflight data | Yes | Separate preflight endpoint and TransitionPreflightDto; status reference endpoint; ClaimDetailDto issues/reserves | Not embedded in detail; UI calls preflight for selected target | None |
| D1 §11.3 detail: submitter/approver names and GL status | Yes | ReserveHistoryDto; ReadQueries reserve projection; ReserveSqlApiTests | SubmittedByName, ApprovedByName, PostingStatus, PostingJobId exposed | None |
| D1 §11.3 detail: assignee/cause/overview/notes | Yes | ClaimDetailDto; LossEventDto; users/cause reference endpoints | Assignee name resolved with reference/users, not embedded | None |
| D1 §11.3 detail: active/inactive party state | Yes | PartyDto.IsActive | Detail returns inactive as well as active non-soft-deleted parties; HTTP/SQL regression passes. | Medium (closed F07) |
| D1 §11.3 detail: document uploader/size/download, audit actor/link | Yes | DocumentDto/AuditLogDto; ReadQueries; DocumentHandlers | None | None |
| D1 12.1/DD-17 GL job input/key/Posted no-op | Yes | PostGLReserveChangeJob; BackgroundJobStore.ChangePostingAsync; BackgroundJobSqlTests sequential/parallel test | Exact identity and stored-tenant matching | None |
| D1 12.1 GL journal/posting status/job ID | Yes | BackgroundJobStore; journal JSON debit/credit/amount/key; BackgroundJobSqlTests | Signed delta preserved, atomic posting + audit | None |
| D1 12.1 terminal retry exhaustion→Failed/audit once | Yes | GlPostingFailureFilter; BackgroundJobSqlTests.StatePipelineOnlyRecordsFailureAfterDefaultRetriesAreExhausted | Default AutomaticRetry elects Scheduled before applied Failed filter | None |
| DD-17 enqueue AFTER owning commit | Yes | GlPostingScheduler → UoW.EnqueuePostCommitAction; BackgroundJobSqlTests.ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt | Scheduler calls during events only register action; no actual early job creation | None |
| DD-33 recovery cron/candidates/same key | Yes | GlPostingRecoveryJob; FindUnscheduledAsync; BackgroundJobSqlTests.RecoveryFindsOnlyOldApprovedPendingUnscheduledRowsIncludingFailedRetryEnqueue | */5; approved Pending null job ID, approval older than 2 minutes | None |
| D1 12.2/DD-18 SLA cron/staleness/tenant/deletion | Yes | SlaMonitoringJob; FindSlaCandidatesAsync; BackgroundJobSqlTests.SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus | */15; Draft/Open; COALESCE age >48h; IgnoreQueryFilters with explicit deletion exclusion | None |
| D1 12.2 SLA dedupe 24h; never change Status/UpdatedAt | Yes | FlagSlaAsync under lock; ExecuteUpdate SlaBreachedAt; same SQL test; PersistenceTests.SlaBulkUpdateAndAuditInsertDoNotBumpClaimActivity | Exactly 24h permits new entry; exactly 48h not stale | None |
| DD-18/34 all business actions update activity/clear SLA | Yes | Claim.Touch; AuditSaveChangesInterceptor; document/party/reserve/GL paths; PersistenceTests.TouchPersistsChildOnlyActivityAndLiabilityColumnsRoundTrip | Duplicate GL/no-op removal does not touch; SLA audit insert never touches | None |
| D1 §12 job registration; DD-31 SQL HangFire/dashboard | Yes | API/BackgroundJobs.cs; BackgroundJobSqlTests.RealApiStartupInstallsStorageStartsServerAndRegistersRecurringJobs | Installer exception explicit; dashboard never mapped; Testing disables workers | None |
| D1 §13/DD-22: configurable provider, 50MB/MIME/filename collisions | Yes | StorageOptions; DocumentFileRules; UploadDocumentCommandValidator; Local/Azure services; DocumentSqlApiTests | Upload replay rewinds content; copy failure propagates without collision retries or partial files; TXT/PDF replay and local failure regressions pass. | High (closed F04) |
| D2 6.1 IStorageService external implementation behind Application | Yes | A/Abstractions/PersistenceContracts.cs; I/Storage implementations | No Azure SDK in Application; service unit doubles | None |
| Audit CLAIM_CREATED | Yes | Claim.Create → AuditDomainNotificationHandler; ClaimsCoreSqlApiTests | None | None |
| Audit STATUS_CHANGED | Yes | Claim.ChangeStatus → AuditDomainNotificationHandler; lifecycle tests | Old/new status and reason JSON | None |
| Audit PARTY_ADDED | Yes | Claim.AddParty → AuditDomainNotificationHandler | FNOL raises PARTY_ADDED for each initial party; domain and persisted HTTP audit/correlation regressions pass. | High (closed F05) |
| Audit PARTY_REMOVED | Yes | Claim.RemoveParty → AuditDomainNotificationHandler; party API tests | IsActive false JSON, related party | None |
| Audit RESERVE_CREATED | Yes | ClaimReserveComponent.Submit → AuditDomainNotificationHandler; reserve tests | Delta/IDs JSON | None |
| Audit RESERVE_AUTO_APPROVED | Yes | Submit → AuditDomainNotificationHandler; auto reserve E2E test | None | None |
| Audit RESERVE_APPROVED | Yes | Approve → AuditDomainNotificationHandler; reserve approval tests | None | None |
| Audit RESERVE_REJECTED | Yes | Reject → AuditDomainNotificationHandler; reserve reject test | OldValue includes rejection reason as D1 requires | None |
| Audit RESERVE_RETRACTED | Yes | Retract → AuditDomainNotificationHandler; reserve retract test | None | None |
| Audit GL_POSTING_SIMULATED | Yes | BackgroundJobStore via AuditLogService; BackgroundJobSqlTests | Only after conditional update; same transaction | None |
| Audit GL_POSTING_FAILED | Yes | BackgroundJobStore/GlPostingFailureFilter; terminal failure tests | Failure reason in NewValue; once per failed state | None |
| Audit DOCUMENT_UPLOADED | Yes | UploadDocumentCommandHandler via IAuditLogService; DocumentSqlApiTests | Document related ID/type | None |
| Audit CLAIM_CLOSED | Yes | Claim.TransitionTo → AuditDomainNotificationHandler; lifecycle tests | Closure reason + justification | None |
| Audit CLAIM_REOPENED | Yes | Claim.Reopen → AuditDomainNotificationHandler; reopen tests | Two STATUS_CHANGED + CLAIM_REOPENED in same transaction | None |
| Audit SLA_BREACH_DETECTED | Yes | BackgroundJobStore.FlagSlaAsync via AuditLogService; SLA job tests | System actor, correlation, 24h dedupe | None |
| Audit VALIDATION_ISSUE_ADDED | Yes | Claim.AddValidationIssue → AuditDomainNotificationHandler; DomainEventAuditTests; FNOL warning tests | None | None |
| Audit CLAIM_RESERVE_OVERRIDE_SET (DD-08) | Yes | Claim.SetManagerOverride → AuditDomainNotificationHandler; cap test | None | None |
| Audit POLICY_LINKED (DD-09) | Yes | Claim.LinkPolicy → AuditDomainNotificationHandler; notes/policy API test | None | None |
| Audit LIABILITY_DETERMINED (DD-37) | Yes | Claim.DetermineLiability → AuditDomainNotificationHandler; ClaimLiabilityTests; E2E lifecycle | None | None |
| Audit NOTES_UPDATED and HANDLER_ASSIGNED | Yes | ClaimHandlers via IAuditLogService; notes/assignee API test | Additional significant-action coverage | None | None |
| D1 14.2 audit writes only IAuditLogService | Yes | IT/ArchitectureTests.ProductionAuditWritesAreCentralizedInAuditLogService; authorized-entry SaveChanges guard | No handler constructs/writes persistence audit entity; background store uses service | None |
| D1 14.2 audit UTC timestamps and request correlation | Yes | AuditLogService; AuditSaveChangesInterceptor; correlation API/SQL tests | Background jobs create explicit system context/correlation | None |
| D1 15.1 GUID keys sequential/default NEWSEQUENTIALID | Yes | Mapping.Key; SequentialIdentifierGenerator; PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Domain warnings use version-7 GUID; explicit assigned IDs retain SQL default | None |
| D1 15.1 monetary DECIMAL(19,4), reject rounding | Yes | Mapping.StandardColumns; RuleGuard.Money; validators; persistence roundtrip/reserve precision tests | No money/float financial columns | None |
| D1 15.1 DATETIMEOFFSET(7), UTC | Yes | Mapping.StandardColumns; AuditSaveChangesInterceptor; persistence roundtrip tests | Policy effective/expiration use DATE as §9.10 requires | None |
| D1 15.1 short codes NVARCHAR(50) | Yes | enum conversion/length + field configs; initial migration | Explicit §9 field-length exceptions preserved | None |
| D1 15.1 names NVARCHAR(255) | Yes | client/company/user/file/cause names configs | Person names §9.3 100 | None |
| D1 15.1 free text NVARCHAR(MAX) | Yes | EF default string mapping; descriptions/JSON/notes | §9's bounded location/reason/document notes preserved | None |
| D1 15.1 flags BIT default 0 | Yes | Mapping.StandardColumns; model conventions test; migration | HasData/domain methods assign active flags true | None |
| D1 15.1 soft delete + DeletedAt | Yes per DD-39 | Combined filters on six removable entities; model convention test | Explicit DD-39 exception for immutable/reference/system/Hangfire tables and components | None |
| D1 15.1 standard audit columns on every app table | Yes | Mapping.StandardColumns; model convention test; migration | Shadow state; timestamp bridge gives single CreatedAt column | None |
| D1 15.1 tenant OrganisationId on every business table | Yes | ClaimsDbContext filters for all 15 types; composite FKs; PersistenceTests.TenantForeignKeysRejectCrossTenantRelationships | No missing entity filter; DD-39 domain carries no tenant property | None |
| D1 15.1 rowversion Claims and reserve components | Yes | Mapping.Aggregate; concurrency integration tests | Pending/workflow-only changes advance component and claim concurrency | None |
| D1 15.2 Fluent API classes, combined filters, precision, IsRowVersion | Yes | P/Configurations/EntityConfigurations.cs; ClaimsDbContext; AllFifteenMappingsFollowD1AndDD39 | Tenant-only filter for DD-39 nonremovable types | None |
| DD-24 enums stored as strings | Yes | Mapping.StandardColumns; migration/snapshot | No integer business enum columns | None |
| D1 15.3 C#/DTO/route/database naming | Yes | production file/entity/DTO/route inventory; CheckpointValidationTests | Removed the internal service input's ICommand marker; its existing public name is retained for compatibility | Low (closed) |
| D1 15.4 HasData/migration seeding; no startup seed/schema | Yes | SeedData.Configure; InitialCreate; Program has no app migration/EnsureCreated/seed | Hangfire installer is DD-31 exception | None |
| D1 15.4 clean migration from scratch/no model drift | Yes | InitialCreate/designer/snapshot; PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Requires configured SQL; no manual reference-data setup | None |
| D1 15.4 no fixed GUIDs in application logic | Yes | SeedConstants only production fixed GUID literals; HttpCurrentUser | Dev-login references persisted seed tenant constant, no duplicated literal | None |
| D2 2.3 .NET 9/C#13/EF9/SQL2022/Hangfire/JSON | Yes | Directory.Build.props; Directory.Packages.props; project files; real SQL fixture | No dependencies to add | None |
| D2 2.4 direct project arrows and responsibilities | Yes | all five csproj files; IT/ArchitectureTests.ProductionProjectsOnlyReferenceAllowedProjects; DT/AssemblyReferenceTests | Infrastructure/Persistence reference Application directly; Domain has no external package | None |
| D2 2.4/6.1 CQRS reads vs writes through MediatR | Yes | command/query interfaces, handlers, TransactionBehavior checks; controller Send calls; CheckpointValidationTests | Every production command has one handler; auth/system/storage transport endpoints outside claim CQRS | Low (closed service-input marker) |
| D2 3.3 validators for all commands | Yes | A/DependencyInjection AddValidatorsFromAssembly; command validators | All production MediatR commands have registered validators and one handler; empty-ID pipeline regressions pass. | Medium (closed F06) |
| D2 6.1 pipeline validation, not controllers | Yes | ValidationBehavior and DI ordering; AT/PipelineBehaviorTests | Validation precedes UoW; warnings collected without rejecting | None |
| D2 6.1 UoW coordinated transactions/repositories | Yes | UnitOfWork and TransactionBehavior; PersistenceTests rollback/order tests | Claim number/audit/cache/children share commit | None |
| D2 6.1 AutoMapper profiles in Application | Yes, mitigated | A/Claims/ClaimMappingProfile.cs; AT/CheckpointValidationTests.EveryProductionCommandHasRegisteredHandlerAndValidator | F08 automatic mappings now MaxDepth(32); custom policy converter is flat. Upstream advisory remains under constrained package policy | High, package-policy blocked |
| D2 2.4/6.1 significant domain events precommit | Yes | Domain events, dispatcher notification handlers; UnitOfWork.CommitAsync; PersistenceTests.DomainEventsRunBeforeCommitAndPostCommitActionsSeeCommittedData | FNOL raises PARTY_ADDED for each initial party; domain and persisted HTTP audit/correlation regressions pass. | High (closed F05) |
| D2 6.2 structured logging/error middleware | Yes | Serilog JSON console, LoggingBehavior, Correlation/UserLogContextMiddleware; CrossCuttingApiTests.StructuredLogContextContainsCorrelationIdAndAuthenticatedUserId | Controller binding failures now throw to the shared middleware; malformed JSON/date/enum/missing multipart file regressions pass. | High (closed F03) |
| DD-26 package pins/licenses | Yes, conflict | Directory.Packages.props: MediatR12.5, AutoMapper14, FV12.1.1; xUnit/Shouldly/NSubstitute; no Moq/FluentAssertions | F08 upstream High advisory; patched majors prohibited; do not change policy silently | High, policy conflict |
| D2 4.1 backend solution/migrations | Yes | backend/ClaimsModule.sln; five projects/three test projects; InitialCreate | None | None |
| D2 4.1 SQL seed scripts | Partial | HasData migration contains every seed; docs/PERSISTENCE.md | Standalone generated SQL seed/deployment script not checked in | Low |
| D2 3.8/4.1 CI/CD build/migrate/deploy backend | No | No .github/workflows or provisioning files; infra/sql now contains runtime permissions only | Deployment deliverable; no configured Azure targets/identity in repo. Not a backend functional change | Medium |
| D2 3.8/4.3 public Azure backend/Swagger/test credentials | Partial | Swagger always mapped; seeded handler/supervisor/manager dev-login | No public deployment URL/resource evidence; deployment remains separate | Medium |
| D2 4.2 README setup/config/migration/walkthrough | Partial | No README.md; docs/PERSISTENCE.md/CLAIMS-CORE.md/RESERVES.md/BACKGROUND-JOBS.md contain backend setup | README and complete Azure reproduction absent | Medium |
| D2 4.4 architecture documentation | Partial | existing domain traceability and backend topic docs | No single architecture/Azure document; deployment design absent | Medium |
| D2 4.5/4.6 AI workflow report/history | No | No AI-WORKFLOW.md/export in repository inventory | Delivery documentation; requires truthful human workflow/history inputs | Low |
| Upload compensation after later database rollback | Partial | UploadDocumentCommandHandler uploads before SQL commit; IStorageService.DeleteAsync exists | Uploaded files can remain orphaned after later metadata/audit/concurrency rollback; no compensation or cleanup job | Medium |
| Fixed 50MB ceiling under configuration overrides | Partial | StorageOptions.DefaultMaxFileSizeBytes; I/DependencyInjection.cs; request limit 52MB | Default file limit is 50MB; administrators can set a higher file limit, still bounded by the 52MB HTTP request limit | Medium |
| D2 maintainability: culture/comparison analyzer warnings | Partial | ReadQueries.SearchAsync uses ToLower/Contains; build output when Persistence recompiles | Eight existing policy-search analyzer warnings; SQL behavior covered, untouched to keep scope focused | Low |

## Verification and closure

The solution build passed with zero errors. The final incremental build reports two NU1903 occurrences for the same AutoMapper advisory (restore and compilation); rebuilding Persistence also emits its eight existing policy-search analyzer warnings. No package or project-reference changes were made. The full suite was run with local SQL Server, using encryption, outside the filesystem sandbox. The fixture migrated fresh uniquely named databases and dropped only its own databases; migration/no-model-drift, tenant isolation, RowVer, counters, financial balances and jobs are verified by the suite.

Final result: **511 passed, 0 failed, 1 skipped** (261 Domain, 139 Application, 111 Integration passed). All new checkpoint regressions passed. The single skipped test is `AzureBlobStorageTests.UploadAndGenerateReadOnlySasAgainstAzurite`: no listener on localhost:10000 and no AZURITE_CONNECTION_STRING supplied. Azure upload/SAS behavior therefore remains unverified in this run. SQL integration tests were not skipped. Build log: [build.log](../../TestResults/Prompt14/build.log); full suite: [closed.log](../../TestResults/Prompt14/closed.log), with three TRX files in `TestResults/Prompt14/closed`. `git diff --check` passed (only existing CRLF normalization notices).

Earlier runs and their failure logs remain under `TestResults/Prompt14` for transparency. The initial unconstrained parallel build/test overlap raced copied testhost DLLs; subsequent build and test operations ran sequentially with `-m:1`. SQL in the sandbox fails with: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` Elevated runs retain encryption rather than weakening connection settings.

Commands (PowerShell, repository root):

```powershell
dotnet build backend/ClaimsModule.sln -m:1 -nr:false -p:UseSharedCompilation=false
$env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test backend/ClaimsModule.sln --no-build --no-restore -m:1 --logger trx --results-directory TestResults/Prompt14/closed
git diff --check
```

| Finding | Closure evidence | Disposition |
| --- | --- | --- |
| F01 | IT/CrossCuttingApiTests.BearerAndTokenCreationRejectUnsetSigningKeys (five cases); valid-token role tests | Closed: both validation and issuance reject placeholder secrets. Configure a real secret before running the host. |
| F02 | DT/ClaimStateMachineTests.ReturningToOpenRequiresCriticalIssuesResolvedAndClaimant; DT/ClaimTests.ReopenWithLaterCriticalIssueIsBlockedAtomicallyUntilResolution; AT/Claims/ClaimTransitionUseCaseTests.CriticalIssueBlocksReturningToOpenInPreflightAndCommand | Closed: preflight and mutation agree; failed reopen preserves state/time/events. |
| F03 | IT/CrossCuttingApiTests.ControllerBindingErrorsUseSharedContract (three cases), MissingMultipartFileUsesSharedBadRequestContract, TransportErrorsUseSharedContract (405/415) | Closed: model-binding and transport errors use shared middleware; automatic ProblemDetails mapping suppressed. |
| F04 | AT/Documents/DocumentTests.TransactionReplayUploadsTheCompleteContentAgain (TXT/PDF), NullContentIsAValidationFailureInsteadOfNullReferenceException; IT/LocalFileSystemStorageTests.CopyFailureIsNotRetriedAsCollisionAndLeavesNoPartialFile | Closed: replay retains full bytes; copy errors propagate once and remove their partial file. Later SQL rollback compensation remains Medium. |
| F05 | DT/ClaimTests.CreateSetsDraftDefaultsAndEmitsClaimCreated; IT/ClaimsCoreSqlApiTests.CreateReturnsPersistedDetailWarningsAuditAndIdempotentReplay; IT/BackendE2eScenarioTests.FnolHappyPathPersistsPartiesAndRiskObjectsThroughDetailView | Closed: all initial party identities audited once, including actor/correlation. Audit pagination expectations updated for added event. |
| F06 | AT/CheckpointValidationTests.EveryProductionCommandHasRegisteredHandlerAndValidator/EmptyIdsInSimpleCommandsAreRejectedBeforeTheHandlerRuns | Closed: six simple command validators added; internal retry-service input no longer masquerades as an unhandled MediatR command. |
| F07 | IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | Closed: both active/inactive parties returned; last-active-claimant invariant unchanged. |
| F08 | ClaimMappingProfile + mapper-depth assertion in CheckpointValidationTests; package advisory | Mitigated for current automatic maps, upstream dependency still blocked by <=14 policy. No recursive map or arbitrary graph mapping exposed; custom policy converter uses flat scalar/DateOnly data. |
| F09 | IT/RuntimePermissionSqlTests.RuntimeRoleAllowsAuditAppendAndBlocksSqlUpdateAndDelete | Closed in repository: repeatable role script, effective permissions checked and SQL UPDATE/DELETE each denied with error 229. Deployed runtime identity still unverified. |

AutoMapper conflict evidence: the [maintainer advisory](https://github.com/advisories/GHSA-rvv3-g6hj-g44x) lists affected versions below 15.1.1, and patched releases 15.1.1 and 16.1.1. The recursive-graph vulnerability motivates explicit depth limits on the three automatic maps. This mitigation does **not** make the pinned package a patched version or remove NU1903. A package-policy exception or permitted upstream backport is needed to eliminate that dependency finding.

Runtime deployment: apply [runtime-permissions.sql](../../infra/sql/runtime-permissions.sql) with a deployment administrator after EF migrations; create a non-owner runtime database user first and pass its name using sqlcmd `-v RuntimeUser="claims_api"`. Install Hangfire with an installer-capable deployment identity before restricting runtime access, then rerun the script to grant the existing HangFire schema DML/EXECUTE permissions. Configure the API and worker to use that runtime identity; never run them as dbo/sysadmin/db_owner, which bypass permission protection. The script does not invent a login, secret or Azure identity. Live deployment application of the script is not claimed.

### Files changed by this checkpoint

Existing uncommitted files from earlier prompts were retained. This list identifies this task's edits, independently of the repository-wide dirty status.

- `backend/src/ClaimsModule.API/Authentication.cs`, `ApiMiddleware.cs`, `Program.cs`.
- `backend/src/ClaimsModule.Application/Claims/ClaimValidators.cs`, `ClaimMappingProfile.cs`.
- `backend/src/ClaimsModule.Application/Documents/DocumentHandlers.cs`, `DocumentValidators.cs`.
- `backend/src/ClaimsModule.Application/Reserves/ReserveValidators.cs`, `ReservePostingRetry.cs`.
- `backend/src/ClaimsModule.Domain/Entities/Claim.cs`, `Rules/ClaimStateMachine.cs`.
- `backend/src/ClaimsModule.Infrastructure/Storage/LocalFileSystemStorageService.cs`.
- `backend/src/ClaimsModule.Persistence/Queries/ReadQueries.cs`.
- `backend/tests/ClaimsModule.Domain.Tests/ClaimTests.cs`, `ClaimStateMachineTests.cs`.
- `backend/tests/ClaimsModule.Application.Tests/CheckpointValidationTests.cs` (new), `Documents/DocumentTests.cs`, `Claims/ClaimTransitionUseCaseTests.cs`.
- `backend/tests/ClaimsModule.IntegrationTests/RuntimePermissionSqlTests.cs` (new), `CrossCuttingApiTests.cs`, `LocalFileSystemStorageTests.cs`, `ClaimsCoreSqlApiTests.cs`, `BackendE2eScenarioTests.cs`, `HealthEndpointTests.cs`, `BackgroundJobSqlTests.cs`.
- `infra/sql/runtime-permissions.sql` (new); `docs/review/backend-checkpoint.md` (new).

Architecture/assumptions: existing Clean Architecture arrows, CQRS, UoW, audit service and domain-event paths preserved. No new dependency, business-table schema, migration, API route or decided business policy introduced. BR-ST-02's generic Open invariant is enforced while DD-10's Draft-specific warning/assignment gating stays Draft-specific. Inactive parties and initial party audit events extend response/history contents without removing fields. Read models already include causeOfLossCode, submitter/approver names and PostingStatus; selected-target preflight and assignee names remain supplied by their existing reference/preflight endpoints.

## Remaining issues

No unmitigated Critical application-code finding remains. F08 is an explicitly unresolved High package-policy conflict. F09's script is verified, but application of it to a deployed runtime identity is unverified. Azure Blob integration is unverified until an existing Azurite service/connection is available; no emulator dependency was installed under the no-new-dependencies constraint.

Remaining **Medium**:

- Azure backend deployment/public URL and CI/CD workflow/provisioning are absent; no Azure target, subscription or deployment identity was supplied. Swagger and seeded credentials exist locally.
- README and complete architecture/Azure reproduction document are absent; backend instructions currently live in separate topic documents.
- Local filesystem download URLs are anonymous and Development-only; they have traversal protection but no tenant authorization or expiry. Public deployments must select Azure. No new authentication mechanism was invented for these URLs.
- DD-32 template-scoped POST replay does not distinguish resource IDs; DD-03 PUT Adjust appends another delta when repeated, despite DD-32's “naturally idempotent PUT” wording. Both decided mechanisms were preserved; resolving the semantic tension requires an explicit decision.
- Uploaded blobs/files can be orphaned when a later SQL metadata/audit transaction rolls back. Partial copies are now removed; cross-system compensation is absent.
- Administrator file-size configuration can exceed the default 50MB file ceiling (HTTP request cap remains 52MB).

Remaining **Low**:

- No checked-in standalone SQL seed export (HasData migration already supplies all seeds).
- AI workflow report/history export absent; truthful workflow/history evidence is a deliverable.
- Existing SQL policy-search culture/comparison analyzer warnings.

Frontend-only Angular implementation and hosting are outside this backend checkpoint. Optional Docker Compose/Key Vault/email/payment/subrogation processing/litigation/fraud/bulk/communications/reinsurance features are not required backend gaps in this bounded slice.
