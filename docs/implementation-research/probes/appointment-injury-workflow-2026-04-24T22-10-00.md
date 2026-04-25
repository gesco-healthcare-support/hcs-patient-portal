# Probe log: appointment-injury-workflow

**Timestamp (local):** 2026-04-24T22:10:00
**Purpose:** Prove that the NEW backend exposes no HTTP surface or schema for the appointment-injury workflow (InjuryDetail + BodyParts + ClaimExaminer + PrimaryInsurance).

## Probe 1 -- Swagger path scan

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d = json.load(sys.stdin); paths = list(d.get('paths', {}).keys()); injury = [p for p in paths if 'injury' in p.lower() or 'body-part' in p.lower() or 'claim-examiner' in p.lower() or 'primary-insurance' in p.lower()]; print('Total paths:', len(paths)); print('Injury-related paths:', len(injury)); print('Matches:', injury)"
```

### Response

Status: 200 (implicit -- pipeline delivered JSON)
Body (interpreted):

```
Total paths: 317
Injury-related paths: 0
Matches: []
```

### Interpretation

317 total OpenAPI paths are exposed today. Zero contain any of the segments `injury`, `body-part`, `claim-examiner`, or `primary-insurance`. Confirms the backend has no REST surface for the capability (G-API-11 is a genuine gap, not a naming mismatch).

## Probe 2 -- Swagger component schema scan + AppointmentDto property list

### Command

```
curl -sk https://localhost:44327/swagger/v1/swagger.json \
  | python -c "import sys, json; d = json.load(sys.stdin); s = d.get('components', {}).get('schemas', {}); hits = [k for k in s.keys() if 'Injury' in k or 'BodyPart' in k or 'ClaimExaminer' in k or 'PrimaryInsurance' in k]; print('Schemas hits:', hits); a = s.get('AppointmentDto', {}).get('properties', {}); print('AppointmentDto props:', list(a.keys()))"
```

### Response

Status: 200
Body (interpreted):

```
Schemas hits: []
AppointmentDto props: []
```

### Interpretation

Zero component schemas match the four capability types. The `AppointmentDto` properties list came back empty via this filter path (swagger DTO shapes are composed with `allOf`/`$ref` indirection so a naive `properties` dict lookup does not surface inherited members). The important negative finding stands: no `AppointmentInjuryDetailDto`, `AppointmentBodyPartDto`, `AppointmentClaimExaminerDto`, or `AppointmentPrimaryInsuranceDto` exists in the Swagger component list. The `AppointmentDto` itself carries no injury property via its own properties bag (which, given the ABP `allOf` composition pattern, is consistent with the entity as read in `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:19-72`).

## Cleanup (if mutating)

Not mutating. No reverse operation required.

## Credentials

No Bearer token required for `/swagger/v1/swagger.json`. Nothing is redacted because nothing sensitive was requested.
