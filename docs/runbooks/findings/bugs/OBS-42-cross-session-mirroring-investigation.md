---
id: OBS-42
title: Investigation of "two browser windows mirrored each other" -- no app-level form-state sharing exists; only the localStorage OIDC session is coupled across same-profile tabs
severity: low
status: investigated
found: 2026-05-27 (reported by Adrian during parallel two-window testing)
flow: auth, appointment-booking
component: angular/src/app/shared/auth/session-identity-watcher.service.ts; angular/src/app/app.config.ts; angular-oauth2-oidc token storage
---

# OBS-42 - Cross-session "mirroring": investigation

## Report

Adrian booked appointments in two windows in parallel (one a normal
window, one incognito, logged into a different account) and observed the
windows "somehow mirrored" -- form values and/or session appearing to
cross between them. This was also seen as the Playwright-automated
booking form auto-filling with values that had not been typed in it
(phones, a fake SSN, "EMP", "ABC").

## Findings (source-verified)

1. **The app stores NO booking-form state in any shared or
   cross-session storage.** Repo-wide search found no `localStorage` /
   `sessionStorage` / `IndexedDB` writes of form-field data, no
   server-side draft auto-save, and no SignalR/websocket sync for the
   booking form. The only `localStorage` uses are the LeptonX theme key
   (`app.config.ts:112`), logout cleanup (`full-logout.ts`), and the
   OIDC token store. Therefore the app **cannot** mirror form *data*
   across sessions. The autofilled values were browser-level: Chrome
   form autofill / a shared browser profile between the automation and
   the user's window (Playwright's persistent profile or attaching to
   the same Chrome). This is not an application defect.

2. **The one genuine cross-window coupling is the auth session.**
   `angular-oauth2-oidc` stores tokens in `localStorage`, which is shared
   across all tabs/windows of the **same browser profile**.
   `session-identity-watcher.service.ts` subscribes to OAuth events and,
   when the token's `sub` claim changes, calls
   `window.location.reload()` to re-bootstrap (`:62-67`). Consequence:
   logging into account B in a second same-profile tab overwrites the
   shared token and the first tab reloads as B -- the two tabs cannot
   hold two different identities simultaneously. This is standard ABP /
   SPA-OIDC behaviour, not a custom bug.

3. **True incognito is isolated** from the normal profile's localStorage
   and (mostly) its autofill, so a genuinely-incognito second window
   should not have shared either tokens or typed form data with the
   normal window. If mirroring was seen across a real incognito
   boundary, the channel was the browser/OS (shared autofill datastore
   or a non-isolated "incognito"), not app code.

## Risk / appropriateness

- For a PHI app on a shared workstation, the localStorage session model
  means a second login silently hijacks the first tab's session. That is
  the documented, accepted SPA-OIDC trade-off (see the service's own
  header comment), but it is worth flagging for the threat model: there
  is no per-tab session isolation.
- No action required to stop form-data mirroring (the app has no such
  mechanism). If stronger session isolation is desired, that is a
  separate hardening effort (e.g. session-scoped storage, per-tab
  tokens) and out of scope of the booking-bug plan.

## Conclusion

Not an application bug for the form-data symptom. Documented so the
"mirroring" report has a definitive, source-backed explanation and the
session-coupling behaviour is on record.

## Related

- `session-identity-watcher.service.ts` header (Bug D / Issue #107) --
  prior work on cross-identity detection.
- [[BUG-034]] refresh-token-not-revoked-on-rotation -- adjacent session
  lifecycle finding.
