---
id: BUG-004
title: User Type pre-selects Patient on NEW; OLD requires explicit selection
severity: low
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: external-user-registration
component: AuthServer wwwroot/global-scripts.js (ensureUserTypeSelect)
---

# BUG-004 — User Type pre-selects Patient

## Severity
low

## Status
**FIXED in PR #197** — disabled placeholder `"Select a user type"` prepended to the role option list.

## Affected role
external (Patient / AA / DA / CE)

## Steps to reproduce
1. Navigate fresh to `http://falkinstein.localhost:44368/Account/Register`.
2. Observe the User Type dropdown.

## Expected (OLD parity)
OLD shows a disabled "Select" placeholder option first, requiring conscious role selection (Patient / Adjuster / PatientAttorney / DefenseAttorney). Forces deliberate selection.

## Actual (pre-fix)
Patient pre-selected. A user clicking Sign Up without changing it silently registers as Patient even when they meant Applicant Attorney etc. Role gates downstream (AA-only JDF upload, DA-only doc paths) make the silent default a UX trap.

## Root cause
`wwwroot/global-scripts.js:128-133` — `ensureUserTypeSelect` appended 4 role `<option>` elements with no leading disabled placeholder, so the browser auto-selected the first (Patient).

## Recommended fix (applied in PR #197)
```javascript
const placeholderOption = document.createElement('option');
placeholderOption.value = '';
placeholderOption.textContent = 'Select a user type';
placeholderOption.disabled = true;
placeholderOption.selected = true;
select.appendChild(placeholderOption);
externalUserRoles.forEach(...);
```
Submit handler now rejects empty role explicitly.

## OLD source
`P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.html` (User Type select)

## Parity doc
`docs/parity/wave-1-parity/external-user-registration.md`
