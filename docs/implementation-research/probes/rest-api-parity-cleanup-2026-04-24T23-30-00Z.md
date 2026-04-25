# Probe log: rest-api-parity-cleanup

**Timestamp (local):** 2026-04-24T23:30:00 local.
**Purpose:** Resolve Q28 (Angular 7 PATCH usage count) and confirm the shape of NEW composite-delete, Doctor M2M, and orphan-lookup surfaces so G-API-17/18/20/21 can close as intentional differences.

## Section 1 -- Swagger HTTP-verb histogram + PATCH scan + `by-date` / `by-slot` / `/all` enumeration

### Command
```
curl -sk https://localhost:44327/swagger/v1/swagger.json 2>&1 | python -c "import json,sys; d=json.load(sys.stdin); methods={};
for p,v in d['paths'].items():
    for m in v.keys():
        methods[m]=methods.get(m,0)+1
print('Method counts:',methods)
print()
print('PATCH paths:')
for p,v in d['paths'].items():
    if 'patch' in v: print(' ',p)
print()
print('by-date / by-slot / all paths:')
for p in d['paths']:
    if 'by-date' in p or 'by-slot' in p or p.endswith('/all'): print(' ',p)"
```

### Response
Status: 200

Body (verbatim):
```
Method counts: {'get': 241, 'post': 85, 'put': 68, 'delete': 44}

PATCH paths:

by-date / by-slot / all paths:
  /api/app/appointment-statuses/all
  /api/app/doctor-availabilities/by-slot
  /api/app/doctor-availabilities/by-date
  /api/saas/editions/all
  /api/language-management/languages/all
  /api/app/locations/all
  /api/identity/organization-units/all
  /api/identity/roles/all
  /api/openiddict/scopes/all
  /api/app/wcab-offices/all
```

### Interpretation
- NEW surface exposes 438 endpoints. **Zero PATCH.** Confirms G-API-17 is a real verb delta, not a miss in the NEW scaffolding.
- NEW DoctorAvailability composite-delete via `/by-slot` + `/by-date` confirms G-API-18 is already covered.
- Bulk delete (`/all`) present on AppointmentStatuses, Locations, WcabOffices plus ABP infrastructure (SaasEditions, LanguageManagement, OrganizationUnits, IdentityRoles, OpenIddictScopes). Not a gap.

### Cleanup
Read-only. No state change.

---

## Section 2 -- Swagger filter for Doctor M2M write routes

### Command
```
curl -sk https://localhost:44327/swagger/v1/swagger.json 2>&1 | python -c "import json,sys; d=json.load(sys.stdin)
for p,v in d['paths'].items():
    if 'doctor' in p.lower() and ('appointment-type' in p or 'location' in p or 'tenant' in p):
        methods=','.join(v.keys()).upper()
        print(f'{methods:25} {p}')"
```

### Response
Status: 200

Body (verbatim):
```
GET                       /api/app/doctors/tenant-lookup
GET                       /api/app/doctors/appointment-type-lookup
GET                       /api/app/doctors/location-lookup
GET                       /api/app/doctor-availabilities/location-lookup
GET                       /api/app/doctor-availabilities/appointment-type-lookup
POST,GET                  /api/app/doctor-tenant
GET,PUT,DELETE            /api/app/doctor-tenant/{id}
GET                       /api/app/doctor-tenant/databases
GET,PUT                   /api/app/doctor-tenant/{id}/connection-strings
POST                      /api/app/doctor-tenant/{id}/apply-database-migrations
GET                       /api/app/doctor-tenant/edition-lookup
POST                      /api/app/doctor-tenant/check-connection-string
POST                      /api/app/doctor-tenant/{id}/set-password
```

### Interpretation
- Zero nested Doctor M2M write routes (no `POST /api/app/doctors/{id}/appointment-types`, no `DELETE /api/app/doctors/{id}/appointment-types/{typeId}`, no `POST /api/app/doctor-appointment-types`).
- M2M is modelled via `AppointmentTypeIds` + `LocationIds` on `DoctorCreateDto` / `DoctorUpdateDto` and the `DoctorManager.Set*Async` sync-by-list pattern (see `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:58-132`).
- Read-side coverage is via `/api/app/doctors/appointment-type-lookup` + `/api/app/doctors/location-lookup` + parallel lookups on `/doctor-availabilities`.

