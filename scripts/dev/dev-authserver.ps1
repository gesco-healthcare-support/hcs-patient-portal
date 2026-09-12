# Run the AuthServer on the host with hot reload via `dotnet watch`.
# Talks to dockerized SQL Server on 127.0.0.1:1434 and Redis on 127.0.0.1:6379.
#
# Usage: .\scripts\dev\dev-authserver.ps1
#
# Prerequisites: docker compose stack must have sql-server, redis, db-migrator
# running (and angular if you want full UI). Bring those up via:
#   docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d sql-server redis db-migrator angular

param(
    [string]$Port = "44368",
    [string]$SqlHostPort = "1434",
    [string]$RedisHostPort = "6379"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$ProjectPath = Join-Path $RepoRoot "src\HealthcareSupport.CaseEvaluation.AuthServer\HealthcareSupport.CaseEvaluation.AuthServer.csproj"

# Required env (mirrors what docker-compose.yml sets for the AuthServer service).
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://+:$Port"

$env:App__SelfUrl = "http://localhost:$Port"
$env:AuthServer__Authority = "http://localhost:$Port"
$env:AuthServer__RequireHttpsMetadata = "false"

# Subdomain CORS support (ADR-006). Bare localhost + wildcard subdomains.
$env:App__CorsOrigins = "http://localhost:4200,http://localhost:44327,http://*.localhost:4200,http://*.localhost:44327"

# Connection strings — sql-server in Docker is exposed at 127.0.0.1:1434.
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

# Encryption + ABP license — also from .env if not already set.
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

Write-Host "AuthServer hot-reload starting on http://localhost:$Port" -ForegroundColor Cyan
Write-Host "  SQL: 127.0.0.1:$SqlHostPort/$dbName" -ForegroundColor DarkGray
Write-Host "  Redis: 127.0.0.1:$RedisHostPort" -ForegroundColor DarkGray
Write-Host ""

dotnet watch run --no-launch-profile --project $ProjectPath --urls "http://+:$Port"
