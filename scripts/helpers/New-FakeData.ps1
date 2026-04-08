<#
.SYNOPSIS
    Randomized fake data generation for HCS Case Evaluation Portal seed scripts.
.DESCRIPTION
    All data looks realistic for California workers' compensation demographics
    but is obviously fake (test emails, reserved SSN range, etc.).
    All values respect confirmed max-length constraints from *Consts.cs files.
#>

# ============================================================================
# NAME POOLS - California workers' comp demographic mix
# ============================================================================

$script:FirstNames_Latino = @(
    "Juan", "Maria", "Carlos", "Rosa", "Miguel", "Ana", "Jose", "Carmen", "Luis", "Elena",
    "Pedro", "Sofia", "Diego", "Isabella", "Alejandro", "Gabriela", "Fernando", "Lucia", "Rafael", "Valentina"
)

$script:FirstNames_EastAsian = @(
    "Wei", "Mei", "Jing", "Hao", "Yuki", "Kenji", "Soo", "Min", "Tuan", "Linh",
    "Hiro", "Aiko", "Chen", "Fang", "Ryu", "Sakura", "Jin", "Hana", "Bao", "Lan"
)

$script:FirstNames_Armenian = @(
    "Armen", "Ani", "Hayk", "Nare", "Levon", "Sona", "Tigran", "Lilit", "Aram", "Anahit"
)

$script:FirstNames_Anglo = @(
    "James", "Sarah", "Michael", "Jennifer", "Robert", "Jessica", "David", "Emily", "William", "Amanda",
    "Daniel", "Rachel", "Thomas", "Laura", "Christopher", "Karen", "Andrew", "Megan", "Kevin", "Lisa"
)

$script:LastNames_Latino = @(
    "Garcia", "Rodriguez", "Martinez", "Lopez", "Hernandez", "Gonzalez", "Perez", "Sanchez", "Ramirez", "Torres",
    "Rivera", "Flores", "Morales", "Reyes", "Cruz", "Ortiz", "Gutierrez", "Chavez", "Mendoza", "Castillo"
)

$script:LastNames_EastAsian = @(
    "Chen", "Wang", "Li", "Zhang", "Liu", "Nguyen", "Tran", "Pham", "Kim", "Park",
    "Lee", "Yang", "Huang", "Wu", "Lin", "Tanaka", "Yamamoto", "Nakamura", "Suzuki", "Watanabe"
)

$script:LastNames_Armenian = @(
    "Petrosyan", "Hovhannisyan", "Sargsyan", "Harutyunyan", "Grigoryan", "Gasparyan", "Khachatryan", "Poghosyan", "Abrahamyan", "Manukyan"
)

$script:LastNames_Anglo = @(
    "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Wilson", "Taylor", "Anderson",
    "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Moore", "Clark", "Lewis", "Walker"
)

$script:AllFirstNames = $script:FirstNames_Latino + $script:FirstNames_EastAsian + $script:FirstNames_Armenian + $script:FirstNames_Anglo
$script:AllLastNames = $script:LastNames_Latino + $script:LastNames_EastAsian + $script:LastNames_Armenian + $script:LastNames_Anglo

# ============================================================================
# ADDRESS POOLS - California cities with real zip codes
# ============================================================================

$script:CaCityZipPairs = @(
    @{ City = "Los Angeles";    ZipCode = "90001" },
    @{ City = "San Francisco";  ZipCode = "94102" },
    @{ City = "San Diego";      ZipCode = "92101" },
    @{ City = "Sacramento";     ZipCode = "95814" },
    @{ City = "Fresno";         ZipCode = "93701" },
    @{ City = "Oakland";        ZipCode = "94607" },
    @{ City = "Long Beach";     ZipCode = "90802" },
    @{ City = "Bakersfield";    ZipCode = "93301" },
    @{ City = "Anaheim";        ZipCode = "92801" },
    @{ City = "Riverside";      ZipCode = "92501" },
    @{ City = "Santa Ana";      ZipCode = "92701" },
    @{ City = "Stockton";       ZipCode = "95202" },
    @{ City = "Irvine";         ZipCode = "92618" },
    @{ City = "Glendale";       ZipCode = "91201" },
    @{ City = "San Bernardino"; ZipCode = "92401" },
    @{ City = "Pasadena";       ZipCode = "91101" },
    @{ City = "Torrance";       ZipCode = "90501" },
    @{ City = "Burbank";        ZipCode = "91502" },
    @{ City = "Pomona";         ZipCode = "91766" },
    @{ City = "Van Nuys";       ZipCode = "91401" }
)

