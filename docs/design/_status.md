---
type: design-status-tracker
audited: 2026-05-04
purpose: Per-feature design.md work tracker. Updated after every feature commit so anyone can see what's done, draft, or descoped at a glance.
---

# design.md status tracker

The single source of truth for which Phase 1 features have a design.md
and what state it is in. The frontend implementation tracks (Phase 19a,
Phase 19b, per-feature UI build-outs) read from this file to know what
contract they are building against.

## Status legend

| Status | Meaning |
|---|---|
| `not-started` | No `<feature>-design.md` file yet. |
| `draft` | design.md exists, missing screenshots or open OLD-source citations. |
| `screenshots-captured` | OLD + (where applicable) NEW screenshots present in `screenshots/old/<feature>/` and `new/<feature>/`. design.md not yet finalised. |
| `complete` | design.md complete + screenshots present + OLD citations spot-checked. |
| `descoped-YYYY-MM-DD` | Out of scope by Adrian's call. One-line rationale in the row. |

## Tooling baseline (this session)

- OLD app running: `http://localhost:4201` (verified via curl 2026-05-04, returns 200).
- NEW Angular: NOT running (no docker stack up). NEW screenshots are
  deferred. Each design.md whose NEW UI exists in `angular/src/app/`
  describes the code-level current state from the source instead.
- chrome-devtools-mcp: available in this session via plugin
  `chrome-devtools-mcp:chrome-devtools`. OLD captures via that MCP.

## Tier 1 -- features with no NEW Angular UI yet

These need design.md before whoever picks up the matching backend
phase starts the UI. Listed in the priority order from the kickoff
prompt.

| # | Feature | design.md path | Status | OLD screenshots | NEW screenshots | Notes |
|---|---------|----------------|--------|-----------------|-----------------|-------|
| 1 | external-user-registration | `external-user-registration-design.md` | draft | pending | n/a (no NEW UI) | Phase 8 UI dependency |
| 2 | external-user-forgot-password | `external-user-forgot-password-design.md` | draft | pending | n/a | Phase 10 UI dependency; backend already shipped 2026-05-03 |
| 3 | external-user-appointment-cancellation | `external-user-appointment-cancellation-design.md` | draft | pending | n/a | Phase 15 UI (Session A); backend already shipped 2026-05-04 |
| 4 | external-user-appointment-rescheduling | `external-user-appointment-rescheduling-design.md` | draft | pending | n/a | Phase 16 UI (Session A); backend phases 11c/11j done |
| 5 | staff-supervisor-change-request-approval | `staff-supervisor-change-request-approval-design.md` | draft | pending | n/a | Phase 17 UI; covers 4 OLD components (list, detail, edit, view) |
| 6 | it-admin-system-parameters | `it-admin-system-parameters-design.md` | draft | pending | n/a | Backend AppService done 2026-05-03; Angular UI pending |
| 7 | it-admin-notification-templates | `it-admin-notification-templates-design.md` | draft | pending | n/a | Phase 18 renderer done 2026-05-04; Angular UI pending |
| 8 | it-admin-custom-fields | `it-admin-custom-fields-design.md` | draft | pending | n/a | Standalone admin page |
| 9 | it-admin-package-details | `it-admin-package-details-design.md` | draft | pending | n/a | Documents + Packets CRUD |
| 10 | it-admin-user-management | `it-admin-user-management-design.md` | draft | pending | partial (host admin /identity/users captured 2026-04-24) | ABP-Identity-delegated; capture branding overrides only |

## Tier 2 -- features with NEW Angular UI but no spec

Delta hunt: what does NEW miss vs OLD, and what is intentional NEW
deviation.

| # | Feature | design.md path | Status | OLD screenshots | NEW screenshots | Notes |
|---|---------|----------------|--------|-----------------|-----------------|-------|
| 11 | external-user-appointment-request | `external-user-appointment-request-design.md` | draft | partial (`old/patient/02-book-appointment.png`, `old/admin/02-book-appointment.png`) | n/a (NEW running stack down) | Booking form, ~2200 LOC NEW |
| 12 | external-user-view-appointment | `external-user-view-appointment-design.md` | draft | pending | n/a | View page, ~1600 LOC NEW |
| 13 | staff-supervisor-doctor-management | `staff-supervisor-doctor-management-design.md` | draft | pending | n/a | Doctor list + availability + locations |
| 14 | clinic-staff-appointment-approval | `clinic-staff-appointment-approval-design.md` | draft | pending | n/a | Pending approval flow |
| 15 | clinic-staff-document-review | `clinic-staff-document-review-design.md` | draft | pending | n/a | Document accept/reject UI |
| 16 | external-user-appointment-package-documents | `external-user-appointment-package-documents-design.md` | draft | pending | n/a | External-user package upload |
| 17 | external-user-appointment-ad-hoc-documents | `external-user-appointment-ad-hoc-documents-design.md` | draft | pending | n/a | Ad-hoc upload (shares chrome) |
| 18 | external-user-appointment-joint-declaration | `external-user-appointment-joint-declaration-design.md` | draft | pending | n/a | JDF upload (AME only) |
| 19 | internal-user-dashboard | `internal-user-dashboard-design.md` | draft | partial (`old/admin/01-dashboard.png`, `old/staff/01-dashboard.png`, `old/supervisor/01-dashboard.png`) | partial (`new/admin/02-dashboard.png`, `new/t1-doctor/02-tenant-dashboard-placeholder.png`) | Counter cards + widgets |

