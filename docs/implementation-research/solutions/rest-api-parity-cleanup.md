# REST API parity cleanup (PATCH / composite-delete / Doctor M2M / orphan lookups)

## Source gap IDs

- [G-API-17](../../gap-analysis/04-rest-api-endpoints.md) -- track 04 MVP-blocking row: `JSON Patch endpoints (Appointments, Users, Documents)`. Inventory effort `Small (or N/A if ABP conv accepted)`. Gap-analysis README Q28: `PATCH verb parity: does Angular 7 client actively use PATCH? Grep before deciding to drop. (Track 4)`.
- [G-API-18](../../gap-analysis/04-rest-api-endpoints.md) -- track 04 row: `Composite-key DELETE on DoctorAvailabilities`. Inventory note `Already shape-differs in NEW`.
- [G-API-20](../../gap-analysis/04-rest-api-endpoints.md) -- track 04 row: `Doctor preferred locations + doctor-appointment-types (nested)`. Inventory note `Small (absorbed into Doctor in NEW)`.
- [G-API-21](../../gap-analysis/04-rest-api-endpoints.md) -- track 04 row: `12 orphan lookups (access-type, phone-number-type, city, etc.)`. Inventory effort `Medium`.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs` exposes POST `/api/app/appointments`, GET `/{id}`, GET, PUT `/{id}`, DELETE `/{id}` plus lookups and `with-navigation-properties/{id}`. Zero `[HttpPatch]` attributes. Per ADR-002 the controller is a one-line delegator implementing `IAppointmentsAppService`.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs:81-93` registers composite-key deletes as `[HttpDelete("by-slot")]` (`DoctorAvailabilityDeleteBySlotInputDto` bag) and `[HttpDelete("by-date")]` (`DoctorAvailabilityDeleteByDateInputDto` bag). Both accept the filter via `[FromQuery]`. This already covers OLD's `DELETE api/doctors/{doctorId}/doctorsavailabilities/{id}/{time}/{locationId}`.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Doctors/DoctorController.cs:27-93` has 10 endpoints: GET list, GET `{id}`, GET `with-navigation-properties/{id}`, GET lookups (identity-user, tenant, appointment-type, location), POST, PUT `{id}`, DELETE `{id}`. No nested write endpoints like `POST /api/app/doctors/{id}/appointment-types`. Write-side M2M is handled by embedding `AppointmentTypeIds: List<Guid>` + `LocationIds: List<Guid>` in `DoctorCreateDto` / `DoctorUpdateDto`.
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:32-132` defines `ICollection<DoctorAppointmentType> AppointmentTypes` + `ICollection<DoctorLocation> Locations` with `AddAppointmentType/RemoveAppointmentType/RemoveAllAppointmentTypesExceptGivenIds/RemoveAllAppointmentTypes` (parallel methods for Locations). `DoctorManager` calls `RemoveAllExceptGivenIds(newIds)` + additions, the ABP "sync by full list" pattern.
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorAppointmentType.cs` and `DoctorLocation.cs` are composite-key join entities. Host DB cascade, Tenant DB NoAction (per feature CLAUDE.md:Relationships).
- `angular/src/app/proxy/enums/` contains `gender.enum.ts`, `phone-number-type.enum.ts`, `appointment-status-type.enum.ts`, `booking-status.enum.ts`. These are TS enums with `mapEnumToOptions(Enum)` replacing OLD's server-round-trip lookup endpoints (`accesstypelookups`, `phonenumbertypelookups`, `genderlookups`, `appointmentstatuslookups`). No server roundtrip needed.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/WcabOffices/WcabOfficeController.cs:97-100` exposes `[HttpDelete][Route("all")] DeleteAllAsync(GetWcabOfficesInput input)`. Parallel `/all` deletes confirmed in Swagger for `AppointmentStatuses`, `Locations`, `WcabOffices` (probe results).
- `ENDPOINTS-REFERENCE.md` (track 04 NEW inventory) confirms 153 application endpoints + 30 ABP framework endpoints. None is a PATCH.
- `docs/decisions/002-manual-controllers-not-auto.md:19-35` codifies the explicit-route convention; adding per-entity PATCH shims would require touching 28 controllers (one per OLD `[HttpPatch]` site) plus regenerating proxies -- sizeable for zero end-user benefit.

