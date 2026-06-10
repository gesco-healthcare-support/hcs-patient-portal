---
feature: live-ame-ime-pqme-lifecycle-ui-test
date: 2026-06-07
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Drive the real Falkinstein UI (Playwright) through the full live lifecycle of one AME, one
IME, and one PQME appointment -- each booked by a different role, each carrying all four
party mailboxes -- approving, uploading/approving documents, generating packets, and issuing
one change-request each, with a dev-only email-link log so notification links are recoverable
from logs instead of an inbox.

## Context

- Verifying `main` after PR #292 (HTML/WeasyPrint packet pipeline) + PR #296 (exam-intake +
  parties correctness pass, breaking, ships migrations) -- they have never run together on
  the existing Falkinstein demo data. Authoritative app/lifecycle facts:
  `.claude/prompts/main-integration-test-2026-06-05.md`.
- Stack is already up and healthy (main-api :44327, angular :4200, authserver :44368, sql
  :1434, redis, minio, gotenberg :3000, packet-renderer :3001 -> /health lists 4 templates).
- Email REALLY sends in dev (real SMTP creds mounted; `NullEmailSender` swap does not fire),
  so the 12 `@gesco.com` mailboxes receive real mail. The user wants links in logs too.
- Tenant: `http://falkinstein.localhost:4200`. Password for all 12 accounts: `1q2w3E*r`.
- Booking is a single scrollable form at `/appointments/add`; type = seeded AppointmentType
  GUID (AME / IME / PQME). PQME requires a Panel Number; AME/IME reject one. CE name+email
  required at appointment level. Pending->Approved blocked without an active claim examiner.

## Approach

- **UI-only, live stack.** Drive every business action through the Angular UI via Playwright
  MCP exactly as a real user would; verify visually with screenshots, not just API/DOM.
  Reason: matches the user's "live lifecycle just like real-life" intent and the QA brief's
  visual-verification rule. Rejected: seeding/SQL/API shortcuts (violates user feedback
  "Explore, don't assume" + "no appointment-seed proposals"; would hide real UI/migration
  bugs).
- **One small instrumented source change: a config-gated email-link log.** Add a helper in
  `NotificationDispatcher` that, after `RenderAsync`, logs template code + context + recipient
  (To/CC) + subject + extracted URLs at Info -- NO body. Gate behind `Notifications:LogLinks`
  (default false) so single-use invite/reset tokens never reach prod logs. Reason: the
  dispatcher is the single chokepoint for every template-rendered email; a flag keeps it
  inert by default. Rejected: logging full bodies (writes PHI-shaped names/addresses to logs);
  wrapping `IEmailSender` (black-box, misses pre-send link composition); per-handler logging
  (scattered, easy to miss a path).
- **Booker-per-type to exercise three paths:** AME by Clinic Staff (on-behalf), IME by
  Applicant Attorney (external party booker, auto-fills own AA section), PQME by Patient
  (self-book, auto-fills own demographics). Rejected: all-by-staff (misses self/external
  paths the user asked to "test out each of them").
- **Keep the logging edit local + uncommitted.** It is a throwaway dev diagnostic, not a
  feature; do not branch/commit/PR it. Reason: QA brief says no commits/PRs; user authorized
  the edit only to ease this test. Surface it for the user to keep or revert at the end.

## Tasks

### Phase 0 -- Pre-flight (gate)

- T0: Confirm integrated-state health before any functional work.
  - approach: code
  - files-touched: []
  - acceptance: `docker compose ps` shows all Main containers healthy and `db-migrator`
    exited 0; packet-renderer `/health` lists doctor/patient/attorney-ame/attorney-pqme
    (already confirmed); `http://falkinstein.localhost:4200` loads the login page; a DB query
    or login probe enumerates which of the 12 `@gesco.com` accounts exist and their roles.
    If db-migrator did not exit 0 or Falkinstein data is missing/corrupt -> STOP and report.

### Phase 1 -- Email-link logging (the only source edit)

- T1: Add config-gated email-link logging.
  - approach: code
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs`
    - (verify) `Application/Emailing/CaseEvaluationAccountEmailer.cs`,
      `Domain/Invitations/InvitationManager.cs`,
      `Application/Notifications/AccountUrlBuilder.cs` -- add a one-line URL log ONLY if
      invite/confirm/reset emails do NOT already flow through `NotificationDispatcher.RenderAsync`.
    - config: `docker/appsettings.secrets.json` or compose env (`Notifications__LogLinks=true`).
  - acceptance: with the flag ON, triggering any notification writes a single Info line per
    email to `docker logs main-api-1` containing recipient + subject + every `http(s)://` URL
    in the rendered body, and NO body text; with the flag OFF (default) nothing is logged.
    api image rebuilt (`docker compose up -d --build api`) and container recreated with flag on.

### Phase 2 -- Account + prerequisite readiness

- T2: Ensure the 12 accounts can log in; invite + register any missing via UI.
  - approach: code
  - files-touched: []
  - acceptance: each of the 12 `@gesco.com` accounts authenticates at the Falkinstein login
    with `1q2w3E*r`; for any missing account, staff issues an invite via User Management, the
    invite link is read from the log, and the account is registered through the AuthServer
    Register page. Screenshot of a successful login per role used as a booker.
