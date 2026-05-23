---
id: OBS-1
title: Register form field inventory (OLD vs NEW)
severity: observation
status: documented
found: 2026-05-13
resolved: 2026-05-22
flow: external-user-registration
---

> **Resolution 2026-05-22.** Field inventory is canonical for the NEW `/Account/Register` form and matches the running OLD app. The discrepancy with the parity audit (`DateOfBirth` + `PhoneNumber` listed as required in both OLD and NEW) was traced to the audit describing **entity-required** columns, not form-required fields. The audit has been amended with a clarifying footnote (see `docs/parity/wave-1-parity/external-user-registration.md`). Future test plans should treat this file as the source of truth for what appears on the register page.

# OBS-1 — Register form field inventory

## OLD `/users/add` form (User Type = Patient)
- User Type (combobox, options Select(disabled)/Patient/Adjuster/PatientAttorney/DefenseAttorney)
- First Name (text)
- Last Name (text)
- Email (text)
- Password (text)
- Confirm Password (text)
- terms-and-conditions link
- "Already have an account? Sign In" link (BOTTOM)

## NEW `/Account/Register` form (User Type = Patient pre-selected pre-fix; placeholder post-[[BUG-004]])
- User Type (combobox, options Patient/Claim Examiner/Applicant Attorney/Defense Attorney)
- First Name (text)
- Last Name (text)
- Email address (text, placeholder `name@example.com`)
- Password (text, with Show password toggle)
- Confirm Password (text)
- terms-and-conditions link
- "Already have an account? Sign In" link (TOP of card, as a heading)
- Language selector (English) — NEW only

## Role-name reconciliations (intentional per `docs/parity/wave-1-parity/_old-docs-index.md`)
- OLD "Adjuster" → NEW "Claim Examiner"
- OLD "PatientAttorney" → NEW "Applicant Attorney"
- OLD "DefenseAttorney" → NEW "Defense Attorney"

## Gap to confirm
`DateOfBirth` + `PhoneNumber` listed in the parity audit `external-user-registration.md` as required form fields for Patient registration, but NEITHER OLD nor NEW shows them on the register page. The audit doc may describe the BACKEND model (where these fields exist on the User entity) rather than the FORM the user fills out.

**Recommended action:** update the audit doc to clarify which fields are form-required vs entity-required, and where the missing fields get captured (probably post-login onboarding profile page).

## Related
- [[OBS-8]] (Firm Name — additional per-role conditional field for AA/DA, surfaced post-PR #197)
