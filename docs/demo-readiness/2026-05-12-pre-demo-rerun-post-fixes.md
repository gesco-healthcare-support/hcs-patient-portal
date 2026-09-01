---
date: 2026-05-12 (rerun after 2026-05-11 fix sprint)
run-by: Claude (Playwright MCP)
branch: fixes/2026-05-11-demo-blockers
tenant: Falkinstein
flow: chained-invite (Six -> Five via email -> Four via email -> Three by Adrian)
---

# Pre-demo readiness rerun report — 2026-05-12

## Executive summary

**Demo-ready: YES, with one CRLF regression risk and the same Bug B
inbox-side caveat from the prior run.** The 4 fixes shipped on
`fixes/2026-05-11-demo-blockers` all held through a fresh chained-invite
discovery run (Six registered + booked; Five registered via emailed
register-link + booked; Four registered via emailed register-link +
booked; 2 approves + 1 reject + packet UI download all verified end-to-end).
One new sev2 surfaced (register URL in AppointmentRequested-Unregistered
template missing `__tenant=` and `role=` query params). A CRLF regression
on the 3 AppointmentRequested templates was detected at pre-flight (re-
applied as TEMP-UNBLOCK SQL UPDATE) -- root cause is the seed contributor
re-running on db-migrator restart; proper fix is in EmailBodyResources.

## Verified-still-fixed (from 2026-05-11 sprint)

| ID | Bug | Verification on this run |
|---|---|---|
| A | Packet attachment race (tenant context) | A00011 approve: 3 packets generated (Patient 449 KB, Doctor 543 KB, AttyCE 220 KB), 4 attachment emails delivered. A00012 approve: same shape, 4 attachments delivered. No "is not Generated; skipping" warnings. |
| C | CE missing from AppointmentRequested fan-out when booker != CE | All 3 bookings (A00011 booker=Six, A00012 booker=Five, A00013 booker=Four) fired 4 AppointmentRequested emails including the CE leg. A00012/13 show CE leg arriving even though booker is not CE -- proves Bug C frontend fix held. |
| E | Packet download UI returns 500 (Bearer not attached) | UI Download button on A00011 returned the Patient.pdf via the authenticated blob fetch path. |

## New / re-surfaced issues

### sev2 (degrade UX, not demo-blocking)

