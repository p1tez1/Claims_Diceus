# Reserve management (Prompt 11)

## Implemented behavior

Reserve transactions can be submitted, approved, rejected, retracted, resubmitted, and retried after failed GL posting. A manager can enable or disable the claim's aggregate reserve override. FNOL initial reserves use the same submission service inside the claim creation transaction. The existing Prompt 8 unsupported-initial-reserve validator/TODO has been removed.

| Route beneath `/api/claims/{id}` | Request | Success |
| --- | --- | --- |
| `POST /reserves` | `{component, amount, changeReason, transactionType, resubmissionOfId?}` | 201: `{transaction, approvalStatus, warnings}`; Location points to reserve history. |
| `PUT /reserves/{componentId}` | `{amount, changeReason, resubmissionOfId?}` | 200: same result; appends an Adjust delta. |
| `GET /reserves` | None | 200: component summaries with current balance, pending amount, RowVersion, and full history. |
| `POST /reserves/{txnId}/approve` | None | 204. |
| `POST /reserves/{txnId}/reject` | `{rejectionReason}` | 204. |
| `POST /reserves/{txnId}/retract` | None | 204. |
| `POST /reserves/{txnId}/retry-posting` | None | 204. |
| `POST /reserve-override` | `{enabled: true}` (defaults true) | 204. |

`changeReason` and `rejectionReason` are required and limited to 500 characters. Initial reserve input remains `{component, amount, changeReason}` and represents Add. All three claims roles may submit any amount. Submission requires a linked policy and a claim that is neither Closed nor Withdrawn. Unknown/no policy with an initial reserve returns 422.

DD-04 signed-delta and decimal(19,4) validation comes from the domain's `ValidateSubmissionAmount`: Add is positive except negative SubrogationRecoverable; Adjust is signed and non-zero; Reverse accepts a positive magnitude and stores a negative delta. Approval checks the absolute delta, rechecks the non-subrogation balance invariant, and recomputes PreviousBalance, NewBalance, and CurrentAmount. Posting status does not affect the ledger balance.

Pending financial fields are immutable. PUT adds another transaction without changing any previous amount. A rejected row remains in history; resubmission must reference a rejected row on the same component and use Add/Adjust. Retraction requires the submitter and PendingApproval, and sets both approval and posting states to Cancelled. Only Failed GL posting can be retried, using the original key. Any authenticated claims role may retry within its tenant; no additional retry-specific role restriction is specified.

GET history and claim detail share one Persistence projection. Each history row includes its component, submitter and approver IDs/names, date, type, signed amount, change reason/sequence/key, approval/posting statuses, approval balances/timestamps, rejection reason, and ResubmissionOfId. Auto-approval has no human approver, so ApprovedByName is null. DD-36 presentation order is ChangeSequence; balances reflect approval order and can be non-monotonic in that presentation order. Pending rows have null approval balances.

When net approved balances plus a submitted delta exceed $10M without ManagerOverride, the result contains the BR-R-05 warning and the transaction stays pending even below the automatic threshold. Approval returns 422 until a manager sets the override. The individual $10M approval ceiling remains in force. The warning also enters the existing request warning collector; its resolution is the explicit override, rather than a claim warning acknowledgement.

## Architecture and transaction boundaries

Handlers load tenant-filtered claims/components and orchestrate existing domain methods. Thresholds, signs, cap checks, self-approval, workflow states, and approved balance invariants stay in Domain. The shared Application `ReserveSubmission` service accepts the owning Claim and component set; both subsequent reserve commands and CreateClaimCommand call it directly, avoiding nested command transactions.

Existing reserve domain events produce RESERVE_CREATED, RESERVE_AUTO_APPROVED, RESERVE_APPROVED, RESERVE_REJECTED, RESERVE_RETRACTED, and CLAIM_RESERVE_OVERRIDE_SET through the existing audit notification handler. Rejection reason remains in OldValue. Successful mutations call Claim.Touch(), advancing activity and clearing SLA breach state. Failed actions make no committed mutation or audit.

Existing ReserveAutoApproved/ReserveApproved notifications register the unchanged IGlPostingScheduler's post-commit action. Pending/rejected/retracted transactions never enqueue posting. Retry reuses the existing ReservePostingRetry service. Prompt 10 job classes, scheduling, terminal-failure processing, and recovery behavior are unchanged.

