# Appointment search standalone view + AllAppointmentRequest permission

## Source gap IDs

- [UI-07](../../gap-analysis/09-ui-screens.md) -- `/appointment-search`
  all-appointments standalone route. Track 09 table row 145, effort `Small`,
  sidebar label `All Appointments`.
- [G-API-19](../../gap-analysis/04-rest-api-endpoints.md) -- Flat list search
  stored proc on appointments (`spm.spAppointments`). Track 04 lines 99, 140.
  Listed as "old-only (intentional); filters baked into GET list". Effort note:
  "Verify `FilterText` covers same fields".
- [5-G07](../../gap-analysis/05-auth-authorization.md) -- Permission:
  `AllAppointmentRequest`. Track 05 row 195. Effort `Medium`. Evidence at
  OLD `access-permission.service.ts:24,45,67,89`.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:27-72`
  -- `AppointmentsAppService` class, class-level `[Authorize]`, `GetListAsync`
  decorated `[Authorize]` (no specific policy), returns
  `PagedResultDto<AppointmentWithNavigationPropertiesDto>`. Delegates to
  `_appointmentRepository.GetCountAsync` + `GetListWithNavigationPropertiesAsync`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/GetAppointmentsInput.cs:7-30`
  -- `GetAppointmentsInput : PagedAndSortedResultRequestDto` with 8 filter
  fields: `FilterText`, `PanelNumber`, `AppointmentDateMin`,
  `AppointmentDateMax`, `IdentityUserId`, `AccessorIdentityUserId` (attorney-
  accessor scope), `AppointmentTypeId`, `LocationId`.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs:92-108`
  -- `ApplyFilter` method. Critical finding: `FilterText`, when provided, is
  applied as `e.Appointment.PanelNumber!.Contains(filterText!)` only. No
  fan-out across patient name, attorney name, doctor name, or confirmation
  number. OLD's stored-proc search spans many more fields (see OLD reference
  below); this is the gap G-API-19's "Verify FilterText covers same fields"
  note made concrete.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs:27-31`
  -- `HttpGet` at `api/app/appointments` with `[RemoteService]` enabled on the
  controller (not on the AppService, per ADR-002). Delegates to
  `AppointmentsAppService.GetListAsync`. Swagger tags this path `Appointment`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:94-100`
  -- `Appointments` permission group. Constants: `Default`, `Create`, `Edit`,
  `Delete`. No `AllAppointmentRequest` sub-permission exists; NEW does not
  replicate the OLD permission name.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:7-12`
  -- `Dashboard` permission group: `CaseEvaluation.Dashboard.Host` (host-only)
  and `CaseEvaluation.Dashboard.Tenant` (tenant-only). Registered at
  `CaseEvaluationPermissionDefinitionProvider.cs:13-14` with
  `MultiTenancySides.Host` and `.Tenant` respectively. This is the NEW
  equivalent of an admin-see-all surface: the host admin implicitly sees all
  tenants' data through ABP's `IDataFilter.Disable<IMultiTenant>()` pattern
  when the request comes from a host context.
- `angular/src/app/appointments/appointment/appointment-routes.ts:4-21` --
  two routes only: `''` (list view, guarded by `authGuard + permissionGuard`
  for `CaseEvaluation.Appointments`) and `'view/:id'` (detail view, guarded
  by `authGuard` only -- that is NEW-SEC-01, tracked separately). No
  `/appointment-search` standalone route. No `add`, `edit`, or `search`
  entries here; the `add` route lives outside this child tree.
- `angular/src/app/appointments/appointment/providers/appointment-base.routes.ts:3-12`
  -- sidebar menu registration: `/appointments`, icon `fas fa-file-alt`,
  localization key `::Menu:Appointments`, `requiredPolicy:
  'CaseEvaluation.Appointments'`. A single nav entry for the whole
  Appointments feature. OLD had two ("All Appointments" via
  `/appointment-search` and "Pending" via `/appointment-pending-request` etc.);
  NEW collapses them under the single list.
- `angular/src/app/appointments/appointment/components/appointment.component.html:1-40`
  -- list page already wraps a `abp-advanced-entity-filters` form with a
  `panelNumber` text input and `min/max appointment date` pickers. This is
  the NEW equivalent of the OLD search page, and it is already filterable
  from the URL via query params (the ABP `ListService` binds query-string
  parameters to filters automatically). Track 09's `/appointment-search`
  route is therefore functionally subsumed by `/appointments` with filters
  applied -- once the filter coverage matches OLD.
