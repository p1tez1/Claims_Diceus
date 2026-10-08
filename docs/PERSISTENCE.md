# Application contracts and SQL Server persistence

Historical checkpoint for Prompt 5. The counter-only handwritten SQL statement below describes that checkpoint; the current background job store also takes a parameterized `UPDLOCK, HOLDLOCK` claim lock. See [BACKGROUND-JOBS](BACKGROUND-JOBS.md), `Persistence/Services/BackgroundJobStore.cs` and [ARCHITECTURE](../ARCHITECTURE.md) (F24, Prompt 23).

Prompt 5 implements the Application persistence/query contracts and the Persistence layer. Domain business methods remain unchanged. The supplied DD-32–DD-40 decisions are in [DESIGN-DECISIONS-addendum.md](DESIGN-DECISIONS-addendum.md).

## Setup

Use .NET SDK 9, SQL Server 2022, and EF CLI 9. The repository already pins EF SQL Server/Design to 9.0.20. No package versions or project references changed in this task.

In this Windows workspace the compatible SDK is installed in the ignored `.dotnet` directory:

```powershell
$env:DOTNET_ROOT = 'C:/Test/DICEUS_FullStack/.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:ConnectionStrings__Default = 'Server=localhost;Database=ClaimsModule;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet build backend/ClaimsModule.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet ef database update --project backend/src/ClaimsModule.Persistence --startup-project backend/src/ClaimsModule.Persistence --no-build
dotnet ef migrations script --project backend/src/ClaimsModule.Persistence --startup-project backend/src/ClaimsModule.Persistence --no-build --output TestResults/InitialCreate.sql
```

Set the connection string for your own SQL Server/Azure SQL host and authentication. The design-time factory uses `ConnectionStrings__Default`; its local fallback uses Windows authentication to `localhost`. Schema and seeds are created only by `InitialCreate`, never at application startup. The verification run used the installed EF CLI 9.0.7 against runtime 9.0.20: it printed an older-tools warning, but migration creation/application, SQL scripting, and the model-drift check succeeded.

## Host integration and transaction contract

`AddPersistence()` registers the context, repositories, query services, number/identifier generators, clock, audit service, and unit of work. The host must register a scoped `ICurrentUser` backed by authenticated claims or an explicit background-job tenant context. There is no fallback to the seed tenant. Domain's existing `IClock` remains the base of Application's clock contract.

Begin an `IUnitOfWork` transaction before allocating a number, appending audit, or saving. Repositories return tracked aggregates; reserve operations load the owning claim and **all** components/history into the same context. `SaveChangesAsync` flushes without committing or clearing events. `CommitAsync` drains the pre-commit dispatcher hook (including newly raised events), saves handler effects, and commits; only then are events cleared and queued actions attempted. A dispatcher must be provided explicitly. No MediatR handlers, controllers, JWT adapter, or job implementations are introduced.

`CommitResult.PostCommitErrors` reports action failures after a successful commit. Every queued action is attempted with a non-cancelled token. Callers must not replay the business command because an enqueue action failed. DD-33's future recovery job can find approved Pending rows with no PostingJobId using the persisted approval time and original idempotency key.

Rollback/disposal detaches tracked objects and discards queued actions. `ExecuteAsync` runs the entire supplied operation callback again after a conflict, so that it reloads and re-evaluates business rules: one retry for `ConcurrencyException`, up to three retries for counter deadlocks. It must not be given a callback that reuses aggregates from an earlier attempt. Bare Begin/Commit users receive the typed exception and coordinate their own retry.

The counter uses parameterized `UPDATE ... WITH (UPDLOCK, HOLDLOCK) ... OUTPUT inserted.LastValue`, followed by insert-if-missing under the retained lock, inside the owning transaction. A duplicate-key retry rolls back to the allocation savepoint. A SQL deadlock aborts the **whole** transaction; it raises `TransactionRetryRequiredException` so `ExecuteAsync` can retry the complete operation without discarding earlier writes. No SQL SEQUENCE is used. Domain numbers enforce the seven-digit range. The counter is the only handwritten SQL in production code.

## Mapping details

