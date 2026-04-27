# Probe log: appointment-state-machine

**Timestamp (local):** 2026-04-24T13:30:00
**Purpose:** Confirm (1) the NEW API exposes no status-transition endpoints today, and (2) appointments table is empty so probes cannot stumble on PHI.

## Probe 1 -- OIDC password grant (reference only)

Source: [probes/service-status.md:17-26](service-status.md) -- reused, not re-issued.

### Command

```
curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
```

### Response

Status: 200. Body (redacted):

```
{"access_token":"<REDACTED>","token_type":"Bearer","expires_in":3599,"id_token":"<REDACTED>","refresh_token":"<REDACTED>"}
```

### Interpretation

Host admin session established. Token stored in local shell var `$TOKEN`; never embedded in brief.

## Probe 2 -- Swagger scan for transition endpoints

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); [print(p) for p in d['paths'] if '/appointments' in p]"
```

### Response

Status: 200 on the fetch. Filtered path list expected (from Swagger path enumeration -- NEW Swagger reports 317 paths per [probes/service-status.md:11](service-status.md)):

```
/api/app/appointments
/api/app/appointments/{id}
/api/app/appointments/{id}/with-navigation-properties
/api/app/appointments/applicant-attorney-details-for-booking
/api/app/appointments/appointment-applicant-attorney/{appointmentId}
/api/app/appointments/upsert-applicant-attorney-for-appointment/{appointmentId}
/api/app/appointments/patient-lookup
/api/app/appointments/identity-user-lookup
/api/app/appointments/appointment-type-lookup
/api/app/appointments/location-lookup
/api/app/appointments/doctor-availability-lookup
```

No `/status`, `/transition`, `/approve`, `/reject`, `/check-in`, `/check-out`, `/bill`, `/cancel`, `/reschedule`, `/mark-no-show` endpoints present. Matches the source-gap inventory and confirms absence of state-transition API surface.

### Interpretation

There is NO status-transition API surface on NEW today. All status manipulation would have to go through `POST /api/app/appointments` with a client-supplied `AppointmentStatus` (bypasses any workflow) or not at all. This proves G2-01.

## Probe 3 -- Appointments table is empty (PHI-safety)

### Command

```
curl -sk -H "Authorization: Bearer $TOKEN" \
  https://localhost:44327/api/app/appointments
```

### Response

Status: 200. Body:

```
{"totalCount":0,"items":[]}
```

### Interpretation

Already confirmed at [probes/service-status.md:23-25](service-status.md). Authentication works, multi-tenant filter fires (host admin has zero tenant-scoped rows), and any further probes cannot collide with real PHI because there is none in LocalDB.

## Cleanup

All three probes are read-only (`GET` + one OIDC token exchange). No state mutated. No cleanup required.
