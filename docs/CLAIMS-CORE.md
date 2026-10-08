# Claims core (Prompt 8)

Historical checkpoint for Prompt 8. Current behavior supersedes the initial-reserve and active-only party statements below: claim creation uses the shared reserve path (DD-29), and detail includes active and inactive parties. See [README](../README.md), [RESERVES](RESERVES.md) and `Persistence/Queries/ReadQueries.cs` for the current implementation (F24, Prompt 23).

Implemented against D1 §§5, 7.1, 7.5, 8, 10.1 and D2 §§3.3.1, 3.4.1, with the supplied design decisions including DD-32–DD-40. Existing uncommitted work was retained. No dependencies or database schema changes were introduced. Reserve submission and status-transition implementation remain outside this task.

## HTTP behavior

| Method / path | Result |
| --- | --- |
| POST `/api/claims` | 201 with persisted `ClaimDetailDto` and Location; critical validation produces field-keyed 422 errors. |
| POST `/api/claims/validate` | 200 `{criticalIssues, warnings}`; issues have `field`, `code`, `message`. No writes or claim-number allocation. |
| GET `/api/claims/{id}` | Detail, active parties, risks, validation issues, reserve components/history, totals and pending amount, documents, latest 20 audit entries. Missing/inaccessible claim: 404. |
| GET `/api/claims` | `items`, `totalCount`, `page`, `pageSize`, newest first. Supports repeated `status` parameters, loss-date bounds, assignee, cause, policy and partial claim-number/client-name search. |
| GET `/api/claims/{id}/audit` | Paginated audit, newest first. Missing/inaccessible claim: 404. |
| POST `/api/claims/{id}/parties` | 201 with the new party. |
| DELETE `/api/claims/{id}/parties/{partyId}` | 204; sets IsActive=false, retains the record. Repeated removal does nothing. Last active Claimant: 422. Missing claim/party: 404. |
| PUT `/api/claims/{id}/notes` | `{notes}`; 204. |
| PUT `/api/claims/{id}/policy` | `{policyId}`; 204. Resolves policy-unknown/outdated period warnings and evaluates the linked policy window. Unknown policy: field-keyed 422. |
| PUT `/api/claims/{id}/assignee` | `{assignedHandlerId}`; 204. Supervisor/manager only; selected user must exist in the tenant. Handler receives 403. |
| PUT `/api/claims/{id}/liability` | `{determination, notes?}`; 204. Accepted, PartiallyAccepted or Denied, explicitly supplied. Closed/Withdrawn claims reject with 422. |

All endpoints require an authenticated handler, supervisor or manager. The existing exact-role users endpoint is unchanged; callers omit `role` for all three assignee roles (DD-40). GET list/audit default to page 1, size 20, maximum size 100. Pagination and inverted date ranges produce 422; malformed query/body values produce 400 through the existing error contract. Date bounds are inclusive instants; policy windows are inclusive **UTC calendar dates**, including the entire expiration date.

Intake accepts `lossDate`, `lossDescription`, `causeOfLossCode`, `lossLocation?`, `estimatedLossAmount?`, `policyId?`, `severity?`, `parties[]`, `riskObjects[]`, `initialReserve?`. Party fields use `role`, `type`, person/company names, optional email/phone/notes. Risk fields use `assetType`, `assetDescription`, optional damage/reference/primary fields. A null policy is allowed with the exact BR-C-06 warning. An explicitly supplied unrecognized policy ID is an input error. Severity defaults to Standard; estimated loss must fit DECIMAL(19,4), be non-negative and require no rounding.

At this historical checkpoint, `initialReserve` was accepted as an optional object but any non-null value returned 422, `errors.InitialReserve = ["initial reserve not yet supported"]`. The current implementation uses the reserve submission path in the owning transaction (DD-29).

`Idempotency-Key` uses the existing DD-32 middleware on create/add-party POSTs. Dry-run validation bypasses it; PUT/DELETE ignore it. Rollback completion now reloads the independently committed reservation if the UnitOfWork detached tracking, retaining tenant/audit shadow state and allowing 4xx replay. Existing hashing, key scope, expiry and concurrency policy were preserved.

## Flow and architectural choices

The controller sends MediatR requests. FluentValidation rejects critical inputs before the command transaction. Creation loads the policy snapshot, builds domain children, allocates the claim number through the existing generator **inside** the transaction, and adds the Draft aggregate. Creator assignment, claim/warning events and activity come from `Claim.Create`. Commit dispatches the existing audit notification handler and saves all effects atomically. The create command returns the identity; the controller sends a detail query **after** commit, ensuring the 201 body includes persisted audit and warning rows rather than a pre-commit view. One captured UTC timestamp supplies both ReportedDate and LossEvent.ReportDate.

Dry-run invokes the identical creation validator, returning failures by severity rather than throwing or entering a transaction. `ClaimIntakeWarnings` is the shared Domain source for preview and persisted warning codes/messages and policy-date comparison. `Claim.LinkPolicy` uses the same UTC window check. Warnings remain domain-managed Open/Acknowledged/Resolved records, and events produce `VALIDATION_ISSUE_ADDED` audit rows.

