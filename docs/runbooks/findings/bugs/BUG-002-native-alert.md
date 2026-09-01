---
id: BUG-002
title: Register-error uses native window.alert() instead of in-app message
severity: medium
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: external-user-registration
component: AuthServer Razor Account/Register page + wwwroot/global-scripts.js
---

# BUG-002 — Register-error uses native window.alert()

## Severity
medium

## Status
**FIXED in PR #197** — `notifyRegisterFailure` now writes to inline `#external-signup-error` banner; native `alert()` removed.

## Affected role
Patient (and all external-user registration)

## Steps to reproduce
1. Trigger any register error (e.g. submit duplicate email per [[BUG-001]] steps).
2. Observe error UI.

## Expected
OLD shows a styled in-page banner: *"Your registration is successfully done, please verify your email to login."* (success case). Error case should use the same styled banner pattern. Native `alert()` is jarring, blocks the UI thread, and is not styleable/localizable.

## Actual (pre-fix)
Native `window.alert()` dialog with the raw server message. No styled in-page error appeared after the alert was dismissed; the form just re-enabled.

## Evidence
- Playwright trapped the dialog: `Modal state: ["alert" dialog with message "Email address is already used: SoftwareThree@gesco.com"]`.

## Root cause
`wwwroot/global-scripts.js:80` — `notifyRegisterFailure` called `alert(message)` on top of the already-styled inline `#external-signup-error` banner placeholder.

## Recommended fix (applied in PR #197)
Drop the `alert()` call. `showInlineRegisterError` (line 55-67) already renders a styled `.alert.alert-danger` banner — keep using that.

## OLD source
`P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.ts` (toast/banner wiring)

## Parity doc
`docs/parity/wave-1-parity/external-user-registration.md`

## Related
- [[BUG-001]] (user enumeration — root cause of message text)