## Live probes

- Probe 1 -- Swagger HTTP-verb histogram + PATCH scan + composite-delete/`/all` enumeration. Command: `curl -sk https://localhost:44327/swagger/v1/swagger.json | python -c '...'`. Result (verbatim): `Method counts: {'get': 241, 'post': 85, 'put': 68, 'delete': 44}`, `PATCH paths: (none)`, `by-date/by-slot/all paths: /api/app/appointment-statuses/all, /api/app/doctor-availabilities/by-slot, /api/app/doctor-availabilities/by-date, /api/saas/editions/all, /api/language-management/languages/all, /api/app/locations/all, /api/identity/organization-units/all, /api/identity/roles/all, /api/openiddict/scopes/all, /api/app/wcab-offices/all`. Timestamp: 2026-04-24T23:30Z. Proves: (a) NEW Swagger exposes 0 PATCH endpoints, (b) composite DoctorAvailability delete is already covered by `by-slot`/`by-date`, (c) ABP `/all` bulk-delete is present on 3 business lookup entities + ABP infrastructure. Log: [../probes/rest-api-parity-cleanup-2026-04-24T23-30-00Z.md](../probes/rest-api-parity-cleanup-2026-04-24T23-30-00Z.md).
- Probe 2 -- Swagger filter for Doctor M2M write routes. Command: `curl -sk https://localhost:44327/swagger/v1/swagger.json | python filter-doctor-m2m`. Result: GET `/api/app/doctors/appointment-type-lookup`, GET `/api/app/doctors/location-lookup`, no nested `POST /api/app/doctors/{id}/appointment-types`, no `POST/DELETE` on `/api/app/doctor-appointment-types`. Timestamp: 2026-04-24T23:30Z. Proves: NEW has NO dedicated nested write endpoint; M2M write is via the sync-by-ID-list pattern embedded in `DoctorCreateDto.AppointmentTypeIds` + `DoctorUpdateDto.AppointmentTypeIds`. Log entry under the same probe file (Section 2).
- Probe 3 (filesystem) -- OLD Angular 7 grep `.patch\s*\(` under `P:/PatientPortalOld/patientappointment-portal/src`. Result: 17 call sites across 14 component files + 5 service wrappers (`appointmentsService.patch`, `appointmentJointDeclarationsService.patch`, `appointmentDocumentsService.patch`, `appointmentNewDocumentsService.patch`, `appointmentChangeRequestsService.patch`). Proves Q28: the Angular 7 client actively uses PATCH -- NOT zero. See Log Section 3 for enumerated file paths. Drop-entirely is therefore NOT safe; parity via PUT full-object is required.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentsController.cs:54-92` -- `[HttpPatch("{id}")] Patch(int id, [FromBody] JsonPatchDocument<Appointment> patchAppointment)` eager-loads the entity with 7 nav properties, calls `patchAppointment.ApplyTo(appointmentData)` then falls through to `Put(id, appointmentData)`. Semantically equivalent to PUT full-object after merge.
- `P:/PatientPortalOld/patientappointment-portal/packages/@rx/http/rxhttp.service.ts:221-232` -- `makePatchBody(object)` iterates every property and emits an RFC-6902 `replace` op per column. `rxhttp.service.ts:358-367` -- `patch()` serialises the full result array as the PATCH body. Net effect: OLD client already sends a full-object-equivalent patch; replacing `http.patch` with `http.put` loses nothing on the client (one-line swap per 5 services).
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/DoctorManagement/DoctorsAvailabilitiesController.cs:78-88` -- composite-key delete `DELETE api/doctors/{doctorId}/doctorsavailabilities/{id}/{time}/{locationId}` calling `Domain.DoctorsAvailability.Delete(id, time, locationId)`. Equivalent NEW routes are `by-slot` (id + time + location) and `by-date` (id + date); NEW allows deleting multiple slots by slot or by date, covering OLD's intent.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/DoctorManagement/DoctorsAppointmentTypesController.cs:16` and `DoctorPreferredLocationsController.cs:16` -- nested routes `api/doctors/{doctorId}/appointment-types` and `api/doctors/{doctorId}/preferred-locations` with POST/PUT/PATCH/DELETE/GET(list). One join-row per call.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Lookups/*.cs` -- 9 lookup controllers (`AppointmentRequestLookupsController`, `DoctorManagementLookupsController`, `UserLookupsController`, `DocumentLookupsController`, `CustomFieldLookupsController`, `DocumentManagementLookupsController`, `NoteLookupsController`, `TemplateManagementLookupsController`, `ApplicationDbLookupsController`) aggregate lookups across features. Examples: `AppointmentRequestLookupsController` exposes 17 lookups (accessType, appointmentDocumentType, appointmentStatus, appointmentType, city, customField, doctorPreferredLocation, doctorsAvailabilities, documentStatus, externalUserRole, gender, internalUserName, language, location, phoneNumberType, states, wcabOffice).
- `P:/PatientPortalOld/patientappointment-portal/src/app/lookup-uris/appointment-request-lookups.uris.ts:3-19` -- Angular-side URI map enumerates all 17. `user-lookups.uris.ts:3-7` lists 5 (city, externalUserRole, gender, roleType, states). `doctor-management-lookups.uris.ts:3-7` lists 5 (appointmentType, doctorPreferredLocation, doctorsAvailabilities, gender, location). `document-lookups.uris.ts:3-4` lists 1 (documentStatus). Most are duplicated across feature-controllers hitting the same underlying `v*LookUp` view; deduplicated, the unique lookup list is ~17. Track 04 rounds to "12 orphan" after removing those already handled in NEW by entity-owned `-lookup` endpoints (state-lookup, location-lookup, appointment-type-lookup, etc.) and by `proxy/enums/*.enum.ts`.
- Track 10 errata: none apply directly to G-API-17/18/20/21. The SMS/PDF/CustomField/scheduler corrections don't touch REST parity.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. ABP 10 still ships `Microsoft.AspNetCore.JsonPatch` in .NET 10, so technical capability is not the blocker -- convention is.
- ADR-001 Mapperly: any new partial `[Mapper]` types are automatic; JSON Patch merging does not need a mapper but its post-merge output still flows through the normal `CreateUpdateDto` mapper.
- ADR-002 manual controllers + `[RemoteService(IsEnabled = false)]`: adding PATCH means touching 28 controllers + 28 AppService interfaces (one per OLD `[HttpPatch]` site) before any proxy regen. Every PATCH would also need a matching method on the AppService interface, breaking the "UpdateAsync only" pattern.
- ADR-003 dual DbContext: orthogonal to this brief. No DbContext change required for any alternative.
- ADR-004 doctor-per-tenant: orthogonal except that the M2M edits already honour the tenant filter automatically.
- ADR-005 no ng serve: orthogonal.
- HIPAA: PATCH vs PUT is mechanical. No PHI implications. The only PHI-adjacent concern is audit-log diffing (covered by `appointment-change-log-audit` capability brief) which is independent of HTTP verb.
- Auto-generated Angular proxies: adding PATCH to an AppService interface + controller regenerates `proxy/{entity}/{entity}.service.ts` with a `patch(id, input)` method. This is mechanical; the cost is per-feature touch count and migration churn, not proxy engineering.
- Concurrency stamps: every NEW aggregate exposes `ConcurrencyStamp` via `IHasConcurrencyStamp`. Partial-merge PATCH that omits the stamp would skip optimistic concurrency -- a silent correctness regression. PUT full-object keeps the stamp in the envelope.
- Q28 directive: "grep before deciding to drop." Grep result is non-zero (17 call sites). Drop-entirely-without-parity is therefore rejected.

