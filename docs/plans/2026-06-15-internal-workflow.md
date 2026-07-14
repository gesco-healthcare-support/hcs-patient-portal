---
status: in-progress
date: 2026-06-15
slug: internal-workflow
branch: feat/redesign-internal-workflow
parent-branch: feat/internal-user-pages
surface: internal (staff) Workflow -- change-requests inbox, change-logs, reports (Prompt 13)
backend: YES (small) -- consent surfaced via PROXY REGEN only (DTO already exposes it); + a report status-counts endpoint (reuses the Prompt 10 repo count) + a CSV export endpoint
related:
  - "design_handoff_appointment_portal/Internal Workflow - Redesign.html"
  - "design_handoff_appointment_portal/components/in-workflow.jsx (CrInbox / ClgPage / RpPage)"
  - "design_handoff_appointment_portal/styles/in-work.css (.cr-* / .clg-* / .rp-*)"
depends-on: "internal shell + dashboard + list + detail + add (merged into feat/internal-user-pages)"
---

# Plan: Internal Workflow (Prompt 13) -- change-requests + change-logs + reports

Three internal-staff surfaces, all supervisor-only, all already wired into the
shell nav (Workspace section). The redesign re-skins each onto the global tokens
and the shared `ia-*` table/toolbar classes; the engines (services, proxies,
permissions, redaction) are reused unchanged. Verified against the live code and
the prototype (2026-06-15 research sweep).

## Surfaces today (verified)

| Surface | Route(s) | Component(s) today | Engine kept |
| --- | --- | --- | --- |
| Change requests | `/appointments/change-requests/reschedules` + `/cancellations` | `ChangeRequestListComponent` (one Bootstrap table per type) + approve/reject modals | `AppointmentChangeRequestApprovalService` (getPending / approve* / reject*) |
| Change logs | `/appointment-change-logs` (global) + `/appointments/view/:id/change-log` (per-appt) | `AppointmentChangeLogListComponent` + `AppointmentChangeLogsComponent` (Bootstrap tables) | `AppointmentChangeLogService` (getList / getByAppointment) |
| Reports | `/reports` | `AppointmentReportComponent` (OnPush, Bootstrap grid + PDF export) | `ReportService` (getList / exportPdf) |

Permissions (unchanged, server-re-enforced): `CaseEvaluation.AppointmentChangeRequests`
(+ `.Approve` / `.Reject`), `CaseEvaluation.AppointmentChangeLogs`,
`CaseEvaluation.Reports` (+ `.Export`). All three nav items are `roles=['supervisor']`
in `internal-nav.config.ts`; the change-requests item carries the `changeRequests`
badge fed by `InternalNavBadgeService` (dashboard poll, 60s). **PHI redaction is
server-side** for all three (change-log `valueRedacted` flag; report SSN->last-4,
DOB->birth-year) -- the frontend renders verbatim, never unmasks.

## Prototype (verified in in-workflow.jsx / in-work.css)

- **Change requests (`CrInbox`, `.cr-*`)**: one unified inbox with tabs
  `All / Reschedules / Cancellations` + live counts; each row is a collapsible
  card (type badge tint-amber/tint-red, patient + conf#, requester + role,
  requested timestamp, `cr-age` pill `>=7 crit / >=4 warn / else ok`, `cr-consent`
  dot `pending|agreed|declined`); expand shows current slot (strikethrough) ->
  requested slot + reason; row actions View / Approve / Reject. Approve & reject
  use `.ra-modal`; **approve shows a consent-override warning** (`.ra-note warn`:
  "Opposing-counsel consent is pending/declined -- approving now overrides it")
  when consent != agreed; reject requires a reason textarea (<=500).
- **Change logs (`ClgPage`, `.clg-*`)**: searchable timeline; a section filter;
  each entry is a collapsible `.clg-entry` (op kind icon update/add/delete, section,
  conf link, who+role, timestamp, field count); expand shows a `.clg-diffs` grid
  (field | old strikethrough | arrow | new); redacted rows render
  "updated (value hidden -- sensitive field)" via `.red`.
- **Reports (`RpPage`, `.rp-*`)**: `ia-head` + CSV/Export-PDF buttons; `ia-toolbar`
  (search, type select, status select, **Columns** picker `rp-cols`/`rp-colmenu`);
  **`rp-stats`** row of 5 status summary cards; `ia-table` grid with `af-pill`
  status; row click -> appointment detail.

## Decisions (RESOLVED 2026-06-15: include the backend bits)

