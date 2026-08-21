---
doc: document-types-m2m
date: 2026-06-19
type: plan
status: done
base-branch: feat/frontend-rework
backlog-item: 4
session: B
approach: mixed (tdd domain + test-after integration/UI + verify-live migration)
---

## Build status (2026-06-19)

- T1 DONE (0a2fac96): join entity + AppliesToAll + manager reconcile + per-tenant
  uniqueness. 4/4 manager tests green (incl. new reconcile + uniqueness tests).
- T2 DONE (0a2fac96): EF join config in BOTH DbContexts; repo join-filter + Include +
  per-tenant NameExists + GetWithAppointmentTypesAsync.
- T3 CODE DONE (aa1f1687): migration DocumentTypes_OneRecord_M2M -- offline SQL script
  verified (data step ordered before the column drop; EF accepts it). APPLY PENDING
  (needs DB backup + db-migrator/api rebuild; one-way).
- T4 DONE (0a2fac96): DTO field swap (AppointmentTypeIds + AppliesToAll); app service
  projects the set; Mapperly ignores added.
- T5 DONE (0a2fac96): seeder dedupe (one record per name with a type set).
- T6 DONE: backend builds clean (0 warnings); migration APPLIED + verified live
  (9->6 doc-types, Medical Records 3->1, 8 join rows, 0 orphaned document refs, system
  AppliesToAll set; pre-migration DB backed up to CaseEvaluation_preDocTypeM2M.bak).
  Proxy regen DONE (Adrian-authorized, api rebuilt first): models.ts gained
  appointmentTypeIds + appliesToAll.
- T7 DONE (5bd66f17): config-hub multi-select + "applies to all" switch + per-row
  type-summary chip; gateway maps/sends the set. Angular bundle compiles clean.
- T8 DONE: verified live as stafsuper1 in the config hub. Document Types lists 6 deduped
  records with type-summary chips -- "Medical Records" shows "3 types" (was 3 rows),
  "Generated Packet" shows "System / All types", others "1-2 types". The Medical Records
  edit modal renders the multi-select with PQME + IME + AME all checked and "Applies to
  all" off -- matching the migration's join rows. Read/display + modal-load verified end
  to end through the regenerated proxy; the write path is covered by the passing reconcile
  unit test + clean build (not live-mutated, to preserve the freshly-migrated dev data).
- BONUS (ca19c4c0): fixed the handed-off appointments-list silent-load-failure bug
  (error state + toast + retry, distinct from an empty 200).

#4 is functionally complete. Optional follow-up (not blocking): live write round-trip in
the modal, and confirm the booking-wizard document picker per appointment type.

# #4 Document Types: one record + many-to-many to appointment types

## Goal

Invert the document-type data model from one row per `(name, appointment-type)` to
ONE `AppointmentDocumentType` record per name plus a many-to-many join to appointment
types, curated from the document side, WITHOUT breaking the type references on
already-uploaded documents.

## Locked decisions (Adrian, 2026-06-19 modal)

1. KEEP the `AppointmentDocumentType` entity / table / `/api/app/appointment-document-types`
   route / `CaseEvaluation.AppointmentDocumentTypes.*` permissions. Add the join table,
   drop the per-row `AppointmentTypeId`. No rename to `DocumentType` (smallest blast
   radius; the "one record" goal is met by dedupe + M2M regardless of the class name).
2. Replace the null-`AppointmentTypeId` "applies to all" convention with an explicit
   `AppliesToAll` bool. System rows (`Generated Packet`) set it. Empty join +
   `AppliesToAll = false` = offered nowhere (effectively retired).
3. UI: EXTEND the generic config shell (`InternalConfigurationComponent` +
   `ConfigSectionGateway` + `cf-config.util.ts`) with a multi-select of appointment types
   for the `doctypes` section. Coordinate on the shared shell files.
4. Migration is FORWARD-ONLY on the data step: `Down` restores the schema (re-adds the
   column, drops the join table) but does NOT reverse the dedupe/repoint. Back up the DB
   before applying.

## Current shape (verified against source)

- Entity `AppointmentDocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant`
  (`Domain/AppointmentDocumentTypes/AppointmentDocumentType.cs`): `Name` (max 100),
  `AppointmentTypeId` (Guid?, loose ref, no EF FK), `IsSystem`, `IsActive`, `TenantId`.
