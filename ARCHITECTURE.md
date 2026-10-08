# Claims Module architecture

This document describes the current repository, not an aspirational design. Functional decisions are recorded in `docs/DESIGN-DECISIONS.md` and `docs/DESIGN-DECISIONS-addendum.md`.

## Solution structure

```text
Angular browser application
  └─ typed HTTP services → JWT/correlation/idempotency-aware ASP.NET Core API
       ├─ ClaimsModule.API
       │    controllers, auth, middleware, Swagger, Hangfire host
       ├─ ClaimsModule.Application → ClaimsModule.Domain
       │    MediatR commands/queries, pipeline, validators, DTOs, interfaces
       ├─ ClaimsModule.Persistence → ClaimsModule.Application
       │    EF Core DbContext, mappings, migration/seeds, queries, UoW
       └─ ClaimsModule.Infrastructure → ClaimsModule.Application
            Azure/local storage and Hangfire jobs

SQL Server / Azure SQL ← EF application data + HangFire schema
Azure Blob / Azurite or local filesystem ← document bytes
```

Repository layout:

```text
backend/
  ClaimsModule.sln
  src/
    ClaimsModule.Domain/          no project/package dependencies
    ClaimsModule.Application/     references Domain
    ClaimsModule.Persistence/     references Application
    ClaimsModule.Infrastructure/  references Application
    ClaimsModule.API/             composes all four projects
  tests/
    ClaimsModule.Domain.Tests/
    ClaimsModule.Application.Tests/
    ClaimsModule.IntegrationTests/
frontend/claims-ui/               Angular 20 standalone application
docs/                             specifications, decisions, topic evidence
infra/                            Azure provisioning and SQL permissions
.github/workflows/                CI and manual Azure deployment
```

The dependency direction keeps business rules independent of EF, HTTP, Hangfire, and Azure. Application interfaces (`IClaimRepository`, `IUnitOfWork`, `IStorageService`, query interfaces, and job contracts) are implemented outward in Persistence or Infrastructure. API is the composition root.

## Runtime request flow and CQRS

State-changing controller actions send an `ICommand<T>` through MediatR. Reads send an `IQuery<T>`. Authentication/dev login, health, Swagger, the Hangfire dashboard, and the Development-only local file transport are host concerns rather than claim CQRS use cases.

SPA route guards wait for shared session initialization before constructing protected features. Restored JWT expiry is checked locally; API 401 recovery shares one login and retries each failed request once with its original body and idempotency key. Dashboard date-only filters expand to inclusive UTC day bounds; explicit API timestamp bounds retain their existing semantics. Query changes cancel older dashboard requests, and individual policy-search failures allow subsequent searches.

Detail mutations hold a pending guard through the following refresh. Financial controls block new reserves on terminal claims and derive aggregate override requirements from loaded approved balances and pending deltas. Party audit links focus retained active/inactive records. Document downloads fetch fresh URLs on click. `Storage:Azure:PublicDownloadOrigin` optionally substitutes the browser-facing origin without changing the signed Blob path/query; Compose uses `http://localhost:10000` for its internally addressed Azurite client.

```text
HTTP request
  → correlation/error/CORS/auth/idempotency middleware
  → API controller
  → MediatR
      → LoggingBehavior
      → ValidationBehavior (FluentValidation; collects warnings, throws errors)
      → TransactionBehavior
          → query: invoke handler directly, no write transaction
          → command: UnitOfWork.ExecuteAsync
              → begin SQL transaction
              → handler loads aggregate/read collaborators
              → domain methods enforce invariants and raise events
              → dispatch domain events before SaveChanges
              → audit event handlers append audit rows in the transaction
              → register GL enqueue as a post-commit action
              → SaveChanges + commit
              → run post-commit actions; log failures without replaying command
  → DTO/status response
```

Why the Unit of Work matters:

- Claim number allocation, aggregate rows, reserve cache/ledger state, and audit rows commit atomically.
- Domain events are dispatched before commit so their database effects share the transaction.
- Rowversion conflicts cause one full reload-and-re-evaluation retry rather than blindly retrying stale tracked state.
- External Hangfire enqueue occurs only after commit, so a worker cannot observe an approved reserve that later rolls back.
- Post-commit failure does not replay the already committed business command; recovery owns delivery repair.

