# Dashboard counter cards (per-role)

## Source gap IDs

- [03-G08 -- Dashboard counters service](../../gap-analysis/03-application-services-dtos.md)
- [A8-11 -- Angular Dashboard client service absent](../../gap-analysis/08-angular-proxy-services-models.md)
- [G-API-14 -- Dashboard counters endpoint (1 endpoint) Small-Medium](../../gap-analysis/04-rest-api-endpoints.md)
- Open question: [Q13 from gap-analysis/README.md:243](../../gap-analysis/README.md) -- "Dashboard counters: which of OLD's 13 cards are needed, per role? (Tracks 2, 3, 4, 9)".

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:7-12` -- nested `Dashboard` class already defines `DashboardGroup`, `Host`, and `Tenant` string constants. Unique in that it has no `.Default / .Create / .Edit / .Delete` child set; Host and Tenant are sibling leaf permissions registered directly under the group, per the `CaseEvaluationPermissionDefinitionProvider`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs:13-14` -- registers `Dashboard.Host` with `MultiTenancySides.Host` and `Dashboard.Tenant` with `MultiTenancySides.Tenant`. These already appear in the admin Permission Management UI. No new permission is required for this capability.
- No `DashboardAppService.cs` or `IDashboardAppService.cs` exists anywhere in `src/HealthcareSupport.CaseEvaluation.Application/` or `Application.Contracts/`. Confirmed via `Glob "**/*Dashboard*.cs"`: zero matches in the Application layer.
- No dashboard controller exists in `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/`. Live Swagger probe confirms 317 paths with zero matching `dashboard` or `counter` (see Live probes below).
- `angular/src/app/dashboard/dashboard.component.ts:1-14` -- root standalone component. Splits into `<app-host-dashboard *abpPermission="'CaseEvaluation.Dashboard.Host'">` and `<app-tenant-dashboard *abpPermission="'CaseEvaluation.Dashboard.Tenant'">`. No proxy service import, no signal, no call to any `/api/app/dashboard` endpoint. Renders at `/dashboard` per `angular/src/app/app.routes.ts:23-28`, guarded by `authGuard + permissionGuard`.
- `angular/src/app/dashboard/host-dashboard/host-dashboard.component.ts:1-86` -- full ABP-supplied shell: four widgets from `@volo/abp.ng.audit-logging` and `@volo/abp.ng.saas` (`ErrorRate`, `AverageExecutionDuration`, `EditionsUsage`, `LatestTenants`). Entirely about platform admin, not about appointment counts. No CaseEvaluation-specific counter cards yet.
- `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.ts:1-8` -- empty stub. Template `tenant-dashboard.component.html:1-7` prints "Add your Tenant related charts/widgets to this page !" -- literally ABP's "fill-me-in" placeholder. This is the widget-host surface for MVP tenant counters.
- `angular/src/app/proxy/` -- auto-generated proxy directory exists, but no `dashboard` subfolder because there's no `IDashboardAppService` to proxy. Regeneration will only emit a client after the AppService is added on the backend.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs:3-18` -- 13-state enum. Provides the authoritative status vocabulary for any count-by-status card. Values: Pending=1, Approved=2, Rejected=3, NoShow=4, CancelledNoBill=5, CancelledLate=6, RescheduledNoBill=7, RescheduledLate=8, CheckedIn=9, CheckedOut=10, Billed=11, RescheduleRequested=12, CancellationRequested=13.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:162-205` plus root CLAUDE.md "ABP Conventions" section -- canonical pattern for new AppServices in this repo: extend `CaseEvaluationAppService`, `[RemoteService(IsEnabled = false)]`, inject `IRepository<T, Guid>`, call `GetQueryableAsync()` + `AsyncExecuter.CountAsync`. The dashboard service follows this same pattern with no entity repository of its own.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:19-72` -- `Appointment` implements `IMultiTenant`, so counts for tenant users are auto-filtered by the ABP tenant filter. For `Dashboard.Host` users viewing cross-tenant totals, the service must wrap the host branch in `using (_dataFilter.Disable<IMultiTenant>())` per the pattern already in `DoctorsAppService` (called out in root `CLAUDE.md` under "Multi-tenancy Rules").
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/MultiTenancy/MultiTenancyConsts.cs:9` -- `IsEnabled = true`, so host vs tenant branching is live in this deployment.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs` -- Patient has a `TenantId` property but does NOT implement `IMultiTenant` (per root `CLAUDE.md` "Multi-tenancy Rules"). A "Patients" counter card on a tenant dashboard must filter manually by `_currentTenant.Id`; the automatic data filter won't apply.