- `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts:14-61`
  -- `AbstractAppointmentComponent` uses `ListService` (ABP's built-in
  list+filter service) and `PermissionService`. `ListService.hookToQuery()`
  binds query-string filters two-way, giving OLD-parity deep-linkable filter
  URLs for the dashboard card navigation pattern (see dashboard-counters
  brief).

## Live probes

- Probe 1 -- `GET https://localhost:44327/swagger/v1/swagger.json`. HTTP 200,
  anonymous. Confirms the only GET list path at `/api/app/appointments` is the
  existing `Appointment` tag with the 11 query parameters listed above
  (`FilterText`, `PanelNumber`, `AppointmentDateMin`, `AppointmentDateMax`,
  `IdentityUserId`, `AccessorIdentityUserId`, `AppointmentTypeId`,
  `LocationId`, `Sorting`, `SkipCount`, `MaxResultCount`). Response schema
  `PagedResultDto<AppointmentWithNavigationPropertiesDto>`. Proves no separate
  "search" endpoint exists on NEW.
- Probe 2 -- `GET /api/app/appointments?FilterText=test` with an admin
  password-grant bearer token. HTTP 200.
  Body: `{"totalCount":0,"items":[]}`. Empty result set because the LocalDB
  has zero appointment rows (reproduced by the service-status probe). The
  probe proves: (a) the endpoint accepts `FilterText` per the swagger spec,
  (b) authentication + authorization let an admin call it successfully, and
  (c) with zero rows present, no downstream behaviour check is possible --
  that has to come from code analysis (done above).
- Probe log:
  [../probes/appointment-search-listview-2026-04-24T13-42-00.md](../probes/appointment-search-listview-2026-04-24T13-42-00.md).

## OLD-version reference

