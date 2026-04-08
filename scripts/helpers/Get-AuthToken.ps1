<#
.SYNOPSIS
    Acquires an OAuth2 access token via password grant.
.DESCRIPTION
    Calls the OpenIddict token endpoint with password grant type.
    Caches tokens per username+tenant to avoid re-auth on every API call.
#>

# PS 5.x compatible: disable SSL certificate validation for localhost dev HTTPS
if (-not ([System.Management.Automation.PSTypeName]'TrustAllCertsPolicy').Type) {
    Add-Type -TypeDefinition @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
}
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

# Token cache: keyed by "username|tenantId"
if (-not $script:TokenCache) {
    $script:TokenCache = @{}
}

function Get-AuthToken {
    param(
        [Parameter(Mandatory)]
        [string]$Username,

        [Parameter(Mandatory)]
        [string]$Password,

        [string]$AuthServerUrl = "https://localhost:44368",

        [string]$TenantId = "",

        [switch]$ForceRefresh
    )

    $cacheKey = "$Username|$TenantId"

    # Return cached token if available and not forcing refresh
    if (-not $ForceRefresh -and $script:TokenCache.ContainsKey($cacheKey)) {
        $cached = $script:TokenCache[$cacheKey]
        # Check if token is still valid (with 60s buffer)
        if ($cached.ExpiresAt -gt (Get-Date).AddSeconds(60)) {
            return $cached.AccessToken
        }
    }

    $tokenUrl = "$AuthServerUrl/connect/token"

    $body = @{
        grant_type    = "password"
        client_id     = "CaseEvaluation_App"
        username      = $Username
        password      = $Password
        scope         = "offline_access CaseEvaluation openid profile email roles"
    }

    $headers = @{
        "Content-Type" = "application/x-www-form-urlencoded"
    }

    # Add tenant header if specified
    if ($TenantId) {
        $headers["__tenant"] = $TenantId
    }

    try {
        $response = Invoke-RestMethod `
            -Uri $tokenUrl `
            -Method Post `
            -Body $body `
            -Headers $headers `
            -TimeoutSec 30

        if (-not $response.access_token) {
            throw "Token response missing access_token field"
        }

        # Cache the token with expiry
        $expiresIn = if ($response.expires_in) { $response.expires_in } else { 3600 }
        $script:TokenCache[$cacheKey] = @{
            AccessToken = $response.access_token
            ExpiresAt   = (Get-Date).AddSeconds($expiresIn)
        }

        return $response.access_token

    } catch {
        $statusCode = $_.Exception.Response.StatusCode
        $errorBody = ""
        if ($_.ErrorDetails.Message) {
            $errorBody = $_.ErrorDetails.Message
        }
        throw "Token acquisition failed for '$Username' (tenant: '$TenantId'). Status: $statusCode. Error: $errorBody. Exception: $($_.Exception.Message)"
    }
}

function Clear-TokenCache {
    $script:TokenCache = @{}
    Write-Host "Token cache cleared." -ForegroundColor Yellow
}
