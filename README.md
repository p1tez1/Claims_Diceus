# Claims Module

Greenfield claims-management assessment covering FNOL intake, claim lifecycle, reserve approval and ledger history, simulated GL posting, document storage, and an immutable audit trail. The backend is .NET 9/C# 13 with Clean Architecture, MediatR, EF Core 9, SQL Server, and Hangfire. The frontend is Angular 20 with standalone lazy routes, Angular Material, reactive forms, and signals.

The source-of-truth order is:

1. `docs/DESIGN-DECISIONS.md` and its linked addendum where they record an explicit decision.
2. `docs/spec/spec-1-functional.md` for functional behavior.
3. `docs/spec/spec-2-assessment.md` for technology, structure, deliverables, and deployment.

See `ARCHITECTURE.md` for implementation details and `AI-WORKFLOW.md` for the assessment's AI-transparency report scaffold.

## Prerequisites

### Full Docker setup

- Docker Desktop with Linux containers and Docker Compose v2.
- Git.
- About 6 GB of free memory for SQL Server, Azurite, the API, and the Angular/Nginx container.

The container build supplies .NET and Node; host installations are not required for this path.

### Host development setup

- .NET SDK 9.0.300 or a later 9.0 feature band accepted by `global.json` (`rollForward: latestFeature`).
- `dotnet-ef` 9.0.20.
- Node.js 20.19+, 22.12+, or 24 and npm. The lock file is authoritative; use `npm ci`.
- SQL Server 2022/Azure SQL, or Docker Desktop to run the included SQL Server 2022 container.
- Optional: Azurite for Azure Blob-compatible local storage. The simpler host-run instructions below use `LocalFileSystem` storage.
- PowerShell 7 for the examples and Azure provisioning script.
- Azure CLI only when reproducing the Azure environment.
- Chrome/Chromium for Angular's `ChromeHeadless` tests.

Install the pinned EF CLI if needed:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.20
dotnet ef --version
```

## Fastest start: Docker Compose

From the repository root:

```powershell
Copy-Item .env.example .env
```

Change both development secrets in `.env`, especially if the ports are reachable beyond localhost. Then run:

```powershell
docker compose up --build
```

The `migrations` container applies the checked-in EF migration and seed data before the API starts. Services are available at:

| Service | URL |
| --- | --- |
| Angular SPA | `http://localhost:4200` |
| Swagger UI | `http://localhost:8080/swagger` |
| API health | `http://localhost:8080/health` |
| SQL Server | `localhost:1433` |
| Azurite Blob | `localhost:10000` |

Stop without deleting data:

```powershell
docker compose down
```

To intentionally delete the named SQL Server and Azurite volumes as well:

```powershell
docker compose down --volumes
```

## Host-run development

This path runs SQL Server in Docker, then runs the API and Angular CLI on the host. Commands start at the repository root.

1. Prepare local environment values and start SQL Server:

```powershell
Copy-Item .env.example .env
docker compose up -d sqlserver
```

2. Set process-scoped API configuration. Keep this shell open for migration and API commands:

```powershell
$env:ConnectionStrings__Default = 'Server=localhost,1433;Database=ClaimsModule;User Id=sa;Password=ClaimsModule_dev_2026!;Encrypt=True;TrustServerCertificate=True'
$env:Auth__Jwt__Issuer = 'ClaimsModule.Development'
$env:Auth__Jwt__Audience = 'ClaimsModule.Client.Development'
$env:Auth__Jwt__SigningKey = 'replace-with-a-local-secret-at-least-32-bytes'
$env:Auth__Jwt__ExpirationMinutes = '480'
$env:Auth__DevLoginEnabled = 'true'
$env:Storage__Provider = 'LocalFileSystem'
$env:Storage__Local__RootPath = 'uploads'
$env:Storage__MaxFileSizeBytes = '52428800'
$env:Cors__Origins__0 = 'http://localhost:4200'
$env:Hangfire__DashboardEnabled = 'false'
```

The password must match `MSSQL_SA_PASSWORD` in `.env`. ASP.NET Core maps double underscores to configuration colons.

3. Apply the migration. This also inserts policies, causes of loss, status transitions, and demo users; there is no separate runtime seed command:

```powershell
dotnet ef database update --project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj --startup-project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj
```

4. Run the API on the port expected by the Angular development proxy:

```powershell
dotnet run --project backend/src/ClaimsModule.API/ClaimsModule.API.csproj --launch-profile http
```

Swagger is at `http://localhost:5020/swagger`; health is at `http://localhost:5020/health`. Hangfire creates its own `HangFire` schema at API startup, as explicitly permitted by DD-31.

5. In a second shell, install the locked frontend dependencies and start Angular:

```powershell
Set-Location frontend/claims-ui
npm ci
npm start
```

