---
feature: hardening-suite-revision
date: 2026-05-19
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Revise `docs/runbooks/HARDENING-TEST-SUITE.md` so it bootstraps from a near-empty
DB (only `stafsuper1@gesco.com` + `clistaff1@gesco.com` seeded), drives the new
12-user roster, exercises all seeded appointment types via staff-supervisor slot
generation (no doctor seeding), pauses for Adrian's manual link verification on
the 4 fresh registrations, adds lockout / password-reset / refresh-token /
concurrent-session probes + email-template language review + packet
recipient-routing + partial-failure isolation, replaces all hard-coded IDs and
date offsets with runtime-resolved values, and adds a Round 3 inline replay
sweep of every open finding.

## Context

The existing suite (880 lines, 2026-05-13) was written against an AME-only
SQL-seeded slot world with a 4-user `SoftwareThree..Six` roster. Adrian needs a
suite that survives the new starting conditions (12-user roster, only 2 internal
seeded, slots generated via the UI), covers the auth surfaces the old plan
silently ignored, and produces a regression signal against prior findings.

Locked constraints from research:
- No code changes this session. Future xUnit migration of R2 noted as a sidebar
  to-do but not scaffolded.
- No `DemoDoctorDataSeedContributor` work. Slots come from
  `DoctorAvailabilitiesAppService.GeneratePreviewAsync` + `CreateAsync` driven
  by `stafsuper1@gesco.com`.
- Hard WAIT marker for the 4 fresh registrations (2 manual + 2 invite). Adrian
  clicks the verification / invite-acceptance links by hand.
- Multi-session is intentional (locked 2026-05-01); the concurrent-session probe
  asserts the policy, not a single-session limit.

## Approach

**Chosen:** edit `docs/runbooks/HARDENING-TEST-SUITE.md` in place as a single
canonical artifact. Drop hard-coded `A0000N` confirmation numbers and `T+Nd`
offsets in favor of a per-run state file at `.hardening-run/<YYYY-MM-DD>.json`
the agent populates and reads as it goes. Restructure into 8 phases inside the
existing two-round frame, add Round 3, and extend Part 6 lessons + Part 7
findings cross-reference to match. Update Part 5 dictionary to the new 12-user
roster.

**Rejected:**
- Scaffolding a new xUnit project for R2 (Adrian: out of scope this session).
- Splitting Round 3 into a separate `REGRESSION-REPLAY.md` runbook (Adrian:
  inline preferred).
- Env-var date anchor with fixed offsets (loses robustness on slot consumption).
- Auto-clicking verify links via Hangfire arg extraction (loses Adrian's manual
  verification gate).
- Writing a separate design spec under `docs/superpowers/specs/` (rpe-workflow
  rule: plan lives at `docs/plans/`, the spec content lives inline in the
  research summary + this plan).

## Tasks

- T1: Replace user roster, prereqs, and frontmatter
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Part 1 prerequisite table references the new 12-user roster
    (only `stafsuper1@gesco.com` + `clistaff1@gesco.com` expected pre-existing;
    other 10 are post-Phase-1 creations). Part 5 dictionary lists the 12
    `@gesco.com` addresses with real-name mapping. Standard password unchanged.
    Doc title + intro reflect "bootstrap-from-near-empty" framing.

- T2: Add Phase 0 - Slot generation via staff-supervisor UI
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: New "Phase 0" block before Phase 1. Steps: log in as
    `stafsuper1@gesco.com`, call
    `POST /api/app/doctor-availabilities/generate-preview` then
    `POST /api/app/doctor-availabilities/create` for each
    (AppointmentType x Location) pair in window `T+3d..T+60d` with equal
    slot count per type. Verify SQL: `COUNT(*) GROUP BY AppointmentTypeId` is
    within +/- 1 across all active types in that window. Stop condition: any
    type has 0 slots in window.

- T3: Restructure Phase 1 - Registration with 2 manual + 2 invite + WAIT gate
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Phase 1.A documents 2 self-register POSTs through
    `/Account/Register` for `patient1@gesco.com` (Daniel Harper) and
    `appatty1@gesco.com` (Marcus Bennett). Phase 1.B documents 2
    `POST /api/app/external-signup/invite` calls (as `stafsuper1`) for
    `defatty1@gesco.com` (Gregory Stone) and `claimE1@gesco.com` (Henry
    Caldwell). After each registration / invitation, the agent captures the
    verify URL (from Hangfire job args or SMTP inbox) and prints it in a
    fenced block tagged `=== WAIT FOR ADRIAN: verify <email> ===`. Agent
    pauses until Adrian responds `verified <email>`; state persists in
    `.hardening-run/<run-date>.json` under `verifications[].email/status`.
    Existing Phase 1.1.1-1.1.4 verify-email-roundtrip scenarios fold into 1.A.

