# Run the HttpApi.Host on the host with hot reload via `dotnet watch`.
# Pairs with dev-authserver.ps1; both should be running for the SPA to work.
#
# Usage: .\scripts\dev\dev-api.ps1

param(
    [string]$Port = "44327",
    [string]$AuthPort = "44368",
    [string]$SqlHostPort = "1434",
    [string]$RedisHostPort = "6379"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$ProjectPath = Join-Path $RepoRoot "src\HealthcareSupport.CaseEvaluation.HttpApi.Host\HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://+:$Port"

$env:App__SelfUrl = "http://localhost:$Port"
$env:AuthServer__Authority = "http://localhost:$AuthPort"
# Talk to AuthServer on host (not the docker hostname) since both services
# run on host now.
$env:AuthServer__MetaAddress = "http://localhost:$AuthPort"
$env:AuthServer__RequireHttpsMetadata = "false"

$env:App__CorsOrigins = "http://localhost:4200,http://localhost:$AuthPort,http://*.localhost:4200,http://*.localhost:$AuthPort"

$sqlPwd = $env:MSSQL_SA_PASSWORD
if (-not $sqlPwd) {
    $envFile = Join-Path $RepoRoot ".env"
    if (Test-Path $envFile) {
        $line = (Get-Content $envFile | Select-String "^MSSQL_SA_PASSWORD=" | Select-Object -First 1).ToString()
        if ($line) { $sqlPwd = ($line -split "=", 2)[1] }
    }
}
if (-not $sqlPwd) { Write-Error "MSSQL_SA_PASSWORD not set (env var or .env file)"; exit 1 }

$dbName = if ($env:DB_NAME) { $env:DB_NAME } else { "CaseEvaluation" }
$env:ConnectionStrings__Default = "Server=127.0.0.1,$SqlHostPort;Database=$dbName;User Id=sa;Password=$sqlPwd;TrustServerCertificate=True"
$env:Redis__Configuration = "127.0.0.1:$RedisHostPort"

foreach ($key in @('STRING_ENCRYPTION_PASSPHRASE','ABP_LICENSE_CODE')) {
    if (-not [Environment]::GetEnvironmentVariable($key)) {
        $envFile = Join-Path $RepoRoot ".env"
        if (Test-Path $envFile) {
            $val = (Get-Content $envFile | Select-String "^$key=" | Select-Object -First 1)
            if ($val) { [Environment]::SetEnvironmentVariable($key, ($val.ToString() -split "=", 2)[1]) }
        }
    }
}
$env:StringEncryption__DefaultPassPhrase = $env:STRING_ENCRYPTION_PASSPHRASE
$env:AbpLicenseCode = $env:ABP_LICENSE_CODE

Write-Host "API hot-reload starting on http://localhost:$Port" -ForegroundColor Cyan
Write-Host "  SQL: 127.0.0.1:$SqlHostPort/$dbName" -ForegroundColor DarkGray
Write-Host "  Redis: 127.0.0.1:$RedisHostPort" -ForegroundColor DarkGray
Write-Host "  AuthServer: http://localhost:$AuthPort" -ForegroundColor DarkGray
Write-Host ""

dotnet watch run --no-launch-profile --project $ProjectPath --urls "http://+:$Port"
