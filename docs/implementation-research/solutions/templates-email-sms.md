# Email + SMS notification templates

## Source gap IDs

- [DB-12](../../gap-analysis/01-database-schema.md) -- Templates entity
  (pre-fill / notification template bodies). Track 01 `Delta` line 135
  cites `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Template.cs`
  + `TemplateType.cs`; flagged needs-decision.
- [03-G13](../../gap-analysis/03-application-services-dtos.md) -- Template
  CRUD surface. Track 03 `Delta` line 149 cites
  `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\TemplateManagement\TemplatesController.cs:37-77`
  -> absent in NEW; estimated 2 days.
- [5-G12](../../gap-analysis/05-auth-authorization.md) -- NotificationTemplates
  permission group. Track 05 `Delta` line 200 cites OLD
  `module-names.const.ts:3` -> absent in NEW; Medium effort.
- [UI-15](../../gap-analysis/09-ui-screens.md) -- `/templates` email/SMS
  template admin. Track 09 `Delta` line 153 cites OLD external reach; flagged
  "absent (ABP TextTemplateManagement covers partly)"; Medium effort.
- [G-API-05](../../gap-analysis/04-rest-api-endpoints.md) -- Template REST
  surface. Included in the G-API-01..21 group of 54 OLD endpoints with no NEW
  counterpart. `/api/templates` + `PATCH /api/templates/{id}` have no ABP-native
  equivalent; ABP TextTemplateManagement exposes `/api/text-template-management/*`
  instead.
- Q7 applies, verbatim: "Template management: port from OLD or use ABP
  TextTemplateManagement?" (`docs/gap-analysis/README.md:237`).

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:42`
  -- `TextTemplateManagementDomainModule` listed in `[DependsOn]`. Module
  brings `ITextTemplateContentContributor` / `TemplateDefinitionProvider`
  plumbing, DB storage (`IAbpTextTemplateContentRepository`), and the ABP
  `TextTemplateContentAppService` into DI. Host + tenant pre-wired.
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:23`
  -- `using Volo.Abp.TextTemplateManagement;` import confirms the namespace
  binding.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260131164316_Initial.cs:446-494`
  -- Initial migration creates three tables:
  `AbpTextTemplateContents` (tenant-scoped content overrides),
  `AbpTextTemplateDefinitionRecords` (runtime-edit definition rows), and
  `AbpTextTemplateDefinitionContentRecords` (runtime-edit body rows). Indexes
  created at lines 1332-1338 (`IX_AbpTextTemplateDefinitionContentRecords_DefinitionId`
  + `IX_AbpTextTemplateDefinitionRecords_Name`).
- `angular/src/app/app.routes.ts:57-61` -- `/text-template-management` route
  lazy-loads `@volo/abp.ng.text-template-management`. That package ships
  the admin list + edit pages out of the box (Administration ->
  Text Templates).
- Grep across `src/` for `TemplateDefinitionProvider`, `ITextTemplateRenderer`,
  `ITextTemplateContentContributor` returns **zero** matches. No custom
  template definitions; no renderer call sites. The framework substrate is
  there, no business code uses it yet.
- Grep across `src/` for `IEmailSender.SendAsync` returns zero consumer
  matches (confirmed in sibling brief `email-sender-consumer`). No email
  consumer to render a template body into.
- Grep across `src/` for `ISmsSender`, `Twilio`, `Volo.Abp.Sms` returns zero
  matches. No SMS provider wired (confirmed in sibling brief
  `sms-sender-consumer`; tracks back to track-10 erratum 2 that OLD SMS is
  mostly disabled in deployment).
- No `src/HealthcareSupport.CaseEvaluation.Domain/Emailing/` folder exists
  today. A `TemplateDefinitionProvider` subclass would be the first file to
  land here (sibling brief `email-sender-consumer` recommends the same
  folder for a placeholder provider).
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`
  contains no `NotificationTemplates` permission group. ABP
  TextTemplateManagement brings its own permissions
  (`AbpTextTemplateManagement.TextTemplates.*`) which are surfaced under
  Administration -> Permissions automatically.

## Live probes