- T4: Slim Phase 3 - All-emails-filled bookings (5 scenarios, not 14)
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Phase 3 table reduces from 14 rows to 5 scenarios, one per
    booker role (Patient, AA, DA, CE) + 1 internal Clinic Staff via
    `/appointments/add` deep-link. Every row sets AA + DA + CE + Insurance to
    `on` and uses ALL FOUR email fields populated (per Adrian's input). New
    rows have NO hard-coded confirmation numbers; the per-scenario template
    captures the actual `RequestConfirmationNumber` from the POST response
    into `state.appointments[scenario_id]`. Slot dates are described as
    "next available slot for type X in window T+3d..T+60d" -- the agent
    resolves at runtime via `/api/app/doctor-availabilities` filtered by type
    and location. Each scenario lists every required AA + DA + CE + Ins field
    drawn from the synthetic-block in Part 5.

- T5: Preserve Phase 5 + Phase 6 - Approvals + packet recipient routing
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Phase 5 keeps its 5-scenario shape but references appointments
    by `state.appointments[<scenario_id>]` instead of `A00005`...`A00011`.
    Phase 6 adds two new check classes: (a) "Recipient routing": for each
    approved appointment, verify the Patient packet email lands at
    `patient.email`, the AttyCE packet emails land at the AA/DA/CE addresses
    bound to the appointment (cross-check via SQL on
    `AppAppointmentPackets` + `Hangfire.Job` args + SMTP catch-all if
    available). (b) "Partial-failure isolation": at least one scenario
    artificially fails one packet kind (e.g., inject a too-long employer name
    or trigger a known Kind=3 duplicate-key path per the 2026-05-15 episodic
    observation) and asserts the other two kinds still generate AND fire
    their emails. Acceptance text says "this is a CURRENT BUG to confirm" so
    the scenario produces a finding if the other two emails do not fire.

- T6: Add Phase 9 - Authentication probes
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: New Phase 9 with 4 scenario classes, each with explicit
    endpoints + expected response shapes + anti-PHI anti-checks:
    - 9.1 Lockout: N failed logins (N = ABP's configured
      `MaxFailedAccessAttempts`), expect AbpUsers.AccessFailedCount = N and
      LockoutEnd > now; expect HTTP 401 or 423 on subsequent attempts; assert
      response body does not echo whether the user exists.
    - 9.2 Password reset: round-trip (request -> email -> click -> change ->
      login), idempotent re-click, tampered token, expired token (manipulate
      `Hangfire.Job` enqueue time or wait beyond TTL), anti-enumeration
      (request reset for non-existent email returns same response as
      existent), 5/hour throttle.
    - 9.3 Refresh-token rotation: POST `/connect/token` with
      `grant_type=refresh_token`, assert response `refresh_token` differs
      from request token, assert old refresh_token returns 400 on reuse,
      assert sliding refresh works within configured window.
    - 9.4 Concurrent sessions: log in as same user from Playwright session A
      AND session B, assert both retain access to a protected endpoint, log
      out from A, assert B still has access (multi-session intentional per
      2026-05-01 decision).

- T7: Add Phase 10 - Email template language review (rubric)
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: New Phase 10 documents a 5-item rubric applied to each
    template code in `NotificationTemplateConsts`:
    1. Subject line <= 60 chars, no template-code leakage (no `Apprvd`,
       `Stackholder`, raw token names like `{{patient_name}}` unresolved).
    2. Single primary CTA per body (verified by counting `<a>` tags with
       button-like classes or "Click here" / "Open" / "Login" phrasings).
    3. All token substitutions resolved (no `{{...}}` left in the rendered
       output for a sample input).
    4. Plain-language pass: no jargon from the banned-words list
       (`AppService`, `DTO`, `BookingPolicyValidator`, `IdentityUser`,
       `BusinessException`, `Hangfire`).
    5. Non-redundancy: subject and first body paragraph do not duplicate >
       70% of tokens.
    Output table written to
    `docs/runbooks/findings/template-review-<run-date>.md` with one row per
    template and pass/fail per rubric item. Templates failing 2+ items are
    filed as new BUG / OBS.

- T8: Trim Round 2 to major probes only + add sidebar on future xUnit migration
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Round 2 keeps the major-probe categories (registration
    validation, booking validation, permission/scope, state machine,
    rejection, email-confirmation endpoint) but each category cites only 1-2
    representative probes. Add a "Future work" sidebar at the top of Round 2
    noting that these probes are good candidates for migration to xUnit
    integration tests under
    `test/HealthcareSupport.CaseEvaluation.Application.Tests/` once the
    revision is reviewed -- explicitly NOT done this session per Adrian's
    direction. Each probe drops hard-coded confirmation numbers in favor of
    `state.appointments[...]` lookups.

- T9: Add Round 3 - Replay sweep of open findings
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: New Round 3 section. For every file in
    `docs/runbooks/findings/bugs/` with frontmatter `status: open` (skip
    `status: resolved-not-a-bug`, `status: needs-rehydration` left as a
    follow-up note, and the OBS-2..OBS-7 stubs), include a 4-line block:
    `Finding id`, `Repro steps (copied from the finding)`,
    `Expected if fixed`, `Action on outcome` (one of: "confirm still open ->
    update found-date on the finding file", "marks fixed -> append
    'Resolved YYYY-MM-DD via run' to the finding", "fluke -> close with
    note"). The list is generated by enumerating Glob
    `docs/runbooks/findings/bugs/*.md`; the runbook references each by ID
    only so it stays stable. Round 3 starts after Round 1 + Round 2
    complete so the appointment corpus exists.

- T10: Replace per-scenario `Verify SQL` and `Inputs` blocks across the doc
       to use `state.*` variables and runtime resolution
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Doc-wide grep for `A0000` and `T+\d+d` returns zero matches
    (except inside the new "Lessons learned" anti-patterns section that
    explicitly calls out the OLD hard-coded approach). Every scenario
    references `state.appointments[<scenario_id>]`,
    `state.users[<role_key>]`, or `state.slots[<type>]` instead. The doc
    includes a new section "Run-state file schema" near Part 2 documenting
    the shape of `.hardening-run/<YYYY-MM-DD>.json`:
    `{ runDate, runPrefix, users[], verifications[], slots[], appointments{} }`.

- T11: Extend Part 6 lessons + Part 7 finding map + Part 9 maintenance
  - approach: code
  - files-touched: [docs/runbooks/HARDENING-TEST-SUITE.md]
  - acceptance: Part 6 gains 4 new rules covering: (a) runtime-resolved IDs
    and run-prefix usage; (b) WAIT-marker pause/resume protocol; (c)
    multi-session is intentional, not a bug; (d) Phase 0 slot generation
    must verify equal counts before proceeding. Part 7 finding-map table
    gains rows for Phase 9 (auth probes), Phase 10 (template review), and
    Round 3 (replay sweep). Part 9 maintenance gains "When you add a new
    appointment type, extend Phase 0's loop" and "When a finding is closed,
    move it from Round 3 active list to a 'historical' subsection".

## Risk / Rollback

- Blast radius: single file edit (`docs/runbooks/HARDENING-TEST-SUITE.md`).
  No code paths touched. The
  `docs/runbooks/findings/template-review-<run-date>.md` artifact is created
  by runs of the suite, not by this PR. Existing finding files are unchanged.
- Rollback: `git checkout main -- docs/runbooks/HARDENING-TEST-SUITE.md`.

## Verification

After all tasks complete:

1. `grep -nE 'A0000|T\+[0-9]+d' docs/runbooks/HARDENING-TEST-SUITE.md` -- only
   matches inside the Part 6 "anti-pattern" reference, zero matches anywhere
   else.
2. `grep -n 'WAIT FOR ADRIAN' docs/runbooks/HARDENING-TEST-SUITE.md` -- at
   least 4 matches (one per fresh registration in Phase 1).
3. `grep -nE '^### Phase [0-9]+|^## Part [0-9]+|^### Round [0-9]+' docs/runbooks/HARDENING-TEST-SUITE.md`
   -- Phase 0 through Phase 10 present, Round 1 + Round 2 + Round 3 present,
   Parts 1-9 present.
4. Section "Part 5: Test data dictionary" lists exactly 12 user rows matching
   Adrian's roster, with real-name mapping.
5. Section "Run-state file schema" exists and documents
   `runDate`, `runPrefix`, `users`, `verifications`, `slots`, `appointments`.
6. Round 3 list count matches the count of open findings under
   `docs/runbooks/findings/bugs/*.md` (excluding `resolved-not-a-bug` and
   stubs); spot-check 3 entries against their source finding files.
7. Read the doc top-to-bottom once for ASCII-only, no smart quotes, no em
   dashes, no emoji.
8. Doc is under the project's complexity ceiling (no hard length cap on docs
   but aim under 1500 lines).
