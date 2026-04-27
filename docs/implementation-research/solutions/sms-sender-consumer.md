# SMS sender consumer

## Source gap IDs

- CC-02 -- [06 / CC-02](../../../main/docs/gap-analysis/06-cross-cutting-backend.md)
- Track 10 erratum 2 -- [10 / erratum 2](../../../main/docs/gap-analysis/10-deep-dive-findings.md)

## NEW-version code read

- No Twilio NuGet package referenced in any `.csproj` across
  `src/HealthcareSupport.CaseEvaluation.*` (confirmed via track-06 grep note at
  `06-cross-cutting-backend.md:111`).
- No `ISmsSender` consumer in `Application/`, `Domain/`, or `HttpApi.Host/`
  (track-06 line 111: "Grep 0 matches").
- No `Volo.Abp.Sms` or `Volo.Abp.Sms.Twilio` module dependency wired in
  `CaseEvaluationDomainModule.cs` or `CaseEvaluationHttpApiHostModule.cs`
  (track-06 line 111).
- Email infrastructure IS wired (`AbpEmailingModule` + `NullEmailSender` under
  DEBUG, per `CaseEvaluationDomainModule.cs:17,38,59-62`); SMS does not have an
  analogous module wiring.
- No `[Authorize]` SMS-related permission branch in
  `CaseEvaluationPermissionDefinitionProvider.cs`; permission tree has 62
  permissions, none SMS-scoped.
- `appsettings.json` in `HttpApi.Host/` contains no `Sms:*` configuration
  section (neither key material nor feature flag).
- No phone-number normalisation helper under `Domain.Shared/` (would be needed
  for E.164 compliance if SMS were implemented).

## Live probes

None run. SMS endpoints are expected absent; confirming absence via grep of the
NEW codebase was sufficient evidence. Test SMS sending is forbidden (capability
brief constraints + HIPAA: would require a real phone number, leaving a
persistent carrier-side record).

## OLD-version reference

- `Infrastructure\TwilioSmsService.cs:12-49` -- `ITwilioSmsService.SendSms`
  gated by `isSMSEnable` (`server-settings.json:54 = false`); prepends
  `twilio.twilioCountryCode = +91` (wrong for US -- pre-existing bug in OLD).
- Bootstrap at `TwilioSmsService.cs:16-21` reads `twilioAccountSid` +
  `twilioAuthToken` from `ServerSetting` and calls `TwilioClient.Init`.
- Consumed ONLY by `SchedulerDomain.cs` (`06-cross-cutting-backend.md:50-51`):
  6 of 9 recurring jobs call Twilio; 2 more call sites are commented out.
- **Track 10 erratum 2 (decisive):** `AppointmentDomain.cs:839-881` `SendSMS`
  switch block: every case commented out. Line 877 hard-sets `isSendSMS = false`
  before reaching any Twilio call. Status-transition SMS is 100% disabled.
- **`server-settings.json:54`:** `isSMSEnable: false` in the deployed config.
- Net effect in production: OLD has Twilio wired, but no status-transition
  path invokes it, scheduler path is gated off, and config flag disables any
  residual call. SMS is effectively not running.
- Implication for MVP: port value is conditional on whether any clinic
  deployment has ever flipped `isSMSEnable` to `true`. If not, CC-02 is not an
  MVP gap.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict.
- Row-level `IMultiTenant` (ADR-004); any SMS job body must persist `TenantId`
  in job args and wrap send in `using (_currentTenant.Change(tenantId))`
  (matches the pattern flagged in track 10 Part 4 for background jobs).
- Riok.Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext
  (ADR-003), no `ng serve` (ADR-005) -- none directly constrain SMS, but a port
  implementation must respect them.
- HIPAA applicability: SMS content MUST NOT include PHI (patient name, DOB,
  case details, diagnosis). Reminder text must reference generic appointment
  information only (time + clinic phone).
- US SMS regulatory: TCPA + CTIA guidelines require opt-in, STOP/HELP support,
  and E.164 formatting. Twilio enforces most of this at the carrier level, but
  the application must surface an opt-in checkbox at patient signup and
  honour inbound STOP messages via a Twilio webhook.
- Depends on `background-jobs-infrastructure` (CC-03) if recurring reminder
  jobs are the primary SMS consumer -- Q18 blocks that capability, and SMS
  usefulness is narrow without it.

## Research sources consulted

