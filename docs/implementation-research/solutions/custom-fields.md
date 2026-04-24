# Capability research: custom-fields (per-AppointmentType extra fields)

## 0. Header context

- Gap IDs rolled up: DB-11 (schema), G2-N2 (domain/services -- listed Non-MVP in track 02), 03-G12 (application service), G-API-07 (5 REST endpoints + lookups), 5-G10 (`CustomFields` permission group), A8-03 (Angular proxy service), UI-08 (`/custom-fields` admin screen).
- Scope question: Q6 verbatim from `docs/gap-analysis/README.md:236` -- "CustomField dynamic form builder: port from OLD, or drop?"
- Track 10 erratum 4 (critical correction): OLD CustomField is **fixed-type**, not dynamic. 7-enum type set; no per-role / per-tenant scoping; hard cap of 10 active fields per AppointmentType; stored as raw `string` value with no type coercion (`CustomFieldDomain.CommonValidation()` is empty, `CustomFieldDomain.cs:105-109`). ABP `ObjectExtensionManager` + `ExtraProperties` is the native replacement. Effort drops from 2+ days to ~1 day.
- NEW-version evidence of absence: zero matches for `CustomField` / `customfield` under `W:/patient-portal/development/src/**` (Grep, word-boundary). Zero matches under `angular/src/app/**`. NEW Swagger `GET https://localhost:44327/swagger/v1/swagger.json` (HTTP 200, 317 paths) contains zero `/customfields*` paths. Capability is fully absent.

## 1. Goal

Decide whether NEW MVP ships a per-AppointmentType extra-field admin capability (matching OLD's "Custom Fields" screen under `Configurations` nav) and, if yes, register those fields on `Appointment` via ABP's native `ObjectExtensionManager` + `IHasExtraProperties` pattern instead of porting OLD's bespoke `CustomField` + `CustomFieldsValue` two-table schema.

## 2. Context

- Business driver: OLD exposes a "Configurations -> Custom Fields" admin screen (`docs/gap-analysis/09-ui-screens.md:40,146`) where an ITAdmin can declare up to 10 extra fields **per AppointmentType** (e.g., a PQME may need a "Body Region" field that ALL does not). Staff then see those fields on the intake form; answers are stored against the booked `Appointment` row.
- Why now: NEW has zero implementation. Gap surfaces across 7 of the 10 tracks (DB-11, G2-N2, 03-G12, G-API-07, 5-G10, A8-03, UI-08), which is why the brief rolls them up.
- Technical driver: OLD's approach is a two-table `CustomField(AppointmentTypeId, FieldLabel, FieldTypeId, IsMandatory, DefaultValue, MultipleValues, DisplayOrder, ...)` + `CustomFieldsValue(CustomFieldId, ReferenceId -> Appointment, CustomFieldValue string)` with a 7-value enum in `CustomFieldType.cs`. NEW already has ABP's `IHasExtraProperties` on every `FullAuditedAggregateRoot<Guid>` (confirmed: `Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant` at `src/.../Domain/Appointments/Appointment.cs:19`). That inherits `AggregateRoot` -> `Entity` -> `ExtraProperties` column with ABP built-in JSON serialization. So the storage layer is already in place; only the definition-management and UI layers are missing.

## 3. Constraints

- HIPAA: custom-field values may hold PHI (e.g., "Body region injured"). Two consequences:
  1. The storage column must inherit the same audit + row-level multi-tenant filter that the host `Appointment` row carries. `ExtraProperties` satisfies this because it is a column on the `AppAppointments` row itself, so it is covered by ABP's automatic tenant filter and by `FullAuditedEntity` creation/modification stamps.
  2. Any admin endpoint that defines fields (not values) must not leak value data. Keep the definition-management service permission-separated from the appointment value-write service.
- Tenant scoping: custom fields should be **tenant-scoped**, not host-scoped -- each doctor (one doctor per tenant per ADR-004) defines their own. OLD's schema does NOT scope per-tenant (it's per-`AppointmentTypeId` only), but OLD predates NEW's row-level multi-tenancy model. Porting to NEW must add tenant scope or it violates data-isolation expectations. Evidence: `ADR-004` "the doctor IS the tenant"; `docs/gap-analysis/README.md:179` lists multi-tenancy model as the #1 intentional architectural difference.
- ADR-001 (Mapperly not AutoMapper): any new DTOs must be mapped via a `[Mapper]` partial class in `CaseEvaluationApplicationMappers.cs`.
- ADR-002 (manual controllers): any new AppService needs a one-line-delegation controller in `src/.../HttpApi/Controllers/` + `[RemoteService(IsEnabled = false)]` on the service.
- ADR-003 (dual DbContext): no new table strictly required under the recommended path, but IF we take Option A (fallback), the two tables go on `CaseEvaluationDbContext` only (tenant-side) with no `IsHostDatabase()` guard.
- Hard cap: OLD enforces max 10 active fields per AppointmentType (`CustomFieldDomain.cs:40`). Preserving this prevents form-bloat and keeps the UI tractable.
- No PHI in code or tests: field definitions MUST use synthetic labels in any test seed data (e.g., "Sample Picklist").