## Live probes

- Swagger scan for any dashboard endpoint: `GET https://localhost:44327/swagger/v1/swagger.json` with client-side filter on `dashboard` or `counter` substring -- confirms `Total paths: 317, Dashboard paths: []`. See [../probes/dashboard-counters-2026-04-24T23-15-00.md](../probes/dashboard-counters-2026-04-24T23-15-00.md).
- Permission Management read for admin: already captured in [../probes/internal-role-seeds-2026-04-24T1255.md](../probes/internal-role-seeds-2026-04-24T1255.md) -- 2 `CaseEvaluation.Dashboard.*` permissions visible in the grant matrix, confirming the host/tenant split is active without new registration work.
- Empty-state read against `/api/app/appointments`: already captured in [../probes/service-status.md](../probes/service-status.md) -- returns `{"totalCount":0,"items":[]}`, proving the dashboard backend will initially render zero-counts until seed data lands. No PHI at risk.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Core/DashboardController.cs:45-54` -- a single `POST /api/Dashboard/post` action. Accepts `DashboardModel { UserTypeId }`, calls `EXEC spm.spDashboardCounters @UserTypeId`, returns the raw JSON string from `StoreProcSearchViewModel.Result`. No per-field typed contract on the wire.
- `P:/PatientPortalOld/PatientAppointment.Models/Models/DashboardModel.cs:9-12` -- the input DTO is one `int UserTypeId`. The role bitmask drives which card set the stored proc emits.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/AllowedApis.cs:14-27` (referenced from track 04) -- OLD's `DashboardController` is in the `AuthenticationByPass` allowlist, so `POST /api/Dashboard/post` was callable anonymously. Per gap-analysis Q25 ("Anonymous endpoints in OLD ... intentional or legacy bug?"), this is flagged as a security smell to NOT replicate. NEW MUST gate the endpoint by `[Authorize(CaseEvaluationPermissions.Dashboard.Host)]` or `.Tenant` class-level attribute.
- `P:/PatientPortalOld/_local/stub-procs.sql:293-298` -- the `spm.spDashboardCounters` body present in the local bring-up is a stub that always returns an empty `[]`. The authoritative per-role card matrix lives only on PROD SQL; the body is not recoverable from the repo. Per gap-analysis errata and local-bring-up evidence, any card set we ship is a forward-looking MVP decision, not a strict port.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/dashboard/dashboard.service.ts:1-20` (referenced at `08-angular-proxy-services-models.md:34, 144, 211`) -- OLD client-side service wrapping `api/Dashboard/post`. Track 09 at `09-ui-screens.md:47` documents the observed 13 cards on the admin landing page: Pending Appointment, Approved Appointment, Rejected Appointment, Cancelled Appointment, Rescheduled Appointment, Checked-In Appointment, Checked-Out Appointment, Billed Appointment, Patient, Claim Examiner, Applicant Attorney, Defense Attorney (12 named in the screenshot caption; track 09's "13 stat cards" wording suggests the 13th is either a duplicate "Cancelled Late" variant OR "Total Appointments" -- the screenshots at `screenshots/old/admin/01-dashboard.png` are the source of truth; the on-disk captions list 12 distinct labels).
- `09-ui-screens.md:52, 56, 63` -- ItAdmin, StaffSupervisor, and ClinicStaff all land on `/dashboard` with an "identical card layout." In OLD, per-role variance at the dashboard level is minimal; the variance is in other screens. External roles (Patient, Adjuster, PatientAttorney, DefenseAttorney) land on `/home` instead of `/dashboard` (see `09-ui-screens.md:154`, `UI-16`, mapped in the `external-user-home` brief).
- Track-10 errata applied: OLD's stored procs aren't locally recoverable; derivation of per-role cards must therefore come from Adrian's choice at Q13, not from the proc body. This supersedes the initial "port the 13 cards" framing in the track 03/04/09 inventories.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. The permission model is already coded for Host + Tenant split; no new permission work needed.
- Row-level `IMultiTenant` (ADR-004): tenant users auto-scoped on Appointment/Doctor/DoctorAvailability/ApplicantAttorney/AppointmentAccessor/AppointmentApplicantAttorney/AppointmentEmployerDetail counts. Host users require `IDataFilter.Disable<IMultiTenant>()` for cross-tenant aggregates. Patient is excluded from the auto-filter; if the "Patients" card ships, apply manual `TenantId == _currentTenant.Id` in tenant branch.
- [ADR-001](../../decisions/001-mapperly-over-automapper.md) -- the `DashboardCountersDto` is hand-authored; no projection from an aggregate is needed, so no Mapperly mapper required.
- [ADR-002](../../decisions/002-manual-controllers-not-auto.md) -- a manual `DashboardController` extends `AbpController, IDashboardAppService` and delegates. `IDashboardAppService` carries `[RemoteService(IsEnabled = false)]` (class attribute on the implementation) per the reference pattern.
- [ADR-003](../../decisions/003-dual-dbcontext-host-tenant.md) -- the service reads via `IRepository<T, Guid>` from `CaseEvaluationDbContext`. No DbContext touching required; repositories already resolved.
- [ADR-005](../../decisions/005-no-ng-serve-vite-workaround.md) -- Angular refresh after backend change is `ng build --configuration development` + `npx serve`.
- HIPAA: counts are aggregate numbers, not PHI. But the count SET (e.g., "Pending Patients = 0 in Tenant A") could leak tenant-capacity signal if cached or logged across tenants; Serilog templates must NOT include the tenant ID alongside the count without consent. Aggregate counts of PHI records are not themselves PHI under the Safe Harbor rule (HIPAA 45 CFR 164.514(b)(2)(i)) provided no identifier is attached.
- Capability-specific:
  - Start with a conservative subset of the OLD 13 cards; Adrian chooses which per role at Q13 resolution. The brief proposes a default subset and flags where expansion lives.
  - Do NOT introduce an anonymous endpoint, per Q25. Every dashboard call requires either `.Host` or `.Tenant` permission.
  - Re-use `*abpPermission` in Angular; do not write a new guard. The host/tenant split is already templated in `dashboard.component.ts`.

## Research sources consulted

All accessed 2026-04-24. Confidence labelled per source.

1. ABP Repository docs (`https://abp.io/docs/latest/framework/architecture/domain-driven-design/repositories`). HIGH. Confirms `CountAsync(predicate)` is the direct idiomatic API and `GetQueryableAsync` + `AsyncExecuter.CountAsync` is the composable alternative. Also confirms `IDataFilter.Disable<IMultiTenant>()` as the cross-tenant host-aggregate pattern.
2. ABP Angular Permission Management docs (`https://abp.io/docs/latest/framework/ui/angular/permission-management`). HIGH. Confirms `*abpPermission="'...'"` structural directive, combined with `*ngIf` and `CurrentTenant` info, as the canonical Angular pattern for host-vs-tenant widget splits.
3. ABP Dashboard sample (`https://abp.io/samples/dashboard`) + LeptonX dashboard screenshot reference. MEDIUM (sample page rate-limits; the in-repo `HostDashboardComponent` built from the same widget classes is the higher-confidence local example).
4. HIPAA 45 CFR 164.514(b)(2)(i) Safe Harbor rule. HIGH. Aggregate counts with no linked identifier are not PHI.
5. Repo evidence: `dashboard/dashboard.component.ts`, `host-dashboard.component.ts`, `tenant-dashboard.component.ts`, `app.routes.ts:23-28`, `CaseEvaluationPermissions.cs:7-12`, `CaseEvaluationPermissionDefinitionProvider.cs:13-14`, `DoctorsAppService.cs` (for `IDataFilter.Disable` pattern), root `CLAUDE.md` (Multi-tenancy Rules). HIGH.
6. OLD evidence: `DashboardController.cs:45-54`, `DashboardModel.cs:9-12`, `stub-procs.sql:293-298`, `08-angular-proxy-services-models.md:34,144,211`, `09-ui-screens.md:47,52,56,63,154,162`. HIGH for citations; LOW for the authoritative card matrix (stored-proc body stubbed locally).