Open `http://localhost:4200`. The development proxy sends `/api` requests to `http://localhost:5020`.

### Reapply or inspect migrations

Apply all pending migrations and migration-owned seed data:

```powershell
dotnet ef database update --project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj --startup-project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj
```

Generate an idempotent deployment script without changing the database:

```powershell
dotnet ef migrations script --idempotent --project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj --startup-project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj --output artifacts/migrations.sql
```

Do not add startup seeding: D1 requires reference/demo data to come from EF migration seeding.

## Configuration reference

Never commit real SQL passwords, signing keys, storage keys, publish profiles, or deployment tokens. Values below are examples.

### API configuration

| JSON key | Environment key | Example | Purpose |
| --- | --- | --- | --- |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | `Server=localhost,1433;Database=ClaimsModule;User Id=sa;Password=<secret>;Encrypt=True;TrustServerCertificate=True` | Application and Hangfire SQL connection. Required. |
| `Storage:Provider` | `Storage__Provider` | `AzureBlob` or `LocalFileSystem` | Selects the storage implementation. Unknown values fail startup resolution. |
| `Storage:Azure:ConnectionString` | `Storage__Azure__ConnectionString` | `UseDevelopmentStorage=true` | Azure/Azurite shared-key connection. Azure mode falls back to local storage if this is missing or a repository placeholder. |
| `Storage:Azure:Container` | `Storage__Azure__Container` | `claim-documents` | Private blob container. |
| `Storage:Azure:PublicDownloadOrigin` | `Storage__Azure__PublicDownloadOrigin` | `http://localhost:10000` | Optional browser-reachable HTTP(S) origin. Compose uses this to replace the internal Azurite host in SAS URLs while preserving signed paths and queries. Omit for normal Azure endpoints. |
| `Storage:Local:RootPath` | `Storage__Local__RootPath` | `uploads` | Local fallback directory, resolved from the API working directory. |
| `Storage:MaxFileSizeBytes` | `Storage__MaxFileSizeBytes` | `52428800` | Document limit; 50 MiB by default. |
| `Auth:Jwt:Issuer` | `Auth__Jwt__Issuer` | `ClaimsModule.Development` | Required JWT issuer. |
| `Auth:Jwt:Audience` | `Auth__Jwt__Audience` | `ClaimsModule.Client.Development` | Required JWT audience. |
| `Auth:Jwt:SigningKey` | `Auth__Jwt__SigningKey` | `<random-secret-at-least-32-bytes>` | Required symmetric signing key; placeholders and short values are rejected. |
| `Auth:Jwt:ExpirationMinutes` | `Auth__Jwt__ExpirationMinutes` | `480` locally, `60` in Azure | Issued dev-token lifetime. |
| `Auth:DevLoginEnabled` | `Auth__DevLoginEnabled` | `true` | Exposes `POST /api/auth/dev-login`; intended only for the assessment/demo. |
| `Hangfire:DashboardEnabled` | `Hangfire__DashboardEnabled` | `false` | Maps `/hangfire` when true; access requires an authenticated Manager. |
| `Cors:Origins` | `Cors__Origins__0`, `Cors__Origins__1`, ... | `http://localhost:4200` | Exact permitted SPA origins, without trailing slash. |
| `Serilog:MinimumLevel:Default` | `Serilog__MinimumLevel__Default` | `Information` | Default structured-log threshold. |
| `Serilog:MinimumLevel:Override:Microsoft` | `Serilog__MinimumLevel__Override__Microsoft` | `Warning` | Framework logging override. |
| `Serilog:MinimumLevel:Override:Microsoft.Hosting.Lifetime` | `Serilog__MinimumLevel__Override__Microsoft__Hosting__Lifetime` | `Information` | Host lifecycle logging override. |
| `AllowedHosts` | `AllowedHosts` | `*` | ASP.NET Core host filtering. |
| — | `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` | Chooses environment settings and enables the local download endpoint only in Development. |
| — | `ASPNETCORE_URLS` or `ASPNETCORE_HTTP_PORTS` | `http://localhost:5020` or `8080` | Optional host binding override; the API container sets port `8080`. |

Kestrel's multipart request cap is fixed in code at 52 MiB to allow a 50 MiB file plus envelope overhead. Hosting proxies can impose a lower limit.

### Docker Compose inputs

| Key | Example | Purpose |
| --- | --- | --- |
| `MSSQL_SA_PASSWORD` | `ClaimsModule_dev_2026!` | SQL Server container SA password. Change it before use outside isolated local development. |
| `CLAIMS_JWT_SIGNING_KEY` | `claims-module-local-signing-key-change-me-2026` | Local container JWT secret. Change it. |
| `API_BASE_URL` | `http://localhost:8080` | API origin compiled into the production Angular container. |

### Frontend production build

