# Probe log: external-user-home

**Timestamp (local):** 2026-04-24T23:35:00
**Purpose:** Confirm the Angular app root `/` is served and renders `HomeComponent`, correcting UI-16's "no external-user home" claim against live code.

## Probe 1 -- anonymous Angular root

### Command

```
curl -sk -o /dev/null -w "%{http_code}\n" http://localhost:4200/
```

### Response

Status: 200
Body: index.html shell bootstrapping Angular (not captured verbatim; conclusion is status + presence of `HomeComponent` in source).

### Interpretation

Root route is live; `app.routes.ts:17-22` lazy-loads `HomeComponent` with no guard. Combined with source reads of `home/home.component.ts` (isPatientUser logic at line 65-74) and `home/home.component.html` (3-tile layout), this confirms UI-16's inventory statement "NEW has /dashboard only; no external-user-optimized home" is obsolete. The external-user landing exists today; only polish is missing.

## Probe 2 -- OIDC password grant (NOT executed this session)

Admin password grant was exercised successfully by Phase 1.5 per `../probes/service-status.md`. Not re-run. No new token issued, logged, or embedded.

## Probe 3 -- role-scoped UI render (manually deferred)

Four external test users do not exist in LocalDB (external roles seeded empty per `docs/gap-analysis/05-auth-authorization.md:167-170`). Rendering `/` as Patient / Claim Examiner / Applicant Attorney / Defense Attorney requires provisioning users (persistent mutation; forbidden by Live Verification Protocol). Source-level verification of `isPatientUser` is definitive for this capability.

## Cleanup

No mutating operations performed. Nothing to revert.