## Alternatives considered

1. **A1. Dedicated `DashboardAppService` with `GetAsync()` returning a single `DashboardCountersDto` -- chosen.** One endpoint per role context; server branches on the caller's role + tenant. Mirrors the OLD shape (single call) while typing the contract. Matches ABP convention; one manual controller route; one Angular proxy service method. Small surface.
2. **A2. Multiple per-entity count endpoints (client stitches).** Add a `GET /api/app/appointments/count?status=Pending` variant per card. Client fires N parallel requests. Tagged: rejected. Chatty (13 HTTP requests per page render); no atomicity (counts across requests can shear); duplicates permission gating across endpoints; harder to cache; works poorly with Angular signals for a single refresh button.
3. **A3. Server-side materialised view refreshed by a scheduled job -- rejected for MVP.** A `DashboardCountersCache` table updated on a 5-minute tick by an `IAsyncBackgroundJob`. Smoothes load on large tenants. Rejected: over-engineers an MVP with zero current appointments (`/api/app/appointments` returns 0 rows); premature optimization; adds a refresh job to scope without demand; revisit post-MVP if tenant scale warrants.
4. **A4. Use ABP's generic `IDashboardService` from `Volo.Abp.Ui.Navigation` -- rejected.** There is no such first-party service; ABP LeptonX supplies per-module widgets (SaaS, Audit Logging, Identity) but no counter-card primitive. The project's `HostDashboardComponent` already consumes those widgets for platform stats. Business-domain counters require our own service.
5. **A5. GraphQL endpoint for dashboard counters -- rejected.** ABP Commercial 10.0.2 has no first-class GraphQL support; would introduce HotChocolate or equivalent as a new platform concern, which exceeds MVP scope and contradicts the "stay on ABP's paved path" theme across the 5 ADRs.
6. **A6. Single generic `/api/app/metrics` returning a flat dictionary of counts -- rejected.** Untyped contract on the wire duplicates OLD's JSON-string antipattern that ADR-001 (Mapperly) + ABP's typed DTOs explicitly replace. Weak signal for Swagger + proxy generator.