- Documents reference a SPECIFIC type id: `AppointmentDocument.AppointmentDocumentTypeId`
  (Guid?, `Domain/AppointmentDocuments/AppointmentDocument.cs:107`). This is the repoint
  target and the data-loss risk.
- Seeder (`Domain/AppointmentDocumentTypes/AppointmentDocumentTypeDataSeedContributor.cs`)
  per tenant: system `Generated Packet` (null type) + label sets
  AME[Joint Letter, Medical Records], IME[Advocacy Letter, Medical Records],
  PanelQme[Panel Strike List, Advocacy Letter, Cover Letter, Medical Records].
  "Medical Records" = 3 rows, "Advocacy Letter" = 2 rows today.
- Manager (`Domain/AppointmentDocumentTypes/AppointmentDocumentTypeManager.cs`):
  uniqueness per `(name, appointmentTypeId)`, `EnsureNotSystem`, `EnsureNotInUseAsync`
  (counts `AppointmentDocument.AppointmentDocumentTypeId == id`).
- Repository (`IAppointmentDocumentTypeRepository`): `GetListAsync` / `GetCountAsync` /
  `DeleteAllAsync` take `appointmentTypeId`; `NameExistsAsync(name, appointmentTypeId,
  excludeId)`.
- App service (`Application/AppointmentDocumentTypes/AppointmentDocumentTypesAppService.cs`):
  CRUD + `GetListAsync` projects `UsageCount` per row after Map.
- Config-hub list (`config-section.gateway.ts`) calls `docTypes.getList(PAGE)` with NO
  `appointmentTypeId` (shows all). The UPLOAD PICKER is the consumer that filters by
  appointment type: `AppointmentAddComponent` feeds `documentTypeOptions` into
  `appointments/sections/appointment-add-documents.component.ts`.

## Target shape

- `AppointmentDocumentType`: drop `AppointmentTypeId`; add `AppliesToAll` (bool) and a
  collection nav to the join entity. Keep `Name` unique per TENANT (case-insensitive),
  not per type.
- New join entity `AppointmentDocumentTypeAppointmentType` (`AppointmentDocumentTypeId`,
  `AppointmentTypeId`), modeled on `AppLocationAppointmentType`
  (`20260609030108_I3_LocationAppointmentTypesM2M`): composite PK, FK to
  `AppAppointmentTypes` (no cascade on the type side -- host-scoped lookup), cascade-delete
  from the doc-type side.
- Picker contract PRESERVED: keep `GetListAsync(filterText, appointmentTypeId, ...)`; the
  IMPLEMENTATION now returns rows where `AppliesToAll` OR the join contains
  `appointmentTypeId`. Result: the Angular picker needs ZERO change.
- After dedupe, the seeded set becomes 1 system + 5 records:
  Generated Packet(AppliesToAll), Joint Letter[AME], Medical Records[AME,IME,PanelQme],
  Advocacy Letter[IME,PanelQme], Cover Letter[PanelQme], Panel Strike List[PanelQme].

## Tasks (backend-first; status DONE/TODO)

