# Deploy the existing Azure review environment

This runbook targets only the supplied resources. It does not provision or contact Azure automatically.

- Resource group: `test`
- API: Linux .NET 9 Web App `diceus`, `https://diceus-adfdbwarg6crc0b8.polandcentral-01.azurewebsites.net`
- SPA: Static Web App `Diceus`, `https://gray-water-0ee27680f.3.azurestaticapps.net`
- SQL: `markiiansqlserver.database.windows.net`, database `claims`, Microsoft Entra-only
- Blob: `markiianstorage`, private container `claim-documents`

The API and in-process Hangfire server share Azure SQL. EF retries transient SQL failures. The lightweight `/health` endpoint does not touch SQL; `/health/ready` performs a five-second database connectivity check. Recurring-job registration retries in a background service, so a paused serverless database does not make application startup fail.

## Ordered manual setup and first deployment

1. Install Azure CLI and .NET SDK 9, then install the EF tool version matching the projects:

   ```powershell
   dotnet tool install --global dotnet-ef --version 9.0.20
   az login
   az account show
   ```

2. On Web App `diceus`, enable **System assigned managed identity**, **HTTPS Only**, and **Always On**. Always On is required because Hangfire runs in the web process.

3. On SQL server `markiiansqlserver`, keep public network access enabled for this review topology. Add the developer machine's current public IP to the SQL firewall. Enable the Azure-services firewall rule so App Service can reach SQL. This is broader than a private endpoint and should be replaced for a hardened environment.

4. Connect to database `claims` as a Microsoft Entra administrator and create the contained user matching the Web App identity, then grant the role needed by both the application and Hangfire's schema installer:

   ```sql
   CREATE USER [diceus] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_owner ADD MEMBER [diceus];
   ```

   `db_owner` is intentional for this assessment because DD-31 permits Hangfire to create and maintain its `HangFire` schema. A split worker/schema-owner model would be preferable in a hardened system.

5. Apply EF migrations manually from the logged-in developer machine. Do not run migrations in GitHub Actions. Temporarily set the connection string for the EF design-time factory:

   ```powershell
   $env:ConnectionStrings__Default = 'Server=tcp:markiiansqlserver.database.windows.net,1433;Initial Catalog=claims;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
   dotnet ef database update --project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj --startup-project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj
   Remove-Item Env:ConnectionStrings__Default
   ```

   The signed-in identity must have DDL rights. The first connection after serverless auto-pause can take about a minute or fail transiently; retry the command if Azure SQL is still resuming.

6. Create the private Blob container `claim-documents` in storage account `markiianstorage` if it does not already exist. Copy a storage access-key connection string for the App Service setting; do not commit it.

7. Configure every setting listed in [`APP-SETTINGS.md`](APP-SETTINGS.md) on `diceus`, including the managed-identity SQL template, Blob connection string, random JWT key, exact Static Web App CORS origin, and `Auth__DevLoginEnabled=true`. Restart the Web App after saving settings.

8. In GitHub, create the `production` environment and add exactly these secrets:

   - `AZURE_WEBAPP_PUBLISH_PROFILE`: the complete publish profile for Web App `diceus`.
   - `AZURE_STATIC_WEB_APPS_API_TOKEN`: the deployment token for Static Web App `Diceus`.

   Run **Actions → Deploy to Azure → Run workflow**. The workflow builds and tests both applications, publishes the API, deploys to `diceus`, checks `/health` with retries, and uploads `frontend/claims-ui/dist/claims-ui/browser` to the Static Web App. It never runs EF migrations.

9. If publish-profile deployment is unavailable, create a Release publish directory and use Azure CLI as an alternative:

   ```powershell
   dotnet publish backend/src/ClaimsModule.API/ClaimsModule.API.csproj -c Release -o artifacts/backend
   Compress-Archive -Path artifacts/backend/* -DestinationPath artifacts/backend.zip -Force
   az webapp deploy --resource-group test --name diceus --src-path artifacts/backend.zip --type zip
   ```

   This command requires Azure RBAC. It may fail if the Web App's SCM basic authentication is disabled and the selected CLI/auth path depends on it; use an RBAC-supported deployment path or deliberately enable SCM basic auth according to the organization's policy.

10. Smoke-test in order:

    - `https://diceus-adfdbwarg6crc0b8.polandcentral-01.azurewebsites.net/health` — HTTP 200 without a database dependency.
    - `https://diceus-adfdbwarg6crc0b8.polandcentral-01.azurewebsites.net/health/ready` — HTTP 200 with `Ready`; retry for about a minute while paused SQL resumes.
    - `https://diceus-adfdbwarg6crc0b8.polandcentral-01.azurewebsites.net/swagger` — public Swagger UI.
    - `https://gray-water-0ee27680f.3.azurestaticapps.net` — SPA loads and can call dev-login without a CORS error.
    - Use Swagger or the SPA to call `POST /api/auth/dev-login` with `handler`, create a claim/reserve, and upload/download a small allowed document.

## Operational notes

The production bundle includes `staticwebapp.config.json` beside `index.html`, with an SPA navigation fallback that excludes static assets. The deployment output is `frontend/claims-ui/dist/claims-ui/browser`.

The Hangfire dashboard remains disabled. If explicitly enabled, its authorization filter still requires a Manager JWT, but keeping it off avoids exposing an operational surface. The `/__test` endpoints are registered only in the `Testing` host environment.

When review ends, disable dev-login, rotate the JWT/storage/deployment secrets, remove unnecessary SQL firewall rules, and consider replacing public SQL access, `db_owner`, and the Blob account key with private networking and narrower identities.