Adrian chose full prototype fidelity. Verified the actual backend so the work is
scoped precisely (much smaller than first feared):

- **D1 -- Per-request consent status in the inbox: PROXY UPDATE ONLY (no C# change).**
  The backend `AppointmentChangeRequestDto` ALREADY carries `ConsentStatus`
  (`ChangeRequestConsentStatus`: NotRequired/Pending/Approved/Rejected/Expired),
  `RequestingSide`, and `AppointmentConfirmationNumber`, and
  `GetPendingChangeRequestsAsync` returns them (Mapperly auto-maps the enum by name).
  The Angular proxy `appointment-change-requests/models.ts` is just STALE -- it lacks
  all three fields. Fix = add those fields to the proxy DTO + add the
  `ChangeRequestConsentStatus` + `ChangeRequestSide` enum proxies, then bind in the
  inbox. (Build step: confirm the entity->DTO Mapperly mapper includes `ConsentStatus`;
  it does by default.)
- **D2 -- Report `rp-stats` status counts: new counts endpoint reusing existing query.**
  Prompt 10 already added a status-counts query on the appointment repository.
  Add `ReportsAppService.GetStatusCountsAsync(GetAppointmentReportInput)` (same filters
  as the grid, EXCLUDING the status filter) returning per-raw-status counts; the
  frontend buckets them into the 5 report cards (keeps frontend as the single source of
  pill bucketing). + controller endpoint + DTO + proxy.
- **D3 -- CSV export: one new endpoint mirroring PDF.**
  Add `ReportsAppService.GetReportCsvAsync(GetAppointmentReportInput)` (full filtered
  set, `ToMaskedRow`, build CSV bytes via a small `AppointmentReportCsv` helper, return
  `DownloadResult{ContentType="text/csv"}`), guarded by `Reports.Export`. Reuses the
  SAME redaction (SSN last-4, DOB year). + controller endpoint + proxy + the existing
  HttpClient-blob-and-anchor download pattern on the client.

Proxy work follows the established **hand-edit** pattern (Prompt 10 precedent:
hand-edit `models.ts` + the service), not a full `abp generate-proxy` sweep, to keep
the diff surgical. NO change to permissions, redaction policy, or the consent
state machine.

## Architecture (assuming the recommended frontend-only path)

New global SCSS partials under `angular/src/styles/`, `@use`'d in `styles.scss`,
ported from `in-work.css` onto the tokens: `_cr-inbox.scss`, `_clg-log.scss`,
`_rp-report.scss`. Reuse existing `_in-appts.scss` (`ia-*`), `_ra-wizard.scss`
(`ra-modal`/`ra-note`), `_af-buttons.scss`.

- **Change requests -> NEW unified inbox component**
  `InternalChangeRequestInboxComponent` (standalone, signals/OnPush). Loads both
  types via `getPending` (one call per type, merged) and filters client-side by tab;
  computes `cr-age` from `createdTime`; redesigned approve/reject modals call the
  same `AppointmentChangeRequestApprovalService` methods. Route: add
  `/appointments/change-requests` (inbox); redirect the legacy `/reschedules` +
  `/cancellations` to it (preserve deep links); point the nav item at the inbox.
  Retire `ChangeRequestListComponent`'s route usage (keep or delete the legacy
  component + its modals depending on remaining references -- decide at build).
- **Change logs -> re-skin both existing components in place** (rewrite templates
  to the `.clg` timeline + add `_clg-log.scss`; keep the components, routes, and
  service wiring). A small shared presentational piece renders the collapsible
  entry + `clg-diffs` + redaction so the global list and the per-appointment view
  share it.
- **Reports -> re-skin `AppointmentReportComponent` in place** (rewrite template to
  `ia-head`/`ia-toolbar`/`ia-table` + `_rp-report.scss`; keep the reactive form,
  PDF export, pagination, and the empty-until-filtered behavior). Stat cards + CSV
  per D2/D3.

Nav + permissions + badge: **unchanged** (the items already exist). Only the
change-requests nav route target changes (to the inbox).

## Tasks (one-by-one; commit each)

### Backend + proxy (do first so the frontend can bind)
- **B1 -- proxy: surface change-request consent [code]**: hand-edit
  `proxy/appointment-change-requests/models.ts` to add `consentStatus`,
  `requestingSide`, `appointmentConfirmationNumber`; add proxy enums
  `ChangeRequestConsentStatus` + `ChangeRequestSide`. Confirm the entity->DTO
  Mapperly mapper carries `ConsentStatus` (read it; expected auto-mapped). No C# edit
  unless the mapper omits it.
