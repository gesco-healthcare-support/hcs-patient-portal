# Appointment notes (thread per appointment, parent/child)

## Source gap IDs

- [DB-10](../gap-analysis/01-database-schema.md) -- Notes on appointments (schema gap).
- [03-G10](../gap-analysis/03-application-services-dtos.md) -- Note CRUD (AppService gap).
- [A8-01](../gap-analysis/08-angular-proxy-services-models.md) -- Notes Angular client service (proxy gap).
- [UI-17](../gap-analysis/09-ui-screens.md) -- `/notes` appointment notes thread screen (UI gap).
- [G-API-06](../gap-analysis/04-rest-api-endpoints.md) -- Notes CRUD (4 endpoints) (REST parity gap).
- Related inventory entry: [G2-N1](../gap-analysis/02-domain-entities-services.md) -- Note thread per appointment (domain-services track).

## NEW-version code read

- `W:/patient-portal/implementation-research/src/` enumerated: no `Note.cs`,
  `NoteManager.cs`, `INoteRepository.cs`, `NotesAppService.cs`, `NoteDto.cs`,
  `NoteCreateDto.cs`, or `INoteAppService.cs` anywhere under any of the 10
  project folders (Domain, Application, Application.Contracts, HttpApi, etc.).
  Glob `**/*Note*.cs` returned zero results.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:3-132`
  defines 15 permission groups (Books, States, AppointmentTypes, ..., AppointmentApplicantAttorneys).
  No `Notes` group. `CaseEvaluationPermissionDefinitionProvider` has no `Notes` registration.
- `src/HealthcareSupport.CaseEvaluation.Domain/` has zero `*Notes*/` folder.
  The current aggregate-per-feature pattern (one folder per aggregate with
  Entity + Manager + Repo interface + WithNavigationProperties type) is
  illustrated by `AppointmentEmployerDetails/` at
  `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/AppointmentEmployerDetail.cs:15`
  (pattern template we would replicate for Notes).
- `angular/src/app/proxy/` enumerated (see root README index at
  `W:/patient-portal/implementation-research/angular/src/app/proxy/index.ts`):
  17 feature proxies generated (applicant-attorneys, appointments, doctors,
  ..., wcab-offices). No `notes/` proxy folder. No TypeScript enum or DTO
  containing the word `note` in any casing.
- `angular/src/app/` enumerated: no feature module, routes file, or component
  for notes. No `note-list.component`, `note-add.component`, or notes service
  wrapper analogous to OLD's `notes-shared-component.module.ts`.
- Track 10 deep-dive added no Note-related erratum or NEW-SEC finding.
  No applicable correction from
  `docs/gap-analysis/10-deep-dive-findings.md`.
- Dual DbContext layout (`CaseEvaluationDbContext` + `CaseEvaluationTenantDbContext`
  at `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/`)
  does NOT currently carry a `DbSet<Note>`. A new tenant-scoped Note entity
  must be added to `CaseEvaluationTenantDbContext` AND to `CaseEvaluationDbContext`
  (the `Both` context), per ADR-003.
- Existing self-referencing FK pattern is in use: `Location.ParentId` (optional
  `Guid?`) is configured with `HasForeignKey("ParentId")` and indexed
  (evidence: `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/CaseEvaluationDbContextModelSnapshot.cs:2575`
  and `20260202081019_Added_Location.Designer.cs:1689,1700,3010`). So the
  codebase already proves EF Core + ABP tolerate self-FK on a multi-tenant
  aggregate. Same pattern is reusable for `Note.ParentNoteId`.
- ABP audit logging is wired via `ABP AuditLogging` + `AbpAuditLogs.Exceptions`
  (gap-analysis README line 198). Field-level change diff for Notes would
  piggyback the generic `AbpEntityChanges` the framework already records --
  no bespoke NoteChangeLog is needed for HIPAA unless Q14 forces bespoke audit.

## Live probes

- `GET https://localhost:44327/swagger/v1/swagger.json | jq '.paths | keys[] | select(test("note"; "i"))'`
  at 2026-04-24T20:20Z. **HTTP 200**. Zero matches. Proves no `/api/app/notes/**`
  endpoints currently exist on the HttpApi.Host. Full log:
  [../probes/appointment-notes-2026-04-24T20-20-00Z.md](../probes/appointment-notes-2026-04-24T20-20-00Z.md).
- Same swagger JSON, `.components.schemas | keys[] | select(test("note"; "i"))`:
  zero matches. Proves no Note-related DTO has reached the proxy generator's
  input surface either.
