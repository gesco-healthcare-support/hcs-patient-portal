---
feature: terms-and-conditions
old-source:
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\term-and-condition\term-and-condition.component.{ts,html}
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: external (registration flow)
depends-on: []
required-by:
  - external-user-registration   # T&C displayed during signup
---

# Terms and Conditions

## Purpose

Static popup component displaying the Terms and Conditions. Used during external-user registration -- user must view (and likely accept via checkbox in the parent form, TO VERIFY) before completing registration.

**Strict parity with OLD.**

## OLD behavior (binding)

### Component (`term-and-condition.component.ts`)

Per the file (33 lines) read on 2026-05-01:

- A popup component (`RxPopup`-based modal).
- `showComponent: bool` flag for render gate.
- `closePopup()` method dismisses.
- No backend interaction in this component itself -- pure display.
- Terms text lives in `term-and-condition.component.html` (TO READ during impl).

### Acceptance tracking

OLD does NOT appear to track per-user acceptance in the DB schema. There is no `TermsAcceptedDate` field on the `Users` table. Strict parity: do not add tracking unless requested as a future feature.

The likely flow:

1. Registration form has a checkbox: "I accept the Terms and Conditions".
2. Checkbox label has a "(view)" link that opens this popup.
3. Submit-button is disabled until the checkbox is checked.
4. The checkbox state is NOT persisted to the DB (or persisted only as a transient form-validation step).

### Critical OLD behaviors

- **Display only.** No DB tracking of acceptance.
- **Static content.** Terms text is hard-coded in the HTML; not editable through any admin UI.
- **Modal style.** Not a separate route.

## OLD code map

| File | Purpose |
|------|---------|
| `patientappointment-portal/src/app/components/term-and-condition/term-and-condition.component.ts` | Popup component |
| `patientappointment-portal/src/app/components/term-and-condition/term-and-condition.component.html` | Static T&C content |

## NEW current state

- TO VERIFY: NEW has a T&C component in `angular/src/app/`.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| T&C popup component | OLD | TO VERIFY | **Add Angular standalone modal component** with the T&C text | I |
| Checkbox + link in registration form | OLD UI | TO VERIFY | **Add `acceptTerms` form control with required validator** + label link triggering modal | I |
| T&C content storage | OLD: hard-coded HTML | -- | **Strict parity:** keep hard-coded in NEW Angular template OR move to `Domain.Shared/Localization` for easier editing. **Recommend localization** -- IT can update without code change. Document as parity-friendly improvement (not a behavior change). | C |
| Acceptance tracked in DB | OLD: NO | -- | None -- match OLD | -- |
| Configurable via IT Admin UI | OLD: NO | -- | None -- not in OLD spec | -- |

## Internal dependencies surfaced

- None.

## Branding/theming touchpoints

- Modal styling (logo, primary color, font).
- T&C content -- per-tenant since each clinic may have different terms.

## Replication notes

### ABP wiring

- Standalone Angular `TermsAndConditionsModalComponent`.
- Triggered from registration form via `MatDialog` (or `NgbModal`) reference.
- Content from `Domain.Shared/Localization/CaseEvaluation/en.json` key `Terms:Body` -- per-tenant override possible via ABP localization.

### Verification

1. Open registration form -> see T&C checkbox + (view) link
2. Click (view) link -> modal opens with terms text
3. Close modal -> back to form
4. Try to submit without checkbox -> validation error
5. Check checkbox + submit -> success