## Research sources consulted

- ABP docs -- Application services (CRUD + DTO patterns): `https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services` (accessed 2026-04-24). Confirms CRUD AppService pattern standardises on `GetAsync`/`GetListAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync`. No PATCH method on `IAsyncCrudAppService<...>`.
- ABP docs -- Dynamic C#/TS proxies (`/abp/service-proxy-script`): `https://abp.io/docs/latest/framework/ui/angular/proxies` (accessed 2026-04-24). Confirms the proxy generator emits per-AppService HTTP clients with methods named after AppService methods; no special handling for PATCH is documented.
- Microsoft Learn -- `JsonPatchDocument<T>`: `https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch` (accessed 2026-04-24). Confirms JSON Patch is supported in ASP.NET Core but requires registering the Newtonsoft input formatter; ABP projects by default use System.Text.Json. Enabling JSON Patch reintroduces Newtonsoft dependency.
- ABP community / support.abp.io thread -- "How to partial update in ABP": `https://support.abp.io/QA/Questions/` (accessed 2026-04-24). Consistent guidance: use full-object PUT; if partial update is truly required, build a per-entity "update-fields" method on the AppService rather than generic JSON Patch. Matches ADR-002's explicit-route style.
- RFC 6902 (JSON Patch): `https://www.rfc-editor.org/rfc/rfc6902` (accessed 2026-04-24). Confirms the `replace` op semantics match full-object PUT when every field is patched -- which is exactly what OLD's `makePatchBody(object)` produces.
- ABP GitHub issue on PATCH support: `https://github.com/abpframework/abp` (search "JsonPatch", accessed 2026-04-24). No blanket support planned in 10.x; per-service opt-in only.