Removing this boundary would allow partial FNOL writes, gaps from rolled-back claim-number allocations, financial changes without audit evidence, stale-rule retries, and jobs that race uncommitted or aborted data.

## Data model

Every application table has shadow `OrganisationId` and audit columns. The domain model intentionally does not carry tenant state; `ICurrentUser` supplies it to Persistence. Combined tenant/soft-delete query filters protect normal reads. Composite tenant-aware foreign keys prevent cross-tenant relationships.

```text
Policy 0..1 ──────── * Claim * ───────── 0..1 User (assigned handler)
                           │
                           ├── 1 LossEvent ───── 1 CauseOfLossCode
                           ├── * ClaimParty
                           ├── * ClaimRiskObject
                           ├── * ClaimValidationIssue
                           ├── * ClaimDocument
                           ├── * ClaimAuditLog
                           └── * ClaimReserveComponent (unique Claim + Component)
                                      └── * ReserveHistory
                                             └── 0..1 ReserveHistory (resubmission of)

Organisation + Year ── 1 ClaimNumberSequence
Organisation + User + Key ── 1 IdempotencyRecord
ClaimStatusTransition, User, Policy, CauseOfLossCode ── seeded reference data
```

### Core and child records

- `Claims` is the lifecycle aggregate root. It owns claim number, optional policy snapshot/link, status, severity, report/closure data, assignee, notes, liability determination, manager override, SLA marker, and `RowVer`.
- `LossEvents` is one-to-one with a claim and references a seeded cause-of-loss code.
- `ClaimParties`, `ClaimRiskObjects`, and `ClaimValidationIssues` are claim children. Party removal deactivates the domain row; the persistence table also follows the removable-business-table soft-delete convention.
- `ClaimDocuments` stores metadata and an external blob/local path, never file bytes.
- `ClaimAuditLog` is append-only evidence. Application writes go through `IAuditLogService`; an interceptor and deployment SQL deny update/delete paths.

### Reserve ledger

- `ClaimReserveComponents` has one row per `(ClaimId, Component)` and a `RowVer`. `CurrentAmount` is a transactional cache.
- `ReserveHistory` is the ledger truth. Each row has an immutable identity, component, transaction type, signed delta, sequence, idempotency key, reason, submitter, and optional resubmission link. Workflow fields record approval/rejection/retraction and posting.
- Approved balance is the sum of deltas whose approval state is `AutoApproved` or `Approved`; posting state does not affect balance.
- `PreviousBalance`/`NewBalance` are assigned in approval order. UI history is grouped by component and ordered by change sequence within each group, so those balances need not be monotonic in display order.
- Unique indexes protect reserve idempotency keys and `(ReserveComponentId, ChangeSequence)`.

### System records

- `ClaimNumberSequences` is unique per organisation/year and supports atomic gap-free allocation in the owning claim transaction.
- `IdempotencyRecords` reserves POST keys per organisation/user. It stores method, route template, canonical request hash, in-progress/completed state, status, and response body for 24 hours.
- `Policies`, `Users`, `CauseOfLossCodes`, and `ClaimStatusTransitions` are migration-seeded reference/demo data.

Soft delete is applied only to `Claims`, `LossEvents`, `ClaimParties`, `ClaimRiskObjects`, `ClaimDocuments`, and `ClaimValidationIssues`. Immutable ledgers/audits, reference tables, counter/idempotency rows, reserve components, and Hangfire tables follow the DD-39 exceptions.

## Domain events and handlers

Aggregates raise these current event types:

| Domain event | Audit/result |
| --- | --- |
| `ClaimCreated` | `CLAIM_CREATED` |
| `ClaimStatusChanged` | `STATUS_CHANGED` |
| `ClaimPartyAdded`, `ClaimPartyRemoved` | `PARTY_ADDED`, `PARTY_REMOVED` |
| `ClaimClosed`, `ClaimReopened` | `CLAIM_CLOSED`, `CLAIM_REOPENED` (reopen also emits two status changes) |
| `ValidationIssueAdded` | `VALIDATION_ISSUE_ADDED` |
| `ReserveOverrideSet` | `CLAIM_RESERVE_OVERRIDE_SET` |
| `PolicyLinked` | `POLICY_LINKED` |
| `LiabilityDetermined` | `LIABILITY_DETERMINED` |
| `ReserveSubmitted` | `RESERVE_CREATED` |
| `ReserveAutoApproved` | `RESERVE_AUTO_APPROVED` and post-commit GL scheduling |
| `ReserveApproved` | `RESERVE_APPROVED` and post-commit GL scheduling |
| `ReserveRejected`, `ReserveRetracted` | `RESERVE_REJECTED`, `RESERVE_RETRACTED` |

