<#
.SYNOPSIS
    Test assertion library and HTTP wrapper for Plan B E2E tests.
.DESCRIPTION
    Provides assertion functions that record results to a shared TestResults ArrayList,
    an HTTP wrapper (Invoke-TestApiCall) that captures error responses instead of throwing,
    and a report generator (Write-TestReport) that produces Test-Report.md.
#>

. "$PSScriptRoot\Http-Common.ps1"

# ---- Invoke-TestApiCall: HTTP wrapper that captures status codes instead of throwing ----

function ConvertFrom-ErrorBody {
    <# Parse an error body as JSON; $null when it is empty or not JSON. #>
    param([string]$Text)

    if (-not $Text) { return $null }
    try {
        # The leading comma keeps a JSON array whole across the function boundary.
        return , ($Text | ConvertFrom-Json)
    } catch {
        return $null
    }
}

function ConvertTo-FailedTestResult {
    <#
    .SYNOPSIS
        The test result for a failed call. The raw response stream wins over ErrorDetails
        here (Invoke-ApiCall prefers ErrorDetails), and a failure with no response at all is
        reported as 500 carrying the exception message.
    #>
    param([Parameter(Mandatory)]$ErrorRecord)

    $statusCode = Get-ErrorStatusCode -ErrorRecord $ErrorRecord
    $errorBody = Read-ErrorResponseStream -ErrorRecord $ErrorRecord
    if (-not $errorBody -and $ErrorRecord.ErrorDetails.Message) {
        $errorBody = $ErrorRecord.ErrorDetails.Message
    }
    if (-not $statusCode) {
        $statusCode = 500
        $errorBody = $ErrorRecord.Exception.Message
    }

    return @{
        StatusCode = $statusCode
        Body       = (ConvertFrom-ErrorBody -Text $errorBody)
        RawBody    = $errorBody
        Success    = ($statusCode -ge 200 -and $statusCode -lt 300)
        Error      = $errorBody
    }
}