- `P:/PatientPortalOld/patientappointment-portal/src/app/components/appointment-request/appointments/search/appointment-search.component.ts:31-215`
  -- `AppointmentSearchComponent` extends `AppointmentDomain`, loads 5 lookup
  collections (locations, appointment types, appointment statuses, document
  statuses), binds an 11-field search form with: `confirmationNumber`,
  `appointmentTypeId`, `locationId`, `claimNumber`, `dateOfInjury`,
  `dateOfBirthPatient`, `socialSecurityNumber`, `appointmentStatusId`,
  `packageDocumentStatusId`, `jointDeclarationReceiptStatusId`, plus free-text
  `searchQuery`. Calls `appointmentsService.search(query)`.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/appointment-request/appointments/search/appointment-search.component.ts:67-70`
  -- reads the `appointmentStatusId` query param from the route and permission-
  checks `MODULES.AllAppointmentRequest` on construction.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/shared/side-bar/side-bar.component.html:34-36`
  -- sidebar "All Appointments" sub-item is guarded by
  `*ngIf="hasPermission(modules.AllAppointmentRequest)"` and routes to
  `appointment-search`.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/start/app.lazy.routing.ts:104-105`
  -- lazy route registration for `appointment-search`.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/dashboard/dashboard.component.ts:75-93`
  -- dashboard click handlers route to `/appointment-search` with
  `appointmentStatusId` query-param values `Approved`, `Rejected`,
  `CancelledNoBill`, `RescheduledNoBill`, `CheckedIn`, `CheckedOut`, `Billed`.
  These are dashboard-card deep links that pre-apply a status filter. NEW's
  `/appointments` list already deep-links via `ListService` query-string
  binding -- the dashboard brief will depend on this pattern being live.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentsSearchController.cs:27`
  -- OLD server-side search calls `EXEC spm.spAppointments @Query, @UserId`
  via `DbContextManager.SqlQueryAsync<StoreProcSearchViewModel>`. The stored
  proc body is generated JSON from search criteria; the actual
  `spm.spAppointments` definition lives in the PROD DB (stub in
  `P:/PatientPortalOld/_local/stub-procs.sql:301`). Track 01 enumerated this
  proc; NEW replaces it with the LINQ-based `GetListAsync` per ADR 003 and
  track 04's "intentional" tag.
- `P:/PatientPortalOld/patientappointment-portal/src/app/domain/access-permission.service.ts:24,45,67,89`
  -- `MODULES.AllAppointmentRequest` appears in four role-access lists
  (ItAdmin, StaffSupervisor, ClinicStaff, plus one external role). In OLD's
  model this is the "I can see every appointment the tenant owns" permission
  that gated the `/appointment-search` route and the sidebar entry.
- Track-10 errata applied: none. Track 10 did not revise UI-07, G-API-19, or
  5-G07.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10; Angular 20 standalone components on the
  frontend; OpenIddict on port 44368 for auth (`CLAUDE.md` stack table).
- Row-level `IMultiTenant` (ADR-004). The `Appointment` entity implements
  `IMultiTenant` (track 10, reference-pattern docs). Auto-filter scopes every
  query to the current tenant. Cross-tenant visibility for a host admin
  relies on `using (_dataFilter.Disable<IMultiTenant>())` -- a per-call
  pattern, not a permission-name toggle.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no `ng serve` (ADR-005). None of these block the capability; any new
  AppService method must be accompanied by a manual controller entry per
  ADR-002, and any DTO change must be Mapperly-generated per ADR-001.
- HIPAA: the list page must not log patient identifiers into client or
  server telemetry beyond ABP's standard audit trail. Search by `dateOfBirth`
  and `socialSecurityNumber` (OLD had these inputs) is a policy concern --
  SSN search in particular is PHI-sensitive and may be descoped; confirm
  before adding.
- NEW already has `CaseEvaluation.Appointments` as the sidebar-and-list
  permission and `CaseEvaluation.Dashboard.Host` / `.Tenant` as the dashboard
  split. Adding a third permission just for cross-tenant visibility would
  duplicate what the host context already provides (see ADR analysis below).
- The NEW list already renders at `/appointments` with built-in filter form
  and query-string deep-linking via `ListService`. Any extra "search page"
  route would be a second component rendering the same data; a duplicate is
  avoided by deep-linking into `/appointments` with query params instead of
  adding `/appointment-search`.

## Research sources consulted

- [ABP -- Data Filtering (`IDataFilter`)](https://abp.io/docs/latest/framework/infrastructure/data-filtering)
  accessed 2026-04-24. HIGH confidence. Canonical pattern:
  `using (_dataFilter.Disable<IMultiTenant>()) { ... }` disables the tenant
  filter for the scope of the using block. Host-context requests naturally
  skip the tenant filter when `CurrentTenant.Id == null`; no explicit disable
  required for genuine host queries.
- [ABP -- Permissions](https://abp.io/docs/latest/framework/fundamentals/authorization/permissions)
  accessed 2026-04-24. HIGH confidence. Permission groups are defined in a
  `PermissionDefinitionProvider` and referenced via
  `[Authorize(ConstantName)]`. `MultiTenancySides.Host`, `.Tenant`, or `.Both`
  can gate a permission's visibility per tenant scope.
- [ABP -- Multi-Tenancy (`MultiTenancySides`)](https://abp.io/docs/latest/framework/architecture/multi-tenancy)
  accessed 2026-04-24. HIGH confidence. Permissions declared with
  `MultiTenancySides.Host` are only grantable to the host side; the
  `Dashboard.Host` permission in NEW follows this exact shape.
- [ABP -- `ListService` and query-string filter binding](https://abp.io/docs/latest/framework/ui/angular/list-service)
  accessed 2026-04-24. HIGH confidence. `ListService.hookToQuery()` wires
  the filter state to the Angular router query params, producing deep-linkable
  URLs. This is the mechanism the NEW `/appointments` route already uses.
- [Angular Router -- Route Guards (canActivate)](https://angular.dev/guide/routing/route-guards)
  accessed 2026-04-24. HIGH confidence. Canonical spot to add
  `permissionGuard` alongside `authGuard` for a route, which NEW-SEC-01
  already calls out for the sibling `view/:id` gap.
- [ABP source -- `SettingManagementPermissionDefinitionProvider.cs`](https://github.com/abpframework/abp/blob/dev/modules/setting-management/src/Volo.Abp.SettingManagement.Application.Contracts/Volo/Abp/SettingManagement/SettingManagementPermissionDefinitionProvider.cs)
  accessed 2026-04-24. HIGH confidence. Reference for an ABP
  `PermissionDefinitionProvider` that registers a group plus children with
  explicit `MultiTenancySides`.
- [ABP support #5410 -- `IDataFilter` vs new permission](https://support.abp.io/QA/Questions/5410)
  accessed 2026-04-24. MEDIUM confidence (community-moderated support thread).
  The recommended pattern for "admin sees all tenants" in row-level
  multi-tenant systems is the `DataFilter` disable, not a separate permission
  name.

## Alternatives considered

1. **Subsume `/appointment-search` into `/appointments` with query-string
   deep-linking; extend `FilterText` to span patient name, attorney name,
   doctor name, confirmation number, claim number.** Add the extra fields to
   the existing repository `ApplyFilter` method; keep routes unchanged. Use
   `CaseEvaluation.Appointments` as the permission (already enforced by
   `permissionGuard` on the route). No new permission, no new AppService, no
   new Angular component. **chosen**. Lowest code footprint; matches NEW's
   flattening rationale in track 04; deep-links from dashboard cards continue
   to work.
2. **Add a second Angular route `/appointments/search` that renders the same
   `AppointmentComponent` with a distinct component selector.** Two sidebar
   entries, two breadcrumb labels, same backing data. **rejected**.
   Duplicates routing metadata for zero user-visible benefit; OLD's parallel
   routes were a legacy of its flat URL scheme and the `AllAppointmentRequest`
   permission being a role-distinct feature gate, not a behaviour gate.
3. **Port the OLD `AllAppointmentRequest` permission literally: add
   `CaseEvaluationPermissions.Appointments.All`, register it, and guard the
   list with it while the existing `CaseEvaluation.Appointments` gates only
   `/appointments/view/:id`.** **rejected**. The `IDataFilter` mechanism
   already produces the "see every tenant's data" outcome for host admins.
   A duplicate permission would drift over time from the actual authorization
   check; maintenance cost outweighs parity.
4. **Build a server-side search endpoint at `POST /api/app/appointments/search`
   that accepts a rich filter DTO (to match OLD's `POST
   api/appointments/search`).** **rejected**. Duplicates the existing
   `GetListAsync` path, contradicts track 04's "intentional difference"
   stance, and adds a second code path to maintain for no functional delta.
5. **Extend `FilterText` and also add first-class input fields
   (`ConfirmationNumber`, `ClaimNumber`) to `GetAppointmentsInput` as
   structured filters, not substrings of `FilterText`.** **conditional**.
   This is the OLD-parity escape hatch if Adrian wants exact-match
   confirmation-number lookup (`A00042`) on a dedicated input rather than
   relying on free-text search. Ship after option 1 if the list page
   usability testing flags ambiguity; keep optional to avoid bloating the
   input DTO.

## Recommended solution for this MVP

Subsume UI-07 into the existing `/appointments` route with filter coverage
that matches OLD, and drop the `AllAppointmentRequest` permission -- the
existing `CaseEvaluation.Appointments` permission plus the `IDataFilter`
pattern for host-context cross-tenant visibility is the NEW-native equivalent.

Exact steps, in dependency order:

1. **Extend `FilterText` to span OLD's implied free-text fields.** Edit
   `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs:92-108`
   `ApplyFilter` so that `FilterText`, when set, matches any of:
   `e.Appointment.PanelNumber`, `e.Appointment.RequestConfirmationNumber`,
   `e.Patient.FirstName`, `e.Patient.LastName`, `e.Patient.Email`,
   `e.IdentityUser.Name`, `e.IdentityUser.Surname`. Combine via a single
   `WhereIf(..., e => e.X.Contains(ft) || e.Y.Contains(ft) || ...)` lambda.
   Sample shape (14 lines):
   ```csharp
   var ft = filterText;
   return query
       .WhereIf(!string.IsNullOrWhiteSpace(ft), e =>
           (e.Appointment.PanelNumber != null && e.Appointment.PanelNumber.Contains(ft!)) ||
           (e.Appointment.RequestConfirmationNumber != null && e.Appointment.RequestConfirmationNumber.Contains(ft!)) ||
           (e.Patient != null && e.Patient.FirstName != null && e.Patient.FirstName.Contains(ft!)) ||
           (e.Patient != null && e.Patient.LastName != null && e.Patient.LastName.Contains(ft!)) ||
           (e.IdentityUser != null && e.IdentityUser.Name != null && e.IdentityUser.Name.Contains(ft!)) ||
           (e.IdentityUser != null && e.IdentityUser.Surname != null && e.IdentityUser.Surname.Contains(ft!)))
       .WhereIf(!string.IsNullOrWhiteSpace(panelNumber), e => e.Appointment.PanelNumber!.Contains(panelNumber!))
       ...
   ```
   Apply the same extension to the non-navigation-property
   `ApplyFilter(IQueryable<Appointment>, ...)` overload on line 110 for
   consistency.
2. **Update the Angular filter form** at
   `angular/src/app/appointments/appointment/components/appointment.component.html`
   to add a free-text `filterText` input alongside the existing `panelNumber`
   input, with a helper label such as "Search patient, attorney, or
   confirmation number". Bind to `filters.filterText` (the proxy-generated
   model already carries `filterText` because `GetAppointmentsInput` has it).
3. **Add `AppointmentStatusId` + `RequestConfirmationNumber` structured
   filters to `GetAppointmentsInput.cs`** (conditional -- if UX testing flags
   free-text as insufficient, these are the OLD-parity structured inputs).
   Follow the existing `PanelNumber` pattern. Regenerate the Angular proxy
   (`abp generate-proxy`) after DTO edits. Not strictly MVP-blocking once
   step 1 ships.
4. **Do NOT add a new `AllAppointmentRequest` permission.** The existing
   `CaseEvaluation.Appointments` permission at
   `CaseEvaluationPermissions.cs:94-100` gates the list route (already
   enforced by `permissionGuard` at `appointment-routes.ts:10`). Cross-
   tenant visibility for host admins relies on
   `using (_dataFilter.Disable<IMultiTenant>())` at call time, which is the
   ABP-native equivalent to OLD's "see all tenants" toggle. If Adrian
   requires an explicit admin-only path (e.g., for a separate host-only
   reporting view), use `CaseEvaluation.Dashboard.Host` -- already defined
   -- rather than introducing a third permission.
5. **Leave the dashboard cards' URL shape unchanged.** OLD dashboard cards
   linked `/appointment-search?appointmentStatusId=N`; NEW dashboard cards
   should link `/appointments?appointmentStatusId=N` (a shape already
   supported by `ListService.hookToQuery()`). That switch lives in the
   `dashboard-counters` brief, not here -- flag it as a downstream
   dependency.

Folder touches:

- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs`
  -- extend `ApplyFilter` overloads to span more fields on `FilterText`.