## Recommended solution for this MVP

Create `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Dashboards/`:

1. `DashboardCountersDto.cs` -- read model. Properties: `int TotalAppointments`, `int PendingAppointments`, `int ApprovedAppointments`, `int RejectedAppointments`, `int CancelledAppointments` (sum of 5+6), `int RescheduledAppointments` (sum of 7+8), `int CheckedInAppointments`, `int CheckedOutAppointments`, `int BilledAppointments`, `int TotalPatients`, `int TotalApplicantAttorneys`, `int TotalDoctors` (Host only), `int TotalTenants` (Host only). 13 fields; sub-set by role at the response layer.
2. `IDashboardAppService.cs` -- interface with one method: `Task<DashboardCountersDto> GetAsync()`. Class-level `[Authorize]` (any authenticated caller); method branches on permissions internally.

Create `src/HealthcareSupport.CaseEvaluation.Application/Dashboards/DashboardAppService.cs`:

1. Class attributes: `[RemoteService(IsEnabled = false)]`, `[Authorize]` (any authenticated; narrower permission checks inside).
2. Extends `CaseEvaluationAppService`. Implements `IDashboardAppService`.
3. Constructor injects: `IRepository<Appointment, Guid>`, `IRepository<Patient, Guid>`, `IRepository<ApplicantAttorney, Guid>`, `IRepository<Doctor, Guid>`, `IDataFilter`, `ICurrentTenant`, `IAuthorizationService`.
4. `GetAsync()` branches on `IAuthorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Host)`:
   - Host branch: `using (_dataFilter.Disable<IMultiTenant>())` wraps the counts. Compute cross-tenant totals: Total + Pending + Approved + Rejected + Cancelled + Rescheduled + CheckedIn + CheckedOut + Billed appointments; TotalPatients (all); TotalApplicantAttorneys (all); TotalDoctors (all); TotalTenants (inject `IRepository<Tenant>` from `Volo.Abp.TenantManagement` -- or fallback to `ICurrentTenant` + `_tenantRepository.CountAsync`).
   - Tenant branch: guarded by `CaseEvaluationPermissions.Dashboard.Tenant` grant check. Scoped counts (ABP auto-filter applies for `Appointment`, `ApplicantAttorney`, `Doctor`); for `Patient`, filter manually via `p => p.TenantId == _currentTenant.Id`. Omit `TotalTenants` (returns 0).
   - Neither: throw `AbpAuthorizationException` (the class-level `[Authorize]` should already catch the unauthenticated case; the neither-permission authenticated case falls through to this exception).
5. Counts use `await _appointmentRepo.CountAsync(a => a.AppointmentStatus == AppointmentStatusType.Pending)` style -- direct overload.

Create `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Dashboards/DashboardController.cs`:

1. `[Route("api/app/dashboard")]`, `[RemoteService(Name = "CaseEvaluation")]`, extends `AbpController, IDashboardAppService`.
2. `[HttpGet]` delegates to `_dashboardAppService.GetAsync()`.

Proxy + Angular:

