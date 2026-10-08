# Hangfire jobs (Prompt 10)

Historical checkpoint for Prompt 10. Current host behavior supersedes the dashboard statement below: `Hangfire:DashboardEnabled` optionally maps `/hangfire`, protected by authenticated Manager authorization in every environment. It is disabled by default. See `API/BackgroundJobs.cs` and [ARCHITECTURE](../ARCHITECTURE.md) (F24, Prompt 23).

The API registers Hangfire SQL Server storage against `ConnectionStrings:Default`, with schema `HangFire` and its installer enabled under DD-31. Normal startup registers the jobs and starts the worker server. No dashboard route is mapped in any environment. The `Testing` environment registers the client dependencies without starting workers, installing Hangfire tables, or registering recurring schedules; tests can invoke job `Execute` methods directly. A separate Development-host test exercises the real startup registrations and server.

| Job | Trigger | Behavior |
| --- | --- | --- |
| `PostGLReserveChangeJob` | Approval post-commit action | Simulates the signed reserve delta, sets Posted/job ID, and appends `GL_POSTING_SIMULATED` atomically. |
| `SlaMonitoringJob` | `*/15 * * * *` (UTC) | Scans all tenants for active Draft/Open claims with activity older than 48 hours. |
| `GlPostingRecoveryJob` | `*/5 * * * *` (UTC) | Re-enqueues approved Pending rows with no PostingJobId and approval older than two minutes, using their original keys. |

## Re-entrancy and transaction boundaries

The unique ReserveHistory.IdempotencyKey index and financial identity mapping are unchanged. GL discovery matches the complete reserve ID, claim ID, and key, then obtains OrganisationId from the persisted row. It creates an explicit background user/tenant context with no actor ID and a fresh correlation ID. No HTTP context or seed-tenant fallback participates in background operations.

Persistence locks the owning active claim using a parameterized `SELECT ... WITH (UPDLOCK, HOLDLOCK)` within the transaction. This serializes parallel deliveries and monitoring for that claim without performing a dummy UPDATE. It loads the reserve domain aggregates, rejects unapproved rows, and invokes existing `MarkPosted`/`MarkPostingFailed` domain methods. An EF conditional UPDATE on the exact reserve identity and approved/nonterminal state must affect one row before audit insertion. Values come from the domain methods; financial fields are never included in the update. The reserve is then marked unchanged in the tracker to avoid a second reserve UPDATE. The existing component RowVer convention is preserved with a tracked component update, and Claim.Touch persists activity and clears SlaBreachedAt.

The posting UPDATE, component/claim activity, and `IAuditLogService` audit INSERT share one transaction. Audit or save failure rolls everything back. Sequential or concurrent duplicate successful deliveries find Posted and make no writes; duplicate failure callbacks find Failed or Posted and make no writes. Failure callbacks after a completed successful retry cannot replace Posted. Posting job IDs come from Hangfire's injected PerformContext; direct tests use `direct:{reserveHistoryId}`.

Hangfire's default automatic retry policy is retained. `GlPostingFailureFilter` implements IApplyStateFilter: retryable exceptions have already had their candidate state replaced with Scheduled by AutomaticRetry, so only an applied Failed state runs the terminal persistence operation. SQL failure recording has its own transaction: if the process stops before Hangfire commits its state, a repeated callback is harmless because the reserve/audit transaction has already committed. References: [Hangfire exception handling](https://docs.hangfire.io/en/latest/background-processing/dealing-with-exceptions.html), [SQL Server storage](https://docs.hangfire.io/en/latest/configuration/using-sql-server.html).

`GlPostingScheduler.Enqueue` registers an existing IUnitOfWork post-commit action. Approval notifications register it while events dispatch before commit; rollback discards it. Enqueue failures appear in CommitResult.PostCommitErrors and do not undo or replay the committed business operation. Recovery may enqueue duplicates while a job is waiting to execute: PostingJobId is written by execution, and at-least-once delivery is safe under the conditional posting transaction.

`ReserveRetryPostingCommand` and the `ReservePostingRetry` Application service are supplied for Prompt 11. The service runs inside the future command handler's transaction, loads tenant-filtered repositories, calls the domain Failed-to-Pending operation, and registers enqueue with the same key. Resetting PostingJobId to null also lets DD-33 recover a manual retry whose enqueue fails. No reserve MediatR handler or endpoint is added here; Prompt 11 supplies the endpoint authorization and wiring.

SLA discovery explicitly excludes soft-deleted claims while ignoring tenant filters. Each candidate is rechecked under the claim lock in its stored tenant: status, COALESCE(UpdatedAt, CreatedAt), soft deletion, and the last 24 hours of SLA audit entries. The timestamp uses ExecuteUpdate, followed by an audit-only SaveChanges inside the same transaction. Neither path changes UpdatedAt or Status. Exactly 24 hours since the previous audit permits another detection; parallel monitors serialize before the dedupe recheck.

