---
status: in-progress
date: 2026-06-14
slug: internal-dashboard
branch: feat/redesign-internal-dashboard
parent-branch: feat/internal-user-pages
surface: internal (staff) dashboard at /dashboard
backend: YES -- DTO enrichment + new query methods (NO schema migration; all data
  is queryable from existing entities)
related:
  - "design_handoff_appointment_portal/Internal Dashboard - Redesign.html"
  - "design_handoff_appointment_portal/components/in-dashboard.jsx"
  - "design_handoff_appointment_portal/styles/in-dash.css"
depends-on: "internal shell (merged into feat/internal-user-pages @ 9ba759c)"
---

# Plan: Internal Dashboard (Prompt 9) -- backend-first, full

## Goal

Replace the legacy `/dashboard` (a permission switch over TenantDashboardComponent +
the ABP-widget HostDashboardComponent) with the redesigned Internal Dashboard rendered
inside the internal shell: role/host-aware KPI cards, a decision-deadline alert, a
6-week trend chart, a 6-status donut, today's schedule, recent activity, and a host
per-tenant table. Per the 2026-06-14 decision this is **backend-first** -- enrich the
data the dashboard endpoint returns, then build the frontend against real data.

## Context / findings (from research)

- The current `DashboardAppService.GetAsync()` (`HealthcareSupport.CaseEvaluation.Application/Dashboards`)
  branches host vs tenant via `IsGrantedAsync(Dashboard.Host/Tenant)` and disables the
  multi-tenant filter (`_dataFilter.Disable<IMultiTenant>()`) for host aggregates. It
  returns `DashboardCountersDto` -- 5 live counts + 8 zeros.
