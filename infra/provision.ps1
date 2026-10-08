[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9-]{2,15}$')]
    [string] $ResourcePrefix,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9]{3,8}$')]
    [string] $UniqueSuffix,

    [string] $Location = 'westeurope',
    [string] $StaticWebAppLocation = 'westeurope',
    [string] $SubscriptionId,
    [string] $SqlAdministrator = 'claimsadmin',
    [ValidateSet('Basic', 'S0', 'S1')]
    [string] $SqlDatabaseSku = 'Basic',
    [switch] $CreateKeyVault
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required. Install it and run az login before this script.'
}
if ([string]::IsNullOrWhiteSpace($env:AZURE_SQL_ADMIN_PASSWORD)) {
    throw 'Set AZURE_SQL_ADMIN_PASSWORD in the current process. The script never stores it in the repository.'
}

if ($SubscriptionId) {
    az account set --subscription $SubscriptionId
}
az account show --output none

$compactPrefix = $ResourcePrefix.Replace('-', '')
$resourceGroup = "$ResourcePrefix-$UniqueSuffix-rg"
$sqlServer = "$ResourcePrefix-$UniqueSuffix-sql"
$sqlDatabase = 'ClaimsModule'
$storageAccount = ("$compactPrefix$UniqueSuffix" + 'store').Substring(0, [Math]::Min(24, ("$compactPrefix$UniqueSuffix" + 'store').Length))
$appServicePlan = "$ResourcePrefix-$UniqueSuffix-plan"
$webApp = "$ResourcePrefix-$UniqueSuffix-api"
$staticWebApp = "$ResourcePrefix-$UniqueSuffix-web"
$keyVault = ("$ResourcePrefix-$UniqueSuffix-kv").Substring(0, [Math]::Min(24, ("$ResourcePrefix-$UniqueSuffix-kv").Length))

Write-Host "Provisioning resource group $resourceGroup in $Location..."
az group create --name $resourceGroup --location $Location --output none

az sql server create `
    --resource-group $resourceGroup `
    --name $sqlServer `
    --location $Location `
    --admin-user $SqlAdministrator `
    --admin-password $env:AZURE_SQL_ADMIN_PASSWORD `
    --enable-public-network true `
    --output none
az sql db create `
    --resource-group $resourceGroup `
    --server $sqlServer `
    --name $sqlDatabase `
    --service-objective $SqlDatabaseSku `
    --backup-storage-redundancy Local `
    --output none

# This demo rule permits Azure-hosted deployment agents. Remove it in a hardened setup and use a private/self-hosted runner.
az sql server firewall-rule create `
    --resource-group $resourceGroup `
    --server $sqlServer `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none

az storage account create `
    --resource-group $resourceGroup `
    --name $storageAccount `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --min-tls-version TLS1_2 `
    --allow-blob-public-access false `
    --output none
$storageKey = az storage account keys list `
    --resource-group $resourceGroup `
    --account-name $storageAccount `
    --query '[0].value' `
    --output tsv
az storage container create `
    --name claim-documents `
    --account-name $storageAccount `
    --account-key $storageKey `
    --public-access off `
    --output none

az appservice plan create `
    --resource-group $resourceGroup `
    --name $appServicePlan `
    --location $Location `
    --is-linux `
    --sku B1 `
    --output none
az webapp create `
    --resource-group $resourceGroup `
    --plan $appServicePlan `
    --name $webApp `
    --runtime 'DOTNETCORE:9.0' `
    --output none
az webapp config set `
    --resource-group $resourceGroup `
    --name $webApp `
    --always-on true `
    --http20-enabled true `
    --output none
az webapp update `
    --resource-group $resourceGroup `
    --name $webApp `
    --https-only true `
    --output none

az extension add --name staticwebapp --upgrade --output none
az staticwebapp create `
    --resource-group $resourceGroup `
    --name $staticWebApp `
    --location $StaticWebAppLocation `
    --sku Free `
    --output none

if ($CreateKeyVault) {
    az keyvault create `
        --resource-group $resourceGroup `
        --name $keyVault `
        --location $Location `
        --enable-rbac-authorization true `
        --output none
}

$staticHost = az staticwebapp show --resource-group $resourceGroup --name $staticWebApp --query defaultHostname --output tsv
$sqlHost = "$sqlServer.database.windows.net"
$blobConnection = "DefaultEndpointsProtocol=https;AccountName=$storageAccount;AccountKey=<storage-key>;EndpointSuffix=core.windows.net"

Write-Host ''
Write-Host 'Provisioning complete. Resource outputs:' -ForegroundColor Green
Write-Host "  Resource group:       $resourceGroup"
Write-Host "  SQL server/database:  $sqlHost / $sqlDatabase"
Write-Host "  Storage/container:    $storageAccount / claim-documents"
Write-Host "  Backend URL:          https://$webApp.azurewebsites.net"
Write-Host "  Frontend URL:         https://$staticHost"
if ($CreateKeyVault) { Write-Host "  Key Vault:            $keyVault" }

Write-Host ''
Write-Host 'App Service settings checklist (set values in Portal/CLI; placeholders are intentional):' -ForegroundColor Yellow
@(
    'ConnectionStrings__Default=Server=tcp:' + $sqlHost + ',1433;Initial Catalog=' + $sqlDatabase + ';User ID=<runtime-user>;Password=<runtime-password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;',
    'Storage__Provider=AzureBlob',
    'Storage__Azure__ConnectionString=' + $blobConnection,
    'Storage__Azure__Container=claim-documents',
    'Storage__MaxFileSizeBytes=52428800',
    'Auth__Jwt__Issuer=ClaimsModule',
    'Auth__Jwt__Audience=ClaimsModule.Client',
    'Auth__Jwt__SigningKey=<random-secret-at-least-32-bytes>',
    'Auth__Jwt__ExpirationMinutes=60',
    'Auth__DevLoginEnabled=true',
    'Cors__Origins__0=https://' + $staticHost,
    'Hangfire__DashboardEnabled=false',
    'ASPNETCORE_ENVIRONMENT=Production'
) | ForEach-Object { Write-Host "  $_" }

Write-Host ''
Write-Host 'GitHub configuration:' -ForegroundColor Yellow
Write-Host "  Variable AZURE_WEBAPP_NAME=$webApp"
Write-Host "  Variable PRODUCTION_API_URL=https://$webApp.azurewebsites.net"
Write-Host '  Secret AZURE_SQL_CONNECTION_STRING=<migration/DDL connection string>'
Write-Host '  Secret AZURE_WEBAPP_PUBLISH_PROFILE=<downloaded publish profile XML>'
Write-Host '  Secret AZURE_STATIC_WEB_APPS_API_TOKEN=<Static Web App deployment token>'
Write-Host ''
Write-Host 'See infra/README-deploy.md before the first deployment.'

