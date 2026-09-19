<#
.SYNOPSIS
  One-shot SAFE compaction of the Docker WSL2 VHD via Optimize-VHD (read-only).
  Invoked by a temporary SYSTEM scheduled task so it runs elevated without a UAC
  prompt. RO mount means the guest filesystem is never written -- no corruption
  risk (this is the SAFE alternative to WSL sparse mode).

  PRECONDITION: WSL must be shut down (`wsl --shutdown`) so the VHD is detached.
  Logs to %LOCALAPPDATA%\docker-vhd-compact.log. Self-contained; safe to re-run.
#>
$ErrorActionPreference = 'Stop'
$vhd = 'C:\Users\RajeevG\AppData\Local\Docker\wsl\disk\docker_data.vhdx'
$log = 'C:\Users\RajeevG\AppData\Local\docker-vhd-compact.log'
function Write-Log([string]$m) { "{0}  {1}" -f (Get-Date -Format 's'), $m | Out-File -FilePath $log -Append -Encoding utf8 }

try {
    Write-Log ("START. size before: {0:N2} GB" -f ((Get-Item $vhd).Length / 1GB))
    Mount-VHD -Path $vhd -ReadOnly
    Write-Log 'mounted read-only'
    Optimize-VHD -Path $vhd -Mode Full
    Write-Log 'Optimize-VHD -Mode Full completed'
}
catch {
    Write-Log ("ERROR: " + $_.Exception.Message)
}
finally {
    Dismount-VHD -Path $vhd -ErrorAction SilentlyContinue
    Write-Log ("DISMOUNTED. size after: {0:N2} GB" -f ((Get-Item $vhd).Length / 1GB))
}
