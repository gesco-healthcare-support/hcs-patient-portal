# Patient Portal -- E2E QA Findings (2026-06-23)

Branch feat/frontend-rework. Driver: Playwright against http://falkinstein.localhost:4250.
Severity scale: blocker > high > medium > low > cosmetic.
Status: COMPLETE (interactive run; Adrian present throughout).

## Top issues (read first)
1. F-017 (HIGH) -- NEW 2026-06-23 (lifecycle round 2). Approving a reschedule DROPS the slot
   time-of-day. The new appointment's AppointmentDate is stored at midnight, so it renders
   "12:00 AM" on the detail header, the my-appointments list, AND the rescheduled-appointment
   confirmation -- instead of the slot time the requester actually picked (e.g. 8:30 AM). Root
   cause: AppointmentChangeRequestsAppService.Approval.cs:276 passes `newSlot.AvailableDate`
   (date-only) as newAppointmentDate without adding `newSlot.FromTime`. Every reschedule produces
   a wrong/midnight appointment time for all parties + packets. Fix size: SMALL.
   FIXED + VERIFIED 2026-06-23 (Approval.cs:276 now `AvailableDate.Date + FromTime.ToTimeSpan()`):
   re-rescheduled A00011 -> child A00018 stored 2026-07-24 10:30 (matches the picked slot). The
   pre-fix A00017 still shows 12:00 AM (screenshot F017-reschedule-midnight-time.png).
2. F-013 (HIGH) -- FIXED + RE-VALIDATED 2026-06-23. Named party / patient got 403 requesting
   reschedule/cancel on a paralegal-booked appointment. Fix uses CanRequestChangeAsync (booker +
   all named parties + Edit-accessor). Round 2 re-validated the PATIENT pathway: patient2 (the
   patient-of-record but NOT the booker) requested a cancel on A00012 -> HTTP 200 (no 403).
3. F-014 (HIGH) -- FIXED + RE-VALIDATED 2026-06-23. Opposing-consent routing now correct in all
   directions: Defense requester -> AA consent (A00008/A00009), Applicant/patient requester ->
   DA consent (A00012), named-DA requester -> AA consent (A00007). All verified end to end via
   the public consent link + supervisor finalize. (Files: AppointmentReadAccessGuard.cs,
   AppointmentChangeRequestsAppService.cs, ChangeRequestSideResolver.cs.) Unit tests still a
   recommended follow-up.
4. F-018 (LOW-MEDIUM, server-side validation) -- NEW. Resubmit (InfoRequested -> Pending) has NO
   server-side check that the staff-flagged fields were addressed; the "fix before resubmit" gate
   is client-side only. An API caller can resubmit an un-fixed appointment. Verified: A00003
   resubmitted (HTTP 204 -> Pending) with the flagged "documents" field still unsatisfied (0 docs).
   Code: AppointmentInfoRequestsAppService.ResubmitAsync (lines 114-132 -- no flagged-field gate).
   Fix size: SMALL. FIXED + VERIFIED 2026-06-23 (ResubmitAsync now calls a new
   GetUnresolvedFlaggedKeysAsync and rejects when any flagged field is still empty / documents
   missing): resubmitting A00015 with the flagged "documents" unmet -> 403 with a friendly
   message; after uploading a PDF -> 204 -> Pending. Legitimate resubmits still pass.
5. F-006 (MEDIUM): Applicant Attorney master FirmName not persisted (Defense is) -- asymmetric.
6. F-011 (MEDIUM): Internal "Booker (identity)" shows the responsible user, not the actual booker.
7. F-009 (candidate): patient email has no required-marker in the wizard but the API rejects empty.
   F-012 (candidate): SSN field is shown to opposing external attorneys (no value tested). Plus
   low/cosmetic F-001..F-016 (see below). F-007 NOT reproducible with real data (see its entry).

## Blockers
(none yet)

## High

### F-013 (HIGH) -- [FIXED + VERIFIED 2026-06-23] attorney-of-record & patient get 403 requesting changes on a paralegal-booked appointment
- Flow: paralegal books on behalf (A00001: booker=paralegal defatty1, attorney-of-record=defatty2);
  attorney defatty2 logs in, sees A00001, clicks Cancel (or Reschedule).
