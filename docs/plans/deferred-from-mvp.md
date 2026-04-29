# Deferred from MVP -- post-demo cleanup ledger

> Append-only register of work cut from Waves 0/1/2/3 to reach a demo-able MVP
> faster. Adrian returns to this list after Wave 3 ships and demo feedback lands.
> Each entry: what was cut, why, where the original decision lives, what unblocks
> picking it back up.

## From Wave 0 (locked when feat/mvp-wave-0 merged)

### Security / policy / hardening

- new-sec-05-hsts-header (XS) -- production HTTPS pinning. Source brief:
  docs/implementation-research/solutions/new-sec-05-hsts-header.md
- security-hardening (XS) -- CORS lock-down + password policy length 12.
  Source: docs/implementation-research/solutions/security-hardening.md
- new-sec-01-appointment-route-permission-guard -- Angular route guards on
  /appointments/view/:id and /appointments/add. Source:
  docs/implementation-research/solutions/new-sec-01-appointment-route-permission-guard.md
- new-sec-02-method-level-authorize -- finer-grained method-level [Authorize]
  on 3 AppServices + 4 helpers. Source:
  docs/implementation-research/solutions/new-sec-02-method-level-authorize.md
- new-sec-04-external-signup-real-defaults -- delete the
  ExternalSignupAppService class entirely (also closes SECURITY.md SEC-03).
  Source:
  docs/implementation-research/solutions/new-sec-04-external-signup-real-defaults.md

### Documentation / runbooks

- ADR-006 cascade-delete documentation. Source:
  docs/implementation-research/solutions/rest-api-parity-cleanup.md
- users-admin-management runbook. Source:
  docs/implementation-research/solutions/users-admin-management.md
- account-self-service runbook. Source:
  docs/implementation-research/solutions/account-self-service.md

### Wave 0 cap-internal carry-overs

- W0-1 data-quality fix: derived CaseEvaluationSaasTenantCreateDto carrying real
  FirstName/LastName/Gender for the onboarded Doctor + Angular tenant-create form
  widening. Doctor rows currently get LastName="" + Gender=Male placeholders.
  **CUT FROM W1-0** (2026-04-27): widening the volo SaaS Host vendor-module
  Angular page (the Adrian-side admin tenant-create form) requires verifying
  ABP's `ObjectExtensionManager.ConfigureSaas()` machinery propagates create-DTO
  extra properties through to the vendor Angular form, OR replacing the volo
  page with a custom Angular tenant-create page. Non-trivial; demo-non-blocking
  (Doctor display name shows in admin lists only, not in any demo flow). Pick
  back up post-W3 cleanup.
- W0-8 AppService delegation:
  PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync
  still uses email-only lookup. Wire to PatientManager.FindOrCreateAsync.
  **[LANDED IN W1-0]** (2026-04-27): wired in
  src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs.
  Email pre-check kept as fast-path; FindOrCreateAsync runs after IdentityUser
  resolution as 3-of-6 fuzzy-match safety net.

### W0-7 cosmetic gaps (verified 2026-04-27)

- en.json missing `Setting:CaseEvaluation.*` localization keys for the 12
  SettingDefinitions; admin UI shows raw keys until added. Cosmetic; not
  demo-blocking.
- `SystemParameters` permission group not registered in
  `CaseEvaluationPermissionDefinitionProvider.cs`. No UI consumer at W1;
  defer until a settings-admin screen is built.

### Tests / coverage

- All Wave 0 caps shipped without new tests. Aggregated under Wave 1
  new-qual-01-critical-path-test-coverage cap if Adrian keeps it in Wave 1; if
  cut from Wave 1, log it here too.

## From Wave 1

### On-the-fly W1-1 decisions (logged 2026-04-27 before execution; revisit in cleanup)