- The prototype needs much more than counts (trend series, full 6-status breakdown,
  a deadline ROW list, today's schedule, activity feed, per-tenant rows). **All of it
  is queryable from existing entities** -- no new tables, no EF migration:
  - `Appointment` (`AppointmentDate`, `CreationTime`, `AppointmentStatus`, `TenantId`).
  - `AppointmentStatusType` (14 values) buckets into the 6 UI pills (see Data contract).
  - `AppointmentChangeRequest` (`RequestStatus`: Pending=25) -> the real pendingChangeRequests.
  - `DecisionSlaPolicy` already computes the overdue cutoff (per-tenant SLA, default 3 days).
  - `IAuditLogRepository` + the existing `AppointmentChangeLogsAppService` logic -> activity.
- Charts in the prototype are pure CSS (conic-gradient donut + CSS bars). No chart library.
- Role variants: backend decides host-vs-tenant DATA; the frontend decides
  Supervisor-vs-Intake PRESENTATION (Intake sees a reduced card set) using
  `resolveInternalRoleKey` (already built for the shell).

## Architecture

- **Backend:** one enriched endpoint. Extend `GetAsync()` to return a composite
  `DashboardDto` (counters + statusBreakdown[] + trend[] + deadlines[] + schedule[] +
  activity[] + tenants[]), optionally parameterized by `range` (week|month|quarter) for
  the time-frame toggle. Host branch fills `tenants[]`; tenant branch fills
  trend/deadlines/schedule/activity. Keep the existing Host/Tenant permission gate.
  No schema migration (DTO + queries only). Regenerate the Angular proxy.
- **Frontend:** one `InternalDashboardComponent` (replaces the DashboardComponent switch),
  rendered in the shell's child outlet at `/dashboard`. Role/host-aware presentation;
  CSS charts; clickable KPIs deep-link to the filtered `/appointments?appointmentStatus=N`.
- **Legacy:** delete TenantDashboardComponent + HostDashboardComponent after live sign-off.

## Data contract (status bucketization -- the load-bearing detail)

The 6 UI pills map from `AppointmentStatusType` as:

| UI pill | enum value(s) |
| --- | --- |
| Pending | Pending (1) |
| Info Requested | InfoRequested (14) |
| Approved | Approved (2) |
| Rejected | Rejected (3) |
| Cancelled | CancelledNoBill (5), CancelledLate (6) |
| Rescheduled | RescheduledNoBill (7), RescheduledLate (8) |

`RescheduleRequested (12)` / `CancellationRequested (13)` are NOT appointment-status pills --
they are pending change requests, counted via `AppointmentChangeRequest` (RequestStatus=Pending).
Mirror the frontend `appointmentStatusToPill` util so backend + UI agree.

## Tasks (one-by-one; commit each; per-task approach flag)

### Backend

**B1 -- counters + status breakdown + counter fixes  [tdd]**
Extend the dashboard service: compute the real 6-pill status counts (bucketized per the
table), fix `pendingChangeRequests` (count `AppointmentChangeRequest` where
RequestStatus=Pending), keep host/tenant scoping. Add `statusBreakdown` to the DTO.
Unit-test: the bucketization (each enum -> correct pill) + host-vs-tenant scoping +
pendingChangeRequests count. (Security-sensitive: host disable-filter must not leak
cross-tenant counts into a tenant response.)

**B2 -- decision-deadline row list + trend series  [test-after]**
- `deadlines[]`: `DashboardDeadlineItemDto { appointmentId, confirmationNumber,
  patientName, requestDate, dueDate, daysRemaining }` using `DecisionSlaPolicy`'s
  overdue/approaching cutoff. Staff-facing (patient name is allowed for staff; still
  no SSN/DOB).
- `trend[]`: `DashboardTrendPointDto { weekStart, label, count }` -- requests per week
  for the last 6 weeks (group `Appointment` by week of `CreationTime`).

**B3 -- today's schedule + recent activity  [test-after]**
- `schedule[]`: `DashboardScheduleItemDto { time, appointmentType, location }` where
  `AppointmentDate` is today (tenant-scoped).
- `activity[]`: `DashboardActivityItemDto { icon, text, when }` from the last N audit
  events, reusing `AppointmentChangeLogsAppService` redaction (no PHI/SSN).

**B4 -- host per-tenant aggregates  [test-after]**
`tenants[]`: `DashboardTenantRowDto { tenantName, appointments, pending, approved,
thisWeek }` -- host-only, via `_dataFilter.Disable<IMultiTenant>()` + group by tenant.

**B5 -- endpoint shape + proxy regen  [code]**
Finalize the composite `DashboardDto` + optional `range` param; run `abp generate-proxy`;
verify `angular/src/app/proxy/dashboards` reflects the new shape. No EF migration.

### Frontend

**F1 -- InternalDashboardComponent  [test-after]**
Build the redesigned dashboard (replaces the DashboardComponent switch), mounted at
`/dashboard` in the shell. Sections: header (+ time-frame toggle for Supervisor/Intake),
hero KPI cards (deltas if B-side provides prior-period; else omit the badge), decision-
deadline alert (row list), trend chart (CSS bars), status donut (CSS conic-gradient),
today's schedule, recent activity, host per-tenant table. Role/host-aware presentation
via `resolveInternalRoleKey`/`isHostScope`. Clickable KPIs -> `/appointments?appointmentStatus=N`.
Unit-test the role-variant card selection + the status->route mapping.

**F2 -- _in-dash.scss  [code]**
Port `in-dash.css` onto global tokens; `@use` in styles.scss.

**F3 -- retire legacy dashboard  [code]**
After live sign-off: delete TenantDashboardComponent + HostDashboardComponent (+ empty
scss), update the `/dashboard` route to the new component, clean imports.

## Resolved decisions (2026-06-14)

1. **Host ABP ops widgets:** DROP from /dashboard. IT Admin gets the redesigned business
   KPI + tenant-table view; the ABP error-rate/execution-duration/editions/latest-tenants
   widgets are removed. (Can get a dedicated ops route later if wanted.)
2. **KPI delta badges:** INCLUDE. B1 also computes prior-period counts for the hero KPIs
   so the cards show real deltas.
3. **Time-frame toggle:** WIRE NOW. Add a `range` (week|month|quarter) param the backend
   honors for the counts + trend; the frontend renders the toggle. Default = week (keep
   the existing UTC-Monday "this week" convention as the week option).

## Risks

- Host disable-filter scoping bug could leak cross-tenant data into a tenant response
  (B1 unit tests guard this; security-sensitive).
- Activity feed must reuse the change-log redaction so no PHI leaks into the dashboard.
- Trend grouping by week is timezone-sensitive (the existing counters use UTC "this
  week" = Monday; keep the same convention for consistency).
- Blast radius: /dashboard is the shell's landing route; a broken dashboard is the first
  thing every internal user sees. Verify per role live before retiring the legacy.

## Verification

- Backend: unit tests (bucketization, host/tenant scoping, counts) + run-tests.
- Proxy: build clean after generate-proxy.
- Frontend: unit tests (role-variant selection, status->route) + build.
- Live (stack on :4250, shifted ports): log in as Staff Supervisor (stafsuper1),
  Intake Staff (clistaff1), IT Admin (it.admin); verify each role's card set, the
  deadline alert (supervisor/intake only), charts render with real data, clickable KPIs
  land on the correctly-filtered appointments list, host sees the tenant table.

## Out of scope

- Day-of-exam metrics (billed/no-show/checked-in/out) -- still backend placeholders;
  a future day-of-exam lifecycle feature.
- The appointments-list filter UI itself (Prompt 10) -- the KPIs just deep-link with a
  status query param the legacy list already honors.
