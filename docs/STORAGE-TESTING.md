# Storage integration tests

The Azure Blob integration tests use Azurite only; they never connect to an Azure Storage account. By default the test fixture starts `mcr.microsoft.com/azure-storage/azurite:3.35.0` with Testcontainers. The SQL-backed API cases continue to use the existing SQL Server Testcontainer.

From the repository root, with Docker Desktop's Linux engine running:

```powershell
dotnet test backend/tests/ClaimsModule.IntegrationTests/ClaimsModule.IntegrationTests.csproj -c Release --filter "Category=AzureBlob"
```

Expected result: **6 total, 6 passed, 0 failed, 0 skipped**. These are three direct Azure storage tests, two SQL-backed document API tests, and one provider-fallback/logging test.

To use an already-running Azurite instance instead of starting a container, set the optional variable to its full connection string before running the same command:

```powershell
$env:CLAIMS_TEST_AZURE_BLOB = '<full Azurite connection string using the public devstoreaccount1 development credentials>'
dotnet test backend/tests/ClaimsModule.IntegrationTests/ClaimsModule.IntegrationTests.csproj -c Release --filter "Category=AzureBlob"
```

The supplied connection must target Azurite and use the well-known `devstoreaccount1` development account. Real Azure endpoints and credentials are intentionally rejected. Remove the process-scoped override with:

```powershell
Remove-Item Env:CLAIMS_TEST_AZURE_BLOB
```

To run all backend tests and the release build:

```powershell
dotnet build backend/ClaimsModule.sln -c Release
dotnet test backend/tests/ClaimsModule.Domain.Tests/ClaimsModule.Domain.Tests.csproj -c Release
dotnet test backend/tests/ClaimsModule.Application.Tests/ClaimsModule.Application.Tests.csproj -c Release
dotnet test backend/tests/ClaimsModule.IntegrationTests/ClaimsModule.IntegrationTests.csproj -c Release
```

With Docker available, the expected complete result is **517 total, 517 passed, 0 failed, 0 skipped**: 261 Domain, 139 Application, and 117 Integration tests.

`AzureBlobStorageService` currently creates account-key read-only SAS URLs through `BlobClient.GenerateSasUri`; Azurite exercises that production path. The application does not currently implement managed identity or user-delegation SAS. Compatibility with the real Azure service is intentionally not exercised by this local test suite.