$script:StreetNames = @(
    "Oak Ave", "Pacific Blvd", "Mission St", "Harbor Dr", "Wilshire Blvd",
    "Sunset Blvd", "Broadway", "Main St", "Central Ave", "Washington Blvd",
    "Valley View Rd", "La Brea Ave", "Figueroa St", "Foothill Blvd", "El Camino Real",
    "Sepulveda Blvd", "Victory Blvd", "Ventura Blvd", "Colorado Blvd", "Olive St",
    "Spring St", "Hope St", "Grand Ave", "Flower St", "Alameda St",
    "Temple St", "Beverly Blvd", "Third St", "Sixth St", "Olympic Blvd"
)

$script:CaAreaCodes = @("213", "310", "323", "415", "510", "619", "650", "714", "818", "909", "916", "949")

# ============================================================================
# BUSINESS NAME POOLS
# ============================================================================

$script:EmployersByIndustry = @{
    "Construction" = @(
        @{ Name = "Pacific Coast Builders Inc"; Occupations = @("Carpenter", "Laborer", "Electrician", "Plumber", "Site Supervisor", "Iron Worker") },
        @{ Name = "Golden State Construction LLC"; Occupations = @("Concrete Worker", "Roofer", "Painter", "Heavy Equipment Operator") },
        @{ Name = "West Valley Contractors"; Occupations = @("Foreman", "Welder", "Mason", "Scaffolder") },
        @{ Name = "Sierra Building Group"; Occupations = @("Drywall Installer", "HVAC Technician", "Framer") }
    )
    "Manufacturing" = @(
        @{ Name = "Coastal Manufacturing Co"; Occupations = @("Machine Operator", "Assembly Worker", "Quality Inspector", "Forklift Operator") },
        @{ Name = "SoCal Plastics Inc"; Occupations = @("Mold Operator", "Line Worker", "Maintenance Tech") },
        @{ Name = "Bay Area Metal Works"; Occupations = @("Welder", "CNC Operator", "Sheet Metal Worker") }
    )
    "Retail" = @(
        @{ Name = "Valley Fresh Markets"; Occupations = @("Cashier", "Stock Clerk", "Deli Worker", "Bakery Associate") },
        @{ Name = "SunCoast Auto Parts"; Occupations = @("Parts Associate", "Store Manager", "Delivery Driver") }
    )
    "Healthcare" = @(
        @{ Name = "Central Valley Medical Center"; Occupations = @("Nurse Aide", "Orderly", "Phlebotomist", "Medical Assistant") },
        @{ Name = "Pacific Home Health Services"; Occupations = @("Home Health Aide", "LVN", "Caregiver") }
    )
    "Agriculture" = @(
        @{ Name = "San Joaquin Farms LLC"; Occupations = @("Farm Worker", "Irrigation Tech", "Equipment Operator", "Harvester") },
        @{ Name = "Napa Valley Vineyards"; Occupations = @("Vineyard Worker", "Cellar Worker", "Tractor Operator") }
    )
    "Logistics" = @(
        @{ Name = "Harbor Freight Logistics"; Occupations = @("Warehouse Worker", "Truck Driver", "Dock Worker", "Dispatcher") },
        @{ Name = "Pacific Shipping Services"; Occupations = @("Longshoreman", "Container Handler", "Crane Operator") }
    )
}

$script:FirmSuffixes = @("& Associates APC", "Law Group LLP", "Legal", "Law Firm", "Legal Services", "Law Office")

$script:WesternStates = @(
    "California", "Nevada", "Arizona", "Oregon", "Washington",
    "Utah", "Colorado", "New Mexico", "Idaho", "Montana",
    "Wyoming", "Hawaii", "Alaska", "Texas", "Oklahoma"
)