## 4. Alternatives

**A. Port OLD's two-table schema (`CustomField` + `CustomFieldsValue`)** -- REJECTED.
- Requires two new entities, two migrations, two repositories, a DomainService, AppService, controller, Angular proxy, admin UI, intake-form renderer, and a lookup endpoint for the 7-type enum.
- OLD's value-table model requires a second SELECT per appointment to fetch values, which duplicates what ABP's `ExtraProperties` column does in a single row read.
- Cost: ~2-3 days per earlier track-03 estimate; `CustomFieldDomain` is thin but the FE rendering + per-type scoping work is the expensive piece.
- Benefit: one-to-one parity with OLD's DB shape (which aids any direct PROD data migration).
- Why rejected: track-10 erratum 4 established the ABP-native path halves the effort and fits NEW's row-level multi-tenant model without adding a new table.

**B. ABP `ObjectExtensionManager` + per-tenant `ExtraProperties` (CHOSEN).**
- ABP ships `ObjectExtensionManager.Instance.AddOrUpdateProperty<TEntity, TProperty>(...)` which registers a property on an extensible entity at module startup. Every `AggregateRoot` (and therefore `Appointment`) already implements `IHasExtraProperties`, so values are persisted to the JSON `ExtraProperties` column or as a real EF-mapped column when `MapEfCoreProperty<>()` is called in `PreConfigureServices` (source: abp.io docs "Module Entity Extensions" and "Object Extensions", 2026-04 lookup).
- For tenant-configurable fields, ABP's static `ObjectExtensionManager` is process-wide, so definitions come from a tenant-scoped store (ABP `SettingManagement` module, which is already wired per NEW `CLAUDE.md` "extras in NEW"). A startup contributor + a tenant-switch listener re-hydrates the per-tenant definitions on tenant context change.
- Pattern: a `CustomFieldDefinition` value object (label, FieldType enum, isMandatory, displayOrder, multipleValues for Picklist/Radio) is stored as JSON in ABP `Setting` under key `CaseEvaluation.CustomFields.<AppointmentTypeId>`. The definition-admin AppService reads/writes settings. The intake form fetches definitions via a read-only `GetDefinitionsAsync(appointmentTypeId)` endpoint and renders dynamically. On save, the Angular form serializes to a dict that maps to `Appointment.SetProperty(fieldKey, value)` in the AppService.
- 7-type enum ports verbatim from `P:/PatientPortalOld/.../Enums/CustomFieldType.cs` (Alphanumeric, Numeric, Picklist, Tickbox, Date, Radio, Time) into `Domain.Shared/Enums/CustomFieldType.cs`.
- 10-field hard cap enforced in the definition-admin service.
- Cost estimate: ~1 day backend (settings-backed definition service + admin controller + permission group + appointment read/write glue) + ~0.5-1 day Angular (dynamic form renderer + admin screen).

**C. ABP Pro `Volo.Forms` module (Google-Forms-style survey builder)** -- REJECTED.
- `Volo.Forms` ships with nested sections, conditional visibility, repeating groups, multi-form-per-entity, response analytics. That is substantially beyond OLD's fixed 10-field single-flat-list model.
- Overkill for a 10-field cap; additional licensing/complexity surface; adds a new admin module Adrian would need to learn.
- Why rejected: YAGNI relative to OLD parity and to the stated MVP scope.

## 5. Recommended solution (prose, per brief format)

