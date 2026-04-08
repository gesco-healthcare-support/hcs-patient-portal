<#
.SYNOPSIS
    REST API wrapper with tenant support, error handling, and JSON serialization.
.DESCRIPTION
    Wraps Invoke-RestMethod to handle bearer auth, tenant headers, JSON body serialization,
    and error reporting for the HCS Case Evaluation Portal API.
    Uses Invoke-RestMethod (PS 5.x compatible) instead of Invoke-WebRequest.
#>

function Invoke-ApiCall {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Url,

        [object]$Body = $null,

        [string]$Token = "",

        [string]$TenantId = "",

        [int]$TimeoutSec = 30,

        [switch]$RawResponse
    )

    $headers = @{}

    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    if ($TenantId) {
        $headers["__tenant"] = $TenantId
    }

    $params = @{
        Uri        = $Url
        Method     = $Method
        Headers    = $headers
        TimeoutSec = $TimeoutSec
    }

    if ($Body -and $Method -in @("POST", "PUT")) {
        if ($Body -is [string]) {
            $params["Body"] = $Body
        } else {
            $params["Body"] = $Body | ConvertTo-Json -Depth 10 -Compress
        }
        $params["ContentType"] = "application/json"
    }

    try {
        $response = Invoke-RestMethod @params -ErrorAction Stop
        return $response

    } catch {
        $statusCode = "Unknown"
        $errorBody = ""

        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errorBody = $reader.ReadToEnd()
                $reader.Close()
            } catch {}
        }

        if ($_.ErrorDetails.Message) {
            $errorBody = $_.ErrorDetails.Message
        }

        # For 404 on DELETE, caller may want to handle gracefully
        if ($Method -eq "DELETE" -and $statusCode -eq 404) {
            Write-Host "    [404] Resource already deleted: $Url" -ForegroundColor DarkYellow
            return $null
        }

        $errorMsg = "API call failed: $Method $Url`n  Status: $statusCode`n  Response: $errorBody"
        Write-Host $errorMsg -ForegroundColor Red

        throw $errorMsg
    }
}

function Invoke-ApiGetList {
    <#
    .SYNOPSIS
        Fetches all items from a paged endpoint.
    .DESCRIPTION
        Calls the endpoint with maxResultCount and skipCount to fetch all pages.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [string]$Token = "",
        [string]$TenantId = "",
        [int]$PageSize = 100
    )

    $allItems = @()
    $skip = 0
    $separator = if ($Url.Contains("?")) { "&" } else { "?" }

    do {
        $pagedUrl = "$Url${separator}maxResultCount=$PageSize&skipCount=$skip"
        $response = Invoke-ApiCall -Method "GET" -Url $pagedUrl -Token $Token -TenantId $TenantId

        if ($response.items) {
            $allItems += $response.items
        }

        $totalCount = if ($response.totalCount) { $response.totalCount } else { 0 }
        $skip += $PageSize

    } while ($skip -lt $totalCount)

    return $allItems
}