---
feature: track-9-new-capture
date: 2026-04-24
status: draft
base-branch: docs/old-new-gap-analysis
related-issues: []
---

## Goal

Close Track 9 of the OLD-vs-NEW gap analysis by capturing every reachable
NEW UI screen for host admin + all 5 seeded tenants + one of each external
role, and synthesise the evidence into an appendix that grounds the 32
MVP-scope questions in `docs/gap-analysis/README.md:227-271` in rendered
reality.

## Context

- `docs/gap-analysis/09-ui-screens.md` captured OLD-side screens for all 7
  OLD roles on 2026-04-23 but left NEW-side as an acknowledged coverage
  gap. The prior session incorrectly believed no role-scoped users were
  seeded in NEW; in fact `scripts/Master-Seed.ps1` seeded a full
  multi-tenant roster (23 users across 5 tenants) via REST API on
  2026-04-20 (see `scripts/seed-state.json`).
- Services are running on default ports (AuthServer 44368, HttpApi 44327,
  Angular 4200) and `chrome-devtools-mcp` is available in this session.
- Base branch `docs/old-new-gap-analysis` inherits the existing
  `docs/gap-analysis/` tree + the 10 OLD screenshots, which lets the NEW
  capture land natively as a sibling appendix without touching
  `09-ui-screens.md`.
- Output informs the docs/product intent work + the 39 solution briefs in
  the implementation-research worktree by showing rendered evidence for
  the per-role UX.

## Approach

- Reuse the already-running NEW services on default ports. The worktree
  helper allocated ports (44438 / 44397 / 4270) are ignored; no stack
  bring-up is needed.
- Drive the capture from the parent session with `chrome-devtools-mcp`.
  For each of the 19 logins: clear storage, use the ABP tenant-switch
  flow in the Angular login page (for non-host users), submit
  credentials, harvest sidebar nav via `evaluate_script`, snapshot +
  screenshot each reachable screen, capture console + network errors,
  log out before the next login.
- Password assumed to be ABP default `1q2w3E*` for all seeded users
  (confirmed by Adrian). First login verifies this assumption; if wrong,
  stop and surface the real value.
- Synthesis appendix is a NEW sibling file at
  `docs/gap-analysis/09-ui-screens-NEW-appendix.md`; the existing
  `09-ui-screens.md` is not modified.

### Rejected alternatives

- Base on `main` + write to `docs/audits/**` in isolation: discoverability
  penalty (readers of `docs/gap-analysis/` wouldn't find the NEW
  appendix). Rejected.
- Wait for `docs/old-new-gap-analysis` to merge into `main` first:
  indefinitely blocks today's capture. Rejected.
- Create new external users via the NEW Angular Register flow to extend
  role coverage: would leave persistent `IdentityUser` + `Patient` rows
  in LocalDB, violating the read-only capture contract. Rejected in
  favour of using the existing Master-Seed roster.

## Tasks

