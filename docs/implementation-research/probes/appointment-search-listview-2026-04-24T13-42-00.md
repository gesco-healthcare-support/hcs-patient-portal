# Probe log: appointment-search-listview

**Timestamp (local):** 2026-04-24T13:42:00
**Purpose:** Confirm the GET `/api/app/appointments` parameter shape in the
live swagger spec and prove the endpoint accepts `FilterText` under admin
authentication.

## Preparation: obtain access token

### Command (token fetch; token value never recorded)

```
curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
```

### Response

Status: 200. Access token length 1369 chars, stored only in a local shell
variable. Redacted to `Bearer <REDACTED>` for every subsequent probe below.

## Probe 1 -- GET /swagger/v1/swagger.json

### Command

```
curl -sk "https://localhost:44327/swagger/v1/swagger.json"
```

### Response

Status: 200. Anonymous. JSON body includes the path
`/api/app/appointments` with `tag: "Appointment"` and 11 query parameters on
the GET method:

- `FilterText` (string)
- `PanelNumber` (string)
- `AppointmentDateMin` (date-time)
- `AppointmentDateMax` (date-time)
- `IdentityUserId` (uuid)
- `AccessorIdentityUserId` (uuid)
- `AppointmentTypeId` (uuid)
- `LocationId` (uuid)
- `Sorting` (string)
- `SkipCount` (int32)
- `MaxResultCount` (int32)

Response schema:
`PagedResultDto<AppointmentWithNavigationPropertiesDto>`.

### Interpretation

Confirms NEW exposes exactly one GET list endpoint; no separate search path
exists. The `FilterText` parameter is documented and accepted. The 8
non-paging filter fields match `GetAppointmentsInput.cs:7-30`. This is the
concrete surface that UI-07 and G-API-19 collapse into: "the list endpoint
is the search endpoint, once FilterText covers enough fields".

## Probe 2 -- GET /api/app/appointments?FilterText=test (authenticated)

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" \
  "https://localhost:44327/api/app/appointments?FilterText=test"
```

### Response

Status: 200.
Body:

```
{"totalCount":0,"items":[]}
```

### Interpretation

Three confirmations:

1. Authentication succeeds with the admin password-grant token.
2. The endpoint accepts `FilterText` as a query parameter (no 400 bad
   request).
3. Zero rows are returned -- consistent with the service-status probe's
   finding that the LocalDB has no appointment rows seeded. Cannot verify
   the filter's actual matching behaviour live; static analysis of
   `EfCoreAppointmentRepository.cs:92-108` (which restricts `FilterText` to
   `PanelNumber.Contains`) is the evidence base for the gap.

## Cleanup (if mutating)

None. All probes are read-only GETs. No persistent state changes.