Adopt Alternative B: ABP's `ObjectExtensionManager` + `IHasExtraProperties` + `SettingManagement` for tenant-scoped definitions. In `CaseEvaluationDomainModule.PreConfigureServices`, do NOT call `ObjectExtensionManager.Instance.MapEfCoreProperty<Appointment, T>()` at module startup (that path adds a real SQL column, which is wrong for us because we want tenant-configurable fields, not globally-fixed fields). Instead, register definitions in a tenant-scoped ABP `Setting` under a stable key pattern (`CaseEvaluation.CustomFields.<AppointmentTypeId>` holding a JSON array of `{ key, label, fieldType, isMandatory, displayOrder, multipleValues }`). Add `CustomFieldDefinitionAppService` (IT-Admin-only) for CRUD on definitions -- reads/writes ABP Setting keys via `ISettingManager.SetForTenantAsync`. Add `GetCustomFieldDefinitionsAsync(appointmentTypeId)` on `AppointmentsAppService` for the intake form to pull at render time. In `AppointmentsAppService.CreateAsync` / `UpdateAsync`, accept a `Dictionary<string, object?> CustomFieldValues` on the DTO; validate each against the definitions (type coercion per enum, mandatory-field check, picklist/radio value membership); write via `appointment.SetProperty("cf:" + fieldKey, value)` so values land in the row's `ExtraProperties` JSON. Enforce the OLD hard cap of 10 active fields per AppointmentType in `CustomFieldDefinitionAppService.AddAsync`. Add a new permission group `CaseEvaluation.CustomFields` with `Default` + `Create` + `Edit` + `Delete` (for definition admin). On the Angular side, build a small generic form renderer keyed off `FieldType` that emits the 7 control shapes (text, number, mat-select single, checkbox, datepicker, mat-radio-group, timepicker), and register the admin screen at `/custom-fields` behind `permissionGuard: CaseEvaluation.CustomFields`. Do NOT add a dynamic form-builder UI beyond what OLD shipped -- OLD's admin screen is just a 10-row list with label/type/mandatory/order columns, and we match that fidelity.

Why this wins over the alternatives: (1) zero new tables -- uses the `ExtraProperties` column every aggregate already carries; (2) tenant-scoping is native because `ISettingManager` supports `SetForTenantAsync`, whereas OLD's schema had no tenant awareness at all; (3) audit trail is free -- the `Appointment` row already lives under ABP's `FullAuditedEntity` stamps, and `ExtraProperties` JSON diff is captured by `AbpEntityChanges` automatically; (4) HIPAA-compatible because the values never leave the row they belong to, so tenant isolation is the same automatic filter that covers `Appointment` itself.

What would change this recommendation:
- If Adrian reports that OLD PROD has MORE than a trivial volume of custom-field data that must migrate one-to-one at cutover, Option A's parity schema may be easier to map over than re-writing migrations as `ExtraProperties` entries. Ask Adrian to pull a row count from PROD `spm.CustomFields` and `spm.CustomFieldsValues` before finalizing.
- If management later asks for cross-appointment-type queries against custom-field values (e.g., "list every appointment where the 'Is Spanish-speaking' tickbox is true"), a typed column beats a JSON scan; at that point switch to `MapEfCoreProperty` mode. Until then, `ExtraProperties` is faster to ship and adequate.

## 6. Effort

- Inventory entry: L (8+ days) if we port OLD's two-table schema (Alternative A).
- With track-10 erratum 4 correction applied (Alternative B): **S-M, approximately 1 day backend + 0.5-1 day Angular**.
  - Backend (1 day): 7-type enum port, `CustomFieldDefinitionSetting` DTO + Mapperly mapper, `CustomFieldDefinitionAppService` (CRUD against ABP Setting), permission group registration, controller, `AppointmentsAppService` hooks for read + validate + write of custom-field values, unit tests for definition CRUD and value validation.
  - Angular (0.5-1 day): admin screen at `/custom-fields` (list of types -> edit-definition modal with 10-row grid); generic renderer component consumed by `appointment-add`, `appointment-view`, and `appointment-detail` modal, switching on `FieldType` for the 7 control shapes.

## 7. Dependencies

