---
id: BUG-005
title: Sign Up button enabled before form is valid; OLD disables until fields filled
severity: low
status: fixed
fixed-in: PR #197
found: 2026-05-13
flow: external-user-registration
component: AuthServer wwwroot/global-scripts.js (ensureButtonDisabledBinding)
---

# BUG-005 — Sign Up button enabled before form valid

## Severity
low

## Status
**FIXED in PR #197** — `ensureButtonDisabledBinding` helper added; button disabled until all required inputs populated.

## Affected role
external (Patient / AA / DA / CE)

## Steps to reproduce
1. Navigate fresh to `http://falkinstein.localhost:44368/Account/Register`.
2. Without typing anything, hover Sign Up.

## Expected (OLD parity)
OLD's Sign Up button is `disabled` until First/Last/Email/Password/ConfirmPassword are all populated and User Type is selected.

## Actual (pre-fix)
Sign Up enabled from page load.

## Root cause
`global-scripts.js` had no form-validity → submit-disabled binding.

## Recommended fix (applied in PR #197)
```javascript
function ensureButtonDisabledBinding(form) {
    var submitBtn = form.querySelector('button[type="submit"]');
    if (!submitBtn || submitBtn.dataset.validationHookAttached) return;
    submitBtn.dataset.validationHookAttached = 'true';
    var inputs = form.querySelectorAll('input[required], select[required]');
    function updateState() {
        var allValid = Array.from(inputs).every(i => i.value.trim() !== '');
        if (allValid) submitBtn.removeAttribute('disabled');
        else submitBtn.setAttribute('disabled', 'disabled');
    }
    inputs.forEach(i => { i.addEventListener('input', updateState); i.addEventListener('change', updateState); });
    updateState();
}
```
Called from `init()` after `ensureExtraRegisterFields(form)`. Razor model marks `<select>` + inputs `required` so `[Required]` triggers validation if JS is disabled.

## Related
- [[BUG-012]] (Firm Name still missing `required` attribute even after BUG-005 fix; new field-injection path needs the same treatment)

## Parity doc
`docs/parity/wave-1-parity/external-user-registration.md`
