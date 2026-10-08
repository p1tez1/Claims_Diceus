# Azure App Service settings

Set these on Linux Web App `diceus`. ASP.NET Core maps the double underscores to configuration section separators. Values marked secret must be stored only in App Service settings (preferably backed by Key Vault), never in source control.

| App Service setting | Purpose | Secret | Value source |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Loads Production configuration and enables fail-fast validation. | No | Literal `Production`. |
| `ConnectionStrings__Default` | Shared EF Core and Hangfire Azure SQL connection. | No | Template below, using the Web App system identity; treat operational connection details as sensitive. |
| `Storage__Provider` | Selects document storage. | No | Literal `AzureBlob`. |
| `Storage__Azure__ConnectionString` | Authenticates to `markiianstorage` and enables account-key SAS generation. | Yes | Storage account access-key connection string from Azure. |
| `Storage__Azure__Container` | Private document container. | No | Literal `claim-documents`. |
| `Storage__MaxFileSizeBytes` | Application upload limit. | No | Literal `52428800`. |
| `Auth__Jwt__Issuer` | JWT issuer validated by the API. | No | Stable demo value, for example `ClaimsModule`. |
| `Auth__Jwt__Audience` | JWT audience validated by the API. | No | Stable demo value, for example `ClaimsModule.Client`. |
| `Auth__Jwt__SigningKey` | Symmetric JWT signing key; startup requires at least 32 UTF-8 bytes. | Yes | Cryptographically random operator-generated secret. |
| `Auth__Jwt__ExpirationMinutes` | Dev-login token lifetime. | No | Literal `60`, unless the operator chooses another demo lifetime. |
| `Auth__DevLoginEnabled` | Exposes the assessment's username-only login. A Production warning is logged when enabled. | No | Literal `true` for this demo; set `false` after review. |
| `Cors__Origins__0` | Exact allowed SPA origin; Production rejects an empty list or wildcards. | No | `https://gray-water-0ee27680f.3.azurestaticapps.net` |
| `Hangfire__DashboardEnabled` | Controls `/hangfire`; even when enabled it requires an authenticated Manager. | No | Literal `false` for Production. |

Azure SQL managed-identity connection string (no credentials):

```text
Server=tcp:markiiansqlserver.database.windows.net,1433;Initial Catalog=claims;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

`Storage__Azure__PublicDownloadOrigin` is not needed for Azure; it exists only for an emulator/reverse-proxy origin override. `Storage__Local__RootPath` is not needed when Azure Blob is selected. `WEBSITES_PORT` is not needed for the .NET 9 built-in App Service stack. Enable HTTPS-only and Always On as platform settings rather than application environment variables.

The current Blob implementation accepts a shared-key connection string and generates account-key SAS URLs. It has no managed-identity Blob mode.