- **Blocks**: none. This capability is a leaf -- no other gap depends on it landing first.
- **Blocked by**: `lookup-data-seeds` (DB-15). AppointmentType rows must exist before a definition can be keyed to one; otherwise the admin dropdown is empty and the feature cannot be demoed. Track 01 already flags DB-15 as "testing blocker" for every dropdown in NEW.
- **Blocked by open question**: Q6 verbatim ("CustomField dynamic form builder: port from OLD, or drop?"). Route to `blocked-on-scope.md` until answered. Per the memory note (`reference_gap_analysis.md`), Q6 is one of the 15 scope questions that feed MVP sequencing; Adrian's answer to Q6 is required before this capability can be scheduled into a Phase/Tier.

## 8. Recommended path given scope-gate status

- If Q6 = DROP: close capability, mark `UI-08`, `A8-03`, `03-G12`, `G-API-07`, `5-G10`, `DB-11`, `G2-N2` as explicitly non-MVP, add a BRAND-style post-MVP note. No code change.
- If Q6 = PORT-AS-IS (Option A): reopen to 2-3 days of work with two new entities + migration. Not recommended.
- If Q6 = PORT-EQUIVALENT (Option B, recommended): proceed with ~1.5-2 days of engineering. Sequence after `lookup-data-seeds` closes. No other blockers.

## 9. Testing plan (only relevant if Q6 = port-equivalent)

- Unit (Application.Tests, xUnit + Shouldly, SQLite in-memory):
  - `CustomFieldDefinitionAppServiceTests.AddAsync_ThrowsWhenTenthFieldAdded` -- enforces 10-field cap.
  - `CustomFieldDefinitionAppServiceTests.AddAsync_ThrowsOnDuplicateLabel` -- matches OLD `UpdateValidation`/`AddValidation` behaviour.
  - `AppointmentsAppServiceCustomFieldTests.CreateAsync_RejectsUnknownFieldKey` -- rejects payload keys not in the definition set for the chosen `AppointmentTypeId`.
  - `AppointmentsAppServiceCustomFieldTests.CreateAsync_RejectsMissingMandatory` -- enforces `isMandatory`.
  - `AppointmentsAppServiceCustomFieldTests.CreateAsync_CoercesNumericValue` -- ensures Numeric field round-trips as number, not string.
  - `AppointmentsAppServiceCustomFieldTests.CreateAsync_TenantIsolation` -- tenant A's definitions never apply to tenant B's appointments.
- Integration: manual smoke via Swagger -- `POST /api/app/custom-field-definitions` -> `POST /api/app/appointments` with `customFieldValues` dict -> `GET /api/app/appointments/{id}` confirms `extraProperties.cf:<key>` persisted.
- Angular: Karma/Jasmine for the renderer component -- one test per FieldType (7 tests) confirming the right control is emitted and form control is correctly bound.
- Synthetic data only per HIPAA rule. Seed definition example: `{ label: "Sample Picklist", fieldType: Picklist, options: ["A","B","C"] }`. Never a real body part or medical term.

## 10. Open questions to route to Adrian

- Q6 verbatim (README line 236) -- must answer.
- If Q6 = port-equivalent: should the admin screen allow **deleting** a definition that has existing values on live appointments? OLD's soft-delete pattern (`StatusId = Delete`) preserves history. Under `ExtraProperties`, deleting the definition does NOT remove stale keys from existing rows -- we need a policy (leave stale keys as inert read-only, or run a migration to scrub). Recommend "leave inert, surface as read-only in view page" but Adrian should confirm.
- If Q6 = port-equivalent: should Patient-role users see custom-field values on their own appointment view? OLD's UI does not gate this; NEW's tighter permission model could. Recommend "yes, read-only" by default.

## 11. Risk / Rollback

- Blast radius (Option B, recommended): low. Adds one new AppService (read/write to ABP settings), one new permission group, one new Angular screen, and a new shape in the Appointment create/update DTOs. Existing `Appointment` rows are unaffected because the new dict is optional. `ExtraProperties` column already exists; no schema change.
- Rollback: drop the new AppService and permission group; the ABP Setting keys are idempotent and can be cleared with a single `DELETE FROM AbpSettings WHERE Name LIKE 'CaseEvaluation.CustomFields.%'`. No data loss to core `Appointment` data because values live in `ExtraProperties` JSON -- the orphaned keys remain but are inert.
- Audit trail: ABP audit log covers `AbpSettings` changes and `Appointment.ExtraProperties` changes automatically. No bespoke log table required.
- Performance: JSON `ExtraProperties` column is already queried on every `Appointment` read; no new joins. Definition lookup is a single `AbpSettings` row fetch per `AppointmentTypeId` per request, cacheable via ABP's distributed cache (Redis is already wired in non-dev).