1. ABP `Volo.Abp.Sms` module reference -- `https://abp.io/docs/latest/framework/infrastructure/sms` (accessed 2026-04-24). HIGH confidence. Confirms `ISmsSender.SendAsync(SmsMessage)` interface and that a provider implementation is needed to actually send.
2. Twilio `twilio-csharp` SDK README -- `https://github.com/twilio/twilio-csharp` (accessed 2026-04-24). HIGH confidence. Package `Twilio` on nuget.org, .NET Standard 2.0, Twilio REST API v1.
3. FCC TCPA summary for healthcare messaging -- `https://www.fcc.gov/general/telephone-consumer-protection-act-1991` (accessed 2026-04-24). HIGH confidence. Opt-in required; STOP/HELP compliance mandatory.
4. CTIA Messaging Principles and Best Practices 2024 -- `https://api.ctia.org/wp-content/uploads/2024/01/240112-CTIA-Messaging-Principles-and-Best-Practices-FINAL.pdf` (accessed 2026-04-24). HIGH confidence. Clarifies consent categories and recurring messaging obligations.
5. ABP GitHub issue #9921 -- `https://github.com/abpframework/abp/issues/9921` (accessed 2026-04-24). MEDIUM confidence (GitHub issue discussion). Community guidance on wiring custom `ISmsSender` with Twilio.
6. `Volo.Abp.Sms` package on nuget.org -- `https://www.nuget.org/packages/Volo.Abp.Sms` (accessed 2026-04-24). HIGH confidence. Confirms the package exists and is compatible with ABP 10.x.

## Alternatives considered

1. **Defer SMS from MVP entirely.** Rationale: track-10 erratum shows OLD ships with SMS effectively disabled. Porting a feature that the running product does not use adds scope without user value. Tag: **chosen (default)**.
2. **Port SMS via `Volo.Abp.Sms` + `ISmsSender` with custom Twilio implementation.** Add `Volo.Abp.Sms` module dependency, implement `TwilioSmsSender : ISmsSender`, register in `CaseEvaluationDomainModule`. Read Twilio credentials from `IConfiguration` (`Sms:Twilio:*`) or ABP `ISettingProvider`. Hook into scheduler-notifications capability for recurring reminders. Tag: **conditional** -- only if deployment check shows `isSMSEnable:true` anywhere in production. Effort M (2-5 days) because it needs permission tree entry, template wiring, opt-in UI, STOP/HELP webhook, and tests.
3. **Port SMS via a third-party community package like `Volo.Abp.Sms.Twilio`.** Same shape as option 2 but lean on a community adapter rather than hand-rolling. Tag: **rejected** -- no vetted community package with an ABP 10.x compatibility claim found at time of research; vendor risk is higher than writing ~40 lines of `TwilioSmsSender` against the stable `ISmsSender` contract.
4. **Use Twilio directly without the ABP `ISmsSender` abstraction.** Replicate OLD's shape. Tag: **rejected** -- breaks the provider-abstraction pattern ABP standardises on (same reason NEW uses `IEmailSender` and `IBlobContainer`). Swapping providers post-launch would require touching every call site.
5. **Use AWS SNS instead of Twilio.** AWS SNS also supports SMS; same cost tier for low volume. Tag: **rejected** -- OLD history is Twilio; reusing the existing carrier registration (including 10DLC brand registration if already in place) is faster than onboarding a new provider.

## Recommended solution for this MVP

**Default (recommended): defer SMS from MVP.** Leave `CaseEvaluationDomainModule` as-is (no `Volo.Abp.Sms.Twilio` dependency). Leave `ISmsSender` unwired. Document the deferral in `docs/decisions/` as ADR-006 ("SMS deferred pending deployment confirmation"). Ship the MVP without SMS and revisit once Adrian confirms whether any clinic deployment has `isSMSEnable:true`.