The production Angular configuration replaces `environment.ts` with `environment.prod.ts`, which points to the supplied `diceus` Web App. The Docker build can still override that file through `API_BASE_URL` and `tools/configure-production-environment.mjs`. Local `npm start` uses `proxy.conf.json` and an empty API base URL.

### Test-only overrides

| Key | Example | Purpose |
| --- | --- | --- |
| `CLAIMS_TEST_SQLSERVER` | `Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True` | Use an existing SQL Server instead of the test fixture's SQL Testcontainer. |
| `CLAIMS_TEST_AZURE_BLOB` | `<full Azurite connection string>` | Use an existing Azurite instance instead of its Testcontainer. Real Azure endpoints are rejected by the fixture. |

### GitHub/Azure deployment settings

Repository/environment secrets:

- `AZURE_WEBAPP_PUBLISH_PROFILE`, the App Service publish profile XML.
- `AZURE_STATIC_WEB_APPS_API_TOKEN`, the Static Web Apps deployment token.

The resource names and frontend API URL are fixed in the production workflow/configuration. Azure SQL migrations are a manual Entra-authenticated step and require no CI secret.

## Seeded data and mock users

All seed rows use organisation `10000000-0000-0000-0000-000000000001` and are inserted by the initial EF migration.

Development login is username-only and must be enabled with `Auth:DevLoginEnabled=true`:

| Username | Display name | Role | Intended demo use |
| --- | --- | --- | --- |
| `handler` | Claims Handler | Handler | FNOL, parties, documents, reserves, normal transitions. |
| `supervisor` | Claims Supervisor | Supervisor | Handler actions plus reserve review to 100,000 and reassignment. |
| `manager` | Claims Manager | Manager | Supervisor actions plus approvals to 10,000,000 and aggregate override. |

The Angular header's role switcher calls `POST /api/auth/dev-login`; these are not password-backed production accounts.

Protected routes wait for shared session initialization. Expired sessions are replaced using the previous acting role; an API 401 triggers one shared login and one request retry. A failed login offers a retry in the header.

Seeded policies:

| Policy | Client | Effective through | Coverage | Seeded status |
| --- | --- | --- | --- | --- |
| `POL-2024-001001` | Meridian Transport LLC | 2024-01-01 — 2026-12-31 | Vehicle, Cargo | Active |
| `POL-2024-001002` | Harborview Properties Inc | 2024-06-01 — 2026-05-31 | Property, Liability | Expired |
| `POL-2025-002001` | Coastal Builders Group | 2025-03-01 — 2027-02-28 | Property, Equipment | Active |
| `POL-2025-002002` | Stanton Medical Group | 2025-01-01 — 2026-12-31 | Liability, Vehicle | Active |
| `POL-2023-000099` | Archived Corp | 2020-01-01 — 2021-12-31 | Property | Expired |

Ten active cause-of-loss rows are seeded: `COL-FIRE`, `COL-FLOOD`, `COL-THEFT`, `COL-VEH-COL`, `COL-VEH-COMP`, `COL-LIAB`, `COL-EQUIP`, `COL-WIND`, `COL-INJURY`, and `COL-OTHER`. The 12 allowed claim-status transition edges and their minimum roles/rule codes are seeded too.

## Feature walkthrough

1. Use the role switcher as `handler` and open **Log New Claim**.
2. Search a seeded policy or choose unknown policy, enter loss details, add at least one Claimant, optionally add risk objects and an initial reserve, review warnings, and submit. Claim creation is transactional and allocates `CLM-{year}-{seven digits}`.
3. Use the claims dashboard filters and open the new claim. Loss-date filters include whole UTC calendar days. The detail view exposes Overview, Parties, Reserves, Documents, and Audit tabs.
4. Edit notes, determine liability, link a missing policy, add/remove parties, and use allowed lifecycle transitions. Supervisor/Manager users can reassign a handler.
5. Add a reserve. Deltas at or below 10,000 auto-approve; larger deltas require a different authorized reviewer. The ledger supports Add, signed Adjust, Reverse, rejection, retraction, resubmission, manager aggregate override, and failed-posting retry. Closed/Withdrawn claims disable new reserves. A manager can see the aggregate override requirement on a fresh visit from approved balances and pending deltas. History groups components and orders each group by change sequence.
6. Observe posting status. Hangfire simulates GL posting after commit and records success/failure in the audit log. A recovery job finds approved pending rows that were not scheduled.
7. Upload PDF, JPEG, PNG, DOCX, XLSX, TXT, or CSV files up to 50 MiB. Azure mode returns one-hour read-only SAS URLs; Download refreshes the URL before opening it. Local mode uses a Development-only download route.
8. Review the reverse-chronological immutable audit log. Party audit links open the Parties tab and focus the retained active/inactive party. The SLA job adds a deduplicated marker after 48 hours of inactivity without changing claim status.

Swagger documents the complete current HTTP surface at `/swagger`.

