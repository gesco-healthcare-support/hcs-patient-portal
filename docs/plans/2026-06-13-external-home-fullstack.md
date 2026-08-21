---
title: External Role Home - full-stack redesign slice
status: in-progress
date: 2026-06-13
base-branch: feat/frontend-rework
slice-branch: feat/redesign-external-home
prototype: design_handoff_appointment_portal/External Role Home - Redesign.html (ext-after.jsx, ext-after2.jsx, ext-data.js, ext-home.css)
replaces: angular/src/app/home/home.component.* (route '' external)
---

# External Role Home - full-stack slice

Frontend page + the backend deltas it needs, hand-in-hand (per Adrian). Cross-cutting
pieces (notifications feed) are stubbed now and wired in a later slice.

## Backend deltas (only what this page needs)

### B1 - 6-status model: add InfoRequested
- `Domain.Shared/Enums/AppointmentStatusType.cs`: add `InfoRequested = 14`.
- Localization: add `Enum:AppointmentStatusType.14` = "Info Requested" to `en.json`.
- Do NOT yet migrate/remove the legacy values (NoShow/CheckedIn/CheckedOut/Billed,
  the Cancelled/Rescheduled variants) -- full migration (§B6) is broader and not
  needed to render this page. The UI maps every legacy value to a pill (below).
- Regenerate the Angular proxy.

### B2 - list DTO carries claim #, ADJ #, confirmation, type, location
- Read the DTO `AppointmentViewService`/`ListService` returns for `/home`. Add any
  missing of: `confirmationNumber`, `claimNumber`, `adjNumber`, `appointmentTypeName`,
  `locationName`, plus patient `firstName`/`lastName`, `appointmentDate`,
  `appointmentStatus` (already present). Wire through the Mapperly mapper + the
  AppService query (claim/ADJ live on `AppointmentInjuryDetail`; surface onto the
  row DTO). Regenerate the proxy. (§31)

### B3 - notifications: STUB for now
- The bell uses a real feed (§G25) that does not exist. Render the dropdown with an
  empty state ("No new notifications") + a `// TODO(G25)` marker. Build the feed in a
  dedicated notifications slice; it then lights up across all shells.

## Frontend

### F1 - AppExternalNavbar (shared external shell)
Standalone `app-external-navbar` (reused by all external pages). From `ext-after.jsx`
ExtNav: tenant logo slot (BrandingAppService) + "Appointment Portal / Patient & case
portal" tag; notifications icon-button + dropdown (stub feed, unread badge); help
icon-button -> query modal; account dropdown (avatar+name+role, My profile / My
documents / Help / org row / Sign out via performFullLogout). Uses `app-icon`. Styles
ported from ext-home.css `.ext-nav*`/`.ext-pop*`/`.ext-acct*`.

### F2 - ExternalHomeComponent (replaces home.component)
- Hero band ("Welcome back, {first}" + role heroSub) -- gradient per ext-home.css.
- Role-aware quick actions (3D cards): Request an Appointment (canBook),
  Request a Re-evaluation (canReeval). Route to `/appointments/add?type=1|2`.
- Appointments section: title/sub + count; toolbar (search, Filters toggle, cards/table
  view toggle defaulted per role); 6-status segments (All/Pending/Info Requested/
  Approved/Rescheduled/Cancelled/Rejected) with live counts; advanced filter panel
  (type/confirmation/location/status/claim/ADJ/DOI/DOB[role]/SSN); active filter chips;
  cards view (patient default) and table view (attorney/examiner default); empty state.
- Data: existing `AppointmentViewService` + `ListService` (server-side involvement
  filter stays). Search/filters map to `service.filters` + `list.get()`. Reuse the
  existing advanced-search lookups (type/location).
- Actions hit real endpoints: View -> `/appointments/view/:id`; Documents -> the
  documents route; no dead buttons.

### F3 - role config from real identity
`canBook`/`canReeval` from permissions (booking roles) or role set; `showPatientCol`/
`showDob`/`defaultView` from role (patient vs attorney/examiner) per ext-data.js. Derive
from `currentUser.roles` (patient -> cards/no patient col; AA/DA/CE -> table/patient col).

### F4 - status -> pill mapping helper (shared)
`shared/ui/status-pill`: map `AppointmentStatusType` -> the 6 pill kinds:
Pending(1)->Pending; Approved(2)->Approved; Rejected(3)->Rejected;
Cancelled* (5,6,13)->Cancelled(neutral); Rescheduled*/RescheduleRequested (7,8,12)->Rescheduled(info);
InfoRequested(14)->InfoRequested(purple); legacy NoShow(4)/CheckedIn(9)/CheckedOut(10)/Billed(11)->neutral.
Segment bucketing mirrors ext-after.jsx bucketOf.

### F5 - reuse + restyle
- SubmitQueryModalComponent (exists) -> restyle to the prototype `.ext-modal` (PHI warning,
  message + optional confirmation #, char count). Toast service (simple) for action feedback.

## Order
1. B1 (status enum + localization + proxy regen).
2. B2 (list DTO fields + mapper + query + proxy regen).
3. F4 (status->pill helper) + F1 (external navbar shell).
4. F2/F3 (home page + appointments wired to real data).
5. F5 (query modal restyle + toast).
6. Verify: `ng build` clean; `docker compose restart angular`+`api`; load `/home` as each
   external role against real data; run the sign-off checklist.
7. After Adrian's live sign-off: delete old home.component.*, remove the throwaway
   /foundation-preview route + _dev component, clean routes.

## Sign-off checklist (per page)
renders pixel-close to prototype · real data loads · every action hits a real endpoint
(no dead buttons) · permission-gated per role · narrow-width OK · no console errors ·
old component deleted + route cleaned.

## Risk / rollback
- Blast radius: the `/home` external surface + the appointment list DTO (additive fields)
  + one enum value. Rollback: revert the slice branch.
- Backend-verify done: status is enum-only (no lookup migration); list DTO fields TBD at B2.
- Deferred: full §B6 status migration of legacy rows; notifications feed (§G25); the
  booking wizards (next slices) -- the action cards route to the existing add form until then.
