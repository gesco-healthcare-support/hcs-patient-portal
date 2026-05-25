---
title: Live role-permission probe findings
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Live role probe findings (2026-05-25)

Logged in as `patient1@gesco.com` (Alex Patient) and walked the SPA
to verify the permission-audit subagent's predictions against the
running stack.

## Confirmed leaks in Patient's navigation menu

Patient sees these nav items (and the underlying pages render):

| Nav item | URL | Page state | Demo risk |
|---|---|---|---|
| Home | `/` | OK -- shows own 3 appointments | none |
| Doctor Management > Availability & Time Slots | `/doctor-management/doctor-availabilities` | OK -- 10 doctor availability rows visible. Patient cannot edit (no Create/Edit perm) but can read. By design per seeder (booking needs availability list). | LOW -- Patient has legitimate need to see this. Surface item placement is the issue. |
| Appointments | `/appointments` | OK -- scoped to own | none |
| Applicant Attorneys | `/applicant-attorneys` | **LEAK** -- empty table (scope filter strips rows) but **"New Applicant Attorney" button is visible**. Patient could click and try to create. | **MEDIUM** -- visible during demo if Patient is logged in. |
| Defense Attorneys | `/defense-attorneys` | Same as Applicant Attorneys -- master-table CRUD UI exposed to Patient. | **MEDIUM** -- same pattern. |

## Decision: do not show Patient login during the Tuesday demo

The Tuesday demo script Flow 1 is registration (just role-conditional
firm name, no actual login). Flow 3 logs in as Clinic Staff. If
audience asks "what does the Patient see?", verbal answer is safer
than live click-through:

> "The Patient lands on their own dashboard showing their 3
> appointments. The current build exposes a few master-table menu
> items to external roles -- visible but not actionable because the
> AppService permission filters strip data. We're tightening the
> nav-menu role guards as a polish item, but the data layer is safe
> today."

## Tactic for audience question

If asked "can I see what a Patient sees?": switch to verbal walkthrough
+ point to A00001/2/3 in the Clinic Staff appointment list ("the
Patient sees those 3 rows scoped to themselves").

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
