# Azure Communication Services (ACS) Email SMTP credentials -- research prompt

This file is a self-contained prompt to paste into a fresh Claude Code session. The fresh session should produce a step-by-step credentials acquisition guide and an exact wiring map for this repo. **Do NOT execute the credential acquisition yourself -- only research and document.**

---

## Paste-in prompt (start of prompt)

You are a research assistant working in the Patient Portal repository at `W:\patient-portal\main` (Windows; Git Bash). The repo is an ABP Commercial 10.0.2 / .NET 10 / Angular 20 multi-tenant SaaS for workers'-compensation IME scheduling. The user is Adrian (sole developer at Gesco). You have the full toolset: Read, Glob, Grep, Bash, WebFetch, WebSearch.

### What you must produce

A single markdown document at `docs/research/2026-04-30-azure-acs-smtp-credentials.md` containing:

1. **Provisioning walkthrough** -- exact, click-by-click steps to obtain working SMTP credentials for Azure Communication Services (ACS) Email, starting from "I have an Azure subscription, I am signed into the portal" and ending at "I have a username and password I can paste into the app." Include:
   - Whether an Azure Communication Services resource and an Email Communication Services resource are two separate resources, and the order they must be created in
   - Whether a verified domain is required to send (Azure-managed subdomain vs. custom domain), the trade-offs, and the fastest path to a working test sender
   - How to connect the Email Communication Services resource to the Communication Services resource so the SMTP endpoint can authenticate (SMTP-over-Entra-ID via app registration is the documented path -- confirm this is still the only supported flow as of January 2026)
   - The exact format of the SMTP username string (it is NOT just an email address; ACS expects `<communication-services-resource-name>.<entra-app-id>.<entra-tenant-id>` -- verify this verbatim against current Microsoft docs) and the exact way to obtain the password (Entra app-registration client secret)
   - The required Entra role assignment on the Communication Services resource for the app registration to send mail
   - The exact SMTP host (`smtp.azurecomm.net`), port (`587`), and TLS mode (STARTTLS) and whether they are the same across all Azure regions
   - How to test the credentials WITHOUT touching the codebase first (a one-shot `Send-MailMessage` PowerShell command or `swaks` Bash command works; pick whichever is more reliable on Windows + Git Bash)

