# Final comprehensive review

## Prompt 23 fix worklist (2026-10-07)

Historical review evidence below is retained. This worklist records the subsequent verification and disposition; it is ordered Critical → High → Medium → Low. No Critical findings exist. All F01–F26 findings were confirmed against the cited working-tree implementation and related tests; none is a false positive.

| ID | Severity | Status / scope | Resolved in |
| --- | --- | --- | --- |
| F01 | High | Partially resolved: compatible Angular/Material/CLI/TypeScript/zone updates and middleware patch remove both Critical entries and all runtime advisories. Full audit retains 12 High and 2 Moderate build/test entries; the High braces advisory has no published patch. | Prompt 23: frontend package.json/package-lock.json; npm audit before/after evidence below |
| F03 | High | Resolved: protected routes await shared login; expired restored sessions retain the acting role; one shared 401 recovery retries original requests once. | Prompt 23: AuthService, auth interceptor/guard, routes/header; auth service, interceptor and navigation regressions |
| F04 | High | Resolved: fresh manager visits derive the cap blocker from approved balances/pending deltas; approval errors are visible. | Prompt 23: claim-reserves.component.ts; claim-financial-tabs.component.spec.ts |
| F05 | High | STOP: DD-03 appends a delta, DD-32 explicitly ignores PUT keys. Requires an explicit revised financial retry contract. | — |
| F06 | High | STOP: DD-32 requires deleting replay records on exceptions/5xx; durable commit reconciliation requires a revised decision. | — |
| F07 | High | Blocked: no remote, Azure subscription/resource credentials, public URLs or reviewer identities supplied. | — |
| F02 | Medium | STOP: patched AutoMapper majors exceed DD-26/AGENTS <=14 policy. Existing flat MaxDepth(32) mitigation is not an upstream patch. | — |
| F08 | Medium | Deferred: authentic historical prompts, corrections, authorship and human assessment must be supplied by the author. | — |
| F09 | Medium | Resolved: user-selected UTC calendar days expand to full inclusive day bounds; API timestamp semantics unchanged. | Prompt 23: claims-list component/spec |
| F10 | Medium | Resolved: reactive search and selected user identity survive delayed reference data and duplicate names. | Prompt 23: claims-list component/template/spec |
| F11 | Medium | Resolved: query changes cancel older requests; failed results clear and future loads continue. | Prompt 23: claims-list component/template/spec |
| F12 | Medium | Resolved: individual search failures no longer terminate either policy typeahead; editing clears stale selection. | Prompt 23: loss-step and claim-detail components/specs |
| F13 | Medium | Partially resolved: detail pending guards, inline financial/reason errors and terminal reserve controls fixed. STOP on expired-policy badge: overlapping D1 requirements still need a decision. | Prompt 23: detail/header/overview/parties/reserves/transition-dialog/shared party-form; detail and financial-tab regressions |
| F14 | Medium | Resolved: Download refreshes the selected document's URL and exposes failures/retry. | Prompt 23: claim-documents component; financial-tab regressions |
| F15 | Medium | Resolved in code: optional browser-facing origin preserves signed Blob path/query; Compose selects localhost. Live emulator transport remains Unverified. | Prompt 23: StorageOptions, AzureBlobStorageService, docker-compose.yml; BlobDownloadOriginTests |
| F16 | Medium | Resolved in configuration: release build selects production environment. Actual GitHub deployment remains Unverified. | Prompt 23: deploy.yml; tools/deployment-contract.test.mjs |
| F17 | Medium | Resolved: policy/reference/status endpoints describe their actual typed success schemas. | Prompt 23: Claims/Policies/Reference controllers; CrossCuttingApiTests |
| F18 | Medium | Deferred: rollback/retry compensation needs a transaction-wide external-resource ownership lifecycle; broader than the cheap fixes. | — |
| F19 | Medium | Deferred: separate Compose runtime principal also requires pre-installing Hangfire schema and installer/runtime startup coordination; Docker unavailable for verification. | — |
| F20 | Medium | Deferred: local browser downloads need signed-capability issuance/verification or an authenticated download transport; not a cheap path-only check. | — |
| F21 | Low | Resolved: component grouping and ascending ChangeSequence replace timestamp sorting. | Prompt 23: claim-reserves component; financial-tab regression |
| F22 | Low | Resolved: party audit links select Parties and focus retained active/inactive party IDs. | Prompt 23: claim-audit/claim-detail/claim-parties components; financial-tab regression |
| F23 | Low | Deferred: generate/verify a reproducible seed artifact separately from the initial schema migration. | — |
| F24 | Low | Resolved: historical checkpoints explicitly identify superseded party, reserve, dashboard and SQL statements. | Prompt 23: docs/CLAIMS-CORE.md, BACKGROUND-JOBS.md, PERSISTENCE.md; document review |
| F25 | Low | Deferred: formatting/query and measured index tuning are maintenance work outside these focused corrections. | — |
| F26 | Low | Deferred: duplicating stateful checks before the transaction risks status/error changes; shared validator evaluation needs a separate design and regression pass. No enforcement bypass exists. | — |

### Prompt 23 verification and scope

The disposition table above is the current status authority for this follow-up. “Resolved in” identifies this uncommitted working-tree change and its regression coverage; no commit was created. Requirement matrices and findings below retain the original pre-fix evidence and severity, rather than rewriting the historical audit. There are 13 fully resolved findings, two partially resolved findings, and 11 stopped/deferred/blocked findings; no false positives. All cited flows and tests were inspected before editing. Existing user changes were preserved, and pre-fix copies/logs are under ignored `TestResults/GapFix`.

- **Fail-before/pass-after:** frontend regressions exposed 18 failures against the original production code (70-test run, including unhandled original typeahead errors). After corrections and additional authentication navigation/recovery cases, the complete 75-test suite passes. The seven new backend regressions failed before (one SAS origin/signature contract plus six OpenAPI schema cases), then passed after. The F16 environment contract fails with the original build environment scope and passes with the correction. F01's audit check fails on the original runtime graph and reports zero runtime advisories on the updated lock; the full audit intentionally remains failing for the residual advisories. F24 is documentation-only and was checked against current code.
- **Backend build:** `dotnet build backend/ClaimsModule.sln` passed with zero errors. Fourteen NU1900 warnings report unavailable NuGet vulnerability data; this does not establish a clean backend advisory graph. Existing analyzer findings are outside this fix scope.
- **Complete backend suite:** with `CLAIMS_TEST_SQLSERVER=Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`, `dotnet test backend/ClaimsModule.sln --no-build --no-restore -m:1 --logger trx --results-directory TestResults/GapFix/FullBackend` passed: Domain 261, Application 139, Integration 119; **519 passed, zero failed, five Azurite tests skipped**. SQL Server 2022 integration tests ran against a local database. Logs/TRX: `backend-tests.log`, `FullBackend/`.
- **Complete frontend suite/build:** `npm run test:ci -- --progress=true` passed **75/75** in Chrome Headless; `npm run build -- --configuration production` passed. Worker limits (`NG_BUILD_MAX_WORKERS=2`, `NG_BUILD_PARALLEL_TS=0`) were used on this host. Build warnings remain: 741.62 kB initial bundle exceeds the existing 700 kB warning budget, and an existing unused MatButton import is reported by the newer compiler. `npm run test:http-boundary` and `npm run test:deployment` pass.
- **Manual real-database smoke:** migrated a separate `ClaimsModule_Prompt23_Smoke_20261007` database; started the Development API at localhost:5020 with a real Hangfire SQL worker and local document storage; manually exercised HTTP endpoints: create claim → pending reserve 25,000 → different supervisor approves → worker posts GL → multipart PDF upload → byte-verified download → audit. Claim `CLM-2026-0000001` / `e05d6219-5374-4fa9-72b1-08df245698b7` reached Approved/Posted, posting job `1`; audit includes CLAIM_CREATED, RESERVE_CREATED, RESERVE_APPROVED, GL_POSTING_SIMULATED and DOCUMENT_UPLOADED. Evidence: `manual-smoke.ps1`, `manual-smoke.json`, `smoke-migrate.log`. This was an HTTP walkthrough, not a browser or live Azure walkthrough. Database and upload evidence are retained.
- **Dependency evidence:** no new dependencies. Existing Angular packages are pinned to 20.3.33, Material/CDK 20.2.14, CLI/devkit 20.3.39, TypeScript 5.9.3, zone.js 0.15.1; webpack-dev-middleware is overridden to compatible patched 7.4.5. Registry metadata confirmed MIT licenses (TypeScript Apache-2.0) and Angular's [supported Node/TypeScript versions](https://angular.dev/reference/versions). Full npm audit moved from **66 entries (2 Critical, 39 High, 18 Moderate, 7 Low)** to **14 (0 Critical, 12 High, 2 Moderate, 0 Low)**; `npm audit --omit=dev --audit-level=high` reports **zero**. Remaining High entries propagate from the [braces advisory with no published patch](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), including the latest Karma graph; incompatible downgrades were not forced. Full audit evidence: `npm-audit-before.json`, `npm-audit-after.json`, `npm-audit-production-after.json`.

Changed files for this follow-up (paths relative to the repository):

- Backend: `Application/Storage/StorageOptions.cs`, `Infrastructure/Storage/AzureBlobStorageService.cs`, API `Controllers/{Claims,Policies,Reference}Controller.cs`; integration tests `BlobDownloadOriginTests.cs`, `CrossCuttingApiTests.cs` (all under the existing backend source/test project paths).
- Frontend: `claims-ui/package.json`, `package-lock.json`, `tools/deployment-contract.test.mjs`; `src/app/app.component.{ts,html}`, `app.routes.ts`, `core/auth.guard.ts`, `core/auth-navigation.spec.ts`, `core/services/auth.service.{ts,spec.ts}`, `core/interceptors/auth.interceptor.ts`, `interceptors.spec.ts`; `features/claims-list/claims-list.component.{ts,html,spec.ts}`; `features/fnol-intake/loss-step.component.{ts,spec.ts}`; `features/claim-detail/claim-{audit,detail,documents,header,overview,parties,reserves}.component.ts`, `claim-detail.component.spec.ts`, `claim-financial-tabs.component.spec.ts`, `transition-dialog.component.ts`; `shared/party-form/party-form.component.ts`.
- Configuration/documentation: `docker-compose.yml`, `.github/workflows/deploy.yml`, `README.md`, `ARCHITECTURE.md`, this review, `docs/CLAIMS-CORE.md`, `docs/BACKGROUND-JOBS.md`, `docs/PERSISTENCE.md`.

**Decisions/remaining work:** no decision authority or financial retry behavior was changed. UTC calendar-day filtering was explicitly selected by the user. F02/F05/F06 stop at existing package/financial decisions; F13's remaining expired-policy badge stops at an unresolved functional interpretation. F07/F08 require genuine external/human evidence. F18/F19/F20 need broader resource, installer or download-access lifecycle work; F23/F25/F26 are deferred maintenance/design work as explained in the worklist. Docker/Azurite transport, a real deployment and reviewer access remain unverified. Public resource credentials and a remote were not available.

## Historical review (before Prompt 23 fixes)

Review dates: 2026-10-06–2026-10-07 (Europe/Kiev). Scope: the then-current working tree in C:/Test/DICEUS_FullStack, including untracked implementation files, not merely HEAD fb100c35b40cd4a3a41442e138567ae0e1902018. This historical audit changed no application, test, configuration, migration, or workflow code.

The implementation has substantial backend coverage and passing local SQL integration tests. It is not ready to call a fully verified submission: retry safety, frontend workflows, dependency advisories, and delivery evidence remain open.

## Summary

| Finding severity | Unique findings |
| --- | ---: |
| Critical | 0 |
| High | 6 |
| Medium | 14 |
| Low | 6 |
| Total | 26 |

These are reviewer-assigned severities for F01–F26, counted once per finding even when several requirements reference it. They are not counts of failing requirement rows or npm advisories. There is no demonstrated Critical application exploit. npm separately reports two Critical package entries in development/build dependencies; exposure and exploit prerequisites are explained in F01.

Backend: 512 passed, 0 failed, 5 skipped after the successful environment-adjusted run. Frontend: 53 passed, 0 failed. Backend and production frontend builds passed. The five skipped tests are Azure Blob/Azurite tests. Live Azure deployment, App Service upload limits, and external reviewer access remain Unverified.

## Basis, precedence, and evidence notation

Read AGENTS.md, both complete specifications, DESIGN-DECISIONS.md including its incorporated DD-32–DD-40 addendum, and the historical backend checkpoint. D1 governs functionality, D2 governs technology/deliverables, and explicit DD decisions override both. The old questions above the incorporated addendum are not independent decisions. In particular, signed adjustments, intake rejection of Critical issues, POST-only replay scope, SLA timestamp semantics, and the six-entity soft-delete set are intentional decisions, not automatically specification defects.

Inspection covered project references/packages; controllers and host middleware; commands, queries, validators, domain rules and event handlers; EF mappings, migrations/snapshot, seeds, repositories, transaction/counter implementation, and jobs; storage implementations; all frontend features/services/interceptors; tests/fixtures; workflows, containers, infrastructure, and documentation. The implementation exists end to end; this is not a scaffold-only repository. Execution supplements static inspection; a source reference does not claim a live Azure or browser walkthrough occurred.

Evidence prefixes below expand to repository-relative paths:
- D = backend/src/ClaimsModule.Domain
- A = backend/src/ClaimsModule.Application
- P = backend/src/ClaimsModule.Persistence
- I = backend/src/ClaimsModule.Infrastructure
- API = backend/src/ClaimsModule.API
- DT = backend/tests/ClaimsModule.Domain.Tests
- AT = backend/tests/ClaimsModule.Application.Tests
- IT = backend/tests/ClaimsModule.IntegrationTests
- UI = frontend/claims-ui/src/app
- MIG = P/Migrations/20261005101331_InitialCreate.cs
- CFG = P/Configurations/EntityConfigurations.cs

A reference such as D/Entities/Claim.cs:43 is a file and one-based line; a qualified test name refers to that test class in the indicated test project. Schema rows cite migration column lines and the passing fresh-database/model test. “Yes” means evidence supports the scoped requirement; “Partial” identifies a concrete gap; “No” identifies absent required content; “Unverified” means execution/evidence was unavailable. Severity is the gap severity, with “—” for a satisfied requirement and no finding. Unverified rows have a severity reflecting delivery importance, not proof of a defect.

## Findings and prioritized fix list

No Critical finding was established. Address the following High findings first, then Medium and Low. Changes below are recommendations only; none were implemented in this audit.

### High

