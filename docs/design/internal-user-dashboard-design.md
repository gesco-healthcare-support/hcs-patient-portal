---
feature: internal-user-dashboard
date: 2026-05-04
phase: 2-frontend (backend IDashboardAppService + DashboardCountersDto exist; tenant-dashboard Angular component implemented with 5 live counters + 8 placeholders)
status: draft
old-source: patientappointment-portal/src/app/components/dashboard/dashboard.component.ts + .html (113/330 lines)
new-feature-path: angular/src/app/dashboard/ (host-dashboard + tenant-dashboard components)
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: partial (old/admin/01-dashboard.png, old/staff/01-dashboard.png, old/supervisor/01-dashboard.png; new/admin/02-dashboard.png, new/t1-doctor/02-tenant-dashboard-placeholder.png)
---

# Design: Internal User -- Dashboard

## Overview

The dashboard is the landing page for all internal users after login. It shows counter
cards for pending work and navigates to filtered list pages on click.

In OLD, a single `DashboardComponent` serves all internal roles with 12 counter cards
(8 appointment-status counters + 4 user-type counters). Role-scoping is applied at the
data layer (Clinic Staff sees their assigned appointments; Supervisor sees all).

In NEW, the dashboard is split into two role-specific components:
- **Host Dashboard** (IT Admin / ABP Host role): ABP commercial analytics widgets
  (error rate, execution duration, editions usage, latest tenants). No appointment counters.
- **Tenant Dashboard** (Clinic Staff / Staff Supervisor / Doctor): 13 counter cards.
  5 return live data; 8 are placeholder cards pending day-of-exam state implementation.

---

## 1. Routes

| | OLD | NEW |
|---|---|---|
| Dashboard route | `/dashboard` | `/dashboard` |
| External user home | `/home` (separate component) | `/home` (separate component) |

Guards:
- OLD: `canActivate: [PageAccess]` with `applicationModuleId` for dashboard.
- NEW: Root `DashboardComponent` uses `*abpPermission` directives:
  - `CaseEvaluation.Dashboard.Host` → renders `<app-host-dashboard>`
  - `CaseEvaluation.Dashboard.Tenant` → renders `<app-tenant-dashboard>`

---

## 2. Shell

Internal-user authenticated shell (side-nav + top-bar).
The dashboard is the default landing page after login for all internal roles.

---

## 3. OLD Dashboard (Single Component -- All Internal Roles)

### 3a. Layout

Responsive Bootstrap grid: `col-sm-6 col-xl-3` (4 cards per row on desktop, 2 per row on tablet).

```
+-----+-----+-----+-----+
|  1  |  2  |  3  |  4  |
+-----+-----+-----+-----+
|  5  |  6  |  7  |  8  |
+-----+-----+-----+-----+
|  9  | 10  | 11  | 12  |
+-----+-----+-----+-----+
```

### 3b. Counter Cards

**Row 1 -- Appointment Status:**

| # | Label | Counter field | Click route |
|---|---|---|---|
| 1 | Pending Appointment | `pendingAppointment` | `/appointment-pending-request` |
| 2 | Approved Appointment | `approvedAppointment` | `/appointment-search?status=Approved` |
| 3 | Rejected Appointment | `rejectedAppointment` | `/appointment-search?status=Rejected` |
| 4 | Cancelled Appointment | `cancelledAppointment` | `/appointment-search?status=Cancelled` |

**Row 2 -- More Appointment Statuses:**

| # | Label | Counter field | Click route |
|---|---|---|---|
| 5 | Rescheduled Appointment | `rescheduledAppointment` | `/appointment-search?status=Rescheduled` |
| 6 | Checked-In Appointment | `checkInAppointment` | `/appointment-search?status=CheckedIn` |
| 7 | Checked-Out Appointment | `checkOutAppointment` | `/appointment-search?status=CheckedOut` |
| 8 | Billed Appointment | `billedAppointment` | `/appointment-search?status=Billed` |

**Row 3 -- User Counts:**

| # | Label | Counter field | Click route |
|---|---|---|---|
| 9 | Patient | `patientCount` | `/users?userRoleTypeId=Patient` |
| 10 | Claim Examiner | `claimExaminerCount` | `/users?userRoleTypeId=Adjuster` |
| 11 | Applicant Attorney | `applicantAttorneyCount` | `/users?userRoleTypeId=ApplicantAttorney` |
| 12 | Defense Attorney | `defenseAttorneyCount` | `/users?userRoleTypeId=DefenseAttorney` |

**Counter card anatomy:**
- Top-left: lnr icon (e.g., `lnr-apartment`, `lnr-users`)
- Center: large counter number (e.g., "42")
- Bottom: label text (e.g., "Pending Appointment")
- Color: success (green) / info (blue) / danger (red) / warning (orange) per card

All 12 cards are clickable. Click calls `counterClicked(value)` which routes to the
corresponding filtered list using query params.

OLD source: `dashboard/dashboard.component.html:1-330`

