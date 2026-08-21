---
id: BUG-012
title: AA/DA registration Firm Name field is server-required but missing `required` attribute client-side
severity: medium
status: open
found: 2026-05-14
flow: external-user-registration
component: AuthServer wwwroot/global-scripts.js (ensureExtraRegisterFields) + Application/ExternalSignups (server validation)
---

# BUG-012 — Firm Name client-required attribute missing

## Severity
medium

## Status
**Open** — for fix session.

## Affected role
Applicant Attorney + Defense Attorney (Firm Name doesn't appear for Patient or CE per [[OBS-8]])

## Steps to reproduce
1. Navigate `/Account/Register`.
2. Pick Applicant Attorney (or Defense Attorney) from User Type.
3. Fill First Name + Last Name + Email + Password + Confirm Password.
4. Leave Firm Name blank.
5. Submit.
6. Observe generic *"An internal error occurred during your request!"* banner; no field highlight.

## Expected
- Client-side: `required` attribute on the Firm Name input, so the empty-form-submit-disabled hook from PR #197 ([[BUG-005]] fix) includes it in the gating set.
- Server-side: if a request still arrives without Firm Name, the validation error should surface a localized message like *"Firm Name is required"* via the standard ABP ModelState pipeline, not fall through to the generic exception path (same shape as [[BUG-009]]).

## Actual
- DOM query on the dynamically injected `<input name="FirmName">` returns `required: false`.
- Server rejects the empty Firm Name submit with a generic 500 / 400 that surfaces as the generic banner.

## Evidence
```javascript
// DOM inspection during repro:
{ name: "FirmName", type: "text", label: "Firm Name", visible: true, required: false, value: "" }
```

## Suspected root cause
`wwwroot/global-scripts.js:128-133` injects the Firm Name input dynamically when User Type = AA/DA, but doesn't emit the `required` attribute. The PR #197 `ensureButtonDisabledBinding` only watches `input[required], select[required]` — so Firm Name doesn't gate submit.

Server-side: throws `BusinessException` or similar without a localized DefaultMessage, hitting the [[BUG-009]] pattern.

## Recommended fix
1. In `ensureExtraRegisterFields` (or wherever the Firm Name input is created), set `firmNameInput.required = true;`.
2. Server-side: ensure the AA/DA validation path throws `UserFriendlyException` with a localized "Firm Name required" message (or use ABP's standard ModelState + DataAnnotations).

## Related
- [[BUG-005]] (empty-form-submit-disabled hook)
- [[BUG-009]] (BusinessException auto-localization gap)
- [[OBS-8]] (Firm Name visibility matrix)
