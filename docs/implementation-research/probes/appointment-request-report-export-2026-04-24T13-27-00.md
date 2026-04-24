# Probe log: appointment-request-report-export

**Timestamp (local):** 2026-04-24T13:27:00
**Purpose:** Prove the inventory of export and download-token endpoints on the
running NEW HttpApi.Host (port 44327) and establish that `/api/app/appointments`
has no export surface today.

## Command

```
curl -sk -o /tmp/swagger.json -w "HTTPS %{http_code}\n" \
  https://localhost:44327/swagger/v1/swagger.json
```

Follow-up filters against `/tmp/swagger.json`:

```
grep -oE '"/api/app/[a-z-]+/(as-excel-file|download-token|export|report)[^"]*"' \
  /tmp/swagger.json | sort -u

grep -oE '"/api/app/appointments?[^"]*"' /tmp/swagger.json | sort -u

grep -oE '"/api/app/[a-z-]+/[a-z-]*export[^"]*"' /tmp/swagger.json | sort -u
```

No Bearer token required; Swagger JSON is anonymous.

## Response

Status: HTTP 200. Body size: 2,607,985 bytes.

First filter output (export/download-token/report surfaces):

```
"/api/app/user-extended/as-excel-file"
"/api/app/user-extended/download-token"
"/api/app/wcab-offices/as-excel-file"
"/api/app/wcab-offices/download-token"
```

Second filter output (appointments surfaces):

```
"/api/app/appointment-accessors"
"/api/app/appointment-accessors/{id}"
"/api/app/appointment-accessors/appointment-lookup"
"/api/app/appointment-accessors/identity-user-lookup"
"/api/app/appointment-accessors/with-navigation-properties/{id}"
"/api/app/appointment-applicant-attorneys"
"/api/app/appointment-applicant-attorneys/{id}"
"/api/app/appointment-applicant-attorneys/applicant-attorney-lookup"
"/api/app/appointment-applicant-attorneys/appointment-lookup"
"/api/app/appointment-applicant-attorneys/identity-user-lookup"
"/api/app/appointment-applicant-attorneys/with-navigation-properties/{id}"
"/api/app/appointment-employer-details"
"/api/app/appointment-employer-details/{id}"
"/api/app/appointment-employer-details/appointment-lookup"
"/api/app/appointment-employer-details/state-lookup"
"/api/app/appointment-employer-details/with-navigation-properties/{id}"
"/api/app/appointment-languages"
"/api/app/appointment-languages/{id}"
"/api/app/appointments"
"/api/app/appointments/{appointmentId}/applicant-attorney"
"/api/app/appointments/{id}"
"/api/app/appointments/applicant-attorney-details-for-booking"
"/api/app/appointments/appointment-type-lookup"
"/api/app/appointments/doctor-availability-lookup"
"/api/app/appointments/identity-user-lookup"
"/api/app/appointments/location-lookup"
"/api/app/appointments/patient-lookup"
"/api/app/appointments/with-navigation-properties/{id}"
"/api/app/appointment-statuses"
"/api/app/appointment-statuses/{id}"
```

Third filter output (any path ending in `*export*`):

```
(empty)
```

## Interpretation

- NEW exposes two `as-excel-file` + `download-token` pairs today:
  `wcab-offices` and `user-extended`. No other export surfaces.
- `/api/app/appointments/*` has 9 routes, none of which include an export,
  excel, download-token, or report path. Confirms `AppointmentsAppService`
  has no export method (matches the source read at
  `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:29-464`).
- No generic `/api/app/reports/*` or `/api/csvexport` (the OLD controller path)
  exists. Confirms gap 03-G11 / G-API-13.
- Evidence for the choice of alternative A: the per-entity ABP pattern is the
  only export pattern present in NEW and is the canonical target to replicate
  on Appointments.

## Cleanup (if mutating)

Not applicable. Probe is read-only (HTTPS GET of Swagger JSON, no mutation).
No authentication used.