- T3: Ensure approval prerequisites exist (verify the CE gate first).
  - approach: code
  - files-touched: []
  - acceptance: determine from a Pending->Approve attempt (or code) whether the
    appointment-level CE email satisfies the "active claim examiner" gate or a CE master
    (`api/app/claim-examiners`, User Management) is required; if required, create one CE master
    via the UI as Staff Supervisor. Document which is true.

### Phase 3 -- Book the three appointments (each with all 4 parties)

- T4: Book AME as Clinic Staff (Rachel Kim) on behalf of Patient, with Applicant Attorney,
  Defense Attorney, and Claim Examiner filled.
  - approach: test-after
  - files-touched: []
  - acceptance: appointment created (confirmation `A#####` captured), status Pending; the four
    party emails (patient/AA/DA/CE) recorded on the appointment; submission notification logged
    with all four recipients + the appointment deep-link; screenshot of the created appointment.
- T5: Book IME as Applicant Attorney (Marcus Bennett); booker's own AA section auto-fills
  read-only; Patient + Defense Attorney + Claim Examiner filled; vary specific accounts
  (patient2/defatty2/claimE2).
  - approach: test-after
  - files-touched: []
  - acceptance: same as T4 for IME; confirm AA fields were locked/auto-filled for the booker;
    confirm AME/IME reject a Panel Number (negative: form blocks / server invariant fires).
- T6: Book PQME as Patient (Daniel Harper); own demographics auto-fill; AA + DA + CE filled;
  Panel Number supplied; stage + designate a Panel Strike List document.
  - approach: test-after
  - files-touched: []
  - acceptance: PQME created with Panel Number; submit blocked if Panel Number omitted
    (negative); strike-list designation required to submit; confirmation + screenshot captured.

### Phase 4 -- Approve, documents, packets, change-requests (per appointment)

- T7: Approve each appointment as internal staff.
  - approach: test-after
  - files-touched: []
  - acceptance: with no active CE the approve action is blocked (negative, per gate from T3);
    after the CE is active each appointment moves Pending->Approved; status-change emails
    logged; screenshots of Approved state.
- T8: Upload + moderate documents on each appointment.
  - approach: test-after
  - files-touched: []
  - acceptance: a synthetic pdf and image upload succeed; one document approved and one
    rejected with a reason; oversize (>10MB) and disallowed-type uploads are rejected
    client+server; status badges render correctly (screenshot).
- T9: Generate + download packets and exercise the #292 pipeline.
  - approach: test-after
  - files-touched: [] (flag flips via compose env only)
  - acceptance: with `PACKETS_HTML_*` default, an approved appointment still produces the
    legacy DOCX/Gotenberg packet (regression); flip all three flags true + recreate api ->
    doctor packet fillable (~1330 fields), patient (~755), attorney flat (0); PQME yields the
    pqme notice (DWC QME form present), AME/IME yields ame_ime notice (no QME form); packet
    emails to patient + attorney/CE send (logs); flags rolled back to false + verified.
- T10: Issue one change-request per appointment.
  - approach: test-after
  - files-touched: []
  - acceptance: Actions menu offers Reschedule / Cancel (not Edit); an internal NoBill request
    auto-approves; an external request stays Pending and appears on the supervisor's pending
    page + dashboard tile; change-request emails logged.

### Phase 5 -- Report + cleanup

- T11: Compile the test report and surface the logging edit for disposition.
  - approach: code
  - files-touched: [`docs/feedback-research/2026-06-07-live-ame-ime-pqme-lifecycle.md`]
  - acceptance: a single markdown report marks every lifecycle step PASS/FAIL/BLOCKED with
    concrete evidence (HTTP status, confirmation #, field counts, screenshot refs, log lines,
    DB output); a Findings list captures any anomalies (repro + suspected area, no fixes); the
    `Notifications:LogLinks` flag is turned back off and the user is told the logging edit is
    uncommitted/local for them to keep or revert.

## Risk / Rollback

- Blast radius: one Application-layer file (`NotificationDispatcher.cs`) plus one config flag;
  all other actions are UI operations against existing demo data. No migrations, no schema
  changes, no deletes of pre-existing data.
- Data risk: new appointments/documents/users are additive synthetic records on the existing
  Falkinstein volume. NEVER `docker compose down -v`. Restart containers only.
- Token-leak risk: invite/reset URLs contain single-use tokens; the flag default-off + Info
  gating keeps them out of prod logs. Flag is turned off again in T11.
- Rollback: `git checkout -- NotificationDispatcher.cs` (and any account-email file touched)
  + unset `Notifications__LogLinks` + `docker compose up -d --build api`. Booked test records
  can be left as demo data or cancelled via the UI.

## Verification

End-to-end, after all tasks: three appointments (AME/IME/PQME) exist in Falkinstein, each
Approved, each with at least one approved and one rejected document, each with a generated +
downloaded packet of the correct notice type, and each with one processed change-request. The
log (`docker logs main-api-1`) shows, for every appointment, the submission + status-change +
packet + change-request notifications with recipient + subject + links and no body. The report
at `docs/feedback-research/2026-06-07-live-ame-ime-pqme-lifecycle.md` accounts for every step
with evidence and lists anything that could not be tested and why.
