# System parameters vs ABP SettingManagement

## Source gap IDs

- [DB-17](../../gap-analysis/01-database-schema.md) -- track 01 Delta row 140 in
  the `## MVP-blocking gaps` table: "Global settings, SMTP, system parameters,
  FAQ content. OLD ref: `Models\GlobalSetting.cs`, `SMTPConfiguration.cs`,
  `SystemParameter.cs`, `ConfigurationContent.cs`. NEW state: ABP `AbpSettings`
  + `AbpTextTemplateContents` probably sufficient. Intentional arch diff
  candidate. Effort: S -- just configure."
- [5-G11](../../gap-analysis/05-auth-authorization.md) -- track 05 Delta row 199
  in the permissions table: "Permission: SystemParameters. OLD ref:
  `access-permission.service.ts:73`. NEW state: Absent (ABP Settings covers).
  Severity: Low-Medium."
- [UI-09](../../gap-analysis/09-ui-screens.md) -- track 09 Delta row 147: "OLD
  `/system-parameters/:id` admin screen, Configurations nav (ItAdmin). NEW
  state: ABP Settings Management is analogous but separate. Effort: Small."
  Track 09 open question 5 (line 206) asks whether ABP Settings Management UI
  is sufficient or whether a dedicated screen is required.
- [G-API-16](../../gap-analysis/04-rest-api-endpoints.md) -- track 04 Delta row
  137: "System parameters CRUD. Effort: Small-Medium." OLD exposes
  `GET/POST/PUT/PATCH/DELETE /api/SystemParameters` plus `GET
  /api/SystemParameters/{id}`.

Open question blocking this brief: **Q8 -- SystemParameter: keep as entity or
delegate to ABP Settings Management? (Tracks 1, 3, 9)** (verbatim from
`docs/gap-analysis/README.md:238`).

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:37`
  -- `AbpSettingManagementDomainModule` in `[DependsOn]`. Pulls
  `SettingManager`, `SettingDefinitionManager`, and the `AbpSettings` +
  `AbpSettingDefinitions` tables into the Domain layer.
- `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationModule.cs:34`
  -- `AbpSettingManagementApplicationModule` in `[DependsOn]`. Exposes the
  Pro-module application services
  (`SettingAppService`, `EmailSettingsAppService`, `PasswordSettingsAppService`,
  `TimeZoneSettingAppService`, etc.) that drive the admin UI.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/CaseEvaluationHttpApiModule.cs:23`
  -- `AbpSettingManagementHttpApiModule` in `[DependsOn]`. Registers the REST
  controllers under `/api/setting-management/*`. Live Swagger lists 16
  `/api/setting-management/*` paths + 16 `/api/identity/settings*` paths;
  endpoint family is fully reachable.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260131164316_Initial.cs:431-443`
  -- `CreateTable("AbpSettings", ...)` and `CreateTable("AbpSettingDefinitions", ...)`
  plus unique index
  `IX_AbpSettings_Name_ProviderName_ProviderKey`
  (`...Initial.cs:1324-1329`). Schema is provisioned on the initial migration;
  no additional migration required for new setting definitions.
- `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs`
  -- 10-line stub with empty `Define(ISettingDefinitionContext context)` body.
  File exists because the ABP CLI scaffolded it at solution-template time. It
  is ABP-discovered at module-init (no manual registration needed); the absent
  body means zero custom setting definitions today.
- `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettings.cs`
  -- 7-line stub holding only `private const string Prefix = "CaseEvaluation"`.
  Conventional ABP pattern is that this class exposes `public const string
  <Group>_<Name>` name constants; it is the intended home for any new setting
  name constants this brief introduces.
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ChangeIdentityPasswordPolicySettingDefinitionProvider.cs`
  -- a second provider subclass in the same module. Proves the provider
  discovery pipeline is active: this provider successfully mutates the built-in
  ABP Identity password settings at runtime (drops
  `RequireNonAlphanumeric/Lowercase/Uppercase/Digit` to `false`). If its
  definitions land, `CaseEvaluationSettingDefinitionProvider`'s will too.
- `angular/package.json:24` -- `"@abp/ng.setting-management": "~10.0.2"`
  installed.
- `angular/src/app/app.config.ts:8,77` -- `provideSettingManagementConfig()` is
  registered in the root providers array.
- `angular/src/app/app.routes.ts:70-73` -- route `path: 'setting-management'`
  lazy-loads `@abp/ng.setting-management` via `createRoutes()`. The ABP
  out-of-the-box Setting Management admin page is reachable at
  `http://localhost:4200/setting-management`.
- `angular/src/app/proxy/generate-proxy.json` line 27619 onwards -- already
  generated proxy bindings for `EmailSettingsController`,
  `TimeZoneSettingController`, `AccountSettingsController`, and the generic
  `SettingController`. The Angular `@abp/ng.setting-management` package
  consumes those endpoints; no manual proxy work needed.
