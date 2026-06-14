# Plan: My Profile redesign (slice)

Status: in-progress
Branch: `feat/redesign-my-profile` (off `feat/frontend-rework`)
Date: 2026-06-13
Approach: `code` (UI slice; live-verify via Playwright, no unit tests -- matches #311/#312/#314)

## Goal

Recreate the redesigned My Profile page from the design handoff
(`My Profile - Redesign.html` / `components/mp-after.jsx` / `styles/mp-profile.css`)
as an Angular standalone component, reusing the legacy profile engine.

## Reuse approach (same `extends` pattern as the wizard/detail)

`PatientProfileRedesignComponent extends PatientProfileComponent` inherits:
- the reactive `form` (all patient controls),
- the two-load topology (`GET /api/app/patients/me` for Patient,
  `GET /api/app/external-users/me` for AA/DA/CE),
- the role discriminator `isExternalUserNonPatient` (from `currentUser.roles`),
- the never-clear SSN contract (SSN nulled on load; empty submit preserves it).

The subclass adds only the `.mp-*` shell + per-section edit/confirm UI state +
State/Language lookups. Buttons use the global `.ap-btn`; fields/modal reuse the
global `.ra-*`/`.ra-modal` (from `_ra-wizard.scss`); the save toast is ABP
`ToasterService`. `body.redesign-shell` is toggled in ngOnInit/ngOnDestroy.

Because the inherited `save()` exposes no completion hook, `confirmSave()`
re-issues the same `PUT /api/app/patients/me` (re-injected `RestService`) so it
can close the editor + toast on success. One PUT persists the whole form.

## Variants (off `isExternalUserNonPatient`)

- Patient: avatar hero + 4 editable cards (Personal / Contact / Address /
  Preferences); each read-only until its Edit; Save -> "Save profile changes?"
  confirm modal -> PUT -> toast. Email is read-only ("Managed by support").
- Attorney / Examiner: one read-only Profile card (Name / Role / Firm / Email)
  + "managed through your firm's account" note. Firm from `/external-users/me`.

Account & Security card (both): Password -> deep-links to the AuthServer
`/Account/Manage`; Email -> "Managed by support" pill. No notification prefs.

## Decisions

- SSN omitted entirely (prototype parity; approved 2026-06-13). SSN stays
  staff-editable on the appointment detail; it is no longer self-editable on
  My Profile (behavior change from legacy).
- "Need an interpreter?" is a UI-only toggle derived from `interpreterVendorName`
  (no backend boolean); selecting "No" clears the vendor on save.
- State + Language render as selects backed by `patients/state-lookup` +
  `patients/appointment-language-lookup` (GUID ids), replacing the legacy text
  inputs.

## Route strategy (strangler-fig)

Temp route `user-management/patients/my-profile-redesign` while legacy
`.../my-profile` stays. After live sign-off: point the external navbar's "My
profile" at the redesign, then delete `PatientProfileComponent` + its template.

## Files

- add `patients/patient/components/patient-profile-redesign.component.{ts,html}`
- add `styles/_mp-profile.scss` + register in `styles.scss`
- `app.routes.ts`: temp route

## Verify

- Live as Patient: each section Edit -> change a field -> confirm -> toast +
  persisted (PUT 200); Cancel reverts.
- Live as Applicant Attorney: read-only Profile card + firm + note.
- `npx ng build --configuration development` clean.