1. Run `abp generate-proxy -t ng` after the backend compiles. Emits `angular/src/app/proxy/dashboards/dashboard.service.ts` with `getAsync(): Observable<DashboardCountersDto>`.
2. Update `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.ts` to inject the proxy and use an Angular signal (`counters = signal<DashboardCountersDto | null>(null)`), populate via `ngOnInit() { this.dashboard.getAsync().subscribe(c => this.counters.set(c)); }`. Render a Bootstrap card grid in `.html` using `@if (counters(); as c) { ... }` syntax over the DTO's fields.
3. Update `host-dashboard.component.ts` + `.html` to render the same card grid below the existing ABP platform widgets (Error Rate / Average Execution Duration / Editions Usage / Latest Tenants). Re-use the same proxy service.
4. No new Angular route; no new permission guard. The existing `*abpPermission` branching in `dashboard.component.ts` is sufficient.

Migrations:

- None. Counts read existing data only. No new schema.

Localization:

- Append card-label keys to `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`: `"Dashboard:TotalAppointments"`, `"Dashboard:PendingAppointments"`, etc. Use `| abpLocalization` pipe in templates.

Per-role card subset (initial MVP proposal, Adrian confirms at Q13):

- **Host (ItAdmin / Q21=1 admin):** all 13 cards.
- **StaffSupervisor (Q21=3):** 9 appointment-state cards + TotalPatients + TotalApplicantAttorneys. Omit TotalDoctors + TotalTenants.
- **ClinicStaff (Q21=3):** 4 cards -- Pending + Approved + CheckedIn + CheckedOut appointments (the "what's happening today" view). Omit the rest.
- **External users (Patient / ApplicantAttorney / DefenseAttorney / ClaimExaminer):** no dashboard cards in MVP; they land at `/home` instead (`external-user-home` brief handles this). Adrian may opt to re-route Patient/Attorney dashboards through a lightweight card later, but that's post-MVP.

Reference implementation to trace: `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs` (for `IDataFilter.Disable<IMultiTenant>` pattern, cross-tenant read from Host context), and `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:162-205` (for the `GetQueryableAsync` + `AsyncExecuter.CountAsync` pattern in a real AppService).

No code block in this brief exceeds 20 lines. Every shape is referenced to an existing file.

## Why this solution beats the alternatives

