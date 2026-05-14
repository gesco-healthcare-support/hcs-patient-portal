---
id: OBS-1
title: Register form field inventory (OLD vs NEW)
severity: observation
found: 2026-05-13
flow: external-user-registration
---

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