$script:LanguagePool = @(
    "English", "Spanish", "Mandarin", "Cantonese", "Tagalog",
    "Vietnamese", "Korean", "Armenian", "Farsi", "Russian",
    "Japanese", "Punjabi", "Arabic", "Hindi", "Portuguese",
    "Thai", "Khmer", "Lao", "Hmong", "Samoan"
)

# Track generated emails for uniqueness
$script:GeneratedEmails = @{}

# ============================================================================
# GENERATION FUNCTIONS
# ============================================================================

function New-FakeFirstName {
    return $script:AllFirstNames | Get-Random
}

function New-FakeLastName {
    return $script:AllLastNames | Get-Random
}

function New-FakeEmail {
    param(
        [Parameter(Mandatory)][string]$FirstName,
        [Parameter(Mandatory)][string]$LastName,
        [string]$Domain = "hcs.test"
    )

    $base = "$($FirstName.ToLower()).$($LastName.ToLower())"
    # Clean non-alphanumeric chars
    $base = $base -replace '[^a-z0-9.]', ''

    $email = "$base@$Domain"

    # Ensure uniqueness by appending a number if needed
    $attempt = 0
    while ($script:GeneratedEmails.ContainsKey($email)) {
        $attempt++
        $email = "${base}${attempt}@$Domain"
    }

    # Max email length varies: Doctor=49, Patient=50, general=256
    # Truncate base if needed (keep domain intact)
    if ($email.Length -gt 49) {
        $maxBase = 49 - $Domain.Length - 1  # -1 for @
        $base = $base.Substring(0, [Math]::Min($base.Length, $maxBase))
        $email = "$base@$Domain"
        $attempt = 0
        while ($script:GeneratedEmails.ContainsKey($email)) {
            $attempt++
            $email = "${base}${attempt}@$Domain"
        }
    }

    $script:GeneratedEmails[$email] = $true
    return $email
}

function Clear-EmailTracker {
    $script:GeneratedEmails = @{}
}

function New-FakeAddress {
    <#
    .SYNOPSIS
        Returns a hashtable with Street, City, ZipCode from CA pool.
    .DESCRIPTION
        Street: number + street name (fits 255-char Street, 100-char Address)
        City: real CA city (fits 50-char max)
        ZipCode: matching real zip (fits 15-char max)
    #>
    param(
        [string]$StateId = ""  # Optional: override state
    )

    $cityZip = $script:CaCityZipPairs | Get-Random
    $streetNum = Get-Random -Minimum 100 -Maximum 9999
    $streetName = $script:StreetNames | Get-Random

    return @{
        Street  = "$streetNum $streetName"
        City    = $cityZip.City
        ZipCode = $cityZip.ZipCode
        StateId = $StateId
    }
}

function New-FakeCaPhone {
    <#
    .SYNOPSIS
        Generates a CA phone number fitting 20-char max.
    .DESCRIPTION
        Format: "XXX-XXX-XXXX" (12 chars, fits 20-char max for PhoneNumber fields)
    #>
    $areaCode = $script:CaAreaCodes | Get-Random
    $exchange = Get-Random -Minimum 200 -Maximum 999
    $subscriber = Get-Random -Minimum 1000 -Maximum 9999
    return "$areaCode-$exchange-$subscriber"
}

function New-FakeCellPhone {
    <#
    .SYNOPSIS
        Generates a cell phone number fitting 12-char max.
    .DESCRIPTION
        Format: "XXXXXXXXXX" (10 digits, no formatting, fits 12-char CellPhoneNumber max)
    #>
    $areaCode = $script:CaAreaCodes | Get-Random
    $exchange = Get-Random -Minimum 200 -Maximum 999
    $subscriber = Get-Random -Minimum 1000 -Maximum 9999
    return "$areaCode$exchange$subscriber"
}

function New-FakeFaxNumber {
    <#
    .SYNOPSIS
        Generates a fax number fitting 19-char max (ApplicantAttorney.FaxNumber).
    .DESCRIPTION
        Format: "XXX-XXX-XXXX" (12 chars, fits 19-char max)
    #>
    return New-FakeCaPhone  # Same format, both fit within their max
}