- Swagger totals confirmed: 317 paths, 335 schemas. None include `note` in any
  casing. Log-file same as above.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/Note.cs:14-77` --
  full OLD entity. Fields: `NoteId` (int PK identity), `Comments` (required
  string, un-sized nvarchar), `CreatedById`, `CreatedDate`, `EditNoteId`
  (nullable -- points at the prior revision when a note is edited),
  `IsLatest` (nullable bool), `ModifiedById`, `ModifiedDate`, `ParentNoteId`
  (required, `Range(1,int.MaxValue)` -- so top-level notes use a sentinel
  value, usually 0, which contradicts the `Range` -- likely legacy noise),
  `AppointmentId` (FK to Appointments), `StatusId` (Status enum:
  Active/InActive/Delete).
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Note/NotesController.cs:37-77`
  -- 4 HTTP endpoints: POST, PUT (`{id}`), PATCH (`{id}`, `JsonPatchDocument<Note>`),
  DELETE (`{id}`). Plus two GETs (`Get(noteId)`, `Get(noteId, id)`).
- `P:/PatientPortalOld/PatientAppointment.Domain/NoteModule/NoteDomain.cs:40-106`
  -- Domain methods `Get/Add/Update/Delete` + stub validators. The
  distinctive OLD behaviour: on `Add`, if `EditNoteId > 0`, OLD marks the
  prior note as `StatusId = Delete` + `IsLatest = false`, rewrites
  `CreatedDate` + `EditNoteId` onto the new row, and inserts the new row.
  This is an **immutable-revision-chain** model, not an in-place update. The
  parent/child semantics are expressed via `ParentNoteId`; revision chain is
  separate via `EditNoteId`.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/note/notes/notes.service.ts:1-95`
  -- Angular 7 Rx service, `applicationModuleId: 33`, 4 HTTP methods (search,
  get, post, put, delete) + `lookup`, `group`, `filterLookup` helpers.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/note/notes/list/note-list.component.html:1`
  -- UI is a **single `<h1>Note</h1>` placeholder**. The list view was never
  implemented in production Angular 7. The list .ts file
  (`list/note-list.component.ts:36-43`) has its `this.listSubscription`
  subscribe call **commented out**. So OLD shipped an empty list screen.
  OLD UI-17 is therefore a ghost gap: the screen exists in code but has no
  real UX. Replicating OLD's exact UI is a near-zero-effort bar.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/note/notes/add/note-add.component.ts:48`
  -- Add component does issue `notesService.post(formGroup.value)`, so there
  IS a real create path and an edit path (for the immutable-revision case).
  So the write-side OLD UI is functional; the read/list side was never wired.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/Appointment.cs:153-154`
  -- Appointment has `ICollection<Note> Notes` inverse navigation,
  confirming one-appointment-to-many-notes on OLD's object graph.

## Constraints that narrow the solution space

- **ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict.** New aggregate
  uses `FullAuditedAggregateRoot<Guid>`, consistent with every other NEW
  aggregate (template: `AppointmentEmployerDetail:15`).
- **Row-level `IMultiTenant` (ADR-004), doctor-per-tenant.** `Note` is
  tenant-scoped because every appointment is tenant-scoped; a note
  inherently belongs to one doctor's tenant. Carry `Guid? TenantId`.
- **Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext
  (ADR-003), no ng serve (ADR-005).** Note gets a manual controller at
  `HttpApi/Controllers/Notes/NoteController.cs` delegating to
  `INoteAppService`; AppService carries `[RemoteService(IsEnabled = false)]`;
  Mapperly partial class added to
  `Application/CaseEvaluationApplicationMappers.cs`; `DbSet<Note>` registered
  in both the host+tenant `CaseEvaluationDbContext` AND the tenant-only
  `CaseEvaluationTenantDbContext`.
- **HIPAA: note bodies may contain clinical observations or patient-identifying
  commentary.** Access must be gated by permission (declarative
  `[Authorize]` at method boundary) + per-row visibility filter (external
  roles can only list notes on appointments they can access, same logic
  `AppointmentAccessors` already enforces elsewhere in the codebase).
  Writes MUST land in `AbpAuditLogs.EntityChanges` (already ABP default for
  entities that inherit auditing base classes).
- **Parent/child (reply thread):** self-FK on `Note.ParentNoteId` (nullable
  Guid). EF Core convention discovers the self-reference; a single
  `HasForeignKey("ParentNoteId")` declaration + explicit
  `OnDelete(DeleteBehavior.Restrict)` is required to prevent
  SQL Server's "cascade cycle" error on the same-table FK. Evidence that
  this pattern already works on this codebase: `Location.ParentId` at
  `Migrations/CaseEvaluationDbContextModelSnapshot.cs:2575`.
- **Per-role visibility:** admin (internal) sees all notes on an
  appointment; external users (patient, attorney) can only list notes where
  the appointment is one they are accessors on. Enforcement piggybacks
  existing `AppointmentAccessor` + multi-tenant filter -- no new visibility
  primitive is invented here.
- **OLD's immutable-revision behaviour via `EditNoteId` is explicitly
  dropped.** ABP audit already records field-level diffs for entities that
  extend auditing base classes; duplicating this in-table doubles storage
  and complicates the self-FK semantics. Scope note below.

## Research sources consulted

1. **Microsoft Learn -- "One-to-many relationships - EF Core" (the Self-referencing
   one-to-many section).** Confirms EF Core convention-discovers a self-FK
   when a single class declares both a reference nav and a collection nav
   to its own type. Cites `HasOne(e => e.Parent).WithMany(e => e.Children)
   .HasForeignKey(e => e.ParentId).IsRequired(false)`.
   Source: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many
   (fetched 2026-04-24). HIGH confidence.
2. **Microsoft Learn -- "Cascade delete - EF Core".** Required because
   SQL Server rejects cascade cycles on self-FK: the default cascade on a
   required self-relationship produces error 1785 ("cycles or multiple
   cascade paths"). Fix: set `DeleteBehavior.Restrict` or
   `DeleteBehavior.NoAction` (or make the FK nullable with `SetNull`, which
   also breaks the cycle).
   Source: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete
   (documented 2023-03-30; verified indirectly via one-to-many page).
   HIGH confidence.
3. **ABP Docs -- Authorization.** Confirms the nested-static-class pattern
   for permissions, the `IPermissionDefinitionContext.AddGroup(...)` +
   `group.AddPermission(...)` registration, method-level
   `[Authorize("CaseEvaluation.Notes.Create")]` declarative enforcement,
   and the distinction between `AuthorizationService.CheckAsync` (throw on
   deny) vs `IPermissionChecker.IsGrantedAsync` (boolean, used for per-row
   visibility in query logic).
   Source: https://abp.io/docs/en/abp/latest/Authorization
   (fetched 2026-04-24). HIGH confidence.
4. **ABP Docs -- Audit Logging.** `AbpEntityChanges` + `AbpEntityPropertyChanges`
   automatically capture field-level diffs for entities that extend
   `FullAuditedAggregateRoot<T>`. This is what the MVP needs for HIPAA
   compliance on Note CRUD; a bespoke NoteChangeLog table is not required
   unless Q14 (AppointmentChangeLog gap) decides otherwise.
   Source: https://abp.io/docs/en/abp/latest/Audit-Logging
   (referenced in gap-analysis README line 198 and
   10-deep-dive-findings.md; not re-fetched this session). MEDIUM confidence.
5. **Gap-analysis track 01 (DB-10 row at line 133).** OLD Note.cs effort
   was priced at S (3 story points). Confirms "small" tier.
6. **Gap-analysis track 02 (G2-N1 row at line 204).** Domain-layer effort
   priced at S (2 days). Confirms small tier.
7. **Gap-analysis track 03 (03-G10 row at line 146).** AppService + REST
   priced at 1 day. Confirms small tier.
8. **Gap-analysis track 04 (G-API-06 row at line 127).** 4 endpoints
   (POST/PUT/PATCH/DELETE) priced Small.

All external URLs cached 2026-04-24.

## Alternatives considered

A. **Full Note entity + AppService + controller + Angular thread dialog --
   CHOSEN (conditional on Q10 = "yes").** Minimal-surface port of DB-10 +
   03-G10 + A8-01 + UI-17 + G-API-06. One aggregate, 4 endpoints, one
   dialog, one permission group. Rides existing primitives -- no new
   infrastructure.

B. **Drop the capability entirely; rely on Appointment.Description or an
   ExtraProperty bag -- REJECTED if Q10 = "yes".** Fails the thread
   requirement (OLD had ParentNoteId; Adrian's source prompt explicitly
   listed parent/child as a constraint). A single free-text field also
   cannot carry per-note visibility; every role sees the whole blob. Only
   acceptable outcome if Q10 = "no".

C. **Reuse ABP's built-in commenting module -- REJECTED; module does not
   exist in the current dep list.** The ABP Commercial tier the project
   licenses (`Volo.Saas`, `Volo.LeptonX`, `Volo.Abp.Identity`, etc.)
   ships no `CmsKit.Comments` consumer. Adding CmsKit for notes alone is
   an outsized buy-in for a Small capability; also introduces domain
   coupling (CmsKit `Comment` is polymorphic via `EntityType` string, which
   is stringly-typed and weaker than a direct `AppointmentId` FK for HIPAA
   row-level filters).

D. **Model notes as a child document under Appointment's aggregate (ABP
   "composition" style: `Appointment.Notes` as an owned collection) --
   REJECTED.** Notes are mutated independently (create note, reply to note,
   delete one note) without touching the parent Appointment. ABP's guidance
   is to keep aggregates small and make Note a separate aggregate when its
   lifecycle diverges from its parent's. Matches OLD's shape too.

E. **Keep OLD's `EditNoteId` immutable-revision chain -- REJECTED.** Doubles
   storage per edit; conflicts with ABP's `AbpEntityPropertyChanges` (which
   already captures the diff); makes the self-FK semantics ambiguous
   (`ParentNoteId` for reply AND `EditNoteId` for history is two parents on
   one row). Dropping revision-history-in-table saves an edge case without
   losing HIPAA auditability.

## Recommended solution for this MVP

Add one new aggregate `Note` as a tenant-scoped entity
(`IMultiTenant`), following the `AppointmentEmployerDetails` blueprint:

- **Domain.Shared**:
  `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Notes/NoteConsts.cs`
  (`BodyMaxLength = 2000`, `VisibilityEnum` with values `Internal`, `All`).
- **Domain**:
  `src/HealthcareSupport.CaseEvaluation.Domain/Notes/Note.cs`
  (aggregate root, `FullAuditedAggregateRoot<Guid>, IMultiTenant`; props:
  `TenantId`, `AppointmentId`, `ParentNoteId?`, `Body`, `Visibility`);
  `NoteManager.cs` (create/reply domain service; enforces
  `Check.Length(body, ..., BodyMaxLength)`;
  `INoteRepository.cs`; `NoteWithNavigationProperties.cs`.
- **Application.Contracts**:
  `Notes/INoteAppService.cs` (GetListByAppointmentAsync, CreateAsync,
  UpdateAsync, DeleteAsync; no GetAsync(id)-listing -- list is always
  appointment-scoped to preserve per-row visibility); DTOs
  `NoteDto`, `NoteCreateDto`, `NoteUpdateDto`, `GetNotesByAppointmentInput`.
- **Permissions**: nested static class `CaseEvaluationPermissions.Notes` in
  `CaseEvaluationPermissions.cs` with `Default`/`Create`/`Edit`/`Delete`;
  registration block in `CaseEvaluationPermissionDefinitionProvider.cs`.
- **Application**: `NotesAppService.cs` with
  `[RemoteService(IsEnabled = false)]` and method-level `[Authorize]`
  attributes (Create/Edit/Delete) per NEW-SEC-02 reinforcement; per-row
  visibility in `GetListByAppointmentAsync` checks caller's
  `IAppointmentAccessor` membership.
- **Mapperly**: add
  `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
  partial class NoteToNoteDtoMapper` to
  `CaseEvaluationApplicationMappers.cs`.
- **EntityFrameworkCore**: `DbSet<Note>` in both `CaseEvaluationDbContext`
  (MultiTenancySides.Both) and `CaseEvaluationTenantDbContext`
  (MultiTenancySides.Tenant); self-FK
  `HasOne(n => n.Parent).WithMany().HasForeignKey(n => n.ParentNoteId)
  .OnDelete(DeleteBehavior.Restrict)`; explicit
  `HasIndex(n => n.AppointmentId)` + `HasIndex(n => n.ParentNoteId)`;
  appointment FK `OnDelete(DeleteBehavior.Cascade)` so deleting the parent
  appointment removes its notes; migration name
  `Added_Note_YYMMDDxxxxx` in both `Migrations/` and `TenantMigrations/`.
- **HttpApi**: manual controller
  `Controllers/Notes/NoteController.cs` at `[Route("api/app/notes")]`,
  delegating each method to `INoteAppService`. 5 endpoints (list, create,
  update, delete, plus `GET /api/app/notes/by-appointment/{appointmentId}`
  for the thread fetch).
- **Angular**: generate the proxy under `angular/src/app/proxy/notes/`
  (never hand-edit); feature folder `angular/src/app/notes/` with a
  standalone `notes-thread.component.ts` mounted inside the appointment
  view route (`/appointments/view/:id`) as a lazy sub-route
  `/appointments/view/:id/notes` OR an inline panel (decision deferred to
  the UX review in the design phase). Create/reply via a shared modal
  `note-edit-dialog.component.ts`.
- **DbMigrator**: no seed data; Notes are user-generated.

Cross-cutting plumbing already present (ABP Identity for author ID, ABP
audit logging for field diffs, LeptonX shell for dialog chrome). No new
package reference.

## Why this solution beats the alternatives

- Scope matches OLD's actual shipped surface (write path + simple thread)
  without replicating the dead list screen or the immutable-revision
  complication.
- Self-FK pattern already proven on the codebase (`Location.ParentId`) and
  validated against Microsoft's EF Core docs (high-confidence source).
- HIPAA requirements satisfied via (a) permission gate on every mutation,
  (b) per-row visibility filter on list endpoint, (c) automatic ABP field
  diff in `AbpEntityPropertyChanges`. Zero bespoke audit code.
- Scope kept Small: one aggregate, 5 endpoints, one dialog. Conforms to
  inventory's S estimate.

## Effort (sanity-check vs inventory estimate)

Inventory aggregates say DB-10 = S (3 pts), 03-G10 = 1 day, G2-N1 = S
(2 days), G-API-06 = Small. Analysis confirms **S (0.5 to 1 day)**.
Rationale: no new infrastructure, the self-FK pattern is precedented, the
aggregate shape is almost a copy of `AppointmentEmployerDetails`, and the
dialog is a standard LeptonX modal. Only risk factor that could inflate
the estimate: the visibility filter for external roles requires confirming
`AppointmentAccessor` is populated before notes can be gated -- see
Dependencies below.

## Dependencies

- **Blocks**: nothing. No other capability in the inventory lists notes
  as a blocker.
- **Blocked by**:
  - `appointment-accessor-auto-provisioning` (slug) -- for external-role
    per-row visibility on note list. If accessor auto-provisioning is not
    in place, the list filter has no lookup source and external users
    will see zero notes (safe-fail, but UX is broken). The capability
    works for admin (internal) role without this dependency.
  - `appointment-state-machine` (slug) -- OPTIONAL. If the design decides
    a note-creation event should trigger a state transition (e.g.,
    "Pending -> Rescheduled" on a scheduler note), state machine must
    exist. Recommended NOT to couple notes to state transitions for MVP;
    keep the dependency advisory only.
- **Blocked by open question**: `"10. **Notes on appointments
  (parent/child thread)**: required? (Tracks 1, 2, 3, 4, 9)"` (verbatim
  from `../gap-analysis/README.md:240`). If Q10 resolves to "no", this
  capability and all 5 source gap IDs are **dropped from MVP**; if "yes",
  proceed as recommended above.