- Every application table has tenant and standard audit columns. Removable tables combine tenant isolation and soft deletion in one global filter. Other tables have the tenant filter only, per DD-39. Composite foreign keys include OrganisationId and reject cross-tenant parent references at the database boundary.
- All GUID primary keys retain `NEWSEQUENTIALID()` defaults. Storage records use EF's sequential GUID generator. Domain factories require non-empty IDs, so callers use `IIdentifierGenerator`; EF treats these as assigned IDs, which also makes newly added aggregate children insert correctly. Existing Domain-generated validation-issue UUIDv7 IDs are preserved.
- Domain `ReserveHistory.CreatedAt` and `ClaimValidationIssue.CreatedAt` APIs are preserved. Their single SQL `CreatedAt` column is an EF shadow property named `AuditCreatedAt`; a materialization interceptor restores the existing Domain property when loading. This is a model alias, not an extra SQL column or an exception to the shadow-column rule.
- The activity marker remains unpersisted Domain state. Its changed revision forces a claim update even when scalar values are unchanged; UpdatedAt preserves the marker's UTC time. Workflow changes and pending reserve submissions force an owning component update so RowVer advances even when CurrentAmount stays unchanged. Updating the owning claim also protects aggregate-cap decisions spanning multiple components.
- DD-34's SLA write uses `ExecuteUpdate` for SlaBreachedAt and a separate audit INSERT in the same transaction. Neither path updates the claim's UpdatedAt. Ordinary tracked claim modifications remain business activity.
- `ClaimAuditLog` accepts inserts only through `IAuditLogService`. SaveChanges rejects updates/deletes, and a query interceptor also rejects EF bulk audit updates/deletes. ReserveHistory deletion and financial identity/delta modification are rejected. Deployment must still give a separate runtime SQL principal no UPDATE/DELETE permissions on ClaimAuditLog, as D1 requires; this task does not create deployment principals or grants.
- LiabilityDetermination and LiabilityDeterminedAt are nullable mapped columns (Accepted/PartiallyAccepted/Denied). The read-side DTO exposes both. The later liability command maps this stored state to Domain's existing transition context and performs the DD-37 workflow/audit action.
- Policy coverage is one JSON-array column. User role queries are exact matches (DD-40); callers omit the role filter for assignee choices. Read queries project DTOs without tracking; claim/audit pagination is caller-specified, ordered by creation time descending with ID tie-breakers. Reserve history is ordered by ChangeSequence with nullable unapproved balances (DD-36). Document queries return metadata for later storage services to enrich with download URLs.
- IdempotencyRecords has the exact unique `(OrganisationId, UserId, Key)` index from DD-32, plus method, route template, hash, InProgress/Completed, nullable response status/body, and timestamps. Request hashing, replay, 24-hour expiration, and in-flight HTTP responses belong to the later request pipeline. No alternate key scope was invented here.