- `GET https://localhost:44327/swagger/v1/swagger.json` -- HTTP 200
  (anonymous). Payload (service-status log `probes/service-status.md`
  reports 317 paths). Searching the downloaded Swagger for `text-template`
  reveals the ABP-native Pro endpoints at
  `/api/text-template-management/text-templates`,
  `/api/text-template-management/text-templates/{name}/content`,
  `/api/text-template-management/text-templates/{name}/restore-default`.
  Proves the admin REST surface is already live, no code change needed for
  CRUD-and-override. Probe log:
  [../probes/templates-email-sms-2026-04-24T22-30-00.md](../probes/templates-email-sms-2026-04-24T22-30-00.md).
- Authenticated token-holding probe against
  `GET /api/text-template-management/text-templates` was not executed by
  this subagent (plan-mode constraint). Static evidence from the Initial
  migration + `CaseEvaluationDomainModule.cs:42` + Angular route binding
  is sufficient to prove the plumbing is live. A future session can run
  the authenticated GET to confirm the (empty) list response shape.

## OLD-version reference

- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Template.cs`
  lines 12-80: `[Table("Templates", Schema = "spm")]`. Columns:
  `TemplateId` (int identity PK), `TemplateCode` (int, enum-like discriminator),
  `TemplateTypeId` (FK to `TemplateTypes`), `Subject` (nvarchar 200),
  `Description` (nvarchar 200), `BodyEmail` (text, required),
  `BodySms` (text, required), audit columns (`CreatedById`, `CreatedDate`,
  `ModifiedById`, `ModifiedDate`), and `StatusId` (soft-delete enum).
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\TemplateType.cs`
  lines 12-38: `[Table("TemplateTypes", Schema = "spm")]`. Columns:
  `TemplateTypeId` (int identity PK), `TemplateTypeName` (nvarchar 200),
  `StatusId`. Used only as a category drop-down in the admin list.
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\TemplateManagement\TemplatesController.cs`
  lines 37-77: full CRUD -- GET paged, GET by id, POST, PUT, **PATCH**
  (JsonPatchDocument), DELETE. Domain service validation per verb via
  `Domain.Template.AddValidation`, `UpdateValidation`, `DeleteValidation`.
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\*.html`
  -- 57 template files on disk. Sampled three:
  - `CommonTemplate.html` lines 34-74 uses tokens `##CompanyLogo##`,
    `##lblHeaderTitle##`, `##name##`, `##body##`, `##lblFooterText##`,
    `##Email##`, `##Skype##`, `##ph_US##` (US phone),
    `##imageInByte##` (inline image), `##fax##`.
  - `Appointment-Request-Booked.html` lines 36-50 extends the common
    layout; body token `##body##` and `##name##` are the variable slots.
  - `Appointment-DueDate-Reminder.html` lines 48-55 substitutes
    `##AppointmentRequestConfirmationNumber##` (PHI-free ID) and
    `##DueDate##` (date string). Proves OLD already uses PHI-safe tokens
    for reminders.
- OLD template-binding mechanism is disk-load + string `.Replace("##x##", val)`
  inside `SendMail.cs` send paths -- no template DB row participates in the
  substitution. `Templates` DB table existed but is not the canonical
  template store; it appears to be for admin-editable bodies while
  `wwwroot/EmailTemplates/*.html` carries the built-in layouts. The
  admin UI at `/templates` writes to the DB table; it is unclear whether
  the DB bodies feed into `SendMail.cs`. (Open sub-question below.)
- **Track-10 errata that apply here:**
  - *Erratum 2* (`10-deep-dive-findings.md:18-29`): OLD SMS is 100% disabled
    at status transitions; only 6 of 9 scheduler jobs invoke Twilio and
    `isSMSEnable: false` is the deployment default. Means `BodySms` column
    in the OLD Template table is of vestigial value. SMS template support in
    NEW is not an MVP blocker.
  - *Erratum 3* (`10-deep-dive-findings.md:31-37`): OLD scheduler hardcodes
    `AppointmentId=1`/`UserId=1` in proc dispatches. Implies OLD jobs
    likely render the same template body regardless of the real
    appointment. Template porting can rely on stored-proc body semantics
    rather than OLD caller behaviour.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10. Angular 20. OpenIddict. Stack is fixed
  by ADR-implicit upgrade (see `README.md:71-88` for the table).
