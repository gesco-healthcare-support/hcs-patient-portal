# Bring up the parts of the stack that stay in Docker (sql-server, redis,
# db-migrator, angular). The api + authserver run on the host via
# dev-api.ps1 + dev-authserver.ps1 in two separate terminals.
#
# Usage: .\scripts\dev\start-dev-stack.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
Set-Location $RepoRoot

Write-Host "Starting Docker side: sql-server, redis, db-migrator, angular" -ForegroundColor Cyan
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d sql-server redis db-migrator angular

Write-Host ""
Write-Host "Waiting for sql-server + redis healthy..." -ForegroundColor DarkGray
$timeout = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $timeout) {
    $sql = docker inspect --format '{{.State.Health.Status}}' replicate-old-app-sql-server-1 2>$null
    $redis = docker inspect --format '{{.State.Health.Status}}' replicate-old-app-redis-1 2>$null
    if ($sql -eq "healthy" -and $redis -eq "healthy") { break }
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "Docker side up. Now run these in TWO separate PowerShell windows:" -ForegroundColor Green
Write-Host ""
Write-Host "  Terminal A:  .\scripts\dev\dev-authserver.ps1" -ForegroundColor Yellow
Write-Host "  Terminal B:  .\scripts\dev\dev-api.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "Then visit http://falkinstein.localhost:4200/ once both `Now listening on` lines appear." -ForegroundColor DarkGray
