# 09. Dashboard / notes / submit-query -- OLD vs NEW behavioral parity

## Coverage

Scope: three loosely-related surfaces grouped by the audit plan:

1. **Dashboard (KPI counters)** -- the internal-staff counter board
   (12 cards driven by a single stored proc) plus the external-user
   "home" landing variant (book-appointment CTAs + isAccessor branch).
2. **Internal appointment Notes** -- a per-appointment comment thread
   (`Note` entity with `ParentNoteId` grouping + `EditNoteId`/`IsLatest`
   edit-chain), exposed only to internal staff through a modal launched
   from the internal appointment-detail queue.
3. **Submit-Query / Contact-Us** -- the `UserQuery` entity (500-char
   message), a "Help / Need Question?" modal opened from the top bar by
   external users, and the email-to-staff fan-out that follows submit.

OLD anchors read in full:
- `PatientAppointment.Api\Controllers\Api\Core\DashboardController.cs`
  (`POST /api/Dashboard/post` -> `EXEC spm.spDashboardCounters @UserTypeId`).
- `PatientAppointment.Api\Controllers\Api\Note\NotesController.cs`
  (`GET`/`POST`/`PUT`/`PATCH`/`DELETE` note CRUD).
- `PatientAppointment.Api\Controllers\Api\Lookups\NoteLookupsController.cs`
  (`vNoteLookUp` queryable lookup used to render the thread).
- `PatientAppointment.Api\Controllers\Api\UserQuery\UserQueriesController.cs`
  (`GET`/`GET{id}`/`POST`/`PUT`/`PATCH`/`DELETE`).
- `PatientAppointment.Domain\NoteModule\NoteDomain.cs`
  (Add edit-chain logic: on `EditNoteId>0` the prior row is soft-deleted
  (`StatusId=Delete`, `IsLatest=false`) and a new row inserted; created-date
  + edit-id carried forward across multi-edit chains).
- `PatientAppointment.Domain\UserQueryModule\UserQueryDomain.cs`
  (Add: stamps CreatedBy/Date, looks up the Approved appointment by
  `AppointmentId`, builds a patient/claim/ADJ subject line, sends email to
  the appointment's `PrimaryResponsibleUserId` when a confirmation number
  is present, otherwise broadcasts to all IT-Admins).
- `PatientAppointment.DbEntities\Models\Note.cs`
  (`Comments` required, `ParentNoteId` Range(1..), `EditNoteId` nullable,
  `IsLatest` nullable bool, `AppointmentId` FK, `StatusId`).
- `PatientAppointment.DbEntities\Models\UserQuery.cs` +
  `ExtendedModels\UserQuery.cs` (`Message` required MaxLength(500), `UserId`
  FK; `[NotMapped]` `RequestConfirmationNumber`, `Query`, `AppointmentId`).