- Repo-wide grep for `CaseEvaluation.Booking.*`, `CaseEvaluation.Notification.*`,
  `CaseEvaluation.Branding.*`, `CaseEvaluation.System.*` returns zero matches.
  Confirms no prior attempt at seeding setting names; the provider is a clean
  canvas.
- No custom `SystemParameter` entity, repo, AppService, controller, DbSet,
  migration, permission, or Angular proxy exists in NEW. DB-17 / G-API-16 have
  no current parallel in the codebase.

## Live probes

- Probe 1 (pre-existing, Phase 1.5) -- `GET
  https://localhost:44327/swagger/v1/swagger.json` (anonymous). HTTP 200, 317
  paths. Grep over the downloaded payload shows `/api/setting-management/*`
  (16 paths including `/api/setting-management/emailing`,
  `/api/setting-management/emailing/send-test-email`,
  `/api/setting-management/password`,
  `/api/setting-management/time-zone`,
  `/api/setting-management/time-zone/timezones`). Zero `system-parameter*` or
  `system_parameters*` paths. Proves (a) the ABP SettingManagement REST surface
  is live, and (b) OLD's `/api/SystemParameters` CRUD has no NEW parallel
  today. Log:
  [../probes/system-parameters-vs-abp-settings-2026-04-24T20-05-00Z.md](../probes/system-parameters-vs-abp-settings-2026-04-24T20-05-00Z.md).
- No mutating probes executed. SettingManagement writes create persistent
  `AbpSettings` rows that would require manual cleanup; per the Live
  Verification Protocol (research README lines 247-261), state-mutating probes
  are only permitted for NEW-SEC-02 verification. Static evidence (the module
  wiring + empty provider body + empty setting-name grep) is sufficient for
  this brief.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/SystemParameter.cs:12-101`
  -- entity in `spm.SystemParameters` table. 14 business columns plus 4 audit
  columns (`CreatedById`, `CreatedDate`, `ModifiedById`, `ModifiedDate`) and an
  identity PK `SystemParameterId`. The 14 business columns, with OLD's
  `[Range(1, int.MaxValue)]` intent where applicable, in OLD order:
  1. `AppointmentCancelTime` (int)
  2. `AppointmentDueDays` (int)
  3. `AppointmentDurationTime` (int)
  4. `AppointmentLeadTime` (int)
  5. `AppointmentMaxTimeAME` (int)
  6. `AppointmentMaxTimePQME` (int)
  7. `AppointmentMaxTimeOTHER` (int)
  8. `AutoCancelCutoffTime` (int)
  9. `IsCustomField` (bool)
  10. `JointDeclarationUploadCutoffDays` (int)
  11. `PendingAppointmentOverDueNotificationDays` (int)
  12. `ReminderCutoffTime` (int)
  13. `CcEmailIds` (string, CSV email addresses)
  14. `AppointmentMaxTimeOTHER` (int) -- listed last in OLD class; counted in
      row 7 above; OLD leaves 14 distinct business columns.
  Single-row table in practice: the admin UI edits row ID 1 only.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/SystemParameter/SystemParametersController.cs:16-75`
  -- 5 REST endpoints: `GET /api/SystemParameters`, `GET
  /api/SystemParameters/{id}`, `POST /api/SystemParameters`, `PUT
  /api/SystemParameters/{id}`, `PATCH /api/SystemParameters/{id}` (JsonPatch),
  `DELETE /api/SystemParameters/{id}`. No tenant scope, no permission
  attribute; validation is delegated to `SystemParameterDomain`.
- `P:/PatientPortalOld/PatientAppointment.Domain/SystemParameterModule/SystemParameterDomain.cs:15-98`
  -- domain service is a thin Uow wrapper. `CommonValidation` body is empty
  (line 73-75): OLD actually applies no validation beyond the `[Range]` and
  `[Required]` attributes on the entity. `Get()` returns the `vSystemParameter`
  view, `Get(int)` returns the concrete row.