- Row-level `IMultiTenant` with auto-filter (ADR-004). ABP
  TextTemplateManagement natively stores per-tenant overrides in
  `AbpTextTemplateContents` (TenantId column); the host owns the default
  body via `TemplateDefinitionProvider`. No extra code to support
  per-tenant edit.
- ABP `TextTemplateManagementDomainModule` is already in `[DependsOn]`
  (`CaseEvaluationDomainModule.cs:42`). Removing or replacing it would
  break the current Administration UI at `/text-template-management/**`
  without delivering offsetting value. Per Chesterton's Fence, leave it.
- `ITextTemplateRenderer.RenderAsync(templateName, model, cultureName)`
  is the canonical ABP API. Consumers call it in their send paths (e.g.
  `scheduler-notifications` domain service, `appointment-documents`
  `SendDocumentEmail`, `account-self-service` flows).
- Virtual File System must hold default template bodies as embedded
  resources (`.tpl` files), pointed to by the `TemplateDefinitionProvider`
  via `.WithVirtualFilePath("/Emailing/Templates/<name>.tpl", isInlineLocalized: true)`.
  ABP reads defaults from the VFS when no DB override exists.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext
  (ADR-003), no `ng serve` (ADR-005). None of the five ADRs constrain
  this capability: the ABP module already ships controllers (no manual
  controller needed), mappers (no Mapperly partial class needed), and the
  Angular package (no new Angular code needed).
- HIPAA (NON-NEGOTIABLE): template bodies must exclude PHI. Allowed
  substitution variables: appointment confirmation number
  (PHI-free identifier), appointment date/time, location name, doctor
  name (role-level, not patient-identifying), portal URL. Disallowed:
  patient name, diagnosis, injury description, body parts, SSN, DOB.
  The OLD templates already enforce this in most cases (confirmation
  numbers + dates); the NEW port keeps the same discipline. Model
  objects passed to `ITextTemplateRenderer.RenderAsync` must therefore
  expose only PHI-safe properties -- a code-review invariant, not a
  framework one.
- `NotificationTemplates` permission (5-G12) is satisfied by ABP's own
  `AbpTextTemplateManagement.TextTemplates.*` permission group. No new
  permission needed; documenting the mapping is enough.
- Angular proxy regeneration is NOT required. The ABP NPM package
  `@volo/abp.ng.text-template-management` already carries the generated
  proxies for ABP's `/api/text-template-management/*` endpoints. No
  `abp generate-proxy` run needed.

## Research sources consulted

