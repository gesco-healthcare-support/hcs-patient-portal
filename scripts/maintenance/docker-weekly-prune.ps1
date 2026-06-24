<#
.SYNOPSIS
  Weekly conservative Docker cleanup for the Patient Portal dev machine (Layer 3).

.DESCRIPTION
  Backstop to the BuildKit GC ceiling in ~/.docker/daemon.json. Removes only the
  safe-to-delete leftovers: dangling (untagged) images, build cache unused for
  more than 7 days, and containers stopped for more than 7 days. Safe to run
  whether or not the dev stack is up -- it never touches tagged images that are
  in use, never touches named volumes, and leaves recent build cache intact for
  fast rebuilds. Registered as a Windows Scheduled Task ("Docker Weekly Prune").

  WHY no `docker system prune --volumes` / `docker volume prune`: the SQL Server
  demo data (the seeded Falkinstein tenant) lives in a named volume. Pruning
  volumes while the stack is down would destroy it and force a full re-seed --
  including the HIPAA-synthetic data rebuild. Volume cleanup must stay manual.

  WHY no `docker builder prune -a`: `-a` wipes cache backing running stacks,
  defeating the purpose of a non-disruptive weekly job (per the 2026-05-27 plan).

.NOTES
  Plan: docs/plans/2026-06-04-docker-disk-automation.md
  Log:  %LOCALAPPDATA%\docker-weekly-prune.log
#>
$ErrorActionPreference = 'Stop'
$logFile = Join-Path $env:LOCALAPPDATA 'docker-weekly-prune.log'

function Write-Log([string]$message) {
    "{0}  {1}" -f (Get-Date -Format 's'), $message | Tee-Object -FilePath $logFile -Append
}

# No-op gracefully if the Docker daemon is not reachable (e.g. machine idle).
try { docker info *> $null } catch { Write-Log 'Docker daemon not reachable; skipping.'; return }

Write-Log '=== weekly prune start ==='
(docker system df) | Tee-Object -FilePath $logFile -Append

docker image prune -f                       | Out-Null   # dangling images only
docker builder prune -f --filter 'until=168h' | Out-Null # build cache unused > 7 days
docker container prune -f --filter 'until=168h' | Out-Null # containers stopped > 7 days

(docker system df) | Tee-Object -FilePath $logFile -Append
Write-Log '=== weekly prune done ==='
