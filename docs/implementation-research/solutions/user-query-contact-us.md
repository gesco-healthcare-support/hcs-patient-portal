# User-query / contact-us (send-only help request)

## Source gap IDs

- [03-G14](../gap-analysis/03-application-services-dtos.md) -- UserQuery AppService absent in NEW (track 03, line 150). Inventory effort: 1-2 days.
- [G-API-08](../gap-analysis/04-rest-api-endpoints.md) -- 5 OLD `/api/userqueries` endpoints absent in NEW (track 04, line 129). Inventory effort: Small.
- Related inventory row: [G2-N4 / G2-N8](../gap-analysis/02-domain-entities-services.md) -- UserQuery "contact us" flow (track 02, lines 207, 211). S (1 day).
- Related row (non-MVP in the master table): [03-G14](../gap-analysis/README.md) -- tagged non-MVP at line 93 of gap-analysis/README.md. Drives Q11 at line 241.

## NEW-version code read

- Repository-wide grep for `UserQuery`, `user-query`, `userquery`, `contact-us`, `ContactUs` across `W:/patient-portal/implementation-research/src/` (all 10 project folders) and `W:/patient-portal/implementation-research/angular/src/app/` returns zero matches. No entity, no manager, no AppService, no DTO, no proxy, no component, no route, no permission constant.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:3-132` enumerates 15 permission groups (Books, States, AppointmentTypes, ... AppointmentApplicantAttorneys, Dashboard). No `UserQueries` or `ContactUs` group exists. Adding one is a ~5-line change plus the matching registration in `CaseEvaluationPermissionDefinitionProvider`.
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:38` wires `AbpEmailingModule`; line 42 wires `TextTemplateManagementDomainModule`. Both are available for any send-side work but currently have zero business consumers (confirmed by the `email-sender-consumer` brief at `../solutions/email-sender-consumer.md:26-29`). A UserQuery entity, if built, would be the first consumer of `IEmailSender` (alongside CC-01 and the `account-self-service` flows).
- `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:60-62` replaces `IEmailSender` with `NullEmailSender` under `#if DEBUG`. So any UserQuery port today would silently drop the outbound email in dev; Release builds would attempt `SmtpEmailSender` and throw because no `Abp.Mailing.Smtp.*` settings are populated. This is not a blocker for the UserQuery capability itself (the entity persists fine regardless) but the "email admins on submit" side-effect is gated by CC-01.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` enumerated: no `DbSet<UserQuery>`, no `OnModelCreating` builder for it. Adding one would follow the tenant-scoped entity pattern already used by `AppointmentEmployerDetail` (see gap-analysis/01 line 141 and the ADR-003 guidance that tenant-scoped aggregates register in both `CaseEvaluationDbContext` (Both) and `CaseEvaluationTenantDbContext`).
- `angular/src/app/shared/layouts/` (LeptonX-derived layout surface; inventoried via the ABP proxy README at `angular/src/app/proxy/README.md`) does not currently expose a user-initiated "Help" or "Contact" link. The LeptonX navbar is configured via ABP route configuration; adding a link is a single-line change, not a layout rebuild. Adrian would add `UserQueriesRoutes` to `app.routes.ts` if alternative A is chosen.
- Track 10 deep-dive (`docs/gap-analysis/10-deep-dive-findings.md`) adds no UserQuery-related erratum or NEW-SEC finding. The capability is a pure OLD-vs-NEW feature gap with no hidden bug surface.

## Live probes

- `GET https://localhost:44327/swagger/v1/swagger.json` at 2026-04-24T23:30 local. **HTTP 200**. Full body pipe: `jq '.paths | keys[] | select(test("query|contact|userquer"; "i"))'` returns zero matches. Proves no `/api/app/user-queries/**`, no `/api/app/contact/**`, and no `/api/app/userquer*/**` endpoints exist on HttpApi.Host today. Same probe against `.components.schemas`: zero matches on `user.?query`, `userquery`, `contact`. No DTO has reached the proxy generator's input surface either. Full log at [../probes/user-query-contact-us-2026-04-24T23-30-00.md](../probes/user-query-contact-us-2026-04-24T23-30-00.md).
- Smoke totals: the same swagger dump reports 317 paths and 335 schemas (matches the Phase 1.5 service-status snapshot at `../probes/service-status.md:11`). None contain `query` outside of OpenIddict / LINQ-style parameters.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/UserQuery.cs:10-60` -- full OLD entity. Fields: `UserQueryId` (int PK identity, `[Table("UserQueries", Schema = "spm")]`), `UserId` (int FK -> `Users`, required, `RelationshipTableAttribue`), `CreatedById` (int, required), `CreatedDate` (DateTime, required), `ModifiedById` (int?), `ModifiedDate` (DateTime?), `Message` (string, required, MaxLength 500). No StatusId, no "read/archived" flag, no IsRead column. So OLD persists fire-and-forget messages without any admin-side state transitions.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/ExtendedModels/UserQuery.cs:10-19` -- three `[NotMapped]` helpers used only by the submit flow: `RequestConfirmationNumber` (string, user types a confirmation number like `A00042` to tie the query to an appointment), `Query` (string, unused in controller code -- dead field), `AppointmentId` (int, defaults to 0 meaning "no appointment context"). These are transport-only scaffolding for the Add flow; they never hit the DB.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/UserQuery/UserQueriesController.cs:29-76` -- 5 endpoints: GET `api/userqueries`, GET `api/userqueries/{id}`, POST, PUT, PATCH (`JsonPatchDocument<UserQuery>`), DELETE. Controller binds directly to the EF entity (no DTO layer, OLD pattern). Requires JWT (inherits `BaseController`). No `[AllowAnonymous]`.
- `P:/PatientPortalOld/PatientAppointment.Domain/UserQueryModule/UserQueryDomain.cs:36-107` -- business methods `Get`, `Get(id)`, `Add`, `Update`, `Delete`. The **only** method carrying real logic is `Add` (lines 53-106):
    1. Stamp `CreatedById = UserClaim.UserId`, `CreatedDate = DateTime.UtcNow`.
    2. Look up `vAppointment` (view) and `vInternalUserEmail` (view) to find the primary responsible user's email address when `RequestConfirmationNumber` is filled.
    3. Render an HTML email body via `GetEmailTemplateFromHTML(EmailTemplate.UserQuery, vemailSenderViewModel, "")`.
    4. Fire-and-forget SMTP send: `SendMail.SendSMTPMail(email, subject, emailBody)`. Subject format: `Patient Appointment Portal - (Patient: FirstName LastName - Claim: XXXX - ADJ: YYYY) - User query`.
    5. If no confirmation number, fall back to "blast every ItAdmin" via another view (`vInternalUserEmail` with `RoleId == (int)Roles.ItAdmin`), semicolon-separated emails.
  The `Get`, `Get(id)`, `Update`, `Delete` methods are trivially declared but have **zero Angular 7 callers** -- verified by `grep -rn user-queries.service` against `P:/PatientPortalOld/patientappointment-portal/src/app/` (only `.post()` is called).
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/user-query/user-queries/add/user-query-add.component.ts:27-100` -- the only UI. Bootstrap popup invoked via `RxPopup.show(UserQueryAddComponent)`. Two modes: free-form "Help" (no `AppointmentId`, no confirmation-number input) and "tied to appointment" (opens from `appointment-edit.component.ts:1125` and `:1475` with `isAskConfirmationNumber: true` + pre-filled `appointmentId` and `requestConfirmationNumber`). Validation: `Message` required, `RequestConfirmationNumber` required + max-length 10 when the modal is raised from the Request Confirmation flow.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/shared/top-bar/top-bar.component.html:79` -- the global entry point: `<a class="nav-link ..." (click)="onAddQuery()">Help <i class="ion ion-md-help text-primary"></i></a>`. Visible on every authenticated page via `top-bar.component.ts:57` which calls `this.popup.show(UserQueryAddComponent)`. So OLD brands the capability as "Help", not "contact us".
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/home/home.component.ts:11,home + app.module.ts:40-41` -- home page and root module wire `UserQueryAddComponent` as an entry component and popup trigger; same popup, same send-only flow.
- **No OLD admin inbox.** Directory listing `P:/PatientPortalOld/patientappointment-portal/src/app/components/user-query/user-queries/` contains `add/`, `domain/`, `user-queries.service.ts`, `user-queries-shared-component.container.ts`, `user-queries-shared-component.module.ts`. No `list/`, no `search/`, no grid component. The prompt's "OLD has admin inbox for incoming contact-us messages" claim is incorrect; the admin inbox is the SMTP mailbox of whichever internal user receives the notification. This erratum is flagged back to Adrian in the brief's Open sub-questions section.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict, Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), doctor-per-tenant row-level `IMultiTenant` (ADR-004), no-ng-serve (ADR-005). Any port must honour all 5.
- No PHI is stored by the OLD entity itself (Message is free-text user input + CreatedById FK + timestamps). But user-typed `Message` content is user-generated text and in practice will contain patient names, claim numbers, phone numbers, etc. -- treat as PHI at rest and in email transport. Not "PHI free" despite the prompt's framing. HIPAA: encrypt at rest (SQL TDE where available) and restrict admin access via permission.
- Any UserQuery port depends on a working `IEmailSender` if the "email admins on submit" behaviour is desired (`email-sender-consumer` -> CC-01). Without it, the entity persists but no admin ever sees the message until they open a list UI.
- LeptonX default layout is used; adding a "Help" / "Contact" link is configurable via ABP route menu contribution, not a template rebuild.
- Inventory currency: the last sync against NEW was 2026-04-23 (gap-analysis timestamp). Zero matches confirmed today in NEW.