## 12. References and evidence chain

- OLD schema: `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/CustomField.cs:12,37,53,61,94` (table `spm.CustomFields`, `CustomFieldId` PK, `FieldLabel` max 200, `FieldTypeId` FK into enum, `AppointmentTypeId` FK).
- OLD value rows: `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/CustomFieldsValue.cs:12,33,53,59` (table `spm.CustomFieldsValues`, `CustomFieldId` -> definition, `ReferenceId` -> `spm.Appointments`, `CustomFieldValue` is a plain string).
- OLD enum: `P:/PatientPortalOld/PatientAppointment.DbEntities/Enums/CustomFieldType.cs:1-13` (Alphanumeric=12, Numeric=13, Picklist=14, Tickbox=15, Date=16, Radio=17, Time=18).
- OLD domain: `P:/PatientPortalOld/PatientAppointment.Domain/CustomFieldModule/CustomFieldDomain.cs:15,35-49` (hard-cap of 10 active fields + duplicate-label check; `CommonValidation()` at 105-109 is empty -- so no type coercion in OLD).
- OLD controller: `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/CustomField/CustomFieldsController.cs:14-75` (5 routes: Get list, Get by id, Post, Put, Patch, Delete under `api/customfields`).
- OLD lookup: `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Lookups/CustomFieldLookupsController.cs:22-26` (one endpoint: `customfieldtypelookups`).
- OLD Angular: `P:/PatientPortalOld/patientappointment-portal/src/app/components/custom-field/custom-fields/` (has `add/`, `edit/`, `list/`, `domain/`, shared-component container, routing module, service). Component shape = standard list + modal for add/edit; confirms OLD is NOT a dynamic form-builder.
- NEW absence (code): `Grep "\\bCustomField\\b"` over `W:/patient-portal/development/src` -> 0 matches. `Grep "custom-field"` over `angular/src/app` -> 0 matches.
- NEW absence (API): `curl -sk https://localhost:44327/swagger/v1/swagger.json | jq '.paths | keys[] | select(. | ascii_downcase | contains("custom"))'` -> no results. Swagger returned HTTP 200, 2607985 bytes, 317 paths total.
- Erratum 4 source: `docs/gap-analysis/10-deep-dive-findings.md:39-50` ("OLD CustomField schema is fixed-type, NOT dynamic forms" ... "Gap `G2-N2`/`03-G12` scope narrows: NEW can replace this with ABP `ExtraProperties` + `ObjectExtensionManager.MapEfCoreProperty<T, TProperty>()` -- no bespoke `CustomField`/`CustomFieldsValue` table needed. ... Estimated effort drops from 2+ days to ~1 day").
- ABP research source: `docs/gap-analysis/10-deep-dive-findings.md:212-217` (Part 4, "Dynamic custom fields: `ObjectExtensionManager.Instance.MapEfCoreProperty<T, TProperty>()`"; "Every `AggregateRoot` already implements `IHasExtraProperties`"; `Volo.Forms` noted as the overkill alternative; `EasyAbp.DynamicForm` as community option).
- Tenant model: `ADR-004` -- "one Doctor per tenant", which is why definitions must be tenant-scoped not host-scoped.
- Mapping: `ADR-001` -- Mapperly not AutoMapper. Any definition DTO mapped via `CaseEvaluationApplicationMappers.cs`.
- Controllers: `ADR-002` -- new `CustomFieldDefinitionController : AbpController, ICustomFieldDefinitionAppService` delegating to the service.
- NEW entity shape: `Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant` at `W:/patient-portal/development/src/.../Domain/Appointments/Appointment.cs:19`. `FullAuditedAggregateRoot` inherits from `AggregateRoot` which implements `IHasExtraProperties` -- confirms the storage piece already exists.
- NEW AppointmentType shape: `src/.../Domain/AppointmentTypes/AppointmentType.cs:14-35` -- host-scoped `FullAuditedEntity<Guid>`, M2M with Doctor via `DoctorAppointmentType`. Host scope is correct for the lookup, but definitions keyed to an AppointmentType must still be tenant-scoped -- resolved via ABP Setting's `SetForTenantAsync`.
- Permission pattern: root `CLAUDE.md` "Permissions" section + `CaseEvaluationPermissions.cs` nested-static pattern. New group will follow Default + Create + Edit + Delete.
- Track 08 evidence: `docs/gap-analysis/08-angular-proxy-services-models.md:143,203` -- A8-03 confirms NEW has no custom-fields proxy service.
- Track 09 evidence: `docs/gap-analysis/09-ui-screens.md:40,146` -- OLD screen under Configurations nav; UI-08 confirms NEW absent.
- Track 03 evidence: `docs/gap-analysis/03-application-services-dtos.md:27,126,148,205` -- OLD controllers under `CustomField/` folder; 03-G12 flagged "CustomField CRUD" at 2 days original estimate.
- Track 04 evidence: `docs/gap-analysis/04-rest-api-endpoints.md:31,128` -- OLD = 1 controller, 5 verbs; G-API-07 medium effort.
- Track 05 evidence: `docs/gap-analysis/05-auth-authorization.md:198` -- 5-G10 permission group in OLD (`access-permission.service.ts:75`); absent in NEW.
- Track 01 evidence: `docs/gap-analysis/01-database-schema.md:134` -- DB-11 L estimate (8-13 story points) if we ported OLD's two-table schema.
- Track 02 evidence: `docs/gap-analysis/02-domain-entities-services.md:56,138,205,279` -- G2-N2 is in the "Non-MVP" table (M, ~4 days at original estimate); domain service `CustomFieldDomain` under OLD's `CustomFieldModule/`.