`DomainEventDispatcher` publishes a common MediatR notification. `AuditDomainNotificationHandler` maps events to append-only audit entries. `GlPostingNotificationHandler` reacts only to approved reserve events and asks the scheduler to register a Unit-of-Work post-commit action. Notes updates and handler assignments are orchestrated application actions and append `NOTES_UPDATED`/`HANDLER_ASSIGNED` directly through `IAuditLogService` in their command transaction. Background jobs likewise use that service for GL/SLA audit entries.

This is deliberately synchronous in-process event dispatch, not an integration-event bus. It makes transactional audit behavior simple but does not publish events to other services.

## Hangfire jobs, delivery, and idempotency

Hangfire uses the same SQL connection as the application and owns schema `HangFire`. Its server runs in the API process.

### `PostGLReserveChangeJob`

- Trigger: registered after an auto/manual reserve approval transaction commits.
- Input: reserve-history ID, claim ID, and `Reserve:{ReserveComponentId}:Change:{ChangeSequence}`.
- The store first discovers the tenant from the exact reserve identity, then reloads under that tenant.
- It locks the claim, verifies identity and approval, and no-ops if missing, mismatched, unapproved, or already Posted.
- A conditional SQL update claims the eligible row and the same transaction writes `GL_POSTING_SIMULATED`. Duplicate deliveries therefore do not duplicate posting or audit.
- Hangfire retry handles exceptions. `GlPostingFailureFilter` acts only when Hangfire applies terminal `Failed`, then marks the row Failed and writes `GL_POSTING_FAILED` once.
- Manual retry changes Failed to Pending and enqueues with the same idempotency key.

### `GlPostingRecoveryJob`

- Schedule: every five minutes, UTC.
- Finds approved/auto-approved rows still Pending, with no job ID and approval older than two minutes.
- Enqueues the same posting job/key. Duplicate recovery delivery is safe because posting is idempotent.
- This repairs the commit/enqueue crash window but is not a general durable outbox.

### `SlaMonitoringJob`

- Schedule: every 15 minutes, UTC.
- Scans all tenants for non-deleted Draft/Open claims whose last activity (`UpdatedAt`, falling back to `CreatedAt`) is older than 48 hours.
- Locks/rechecks each claim, sets `SlaBreachedAt` through `ExecuteUpdate` so `UpdatedAt` is not touched, and appends `SLA_BREACH_DETECTED`.
- An existing event in the preceding 24 hours suppresses duplicates.
- It never changes lifecycle status. A later business action updates activity and clears the marker.

The Hangfire dashboard is absent unless enabled. When mapped at `/hangfire`, its authorization filter requires an authenticated Manager.

## Concurrency and idempotency

- Claims and reserve components use SQL rowversion. The Unit of Work retries the entire command once after a concurrency conflict; the callback reloads data and re-evaluates business rules. A repeated conflict becomes HTTP 409.
- Concurrent first use of the same reserve component can collide on the unique component index before a rowversion exists. That specific insert conflict is translated into the same reload/re-evaluate path.
- The claim rowversion serializes cross-component aggregate-cap decisions. Approval balances are recalculated at approval time.
- Claim-number allocation uses `UPDLOCK, HOLDLOCK` with `UPDATE ... OUTPUT`, insert-if-missing, savepoint retry for duplicate insert, and whole-transaction retry for deadlock. The sequence increment rolls back with failed claim creation.
- With an `Idempotency-Key`, the current middleware intercepts every authenticated non-multipart POST except claim validation. It hashes canonical JSON with SHA-256. The database uniqueness key is `(OrganisationId, UserId, Key)`; method and route template are stored and a reuse against either a different payload or operation returns 422. Same-operation retries replay stored responses below 500, concurrent in-progress use returns 409 with `Retry-After`, and 5xx removes the reservation. Multipart upload bypasses it. This real coverage is broader than DD-32's named JSON endpoint list, and its unique key behaves as user-wide rather than allowing the same textual key in separate route scopes.

