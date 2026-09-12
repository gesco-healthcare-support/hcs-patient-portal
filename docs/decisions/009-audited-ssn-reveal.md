# ADR-009: Audited SSN reveal (design B)

**Status:** Accepted
**Date:** 2026-05-29
**Verified by:** code-inspect

## Context

`Patient.SocialSecurityNumber` is PHI. Internal staff and a patient's own account legitimately need
the full value (matching WCAB records, self-service), but it must not leak to every viewer of a
patient record or ride along in routine list/detail payloads.

## Decision

Standard patient DTO payloads carry only the masked last four digits -- `SsnVisibility.MaskToLast4`
is applied on every patient read AND write path. The full value is served only by a dedicated
`GetFullSsnAsync` endpoint returning `SsnRevealDto`, gated by TWO independent checks: the
`Patients.RevealSsn` permission AND an `SsnRevealAccess` predicate (caller is an internal role OR
the record owner). There is no other path to the unmasked value.

## Consequences

- Full SSN never appears in standard payloads, so a broad "view patients" grant cannot expose it.
- Reveal is both permission-gated and ownership-gated, and is a single auditable endpoint.
- Legacy callers that expected the full SSN in `PatientDto` now receive the masked value and must
  call the reveal endpoint.
- SSN is still stored plaintext at rest; at-rest encryption is a separate, deferred decision.

## Alternatives Considered

- Design A: return the full SSN to anyone holding the Patients view permission -- rejected as
  over-exposure of PHI.
- Field-level encryption at rest -- deferred (orthogonal to display masking; tracked separately).
- Omit SSN from the UI entirely -- rejected: staff need it to reconcile against WCAB filings.
