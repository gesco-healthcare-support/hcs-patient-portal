# Email sender consumer (IEmailSender wiring)

## Source gap IDs

- [CC-01](../../gap-analysis/06-cross-cutting-backend.md) -- Send appointment
  reminder emails. Track 06 `Delta` lines 142 and `NEW version state` lines
  104-109. Open question at line 191.

## NEW-version code read

- `CaseEvaluationDomainModule.cs:38` -- `AbpEmailingModule` in `[DependsOn]`.
  The module brings `IEmailSender` + `SmtpEmailSender` (default) + standard
  setting definitions for `Abp.Mailing.Smtp.*` into DI, ready to resolve.
- `CaseEvaluationDomainModule.cs:42` -- `TextTemplateManagementDomainModule`
  in `[DependsOn]`. This enables DB-backed templates (`AbpTextTemplateContents`
  table, created by migration `20260131164316_Initial`) and the admin UI under
  Administration -> Text Templates. No template definition providers exist
  yet; the table will be empty until a `TemplateDefinitionProvider` is added
  or an admin manually creates a row.
- `CaseEvaluationDomainModule.cs:60-62` -- `#if DEBUG` block replaces
  `IEmailSender` with `NullEmailSender` for Debug builds only. Release builds
  fall through to the default `SmtpEmailSender` registered by `AbpEmailingModule`,
  which in an unconfigured environment will throw `SmtpException` on the first
  send (no graceful fallback when `Abp.Mailing.Smtp.Host` is empty -- confirmed
  by ABP support #6912).
- Grep for `IEmailSender` across `W:/patient-portal/main/src` returns exactly
  one match: the `NullEmailSender` replacement at
  `CaseEvaluationDomainModule.cs:61`. Zero business consumers. Domain
  services, AppServices, and controllers never inject `IEmailSender`.
- Grep for `TemplateDefinitionProvider` and `ITemplateRenderer` across
  `W:/patient-portal/main/src` returns zero matches. No template definitions
  or renderers are used.
- Grep for `Abp\.Mailing` in any `appsettings*.json` returns zero matches.
  `appsettings.json` lines 1-24 has no `Settings:` section. The SMTP host,
  port, username, password, and default-from fields are all undefined in
  config, which means ABP falls back to `SmtpEmailSenderConfiguration`'s
  hardcoded defaults (host `127.0.0.1`, port `25`, no auth) -- unusable in
  production and in dev.
- `appsettings.Development.json` lines 1-2 is literally `{}` -- zero overrides.
- ABP Swagger exposes `/api/setting-management/emailing` and
  `/api/setting-management/emailing/send-test-email`. These Pro-module endpoints
  let host or tenant admins manage SMTP values at runtime via the ABP admin UI.
  They are wired automatically by `AbpSettingManagementDomainModule` and need
  no code change to expose -- only a working SMTP host behind the scenes.
- Swagger also exposes `/api/account/send-email-confirmation-code`,
  `/api/account/send-password-reset-code`,
  `/api/account/send-email-confirmation-token`,
  `/api/account/confirm-email`, and `/api/account/reset-password`. These
  Account-module endpoints internally call `IEmailSender.SendAsync`. Today,
  in Debug, they resolve to `NullEmailSender` (silent drop); in a Release
  build they would attempt to send via the default `SmtpEmailSender` and throw
  because no SMTP host is configured. This means two MVP capabilities
  (`account-self-service` forgot-password and email verification) quietly
  depend on CC-01 being resolved.

## Live probes

- `GET https://localhost:44327/swagger/v1/swagger.json` -- HTTP 200.
  Anonymous. Payload contains `/api/setting-management/emailing`,
  `/api/setting-management/emailing/send-test-email`,
  `/api/account/send-email-confirmation-code`,
  `/api/account/send-password-reset-code`, `/api/account/reset-password`,
  `/api/account/confirm-email`. Proves the ABP Emailing +
  SettingManagement + Account plumbing is already reachable; only the
  SMTP settings and the `NullEmailSender` replacement gate live delivery.
  Probe log: [../probes/email-sender-consumer-2026-04-24T120000.md](../probes/email-sender-consumer-2026-04-24T120000.md).
- Token-holding probe against
  `GET https://localhost:44327/api/setting-management/emailing` (which would
  return the current SMTP settings) was not executed: this subagent's sandbox
  denied the password-grant `POST /connect/token` call. Static analysis
  (zero `Abp.Mailing` keys anywhere in the repo) is sufficient to prove the
  settings are empty; the endpoint would return all-null or all-default
  values if called.

## OLD-version reference

- `P:\PatientPortalOld\PatientAppointment.Api\Infrastructure\Utilities\SendMail.cs`
  lines 23-472 implements `ISendMail` with two parallel transports:
  AWS SES via `AmazonSimpleEmailServiceClient` (line 33 hardcodes
  `RegionEndpoint.USWest1`; lines 47-161 `SendSMTPMailAWS`), and
  `System.Net.Mail.SmtpClient` (lines 166-265 `SendSMTPMail` pulls SMTP
  config from a `SMTPConfiguration` row in the DB). Two attachment variants
  at lines 270-364 and 388-463, one of them (line 270) already dead (send
  call commented at 352-356).
- CC list sourced per-send from `SystemParameter.CcEmailIds`
  (`SendMail.cs:49,276`). Per-tenant CC mailbox list. NEW has no parallel
  today; flagged as non-MVP gap CC-07 in track 06.
- OLD send is triggered by the 9 `SchedulerDomain` jobs
  (`SchedulerDomain.cs:72-364`) and by per-event calls in `AppointmentDomain.cs`
  (status-transition emails). Status-transition SMS code is all commented
  out per track-10 erratum 2; status-transition email code is alive.
- OLD templates: disk files under
  `PatientAppointment.Api\wwwroot\EmailTemplates\*.html` with placeholder
  tokens (`##CompanyName##`, `##ClinicLogoUrl##`, etc.) substituted at
  send time by string replacement. No template DB.
- **Track-10 errata that apply here:**
  - *Erratum 2* (`10-deep-dive-findings.md:18-29`): OLD SMS is 100% disabled
    at the status-transition layer; 6 of 9 scheduler jobs call Twilio only
    when `isSMSEnable: true` (deployment default `false`). Bound to CC-02,
    not CC-01, but relevant to how "email + SMS" capabilities co-depend on
    `scheduler-notifications`.
  - *Erratum 3* (`10-deep-dive-findings.md:31-37`): OLD scheduler hardcodes
    `AppointmentId=1`/`UserId=1` at the stored-proc call sites (likely a
    live production bug). Affects how faithfully the 9 recurring jobs
    should be re-implemented -- spec from the proc body, not from caller
    behaviour. Email send mechanics (CC-01) remain unchanged.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10. Angular 20 on the frontend. OpenIddict
  for OAuth. (CLAUDE.md, lines 10-16 of stack table.)
- Row-level `IMultiTenant` with `ABP IDataFilter` auto-filter (ADR-004).
  Email sender must resolve the right SMTP credentials for the current
  tenant context: ABP's default `SmtpEmailSenderConfiguration` already
  reads from `ISettingProvider`, which resolves per-tenant if a tenant
  override exists in SettingManagement; host-level values come from
  `Settings:Abp.Mailing.Smtp.*` in appsettings.json. No extra code needed.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no `ng serve` (ADR-005). None of these constrain the email capability
  directly; they do constrain any NEW code (e.g., no AutoMapper for a DTO we
  might introduce for an email-sending request).
- HIPAA: email bodies MUST NOT include PHI. Use confirmation numbers,
  appointment codes, and link URLs only. This is a policy constraint on
  consumers of `IEmailSender`, not on the sender plumbing itself, but it
  must be spelled out to every consumer we add (scheduler-notifications,
  templates-email-sms, account-self-service, appointment-documents
  email flows).
- ABP `IEmailSender` default `SmtpEmailSender` pulls from `ISettingProvider`
  (per the ABP source on GitHub, confirmed below) -- so adding any ABP-
  compatible SMTP backend that can handle `SmtpClient` is a config-only
  change, not a code change. An SES-SMTP backend fits. Sendmail over the
  local network fits. An on-box SMTP relay for dev (smtp4dev / Papercut)
  fits.
- Production delivery provider MUST either (a) be AWS SES (already under the
  AWS BAA, which Adrian can sign via AWS Artifact) or (b) another
  HIPAA-eligible provider with a BAA in place before PHI-adjacent traffic
  flows. Default SendGrid self-serve does not ship a BAA.
- `NullEmailSender` under `#if DEBUG` is intentional: developer boxes have
  no SMTP and should not silently produce `SmtpException`. Do not remove
  the guard; refine it so the dev behaviour is explicit (either "log
  emails" via `NullEmailSender` or "send to local smtp4dev"), and so a
  Release build never resolves to `NullEmailSender` even if built on a
  developer box.

## Research sources consulted

- [ABP -- Email Sending](https://abp.io/docs/latest/framework/infrastructure/emailing)
  accessed 2026-04-24. HIGH confidence. Canonical settings keys:
  `Abp.Mailing.Smtp.Host/Port/UserName/Password/Domain/EnableSsl/UseDefaultCredentials`
  plus `Abp.Mailing.DefaultFromAddress`, `Abp.Mailing.DefaultFromDisplayName`.
  `IEmailSender.SendAsync` is the provider-independent API.
  `IEmailSender.QueueAsync` defers to `IBackgroundJobManager` for async
  dispatch. Password is stored encrypted via `ISettingEncryptionService`.
- [ABP source -- SmtpEmailSender.cs](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Emailing/Volo/Abp/Emailing/Smtp/SmtpEmailSender.cs)
  accessed 2026-04-24. HIGH confidence. Constructor takes `ICurrentTenant`,
  `ISmtpEmailSenderConfiguration`, `IBackgroundJobManager`.
  `BuildClientAsync()` reads host/port/EnableSsl/UseDefaultCredentials/
  UserName/Password/Domain from the configuration provider and constructs
  `SmtpClient`.
- [ABP source -- SmtpEmailSenderConfiguration.cs](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Emailing/Volo/Abp/Emailing/Smtp/SmtpEmailSenderConfiguration.cs)
  accessed 2026-04-24. HIGH confidence. Default configuration reads from
  `ISettingProvider`. Per-tenant overrides resolve automatically.
- [ABP support #4036 -- NullEmailSender in DEBUG](https://abp.io/support/questions/4036/NullEmailSender-service-works-all-environments-not-only-debug)
  accessed 2026-04-24. HIGH confidence (ABP support). The `#if DEBUG`
  pattern is the documented way to short-circuit email in Debug; it is not
  a bug.
- [ABP support #6912 -- Empty SMTP host](https://abp.io/support/questions/6912/When-SMTP-settings-are-empty-there-should-be-a-specific-error-message-instead-of-a-generic-error)
  accessed 2026-04-24. HIGH confidence. Confirms there is no graceful
  fallback when `Abp.Mailing.Smtp.Host` is unset: ABP will attempt to
  connect to `127.0.0.1:25` and throw `SmtpException`.
- [ABP Community -- Replacing Email Templates and Sending Emails](https://abp.io/community/articles/replacing-email-templates-and-sending-emails-jkeb8zzh)
  accessed 2026-04-24. MEDIUM confidence (community walkthrough). Canonical
  code shape: inject `ITemplateRenderer` + `IEmailSender`; call
  `RenderAsync(templateName, model, cultureName)` then
  `SendAsync(target, subject, body)`. Template definitions live in
  `TemplateDefinitionProvider` subclasses; virtual file system points the
  template name at an embedded `.tpl` file.
- [AWS -- Amazon SES endpoints and quotas](https://docs.aws.amazon.com/general/latest/gr/ses.html)
  accessed 2026-04-24. HIGH confidence. SMTP endpoint hostname format
  `email-smtp.<region>.amazonaws.com`; ports 25, 587, 2587 (STARTTLS) or
  465, 2465 (TLS wrapper). us-west-1 endpoint is
  `email-smtp.us-west-1.amazonaws.com` (OLD's SES region).
- [AWS -- Connecting to an Amazon SES SMTP endpoint](https://docs.aws.amazon.com/ses/latest/dg/smtp-connect.html)
  accessed 2026-04-24. HIGH confidence. All SES SMTP connections require
  TLS. Port 25 throttled by default on EC2 -- use 587 in production.
- [AWS -- HIPAA Compliance](https://aws.amazon.com/compliance/hipaa-compliance/)
  accessed 2026-04-24. HIGH confidence. AWS publishes a standard BAA via
  AWS Artifact; Amazon SES is on the HIPAA-eligible services list (per
  paubox 2026 review cross-checked to the AWS Artifact eligible-services
  reference, last updated 2026-02-10).
- [Paubox -- Is Amazon SES HIPAA compliant? (2026 update)](https://www.paubox.com/blog/amazon-ses-hipaa-compliant)
  accessed 2026-04-24. MEDIUM confidence (secondary source, verified 2026).
  SES is on the HIPAA-eligible list and accepts the AWS BAA.
- [.NET Core + AWS SES via SMTP (Jason Watmore)](https://jasonwatmore.com/post/2020/11/28/net-core-c-aws-ses-send-email-via-smtp-with-aws-simple-email-service)
  accessed 2026-04-24. LOW confidence (2020 blog; used only to confirm the
  SMTP-credential-vs-IAM-key distinction, which is a stable AWS fact).

## Alternatives considered

1. **ABP default `SmtpEmailSender` wired to AWS SES SMTP endpoint.** Rely on
   ABP's out-of-the-box SMTP implementation; configure `Abp.Mailing.Smtp.*`
   settings to point at `email-smtp.us-west-1.amazonaws.com:587` with TLS.
   SES SMTP credentials go into Settings (encrypted). Zero new code paths;
   `NullEmailSender` override stays `#if DEBUG` but only the behaviour is
   hardened (see recommended solution below). **chosen**. Reason: lowest
   friction (track 06 open question already flags this as the
   recommendation), provider-agnostic (swap backends via config), HIPAA-
   covered when signed under the AWS BAA, and every ABP module that already
   uses `IEmailSender` (Account, SettingManagement test email, Identity
   2FA) works without change.
2. **Custom `SesEmailSender` using `AmazonSimpleEmailServiceClient` SDK.**
   Port OLD's `SendSMTPMailAWS` verbatim. Implement `IEmailSender` by
   wrapping the AWS SDK. **rejected**. More code to maintain, no runtime
   benefit over SMTP for a medical scheduling workload (throughput is far
   below SES SMTP limits), and config becomes IAM-keyed rather than
   Setting-backed so per-tenant overrides stop working cleanly.
3. **SendGrid via their SMTP relay.** Pointed to by community articles.
   **rejected** for MVP. Default SendGrid self-serve does not include a BAA
   (HIPAA Journal, Jotform, Compliancy Group independently confirm). Twilio
   negotiates BAAs for SendGrid only under enterprise contracts. Gesco has
   no such contract today. Reopen if Gesco shifts to Twilio for SMS and
   folds email into that BAA.
4. **Azure Communication Services Email.** HIPAA-eligible under the
   Microsoft default BAA. **conditional**. Viable if Gesco already has an
   Azure tenant with a BAA in place; not Adrian's current default. Would
   plug in exactly the same way as SES SMTP (different host/credentials).
5. **Local smtp4dev / Papercut for dev + SES in production.** Dev-loop
   improvement. **conditional** -- useful layer on top of option 1: in
   Debug, instead of `NullEmailSender`, point `Abp.Mailing.Smtp.Host` at
   `localhost:25` with smtp4dev running on dev boxes. Lets developers see
   outgoing mail. Optional; recommended but not required for CC-01.

## Recommended solution for this MVP

Wire the ABP default `SmtpEmailSender` against AWS SES SMTP, storing all
SMTP values in ABP Settings via appsettings.json and (optionally) per-tenant
overrides through the existing SettingManagement admin UI.

Exact steps, in dependency order:

1. **Narrow the `NullEmailSender` override** in
   `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`.
   Replace the `#if DEBUG` block with an explicit guard that checks both the
   environment and whether an SMTP host is actually configured. This makes
   Release builds safe (no silent drops) and lets a dev with smtp4dev
   installed still exercise real delivery in Debug. Pattern:
   ```csharp
   var hostEnv = context.Services.GetSingletonInstance<IHostEnvironment>();
   var config = context.Services.GetConfiguration();
   var smtpHost = config["Settings:Abp.Mailing.Smtp.Host"];
   if (hostEnv.IsDevelopment() && string.IsNullOrWhiteSpace(smtpHost))
   {
       context.Services.Replace(
           ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
   }
   ```
   HIGH-confidence pattern: matches the ABP support team's own guidance in
   support thread #4036.
2. **Add SMTP settings to appsettings.json** (host-level defaults) under
   `Settings:Abp.Mailing.Smtp.*`. For SES us-west-1:
   ```json
   "Settings": {
     "Abp.Mailing.DefaultFromAddress": "noreply@gesco.com",
     "Abp.Mailing.DefaultFromDisplayName": "Gesco Patient Portal",
     "Abp.Mailing.Smtp.Host": "email-smtp.us-west-1.amazonaws.com",
     "Abp.Mailing.Smtp.Port": "587",
     "Abp.Mailing.Smtp.EnableSsl": "true",
     "Abp.Mailing.Smtp.UseDefaultCredentials": "false",
     "Abp.Mailing.Smtp.UserName": "<SES SMTP username>",
     "Abp.Mailing.Smtp.Password": "<SES SMTP password -- encrypted>"
   }
   ```
   The `Password` value must be encrypted via `ISettingEncryptionService.Encrypt()`
   at write time -- ABP calls `Decrypt()` on read. In practice, the value
   Gesco commits to appsettings for dev/staging will be a placeholder; real
   credentials go into `appsettings.secrets.json` (not in git -- note that
   currently `appsettings.secrets.json` IS tracked for the ABP license key;
   Adrian should confirm whether SMTP creds live there or in a Key Vault /
   Parameter Store).
3. **Provision SES SMTP credentials** (separately from IAM console access
   keys): AWS SES -> SMTP Settings -> Create SMTP credentials. Produces a
   username/password distinct from AWS access keys. Record which region.
4. **Verify sender identity** in SES (domain or individual address).
   Production DKIM + SPF + DMARC records must be in place before live
   traffic. Out of scope for this brief; flag to Adrian for the rollout
   runbook.
5. **(Optional, small)** Add a `TemplateDefinitionProvider` placeholder in
   `src/HealthcareSupport.CaseEvaluation.Domain/Emailing/` so downstream
   capabilities (`scheduler-notifications`, `templates-email-sms`,
   `appointment-documents`) have an existing pattern to extend. Template
   body content is defined later by those capabilities.
6. **Validate** with a `POST /api/setting-management/emailing/send-test-email`
   call (Pro module endpoint exposed at Swagger) from the host admin UI.
   This goes through the same `IEmailSender` path as every other email
   consumer, so a success here means every future consumer will work.

Folder touches:
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`
  -- narrow the `#if DEBUG` guard.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` -- add
  `Settings:Abp.Mailing.*` block with placeholder values.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json`
  (or environment-specific override) -- real encrypted SMTP password.
- `src/HealthcareSupport.CaseEvaluation.Domain/Emailing/` (optional, new
  folder) -- placeholder `TemplateDefinitionProvider` when downstream work
  needs a template.

No migration, no new entity, no new AppService, no new proxy, no new Angular
code. Consumers (new `IEmailSender` call sites) come with later capabilities.

## Why this solution beats the alternatives

- **Lowest code change per constraint**: one file edit in DomainModule, one
  settings block in appsettings.json. Every existing ABP module
  (`/api/setting-management/emailing/send-test-email`, Account
  forgot-password, Identity 2FA) starts sending automatically.
- **Provider-agnostic**: same `Abp.Mailing.Smtp.*` keys work against SES,
  Office 365, Azure Communication Services SMTP relay, smtp4dev, Postmark,
  etc. Config-only swap if Gesco's BAA strategy changes later.
- **Per-tenant aware for free**: ABP Settings resolves per-tenant overrides
  via the existing SettingManagement UI. If a tenant wants a custom
  from-address (BRAND-03 post-MVP gap), only a setting row is needed.
- **HIPAA-viable today**: AWS SES is on the HIPAA-eligible list under the
  AWS BAA, which Adrian can sign via AWS Artifact. No new vendor contracts.

## Effort (sanity-check vs inventory estimate)

Inventory says M (2-5 days). Analysis confirms **S-M (1-2 days)** for the
CC-01 slice alone:
- ~1 hour to narrow the `NullEmailSender` guard and add the settings block.
- ~30 min to provision SES SMTP credentials (assuming an AWS account with
  SES access).
- ~2 hours for domain/DKIM/SPF verification + sender-identity approval
  (wall-clock time waiting for DNS; not Adrian's hands-on time).
- ~2 hours to add `appsettings.secrets.json` entries, test
  `/api/setting-management/emailing/send-test-email`, test
  `/api/account/send-password-reset-code`, verify receipt.
- ~1 hour to add a no-op `TemplateDefinitionProvider` so downstream work
  has a pattern.

Inventory's M accounts for downstream template work +
scheduler-notifications tie-in, which live in other briefs. The CC-01 slice
alone is closer to S. Flag for the estimate roll-up in Phase 5.

## Dependencies

- **Blocks** `scheduler-notifications` (9 recurring jobs each send email
  via `IEmailSender`).
- **Blocks** `templates-email-sms` (email template bodies rendered via
  `ITemplateRenderer` and sent via `IEmailSender`).
- **Blocks** `account-self-service` (forgot-password and email-verification
  flows call `IEmailSender` through ABP Account module; in Release they
  throw `SmtpException` today until CC-01 is resolved).
- **Blocks** `appointment-documents` (OLD `SendDocumentEmail` surface --
  port lands here or in templates-email-sms, depending on how the
  downstream brief scopes it).
- **Blocked by**: none strictly. SMTP settings are self-contained config.
- **Soft-blocked-by** `blob-storage-provider` only when downstream
  consumers (appointment-documents, joint-declarations) attach files in
  outbound email. The SES SMTP message-size limit is 40 MB per message;
  policy constraint is fine for typical appointment PDFs.
- **Blocked by open question**: "Adrian, please clarify: when NEW sends
  email, SES-native (custom `IEmailSender`) or ABP SMTP with SES SMTP
  credentials? SMTP-over-SES is lowest friction." -- verbatim from
  `docs/gap-analysis/06-cross-cutting-backend.md:191`. The recommended
  solution assumes the "ABP SMTP over SES" answer. If Adrian chooses
  SES-native, the recommended-solution section above collapses to
  option 2 (custom `SesEmailSender`) with +2-3 days effort and a loss of
  the per-tenant override path. No other capability blocks on this
  answer.

## Risk and rollback

- **Blast radius**: low. One module file and one settings file. No EF
  migration, no API shape change, no Angular change. If SMTP goes wrong,
  the symptom is `SmtpException` in the HTTP response body for send
  paths; no data corruption, no cross-tenant leak. Account module email
  paths (password reset, email confirmation) would fail until fixed, but
  no other entity is affected.
- **Rollback**: revert the two edits (`CaseEvaluationDomainModule.cs`
  guard widening back to `#if DEBUG`, `appsettings.json` settings block
  deletion). Alternatively keep the settings block but set
  `Abp.Mailing.Smtp.Host` to empty string and re-register
  `NullEmailSender` unconditionally. No downtime; no state to clean up.
- **Secrets risk**: SES SMTP credentials must not land in git. Use
  `appsettings.secrets.json` (already `.gitignore`-adjacent) or a CI
  secret store. If committed by mistake, rotate via SES console
  immediately.

## Open sub-questions surfaced by research

- **Encrypted password storage**: ABP expects
  `Settings:Abp.Mailing.Smtp.Password` to be a ciphertext produced by
  `ISettingEncryptionService.Encrypt()`. Who runs the encrypt helper in
  the dev loop, and how does the CI pipeline encrypt the production value
  before writing it into the deployed config? Out of scope for this
  brief; surface to Adrian as an operational runbook item.
- **Dev-loop choice**: should Debug resolve to `NullEmailSender` (silent
  drop) or to smtp4dev (local inbox)? Nice-to-have, not MVP-blocking.
- **Per-tenant from-address**: BRAND-03 is the post-MVP placeholder for
  tenant-level branding in email templates. No work needed in CC-01, but
  note that the solution as written already makes BRAND-03 a config-only
  exercise.
- **DefaultFromAddress ownership**: the exact string (`noreply@gesco.com`?
  `appointments@gesco.com`?) affects DKIM/SPF/DMARC records. Adrian to
  confirm with business.