- Repro: login defatty2 -> A00001 (Approved) -> Cancel -> enter reason -> submit.
- Expected (per Adrian's model): "attorneys can view and request changes ... as well as the
  patients." The named attorney-of-record and the patient should be able to request
  reschedule/cancel.
- Actual: POST /api/app/appointment-change-requests/cancel/{id} returns 403 Forbidden. The
  request silently fails (A00001 stays Approved; no change request; console 403 only).
- Root cause: AppointmentChangeRequestsAppService.RequestCancellationAsync /
  RequestRescheduleAsync call EnsureCanEditAsync, which (per its own comment + the
  AppointmentReadAccessGuard EDIT policy) allows ONLY the creator (booker) OR an AccessType.Edit
  accessor. The attorney-of-record (linked by email+role) and the patient (linked by patient
  identity) are neither -> 403. VIEW access is broad (they can see it); EDIT/change access is
  narrow (creator/edit-accessor only) -> mismatch.
- UI mismatch: the external detail SHOWS Reschedule + Cancel buttons to defatty2 (a non-editor),
  so an intuitive action dead-ends in a 403 -- exactly the failure mode Adrian called out.
- Impact: breaks the core book-on-behalf workflow (the ~90% paralegal case). The attorney /
  patient cannot manage an appointment a paralegal booked for them; only the paralegal (or an
  explicitly-added Edit accessor) can. Patient case not yet empirically confirmed but follows
  from the same rule (patient is not the creator).
- Fix size: MEDIUM + a design decision. Need Adrian to define WHO may request changes (named
  attorney-of-record? patient? all parties?), then broaden the change-request edit policy
  (EnsureCanEditAsync / AppointmentReadAccessGuard.CanEdit) to match -- and gate the UI buttons
  on the same rule so non-editors don't see them.

## High (cont.)

### F-014 (HIGH, design) -- [FIXED + VERIFIED 2026-06-23] opposing-consent gating is bypassed/unreachable in the paralegal flow
- Context: ConsentGatingEnabled=true is meant to require the OPPOSING attorney's consent before a
  reschedule/cancel is finalized.
- Repro: booker = paralegal defatty1 (NOT a named party on A00001; defatty2 is the named DA).
  defatty1 requests cancellation -> change-request row created with ChangeRequestType=1,
  RequestStatus=25, but ConsentStatus=0, RequestingSide=NULL, ConsentTokenHash=NULL,
  ConsentExpiresAt=NULL. No consent email/token issued; supervisor can finalize with no consent.
- Root cause: IssueConsentAndNotifyAsync resolves the requester's "side" from their email vs the
  appointment's party emails. The paralegal/booker is not a named party, so RequestingSide can't
  be resolved -> consent is skipped entirely.
- Interaction with F-013 (the catch-22): the ONLY external user allowed to request a change is
  the creator/booker (paralegal), whose request skips consent; the NAMED attorney who WOULD
  trigger consent is blocked by 403 (F-013). Net: in the ~90% paralegal model, the opposing-
  consent feature does not actually gate anything. Consent would only trigger when the booker is
  also a named party (self-book), which is the ~10% path.
- Also note: the appointment STATUS stays Approved (2) while a cancel/reschedule request is
  pending (the CR row tracks RequestStatus=25); the AppointmentStatus enum values
  CancellationRequested(13)/RescheduleRequested(12) appear unused. Minor/by-design, flagging for
  awareness.
- Impact: the consent control that's enabled in prod may be a no-op for most real bookings.
  Needs a design decision (should consent key off the appointment's two firm sides regardless of
  who clicks request?). Pairs with F-013.

### F-017 (HIGH, data correctness) -- [NEW 2026-06-23] reschedule approval drops the slot time-of-day
- Flow: external requests a reschedule to a new slot -> opposing consent -> supervisor
  approve-reschedule -> a NEW appointment is cloned at the new slot, source -> Rescheduled.
- Repro: A00007 (8:30 AM slot) rescheduled to a 8:30 AM slot on Jul 21 -> child A00017.
- Expected: A00017 AppointmentDate = 2026-07-21 08:30 (the picked slot's time).
- Actual: A00017 AppointmentDate = 2026-07-21 00:00 (midnight). UI shows "Time: 12:00 AM" on the
  detail header, "Tue, Jul 21, 2026, 12:00 AM" in Appointment details, and "Jul 21, 2026 12:00 AM"
  in the my-appointments list. The confirmation banner ("Please arrive 15 minutes early") sits over
  a midnight time. (screenshot F017-reschedule-midnight-time.png)
- Proof: original bookings store the slot time in AppointmentDate (A00005/07/08 match their slot
  FromTime); only the rescheduled child is at midnight, while its slot.FromTime IS 08:30.
- Root cause: AppointmentChangeRequestsAppService.Approval.cs:276 --
  `newAppointmentDate: newSlot.AvailableDate` (a date-only DateTime). The slot's time lives in the
  separate `newSlot.FromTime`; the booking flow combines date + time, this path does not.
- Impact: EVERY approved reschedule produces a wrong (midnight) appointment time shown to patient,
  attorneys, CE, and on generated packets -- corrupts the core scheduled-time data. HIGH.
- Fix size: SMALL -- combine AvailableDate.Date + FromTime at line 276 (mirror the booking flow).
- FIXED + VERIFIED 2026-06-23: Approval.cs:276 now `newSlot.AvailableDate.Date + newSlot.FromTime.ToTimeSpan()`.
  Re-verified via a fresh reschedule (A00011 -> A00018): child AppointmentDate = 2026-07-24 10:30
  (== slot FromTime). api rebuilt + restarted clean.

### F-018 (LOW-MEDIUM, server-side validation) -- [NEW 2026-06-23] resubmit not gated server-side on flagged fields
- Flow: staff Send Back flags fields (A00003: cellPhoneNumber + documents) -> external user fixes
  -> "Resubmit to clinic" (InfoRequested -> Pending).
- Expected: server rejects resubmit until the flagged fields are actually addressed (the UI
  disables the button until then -- confirmed in round 1).
- Actual: AppointmentInfoRequestsAppService.ResubmitAsync (lines 114-132) only checks edit-access,
  snapshots values, marks the request resolved, and transitions to Pending -- NO check that the
  flagged fields were filled. Verified: A00003 resubmitted (HTTP 204 -> Pending) with the
  "documents" field still unsatisfied (0 documents on the appointment at resubmit time).
- Impact: an API caller (or a tampered client) can resubmit an un-fixed appointment, defeating the
  Send-Back gate. Integrity, not a security hole (staff re-review on approval). LOW-MEDIUM.
- Fix size: SMALL -- validate the open request's flagged fields are resolved before the transition.
- FIXED + VERIFIED 2026-06-23: ResubmitAsync now calls GetUnresolvedFlaggedKeysAsync (checks each
  flagged scalar field has a value + >=1 document when "documents" is flagged; unknown keys
  fail-open) and throws a UserFriendlyException if any remain. Verified on A00015: resubmit with
  "documents" unmet -> 403; after PDF upload -> 204. Files: AppointmentInfoRequestsAppService.cs.

## Lifecycle round 2 (2026-06-23) -- API bookings to 16 + full lifecycle (all PASS)
- Booking: A00007-A00016 booked via API (faithful: get-or-create patient + create + injury +
  active CE), so each passes the REAL approval gates. 16 total. Booker mix DA 9 (56%) / AA 3 (19%)
  / Patient 1 / CE 2 / staff 1; attorney bookings 92% paralegal (booker != named AoR). A00007-A00012
  approved through the real gates (no SQL shortcuts).
- RESCHEDULE happy path (A00007 -> A00017): named DA defatty2 requested (HTTP 200, no 403) ->
  consent to opposing AA appatty1 -> granted via public link (status 2) -> supervisor
  approve-reschedule (RescheduledNoBill) -> source A00007 = Rescheduled(7), child A00017 = Approved,
  injury + CE cloned, all parties emailed each step. (Surfaced F-017.)
- RESCHEDULE reject (A00008): request -> consent to appatty2 -> appatty2 DENIED (status 3) ->
  supervisor reject-reschedule -> appointment stays Approved. CR status Rejected.
- CANCEL reject (A00009): request -> supervisor reject-cancellation with consent still pending
  (reject is NOT gated on consent -- reasonable) -> stays Approved.
- DIRECT staff cancel (A00010): supervisor POST appointment-approvals/{id}/cancel (CancelledNoBill)
  -> Cancelled, no external request / no consent.
- PATIENT-INITIATED change (A00012): patient2 (patient-of-record, NOT booker) requested cancel ->
  HTTP 200 (no 403, validates F-013) -> consent to opposing DA defatty2 (validates F-014 patient
  routing) -> DA granted -> supervisor approve-cancellation -> Cancelled.
- RESUBMIT completion (A00003): correction (cellPhone, HTTP 204) + document upload + resubmit ->
  Pending -> approve -> Approved. (Surfaced F-018.)
- VOLUME internal: approve A00016, reject A00013, send-back A00014 (-> InfoRequested) -- all 200,
  no 403/500.
- RE-EVALUATION: home "Request a Re-evaluation" routes to /appointments/request?type=2; wizard
  loads with "Follow-up evaluation" context + a "Load prior appointment" lookup; type filter shows
  AME/IME/PQME (all EvaluationType=Both). Re-eval reuses the booking wizard (no distinct create
  path; no DB source-link on the create DTO). Smoke PASS.
- Public consent page (real in-app): /public/change-request-consent/{token} loaded the consent
  info + Yes/No buttons correctly (anonymous). The tenant for that endpoint resolves from the
  SUBDOMAIN (a bare-localhost API call with only __tenant header returns ConsentTokenInvalid) --
  fine in-app since the SPA uses the tenant subdomain; flagged only as an integration note.
- POSITIVE: document upload validates file CONTENT (a text file renamed .pdf -> 400; a real
  %PDF-1.4 file -> 200). Good server-side file-type enforcement.

### F-015 -- CONFIRMED still present (consent page grammar)
- The public consent page reads "A request to **cancellation** appointment A00009 has been
  submitted..." Should be "A request to cancel" (or "A request for cancellation of"). Cosmetic.

## Medium

### F-006 (medium, data consistency) -- Applicant Attorney master FirmName not persisted (Defense is)
- Flow: external registration (self-signup + invite), Phase 1 setup.
- Repro: register an Applicant Attorney with a firm name (appatty1 via API firmName="Bennett
  Lawson Law"; appatty3 via invite UI firm="Rogers Jones Law"). Then inspect masters.
- Expected: AppApplicantAttorneys.FirmName populated, same as AppDefenseAttorneys.FirmName is
  for defense attorneys.
- Actual: AppApplicantAttorneys.FirmName = NULL for both AA accounts, while
  AppDefenseAttorneys.FirmName IS populated for all 3 DA accounts. The firm name IS stored on
  the IdentityUser extension (ExtraProperties.FirmName) for BOTH AA and DA, so it's only the
  AA *master* column that's left unset -- asymmetric with DA.
- Impact: if any query/report/booking-prefill/display reads AppApplicantAttorneys.FirmName
  (rather than the IdentityUser ext prop), applicant firm shows blank while defense firm
  shows. To be confirmed during booking/display. Likely a missed assignment in the AA
  master-creation path vs the DA path.

### F-011 (medium, data accuracy) -- internal "Booker (identity)" shows responsible user, not actual booker
- Flow: internal appointment detail (stafsuper1) > "Internal -- staff only" > Booker (identity).
- Repro: open an APPROVED appointment whose PrimaryResponsibleUserId differs from BookedByUserId
  (A00001: booked by defatty1, approved with responsible user stafsuper1).
- Expected: "Booker (identity)" = defatty1@gesco.com (the actual booker; DB BookedByUserId=defatty1).
- Actual: shows stafsuper1@gesco.com (the responsible user/approver). A00002 (Pending, no
  responsible user) correctly shows its booker (appatty1), so the field appears to bind to the
  responsible user once set, instead of the booker.
- Impact: misrepresents who booked the appointment -- exactly the paralegal/firm audit info that
  matters for the book-on-behalf model. Medium. Likely a small binding fix.

## Positive results (lifecycle + linking SPINE)
- REGISTER-AFTER LINKING (the headline): patient2/appatty2/claimE2 registered AFTER A00002/A00004
  were booked naming their emails. On registration: patient2 master got IdentityUserId set;
  appatty2 got an AA-link row for A00002. Logged in as patient2 -> home shows exactly A00002 +
  A00004 ("of 2 shown") and NOT patient1's A00001/A00003. Retroactive linking + per-role
  visibility/HIPAA scoping BOTH correct. (screenshot 07)
- Internal lifecycle: APPROVE (A00001, A00004-blocked), REJECT (A00002 -> note saved, all
  stakeholders emailed), SEND-BACK/Request-info (A00003 -> Info Requested, requester emailed a
  fix-it link with field checklist).
- Business rules correctly enforced on approve (409 + domain code, not a crash):
  - PQME requires a Panel Strike List document before approval (A00002: ApprovalRequiresPanelStrikeList).
  - Approval requires >=1 injury detail (A00004 API-created w/o injury: ApprovalRequiresInjuryDetail).
    (UI bookings always include an injury, so this only bit the API-minimal create.)
- Reject email subject is rich + clear: "(Patient ... Claim ... ADJ ...) ... rejected by our clinic staff."

## CANDIDATE (verify) -- 409 user-facing message
- The PQME panel-strike-list and injury-detail rules return 409 with a domain code; confirm the
  UI shows a friendly toast (not just a silent console 409). Toast faded before capture.

## Positive results (internal actions)
- Internal appointment detail (Staff Supervisor): full PHI + all party sections + claim info +
  packets + authorized users + internal-only block. Actions: Approve / Reject / Reschedule /
  Cancel / Request info / Edit details / Change log / Demographics.
- Approve A00001: modal requires Responsible User + optional comments; on approve -> status
  Approved, packets generated, "Appointment approved" notice emailed to appatty1, defatty2, claimE1.

## Low / Cosmetic / Dev-tooling

### F-001 (low, dev-tooling) -- dev/delete-test-users soft-deletes and can't clean partial users
- Flow: Phase 1 setup (DB clean via POST /api/public/external-signup/dev/delete-test-users).
- Repro: POST with emails [appatty1, claimE1, defatty1, patient1, defatty2, verify.ce, verify.da].
- Expected: listed test users fully removed so emails are reusable.
- Actual: response `deleted` lists 4 but those AbpUsers rows persist soft-deleted (IsDeleted=1,
  roles+masters stripped); `defatty2` (role NULL, no master) + 2 verify.* returned `notFound`
  and were untouched. Re-registering a soft-deleted email risks a unique-index collision; the
  endpoint cannot clean partial/role-less registrations. Worked around with direct SQL hard-delete.
- Impact: dev-only helper; does not ship. Note for whoever maintains the test harness.

### F-002 (low, cosmetic) -- vestigial "Doctor" concept causes repeated false "missing prerequisite"
- Flow: Phase 0 investigation / any schema inspection.
- Detail: availabilities are decoupled from doctors (AppDoctorAvailabilities has no DoctorId;
  booking path never touches AppDoctors; generate input has no doctorId). AppDoctors is empty
  by design. But the Doctor entity, DbSets, DoctorsAppService/DoctorTenantAppService/
  DoctorPreferredLocations, Doctors/DoctorAvailabilities permission groups, and the "Doctor
  Availabilities" page name all remain. Every QA run rediscovers an empty AppDoctors and
  mistakes it for a blocker.
- Recommendation: finish the removal (drop Doctor* entities/services/permissions, rename
  DoctorAvailability* -> Availability* incl. table) OR document AppDoctors as vestigial.
  Adrian chose note-and-proceed for this run.

### F-004 (low, a11y) -- Generate Slots appointment-type toggles lack aria-pressed
- Flow: Doctor Availabilities > Generate slots.
- Detail: the AME/IME/PQME type toggles track state via a `data-on` attribute but expose no
  `aria-pressed` and no role state; a screen reader user cannot tell which types are selected.
  Selected state is absent from the accessibility tree. Also observed: rapid successive clicks
  on these toggles did not reliably register (a 3-click batch netted all-off) -- possible
  debounce/race; low confidence, noted for follow-up.
- Impact: accessibility gap on a staff-only config screen. Low.

### F-005 (low, cosmetic/UX inconsistency) -- invite form vs self-register form diverge for attorneys
- Flow: Users & Access > Invite External User (vs /Account/Register self-signup).
- Detail: (1) On self-register, selecting an attorney role HIDES First/Last name and shows
  only Firm name. On the invite form, First/Last name stay visible AND a Firm name field is
  added. (2) The Firm name entered on the invite is NOT carried to the invitee's registration
  page (it renders blank; invitee must re-enter). First/Last name ARE carried (seen on the CE
  invite). So firm-on-invite is collected but discarded.
- Impact: inconsistent UX; wasted firm entry on invite. Low.

### F-003 (low) -- stafsuper2 / clistaff2 not seeded
- Flow: Phase 1 setup.
- Expected (per account list): two Staff Supervisors + two Intake Staff.
- Actual: only stafsuper1 (Staff Supervisor) + clistaff1 (Intake Staff) seeded. stafsuper2 /
  clistaff2 absent. Multi-supervisor / multi-intake handoff scenarios can't be tested without
  manual user creation.
- Impact: minor test-coverage limitation; using the single seeded internal of each kind.

### F-007 (low, data consistency) -- dashboard "Requests over time" shows stale count
- Flow: internal dashboard (stafsuper1) on a clean DB.
- Actual: "Requests over time" chart shows 1 received / 1 approved (~May) while stat cards +
  "Status breakdown" all show 0 and there are 0 appointments. Inconsistent aggregate source.
- Impact: misleading metric on an otherwise-empty dashboard. Re-check after real bookings.
- RE-CHECK 2026-06-23 (17 appts): NOT reproducible. Stat cards (pending 2 / approved 7 / rejected
  2), trend "Requests over time" (current week 17 received / 7 completed), and status breakdown
  (sums to 17) are all CONSISTENT now. The earlier phantom "1 in May" was a clean-DB/empty-state
  artifact. Effectively resolved; no code change indicated.

### F-008 (cosmetic) -- external avatar initials wrong for firm names
- Flow: external DA home (defatty1 = "Stone & Perez Defense LLP").
- Actual: avatar shows "SL" (appears to take first + last token incl. "LLP"); expected "SP"
  or "S". Cosmetic; verify across other firms.

## Positive results (working as intended)
- Self-register UX (firm-aware: attorney=firm name, patient/CE=first/last), email
  verification link, and login all work.
- Invite flow: staff invite -> inline link + email -> invite registration (role locked,
  email pre-filled) -> auto-confirmed (no separate verification). Works.
- Availability generation (doctor-less) -> bookable slots -> wizard time dropdown populated.
- Booking wizard (9 steps) end-to-end as DA paralegal: all sections, injury dialog, USPS
  address-standardization dialog, draft autosave, submit -> A00001 Pending.
- LINKING/EMAIL SPINE (A00001): "Appointment Requested" email to=defatty2 (AoR) cc=patient1,
  appatty1, claimE1, stafsuper1, defatty1 (booker). Every party notified. CONFIRMED.

### F-009 (candidate, medium -- VERIFY) -- patient email: no wizard required-marker but API rejects empty
- Flow: booking wizard step 2 (Patient), submit.
- Observed: the get-or-create patient call (`/api/app/patients/for-appointment-booking/get-or-create`)
  returned 400 AbpValidationException (ModelState invalid) when submitted with an empty patient
  email. The patient Email field shows NO required asterisk in the wizard.
- Caveat: in this instance the empty email was a TEST-HARNESS artifact (JS-fill didn't bind the
  email); a normally-typed email submits fine (U1). NEEDS a deliberate empty-email submission to
  confirm whether real users can reach Submit with a blank patient email and hit a raw 400 with
  no friendly client-side validation. If confirmed: client/server required-ness mismatch.

## Positive results (U2 / AA booker A00002)
- PQME selection ENABLES + REQUIRES the Panel Number field (disabled for AME). Correct.
- Demo Clinic South (no availability) shows a clear empty-state message instead of a dead calendar.
- Cumulative Trauma = Yes + multiple body parts (Neck, Right shoulder) accepted.
- Additional Authorized User (accessor): add form (name/email/role/View-Edit rights) works.
- Inline address autocomplete (Smarty) on attorney street + USPS standardization. Works.
- A00002 linking/email: to=appatty2 (AoR) cc=patient2, defatty1, claimE2, appatty1 (booker); plus
  Approve/Reject notifications to BOTH stafsuper1 and clistaff1. All register-after parties emailed.

## Test-harness note
- JS-driven form fill via input events is UNRELIABLE for some controls (email didn't bind -> 400).
  Using Playwright real typing or verifying bound values before submit for remaining UI bookings.

### F-012 (candidate, privacy -- VERIFY) -- SSN field shown to opposing external attorney
- Flow: external attorney detail (defatty2 viewing A00001) > Patient section shows an "SSN" field
  (displayed "Not provided" since none was entered).
- Concern: if a real SSN is on file, is it shown to the OPPOSING defense attorney (and applicant
  attorney / CE)? For a medical-legal claim the parties see claimant identifiers, but full SSN to
  the opposing side may be a privacy/HIPAA concern. Needs a booking WITH an SSN to confirm whether
  the value (vs just the label) renders to each external role. Decision for Adrian on what each
  role should see.

## Positive results (lifecycle continued)
- CANCEL lifecycle (booker-initiated): defatty1 (booker) requests cancel on A00001 -> all parties
  emailed "Cancellation request received" -> supervisor Change Requests queue -> Approve
  cancellation (billing outcome No bill/Late) -> A00001 CancelledNoBill -> all parties emailed
  "Cancellation request accepted". Works end to end.
- Send-back fix-it gating: patient1 on A00003 (Info Requested) sees the staff note; "Resubmit to
  clinic" is correctly DISABLED until the flagged fields (Documents) are addressed. Correct gating.
- Attorney sees paralegal-booked appointment: defatty2 (attorney-of-record) sees A00001 that
  paralegal defatty1 booked (proves the firm/paralegal view side of the model).

## Coverage summary (tested vs not)

TESTED / VERIFIED:
- Setup: clean DB, 57 availabilities, 12 external accounts via 3 paths (4 self-signup, 3 invite,
  5 register-after), internal seeds.
- Registration: self-signup (firm-aware), invite flow, register-after-booking + AutoLink.
- Booking wizard: all 9 steps; AME/IME/PQME; panel-number conditional; both locations + South
  empty-state; single + cumulative-trauma + multi-body-part injuries; accessor add; address
  autocomplete + USPS standardization; required-field validation; patient self-prefill. All 4
  external roles booked (DA/AA/Patient via UI; CE via API).
- Linking + email SPINE: every booking emails all parties; register-after linking + per-role
  visibility/HIPAA scoping (patient2 sees only its 2 appts; defatty2 sees only its 2). CONFIRMED.
- Internal actions: Approve, Reject, Request-info/Send-back; Change Requests queue + Approve
  cancellation. Business rules: PQME needs panel-strike-list; approval needs injury detail.
- Change request: cancel request (booker) -> supervisor approve -> Cancelled.

DRIVEN IN ROUND 2 (2026-06-23) -- see "Lifecycle round 2" section:
- Reschedule request + approve (A00007) and reject (A00008); cancel reject (A00009); direct staff
  cancel (A00010); patient-initiated change (A00012); resubmit completion (A00003); volume
  approve/reject/send-back (A00016/A00013/A00014); re-evaluation wizard smoke; opposing-consent
  happy path (now reachable post-fix) in all routing directions; patient pathway (no 403).

STILL NOT DRIVEN (with reasons):
- No-Show / Check-In / Check-Out / Bill: not implemented in the UI (state-machine only).
- Reschedule via the in-app date-picker UI: driven via the API endpoint instead (the picker needs
  real click/select, a harness limitation, not an app bug). The same AppService path is exercised.

FIDELITY CAVEAT (test method, not a bug):
- Round-2 appointments (A00007-A00017) were booked via API, which stores the party EMAILS but does
  NOT create the attorney LINK records (AppAppointmentApplicantAttorneys / *DefenseAttorneys) that
  the UI booking creates. Consequence: their detail view shows "Not provided" for attorney
  name/firm/email even though the scalar email exists. Consent + notifications work off the scalar
  emails (proven), and approval is not gated on attorney records -- so lifecycle results are valid.
  If a pristine dataset is wanted for demos, the attorney link records can be backfilled via
  POST /api/app/appointment-applicant-attorneys and /appointment-defense-attorneys.
