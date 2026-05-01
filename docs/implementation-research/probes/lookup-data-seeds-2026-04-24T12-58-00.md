# Probe log: lookup-data-seeds

**Timestamp (local):** 2026-04-24T12:58:00
**Purpose:** Confirm whether the 6 lookup-table endpoints return empty (as track 01 and the README claim) or populated result sets, and capture row shape + row counts for use in the solution brief.

## Preparation: obtain access token

### Command (token fetch; token value never recorded)

```
curl -sk -X POST https://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
```

### Response

Status: 200. Access token length 1369 chars, stored only in a local shell variable. Redacted to `Bearer <REDACTED>` for every subsequent probe below.

## Probe 1 -- GET /api/app/states

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/states?MaxResultCount=5"
```

### Response

Status: 200.
Body (redacted; synthetic + public data only):

```
{
  "totalCount": 12,
  "items": [
    { "name": "TestState_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX", "id": "07e4e174-dac3-64b2-68aa-3a2062cc995f", ... },
    { "name": "North Dakota", "id": "fe43d350-9be4-a7c8-e2c5-3a2062cc994d", ... },
    { "name": "Texas", "id": "a238789d-6ed1-d126-6d3d-3a2062cc9932", ... },
    { "name": "Montana", "id": "1bbf063e-78cf-25b0-648d-3a2062cc991c", ... },
    { "name": "Oregon", "id": "c3018717-b982-c22b-2266-3a2062cc98ff", ... }
  ]
}
```

### Interpretation

Table is populated (12 rows, not 0). Real US state names plus one length-boundary test row. The README "every dropdown shows No data" claim does NOT reproduce; the rows survive in the current LocalDB file. Important because this means **the DB-15 gap is actually "seeds are not reproducible from code", not "seeds are empty at runtime"**. On DB rebuild the rows would be lost; the capability is still MVP-blocking, but the phrasing in the brief must be precise.

## Probe 2 -- GET /api/app/appointment-types

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/appointment-types?MaxResultCount=5"
```

### Response

Status: 200. `totalCount: 7`. Rows include `Record Review`, `Deposition`, `Agreed Medical Examination (AME)`, `Supplemental Medical Report`, plus a synthetic `TestType_XXXX...` length-boundary row.

### Interpretation

7 rows present. Names are workers'-comp IME terms. Usable as canonical MVP seed values after removing the synthetic row.

## Probe 3 -- GET /api/app/appointment-statuses

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/appointment-statuses?MaxResultCount=5"
```

### Response

Status: 200. `totalCount: 14`. Rows seen: `CancellationRequested`, `RescheduleRequested`, `Billed`, `CheckedOut`, `CheckedIn` (first page of 5).

### Interpretation

Count aligns with the 13-state `AppointmentStatusType` enum plus one extra label. All 13 canonical names present. These become the exact seed names.

## Probe 4 -- GET /api/app/appointment-languages

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/appointment-languages?MaxResultCount=5"
```

### Response

Status: 200. `totalCount: 13`. Rows seen include `Hmong`, `Armenian`, `Portuguese`, `Japanese`, plus a synthetic test row.

### Interpretation

12 real languages + 1 test row. The 12 real values map to the interpreter-language list expected in Southern California workers'-comp intake. Suitable as canonical seed set after removing the synthetic row.

## Probe 5 -- GET /api/app/locations

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/locations?MaxResultCount=5"
```

### Response

Status: 200. `totalCount: 8`. Rows include `HCS Closed Santa Ana`, `HCS Fresno Office 6`, `HCS Anaheim Office 5`, `HCS Santa Ana Office 4`, plus a synthetic `MaxLoc_XXXX...`.

### Interpretation

Rows are HCS-specific clinic records, NOT generic "host" data. These are deployment/tenant-operational data, not architectural seed data. The brief recommends seeding only 2 synthetic demo locations and leaving real clinics to a PROD-snapshot path.

## Probe 6 -- GET /api/app/wcab-offices

### Command

```
curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/wcab-offices?MaxResultCount=5"
```

### Response

Status: 200. `totalCount: 7`. Rows seen: `WCAB Irvine District Office`, `WCAB Riverside District Office`, `WCAB Bakersfield District Office`, `WCAB San Bernardino District Office`, `WCAB Glendale District Office`.

### Interpretation

Complete Southern California WCAB district-office roster present. Public government data -- suitable as canonical seed. Each row carries a `StateId` FK to California.

## Cleanup (if mutating)

None. All probes are read-only GETs. No persistent state changes.