## Risk and rollback

- **Blast radius**: appointment view page gains a new panel; no existing
  surface regresses. New controller at `api/app/notes/**` is additive.
  New DB tables (one new table `Notes` in both host-side and tenant-side
  migrations) cannot affect existing tables because the only inbound FK
  is Appointment->Note (cascade-delete parent side). No cross-tenant
  leak possible because `IMultiTenant` filter is automatic.
- **Rollback**: (a) set a feature flag
  `CaseEvaluation.Features.AppointmentNotes` to hide the Angular panel;
  (b) if full revert: remove the manual controller, remove the AppService
  registration, run `dotnet ef migrations remove` twice (once per
  context), redeploy. Data in the `Notes` table is orphaned but not
  accessible without the controller; drop the table in a follow-up migration.

## Open sub-questions surfaced by research

1. **UI placement**: sub-route (`/appointments/view/:id/notes`) vs inline
   panel vs side-drawer? UX design call. OLD had a standalone `/notes`
   route with a broken list, so we have no user expectation to match.
2. **Soft vs hard delete on a note**: ABP's `ISoftDelete` is inherited
   by default via `FullAuditedAggregateRoot`. For HIPAA, a soft delete
   preserves the audit trail and is the default; confirm this is
   acceptable before implementation (the old rejected alternative
   `ISoftDelete = false` would require a policy conversation).
3. **Max thread depth**: unlimited (`ParentNoteId` -> chain of any depth)
   vs capped at 1 (only one reply level)? OLD is silent; inventory has
   no constraint. Recommend unlimited for MVP with a depth-sort in the
   read DTO. Revisit if a replies-of-replies UX becomes unwieldy.
4. **Angular sub-route vs inline**: captured above under (1); one
   sub-question is whether to co-locate under the appointments module or
   a dedicated `notes/` feature folder. Recommend dedicated feature
   folder so the module can be reused from a future
   `/appointments/view/:id/full-thread` dialog without a circular import.
5. **External-role write permission**: can an attorney external role
   POST a note, or is write internal-only? Inventory is silent. Default
   to internal-only for MVP to sidestep the adversarial-review attack
   surface; revisit in a workflow-specific ADR if attorneys need the
   feature.