DD-35 uses the existing component RowVer and owning claim RowVer activity update. UnitOfWork reloads and re-evaluates once after a conflict; a second conflict returns 409. If a conflicting review already moved the same transaction out of PendingApproval, the re-evaluation returns 409. A later independent review of a nonpending transaction returns 422. Different transactions can both approve after reload, with balances computed in commit/approval order. The claim concurrency token also prevents two different components from independently exceeding the aggregate cap.

Simultaneous first submissions can collide on `IX_ClaimReserveComponents_ClaimId_Component` before SQL executes the claim RowVer update. Persistence recognizes only an Added component's SQL 2601/2627 collision on that specific index and translates it to the same ConcurrencyException/retry path. Other uniqueness errors retain their existing behavior. The retry loads the winner's component and appends the next sequence; no new locking or schema mechanism was introduced.

Direct project references and package versions are unchanged. No schema or migration changes were needed. Existing POST idempotency middleware is reused. Per DD-32, PUT ignores Idempotency-Key; the DD-03 compatibility adapter appends a new Adjust for each successful request, so clients must avoid repeating it unintentionally. JSON/status replay follows the existing middleware behavior; replay does not restore Location headers.

## Files changed in this session

The checkout already contained work from earlier prompts; that work was preserved. This list describes only Prompt 11 edits, including edits to files already untracked at session start.

| Area | Files |
| --- | --- |
| API | `backend/src/ClaimsModule.API/Controllers/ReservesController.cs` |
| Application contracts/registration | `backend/src/ClaimsModule.Application/Abstractions/PersistenceContracts.cs`, `DependencyInjection.cs`, `Models/PersistenceDtos.cs` |
| FNOL | `backend/src/ClaimsModule.Application/Claims/ClaimHandlers.cs`, `ClaimValidators.cs` |
| Reserve use cases | `backend/src/ClaimsModule.Application/Reserves/ReserveContracts.cs`, `ReserveHandlers.cs`, `ReserveSubmission.cs`, `ReserveValidators.cs`, `ReservePostingRetry.cs` (comment updated; service behavior unchanged) |
| Domain | `backend/src/ClaimsModule.Domain/Entities/Claim.cs`, `ClaimReserveComponent.cs` |
| Persistence | `backend/src/ClaimsModule.Persistence/ClaimsDbContext.cs`, `DependencyInjection.cs`, `Queries/ReadQueries.cs`, `Services/UnitOfWork.cs` |
| Tests | `backend/tests/ClaimsModule.Application.Tests/Claims/ClaimUseCaseTests.cs`, `Reserves/ReserveUseCaseTests.cs`; `backend/tests/ClaimsModule.Domain.Tests/ReserveSubmissionValidationTests.cs`; `backend/tests/ClaimsModule.IntegrationTests/ReserveSqlApiTests.cs` |
| Documentation | `docs/RESERVES.md` |

## Verification

The new unit/API tests cover the four requested thresholds, role/individual ceilings, supervisor/manager self-approval, all component/type sign and zero combinations, precision/reasons/enums, rejected resubmission, PUT append behavior, pending immutability, retract rules, cap warnings/override/net recovery offset, approval-order balances and rechecked negative balances, cache/ledger equality, unknown policy, initial auto/pending/recovery reserves, claim creation rollback on reserve audit failure, failed-only posting retry, POST replay, Closed/Withdrawn claims, missing/cross-claim IDs, tenant isolation, and soft deletion.

SQL concurrency tests use separate WebApplicationFactory request scopes and real SQL Server rowversions. A gate forces both requests to read the same starting state. They verify: one success/one 409/one post-commit job for the same transaction; two distinct approvals both committing with correct reloaded balances; cross-component cap protection; exactly one retry followed by 409 on a second conflict; and simultaneous first/subsequent submissions retaining distinct history sequences on one component. A recording Hangfire client verifies enqueue visibility only after the approved row and audit are committed. The existing real-Hangfire/job regression tests run in the complete suite.

Commands use the repository's installed .NET SDK and an isolated output directory because the ordinary output may be held by an existing API process:

```powershell
.\.dotnet\dotnet.exe build backend/ClaimsModule.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:OutDir=C:/Test/DICEUS_FullStack/TestResults/Prompt11/bin/
$env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
.\.dotnet\dotnet.exe test backend/ClaimsModule.sln --no-build --no-restore -m:1 -nr:false -p:OutDir=C:/Test/DICEUS_FullStack/TestResults/Prompt11/bin/ --logger trx --results-directory TestResults/Prompt11/final
```

SQL verification runs outside the filesystem sandbox to support the existing encrypted local SQL connection. Fixtures migrate and remove only their own uniquely named test databases. Frontend verification is not applicable: no frontend files were changed.

Final verification on 2026-10-06: solution build passed with zero errors. The final incremental build reported the existing AutoMapper NU1903 warning; a preceding full Persistence compilation also reported the eight existing read-query analyzer warnings. The complete suite passed **459 tests, zero failed, one skipped**: 260 Domain, 117 Application, and 82 Integration passed. All 16 new reserve SQL API tests passed. The skipped Azure Blob test needs an Azurite connection and remains unverified. TRX files are in `TestResults/Prompt11/final`. `git diff --check` passed.

## Tested threshold/authority matrix

Automatic approval applies to any submitting role; manual review rows use a different actor. Amount is the absolute signed delta for authority evaluation.

| Amount | Submission | Handler review | Supervisor review | Manager review |
| --- | --- | --- | --- | --- |
| $10,000 | AutoApproved (API/unit) | Not needed | Not needed | Not needed |
| $10,000.01 | PendingApproval (API/unit) | 403 (API) | Approved (API) | Approved (unit) |
| $100,000 | PendingApproval (API/unit) | 403 (API) | Approved (API/unit) | Approved (unit) |
| $100,000.01 | PendingApproval (API/unit) | 403 (API) | 403 (API/unit) | Approved (API/unit) |
| $10,000,000 | PendingApproval (API/unit) | Not separately exercised | Not separately exercised | Approved (API/unit) |
| $10,000,000.01 | PendingApproval (API/unit) | Not separately exercised | Not separately exercised | 403 even with override (API/unit) |

Supervisor and manager self-approval both return 422 (API/unit). A $1 delta taking the aggregate above $10M is warning + PendingApproval; both eligible reviewers receive 422 until a manager enables override, after which supervisor approval succeeds. Handler and supervisor override requests return 403. Two competing reviews of the same pending transaction produce one 204 and one 409, one approved audit, and one committed GL enqueue. A separate second-conflict test returns 409 after exactly two evaluations with no approval audit or job.

## Manual happy path

The standalone API runs on `http://127.0.0.1:5188` with its real Hangfire SQL storage and worker, using isolated database `ClaimsModule_Prompt11_Verification_20261006`. Existing migrations were applied without a schema change. The manual script logs in as handler, creates a claim with a $50,000 initial Indemnity reserve, verifies PendingApproval/zero balance, logs in as supervisor, approves, waits for the real job's Posted state, and checks the audit through the public API. It verifies one each of CLAIM_CREATED, RESERVE_CREATED, RESERVE_APPROVED, and GL_POSTING_SIMULATED, as well as a $50,000 current balance and zero pending amount. The temporary process is stopped afterward; the verification database is retained for inspection.

The final run on 2026-10-06 created `CLM-2026-0000002` (claim ID `e399660f-3013-42f1-2bd8-08df2338bcc3`): creation returned 201, approval returned 204, and real Hangfire job `4` produced Posted with the expected names, balance, and four audit entries. Evidence is written to `TestResults/Prompt11/manual-result.json`, with host logs and the reproducible `manual-happy-path.ps1` in the same ignored verification directory.

## Assumptions and remaining issues

All DD-32 through DD-40 decisions are applied. No new functional assumptions or unresolved reserve-rule decisions were introduced. Retry is exposed to all authenticated claims roles with existing tenant authorization because no stricter retry role was prescribed. No dependencies were added, and existing package pins/licenses remain unchanged.

The existing AutoMapper 14.0.0 NU1903 vulnerability warning and existing read-query culture/comparison analyzer warnings remain. Package upgrades are outside this task's authorization/policy. The Azurite-dependent Azure Blob integration check requires an external emulator connection; it is unrelated to reserve behavior. EF tooling still warns that CLI 9.0.7 is older than runtime 9.0.20. These limitations are reported explicitly rather than changing package versions.