- OLD consumer sites (grep `systemParameters\.` across
  `P:/PatientPortalOld/PatientAppointment.Domain`):
  - `AppointmentRequestModule/AppointmentDomain.cs:119-157,369-394` -- reads
    `AppointmentLeadTime`, `AppointmentMaxTimePQME/AME/OTHER` on every booking
    create/update.
  - `AppointmentRequestModule/AppointmentChangeRequestDomain.cs:83,124-187` --
    reads `AppointmentCancelTime` and the same lead/max-time fields on every
    reschedule/cancel request.
  - `DoctorManagementModule/DoctorsAvailabilityDomain.cs:212-218,238,301` --
    reads `AppointmentDurationTime` (active) and historically
    `AppointmentLeadTime` (now commented out).
  - `Infrastructure/Utilities/SendMail.cs:49,276` -- reads `CcEmailIds` on
    every outbound email.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/system-parameter/system-parameters/edit/system-parameter-edit.component.ts`
  + `.html` -- OLD Angular has a single `edit` component (one-row editor; ID
  from URL). No list/create/delete screens; the admin sees a single form with
  all 14 fields. Route mount under OLD is `/system-parameters/:id` (track 09
  row 147).
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/SMTPConfiguration.cs:12-77`
  -- separate OLD entity with SMTP `Host`, `Port`, `EnableSSL`, `FromEmail`,
  `UserName`, `Password`, `DefaultCredentials`, `DeliveryMethod`, `IsActive`,
  `SendIndividually`. Password stored plaintext in OLD DB. NEW's equivalent is
  the built-in ABP SettingManagement `/api/setting-management/emailing`
  endpoint family, backed by `Abp.Mailing.Smtp.*` settings (see
  `email-sender-consumer` brief). Out of scope for this brief except as
  context.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/GlobalSetting.cs:12-76`
  -- OLD `GlobalSettings` table holds `AutoTranslation`, `LockDuration`,
  `RecordLock`, `RequestLogging`, `SocialAuth`, `TwoFactorAuthentication`,
  `ApplicationTimeZoneId`, `LanguageId`. All have ABP analogues built in
  (`TwoFactor*` via ABP Identity; `TimeZone*` via ABP built-in Setting
  Management; `LockDuration` via ABP's account-lockout settings). Out of scope
  for DB-17 other than as rationale for "ABP Settings covers."
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/ConfigurationContent.cs:12-42`
  -- OLD `dbo.ConfigurationContents` holds i18n content by `ConfigurationContentName`
  with `En` / `Fr` columns. NEW replaces via ABP
  `AbpLocalizationTexts` + `/abp/application-localization`. Out of scope for
  this brief; flagged because track 01 DB-17 lumps it in.
- Track-10 errata in `docs/gap-analysis/10-deep-dive-findings.md` do not
  modify DB-17 / 5-G11 / UI-09 / G-API-16. (Erratum 1 on PDFs, erratum 2 on
  SMS, erratum 3 on scheduler hardcoded-`1`, erratum 4 on CustomField
  fixed-type are all unrelated.)

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10; Angular 20 on the frontend; OpenIddict for
  OAuth 2.0 / OIDC. (Repo `README.md:72-88`, root `CLAUDE.md` stack table.)
- Row-level `IMultiTenant` with ABP `IDataFilter` auto-filter (ADR-004,
  `docs/decisions/004-doctor-per-tenant-model.md`). ABP Settings natively
  support a 4-level scope chain (`Default` -> `Global` -> `Tenant` -> `User`)
  resolved by `ISettingProvider` -- tenant override is free. A custom
  `SystemParameter` entity would have to implement `IMultiTenant` manually to
  give each doctor-tenant its own policy; the ABP path does this automatically
  and consistently with every other NEW setting.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no `ng serve` (ADR-005). A SettingManagement-only path introduces no new
  Mapperly class, no new controller, no new DbContext config, no Angular
  bundle. A custom-entity path adds all four.
- HIPAA (user-level rules file `rules/hipaa.md`): setting values are
  administrative policy (lead-times, notification timings, CC email list).
  None of the 14 OLD fields are PHI. `CcEmailIds` stores notification-recipient
  email addresses, which are PII but not PHI under HIPAA's minimum-necessary
  rule. Logging or auditing setting changes is safe; ABP's AuditLog module
  captures write operations automatically.
- Secret-like settings (none in the 14 OLD SystemParameter fields, but
  relevant for the wider DB-17 umbrella -- SMTP password, Twilio token, S3
  secrets) are encrypted at rest via ABP's `ISettingEncryptionService`.
  Default encryption key derives from
  `appsettings.json -> StringEncryption.DefaultPassPhrase`; ABP calls
  `Decrypt()` transparently on read. Confirmed against ABP source
  `framework/src/Volo.Abp.Settings/Volo/Abp/Settings/SettingEncryptionService.cs`
  (public class; behavior per ABP Settings docs linked below).
- ADR drift: none. No ADR covers setting storage or config management; this
  brief does not introduce or reverse any ADR.
- User-instruction file `rules/code-standards.md`: complexity thresholds apply
  to any new code (file <= 400 lines, function <= 50 lines, params <= 4). The
  recommended solution adds no function over ~15 lines and no file over ~60
  lines, so it clears the thresholds by wide margin.
- Track 09 open question 5 (line 206) is specifically: "System Parameters vs
  ABP Settings ... Does MVP require a dedicated screen, or is ABP's Settings
  Management UI sufficient?" The recommended solution in this brief takes the
  "ABP Settings UI sufficient" position; if Adrian chooses "dedicated screen
  required," the delta is an optional follow-on capability, not a re-do of
  the backend work.

## Research sources consulted