### 3c. Role-Scoped Filtering

- **Clinic Staff:** Sees only appointments where `PrimaryResponsibleUserId == currentUserId`.
- **Staff Supervisor / IT Admin:** Sees all appointments (no user filter).
- User-count cards (9-12): visible to Staff Supervisor and IT Admin; hidden from Clinic Staff.

OLD source: `dashboard/dashboard.service.ts` (role-aware query in POST body)

### 3d. OLD API Endpoint

`POST /api/Dashboard/post` -- single round-trip returning all counters.

---

## 4. NEW Host Dashboard (IT Admin)

ABP commercial analytics widgets; no appointment-specific counters.

```
+-------------------------------------------------------+
| [H2] Dashboard                                       |
| [Date Range Picker]                                  |
+---------------------------+---------------------------+
| Error Rate Widget         | Average Execution Duration|
+---------------------------+---------------------------+
| Editions Usage Widget     | Latest Tenants Widget     |
+---------------------------+---------------------------+
```

This is the ABP commercial default host dashboard. It shows ABP SaaS-level metrics,
not patient portal operational counters. Appointment operations are not visible here.

NEW source: `dashboard/host-dashboard/host-dashboard.component.html`

---

## 5. NEW Tenant Dashboard (Clinic Staff / Staff Supervisor / Doctor)

### 5a. Layout

Bootstrap grid: `col-md-3 col-sm-6` (4 cards per row on desktop).
Placeholder cards styled with `.placeholder-card` CSS class.

```
+--------+--------+--------+--------+
| Pend.  | Approv.| Reject.| Pend.  |
| Req.   | Week   | Week   | Change |
+--------+--------+--------+--------+
| Legal  | Billed | No-Show| Resched|
| Deadl. | Month* | Month* | Month* |
+--------+--------+--------+--------+
| Cancel.| Check  | Check  | Total  |
| Week*  | In*    | Out*   | Doctor*|
+--------+--------+--------+--------+
(* = placeholder, always 0 until day-of-exam states ship)
```

### 5b. Counter Cards

**Live counters (5 cards):**

| # | Label | DTO field | Notes |
|---|---|---|---|
| 1 | Pending Requests | `pendingRequests` | Appointments with status Pending |
| 2 | Approved This Week | `approvedThisWeek` | Approved in current calendar week |
| 3 | Rejected This Week | `rejectedThisWeek` | Rejected in current calendar week |
| 4 | Pending Change Requests | `pendingChangeRequests` | Reschedule/cancel requests awaiting staff action |
| 5 | Approaching Legal Deadline | `requestsApproachingLegalDeadline` | CCR Sec. 31.5: appointments within 60 days of statutory deadline |

**Placeholder cards (8 cards -- always return 0 until implemented):**

| # | Label | DTO field |
|---|---|---|
| 6 | Billed This Month | `billedThisMonth` |
| 7 | No-Show This Month | `noShowThisMonth` |
| 8 | Rescheduled This Month | `rescheduledThisMonth` |
| 9 | Cancelled This Week | `cancelledThisWeek` |
| 10 | Checked In Today | `checkedInToday` |
| 11 | Checked Out Today | `checkedOutToday` |
| 12 | Total Doctors | `totalDoctors` |
| 13 | Total Tenants | `totalTenants` |

Placeholder cards are styled differently (grayed out / muted) to indicate they are
pending implementation.

### 5c. NEW API Endpoint

`GET /api/app/dashboard` → `DashboardCountersDto` (13 fields).

Single round-trip, same as OLD. HTTP method changes from POST to GET (ABP convention
deviation; functionally equivalent -- see Exception 2).

### 5d. Drill-Down Navigation

Card clicks call `openByStatus(statusId)`:
- Routes to `/appointments?appointmentStatus={statusId}` (pre-filtered list).
- Pending Change Requests → `/appointments?hasChangePending=true` (TBD).
- Approaching Legal Deadline → `/appointments?approachingDeadline=true` (TBD).

NEW source: `dashboard/tenant-dashboard/tenant-dashboard.component.ts:1-73`,
`dashboard/tenant-dashboard/tenant-dashboard.component.html:1-137`

---

## 6. Parity Gaps -- Missing Counters/Widgets

The following OLD dashboard elements are NOT yet represented in the NEW tenant dashboard:

| Missing element | OLD source | Action |
|---|---|---|
| Pending JDF counter (AME appts where JDF not yet uploaded) | Dashboard spec line 251 | Add `pendingJdfCount` to `DashboardCountersDto` and backend query |
| Upcoming Appointments widget (next N days) | Dashboard spec line 253 | Add widget or counter for appointments in next 7/14 days |
| User count by type (Patient / Adjuster / AA / DA) | OLD cards 9-12 | Add 4 user-count cards to tenant dashboard; hidden from Clinic Staff |
| Role-scoping (Clinic Staff sees own; Supervisor sees all) | `DashboardController.cs` query filter | Verify `IAppointmentAccessPolicy` is applied in `IDashboardAppService.GetCountersAsync()` |

