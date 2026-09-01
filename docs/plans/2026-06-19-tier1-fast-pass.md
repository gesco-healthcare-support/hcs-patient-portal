---
feature: tier1-fast-pass
date: 2026-06-19
status: in-progress
base-branch: feat/frontend-rework
lane: Session A (integrator)
related-issues: []
backlog: 2026-06-17-frontend-rework-backlog.md
protocol: 2026-06-19-parallel-build-protocol.md
---

## Goal

Land the two fast backlog items -- #3 (WCAB -> Configuration hub) and #10 (dashboard
completion line) -- as Session A's lane in the parallel build, #3 first to unblock
Session B's config-hub work (#4/#6).

## Context

Session A's lane in the two-session parallel build (see the protocol doc). #3 is the
shared-surface change (config hub + nav + routing) that Session B is waiting on, so it
lands first. Neither item creates an EF migration (good -- migrations are Session B's
exclusive lane); #10 adds one DTO field, so Session A runs the proxy regen.

Research findings folded in:
- #3: the Config hub (`InternalConfigurationComponent`) is a generic `ConfigRow` table
  (name/description/active) driven by `CONFIG_SECTIONS`. WCAB is a rich component
  (`InternalWcabOfficesComponent`: name + code + address/city/zip/stateId + custom modal)
  and cannot be a `ConfigRow`. Folding it into the generic table is lossy and rejected.
  WCAB + Locations currently sit under the `Scheduling` nav group as their own components.
- #10: completion line is a pure query aggregation (no schema change). `completed` =
  `Approved` (2) only -- the redesign's terminal positive decision (the hero "Approved
  Requests" KPI); `CheckedOut`(10)/`Billed`(11) are legacy day-of-exam states excluded
  from the redesign UI. Bars count requests in by `CreationTime`; the line counts
  completions out by `AppointmentApproveDate` -- different axes, the correct
  "in vs out" read, to be labeled so it is not misread.

## Approach

### #3 -- chosen: WCAB rail entry, own component, under the hub shell
Add WCAB as a rail item in the Configuration hub, rendered by its existing rich
component inside the hub shell (the rail + chrome fill the empty gutter that prompted the
whitespace complaint); de-route the standalone `/doctor-management/wcab-offices` page and
move its nav item from `Scheduling` into `Configuration`. De-route = remove the route +
nav entry, leave the component file (the established pattern; legacy components stay
in `components/`). Scope (WCAB-only vs also relocating Locations) is the open question
below.

Rejected: folding WCAB into the generic `ConfigRow` table (drops address/state/code --
lossy); leaving it a bare standalone page (does not fix the whitespace, the point of #3).

### #10 -- chosen: CompletedCount (Approved per week) + line overlay
Backend adds `CompletedCount` to `DashboardTrendPointDto`; `BuildTrendAsync` adds a second
per-week `CountAsync(AppointmentStatus == Approved && AppointmentApproveDate in [weekStart,
weekEnd))`, mirroring the hero KPI. FE draws a line overlay (inline SVG polyline / dots) in
the existing `.dh-chart` block over the shipped bars, sharing the y-scale. Bars + line use
a short card/tooltip note clarifying "requests received vs. approved that week."

Rejected: counting `CheckedOut`/`Billed` as completed (those day-of-exam states are not
wired in the redesign; would read as all-zero).

## Tasks

- T1 (#3): Move WCAB into the Configuration hub + de-route the standalone page.
  - approach: code
  - files-touched: [angular/src/app/configuration/config-rail.component.ts (new), angular/src/app/configuration/cf-config.util.ts, angular/src/app/configuration/internal-configuration.component.ts, angular/src/app/configuration/internal-configuration.component.html, angular/src/app/wcab-offices/wcab-office/internal-wcab-offices.component.ts, angular/src/app/wcab-offices/wcab-office/internal-wcab-offices.component.html, angular/src/app/shared/components/internal-shell/internal-nav.config.ts]
  - implementation note (deviation): route KEPT at /doctor-management/wcab-offices (no
    app.routes.ts / wcab-office-routes.ts change, fewer shared files for Session B); rail
    extracted into a shared ConfigRailComponent (CONFIG_RAIL_ITEMS derived from
    CONFIG_SECTIONS + WCAB) rendered by both the hub and the WCAB page.
  - acceptance: WCAB reachable from the Configuration hub rail, rendered inside the hub
    shell (rail visible, no large empty gutter); the `Scheduling` nav no longer lists WCAB
    (now under `Configuration`). Screenshot-verified at desktop width.
  - parallel note: edits shared nav + routes; the commit landing is Session B's signal to
    start #4/#6 hub UI.

- T2 (#10 backend): Add the per-week approved/completed count.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application.Contracts/Dashboards/DashboardDto.cs, src/HealthcareSupport.CaseEvaluation.Application/Dashboards/DashboardAppService.cs]
  - acceptance: an app-service/integration test asserts per-week `CompletedCount` equals
    seeded `Approved` appointments (by `AppointmentApproveDate`); existing trend fields
    unchanged.

- T3 (#10 proxy): Regenerate the dashboards proxy for the new DTO field.
  - approach: code
  - files-touched: [angular/src/app/proxy/dashboards/models.ts, angular/src/app/proxy/generate-proxy.json]
  - command: `abp generate-proxy -t ng -u http://localhost:44377` (no trailing slash);
    commit only `models.ts` + `generate-proxy.json`, discard EOL-only `index.ts` no-ops.
    Session A owns regen (single-writer rule).
  - acceptance: `DashboardTrendPointDto` in the proxy carries `completedCount`; Angular
    compiles.

- T4 (#10 FE): Draw the completion line over the bars.
  - approach: code
  - files-touched: [angular/src/app/dashboard/internal-dashboard.component.ts, angular/src/app/dashboard/internal-dashboard.component.html, angular/src/styles/_in-dash.scss]
  - acceptance: the trend card shows a line tracking weekly approvals over the volume
    bars, aligned to the shared y-axis, with the in-vs-out label; screenshot-verified.

## Execution order

T1 (#3) -> commit + push (unblocks Session B) -> T2 -> T3 (regen) -> T4 -> commit + push.

**Progress (2026-06-19):** T1 (#3) DONE -- committed 2885512, pushed, screenshot-verified
(WCAB renders in the config-hub shell with the shared rail; nav moved to Configuration).
T2-T4 (#10) next.

## Risk / Rollback

- Blast radius: #3 touches shared nav + top-level routes (coordinated via the protocol);
  #10 touches the dashboards proxy (Session A-owned regen) + the trend query. No
  migration, no Session B file overlap.
- Rollback: revert the #3 commit (nav/route only) and/or the #10 commits; the DTO field
  is additive.

## Verification

Live on Falkinstein as staff supervisor. #3: navigate from the Configuration hub rail to
WCAB, confirm it renders in-shell and the old route 404s / is gone; screenshot. #10: open
the dashboard, confirm the completion line tracks weekly approvals over the bars with the
clarifying label; screenshot. Plus T2's backend test.