function Invoke-TestApiCall {
    <#
    .SYNOPSIS
        Wraps Invoke-RestMethod to capture HTTP status codes for test assertions.
    .DESCRIPTION
        Unlike Invoke-ApiCall (which throws on non-2xx), this returns a structured result
        with StatusCode, Body, and Error fields so tests can assert on 400/401/403/404.
    #>
    param(
        [Parameter(Mandatory)]
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Url,

        [object]$Body = $null,
        [string]$Token = "",
        [string]$TenantId = "",
        [int]$TimeoutSec = 30
    )

    $params = Get-ApiRequest -Method $Method -Url $Url -Body $Body -Token $Token `
        -TenantId $TenantId -TimeoutSec $TimeoutSec

    $result = @{
        StatusCode = 0
        Body       = $null
        RawBody    = ""
        Success    = $false
        Error      = ""
    }

    try {
        $response = Invoke-RestMethod @params -ErrorAction Stop
        $result.StatusCode = 200
        $result.Body = $response
        $result.Success = $true
        return $result

    } catch {
        return (ConvertTo-FailedTestResult -ErrorRecord $_)
    }
}

# ---- Test Result Recording ----

function Add-TestResult {
    param(
        [Parameter(Mandatory)]
        [System.Collections.ArrayList]$TestResults,

        [Parameter(Mandatory)]
        [string]$TestId,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateSet("PASS", "FAIL", "FAIL-EXPECTED", "SKIP")]
        [string]$Status,

        [string]$Details = "",
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $color = switch ($Status) {
        "PASS"          { "Green" }
        "FAIL"          { "Red" }
        "FAIL-EXPECTED" { "DarkYellow" }
        "SKIP"          { "DarkGray" }
    }

    $TestResults.Add(@{
        TestId     = $TestId
        Phase      = ($TestId -split '\.')[0]
        Name       = $Name
        Status     = $Status
        Details    = $Details
        GapId      = $GapId
        DurationMs = $DurationMs
        Timestamp  = (Get-Date).ToString("o")
    }) | Out-Null

    $label = "[$Status]"
    if ($GapId) { $label = "[$Status $GapId]" }
    Write-Host "    $label $TestId - $Name" -ForegroundColor $color
    if ($Details -and $Status -ne "PASS") {
        Write-Host "           $Details" -ForegroundColor DarkGray
    }
}

function Add-SkipResult {
    param(
        [Parameter(Mandatory)]
        [System.Collections.ArrayList]$TestResults,

        [Parameter(Mandatory)]
        [string]$TestId,

        [Parameter(Mandatory)]
        [string]$Name,

        [string]$Reason = "Prerequisite not met"
    )

    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status "SKIP" -Details $Reason
}

# ---- Assertion Functions ----

function Assert-IsTrue {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        [bool]$Condition,
        [string]$Details = "",
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    if ($Condition) {
        Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status "PASS" -Details $Details -DurationMs $DurationMs
    } else {
        $status = if ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
        Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $Details -GapId $GapId -DurationMs $DurationMs
    }
}

function Assert-AreEqual {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        $Actual,
        $Expected,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $pass = ("$Actual" -eq "$Expected")
    $details = if ($pass) { "Expected: $Expected" } else { "Expected: $Expected, Actual: $Actual" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-IsNotNull {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        $Value,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $pass = ($null -ne $Value -and "$Value" -ne "")
    $details = if ($pass) { "Value present" } else { "Value is null or empty" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-IsNull {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        $Value,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $pass = ($null -eq $Value -or "$Value" -eq "")
    $details = if ($pass) { "Value is null as expected" } else { "Value is NOT null: $Value" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-GreaterOrEqual {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        [int]$Actual,
        [int]$Expected,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $pass = ($Actual -ge $Expected)
    $details = if ($pass) { "Actual: $Actual >= Expected: $Expected" } else { "Actual: $Actual < Expected: $Expected" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-StatusCode {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        [hashtable]$Response,
        [int]$Expected,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $actual = $Response.StatusCode
    $pass = ($actual -eq $Expected)
    $details = if ($pass) { "Status: $actual" } else { "Expected: $Expected, Actual: $actual. Error: $($Response.Error)" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-Match {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        [string]$Value,
        [string]$Pattern,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $pass = ($Value -match $Pattern)
    $details = if ($pass) { "Value '$Value' matches /$Pattern/" } else { "Value '$Value' does not match /$Pattern/" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

function Assert-ErrorContains {
    param(
        [System.Collections.ArrayList]$TestResults,
        [string]$TestId,
        [string]$Name,
        [hashtable]$Response,
        [string]$Fragment,
        [string]$GapId = "",
        [long]$DurationMs = 0
    )

    $errorText = "$($Response.Error) $($Response.RawBody)"
    $pass = ($errorText -like "*$Fragment*")
    $details = if ($pass) { "Error contains '$Fragment'" } else { "Error does not contain '$Fragment'. Actual: $errorText" }
    $status = if ($pass) { "PASS" } elseif ($GapId) { "FAIL-EXPECTED" } else { "FAIL" }
    Add-TestResult -TestResults $TestResults -TestId $TestId -Name $Name -Status $status -Details $details -GapId $GapId -DurationMs $DurationMs
}

# ---- Test Report Generator ----

function Write-TestReport {
    param(
        [Parameter(Mandatory)]
        [System.Collections.ArrayList]$TestResults,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $totalTests = $TestResults.Count
    $passed = ($TestResults | Where-Object { $_.Status -eq "PASS" }).Count
    $failedUnexpected = ($TestResults | Where-Object { $_.Status -eq "FAIL" }).Count
    $failedExpected = ($TestResults | Where-Object { $_.Status -eq "FAIL-EXPECTED" }).Count
    $skipped = ($TestResults | Where-Object { $_.Status -eq "SKIP" }).Count

    # Group by phase
    $phases = $TestResults | Group-Object { $_.Phase } | Sort-Object Name

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# HCS Case Evaluation Portal - E2E Test Report")
    [void]$sb.AppendLine("**Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$sb.AppendLine("**Tester**: Automated (Plan B)")
    [void]$sb.AppendLine("**Seed State**: scripts/seed-state.json")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Summary")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Category | Total | Passed | Failed Unexpected | Failed Expected | Skipped |")
    [void]$sb.AppendLine("|----------|-------|--------|-------------------|-----------------|---------|")

    foreach ($phase in $phases) {
        $pTotal = $phase.Group.Count
        $pPass = ($phase.Group | Where-Object { $_.Status -eq "PASS" }).Count
        $pFail = ($phase.Group | Where-Object { $_.Status -eq "FAIL" }).Count
        $pExpected = ($phase.Group | Where-Object { $_.Status -eq "FAIL-EXPECTED" }).Count
        $pSkip = ($phase.Group | Where-Object { $_.Status -eq "SKIP" }).Count
        [void]$sb.AppendLine("| $($phase.Name) | $pTotal | $pPass | $pFail | $pExpected | $pSkip |")
    }

    [void]$sb.AppendLine("| **TOTAL** | **$totalTests** | **$passed** | **$failedUnexpected** | **$failedExpected** | **$skipped** |")
    [void]$sb.AppendLine("")

    # Unexpected failures
    $unexpectedFailures = $TestResults | Where-Object { $_.Status -eq "FAIL" }
    [void]$sb.AppendLine("## UNEXPECTED FAILURES - Investigate / Contact Previous Developer")
    [void]$sb.AppendLine("")
    if ($unexpectedFailures.Count -eq 0) {
        [void]$sb.AppendLine("None! All non-gap tests passed.")
    } else {
        [void]$sb.AppendLine("| Test ID | Description | Details |")
        [void]$sb.AppendLine("|---------|-------------|---------|")
        foreach ($f in $unexpectedFailures) {
            $det = $f.Details -replace '\|', '/'  -replace "`n", " "
            [void]$sb.AppendLine("| $($f.TestId) | $($f.Name) | $det |")
        }
    }
    [void]$sb.AppendLine("")

    # Expected failures (known gaps)
    $expectedFailures = $TestResults | Where-Object { $_.Status -eq "FAIL-EXPECTED" }
    [void]$sb.AppendLine("## EXPECTED FAILURES - Confirmed Handover Gaps")
    [void]$sb.AppendLine("")
    if ($expectedFailures.Count -eq 0) {
        [void]$sb.AppendLine("None - all known gaps passed (unexpected!).")
    } else {
        [void]$sb.AppendLine("| Test ID | Gap ID | Description | Details |")
        [void]$sb.AppendLine("|---------|--------|-------------|---------|")
        foreach ($f in $expectedFailures) {
            $det = $f.Details -replace '\|', '/' -replace "`n", " "
            [void]$sb.AppendLine("| $($f.TestId) | $($f.GapId) | $($f.Name) | $det |")
        }
    }
    [void]$sb.AppendLine("")

    # Detailed results per phase
    [void]$sb.AppendLine("## Detailed Test Results")
    [void]$sb.AppendLine("")

    foreach ($phase in $phases) {
        [void]$sb.AppendLine("### $($phase.Name)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Test ID | Name | Status | Details |")
        [void]$sb.AppendLine("|---------|------|--------|---------|")
        foreach ($t in $phase.Group) {
            $det = $t.Details -replace '\|', '/' -replace "`n", " "
            if ($det.Length -gt 120) { $det = $det.Substring(0, 120) + "..." }
            [void]$sb.AppendLine("| $($t.TestId) | $($t.Name) | $($t.Status) | $det |")
        }
        [void]$sb.AppendLine("")
    }

    # Manual testing checklist (B17)
    [void]$sb.AppendLine("## B17: Frontend UI Testing (Manual Checklist)")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### B17.1 Authentication Flow")
    [void]$sb.AppendLine("- [ ] Navigate to http://localhost:4200 - Home loads")
    [void]$sb.AppendLine("- [ ] Click Login - Redirect to AuthServer")
    [void]$sb.AppendLine("- [ ] Enter admin@abp.io / <TEST_PASSWORD> - Logged in")
    [void]$sb.AppendLine("- [ ] Logout works correctly")
    [void]$sb.AppendLine("- [ ] Route guard redirects to login when not authenticated")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### B17.2 Role-Specific Views")
    [void]$sb.AppendLine("- [ ] Admin: Generic home page")
    [void]$sb.AppendLine("- [ ] Patient: Patient appointment list + Book button")
    [void]$sb.AppendLine("- [ ] Applicant Attorney: Filtered appointment view")
    [void]$sb.AppendLine("- [ ] External user: Sidebar hidden")
    [void]$sb.AppendLine("- [ ] Internal user: Full sidebar visible")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### B17.3 CRUD Pages")
    [void]$sb.AppendLine("- [ ] States: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Appointment Types: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Appointment Statuses: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Appointment Languages: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Locations: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] WCAB Offices: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Doctors: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Doctor Availabilities: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Patients: List, Create, Edit, Delete")
    [void]$sb.AppendLine("- [ ] Appointments: List, Create, Edit, Delete")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### B17.4 Appointment Booking Flow")
    [void]$sb.AppendLine("- [ ] Patient search by email works")
    [void]$sb.AppendLine("- [ ] Location dropdown shows tenant locations only")
    [void]$sb.AppendLine("- [ ] Date picker shows available dates")
    [void]$sb.AppendLine("- [ ] Time slot selector shows available slots")
    [void]$sb.AppendLine("- [ ] Booking creates appointment with confirmation number")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### B17.5 ABP Admin Modules")
    [void]$sb.AppendLine("- [ ] Identity Users and Roles")
    [void]$sb.AppendLine("- [ ] SaaS Tenants list")
    [void]$sb.AppendLine("- [ ] Audit Logs visible")
    [void]$sb.AppendLine("- [ ] Settings page loads")
    [void]$sb.AppendLine("- [ ] Excel exports work")
    [void]$sb.AppendLine("")

    $sb.ToString() | Set-Content -Path $OutputPath -Encoding UTF8
    Write-Host "`n  Test report written to: $OutputPath" -ForegroundColor Cyan
}