- **Send-back `FlaggedFields[]` shape: free-text `string` collection storing
  appointment-form field names (e.g. `["ClaimNumber", "EmployerAddress"]`).**
  *Why deferred:* a typed enum / form-field registry would give IDE-assisted
  refactoring + an admin-UI dropdown for the office's send-back action, but
  building the registry intersects W2's `custom-fields` cap (per-AppointmentType
  field visibility / pre-fill / disable config). Field-name strings keep W1-1
  decoupled from custom-fields scope.
  *Cleanup task:* once W2 custom-fields lands, refactor `FlaggedFields[]` to
  reference the `AppointmentTypeFieldDefinition` entity (or whatever form-field
  registry custom-fields produces); drop the freeform string list.
- **`Appointment.AppointmentStatus` setter visibility: stays PUBLIC at MVP.**
  *Why deferred:* the state-machine brief recommends narrowing to `internal`
  to force callers through `AppointmentManager`, but Mapperly target mapping,
  ABP `[ConcurrencyStamp]` reflection, and the auto-generated proxy projection
  all interact with the property. Verifying each integration is non-trivial
  (~half a day of touchwork). The convention is preserved at MVP via the
  `AppointmentManager` API surface (no AppService callers mutate status
  directly). Risk is a future contributor bypassing the manager.
  *Cleanup task:* narrow setter to `internal` (or `private set` with a
  `protected internal` mutation method); audit Mapperly targets,
  `AppointmentDto` projection, and `AppointmentManager.UpdateAsync` flow;
  add a unit test pinning the manager-only-mutation invariant.
- **Stateless graph: ALL 14 appointment transitions wired declaratively;
  only 4 endpoints exposed at W1-1.**
  *Why kept full:* cheap to wire all states in the same `BuildMachine` call;
  gives Wave 3 (`appointment-change-requests`) a clean integration point; the
  `ToDotGraph()` / `ToMermaid()` export documents the full lifecycle in code
  for review. Cancel / reschedule transitions are configured but unreachable
  until Wave 3 adds the corresponding endpoints + UI.
  *Cleanup task:* none beyond the Wave 3 endpoint additions; this is a
  positive over-investment, not a deferral. Listed here for audit only.

### Cuts logged during W1-0 execution

- **W0-1 SaaS DTO widening** (`CaseEvaluationSaasTenantCreateDto` derived from
  `SaasTenantCreateDto` + Angular volo-vendor-module form widening): cut from
  W1-0 -- see Wave 0 cap-internal carry-over note above for full rationale +
  cleanup task. Doctor row still seeds with `LastName=""` + `Gender=Male`
  placeholders; cosmetic only.

### Cuts logged during W1-2 execution

- **W1-2 inline email body strings instead of ABP TextTemplating.**
  StatusChangeEmailHandler builds HTML for the 3 transition emails inline
  (English only, hardcoded copy). Reasoning: ABP's TextTemplateManagement
  combined with Razor partial files plus localization wiring is overkill
  for 3 demo emails. Saves ~half a day at MVP.
  *Cleanup task:* port the 3 inline templates to ABP TextTemplate
  definitions when post-MVP localization or admin-editable copy is
  needed. Provider is in place at
  `src/HealthcareSupport.CaseEvaluation.Domain/Emailing/CaseEvaluationTemplateDefinitionProvider.cs`.