## Document storage

`IStorageService` hides two implementations:

- `AzureBlobStorageService` writes private blobs beneath `{organisation}/{claim}/{sanitized-file}` and generates one-hour read-only account-key SAS URLs.
- `LocalFileSystemStorageService` writes beneath the configured root with the same path scheme. It prevents rooted/traversal/control-character paths and creates numeric collision suffixes. A Development-only anonymous endpoint streams local files.

Both use create-if-absent semantics for name collisions. Validation permits PDF, JPEG, PNG, DOCX, XLSX, TXT, and CSV up to the configured limit (50 MiB by default).

## Azure architecture

```text
User browser
  ├─ HTTPS → Azure Static Web Apps (Angular assets)
  └─ HTTPS/JWT → Linux Azure App Service (.NET API + Hangfire server)
                         ├─ TLS → Azure SQL
                         │          application schema + migration history
                         │          HangFire schema
                         └─ HTTPS → private Azure Blob container
                                      ← one-hour read-only SAS from API

GitHub Actions (manual production workflow)
  ├─ build/test API and SPA
  ├─ deploy API using publish profile
  └─ deploy SPA using Static Web Apps token
```

The supplied B1 App Service plan must keep Always On enabled for the in-process worker. EF migrations are run manually from an Entra-authenticated developer machine; the deployment workflow does not hold a database credential. The Web App system identity uses the same managed-identity SQL connection for EF runtime operations and Hangfire. Azure identity creation and secret wiring remain operator-managed.

## Key decisions and tradeoffs

### Claim number allocation (DD-16, DD-35)

A tenant/year counter table replaces SQL `SEQUENCE`. The lock and update occur inside claim creation, so rollback also rolls back the number and preserves the no-gap rule. This increases contention at a tenant/year hot row and needs deadlock/duplicate retry, but satisfies the explicit gap-free requirement that a normal sequence cannot guarantee.

### Reserve ledger semantics (DD-04 through DD-08, DD-27, DD-28, DD-36)

History is an append-oriented signed-delta ledger: Add is normally positive, Reverse stores a negative delta, and Adjust may have either sign. Subrogation recoverable uses the decided negative convention. Authority is based on absolute transaction delta, while aggregate limits use net approved balances. Workflow columns can change through domain methods; financial identity fields cannot. The component's `CurrentAmount` is a cache updated in the approval transaction, while history remains truth. This supports auditability and reversal without rewriting old money, at the cost of more involved approval-order presentation and reconciliation tests.

### Post-commit scheduling (DD-17, DD-33)

The transaction commits before Hangfire enqueue. This avoids jobs reading data that can still roll back. The unavoidable crash window is repaired by the recovery job and idempotent posting, yielding at-least-once rather than exactly-once delivery. A durable outbox would generalize and tighten delivery guarantees but is not implemented.

### Shadow state and tenant isolation (DD-24, DD-39)

Tenant, audit, and selected soft-delete columns are EF shadow properties, leaving Domain independent of persistence and tenancy concerns. Interceptors populate them and global filters enforce normal tenant visibility. The tradeoff is less discoverability in domain classes and a need for explicit tenant-aware composite keys, query tests, and job discovery logic.

### SLA marker rather than lifecycle state (DD-18, DD-34)

D2 suggests `SlaBreached` status; D1 says the job must not alter status. The implementation follows D1: nullable `SlaBreachedAt` plus audit evidence. `ExecuteUpdate` intentionally bypasses the activity interceptor so detecting a breach does not make an inactive claim appear active.

### Role model (DD-07, DD-11, DD-19, DD-40)

JWT roles form `Handler < Supervisor < Manager`. Any role may submit any reserve; review authority and self-approval rules are enforced separately. The creator becomes assignee even if they are Supervisor/Manager, so the assignee list includes all roles. This is a demo authorization model, not production IAM.

### Other explicit deviations from the specifications