## Tests and static checks

Backend:

```powershell
dotnet build backend/ClaimsModule.sln
dotnet test backend/tests/ClaimsModule.Domain.Tests/ClaimsModule.Domain.Tests.csproj
dotnet test backend/tests/ClaimsModule.Application.Tests/ClaimsModule.Application.Tests.csproj
dotnet test backend/tests/ClaimsModule.IntegrationTests/ClaimsModule.IntegrationTests.csproj
```

Integration tests use SQL Server and Azurite Testcontainers when Docker is available. See `docs/STORAGE-TESTING.md` for the Azure Blob category and optional emulator override.

Frontend:

```powershell
Set-Location frontend/claims-ui
npm ci
npm run test:ci -- --no-progress
npm run test:http-boundary
npm run build -- --configuration production
```

The HTTP-boundary check prevents components from bypassing the typed service layer. CI runs the same backend/frontend build and test families in `.github/workflows/ci.yml`.

## Azure deployment and reproduction

The prepared review environment uses Web App `diceus`, Static Web App `Diceus`, Azure SQL server `markiiansqlserver`/database `claims`, and Blob account `markiianstorage`. Exact settings are in [`infra/APP-SETTINGS.md`](infra/APP-SETTINGS.md); the full operator runbook is [`infra/README-deploy.md`](infra/README-deploy.md).

Deployment order is:

1. Run `az login`; enable the Web App system identity, HTTPS-only, and Always On.
2. Enable SQL public access for the review topology, add the developer IP firewall rule, and allow Azure services.
3. As the Entra database administrator, run `CREATE USER [diceus] FROM EXTERNAL PROVIDER;` and add it to `db_owner` so Hangfire can install its permitted schema.
4. Install `dotnet-ef` 9.0.20 and manually run `dotnet ef database update` from the developer machine with an `Authentication=Active Directory Default` connection after `az login`. CI deliberately does not migrate the Entra-only database.
5. Create/verify `markiianstorage` container `claim-documents`, then configure the App Service settings, including the managed-identity runtime SQL connection, Blob shared-key connection string, JWT secret, exact Static Web App CORS origin, and demo `Auth__DevLoginEnabled=true`.
6. Add GitHub secrets `AZURE_WEBAPP_PUBLISH_PROFILE` and `AZURE_STATIC_WEB_APPS_API_TOKEN`, then manually run **Deploy to Azure**. If publish profiles are unsuitable, use `az webapp deploy` as documented; SCM basic auth may be disabled.
7. Check `/health`, then call `/health/ready` repeatedly for up to roughly a minute while serverless SQL wakes. Verify `/swagger`, the Static Web App, dev-login, a claim/reserve flow, and Blob upload/download.

The exact public URLs are `https://diceus-adfdbwarg6crc0b8.polandcentral-01.azurewebsites.net` and `https://gray-water-0ee27680f.3.azurestaticapps.net`. This repository prepares deployment but does not claim those endpoints were contacted or verified.

## Known limitations

- No public Azure URLs or live-resource evidence are committed. Infrastructure and workflow reproduction are present; deployment status must not be inferred.
- Demo authentication is symmetric JWT plus username-only dev login. It is not an identity-provider integration and should be disabled after review.
- The seed set is one organisation. Tenant filters exist, but there is no tenant administration UI.
- Azure Blob uses an account key to generate SAS URLs. Managed identity/user-delegation SAS is not implemented, and local tests exercise Azurite rather than the real Azure service.
- The Development-only local download route is anonymous and has no expiry. It has path-traversal protection; public deployments must use Azure Blob.
- A file can remain orphaned if storage upload succeeds and the later SQL metadata/audit transaction rolls back. There is no cross-system compensation or cleanup job.
- With an `Idempotency-Key`, the current middleware covers every authenticated non-multipart POST except claim validation. That is broader than DD-32's named endpoint list; multipart upload and PUT/DELETE do not use it. Repeating the reserve adjustment PUT appends another signed delta.
- GL enqueue is not backed by an outbox. The five-minute recovery job repairs approved rows left Pending with no job ID after two minutes, giving at-least-once behavior for that workflow.
- Reserve amounts are single-currency decimals; no currency code, exchange rate, or currency-specific aggregation exists.
- Hangfire runs inside the API process and therefore depends on an Always On App Service plan; it is not a separately scaled worker.
- The application limit defaults to 50 MiB, but an administrator can configure a larger value while the code-level multipart request cap remains 52 MiB.
- AutoMapper is pinned to 14.0.0 by the repository package policy. Existing review documentation records the unresolved NU1903 advisory; current flat mappings use explicit depth limits, but the dependency is not a patched release.
- Key Vault provisioning is optional and creates an empty vault only; secret population and access policies are not automated.

Additional implementation evidence and historical verification notes are under `docs/`.