## Scope and dependencies

Job classes and Hangfire integration live in Infrastructure/API. Application declares scheduling and persistence-operation contracts; Persistence implements SQL/domain/audit operations. Direct project references remain unchanged. Existing Hangfire.AspNetCore/Core/SqlServer 1.8.25 pins are reused, with no new packages or version changes. Their installed LICENSE.md files offer LGPL v3; no commercial Hangfire package is used.

The existing storage-options registration now reads configuration when its singleton is resolved. Verification exposed that eager reading before host configuration overrides caused the existing upload-size test to accept a file above its configured limit. Deferring this read preserves the configured limit/provider and fixes that test without altering upload rules.

Files added:

- `backend/src/ClaimsModule.API/BackgroundJobs.cs`
- `backend/src/ClaimsModule.Application/Abstractions/BackgroundJobContracts.cs`
- `backend/src/ClaimsModule.Application/Behaviors/GlPostingNotificationHandler.cs`
- `backend/src/ClaimsModule.Application/Reserves/ReservePostingRetry.cs`
- `backend/src/ClaimsModule.Infrastructure/Jobs/GlPostingJobs.cs`
- `backend/src/ClaimsModule.Infrastructure/Jobs/GlPostingFailureFilter.cs`
- `backend/src/ClaimsModule.Infrastructure/Jobs/SlaMonitoringJob.cs`
- `backend/src/ClaimsModule.Persistence/Services/BackgroundJobStore.cs`
- `backend/tests/ClaimsModule.IntegrationTests/BackgroundJobSqlTests.cs`
- `docs/BACKGROUND-JOBS.md`

Existing files changed by Prompt 10:

- `backend/src/ClaimsModule.API/Program.cs`
- `backend/src/ClaimsModule.Application/DependencyInjection.cs`
- `backend/src/ClaimsModule.Domain/Entities/ReserveHistory.cs`
- `backend/src/ClaimsModule.Infrastructure/DependencyInjection.cs`
- `backend/src/ClaimsModule.Persistence/DependencyInjection.cs`
- `backend/tests/ClaimsModule.IntegrationTests/HealthEndpointTests.cs`

The working tree already contained changes from preceding prompts. Those changes were retained.

## Verification

Use the installed compatible SDK and an isolated output directory if a pre-existing API process holds the normal bin files:

```powershell
.\.dotnet\dotnet.exe build backend/ClaimsModule.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:OutDir=C:/Test/DICEUS_FullStack/TestResults/Prompt10/bin/
$env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
.\.dotnet\dotnet.exe test backend/ClaimsModule.sln --no-build --no-restore -m:1 -nr:false -p:OutDir=C:/Test/DICEUS_FullStack/TestResults/Prompt10/bin/ --logger trx --results-directory TestResults/Prompt10/final
```

Local SQL tests require execution outside this filesystem sandbox: inside it SQL encryption initialization fails with "The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it." Elevated verification preserves encryption; it does not weaken connection settings. SQL fixtures migrate uniquely named databases and delete only their owned databases.

Ten new SQL tests cover sequential/eight-way parallel posting with one audit and unchanged duplicate-delivery RowVer/activity; approved versus pending/rejected/cancelled and mismatched identities; audit-failure rollback; the actual Hangfire default-retry state pipeline; repeated terminal filters and same-key retry; post-commit visibility and rollback; recovery and failed manual-retry enqueue; cross-tenant GL; failure activity; parallel cross-tenant SLA, exact 48h/24h boundaries, null UpdatedAt, soft deletion, and unchanged status/activity; and real API startup/server/storage/dashboard checks.

On 2026-10-06 the API was also started as a standalone Kestrel process on `http://127.0.0.1:5187`, against isolated local database `ClaimsModule_Prompt10_Verification_20261006_c54838af`. `/health` returned Healthy. Direct SQL reads confirmed both recurring-job hashes with their required crons and one active Hangfire server. The temporary process was stopped after verification; the database is retained for inspection.

Final verification on 2026-10-06: solution build passed with zero errors and nine existing warnings (AutoMapper 14.0.0 NU1903 and read-query culture/comparison analyzer warnings). The complete suite passed: 258 Domain, 58 Application, and 66 Integration tests, for **382 passed, zero failed, one skipped**. All ten new SQL job tests passed. The skipped existing AzureBlobStorage test requires an Azurite connection; it remains unverified. TRX reports are in `TestResults/Prompt10/final`. `git diff --check` passed. EF CLI 9.0.7 also printed its existing version warning against runtime 9.0.20 during standalone database migration.