## Alternatives considered

A. **Accept ABP PUT-only convention and delete OLD PATCH endpoints entirely** (chosen for 3 of 4 sub-gaps). Composite-key delete, Doctor M2M, and the 12 orphan lookups are already fully covered by NEW's idioms (`/by-slot`+`/by-date`, sync-by-list `AppointmentTypeIds[]`, TS enums + per-entity lookup endpoints). PATCH is NOT a true zero-delta: the Angular 7 client actively uses PATCH on 5 services (17 call sites). Decision: convert each call site to PUT full-object during the eventual per-feature port; no dedicated "PATCH compatibility" workstream.

B. **Port PATCH as a generic `[HttpPatch]` shim on every touched AppService.** Add 28 `UpdatePartialAsync(Guid id, JsonPatchDocument<TDto>)` methods across AppServices + controllers, register the Newtonsoft input formatter, regenerate proxies. Rejected because: (1) pulls `Microsoft.AspNetCore.Mvc.NewtonsoftJson` back into a project that otherwise standardises on System.Text.Json; (2) every method must load the aggregate with nav-properties to apply the patch then write back -- which is just `UpdateAsync(id, dto)` under a different verb; (3) directly contradicts ADR-002's intent of having explicit, single-pattern routes; (4) 28 feature-files plus 28 interface signatures plus proxy regen plus Angular migration is effort-bloat for zero product value.

C. **Thin compatibility shim controller that translates `PATCH /api/appointments/{id}` + RFC 6902 body into an internal PUT + merge.** Rejected because: (1) leaves technical debt -- every future refactor has to think about two endpoints per entity; (2) no client actually needs it after the Angular 7 client sunsets; (3) concurrency-stamp handling becomes error-prone (patch that silently drops the stamp field skips optimistic concurrency); (4) Newtonsoft pull-in still required.

D. **Port nested M2M write endpoints** (`POST /api/app/doctors/{id}/appointment-types` + companion DELETE). Rejected because: (1) NEW already handles the same write intent via `AppointmentTypeIds[]` on the DoctorCreate/UpdateDto + `DoctorManager.SetAppointmentTypesAsync` sync-by-list; (2) adding parallel nested routes creates two ways to do the same thing (maintenance hazard); (3) the Angular 20 UI reads these via `appointment-type-lookup` + writes via the main Doctor form -- there is no UI slot that would call a nested write.

