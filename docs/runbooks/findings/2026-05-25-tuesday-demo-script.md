---
title: Tuesday demo script -- click-by-click flow
date: 2026-05-25
status: ready
audience: Adrian (presenter), demo viewers
---

# Tuesday demo script

This is the recommended click-by-click flow for the Tuesday-morning
presentation. Every step in this script has been verified live on
`main` during the 2026-05-24..25 hardening sweep. The DB has been
pre-seeded with the demo state described in the
[Sun PM/Mon AM hand-off doc](./2026-05-24-tuesday-demo-prep-handoff.md).

## Pre-flight (5 min before the demo)

1. Confirm Docker is up: `docker compose ps` -- all 7 services
   should show `Up X (healthy)` next to `main-api-1`,
   `main-authserver-1`, `main-angular-1`, `main-sql-server-1`,
   `main-gotenberg-1`, `main-minio-1`, `main-redis-1`.
2. Confirm the demo URLs:
   - SPA: `http://falkinstein.localhost:4200/`
   - AuthServer: `http://falkinstein.localhost:44368/`
   - API: `http://falkinstein.localhost:44327/`
   - Hangfire dashboard:
     `http://falkinstein.localhost:44327/hangfire`
3. Open the SPA in one tab, Hangfire in a second tab. Keep both
   visible during the demo so backend job dispatching is visible.

## The 5 demo flows (~10 min total)

### Flow 1: New-user registration with role-based fields (2 min)

**Story:** "Anyone can register; the form adapts to the role they
pick. Watch the Firm Name field appear when I pick Attorney."

