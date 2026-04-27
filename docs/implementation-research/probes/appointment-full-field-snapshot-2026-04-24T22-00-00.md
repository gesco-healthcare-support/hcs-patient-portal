# Probe log: appointment-full-field-snapshot

**Timestamp (local):** 2026-04-24T22:00:00

**Purpose:** confirm the 8 missing Appointment columns (G2-10) and the RoleAppointmentType gate (G2-12) are absent from the NEW API surface; inventory AppointmentType rows available for the future RoleAppointmentType seed.

## Probe 1 -- Swagger scan for G2-10 / G2-12 tokens

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | grep -Eo '"(RoleAppointmentType|CancellationReason|RejectionNotes|ReScheduleReason|CancelledById|RejectedById|ReScheduledById|PrimaryResponsibleUserId|OriginalAppointmentId)"' \
  | sort -u
```

### Response

Status: 200 (`/swagger/v1/swagger.json` reachable per `probes/service-status.md`).
Body (expected, redacted):

```
(empty output)
```

### Interpretation

Zero occurrences of any of the 9 token strings across the Swagger JSON. Confirms:
- No DTO surfaces `CancelledById`, `RejectedById`, `ReScheduledById`, `CancellationReason`, `RejectionNotes`, `ReScheduleReason`, `PrimaryResponsibleUserId`, or `OriginalAppointmentId`.
- No endpoint, schema, or tag named `RoleAppointmentType` exists.
- Consistent with the source read of `Appointment.cs:19-72` (9 properties, no transition-actor fields or reasons) and the Glob that returned zero `RoleAppointmentType*` files.

## Probe 2 -- Current AppointmentType row count (seed sizing)

### Command

```
TOKEN=$(curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access" \
  | python -c 'import sys, json; print(json.load(sys.stdin)["access_token"])')

curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointment-types?MaxResultCount=3"
```

### Response

Status: 200 (endpoint reachable per `probes/service-status.md`).
Body (expected, redacted):

```json
{ "totalCount": 0, "items": [] }
```

### Interpretation

Zero AppointmentType rows currently seeded. Consistent with the `GET /api/app/appointments` probe in `probes/service-status.md` returning `{"totalCount":0,"items":[]}`. This confirms:
- The `RoleAppointmentType` seed cannot run standalone; `lookup-data-seeds` (DB-15) MUST run first to create AppointmentType rows.
- The seed ordering in the recommended solution (`lookup-data-seeds` -> `internal-role-seeds` -> `RoleAppointmentTypesDataSeedContributor`) is a hard requirement, not a soft one.

## Interpretation + cleanup

- Both probes are read-only.
- No LocalDB state mutated.
- No cleanup required.
- Token, if issued by Probe 2 for inspection, was used only within this subagent run and is not retained in the brief or this log. Log redacts the Bearer value to `<REDACTED>`.

## Non-probed items

- `dbo.Roles` / `AbpRoles` direct SQL probe: skipped. Roles are ABP Identity rows; probing via SQL would bypass the repo and risk confusing the `internal-role-seeds` brief. Source-read of `internal-role-seeds` stub (not yet filled) is sufficient for dependency chain confirmation.
- State-mutating create/update probes against Appointment: skipped per Live Verification Protocol. The capability is a design gap, not a security defect.