**Conditional fallback if deployment check proves SMS is in use:** Add `Volo.Abp.Sms` module dep in `CaseEvaluationDomainModule.cs` alongside `AbpEmailingModule`. Create `src/HealthcareSupport.CaseEvaluation.Domain/Sms/TwilioSmsSender.cs` implementing `ISmsSender` with a constructor that reads `Sms:Twilio:AccountSid`, `:AuthToken`, `:FromNumber` from `IConfiguration`. Wire via `context.Services.Replace(ServiceDescriptor.Transient<ISmsSender, TwilioSmsSender>())` in `CaseEvaluationDomainModule.ConfigureServices`. Add permission `CaseEvaluation.Sms.Send` to the permission tree. Store per-tenant "SMS enabled" flag via ABP `ISettingManager` (mirror OLD's `isSMSEnable`). Inject `ISmsSender` into the reminder background jobs (blocks on `background-jobs-infrastructure`). Consumers are the recurring reminder jobs in the `scheduler-notifications` capability; no status-transition SMS (match OLD erratum 2). Add opt-in field to patient profile and a STOP/HELP webhook endpoint in `HttpApi.Host/` at `/api/app/sms/twilio-webhook`. Effort M.

## Why this solution beats the alternatives

- **Matches actual OLD behaviour.** OLD runs with SMS disabled in production (track-10 erratum 2). Porting nothing matches reality better than porting an unused feature.
- **Preserves the ABP provider abstraction** (conditional path): using `ISmsSender` lets NEW swap Twilio for AWS SNS or a test fake without touching the 9 (or fewer) reminder jobs that would call it.
- **HIPAA-safer by default.** Zero SMS path means zero risk of PHI leaking into an SMS body. If the conditional path later adds SMS, the reminder templates can be reviewed in isolation before enabling.
- **Respects the TCPA opt-in requirement.** Deferring lets Adrian design the opt-in UX up front rather than retrofit it; the conditional path makes opt-in + STOP/HELP first-class.

## Effort (sanity-check vs inventory estimate)

- Inventory in track 06 Delta table (`06-cross-cutting-backend.md:143`) says **M-L** (2-10 days).
- Track 10 erratum 2 revised severity from MVP-blocking to **needs-decision**.
- Confirmed: if **defer (default)** -- effort **0 for MVP**; add ADR-006 = ~0.5 day post-decision.
- If **port (conditional)** -- effort **M (2-5 days)** post-MVP. One day of that is the opt-in UI + STOP/HELP webhook + per-tenant setting, which OLD skipped but US-law requires for a compliant launch.
- Inventory estimate was predicated on MVP-blocking + status-transition SMS + 9-job port. Erratum shrinks the actual port surface to the 6 recurring-reminder jobs that OLD actually calls Twilio from, and the setting-flag gate. Port effort trimmed accordingly.

## Dependencies

- Blocks: `scheduler-notifications` only in the conditional port path. In the
  default path, `scheduler-notifications` uses email-only and is not blocked by
  CC-02.
- Blocked by: `background-jobs-infrastructure` (CC-03) in the conditional port
  path -- recurring reminder jobs are the only SMS consumer once status
  transitions are excluded (per OLD erratum). If CC-03 is deferred, the
  conditional port's primary consumer is absent. Default path has no blocker.
- Blocked by open question: `Adrian, please clarify: MVP SMS off (matching OLD `isSMSEnable = false`)? Defers the Twilio SDK decision.` (`06-cross-cutting-backend.md:192`)

## Risk and rollback

- **Default (defer) blast radius:** zero. No code changes land. Follow-up work
  is restricted to writing ADR-006. Rollback is trivially a no-op.
- **Conditional (port) blast radius:** one new module dep, one new service
  class, one new config section, one new permission, one new webhook endpoint,
  one new patient-profile opt-in field, one new per-tenant setting, one new
  migration (if opt-in stored on `Patient`). If live SMS sends misbehave, set
  per-tenant `Sms:Enabled = false` via `ISettingManager` to kill the path
  without code deploy. Full rollback: drop the `Volo.Abp.Sms` dep and revert
  the migration.
- **HIPAA rollback trigger:** if any reminder template is found to include
  PHI, disable SMS immediately via the per-tenant flag and re-audit templates
  before re-enabling.
- **Legal rollback trigger:** if Twilio flags the brand for suspected opt-in
  non-compliance (10DLC or toll-free verification rejected), disable via the
  tenant flag.

## Open sub-questions surfaced by research

- Does any Gesco OLD deployment have `isSMSEnable:true` in its
  `server-settings.json`? This single answer determines which path applies.
  Adrian should check production config across every clinic before ADR-006 is
  finalised.
- If SMS is ported: is the clinic's Twilio number already 10DLC-registered
  (required for US A2P messaging since 2023)? If not, factor in the 2-6 week
  carrier registration window into the post-MVP effort.
- Phone-number validation scope: does NEW's `Patient.PhoneNumber` already
  enforce E.164? Grep confirms it is a free-text string today (track-06
  context); if SMS goes live, add validation at DTO boundary.
- Who owns the STOP/HELP inbound handler URL in production? Likely
  `HttpApi.Host` -- but needs confirmation that the host's public URL is
  stable and reachable by Twilio.