## Tier 3 -- smaller surfaces

| # | Feature | design.md path | Status | OLD screenshots | NEW screenshots | Notes |
|---|---------|----------------|--------|-----------------|-----------------|-------|
| 20 | external-user-login | `external-user-login-design.md` | draft | pending (auth-shell variant 1; same chrome as register/forgot) | partial (`new/_non-role/01-login-page.png`) | Login (auth-shell variant 1) |
| 21 | external-user-submit-query | `external-user-submit-query-design.md` | draft | pending | n/a | Help/Query form modal |
| 22 | internal-user-view-all-appointments | `internal-user-view-all-appointments-design.md` | draft | pending | partial (`new/admin/03-appointments-list-host-context.png`, `new/t1-doctor/03-appointments-list-13-rows.png`) | All-appointments list + filters |
| 23 | internal-user-reports | `internal-user-reports-design.md` | draft | pending | n/a | Reports landing + Excel/PDF export |
| 24 | clinic-staff-check-in-check-out | `clinic-staff-check-in-check-out-design.md` | draft | partial (`old/admin/03-checkin-checkout.png`) | n/a | Day-of check-in/out view |
| 25 | master-data-crud | `master-data-crud-design.md` | not-started | pending | n/a | States, locations, WCAB offices, types, statuses, languages |
| 26 | application-configurations | `application-configurations-design.md` | not-started | pending | n/a | App config screen |
| 27 | appointment-notes | `appointment-notes-design.md` | not-started | pending | n/a | Per-appointment notes UI |
| 28 | appointment-change-log | `appointment-change-log-design.md` | not-started | pending | n/a | Per-appointment audit log view |
| 29 | record-locking | `record-locking-design.md` | not-started | pending | n/a | Record-lock indicator/UI element |
| 30 | document-upload-download | `document-upload-download-design.md` | not-started | pending | n/a | Cross-cutting upload primitive |
| 31 | terms-and-conditions | `terms-and-conditions-design.md` | not-started | pending | n/a | T&C modal/page |

## Cross-cutting docs (already in `docs/design/`, do not need design.md)

These shared globals already exist; per-feature design docs cite them
rather than duplicate.

| Doc | Status | Notes |
|---|---|---|
| `_design-tokens.md` | stable | Color/typography/spacing/radius/shadow contract |
| `_shell-layout.md` | stable | 3 shell variants + side-bar matrix |
| `_components.md` | stable | Buttons/forms/modals/toasts/tables/select |
| `_design-doc-template.md` | stable | The skeleton this doc tracks status for |

## Cross-cutting parity audits NOT becoming design.md (referenced only)

These four parity audits document cross-cutting cleanup or deep-dive
investigations rather than user-facing features. They do not get a
design.md.

| Parity audit | Reason no design.md |
|---|---|
| `docs/parity/_appointment-form-validation-deep-dive.md` | Deep-dive investigation -- cross-linked from `external-user-appointment-request-design.md`. |
| `docs/parity/_branding.md` | Cross-cutting; lives at the design-globals layer (consumed by every design.md via the branding-tokens section). |
| `docs/parity/_cleanup-tasks.md` | Phase-0 cleanup tasks -- doctor login removal + AppointmentSendBackInfo removal. Not user-facing. |
| `docs/parity/_old-docs-index.md` | Pointer index. |
| `docs/parity/_slot-generation-deep-dive.md` | Backend-logic deep dive -- cross-linked from `staff-supervisor-doctor-management-design.md`. |
| `docs/parity/scheduler-background-jobs.md` | Backend-only feature (cron jobs); no UI. |

## Open blockers + how they are handled in this session

| Blocker | Handling |
|---|---|
| NEW Angular not running | NEW screenshots deferred; design.md cites code-level current state from `angular/src/app/<feature>/` source instead. |
| chrome-devtools-mcp permission per session | OLD captures done via the parent session's chrome-devtools-mcp tools. |
| OLD source contradicts a parity audit | Surface to Adrian before "correcting" the audit. None encountered yet. |

## Update protocol

After every feature design.md commit, edit this file to advance the
row's status + screenshot column. Squash on merge keeps the history
tidy.