## Research sources consulted

- ABP docs, Emailing module: `https://abp.io/docs/en/abp/latest/Emailing` (accessed 2026-04-24). Confirmed send-only semantics, no built-in contact-us / feedback inbox; `IEmailSender` + `QueueAsync` pattern is the intended wiring; DB-persisted inboxes are out of scope for this module.
- ABP community articles index: `https://abp.io/community/articles` (accessed 2026-04-24). Search returned zero results for "contact us" / "feedback" / "user query" pattern articles. No first-party recipe exists; any port is bespoke.
- ABP Text Template Management module: `https://abp.io/docs/commercial/latest/modules/text-template-management` (accessed 2026-04-24). Confirms the module is the storage + editor for `AbpTextTemplateContents` table; wired by default in NEW (see `CaseEvaluationDomainModule.cs:42`). Usable for the email body when CC-01 lands. Not required for the entity itself.
- ABP text templating framework doc: `https://abp.io/docs/latest/framework/infrastructure/text-templating` (accessed 2026-04-24). Confirms `ITemplateRenderer` is the core API; Razor or Scriban engines available; supports localisation and layouts.
- Repo-local erratum source: `docs/gap-analysis/10-deep-dive-findings.md` -- no UserQuery-related erratum applies. Track 10's 4 errata (PDF, SMS, scheduler, CustomField) are all unrelated.
- Repo-local OLD source: `P:/PatientPortalOld/PatientAppointment.*/...` as cited above.