### Cleanup
Read-only.

---

## Section 3 -- OLD Angular 7 PATCH usage enumeration (Q28)

### Command
Grep for `.patch\s*\(` under the OLD Angular source and count occurrences.
```
# Ran via Grep tool equivalent to:
# rg -n '\.patch\s*\(' P:/PatientPortalOld/patientappointment-portal/src
```

### Response
Count: **17 total occurrences across 14 files** (component call sites). Plus **5 service wrappers** that each call `this.http.patch(...)`.

Call sites (component layer):
```
appointment-request/appointments/view/appointment-view.component.ts:95
appointment-request/appointments/edit/appointment-edit.component.ts:932
appointment-request/appointments/list/appointment-list.component.ts:179
appointment-request/appointment-change-requests/view/appointment-change-request-view.component.ts:64
appointment-request/appointment-change-requests/edit/appointment-change-request-edit.component.ts:217
appointment-request/appointment-change-requests/list/appointment-change-request-list.component.ts:177
appointment-request/appointment-change-requests/add/appointment-change-request-add.component.ts:173
appointment-request/appointment-new-documents/detail/appointment-new-documents-details.component.ts:170
appointment-request/appointment-new-documents/detail/appointment-new-documents-details.component.ts:190 (commented)
document/appointment-joint-declarations/view/appointment-joint-declaration-view.component.ts:75
document/appointment-joint-declarations/search/appointment-joint-declaration-search.component.ts:115
document/appointment-documents/view/appointment-document-view.component.ts:83
document/appointment-documents/search/appointment-document-search.component.ts:173 (commented)
document/appointment-documents/search/appointment-document-search.component.ts:195
document/appointment-documents/info/appointment-document-info.component.ts:84
document/appointment-documents/info/appointment-document-info.component.ts:114
document/appointment-documents/detail/appointment-document-detail.component.ts:84 (commented)
```

Service wrappers calling `this.http.patch(...)`:
```
appointment-request/appointments/appointments.service.ts:70
appointment-request/appointment-change-requests/appointment-change-requests.service.ts:71
appointment-request/appointment-new-documents/appointment-new-documents.service.ts:84
document/appointment-documents/appointment-documents.service.ts:65
document/appointment-joint-declarations/appointment-joint-declarations.service.ts:68
```

### Interpretation
- Non-zero PATCH usage: Q28 answered. Per the gap-analysis directive ("Grep before deciding to drop"), drop-without-replacement is not safe.
- However, `@rx/http/rxhttp.service.ts:221-232` `makePatchBody(obj)` converts the full object into an RFC-6902 `replace` op per property. The PATCH body is therefore semantically equivalent to a PUT full-object body. Migrating each `http.patch(api, id, obj, true)` to `http.put(api, obj, false)` on the Angular 20 side loses no information.
- Server side: each OLD `[HttpPatch("{id}")]` action does `patchDoc.ApplyTo(loadedEntity)` then `return Put(id, loadedEntity)` (e.g. `AppointmentsController.cs:90-91`). PATCH is a delegate to PUT by construction.
- Verdict: resolve as **intentional architectural difference** in ADR-006 + track-04 table. No PATCH endpoints added to NEW.

### Cleanup
Filesystem-only read. No state change.

---

## Overall verdict for the capability

- **G-API-17 (PATCH):** Close as intentional difference. ADR-006 + Angular-migration-per-feature when each feature is ported.
- **G-API-18 (composite delete):** Close as already-implemented via `/by-slot` + `/by-date`. No action.
- **G-API-20 (Doctor M2M):** Close as already-implemented via `AppointmentTypeIds[]` + `LocationIds[]` on DoctorCreateDto / DoctorUpdateDto + `SetAppointmentTypesAsync` / `SetLocationsAsync` sync-by-list pattern. No action.
- **G-API-21 (orphan lookups):** 16 of 17 unique lookups already covered via TS enums + per-entity `-lookup` endpoints + ABP Identity. 1 genuine gap (`city-lookup` for patient UI typeahead) deferred to the patient-auto-match / UI capability when needed.

Total effort: S (0.5 day of ADR + table entries + Q28 resolution). No backend code, no schema, no controller changes.
