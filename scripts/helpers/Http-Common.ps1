<#
.SYNOPSIS
    Request building and error reading shared by Invoke-ApiCall and Invoke-TestApiCall.
.DESCRIPTION
    Dot-sourced by Invoke-ApiCall.ps1 and Assert-Response.ps1 themselves, so a script that
    loads either one alone still gets these. The two callers deliberately differ in how they
    treat a failure (which body wins, what an unknown status is called); those decisions stay
    in the callers and only the mechanics live here.
#>

function ConvertTo-RequestBody {
    param([object]$Body)

    if ($Body -is [string]) { return $Body }
    return ($Body | ConvertTo-Json -Depth 10 -Compress)
}

function Get-ApiRequest {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = "",
        [string]$TenantId = "",
        [int]$TimeoutSec = 30
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($TenantId) { $headers["__tenant"] = $TenantId }

    $params = @{
        Uri        = $Url
        Method     = $Method
        Headers    = $headers
        TimeoutSec = $TimeoutSec
    }

    if ($Body -and $Method -in @("POST", "PUT")) {
        $params["Body"] = ConvertTo-RequestBody -Body $Body
        $params["ContentType"] = "application/json"
    }

    return $params
}

function Get-ErrorStatusCode {
    <# The HTTP status of a failed call, or $null when the failure never got a response. #>
    param($ErrorRecord)

    if ($ErrorRecord.Exception.Response) {
        return [int]$ErrorRecord.Exception.Response.StatusCode
    }
    return $null
}

function Read-ErrorResponseStream {
    <# The body of a failed call's response, or "" when there is none or it cannot be read. #>
    param($ErrorRecord)

    if (-not $ErrorRecord.Exception.Response) { return "" }
    try {
        $stream = $ErrorRecord.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $reader.Close()
        return $body
    } catch {
        Write-Verbose "could not read the error response body; leaving it empty: $_"
        return ""
    }
}