- `angular/src/app/appointments/appointment/components/appointment.component.html`
  -- add a `filterText` input in the filter form.
- (Conditional) `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/GetAppointmentsInput.cs`
  -- add `AppointmentStatusId`, `RequestConfirmationNumber` properties if
  Adrian approves structured-filter parity with OLD.

No migration, no new entity, no new AppService class, no new Angular route,
no new permission, no regenerated proxy (unless step 3 is taken).

## Why this solution beats the alternatives

- **Lowest code change per constraint**: one repository method edit and one
  HTML input addition. The route, component, permission, and AppService
  already exist in NEW; the capability gap is entirely in the repository's
  filter breadth.
- **Honours track 04's "intentional difference" stance**: NEW intentionally
  removed OLD's `POST api/appointments/search` in favour of filters on the
  GET list. Adding a second route or endpoint would reverse that design
  choice; extending `FilterText` preserves it.
- **Avoids permission drift**: `AllAppointmentRequest` in OLD is a role-
  assigned flag that multiple roles shared. NEW uses row-level multi-tenancy
  plus `CaseEvaluation.Dashboard.Host` for host admins; cross-tenant
  visibility is data-layer, not permission-layer. A new permission name
  would drift from the actual authorization check.
- **Dashboard deep-linking continues to work**: `ListService.hookToQuery()`
  already binds filters to the URL query string; the NEW dashboard cards
  can deep-link to `/appointments?appointmentStatusId=N` with no additional
  wiring.