- `patientappointment-portal\src\app\components\dashboard\` (component, html,
  service, routing, module) -- the 12 counters + click-through.
- `patientappointment-portal\src\app\components\home\home.component.{ts,html}`
  (external landing: book/re-book CTAs, `isAccessor` branch, an unused
  `searchAppointment(confirmationNumber)` method).
- `patientappointment-portal\src\app\components\shared\top-bar\top-bar.component.{ts,html}`
  (`onAddQuery()` opens `UserQueryAddComponent`; the "Help" link is gated
  `*ngIf="isShowHelp"` which is set true only for ExternalUser).
- `patientappointment-portal\src\app\components\user-query\user-queries\add\user-query-add.component.{ts,html}`
  (the "Need Question?" modal; optional readonly confirmation-number field;
  message textarea; 10-char maxlength on confirmation number).
- `patientappointment-portal\src\app\components\note\notes\` (add/edit/list
  components are ALL `<h1>Note</h1>` stubs; service + domain are real).
- `patientappointment-portal\src\app\components\appointment-request\appointments\info\appointment-info.component.{ts,html}`
  (the REAL notes UI: a "Notes" modal with a `<textarea>`, add/edit/delete,
  per-appointment thread sorted by modified/created date).
- `patientappointment-portal\src\app\components\appointment-request\appointments\detail\appointment-detail.component.ts:202`
  (`NoteRequest(...)` opens `AppointmentInfoComponent`; the launching link
  at `appointment-detail.component.html:138` is COMMENTED OUT).
- `_local\stub-procs.sql:295` (`spm.spDashboardCounters` body is a local
  `WHERE 1=0` stub; real proc lives only in the production DB).

NEW anchors checked:
- `src\HealthcareSupport.CaseEvaluation.Application\Dashboards\DashboardAppService.cs`
  (`GET /api/app/dashboard` -> `DashboardCountersDto`; host vs tenant branch).
- `src\...Application.Contracts\Dashboards\DashboardCountersDto.cs`,
  `IDashboardAppService.cs`.
- `src\...HttpApi\Controllers\Dashboards\DashboardController.cs`.
- `src\...Application.Contracts\Permissions\CaseEvaluationPermissions.cs:7`
  (`Dashboard.Host`, `Dashboard.Tenant`).
- `angular\src\app\dashboard\dashboard.component.ts` (perm-switched host vs
  tenant), `tenant-dashboard\` + `host-dashboard\` (13 cards).
- `angular\src\app\home\home.component.{ts,html}` (external landing: book +
  re-book CTAs, quick + advanced search incl. Confirmation Number, my-
  appointments grid).
- `angular\src\app\shared\components\top-header-navbar\top-header-navbar.component.{ts,html}`
  (declares `showHelp`/`helpClick` API but the HTML renders NO Help button;
  `home.component.html` does not bind `(helpClick)`).
- Grep across `src\` (excluding bin/obj/Migrations) for `Note.cs`,
  `UserQuery`, `SubmitQuery`, `Notes` -- NO `Note` or `UserQuery` entity,
  AppService, DTO, controller, migration, or domain service exists.

### Key structural findings

**1. The NEW dashboard is a deliberate redesign, not a port.** OLD has a
single internal-staff dashboard of 12 counters fed by `spDashboardCounters`
(8 appointment-status counts + 4 external-user-role headcounts), each card
click-through navigating to a pre-filtered `appointment-search` or `users`
list. NEW has a 13-card board split into a Host (cross-tenant) and a Tenant
variant, of which only 5 cards carry live counts and 8 are hard-coded `0`
placeholders. The counter SETS overlap only partially (see the counter map);
this is the single largest behavioral delta in the area.

**2. Notes is a real, functioning OLD feature -- but it was already
disabled in the OLD live UI.** The backend (`NotesController` +
`NoteDomain`) and the modal UI (`appointment-info.component`) are fully
implemented: internal staff could add/edit/delete per-appointment comments
with an author-only edit guard and a soft-delete edit-chain. BUT the only
entry point -- the "Notes" link in the internal appointment-detail grid --
is COMMENTED OUT (`appointment-detail.component.html:138`). So in the OLD
app as shipped, no user could actually open the Notes modal. This makes
Notes a borderline gap: capability existed in code and DB, but was not
reachable by an end user. Recorded as a Partial/latent gap (G-09-04),
flagged for the owner to decide whether to revive it.

**3. Submit-Query (Contact-Us) is fully MISSING in NEW.** OLD external
users had a "Help" link in the top bar that opened a "Need Question?"
modal; submitting wrote a `UserQuery` row and emailed either the
appointment's primary-responsible internal user (when a confirmation
number was supplied) or all IT-Admins (general queries). NEW has the
vestigial `showHelp`/`helpClick` API on `top-header-navbar` but renders
no button, has no `UserQuery` entity, no submit endpoint, no admin list,
and no email trigger.

**4. The OLD `home` confirmation-number search is dead code; NEW's home
search is richer.** OLD `home.component.ts` has a `searchAppointment()`
method bound to `confirmationNumber`, but the `home.component.html`
(25 lines) contains no input wired to it -- the live confirmation-number
search lived on the appointment-request-report-search screen, not home.
NEW's home has a real quick-search + an advanced-search panel that
includes a Confirmation Number field. This is an improvement, not a gap.

## Summary counts

| Class | Count |
| --- | --- |
| Missing behavior | 3 |
| Partial behavior | 3 |
| Intent deviation | 1 |
| Equivalent (different implementation) | 4 |
| OLD-bug (do not port) | 2 |

## Dashboard counter map

OLD's 12 counters come from `dashboard.component.ts:56-67` (keys from
`spDashboardCounters`). NEW's 13 cards come from `DashboardCountersDto`.
Status legend: MATCH = same concept present and live; STUB = present in
NEW but hard-coded 0; MISSING = no NEW card; NEW-ONLY = no OLD equivalent.

| OLD counter (label) | NEW card | Status |
| --- | --- | --- |
| Pending Appointment (`PendingAppointment`) | Pending Requests (`pendingRequests`, live) | MATCH |
| Approved Appointment (`UpcomingAppointment`) | Approved This Week (`approvedThisWeek`, live) | PARTIAL -- NEW scopes to "this week"; OLD was an all-time approved count |
| Rejected Appointment (`RejectedAppointment`) | Rejected This Week (`rejectedThisWeek`, live) | PARTIAL -- NEW scopes to "this week"; OLD all-time |
| Cancelled Appointment (`CancelledAppointment`) | Cancelled This Week (`cancelledThisWeek`, STUB=0) | STUB |
| Rescheduled Appointment (`RescheduledAppointment`) | Rescheduled This Month (`rescheduledThisMonth`, STUB=0) | STUB |
| Checked-In Appointment (`CheckedInAppointment`) | Checked In Today (`checkedInToday`, STUB=0) | STUB |
| Checked-Out Appointment (`CheckedOutAppointment`) | Checked Out Today (`checkedOutToday`, STUB=0) | STUB |
| Billed Appointment (`BilledAppointment`) | Billed This Month (`billedThisMonth`, STUB=0) | STUB |
| Patient (`Patient`) | -- | MISSING (no external-role headcount card) |
| Claim Examiner / Adjuster (`Adjuster`) | -- | MISSING |
| Applicant Attorney (`PatientAttorney`) | -- | MISSING |
| Defense Attorney (`DefenseAttorney`) | -- | MISSING |
| -- | Pending Change Requests (`pendingChangeRequests`, STUB=0) | NEW-ONLY (W3 placeholder) |
| -- | Approaching Legal Deadline (`requestsApproachingLegalDeadline`, live) | NEW-ONLY |
| -- | No-Show This Month (`noShowThisMonth`, STUB=0) | NEW-ONLY (OLD had no NoShow status counter) |
| -- | Total Doctors (`totalDoctors`, host-only) | NEW-ONLY |
| -- | Total Tenants (`totalTenants`, host-only) | NEW-ONLY (multi-tenant concept absent in OLD) |

Net: of OLD's 12 counters, 1 is a clean MATCH, 2 are PARTIAL (period-scoped
re-interpretation), 5 are STUB (card exists but returns 0), and 4 (the
external-role headcounts) are MISSING entirely.

## Behavioral gaps

### G-09-01 -- Dashboard external-role headcount counters dropped
- **Class:** Missing behavior
- **OLD:** `dashboard.component.{ts:64-67,html:119-171}` -- four cards:
  Patient, Claim Examiner (Adjuster), Applicant Attorney (PatientAttorney),
  Defense Attorney; each clicks through to `/users?userRoleTypeId=N`.
- **NEW:** `DashboardCountersDto.cs` + `tenant-dashboard.component.html` --
  no external-role headcount cards exist.
- **What it is:** Live counts of how many external users of each role are
  registered, with click-through to the filtered user list.
- **Why it existed:** Gave staff an at-a-glance roster size and a one-click
  jump into the user-management list filtered by role.
- **What it does + user impact:** Staff lose the registered-user census on
  the dashboard and the one-click navigation into a role-filtered user
  list. They must instead open user management and filter manually.
- **Plain-English:** The old dashboard showed boxes like "Patients: 142,
  Defense Attorneys: 17" you could click to see that list. The new one
  doesn't.
- **Keep in NEW?** Likely yes -- cheap to add as EF `CountAsync` over the
  Identity users-by-role, click-through to the NEW user list. Confirm with
  owner whether headcounts belong on the staff dashboard.

### G-09-02 -- Submit-Query / Contact-Us ("Help / Need Question?") absent
- **Class:** Missing behavior
- **OLD:** `top-bar.component.{ts:56-58,html:79}` ("Help" link, external-
  only) -> `user-query-add.component` modal -> `UserQueriesController.Post`
  -> `UserQueryDomain.Add` (writes `UserQuery`, emails staff).
- **NEW:** `top-header-navbar.component.ts:16,20,27` declares
  `showHelp`/`helpClick`/`onHelpClick` but `top-header-navbar.component.html`
  renders NO Help button and `home.component.html` does not bind
  `(helpClick)`. No `UserQuery` entity / AppService / endpoint anywhere.
- **What it is:** An in-app contact form letting an external user send a
  free-text question (<=500 chars) to the office.
- **Why it existed:** Primary inbound support channel for patients /
  attorneys / claim examiners without a phone call.
- **What it does + user impact:** External users have no in-app way to
  reach the office; the staff lose the inbound query inbox and the
  auto-routed email notification.
- **Plain-English:** The old app had a "Help" button that popped up a
  "send us your question" box and emailed the office. The new app has
  no such button or box.
- **Keep in NEW?** Yes if Contact-Us is in scope. Needs: `UserQuery`
  entity (Message MaxLength 500, optional AppointmentId + confirmation
  number, CreatedBy), submit AppService, the navbar Help button wired to a
  modal, and the email fan-out (see G-09-03 for the routing rule).

### G-09-03 -- UserQuery email routing (primary-responsible vs IT-Admin) absent
- **Class:** Missing behavior
- **OLD:** `UserQueryDomain.cs:77-105` -- if a confirmation number is
  present AND an Approved appointment matches, email goes to that
  appointment's `PrimaryResponsibleUserId` with a subject line built from
  patient name + claim + ADJ; otherwise the query is emailed to ALL
  IT-Admins (`Roles.ItAdmin`). Body rendered from the `UserQuery` email
  template.
- **NEW:** No UserQuery feature, therefore no routing logic.
- **What it is:** Smart routing of an inbound query to the right internal
  owner, falling back to the admin pool.
- **Why it existed:** Appointment-specific questions reach the assigned
  staffer directly; generic questions reach admins.
- **What it does + user impact:** Without it, even if a Contact-Us form is
  added, queries would not auto-route -- a regression vs OLD intent.
- **Plain-English:** The old app sent a patient's question to whoever owns
  their appointment, or to the admins if it was a general question. That
  smart routing is gone.
- **Keep in NEW?** Yes, ship together with G-09-02. The "PrimaryResponsible"
  concept maps to the appointment's owning internal user in the NEW model.

### G-09-04 -- Internal per-appointment Notes (latent in OLD, absent in NEW)
- **Class:** Partial behavior
- **OLD:** `NotesController.cs`, `NoteDomain.cs`, `Note.cs`,
  `appointment-info.component.{ts,html}` -- a "Notes" modal with a textarea,
  add/edit/delete, per-appointment thread, author-only edit
  (`appointment-info.component.html:52` gates edit on
  `parentNoteId == item.createdById`). BUT the only launcher
  (`appointment-detail.component.html:138`) is COMMENTED OUT, so the modal
  was unreachable in the shipped OLD UI.
- **NEW:** No `Note` entity / AppService / UI anywhere.
- **What it is:** Internal staff annotations attached to a specific
  appointment, threaded and editable by their author.
- **Why it existed:** Lets staff record case context on an appointment
  without touching patient-facing fields.
- **What it does + user impact:** Because OLD shipped it disabled, NO live
  OLD user actually used it -- so end-user impact of its absence is
  effectively nil today. The CAPABILITY (and any historical Note rows in
  the OLD DB) is nonetheless lost.
- **Plain-English:** The old app had a half-finished "staff notes" feature
  on appointments that was switched off before release. The new app has
  none of it.
- **Keep in NEW?** Owner decision. Low priority given it was disabled in
  OLD. If revived, it is a clean ABP CRUD (entity + AppService + a notes
  panel on the appointment-view page). Do NOT auto-port the latent code as
  if it were live.

### G-09-05 -- Approved/Rejected counters re-scoped from all-time to "this week"
- **Class:** Partial behavior
- **OLD:** `spDashboardCounters` returns `UpcomingAppointment` and
  `RejectedAppointment` as (per the click-through semantics and labels)
  status totals, not week-scoped subsets.
- **NEW:** `DashboardAppService.cs:85-93` -- `approvedThisWeek` and
  `rejectedThisWeek` filter on `>= lastMondayUtc`.
- **What it is:** The same two cards, but counting a 7-day window vs a
  running total.
- **Why it existed:** OLD showed cumulative pipeline size; NEW shows recent
  throughput.
- **What it does + user impact:** A staffer reading "Approved: 4" in NEW
  sees this-week's approvals, not the total approved backlog OLD showed.
  Different number, different meaning -- can mislead anyone expecting OLD
  semantics.
- **Plain-English:** Two dashboard numbers that used to be "total approved
  / total rejected" now mean "approved / rejected this week".
- **Keep in NEW?** Owner decision on intended semantics. NOTE: the OLD
  source-of-truth proc body is not in the repo (only a stub), so the
  all-time vs windowed distinction is inferred from labels + click-through;
  see Open Questions.

### G-09-06 -- Counter click-through coverage reduced
- **Class:** Partial behavior
- **OLD:** every counter is a deep link -- 7 status counters go to
  `/appointment-search?appointmentStatusId=N`, 4 role counters go to
  `/users?userRoleTypeId=N` (`dashboard.component.ts:73-107`).
- **NEW:** only the 3 live status cards are clickable
  (`tenant-dashboard.component.ts:70-72` -> `/appointments?appointmentStatus=N`).
  The 8 placeholder cards and the host-only totals are not clickable, and
  the 4 role click-throughs are gone with their cards (G-09-01).
- **What it is:** Card-as-navigation behavior.
- **Why it existed:** One-click from a count into the matching filtered list.
- **What it does + user impact:** Fewer dashboard cards act as shortcuts;
  staff must navigate manually for the non-live counts.
- **Plain-English:** In the old app every dashboard box was clickable and
  took you to that list. In the new app only a few boxes are clickable.
- **Keep in NEW?** Naturally resolves as the STUB cards become live; ensure
  click-through is wired when each placeholder is populated.

### G-09-07 -- "Approved" card maps to status filter 2 but uses "upcoming" intent
- **Class:** Intent deviation
- **OLD:** the Approved card key is `UpcomingAppointment` and its
  click-through filters `AppointmentStatusTypeEnums.Approved`
  (`dashboard.component.ts:57,74-75`) -- i.e. "approved" and "upcoming" are
  treated as the same set.
- **NEW:** `approvedThisWeek` counts `AppointmentStatusType.Approved` with a
  week window; click-through navigates to `appointmentStatus=2` (Approved).
- **What it is:** A naming/intent mismatch carried from OLD (the variable
  said "upcoming", the filter said "approved").
- **Why it existed:** OLD conflated "approved" with "upcoming" (no separate
  upcoming/future-date filter existed).
- **What it does + user impact:** Minor -- both apps land on the Approved
  status list. The deviation is that NEW does NOT attempt an "upcoming"
  (future-date) interpretation, and it adds a week window on top.
- **Plain-English:** The old "Approved/Upcoming" box and the new "Approved
  This Week" box both show approved appointments, but the new one is
  scoped to this week and drops the vague "upcoming" idea.
- **Keep in NEW?** Yes -- NEW's clearer "Approved" labeling is preferable;
  confirm the week-window is intended (ties to G-09-05).

## Equivalent (different implementation)

- **E-09-01 -- Counter transport: POST stored-proc vs GET EF.** OLD
  `POST /api/Dashboard/post` body `{UserTypeId}` -> `EXEC spDashboardCounters`.
  NEW `GET /api/app/dashboard` derives host/tenant scope from the caller's
  permissions and counts via EF `CountAsync`. Outcome-equivalent transport;
  the body-vs-permission scoping and POST->GET are expected, not gaps.
- **E-09-02 -- UserTypeId derived from auth, not request body.** OLD passes
  `dashboardModel.UserTypeId` (the SPA sends an empty `{}`, so the proc
  presumably reads identity server-side anyway). NEW derives Host vs Tenant
  from `IsGrantedAsync` on `Dashboard.Host`/`Dashboard.Tenant`. Expected,
  not a gap.
- **E-09-03 -- Home/landing external-user view.** OLD `home.component`
  (book + re-book CTAs, isAccessor branch hiding the CTAs) is functionally
  reproduced by NEW `home.component` (book + re-book CTAs, role-gated layout,
  my-appointments grid). NEW additionally has quick + advanced search; the
  isAccessor "hide book buttons" behavior maps to NEW's role checks
  (`isPatientUser`/`isPatientRole`). Equivalent-or-better.
- **E-09-04 -- Confirmation-number search.** OLD's home
  `searchAppointment(confirmationNumber)` is dead code (no bound input);
  the live OLD confirmation search lived on the report-search screen. NEW
  exposes a Confirmation Number field in the home advanced-search panel
  (`home.component.html:80-87`). Same user capability, cleaner placement.

## OLD bugs (do not port)

- **B-09-01 -- `NoteDomain.Add` null-ref when `EditNoteId` points to a
  missing row.** `NoteDomain.cs:57-58` does
  `oldNoteRecord.EditNoteId` immediately after a `FirstOrDefault()` with no
  null guard; an edit referencing a deleted/absent note throws NRE. Do not
  replicate; if Notes is revived, guard the lookup. (Low real-world risk
  since the feature was disabled in OLD.)
- **B-09-02 -- `UserQueryDomain.Add` null-ref on internal-user email
  lookup.** `UserQueryDomain.cs:79-80` does
  `vInternalUserEmail.EmailId` after a `FirstOrDefault()` with no null guard;
  if the matched appointment's `PrimaryResponsibleUserId` has no
  `vInternalUserEmail` row, the submit throws after the query is already
  persisted (`Commit()` at line 75 runs first). If Contact-Us is revived,
  guard the lookup and fall back to the IT-Admin pool. Do not replicate the
  unguarded access.

## Open questions

1. **`spDashboardCounters` semantics.** The proc body is not in the repo
   (only a `WHERE 1=0` stub at `_local\stub-procs.sql:295`); whether OLD's
   Approved/Rejected/etc. counts were all-time or windowed is inferred from
   the SPA labels + click-through filters. If the production proc is
   available, confirm before locking G-09-05/G-09-07. (Confidence: MEDIUM.)
2. **Are the 4 external-role headcount cards (G-09-01) wanted on the NEW
   staff dashboard?** They are cheap to add but may be redundant with the
   NEW user-management list. Owner call.
3. **Is Contact-Us / Submit-Query (G-09-02/03) in scope for parity?** It is
   a real OLD external-user feature that is entirely absent in NEW. Confirm
   priority before building the entity + email routing.
4. **Should internal Notes (G-09-04) be revived given it shipped disabled
   in OLD?** Capability + historical DB rows exist, but no live OLD user
   reached it. Decide whether to treat it as in-scope parity or drop it.
5. **Host vs Tenant dashboard split** is a NEW multi-tenancy concept with no
   OLD analog (OLD was single-tenant). Confirm this is intended to persist
   through Phase 1 (per CLAUDE.md multi-tenant scaffolding stays wired but
   targets one office) rather than being a parity deviation to unwind.