## Alternatives considered

- **Alternative A: Port UserQuery end-to-end, send-only (no inbox UI).** Add `UserQuery` tenant-scoped entity + Manager + AppService + manual controller + Angular proxy + one "Help" popup in the LeptonX navbar that posts via `IEmailSender` to the ItAdmin role (matching OLD's fall-back path). No list/inbox screen (OLD has none). **Tag: conditional.** Chosen only if Q11=yes.
- **Alternative B: Drop UserQuery from MVP. Use mailto + footer / navbar link.** Hardcode or inject a `mailto:support@gesco.example` into the LeptonX navbar. Zero DB schema, zero AppService, zero permission. **Tag: chosen (default).** Chosen when Q11=no (or absent).
- **Alternative C: Port UserQuery + add admin inbox list view (go beyond OLD parity).** OLD never shipped the inbox; adding it would be new functionality, not a port. Minimum adds: paged list grid, filter by status, mark-as-read toggle, per-query detail pane, possibly email-reply button. **Tag: rejected.** Q11 is phrased as "required?" not "required with an admin inbox?" -- scope creep in the "yes" direction is unsupported. If Adrian wants the inbox, that becomes a separate post-MVP capability.
- **Alternative D: Use a third-party SaaS form (Typeform, Google Forms, ABP SaaS cross-tenant messaging).** Zero code, but hands PHI to a third party that has not signed a BAA. **Tag: rejected on HIPAA.**
- **Alternative E: Emit Application Insights / Serilog event on a synthetic "contact-us" URL instead of persisting.** Out-of-band to HIPAA-compliant storage; opaque retention. **Tag: rejected.** OLD users expect acknowledgement and a confirmation toast ("userQuerySubmitted").

Only Alternative B is viable when Q11=no. Only Alternative A is viable when Q11=yes. C, D, E are rejected in both branches.

## Recommended solution for this MVP

**Default (Q11=no, contingent on Adrian's answer):** Drop UserQuery from MVP. Add a single `<a href="mailto:support@gesco.example?subject=Patient%20Portal%20Help%20Request">Help</a>` link in the Angular LeptonX top-nav, mirroring OLD's "Help" entry point location but routing to email instead of an in-app popup. Target file: the menu contribution TS module at `angular/src/app/shared/layouts/navbar-user-menu/*` (exact file to be identified when BRAND-02 lands; for now, inject into `app-menu.service.ts` or the route-config menu slot). Zero backend work, zero migrations, zero permissions. HIPAA risk is lower than an in-app form because the email client owned by the user carries its own transport security.

**If Q11=yes:** Port OLD's send-only flow only (not alternative C's inbox). WHAT: `UserQuery` tenant-scoped aggregate root with `Id (Guid)`, `TenantId (Guid?)`, `Message (string, max 500)`, `AppointmentId (Guid?)`, `RequestConfirmationNumber (string?, max 10)`, plus ABP audit columns from `FullAuditedAggregateRoot<Guid>`. WHERE: `src/HealthcareSupport.CaseEvaluation.Domain/UserQueries/` for entity + manager + repo interface; `src/HealthcareSupport.CaseEvaluation.Application/UserQueries/` for AppService and Mapperly partial; `src/HealthcareSupport.CaseEvaluation.Application.Contracts/UserQueries/` for DTOs (`UserQueryCreateDto`, `UserQueryDto`, `GetUserQueriesInput`); `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/UserQueriesController.cs` (manual controller, delegates). WHICH ABP primitive: `FullAuditedAggregateRoot<Guid>` + `IMultiTenant` + `IRepository<UserQuery, Guid>` + `IEmailSender.SendAsync` (conditioned on CC-01 resolution). Permission group `UserQueries.Default` for submit (authenticated users); no inbox permission (no inbox UI). Migration: EF adds `UserQueries` table in the tenant DbContext. Shape: entity -> Domain -> AppService -> Controller -> proxy regen (`abp generate-proxy`) -> Angular service (auto) -> one-popup component at `angular/src/app/user-queries/add/` modelled on `anonymous-document-upload` UX but behind `authGuard`.

## Why this solution beats the alternatives

- **B over A (default):** zero code, zero DB, zero PHI-at-rest in NEW, zero migration to manage. Adrian has flagged UserQuery as non-MVP in the master table at gap-analysis/README.md line 93; every deferred gap that can be served by a `mailto:` link is a deferred gap with no portability debt.
- **B over C/D/E:** `mailto:` keeps PHI inside the user's email client (BAA-covered by Gesco's mail provider), no third-party trust boundary, no DB write path for user-pasted PHI.
- **A over C (if Q11=yes):** OLD itself did not ship the admin inbox. Adding it would be new work the inventory never counted. A respects the "port, do not extend" constraint.
- **A over D/E (if Q11=yes):** Same HIPAA and UX arguments as B; add BAA-coverage by persisting PHI in the same HIPAA-compliant SQL Server NEW already uses for Appointments.