2. **Codebase wiring map** -- the exact files, JSON keys, and code paths that consume SMTP settings, plus the precise edit each one needs. Treat this as a checklist Adrian will tick off line by line. At minimum cover:
   - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` -- the `Settings` block already has `Abp.Mailing.Smtp.*` keys with `REPLACE_ME_LOCALLY` placeholders. Confirm whether ABP reads SMTP settings from `appsettings.json` `Settings:*` (ISettingProvider seeded from config), from environment variables, or from the runtime ABP setting management UI. State the order of precedence and which one wins at startup vs. at runtime.
   - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json` -- already exists and only holds `AbpLicenseCode`. Decide whether secrets belong here (committed to repo? gitignored?) or in `dotnet user-secrets` / environment variables / Azure Key Vault. Cite Adrian's `~/.claude/rules/code-standards.md` "Security Fundamentals" section: secrets must come from environment variables, not hardcoded. Recommend the path that is consistent with that rule.
   - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` -- already injects `IEmailSender`. Confirm no code change is needed in this file once credentials are present (the file's own docstring claims SMTP exceptions are caught and logged; verify the assertion is still accurate). If a code change IS needed (e.g., a new `MailKit.SmtpClient` configuration extension), state the exact diff.
   - `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs` -- search for any `Configure<AbpMailingOptions>` / `Configure<AbpMailKitOptions>` block. If absent, state whether a code-side configuration is required or whether the JSON `Settings:*` keys are sufficient.
   - The ABP setting keys `Abp.Mailing.DefaultFromAddress`, `Abp.Mailing.DefaultFromDisplayName`, `Abp.Mailing.Smtp.Host`, `Abp.Mailing.Smtp.Port`, `Abp.Mailing.Smtp.EnableSsl`, `Abp.Mailing.Smtp.UseDefaultCredentials`, `Abp.Mailing.Smtp.UserName`, `Abp.Mailing.Smtp.Password` -- map each to the exact value Adrian should set (or leave). Specifically confirm whether `EnableSsl=true` is correct for STARTTLS on port 587 in MailKit (MailKit's `EnableSsl` semantics differ from `System.Net.Mail.SmtpClient`), and whether MailKit needs `SecureSocketOptions.StartTls` configured separately.

3. **Verification plan** -- one Bash or PowerShell test that proves the API container can deliver a real email after the wiring. Use only the running Docker compose stack; do not rebuild or modify config beyond the credential keys. The test must:
   - Trigger the existing `SendAppointmentEmailJob` once (creating an Appointment via the API is acceptable; pick the simplest trigger and document the exact endpoint + payload)
   - Show the Hangfire job moving to "Succeeded" state and log line `SendAppointmentEmailJob: delivered ...` in `docker logs main-api-1`
   - Confirm receipt at the test inbox (Adrian will check the inbox manually; you only need to confirm the SMTP `250 OK`)
   - Document the rollback if delivery fails (revert keys, the job will go back to logging `SMTP delivery failed`)

4. **Risks and gotchas** -- a final section that calls out everything that could waste Adrian's time. Examples to look for and verify:
   - ACS Email free tier daily-send limits (verify the current 2026 quota)
   - Whether Azure-managed `<resource>.<region>.communication.azure.com` subdomains can send to external Gmail/Outlook recipients without ending in spam, or whether a custom verified domain is required for production deliverability
   - Whether SMTP-over-Entra-ID requires the Entra tenant to NOT block app-registration secrets (some tenants block secret-based auth and force certificate auth)
   - Region pinning: whether the Communication Services resource region must match the Email Communication Services resource region
   - Any 2025 or 2026 ACS deprecation announcements (e.g., SMTP retirement plans, Entra-ID-only auth mandates)

### Method (not negotiable)

- **Verify every claim against current Microsoft docs.** Do not rely on training memory. ACS Email and SMTP-over-Entra-ID changed multiple times between 2023 and 2026. Run WebSearch / WebFetch against `learn.microsoft.com` for every step. Cite the doc URL inline.
- **Cross-reference at least two sources** for every nontrivial claim (Microsoft docs + a community post / GitHub issue / release notes). If a community post contradicts the docs, the docs win and you flag the discrepancy.
- **Label confidence per Adrian's `rules/communication.md`:** HIGH (official docs), MEDIUM (reputable community), LOW (inference). Inline tags after each non-obvious assertion.
- **Read these repo files before drafting** so the wiring map is grounded in current code, not a guess:
  - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json`
  - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json`
  - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs`
  - `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`
  - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`
  - `docker-compose.yml` (so the env-var path is described against the current container shape)
- **Do NOT execute the provisioning yourself.** You are writing instructions for Adrian to follow in his own Azure tenant. Provisioning requires his subscription, his Entra tenant, and his decisions about region and domain. Your output is documentation only; no Azure CLI commands fired against a live subscription.
- **Do NOT modify any source file.** The deliverable is one new markdown file at the path above.

### Success criteria

- Adrian can paste the markdown into a checklist tracker and complete provisioning in under 30 minutes without re-reading Microsoft docs
- The wiring map names every file path with a relative line range and the exact JSON key or C# block to edit
- The verification plan runs in under 2 minutes against the existing Docker compose stack
- Every nontrivial claim has a cited URL and a confidence label
- The risks section flags at least one issue Adrian would otherwise hit at deployment time

### Constraints

- ASCII only -- no smart quotes, em dashes, or Unicode decorations (Adrian's `rules/code-standards.md`)
- HIPAA: do NOT include any real patient data in examples or test payloads (Adrian's `rules/hipaa.md`)
- Token budget: the markdown output should be under 8,000 words; favor density over restating Microsoft docs verbatim

### What "done" looks like

You produce one file at `docs/research/2026-04-30-azure-acs-smtp-credentials.md`. You do not modify any other file. You do not start the wiring. You report back to Adrian with: "Document is at `<path>`. Provisioning takes about <N> minutes. Top risk to verify before paying: <one-line>."

## Paste-in prompt (end of prompt)

---

## After the research session returns

Once the new session writes that document, this session (or a follow-up) should:

1. Read the document and compare its wiring map against the actual repo state
2. Wait for Adrian to fill in the credentials in `appsettings.secrets.json` (or wherever the document recommends)
3. Run the verification plan against the running Docker stack
4. If `250 OK` is observed, mark the SMTP-credentials portion of step 6.1 (email fan-out) as unblocked in `docs/reports/2026-04-29-wave-2-demo-lifecycle.md`
