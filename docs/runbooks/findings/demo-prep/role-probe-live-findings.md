---
title: Live role-permission probe findings
date: 2026-05-25 (revised same-day after deeper inspection)
status: ready -- earlier "leak" claim CORRECTED
audience: Adrian (presenter)
---

# Live role probe findings (2026-05-25, REVISED)

## Correction (2026-05-26 02:12 PT)

The original document below claimed Patient sees a side-nav menu
leak (Applicant Attorneys / Defense Attorneys / Doctor Management).
**That was wrong** -- I queried the DOM and saw entries, but the
entire sidebar is hidden via the `externaluser-role` body class
wired in `app.component.ts:101-111` + `styles.scss:73-92`.

Live verification 2026-05-26 02:12 PT, logged in as patient1:

| Element | DOM state | Visible? |
|---|---|---|
| body class | "lpx-theme-light **externaluser-role**" | -- |
| Sidebar element | in DOM | NO (computed `display: none`) |
| "Applicant Attorneys" link | in DOM | NO (`offsetParent: null`) |
| "Defense Attorneys" link | in DOM | NO (`offsetParent: null`) |

**Patient login during the Tuesday demo is safe.** The sidebar is
hidden; only Home content (with Book Appointment, Book
Re-evaluation, My Appointments table) is visible.

The earlier nav-link extraction returned entries because
`querySelectorAll` finds DOM elements regardless of CSS visibility.
The actual user-facing view does not show the menu items.

## Confirmed leaks in Patient's navigation menu (ORIGINAL CLAIM -- NOW INVALID)

~~Patient sees these nav items (and the underlying pages render):~~

| Nav item | URL | Page state | Demo risk |
|---|---|---|---|
| Home | `/` | OK -- shows own 3 appointments | none |
| Doctor Management > Availability & Time Slots | `/doctor-management/doctor-availabilities` | OK -- 10 doctor availability rows visible. Patient cannot edit (no Create/Edit perm) but can read. By design per seeder (booking needs availability list). | LOW -- Patient has legitimate need to see this. Surface item placement is the issue. |
| Appointments | `/appointments` | OK -- scoped to own | none |
| Applicant Attorneys | `/applicant-attorneys` | **LEAK** -- empty table (scope filter strips rows) but **"New Applicant Attorney" button is visible**. Patient could click and try to create. | **MEDIUM** -- visible during demo if Patient is logged in. |
| Defense Attorneys | `/defense-attorneys` | Same as Applicant Attorneys -- master-table CRUD UI exposed to Patient. | **MEDIUM** -- same pattern. |

## Decision (REVISED): Patient login during demo is SAFE

The sidebar hides automatically. Patient sees only the Home page
with their 3 appointments. Demo as needed.

## Tactic for audience question

If asked "what does the Patient see?": show it live or screenshot
08-patient-home-visible.png. No menu items leak.

## Other live probes attempted

- **Direct navigation as Patient to `/admin`**: not tested (would
  likely 403).
- **Direct navigation as Patient to `/hangfire`**: not tested.
- **Direct navigation as Patient to `/identity/users`**: not tested.

These are out-of-script paths an audience member won't ask to see.
Skip during the demo.

## What was verified clean

- Patient sees scoped Appointments list (own 3 rows, no cross-tenant
  rows -- multi-tenant filter working).
- F4-01 confirmed live: SSN inputs no longer have
  `app-ssn-redacted` class, `-webkit-text-security: none`.
- Welcome banner: "Welcome, Alex (Patient)" -- role label rendered
  correctly.
- No console errors on any Patient page visited.