## Effort (sanity-check vs inventory estimate)

Inventory says UI-07 = S, G-API-19 = "Verify", 5-G07 = M. Aggregate inventory
position is roughly S-M. Analysis confirms **S (0.5-1 day)** for the core
slice:

- ~1 hour to extend `ApplyFilter` in both overloads with multi-field
  `FilterText`.
- ~30 min to add the Angular filter input and localization keys.
- ~1 hour to verify deep-link behaviour and run a manual smoke test against
  the list page with synthetic seed data.
- ~1 hour to confirm swagger still reflects the unchanged DTO (the DTO is
  unchanged in the minimum-footprint path) and the manual controller still
  delegates correctly.
- ~1 hour to write a sanity test against `EfCoreAppointmentRepository`
  (test-after, not tdd) to lock the multi-field filter behaviour.

If Adrian approves step 3 (structured `AppointmentStatusId` +
`RequestConfirmationNumber` filters on `GetAppointmentsInput`), add ~1 hour
for the DTO edit + `abp generate-proxy` + regenerated Angular model plumbing.
Overall still well within S.

## Dependencies

- **Blocks** nothing strictly. The list page is already live; extending
  filter breadth strengthens downstream features but does not gate them.
- **Soft-blocks** `dashboard-counters`: the dashboard cards' deep-link URL
  shape assumes the list page honours `appointmentStatusId` (and possibly
  other) query-string filters. Once this brief lands, dashboard-counters
  can wire the deep links directly to `/appointments` without needing a
  separate `/appointment-search` route.