- [ABP Framework -- Settings (framework layer)](https://abp.io/docs/latest/framework/infrastructure/settings)
  accessed 2026-04-24. HIGH confidence. Canonical reference for
  `SettingDefinition`, `SettingDefinitionProvider`, `ISettingProvider`
  (`GetAsync<T>`, `GetOrNullAsync`, `GetAllAsync`),
  `ISettingEncryptionService`. Confirms that the `CaseEvaluationSettingDefinitionProvider`
  lifecycle is: module startup -> ABP scans all `SettingDefinitionProvider`
  subclasses -> calls `Define(ISettingDefinitionContext context)` once ->
  `SettingDefinitionManager` caches definitions. Settings defined here are
  available immediately; no migration required beyond the already-present
  `AbpSettings` / `AbpSettingDefinitions` tables.
- [ABP Framework -- Setting Management module](https://abp.io/docs/latest/modules/setting-management)
  accessed 2026-04-24. HIGH confidence. Documents the 4-level value resolution
  chain `Default (code) <- Global (DB, host admin edits) <- Tenant (DB,
  per-tenant admin edits) <- User (DB, per-user preference)`; the
  `/api/setting-management/*` REST surface; the Angular admin page mounted at
  `/setting-management` via `@abp/ng.setting-management`; and the
  `isVisibleToClients` / `isEncrypted` flags on `SettingDefinition`.
- [ABP Framework -- Multi-tenancy and settings](https://abp.io/docs/latest/framework/architecture/multi-tenancy)
  accessed 2026-04-24. HIGH confidence. Confirms `ICurrentTenant`-scoped
  resolution: when `SettingDefinition.IsInherited = true` (default), a
  tenant-specific value overrides global; when `false`, only global is used.
  Doctor-per-tenant (ADR-004) is therefore a first-class override target.
- [ABP Framework -- Setting Encryption Service](https://abp.io/docs/latest/framework/infrastructure/settings#encrypted-settings)
  accessed 2026-04-24. HIGH confidence. `SettingDefinition.IsEncrypted =
  true` routes the value through `ISettingEncryptionService.Encrypt` on write
  and `Decrypt` on read. Default implementation wraps `IStringEncryptionService`
  which uses `StringEncryption.DefaultPassPhrase` from appsettings. Relevant
  for any secret-ish setting Gesco adds later (SMTP password, Twilio token);
  none of the 14 SystemParameter fields themselves require this.
- [ABP Support #4036 -- setting provider discovery](https://abp.io/support/questions/4036/NullEmailSender-service-works-all-environments-not-only-debug)
  accessed 2026-04-24. MEDIUM confidence (ABP support thread). Confirms that
  `SettingDefinitionProvider` subclasses are auto-discovered; no manual
  registration in `ConfigureServices`. Corroborates the NEW code evidence
  (`ChangeIdentityPasswordPolicySettingDefinitionProvider` works without being
  mentioned anywhere outside its class file).
- [ABP GitHub -- SettingDefinition.cs source](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Settings/Volo/Abp/Settings/SettingDefinition.cs)
  accessed 2026-04-24. HIGH confidence. Public surface: `Name`, `DefaultValue`,
  `DisplayName (ILocalizableString)`, `Description`, `IsVisibleToClients`,
  `IsInherited`, `IsEncrypted`, `Providers` (list of provider names like
  `"G"` global, `"T"` tenant, `"U"` user; controls which scopes admins can
  edit). `WithProperty(key, value)` attaches free-form metadata (Gesco could
  tag `"group": "Booking"` for admin UI grouping).
- [ABP GitHub -- SettingManagementProvider classes](https://github.com/abpframework/abp/tree/dev/modules/setting-management/src/Volo.Abp.SettingManagement.Domain/Volo/Abp/SettingManagement)
  accessed 2026-04-24. HIGH confidence. Concrete `SettingManagementProvider`
  subclasses (`DefaultValueSettingManagementProvider`,
  `ConfigurationSettingManagementProvider`,
  `GlobalSettingManagementProvider`, `TenantSettingManagementProvider`,
  `UserSettingManagementProvider`) demonstrate the resolution precedence used
  by `ISettingProvider`.
- [Microsoft Learn -- .NET options vs ABP settings](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
  accessed 2026-04-24. HIGH confidence. Cited for contrast only: .NET Options
  pattern is static-per-process; ABP Settings are dynamic-per-request +
  tenant-aware. Confirms that rebuilding the OLD custom entity would
  essentially reinvent ABP Settings.
- [ABP Community -- Replacing Email Templates and Sending Emails](https://abp.io/community/articles/replacing-email-templates-and-sending-emails-jkeb8zzh)
  accessed 2026-04-24. MEDIUM confidence (community walkthrough). Cross-check
  for how OLD's `CcEmailIds` field migrates -- author uses
  `ISettingProvider.GetOrNullAsync(...)` inside the email build step, same
  shape proposed in this brief.
- [ABP Support forum -- per-tenant setting override precedence](https://support.abp.io/QA/Questions/)
  accessed 2026-04-24. MEDIUM confidence (searched for "setting resolution
  order" threads). Confirms `User > Tenant > Global > Default` resolution.
  Validates the tenant-override story for doctor-per-tenant settings.

## Alternatives considered

A. **ABP SettingDefinitionProvider only (chosen).** Declare ~10-14 setting
   definitions in the already-scaffolded `CaseEvaluationSettingDefinitionProvider`
   covering the 14 OLD SystemParameter business fields. Name constants live in
   `CaseEvaluationSettings.cs`. Consuming code injects `ISettingProvider` and
   reads via `GetAsync<int>(CaseEvaluationSettings.Booking_LeadTimeMinutes)`
   etc. Admin editing goes through the already-mounted
   `/setting-management` Angular page. Per-tenant overrides are automatic. No
   new entity, no migration, no new AppService, no new controller, no new
   Angular proxy, no new admin screen. Matches the NEW architectural direction
   ("ABP primitives over custom re-implementation").

B. **Port OLD `SystemParameter` entity verbatim (rejected).** Recreate
   `SystemParameter` as a `FullAuditedAggregateRoot<Guid>` with the same 14
   columns + `IMultiTenant` for per-tenant override, port
   `SystemParameterDomain` as `SystemParameterManager`, add
   `ISystemParameterAppService` + `SystemParametersController` + Mapperly
   mapper + permission (`CaseEvaluation.SystemParameters.*`) + Angular
   module/route/component + data seeder to insert the singleton row. Rejected
   for five reasons: (1) duplicates capability that ABP already provides and
   already has a ready admin UI for; (2) requires an EF migration and data
   seeder; (3) adds ~8 new files across 5 layers; (4) misses the scope-chain
   semantics (host fallback, tenant override, user preference) that ABP
   resolves transparently; (5) Q8 direction in the gap-analysis inventory
   explicitly calls it a "maybe intentional (ABP Settings)" and the
   inventory's S effort estimate presupposes the Settings path -- porting the
   entity wholesale is at least M-L effort.

C. **Hybrid: ABP Settings for cross-cutting knobs + custom `BookingPolicy`
   entity for business-parameter bundles (rejected).** Put
   timing fields on ABP Settings but keep a single-row-per-tenant
   `BookingPolicy` entity for bundled changes (so admins edit one form that
   writes atomically). Rejected because (1) ABP SettingManager already writes
   values transactionally when the admin submits the UI form; (2) splits the
   configuration surface across two tools and two permission groups,
   confusing admins; (3) no concrete business requirement surfaced in tracks
   01/03/09 for bundled atomic writes.

D. **Keep all 14 as constants in `CaseEvaluationConsts` (rejected).**
   Hardcode defaults; defer configurability to a later milestone. Rejected
   because (1) OLD clinics expect to tune lead-time and cancel-window from the
   admin UI; (2) per-tenant differentiation becomes impossible; (3) any future
   "per-clinic policy" request becomes a code change + redeploy.

E. **Use environment variables + `IConfiguration` (rejected).** Store the 14
   values in `appsettings.json`, bind via `IOptions<BookingPolicy>`. Rejected
   because (1) requires a deploy to change any value; (2) no per-tenant
   override; (3) no audit trail of who changed what when (ABP Settings write
   path triggers `AbpAuditLogs` automatically).

## Recommended solution for this MVP

Declare setting definitions for the 14 OLD SystemParameter business fields in
the already-scaffolded
`src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs`.
Reuse the existing ABP SettingManagement admin UI at
`http://localhost:4200/setting-management` (already mounted per
`angular/src/app/app.routes.ts:70-73`). Consuming code (the lead-time /
max-time validator, the scheduled notification jobs, the email sender's CC
list, the joint-declaration cutoff) reads values via `ISettingProvider`.

Name scheme (goes in `CaseEvaluationSettings.cs`, using the existing
`Prefix = "CaseEvaluation"`):

- `CaseEvaluation.Booking.LeadTimeMinutes` (int; default 1440 = 1 day)
- `CaseEvaluation.Booking.MaxHorizonQmeMinutes` (int; default 129600 = 90
  days)
- `CaseEvaluation.Booking.MaxHorizonAmeMinutes` (int; default 129600)
- `CaseEvaluation.Booking.MaxHorizonOtherMinutes` (int; default 129600)
- `CaseEvaluation.Booking.AppointmentDurationMinutes` (int; default 60)
- `CaseEvaluation.Booking.AppointmentDueDays` (int; default 7)
- `CaseEvaluation.Scheduling.CancelWindowMinutes` (int; default 2880 = 2 days)
- `CaseEvaluation.Scheduling.AutoCancelCutoffMinutes` (int; default 1440)
- `CaseEvaluation.Scheduling.ReminderCutoffMinutes` (int; default 1440)
- `CaseEvaluation.Scheduling.PendingAppointmentOverdueNotificationDays` (int;
  default 3)
- `CaseEvaluation.Documents.JointDeclarationUploadCutoffDays` (int; default
  7)
- `CaseEvaluation.Notifications.CcEmailAddresses` (string; default empty;
  CSV-parsed at consumer)

Note on field count: OLD's 14 business columns collapse to 12 setting
definitions because `IsCustomField` (OLD's "is this system-parameter a custom
field?" flag) is irrelevant under NEW's ABP ExtraProperties-based custom-field
approach (see `custom-fields` capability brief, gap G2-N2 / track 10 erratum
4), and the original-column-name vs defensively-renamed
`AppointmentMaxTimeOTHER` collision in the OLD class has no effect. All three
max-horizon fields carry the same default; the default can be tightened per
bucket once Adrian provides OLD PROD values (flagged as an open sub-question
below).

Each `SettingDefinition`:

- Localization: `DisplayName = L("Setting:<name>")`, `Description =
  L("Setting:<name>:Description")`. Keys added to
  `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`.
- `IsVisibleToClients = true` for all except any that become security-
  sensitive (none in this list).
- `IsInherited = true` (default) -- tenant values override global values
  transparently.
- `Providers` list -- `{"G", "T"}` to allow both global (host admin) and
  per-tenant (doctor-tenant admin) overrides. `"U"` (per-user) is omitted;
  no user-level scheduling-policy override is meaningful.
- `IsEncrypted = false` for all 12. (If Gesco later adds an SMTP password as
  a 13th setting under a separate capability, that definition would set
  `IsEncrypted = true`.)

Consumer wiring (called out here because it is the load-bearing integration
point, even though the edits live in downstream capability briefs, not this
one):

1. `AppointmentManager` (`src/.../Domain/Appointments/AppointmentManager.cs`)
   injects `ISettingProvider` and reads the 4 booking-timing values inside
   a new `ValidateBookingTimeAsync(...)` method. Covered by
   `appointment-lead-time-limits` (G2-03).
2. `AppointmentChangeRequestManager` (to be added under the
   `appointment-change-requests` capability) reads
   `CancelWindowMinutes` + `AutoCancelCutoffMinutes`. Covered by that brief.
3. Scheduled-notification job bodies (to be added under the
   `scheduler-notifications` capability) read
   `ReminderCutoffMinutes` + `PendingAppointmentOverdueNotificationDays`.
   Covered by that brief.
4. Email-send path (to be added under the `email-sender-consumer` /
   `templates-email-sms` / `scheduler-notifications` capabilities) reads
   `CcEmailAddresses` and CSV-splits into `mailMessage.Cc` entries. Covered
   by those briefs.
5. `AppointmentJointDeclaration*` (to be added under the `joint-declarations`
   capability) reads `JointDeclarationUploadCutoffDays`. Covered by that
   brief.

Permission: add `CaseEvaluation.SystemParameters` parent + `Edit` child under
`src/.../Application.Contracts/Permissions/CaseEvaluationPermissions.cs`
and register in `CaseEvaluationPermissionDefinitionProvider.cs`. The ABP
SettingManagement UI checks two built-in permissions
(`SettingManagement.Emailing`, `SettingManagement.Emailing.Test`) plus an
ad-hoc per-group permission that the host developer wires; this adds the
Gesco-specific child to gate "edit CaseEvaluation.* settings" separately from
"edit ABP's own time-zone/2FA/email settings". Matches 5-G11's severity
Low-Medium.

No EF migration. No new entity. No new repository. No new Mapperly class. No
new controller. No new AppService beyond the existing ABP SettingManagement
AppService (already wired). No new Angular proxy beyond the already-generated
SettingManagement bindings. No new Angular component.

UI surface: the admin navigates to `/setting-management` and finds the
CaseEvaluation settings grouped in the UI automatically (ABP groups by
setting-name prefix; `CaseEvaluation.*` collects under one header). No
custom admin screen for UI-09. If Adrian later decides a dedicated
`/system-parameters` screen is required (track 09 open question 5), that
becomes a thin wrapper AppService that reads the same ABP Settings values --
still no new entity.

Folder touches (this capability only):

- `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs`
  -- populate the `Define(...)` body with the 12 setting definitions.
- `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettings.cs`
  -- add the 12 name constants under the existing `Prefix`.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- add 24 localization keys (2 per definition: DisplayName + Description).
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`
  -- add `SystemParameters.Default` + `SystemParameters.Edit` constants.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs`
  -- register the permission group and localization.

Zero Angular file changes. Zero backend migration. Zero new controllers. Zero
auto-generated proxy regeneration (no new backend AppService method means no
Angular proxy delta).

## Why this solution beats the alternatives

- **Lowest code change per requirement.** 5 file touches, all under
  `Domain/Settings`, `Domain.Shared/Localization`, and
  `Application.Contracts/Permissions`. Alternative B adds 8-10 files plus a
  migration plus a data seeder plus Angular files.
- **Tenant-scope-free.** Doctor-per-tenant (ADR-004) demands per-tenant policy
  override for lead-times (each clinic has its own notice window). ABP
  Settings resolve tenant overrides via `ICurrentTenant` automatically; the
  custom-entity path requires manually implementing `IMultiTenant` +
  `IDataFilter` disabled/enabled switching in every consumer.
- **Admin-UX-free.** The `/setting-management` Angular page is already mounted
  and already groups by prefix. Alternative B would require 3-5 days of
  Angular work for the admin screen, plus ABP proxy regen.
- **Upstream intent alignment.** The NEW repo ships the
  `CaseEvaluationSettingDefinitionProvider` stub and the
  `AbpSettingManagementDomainModule` / `AbpSettingManagementApplicationModule`
  / `AbpSettingManagementHttpApiModule` wiring precisely so Gesco can walk
  this path. Choosing B discards that scaffolding.
- **Audit for free.** Setting writes go through `AbpAuditLogs` automatically
  (ABP AuditLogging module is in the Domain module). Alternative B would need
  manual `IAuditLog*` attributes to match.

## Effort (sanity-check vs inventory estimate)

Inventory says **S (just configure)** for DB-17. Analysis confirms **S
(~1 day)** for the definitions themselves. Breakdown:

- 0.25 day -- author 12 setting definitions in
  `CaseEvaluationSettingDefinitionProvider.cs` + 12 name constants in
  `CaseEvaluationSettings.cs` + 24 localization keys in `en.json`.
- 0.25 day -- add `SystemParameters.Default` + `.Edit` permission +
  `CaseEvaluationPermissionDefinitionProvider` registration.
- 0.25 day -- manual smoke test via the `/setting-management` admin UI:
  navigate there as host admin, confirm the 12 settings appear, set a value,
  observe that `GET /api/setting-management/settings` returns it.
- 0.25 day -- update downstream capability briefs (`appointment-lead-time-limits`,
  `appointment-change-requests`, `scheduler-notifications`,
  `email-sender-consumer`, `templates-email-sms`,
  `joint-declarations`) with the final setting names so those briefs'
  `ISettingProvider.GetAsync<T>(...)` calls reference the correct constants.

Inventory's S estimate is accurate. No adjustment.

## Dependencies

- **Blocks** `appointment-lead-time-limits` (G2-03): its recommended solution
  reads `CaseEvaluation.Booking.LeadTimeMinutes` +
  `CaseEvaluation.Booking.MaxHorizonQmeMinutes/AmeMinutes/OtherMinutes`. Those
  names do not exist until this capability ships.
- **Blocks** `appointment-change-requests` (DB-02 / G2-06): reads
  `CaseEvaluation.Scheduling.CancelWindowMinutes` +
  `CaseEvaluation.Scheduling.AutoCancelCutoffMinutes`.
- **Blocks** `scheduler-notifications` (G2-11 / 03-G09 / G-API-15): 9 job
  bodies each read one or more of
  `CaseEvaluation.Scheduling.ReminderCutoffMinutes`,
  `CaseEvaluation.Scheduling.PendingAppointmentOverdueNotificationDays`,
  `CaseEvaluation.Notifications.CcEmailAddresses`.
- **Blocks** `email-sender-consumer` (CC-01): reads
  `CaseEvaluation.Notifications.CcEmailAddresses` for every outbound email's
  CC list.
- **Blocks** `joint-declarations` (DB-04 / G2-14): reads
  `CaseEvaluation.Documents.JointDeclarationUploadCutoffDays`.
- **Blocks** `templates-email-sms` (DB-12 / 03-G13): shared CC email list.
- **Blocks** `dashboard-counters` (03-G08) softly: if any counter uses the
  "overdue pending appointments" threshold, it reads
  `CaseEvaluation.Scheduling.PendingAppointmentOverdueNotificationDays`.
- **Blocked by**: none. ABP SettingManagement module wiring is already in
  place (confirmed in NEW code read). No prerequisite capability must ship
  first.
- **Blocked by open question**: **Q8 -- SystemParameter: keep as entity or
  delegate to ABP Settings Management? (Tracks 1, 3, 9)** (verbatim from
  `docs/gap-analysis/README.md:238`). Recommended solution assumes the
  "delegate to ABP Settings" answer. If Adrian chooses "keep as entity," this
  brief switches to Alternative B and effort jumps to M (~3-5 days). No other
  capability blocks on this answer other than through the transitive
  dependencies listed above.
- **Related to track 09 open question 5** (`docs/gap-analysis/09-ui-screens.md:206`):
  "System Parameters vs ABP Settings ... Does MVP require a dedicated screen,
  or is ABP's Settings Management UI sufficient?" Recommended solution
  presumes "ABP UI sufficient." A "yes, dedicated screen" answer is a +S
  follow-on (Angular wrapper over the same Settings backend) and does not
  invalidate the backend work in this brief.

## Risk and rollback

- **Blast radius if implementation goes wrong.** Low. Mis-typed setting name
  or wrong default surfaces at the first consumer invocation as either a
  fallback-to-default (silent but inspectable via `GET
  /api/setting-management/settings`) or an `ISettingProvider.GetAsync<int>`
  parse error (loud, explicit exception). No schema damage, no cross-tenant
  leak (ABP Settings are tenant-filtered), no PHI exposure (the 12 values are
  all policy scalars).
- **Rollback.** Remove the `Define(...)` body (back to the 10-line stub) and
  remove the permission child. Any `AbpSettings` rows the admin wrote remain
  as orphan rows; ABP ignores orphan rows with no matching definition (per
  `SettingManager` source). Consumers that read a now-undefined setting
  receive a null / default fallback and continue to run; they do not crash.
  No migration to revert. No Angular proxy regen.
- **Forward compatibility risk.** If Gesco later renames a setting
  (`CaseEvaluation.Booking.LeadTimeMinutes` -> `.LeadTimeHours`), existing
  `AbpSettings` rows with the old name become orphans. Mitigation: add a
  one-off data-seeder that copies old-name value to new-name and deletes the
  old row. ABP does not provide a built-in rename path; this is a standard
  caveat documented in the ABP support forum.
- **Operational risk.** Per-tenant settings can only be written while
  `ICurrentTenant.Id` is set (tenant-scope switch in the admin UI, or
  impersonation). Host admin must switch tenant scope in the
  `/setting-management` UI before writing a tenant override. Documented in
  ABP Settings docs; flag to Adrian for the admin runbook.
- **Deploy-sequencing risk.** Ship setting-definition PR before any downstream
  consumer PR. Consumer code will read `null` / default if it deploys first,
  which is acceptable but does not satisfy the intent of the downstream
  capability.

## Open sub-questions surfaced by research

- **Production defaults.** OLD PROD `spm.SystemParameters` row holds real
  values for the 14 fields. Adrian or the clinic should provide the row so
  the `DefaultValue` on each setting definition matches existing policy. If
  unavailable, ship with the placeholder defaults proposed above and record
  the assumption in the first migration note.
- **Unit choice (minutes vs days).** OLD stores integers whose unit is
  implied by the field name (`AppointmentLeadTime` in days,
  `ReminderCutoffTime` in minutes -- the naming is inconsistent in OLD).
  NEW normalizes to minutes everywhere for consistency and finer granularity
  (the admin UI displays `N minutes` and the consumer converts as needed).
  Confirm with Adrian before shipping; the setting names in this brief carry
  `Minutes` / `Days` suffixes to make the unit explicit and avoid OLD's
  ambiguity.
- **Timezone semantics for time-window validators.** NEW `Appointment.AppointmentDate`
  is `DateTime`. Cross-reference with `appointment-lead-time-limits` brief
  (its Open sub-questions section 3): the validator needs to know whether
  `AppointmentDate` is UTC or local. Orthogonal to this brief but must be
  resolved before the first consumer ships.
- **`IsCustomField` migration.** OLD's 14th business column is conceptually
  "is SystemParameter row a custom-field bag?" Under track-10 erratum 4, NEW
  replaces OLD's custom-field surface with ABP `ExtraProperties` + the
  `custom-fields` capability (G2-N2). `IsCustomField` therefore does not
  migrate to an ABP Setting; it is dropped. Flagged because a faithful
  "14-field port" reader might expect it.
- **Dedicated `/system-parameters` Angular screen (track 09 Q5).** If Adrian
  chooses "yes, dedicated screen," add a follow-on capability
  `system-parameters-admin-ui` in Phase 3 that mounts a simple Angular
  component at `/system-parameters` whose only job is to read/write the same
  ABP Settings via the existing proxy. Keeps the backend work in this brief
  unchanged.
- **`CcEmailAddresses` type.** Stored as CSV string to match OLD. If ABP adds
  native array settings in a future release, migrate the representation.
  Consumers parse on read via `value.Split(',')`. No cross-tenant concern
  (each tenant has its own CSV).
- **Audit stamping for policy edits.** HIPAA auditor may request "who
  changed lead-time and when." ABP AuditLogging covers setting writes via
  the SettingManagement AppService; confirm the audit log retention window
  (`AbpAuditLogs` / `AbpAuditLogActions`) meets the 6-year HIPAA requirement
  before MVP cutover. Out of scope for this brief; flag to
  `appointment-change-log-audit` capability (DB-03 / G2-13).
