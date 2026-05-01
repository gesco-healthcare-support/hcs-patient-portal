# Azure Communication Services Email: SMTP Credential Provisioning Guide

**Date:** 2026-04-30
**Author:** Research session (Adrian / Gesco Patient Portal)
**Status:** Ready to execute
**Confidence labels:** HIGH = official docs/source, MEDIUM = reputable community, LOW = inference

---

## Critical Blocker -- Read Before Provisioning

`src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs` lines 65-70
replace `IEmailSender` with `NullEmailSender` whenever
`ASPNETCORE_ENVIRONMENT=Development`. Docker compose sets exactly that environment for the
`api` container. Real credentials have zero effect until you address this. See Section 3
for the exact one-line fix required before running the verification.

---

## 1. Provisioning Walkthrough

Start: signed into the Azure portal with subscription Owner or Contributor access.
End: SMTP username and client secret in hand, ready to paste into `docker/appsettings.secrets.json`.

Estimated time: 20-30 minutes.

### 1.1 Two Separate Resources

ACS email requires two distinct Azure resources created in this exact order:

1. **Communication Services resource** -- the general-purpose communications platform.
   Hosts the SMTP endpoint, holds the Entra role assignment, and is the resource
   you create SMTP Username entries on.
2. **Email Communication Services resource** -- manages email domains and the sending
   infrastructure. Must be created second and then linked into the Communication
   Services resource.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/create-communication-resource [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/create-email-communication-resource [HIGH]

### 1.2 Step 1 -- Create the Communication Services Resource

1. Portal > Create a resource > search "Communication Services".
2. Click **Create**.
3. Fill in:
   - **Subscription:** your subscription
   - **Resource group:** existing group (e.g., `rg-gesco-patient-portal`)
   - **Resource name:** e.g., `gesco-acs` (you will reference this name later)
   - **Data location:** United States (cannot be changed after creation)
4. Click **Review + Create** then **Create**.
5. Wait for deployment. Note the resource name -- you need it for the SMTP username.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/create-communication-resource [HIGH]

### 1.3 Step 2 -- Create the Email Communication Services Resource

1. Portal > Create a resource > search "Email Communication Services".
2. Click **Create**.
3. Fill in:
   - **Subscription:** same subscription
   - **Resource group:** same resource group
   - **Resource name:** e.g., `gesco-acs-email`
   - **Data location:** United States -- MUST match the Communication Services
     resource data location; this is immutable after creation. [HIGH]
4. Click **Review + Create** then **Create**.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/create-email-communication-resource [HIGH]

Region note: "The Email Communication Services resource and the Communication Services
resource MUST share the same data location/sub-region. Data location is immutable after
resource creation." [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/connect-email-communication-resource

### 1.4 Step 3 -- Provision a Domain

You need at least one provisioned domain before the SMTP endpoint will accept mail.

#### Option A: Azure-managed subdomain (fastest path for testing)

1. Open the **Email Communication Services** resource.
2. Navigate to **Domains** (left menu).
3. Click **+ Add domain** > **Azure subdomain**.
4. A domain in the format `<guid>.azurecomm.net` is provisioned automatically.
   No DNS changes required. Takes under 2 minutes.

Trade-offs:
- SPF and DKIM are pre-configured by Microsoft. [HIGH]
- Sending volume: limited by default quota (25 emails per 60-minute window). [HIGH]
  Source: https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits
- Deliverability: external recipients (Gmail, Outlook.com) may receive mail in spam.
  See Section 4 -- Risks. [MEDIUM]
- Cannot customize the sender address beyond the `From` display name.
- Suitable for dev/staging verification; not recommended for production.

#### Option B: Custom verified domain (production path)

1. Open the **Email Communication Services** resource > **Domains** > **+ Add domain** > **Custom domain**.
2. Enter your domain (e.g., `mail.gesco.com`).
3. Add the TXT, DKIM, and DKIM2 records shown in the portal to your DNS provider.
4. Click **Verify** after DNS propagation (typically 5-15 minutes, up to 48 hours).

Custom domains lift the quota ceiling and eliminate the spam risk from shared
`azurecomm.net` IPs. Required for production. [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/add-azure-managed-domains

### 1.5 Step 4 -- Connect the Email Resource to the Communication Services Resource

1. Open the **Communication Services** resource (the first one you created).
2. Navigate to **Email** > **Domains** (left menu).
3. Click **Connect domain**.
4. Select the **Email Communication Services** subscription, resource group, resource,
   and domain you provisioned in Steps 2-3.
5. Click **Connect**.

This link tells the SMTP endpoint which email infrastructure to use for outbound mail.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/connect-email-communication-resource [HIGH]

### 1.6 Step 5 -- Create the Entra App Registration

This is the service principal the API will authenticate as.

1. Portal > Microsoft Entra ID > **App registrations** > **+ New registration**.
2. Fill in:
   - **Name:** e.g., `gesco-patient-portal-smtp`
   - **Supported account types:** Accounts in this organizational directory only
   - **Redirect URI:** leave blank
3. Click **Register**.
4. On the Overview page, record:
   - **Application (client) ID** (a GUID -- the "client ID")
   - **Directory (tenant) ID** (a GUID)

Source: https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal [HIGH]

### 1.7 Step 6 -- Create the Client Secret (SMTP Password)

1. In the app registration > **Certificates & secrets** > **Client secrets** > **+ New client secret**.
2. Set a description (e.g., `patient-portal-smtp-dev`) and expiry (12 or 24 months).
3. Click **Add**.
4. IMMEDIATELY COPY the **Value** column (not the Secret ID column). This value is
   shown only once. This string is your SMTP password.

Source: https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal [HIGH]

### 1.8 Step 7 -- Assign the Role

The app registration must be granted the built-in
**Communication and Email Service Owner** role on the **Communication Services**
resource (not the Email resource, not the resource group).

1. Open the **Communication Services** resource.
2. Left menu > **Access control (IAM)** > **+ Add** > **Add role assignment**.
3. **Role** tab: search for and select **Communication and Email Service Owner**.
4. Click **Next**.
5. **Members** tab: choose **User, group, or service principal** > **+ Select members**.
6. Search for your app registration name (e.g., `gesco-patient-portal-smtp`) and select it.
7. Click **Select** > **Next** > **Review + assign**.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication [HIGH]

Alternative: create a custom role with only these three permissions if least-privilege
is required: `Microsoft.Communication/CommunicationServices/Read`,
`Microsoft.Communication/CommunicationServices/Write`,
`Microsoft.Communication/EmailServices/write`. [HIGH]

### 1.9 Step 8 -- Create the SMTP Username Resource

This step is often missed. Without it, the SMTP server will reject authentication.

1. Open the **Communication Services** resource.
2. Left menu > **SMTP Usernames** > **+ Add SMTP Username**.
3. In the dropdown, select the Entra app registration you created in Step 5.
   If it does not appear, the role assignment from Step 7 has not propagated yet;
   wait 1-2 minutes and refresh.
4. **Username:** enter a string of your choice. Can be an email address (e.g.,
   `no-reply@mail.gesco.com`) or freeform text (e.g., `gesco-patient-portal`).
   - If you use an email format, the domain portion must be one of the connected
     domains from Step 4.
   - This exact string is what you will enter as the SMTP username in the app.
5. Click **Add**.
6. The entry shows status **Ready to use** once requirements are met.

Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication [HIGH]

Important: the username you define here is what ABP sends as the SMTP username.
The old documented format `<acs-resource>.<entra-app-id>.<tenant-id>` appears in
older blog posts and is no longer the provisioning path -- the current model uses
user-defined SMTP Username resources linked to an Entra app. [HIGH]

### 1.10 Step 9 -- Test Credentials Before Touching the App

Test SMTP auth from the Windows terminal WITHOUT modifying any code or config.

Using PowerShell (most reliable on Windows -- no extra install needed):

```powershell
# Replace the four variables below with your real values.
$smtpHost   = "smtp.azurecomm.net"
$smtpPort   = 587
$smtpUser   = "your-smtp-username-from-step-8"
$smtpPass   = "your-client-secret-from-step-6"
$from       = "no-reply@mail.gesco.com"   # must match a connected domain From address
$to         = "adriang@gesco.com"          # your test inbox
$subject    = "ACS SMTP test - patient portal dev"
$body       = "If you receive this, ACS SMTP credentials are working."

$cred = New-Object System.Management.Automation.PSCredential(
    $smtpUser,
    (ConvertTo-SecureString $smtpPass -AsPlainText -Force)
)

Send-MailMessage `
    -SmtpServer $smtpHost `
    -Port $smtpPort `
    -UseSsl `
    -Credential $cred `
    -From $from `
    -To $to `
    -Subject $subject `
    -Body $body
```

Expected outcome: no error, email arrives in inbox (or spam -- check both).
A `5.7.x` SMTP error means auth failed; double-check the SMTP username and
client secret, and verify the SMTP Username resource shows "Ready to use".

### 1.11 Credential Summary

At the end of provisioning you have:

| Item | Where to find it |
|---|---|
| SMTP host | `smtp.azurecomm.net` (global, same across all Azure regions) [HIGH] |
| SMTP port | `587` (STARTTLS) |
| TLS mode | STARTTLS |
| SMTP username | The string you defined in Step 8 |
| SMTP password | The client secret Value from Step 6 |
| From address | Any address at a connected domain (Step 4) |

Source for host/port/TLS: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication [HIGH]

---

## 2. Codebase Wiring Map

### 2.1 Where to Write the Credentials

**File:** `docker/appsettings.secrets.json` (gitignored; mounted into all containers)
**Lines 5-6** (current placeholders):

```json
"Abp.Mailing.Smtp.UserName": "REPLACE_WITH_ACS_USERNAME",
"Abp.Mailing.Smtp.Password": "REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING"
```

Replace with:

```json
"Abp.Mailing.Smtp.UserName": "<the username string from Step 8>",
"Abp.Mailing.Smtp.Password": "<the client secret value from Step 6>"
```

Also fix the stale comment on line 4 (the old format description is wrong):

```json
"_comment_acs": "ACS SMTP. UserName = the SMTP Username resource string created in the Azure portal. Password = the Entra app client secret value (not the connection string). See docs/research/2026-04-30-azure-acs-smtp-credentials.md."
```

Do NOT put these credentials in:
- `src/.../appsettings.secrets.json` -- that file is gitignored too, but it only feeds
  local-IIS/Kestrel runs, not Docker. Docker reads `docker/appsettings.secrets.json`.
- `src/.../appsettings.json` -- that file IS committed; never put secrets there.
- Environment variables in `docker-compose.yml` -- that file IS committed.

This approach is consistent with `~/.claude/rules/code-standards.md` "Security Fundamentals":
secrets come from gitignored files or environment variables, never hardcoded.

### 2.2 src/.../appsettings.json -- No Edit Required

`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` lines 26-34:

```json
"Settings": {
  "Abp.Mailing.DefaultFromAddress": "noreply@gesco.com",
  "Abp.Mailing.DefaultFromDisplayName": "Gesco Patient Portal",
  "Abp.Mailing.Smtp.Host": "smtp.azurecomm.net",
  "Abp.Mailing.Smtp.Port": "587",
  "Abp.Mailing.Smtp.EnableSsl": "true",
  "Abp.Mailing.Smtp.UseDefaultCredentials": "false",
  "Abp.Mailing.Smtp.UserName": "REPLACE_ME_LOCALLY",
  "Abp.Mailing.Smtp.Password": "REPLACE_ME_LOCALLY"
}
```

These values are correct as-is. Only `UserName` and `Password` need to change,
and they are overridden by `docker/appsettings.secrets.json` at runtime.

Update `DefaultFromAddress` to match a sender address at your connected domain once
provisioned (e.g., `no-reply@mail.gesco.com`). Do not use `noreply@gesco.com` unless
that domain is connected -- the ACS SMTP server validates that the From domain is a
connected domain.

### 2.3 EnableSsl=true -- Correct for This Project

The project uses `Volo.Abp.Emailing` version 10.0.2 (see
`src/.../Domain/HealthcareSupport.CaseEvaluation.Domain.csproj`).
`Volo.Abp.MailKit` is NOT installed.

ABP's default SMTP implementation in `Volo.Abp.Emailing` uses `System.Net.Mail.SmtpClient`.
For that class, `EnableSsl = true` means STARTTLS negotiation -- which is exactly what
port 587 / ACS requires. No additional `SecureSocketOptions` configuration is needed.

If `Volo.Abp.MailKit` is ever added in the future, this changes: MailKit requires
explicit `options.SecureSocketOption = SecureSocketOptions.StartTls` in a
`Configure<AbpMailKitOptions>` block in the Domain or Host module. [HIGH]
Source: https://abp.io/docs/latest/framework/infrastructure/mail-kit

### 2.4 ABP Settings Precedence (Who Wins)

ABP resolves settings in this order, lowest-to-highest priority:

1. Code defaults (lowest)
2. `Settings:*` keys in `appsettings.json` via `ConfigurationSettingValueProvider`
3. Database values set via the ABP Setting Management UI or API (highest)

For Docker: `docker/appsettings.secrets.json` is mounted as a volume that overlays
`appsettings.json` -- so the secrets file's `Settings:*` keys override the base file.

At runtime, any admin who updates SMTP settings through the ABP admin UI will override
both files because the database provider has the highest priority. If credentials are
updated in the portal and behavior does not change, check the database setting values
via the ABP Setting Management page.

There is NO need for `Configure<AbpMailingOptions>` or `Configure<SmtpEmailSenderConfiguration>`
in C# code -- the `Settings:*` JSON keys are sufficient and are the standard ABP approach. [HIGH]

### 2.5 SendAppointmentEmailJob -- No Code Change Needed

`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs`

The job injects `IEmailSender` and calls `SendAsync`. Once real credentials are wired
and the `NullEmailSender` bypass (see 2.6) is removed, this job will attempt delivery
and log either:
- `SendAppointmentEmailJob: delivered (<Context>) to <To>.` on success
- `SendAppointmentEmailJob: SMTP delivery failed ...` on SMTP error

No edit to this file is required. The try/catch behavior is intentional for MVP per the
docstring (line 19-29): exceptions are swallowed so Hangfire reports Succeeded and the
HTTP request returns normally regardless of SMTP outcome. Remove the try/catch in a
future hardening pass when email delivery should gate behavior.

### 2.6 CRITICAL: NullEmailSender Bypasses All Credentials in Docker

`src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs` lines 65-70:

```csharp
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
{
    context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
}
```

`docker-compose.yml` line 91: `ASPNETCORE_ENVIRONMENT: Development`

This means the real SMTP sender is NEVER reached in the current Docker stack. Hangfire
will show "Succeeded" for all email jobs, but no mail is sent -- the `NullEmailSender`
discards everything silently.

**To enable SMTP delivery in the Docker dev stack, edit `docker-compose.yml` `api` service:**

Change:
```yaml
ASPNETCORE_ENVIRONMENT: Development
```

To (for a one-session test):
```yaml
ASPNETCORE_ENVIRONMENT: Staging
```

Then rebuild and restart only the `api` container:
```bash
docker compose up -d --build api
```

Revert to `Development` after the credential test is confirmed working. The `Staging`
environment also disables the developer exception page and switches Redis data protection
on (line 229 in `CaseEvaluationHttpApiHostModule.cs`), which is fine for a brief test.

Alternatively, add a separate override flag (e.g., `Email__UseRealSender=true`)
and adjust the condition in `CaseEvaluationDomainModule.cs` to check that flag instead
of the environment name -- this would let you run Development-environment tooling while
still sending real email. Deferred to Wave 3 hardening.

### 2.7 ABP Setting Key Reference

| Setting key | Correct value | Notes |
|---|---|---|
| `Abp.Mailing.DefaultFromAddress` | e.g., `no-reply@mail.gesco.com` | Must be at a connected domain |
| `Abp.Mailing.DefaultFromDisplayName` | `Gesco Patient Portal` | No change needed |
| `Abp.Mailing.Smtp.Host` | `smtp.azurecomm.net` | Same across all Azure regions [HIGH] |
| `Abp.Mailing.Smtp.Port` | `587` | ACS SMTP recommended port [HIGH] |
| `Abp.Mailing.Smtp.EnableSsl` | `true` | STARTTLS via System.Net.Mail [HIGH] |
| `Abp.Mailing.Smtp.UseDefaultCredentials` | `false` | Must be false for credential auth |
| `Abp.Mailing.Smtp.UserName` | SMTP Username string from Step 8 | User-defined in Azure portal |
| `Abp.Mailing.Smtp.Password` | Entra client secret Value | From Step 6 |

Source for key names: https://docs.abp.io/en/abp/latest/Emailing [HIGH]

---

## 3. Verification Plan

**Pre-condition:** credentials are in `docker/appsettings.secrets.json` AND the
`NullEmailSender` bypass is disabled (Section 2.6 change applied).

### 3.1 Checklist Before Running

- [ ] `docker/appsettings.secrets.json` has real values for `Abp.Mailing.Smtp.UserName`
      and `Abp.Mailing.Smtp.Password`
- [ ] `docker-compose.yml` `api` `ASPNETCORE_ENVIRONMENT` changed to `Staging`
      (or the NullEmailSender condition removed)
- [ ] PowerShell credential test from Section 1.10 succeeded (email received in inbox)
- [ ] `docker compose up -d --build api` run after edits
- [ ] API container health check green: `docker compose ps` shows `api` as healthy

### 3.2 Trigger SendAppointmentEmailJob

The fastest trigger is submitting an appointment request via the API. Log into the
running app as an external user (Booker role), submit an appointment -- the
`SubmissionEmailHandler` will enqueue a `SendAppointmentEmailJob` for each party.

Alternative: use the Hangfire dashboard at `http://localhost:44327/hangfire` to inspect
any Enqueued or Failed jobs and manually requeue them if a prior test run left some.

### 3.3 Confirm Job Succeeded

1. Open `http://localhost:44327/hangfire` in the browser.
2. Navigate to **Succeeded** tab.
3. Locate the `SendAppointmentEmailJob` entry.
4. The job details pane shows the `args.To` and `args.Context` values.

### 3.4 Confirm Delivery in Logs

```bash
docker logs main-api-1 2>&1 | grep -i "SendAppointmentEmailJob"
```

Success looks like:
```
[INF] SendAppointmentEmailJob: delivered (AppointmentSubmitted) to test@example.com.
```

Failure (bad credentials) looks like:
```
[WRN] SendAppointmentEmailJob: SMTP delivery failed (AppointmentSubmitted) to test@example.com. Configure ACS credentials to deliver. Job will not retry until Attempts policy is raised.
```

If you see the WRN line: check the SMTP username (must exactly match the string
created in the Azure portal SMTP Usernames blade), check the client secret has not
expired, and re-run the PowerShell test from Section 1.10 to isolate whether the
issue is credentials or code.

### 3.5 Manual Inbox Confirmation

Check the test inbox (including the spam/junk folder). ACS delivers synchronously to
the SMTP endpoint and returns `250 OK` before the inbox actually receives the message;
there may be a 10-30 second delay before the message appears.

### 3.6 Rollback

If delivery fails and you want to revert to silent discard mode:

1. Revert `docker-compose.yml` `ASPNETCORE_ENVIRONMENT` back to `Development`.
2. Run `docker compose up -d api` (no rebuild needed -- only the env var changed).
3. The `NullEmailSender` re-registers; all email jobs silently succeed again.
4. The SMTP credentials remain in `docker/appsettings.secrets.json` for the next attempt.

---

## 4. Risks and Gotchas

### R1: NullEmailSender Active in All Dev Runs (HIGH PROBABILITY HIT)

Already detailed in Section 2.6. This is the most likely blocker. Every dev session
running against Docker with `ASPNETCORE_ENVIRONMENT=Development` sends zero real emails
regardless of what credentials are in the secrets file. The Hangfire dashboard will
show "Succeeded" which makes this invisible until you look at the log output.

### R2: Azure-Managed Domain Spam Risk for External Recipients

Azure-managed `<guid>.azurecomm.net` subdomains share IP space with other ACS tenants.
External providers (Gmail, Outlook.com) apply reputation filters that may route mail
from these shared IPs to spam. [MEDIUM]

Source: https://learn.microsoft.com/en-us/answers/questions/1457409/azure-communication-service-emails-delivered-to-ju

For dev/staging testing to `adriang@gesco.com` (corporate Outlook), this is usually
fine. For production sending to patients' personal inboxes, a custom verified domain
is required. This is already in scope as a post-MVP hardening item.

### R3: Sending Quota -- 25 Emails per 60 Minutes on Azure-Managed Domain

Default quota for Azure-managed domains: 25 emails per 60-minute rolling window. [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits

The appointment submission flow for a single appointment enqueues up to 4 email jobs
(booker + attending physician + defense attorney + claim examiner). A modest dev
testing session can hit the 25-email ceiling within an hour.

To request a quota increase: submit a support ticket from the Azure portal on the
Communication Services resource. Custom domains have a higher default ceiling (2400/day
at the global level). [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-quota-increase

### R4: SMTP Username Resource Required -- Often Missed

The step that trips up most developers (confirmed in community Q&A [MEDIUM]): after
creating the Entra app registration and assigning the role, you MUST also create an
SMTP Username resource in the Communication Services resource portal blade. Without it,
the SMTP server returns an authentication error even with valid credentials.

### R5: From Address Must Use a Connected Domain

The `From` address (set via `Abp.Mailing.DefaultFromAddress`) must use a domain that
is connected to the Communication Services resource. Sending `From: noreply@gesco.com`
when only `<guid>.azurecomm.net` is connected will result in SMTP rejection. [HIGH]

For the Azure-managed domain path: set `DefaultFromAddress` to something like
`DoNotReply@<guid>.azurecomm.net` (copy the exact domain from the portal).

### R6: Client Secret Expiry

Entra client secrets expire (typically 12 or 24 months). When the secret expires,
all email delivery silently fails (SMTP auth error, caught by the try/catch in
`SendAppointmentEmailJob`, logged as warning). Set a calendar reminder to rotate the
secret before expiry and update `docker/appsettings.secrets.json` and production secrets.

### R7: Entra Tenant May Block Client-Secret Auth

Some Entra tenants have conditional access policies that require MFA or block
client-secret (password) based service principal authentication in favor of
certificate-based auth. If auth fails despite correct credentials, check with the
tenant admin whether app-secret auth is allowed for service principals. [MEDIUM]

### R8: Region Data Location Immutable

If you create either resource with the wrong data location, you cannot change it.
You must delete and recreate both resources. Data location "United States" is the
only generally-available option as of early 2026. [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/create-email-communication-resource

### R9: Old Username Format in Code Comment

`docker/appsettings.secrets.json` line 4 contains an outdated comment:
`UserName format: '<acs-resource>.<entra-app-name>.<entra-tenant-id>'`

This reflects the original 2023 ACS SMTP format. Current provisioning uses a
user-defined SMTP Username resource (Section 1.9); the username is whatever string
you choose when creating the resource, not a derived compound string. The comment
should be updated per Section 2.1. [HIGH]

### R10: No ACS SMTP Deprecation Notices as of 2026-04-30

No deprecation announcements for ACS SMTP were found in official Microsoft documentation
as of this research date. The last major update to the SMTP auth article was April 2025.
SMTP-over-Entra-ID remains the only supported authentication flow (basic username/password
auth against a static key was never offered; Entra app registration has been the only
supported path since general availability). [HIGH]
Source: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication

---

## 5. Next Steps After Provisioning

1. Run the PowerShell test (Section 1.10) -- confirm `250 OK` before touching the app.
2. Edit `docker/appsettings.secrets.json` with real values (Section 2.1).
3. Update `Abp.Mailing.DefaultFromAddress` in `appsettings.json` to use the connected domain.
4. Apply the environment name change (Section 2.6) to disable `NullEmailSender`.
5. Rebuild and restart the `api` container.
6. Submit a test appointment and confirm log line `delivered` (Section 3.4).
7. Revert `docker-compose.yml` to `Development` after the test.
8. Mark step 6.1 (email fan-out) as unblocked in `docs/reports/2026-04-29-wave-2-demo-lifecycle.md`.

---

## 6. Sources

| URL | Used for |
|---|---|
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/create-communication-resource | Provisioning step 1 |
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/create-email-communication-resource | Provisioning step 2 |
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/add-azure-managed-domains | Domain options |
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/connect-email-communication-resource | Connect resources, region requirement |
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication | SMTP username, role, password |
| https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/send-email-smtp | SMTP host/port/TLS |
| https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-smtp-overview | SMTP overview |
| https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits | Quota limits |
| https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-quota-increase | Quota increase |
| https://learn.microsoft.com/en-us/answers/questions/1457409/azure-communication-service-emails-delivered-to-ju | Spam / deliverability |
| https://abp.io/docs/latest/framework/infrastructure/mail-kit | ABP MailKit SecureSocketOptions |
| https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal | Entra app registration |