**ISS-015 (NEW) -- AppointmentRequested-Unregistered template register URL drops `__tenant=` and `role=`.**
Reproduction: any booking that names an external party with no
IdentityUser produces an email with a register link. Both Five (after
A00011) and Four (after A00012) received links of the shape
`http://falkinstein.localhost:44368/Account/Register?email=SoftwareFive%40gesco.com`.
The expected shape (matches the /users/invite endpoint's output) is
`?__tenant=Falkinstein&email=...&role=DefenseAttorney`. The
host-based tenant resolution (`falkinstein.localhost`) still works, so
the missing `__tenant=` is cosmetic, but the missing `role=` forces
the recipient to manually pick Defense Attorney / Applicant Attorney
from the dropdown. Suspected cause:
`BookingSubmissionEmailHandler.BuildRegisterUrl` builds the query
string from tenantName + email but never appends role; possibly also
gets `tenantName=null` from the resolver under some path. Compare
against `ExternalUserController.InviteExternalUser` (the dedicated
invite flow) which builds the full 3-param URL. Recommended fix:
align BuildRegisterUrl with the invite flow.

**ISS-016 (REGRESSION) -- Bug B CRLF fix wiped by db-migrator reseed.**
The 2026-05-11 SQL UPDATE that normalized the 3 AppointmentRequested
template `BodyEmail` columns from LF to CRLF was overwritten when the
db-migrator container re-seeded between sessions. Re-applied as
TEMP-UNBLOCK SQL UPDATE in this run's pre-flight. Proper fix:
`EmailBodyResources.LoadBody` in
`src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/`
returns whatever line endings the embedded resource holds. The HTML
files on disk are CRLF, but somewhere in the MSBuild embedded-resource
pipeline they're being normalized to LF before reaching the runtime
reader. Fix: post-load `body.Replace("\r\n", "\n").Replace("\n",
"\r\n")` to normalize to CRLF on every load. Single-line fix in
EmailBodyResources; future seeds will write CRLF.

**ISS-017 (BY DESIGN, but UX gap) -- AttyCE packet download returns 500
after row pruned post-successful-email.**
Per the retention rule (AttyCE rows persist only on send failure), a
successful AttyCE delivery prunes the row + blob immediately. Any
subsequent download request -- from the UI or the
Book-RealAppointment.ps1 smoke script -- gets 500 with
`EntityNotFoundException`. The UI hides the AttyCE row entirely on
the appointment view (only Patient + Doctor remain), so the demo path
is fine. The smoke script will always fail AttyCE download after the
Bug A fix is correct. Fix: either (a) update
Book-RealAppointment.ps1 to skip the AttyCE download after send (it
expects pre-Bug-A-fix behavior), or (b) map the 500 to a "packet no
longer available" 410 Gone response with a friendlier body for any
UI that surfaces it.

### sev2 (consistent with prior run, still open)

- **ISS-007 to ISS-014** from the 2026-05-11 report remain unchanged
  (location dupes, patient dupes, pagination icons, footer year, etc.).
  None blocking demo.

## Workflow matrix

| Phase | Flow | Status | Notes |
|---|---|---|---|
| 1.A.1 | Six (CE) register at AuthServer | PASS | Direct Register page, manual role pick |
| 1.A.2 | Six books PQME (A00011) | PASS | All 4 roles named; 8 emails fired; A00011 created with ClaimExaminerEmail=SoftwareSix |
| 1.A.3 | Five (DA) register via email link from A00011 | PASS w/ ISS-015 | Email pre-filled; role manually changed from Patient default to Defense Attorney |
| 1.A.4 | Five books PQME (A00012) | PASS | 8 emails fired |
| 1.A.5 | Four (AA) register via email link from A00012 | PASS w/ ISS-015 | Same as 1.A.3 pattern |
| 1.A.6 | Four books PQME (A00013) | PASS | 8 emails fired |
| 1.A.7 | Three (Patient) register | HANDED OFF | Adrian's manual run |
| 1.C | Cross-role visibility (Six/Five/Four) | PASS | Six sees 9 (3 new + 6 historical via email-match), Five sees 3, Four sees 3 |
| 2 | Approve A00011 (Six's booking) | PASS | 5 status emails + 3 packets + 4 attachment emails; Bug A holding |
| 2 | Approve A00012 (Five's booking) | PASS | Same shape |
| 2 | Reject A00013 (Four's booking) | PASS | 4 stakeholder emails delivered |
| 2 | Packet UI download from A00011 | PASS | Patient.pdf downloaded via authenticated blob fetch (Bug E holding) |
| 2 | Invite external user | PASS | Full 3-param URL produced (highlights the ISS-015 differential) |

## TEMP-UNBLOCK patches in place at end of run

```sql
-- ISS-016: Reapply CRLF normalization on AppointmentRequested templates.
-- Will get wiped again on next db-migrator restart until EmailBodyResources
-- normalizes line endings in code.
UPDATE AppNotificationTemplates
SET BodyEmail = REPLACE(REPLACE(BodyEmail, CHAR(13)+CHAR(10), CHAR(10)), CHAR(10), CHAR(13)+CHAR(10))
WHERE TemplateCode IN ('AppointmentRequestedRegistered',
                       'AppointmentRequestedUnregistered',
                       'AppointmentRequestedOffice');
```

No source-tree TEMP-UNBLOCK patches were applied during this run.

## Recommendations for live demo

1. **Use Six / Five / Four for stakeholder demonstration.** Patient
   (Three) is Adrian's manual run -- coordinate so Three's flows
   demonstrate in sequence with the prior set.
2. **For the "register from invite email" story:** show one of the
   chained-invite emails (Five's or Four's) -- the URL works for
   tenant resolution despite missing `__tenant=`, so the demo path
   is fine. If asked about the dropdown default, that's ISS-015.
3. **For packet download:** demo Patient + Doctor downloads from the
   UI (both work). For AttyCE, show the inbox-side attachment landing
   on Four / Five / Six (Bug A fix evidence). Avoid clicking the UI
   AttyCE download button (row pruned post-send by design).
4. **SoftwareFour-junk caveat from prior run still applies** -- if
   the recipient's mail rule routes patientportal@securemailprotocol.com
   to junk, Four's emails land there. Adrian's IT to add safe-sender
   allow rule before demo.

## Fix sprint plan delta from 2026-05-11

Cluster F additions:
- ISS-015 BookingSubmissionEmailHandler.BuildRegisterUrl: add `role=`
  query param and confirm `tenantName` is being passed through; align
  with ExternalUserController.InviteExternalUser as the reference shape.
- ISS-016 EmailBodyResources.LoadBody: post-load CRLF normalization
  (`body.Replace("\r\n", "\n").Replace("\n", "\r\n")`) so seed writes
  consistent CRLF regardless of how MSBuild normalizes the embedded resource.

Cluster G addition:
- ISS-017 AttyCE-post-prune 500: map EntityNotFoundException on the
  packet/download endpoint to 410 Gone (or a 200 with empty body and a
  "Packet no longer available" header) so the UI / scripts get a
  meaningful response. Update Book-RealAppointment.ps1 to not fail on
  410 AttyCE.

## Branch state

Branch: `fixes/2026-05-11-demo-blockers`. No new commits this run -- this
is a discovery rerun, fixes from the prior session held. The TEMP-UNBLOCK
SQL UPDATE was applied directly to the DB (not committed to source).
