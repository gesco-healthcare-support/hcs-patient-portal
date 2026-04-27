# Probe log: appointment-accessor-auto-provisioning

**Timestamp (local):** 2026-04-24T13:05:00
**Purpose:** Confirm that (a) the AppointmentAccessors endpoint exists and returns empty-seed, (b) no auto-provisioning endpoint surface is exposed, (c) the existing POST body accepts only the 3-field CreateDto with pre-existing IdentityUserId.

## Probe 1 -- GET /api/app/appointment-accessors?MaxResultCount=3

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointment-accessors?MaxResultCount=3"
```

### Response

Status: 200
Body:

```
{"totalCount":0,"items":[]}
```

### Interpretation

Endpoint is live. Table is empty (LocalDB, no seed contributor for AppointmentAccessors exists, matches gap-analysis DB-15 scope). Proves the get-list surface; nothing to redact. No state change.

## Probe 2 -- Swagger signature enumeration

### Command

```
curl -sk "https://localhost:44327/swagger/v1/swagger.json" | python -c "<filter to appointment-accessors paths + requestBody schemas>"
```

### Response

Paths discovered:

- `GET  /api/app/appointment-accessors`
- `POST /api/app/appointment-accessors` -- body schema: `AppointmentAccessorCreateDto` (`AccessTypeId`, `IdentityUserId`, `AppointmentId`)
- `GET  /api/app/appointment-accessors/{id}`
- `PUT  /api/app/appointment-accessors/{id}` -- body schema: `AppointmentAccessorUpdateDto`
- `DELETE /api/app/appointment-accessors/{id}`
- `GET  /api/app/appointment-accessors/with-navigation-properties/{id}`
- `GET  /api/app/appointment-accessors/identity-user-lookup`
- `GET  /api/app/appointment-accessors/appointment-lookup`

### Interpretation

Confirms G2-05: the POST endpoint requires a pre-existing `IdentityUserId` (no email / first-name / last-name / role in the schema), so there is no server-side auto-provisioning path. Also confirms that the only Angular-proxy-facing operations are the 8 above -- consistent with A8-04's "proxy absent" finding because there is no `angular/src/app/proxy/appointment-accessors/` directory (verified by Glob). No state change.

## Cleanup (if mutating)

N/A -- both probes are read-only.
