---
feature: terms-and-conditions
date: 2026-05-04
phase: 2-frontend (NOT YET IMPLEMENTED in NEW; triggered from registration form)
status: draft
old-source: patientappointment-portal/src/app/components/term-and-condition/term-and-condition.component.html + .ts
new-feature-path: n/a (no T&C component in angular/src/app/)
shell: unauthenticated (modal overlay on registration form)
screenshots: pending
---

# Design: Terms & Conditions

## Overview

The Terms & Conditions modal is a read-only popup that displays the application's T&C
text. It is triggered from the user registration form when a user clicks the "Terms &
Conditions" link (part of the consent checkbox).

In OLD, the modal contains **Lorem Ipsum placeholder text** -- no real T&C content was
ever added. The modal has a single "Close" button; there is no accept/reject action.

In NEW, **this feature is not yet implemented**. The modal must be wired to the
registration form and populated with real T&C text before going live.

**Priority: lowest.** Adrian confirmed: T&C modal will be implemented only after the
primary appointment booking lifecycle is complete and verified.

**Blocker before implementation:** Adrian (or Gesco legal) must supply the actual
T&C text to replace the Lorem Ipsum placeholder.

---

## 1. Route

No dedicated route. T&C is a modal popup triggered from the registration form.

OLD entry point: `RxPopup.show(TermAndConditionComponent)` in `user-add.component.ts`
NEW entry point: `MatDialog.open(TermsConditionsDialogComponent)` from the registration form

---

## 2. Shell

Modal overlay. Accessible from the unauthenticated registration form shell.

---

## 3. OLD Modal Layout

```
+------------------------------------------+
| Terms & Conditions              [X]      |
+------------------------------------------+
| Lorem Ipsum is simply dummy text ...     |
|                                          |
| [second paragraph of Lorem Ipsum]       |
+------------------------------------------+
| [Close]                                  |
+------------------------------------------+
```

Modal class: `modal-lg`. Header background: `--brand-primary`.

**Content:** Two paragraphs of Lorem Ipsum placeholder text. Must be replaced with
real T&C text before going live.

OLD source: `term-and-condition/term-and-condition.component.html:1-28`

---

## 4. Behavior

| Element | Behavior |
|---|---|
| X button (header) | Closes popup via `closePopup()` / `popup.hide()` |
| Close button (footer) | Same -- closes popup |
| No other action | Read-only modal; no Accept / Decline buttons |

The T&C modal is view-only. The consent itself is captured by the checkbox on the
registration form (separate from this popup).

OLD source: `term-and-condition/term-and-condition.component.ts:24-28`

---

## 5. Registration Form Connection

The T&C link is part of the registration form's consent checkbox label:
`[ ] I agree to the [Terms & Conditions]` where the bracketed text is a link/button.

This link triggers the T&C popup. The registration form cannot be submitted unless the
checkbox is checked (separate validation). See `external-user-registration-design.md`
Section 5 for the full registration form field inventory.

OLD source: `user/users/add/user-add.component.ts:popup.show(TermAndConditionComponent)`

---

## 6. Role Visibility

| Role | Can view T&C | Notes |
|---|---|---|
| Unauthenticated (registering) | Yes | Entry point on registration form |
| Authenticated external user | No -- no link on authenticated pages | Can revisit via dedicated URL if needed |
| Internal users | No | Not applicable |

---

## 7. Branding Tokens

| Element | Token |
|---|---|
| Modal header | `--brand-primary` background |
| Header title | white text |
| Close button (footer) | `btn-secondary` |
| X close button | white text via `text-white` |
| Body text | `--text-primary` |

---

## 8. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Lorem Ipsum content | Two paragraphs of Lorem Ipsum placeholder text | Real T&C text from Gesco legal (required before go-live) | Placeholder was never replaced in OLD; must not go live with placeholder |
| 2 | `RxPopup` | `RxPopup.show(TermAndConditionComponent)` | `MatDialog.open(TermsConditionsDialogComponent)` | Framework replacement; visually equivalent |

---

## 9. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `term-and-condition/term-and-condition.component.html` | 1-28 | Modal layout: header, Lorem Ipsum body, Close footer button |
| `term-and-condition/term-and-condition.component.ts` | 1-33 | `closePopup()` -- `popup.hide()` |
| `user/users/add/user-add.component.ts` | (popup.show line) | Entry point: `RxPopup.show(TermAndConditionComponent)` on T&C link click |

---

## 10. Verification Checklist

*(Pending implementation)*

- [ ] T&C link on registration form opens T&C modal
- [ ] Modal title "Terms & Conditions" displayed
- [ ] Modal body contains real T&C text (NOT Lorem Ipsum)
- [ ] X button (header) closes modal
- [ ] Close button (footer) closes modal
- [ ] Modal is read-only -- no Accept/Decline buttons
- [ ] Closing modal returns focus to the registration form
- [ ] T&C modal not accessible to authenticated users (no nav link)