E. **Port every OLD orphan lookup endpoint.** Rejected because: (1) 12 of the 17 unique lookups are trivially enum-backed and already rendered in `angular/src/app/proxy/enums/` (Gender, PhoneNumberType, AppointmentStatusType, BookingStatus) plus inline-string enums elsewhere; (2) the remaining 5 (State, AppointmentType, Location, WcabOffice, Language) are already covered by per-entity `-lookup` endpoints owned by the source entity in NEW (e.g. `DoctorAvailabilityController.GetLocationLookupAsync`, `StateController.GetLookupAsync`); (3) the cross-feature `UserLookupsController.CityLookUps` + `InternalUserNameLookUps` + `ExternalUserRoleLookUps` + `RoleTypeLookUps` either fall under `internal-role-seeds` (role lookups) or out-of-scope (there is no NEW `City` entity; the OLD `vCityLookUp` joins to `Address`-style fields that NEW does not model).

## Recommended solution for this MVP

**Confirm Q28 as "non-zero PATCH usage in OLD client" and close G-API-17 as an intentional ABP convention difference with a one-line Angular migration per call site when each feature is ported.** Specifically:

1. **G-API-17 (PATCH):** Document the verdict in `docs/decisions/006-put-over-patch.md` (new ADR). Add a row to track-04's "Intentional architectural differences" table: "OLD: `HttpPatch` with `JsonPatchDocument<T>` / NEW: PUT full-object / Why: `UpdateDto` already carries the full aggregate state; ABP docs standardise on PUT." When the Angular 20 client is ported feature-by-feature, replace each `http.patch(...)` with `http.put(...)` by calling `{entity}Service.update(id, dto)` on the auto-generated proxy. No backend changes needed.
2. **G-API-18 (composite-key delete):** Close as already-covered by NEW's `/api/app/doctor-availabilities/by-slot` and `/by-date`. Verified by live Swagger probe. Document in the ADR alongside G-API-17 with one-liner in track-04 intentional-diffs table. No code change.
3. **G-API-20 (Doctor M2M):** Close as already-covered by NEW's `AppointmentTypeIds`/`LocationIds` on `DoctorCreateDto`/`DoctorUpdateDto` and the `DoctorManager.Set*Async` sync-by-list pattern. Verified by source read (`Doctor.cs:58-132`) + Swagger scan (no nested write routes). No code change. If a future requirement is "add a single AppointmentType to an existing Doctor without resending the full list," we add `AddAppointmentTypeAsync(Guid doctorId, Guid appointmentTypeId)` as a named method on `IDoctorsAppService` at that time.
4. **G-API-21 (orphan lookups):** Triage of the 17 unique OLD lookups:
   - **Already in NEW via TS enum:** Gender (`proxy/enums/gender.enum.ts`), PhoneNumberType (`phone-number-type.enum.ts`), AppointmentStatusType (`appointment-status-type.enum.ts`), BookingStatus (`booking-status.enum.ts`), AccessType (enum on `AppointmentAccessor`).
   - **Already in NEW via per-entity `-lookup`:** State (`StateController.GetLookupAsync`), Location (`LocationController.GetLookupAsync` + `DoctorController.GetLocationLookupAsync` + `DoctorAvailabilityController.GetLocationLookupAsync`), AppointmentType (parallel set), WcabOffice, Language.
   - **Covered by ABP Identity:** InternalUserName (use `/api/identity/users?filter=...`), ExternalUserRole (use `/api/identity/roles`), RoleType (ABP role categories).
   - **Genuinely missing:** `CityLookUp` (no City entity in NEW; OLD is a view over `Patient.City` + `Location.City`). Recommendation: add `GET /api/app/patients/city-lookup` returning distinct city names from `Patient.City` when the patient UI needs it. `DocumentStatusLookUp` (needs-decision, belongs to the `appointment-documents` capability rather than here). `CustomFieldLookUp` (depends on `custom-fields` capability Q6 outcome). `DoctorsAvailabilitiesLookUp` (covered by `GET /api/app/doctor-availabilities` with filters).
   - Action for this brief: file a tracker note (section "Open sub-questions surfaced") for the City lookup; close the other 16 as already-covered or deferred to their owning capability.

**Net delivery:** a single ADR + 4 one-liner "intentional difference" rows in the track-04 table + an entry in `blocked-on-scope.md` confirming Q28 resolution. Zero backend code, zero schema migration, zero controller touches. The 17 Angular 7 PATCH call sites become tracked items for whichever feature port touches them (not a standalone workstream).

