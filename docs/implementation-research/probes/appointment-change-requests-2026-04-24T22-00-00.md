# Probe log: appointment-change-requests

**Timestamp (local):** 2026-04-24T22:00:00
**Purpose:** Confirm that no REST endpoint, entity, service, or Angular route for the change-request capability exists in NEW (verifies capability is a pure net-add). Baseline appointments endpoint inventory establishes reference shape.

## Command 1: swagger scan for change-request / rescheduled / cancel-request / change-log endpoints

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | grep -oE '"/api/[^"]+"' \
  | grep -iE "change-request|rescheduled|cancel-request|change-log" \
  | sort -u
```

### Response 1

Status: 200 (swagger JSON fetch)

Body (filter output):

```
(empty -- zero matches)
```

### Interpretation 1

Zero endpoints in the live API surface match any of: `change-request`, `rescheduled`, `cancel-request`, `change-log`. Confirms the capability (G-API-10, R-01, R-02, R-10) is a net-add. No existing endpoint name needs to be avoided, renamed, or migrated. Authoritative evidence that NEW has no ChangeRequest implementation at any layer the HTTP surface would expose.

## Command 2: baseline appointments endpoint inventory (to establish reference controller shape)

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | grep -oE '"/api/app/appointments[^"]*"' \
  | sort -u
```

### Response 2

Status: 200 (swagger JSON fetch)

Body (filter output):

```
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
```

### Interpretation 2

The `AppointmentController` surfaces 10 endpoints grouped into: collection-level CRUD (`GET /, POST /, PUT /{id}, DELETE /{id}` via the same root path -- verbs multiplex the count); a `/with-navigation-properties/{id}` eager-load read; one sub-resource write at `{appointmentId}/applicant-attorney`; and five `*-lookup` dropdown endpoints. This establishes the reference-pattern shape the new `AppointmentChangeRequestController` will mirror: root at `/api/app/appointment-change-requests`, CRUD on the root + `{id}`, plus workflow actions (`POST {id}/approve`, `POST {id}/reject`) and a lookup endpoint for the `RequestType` / `Status` enums if the admin filter UI needs it. Also confirms the controller-surface cardinality expected for a tenant-scoped aggregate (~8-10 endpoints) -- matches the L effort band in the solution brief.

## Cleanup

None required. Both probes are read-only HTTP GETs against the swagger JSON endpoint. No database state, no authentication state, no persistent side effect. The curl invocations do not hit protected endpoints and no bearer token was issued for these probes.