**F01 — Vulnerable frontend dependency graph.** The lock pins Angular 18.2.14, piscina 4.6.1, and tar 6.2.1. The completed npm audit reports 66 affected package entries: 2 Critical, 39 High, 18 Moderate, 7 Low. The runtime-only audit reports 10 entries: 4 High, 6 Moderate, no Critical. Counts include transitive effects and are not unique CVEs. Both Critical entries are in build tooling, not demonstrated browser-runtime vulnerabilities. Piscina requires an additional prototype-pollution primitive; tar requires relevant malicious archive input. Review and update the authorized Angular/tooling package graph with a lockfile rebuild and regression checks; do not run a blind force upgrade. Evidence: frontend/claims-ui/package-lock.json entries for those packages; TestResults/FinalAudit/npm-audit.json and npm-audit-production.json. Primary advisories: [Piscina](https://github.com/piscinajs/piscina/security/advisories/GHSA-67c8-pqhq-4rmx), [node-tar](https://github.com/isaacs/node-tar/security/advisories/GHSA-23hp-3jrh-7fpw). The Piscina maintainer labels its advisory High even though this registry audit labels the entry Critical.

**F03 — Initial navigation races authentication; expired sessions do not recover.** AppComponent starts an asynchronous login in ngOnInit while the router outlet is already active. The list constructor immediately requests causes, users, and claims; detail immediately forkJoins three protected requests. If login finishes later, these calls get 401 and are not retried when auth state changes. AuthService also restores a token without checking expiry; initialize skips login whenever any stored token exists. Gate initial protected navigation/data loading on authentication completion and provide an expiry/401 recovery path. Evidence: UI/app.component.ts:24, UI/app.component.html (unconditional router outlet), UI/app.routes.ts:3, UI/core/services/auth.service.ts:35 and :84, UI/core/interceptors/auth.interceptor.ts, UI/features/claims-list/claims-list.component.ts:30, UI/features/claim-detail/claim-detail.component.ts:78. Static race, not a recorded live-browser reproduction; synchronous mocked tests do not cover the ordering.

**F04 — A manager cannot recover the aggregate-limit approval flow on a fresh detail visit.** The override button requires the component-local warning signal, initially null. That signal is populated only by the response to a submission in this component. A different manager opening the claim, reloading, or revisiting the tab has no override control even when a pending reserve requires it; approve returns 422 and does not populate this signal. Derive the action from persisted claim/reserve state and display the server's approval blocker. Evidence: UI/features/claim-detail/claim-reserves.component.ts:29, :69, :86, :95, :101–102. Backend protection itself passes ReserveSqlApiTests.AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains.

**F05 — The PUT adjustment adapter is not naturally idempotent.** Repeating the same PUT amount creates a new Adjust history row and applies the delta again. IdempotencyMiddleware ignores all PUTs, including one carrying the same Idempotency-Key. This contradicts DD-32's rationale that PUT/DELETE are naturally idempotent, although the adapter follows DD-03's instruction to append an adjustment. Example: retry PUT amount 100 after a lost response adds 200 in total. Resolve this DD-03/DD-32 contract conflict explicitly, then implement retry-safe identity or absolute replacement semantics without silently changing financial behavior. Evidence: API/IdempotencyMiddleware.cs:15, API/Controllers/ReservesController.cs:38, A/Reserves/ReserveHandlers.cs:21; docs/DESIGN-DECISIONS-addendum.md:8. Existing PUT tests verify a single call, not same-request replay.

**F06 — POST replay state can be removed after the business transaction commits.** The idempotency reservation, claim/reserve transaction, and completed response are three separate commits. If response capture/persistence or final response writing throws after the handler commit, the outer catch deletes the record. A retry then executes the committed business operation again. A process crash can instead strand InProgress until its 24-hour expiry. In addition, reserve submission generates a fresh key each click after an uncertain failure, unlike FNOL's retained key. Preserve/reconcile durable operation identity across post-commit failures and reuse the key for an unchanged UI submission. Evidence: API/IdempotencyMiddleware.cs:78–149, P/Services/UnitOfWork.cs:56, UI/features/claim-detail/claim-reserves.component.ts:94. Happy-path SQL replay tests pass; commit-then-response-failure and process-crash cases are not covered. DD-32's unconditional exception cleanup needs refinement; this is not a claim that successful ordinary replays fail.

**F07 — Mandatory public delivery evidence is absent.** README explicitly states that no public Azure URLs/live-resource evidence are committed; no Git remote is configured in this checkout. Provisioning and deployment YAML exist but do not prove a running backend/Swagger, frontend, deployed demo users, or granted reviewer repository access. Publish/verify the required environment and record actual URLs and access instructions before submission. Evidence: README.md:297; infra/README-deploy.md:89; git remote -v returned no entries. External deployments may exist elsewhere: running resources and repository access are Unverified, not proven absent.

### Medium

**F02 — AutoMapper policy pin has a security advisory with no permitted patched major.** AutoMapper 14.0.0 complies with the <=14 policy and its MIT licensing, but falls in the affected range of CVE-2026-32933. Current mappings are flat and explicitly bounded with MaxDepth(32), so a reachable exploit was not established. The upstream patched versions are beyond the allowed major. Record a policy decision or a reviewed mitigation/exception; do not silently adopt a commercially licensed major. Evidence: Directory.Packages.props:7; A/Claims/ClaimMappingProfile.cs:12–18; AT/CheckpointValidationTests.cs:19; [maintainer advisory](https://github.com/LuckyPennySoftware/AutoMapper/security/advisories/GHSA-rvv3-g6hj-g44x).

**F08 — AI delivery evidence remains placeholders.** AI-WORKFLOW.md provides an honest scaffold but lacks the required three actual prompts, two concrete corrections, authorship account, personal assessment, and conversation export/link. docs/ai-log contains instructions rather than history. Supply authentic evidence; do not reconstruct fictional conversations. Evidence: AI-WORKFLOW.md:3, :44–93; docs/ai-log/README.md:3. Affects D2's 25% AI-assisted delivery dimension.

**F09 — Dashboard date-to excludes most of the selected last day.** Date controls send date-only strings unchanged. The backend compares LossDate <= DateTo at midnight, so a claim at noon on the selected “to” date is omitted. Define the date-zone convention and convert an inclusive UI end date to an exclusive next-day bound or equivalent. Evidence: UI/features/claims-list/claims-list.component.html date inputs, .ts:41; P/Queries/ReadQueries.cs:21. The timestamp API comparison itself is consistent; the date-only UI/API integration is incorrect.

**F10 — Assignee search and URL restoration are unreliable.** filteredUsers is a computed signal whose search term is a FormControl value, not a reactive signal dependency. Once users load, typing does not invalidate the computed result. A URL assignee is patched before asynchronous users arrive and is never repatched; the next filter edit can silently remove assignedHandlerId. Use reactive search state and retain the selected user ID independently of a display name. Evidence: UI/features/claims-list/claims-list.component.ts:28, :31–36, :41–43.

**F11 — Older list responses can overwrite a newer filter result.** Each query-parameter update opens an independent subscription with no cancellation or generation check. A slow old response can overwrite rows/count after a newer response has completed. Failed loads also leave previous rows under new filter controls. Use switchMap or equivalent last-request ownership and explicit load-error state. Evidence: UI/features/claims-list/claims-list.component.ts:32, :40.

**F12 — One policy search error terminates subsequent typeahead searches.** Both typeahead pipelines let inner HTTP errors escape switchMap; the interceptor shows a snackbar then rethrows. The outer subscription terminates, so further typing cannot query again until component recreation. Catch within the inner search and retain an error/retry state. Evidence: UI/features/fnol-intake/loss-step.component.ts:77; UI/features/claim-detail/claim-detail.component.ts:81; UI/core/interceptors/error.interceptor.ts.

**F13 — Pending-action and inline-error requirements are incomplete.** Detail notes/party/assignee/policy/liability/status mutations have no shared pending state guarding their buttons. Repeated clicks can issue duplicate writes; a global progress bar does not disable them. The reserve drawer and transition dialog also use generic messages/disabled controls without Material field errors for every invalid field. Add operation-specific pending guards and field-level errors; reflect terminal claim status in financial controls. Evidence: UI/features/claim-detail/claim-detail.component.ts:84–107; claim-overview.component.ts, claim-parties.component.ts, claim-header.component.ts; claim-reserves.component.ts:27 and :42–50; transition-dialog.component.ts. Reserve submit/review and document-upload busy guards already exist. The in-force badge uses only the loss-date window (loss-step.component.ts:93), not the policy's Expired status; D1's overlapping historical-loss color wording needs an explicit interpretation before changing it.

**F14 — Document download uses an expired cached SAS after one hour.** The click handler opens document.downloadUrl from the earlier list response and never requests a fresh URL, contrary to D1 §11.3. Refreshing the tab manually works but is not the required click behavior. Fetch a fresh URL immediately before opening, with appropriate loading/error handling. Evidence: UI/features/claim-detail/claim-documents.component.ts:47 and :65; A/Documents/DocumentHandlers.cs:49. SAS generation itself is one-hour/read-only in source; emulator tests were skipped here.

**F15 — Compose emits a browser-inaccessible Blob hostname.** The API's BlobEndpoint is http://azurite:10000/claimsdevstore, and GenerateSasUri preserves that endpoint. The host browser opened by the SPA cannot normally resolve the Docker-only name azurite. Provide a browser-reachable download endpoint while preserving the signed URI path/query. Evidence: docker-compose.yml:54; I/Storage/AzureBlobStorageService.cs:48–62; UI/features/claim-detail/claim-documents.component.ts:65. Static configuration finding; Docker end-to-end execution was unavailable.

**F16 — Documented environment-scoped API URL does not reach the build job.** README says repository/environment variables and later instructs creating the production environment. deploy.yml reads PRODUCTION_API_URL in build-test, which has no environment declaration; only deploy selects production. If configured only on that environment, the build script throws because API_BASE_URL is empty. Clarify repository scope or bind the build job to the intended environment. Evidence: README.md:197–200 and :289; .github/workflows/deploy.yml:15, :65–70, :89; frontend/claims-ui/tools/configure-production-environment.mjs:3–5.

**F17 — Swagger lacks several success response schemas.** Policy search/coverage, reference causes/statuses/users, and status transition return IActionResult with untyped 200 metadata. Swagger therefore cannot describe their success payloads as accurately as typed claim/reserve actions. Document actual schemas and relevant error codes; add a contract check. Evidence: API/Controllers/PoliciesController.cs:15–23, ReferenceController.cs:16–29, ClaimsController.cs:160; README.md:245. Functional endpoint tests do not establish OpenAPI schema completeness.

**F18 — Blob/file upload is outside SQL rollback and retry compensation.** The handler stores bytes before metadata/audit/claim changes commit. A later audit/SQL failure leaves an orphan; a concurrency retry uploads another numeric-suffix object. IStorageService.DeleteAsync exists but is never used to compensate this handler. Make upload ownership and failure cleanup explicit. Evidence: A/Documents/DocumentHandlers.cs:17–31; I/Storage/AzureBlobStorageService.cs:26–43; P/Services/UnitOfWork.cs:98. Existing tests check validation rejection and successful uploads, not failed SQL commit after successful storage.

**F19 — Default Compose API credentials bypass database append-only permissions.** The application has effective EF guards, a tested DENY script, and a documented runtime-role procedure, but Compose runs the API as sa. A sysadmin is not constrained by DENY UPDATE/DELETE on ClaimAuditLog. Give the API a separate restricted principal and retain installer credentials only for migration/Hangfire setup. Evidence: docker-compose.yml API ConnectionStrings__Default; infra/sql/runtime-permissions.sql; IT/RuntimePermissionSqlTests.RuntimeRoleAllowsAuditAppendAndBlocksSqlUpdateAndDelete. Azure's actual runtime identity remains Unverified.

**F20 — Development local downloads bypass tenant authorization and expiry.** The route is AllowAnonymous and authorizes only by environment/path safety; a known path for another tenant can be retrieved without a token. README candidly describes this limitation. It is confined to Development and is not evidence of public Azure leakage, but it fails an unrestricted “authorization on every document endpoint” claim. Use a tenant-authorized or short-lived signed local download path if Development is shared. Evidence: API/Controllers/LocalStorageController.cs:8–19; README.md:301; I/Storage/LocalFileSystemStorageService.cs. Public demo login is also intentionally unauthenticated when explicitly enabled; see the security inventory.

### Low

**F21 — Reserve history presentation contradicts DD-36 and ARCHITECTURE.md.** The read query orders by ChangeSequence, but the UI flattens and reorders by descending CreatedAt. Use a clearly defined component/sequence ordering, or explicitly revise the decision. Evidence: P/Queries/ReadQueries.cs:77; UI/features/claim-detail/claim-reserves.component.ts:71; docs/DESIGN-DECISIONS-addendum.md:16; ARCHITECTURE.md:116. Approval-time balances may legitimately be non-monotonic in sequence order.

**F22 — Applicable party audit links are missing.** The audit component links ReserveHistory and ClaimDocument only; PARTY_ADDED/PARTY_REMOVED have related ClaimParty IDs but no navigation target. Evidence: UI/features/claim-detail/claim-audit.component.ts related-link predicate; A/Behaviors/DomainEventDispatcher.cs:32. Add party targeting, including inactive parties.

**F23 — No standalone SQL seed script deliverable.** Migration HasData/InsertData seeding is correct for D1 and runs on a fresh SQL database. D2 §4.1 separately asks for SQL seed scripts; only runtime-permissions.sql is present under infra/sql and the deployment migration script is generated, not supplied as a seed artifact. Provide an EF-generated reproducible SQL artifact/instruction explicitly satisfying this deliverable; do not duplicate startup seeding. Evidence: P/Seeding/SeedData.cs; MIG:462; .github/workflows/deploy.yml migration-script step.

**F24 — Historical topic documents contain current-looking stale assertions.** CLAIMS-CORE says active parties only while detail queries include inactive parties; BACKGROUND-JOBS says no dashboard is mapped while an optional manager-protected dashboard exists; PERSISTENCE's “only handwritten SQL” counter statement overlooks the job claim lock. Mark these as historical scope or update the assertions. Evidence: docs/CLAIMS-CORE.md, docs/BACKGROUND-JOBS.md, docs/PERSISTENCE.md; P/Queries/ReadQueries.cs:52; API/BackgroundJobs.cs; P/Services/BackgroundJobStore.cs:124. Root-document contradictions are listed separately below.

**F25 — Readability and query-polish debt.** Dense one-line Angular methods/templates make async state review difficult; policy search's culture-sensitive ToLower calls generate eight analyzer warnings and apply functions to indexed columns. Current list projection avoids N+1, but no production query-plan/load evidence establishes adequacy of date/tenant/sort indexing. Remove the analyzer warnings, format the complex UI flows, and measure representative SQL before choosing additional indexes. Evidence: P/Queries/ReadQueries.cs:119–121; CFG:82–85; UI/features/claims-list/claims-list.component.ts:25–44. This is maintenance/performance follow-up, not proof of slow production execution.

**F26 — Stateful validation rules are not FluentValidation rules as literally requested.** Every command has a registered validator, but approval validators check only IDs, and the transition validator checks IDs/enums/reason lengths. Approval authority/self-approval, aggregate reserve cap, valid-next-status and closure constraints run in handlers/Domain instead of FluentValidation. This is not a server-side enforcement bypass: transaction-scoped domain checks and concurrency re-evaluation are verified. D1 §8 and D2 §3.4 explicitly ask for these rules in validators as well. Either record this intentional placement as an explicit decision or expose the shared evaluation through validators while retaining authoritative transaction-time checks. Evidence: A/Reserves/ReserveValidators.cs:59; A/Claims/ClaimTransitionHandlers.cs:21; A/Reserves/ReserveHandlers.cs:36; D/Rules/ClaimStateMachine.cs.

## Build, test, and audit execution

Commands below are the exact build/test/audit commands used. Root commands ran in C:/Test/DICEUS_FullStack; npm commands ran in C:/Test/DICEUS_FullStack/frontend/claims-ui. Runtime versions observed: .NET SDK 9.0.318, Node v24.13.0, npm 11.6.2, Chrome Headless 154 on Windows. CI declares Node 20; that Linux/Node-20 combination was inspected, not executed locally.

| Command | Result |
| --- | --- |
| dotnet build backend/ClaimsModule.sln -m:1 -nr:false -p:UseSharedCompilation=false | PASS, exit 0; 0 errors, 22 warnings; approximately 44.47 s. 14 NU1900 warning occurrences from unavailable NuGet vulnerability feed plus 8 CA1311/CA1304/CA1862 occurrences in ReadQueries. Cached dependencies restored. |
| npm run build -- --configuration production | First sandbox run blocked with spawn EPERM. Same command rerun with approved process access: PASS, exit 0, approximately 13 s; initial bundle 712.34 kB, estimated transfer 158.60 kB; all three lazy feature routes built. |
| npm run test:ci -- --no-progress | PASS, 53/53, exit 0, Chrome Headless/Karma 6.4.4. |
| npm run test:http-boundary | PASS, exit 0: HTTP boundary check passed. |
| dotnet test backend/ClaimsModule.sln --no-build --no-restore -m:1 --logger trx --results-directory TestResults/FinalAudit | First sandbox run: Domain 261 pass, Application 139 pass, Integration 40 pass/72 fail/5 skip. SQL connection failures reported “The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.” These are environment failures, superseded by the next run. |
| dotnet test backend/ClaimsModule.sln --no-build --no-restore -m:1 --logger trx --results-directory TestResults/FinalAuditVerified | Rerun with approved SQL/process access: PASS exit 0. Domain 261, Application 139, Integration 112 passed/5 skipped. Total 512 passed, 0 failed, 5 skipped. |
| docker info --format '{{.ServerVersion}}' | BLOCKED: Docker Desktop Linux engine pipe //./pipe/dockerDesktopLinuxEngine unavailable. |
| npm audit --json > ../../TestResults/FinalAudit/npm-audit.json | Exit 1 because advisories found: 66 entries (2 Critical, 39 High, 18 Moderate, 7 Low). Registry query completed. No packages changed. |
| npm audit --omit=dev --json > ../../TestResults/FinalAudit/npm-audit-production.json | Exit 1 because advisories found: 10 entries (4 High, 6 Moderate). No packages changed. |

Both dotnet test commands used the following session-only override, exactly:

    $env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'

The fixture replaced the database name with a unique ClaimsModule_Tests_<GUID>, applied the real migration, and deleted only its own database. No existing application database was reset. Relevant results are in the ignored TestResults/FinalAuditVerified directory:
- marek_P1TEZ1_2026-10-06_22_59_19.trx: Domain 261/261.
- marek_P1TEZ1_2026-10-06_22_59_20.trx: Application 139/139.
- marek_P1TEZ1_2026-10-06_22_59_23.trx: Integration 112/117 executed; five skipped.

All discovered repository suites were invoked. The skipped test names are:
- AzureBlobStorageTests.FilenameCollisionGetsNumericSuffix
- AzureBlobStorageTests.UploadUsesSafeExpectedPathAndWorkingOneHourReadOnlySas
- AzureBlobStorageTests.StoredPathTraversalIsRejected
- AzureBlobDocumentSqlApiTests.PostAndGetDocumentsUseAzuriteAndReturnWorkingDownloadUrl
- AzureBlobDocumentSqlApiTests.WrongMimeAndOversizeAreRejectedWithoutCreatingDocuments

Reason: Docker/Azurite was unavailable, no azurite or azurite-blob executable was found, and CLAIMS_TEST_AZURE_BLOB was not configured. No full browser end-to-end suite or measured code-coverage suite is defined; the 53 frontend tests are unit/component/service tests, not a deployed walkthrough. Passing counts are not a claim of 100% behavioral or line coverage.

## Requirement matrix

The following sections form one traceability matrix with the requested five columns. Repeated obligations in D2 receive their own rows, including the differently numbered D2 business rules. D1 §9 is expanded to one row per specified field. Optional/excluded requirements are identified explicitly.

### D1 scope, roles, lifecycle, intake, and reserve workflow (§§1–6)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §1.3/§2 FNOL, lifecycle, reserves, GL, documents, audit vertical slice | Partial | API/Controllers/ClaimsController.cs:19; API/Controllers/ReservesController.cs:25; I/Jobs/GlPostingJobs.cs:8 | Backend paths exist; F03–F06 and F14 affect end-to-end use. | High |
| D1 §2 policy simulation, SLA, list/intake/detail screens | Yes | P/Seeding/SeedData.cs:1; I/Jobs/SlaMonitoringJob.cs:5; UI/app.routes.ts:3 | No additional domain subsystem is required. | — |
| D1 §2 excluded coverage adjudication, payments, litigation, fraud, bulk, messaging, reinsurance | N/A | docs/spec/spec-1-functional.md:95 | Not required; recovery reserve component remains in scope despite full recoveries being excluded. | — |
| D1 §3 Handler create/party/document/within-authority access | Yes | API/Controllers/ClaimsController.cs:12; D/Rules/ReserveAuthorityPolicy.cs:7; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Any role may submit a larger pending transaction per DD-07. | — |
| D1 §3 Supervisor approval/rejection up to 100,000 | Yes | API/Controllers/ReservesController.cs:48; D/Rules/ReserveAuthorityPolicy.cs:7; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Status permissions follow DD-11's explicit handler-level edges. | — |
| D1 §3 Manager authority up to 10,000,000 and override | Partial | API/Controllers/ReservesController.cs:98; IT/ReserveSqlApiTests.AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains | Backend correct; manager UI F04. | High |
| D1 §3 role enforcement in backend and UI review buttons | Yes | API/Controllers/ReservesController.cs:48 and :61; UI/features/claim-detail/claim-reserves.component.ts:35; UI/core/services/auth.service.ts:66 | Self-approval/amount checks also enforced server-side. | — |
| D1 §4.1 seven status values | Yes | D/Enums/ClaimEnums.cs:1; D/Rules/ClaimStateMachine.cs:6 | Reopened is transient per DD-13. | — |
| D1 §4.2 Draft → Open | Yes | DT/ClaimTests.DD10WarningsPersistAndOpenAcknowledgesThem; D/Rules/ClaimStateMachine.cs:6 | Requires claimant, assignee, no Critical issues, warning acknowledgement. | — |
| D1 §4.2 Open → UnderInvestigation | Yes | IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | Seeded edge enforced. | — |
| D1 §4.2 Open → PendingPayment | Yes | DT/ClaimTests.PendingPaymentAcceptsAutoApprovedReserveAndInvestigationRequiresAcceptedLiability | Approved or AutoApproved qualifies per DD-05. | — |
| D1 §4.2 Open → Closed | Yes | DT/ClaimStateMachineTests; A/Claims/ClaimTransitionHandlers.cs:36 | DD-14 permits this edge without an extra feature flag; closure checks still apply. | — |
| D1 §4.2 Open → Withdrawn | Yes | DT/ClaimTests.WithdrawnIsTerminalAndRequiresReason | Reason required. | — |
| D1 §4.2 UnderInvestigation → Open | Yes | DT/ClaimStateMachineTests; P/Seeding/SeedData.cs:1 | Seeded edge enforced. | — |
| D1 §4.2 UnderInvestigation → PendingPayment | Yes | DT/ClaimTests.PendingPaymentAcceptsAutoApprovedReserveAndInvestigationRequiresAcceptedLiability; DT/ClaimLiabilityTests | Persisted Accepted/PartiallyAccepted determination per DD-37. | — |
| D1 §4.2 UnderInvestigation → Closed | Yes | DT/ClaimStateMachineTests; A/Claims/ClaimTransitionHandlers.cs:36 | Same closure conditions. | — |
| D1 §4.2 UnderInvestigation → Withdrawn | Yes | DT/ClaimStateMachineTests; D/Rules/ClaimStateMachine.cs:6 | Reason required. | — |
| D1 §4.2 PendingPayment → Closed | Yes | IT/BackendE2eScenarioTests.FullLifecycleClosesAndReopensToOpen | Same closure conditions. | — |
| D1 §4.2 Closed → Reopened | Yes | DT/ClaimTests.BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened | Supervisor/Manager plus reason. | — |
| D1 §4.2 Reopened → Open immediately | Yes | DT/ClaimTests.BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened | Atomic preflight of both transitions. | — |
| D1 §4.2 all other transitions invalid, 422 | Yes | DT/ClaimStateMachineTests; IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | Includes Withdrawn terminal state. | — |
| D1 §5.1 Critical versus Warning intake behavior | Yes | A/Claims/ClaimValidators.cs:41; A/Behaviors/PipelineBehaviors.cs:30; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | DD-10 rejects Critical creation rather than persisting an invalid Draft. | — |
| D1 §5.2 step 1 policy lookup/details/unknown-policy/loss fields | Partial | UI/features/fnol-intake/loss-step.component.ts:17; A/Claims/ClaimHandlers.cs:17 | Fields implemented; policy error recovery F12. | Medium |
| D1 §5.2 step 2 parties and five asset types | Yes | UI/features/fnol-intake/parties-step.component.ts:12; UI/features/fnol-intake/fnol-forms.ts:4; D/Enums/ClaimEnums.cs:1 | Zero risks allowed with warning per DD-10. | — |
| D1 §5.2 step 3 optional reserve, authority, review, transactional create | Yes | UI/features/fnol-intake/review-step.component.ts:13; A/Claims/ClaimHandlers.cs:17; IT/ReserveSqlApiTests.InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422 | Creation includes all children in the same unit of work. | — |
| D1 §5.2 creation success navigates with ClaimNumber | Yes | UI/features/fnol-intake/fnol-intake.component.ts:18; UI/features/claim-detail/claim-detail.component.ts:115 | Snackbar after detail loads; startup auth caveat F03 is separate. | — |
| D1 §5.3 unique atomic gap-free number; rollback and no reuse | Yes | P/Services/ClaimNumberGenerator.cs:11; IT/Persistence/PersistenceTests.FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap | Tenant/year transactional counter, not a non-transactional SQL SEQUENCE. | — |
| D1 §5.4 explicit dry-run plus synchronous intake validation | Yes | API/Controllers/ClaimsController.cs:30; IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | Warning collection is non-blocking until acknowledged for Open. | — |
| D1 §5.5 POL-2024-001001 / Meridian / Vehicle,Cargo / specified dates | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Exact seed tested. | — |
| D1 §5.5 POL-2024-001002 / Harborview / Property,Liability / dates | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Exact seed tested. | — |
| D1 §5.5 POL-2025-002001 / Coastal / Property,Equipment / dates | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Exact seed tested. | — |
| D1 §5.5 POL-2025-002002 / Stanton / Liability,Vehicle / dates | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Exact seed tested. | — |
| D1 §5.5 POL-2023-000099 / Archived / Property / expired dates | Yes | P/Seeding/SeedData.cs:1; IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | Produces warning rather than rejection. | — |
| D1 §5.6 COL-FIRE | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-FLOOD | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-THEFT | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-VEH-COL | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-VEH-COMP | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-LIAB | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-EQUIP | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-WIND | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-INJURY | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §5.6 COL-OTHER | Yes | P/Seeding/SeedData.cs:1; IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift | Active seeded reference. | — |
| D1 §6.1–6.2 Indemnity, Expense, ALAE, SubrogationRecoverable | Yes | D/Enums/ClaimEnums.cs:1; D/Entities/ClaimReserveComponent.cs:11 | DD-02 overrides D2 appendix's different component names. | — |
| D1 §6.3 three authority tiers and upper individual ceiling | Yes | D/Rules/ReserveAuthorityPolicy.cs:7; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Absolute signed delta per DD-04/07. | — |
| D1 §6.4 submission/autoapprove/pending/manual approval | Yes | A/Reserves/ReserveSubmission.cs:10; IT/ReserveSqlApiTests.InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422 | GL remains post-commit. | — |
| D1 §6.4 immutable pending financial fields | Yes | D/Entities/ReserveHistory.cs:5; P/Interceptors/AuditSaveChangesInterceptor.cs:1; IT/Persistence/PersistenceTests.ReserveFinancialFieldsAndDeletionAreRejected | Workflow metadata can change; amount/component/reason cannot be edited in place. | — |
| D1 §6.4 rejection/revised resubmission/retraction | Yes | D/Entities/ClaimReserveComponent.cs:11; IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | DD-27 applies normal threshold to the new row; original retained. | — |
| D1 §6.5 GL simulation and one posting per key | Yes | I/Jobs/GlPostingJobs.cs:8; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | No real accounting integration required. | — |
| D1 §6.6 ledger history and current approved balance | Yes | D/Entities/ReserveHistory.cs:5; IT/ReserveSqlApiTests.ApprovalOrderBalancesAndNegativeBalanceRecheckArePreserved | DD-05 excludes Pending/Rejected/Cancelled from current balance; Posted not required. | — |

### D1 business rules, closure conditions, and validation (§§7–8)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| BR-C-01 future loss date Critical | Yes | A/Claims/ClaimValidators.cs:45; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | Enforced in validator/domain. | — |
| BR-C-02 outside policy period Warning, audit and acknowledgement | Yes | D/Rules/ClaimIntakeWarnings.cs:9; DT/ClaimTests.DD10WarningsPersistAndOpenAcknowledgesThem | UTC calendar dates include the expiration day. | — |
| BR-C-03 claimant before Draft → Open | Yes | DT/ClaimTests.DD10ClaimantIsMandatoryAtCreation; D/Rules/ClaimStateMachine.cs:6 | DD-10 also requires claimant on creation. | — |
| BR-C-04 organisation-unique formatted atomic numbers | Yes | D/ValueObjects/ClaimNumber.cs:16; IT/Persistence/PersistenceTests.FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap | Soft-deleted numbers retained. | — |
| BR-C-05 cause exists and active for tenant | Yes | A/Claims/ClaimValidators.cs:49; P/Queries/ReadQueries.cs:104; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | Tenant-filtered reference lookup. | — |
| BR-C-06 unknown-policy Warning and block reserves | Yes | D/Rules/ClaimIntakeWarnings.cs:12; IT/ReserveSqlApiTests.InitialAutoAndPendingUseSameAtomicPathAndNoPolicyIs422 | Link-policy route clears/re-evaluates warning. | — |
| BR-C-07 description required, at least 20 characters | Yes | A/Claims/ClaimValidators.cs:47; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | Trims before minimum check. | — |
| BR-R-01 transaction sign and nonzero amount | Yes | D/Entities/ClaimReserveComponent.cs:11; DT/ReserveSubmissionValidationTests; IT/ReserveSqlApiTests.AllComponentSignsZeroPrecisionAndRequestValidationThroughApi | DD-04 permits signed Adjust and positive-magnitude Reverse. | — |
| BR-R-02 authority thresholds, GL after approval | Yes | D/Rules/ReserveAuthorityPolicy.cs:7; IT/BackgroundJobSqlTests.ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt | Approval event schedules after SQL commit. | — |
| BR-R-03 no self-approval, 422 | Yes | IT/ReserveSqlApiTests.SupervisorAndManagerCannotApproveOwnSubmission | Both supervisor and manager blocked. | — |
| BR-R-04 resubmit rejected as new immutable row | Yes | IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | Original remains Rejected, linked revision retained. | — |
| BR-R-05 aggregate 10M requires manager override | Partial | IT/ReserveSqlApiTests.ConcurrentDifferentComponentsCannotBypassAggregateCap; AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains | Server correct; manager UI F04. | High |
| BR-R-06 GL key Reserve:{ReserveId}:Change:{ChangeSequence}, reentrant | Yes | D/Entities/ReserveHistory.cs:5; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | Component ID is ReserveId per DD-17. | — |
| BR-ST-01 valid transitions only; 422 lists valid next statuses | Yes | A/Claims/ClaimTransitionHandlers.cs:36; IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | Shared transition exception carries blockers and allowed states. | — |
| BR-ST-02 any Open entry requires no Critical issue and claimant | Yes | DT/ClaimTests.BRST02LaterCriticalIssuesMustBeResolvedAndCannotBeAcknowledged; ReopenWithLaterCriticalIssueIsBlockedAtomicallyUntilResolution | Critical cannot be acknowledged as a Warning. | — |
| BR-ST-03 closure validates all conditions, 422 blockers | Yes | D/Rules/ClaimStateMachine.cs:6; IT/ClaimTransitionHttpTests | Preflight shares same evaluation. | — |
| BR-ST-04 supervisor+reason reopen ends Open | Yes | DT/ClaimTests.BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened; IT/BackendE2eScenarioTests.FullLifecycleClosesAndReopensToOpen | Atomic two-edge transition. | — |
| BR-D-01 private Azure path and sanitized filename | Partial | I/Storage/AzureBlobStorageService.cs:21; A/Storage/DocumentFileRules.cs:5; IT/LocalFileSystemStorageTests | Azure live transport Unverified; Compose URL F15. | Medium |
| BR-D-02 one-hour SAS, no byte proxy | Partial | I/Storage/AzureBlobStorageService.cs:48; A/Documents/DocumentHandlers.cs:49 | Server implementation present; skipped Blob tests; stale client SAS F14. | Medium |
| BR-D-03 configurable IStorageService/local fallback | Yes | I/DependencyInjection.cs:10; IT/StorageProviderFallbackTests | Missing/placeholder Azure config falls back locally; an actual remote outage does not silently switch stores. D1 requires missing-config fallback. | — |
| BR-P-01 claimant required before Open | Yes | DT/ClaimTests.BRP01LastActiveClaimantCannotBeRemovedEvenInDraft | DD-10 additionally forbids removal of last active claimant in Draft. | — |
| BR-P-02 five roles and repeated same-role parties | Yes | DT/ClaimTests.BRP02AllRolesAndRepeatedRolesAreSupported | Person/company field validation also enforced. | — |
| BR-A-01 append-only audit application enforcement | Yes | IT/Persistence/PersistenceTests.AuditIsAppendOnlyPropagatesActorAndCorrelationAndCannotBypassService; AuditBulkMutationAlsoThrowsAndFailedAuditRollbackLeavesNoRows | Database credential limitation is F19 / §14.2. | — |
| BR-A-02 every significant business action audited | Yes | AT/DomainEventAuditTests.EveryDomainEventHasAnAuditMappingWithJsonValues; IT/BackendE2eScenarioTests | Domain events plus explicit document/job/notes/assignee audit paths. | — |
| CC-01 no pending reserve at closure | Yes | DT/ClaimStateMachineTests; D/Rules/ClaimStateMachine.cs:6 | Pending transactions, not component status, checked per DD-05. | — |
| CC-02 no unresolved Critical validation at closure | Yes | DT/ClaimTests.BRST02LaterCriticalIssuesMustBeResolvedAndCannotBeAcknowledged | Unresolved Critical blocks. | — |
| CC-03 at least one active Claimant | Yes | D/Rules/ClaimStateMachine.cs:6; DT/ClaimTests.BRP01LastActiveClaimantCannotBeRemovedEvenInDraft | Inactive parties do not qualify. | — |
| CC-04 nonzero reserve warning and explicit justification | Yes | DT/ClaimTests.CC04ClosureRequiresJustificationForPositiveOrNegativeBalance | DD-14 covers negative recovery balances too. | — |
| D1 §8 LossDate not future / Critical message | Yes | A/Claims/ClaimValidators.cs:45; IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | 422 business validation. | — |
| D1 §8 LossDate required/valid date | Yes | A/Claims/ClaimValidators.cs:45; IT/CrossCuttingApiTests.MalformedJsonReturnsBadRequestContract | Missing/default date 422; syntactically malformed JSON/date is shared 400 per DD-20. | — |
| D1 §8 LossDate policy window / Warning | Yes | A/Claims/ClaimValidators.cs:68; D/Rules/ClaimIntakeWarnings.cs:9 | Warning is collected, persisted and audited. | — |
| D1 §8 LossDescription required and min 20 | Yes | A/Claims/ClaimValidators.cs:47 | Required Critical rule present. | — |
| D1 §8 CauseOfLossCode valid active reference | Yes | A/Claims/ClaimValidators.cs:49 | Asynchronous tenant-filtered validation. | — |
| D1 §8 null PolicyId Warning | Yes | D/Rules/ClaimIntakeWarnings.cs:12; IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | No-policy intake permitted without initial reserve. | — |
| D1 §8 ClaimParties claimant before Open | Yes | A/Claims/ClaimValidators.cs:58; D/Rules/ClaimStateMachine.cs:6 | DD-10 requires claimant at creation as well. | — |
| D1 §8 ReserveAmount sign | Yes | A/Reserves/ReserveValidators.cs:16; DT/ReserveSubmissionValidationTests | Delegates shared amount invariant to Domain. | — |
| D1 §8 ReserveComponent defined enum | Yes | A/Reserves/ReserveValidators.cs:12; IT/ReserveSqlApiTests.AllComponentSignsZeroPrecisionAndRequestValidationThroughApi | Invalid enum rejected. | — |
| D1 §8 ReserveAmount aggregate-cap Warning | Partial | D/Entities/ClaimReserveComponent.cs:11; IT/ReserveSqlApiTests.AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains | Domain enforcement verified; not a FluentValidation cap rule (F26). UI F04 separate. | Low |
| D1 §8 StatusTransition valid next status | Partial | A/Claims/ClaimTransitionHandlers.cs:21 and :36; D/Rules/ClaimStateMachine.cs:6 | Command shape in pipeline; state constraint only in Domain (F26). | Low |
| D1 §8 StatusTransition closure conditions | Partial | A/Claims/ClaimTransitionHandlers.cs:36; IT/ClaimTransitionHttpTests | All blockers returned; closure constraint is not in FluentValidation (F26). | Low |
| D1 §8 ReserveApproval elevated role | Partial | A/Reserves/ReserveHandlers.cs:36; API/Controllers/ReservesController.cs:48 | 403 per DD-07; server-side enforcement verified, not a FluentValidation authority rule (F26). | Low |
| D1 §8 ReserveApproval no self-approval | Partial | IT/ReserveSqlApiTests.SupervisorAndManagerCannotApproveOwnSubmission | 422 with required message; self-check in Domain rather than FluentValidation (F26). | Low |
| D1 §8 validators on MediatR commands, not only controllers | Yes | A/DependencyInjection.cs:10; AT/CheckpointValidationTests.EveryProductionCommandHasRegisteredHandlerAndValidator; AT/PipelineBehaviorTests.WarningIsCollectedAndHandlerRuns | Rules needing current aggregate state remain in transaction/domain to handle concurrency. | — |

### D1 required entities and fields (§9)

The specification allows an equivalent schema. Id/Role/Type domain property names map to the required database names. Common shadow columns and precision are covered by CFG:40 and the passing IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39. The migration was actually applied to fresh databases.

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §9.1 Claims.ClaimId — Primary key (GUID) | Yes | MIG:147; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.OrganisationId — Tenant isolation (GUID, NOT NULL) | Yes | MIG:166; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | NOT NULL; global tenant filter and composite tenant foreign keys. | — |
| D1 §9.1 Claims.ClaimNumber — System-generated unique identifier (NVARCHAR(50), NOT NULL) | Yes | MIG:148; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.PolicyId — FK to simulated policy table (GUID, nullable — unknown policy intake) | Yes | MIG:149; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.PolicyNumber — Denormalised for display; populated from linked policy | Yes | MIG:150; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.ClientName — Insured/policyholder name; denormalised for display | Yes | MIG:151; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.Status — Claim status (NVARCHAR(50), NOT NULL) | Yes | MIG:152; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.Severity — Catastrophic / Critical / Standard / Minor (NVARCHAR(50)) | Yes | MIG:153; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.ReportedDate — When FNOL was received (DATETIMEOFFSET(7), NOT NULL) | Yes | MIG:154; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.AssignedHandlerId — Assigned handler user ID (GUID, nullable) | Yes | MIG:155; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.ClosedAt — When claim was closed (DATETIMEOFFSET(7), nullable) | Yes | MIG:156; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.ClosureReason — Reason code or text for closure (nullable) | Yes | MIG:157; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.Notes — Internal notes (NVARCHAR(MAX), nullable) | Yes | MIG:158; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.IsDeleted — Soft delete flag | Yes | MIG:163; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.CreatedAt — Audit timestamp | Yes | MIG:161; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.UpdatedAt — Audit timestamp (nullable) | Yes | MIG:167; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.UserCreated — Audit user ID (nullable) | Yes | MIG:168; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.UserModified — Audit user ID (nullable) | Yes | MIG:169; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.1 Claims.RowVer — Optimistic concurrency (ROWVERSION) | Yes | MIG:170; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.LossEventId — Primary key | Yes | MIG:374; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.ClaimId — FK to Claims | Yes | MIG:375; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.LossDate — Date/time of the loss event (DATETIMEOFFSET(7), NOT NULL) | Yes | MIG:376; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.LossDescription — Narrative description (NVARCHAR(MAX), NOT NULL) | Yes | MIG:377; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.LossLocation — Free-text location description (NVARCHAR(500), nullable) | Yes | MIG:378; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.CauseOfLossCode — FK to CauseOfLossCodes.Code (NVARCHAR(50), NOT NULL) | Yes | MIG:379; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.EstimatedLossAmount — Initial dollar estimate (DECIMAL(19,4), nullable) | Yes | MIG:380; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.ReportDate — Date claim was formally reported (DATETIMEOFFSET(7), NOT NULL) | Yes | MIG:381; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.2 LossEvents.PoliceReportNumber — If applicable (NVARCHAR(100), nullable) | Yes | MIG:382; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.ClaimPartyId — Primary key | Yes | MIG:254; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.ClaimId — FK to Claims | Yes | MIG:255; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.PartyRole — Claimant / Insured / ThirdParty / Witness / Attorney (NVARCHAR(50)) | Yes | MIG:256; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.PartyType — Person / Company (NVARCHAR(20)) | Yes | MIG:257; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.FirstName — For person parties (NVARCHAR(100), nullable) | Yes | MIG:258; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.LastName — For person parties (NVARCHAR(100), nullable) | Yes | MIG:259; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.CompanyName — For company parties (NVARCHAR(255), nullable) | Yes | MIG:260; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.Email — Contact email (NVARCHAR(255), nullable) | Yes | MIG:261; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.Phone — Contact phone (NVARCHAR(50), nullable) | Yes | MIG:262; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.Notes — Additional notes (NVARCHAR(MAX), nullable) | Yes | MIG:263; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.3 ClaimParties.IsActive — Soft remove from claim (BIT) | Yes | MIG:264; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.ClaimRiskObjectId — Primary key | Yes | MIG:315; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.ClaimId — FK to Claims | Yes | MIG:316; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.AssetType — Vehicle / Property / Person / Equipment / Other (NVARCHAR(50)) | Yes | MIG:317; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.AssetDescription — Free-text description of the asset (NVARCHAR(500)) | Yes | MIG:318; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.DamageDescription — Description of damage sustained (NVARCHAR(MAX), nullable) | Yes | MIG:319; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.IsPrimary — Is this the primary risk object? (BIT) | Yes | MIG:320; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.4 ClaimRiskObjects.AssetReference — Optional reference number, VIN, property address etc. (NVARCHAR(255), nullable) | Yes | MIG:321; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.5 ClaimReserveComponents.ReserveComponentId — Primary key | Yes | MIG:287; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.5 ClaimReserveComponents.ClaimId — FK to Claims | Yes | MIG:288; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.5 ClaimReserveComponents.Component — Indemnity / Expense / ALAE / SubrogationRecoverable (NVARCHAR(50)) | Yes | MIG:289; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.5 ClaimReserveComponents.CurrentAmount — Computed: sum of all posted/approved history transactions (DECIMAL(19,4)) | Yes | MIG:290; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Approved/AutoApproved ledger sum cached per DD-05/DD-28; independent of GL posting. | — |
| D1 §9.5 ClaimReserveComponents.Status — Active / Closed (NVARCHAR(50)) | Yes | MIG:291; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.5 ClaimReserveComponents.Notes — Optional notes (NVARCHAR(MAX), nullable) | Yes | MIG:293; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Optional EF shadow property. | — |
| D1 §9.6 ReserveHistory.ReserveHistoryId — Primary key | Yes | MIG:410; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ReserveComponentId — FK to ClaimReserveComponents | Yes | MIG:411; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ClaimId — FK to Claims (denormalised for query convenience) | Yes | MIG:412; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.TransactionType — Add / Adjust / Reverse (NVARCHAR(50)) | Yes | MIG:413; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.Amount — Delta amount (positive = increase, negative = decrease) (DECIMAL(19,4)) | Yes | MIG:414; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.PreviousBalance — Balance before this transaction (DECIMAL(19,4)) | Yes | MIG:415; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Nullable until approval by DD-05/DD-36; approval-order values tested. | — |
| D1 §9.6 ReserveHistory.NewBalance — Balance after this transaction (DECIMAL(19,4)) | Yes | MIG:416; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Nullable until approval by DD-05/DD-36; approval-order values tested. | — |
| D1 §9.6 ReserveHistory.ApprovalStatus — AutoApproved / PendingApproval / Approved / Rejected / Cancelled (NVARCHAR(50)) | Yes | MIG:422; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ApprovedByUserId — User who approved (nullable) | Yes | MIG:423; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ApprovedAt — Timestamp of approval (DATETIMEOFFSET(7), nullable) | Yes | MIG:424; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.RejectedByUserId — User who rejected (nullable) | Yes | MIG:425; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.RejectedAt — Timestamp of rejection (nullable) | Yes | MIG:426; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.RejectionReason — Free text (nullable) | Yes | MIG:427; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ChangeReason — Why the reserve was changed (NVARCHAR(500)) | Yes | MIG:419; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.PostingStatus — Pending / Posted / Failed / Cancelled (NVARCHAR(50)) | Yes | MIG:430; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.PostingJobId — Hangfire job ID (nullable) | Yes | MIG:431; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.IdempotencyKey — Reserve:{ReserveId}:Change:{Seq} (NVARCHAR(200)) | Yes | MIG:418; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.ChangeSequence — Monotonically increasing per component (INT) | Yes | MIG:417; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.SubmittedByUserId — User who submitted (nullable) | Yes | MIG:420; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.6 ReserveHistory.CreatedAt — Timestamp | Yes | MIG:435; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.ClaimDocumentId — Primary key | Yes | MIG:222; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.ClaimId — FK to Claims | Yes | MIG:223; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.DocumentType — E.g. PoliceReport, MedicalReport, Invoice, Other (NVARCHAR(100)) | Yes | MIG:224; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.DocumentName — Filename as uploaded (NVARCHAR(255)) | Yes | MIG:225; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Sanitized/collision-safe stored name per document rules. | — |
| D1 §9.7 ClaimDocuments.BlobPath — Full path in blob storage (NVARCHAR(500)) | Yes | MIG:226; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.ContentType — MIME type (NVARCHAR(100)) | Yes | MIG:227; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.FileSizeBytes — File size in bytes (BIGINT) | Yes | MIG:228; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.UploadedAt — Upload timestamp (DATETIMEOFFSET(7)) | Yes | MIG:229; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.UploadedByUserId — User who uploaded (GUID, nullable) | Yes | MIG:230; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.7 ClaimDocuments.Notes — Optional description (NVARCHAR(500), nullable) | Yes | MIG:231; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.AuditLogId — Primary key | Yes | MIG:192; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.ClaimId — FK to Claims | Yes | MIG:193; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.EventType — E.g. CLAIM_CREATED, STATUS_CHANGED, PARTY_ADDED, RESERVE_CREATED, RESERVE_APPROVED, RESERVE_REJECTED, DOCUMENT_UPLOADED, GL_POSTING_SIMULATED, SLA_BREACH_DETECTED (NVARCHAR(100)) | Yes | MIG:194; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.Description — Human-readable description of the event (NVARCHAR(MAX)) | Yes | MIG:195; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.OldValue — Previous value as JSON (nullable, NVARCHAR(MAX)) | Yes | MIG:196; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.NewValue — New value as JSON (nullable, NVARCHAR(MAX)) | Yes | MIG:197; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.RelatedEntityId — ID of the related entity (reserve, document, etc.) (GUID, nullable) | Yes | MIG:198; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.RelatedEntityType — Type name of the related entity (nullable) | Yes | MIG:199; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.CorrelationId — Request correlation ID (GUID, nullable) | Yes | MIG:200; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.CreatedAt — Timestamp (DATETIMEOFFSET(7), NOT NULL) | Yes | MIG:202; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.8 ClaimAuditLog.CreatedByUserId — User or system actor (GUID, nullable) | Yes | MIG:201; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.CauseOfLossCodeId — Primary key | Yes | MIG:20; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.Code — Unique code (NVARCHAR(50), UNIQUE) | Yes | MIG:21; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.Name — Display name (NVARCHAR(255)) | Yes | MIG:22; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.PerilCategory — Property / Auto / Liability / Weather / Equipment / Crime / General | Yes | MIG:23; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.IsActive — Whether available for selection (BIT) | Yes | MIG:24; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.9 CauseOfLossCodes.SortOrder — Display order (INT) | Yes | MIG:25; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.PolicyId — Primary key | Yes | MIG:104; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.PolicyNumber — Unique policy number (NVARCHAR(50)) | Yes | MIG:105; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.ClientName — Policyholder name (NVARCHAR(255)) | Yes | MIG:106; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.EffectiveDate — Policy start date (DATE) | Yes | MIG:107; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.ExpirationDate — Policy end date (DATE) | Yes | MIG:108; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.Status — Active / Expired / Cancelled (NVARCHAR(50)) | Yes | MIG:109; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |
| D1 §9.10 Policies.CoverageTypes — Comma-separated list or JSON array of coverage type names | Yes | MIG:110; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Mapped with specified nullability/type or equivalent schema. | — |

### D1 API behavior (§10)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §10 REST JSON and Bearer JWT | Yes | API/Program.cs:28; API/Authentication.cs:14; IT/CrossCuttingApiTests.RolesAndCorrelationIdFlowFromToken | Multipart document upload is the specified exception. | — |
| D1 §10 all writes idempotent with key, as narrowed by DD-32 | Partial | API/IdempotencyMiddleware.cs:15; IT/CrossCuttingSqlApiTests.AuditReceivesCorrelationAndIdempotencyReplaysOrRejectsMismatch | F05 PUT financial duplication; F06 post-commit replay failure. POST multipart/dry-run exclusions are explicit decisions. | High |
| D1 §10.1 POST /api/claims, transactional graph, 201/detail | Yes | API/Controllers/ClaimsController.cs:19; IT/ClaimsCoreSqlApiTests.CreateReturnsPersistedDetailWarningsAuditAndIdempotentReplay; IT/ReserveSqlApiTests.InitialReserveAuditFailureRollsBackClaimNumberHistoryAndJob | All children, number and audit roll back together; response-replay caveat F06. | — |
| D1 §10.1 GET /api/claims, pagination/all filters/summary shape | Yes | API/Controllers/ClaimsController.cs:44; P/Queries/ReadQueries.cs:15; IT/ClaimsCoreSqlApiTests.ListFiltersSearchPaginationAndSortAreAppliedInSql | Supports policyId and partial claim/client search; UI date semantics F09 separate. | — |
| D1 §10.1 GET /api/claims/{id}, full graph/recent audit | Yes | API/Controllers/ClaimsController.cs:37; P/Queries/ReadQueries.cs:38 | Includes parties, risks, reserves, documents, latest 20 audit rows. | — |
| D1 §10.1 PUT /api/claims/{id}/status, 422 blockers | Yes | API/Controllers/ClaimsController.cs:159; IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | Success returns current id/status; schema documentation F17. | — |
| D1 §10.1 GET /api/claims/{id}/audit, reverse-chronological page | Yes | API/Controllers/ClaimsController.cs:54; P/Queries/ReadQueries.cs:86 | Timestamp plus ID tie-break; read only. | — |
| D1 §10.1 POST /api/claims/{id}/parties | Yes | API/Controllers/ClaimsController.cs:86; IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | 201 and audit. | — |
| D1 §10.1 DELETE party, inactive retained, last claimant 422 | Yes | API/Controllers/ClaimsController.cs:98; IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | Repeated inactive removal is harmless. | — |
| D1 §10.1 POST documents multipart + metadata | Partial | API/Controllers/ClaimsController.cs:64; IT/DocumentSqlApiTests.UploadSanitizesNameAndListReturnsUploaderUrlAndAudit | F18 storage rollback; Azure transport skipped here. | Medium |
| D1 §10.1 GET documents with one-hour download URLs | Partial | API/Controllers/ClaimsController.cs:79; A/Documents/DocumentHandlers.cs:43; I/Storage/AzureBlobStorageService.cs:48 | Azure transport Unverified; F14 and F15 affect client use. | Medium |
| D1 §10.2 POST reserve, signed transaction, 201/status | Yes | API/Controllers/ReservesController.cs:25; IT/ReserveSqlApiTests.AllComponentSignsZeroPrecisionAndRequestValidationThroughApi | Post-commit replay limitation F06. | — |
| D1 §10.2 GET reserve summary and full history | Yes | API/Controllers/ReservesController.cs:18; P/Queries/ReadQueries.cs:72 | Current/pending balances, users, workflow status included. | — |
| D1 §10.2 POST transaction approve | Yes | API/Controllers/ReservesController.cs:47; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | 204; role/self/aggregate checks. | — |
| D1 §10.2 POST transaction reject with reason | Yes | API/Controllers/ReservesController.cs:60; IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | 204; reason retained. | — |
| D1 §10.2 POST transaction retract, submitter only | Yes | API/Controllers/ReservesController.cs:73; DT/ReserveComponentTests | Pending becomes Cancelled; foreign submitter blocked. | — |
| D1 §10.3 GET active cause codes/category | Yes | API/Controllers/ReferenceController.cs:15; P/Queries/ReadQueries.cs:104; IT/ReferencePolicySqlApiTests | Result schema F17. | — |
| D1 §10.3 GET statuses with valid next transitions | Yes | API/Controllers/ReferenceController.cs:21; P/Queries/ReadQueries.cs:109; IT/ReferencePolicySqlApiTests | Seeded transitions returned. | — |
| D1 §10.3 GET policies/search by q, dates/status/coverage | Yes | API/Controllers/PoliciesController.cs:14; P/Queries/ReadQueries.cs:117; IT/ReferencePolicySqlApiTests | Result schema F17; ToLower warnings F25. | — |
| D1 §10.4 uniform type/title/status/errors structure | Yes | API/ApiMiddleware.cs:13; IT/CrossCuttingApiTests.ErrorsUseSharedContract; TransportErrorsUseSharedContract | Includes correlation ID; tested 400/401/403/404/405/409/415/422 handling. | — |
| API undocumented extras and backward compatibility | Partial | API/Controllers/ClaimsController.cs:110; :122; :134; :147; :172; API/Controllers/ReservesController.cs:85 and :97 | Additive notes/policy/assignee/liability/preflight/retry/override routes; F05 PUT semantics and F17 schema gaps. No released consumer baseline supplied. | High |

### D1 frontend behavior (§11), one row per listed element

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §11 typed service-only HTTP | Yes | UI/core/services/api-services.spec.ts:1; frontend/claims-ui/tools/check-http-boundary.mjs | Boundary script passed; no component HttpClient use found. | — |
| D1 §11.1 paginated table with all seven columns | Yes | UI/features/claims-list/claims-list.component.ts:26; claims-list.component.html | Columns include cause of loss and reserve total. | — |
| D1 §11.1 seven exact status badge colors | Yes | UI/shared/status-badge/status-badge.component.scss:1; status-badge.component.spec.ts | Grey/blue/orange/purple/green/amber/dark-grey mappings present. | — |
| D1 §11.1 multi-status filter | Yes | UI/features/claims-list/claims-list.component.html:1; .ts:27 | Sends repeated status query values. | — |
| D1 §11.1 loss-date range filter | Partial | UI/features/claims-list/claims-list.component.ts:41; P/Queries/ReadQueries.cs:21 | F09 end-day omission; F11 stale response race. | Medium |
| D1 §11.1 assignee text search | Partial | UI/features/claims-list/claims-list.component.ts:28 and :43 | F10 reactivity/restoration defects. | Medium |
| D1 §11.1 cause dropdown | Yes | UI/features/claims-list/claims-list.component.html:1; .ts:31 | Active references populate control. | — |
| D1 §11.1 clickable rows → detail | Yes | UI/features/claims-list/claims-list.component.ts:39; claims-list.component.spec.ts | Router navigation implemented. | — |
| D1 §11.1 Log New Claim primary action | Yes | UI/features/claims-list/claims-list.component.html:1; UI/app.routes.ts:6 | FNOL route lazy loaded. | — |
| D1 §11.1 total count and page-size selector | Yes | UI/features/claims-list/claims-list.component.html:1; .ts:26 and :39 | Total from paginated response. | — |
| D1 §11.1 friendly no-results state | Partial | UI/features/claims-list/claims-list.component.html:1; .ts:40 | Empty state exists; F11 does not distinguish load error from real empty result. | Medium |
| D1 §11.2 independent step validation | Yes | UI/features/fnol-intake/fnol-intake.component.html:1; fnol-forms.spec.ts | Three reactive form groups and guarded progression. | — |
| D1 §11.2.1 policy typeahead/details/effective dates | Partial | UI/features/fnol-intake/loss-step.component.ts:27 and :77 | F12 stream terminates after search failure. | Medium |
| D1 §11.2.1 in-force green / outside-or-expired amber badge | Partial | UI/features/fnol-intake/loss-step.component.ts:36 and :93; fnol-forms.spec.ts | Checks loss-date window; does not separately use policy.status === Expired. Historical in-window loss makes the literal color requirements overlap; clarify per F13. | Medium |
| D1 §11.2.1 Unknown Policy toggle | Yes | UI/features/fnol-intake/loss-step.component.ts:86; fnol-forms.ts | Clears policy selection and allows intake without initial reserve. | — |
| D1 §11.2.1 datetime picker/future error | Yes | UI/features/fnol-intake/loss-step.component.ts:39–42; fnol-forms.spec.ts | datetime-local input; server revalidates. | — |
| D1 §11.2.1 searchable cause dropdown from API | Yes | UI/features/fnol-intake/loss-step.component.ts:44 and :76–83 | Text filter plus selected-code validation. | — |
| D1 §11.2.1 description textarea/minimum character indicator | Yes | UI/features/fnol-intake/loss-step.component.ts:51 | 20-character rule and inline error. | — |
| D1 §11.2.1 location text | Yes | UI/features/fnol-intake/loss-step.component.ts:56 | Mapped into request. | — |
| D1 §11.2.1 optional estimated amount | Yes | UI/features/fnol-intake/loss-step.component.ts:59; fnol-forms.ts | Nonnegative check; server precision validation. | — |
| D1 §11.2.2 add parties inline with role/type/names/email/phone | Yes | UI/features/fnol-intake/parties-step.component.ts:12; UI/shared/party-form/party-form.component.ts:11 | Reuses shared reactive party form. | — |
| D1 §11.2.2 claimant inline warning | Yes | UI/features/fnol-intake/parties-step.component.ts:12; fnol-forms.spec.ts | Step invalid without Claimant. | — |
| D1 §11.2.2 risk fields/type/damage/reference and no-risk advisory | Yes | UI/features/fnol-intake/parties-step.component.ts:28; fnol-forms.ts | No-risk warning is advisory. | — |
| D1 §11.2.2 added party/risk cards and removal | Yes | UI/features/fnol-intake/parties-step.component.ts:12 | Form arrays rendered with remove actions. | — |
| D1 §11.2.3 optional initial component/amount | Yes | UI/features/fnol-intake/review-step.component.ts:13; fnol-forms.spec.ts | Four D1 components; unknown-policy gating. | — |
| D1 §11.2.3 live authority threshold indicator | Yes | UI/shared/authority/authority-indicator.pipe.ts:4; authority-indicator.pipe.spec.ts | Exact <=10k/<=100k/>100k tier boundaries tested. | — |
| D1 §11.2.3 read-only entered-data review | Yes | UI/features/fnol-intake/review-step.component.ts:13 | Review summary includes policy, loss, parties, risks and reserve. | — |
| D1 §11.2.3 Create disabled until valid, warnings confirmation | Yes | UI/features/fnol-intake/fnol-intake.component.html:1; fnol-intake.component.spec.ts; intake-warnings-dialog.component.ts | Uses server dry-run before final creation. | — |
| D1 §11.2.3 success snackbar ClaimNumber and navigation | Yes | UI/features/fnol-intake/fnol-intake.component.ts:18; UI/features/claim-detail/claim-detail.component.ts:115 | Success preserved in navigation state/query. | — |
| D1 §11.2.3 errors inline and relevant-step summary | Yes | UI/features/fnol-intake/fnol-intake.component.ts:18; fnol-forms.ts stepForField; fnol-intake.component.spec.ts | Request field names mapped to affected step. | — |
| D1 §11.3 five-tab full-detail shell | Yes | UI/features/claim-detail/claim-detail.component.ts:38 | Overview, Parties, Reserves, Documents, Audit Log. | — |
| D1 §11.3 header prominent/copyable ClaimNumber | Yes | UI/features/claim-detail/claim-header.component.ts:11; claim-detail.component.ts copyClaimNumber | Clipboard action present. | — |
| D1 §11.3 header status chip | Yes | UI/features/claim-detail/claim-header.component.ts:11; UI/shared/status-badge/status-badge.component.ts:4 | Shared color mapping. | — |
| D1 §11.3 header policy/client | Yes | UI/features/claim-detail/claim-header.component.ts:11 | Unknown policy display supported. | — |
| D1 §11.3 header loss date/cause | Yes | UI/features/claim-detail/claim-header.component.ts:11 | Typed loss-event fields. | — |
| D1 §11.3 header valid transition action/confirmation/closure checklist | Yes | UI/features/claim-detail/claim-detail.component.ts:72 and :90; transition-dialog.component.ts; claim-detail.component.spec.ts | Server preflight; required reasons/justification. Pending disable gap F13 separate. | — |
| D1 §11.3 header assigned handler | Yes | UI/features/claim-detail/claim-detail.component.ts:71; claim-header.component.ts | All-role users picker, supervisor-gated reassignment per DD-40. | — |
| D1 §11.3 Overview loss details | Yes | UI/features/claim-detail/claim-overview.component.ts:11 | Date/description/location/cause. | — |
| D1 §11.3 Overview severity badge | Yes | UI/features/claim-detail/claim-overview.component.ts:11 | Standard default per DD-30. | — |
| D1 §11.3 Overview estimated loss | Yes | UI/features/claim-detail/claim-overview.component.ts:11 | Formatted amount/null state. | — |
| D1 §11.3 Overview editable notes | Partial | UI/features/claim-detail/claim-overview.component.ts:11; claim-detail.component.ts saveNotes | Save path exists; F13 pending button state. | Medium |
| D1 §11.3 Parties role/name/contact/active-inactive list | Yes | UI/features/claim-detail/claim-parties.component.ts:7; P/Queries/ReadQueries.cs:52 | Inactive rows deliberately retained. | — |
| D1 §11.3 Parties inline add | Partial | UI/features/claim-detail/claim-parties.component.ts:7; claim-detail.component.ts addParty | F13 duplicate-click/pending handling. | Medium |
| D1 §11.3 Parties remove disabled for last Claimant | Yes | UI/features/claim-detail/claim-parties.component.ts:7; claim-detail.component.spec.ts | Server also protects invariant. | — |
| D1 §11.3 Reserves summary per component/current/pending amber | Yes | UI/features/claim-detail/claim-reserves.component.ts:32 and :83–84 | Four cards, approved current balance and pending delta. | — |
| D1 §11.3 Reserves date/type/component/signed color/status/submitter/approver | Partial | UI/features/claim-detail/claim-reserves.component.ts:33–34 and :71 | Fields/color present; DD-36 ordering F21. | Low |
| D1 §11.3 Add Reserve slide-in component/amount/reason/authority | Partial | UI/features/claim-detail/claim-reserves.component.ts:41–50 | Drawer works; Material field errors/pending/status consistency F13; retry identity F06. | High |
| D1 §11.3 Approve/Reject only pending supervisor/manager rows | Yes | UI/features/claim-detail/claim-reserves.component.ts:35; UI/core/services/auth.service.ts:66 | Self and authority limits disable review with explanation. | — |
| D1 §11.3 own pending Retract | Yes | UI/features/claim-detail/claim-reserves.component.ts:36; UI/core/services/auth.service.ts:78 | Hidden for other users/nonpending states. | — |
| D1 §11.3 GL Pending/Posted/Failed badge and Failed retry | Yes | UI/features/claim-detail/claim-reserves.component.ts:34 and :37; claim-financial-tabs.component.spec.ts | Retry endpoint keeps GL identity; bounded refresh polling. | — |
| D1 §11.3 Documents metadata/uploader/size | Yes | UI/features/claim-detail/claim-documents.component.ts:28 | Name/type/date/uploader/size included. | — |
| D1 §11.3 Download obtains SAS on click/new tab | Partial | UI/features/claim-detail/claim-documents.component.ts:65 | F14 uses cached URL; noopener/noreferrer present. | Medium |
| D1 §11.3 upload picker/multipart/progress | Yes | UI/features/claim-detail/claim-documents.component.ts:23–25 and :57 | Visible file picker plus upload action; percentage/busy states. Azure transport Unverified. | — |
| D1 §11.3 Audit reverse chronological | Yes | UI/features/claim-detail/claim-audit.component.ts:9; P/Queries/ReadQueries.cs:91 | Server-defined timestamp/ID ordering. | — |
| D1 §11.3 Audit timestamp/event/description/actor/related links | Partial | UI/features/claim-detail/claim-audit.component.ts:13 and :30 | F22 party-related links missing. | Low |
| D1 §11.3 Audit paginated, no editing/deleting | Yes | UI/features/claim-detail/claim-audit.component.ts:9 | Read-only service and paging controls. | — |
| D1 §11.4 lazy ClaimsList/ClaimDetail/FnolIntake | Yes | UI/app.routes.ts:3 | Standalone loadComponent accepted by DD-23. | — |
| D1 §11.4 mock users/JWT interceptor/header/role switcher | Partial | UI/core/services/auth.service.ts:9; UI/app.component.ts:24; UI/core/interceptors/auth.interceptor.ts:5 | F03 initialization/expiry lifecycle. Server mock login intentional per DD-19. | High |
| D1 §11.4 Material custom primary palette | Yes | frontend/claims-ui/src/styles.scss:1; UI/app.component.html:1 | Custom palette/theme tokens applied. | — |
| D1 §11.4 API errors caught/service-layer snackbar severity | Partial | UI/core/interceptors/error.interceptor.ts:6; UI/core/services/error-notification.service.ts:9 | Central service displays errors; F12 terminated streams and F03 recovery remain. | High |
| D1 §11.4 all async loading and disabled pending buttons | Partial | UI/core/interceptors/loading.interceptor.ts:6; UI/app.component.html:1; claim-detail.component.ts | Global progress plus feature loaders; F13 not every button disabled. | Medium |
| D1 §11.4 Reactive Forms / Material errors throughout | Partial | UI/features/fnol-intake/fnol-forms.ts:4; claim-reserves.component.ts:42–50; transition-dialog.component.ts | Reactive forms present; field-level error omissions F13. | Medium |
| D1 §11.4 usable responsive layout at 1280px+ | Unverified | UI/features/claims-list/claims-list.component.scss:1; UI/features/claim-detail/claim-reserves.component.ts:53; frontend/claims-ui/src/styles.scss | Responsive CSS inspected and compiled; no visual 1280px browser walkthrough performed. | Low |

### D1 background jobs and document management (§§12–13)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §12 startup registers Hangfire jobs | Yes | API/BackgroundJobs.cs:10 and :28; IT/BackgroundJobSqlTests.RealApiStartupInstallsStorageStartsServerAndRegistersRecurringJobs | Testing host intentionally suppresses worker; separate real-start test passes. | — |
| D1 §12.1 PostGLReserveChangeJob exists | Yes | I/Jobs/GlPostingJobs.cs:8 | Hangfire fire-and-forget wrapper. | — |
| D1 §12.1 auto/manual approval triggers enqueue | Yes | A/Behaviors/GlPostingNotificationHandler.cs:7; IT/BackgroundJobSqlTests.ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt | Post-commit action, not before SaveChanges. | — |
| D1 §12.1 input HistoryId/ClaimId/key | Yes | A/Abstractions/BackgroundJobContracts.cs:8; I/Jobs/GlPostingJobs.cs:8 | Full identity checked again in persistence. | — |
| D1 §12.1 key format | Yes | D/Entities/ReserveHistory.cs:5; DT/ReserveComponentTests | Monotonic per-component sequence. | — |
| D1 §12.1 already Posted → no-op | Yes | P/Services/BackgroundJobStore.cs:38; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | No duplicate audit, posting timestamp or activity update. | — |
| D1 §12.1 otherwise journal audit + Posted + job ID atomically | Yes | P/Services/BackgroundJobStore.cs:45–75; IT/BackgroundJobSqlTests.AuditFailureRollsBackPostingSoDeliveryCanBeRetried | DR/CR simulation and financial delta included. | — |
| D1 §12.1 default retries then Failed + GL_POSTING_FAILED once | Yes | I/Jobs/GlPostingFailureFilter.cs:10; IT/BackgroundJobSqlTests.StatePipelineOnlyRecordsFailureAfterDefaultRetriesAreExhausted; TerminalFailureFilterIsIdempotentAndRetryRecoversWithSameKey | Tests drive actual Hangfire state filter pipeline and duplicate failure. | — |
| D1 §12.1 parallel reentrancy | Yes | IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | Claim lock plus conditional SQL update and atomic audit. | — |
| D1 §12.2 SlaMonitoringJob every 15 minutes/startup/no manual trigger | Yes | API/BackgroundJobs.cs:31; I/Jobs/SlaMonitoringJob.cs:5 | UTC cron */15 * * * *. | — |
| D1 §12.2 Draft/Open stale over 48h only | Yes | P/Services/BackgroundJobStore.cs:92; IT/BackgroundJobSqlTests.SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus | COALESCE(UpdatedAt, CreatedAt) per DD-18. | — |
| D1 §12.2 prescribed audit/description, no status change | Yes | P/Services/BackgroundJobStore.cs:114–119; IT/Persistence/PersistenceTests.SlaBulkUpdateAndAuditInsertDoNotBumpClaimActivity | SlaBreachedAt records flag; UpdatedAt also unchanged. | — |
| D1 §12.2 dedupe until 24h, parallel safety | Yes | P/Services/BackgroundJobStore.cs:110; IT/BackgroundJobSqlTests.SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus | Equality at 24h allows a new audit. | — |
| D1 §12.2 no email/push | Yes | I/Jobs/SlaMonitoringJob.cs:5; P/Services/BackgroundJobStore.cs:118 | Only DB flag/audit. | — |
| D1 §13 binaries external; Azure primary/local fallback | Yes | I/DependencyInjection.cs:10; IT/StorageProviderFallbackTests; P/Entities/PersistenceEntities.cs:46 | SQL stores metadata, not file bytes. | — |
| D1 §13 container claim-documents | Yes | A/Storage/StorageOptions.cs:17; I/Storage/AzureBlobStorageService.cs:18 | Configurable with required default. | — |
| D1 §13 organisation/claim/sanitized filename path | Yes | I/Storage/AzureBlobStorageService.cs:26–30; A/Storage/DocumentFileRules.cs:5 | Traversal rejected, collisions suffixed. Azure execution skipped. | — |
| D1 §13 reasonable 50 MB cap | Partial | A/Storage/StorageOptions.cs:6; A/Documents/DocumentValidators.cs:14; API/Program.cs:42; IT/DocumentSqlApiTests.WrongMimeOversizeAndMissingClaimReturnExpectedValidation | Default 50 MiB application cap and 52 MiB multipart transport cap; full-size App Service transport Unverified. | Medium |
| D1 §13 SAS 1h, Azure SDK, new tab, no proxy | Partial | I/Storage/AzureBlobStorageService.cs:48–62; UI/features/claim-detail/claim-documents.component.ts:65 | F14 cached SAS and F15 Compose URL; real Azure Unverified. | Medium |
| D1 §13 local uploads path/local URL | Partial | I/Storage/LocalFileSystemStorageService.cs:7; API/Controllers/LocalStorageController.cs:8 | F20 anonymous/nonexpiring local capability. Development-only exception documented. | Medium |
| D1 §13 IStorageService and both configurable implementations | Yes | A/Abstractions/PersistenceContracts.cs:44 IStorageService; I/DependencyInjection.cs:10 | Interface includes upload, URL, exists, delete. | — |
| D1 §13 DOCUMENT_UPLOADED with RelatedEntityId | Yes | A/Documents/DocumentHandlers.cs:28; IT/DocumentSqlApiTests.UploadSanitizesNameAndListReturnsUploaderUrlAndAudit | Atomic metadata/audit; orphan bytes risk F18. | — |
| D1 §13 PDF/JPEG/PNG/DOCX/XLSX/TXT/CSV allowlist | Yes | A/Storage/DocumentFileRules.cs:5; A/Documents/DocumentValidators.cs:11; AT/Documents/DocumentTests | Extension+MIME allowlist and PDF/JPEG/PNG signatures; no antivirus requirement in specs. | — |

### D1 audit events and integrity (§14)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §14.1 CLAIM_CREATED | Yes | A/Behaviors/DomainEventDispatcher.cs:30; AT/DomainEventAuditTests | Creation event → audit in transaction. | — |
| D1 §14.1 STATUS_CHANGED old/new | Yes | A/Behaviors/DomainEventDispatcher.cs:31; IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | Both reopen edges retained. | — |
| D1 §14.1 PARTY_ADDED | Yes | A/Behaviors/DomainEventDispatcher.cs:32; IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | Related party reference. | — |
| D1 §14.1 PARTY_REMOVED | Yes | A/Behaviors/DomainEventDispatcher.cs:35; IT/ClaimsCoreSqlApiTests.PartiesAreSoftRemovedLastClaimantProtectedAndPostReplayWorks | IsActive change audited. | — |
| D1 §14.1 RESERVE_CREATED | Yes | A/Behaviors/DomainEventDispatcher.cs:51; AT/DomainEventAuditTests | Signed amount/identity retained. | — |
| D1 §14.1 RESERVE_AUTO_APPROVED | Yes | A/Behaviors/DomainEventDispatcher.cs:54; IT/BackendE2eScenarioTests.AutoApprovedReserveRunsGlJobAndWritesBothAuditEvents | Approval event separate from submission. | — |
| D1 §14.1 RESERVE_APPROVED | Yes | A/Behaviors/DomainEventDispatcher.cs:56; IT/BackendE2eScenarioTests.PendingReserveSupervisorApprovalRunsGlPosting | Actor/approval recorded. | — |
| D1 §14.1 RESERVE_REJECTED with reason | Yes | A/Behaviors/DomainEventDispatcher.cs:60; IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | Reason in NewValue per DD-15, overriding D1's OldValue wording. | — |
| D1 §14.1 RESERVE_RETRACTED | Yes | A/Behaviors/DomainEventDispatcher.cs:63; AT/DomainEventAuditTests | Cancelled workflow audited. | — |
| D1 §14.1 GL_POSTING_SIMULATED with journal | Yes | P/Services/BackgroundJobStore.cs:65–73; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | Debit/credit/amount/key JSON. | — |
| D1 §14.1 GL_POSTING_FAILED with reason | Yes | P/Services/BackgroundJobStore.cs:68–73; IT/BackgroundJobSqlTests.TerminalFailureFilterIsIdempotentAndRetryRecoversWithSameKey | One audit for exhausted failure state. | — |
| D1 §14.1 DOCUMENT_UPLOADED related document | Yes | A/Documents/DocumentHandlers.cs:28; IT/DocumentSqlApiTests | RelatedEntityId and type stored. | — |
| D1 §14.1 CLAIM_CLOSED reason | Yes | A/Behaviors/DomainEventDispatcher.cs:38; IT/BackendE2eScenarioTests.FullLifecycleClosesAndReopensToOpen | Reason/justification in JSON. | — |
| D1 §14.1 CLAIM_REOPENED reason | Yes | A/Behaviors/DomainEventDispatcher.cs:40; DT/ClaimTests.BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened | Additional event besides two status events. | — |
| D1 §14.1 SLA_BREACH_DETECTED | Yes | P/Services/BackgroundJobStore.cs:118; IT/BackgroundJobSqlTests.SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus | Dedupe and activity preserved. | — |
| D1 §14.1 VALIDATION_ISSUE_ADDED | Yes | A/Behaviors/DomainEventDispatcher.cs:42; IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | Persisted warnings audited; dry-run writes none. | — |
| D1 §14.2 no app UPDATE/DELETE permissions on audit | Partial | P/Interceptors/AuditSaveChangesInterceptor.cs:1; P/Interceptors/AuditQueryInterceptor.cs:1; infra/sql/runtime-permissions.sql; IT/RuntimePermissionSqlTests | F19 Compose sa bypasses SQL DENY; live Azure principal Unverified. | Medium |
| D1 §14.2 all audit writes through IAuditLogService | Yes | P/Services/PersistenceServices.cs:10 AuditLogService; IT/ArchitectureTests.ProductionAuditWritesAreCentralizedInAuditLogService | Domain, job and explicit handler audit paths inspected. | — |
| D1 §14.2 UTC DATETIMEOFFSET(7) | Yes | CFG:40; P/Interceptors/AuditSaveChangesInterceptor.cs:1; IT/Persistence/PersistenceTests.ShadowsDomainTimestampBridgeAndDecimalPrecisionRoundTrip | Domain timestamp bridge included. | — |
| D1 §14.2 HTTP correlation ID propagated to every request audit | Yes | API/ApiMiddleware.cs:13; IT/CrossCuttingSqlApiTests.AuditReceivesCorrelationAndIdempotencyReplaysOrRejectsMismatch | Job executions have independent system correlation. | — |

### D1 conventions, EF, naming, and migrations (§15)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D1 §15.1 GUID PK, NEWSEQUENTIALID default/sequential generation | Yes | CFG:18; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Domain factories receive generated IDs; migration defaults remain present. | — |
| D1 §15.1 money DECIMAL(19,4), never float/money | Yes | CFG:57; IT/Persistence/PersistenceTests.ShadowsDomainTimestampBridgeAndDecimalPrecisionRoundTrip | Validators reject precision overflow/rounding. | — |
| D1 §15.1 UTC DATETIMEOFFSET(7) timestamps | Yes | CFG:47 and :58; P/Interceptors/DomainTimestampInterceptor.cs:1 | SQL roundtrip tested. | — |
| D1 §15.1 short codes NVARCHAR(50) | Yes | CFG:59; MIG:149 | Per-field exceptions follow §9's explicit lengths. | — |
| D1 §15.1 names NVARCHAR(255) | Yes | CFG:79, :126, :196, :222, :240, :251 | Person first/last use §9-specific 100. | — |
| D1 §15.1 free text NVARCHAR(MAX) | Yes | MIG:157; CFG entity mappings | §9-specific bounded descriptions/notes retained. | — |
| D1 §15.1 boolean BIT NOT NULL DEFAULT 0 | Yes | CFG:64; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Active factory/seed values explicitly true where needed. | — |
| D1 §15.1 soft delete and DeletedAt | Yes | P/ClaimsDbContext.cs:59–96; IT/Persistence/PersistenceTests.TenantAndSoftDeleteFiltersHideRowsAndNumbersAreNeverReused | DD-39 limits to Claims/LossEvents/ClaimParties/ClaimRiskObjects/ClaimDocuments/ClaimValidationIssues; immutable/reference tables intentionally excluded. | — |
| D1 §15.1 four audit columns on all business tables | Yes | CFG:40–51; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Shadow properties satisfy DD-24. | — |
| D1 §15.1 OrganisationId all business tables/fixed seed organisation | Yes | CFG:51; P/ClaimsDbContext.cs:86; P/Seeding/SeedConstants.cs:3; IT/Persistence/PersistenceTests.TenantForeignKeysRejectCrossTenantRelationships | One seeded tenant, tests include another. | — |
| D1 §15.1 aggregate ROWVERSION | Yes | CFG:29; IT/Persistence/PersistenceTests.ClaimRowVersionConflictSurfacesTypedException; ComponentRowVersionProtectsChangesWithoutAClaimUpdate | Claim/component conflicts reload and re-evaluate once. | — |
| D1 §15.2 IEntityTypeConfiguration/Fluent API, no schema annotations | Yes | CFG:70–287; P/ClaimsDbContext.cs:16 | Separate entity configuration classes. | — |
| D1 §15.2 global soft-delete filters | Yes | P/ClaimsDbContext.cs:86–96; IT/Persistence/PersistenceTests.TenantAndSoftDeleteFiltersHideRowsAndNumbersAreNeverReused | Tenant filter every entity; soft-delete set per DD-39. | — |
| D1 §15.2 all decimals HasPrecision(19,4) | Yes | CFG:57; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Model-wide convention. | — |
| D1 §15.2 IsRowVersion concurrency | Yes | CFG:32; IT/Persistence/PersistenceTests.ReserveWorkflowAdvancesRowVersionEvenWhenBalanceIsUnchanged | Workflow-only changes still participate. | — |
| D1 §15.2 GUID defaults | Yes | CFG:22; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Verified in model/migration. | — |
| D1 §15.3 table naming | Yes | CFG:74, :108, :121, :136, :156, :174 | Uses the specification's plural examples; “singular noun” wording is inconsistent with those examples. | — |
| D1 §15.3 PascalCase columns | Yes | MIG:14; CFG | Column-name mappings for Id/Role/Type present. | — |
| D1 §15.3 PascalCase C# entity names | Yes | D/Entities/Claim.cs:10; P/Entities/PersistenceEntities.cs:6 | Singular C# classes map to specified plural tables; equivalent schema permitted by §9. | — |
| D1 §15.3 DTO/Request/Response suffixes | Yes | A/Models/PersistenceDtos.cs:5; A/Claims/ClaimContracts.cs:8 | Commands and typed DTOs consistently distinguished. | — |
| D1 §15.3 kebab-case plural API routes | Yes | API/Controllers/ClaimsController.cs:11; ReferenceController.cs:15; ReservesController.cs:85 | cause-of-loss-codes/retry-posting/reserve-override paths. | — |
| D1 §15.3 VerbNounCommand | Yes | A/Claims/ClaimContracts.cs:8; A/Reserves/ReserveContracts.cs:9 | Existing naming reused. | — |
| D1 §15.3 Get/List query naming | Yes | A/Claims/ClaimContracts.cs:8; A/Reference/ReferenceQueries.cs:9 | SearchPoliciesQuery and read-only ValidateClaimQuery are clear semantic equivalents. | — |
| D1 §15.4 HasData/migration seeding, no startup seed | Yes | P/Seeding/SeedData.cs:23–45; MIG:462 | App startup does not seed application tables. | — |
| D1 §15.4 fresh DB migration reproducibility | Yes | IT/Persistence/PersistenceTests.MigrationAppliesToEmptyDatabaseWithExactSeedsAndNoModelDrift; IT/Persistence/SqlServerFixture.cs:52 | MigrateAsync exercised on fresh local SQL DBs; CLI Azure deployment not run. | — |
| D1 §15.4 startup after migrations, no manual app schema creation | Yes | IT/BackgroundJobSqlTests.RealApiStartupInstallsStorageStartsServerAndRegistersRecurringJobs; API/Program.cs:146 | Requires valid runtime configuration/secrets; Hangfire installer expressly allowed DD-31. | — |
| D1 §15.4 no hard-coded GUIDs in application logic | Yes | P/Seeding/SeedConstants.cs:3; A/Claims/ClaimHandlers.cs:17; P/Services/ClaimNumberGenerator.cs:11 | Fixed reference IDs confined to seeds/tests; tenant comes from authenticated context. | — |

### D2 technology, architecture, database (§§2.3–2.4, 3.2)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D2 §2.3 .NET 9 / C# 13 | Yes | Directory.Build.props:3–5; backend build | Observed SDK 9.0.318. | — |
| D2 §2.3 ASP.NET Core Web API | Yes | API/ClaimsModule.API.csproj; API/Program.cs:146 | Controllers, JSON and middleware configured. | — |
| D2 §2.3 MediatR 12+ under AGENTS 12.x policy | Yes | Directory.Packages.props:14; A/DependencyInjection.cs:10 | 12.5.0 pinned. | — |
| D2 §2.3 EF Core 9 | Yes | Directory.Packages.props:17–18; P/ClaimsDbContext.cs:16 | 9.0.20 pinned. | — |
| D2 §2.3 FluentValidation | Yes | Directory.Packages.props:9–10; A/Behaviors/PipelineBehaviors.cs:30 | 12.1.1 pinned, pipeline enabled. | — |
| D2 §2.3 AutoMapper | Partial | Directory.Packages.props:7; A/Claims/ClaimMappingProfile.cs:8 | Present and correctly layered; F02 security/policy conflict. | Medium |
| D2 §2.3 Hangfire | Yes | Directory.Packages.props:11–13; API/BackgroundJobs.cs:10 | 1.8.25 pinned; SQL server host tested. | — |
| D2 §2.3 SQL Server 2022 or Azure SQL | Yes | docker-compose.yml SQL Server 2022 image; P/DependencyInjection.cs:11 UseSqlServer; IT/Persistence/SqlServerFixture.cs:13 | Real local SQL verified; target 2022 Compose/Azure environment not run. | — |
| D2 §2.3 System.Text.Json or Newtonsoft | Yes | API/Program.cs:146 JSON options; A/Behaviors/DomainEventDispatcher.cs:9 | System.Text.Json used. | — |
| D2 §2.3 Azure Blob Storage | Partial | I/Storage/AzureBlobStorageService.cs:11; Directory.Packages.props:8 | Implementation present; Blob tests skipped/live Azure Unverified. | Medium |
| D2 §2.3 Angular 18+ | Partial | frontend/claims-ui/package.json; package-lock.json; production build | 18.2.14 present; dependency advisories F01. | High |
| D2 §2.3 Angular Material | Yes | frontend/claims-ui/package.json; frontend/claims-ui/src/styles.scss | 18.2.14 theme/components. | — |
| D2 §2.3 Reactive Forms | Yes | UI/features/fnol-intake/fnol-forms.ts:4; UI/shared/party-form/party-form.component.ts:11 | Typed form construction. | — |
| D2 §2.3 selected state management | Yes | UI/core/services/auth.service.ts:7; UI/features/claim-detail/claim-reserves.component.ts:65 | Signals/services per DD-23; reactivity defect F10 scoped separately. | — |
| D2 §2.3 Clean Architecture | Yes | IT/ArchitectureTests.ProductionProjectsOnlyReferenceAllowedProjects | Direct project arrows match AGENTS. | — |
| D2 §2.3 Microsoft Azure deployment | Unverified | infra/provision.ps1; README.md:297 | F07 no actual public environment evidence. | High |
| D2 §2.3 shared/public Git | Unverified | .git; git remote -v (empty) | F07 reviewer URL/access not supplied. Dirty/untracked implementation is part of reviewed scope, not a published commit. | High |
| D2 §2.3 basic CI/CD | Partial | .github/workflows/ci.yml; .github/workflows/deploy.yml | YAML exists; F16 variable-scope issue and no remote run evidence. | Medium |
| D2 §2.4 Domain owns entities/value objects/events/enums; no dependencies | Yes | D/ClaimsModule.Domain.csproj; D/Entities; D/ValueObjects; D/Events; IT/ArchitectureTests | No external packages or project references. | — |
| D2 §2.4 Application owns CQRS/validators/DTOs/interfaces/maps; → Domain only | Yes | A/ClaimsModule.Application.csproj; A/DependencyInjection.cs:10; IT/ArchitectureTests | External services behind Application contracts. | — |
| D2 §2.4 Infrastructure owns storage/Hangfire jobs; → Application only | Yes | I/ClaimsModule.Infrastructure.csproj; I/Storage; I/Jobs; IT/ArchitectureTests | No persistence project reference. | — |
| D2 §2.4 Persistence owns EF/migrations/repositories/UoW; → Application only | Yes | P/ClaimsModule.Persistence.csproj; P/Services/UnitOfWork.cs:11; IT/ArchitectureTests | Transitive Domain types used without illegal direct reference. | — |
| D2 §2.4 API composition/middleware/controllers; four allowed references | Yes | API/ClaimsModule.API.csproj; API/Program.cs:146; IT/ArchitectureTests | Thin controllers send MediatR requests. | — |
| D2 §2.4 every use-case mutation Command/read Query through MediatR | Yes | A/Abstractions/RequestContracts.cs:1; A/Behaviors/PipelineBehaviors.cs:59; AT/CheckpointValidationTests | Host auth/health/file transport and background persistence adapters are documented boundaries. Dry-run POST is logically a Query. | — |
| D2 §2.4 significant state transitions raise Domain Events | Yes | D/Events/DomainEvents.cs:7; A/Behaviors/DomainEventDispatcher.cs:9; AT/DomainEventAuditTests | Events dispatched before commit, audited once. | — |
| D2 §2.4 Unit of Work coordinates persistence | Yes | P/Services/UnitOfWork.cs:39 and :98; IT/Persistence/PersistenceTests.DispatchFailureRollsBackEarlierSavesAndCounterAndDiscardsPostCommitActions | Includes number, aggregate graph, audit; external file compensation F18 remains. | — |
| D2 §3.2 Claims entity/key fields | Yes | D/Entities/Claim.cs:10; CFG:70; MIG:143 | Loss fields normalized into LossEvents; ClaimType deliberately omitted DD-30. | — |
| D2 §3.2 LossEvents | Yes | D/Entities/LossEvent.cs:6; CFG:104 | One-to-one claim loss. | — |
| D2 §3.2 ClaimParties | Yes | D/Entities/ClaimParty.cs:7; CFG:117 | ContactInfo represented by email/phone. | — |
| D2 §3.2 ClaimRiskObjects | Yes | D/Entities/ClaimRiskObject.cs:6; CFG:132 | D1 AssetType naming wins. | — |
| D2 §3.2 ClaimReserveComponents | Yes | D/Entities/ClaimReserveComponent.cs:11; CFG:152 | Approval state belongs to transactions per DD-05. | — |
| D2 §3.2 ReserveHistory | Yes | D/Entities/ReserveHistory.cs:5; CFG:170 | Immutable financial ledger plus workflow metadata. | — |
| D2 §3.2 ClaimDocuments | Yes | P/Entities/PersistenceEntities.cs:46; CFG:189 | External bytes/metadata SQL. | — |
| D2 §3.2 ClaimAuditLog | Yes | P/Entities/PersistenceEntities.cs:60; CFG:203 | Append-only guard and audit service. | — |
| D2 §3.2 CauseOfLossCodes | Yes | P/Entities/PersistenceEntities.cs:27; CFG:245; P/Seeding/SeedData.cs:1 | Active references seeded. | — |
| D2 §3.2 ClaimStatusTransitions | Yes | P/Entities/PersistenceEntities.cs:37; CFG:257; P/Seeding/SeedData.cs:42 | Tenant-specific seeded transition rows are source of truth. | — |
| D2 §3.2 all-table PK/money/time/audit/tenant/soft delete conventions | Yes | IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39; CFG:40 | OrganisationId naming and six-table soft delete per explicit DD-24/DD-39. | — |
| D2 §3.2 EF migrations, no raw runtime app-schema creation | Yes | MIG:14; API/Program.cs:146; API/BackgroundJobs.cs:18 | Hangfire's own installer allowed DD-31; application schema owned by EF. | — |

### D2 API and business rules (§§3.3–3.4)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D2 §3.3 MediatR handler for every command/query | Yes | AT/CheckpointValidationTests.EveryProductionCommandHasRegisteredHandlerAndValidator; A/Reference/ReferenceQueries.cs:9 | Command coverage reflection test plus controller/read inspection. | — |
| D2 §3.3 FluentValidation validator for every command | Yes | AT/CheckpointValidationTests.EveryProductionCommandHasRegisteredHandlerAndValidator | Pipeline invocation verified, not just registration. | — |
| D2 §3.3.1 POST claims | Yes | API/Controllers/ClaimsController.cs:19; IT/ClaimsCoreSqlApiTests.CreateReturnsPersistedDetailWarningsAuditAndIdempotentReplay | Transactional create/event. F06 uncertainty caveat. | — |
| D2 §3.3.1 GET claims filter/page | Yes | API/Controllers/ClaimsController.cs:44; IT/ClaimsCoreSqlApiTests.ListFiltersSearchPaginationAndSortAreAppliedInSql | SQL-side projection/paging. | — |
| D2 §3.3.1 GET claim detail | Yes | API/Controllers/ClaimsController.cs:37; P/Queries/ReadQueries.cs:38 | Required nested detail. | — |
| D2 §3.3.1 PUT status against transitions table | Yes | A/Claims/ClaimTransitionHandlers.cs:36; IT/ClaimsCoreSqlApiTests.StatusApiWalkUsesSeededTransitionsPreflightAndPersistsReopenAudit | DD-11 handler edges/reopen restriction. | — |
| D2 §3.3.1 GET immutable audit | Yes | API/Controllers/ClaimsController.cs:54; P/Queries/ReadQueries.cs:86 | Reverse chronological/page. | — |
| D2 §3.3.1 POST document to Azure plus SQL metadata | Partial | A/Documents/DocumentHandlers.cs:13; I/Storage/AzureBlobStorageService.cs:21 | F18 storage orphan and Blob execution Unverified. | Medium |
| D2 §3.3.1 GET documents with signed URL | Partial | A/Documents/DocumentHandlers.cs:43; I/Storage/AzureBlobStorageService.cs:48 | Azure test skipped; Compose F15. | Medium |
| D2 §3.3.2 simulated policies, no full Policy module | Yes | P/Seeding/SeedData.cs:1; P/Queries/ReadQueries.cs:117 | Five required policies seeded. | — |
| D2 §3.3.2 GET policies/search fields | Yes | API/Controllers/PoliciesController.cs:14; A/Models/PersistenceDtos.cs:5 PolicyDto | Includes expiration/coverages as well. | — |
| D2 §3.3.2 GET policies/{id}/coverage | Yes | API/Controllers/PoliciesController.cs:20; IT/ReferencePolicySqlApiTests | Tenant-scoped/not found behavior; Swagger F17. | — |
| D2 §3.3.3 POST reserve component | Yes | API/Controllers/ReservesController.cs:25; A/Reserves/ReserveSubmission.cs:10 | One component per claim/type, appended transaction. | — |
| D2 §3.3.3 PUT reserve adjustment | Partial | API/Controllers/ReservesController.cs:38; A/Reserves/ReserveHandlers.cs:21 | F05 repeated delta; DD-03 adapter exists. | High |
| D2 §3.3.3 GET reserve balances/status/history | Yes | P/Queries/ReadQueries.cs:72; API/Controllers/ReservesController.cs:18 | Transaction approval state returned. | — |
| D2 §3.3.3 POST approve | Yes | API/Controllers/ReservesController.cs:47; IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Transaction ID per DD-03, not component ID. | — |
| D2 §3.3.3 POST reject/reason | Yes | API/Controllers/ReservesController.cs:60; IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | Transaction ID per DD-03. | — |
| D2 §3.3.4 cause reference/category | Yes | API/Controllers/ReferenceController.cs:15; IT/ReferencePolicySqlApiTests | Active only. | — |
| D2 §3.3.4 statuses/allowed transitions | Yes | API/Controllers/ReferenceController.cs:21; IT/ReferencePolicySqlApiTests | Correct source table. | — |
| D2 §3.4 FluentValidation plus domain rules | Partial | A/Claims/ClaimValidators.cs:12; A/Reserves/ReserveValidators.cs:8; D/Rules/ClaimStateMachine.cs:6; AT/PipelineBehaviorTests | F26: pipeline covers shape/severity, several stateful rules only exist in Domain. | Low |
| D2 §3.4.1 BR-C-01 no future date | Yes | IT/ClaimsCoreSqlApiTests.EveryCriticalRuleReturns422WithFieldErrorsAndNoPersistence | Same as D1 BR-C-01. | — |
| D2 §3.4.1 BR-C-02 policy window warning/audit | Yes | IT/ClaimsCoreSqlApiTests.DryRunAndExpiredPolicyWarningsMatchCreationWithoutWriting | Same as D1 BR-C-02. | — |
| D2 §3.4.1 BR-C-03 claimant required | Yes | DT/ClaimTests.DD10ClaimantIsMandatoryAtCreation | Creation and Open gates. | — |
| D2 §3.4.1 BR-C-04 atomic unique number | Yes | IT/Persistence/PersistenceTests.FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap | Transactional table satisfies stricter no-gap D1. | — |
| D2 §3.4.1 BR-C-05 active cause for organisation | Yes | A/Claims/ClaimValidators.cs:49; P/Queries/ReadQueries.cs:104 | Tenant isolation. | — |
| D2 §3.4.1 BR-C-06 Draft cannot directly close | Yes | DT/ClaimStateMachineTests; D/Rules/ClaimStateMachine.cs:6 | D2 BR-C-06 differs from D1 BR-C-06. DD-14 allows Open→Closed without extra configuration. | — |
| D2 §3.4.2 BR-R-01 positive amount | Yes | D/Entities/ClaimReserveComponent.cs:11; DT/ReserveSubmissionValidationTests | D1/DD-04 signed recovery/adjustment exceptions supersede literal positivity. | — |
| D2 §3.4.2 BR-R-02 <=10k autoapprove/enqueue | Yes | IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi; IT/BackgroundJobSqlTests.ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt | Aggregate-cap pending override exception DD-08. | — |
| D2 §3.4.2 BR-R-03 >10k through100k supervisor | Yes | IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Manager can also approve. | — |
| D2 §3.4.2 BR-R-04 >100k manager | Yes | IT/ReserveSqlApiTests.ThresholdBoundariesAndRoleAuthorityThroughApi | Up to individual 10M ceiling. | — |
| D2 §3.4.2 BR-R-05 GL identity/reentrancy | Yes | IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | Same as D1 BR-R-06. | — |
| D2 §3.4.2 BR-R-06 rejected revised new row retained | Yes | IT/ReserveSqlApiTests.RejectResubmitPutAndRetractRetainImmutableRowsAndAuditReasons | DD-27 allows autoapproval of small revisions; literal “new pending” superseded. | — |
| D2 §3.4.2 BR-R-07 aggregate cap/override | Partial | IT/ReserveSqlApiTests.ConcurrentDifferentComponentsCannotBypassAggregateCap; UI/features/claim-detail/claim-reserves.component.ts:29 | Backend passes, UI F04. | High |

### D2 jobs, storage, frontend, deployment (§§3.5–3.8)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D2 §3.5 GL approval-triggered DR/CR audit, idempotent | Yes | P/Services/BackgroundJobStore.cs:65–73; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | DR Change in Outstanding Reserves / CR Outstanding Loss Reserves. | — |
| D2 §3.5 SLA Draft/Open >48h every15m, flagged and audited | Yes | API/BackgroundJobs.cs:31; P/Services/BackgroundJobStore.cs:92–120 | SlaBreachedAt, not extra status, per D1/DD-18/DD-34. | — |
| D2 §3.6 POST stores Azure organisation/claim container path | Partial | I/Storage/AzureBlobStorageService.cs:21 | Correct source path; integration transport skipped; F18. | Medium |
| D2 §3.6 SAS retrieval 1h | Partial | I/Storage/AzureBlobStorageService.cs:48 | Correct source TTL/Read; live/emulator check Unverified. | Medium |
| D2 §3.6 local fallback when Azure unavailable in local dev | Yes | I/DependencyInjection.cs:10; IT/StorageProviderFallbackTests | Missing/placeholder configuration interpreted per D1 BR-D-03; configured service outage surfaces as error. | — |
| D2 §3.6 configurable provider | Yes | A/Storage/StorageOptions.cs:3; I/DependencyInjection.cs:10 | AzureBlob/LocalFileSystem supported; invalid provider fails. | — |
| D2 §3.7 typed Angular service-only API boundary | Yes | npm run test:http-boundary; UI/core/services/api-services.spec.ts:1 | Passed boundary check and service tests. | — |
| D2 §3.7.1 paginated/filterable dashboard | Partial | UI/features/claims-list/claims-list.component.ts:30–43 | F09–F11. | Medium |
| D2 §3.7.1 status/date/assignee/cause filters | Partial | UI/features/claims-list/claims-list.component.html:1; .ts:28 and :41 | Date and assignee defects F09/F10. | Medium |
| D2 §3.7.1 required six columns | Yes | UI/features/claims-list/claims-list.component.ts:26 | Includes additional D1 cause column. | — |
| D2 §3.7.1 color-coded status badges | Yes | UI/shared/status-badge/status-badge.component.scss:1; status-badge.component.spec.ts | All seven states. | — |
| D2 §3.7.1 row navigation | Yes | UI/features/claims-list/claims-list.component.ts:39 | Detail route. | — |
| D2 §3.7.1 Log New Claim button | Yes | UI/features/claims-list/claims-list.component.html:1 | FNOL route. | — |
| D2 §3.7.2 three-step reactive FNOL | Yes | UI/features/fnol-intake/fnol-intake.component.html:1; fnol-intake.component.spec.ts | Material stepper. | — |
| D2 §3.7.2 step1 policy/loss/cause/description/location | Partial | UI/features/fnol-intake/loss-step.component.ts:17 | Fields present; F12 policy error recovery. | Medium |
| D2 §3.7.2 step2 claimant/additional parties/risks | Yes | UI/features/fnol-intake/parties-step.component.ts:12; fnol-forms.spec.ts | Required claimant, optional risks with advisory. | — |
| D2 §3.7.2 step3 initial reserve/authority/review | Yes | UI/features/fnol-intake/review-step.component.ts:13; UI/shared/authority/authority-indicator.pipe.spec.ts:1 | Threshold boundaries tested. | — |
| D2 §3.7.2 validate each step before advancing | Yes | UI/features/fnol-intake/fnol-intake.component.html:1; fnol-forms.spec.ts | Typed groups with independent validation. | — |
| D2 §3.7.2 Material inline validation messages | Yes | UI/features/fnol-intake/loss-step.component.ts:40; review-step.component.ts:23; UI/shared/party-form/party-form.component.ts:11 | FNOL supports inline server and client errors; general detail gaps F13. | — |
| D2 §3.7.2 success ClaimNumber/detail link | Yes | UI/features/fnol-intake/fnol-intake.component.ts:18; UI/features/claim-detail/claim-detail.component.ts:115 | Immediate navigation plus snackbar satisfies D1 clarification. | — |
| D2 §3.7.3 detail header fields | Yes | UI/features/claim-detail/claim-header.component.ts:11 | All named fields displayed. | — |
| D2 §3.7.3 five tabs/sections | Yes | UI/features/claim-detail/claim-detail.component.ts:38–43 | All present. | — |
| D2 §3.7.3 reserves/components/approval/status/Add form | Partial | UI/features/claim-detail/claim-reserves.component.ts:27–50 | Core present; manager override F04, pending/errors F13. | High |
| D2 §3.7.3 documents list/download/upload picker | Partial | UI/features/claim-detail/claim-documents.component.ts:21–28 and :65 | Cached download F14; Compose F15. | Medium |
| D2 §3.7.3 immutable reverse-chronological audit | Yes | UI/features/claim-detail/claim-audit.component.ts:9; P/Queries/ReadQueries.cs:91 | No mutation controls/API. | — |
| D2 §3.7.3 contextual transitions/confirmation | Yes | UI/features/claim-detail/claim-detail.component.ts:72; transition-dialog.component.ts | Role-filtered edges and server preflight. | — |
| D2 §3.7.4 lazy routing | Yes | UI/app.routes.ts:3 | DD-23 permits standalone components. | — |
| D2 §3.7.4 JWT mockauth/Bearer/logged-in context | Partial | UI/core/services/auth.service.ts:35; UI/core/interceptors/auth.interceptor.ts:5; UI/app.component.html:1 | F03 initial race/expired session. | High |
| D2 §3.7.4 role-gated approval buttons | Yes | UI/features/claim-detail/claim-reserves.component.ts:35; UI/core/services/auth.service.ts:66 | Backed by API authorization. | — |
| D2 §3.7.4 Material theme/custom primary | Yes | frontend/claims-ui/src/styles.scss:1 | Custom primary palette. | — |
| D2 §3.7.4 all HTTP errors user-friendly snackbar | Partial | UI/core/services/error-notification.service.ts:9; UI/core/interceptors/error.interceptor.ts:6 | Notifications present, F12 search recovery missing. | Medium |
| D2 §3.7.4 loading spinner/skeleton for async | Partial | UI/core/interceptors/loading.interceptor.ts:6; UI/shared/loading/loading-indicator.component.ts:4; UI/app.component.html:1 | Many loaders; some operations only global progressbar and no pending guards, F13. | Medium |
| D2 §3.8 publicly accessible Microsoft Azure solution | Unverified | README.md:297; infra/README-deploy.md | F07 no actual URL/deployment evidence. | High |
| D2 §3.8 API App Service/Container Apps resource | Unverified | infra/provision.ps1 App Service provisioning | Script targets Linux .NET9; not run in this audit. F07. | High |
| D2 §3.8 frontend SWA/second App Service resource | Unverified | infra/provision.ps1 Static Web App provisioning | F07 no live resource. | High |
| D2 §3.8 Azure SQL resource | Unverified | infra/provision.ps1 SQL server/database provisioning | Local SQL passed; Azure connectivity/permissions unverified. F07. | High |
| D2 §3.8 Azure Blob resource | Unverified | infra/provision.ps1 private container provisioning | F07 private-container/live SAS behavior not exercised. | High |
| D2 §3.8 Key Vault optional | N/A | infra/provision.ps1 optional Key Vault; infra/README-deploy.md:56 | Optional script support exists; not required to mark complete. | — |
| D2 §3.8 working public review app (free/trial allowed) | Unverified | README.md:297 | F07 working deployment cannot be inferred from YAML. | High |
| D2 §3.8 pipeline builds backend, migrates, builds frontend, deploys both | Partial | .github/workflows/deploy.yml | All stages defined; F16 settings issue; actual run Unverified. Manual trigger is permitted. | Medium |

### D2 deliverables (§4)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D2 §4.1 accessible Git repository | Unverified | git remote -v empty; README.md:297 | F07 URL/access not supplied. | High |
| D2 §4.1 full backend solution | Yes | backend/ClaimsModule.sln; backend build/test results | Five production and three test projects present. | — |
| D2 §4.1 full Angular project | Yes | frontend/claims-ui/angular.json; package.json; production build | Buildable project, open defects listed above. | — |
| D2 §4.1 EF migration files | Yes | MIG:14; P/Migrations/ClaimsDbContextModelSnapshot.cs:1; fresh migration test | Initial migration/model consistent. | — |
| D2 §4.1 SQL seed scripts | Partial | P/Seeding/SeedData.cs:1; MIG:462; .github/workflows/deploy.yml | F23 migration seeding present, separate SQL seed artifact missing. | Low |
| D2 §4.1 optional docker-compose/equivalent | Partial | docker-compose.yml; backend/Dockerfile; frontend/claims-ui/Dockerfile | Defined, but F15 Blob URL and F19 credentials; Docker unavailable. | Medium |
| D2 §4.1 CI/CD YAML | Partial | .github/workflows/ci.yml; deploy.yml | Definition present; F16 and no executed deployment evidence. | Medium |
| D2 §4.1 README setup | Yes | README.md:24–144 | Covers host and Compose paths. | — |
| D2 §4.2 prerequisites SDK/tools/environment | Yes | README.md:24–31; global.json; Directory.Build.props | Exact stack documented; local Node24 differs from recommended/CI20. | — |
| D2 §4.2 step-by-step local setup | Partial | README.md:35–130 | Host path documented; Compose download F15. | Medium |
| D2 §4.2 all required config/example values | Partial | README.md:146–207; .env.example; API/appsettings.json | Broad coverage; production variable scope F16. Examples are demo fixtures/placeholders. | Medium |
| D2 §4.2 migrations and seeds application | Yes | README.md:106 and :132–144 | EF instructions and seed ownership explicit. | — |
| D2 §4.2 deployed Azure resources/reproduction | Partial | README.md:272–301; infra/README-deploy.md; infra/provision.ps1 | Reproduction present, actual deployed-resource description/URLs Unverified F07. | High |
| D2 §4.2 application walkthrough | Yes | README.md:234–245 | FNOL/reserves/documents/status/audit steps. | — |
| D2 §4.3 public backend URL and Swagger accessible | Unverified | README.md:297 | F07 no actual URL. | High |
| D2 §4.3 public frontend URL | Unverified | README.md:297 | F07 no actual URL. | High |
| D2 §4.3 at least handler and supervisor test accounts | Partial | README.md:210; P/Seeding/SeedData.cs:45; API/Program.cs:86 | Three local seeded users; deployed credentials/login unverified F07. | High |
| D2 §4.3 app running at review time | Unverified | README.md:297 | F07 requires external smoke check. | High |
| D2 §4.4 solution structure diagram/description | Yes | ARCHITECTURE.md:3–44 | References match tested project graph. | — |
| D2 §4.4 data model/key relationships | Partial | ARCHITECTURE.md:83–123; CFG | Model documented; F21 history display statement incorrect. | Low |
| D2 §4.4 CQRS API→Application→Persistence flow | Yes | ARCHITECTURE.md:46; A/Behaviors/PipelineBehaviors.cs:7 | Precommit audit/postcommit enqueue described. | — |
| D2 §4.4 domain events and handlers | Yes | ARCHITECTURE.md domain-events table; A/Behaviors/DomainEventDispatcher.cs:9 | Matches concrete event mappings. | — |
| D2 §4.4 Hangfire design/triggers/idempotency | Yes | ARCHITECTURE.md background-processing section; IT/BackgroundJobSqlTests | GL/SLA/recovery included. Request replay limits F05/F06 distinct from GL. | — |
| D2 §4.4 Azure architecture/services/interactions | Yes | ARCHITECTURE.md:198–217 | Design accurate as intended architecture; not live deployment proof. | — |
| D2 §4.4 design decisions/tradeoffs | Partial | ARCHITECTURE.md:219; docs/DESIGN-DECISIONS.md; addendum | F05/DD-32 and F21/DD-36 contradictions remain. | High |
| D2 §4.5 AI tools used | Partial | AI-WORKFLOW.md:5 | Codex named; human completeness/model account placeholder, F08. | Medium |
| D2 §4.5 workflow/context/prompt sequencing/iteration | Partial | AI-WORKFLOW.md:12–40 | Repository-based explanation exists; actual iteration evidence incomplete F08. | Medium |
| D2 §4.5 at least three representative prompts/purpose | No | AI-WORKFLOW.md:44–58 | Three placeholders, F08. | Medium |
| D2 §4.5 AI-generated versus manual work | No | AI-WORKFLOW.md:69 | Authorship placeholder, F08. | Medium |
| D2 §4.5 at least two incorrect-output corrections | No | AI-WORKFLOW.md:71–81 | Two placeholders, F08. | Medium |
| D2 §4.5 honest assessment of AI benefits | No | AI-WORKFLOW.md:85–87 | Human assessment placeholder, F08. | Medium |
| D2 §4.5 link/export of conversation history | No | AI-WORKFLOW.md:89–93; docs/ai-log/README.md:3 | No history export/link present, F08. | Medium |
| D2 §4.6 substantive AI interaction history evidence | No | docs/ai-log/README.md | Instructions only; no transcript/screenshots/share link. F08. | Medium |

### D2 evaluation and live review (§§5–8, including each §6.1 item)

| Requirement | Implemented | Evidence (file:line / test name) | Missing/Incorrect | Severity |
| --- | --- | --- | --- | --- |
| D2 §5.1 AI-assisted workflow expected | Partial | AI-WORKFLOW.md:5–40 | Claimed tools/context documented, substantive evidence F08. | Medium |
| D2 §5.2 recommended five-phase approach | N/A | docs/spec/spec-2-assessment.md:425 | Guidance, not mandatory implementation requirements; honest history needed for assessment. | — |
| D2 §5.3 own and validate AI output | Partial | docs/review/backend-checkpoint.md; this audit's verification | Tests/reviews provide technical evidence, authorship/refinement account F08. | Medium |
| D2 §6 Engineering Quality & Architecture, 35% | Partial | IT/ArchitectureTests; IT/Persistence/PersistenceTests; AT/PipelineBehaviorTests | Strong layer/schema/domain evidence; F02/F05/F06/F17/F18/F19/F25 remain. No numeric assessment score invented. | High |
| D2 §6 AI-assisted Delivery Effectiveness, 25% | Partial | AI-WORKFLOW.md; docs/ai-log/README.md | F08 required evidence missing. | Medium |
| D2 §6 Fullstack Ownership & Delivery, 25% | Partial | Build/test results; .github/workflows/deploy.yml; README.md:297 | F03/F04 UI, F07 deployment, F15/F16 delivery concerns. | High |
| D2 §6 Domain & Product Thinking, 15% | Partial | DT/ClaimStateMachineTests; DT/ReserveComponentTests; IT/ReserveSqlApiTests | Strong rule/edge testing; F04–F06 financial workflow and retry behavior remain. | High |
| D2 §6.1 reads/writes separated with MediatR | Yes | A/Abstractions/RequestContracts.cs:1; AT/PipelineBehaviorTests.QuerySkipsTransactionAndCommandUsesUnitOfWork | Commands own transaction; queries no write transaction. | — |
| D2 §6.1 Domain Events raised/handled correctly | Yes | AT/DomainEventAuditTests.EveryDomainEventHasAnAuditMappingWithJsonValues; IT/Persistence/PersistenceTests.DomainEventsRunBeforeCommitAndPostCommitActionsSeeCommittedData | Audit before commit, GL after. | — |
| D2 §6.1 FluentValidation pipeline-level | Yes | A/DependencyInjection.cs:10; AT/PipelineBehaviorTests.ErrorStopsHandlerWithValidationException | Warnings collected without throwing. | — |
| D2 §6.1 EF conversions/shadows/global filters | Yes | CFG:40; P/ClaimsDbContext.cs:86; IT/Persistence/PersistenceTests.AllFifteenMappingsFollowD1AndDD39 | Enum strings, JSON comparer, all tenant filters, selected soft delete. | — |
| D2 §6.1 UoW transactions across repositories | Yes | P/Services/UnitOfWork.cs:11; IT/Persistence/PersistenceTests.DispatchFailureRollsBackEarlierSavesAndCounterAndDiscardsPostCommitActions | Atomic database writes; storage compensation separate F18. | — |
| D2 §6.1 AutoMapper profiles in Application | Yes | A/Claims/ClaimMappingProfile.cs:8; AT/CheckpointValidationTests | No API mapping profile. | — |
| D2 §6.1 Hangfire idempotency keys | Yes | D/Entities/ReserveHistory.cs:5; IT/BackgroundJobSqlTests.SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate | Real SQL parallel delivery test passed. | — |
| D2 §6.1 Azure Blob behind interface/testable | Yes | A/Abstractions/PersistenceContracts.cs:44 IStorageService; I/Storage/AzureBlobStorageService.cs:11; AT/Documents/DocumentTests | Real Blob transport unverified here, mock/local tests passed. | — |
| D2 §6.2 well-reasoned architecture | Partial | ARCHITECTURE.md; docs/DESIGN-DECISIONS.md | F05/F21 discrepancies; rest substantially aligned. | High |
| D2 §6.2 AI correction evidence | No | AI-WORKFLOW.md:71 | F08 placeholders. | Medium |
| D2 §6.2 rich insurance domain model | Yes | D/Entities/Claim.cs:10; ClaimReserveComponent.cs; ReserveHistory.cs; DT tests | Behavior/private setters/invariants, not CRUD-only data bags. | — |
| D2 §6.2 structured logging/consistent middleware errors | Yes | API/Program.cs:16; API/ApiMiddleware.cs:13; IT/CrossCuttingApiTests.StructuredLogContextContainsCorrelationIdAndAuthenticatedUserId | Serilog JSON and correlation/user context. | — |
| D2 §6.2 working Azure/complete Swagger/no console errors | Unverified | README.md:297; F03/F17 evidence | F07 live UI/API not exercised; schema gap established statically. | High |
| D2 §6.2 consistency/naming/maintainability | Partial | Project layout; CFG; UI/features/claims-list/claims-list.component.ts:23 | F25 dense UI/analyzer debt; no broad rewrite warranted by review. | Low |
| D2 §7.1 local environment/repository/deployed app ready for 90-minute review | Partial | Passing local builds/tests; README.md:297 | F07 deployment/repository sharing and Blob/Docker environment incomplete. | High |
| D2 §7.2 architecture walkthrough readiness | Partial | ARCHITECTURE.md; project source | Materials exist; human ability to explain cannot be verified from files. | Low |
| D2 §7.2 AI workflow walkthrough/history | No | AI-WORKFLOW.md:89; docs/ai-log/README.md | F08 evidence absent. | Medium |
| D2 §7.2 deployed FNOL/reserve/approval/document/audit demo | Unverified | IT/BackendE2eScenarioTests; README.md:297 | API scenarios pass locally; F07 live demo and F03/F04/F14 UI concerns. | High |
| D2 §7.2 deep-dive/live small change ability | Unverified | Tests/source/documentation available | Future human evaluation, no live exercise performed or code changed in this review. | Low |
| D2 §7.3 ownership/reasoning/AI maturity/problem solving | Unverified | docs/spec/spec-2-assessment.md:558; AI-WORKFLOW.md | Requires live review; technical artifact quality alone cannot prove human capability. | Low |
| D2 §7.4 authenticity and tradeoff explanation | Unverified | AI-WORKFLOW.md:61–93 | Do not invent human actions or authorship; F08 evidence needed. | Medium |
| D2 §8.1 recruiter timeline/live-review scheduling | N/A | docs/spec/spec-2-assessment.md:582 | No recruiter deadline provided; not a code property. | — |
| D2 §8.2 repository, frontend, backend/Swagger, users, AI-history package | Partial | README.md:210 and :297; AI-WORKFLOW.md:89 | F07 URLs/access and F08 history missing/unverified. | High |
| D2 §8.3–8.4 scope guidance/questions | N/A | docs/spec/spec-2-assessment.md:604–613 | Advisory process guidance; audit does not contact recruiter. | — |
| D2 Appendix A condensed statuses/components/thresholds/conventions | Yes | D/Enums/ClaimEnums.cs:1; D/Rules/ReserveAuthorityPolicy.cs:7; CFG; docs/DESIGN-DECISIONS.md | D1 and DD-02/04/05/14/24/39 resolve appendix inconsistencies; not an additional conflicting component set. | — |

## Required trap checks

| Trap | Outcome | Evidence and limits |
| --- | --- | --- |
| Claim numbers remain gap-free on rollback | Verified | IT/Persistence/PersistenceTests.FiftyParallelFirstAllocationsAreUniqueContiguousAndRollbackLeavesNoGap and DispatchFailureRollsBackEarlierSavesAndCounterAndDiscardsPostCommitActions passed. Counter update is inside claim transaction; soft deletion never returns a number. |
| GL enqueued only after commit | Verified | IT/BackgroundJobSqlTests.ApprovalNotificationEnqueuesAfterCommitAndRollbackDiscardsIt; P/Services/UnitOfWork.cs:56–78. Failed post-commit enqueue does not undo the commit; DD-33 recovery scans unscheduled rows. |
| GL parallel execution idempotent | Verified | SequentialAndParallelPostingHasOneAuditAndOneWorkflowUpdate exercises real SQL and concurrent deliveries. Conditional update, claim lock, audit transaction, and unique key align. |
| Retry exhaustion sets Failed and GL_POSTING_FAILED once | Verified | StatePipelineOnlyRecordsFailureAfterDefaultRetriesAreExhausted and TerminalFailureFilterIsIdempotentAndRetryRecoversWithSameKey passed. Retryable Scheduled states do not record terminal failure. |
| SLA never alters UpdatedAt or Status | Verified | SlaCrossTenantParallelScanDedupeAndExpiryPreserveActivityAndStatus and SlaBulkUpdateAndAuditInsertDoNotBumpClaimActivity passed. Bulk setter only writes SlaBreachedAt; audit save does not touch claim activity. |
| Audit append-only enforced | Partial | EF tracked and bulk mutation guards verified; restricted SQL principal DENY verified. Default Compose uses sa and bypasses SQL permission protection (F19); Azure identity unverified. |
| Tenant + soft-delete filters on all entities | Verified within DD-39 | All 15 business mappings have tenant filtering. The six explicitly removable entities also have IsDeleted/DeletedAt filters; immutable ledger/audit/reference/technical tables have deliberate exceptions. Job discovery uses IgnoreQueryFilters only with exact identity/tenant rediscovery or cross-tenant scan, excluding deleted claims and rechecking under a tenant context. |
| Pending reserve financial immutability | Verified | IT/Persistence/PersistenceTests.ReserveFinancialFieldsAndDeletionAreRejected; private financial setters; new row for revision; lifecycle metadata changes allowed by DD-06. |
| 10M manager override gating | Backend verified; UI gap | AggregateWarningForcesPendingUntilManagerOverrideAndIndividualCeilingRemains and ConcurrentDifferentComponentsCannotBypassAggregateCap passed. F04 blocks a normal fresh manager UI visit. |
| Reopen ends Open | Verified | Two atomic transitions plus CLAIM_REOPENED; failed second-leg preflight leaves Closed. Domain and HTTP scenario tests passed. |
| No component HttpClient outside services | Verified | npm run test:http-boundary passed; component/service imports inspected. Interceptors are cross-cutting transport functions, not component HttpClient usage. |
| MediatR/AutoMapper/FluentAssertions licensing pins | Policy pins verified, residual advisory | MediatR 12.5.0; AutoMapper 14.0.0; no FluentAssertions or Moq dependency found. FluentValidation 12.1.1; xUnit/Shouldly/NSubstitute present. F02 remains a security-versus-version-policy conflict, not a license-pin violation. |
| 50 MiB upload works on App Service | Unverified | App/server/form/action limits and small configured-cap rejection tests inspected/passed. No live App Service endpoint or actual 50 MiB upload smoke test. A 52 MiB multipart request allowance does not prove every upstream proxy accepts it. |
| No committed secrets | No real secret found in reviewed scope | Heuristic scan of current tracked/untracked nonignored files and all three reachable Git commits; matches reviewed as placeholders, emulator/demo fixtures, or test signing keys. No private key/token/publish profile/real Azure credential found. Not proof about inaccessible remote/deleted history or external settings. |

## Explicit cross-cutting assessment

### Functionality, edge cases, and validation

The deepest verified behavior is in reserves/statuses rather than merely happy-path HTTP status assertions. Tests check exact threshold boundaries, signed recovery/adjustment/reversal rules, zero/precision rejection, pending balance exclusion, approval-order balances, self-approval, last-claimant protection, unknown-policy financial blocking, all state-machine edges, reopening atomicity, warning acknowledgement, and cross-component aggregate-cap concurrency. Important untested failure windows remain F05/F06/F18. No change to the accepted cap/netting/signed-delta decisions is proposed without a decision update.

FluentValidation runs before the transaction, with Warning failures collected and Error failures raised. Stateful balance/role/lifecycle checks are re-evaluated by Domain inside the transaction after loading current aggregates, including the one-time concurrency retry. This preserves transaction-time enforcement, but the literal requirement to also express those rules in FluentValidation remains the Low-severity documentation/placement gap F26. Critical is the application's business severity; FluentValidation uses Error to block it. Warnings persist as ClaimValidationIssue rows and audit events on creation; dry-run validation does not write.

### Architecture, CQRS, and code quality

The direct project-reference graph matches every AGENTS arrow and passes ArchitectureTests. Domain has no package/reference dependency. Application owns DTOs, validators, MediatR contracts, mapping profiles and external interfaces. Persistence contains database implementations and query projections; Infrastructure contains external storage/job definitions. API composes them and forwards claim/reserve use cases through ISender. Controller logic is chiefly DTO/binding/status/Location adaptation; host authentication, health, Swagger and Development file transport are documented host concerns.

Read queries are separate from write repositories and do not track entity graphs. Commands are transaction wrapped; event/audit writes are pre-commit and job submission is post-commit. Background stores are specialized Application-interface implementations, not controllers reaching into EF. There is no broad need to replace the architecture.

F25 covers dense frontend formatting, repeated subscription/state patterns, and search analyzer warnings. A few mapping/helper extension points are currently lightly used; no dangerous dead code path was established. Error and transition contracts are mostly coherent, but Swagger success schemas need F17. Duplicate validation in UI and backend is intentional enforcement at different trust boundaries; stateful domain invariants remain server-side.

### Security and endpoint authorization inventory

| Endpoint/surface | Actual authorization | Evidence / qualification |
| --- | --- | --- |
| POST /api/claims; POST /api/claims/validate | HandlerOrAbove | API/Controllers/ClaimsController.cs:12. Dry run is read-only despite POST. |
| GET /api/claims; GET /api/claims/{id}; GET audit | HandlerOrAbove | Same controller policy; tenant-filtered queries; cross-tenant API tests passed. |
| POST/DELETE claim parties | HandlerOrAbove | Same controller policy; owning claim and party looked up in tenant. |
| POST/GET claim documents | HandlerOrAbove | Same controller policy; owning claim loaded before storage/metadata access. |
| PUT notes/policy/liability/status; GET transition preflight | HandlerOrAbove plus domain transition restrictions | ClaimsController; supervisor-only reopen enforced in seeded state machine; invalid operation returns 422/403. |
| PUT claim assignee | SupervisorOrAbove | API/Controllers/ClaimsController.cs:135; chosen user must be visible to tenant. |
| GET/POST claim reserves; PUT reserve adjustment | HandlerOrAbove | API/Controllers/ReservesController.cs:11; state/amount/domain checks; F05/F06 replay gaps. |
| POST reserve approve/reject | SupervisorOrAbove plus authority/self checks | ReservesController.cs:48 and :61; Domain authority checks survive forged UI calls. |
| POST reserve retract | HandlerOrAbove plus submitter identity | ReservesController.cs:73; Domain requires pending and original submitter. |
| POST retry-posting | HandlerOrAbove | ReservesController.cs:85; own tenant and eligible Failed row; all claims roles intentionally allowed. |
| POST reserve-override | ManagerOnly | ReservesController.cs:98; handler repeats manager enforcement. |
| GET reference causes/statuses/users | Authenticated | API/Controllers/ReferenceController.cs:9; data filters tenant; optional exact role filter for users. |
| GET policy search/coverage | Authenticated | API/Controllers/PoliciesController.cs:8; tenant-filtered reference reads. |
| POST /api/auth/dev-login | Anonymous only when Auth:DevLoginEnabled is true | API/Program.cs:86. Base appsettings disables it; Development enables; Azure runbook intentionally enables for the assessment. Anyone can select manager in this demo mode. This matches DD-19/mock-auth scope; it is not production identity assurance. |
| GET /api/storage/local/{path} | Anonymous, Development-only | API/Controllers/LocalStorageController.cs:8–16; F20 no tenant check/TTL. Production returns 404. |
| /health | Public | API/Program.cs:82; returns simple health DTO, not credentials. |
| /swagger and OpenAPI JSON | Public | API/Program.cs:79; public Swagger is a D2 deliverable. Does not authorize protected business operations. |
| /hangfire when enabled | Authenticated Manager; otherwise not mapped | API/BackgroundJobs.cs:37–49; disabled by default. |
| Direct Blob download | Bearer capability via read-only SAS | I/Storage/AzureBlobStorageService.cs:48. API authenticates URL issuance; possession of URL intentionally permits download until expiry. |

JWT issuer/audience/signature/lifetime validation and unset/short signing-key rejection are tested. Cross-tenant reads/writes and composite tenant foreign keys are covered by SQL tests. No SQL string interpolation of user values into nonparameterized commands was found: counter and claim-lock SQL use parameters/interpolated EF APIs. GUID path components, basename sanitization, traversal rejection, MIME/extension allowlist, selected magic-byte checks, size validation, private-container creation and collision handling provide upload defenses. No malware scan is specified or implemented; do not equate an allowlist with content sanitization. HTML/script extensions are not in the allowlist.

Development demo credentials/emulator keys are published fixtures, not confidential secrets. Real signing keys and connection strings must be supplied externally; placeholder JWT keys now fail startup/token creation, so the historical backend-checkpoint signing-key issue is not repeated as an open defect. Secret scanning was heuristic, not a formal credential-service scan; external settings and unreachable history remain unverified. Configuring Auth:DevLoginEnabled=true on a public assessment environment deliberately permits role impersonation and must remain limited to demo data.

### Performance and database behavior

The claims list uses AsNoTracking, CountAsync, SQL filtering, deterministic ordering, Skip/Take and a projected reserve SUM; it does not enumerate claims then fetch reserves per row. Related detail data is loaded with a bounded number of separate queries rather than an N+1 per child. Full reserve histories/document lists remain intentionally unpaginated because the specifications request full histories; large-claim memory/response size is unmeasured. Some existence checks load the full claim aggregate through IClaimRepository, a potential optimization target if profiles justify it.

Indexes cover tenant+ClaimNumber uniqueness, status, UpdatedAt, claim/component uniqueness, reserve key/sequence, audit claim/time, references and foreign keys. There is no dedicated LossDate index or demonstrated tenant+created-order workload index; policy search uses contains/ToLower and will not generally seek a plain index efficiently. F25 recommends measured tuning, not declaring a latency failure without data. Counter locking intentionally serializes a tenant/year allocation point to guarantee no rollback gaps. Jobs lock one claim while checking/updating to prevent concurrent duplicate effects; load-level throughput/lock duration is unverified.

The real migration created clean test databases, exact reference/demo seeds, all fifteen mappings, expected precision/defaults/enum conversions/shadows/rowversions, and no model drift. EF reports typed concurrency failures; the UoW reloads/re-evaluates an operation once and handles counter races separately. Seed constants remain in Persistence; controllers do not embed tenant GUIDs. Global filters and tenant-qualified foreign keys protect normal queries and relationships. Job discovery's intentional filter bypasses are re-scoped before writes. SQL audit DENY was verified using the restricted role, while F19 prevents asserting that the default Compose runtime has this protection.

### Tests, error contracts, and backward compatibility

The passing suites include meaningful stored-state/audit assertions and real SQL concurrency cases, not only mocks of their own implementation. GL tests run the actual retry-state filter pipeline; number tests perform many concurrent allocations and rollback; permission tests exercise SQL denial directly. Frontend tests cover forms, service request shapes, role gating, financial tabs, interceptors and shared UI. They do not cover cold-login latency, reordered list responses, a reloaded cap-blocked manager workflow, one-hour SAS expiry, or browser-to-Compose Blob DNS. Those missing scenarios explain why passing tests do not clear F03/F04/F09–F16.

No flaky failure was observed in the successful run. Some integration tests depend on wall-clock time and startup timing (BackgroundJobSqlTests polls for a server for ten seconds; SQL concurrency tests have bounded timeouts), so one pass cannot establish long-term flake freedom. Domain tests use injected clocks. Several API fixtures use yesterday's real date with fixed policy seeds; future dates and slower CI hosts warrant explicit regression review rather than a claim of guaranteed stability. SQL/Azurite facts can skip without supporting services; CI should make required service execution visible so a green unit-only run is not mistaken for full integration coverage.

Shared middleware normalizes business and transport errors and includes correlation identifiers without exposing exception details in production. Actual endpoint status-code/shape assertions pass in the API tests; OpenAPI schema parity has not been exhaustively tested and has the known F17 omissions. POST replay currently stores status/body but not original Location and other headers; clients must not assume header-identical replay. This is a documentation/contract limitation within the broader F06 replay work, rather than an additional counted finding.

This is a greenfield repository with no supplied released client/database baseline. Required D1 routes are present, D2's PUT adjustment is present as the DD-03 adapter, and extra routes are additive. No compatibility regression against a previous release can be asserted or ruled out. F05 is a concrete HTTP retry-semantics defect regardless of release history. No source/API/schema changes were made by this audit.

## Contradictions and accepted overrides

| Documents / decision | Implementation evidence | Assessment |
| --- | --- | --- |
| DD-03 says PUT appends an Adjust transaction; DD-32 says PUT/DELETE naturally idempotent | A/Reserves/ReserveHandlers.cs:21; API/IdempotencyMiddleware.cs:15 | F05: replaying PUT appends another delta. Both statements cannot describe a retry-safe financial PUT contract without an additional mechanism. Preserve the decision conflict; do not silently invent new semantics. |
| DD-32 key scope says organisation/user/method/route template; index says organisation/user/key | API/IdempotencyMiddleware.cs:49–66; CFG:285; addendum:8 | Literal implementation follows the specified unique index and compares method/template/hash afterwards. The same key is user-wide, not independently reusable per operation; concrete route IDs are not hashed. Reusing a key/body on another claim with the same route template can replay the earlier response. This is a documented design limitation to resolve alongside F05/F06, not a hidden tenant leak. |
| DD-32 delete record on exception versus reliable replay after a committed command | API/IdempotencyMiddleware.cs:137–149; P/Services/UnitOfWork.cs:56 | F06: implementation follows cleanup wording but creates post-commit replay/crash gaps. A refinement must distinguish committed operation from failed delivery. |
| DD-36 “History is displayed ordered by ChangeSequence” | UI/features/claim-detail/claim-reserves.component.ts:71 | F21: frontend resorts by CreatedAt; read query follows sequence. |
| DD-08 manager override workflow | UI/features/claim-detail/claim-reserves.component.ts:29 and :101 | F04: server conforms, fresh manager screen cannot perform the required action. |
| README “Swagger documents the complete current HTTP surface” | README.md:245; API/Controllers/PoliciesController.cs:16; ReferenceController.cs:17; ClaimsController.cs:160 | Routes are surfaced, but success schemas are incomplete (F17). Narrow wording or complete the contracts. |
| README repository/environment deployment variables and protected production environment | README.md:197 and :289; deploy.yml build-test versus deploy jobs | F16: environment-only PRODUCTION_API_URL does not reach build-test. |
| README fastest Compose start/document walkthrough | README.md:35–65 and :242; docker-compose.yml:54 | F15: returned SAS host is internal to Compose; ordinary host browser download fails by configuration. Docker run not reproduced. |
| ARCHITECTURE “UI history is shown by change sequence” | ARCHITECTURE.md:116; claim-reserves.component.ts:71 | F21 direct contradiction. |
| ARCHITECTURE restricted runtime SQL role | ARCHITECTURE.md:217; docker-compose.yml API connection | The architecture explicitly says operator-managed/convention; no false claim of automatically installed production identity. Compose sa remains F19; Azure is unverified. |
| README explicitly no live URLs, anonymous local route, AI report scaffold | README.md:297 and :301; AI-WORKFLOW.md:3 | Honest limitations, not contradictions. They still leave D2 delivery work outstanding. |
| Historical topic assertions | docs/CLAIMS-CORE.md:11; docs/BACKGROUND-JOBS.md:3; docs/PERSISTENCE.md:32 | F24: inactive parties now returned, optional manager dashboard now mapped, job locking also uses handwritten SQL. |
| D1 Critical draft intake versus DD-10 reject-at-create; D2 claimant wording | A/Claims/ClaimValidators.cs; DT/ClaimTests.DD10CriticalIssueRejectsCreation | Accepted override, not a defect. |
| D2 component names/positive-only amounts versus D1/DD-02/DD-04 | D/Enums/ClaimEnums.cs; D/Entities/ClaimReserveComponent.cs | Accepted D1 components and signed delta behavior. |
| D2 rejected revision “new pending” versus DD-27 | D/Entities/ClaimReserveComponent.cs; reserve SQL tests | Normal authority rules apply; small revision may autoapprove. Accepted override. |
| D2 SLA SlaBreached status versus D1/DD-18/DD-34 | P/Services/BackgroundJobStore.cs:114 | SlaBreachedAt with unchanged Status/UpdatedAt is correct. |
| All-entity soft delete / OrganizationEntityId versus DD-24/DD-39 | P/ClaimsDbContext.cs:59–96; CFG:51 | OrganisationId shadow filter on all business mappings; removable-six soft-delete set is correct. |
| D1 rejection reason in OldValue versus DD-15 | A/Behaviors/DomainEventDispatcher.cs:60 | Reason in NewValue is correct. |
| D1/D2 NgModule language versus DD-23 | UI/app.routes.ts:3 | Lazy standalone components are correct. |
| No runtime schema creation versus DD-31 Hangfire installer | API/BackgroundJobs.cs:18 | Hangfire-owned schema installation is explicitly permitted. |
| D2 simplified ClaimType/approval fields versus DD-30/DD-05 | D/Entities/Claim.cs; ReserveHistory.cs | ClaimType omission and transaction approval state are explicit decisions. |

## Package policy and licenses

Central package versions are pinned in Directory.Packages.props; Angular installation is reproducible through package-lock.json/npm ci even where package.json uses version ranges. MediatR is 12.5.0, AutoMapper is 14.0.0, FluentValidation is 12.1.1, and Serilog.AspNetCore is 9.0.0. Tests use xUnit 2.9.3, Shouldly 4.3.0, NSubstitute 6.2.0; no Moq or FluentAssertions package was found in the reviewed project/package definitions.

The official package listings confirm [MediatR 12.5.0 Apache-2.0](https://packages.nuget.org/packages/mediatr/12.5.0), [AutoMapper 14.0.0 MIT](https://packages.nuget.org/packages/AutoMapper/14.0.0), and [FluentValidation package/license and compatible framework targets](https://www.nuget.org/packages/FluentValidation/). The observed stable FluentValidation listing was 12.1.1, compatible with .NET 9. No package was selected, installed or upgraded during this audit. The AutoMapper advisory remains F02 despite compliance with the version/license rule.

The initial build could not reach NuGet's vulnerability service (NU1900). A supplementary command attempted:
    
    dotnet list backend/ClaimsModule.sln package --vulnerable --include-transitive --format json --no-restore > TestResults/FinalAudit/nuget-audit.json

It exited 1 because this installed SDK does not support --no-restore for that command. The corrected read-only query was then requested:
    
    dotnet list backend/ClaimsModule.sln package --vulnerable --include-transitive --format json > TestResults/FinalAudit/nuget-audit.json

It was not executed: automatic approval review hit its usage limit. The rejection explicitly said review could not be completed and was not a determination that the action was unsafe. Full NuGet transitive advisory coverage remains Unverified. The npm audit and independent AutoMapper/MediatR/FluentValidation source checks completed; the nuget-audit.json artifact must not be treated as a successful advisory report.

## Unverified items and what would close them

| ID | Unverified item | Reason / required evidence |
| --- | --- | --- |
| U01 | Real Azure backend/Swagger/frontend and review-time availability | No public URLs, credentials to resources, or deployed-run evidence supplied. Execute the runbook, then record working origins and a full FNOL→reserve→approval→GL→document→audit walkthrough. |
| U02 | App Service full-size upload | No live host/proxy path. Upload just below/at/above configured 50 MiB through the actual frontend/API, verify status/error contract and stored bytes. Inspect actual upstream limits rather than assuming Kestrel settings suffice. |
| U03 | Azure Blob/Azurite transport, SAS expiry and collision behavior | Five tests skipped; Docker engine/emulator absent. Run the named Azure tests with Azurite, then verify private Azure container/read-only one-hour SAS separately. |
| U04 | Compose full-stack startup/browser download | Docker Desktop engine unavailable. Static F15 and F19 findings remain independently supported. |
| U05 | GitHub Actions execution, Linux/Node20, deployed artifact paths | YAML inspected and equivalent local build/test steps passed on Windows/Node24. No workflow run/remote configured. |
| U06 | Reviewer repository access and complete committed delivery | No remote or reviewer URL/access proof; most implementation is untracked/modified in this checkout. Review covers working files, not a complete committed release. |
| U07 | Actual Azure least-privilege SQL identity/secrets/Blob privacy/CORS/TLS/AlwaysOn settings | Provisioning/runbook source inspected; no live resource query performed. Restricted SQL role tested locally only. |
| U08 | 1280px visual layout/accessibility and real browser console | Components/CSS and unit tests inspected; no integrated visual browser pass or accessibility audit run. |
| U09 | Production-scale latency, query plans, load and soak | No representative dataset/workload or performance target supplied. Static list/N+1/index analysis is recorded, not benchmark results. |
| U10 | Long-term flake rate and numeric code coverage | One complete successful supported-service run, no repeated soak or collected coverage report; no browser E2E suite defined. |
| U11 | Full NuGet dependency vulnerability graph | Feed unavailable in sandbox; corrected supplementary query blocked by approval-review usage limit. Individual known advisory/license checks do not replace a full graph scan. |
| U12 | Absolute absence of secrets outside reviewed scope | Heuristic local working-tree + three reachable-commit scan found no real secret. Remote/unreachable history and external settings inaccessible. |
| U13 | Existing consumer/backward-compatibility baseline | Greenfield repository, no deployed prior contract or consumer tests supplied. F05 is nevertheless a concrete retry-semantics problem. |
| U14 | Human live-review capability and AI authorship/history | Requires authentic human evidence and future live session; cannot be inferred or manufactured from code. |

## Assumptions and scope decisions

1. “Complete implementation” means the present working tree, including existing uncommitted/untracked work. No attempt was made to discard, stage, commit, rewrite, or publish it.
2. The requested path final-review\.md was interpreted as docs/review/final-review.md, not a filename containing a backslash.
3. DD-32–DD-40 are effective because DESIGN-DECISIONS.md explicitly incorporates the addendum. Accepted overrides are not counted as defects. Remaining contract tensions (especially F05/F06) are reported rather than resolved by code.
4. A file/test reference supports only the stated scope. Source-proven implementation, passing local integration, skipped emulator integration, and live-cloud verification are deliberately distinguished.
5. “50 MB” follows the existing implementation's 50 * 1024 * 1024-byte convention (50 MiB), with 52 MiB request overhead allowance. The configurable cap must be checked against actual deployment settings.
6. UI date filtering is assumed to include the calendar day selected as the upper date; the intended timezone requires confirmation when fixing F09. D1's expired-policy badge wording overlaps its in-force-on-loss-date rule for historical losses; no behavior was invented.
7. Demo login and local development keys are intentional assessment fixtures under DD-19. That does not make anonymous local documents tenant-authorized or a demo login suitable for private production claims.
8. Advisory counts are package entries reported by the registry, not independent exploitable flaws. Reviewer severities account for demonstrated code paths and impact; F01 is High despite two upstream Critical npm entries.
9. Scope permits generated ignored build/test/audit artifacts. Only docs/review/final-review.md is an intentional repository deliverable changed by this review. No new dependencies or tests were added.

## AGENTS final reporting checklist

1. **Implemented:** this evidence-based audit/report only; no feature implementation or fixes.
2. **Files changed:** docs/review/final-review.md only as an intentional repository change. Existing dirty files preserved; builds/tests generated ignored outputs and TestResults artifacts.
3. **Architectural decisions:** none made. Applied existing precedence and DD decisions; flagged contradictions for explicit resolution.
4. **Tests executed:** all three backend projects via the solution, all frontend unit/component/service tests, and HTTP-boundary check; exact commands above.
5. **Test results:** 512 backend passed / 0 failed / 5 Blob tests skipped; 53 frontend passed; boundary check passed. Initial sandbox SQL failures were superseded by the successful rerun.
6. **Build result:** backend and production frontend passed; warnings and initial frontend sandbox EPERM recorded.
7. **Assumptions:** listed above, including working-tree scope, decision precedence, MiB/date conventions, and limits of static evidence.
8. **Remaining issues:** 26 unique findings (0 Critical, 6 High, 14 Medium, 6 Low), plus explicitly unverified items U01–U14. No claim of complete deployed acceptance.

