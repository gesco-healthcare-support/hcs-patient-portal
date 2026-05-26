---
title: Open findings pocket-answer cheat sheet
date: 2026-05-25
status: ready
audience: Adrian (presenter)
source: agent audit of docs/runbooks/findings/bugs/*.md (2026-05-25)
---

# Open findings -- demo visibility audit

22 open / open-low / needs-rehydration items classified against the
5 demo flows. Sorted highest -> none visibility.

## High visibility (audience might trip over these)

### OBS-29 -- Cookie banner overlays invite form

**Symptom:** On a clean browser, the GDPR consent banner sits on top
of the `/users/invite` form until accepted.

**Pocket answer:**
> "On a clean browser the GDPR consent banner sits on top of the
> invite form until you click Accept. It's a z-index ordering
> issue, tracked as OBS-29, cosmetic, not a ship-blocker."

**Mitigation:** Click Accept on the cookie banner during pre-demo
setup.

### OBS-28 -- "Send invite" has no success toast

**Symptom:** Click "Send invite" -> invite is sent (DB row created,
Hangfire job enqueued) but no UI feedback fires.

**Pocket answer:**
> "The invite is actually sent -- you can see the row in the
> database and the success card below the form -- but we haven't
> wired the success toast yet. Tracked as OBS-28, one-liner fix
> on the polish list."

**Mitigation:** Show the green success card that DOES appear, just
not as a toast.

### OBS-27 -- "Hi ," empty greeting in invite email

**Symptom:** Invite email greeting interpolates to "Hi ," because
recipient hasn't registered yet.

**Pocket answer:**
> "The invite goes out before the recipient picks their name, so
> the greeting interpolates to empty. Tracked as OBS-27 -- we'll
> drop the greeting line or fall back to 'Hello,'. Cosmetic."

**Mitigation:** Don't open the invite email inbox during demo.

### OBS-25 -- Invitee gets second verify email after registering

**Symptom:** Invitee clicks invite link -> registers -> gets a
second "please verify your email" email.

**Pocket answer:**
> "Right now the invitee clicks the invite link, registers, then
> gets a second 'please verify your email' message. We know it's
> redundant -- the invite click already proved email ownership.
> OBS-25, planned to auto-confirm in
> `InvitationManager.AcceptAsync`."

**Mitigation:** Skip the invite-acceptance demo unless asked.

### OBS-40 -- Console 401 spam on pending-count poll

**Symptom:** Dashboard's pending-count widget polls without
checking auth state -> 401s in DevTools console on the login
screen.

**Pocket answer:**
> "The dashboard's pending-count widget polls without checking
> auth state, so on the login screen you see 401s in DevTools.
> Zero functional impact -- the widget works once signed in.
> OBS-40."

**Mitigation:** Don't open DevTools console during demo. If asked
about console noise, this is the explanation.

### BUG-012 -- Empty Firm Name -> generic error

**Symptom:** AA/DA registration requires Firm Name server-side
but the input does not carry `required`, so submitting empty
falls through to a generic banner.

**Pocket answer:**
> "AA and DA registration requires Firm Name server-side but the
> input doesn't carry the `required` attribute, so submitting
> empty falls through to a generic banner instead of a field-level
> error. Medium severity, BUG-012, fix work-in-progress on a
> dedicated branch."

**Mitigation:** Always type a Firm Name during the registration
demo. Branch `fix/registration-firmname-required-coverage` already
has the conditional-required wired -- live behavior shows the
field IS marked required when Attorney role selected (verified
2026-05-25 in Flow 1 rehearsal).

## Medium visibility

### OBS-39 -- Blank-name seed patient

**Symptom:** Seed contributor creates `AppPatients` row without
First/Last name. Visible if navigating to Patients admin list.

**Pocket answer:**
> "The data-seed for `patient@falkinstein.test` creates a Patient
> row without First/Last name, so the Patients admin list shows a
> blank row at the top. OBS-39, seed-contributor fix is small,
> cosmetic."

**Mitigation:** Don't navigate to /patients admin list.

### BUG-037 -- Clinic Staff cannot upload documents

**Pocket answer:**
> "Clinic Staff role currently doesn't have AppointmentDocuments
> Create perm. BUG-037 -- we'll grant it in the next role-matrix
> revision. For the demo, Staff Supervisor or IT Admin does the
> upload."

**Mitigation:** Use stafsuper1 for the document upload demo, not
clistaff1. Adjust Flow 4 if needed.

### OBS-38 -- Existing-patient dropdown doesn't prepopulate DoB

**Pocket answer:**
> "When you pick an existing patient from the booking dropdown,
> we currently prepopulate name + email but not the date of
> birth. OBS-38, ~10-line TypeScript fix, on the polish list."

**Mitigation:** Don't demo "book a returning patient" as a fresh
booking unless prepared to explain. (Flow 3 uses A00003 which is
already booked, so this only surfaces in optional fresh-booking
demo.)

## Low visibility (only with specific deep-link or fast-click)

- **BUG-038**: `/appointments/add` route missing permissionGuard.
  Only visible if audience deep-links the URL as wrong role.
- **BUG-009**: Lead-time-violating slot -> "internal error". Only
  if approver tries an out-of-bounds slot.
- **BUG-010**: SMTP silent fail on `*.test` recipients. Demo uses
  `@gesco.com` real mailbox -> won't surface.
- **BUG-018**: SMTP misleading error during burst (Exchange rate-
  limit). Single email at a time during demo -> won't surface.
- **BUG-021**: Datepicker race for fast-clicker CE. Slow click ->
  won't surface.
- **BUG-033**: Kind=3 packet cascade failure (>=2 approved AttyCE
  packets in same burst). Demo has 1 patient + 1 booking ->
  unlikely.
- **BUG-036**: Silent packet skip on some types. Already FIXED
  (T1/T2/T3 of the packet-soft-delete-race PR). Visible only via
  DB inspection.

## None visibility (won't surface in 5 demo flows)

BUG-008 (patient PUT/me concurrency), BUG-040 (cumulative trauma
not persisting -- booking modal), BUG-041 (authorized user picker
parity gap -- booking authorized-user modal), BUG-039 (internal
booker uses generated CRUD modal), OBS-26 (slot-gen location
conflict), OBS-30 (tampered email token), OBS-31 (internal booker
no redirect), OBS-32 (booker AA section prefill first name only),
OBS-34 (password-reset UX), OBS-35 (CE scope misses injuryless),
OBS-36 (23 stub templates -- corrected framing per email research),
OBS-37 (Patient role no 403 on Create), OBS-22 (docker watch
misses bind-mount).

## General fallback if asked about anything else

> "Tracked in `docs/runbooks/findings/bugs/` -- every observation
> has an ID, severity, repro steps, and a proposed fix. We
> catalog before we ship."
