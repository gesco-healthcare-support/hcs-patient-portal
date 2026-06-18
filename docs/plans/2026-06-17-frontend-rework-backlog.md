---
doc: frontend-rework-backlog
date: 2026-06-17
type: reference
status: living
base-branch: feat/frontend-rework
source: Adrian post-merge testing observations (13-item list)
---

# Frontend Rework - Post-Merge Backlog

Triage of Adrian's 13-item observation list captured after the send-back
(Prompt 17) merge, ahead of the testing / bug-fix phase. Grounded in 8 read-only
code probes plus 2 live diagnoses on Falkinstein. This is the single source for
the categorization, the locked design decisions, and the two reproduced bugs.

Buildable plan for the frontend-only subset: `2026-06-17-fe-quick-batch.md`.

## Legend

- FE         = frontend change, mechanical (CSS / template), no design artifact.
- FE+Design  = frontend change that needs UX / layout design work.
- Full-stack = needs backend (entity / endpoint / migration / permission).
- Diagnose   = reproduce / clarify before it is categorizable.

## Categorization

| # | Item | Category | Backend | Key fact |
| --- | --- | --- | --- | --- |
| 1a/1b | Sidebar: collapse Configuration + People groups | FE+Design | No | Menu is a hardcoded Angular array (internal-nav.config.ts:50). Config hub already renders an in-page rail of the same siblings (duplication). |
| 1c | No underlines on button labels | FE (diagnose) | No | No global underline rule found; sidebar links already text-decoration:none. Underlines are scoped hover states. Need Adrian to point at which buttons. |
| 2 | Doctor Availabilities week-view rework | Full-stack | Yes | Per-day available/booked/reserved counts already computed FE-side; patient names per slot are NOT in the DTO. Patient-name chips need a new endpoint. |
| 3 | WCAB Offices whitespace | FE+Design | No (route) | Standard filter+table+modal, ~40% whitespace. A "Locations" page already exists under Scheduling. |
| 4 | Document Types: one doc -> mark which appointment types need it | Full-stack | Yes | Today 1 row per (name, appointment-type) by design ("Medical Records" seeded 3x). Inversion = new M2M + dedupe migration + app service + UI. |
| 5 | New Patient/AA/DA modals: whitespace + tiny inputs | FE+Design (reclassified) | No | Live modal is `app-people-edit-modal` (860px `.ra-modal--lg`, 12-col `.ra-grid`, fields use `col-N` spans) -- NOT the legacy `abp-modal` detail components. Tuning the field grid is design. |
| 6 | Notification Templates rework (formatting + list) | Full-stack | Yes | Stored plain text, no format field; email sender hardcodes IsBodyHtml=true across ~59 handlers. |
| 7 | Date filters: smaller + start/end labels | FE | No | .ia-input width:100% forces full width. Native type=date ignores placeholder -> needs labels. |
| 8 | Hardcoded side margins (my-profile, request, view) | FOLDED into #16 | No | These three are the external redesign pages; the systemic version is #16. Track there. |
| 9 | AA/DA edit name + firm; unlock; keep past appts | Full-stack | Yes | No self-edit endpoint exists. Appointments resolve firm by FK to a shared master, so an edit changes past appts unless we snapshot. |
| 10 | Dashboard "Requests over time" graph | FE+Design | Yes (small) | Backend already returns real 6-week date windows; FE labels them "Wk N" with no y-axis. Completion line needs per-status counts. |
| 11 | /users/internal 403 for staff supervisor | Full-stack (bug) | Yes | DIAGNOSED below. |
| 12 | Audit Time column raw format | FE | No | internal-admin-hub.component.html:399 binds executionTime raw. Add a date pipe. |
| 13a/b/d | Calendar / SSN-eye / trash buttons wrap to next line | FE | No | Same root cause: Bootstrap .input-group flex-wrap in narrow columns. One shared fix. |
| 13c | "Web Address" -> "Website" (AA + DA) | FE | No | Hardcoded string (appointment-add-attorney-section.component.html:78); i18n key exists. |
| 13 (highlight) | Highlight available appointment dates | FE | No | DIAGNOSED below. Class applied, style not rendering. |
| 13e | Review page: full claim info + all fields | FE+Design | No | Today shows "{N} claim entries added" + a subset. Full-mirror rework. |
| 14 | Appointment-detail change log shows nothing (incl. booker resubmit edits) | Full-stack | Yes | No inline panel; the change-log route projects ABP audit over only 5 entity types, and resubmit writes to Patient/DefenseAttorney (not scanned) -> ~no rows. Lever: B2 `GetHistoryAsync` already returns plain-language who/when/old->new, just not shown here or to external users. Effort L. |
| 15 | Real draft save/resume on the booking wizard | Full-stack | Yes | "Draft saved" pill is cosmetic; a localStorage autosave exists but is wiped on navigate-away (ngOnDestroy). No backend draft, no nav guard, no expiry worker. Needs AppointmentDraft entity + migration + AppService + save-on-Continue + CanDeactivate modal + resume + a first-of-its-kind background cleanup worker. Effort XL; holds PHI -> tdd. |
| 16 | Systemic excess margins on wide screens (supersedes #8) | FE+Design | No | Internal shell already fluid (`.in-content` clamp+2240px, Prompt 15); only outlier is internal detail `.ad--wide` (1560px cap), effort S. External redesign pages keep fixed caps (`.ad-wrap` 1080, `.ra-wrap` 1100, `.mp-wrap` 920) + banded sub-wrappers -> convert to a shared fluid-gutter mixin across _ad-detail/_ra-wizard/_mp-profile, effort M. |

## Locked decisions

| # | Decision | Effect on scope |
| --- | --- | --- |
| 4 | One DocumentType record + M2M, edited from the document side | Full-stack: new join entity + dedupe migration + app service + UI rework |
| 6 | WYSIWYG rich-text editor | Pipeline already sends HTML (IsBodyHtml=true), so keep that; store sanitized HTML, render it in preview. Work is FE editor + server-side HTML sanitization + list pagination |
| 9 | Snapshot firm/name onto the appointment at booking | Full-stack: denormalize firm/name onto Appointment + capture at booking + new self-edit endpoint + FE unlock |
| 2 | Full scope incl. patient-name chips | Full-stack: new endpoint mapping slots -> booked/reserved patients + FE status-bar/chip rework |
| 1a/1b | Collapsible accordion sidebar groups | FE + design |
| 3 | Merge WCAB into the Configuration hub ("Locations") | FE/IA: routing + hub entry; near-empty standalone page retired |
| 10 | Volume bars + completion line, real dates + y-axis | Full-stack (small): backend adds completed/approved counts per week; FE rebuilds the chart |
| 13e | Full mirror of the form in review (incl. empty fields + full claim entries) | FE + design |

## Diagnoses (reproduced live on Falkinstein, 2026-06-17)

### #11 - /users/internal 403 for staff supervisor (Full-stack bug)

- Account stafsuper1 (role "Staff Supervisor") carries CaseEvaluation.InternalUsers
  + .Create + .Edit (109 granted policies). Route guard (InternalUsers.Create)
  PASSES, so the page renders.
- The page's data call GET /api/app/user-extended returns 403, tripping the global
  "You don't have access" overlay and leaving the table empty.
- Root cause: UserExtendedAppService (UserExtendedAppService.cs:17) extends ABP's
  IdentityUserAppService with NO method override and NO [Authorize], so its
  list/create/update/delete inherit ABP's AbpIdentity.Users.* permissions. Staff
  Supervisor lacks AbpIdentity.Users. Route and endpoint are gated by two
  different permission families.
- DECISION: re-gate the app-service CRUD methods to CaseEvaluation.InternalUsers.*
  (least-privilege; aligns route + endpoint). NOT a reseed.

### #13 - appointment date field (FE only)

1. Available-date highlight is wired but invisible. The availability fetch fires
   (GET /api/app/doctor-availabilities/lookup -> 200) and 27 June days get the
   `.available-day` class. But computed style on those days is background
   transparent + navy text, identical to non-available days. Likely a
   ViewEncapsulation mismatch: the [dayTemplate] lives in
   app-appointment-add-schedule, the green `.available-day` rule lives in the
   parent appointment-add.component.scss, so the emulated style never matches the
   projected span. (MEDIUM confidence; confirm at fix time.)
2. Calendar button wraps below the input - confirmed by bounding boxes (input
   bottom y515, button top y515 and left of input right). Cause is the shared
   Bootstrap .input-group { flex-wrap: wrap } in a narrow column - same root cause
   as the SSN eye (#13b) and the claim-modal trash button (#13d).

## Sequencing

1. Frontend-only batch (`2026-06-17-fe-quick-batch.md`) -- SHIPPED + verified:
   items 7, 12, 13a/b/d (button wraps), 13c (Website), and the #13 available-day
   highlight. Item 1c deferred pending Adrian locating the underlined buttons.
2. Reclassified to FE+Design during build (touch the redesign layout system):
   #5 (people-edit-modal grid) and #8 (page max-widths) -- moved to bucket 3.
3. Remaining (separate plans, per item, via RPE):
   - FE+Design = 1a/1b, 3, 5, 10, 13e, 16 (systemic margins; subsumes 8).
   - Full-stack = 2, 4, 6, 9, 11, 14 (detail change log), 15 (draft save/resume).

## New items added 2026-06-18 (investigation findings)

- #14 (change logs): the per-appointment change-log surface is an ABP-audit
  projection scanning only 5 entity types with deny-by-default redaction, so
  booker resubmit edits (written to Patient/DefenseAttorney) produce almost no
  visible rows, and it is internal-only + one click off the detail page. The B2
  `GetHistoryAsync` (AppointmentInfoRequestsAppService) already returns
  human-readable rounds (who/when/old->new) -- the cheapest path is to surface
  that on the detail page (and an external version), not to widen the audit scan.
- #15 (draft save): the wizard backs BOTH internal /appointments/add and external
  /appointments/request. "Draft saved" is a static label; a localStorage autosave
  exists but ngOnDestroy wipes it on navigate-away. No backend draft entity,
  status, nav-guard, or background worker exist. Auto-discard-when-date-passed
  requires a persisted draft + a recurring worker (none today). XL, PHI-bearing.
- #16 (margins): internal is ~95% already fluid (Prompt 15 `.in-content`); the
  only internal fix is the `.ad--wide` 1560px cap on the detail page (~3 lines,
  S). External redesign pages (`.ad-wrap`/`.ra-wrap`/`.mp-wrap` + their banded
  header/nav/footer sub-wrappers) still use fixed centered columns; fix is one
  shared fluid-gutter mixin (clamp + high max-width) across 3 partials (M). Leave
  the centered-card state/public pages narrow (intentional).