function New-FakeSsn {
    <#
    .SYNOPSIS
        Generates a fake SSN in the reserved 9XX range (never issued by SSA).
    .DESCRIPTION
        Format: "9XX-XX-XXXX" (11 chars, fits 20-char max)
    #>
    $area = Get-Random -Minimum 900 -Maximum 999
    $group = Get-Random -Minimum 10 -Maximum 99
    $serial = Get-Random -Minimum 1000 -Maximum 9999
    return "$area-$group-$serial"
}

function New-FakeDOB {
    <#
    .SYNOPSIS
        Generates a realistic DOB for a workers' comp patient (25-65 years ago).
    #>
    $yearsAgo = Get-Random -Minimum 25 -Maximum 65
    $daysOffset = Get-Random -Minimum 0 -Maximum 365
    return (Get-Date).AddYears(-$yearsAgo).AddDays(-$daysOffset).Date
}

function New-FakePanelNumber {
    <#
    .SYNOPSIS
        Generates a workers' comp panel number (max 50 chars).
    .DESCRIPTION
        Format: "WC-YYYY-XXX-NNNN" (~16 chars, well within 50-char max)
    #>
    $year = (Get-Date).Year
    $letters = -join ((65..90) | Get-Random -Count 3 | ForEach-Object { [char]$_ })
    $digits = Get-Random -Minimum 1000 -Maximum 9999
    return "WC-$year-$letters-$digits"
}

function New-FakeAppointmentDate {
    <#
    .SYNOPSIS
        Creates a DateTime that falls within a slot's [FromTime, ToTime) range.
    .PARAMETER SlotDate
        The AvailableDate of the slot (date only).
    .PARAMETER FromTime
        The slot's FromTime as "HH:mm:ss" string.
    .PARAMETER ToTime
        The slot's ToTime as "HH:mm:ss" string.
    #>
    param(
        [Parameter(Mandatory)][DateTime]$SlotDate,
        [Parameter(Mandatory)][string]$FromTime,
        [Parameter(Mandatory)][string]$ToTime
    )

    $from = [TimeSpan]::Parse($FromTime)
    $to = [TimeSpan]::Parse($ToTime)

    # Random offset: at least 0 minutes from start, at most (duration - 1 minute) from start
    $durationMinutes = ($to - $from).TotalMinutes
    if ($durationMinutes -le 1) {
        $offsetMinutes = 0
    } else {
        $offsetMinutes = Get-Random -Minimum 0 -Maximum ([int]($durationMinutes - 1))
    }

    return $SlotDate.Date.Add($from).AddMinutes($offsetMinutes)
}

function New-FakeAppointmentDateAtBoundary {
    <#
    .SYNOPSIS
        Creates a DateTime at exactly the slot's FromTime (boundary test: >= FromTime).
    #>
    param(
        [Parameter(Mandatory)][DateTime]$SlotDate,
        [Parameter(Mandatory)][string]$FromTime
    )

    $from = [TimeSpan]::Parse($FromTime)
    return $SlotDate.Date.Add($from)
}

function New-FakeAppointmentDateNearEnd {
    <#
    .SYNOPSIS
        Creates a DateTime 1 minute before the slot's ToTime (boundary test: < ToTime).
    #>
    param(
        [Parameter(Mandatory)][DateTime]$SlotDate,
        [Parameter(Mandatory)][string]$ToTime
    )

    $to = [TimeSpan]::Parse($ToTime)
    return $SlotDate.Date.Add($to).AddMinutes(-1)
}

function New-FakeEmployerData {
    <#
    .SYNOPSIS
        Returns a hashtable with EmployerName, Occupation from matched industry pools.
    #>
    $industries = $script:EmployersByIndustry.Keys | Get-Random
    $employers = $script:EmployersByIndustry[$industries]
    $employer = $employers | Get-Random
    $occupation = $employer.Occupations | Get-Random

    return @{
        EmployerName = $employer.Name
        Occupation   = $occupation
    }
}

function New-FakeFirmName {
    <#
    .SYNOPSIS
        Generates a law firm name (max 50 chars).
    .DESCRIPTION
        Format: "{LastName} {Suffix}" - kept short to fit 50-char FirmName max.
    #>
    param(
        [string]$LawyerLastName = ""
    )

    if (-not $LawyerLastName) {
        $LawyerLastName = New-FakeLastName
    }

    $suffix = $script:FirmSuffixes | Get-Random
    $firmName = "$LawyerLastName $suffix"

    # Ensure fits 50-char max
    if ($firmName.Length -gt 50) {
        $firmName = $firmName.Substring(0, 50)
    }

    return $firmName
}