- One endpoint = one HTTP round trip per dashboard render. A2's 13 parallel requests waste bandwidth, create shear between counts, and duplicate permission gating 13 times; A1 collapses to a single `[Authorize]` attribute + internal role branch.
- Types on the wire. A6's untyped dictionary reintroduces OLD's JSON-string antipattern (which ADR-001 + ABP's Angular proxy generator explicitly replace). Swagger and the auto-generated proxy both benefit from a named DTO -- `DashboardCountersDto` shows every field with `#/components/schemas/...` in Swagger, and Angular gets compile-time property names.
- Honours the ABP idioms -- repository-per-entity, `CountAsync(predicate)`, `IDataFilter.Disable<IMultiTenant>()`, class-level `[RemoteService(IsEnabled = false)]` per ADR-002, manual controller route per ADR-002, Mapperly N/A because no projection is required. No new architectural concepts.
- Zero new permissions. `Dashboard.Host` and `Dashboard.Tenant` already exist and are already registered; the admin UI already shows them. The migration cost for the permission surface is zero -- Adrian's Q22 answer on the internal-role matrix also applies here by simply granting these two existing permission strings.

## Effort (sanity-check vs inventory estimate)

- Inventory says `1-2 days` ([03-application-services-dtos.md:144](../../gap-analysis/03-application-services-dtos.md)) and `Small-Medium` ([04-rest-api-endpoints.md:135](../../gap-analysis/04-rest-api-endpoints.md)).
- Analysis confirms the baseline at S (~1 day): DTO (30 min), AppService with the 13 count queries + host/tenant branch (3 hrs), controller (30 min), `abp generate-proxy` + Angular tenant-dashboard binding (2 hrs), host-dashboard integration (1 hr), localization strings (15 min), smoke test (30 min).
- Rises to M (~2-3 days) under Q13 = "yes, full per-role matrix": each internal role gets its own subset, requires 3 subset-selection conversations with Adrian, adds `DashboardCountersDto` role-variant unit tests, and requires admin + StaffSupervisor + ClinicStaff token probes.
- Test coverage (`NEW-QUAL-01` scope) adds another half day if we want a unit test per role path using `AbpAuthorizationTestBase` + seed roles.

## Dependencies

- Blocks:
  - [external-user-home](external-user-home.md) (UI-16) -- this brief's "no dashboard cards in MVP for external roles" decision keys directly into external-user-home's role-based landing-page routing. If Adrian decides external roles DO get a lightweight counter view, `external-user-home` consumes part of this brief's DTO.
  - None else strictly. Dashboard cards are a downstream read; nothing else depends on counters existing first.
- Blocked by:
  - [internal-role-seeds](internal-role-seeds.md) (DB-16 / 5-G01..5-G04) -- to TEST StaffSupervisor vs ClinicStaff visibility of different card subsets, those roles must be seeded. If Q21=1 (admin only), dashboard cards collapse to "admin sees all; every other authenticated user sees none"; no role-seed dependency.
  - [appointment-state-machine](appointment-state-machine.md) (G2-01) -- counts-by-status become trustworthy only once transitions are enforced. Today NEW allows any caller to POST an appointment with `AppointmentStatus = Billed`, so the Billed counter is artificially settable. The dashboard can ship before G2-01 ("counts reflect current state, whatever that is"), but the user signal value is low until state machine lands.
  - [lookup-data-seeds](lookup-data-seeds.md) (DB-15) -- not a hard block, but walkable UI for testing the cards end-to-end requires some seed rows; the dashboard itself compiles and runs against zero rows.
- Blocked by open question: **verbatim Q13 from [gap-analysis/README.md:243](../../gap-analysis/README.md) -- "Dashboard counters: which of OLD's 13 cards are needed, per role? (Tracks 2, 3, 4, 9)".** Brief proposes a default subset (see Recommended solution) pending Adrian's choice. If Q13 resolves to "drop dashboard counters from MVP entirely", this capability closes as "deferred" with zero work on branch.

## Risk and rollback

- Blast radius: low. New AppService + new controller + new DTO; zero touches to existing entities, migrations, permissions, or multi-tenant filters. Worst case: a bug in the Host branch returns wrong cross-tenant totals (display-only; no PHI leak because aggregate counts are not PHI). Tenant branch relies on the existing ABP auto-filter; a bug there would at most show zero cards or the wrong tenant's counts (still no PHI because Patient count is a number, not a row).
- Rollback: revert the PR. No schema change, no data migration, no OpenIddict change, no permission migration. Remove the new files; `abp generate-proxy` next run deletes the Angular proxy folder; `dashboard.component.html` reverts to the pre-PR shell. No cleanup SQL required.
- HIPAA safety: `ILogger` uses in `DashboardAppService` MUST NOT emit the count values under Information level if the caller is cross-tenant (to avoid tenant-capacity signal leakage across the log stream). Use `LogDebug` or omit the count values from the template. Covered by PR-review checklist item in CONTRIBUTING.md.

## Open sub-questions surfaced by research

- Which is the 13th OLD card? The ItAdmin screenshot caption at `09-ui-screens.md:47` lists 12 labels (8 appointment-state + Patient + Claim Examiner + Applicant Attorney + Defense Attorney) but the prose says "13 stat cards". The gap is either a duplicated state card (Cancelled No-Bill vs Late, or Rescheduled No-Bill vs Late) or a "Total Appointments" aggregate. Resolve during implementation by re-viewing `screenshots/old/admin/01-dashboard.png` -- this is a 30-second visual check, not an MVP blocker.
- Should counts be date-filtered (e.g., "today only", "this week", "this month") per OLD convention? OLD's `spDashboardCounters` body isn't recoverable locally, so the temporal windowing is unknown. Default for MVP: all-time counts. Adrian can add a date-range picker (already present on the host dashboard's ABP widgets) in a post-MVP pass once Q13 subset is fixed.
- For the StaffSupervisor + ClinicStaff subset, who decides the labels? Default to the OLD 12 labels verbatim; Adrian can rename in the i18n JSON without a backend change once they are on the wire.
- `TotalTenants` on Host -- is that `AbpTenants.Count()` or `SaasTenants.Count()`? In ABP Commercial with the SaaS module, `Volo.Abp.Saas.Tenants.Tenant` is the entity; `Volo.Abp.TenantManagement.Tenant` is the base framework variant. Check `CaseEvaluationDbContext.Tenants` wiring during implementation; both roll up to the same underlying `AbpTenants` table. Not a blocker.
- Should a Bearer-less request hit the endpoint get 401 or 403? The `[Authorize]` class attribute returns 401 on missing token and 403 on authenticated-but-unauthorized by default; this matches ABP convention and is the desired behaviour. Confirms no change needed.