These gaps are flagged as Implementation severity I (blocker before Phase 19b UI sign-off).

---

## 7. Role Visibility Matrix

| Role | Host Dashboard | Tenant Dashboard | User count cards |
|---|---|---|---|
| IT Admin | Yes (`CaseEvaluation.Dashboard.Host`) | No | n/a (host dashboard) |
| Staff Supervisor | No | Yes (all appointments) | Yes |
| Clinic Staff | No | Yes (own appointments only) | No |
| Doctor | No | Yes (placeholder only -- no appointment data yet) | No |

---

## 8. Branding Tokens

| Element | Token |
|---|---|
| Page heading | `--text-primary` |
| Counter card -- Pending | `--status-pending` (yellow) background/accent |
| Counter card -- Approved | `--status-approved` (green) |
| Counter card -- Rejected | `--status-rejected` (red) |
| Counter card -- Change Requests | `--brand-primary` (blue) |
| Counter card -- Legal Deadline | `--status-rejected` (red/orange urgency) |
| Placeholder card | muted/grey via `--text-muted` |
| Counter number | large, bold, `--text-primary` |

Token definitions: `_design-tokens.md`.

---

## 9. NEW Stack Delta

| Aspect | OLD | NEW |
|---|---|---|
| Single vs split dashboard | Single component for all internal roles | Host dashboard (IT Admin) + Tenant dashboard (Staff/Supervisor) |
| Counter count | 12 cards (8 appointment + 4 user-type) | 13 cards in tenant (5 live + 8 placeholder); 4 ABP widgets in host |
| Change request counter | Not present (no change requests in OLD) | NEW adds Pending Change Requests counter |
| Legal deadline counter | Not present | NEW adds Approaching Legal Deadline (CCR 31.5) |
| State management | Plain component properties | Angular signals (`counters()`, `isLoading()`, `errorMessage()`) |
| HTTP method | POST (old convention) | GET (ABP REST convention) |
| Host dashboard | No separate host dashboard | ABP analytics widgets (error rate, editions, tenants) |

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Host vs Tenant split | Single dashboard for all internal roles | IT Admin gets ABP host dashboard; Staff/Supervisor get tenant dashboard | Architecture improvement; IT Admin role focuses on system health, not appointment ops |
| 2 | HTTP method | `POST /api/Dashboard/post` | `GET /api/app/dashboard` | ABP REST convention; functionally equivalent (no body, same response) |
| 3 | Appointment status cards missing | Billed, No-Show, Rescheduled, Cancelled, Checked-In, Checked-Out cards present in OLD | Placeholder (0) in NEW until day-of-exam states ship | Phased implementation; Phase 1 demo does not require day-of-exam states |
| 4 | User count cards | 4 user-type count cards (Patient / Adjuster / AA / DA) in OLD | Not yet in NEW tenant dashboard | Phase 19b addition required (see parity gaps Section 6) |
| 5 | Pending JDF counter | In OLD spec; expected on dashboard | Not in NEW DashboardCountersDto | Phase 19b addition required |
| 6 | Upcoming appointments widget | In OLD spec | Not in NEW | Phase 19b addition required |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `dashboard/dashboard.component.html` | 1-330 | 12 counter cards + layout + click handlers |
| `dashboard/dashboard.component.ts` | 1-113 | `counterClicked()`, role-based navigation |
| `dashboard/dashboard.service.ts` | all | `POST /api/Dashboard/post` call + response mapping |
| `docs/parity/internal-user-dashboard.md` | all | Gap analysis: missing counters/widgets + role-scoping requirement |

---

## 12. Verification Checklist

- [ ] IT Admin lands on the host dashboard (ABP analytics widgets visible)
- [ ] Clinic Staff and Staff Supervisor land on the tenant dashboard (counter cards visible)
- [ ] Pending Requests counter shows correct count of Pending-status appointments
- [ ] Approved/Rejected This Week counters update daily
- [ ] Pending Change Requests counter shows reschedule/cancel requests awaiting staff action
- [ ] Approaching Legal Deadline counter shows appointments within CCR Sec. 31.5 deadline window
- [ ] Clicking Pending Requests routes to `/appointments` pre-filtered to Pending status
- [ ] Clicking Approved This Week routes to filtered appointment list
- [ ] Clicking Pending Change Requests routes to change requests list (TBD route)
- [ ] Clinic Staff dashboard shows only their assigned appointments in counters
- [ ] Staff Supervisor dashboard shows all appointments (no user filter)
- [ ] Placeholder cards display 0 and are visually distinguished from live cards
- [ ] User count by type cards added (Phase 19b -- Exception 4)
- [ ] Pending JDF counter added (Phase 19b -- Exception 5)
- [ ] Role-scoping verified: Clinic Staff counter queries filter by `PrimaryResponsibleUserId`
- [ ] Dashboard loads in a single API round-trip