1. Navigate to `http://falkinstein.localhost:44368/Account/Register`.
2. Show the empty form.
3. Pick **User type = Patient**. No Firm Name field.
4. Pick **User type = Applicant Attorney**. **Firm Name field
   appears + is required.** (This is BUG-012 fix #238.)
5. Switch back to Patient (Firm Name disappears).
6. Don't actually submit -- this is the conditional-field demo.

### Flow 2: Existing-data demo (1 min)

**Story:** "Here's the data we've prepared for the demo."

1. Login as `stafsuper1@gesco.com` / `1q2w3E*r` (Staff Supervisor).
2. Show Dashboard:
   - Pending Requests: **1** (A00003)
   - Approved This Week: **1** (A00001)
   - Rejected This Week: **1** (A00002)
   - Various other metrics (Cancelled, Rescheduled, etc. -- W3 not
     yet shipped, so several read 0).
3. Click into the Appointments list. **3 appointments visible**:
   - A00001 Approved AME 2026-06-02
   - A00002 Rejected QME 2026-06-03
   - A00003 Pending Panel QME 2026-06-04

### Flow 3: Approve a pending appointment (3 min)

**Story:** "Here's the clinic-staff approval flow. Watch what happens
in Hangfire when I click Approve."

1. Logout. Login as `clistaff1@gesco.com` / `1q2w3E*r` (Clinic
   Staff).
2. Navigate to `/appointments`. Show A00003 Pending in the list.
3. Click **Actions** > **Review** on A00003. Detail view opens.
4. Click **Approve**. Modal opens.
5. Pick the responsible user (`clistaff1@gesco.com`), add a
   short comment, click **Approve**.
6. Switch to the Hangfire tab. **Watch the packet job appear and
   transition from Enqueued -> Processing -> Succeeded.** 3 packets
   generate (Patient + Doctor + AttyCE), Hangfire shows 4 jobs
   (packet + 3 emails), all Succeeded.
7. Switch back to SPA. Refresh the appointment view -- status is now
   Approved. AppointmentApproveDate populated (BUG-030 fix #239).

### Flow 4: Document upload + packet regeneration (BUG-036 fix demo) (3 min)

**Story:** "We just fixed a tricky bug where regenerating a packet
after an email send would race the DB unique index. Watch the fix
in action."

1. Still on the appointment detail view (A00003, now Approved).
2. Show the packet section -- 3 packet PDFs visible, all Generated.
3. Click **Upload Documents**.
4. Pick a small PDF (the user can use any local PDF; there's
   `.playwright-mcp/test-9mb.pdf` in the repo for stress testing
   the 10 MB cap).
5. Click **Upload**. Document appears in the table.
6. Wait 30 seconds. (The AttyCE retention rule soft-deletes the
   Kind=3 packet row after the AttyCE email send.)
7. Click **Regenerate** packets. Switch to Hangfire tab.
8. **Watch the packet job Succeed on first attempt -- no retries,
   no failures.** Before #247's fix, this would have failed with
   a SQL Server unique-index violation because the soft-deleted
   Kind=3 row blocked the fresh INSERT. With the filtered unique
   index, the INSERT succeeds.
9. (Optional, narrative only) "The fix has 3 layers: filtered unique
   index, OnCompleted deferral, and catch-filter widening. All 3
   are tested and live."

### Flow 5: Invite an external user (1 min)

**Story:** "Staff Supervisors can invite external users (attorneys,
claim examiners) with a one-time tokenized link."

1. Logout. Login as `stafsuper1@gesco.com`.
2. Navigate to `/users/invite`.
3. Show the form: Email + Role dropdown (Patient, Applicant
   Attorney, Defense Attorney, Claim Examiner).
4. Pick **Defense Attorney**, enter a demo email (use
   `demo-da@example.test` to keep it obviously synthetic).
5. Click **Send invite**.
6. Switch to Hangfire tab -- watch the email job dispatch + Succeed.
7. Switch back to SPA -- show the AppInvitations table via direct
   SQL or via the user-management list if the listing UI exists.

## Optional flows (if time)

- **Booking a NEW appointment.** This shows the full 30+-field
  booking form. Takes 5-7 min to walk through. Skip if running short.
- **Reject an appointment.** Reverse of Approve, shows the rejection
  reason capture (BUG-032 fix #236).
- **Password-reset flow.** Shows the dual-partition rate limiter
  fix (BUG-019 / #249) preventing per-IP DoS.

## What NOT to show on Tuesday

- The Notification Templates UI -- doesn't exist yet (would need
  editing 23 stub templates which is OBS-36 territory).
- The Reschedule flow -- W3 feature deferred.
- Mass user creation / dashboard for tenants -- single-tenant
  (Falkinstein) demo only.
- The actual rejection email content for AppointmentBooked /
  AppointmentApproved / AppointmentRejected -- those templates are
  STUBS that will render literally "Stub body for
  AppointmentApproved" if a viewer opens the email. **Hide the
  email tabs in the recipient's inbox during the demo.**

## Known limitations to acknowledge if asked

- "Emails are sent to real inboxes." (No mail-trap container in
  dev.) -- Use `@gesco.com` or `@example.test` synthetic addresses.
- "Some notification template bodies still say 'Stub body for X'."
  Per OBS-36: 23 of 64 templates are TODO. Demo flows that send
  these emails will surface the stub text in the recipient's inbox.
- "Reschedule + cancel features come in W3." -- Documented design,
  not a regression.

## Where to read more

- [Sunday-night hand-off](./2026-05-24-tuesday-demo-prep-handoff.md)
  -- what we landed Sat-Mon, what's verified, what's pending.
- [OBS-32](./bugs/OBS-32-booker-aa-section-prefill-first-name-only.md)
  + [OBS-38](./bugs/OBS-38-existing-patient-no-dob-prepop.md) +
  [OBS-39](./bugs/OBS-39-seed-patient-blank-name.md) -- UI polish
  items that didn't get fixed for Tuesday. Acknowledge if asked;
  schedule for next sprint.
- [BUG-036 finding](./bugs/BUG-036-packet-generation-silently-fails-for-some-appointments.md)
  + [plan](../../plans/2026-05-23-bug-036-packet-fix.md) -- the
  3-layer packet-soft-delete-race fix that Flow 4 demos.
