---
feature: appointment-detail-change-log
date: 2026-06-19
status: in-progress
base-branch: feat/frontend-rework
lane: Session B
related-issues: []
backlog: 2026-06-17-frontend-rework-backlog.md
protocol: 2026-06-19-parallel-build-protocol.md
backlog-item: 14
---

## Goal

Make the appointment change history visible where users expect it, by surfacing the
EXISTING `GetHistoryAsync` (the Send Back / request-info rounds) on two surfaces:

1. Augment the internal "Change log" page (the ABP-audit projection that shows ~nothing
   for booker resubmit edits) with the plain-language info-request rounds.
2. Add a lighter-summary history section to the external appointment-detail page, which
   today shows no history at all.

No backend, no EF migration, no proxy regen. Frontend-only (Angular).

## Context

Backlog #14. Clicking the internal "Change log" shows almost nothing because
`AppointmentChangeLogsAppService` projects ABP audit logs over only 5 entity types
(Appointment + 4 children); booker resubmit edits write to Patient / DefenseAttorney,
which are not scanned, so the page is near-empty. A richer plain-language history already
exists -- `AppointmentInfoRequestsAppService.GetHistoryAsync(appointmentId)` over the
`AppointmentInfoRequest` table (the 2026-06-14 Send Back redesign) -- and is already
surfaced INLINE on the internal detail page as a working "Request history" section. The
external detail page has neither.

Decisive research finding (why this is frontend-only): `GetHistoryAsync` gates on
`AppointmentReadAccessGuard.EnsureCanReadAsync`, which explicitly admits external parties
(creator / patient identity / accessor grant / email+role match -- the same rule as the
production list query). The manual `AppointmentInfoRequestController` already exposes
`GET /api/app/appointment-info-requests/history/{id}`, and the Angular proxy already has
`AppointmentInfoRequestService.getHistory()`. So external parties already pass the guard
and the endpoint + proxy already exist -- nothing new on the backend.

HIPAA: SSN is masked to last-4 at capture (`InfoRequestSnapshot.MaskSsn`); diffs cover
only flagged scalar fields; documents are excluded. The external "lighter summary" omits
field-level old->new values entirely, so it exposes nothing beyond what the external
detail page already shows the same party.

### Locked decisions (from brainstorming with Adrian, 2026-06-19)

- Internal Change-log page: AUGMENT with the info-request rounds (keep the audit rows).
- External history: LIGHTER SUMMARY -- note + who/when + change counts; NO field-level
  old->new values.
- Internal inline "Request history" (on the detail page): already works -- leave it alone.
- Rounds and audit rows render as SEPARATE sections on the change-log page (two different
  data shapes; no shared sort key) -- not interleaved.
- External "Requested by" is genericized to "HCS staff" (do not expose the staff member's
  name to external parties). The resubmitter name is the external party themselves -- shown
  as-is. History visible to any party that passes the existing read guard (same set as the
  detail page itself).
- External history section is hidden entirely when there are no rounds (most appointments
  never had a send-back) -- no empty nav link, no empty section.

## Parallel-build safety (Session A conflict check -- CLEAR)

- Session A (integrator) is mid-flight on #3 (config hub + WCAB) and #10 (dashboard line).
  Its files: `configuration/*`, `wcab-offices/*`, `dashboard/*`, `proxy/dashboards/*`,
  `Dashboards/*.cs`, and shared `internal-nav.config.ts`. #3 not yet committed.
- This plan's files are all under `appointments/*` -- DISJOINT from Session A's set.
- No shared-file edits: no `app.routes.ts` (change-log route already exists; no new nav
  entry), no `internal-nav.config.ts`, no shared `_*.scss` (new component is
  component-scoped; reuses global CSS-variable tokens read-only).
