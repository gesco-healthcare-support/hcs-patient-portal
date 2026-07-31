# Security review -- change-request opposing-side consent endpoint (Group D, 2026-06-09)

Anonymous, state-changing public surface (`api/public/change-request-consent`). Run
`/security-review` on the diff before merge and tick each item.

## Surface
- `PublicChangeRequestConsentController` -- `[IgnoreAntiforgeryToken]` (class), `[AllowAnonymous]` (actions).
- `GET {token}` -> read-only consent info (safe for email-scanner prefetch; no state change).
- `POST {token}` -> records the Yes/No decision (the only state-changing path).

## Checklist
- [ ] **Token entropy:** 256-bit (`RandomNumberGenerator.Fill`, 32 bytes), Base64-URL. (`ChangeRequestConsentManager.GenerateRawToken`.)
- [ ] **Storage:** only the SHA256 hex is persisted (`ConsentTokenHash`); the raw token never hits the DB. A DB breach cannot reconstruct live consent links.
- [ ] **Single-use:** decision recorded only when `ConsentStatus == Pending`; the aggregate concurrency stamp resolves a double-click race (loser -> `AbpDbConcurrencyException`); replay returns the existing decision (idempotent), never a duplicate side-effect.
- [ ] **Expiry:** 7-day TTL; an expired token defaults to **No** (`Expired`) and the request surfaces in the supervisor mediation bucket.
- [ ] **GET vs POST:** GET is read-only; the decision is taken only on POST -> email prefetchers/scanners cannot auto-decide.
- [ ] **Tenant scoping:** resolved from the request subdomain (same as the public document-upload flow); the `IMultiTenant` filter scopes the token lookup to one tenant.
- [ ] **No PHI in logs / URL:** the token carries no PHI; audit logs record the decision + change-request id, not patient identifiers. The landing page shows only what the recipient already holds as a party (conf #, requested date, reason).
- [ ] **No enumeration leak:** an unknown/forged/expired token returns the generic `ChangeRequestConsentTokenInvalid` (no signal whether a token shape is valid).
- [ ] **Authorization:** the token IS the credential; the opposing-side representative receives it by email. No cross-user replay (token is bound to one change request).

## Known follow-ups (not blocking the demo)
- **Per-token / per-IP rate limit:** currently relies on ABP's global IP fixed-window limiter (same gap as `PublicDocumentUploadController`). Add a per-token throttle on the POST.
- **Explicit staff-notification email on No / Expired:** today surfaced via the supervisor queue mediation bucket; an explicit "consent declined / expired" email to staff is a follow-up.
- **Feature flag as a per-tenant setting:** `AppointmentChangeRequestConsts.ConsentGatingEnabled` is a compile-time const kill switch; promote to `ISettingProvider` for per-tenant toggling.