The timestamp bridge, assigned-ID EF configuration, tenant-aware foreign keys, and explicit commit result are implementation mechanisms that preserve the supplied business decisions. They do not introduce a new domain policy. Microsoft references: [EF interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors), [transactions/savepoints](https://learn.microsoft.com/en-us/ef/core/saving/transactions), and [SQL Server value generation](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/value-generation).

## Schema summary

Every row below has a GUID primary key, OrganisationId, CreatedAt/UpdatedAt/UserCreated/UserModified. Monetary columns are DECIMAL(19,4), timestamps DATETIMEOFFSET(7), flags BIT with default 0, and enums NVARCHAR strings. Column-specific lengths follow D1 section 9.

| Table | Soft delete | Main constraints / contents |
| --- | --- | --- |
| Claims | Yes | Unique OrganisationId + ClaimNumber; Status/UpdatedAt indexes; RowVer; nullable policy and liability fields |
| LossEvents | Yes | One event per claim; cause-of-loss FK; nullable estimated loss |
| ClaimParties | Yes | Claim FK; role/type, contact fields, IsActive |
| ClaimRiskObjects | Yes | Claim FK; asset/damage details |
| ClaimValidationIssues | Yes | Claim FK; severity, lifecycle, acknowledgement/resolution times |
| ClaimReserveComponents | No | Unique ClaimId + Component; RowVer; cached CurrentAmount |
| ReserveHistory | No | Unique IdempotencyKey and ReserveComponentId + ChangeSequence; approval/posting workflow; resubmission FK |
| ClaimDocuments | Yes | Claim FK; document/storage metadata |
| ClaimAuditLog | No | Append-only; ClaimId + CreatedAt index; actor/correlation/JSON |
| Policies | No | Unique PolicyNumber; DATE effective/expiry; JSON coverage; 5 seeds (2 Expired) |
| CauseOfLossCodes | No | Unique Code; 10 active seeds |
| ClaimStatusTransitions | No | Unique tenant + From + To; 12 seeded edges and minimum roles |
| Users | No | Unique tenant + Username; handler/supervisor/manager seeds |
| ClaimNumberSequences | No | Unique OrganisationId + Year; transactional LastValue |
| IdempotencyRecords | No | Unique OrganisationId + UserId + Key; request/response state |

Hangfire owns its separate schema under DD-31 and is not part of this migration. ClaimReserveComponents is not in DD-39's removable-table list and therefore has no soft-delete fields.

## Verification

```powershell
$env:CLAIMS_TEST_SQLSERVER = 'Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test backend/ClaimsModule.sln --no-build --no-restore --logger trx --results-directory TestResults/final -m:1 -nr:false
```

The fixture overrides the supplied connection's database name with a unique `ClaimsModule_Tests_*` name, migrates that empty database, and deletes only that owned database after the tests. Without a connection, it uses Testcontainers.MsSql when Docker is available. When neither is available, SQL-dependent facts skip with a reason; a configured but broken SQL connection fails rather than being silently skipped.

Verified on 2026-10-05 against local SQL Server 2022 Developer 16.0.1000.6:

- Whole solution: 282 passed (253 Domain, 1 Application, 28 Integration); zero failed or skipped. This includes 21 real SQL tests and one model-convention test added by Prompt 5.
- The tests cover empty-database migration, exact seed counts, no model drift, tenant/soft-delete filters and FK isolation, audit mutation/bulk guards, both RowVer roots, pending changes, re-evaluation after conflict, a real counter deadlock, 50 parallel first allocations, gap-free rollback, unique reserve/request keys, decimal precision, shadow columns, timestamp hydration, child activity, pre-/post-commit ordering and failures, liability storage, and DTO projections. The persisted component cache is checked against its approved ledger sum.
- With the SQL connection unset and Docker unavailable: 7 integration tests passed and 21 skipped cleanly; no failures. Docker container startup itself was not exercised because Docker is stopped on this host.
- CLI `database update` created the schema and all seeds in `ClaimsModule_Prompt5_Verification_ba717401cad54e82ada9109ca2aec097`, retained for review. The test fixture databases were deleted.
- `migrations script` generated `TestResults/InitialCreate.sql`. Review confirmed 15 application tables, 15 GUID defaults, audit/tenant fields on every table, exactly 6 soft-delete tables, 2 RowVer columns, DECIMAL(19,4), DATETIMEOFFSET(7), required NVARCHAR lengths, JSON policy coverage, and every requested index. `migrations has-pending-model-changes` reports no changes.
- The build has zero errors and the pre-existing AutoMapper 14.0.0 NU1903 warning. No dependency updates were made. Generated migration code is marked generated in its local `.editorconfig`, rather than hand-editing EF output to satisfy source-style analyzers.

## Changed files and boundaries

New Application files: `Abstractions/PersistenceContracts.cs`, `Models/PersistenceDtos.cs`, `Exceptions/ConcurrencyException.cs`.

New Persistence files: `ClaimsDbContext.cs`, `ClaimsDbContextFactory.cs`, `Configurations/EntityConfigurations.cs`, `Entities/PersistenceEntities.cs`, the three interceptors under `Interceptors/`, `Queries/ReadQueries.cs`, both files under `Seeding/`, three files under `Services/`, and `Migrations/20261005101331_InitialCreate.cs`, its designer, model snapshot and generated-code configuration. `DependencyInjection.cs` registers these services.

Tests: `Persistence/SqlServerFixture.cs`, `Persistence/PersistenceTests.cs`, and `ArchitectureTests.cs`. The architecture test now checks direct ProjectReference declarations rather than rejecting valid transitive assembly references. Documentation: this file and the link to the user's existing addendum in `DESIGN-DECISIONS.md`; the supplied addendum's contents were preserved.

Future Application/API/Infrastructure tasks supply authenticated current-user context, dispatching handlers, request idempotency behavior, GL/SLA/recovery jobs, and document URL generation. Those are integration points beyond this contracts/persistence task, not placeholder implementations registered in their place.