- **Blocked by** `lookup-data-seeds`: populated `AppointmentStatus`,
  `AppointmentType`, `Location` tables are required for the filter
  dropdowns to have options. The probe log confirmed current LocalDB rows
  exist but noted seeds are not reproducible from code; the MVP seed
  contributor lands with lookup-data-seeds.
- **Blocked by** `internal-role-seeds`: the permission
  `CaseEvaluation.Appointments` must be granted to the four internal roles
  (ItAdmin, StaffSupervisor, ClinicStaff, Adjuster) for them to see the
  list at all. The role seeds brief owns this assignment.
- **Blocked by open question**: none that blocks the minimum slice. If
  Adrian wants structured `AppointmentStatusId` + `RequestConfirmationNumber`
  filters (option 3), that is a scope choice rather than a blocker. If
  SSN / date-of-birth search from OLD must be replicated, that is a HIPAA
  scope question -- confirm with Adrian before adding.

## Risk and rollback

- **Blast radius**: low. Filter extension touches the read path only; write
  paths (`CreateAsync`, `UpdateAsync`, `DeleteAsync`) are untouched. A bug
  in the filter lambda produces wrong-data in the list page (false negatives
  or false positives) but cannot corrupt rows, leak cross-tenant data, or
  break other features.
- **Rollback**: revert the repository edit (one commit) to restore the
  original `PanelNumber`-only `FilterText` behaviour. Revert the Angular
  HTML edit to remove the free-text input. No migration, no data state to
  unwind. No-downtime rollback.
- **Performance risk**: the extended `FilterText` lambda now OR-joins
  across 6 string columns. For typical appointment-table sizes (hundreds to
  thousands of rows per tenant) SQL Server handles this with a scan; no
  index is required. If volume grows to the point where full-text search is
  warranted, introduce `CONTAINS` via a SQL Server full-text catalog in a
  later capability. Not an MVP concern.
- **Security risk**: multi-field `FilterText` means an attacker probing the
  endpoint can learn column shapes. Authentication is already required; the
  `permissionGuard` on the route and `[Authorize]` on `GetListAsync` gate
  access. No SQL injection risk (LINQ parameterises).

## Open sub-questions surfaced by research

- **Free-text search scope**: should `FilterText` include patient date of
  birth and social-security-number (OLD did)? SSN search is a HIPAA-sensitive
  capability. Recommend leaving it off by default and only adding if Adrian
  confirms the business need and logging/audit posture.
- **Structured-filter add**: Adrian, please confirm whether the list page
  should expose `appointmentStatusId` and `requestConfirmationNumber` as
  first-class filter inputs (option 3) or rely on free-text `FilterText`
  for both. OLD exposed them as dropdown/text; NEW's dashboard deep links
  use `appointmentStatusId` as a query param regardless, so at minimum
  `appointmentStatusId` should be a structured filter to support clean
  dashboard navigation.
- **Cross-tenant host-admin visibility**: where do host admins enter the
  "see all tenants" view? OLD put it behind the `AllAppointmentRequest`
  permission at `/appointment-search`. NEW's `CaseEvaluation.Dashboard.Host`
  permission plus `IDataFilter.Disable<IMultiTenant>()` in code gives the
  same outcome, but no UI route currently exercises it. If Adrian wants an
  explicit host-only cross-tenant list, that is a new capability (flag for
  Phase 5 scoping), not a gap under UI-07.