## 13. Probe log

- Probe 1: `curl -sk -o /tmp/swagger.json -w "HTTP_%{http_code} bytes=%{size_download}\n" https://localhost:44327/swagger/v1/swagger.json` -> `HTTP_200 bytes=2607985`. NEW Swagger reachable at HTTPS 44327 (despite README listing HTTP 44327 at line 359 -- that entry in the repro cheat sheet is wrong, HTTP returns ERR_CONNECTION_REFUSED in this session).
- Probe 2: `grep -io '"/api/app/[^"]*"' /tmp/swagger.json | grep -i custom` -> 0 matches. Capability fully absent at the API surface.
- Probe 3: `grep -ioE '"/[^"]+":\s*\{' /tmp/swagger.json | wc -l` -> 317 paths total. Cross-check vs track-03 claim of 317 paths/438 verbs (track 10 Part 3) -- matches.
- Probe 4: appointment-type paths present: `/api/app/appointments/appointment-type-lookup`, `/api/app/appointment-types`, `/api/app/appointment-types/{id}`, `/api/app/doctor-availabilities/appointment-type-lookup`, `/api/app/doctors/appointment-type-lookup`, `/api/app/locations/appointment-type-lookup`. These confirm the host-scoped AppointmentType lookup surface that a definition-admin UI will consume.
- Probe 5: `Grep "\\bCustomField\\b"` against `W:/patient-portal/development/src` -> 0 matches (the 45 earlier hits were substring false-positives for `AppointmentType`; word-boundary search returns zero).
- Probe 6: `Grep "custom-field"` against `W:/patient-portal/development/angular/src/app` -> 0 files found.
- Probe 7: OLD Angular -> `ls P:/PatientPortalOld/patientappointment-portal/src/app/components/custom-field/custom-fields/` -> `add/ edit/ list/ domain/ custom-fields.module.ts custom-fields.routing.ts custom-fields.service.ts custom-fields-shared-component.container.ts custom-fields-shared-component.module.ts`. Classic add/edit/list structure -- confirms OLD is an admin CRUD screen, NOT a drag-and-drop form builder.
- Probe 8: OLD sources at `P:/PatientPortalOld/.../CustomField*.cs` -> 12 files confirmed (2 entity classes, 1 enum, 2 contexts, 2 UoWs, 1 domain service, 2 controllers, 1 constants class, 1 extended model, 1 `vCustomField*` set of 7 view-tables for projections). Evidence chain complete.

---

DONE custom-fields