Application AutoMapper profiles map policy snapshots and domain child DTOs. Existing Persistence read projections handle the database lists/detail; pagination, filtering, reserve totals and actor-name lookup stay server-side, with no per-item queries. Detail now excludes IsActive=false parties, exposes `warnings`, `totalReserves`, `pendingAmount` alongside existing DTO fields, and bounds recent audit at 20; the audit endpoint provides the full paginated history.

Party, notes, policy, assignee and liability actions call domain methods that Touch the owning claim. Notes/assignment append old/new JSON through IAuditLogService in the command transaction. Party/policy/liability events use the existing pre-commit event-to-audit path. Liability uses the existing database columns, promoted to Domain properties and a Domain enum; this requires no migration and introduces no transition or reserve rule changes. Liability notes are audit evidence and do not replace claim notes. Missing liability determination is rejected instead of defaulting to Accepted.

## Verification on 2026-10-06 (Europe/Kiev)

Using the repository's .NET SDK 9.0.318:

```powershell
$env:DOTNET_ROOT = 'C:/Test/DICEUS_FullStack/.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet build backend/ClaimsModule.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
$env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test backend/ClaimsModule.sln --no-build --no-restore -m:1 -nr:false --logger trx --results-directory TestResults/claims-core-verified
```

Build succeeded, zero errors. The existing AutoMapper 14.0.0 NU1903 warning remains; existing policy-query analyzer warnings also appear when recompiling Persistence. No package upgrades were authorized or made. Final suite: **351 passed, 0 failed, 0 skipped** (258 Domain, 44 Application, 49 Integration). SQL-dependent tests ran against local SQL Server, including the existing migration/model-drift, gap-free counter, transaction, tenant, audit-immutability and reserve-cache tests. New coverage includes critical inputs and field keys, async inactive/unknown cause lookup, party/company/email/asset validation, zero/negative/precision estimated losses, dry-run no writes, expired POL-2023-000099, UTC expiration-day boundary, warning persistence/audit, 20 parallel API creates, list filters/search/pagination/sort, detail shape, soft removal and last-claimant protection, party POST replay, failed-command replay, notes, policy resolution, all-role assignees, liability, recent/paginated audit, auth and cross-tenant access.

Initial sandbox SQL attempts failed before executing tests (TLS support/SSPI context). Approved execution outside the sandbox with Windows authentication allowed the entire SQL suite to run. Test fixtures create/delete only their own uniquely named databases. Final TRX files are in `TestResults/claims-core-verified/`; those outputs are ignored by Git.

The existing migration was applied successfully to isolated database `ClaimsModule_Prompt8_Verification_20261006_b7341842`. The API was started in Development on loopback `http://127.0.0.1:5088`, using Windows SQL authentication and a local verification signing key. Through **Swagger UI**, dev-login returned 200, Bearer authorization was applied, and POST /api/claims returned **201** for synthetic claim **CLM-2026-0000001**, ID `0f615cc3-bad3-4ee9-05ed-08df2330cc31`. The response showed Draft, the creator as assignee, matching report timestamps, a claimant, BR-C-06/NO_RISK_OBJECTS warnings and their audits, empty reserves/documents, and CLAIM_CREATED with correlation ID `b5b1c57a-2422-41d3-9ede-28ae3b4189f3`. Screenshot: `TestResults/claims-core-verified/swagger-201.jpg`. The database and local API were retained for review.

Frontend verification: not applicable, no frontend files changed. Remaining work in this scope: none. Initial reserve wiring is deliberately deferred to Prompt 11; document upload remains Prompt 9. Dependency warnings above remain existing issues.

## Files created or modified by this task

- Application: `Claims/ClaimContracts.cs`, `Claims/ClaimValidators.cs`, `Claims/ClaimHandlers.cs`, `Claims/ClaimMappingProfile.cs`, `Models/PersistenceDtos.cs`, `Behaviors/DomainEventDispatcher.cs`.
- API: `Controllers/ClaimsController.cs`, `Authentication.cs`, `IdempotencyMiddleware.cs`.
- Domain: `Rules/ClaimIntakeWarnings.cs`, `Entities/Claim.cs`, `Enums/ClaimEnums.cs`, `Events/DomainEvents.cs`.
- Persistence: `Configurations/EntityConfigurations.cs`, `Entities/PersistenceEntities.cs`, `Queries/ReadQueries.cs`.
- Tests: `ClaimsModule.Application.Tests/Claims/ClaimUseCaseTests.cs`, `ClaimsModule.Domain.Tests/ClaimLiabilityTests.cs`, `ClaimsModule.IntegrationTests/ClaimsCoreSqlApiTests.cs`.
- Documentation: `docs/CLAIMS-CORE.md`.

Source/test paths above are under `backend/src/` or `backend/tests/`. Other working-tree changes already existed and were not part of this implementation.