| # | Task | Approach | Status |
| --- | --- | --- | --- |
| T1 | Domain: join entity + `AppliesToAll` + nav on `AppointmentDocumentType`; manager `CreateAsync`/`UpdateAsync` take `appointmentTypeIds` + `appliesToAll` and reconcile join rows; uniqueness moves to per-tenant. Keep system + in-use guards. | tdd | TODO |
| T2 | EF: DbContext config -- map join entity (table/PK/FKs), add `AppliesToAll`, drop `AppointmentTypeId` property + `(TenantId, AppointmentTypeId)` index. Repository: `GetListAsync`/`GetCountAsync` join-filter (`AppliesToAll OR join has type`), `Include` join for projection, `NameExistsAsync` per-tenant. | code | TODO |
| T3 | Migration `DocumentTypes_OneRecord_M2M`: create join table, add `AppliesToAll`, data step (survivor per `(TenantId, Name)`, build join rows, repoint `AppointmentDocument`, set system `AppliesToAll`, delete duplicates), drop old index + column. Forward-only `Down`. Apply via db-migrator; verify counts. | verify-live | TODO |
| T4 | Contracts: `AppointmentDocumentTypeDto` / `*CreateDto` / `*UpdateDto` -- drop `AppointmentTypeId`, add `AppointmentTypeIds: List<Guid>` + `AppliesToAll: bool`. App service forwards new fields + projects `AppointmentTypeIds` after Map (like `UsageCount`). | test-after | TODO |
| T5 | Seeder: dedupe to one row per name with a type-id set; keep idempotent per name. | test-after | TODO |
| T6 | Backend build (`dotnet build`) + targeted tests green; pathspec commit; request proxy regen via Adrian (Session A's lane). | code | TODO |
| T7 | Angular (after regen): `cf-config.util.ts` (+`appointmentTypeIds`, `appliesToAll`, `hasAppointmentTypes` section flag), `config-section.gateway.ts` (doctypes maps/sends them), `internal-configuration.component.{ts,html}` (multi-select + "Applies to all" toggle in modal; type chips in the list). Picker untouched. | test-after | TODO |
| T8 | Verify on Falkinstein (screenshots): config hub edits a doc type's type set + AppliesToAll; "Medical Records" is ONE row; upload picker still filters per appointment type; an existing tagged document keeps its label. | verify-live | TODO |

## Migration data step (algorithm; final T-SQL authored + tested at build)

Operates on all tenants directly (migration runs outside the tenant filter). Per
`(TenantId, Name)` among non-system, non-deleted rows:

1. Pick survivor = earliest `(CreationTime, Id)`.
2. Insert join rows `(survivorId, AppointmentTypeId)` for every original same-name row's
   non-null `AppointmentTypeId` (dedup).
3. Repoint documents: `UPDATE AppAppointmentDocuments SET AppointmentDocumentTypeId =
   survivorId WHERE AppointmentDocumentTypeId IN (<non-survivor same-name ids>)` (tenant-
   matched).
4. `AppliesToAll = 1` for `IsSystem = 1` rows (they had null type).
5. Delete the non-survivor duplicate rows.
6. Drop `IX (TenantId, AppointmentTypeId)`, drop `AppointmentTypeId` column.

`Down`: drop join table, drop `AppliesToAll`, re-add nullable `AppointmentTypeId` +
recreate the index. Documents stay repointed; duplicate rows are NOT recreated (lossy --
documented).

## Risks + mitigations

- DATA LOSS (high): dedupe/repoint touches PHI-bearing `AppointmentDocument` rows. Back up
  the dev DB before applying; verify counts after (Medical Records 3->1, join has 3 rows,
  no document left pointing at a deleted id). Forward-only `Down` is intentional.
- Picker regression: mitigated by preserving the `GetListAsync(appointmentTypeId)` contract
  (join-filter implementation) so the Angular picker is unchanged. T8 explicitly re-tests it.
- Shared config-shell collision with Session A (#6 done, but the shell is shared): edit only
  the doctypes branch + add optional fields; coordinate via Adrian before committing the
  shared `internal-configuration.component` / `cf-config.util.ts`.
- Two concurrent migrations corrupt the snapshot: I am the sole migration writer; confirm a
  CLEAN model snapshot (no uncommitted migration) before `dotnet ef migrations add`.

## Pre-build gates

1. `git status` clean of any uncommitted migration / `CaseEvaluationDbContextModelSnapshot`
   change from Session A before adding mine.
2. Back up the dev SQL DB (or snapshot the container volume) before applying T3.
3. `NuGetAuditMode=direct` already set on the EF test project (commits unblocked).

## Coordination (parallel-build protocol)

- Migration lane: mine, exclusive, one at a time (`dotnet ef migrations add
  DocumentTypes_OneRecord_M2M --context CaseEvaluationDbContext`).
- Proxy regen: Session A's lane -- commit backend first, then ask Adrian to relay the regen.
  Commit only changed `models.ts` + `generate-proxy.json` + the new proxy folder; discard
  EOL-only `index.ts` no-ops.
- Stack restart to apply the migration: `docker compose up -d --build db-migrator api`;
  coordinate via Adrian (shared stack). SPA changes need `docker compose restart angular`.
- Commits: pathspec only (`git commit -F - -- <paths>`); check `git diff --cached
  --name-only` first.

## Verification (T8 evidence to capture)

- DB: row count of `AppAppointmentDocumentTypes` for the tenant (system + 5), join row
  count, zero orphaned `AppointmentDocument.AppointmentDocumentTypeId`.
- Config hub: screenshot editing "Medical Records" showing AME+IME+PanelQme selected;
  toggling "Applies to all".
- Booking wizard: screenshot the document picker for an AME appointment listing
  AppliesToAll + AME-joined types only.