| id | description | approach | files-touched | acceptance |
|----|-------------|----------|---------------|------------|
| T1 | Create branch `docs/track-9-new-capture-2026-04-24` from `docs/old-new-gap-analysis`, run `scripts/worktrees/add-worktree.sh`, worktree lives at `../docs-track-9-new-capture-2026-04-24` | code | (no files) | Worktree present; `git worktree list` shows the new entry; branch tracks no remote yet (push happens at T14) |
| T2 | Sub-task A: write `docs/gap-analysis/screenshots/new/00-services.md` logging the 3 running services, ports, timestamps of `/ready` probes | code | `docs/gap-analysis/screenshots/new/00-services.md` | File present; 3 curl probes recorded as HTTP 200 with timestamp |
| T3 | Sub-task B: verify `1q2w3E*` via `POST /connect/token` for host admin and for one tenant user (with `__tenant` header). Draft the credential matrix section of the appendix | code | `docs/gap-analysis/09-ui-screens-NEW-appendix.md` (credential matrix section only) | Both token requests return 200 with `access_token`. Matrix lists all 19 users with reachable status |
| T4 | Capture host admin (`admin@abp.io`, TenantId=null): landing, full sidebar + feature-route traversal | code | `docs/gap-analysis/screenshots/new/admin/*.png` | >=5 screens captured; console/network log noted |
| T5 | Capture T1 tenant users: Doctor, AA, DA, CE, Patient | code | `docs/gap-analysis/screenshots/new/t1-*/*.png` | >=1 screen per role (5 roles); logout between roles |
| T6 | Capture T2 tenant users: Doctor, AA, DA, CE, Patient | code | `docs/gap-analysis/screenshots/new/t2-*/*.png` | >=1 screen per role |
| T7 | Capture T3 tenant users: Doctor, AA, DA, CE, Patient | code | `docs/gap-analysis/screenshots/new/t3-*/*.png` | >=1 screen per role |
| T8 | Capture T4 tenant users: Doctor, T4_NullPatient | code | `docs/gap-analysis/screenshots/new/t4-*/*.png` | >=1 screen per role |
| T9 | Capture T5 Doctor | code | `docs/gap-analysis/screenshots/new/t5-doctor/*.png` | >=1 screen captured |
| T10 | Capture special non-role surfaces: Swagger, unauthenticated landing, ABP Login page, Register form (pre-submit, NO mutation) | code | `docs/gap-analysis/screenshots/new/_non-role/*.png` | 4 screens captured |
| T11 | Sub-task D: write synthesis appendix with per-role sections, classification, side-by-side OLD-vs-NEW table for exists-in-both, console/network findings, and Impact-on-32-Qs section citing >=6 Qs | code | `docs/gap-analysis/09-ui-screens-NEW-appendix.md` | File present; all 7 required sections present; >=20 screenshots referenced (target 40+) |
| T12 | Commit artifacts with `docs(gap-analysis)` type, <=72 char title, bullet body explaining the WHY | code | git history | Commit present on feature branch; message passes validate-commit-message hook |
| T13 | Push feature branch | code | remote | `origin/docs/track-9-new-capture-2026-04-24` exists |
| T14 | Open PR targeting `docs/old-new-gap-analysis` with the 10-section body, screenshots section referencing captures via relative paths | code | PR body | PR URL returned; `validate-pr.sh` hook green; CI green on required checks |

## Risk / Rollback

- Blast radius: docs + screenshots only. No source code, migrations, DB
  rows, or shared infra touched.
- Rollback: revert the PR on merge; `git worktree remove` the feature
  worktree; branch is preserved per branch-deletion policy (never auto-delete).

## Verification

1. `curl -sk https://localhost:44327/swagger/index.html` returns 200
   before capture begins.
2. `curl -sk https://localhost:44368/.well-known/openid-configuration`
   returns 200.
3. `curl -s http://localhost:4200/` returns 200.
4. `POST /connect/token` returns 200 + `access_token` for `admin@abp.io`
   (host) and for one tenant user with `__tenant` header set to that
   tenant's GUID.
5. Screenshot total >= 20 across all role folders (target >= 40).
6. `docs/gap-analysis/09-ui-screens-NEW-appendix.md` contains: Summary,
   Method + timestamp, credential matrix, per-role sections, side-by-side
   OLD-vs-NEW for exists-in-both, console/network findings,
   Impact-on-32-Qs citing >= 6 Qs.
7. `docs/gap-analysis/screenshots/new/00-services.md` present with
   running-state record.
8. PR opened with 10-section body; all CI checks pass; `validate-pr.sh`
   green.
9. Post-merge: auto-promote PR `docs/old-new-gap-analysis -> development`
   (auto-pr-dev.yml) is reviewed for green (out-of-band of this plan).

## Stop-points

- After T1 (worktree + branch): present, wait (DONE as of plan-write time).
- After T2 (services log): present, wait.
- After T3 (credential matrix draft + password verification): present, wait.
- After each of T4-T10 individually: report screen count + URLs touched +
  console/network errors, wait.
- After T11 (appendix draft): present, wait.
- Before T12-T14 (commit, push, PR).
