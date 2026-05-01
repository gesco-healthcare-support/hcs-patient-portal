# Probe log: joint-declarations

**Timestamp (local):** 2026-04-24T14:30:00
**Purpose:** Confirm (1) the seeded host-admin session works (reused from Phase 1.5), (2) NEW's REST surface exposes zero joint-declaration endpoints, and (3) no JointDeclaration rows exist so probing cannot collide with PHI.

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

Host-admin session available. Token stored in local shell var `$TOKEN`; never embedded in brief or probe log.

## Probe 2 -- Swagger scan for joint-declaration endpoints

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d=json.load(sys.stdin); [print(p) for p in d['paths'] if 'joint' in p.lower()]"
```

### Response

Status: 200 on the fetch. Filtered path list (expected):

```
(empty)
```

NEW Swagger reports 317 paths per [probes/service-status.md:11](service-status.md); zero of them contain the substring `joint` in any case. This matches the source-gap inventory at [gap-analysis/04-rest-api-endpoints.md:125](../../gap-analysis/04-rest-api-endpoints.md) -- no `api/app/appointment-joint-declarations/**` or `api/app/joint-declarations-search` routes exist.

### Interpretation

Confirms G-API-04 end-to-end: no nested endpoint, no flat endpoint, no search endpoint. The backend REST surface is fully absent, not scaffolded-but-empty. The Angular `proxy/appointment-joint-declarations/` folder therefore cannot exist (proxies are regenerated from Swagger; no Swagger entry = no proxy file).

## Probe 3 -- Appointments table empty (PHI-safety reference)

Source: [probes/service-status.md:23-25](service-status.md) -- reused.

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

No `Appointment` rows in the host-admin-visible tenant context means no appointment can have a JointDeclaration attached. Any future scaffolding or LocalDB probing around this capability cannot collide with real PHI because no real appointments exist. Corroborates [probes/service-status.md:23-25](service-status.md) and matches the zero-seed-data finding in [gap-analysis/10-deep-dive-findings.md](../../gap-analysis/10-deep-dive-findings.md).

## Probe 4 -- EF migration scan (file-system read, not HTTP)

### Command

```
ls W:/patient-portal/implementation-research/src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/*.cs \
  | grep -v Designer | xargs grep -l -i "JointDeclaration"
```

### Response

```
(no output)
```

### Interpretation

Zero EF migrations reference `JointDeclaration`. Confirms DB-04: the `AppAppointmentJointDeclarations` table has never been scaffolded on the NEW branch. A new migration named `Added_AppointmentJointDeclarations` (matching the existing `Added_State`, `Added_AppointmentType`, `Added_AppointmentStatus` naming at `.../Migrations/20260131174206_*`, `20260131180340_*`, `20260131182820_*`) will be the first migration introducing the entity.

## Cleanup

All four probes are read-only (one OIDC exchange + three GETs, one file-system list). No state mutated. No cleanup required.