function New-FakeWebAddress {
    <#
    .SYNOPSIS
        Generates a fake law firm web address (max 100 chars).
    #>
    param(
        [string]$FirmName = ""
    )
    $slug = ($FirmName -replace '[^a-zA-Z0-9]', '').ToLower()
    if ($slug.Length -gt 30) { $slug = $slug.Substring(0, 30) }
    return "https://www.$slug.hcs.test"
}

function New-FakeDueDate {
    <#
    .SYNOPSIS
        Generates a due date 30-90 days after the appointment date, or $null.
    .DESCRIPTION
        70% chance of returning a date, 30% chance of $null.
    #>
    param(
        [Parameter(Mandatory)][DateTime]$AppointmentDate
    )

    if ((Get-Random -Minimum 1 -Maximum 100) -le 30) {
        return $null
    }

    $daysAhead = Get-Random -Minimum 30 -Maximum 90
    return $AppointmentDate.AddDays($daysAhead)
}

function New-MaxLengthString {
    <#
    .SYNOPSIS
        Generates a string of exactly the specified length for boundary testing.
    #>
    param(
        [Parameter(Mandatory)][int]$Length,
        [string]$Prefix = "MaxLen_"
    )

    if ($Length -le $Prefix.Length) {
        return $Prefix.Substring(0, $Length)
    }

    $remaining = $Length - $Prefix.Length
    $filler = "X" * $remaining
    return "$Prefix$filler"
}

function New-FakeInterpreterVendor {
    <#
    .SYNOPSIS
        Generates a fake interpreter vendor name (max 255 chars).
    #>
    $prefixes = @("Pacific", "West Coast", "Golden State", "Bay Area", "SoCal", "Valley", "Coastal")
    $suffixes = @("Interpreting Services", "Language Solutions", "Translation Group", "Interpreter Network")
    $prefix = $prefixes | Get-Random
    $suffix = $suffixes | Get-Random
    return "$prefix $suffix"
}

function New-WcabOfficeName {
    <#
    .SYNOPSIS
        Generates a WCAB district office name and abbreviation.
    #>
    param(
        [Parameter(Mandatory)][string]$City
    )

    # Abbreviation: 2-4 uppercase letters from city name
    $words = $City -split '\s+'
    if ($words.Count -gt 1) {
        $abbr = ($words | ForEach-Object { $_[0] }) -join ''
    } else {
        $abbr = $City.Substring(0, [Math]::Min(4, $City.Length))
    }
    $abbr = $abbr.ToUpper()

    return @{
        Name         = "WCAB $City District Office"
        Abbreviation = $abbr
    }
}

# ============================================================================
# INLINE SELF-TESTS (run with: . .\New-FakeData.ps1 -RunTests)
# ============================================================================

