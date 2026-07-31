---
id: OBS-8
title: Firm Name is a per-role conditional field (AA + DA only)
severity: observation
status: documented
found: 2026-05-14
resolved: 2026-05-22
flow: external-user-registration
---

# OBS-8 — Firm Name visibility matrix

The Firm Name input on the AuthServer Register page appears for:

| User Type | Numeric role id | Firm Name visible? |
|---|---|---|
| Applicant Attorney | 3 | yes |
| Defense Attorney | 4 | yes |
| Patient | 1 | no |
| Claim Examiner | 2 | no |

This matches OLD parity: attorneys belong to a firm; CE belongs to an insurance carrier captured per-claim, not per-user. Documenting the field-visibility matrix so future test plans don't have to re-discover.

## Code citation (verified 2026-05-22)

The role-check that drives visibility lives in `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js:271-297`:

```js
// OLD parity: Firm Name input shown for the two attorney roles
// (Applicant Attorney = 3, Defense Attorney = 4). Hidden for the
// other roles. Backend ValidateRegistrationInput requires FirmName
// non-empty for attorney roles; submitting an attorney without a
// Firm Name would throw BusinessException.
var firmNameInput = ensureTextInput(form, { id: 'external-firm-name', name: 'FirmName', ... });
...
var isAttorney = role === 3 || role === 4;
var firmWrap = firmNameInput.closest('.form-floating, .mb-2, .mb-3');
if (firmWrap) firmWrap.style.display = isAttorney ? '' : 'none';
```

Submission-side at the same file lines 908-915 reinforces the same matrix server-bound:
```js
const firmName = getFirstValue(form, ['#external-firm-name', 'input[name="FirmName"]']);
const isAttorney = userType === 3 || userType === 4;
const payload = {
  ...
  firmName: isAttorney ? (firmName || null) : null,
  ...
};
```

## Related
- [[BUG-012]] (Firm Name's missing `required` attribute when it does appear) -- in-flight on branch `fix/registration-firmname-required` as of 2026-05-22.
