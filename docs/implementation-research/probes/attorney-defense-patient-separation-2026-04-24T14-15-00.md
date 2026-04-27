# Probe log: attorney-defense-patient-separation

**Timestamp (local):** 2026-04-24T14:15:00
**Purpose:** Enumerate all Swagger paths containing "attorney" to prove the API surface has no defense-attorney endpoints and only the unified `applicant-attorneys` + `appointment-applicant-attorneys` controllers. Supports Alternative A vs B decision in the brief.

## Planned probe (NOT executed this run -- plan mode + credential gate)

### Command

```
TOKEN=... # password grant, see probes/service-status.md smoke test
curl -sk "https://localhost:44327/swagger/v1/swagger.json" \
  | python -c "import sys,json; d=json.load(sys.stdin); \
  [print(p) for p in sorted(d['paths']) if 'attorney' in p.lower()]"
```

### Expected response (based on static controller scan)

Status: 200 on swagger JSON. Filtered paths:

```
/api/app/applicant-attorneys
/api/app/applicant-attorneys/{id}
/api/app/applicant-attorneys/with-navigation-properties
/api/app/applicant-attorneys/with-navigation-properties/{id}
/api/app/applicant-attorneys/state-lookup
/api/app/applicant-attorneys/identity-user-lookup
/api/app/applicant-attorneys/as-excel-file
/api/app/applicant-attorneys/download-token
/api/app/appointment-applicant-attorneys
/api/app/appointment-applicant-attorneys/{id}
/api/app/appointment-applicant-attorneys/with-navigation-properties
/api/app/appointment-applicant-attorneys/with-navigation-properties/{id}
/api/app/appointment-applicant-attorneys/appointment-lookup
/api/app/appointment-applicant-attorneys/applicant-attorney-lookup
/api/app/appointment-applicant-attorneys/identity-user-lookup
```

No `defense-attorney` or `patient-attorney` paths.

### Interpretation (static evidence)

Two `grep -rl` hits under `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers`:

- `Controllers/ApplicantAttorneys/ApplicantAttorneyController.cs`
- `Controllers/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyController.cs`

Zero hits for `DefenseAttorney*Controller.cs`. Confirms NEW has ONE attorney domain + ONE join controller, and any separation must be added. This result alone is sufficient to narrow Alternative A vs B; rerunning the live Swagger probe in Phase 1.5 follow-up will corroborate without adding decision-relevant information.

### Cleanup (if mutating)

N/A -- read-only probe by design. No state change anticipated.

## Interpretation (what this tells us for the brief)

- NEW has no defense-attorney-specific HTTP surface, entity, service, controller, or Angular module.
- The existing `ExternalUserType` enum + `ExternalUserRoleDataSeedContributor` already seat Defense Attorney as a first-class identity actor.
- The entity layer currently conflates what identity + signup layers separate. That asymmetry is the capability's core finding.
- Alternative A (unified entity + join-side `AttorneyType` discriminator) remains executable without new controllers or new proxy modules. Alternative B would require adding 2 controllers + 2 proxy modules + 2 Angular modules.
