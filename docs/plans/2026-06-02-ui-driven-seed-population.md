---
feature: ui-driven-seed-population
date: 2026-06-02
status: draft
base-branch: main
related-issues: []
---

## Goal

Populate the Falkinstein tenant with realistic, varied synthetic traffic entirely
through the live UI (Playwright MCP) -- no SQL, no direct API -- covering every role,
all 6 appointment types, and the full appointment lifecycle, so the database looks
like genuine production usage.

## Context

Verified live on the `main` Docker stack (2026-06-02), NOT from runbooks:

- Tenant: **Falkinstein** + host only. No other tenants.
- Baseline counts: **0 doctors, 0 slots, 0 appointments, 1 patient** (`patient@falkinstein.test`).
- Master data seeded: 50 states, **6 appointment types** (QME, Panel QME, AME,
  Record Review, Deposition, Supplemental Medical Report), 12 languages, 7 WCAB
  offices, **2 locations** (Demo Clinic North/LA, Demo Clinic South/San Diego),
  1 SystemParameter row.
- Ports: main = Angular 4200 / AuthServer 44368 / API 44327; replicate stack = +30
  (no contention). Login flow: SPA -> AuthServer Razor at `falkinstein.localhost:44368`;
  subdomain carries the tenant; internal users land on `/dashboard`.
- Grids are **ngx-datatable** (`datatable-body-row`), not `<table>`.

Constraints / decisions:

- **No SQL, no direct API.** All data created by driving the live SPA + AuthServer as
  a real user via Playwright MCP.
- **Password `1q2w3E*r`** for every account (existing + created). The 7-char `1q2w3E*`
  fails the `RequiredLength=8` policy.
- **No doctor** is created (not needed for slots or booking; explicitly declined).
- Fill **every** field with realistic synthetic data. No "test"/"example" in names or
  emails; role-tagged local-parts (`patient1@...`) are fine. Exhaust the 12 real
  accounts before inventing synthetic named parties.
- **Slot generation** requires Staff Supervisor / admin (Clinic Staff cannot).
- **Internal-user creation** requires IT Admin (`it.admin@hcs.test`); the UI emails a
  temp password and forces a reset on first login -> must reset to `1q2w3E*r`.
- External bookings land **Pending**; internal-staff bookings **auto-approve**.

Accounts provided (all `1q2w3E*r`):

| Role | Accounts | Seed status |
| --- | --- | --- |
| Patient (external) | patient1@gesco.com (Daniel Harper), patient2@gesco.com (Olivia Turner) | new -> self-register + verify |
| Applicant Attorney | appatty1@gesco.com (Marcus Bennett), appatty2@gesco.com (Tiffany Lawson) | new -> self-register + verify |
| Defense Attorney | defatty1@gesco.com (Gregory Stone), defatty2@gesco.com (Alicia Perez) | new -> self-register + verify |
| Claim Examiner | claimE1@gesco.com (Henry Caldwell), claimE2@gesco.com (Jasmine Reid) | new -> self-register + verify |
| Staff Supervisor | stafsuper1@gesco.com (Patrick O'Neal), stafsuper2@gesco.com (Denise Fowler) | stafsuper1 ALREADY SEEDED; create stafsuper2 |
| Clinic Staff | clistaff1@gesco.com (Rachel Kim), clistaff2@gesco.com (Luis Mendoza) | clistaff1 ALREADY SEEDED; create clistaff2 |

Email verification links: Adrian clicks them in the real inbox or relays them to the
session.

## Approach

Drive the live SPA + AuthServer via Playwright MCP, role by role, in dependency order:
build a fill helper -> generate slots -> create/verify accounts -> book appointments ->
advance lifecycle -> upload documents. Build a reusable, parametrized appointment-form
fill routine first (after a deep study of `/appointments/add` and its modals) so the
~50-80 bookings are fast and consistent. Generate volume primarily through internal
staff (auto-approved) and the real external accounts (Pending -> approved), filling out
breadth with synthetic free-text parties.

Rejected alternatives:

- SQL / `Master-Seed.ps1` seeding -- violates the "UI only" goal (the whole point is to
  simulate real user traffic, not bulk-insert).
- Creating a Doctor entity -- not needed for slots/booking and explicitly declined.
- Trusting runbook credentials/state -- stale; verified live against code + the app.

## Tasks

- T1: Deep-study `/appointments/add` + build a reusable fill helper.
  - approach: code
  - files-touched: [scripts/seed/ (working dir, gitignored)]
  - acceptance: a parametrized routine that fills every section (Schedule, Patient
    Demographics, Authorized Users, Employer, Applicant Attorney, Defense Attorney,
    Claim Information injury modal, Custom Fields) and correctly handles the masked SSN
    control, the date pickers, the attorney enable/disable toggles, and any USPS
    address-confirm dialog; validated against the live form without submitting.
- T2: Generate availability slots (as supervisor@falkinstein.test or stafsuper1).
  - approach: code
  - acceptance: hundreds of future-dated Available slots across both locations and all
    6 appointment types; the booking-form slot picker shows options for each type.
- T3: Create 2 internal users (stafsuper2, clistaff2) as IT Admin; reset temp passwords.
  - approach: code
  - acceptance: both log in with `1q2w3E*r` and land on the dashboard with the correct role.
- T4: Register + verify 8 external users (2 each Patient/AA/DA/CE) via `/Account/Register`.
  - approach: code
  - acceptance: all 8 EmailConfirmed and able to log in (Adrian confirms/relays links).
- T5: Create appointment volume (~50-80) across all 6 types.
  - approach: code
  - acceptance: ~50-80 appointments; spread across all types; mixed bookers (staff
    auto-approved + external Pending); real accounts exhausted before synthetic parties;
    every section populated with realistic, varied synthetic data.
- T6: Drive the status lifecycle.
  - approach: code
  - acceptance: realistic status distribution -- approve most, reject some (with reasons),
    check-in/out + bill a subset, raise + action a few cancel/reschedule requests;
    dashboard counts reflect it.
- T7: Upload documents where the driver allows; stage the rest for manual upload.
  - approach: code
  - acceptance: a subset of appointments have uploaded documents; any blocked uploads are
    staged in a `downloads/testing/` folder with a manifest for Adrian to upload.

## Risk / Rollback

- Blast radius: data only, single dev tenant (Falkinstein) on the main Docker stack. No
  code or schema changes; nothing touches production.
- Rollback: `docker compose down -v && docker compose up -d --build` re-seeds the clean
  baseline. No SQL cleanup required.
- Risks: email verification depends on real-inbox delivery (Adrian-in-the-loop); the
  PUT /patients/me concurrency quirk on a failed booking retry (mitigated by reloading the
  form between attempts); SSN/datepicker/modal automation fragility (mitigated by the T1
  study); all creation timestamps are "today" (accepted realism limitation -- variety
  comes from appointment dates, statuses, and data).

## Verification

- As Clinic Staff, confirm the dashboard counts reflect the created volume
  (pending / approved / rejected / billed / checked-in / change-requests).
- Spot-check several appointments across types: all sections populated, parties and
  injuries varied, statuses correct.
- Log in as 1-2 real external accounts; confirm each sees only their linked appointments
  with correct data.
- Confirm no real PHI and no "test"/"example" tokens -- the data reads like genuine traffic.
