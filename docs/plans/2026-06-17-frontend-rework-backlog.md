---
doc: frontend-rework-backlog
date: 2026-06-17
updated: 2026-06-19
type: reference
status: living
base-branch: feat/frontend-rework
source: Adrian post-merge testing observations (13-item list + 3 follow-ups)
---

# Frontend Rework - Post-Merge Backlog

Triage of Adrian's observation list captured after the send-back (Prompt 17) merge,
ahead of the testing / bug-fix phase. Grounded in read-only code probes plus live
diagnoses on Falkinstein. Single source for the categorization, the locked design
decisions, the reproduced bugs, and the running done/open status.

Buildable plan for the frontend-only subset: `2026-06-17-fe-quick-batch.md` (SHIPPED).
New feature spawned from this backlog: `2026-06-19-appointment-change-control.md` (draft,
post-multi-tenant).

## Legend

- FE         = frontend change, mechanical (CSS / template), no design artifact.
- FE+Design  = frontend change that needs UX / layout design work.
- Full-stack = needs backend (entity / endpoint / migration / permission).
- Diagnose   = reproduce / clarify before it is categorizable.
- Status: DONE (commit) / PARTIAL / OPEN / DEFERRED (-> where).

## Categorization

| # | Item | Status | Category | Backend | Key fact |
| --- | --- | --- | --- | --- | --- |
| 1a/1b | Sidebar: collapse Configuration + People groups | DONE (8f7a66d) | FE+Design | No | Menu is a hardcoded Angular array (internal-nav.config.ts:50); shipped as collapsible accordion groups. |
| 1c | No underlines on button labels | DONE (b1685a1) | FE | No | Root cause: `.af-btn`/`.ap-btn` used on `<a>` elements -> browser anchor underline. Fix: `text-decoration:none` on both; 5 stray hover underlines also removed. |
| 2 | Doctor Availabilities week-view rework | DONE (A: 0f904e7) | Full-stack (no migration) | Yes | Added a bulk slot->patient-names read endpoint (mirrors GetActiveCountsForSlotsAsync + Patient join); week grid now renders per-slot patient chips. Verified end-to-end (endpoint 200 returns names; chip renders on a reserved slot). |
| 3 | WCAB Offices whitespace | DONE (2885512) | FE+Design | No (route) | Standard filter+table+modal, ~40% whitespace. A "Locations" page already exists under Scheduling. Decision: merge into the Configuration hub. |
| 4 | Document Types: one doc -> mark which appointment types need it | OPEN | Full-stack | Yes | Today 1 row per (name, appointment-type) by design ("Medical Records" seeded 3x). Inversion = new M2M + dedupe migration + app service + UI. |
| 5 | New Patient/AA/DA modals: whitespace + tiny inputs | DONE (b1685a1) | FE+Design | No | Live modal is `app-people-edit-modal`. Shipped: `ra-modal--xl` (1040px) + fields `col-4`->`col-6` (2-up); field placeholders added (f84e998). |
| 6 | Notification Templates rework (formatting + list) | DONE (A: b06f453, 51ae631, 14d4084) | Full-stack (no migration) | Yes | ngx-quill WYSIWYG on BodyEmail + caret ##Var## insert; server-side Ganss HtmlSanitizer in UpdateAsync (preserves ##Token## URLs); client-side list pager (20/pg). Correction to the old note: BodyEmail is already nvarchar(max) sent with IsBodyHtml=true at dispatch, so it was already HTML -- no schema/format-field change needed. Verified live. |
| 7 | Date filters: smaller + start/end labels | DONE (d5ebeaf) | FE | No | `.ia-input` capped (~150px) + Start/End labels added; change-logs + reports. |
| 8 | Hardcoded side margins (my-profile, request, view) | DONE (via #16, bf14ebd) | FOLDED into #16 | No | Shipped as part of the systemic #16 fluid-gutter pass. |
| 9 | AA/DA edit name + firm; unlock; keep past appts | OPEN | Full-stack | Yes | No self-edit endpoint exists. Appointments resolve firm by FK to a shared master, so an edit changes past appts unless we snapshot. |
| 10 | Dashboard "Requests over time" graph | DONE (2abba65, 41a9fe7) | FE+Design + Full-stack (small) | Yes | Bars + real 6-week dates + y-axis (2abba65, WeekStart on DTO); approved-completions line + Received/Approved legend (41a9fe7, CompletedCount on DTO + per-week query). |
| 11 | /users/internal 403 for staff supervisor | DEFERRED -> multi-tenant prep | Full-stack (bug) | Yes | Re-gate attempt FAILED (see diagnosis). Folded into the cross-tenant access work (IT Admin already has it; Staff Supervisor needs it). |
| 12 | Audit Time column raw format | DONE (d5ebeaf) | FE | No | `executionTime` now bound through a date pipe ("Jun 18, 2026, 3:12 AM"). |
| 13a/b/d | Calendar / SSN-eye / trash buttons wrap to next line | DONE (d5ebeaf) | FE | No | Shared Bootstrap `.input-group` flex-wrap in narrow columns; one fix, buttons now inline. |
| 13c | "Web Address" -> "Website" (AA + DA) | DONE (d5ebeaf) | FE | No | Label swapped on both attorney sections. |
| 13 (highlight) | Highlight available appointment dates | DONE (d5ebeaf) | FE | No | ViewEncapsulation fix: green `.available-day` rule moved into the schedule component's SCSS; 27 days render rgb(25,135,84). |
| 13e | Review page: full claim info + all fields | DONE (cce23d7, 140162e) | FE+Design | No | Full-mirror review + patient gender now shown. |
| 14 | Appointment-detail change log shows nothing (incl. booker resubmit edits) | DONE (B: e57423c..e0996c7) | Full-stack -> FE-only | No | Session B surfaced the existing `GetHistoryAsync` rounds on the internal change-log page + a lighter external-detail summary (no backend/migration needed -- the endpoint + read-guard already admit external parties). Incl. a markForCheck fix for the stuck-loading change-log page. |
| 15 | Real draft save/resume on the booking wizard | OPEN | Full-stack | Yes | "Draft saved" pill is cosmetic; localStorage autosave is wiped on navigate-away (ngOnDestroy). Needs AppointmentDraft entity + migration + AppService + save-on-Continue + CanDeactivate modal + resume + first-of-its-kind background cleanup worker. Effort XL; holds PHI -> tdd. |
| 16 | Systemic excess margins on wide screens (supersedes #8) | DONE (bf14ebd) | FE+Design | No | Shipped shared fluid-gutter (clamp + high max-width): external detail/wizard ->1560, my-profile ->1100, internal detail `.ad--wide` ->2240. |
| 17 | Sidebar: only Configuration + People collapsible, moved to bottom | DONE (c7fab6b) | FE | No | Refinement of #1a/1b: Workspace/Scheduling/Administration stay open; `collapsible` flag on the nav group + a static header for the rest. |

## Locked decisions

(Most have shipped; retained as design rationale. See Status column above.)

| # | Decision | Effect on scope |
| --- | --- | --- |
| 4 | One DocumentType record + M2M, edited from the document side | Full-stack: new join entity + dedupe migration + app service + UI rework |
| 6 | WYSIWYG rich-text editor | Pipeline already sends HTML (IsBodyHtml=true), so keep that; store sanitized HTML, render it in preview. Work is FE editor + server-side HTML sanitization + list pagination |
| 9 | Snapshot firm/name onto the appointment at booking | Full-stack: denormalize firm/name onto Appointment + capture at booking + new self-edit endpoint + FE unlock |
| 2 | Full scope incl. patient-name chips | Full-stack: new endpoint mapping slots -> booked/reserved patients + FE status-bar/chip rework |
| 1a/1b | Collapsible accordion sidebar groups | SHIPPED |
| 3 | Merge WCAB into the Configuration hub ("Locations") | FE/IA: routing + hub entry; near-empty standalone page retired |
| 10 | Volume bars + completion line, real dates + y-axis | Bars/dates/y-axis SHIPPED; completion line still needs backend completed/approved counts per week |
| 13e | Full mirror of the form in review (incl. empty fields + full claim entries) | SHIPPED |

## Diagnoses (reproduced live on Falkinstein)

### #11 - /users/internal 403 for staff supervisor (DEFERRED -> multi-tenant prep)

- Account stafsuper1 (role "Staff Supervisor") carries CaseEvaluation.InternalUsers
  + .Create + .Edit. Route guard (InternalUsers.Create) PASSES, so the page renders.
- The page's data call GET /api/app/user-extended returns 403, tripping the global
  "You don't have access" overlay and leaving the table empty.
- Root cause: UserExtendedAppService extends ABP's IdentityUserAppService; its CRUD
  inherits ABP's AbpIdentity.Users.* permissions. Staff Supervisor lacks AbpIdentity.Users.
  Route and endpoint are gated by two different permission families.
- ATTEMPTED FIX (FAILED, branch fix/internal-users-403, un-merged):
  1. Override GetListAsync/GetAsync/UpdateAsync with [Authorize(CaseEvaluation.InternalUsers.*)]
     -- did NOT work: ABP AND-combines inherited method [Authorize] (AuthorizeAttribute is
     Inherited=true), so the override ADDS a requirement, it does not replace AbpIdentity.Users.
  2. Grant AbpIdentity.Users to the tenant Staff Supervisor role -- did NOT take effect even
     after --no-cache db-migrator rebuild + reseed + Redis FLUSHALL: AbpIdentity.Users is
     host-restricted / not grantable to a tenant-scoped role.
- DECISION: fold into multi-tenant prep. Needs a non-inheriting internal-users service AND a
  cross-tenant access design (IT Admin already has cross-tenant; Staff Supervisor needs it).
  See ~/.claude memory abp-identity-appservice-regating-and-tenant-grants.

### #13 - appointment date field (FE only) -- RESOLVED

1. Available-date highlight: was invisible due to a ViewEncapsulation mismatch ([dayTemplate]
   in app-appointment-add-schedule, green rule in the parent SCSS). Fixed by moving the
   `.available-day` rule into the schedule component. Days now render green.
2. Calendar/SSN/trash buttons wrapped below the input via the shared Bootstrap
   `.input-group { flex-wrap: wrap }` in narrow columns. Fixed.

## Status summary (2026-06-19)

- SHIPPED on feat/frontend-rework: 1a/1b, 1c, 5, 7, 8 (via 16), 12, 13a/b/d, 13c,
  13-highlight, 13e, 16, 10 (complete -- completion line 41a9fe7), #3 (WCAB in the
  config hub, 2885512), #17 (sidebar collapse refinement, c7fab6b), and the #5 field
  placeholders (f84e998).
- DONE since: #14 (Session B, e57423c..e0996c7 -- FE-only surfacing of GetHistoryAsync);
  #2 (Session A, 0f904e7 -- availabilities patient chips + bulk read endpoint, no migration);
  #9 (Session B, ..5daf9a8 -- attorney self-edit + snapshot; reported complete T1-T8 in 4508db6);
  #6 (Session A, b06f453..14d4084 -- notification-template WYSIWYG + server HTML sanitize + pager).
- OPEN (need EF migrations -> Session B's lane): 4, 15.
- BUG TRIAGED 2026-06-19 (was "Appointments list shows 0 rows while chips count 4" + console
  error): NOT a query/visibility bug. GetListAsync + GetStatusCountsAsync share
  ComputeExternalPartyVisibilityAsync, which returns null (no narrowing) for internal users
  (AppointmentsAppService.cs:118,148,209) -- so for a supervisor they apply identical filters
  and cannot persistently disagree. A status-filter-only mismatch would return HTTP 200 with 0
  items and NO console error; the reported error means the list REQUEST itself failed (token
  expiry mid-session is the prime suspect) while the separate counts call succeeded. The FE then
  leaves the list empty on error (internal-appointments.component.ts:230,
  `error: () => this.loading.set(false)` -- no toast, no error state), so first-load failure
  reads as "Showing 0-0 of 0" beside populated chips. Not reproducing now (list=counts=6, no
  console errors). FIX (Session B's appointments lane): surface a load failure (toaster +
  retry/error state) and distinguish "no appointments" (200, 0 items) from "failed to load",
  instead of the silent empty list.
- DEFERRED: 11 (-> multi-tenant prep), appointment-change-control (-> post-multi-tenant,
  own spec 2026-06-19-appointment-change-control.md).

## Fastest order to knock down what is left

Weekend sequence overall: bug list (below) -> multi-tenant implementation + infra +
2-tenant live test -> appointment-change-control build.

Tier 1 -- genuinely fast, low/no backend (clear these first):
1. [DONE 2885512] #3 WCAB -> rail entry + own component in the Configuration hub shell.
2. #10 completion line. Backend adds per-week completed/approved counts to the existing
   dashboard DTO/builder (WeekStart already added); FE draws a line over the shipped bars. S.

Tier 2 -- medium, high user value:
3. #14 detail change log. Surface B2 `GetHistoryAsync` on the appointment detail page +
   an external read path (cheaper than widening the audit scan). M.

Tier 3 -- large full-stack, each its own RPE plan (order by value/risk):
4. #9 AA/DA self-edit + snapshot-at-booking (denormalize + capture + self-edit endpoint). L.
5. #4 document types one-record + M2M (join entity + dedupe migration + app service + UI). L.
6. #6 notification templates WYSIWYG + server-side sanitization + list pagination. L.
7. #2 doctor availabilities week-view + patient-name chips (new slots->patients endpoint). L.

Tier 4 -- XL, schedule on its own (do NOT cram into the fast pass):
8. #15 draft save/resume (AppointmentDraft entity + migration + AppService + CanDeactivate
   guard + resume + a first-of-its-kind background cleanup worker; PHI-bearing -> tdd). XL.

Realistic fast pass before pivoting to multi-tenant = Tier 1 (+ #14 if time). The Tier 3
items are roughly a day each; decide how many to clear before the multi-tenant pivot.

## New items added 2026-06-18 (investigation findings)

- #14 (change logs): per-appointment change-log is an ABP-audit projection scanning only 5
  entity types with deny-by-default redaction; booker resubmit edits (Patient/DefenseAttorney)
  produce almost no visible rows, and it is internal-only + one click off the detail page.
  Cheapest path: surface B2 `GetHistoryAsync` on the detail page (+ external version).
- #15 (draft save): the wizard backs BOTH internal /appointments/add and external
  /appointments/request. "Draft saved" is a static label; localStorage autosave is wiped on
  navigate-away. No backend draft entity, status, nav-guard, or worker exist. XL, PHI-bearing.
- #16 (margins): SHIPPED. Internal was ~95% already fluid; the fix converted the external
  redesign pages + internal `.ad--wide` to a shared fluid-gutter (clamp + high max-width).

## New items added 2026-06-19

- appointment-change-control (NEW FEATURE, spec'd, deferred to post-multi-tenant). Internal
  staff get direct edit/reschedule/cancel from request time; external parties route every
  change (date/type/cancel + any field) through one unified request-and-approve path that
  can never apply unilaterally, with opposing-party consent on Tier-A (date/type/cancel).
  Builds on the existing opposing-party consent-link infrastructure (Group D, 2026-06-09).
  Full spec + 15-task breakdown: 2026-06-19-appointment-change-control.md. Supersedes the
  lost "pre-approval reschedule/cancel" change.
