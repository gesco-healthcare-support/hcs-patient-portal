<#
.SYNOPSIS
  One-time Docker disk reclaim. The non-destructive prune runs by default; the
  VHD-shrink step is OPT-IN because both available methods have caveats.

.DESCRIPTION
  Default run (no switches): prunes unused build cache + dangling images while
  the stack is up. Safe, non-disruptive, reclaims space INSIDE the VHD (stops
  runaway growth) but does NOT shrink the .vhdx file on the host.

  Returning freed space to the HOST disk requires shrinking the .vhdx, and the
  two methods both have caveats discovered 2026-06-05:

  A) WSL sparse auto-shrink -- `wsl --manage docker-desktop --set-sparse true`
     now REQUIRES `--allow-unsafe`; Windows gates it with an explicit
     "potential data corruption" warning. This VHD holds the seeded SQL demo
     data, so this script will NOT enable sparse unless you pass BOTH
     -EnableSparse AND -IHaveBackedUpVolumes (back the data up first).

  B) Optimize-VHD -Mode Full -- no corruption risk, but needs an ELEVATED shell
     you launch yourself, and reclaims little unless the guest first runs
     `fstrim` (WSL2 ext4 freed blocks are not auto-unmapped). Steps at bottom.

  WHY no `--volumes` / `container prune` here: protects the seeded SQL demo data.

.PARAMETER EnableSparse
  Enable WSL sparse auto-shrink (method A). Requires -IHaveBackedUpVolumes.

.PARAMETER IHaveBackedUpVolumes
  Asserts you have a recoverable backup of the SQL demo-data volume.

.NOTES
  Plan: docs/plans/2026-06-04-docker-disk-automation.md
#>
[CmdletBinding()]
param(
    [switch]$EnableSparse,
    [switch]$IHaveBackedUpVolumes
)
$ErrorActionPreference = 'Stop'
$vhd = Join-Path $env:LOCALAPPDATA 'Docker\wsl\disk\docker_data.vhdx'

Write-Host '[1/2] Pruning build cache + dangling images (non-disruptive)...'
docker builder prune -f | Out-Null   # NO -a (leaves cache for running stacks)
docker image prune -f   | Out-Null   # dangling images only

if (-not $EnableSparse) {
    Write-Host ''
    Write-Host 'Prune done. VHD file NOT shrunk (host space unchanged). To reclaim host space:'
    Write-Host '  A) Sparse auto-shrink: back up the SQL volume, then re-run with'
    Write-Host '     -EnableSparse -IHaveBackedUpVolumes  (accepts the corruption-gate risk).'
    Write-Host '  B) Optimize-VHD (safe): see the fstrim + Optimize steps in this file header.'
    return
}

if (-not $IHaveBackedUpVolumes) {
    throw 'Refusing to enable sparse without -IHaveBackedUpVolumes. Back up the seeded SQL volume first; sparse mode is gated by Windows as potential data corruption.'
}

Write-Host '[2/2] Enabling WSL sparse VHD (you confirmed a volume backup exists)...'
Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 5
wsl --shutdown
wsl --manage docker-desktop --set-sparse true --allow-unsafe
fsutil sparse queryflag "$vhd"
Start-Process (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe')
Write-Host 'Sparse enabled; the VHD will trim over time. Bring stacks back with docker compose up.'

# --- Method B (safe, manual, ELEVATED shell) -------------------------------
#   1. (optional but effective) trim inside the distro so freed blocks unmap:
#        wsl -d docker-desktop -e fstrim -av     # if fstrim is present
#   2. wsl --shutdown
#   3. In an ADMIN PowerShell:
#        Mount-VHD -Path "$env:LOCALAPPDATA\Docker\wsl\disk\docker_data.vhdx" -ReadOnly
#        Optimize-VHD -Path "$env:LOCALAPPDATA\Docker\wsl\disk\docker_data.vhdx" -Mode Full
#        Dismount-VHD -Path "$env:LOCALAPPDATA\Docker\wsl\disk\docker_data.vhdx"
#   4. Restart Docker Desktop.