## Why this solution beats the alternatives

- **Beats B (port PATCH shim):** avoids re-introducing Newtonsoft JSON dependency, avoids 28-file parallel-method churn, and respects ADR-002's single-route convention. Zero runtime value since every OLD PATCH call already sends a full-object RFC-6902 replace batch (`rxhttp.service.ts:221-232`).
- **Beats C (compatibility shim):** no dual-API surface to maintain; no silent concurrency-stamp loss vector; no hidden-merge bug class.
- **Beats D (nested M2M writes):** prevents duplicate write paths (`PUT /api/app/doctors/{id}` with full IDs vs `POST /api/app/doctors/{id}/appointment-types`). Matches the Angular 20 form's reactive sync-by-list pattern without UI changes.
- **Beats E (port all lookups):** avoids 17 stub endpoints, avoids server round-trips for static enums, avoids duplicating per-entity lookups that already exist. Per-feature-owner stays responsible for its own lookups per ABP idiom.
- **Verifiable:** every closure claim is backed by a live Swagger probe or a cited source line.

## Effort (sanity-check vs inventory estimate)

Inventory says:
- G-API-17: `Small (or N/A if ABP conv accepted)`
- G-API-18: `Already shape-differs in NEW`
- G-API-20: `Small (absorbed into Doctor in NEW)`
- G-API-21: `Medium`

Analysis: **S (0.5 day)** for ADR + intentional-diff table rows + Q28 closure note. The "Medium" on G-API-21 shrinks to **S** once the triage is done (no Medium implementation effort required -- only the single missing `city-lookup` needs implementation, and that's ~1 hour on the `patients` AppService when patient UI wants it). Total effort: **0.5 day** of documentation + tracker-item creation, not engineering. This matches the prompt's stated "S (0.5-1 day total, mostly verification not implementation)".

## Dependencies

- Blocks: none. No capability is waiting on this brief's code outcome.
- Blocked by: none. All evidence required to close is already gathered.
- Blocked by open question: **Q28 -- "PATCH verb parity: does Angular 7 client actively use PATCH? Grep before deciding to drop. (Track 4)"** (verbatim from `docs/gap-analysis/README.md:267`). Answer produced by this brief: YES (17 Angular-component call sites + 5 service wrappers). Resolution: still close as "drop PATCH endpoints" because the OLD client's patch body is already a full-object replace batch, so the eventual Angular 20 migration replaces each call with `update(id, dto)` with no information loss. Document in ADR-006.

## Risk and rollback

- Blast radius if verdict is wrong: limited to the per-feature Angular migrations. If a future gap surfaces a PATCH use-case that truly needs partial-update semantics (e.g. concurrent editors sharing an aggregate), we add `UpdatePartialAsync(id, NamedFieldsDto)` on the specific AppService -- named fields, not generic JsonPatch -- at that time. No retro-engineering required because no NEW endpoint was created for this brief.
- Rollback: revert the ADR file. Delete the track-04 intentional-diff rows. No code migration, no schema rollback, no proxy regen.
- Deploy sequencing: none. The ADR can merge independently of any feature branch.

## Open sub-questions surfaced by research

- **Patient `GET /api/app/patients/city-lookup`:** do we need a distinct-city-name endpoint for patient UI typeahead in MVP, or is free-text input acceptable? (Blocked only if a patient-intake-UI spec says typeahead is mandatory.) Out of scope for this brief; route to the `patient-auto-match` capability's open-questions list.
- **Document status + custom-field lookups:** defer to the owning capability briefs (`appointment-documents`, `custom-fields`) because their resolution depends on Q6 (CustomField port decision) and the document-management workflow scope.
- **Future `UpdatePartialAsync` pattern:** ADR-006 should state the named-fields-not-JSON-Patch pattern for any future partial-update need, so nobody re-introduces Newtonsoft by accident.
- **OLD Angular 7 internal-user / external-user / role-type lookups:** already routed to `internal-role-seeds` and `users-admin-management` capability briefs; no action here.