- D1 reserve component names win over D2's Recovery/Litigation names (DD-02).
- The D1 reserve transaction API is canonical; D2's PUT is a thin Adjust adapter (DD-03).
- Critical intake failures reject creation rather than persisting an invalid Draft; warnings persist and require acknowledgement to open (DD-10).
- Reopen executes `Closed → Reopened → Open` atomically and audits both status legs (DD-13).
- ClaimType is omitted; Severity defaults to Standard (DD-30).
- Angular uses standalone lazy-loaded components instead of feature NgModules (DD-23).
- POST idempotency is scoped by DD-32; PUT/DELETE ignore the header despite D1's broader wording.
- Only six removable business tables use soft-delete fields; immutable/system/reference exceptions follow DD-39.
- Hangfire's own schema installer is the sole runtime-schema exception (DD-31).

## Deployment tradeoffs

- Azure SQL is reached through public networking plus the Azure-services firewall rule rather than a private endpoint. Authentication remains Microsoft Entra managed identity, but the network boundary is intentionally broader for the assessment.
- The Web App managed identity is `db_owner` because Hangfire creates and upgrades its permitted `HangFire` schema at runtime. A hardened design would separate schema ownership/migration from normal runtime permissions.
- Blob access uses a shared-key connection string so the API can generate account-key SAS URLs. Managed-identity Blob access and user-delegation SAS are not implemented.
- `Auth:DevLoginEnabled` is enabled for the public demo. It is username-only assessment authentication and must be disabled after review.
- Azure SQL serverless auto-pause makes the first connection slow or transiently unsuccessful. EF uses bounded retries, recurring Hangfire registration retries without terminating startup, and `/health/ready` reports database availability separately from `/health`.
- The resources span mixed Azure regions, increasing latency and cross-region dependency exposure; co-location would be preferable for a production workload.
- AutoMapper 14.0.0 is retained because the assessment requires AutoMapper and the repository package policy caps it at 14.x. Advisory GHSA-rvv3-g6hj-g44x is accepted for the current flat, non-recursive DTO maps; mapping depth is explicitly bounded.

## Likely live-review questions

### Why is this a Command rather than a direct service call?

A command declares an application use case and forces every state change through the same logging, validation, transaction, domain-event, concurrency, and audit path. Controllers remain transport adapters. A direct service call from a controller could bypass those cross-cutting guarantees and would couple HTTP to orchestration details. Internal reusable operations such as `ReserveSubmission` are services because they are invoked inside an already owning command transaction, avoiding nested commands/UoWs.

### What breaks without the Unit of Work?

FNOL children, counter allocation, warnings, reserve rows/cache, and audit entries could commit independently. Domain-event audit could be missing for a successful mutation. A posting job could start before approval commits. Concurrency retry could reuse stale tracked aggregates, and a post-commit failure might incorrectly replay already committed money. The Unit of Work makes the SQL work atomic and gives external delivery a clear after-commit boundary.

### How would reserves be extended to multiple currencies?

Add an ISO currency to every monetary fact and make component identity currency-aware, for example `(ClaimId, Component, Currency)`. Never sum unlike currencies. Store original amount/currency and, when reporting in a claim/base currency is required, persist the rate, rate source, effective instant, and converted amount used for each approved transaction so history is reproducible. Authority and aggregate-cap policies must state whether they operate in transaction currency or a converted base currency. Existing data needs an explicit default-currency migration; decimal precision and rounding policy need currency-specific tests.

### How is concurrency handled?

Rowversion protects Claim and reserve components. A conflict causes one complete command reload/re-evaluation; a second conflict returns 409. Unique-index translation covers simultaneous first component creation. The Claim token serializes aggregate-cap decisions across components. Counter allocation uses SQL range locks and bounded duplicate/deadlock retries. HTTP POST idempotency handles client retries, while GL delivery has its own persistent idempotency key and conditional update.

### Why are domain events dispatched before commit but Hangfire enqueued after commit?

Audit handlers write SQL state that must succeed or roll back with the business action, so they run inside the transaction. Hangfire is an external delivery side effect; enqueueing before commit risks a worker seeing or acting on data that later rolls back. The recovery job covers the after-commit enqueue failure window.

### Why persist `CurrentAmount` if history is the ledger?

It makes common summaries and cap evaluation efficient, while the ledger retains audit truth. The cache is updated in the same approval transaction, and integration tests assert it equals the approved signed-delta sum. If that invariant cannot be maintained, the cache should be rebuilt from history rather than treating it as an independent truth.