- No EF migration (Session B's exclusive lane -- nothing to create here).
- No `abp generate-proxy` (Session A's exclusive lane -- not needed; proxies exist).
- Not gated by #3: #14 does not touch the Configuration hub.
- Do NOT edit `2026-06-17-frontend-rework-backlog.md` during build (Session A has it
  modified uncommitted); the #14 status update is coordinated via Adrian at ship time.

## Approach

A single new standalone presentational component renders the info-request rounds; both new
surfaces host it. Chosen over (a) duplicating the round markup into each page (incidental
duplication across two new consumers) and (b) extracting the internal detail page's
existing inline round markup into a shared component and re-pointing the detail page too
(a refactor of working code -- larger blast radius, needs approval, deferred). The internal
detail page is left untouched.

The component takes an `externalView` flag: when false (internal change-log page) it renders
per-field old->new diffs; when true (external page) it omits diffs, shows a "N of M
requested fields updated" summary, and genericizes the staff requester to "HCS staff".

## Tasks

- T1: Create the `app-info-request-history` presentational component.
  - approach: test-after
  - files-touched (new):
    - angular/src/app/appointments/appointment/components/info-request-history.component.ts
    - angular/src/app/appointments/appointment/components/info-request-history.component.html
    - angular/src/app/appointments/appointment/components/info-request-history.component.scss
  - details: Standalone, OnPush. Inputs: `rounds: AppointmentInfoRequestRoundDto[]`,
    `externalView = false`. Renders rounds in the order received (`getHistory` already
    returns newest-first). Per round: round number, staff note, requested/resolved
    timestamps (date pipe), requester name, resubmitter name, and counts. When
    `!externalView`: render the per-field diffs (`round.diffs`, key -> human label,
    old -> new). When `externalView`: omit diffs; show "{fixedCount} of {flaggedCount}
    requested fields updated"; render requester as "HCS staff"; show resubmitter as-is.
    Reuse the EXISTING send-back field-key -> label map that the internal detail page's
    diff cards already use (do not duplicate the registry); if it is not exported in a
    reusable form, lift it to a tiny shared map and have both call sites import it.
    Component-scoped SCSS only (no shared `_*.scss`); use global CSS-variable design tokens.
  - acceptance: a karma spec (headless, targeted) asserts: externalView=true renders no
    diff rows and shows the "N of M" summary + "HCS staff"; externalView=false renders diff
    rows with old/new; empty `rounds` renders nothing. Component compiles; lint clean.
  - test note: run headless targeted per the karma-headless-targeted-run memory
    (CHROME_BIN + `ng test --watch=false --browsers=ChromeHeadless --include=<glob>`); do
    NOT `spyOn(subject,'next')` (karma-no-spy-rxjs-subject-next) -- subscribe + assert a flag.

- T2: Augment the internal Change-log page with the rounds section.
  - approach: code
  - files-touched:
    - angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts
    - angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.html
    - angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.scss (new)
  - implementation note: added a component-scoped `.scss` for the two section
    headings (Resubmit/request history vs Field-level audit) instead of inline
    styles -- keeps the page clean and touches no shared `_*.scss`.
  - details: Inject `AppointmentInfoRequestService`; in `ngOnInit` call `getHistory(id)`
    alongside the existing `getByAppointment(id)` (independent subscriptions, each with its
    own loading/error flag, or `forkJoin` -- keep the audit timeline rendering even if the
    rounds call fails, and vice versa). Add `<app-info-request-history [rounds]="rounds"
    [externalView]="false" />` in a clearly-headed section ABOVE the existing
    `<app-change-log-timeline [rows]="entries" />`. Headings distinguish the two: e.g.
    "Resubmit / request history" (rounds) vs "Field-level audit" (timeline). Add the new
    component to `imports`.
  - acceptance: on an appointment that has send-back rounds, the change-log page shows the
    populated rounds section above the audit timeline; the audit timeline still renders; an
    appointment with no rounds shows only the audit timeline (rounds section hidden).
    Screenshot-verified on Falkinstein as staff supervisor.

- T3: Add the external history section to the external detail page.
  - approach: code
  - files-touched:
    - angular/src/app/appointments/appointment/components/external-appointment-detail.component.ts
    - angular/src/app/appointments/appointment/components/external-appointment-detail.component.html
  - implementation note (open question resolved): used the typed
    `AppointmentInfoRequestService.getHistory()` proxy (not the page's `RestService`
    pattern). Same endpoint, which authorizes external parties via the read guard;
    type-safe and consistent with T2 + the internal detail. No external-auth reason
    to fall back to `RestService`. Nav link + section render only when rounds exist.
  - details: Load the rounds in `ngOnInit` via `loadHistory()`, mirroring the existing
    `loadInfoRequest()` pattern (prefer the `AppointmentInfoRequestService.getHistory()`
    proxy for type safety, consistent with the internal detail; fall back to the existing
    `RestService` pattern only if external auth requires it -- verify at build time). Store
    `historyRounds`. Add a nav link `<a (click)="scrollTo('ad-sec-history')">History</a>` to
    `.ad-nav__links` and an `<div class="ad-card" id="ad-sec-history">` section rendering
    `<app-info-request-history [rounds]="historyRounds" [externalView]="true" />`. Render the
    nav link AND the section only when `historyRounds.length > 0`. Add the new component to
    `imports`.
  - acceptance: as an external applicant attorney (appatty1) on an appointment that had a
    send-back, the external detail shows the lighter-summary history (note, who/when, "N of
    M" counts, NO field values; requester shown as "HCS staff"); SSN and other field values
    are absent. On an appointment with no send-back, no History nav link and no empty
    section. Screenshot-verified on Falkinstein.

- T4: Live verification on Falkinstein (both surfaces, both role contexts).
  - approach: code (verification only -- no new code)
  - details: Internal staff (stafsuper1): open the Change-log page for an appointment with
    rounds; confirm rounds-above-audit layout. External (appatty1): open the external detail
    for the same/an analogous appointment; confirm the lighter summary and the absence of
    field values + staff name. Capture screenshots for the PR (save under .github/pr-media/).
  - acceptance: screenshots captured for both surfaces; no console errors; PHI checks pass
    (no field values or staff name in the external view).

## Execution order

T1 (component + spec) -> T2 (internal page) -> T3 (external page) -> T4 (verify).
Commit small and often, explicit paths only: T1 files, then T2 files, then T3 files.
T1 is committable on its own (component + spec). T2 and T3 each become their own commit.

## Risk / Rollback

- Blast radius: additive, frontend-only, no schema/endpoint/proxy change; confined to
  `appointments/*`. No Session A file overlap, no migration, no regen.
- Rollback: revert the per-task commits independently; each surface is isolated.
- Watch item: the augmented change-log page mixes two data sources -- keep them in clearly
  labeled separate sections so the "audit" vs "resubmit history" semantics are not muddled.
- PHI: confirm in T4 that the external view shows no field-level values and no staff name;
  the lighter-summary mode is the guard.

## Verification

- Backend: none.
- Unit: T1 karma spec (headless, targeted include).
- Live: T4 screenshots on Falkinstein for the internal change-log page and the external
  detail history, per the protocol ("verify UI with screenshots, not just it compiled").
- Lint/build: `ng lint` + a successful Angular build of the touched components.

## Proxy / migration / coordination

- Proxy regen: NOT required (getHistory + change-log proxies already exist).
- EF migration: NOT required.
- Session A handshake: none needed. No shared-file edits. No #3 dependency.

## Build status (2026-06-19)

- T1 (component + spec): DONE, commit e57423c. 3/3 karma specs pass; lint clean.
- T2 (internal change-log augment): DONE, commit 482ad44. Dev build + template
  type-check pass; lint clean.
- T3 (external history section): DONE, commit 2b4386d. Dev build + template
  type-check pass; lint clean.
- T2 follow-up FIX: render change-log via markForCheck, commit e0996c7. During T4
  verification the change-log page sat stuck on "Loading change log" -- the page is
  OnPush but (pre-existing) never called markForCheck, so neither the audit nor the
  new rounds rendered after their HTTP calls returned. This was a real cause of "#14
  shows nothing". Fixed by injecting ChangeDetectorRef + markForCheck in both
  subscribes, matching the global AppointmentChangeLogList component. External detail
  + internal detail are Default CD, so they need no such fix.
- T4 (live Falkinstein screenshots): IN PROGRESS. Verified on Falkinstein (chrome):
  new code is deployed (the "Field-level audit" heading + updated subtitle render),
  getHistory returns 5 real rounds for A00001, and the internal detail inline
  "Request history" renders them. The change-log rounds section was blocked by the
  OnPush bug above; needs a SECOND Angular restart to serve e0996c7, then re-shoot
  both surfaces (internal change-log rounds + external lighter-summary history as
  appatty1, who is A00001's booker/creator -> passes the read guard).
- Test data: A00001 (id e2ea909e-...edf0) already has 5 send-back rounds incl. a real
  DOB diff -- no data setup needed.
- Context note: #3 (config hub + WCAB) landed as commit 2885512 -- unblocks the
  later #4/#6 config-hub UI (not part of #14).