- **B2 -- report status-counts endpoint [test-after]**: add
  `GetStatusCountsAsync(GetAppointmentReportInput)` to `IReportsAppService` +
  `ReportsAppService` (reuse the appointment repo's Prompt 10 status-counts query with
  the report filters, status filter excluded) returning a `ReportStatusCountDto[]`
  (rawStatus + count); `ReportController` endpoint; hand-edit `proxy/reports`
  (models + service). Unit-test the bucketing helper on the client.
- **B3 -- report CSV export endpoint [test-after]**: add
  `GetReportCsvAsync(GetAppointmentReportInput)` ([Authorize Reports.Export]) +
  an `AppointmentReportCsv` builder (pure: masked rows -> CSV text, 10 columns matching
  the PDF); `ReportController` endpoint returning `DownloadResult` text/csv; hand-edit
  `proxy/reports`. Unit-test the CSV builder (header + a row + comma/quote escaping).

### Frontend
- **T1 -- `_cr-inbox.scss` + `InternalChangeRequestInboxComponent` [test-after]**:
  unified tabbed inbox; client-side age (`cr-age` ok/warn/crit) + tab filtering +
  consent dot (`cr-consent` from `consentStatus`) (pure helpers -> `cr-inbox.util.ts`
  + spec); reuse the approval service; show `appointmentConfirmationNumber`.
- **T2 -- redesigned approve/reject modals + consent-override warning [code]**:
  `.ra-modal` modals calling approve*/reject*; reason validation (<=500); the approve
  modal shows the consent-override warning driven by the real `consentStatus`
  (Pending/Rejected/Expired != Approved).
- **T3 -- route the inbox + redirect legacy change-request paths + nav target [code]**.
- **T4 -- `_clg-log.scss` + re-skin the global change-log list [test-after]**:
  `.clg` timeline + diff grid + redaction (`valueRedacted` -> "value hidden"); keep
  filters + paging; any pure diff/format helper unit-tested.
- **T5 -- re-skin the per-appointment change-log view [code]**: same `.clg`
  presentation at `/appointments/view/:id/change-log`.
- **T6 -- `_rp-report.scss` + re-skin the reports grid [code]**: `ia-*` toolbar +
  table + `af-pill` status + PDF export + pagination; Columns picker (`rp-cols`);
  `rp-stats` cards fed by B2; CSV button fed by B3 (HttpClient blob + anchor download,
  mirroring PDF). Keep empty-until-filtered + SSN-last-4/DOB-year.

## Verification

- Build clean; karma for any new util (cr-inbox / clg helpers); internal-* specs green.
- Live (stack :4250, Supervisor `stafsuper1@gesco.com`):
  - **Change requests**: inbox loads both types; tabs filter + counts correct; age
    pills bucket right; expand shows slot diff + reason; approve (consent warning
    when applicable) + reject (reason required) hit the API and clear the row; badge
    count consistent.
  - **Change logs**: global list timeline renders with filters + paging; redacted
    rows show "value hidden"; per-appointment view (from a detail's Change log
    button) renders the same timeline.
  - **Reports**: empty until a filter; search/type/status/date filters work; grid +
    pagination; PDF export downloads; SSN last-4 + DOB year only; Columns picker
    toggles columns; `rp-stats` cards show correct per-status counts for the filter
    set (B2); CSV export downloads a masked file matching the grid (B3).
  - Intake Staff does NOT see these nav items / is blocked (role gating intact).
- Backend: `dotnet build` clean; the counts + CSV endpoints return for a Supervisor
  and 403 for a non-`Reports` user; CSV bytes carry no full SSN/DOB.
- Squash-merge to `feat/internal-user-pages`, push, resync local.

## Risks

- Change-request route unification could break deep links -> mitigate with redirects
  from the legacy `/reschedules` + `/cancellations` paths.
- Change-log redaction is server-side; the re-skin must not assume unmasked values
  exist (render `valueRedacted` rows as hidden). Do not add any client-side reveal.
- Report must keep SSN/Fax out and the empty-until-filtered guard (client + server).
- Badge poll (60s) + permission-gated start is existing behavior; don't regress it.

## Out of scope

- Any backend beyond B1-B3 (no change to the consent state machine, permissions,
  redaction policy, or the change-log audit scope).
- The patient-initiated reschedule/cancel request modals (appointment-detail flow,
  already shipped) -- this plan is the staff approval inbox, not request creation.
- Send-back / Request-info (Prompt 17) and Scheduling (Prompt 14).