function Test-FakeData {
    Write-Host "`nRunning New-FakeData self-tests..." -ForegroundColor Cyan
    $passed = 0
    $failed = 0

    function Assert-True($condition, $msg) {
        if ($condition) { $script:passed++; Write-Host "  PASS: $msg" -ForegroundColor Green }
        else { $script:failed++; Write-Host "  FAIL: $msg" -ForegroundColor Red }
    }

    # Name generation
    $fn = New-FakeFirstName
    Assert-True ($fn.Length -gt 0) "New-FakeFirstName returns non-empty: '$fn'"
    Assert-True ($fn.Length -le 50) "FirstName within 50-char max"

    $ln = New-FakeLastName
    Assert-True ($ln.Length -gt 0) "New-FakeLastName returns non-empty: '$ln'"
    Assert-True ($ln.Length -le 50) "LastName within 50-char max"

    # Email uniqueness and max length
    Clear-EmailTracker
    $email1 = New-FakeEmail -FirstName "Juan" -LastName "Garcia"
    $email2 = New-FakeEmail -FirstName "Juan" -LastName "Garcia"
    Assert-True ($email1 -ne $email2) "Duplicate names get unique emails: '$email1' vs '$email2'"
    Assert-True ($email1.Length -le 49) "Email within 49-char max (Doctor): '$email1' ($($email1.Length) chars)"
    Clear-EmailTracker

    # Phone numbers
    $phone = New-FakeCaPhone
    Assert-True ($phone.Length -le 20) "Phone within 20-char max: '$phone' ($($phone.Length) chars)"
    Assert-True ($phone -match '^\d{3}-\d{3}-\d{4}$') "Phone format XXX-XXX-XXXX: '$phone'"

    $cell = New-FakeCellPhone
    Assert-True ($cell.Length -le 12) "CellPhone within 12-char max: '$cell' ($($cell.Length) chars)"
    Assert-True ($cell -match '^\d{10}$') "CellPhone format 10 digits: '$cell'"

    # SSN
    $ssn = New-FakeSsn
    Assert-True ($ssn.Length -le 20) "SSN within 20-char max: '$ssn'"
    Assert-True ($ssn -match '^9\d{2}-\d{2}-\d{4}$') "SSN starts with 9XX: '$ssn'"

    # Address
    $addr = New-FakeAddress
    Assert-True ($addr.Street.Length -le 100) "Street within 100-char max: '$($addr.Street)'"
    Assert-True ($addr.City.Length -le 50) "City within 50-char max: '$($addr.City)'"
    Assert-True ($addr.ZipCode.Length -le 15) "ZipCode within 15-char max: '$($addr.ZipCode)'"

    # Panel number
    $pn = New-FakePanelNumber
    Assert-True ($pn.Length -le 50) "PanelNumber within 50-char max: '$pn'"
    Assert-True ($pn -match '^WC-\d{4}-[A-Z]{3}-\d{4}$') "PanelNumber format: '$pn'"

    # DOB
    $dob = New-FakeDOB
    $age = [Math]::Floor(((Get-Date) - $dob).TotalDays / 365.25)
    Assert-True ($age -ge 25 -and $age -le 65) "DOB age range 25-65: age=$age"

    # Firm name
    $firm = New-FakeFirmName -LawyerLastName "Petrosyan"
    Assert-True ($firm.Length -le 50) "FirmName within 50-char max: '$firm'"

    # Employer data
    $emp = New-FakeEmployerData
    Assert-True ($emp.EmployerName.Length -le 255) "EmployerName within 255-char max"
    Assert-True ($emp.Occupation.Length -le 255) "Occupation within 255-char max"

    # Max-length string
    $maxStr = New-MaxLengthString -Length 50
    Assert-True ($maxStr.Length -eq 50) "MaxLengthString exactly 50 chars: $($maxStr.Length)"

    # Fax number
    $fax = New-FakeFaxNumber
    Assert-True ($fax.Length -le 19) "FaxNumber within 19-char max: '$fax' ($($fax.Length) chars)"

    # Appointment date within slot
    $slotDate = (Get-Date).Date.AddDays(5)
    $apptDate = New-FakeAppointmentDate -SlotDate $slotDate -FromTime "09:00:00" -ToTime "10:00:00"
    $apptTime = $apptDate.TimeOfDay
    Assert-True ($apptTime -ge [TimeSpan]::Parse("09:00:00")) "AppointmentDate >= FromTime"
    Assert-True ($apptTime -lt [TimeSpan]::Parse("10:00:00")) "AppointmentDate < ToTime"

    # Boundary dates
    $boundaryStart = New-FakeAppointmentDateAtBoundary -SlotDate $slotDate -FromTime "09:00:00"
    Assert-True ($boundaryStart.TimeOfDay -eq [TimeSpan]::Parse("09:00:00")) "Boundary date at exact FromTime"

    $boundaryEnd = New-FakeAppointmentDateNearEnd -SlotDate $slotDate -ToTime "10:00:00"
    Assert-True ($boundaryEnd.TimeOfDay -eq [TimeSpan]::Parse("09:59:00")) "Boundary date 1 min before ToTime"

    Write-Host "`n  Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
    return $failed -eq 0
}

# Run tests if invoked with -RunTests
if ($args -contains "-RunTests") {
    $result = Test-FakeData
    if (-not $result) { exit 1 }
}