- **W1-2 single-recipient emails** (booker only). T11 says "all-parties
  notification on every transition" -- patient + applicant attorney +
  defense attorney + insurance carrier + claim examiner + doctor's
  office + (case-by-case) employer. At MVP, only the appointment's
  IdentityUser (the booker) receives an email per transition.
  *Cleanup task:* expand StatusChangeEmailHandler.ResolveRecipientEmailAsync
  to the full all-parties list. Depends on W2 caps shipping the
  Insurance Carrier + Claim Adjuster + Defense Attorney entities (the
  recipient sources don't exist yet).
- **W1-2 SMS path skipped entirely.** No SMS sender wired; no SMS
  templates. Per dependencies.md scope-lock 2026-04-24.
- **W1-2 initial-submission email skipped.** No email fires when an
  appointment lands at Pending on initial submit (status fires only on
  transitions, not on creation). The office sees new appointments in
  their queue immediately so no functional gap; bookers see a success
  toast on submit.
  **[LANDED IN W1-CLEANUP]** (2026-04-28): AppointmentSubmittedEto
  published from AppointmentsAppService.CreateAsync; new
  SubmissionEmailHandler dispatches the office "new request" email
  (recipient pulled from `CaseEvaluation.Notifications.OfficeEmail`
  ABP setting per tenant) and a booker "request received" confirmation
  email. Office email skipped silently if the setting is empty.

### Cuts logged during W1-1 execution

- **Wave 2 `appointment-injury-workflow` brief covers Insurance Carrier +
  Claim Adjuster** (the 4 OLD entities `AppointmentInjuryDetail` + body
  parts + `AppointmentClaimExaminer` + `AppointmentPrimaryInsurance`).
  Surfaced 2026-04-27 because `dependencies.md` cap name reads as
  "injury only" -- creates ambiguity. Brief itself is correctly scoped.
  Clarifying note added to the brief; no scope expansion required.
  *Cleanup task:* refresh `docs/implementation-research/dependencies.md`
  cap-name table during Wave 2 plan drafting to disambiguate this.
- **Wave 1 cap `attorney-defense-patient-separation` stays cut** from
  my W1 plan (deferred to Wave 2 or post-MVP). Defense Attorney is
  visible in the W1-1 send-back modal as flag-only checkboxes; the
  booker form section + entity work ships when that cap lands.
- **Backend rename Respond -> SaveAndResubmit landed in W1-1** (commit
  before frontend work, no proxy mismatch window). No cleanup task.

### W1 bugfix sprint -- 2026-04-28 (post-W1 cleanup before W2)

Adrian smoke-tested the post-W1 docker stack 2026-04-28 and reported slow loads (LCP 30.34 s), 1.77 s appointment-submit blocking on SMTP, cross-tenant patient lookup leak, missing tenant-admin path to W1-1 transition dropdown, external-user 403 on the appointment detail link, and HealthChecks UI poller log spam. Plan: `docs/plans/2026-04-28-mvp-wave-1-bugfix.md`. Branch: `fix/w1-bugfix`. Five tasks landed; deferred follow-ups below.

- **B2 SMTP retry policy switch** -- once Azure Communication Services (ACS) Email connection-string credentials land in `docker/appsettings.secrets.json` (the placeholder rows are pre-wired), revisit `SendAppointmentEmailJob`: remove the try/catch around `_emailSender.SendAsync` so failures propagate, and let Hangfire's default retry policy (10 attempts with backoff) handle transient SMTP errors. If Adrian wants email completion to gate the request itself, also flip the two handlers from `_backgroundJobManager.EnqueueAsync` back to direct `await _emailSender.SendAsync` (synchronous). Reference: `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` (header doc-comment names the exact edit points).
- **B3 same-firm-attorney access design question** -- T3 minimum-bar restricts `GetPatientLookupAsync` and `GetIdentityUserLookupAsync` to (a) appointments the current attorney booked OR (b) appointments where the attorney is named via `AppointmentApplicantAttorney`. **Open question for Adrian:** should attorneys at the same law firm see each other's caseload (broader scope), or stay strict per-attorney (current behavior)? This is a product decision, not a code question. Logged so Wave 3 F3-full audit picks it up. No same-firm entity exists today; if "yes", the design also implies a Firm entity.
- **B4 HealthChecks UI dashboard removal candidate** -- if Adrian finds he never opens `http://localhost:44327/health-ui` post-W3, drop the package: remove `services.AddHealthChecksUI(...)`, `MapHealthChecksUI`, and the `AspNetCore.HealthChecks.UI*` NuGet refs. ~3 packages out of the API image. The plain `/health-status` endpoint (used by docker-compose healthchecks) does NOT depend on the UI package and stays. Roughly 5 minutes of work to revert.
- **T1 build-config consolidation** -- the docker config now derives from production (Option B). If Angular CLI updates production defaults later (e.g., new optimization flags in CLI 21+), docker auto-inherits, but if Angular changes the production stack in a way that breaks docker-specific runtime needs (e.g., environment.docker.ts), revisit. No active task; logged so future "why is docker = production" question is answerable.
- **T3 role-name consts refactor** -- `IsApplicantAttorneyAsync()` inlines the literal `"Applicant Attorney"` per `ExternalUserRoleDataSeedContributor.cs:27`. No const declaration exists in that contributor for any of the 4 external roles (Patient, Claim Examiner, Applicant Attorney, Defense Attorney), unlike `InternalUserRoleDataSeedContributor` which uses `ItAdminRoleName` etc. *Cleanup task:* add public consts to `ExternalUserRoleDataSeedContributor` (or a central `CaseEvaluationRoleNames` static class) and replace inlined literals across the codebase. Lands naturally with the W3 F3-full audit since that work touches role-scoping helpers everywhere.
- **F3-full role-scope audit** (Wave 3) -- see `## From Wave 3` below.
- **F4-mini queue-grid Review link + read-only edit-mode gate** (Wave 2) -- see `## From Wave 2` below.
- **F4-full permission redesign** (Wave 3) -- see `## From Wave 3` below.

### Pre-existing W1-1 ledger entry (kept for completeness)

- **Tenant DbContext migration generation is broken in the repo.** Pre-existing
  before W1-1 (most recent successful `TenantMigrations/` row is dated
  2026-01-31). `dotnet ef migrations add ... --context CaseEvaluationTenantDbContext`
  fails with `The entity type 'ExtraPropertyDictionary' requires a primary key`
  because `CaseEvaluationDbContextFactoryBase` looks up a `TenantDevelopmentTime`
  connection string that does not exist in `DbMigrator/appsettings.json`. The
  W0 caps that added new entities (e.g. blob container markers, although those
  are not actual tables) did not trigger this; W1-1's `AppointmentSendBackInfo`
  is the first new tenant-eligible table since the regression.
  *Workaround for MVP:* the host DbContext has `MultiTenancySides.Both` so
  `AppointmentSendBackInfo` is created in the host DB (where docker dev runs
  everything via the `Default` connection string). The host migration
  (`20260428003045_Added_AppointmentSendBackInfo`) covers the demo path.
  *Cleanup task:* add a `TenantDevelopmentTime` entry to
  `DbMigrator/appsettings.{Development.json}` pointing at a per-tenant LocalDB
  / SQL Server name; or refactor `CaseEvaluationDbContextFactoryBase` to fall
  back to `Default` when the named connection string is missing. After fix,
  generate the back-fill `Added_AppointmentSendBackInfo` tenant migration so
  per-tenant-database deployments work. Audit other tenant-side entities
  added since 2026-01-31 for missing migrations at the same time.
  **[PARTIALLY LANDED IN W2-T0]** (2026-04-29): connection-string fall-back
  to `Default` lands in `CaseEvaluationDbContextFactoryBase.CreateDbContext`
  -- the original null-connection-string symptom is gone. `AppointmentDocument`
  DbSet + entity config also added to `CaseEvaluationTenantDbContext` to
  match the host context. **Tenant migration generation still blocked** by a
  separate ABP wiring issue: `ConfigureIdentityPro()` and similar host-only
  module configurations skip Identity table registration on
  `MultiTenancySides.Tenant`, so `HasOne<IdentityUser>()` navigation refs in
  tenant-side entity configs leave `IdentityUser`'s `ExtraPropertyDictionary`
  unmapped, which EF Core treats as an orphan keyless type. New cleanup
  entry below tracks the remaining work.

- **Tenant DbContext migration generation -- ABP Identity wiring follow-up**
  (added 2026-04-29 during W2-T0). After T0's connection-string fix landed,
  `dotnet ef migrations add ... --context CaseEvaluationTenantDbContext` still
  fails with the same `ExtraPropertyDictionary requires a primary key` error.
  Root cause confirmed via the ABP issue tracker: `IdentityUser` carries an
  `IHasExtraProperties` shape; when tenant-side `ConfigureIdentityPro()`
  skips the Identity table registration (correct for split-DB deployments),
  the navigation refs from tenant entities still pull `IdentityUser` into
  the model graph, and EF treats `ExtraPropertyDictionary` as a keyless
  orphan. Two paths forward (pick at follow-up time):
  (a) Refactor every `HasOne<IdentityUser>()` in `CaseEvaluationTenantDbContext`
  to a bare `Property(x => x.IdentityUserId)` (drop the navigation; FK
  enforcement moves to host-side migration). Verify no tenant-side EF
  query traverses the User navigation before deciding -- ~1-2h refactor.
  (b) Configure `IdentityUser` (and similar host types) as keyless / owned /
  excluded explicitly in tenant context's `OnModelCreating`. Smaller code
  change but riskier per ABP issue tracker discussion.
  *MVP impact:* none. Single-DB MVP runs on host migrations
  (`MultiTenancySides.Both`), and the Wave 2 host migrations cover all the
  new tenant-eligible entities. Tenant migration regen lands when split-DB
  deployment is actually scheduled (post-MVP). References:
  <https://github.com/abpframework/abp/issues/14498>,
  <https://abp.io/support/questions/7025>.

## From Wave 2

- **F4-mini -- queue-grid Review link** (added 2026-04-28 W1 bugfix sprint, deferred to Wave 2). Tenant admin / office staff currently cannot drill from `/appointments` (the queue grid) into `/appointments/view/:id` to use the W1-1 Approve / Reject / SendBack dropdown. Add a "Review" `<a routerLink>` item to the actions dropdown in `angular/src/app/appointments/appointment/components/appointment.component.html` (next to Edit + Delete). Permission gate: visible to anyone with `CaseEvaluation.Appointments.Default`. ~XS effort (~0.5d).
- **F4-mini -- read-only edit-mode gate on appointment-view** (added 2026-04-28 W1 bugfix sprint, deferred to Wave 2). External users currently get a fully-editable view page or a 403; should see read-only fields by default, with edit unlocked ONLY when status = `AwaitingMoreInfo` AND the field appears in `latestSendBackInfo.flaggedFields`. Tenant admin / office staff stay editable subject to existing permission gates. Pairs with F3-full / F4-full in Wave 3 but lands first as a small targeted change so the demo path improves before W3 ships. ~S effort (~1d).
- **Post-login redirect guard for role-aware landing** (added 2026-04-29 during W2-2). Today every user lands on `/` (the home component). Admins (Office Admin / Practice Admin / Supervisor / Host) would benefit from auto-routing to `/dashboard` so they hit the queue counters first; external users should keep landing on `/`. Implementation candidate: a lightweight Angular `CanActivate` guard on `/` that checks roles and `router.navigateByUrl('/dashboard')` for matching admins, otherwise lets the route render. Not demo-blocking -- admin can navigate to `/dashboard` manually -- so deferred to post-MVP UX polish. ~XS (~0.5d).
- **W2-5 admin Angular CRUD module for AppointmentTypeFieldConfig** (added 2026-04-29 during W2-5). The W2-5 deep-dive called for a dedicated Angular admin module under `appointment-type-field-configs/` (list / create / edit / delete pages, mirroring the AppointmentType admin pattern). Backend ships fully (CRUD AppService, controller, permissions, EF migration), and admins can seed configs at MVP via Swagger / curl. Custom Angular admin UI is a polish iteration -- no demo-flow blocker. Pick up when /setting-management or another admin shell needs a tile entry for it. ~S (~0.5-1d).
- **W2-5 per-field `[hidden]` HTML bindings on appointment-add form** (added 2026-04-29 during W2-5). The component framework is in place: `applyFieldConfigsForAppointmentType()` populates `hiddenFieldNames` / `readOnlyFieldNames` Sets and disables the FormControls. The HTML side requires `[hidden]="isFieldHidden('fieldName')"` on each form-row container that should respect the config. ReadOnly + DefaultValue work today via FormControl APIs; the visual-vanish for Hidden is an HTML decoration that gets wired naturally as W2-7 (defense attorney) and W2-8 (injury / claim / insurance / examiner) introduce their respective form sections. Per-field bindings on existing W1 sections can be added in a small follow-up sweep when admin demand surfaces. ~XS (~0.5d).

### W1-1 polish carry-overs deferred (added 2026-04-29 Wave 2 Phase A prep)

The Phase-A read of `appointment-view.component.ts:391-427` and
`StatusChangeEmailHandler.cs:62-189` produced a 12-item polish list (full table in
the canonical Wave 2 plan at
`C:\Users\RajeevG\.claude\plans\we-are-implementing-this-eager-reddy.md`,
section "W1-1 polish carry-overs"). Items 1-5 land inside Wave 2 (pre-T0 sweep
and W2-1). Items 6-12 are out-of-W2 scope and deferred here:

- **Approved transition email content review** (item 6). `StatusChangeEmailHandler.cs:135-141` produces an inline-HTML body with no doctor name, no clinic address, no time-zone-aware date display. Bundle the rewrite with the existing W1-2 ABP TextTemplating port (already on this ledger above).
- **Rejected transition email content review** (item 7). `StatusChangeEmailHandler.cs:142-151` body is "Reason from the office: X" with no rebooking guidance and no link to start a new request. Bundle with #6.
- **SendBack transition email content review** (item 8). `StatusChangeEmailHandler.cs:152-165` adequate but reuses generic phrasing; clinic-customizable copy needs ABP TextTemplating. Bundle with #6.
- **Tenant-customized email From-name** (item 9). `StatusChangeEmailHandler.cs` uses ABP default sender; no per-tenant clinic-name override site exists. W2-10 carries tenant context via the recipient resolver, but per-tenant From-name lookup is post-MVP.
- **TimeZone-aware email date display** (item 10). `StatusChangeEmailHandler.cs:131` uses `appointment.AppointmentDate.ToString("MMM d, yyyy h:mm tt")` rendered in server local TZ. Bundle with localization (i18n + TZ together) post-MVP.
- **`flaggedFieldLabels` raw-key fallback** (item 11). `appointment-view.component.ts:342-350` falls back to the raw key when the office flagged a field key not in `send-back-fields.ts` (e.g. after a future W2-5 custom-fields rename). Low impact; office workflow won't rename fields mid-flight. Pin if W2-5 changes the registry shape.
- **Multi-send-back UI surfacing** (item 12). `appointment-view.component.ts:411-427` only drives the banner from the latest unresolved send-back. Multiple successive send-backs (office sends back twice without booker resubmitting between) work correctly server-side but the UI doesn't surface "this is your 2nd send-back". Product decision; capture Adrian's call before adding any visual treatment.

(append as Wave 2 ships further)

## From Wave 3

- **F3-full -- comprehensive role-scope helper across ALL lookup / list endpoints** (added 2026-04-28 W1 bugfix sprint, deferred to Wave 3). T3 minimum-bar covers only `GetPatientLookupAsync` and `GetIdentityUserLookupAsync`. The full audit applies the same shape to every other lookup / list / get endpoint (`GetListAsync` for Appointments, DoctorAvailabilities, ApplicantAttorneys, AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentAccessors, plus AppointmentTypes / Locations lookups -- host-scoped reference data, but tenants may want to hide some). Introduce a single `BaseAppService.ScopeForCurrentUserAsync<T>(query)` helper, branch on the canonical role names, and replace inline filters everywhere. Pairs with F4-full so the auth model is coherent. Each per-role decision goes through the AskUserQuestion modal as a row table BEFORE code is written. HIPAA-relevant change. Effort: M (~3-5d).
- **F4-full -- move class-level `[Authorize(Permissions.X.Default)]` to method-level + row-level access predicate** (added 2026-04-28 W1 bugfix sprint, deferred to Wave 3). Class-level Default-permission gates on `ApplicantAttorneysAppService` and `AppointmentApplicantAttorneysAppService` (and similar) cause external users to 403 when the appointment-view page tries to load attorney info, even for appointments they should see. Fix: read methods (`GetAsync`, `GetListAsync`, `GetWithNavigationPropertiesAsync`) become `[Authorize]` (any signed-in user) plus a runtime per-row "can see this entity?" check that delegates to F3-full's helper. Write methods (`CreateAsync`, `UpdateAsync`, `DeleteAsync`) keep existing permission gates. Effort: M (~2-3d). Pairs with F3-full.

(append as Wave 3 ships further)

## Resumption order (filled in after Wave 3 demo)

(Adrian populates this when deciding what cleanup to tackle first)

### Candidate post-W3 cleanup -- HTTPS dev migration (Option D from 2026-04-28 W1 bugfix)

**Why deferred from W1 bugfix:** during the 2026-04-28 W1 bugfix sprint Adrian and I weighed four options for the Docker Desktop on Windows + WSL2 IPv6 port-forward bug. Symptom: Chrome's Happy Eyeballs prefers IPv6 first; `wslrelay`'s IPv6 binding for `localhost:4200` (and intermittently other ports) is wedged on the Windows host; pages stall for the OS-level TCP timeout (~21s) before falling through to IPv4. We shipped Option A as the temp fix (5-line `docker-compose.yml` edit -- bind all 5 host ports to `127.0.0.1` so only IPv4 listeners exist; Chrome's IPv6 attempt now fast-fails and falls through immediately). Option D below is the long-term proper fix that also resolves several adjacent issues.

**Scope:**

1. **Generate a localhost-trusted dev TLS cert.** Use `mkcert` (one-time dev install per machine; creates a local CA the OS trusts). Output: `localhost.pem` + `localhost-key.pem` valid for `localhost`, `127.0.0.1`, `::1`.
2. **Wire nginx for TLS.** Add `listen 443 ssl http2` + cert paths to `angular/nginx.conf`. Drop the plain-HTTP `listen 80` or keep both with redirect.
3. **Wire Kestrel for TLS.** Set `ASPNETCORE_URLS=https://+:8443` in docker-compose for AuthServer and HttpApi.Host; mount cert as a docker volume; configure Kestrel to use it.
4. **Update `dynamic-env.json`** to `https://localhost:4200` / `:44327` / `:44368`.
5. **Update OpenIddict client app registrations** (`OpenIddict__Applications__CaseEvaluation_App__RootUrl` env var in db-migrator service). Re-run migrator to update the seeded redirect URIs.
6. **Update docker-compose env vars** for the three services to use `https://` for `App__SelfUrl`, `App__CorsOrigins`, `AuthServer__Authority`. Set `AuthServer__RequireHttpsMetadata` back to `true`.
7. **Revert Option A** -- remove the `127.0.0.1:` host-port-binding prefix in `docker-compose.yml` once HTTPS is verified, OR keep it (HTTPS doesn't require it but loopback-only binding is also fine for dev).
8. **Smoke test.** Full OAuth flow, all API calls, cache-disabled reloads.

**First-, second-, third-order effects:**

- 1st: dev environment uses HTTPS end-to-end, just like production.
- 2nd: HTTP/2 multiplexes all of Chrome's parallel requests over a single TCP connection -> Happy Eyeballs only races once -> the IPv6 bug becomes invisible regardless of which port wedges. HSTS (which poisoned us 2026-04-28) works correctly because we're now using HTTPS legitimately.
- 3rd: every developer needs `mkcert` set up once. Cert renewal needed yearly (or per `mkcert` defaults). Some legacy scripts that hard-code `http://` need updating. Mixed-content / secure-cookie / SameSite=None semantics start matching production -- this catches a class of bugs that Option A masks.

**Estimate:** ~1-2 engineer-days. Lands as its own cap with its own plan in `docs/plans/`.

**Acceptance criteria:**

- `https://localhost:4200/` loads without cert warnings (after `mkcert -install`)
- Chrome DevTools shows HTTP/2 (`h2`) for all 3 origins
- OAuth flow works end-to-end
- Cache-disabled reload works reliably across 10 attempts
- Option A's `127.0.0.1:` prefix in `docker-compose.yml` can be removed without breaking page loads (proves the fix is real, not coincidental)

**Logged 2026-04-28 by:** W1 bugfix sprint. Plan: `docs/plans/2026-04-28-mvp-wave-1-bugfix.md`. Empirical evidence backing this plan: per-port IPv4/IPv6 probe matrix in that plan's verification section + Chrome MCP traces showing `ERR_CONNECTION_RESET` on cache-disabled reload before the fix.