- [ABP -- Text Template Management module](https://abp.io/docs/latest/modules/text-template-management)
  accessed 2026-04-24. HIGH confidence. Canonical docs for the module:
  (a) define templates via `TemplateDefinitionProvider` subclasses
  (host-level), (b) override bodies per-tenant via the admin UI which
  writes to `AbpTextTemplateContents`, (c) render via
  `ITextTemplateRenderer.RenderAsync(templateName, modelObject,
  cultureName)`, (d) `.WithVirtualFilePath("/...tpl", isInlineLocalized)`
  on the definition to pin the default body to an embedded resource.
- [ABP -- Virtual File System](https://abp.io/docs/latest/framework/infrastructure/virtual-file-system)
  accessed 2026-04-24. HIGH confidence. Explains
  `Configure<AbpVirtualFileSystemOptions>` + `options.FileSets.AddEmbedded<...>()`
  needed to expose `.tpl` resources to the renderer. Embedded resources
  require `<EmbeddedResource Include="Emailing\Templates\*.tpl" />` in
  the Domain `.csproj`.
- [ABP -- TemplateDefinitionProvider (source)](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.TextTemplating/Volo/Abp/TextTemplating/TemplateDefinitionProvider.cs)
  accessed 2026-04-24. HIGH confidence. Shows `TemplateDefinitionContext`
  API, `context.Add(new TemplateDefinition(name, ...))`, and template
  chaining via `.WithLayout("CaseEvaluation:Layout")`.
- [ABP Community -- Replacing Email Templates and Sending Emails](https://abp.io/community/articles/replacing-email-templates-and-sending-emails-jkeb8zzh)
  accessed 2026-04-24. MEDIUM confidence (walkthrough). Demonstrates
  porting pattern from disk-file templates to ABP
  `TemplateDefinitionProvider` + Scriban-based body rendering. Covers
  localization keys and the VFS wiring.
- [ABP -- Scriban engine used by Text Template Management](https://github.com/scriban/scriban)
  accessed 2026-04-24. HIGH confidence. Scriban is ABP's default
  template engine; tokens use `{{ property_name }}` syntax, not OLD's
  `##Placeholder##`. Porting requires translating tokens -- documented
  below in the recommended solution.
- [ABP commercial -- Text Template Management Pro UI](https://abp.io/docs/latest/modules/text-template-management-pro)
  accessed 2026-04-24. HIGH confidence (commercial docs). Confirms the
  admin UI lives at `/text-template-management` with list + edit +
  restore-default actions. The screen UI-15 asks for is this page.
- [ABP -- Permissions / ABP TextTemplateManagement](https://github.com/abpframework/abp/blob/dev/modules/text-template-management/src/Volo.Abp.TextTemplateManagement.Domain.Shared/Volo/Abp/TextTemplateManagement/TextTemplateManagementPermissionDefinitionProvider.cs)
  accessed 2026-04-24. HIGH confidence. Defines
  `AbpTextTemplateManagement.TextTemplates`,
  `AbpTextTemplateManagement.TextTemplates.Edit`,
  `AbpTextTemplateManagement.TextTemplates.ManageDefinitions` --
  satisfies 5-G12.
- [StackOverflow -- ABP email template rendering](https://stackoverflow.com/questions/67828728/abp-sending-emails-with-templates-and-parameters)
  accessed 2026-04-24. LOW confidence (single answer, 3 upvotes).
  Reinforces the `ITextTemplateRenderer.RenderAsync(name, model)` pattern
  but is not used as primary evidence.

## Alternatives considered

1. **ABP TextTemplateManagement with a `CaseEvaluationTemplateDefinitionProvider`
   declaring the MVP templates, default bodies as embedded `.tpl` files,
   admin overrides via the existing UI.** Zero new REST code, per-tenant
   editing out of the box, 5-G12 permission satisfied by the ABP module's
   own permissions. **chosen**.
2. **Port OLD's custom `Template` + `TemplateType` entities + `TemplatesController`
   end-to-end.** Would give parity with OLD's schema and the existing
   `/api/templates` URL shape. **rejected**. (a) Reinvents what
   `TextTemplateManagement` already does, (b) undermines the admin-UI
   investment already sunk into the `@volo/abp.ng.text-template-management`
   Angular package, (c) gives no per-tenant override path without more
   code, and (d) OLD's `BodySms` column is almost unused in production
   (track-10 erratum 2).
3. **Hardcode all template bodies in C# string literals inside a render
   helper.** **rejected**. Admin cannot edit without a redeploy. Violates
   UI-15 requirement to expose an admin surface. Not tenant-aware.
4. **Azure Notification Hubs / SendGrid Dynamic Templates + external template
   store.** **rejected** for MVP. Adds an external dependency, fragments
   the template authoring story (some templates live in ABP, some in the
   external service), and its BAA coverage is out of scope for CC-01 /
   CC-02 resolution. Reopen if Gesco adopts a cloud notification platform
   later.
5. **Port disk-file `.html` templates as-is, keep `##token##` substitution
   via a thin string-replace adapter in a custom `IEmailSender`
   decorator.** **conditional / rejected for MVP**. Would save translation
   effort if Adrian insisted on bitwise-identical HTML, but loses every
   benefit of the ABP module (admin UI, per-tenant overrides, localization,
   permissions). Only viable if Gesco's branding team must keep pixel-
   identical mail -- in which case the 57 OLD `.html` files become
   embedded resources and the provider points to them verbatim, with a
   custom `ITextTemplateContentContributor` bypassing Scriban. Flagged
   post-MVP as a potential accelerator if porting time becomes a crunch.

## Recommended solution for this MVP

Use ABP Text Template Management. Declare the ~10 MVP templates
(reminder + admin notifications) in a new
`CaseEvaluationTemplateDefinitionProvider`; ship default bodies as
embedded `.tpl` files in the Domain project's virtual file system;
expose admin overrides via the existing
`/text-template-management` Angular route; render at send time via
`ITextTemplateRenderer.RenderAsync`.

Folder layout and shape:

- `src/HealthcareSupport.CaseEvaluation.Domain/Emailing/`
  - `CaseEvaluationEmailTemplates.cs` -- `public static class` with
    `Consts` template names (one `const string` per template, e.g.
    `public const string AppointmentApproved = "CaseEvaluation.AppointmentApproved";`).
  - `CaseEvaluationTemplateDefinitionProvider.cs` --
    `public class CaseEvaluationTemplateDefinitionProvider : TemplateDefinitionProvider`
    overriding `Define(ITemplateDefinitionContext context)` and calling
    `context.Add(new TemplateDefinition(...))` once per template. Layout
    chained via `.WithLayout(...)` to share the common HTML shell.
  - `Templates/` sub-folder with embedded `.tpl` files (one per template +
    one shared layout `.tpl`). Added to the csproj as
    `<EmbeddedResource Include="Emailing\Templates\**\*.tpl" />`.
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs`
  -- add `Configure<AbpVirtualFileSystemOptions>(options =>
  options.FileSets.AddEmbedded<CaseEvaluationDomainModule>())` in
  `ConfigureServices`. This exposes the embedded `.tpl` resources to the
  renderer. No new `[DependsOn]` module.

MVP template catalogue (10 templates). Names map one-to-one to OLD
scheduler-notification bodies and to the five most common
status-transition emails:

| Template name | Maps from OLD | Trigger |
|---|---|---|
| `CaseEvaluation.AppointmentApproved` | `Patient-Appointment-ApprovedInternal.html` | status -> Approved |
| `CaseEvaluation.AppointmentRejected` | `Patient-Appointment-Rejected.html` | status -> Rejected |
| `CaseEvaluation.AppointmentCheckedIn` | `Patient-Appointment-CheckedIn.html` | status -> CheckedIn |
| `CaseEvaluation.AppointmentCheckedOut` | `Patient-Appointment-CheckedOut.html` | status -> CheckedOut |
| `CaseEvaluation.AppointmentBilled` | (new -- status -> Billed) | status -> Billed |
| `CaseEvaluation.AppointmentRescheduleRequest` | `Appointment-Reschedule-Request-Booked.html` | reschedule-request flow |
| `CaseEvaluation.AppointmentCancelled` | `Patient-Appointment-CancelledNoBill.html` | cancel-request flow |
| `CaseEvaluation.JointDeclarationUploaded` | `Joint-Agreement-Letter-Uploaded.html` | joint-declaration workflow |
| `CaseEvaluation.DocumentPending` | `Upload-Pending-Documents.html` | scheduler -- document-pending |
| `CaseEvaluation.PendingAppointmentDaily` | `PendingAppointmentDailyNotification.html` | scheduler -- daily digest |

Consumers call (one-liner pattern):

```csharp
var body = await _templateRenderer.RenderAsync(
    CaseEvaluationEmailTemplates.AppointmentApproved,
    new { ConfirmationNumber = x, AppointmentDate = y, PortalUrl = z },
    CultureInfo.CurrentUICulture.Name);
await _emailSender.SendAsync(target, subject, body, isBodyHtml: true);
```

Localization keys for subject lines live in
`src/.../Domain.Shared/Localization/CaseEvaluation/en.json`. Per-tenant
overrides happen via Administration -> Text Templates in the Angular admin
UI (already mounted).

Placeholder-token translation (required):
- OLD `##ConfirmationNumber##` -> Scriban `{{ confirmation_number }}`.
- OLD `##DueDate##` -> Scriban `{{ due_date }}`.
- OLD `##name##` -> Scriban `{{ recipient_display_name }}` (PHI-safe: role
  display name, not the patient's real name).
- OLD `##body##` -> body inclusion handled via Scriban layout chaining,
  not a literal token.
- OLD branding tokens (`##CompanyLogo##`, `##lblFooterText##`, etc.) are
  deferred to BRAND-03 (post-MVP per `06-cross-cutting-backend.md:161`);
  MVP uses the default layout.

Permission mapping for 5-G12:
- `AbpTextTemplateManagement.TextTemplates` (ABP module) -- list + view.
- `AbpTextTemplateManagement.TextTemplates.Edit` -- edit DB override rows.
- `AbpTextTemplateManagement.TextTemplates.ManageDefinitions` -- manage
  definitions via the runtime API (Pro only).
- Grant these to the internal roles seeded under
  `internal-role-seeds` (admin/supervisor); external roles (Patient,
  Applicant Attorney, Defense Attorney, Claim Examiner) get none.

No migration, no new entity, no new AppService, no new manual controller,
no new Angular code. All delivered via the existing ABP module wiring
plus one new C# provider + embedded `.tpl` resources.

## Why this solution beats the alternatives

- **Zero duplicative infrastructure**: `TextTemplateManagementDomainModule`
  is already in `[DependsOn]`, migrations already created the tables, the
  Angular admin page is already routed. Building on what ships satisfies
  Chesterton's Fence and the "foundations first" rule from
  `code-standards.md`.
- **Per-tenant overrides for free**: `AbpTextTemplateContents.TenantId`
  column + the admin UI's tenant switch combine to give every clinic
  its own body bodies without new code. Directly addresses BRAND-03
  when that post-MVP work lands (one-setting-row change).
- **5-G12 satisfied by native permissions**: the ABP module carries its
  own permission group. No `CaseEvaluationPermissions.NotificationTemplates`
  needed. Fewer new concepts.
- **Renderer plug-point already standard**: downstream consumers
  (`scheduler-notifications`, `appointment-documents.SendDocumentEmail`,
  `account-self-service` flows) call the same
  `ITextTemplateRenderer.RenderAsync` + `IEmailSender.SendAsync` pair --
  one consumer pattern for every email site.

## Effort (sanity-check vs inventory estimate)

Inventory says M (rough roll-up from DB-12 "M if MVP" + 03-G13 "2 days"
+ 5-G12 "Medium" + UI-15 "Medium"). Analysis confirms **M (~2 days)**:

- ~2 hours: create `CaseEvaluationTemplateDefinitionProvider`, `Consts`
  class, folder structure.
- ~4-6 hours: author 10 `.tpl` default bodies + translate OLD
  `##token##` -> Scriban `{{ token }}`. Subject-line localization keys.
- ~1 hour: csproj `<EmbeddedResource>` line + VFS registration in
  `CaseEvaluationDomainModule.ConfigureServices`.
- ~1 hour: spot-check the admin UI at `/text-template-management`
  showing the 10 new definitions; edit one, save, verify
  `AbpTextTemplateContents` row appears.
- ~1 hour: permission grants to seeded internal roles (ties into
  `internal-role-seeds` brief).
- ~1 hour: smoke-test render via a `dotnet test` unit that resolves
  `ITextTemplateRenderer` and calls `RenderAsync` on one template with a
  synthetic model, asserts output contains the expected token
  substitutions (no PHI in the synthetic input).

Total ~2 days, matching inventory's M. No drift.

## Dependencies

- **Blocks** `scheduler-notifications` -- the 9 (or 6 post-erratum)
  recurring jobs each render a template body via `ITextTemplateRenderer`.
  Without definitions, renderer calls will throw
  `AbpException: Template not found`.
- **Blocks** `account-self-service` -- forgot-password + email
  verification flows render ABP's own
  `AbpAccount.EmailConfirmation` / `AbpAccount.PasswordReset` templates.
  Those are brought in by the Account module already, not this capability,
  but branding alignment (if BRAND-03 lands early) will want the same
  layout provider hook-in.
- **Blocks** `appointment-change-requests` -- reschedule / cancel
  notifications send via `IEmailSender` with the templates listed above.
- **Soft-blocks** `appointment-documents` -- OLD `SendDocumentEmail`
  surface expects a template body; in NEW, it renders via
  `AppointmentDocumentRejected` / `AppointmentDocumentAccepted`
  templates (names TBD by the appointment-documents brief -- add them to
  this provider then).
- **Blocked by** `email-sender-consumer` (CC-01) -- without a working
  `IEmailSender` implementation, rendered bodies have no transport.
  CC-01 is the upstream prerequisite.
- **Not blocked by** `sms-sender-consumer` (CC-02). SMS template bodies
  are a natural extension (same provider adds `.WithVirtualFilePath` to
  a separate `.sms.tpl` file) but the MVP defers SMS per track-10
  erratum 2 + Q-answer guidance. Templates for SMS are post-MVP.
- **Blocked by open question**: "Template management: port from OLD or
  use ABP TextTemplateManagement?" -- verbatim from
  `docs/gap-analysis/README.md:237`. The recommended solution above
  assumes the "use ABP TextTemplateManagement" answer. If Adrian chooses
  "port from OLD", the recommended solution collapses to alternative 2
  (port `Template` entity + `TemplatesController`) with ~5-8 days effort
  and loss of the admin UI / per-tenant override capabilities.

## Risk and rollback

- **Blast radius**: low. One new provider class, one new folder of
  embedded `.tpl` files, one csproj `<EmbeddedResource>` line, one
  `Configure<AbpVirtualFileSystemOptions>` call. No migration, no API
  shape change, no Angular change. Failure modes: (a) typo in a template
  name const -> consumer throws `AbpException: Template not found` on
  render (clear, caught in smoke test); (b) Scriban syntax error in a
  `.tpl` -> runtime `ScribanException` with line/column info (caught by
  the planned smoke-test unit); (c) embedded-resource build failure ->
  compile error in `Domain.csproj`.
- **Rollback**: delete the provider class file and the
  `Emailing/Templates/` folder, revert the
  `Configure<AbpVirtualFileSystemOptions>` addition, revert the csproj
  `<EmbeddedResource>` line. No migration to reverse (the
  `AbpTextTemplateContents` tables stay -- empty). No downtime.
- **HIPAA risk**: template model objects MUST exclude PHI. Code-review
  invariant; enforced by convention (static analyser or documented code
  review checklist). If a consumer accidentally passes a model with
  `PatientName`, rendering succeeds but the body leaks PHI by email. The
  planned smoke-test should include a "model with PHI fails review"
  assertion against the synthetic-data fixture; plus a pre-commit reminder
  to every consumer PR.
- **Tenant-override risk**: a tenant admin could author a template body
  containing disallowed fields (e.g., an attorney typing a patient name
  into the body). Mitigation: keep the tenant override restricted to
  subject / greeting / signoff; do not expose the raw body editor to
  tenants. The ABP module's UI is subject to the
  `AbpTextTemplateManagement.TextTemplates.Edit` permission; granting
  that only to internal roles (per the permission plan above) blocks
  this risk in practice.

## Open sub-questions surfaced by research

- **OLD DB `Templates` table vs disk `wwwroot\EmailTemplates\*.html` --
  which is canonical?** OLD has both; the controller writes the DB
  table but `SendMail.cs` loads from disk. Does any production send
  path actually read the DB bodies? If yes, porting must migrate
  existing DB rows (potentially with clinic-customised copy). If no,
  the disk files are authoritative and the DB table is orphaned. Flag
  to Adrian; blocks Phase-3 effort roll-up only if the answer is
  non-obvious.
- **Exact template catalogue for MVP.** 10 listed above covers the core
  status-transition emails and two scheduler jobs. If `scheduler-notifications`
  requires more (e.g., the 6-job post-erratum count each wants a
  distinct body), add them here as that brief firms up.
- **Localization culture per template.** ABP supports
  `cultureName` per `RenderAsync` call. MVP default is `en`. If
  multi-language (e.g., Spanish for worker's-comp claimants) is in
  scope, every template needs an `en.tpl` + `es.tpl` pair plus the
  provider passes culture-aware paths. Out of MVP scope for this brief;
  post-MVP capability.
- **Who runs the smoke-test unit?** Test lives in
  `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Emailing/`,
  following the existing Doctors-test pattern; inventory's
  `NEW-QUAL-01` brief will set overall test-coverage direction. Flag
  that the template smoke test is a small sibling of that work.
