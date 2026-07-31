---
feature: fe-quick-batch
date: 2026-06-17
status: in-progress
base-branch: feat/frontend-rework
related-issues: []
backlog: 2026-06-17-frontend-rework-backlog.md
---

## Goal

Land all the frontend-only items from the post-merge backlog in one fast batch,
leaving only the backend and design-heavy items for dedicated plans.

## Context

Adrian's 13-item testing list was triaged (see the backlog doc). This plan covers
the "FE" bucket only: pure CSS / template fixes with no backend change and no new
design artifact. Decisions are already locked. The dev stack is up on Falkinstein
(angular 4250 / api 44377 / authserver 44418) for live verification. Excluded from
this batch: FE+Design items (1a/1b sidebar, 3 WCAB, 10 dashboard, 13e review) and
all full-stack items (2, 4, 6, 9, 11). Item 1c (underlines) is deferred pending
Adrian pointing at the affected buttons (no global underline rule exists).

## Approach

- One branch `fix/fe-quick-batch` off feat/frontend-rework; per-task commits;
  squash-merge back to feat/frontend-rework on sign-off.
- All tasks are `code` approach: these are visual CSS/template changes that do not
  decompose into unit tests; verification is live runtime observation (per
  testing.md, UI leans test-after, and pure styling is `code`).
- Scope each shared-cause fix narrowly to avoid visual regressions elsewhere
  (esp. the .input-group and the fluid-gutter changes).

## Tasks

- T1: Inline the input-group addon buttons so they sit beside the field, not
  wrapped below (covers #13a calendar, #13b SSN eye, #13d claim trash).
  - approach: code
  - files-touched: [angular/src/app/appointments/sections/appointment-add-schedule.component.html, angular/src/app/shared/components/ssn-input.component.ts, angular/src/app/appointments/sections/appointment-add-claim-information.component.html, angular/src/app/appointments/appointment-add.component.scss]
  - root: Bootstrap `.input-group { flex-wrap: wrap }` in narrow columns.
  - acceptance: at the form's column widths, each addon button renders on the same
    row as its input (button top == input top, not below) - verified via bounding
    boxes in the open form.

- T2: Make available appointment dates visibly highlighted in the datepicker.
  - approach: code
  - files-touched: [angular/src/app/appointments/sections/appointment-add-schedule.component.scss, angular/src/app/appointments/appointment-add.component.scss]
  - root: `.available-day` class is applied (27 days) but its style does not render
    - likely ViewEncapsulation mismatch between the schedule component's
    [dayTemplate] and the parent component's SCSS rule. Resolve by defining the
    rule where the template lives (or a global/non-encapsulated style).
  - acceptance: available days render with a distinct background/marker (computed
    background != transparent) while unavailable/disabled days do not.

- T3: Rename "Web Address" to "Website" for Applicant + Defense Attorney.
  - approach: code
  - files-touched: [angular/src/app/appointments/sections/appointment-add-attorney-section.component.html, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - acceptance: both attorney sections show "Website" as the field label; driven by
    the localization key (WebAddress -> "Website"), not a hardcoded literal.

- T4: Format the audit-log Time column.
  - approach: code
  - files-touched: [angular/src/app/admin/internal-admin-hub.component.html]
  - root: line 399 binds `{{ l.executionTime }}` raw.
  - acceptance: the Time column shows a human-readable local datetime (via a date
    pipe / the project datetime resolver), not the raw ISO/DB string.

- T5: Resize + label the date-range filters on change-logs and reports.
  - approach: code
  - files-touched: [angular/src/styles/_in-appts.scss, angular/src/app/appointment-change-logs/appointment-change-log-list.component.html, angular/src/app/reports/appointment-report.component.html]
  - root: `.ia-input { width:100% }`; native type=date inputs ignore placeholder.
  - acceptance: start + end date inputs fit on one line at desktop width, each
    capped (~160px), each labeled "Start date" / "End date".

- T6: Apply fluid side gutters to the over-margined pages.
  - approach: code
  - files-touched: [angular/src/app/patients/patient/components/patient-profile.component.scss, angular/src/app/appointments/appointment-view.component.html, angular/src/styles/_ra-wizard.scss]
  - root: fixed px padding / max-width + auto margins strand whitespace on wide
    screens.
  - acceptance: on a wide viewport, content scales with the viewport (gutters stay
    proportional, not growing unboundedly) - matches the established fluid-gutter
    convention (clamp/vw + high max-width).

- T7: Widen the create-entity modals and size their inputs.
  - approach: code
  - files-touched: [angular/src/app/patients/patient/components/patient-detail.component.html, angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney-detail.component.html, angular/src/app/defense-attorneys/defense-attorney/components/defense-attorney-detail.component.html]
  - root: ABP default ~550px modal + Bootstrap col grid -> cramped inputs +
    whitespace.
  - acceptance: New Patient / New AA / New DA modals use the available width; inputs
    are wide enough for typical values (no cramped single-column on desktop).

## Progress (2026-06-17)

- DONE + live-verified on Falkinstein: T1 (calendar/SSN/trash buttons inline -
  measured same-row), T2 (available dates render green rgb(25,135,84), 27 days),
  T3 (Website label - compiled; literal swap), T4 (audit Time -> "Jun 18, 2026,
  3:12 AM"), T5 (Start/End date labels + 150px inputs on one row, change-logs +
  reports).
- RECLASSIFIED to FE+Design (out of this batch): T6 and T7. Both turned out to
  manipulate the redesign's intentional layout system, not mechanical CSS:
  - T7 (#5): the live create/edit modal is `app-people-edit-modal` (a 860px
    `.ra-modal--lg` with a 12-col `.ra-grid`, fields spanning `col-N`), NOT the
    legacy `abp-modal` detail components first targeted (those are unused by the
    redesign - my edits there were reverted). Tuning field spans/width is design.
  - T6 (#8): the three pages cap content at deliberate fixed widths (`.mp-wrap`
    920px, `.ad-wrap` 1080px with an `.ad--wide` 1560px variant, `.ra-wrap`
    1100px), with header/nav/footer pinned to match. Choosing new widths is design.

## Deferred (not built this batch)

- #1c underlines: no global rule found; sidebar links already none. Needs Adrian to
  point at the affected buttons before it can be scoped.

## Risk / Rollback

- Blast radius: UI/CSS only, across the booking form, two list pages, three create
  modals, the audit table, and three page wrappers. No logic, data, or API change.
- Highest side-effect risk: T1 (.input-group) and T6 (gutters) - both can affect
  other screens that share the styles. Scope to the targeted elements; live-check
  neighbors.
- Rollback: revert the squash commit on feat/frontend-rework.

## Verification

Live on Falkinstein, per task (stack already up). For each:
- T1: open the staff add-appointment form; confirm calendar/eye/trash buttons sit
  inline (bounding-box top equals the input's).
- T2: open the datepicker after selecting type+location; confirm available days are
  visually distinct from disabled days.
- T3: attorney steps show "Website".
- T4: /admin/audit Time column is readable.
- T5: change-logs + reports show two capped, labeled date inputs on one line.
- T6: widen the viewport on my-profile / appointment view / request wizard; gutters
  stay proportional.
- T7: open each create modal; inputs use the width.
No automated tests (code approach).