## Effort (sanity-check vs inventory estimate)

Inventory says 1 to 2 days (`03-G14` row at track 03 line 150). Analysis adjusts to:

- **Alternative B (default):** XS (~0.5 day, or effectively a footer/navbar link when BRAND-02 lands). Inventory over-counts because it assumed a port.
- **Alternative A (if Q11=yes):** S (~0.5 to 1 day). OLD's admin inbox was never built; the port is just entity + CRUD AppService + controller + a copy-paste variant of the anonymous-document-upload component but simpler (one textarea). Mapperly `partial class UserQueryMapper : MapperBase<UserQuery, UserQueryDto>` is ~10 lines; permission provider is ~5 lines; migration is auto-generated; manual controller is ~40 lines from the `AppointmentEmployerDetailController.cs` template.

Dev-data rationale: one entity + one AppService + one popup + one permission + one migration = half-day to full-day. The inventory's 1-2 day estimate baked in port time for the admin inbox that doesn't exist.

## Dependencies

- **Blocks:** None. UserQuery is terminal in the dependency graph: nothing else uses or references it.
- **Blocked by (hard):** None. The entity can be built and tested in isolation.
- **Blocked by (soft) if Q11=yes:** [email-sender-consumer](./email-sender-consumer.md) (CC-01) -- the "email admins on submit" side-effect is a no-op until `IEmailSender` is wired. UserQuery persists fine without CC-01; the email notification is gated. Acceptable MVP posture: ship the entity, leave the `IEmailSender.SendAsync` call in place, let it no-op under `NullEmailSender` in Debug and throw in Release until CC-01 lands. CC-01 is already Wave 1 per the gap-analysis sequencing suggestion (README.md line 370+).
- **Blocked by (soft) if Q11=no:** [BRAND-02](../../gap-analysis/README.md#post-mvp-deferred-gaps) -- the LeptonX navbar footer/slot is a post-MVP gap. Without BRAND-02, the mailto link must be injected into whatever menu surface is present today (`app-routing.module.ts` or `app-menu.service.ts`). A 1-hour hack today, cleaner when BRAND-02 lands.
- **Blocked by open question:** verbatim from `docs/gap-analysis/README.md:241`: `"11. **UserQuery / contact-us**: required? (Tracks 2, 3, 4, 9)"`.

## Risk and rollback

- **Blast radius (Alternative B, default):** zero. Nothing in NEW references UserQuery; a mailto link is additive.
- **Blast radius (Alternative A, if Q11=yes):** single tenant-scoped aggregate, single popup, single permission. Misconfigured `IEmailSender` silently drops the notification but does not prevent the entity write. Worst case: users submit queries that admins never see in the email inbox (same failure mode as OLD without SMTP config).
- **Rollback (Alternative B):** remove the one-line menu entry, redeploy Angular bundle. No DB change.
- **Rollback (Alternative A):** generate EF migration `Remove_UserQueries`, run `dotnet ef database update`, remove the feature folder. Angular: delete the component + proxy folder and regenerate. Branch revert: single PR squash-revert.

## Open sub-questions surfaced by research

1. **Correct the prompt framing**: OLD does not have an admin inbox for contact-us messages. It is send-only with SMTP delivery to either the appointment's primary responsible user (when a confirmation number is supplied) or to every `ItAdmin`-role user (fall-back blast). Adrian should confirm whether MVP is strictly an OLD-parity port (no inbox) or whether a list UI is desired separately (which becomes a new capability, not a port).
2. **If Q11=yes**, is the subject-line formula `Patient Appointment Portal - (Patient: FirstName LastName - Claim: XXXX - ADJ: YYYY) - User query` still desired, or should the subject omit patient name to avoid PHI leakage via email subject-line logs? OLD leaks patient name in SMTP subject; that pattern should not be replicated uncritically.
3. **If Q11=no**, where exactly should the mailto link live before BRAND-02 lands? Candidates: `app-menu.service.ts`, hardcoded in `app.component.html`, or a new `HelpMenuContributor` that the LeptonX module can register.
4. **Email destination** for the mailto link: `support@gesco.example`? `help@gesco.com`? Adrian's real mailbox? This determines whether the mailto is placeholder or production-ready.
