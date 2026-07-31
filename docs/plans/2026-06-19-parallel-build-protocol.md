---
doc: parallel-build-protocol
date: 2026-06-19
type: reference
status: living
base-branch: feat/frontend-rework
---

# Parallel Build Protocol -- two sessions, one worktree, one branch

Two Claude sessions work CONCURRENTLY in the same worktree
(`W:\patient-portal\feat-frontend-rework`) on the same branch
(`feat/frontend-rework`), one shared docker stack, WSL RAM raised to maximum. There is
NO git merge safety net -- both sessions mutate the same working tree live. The
discipline below is mandatory.

## Lanes

- **Session A (integrator)** -- the fast pass: #3 (WCAB -> Config hub), #10 (dashboard
  completion line). Owns proxy regen, stack lifecycle, pushes to origin, and watches
  commit hygiene.
- **Session B** -- backend-heavy backlog: #14 (detail change log), #9 (AA/DA self-edit +
  snapshot), #4 (document types M2M), #6 (notification templates), #2 (doctor
  availabilities week-view), #15 (draft save/resume). Owns ALL EF migrations.

Out of scope for both: #11 (folded into multi-tenant prep), the multi-tenant work
itself, and appointment-change-control (post-multi-tenant; spec at
`2026-06-19-appointment-change-control.md`).

## The discipline (load-bearing)

1. **Explicit-path commits ONLY.** Never `git add -A` / `git add .` / `git commit -a`.
   Stage only the files for your current task, by name. Commit small and often -- a long
   uncommitted window is when the other session's edits leak into your commit.
2. **Migrations are single-writer = Session B.** Only Session B runs
   `dotnet ef migrations add`, one at a time. Session A creates none (#3 has no backend;
   #10 is a pure aggregate query). Two concurrent migrations corrupt the shared
   `CaseEvaluationDbContext` ModelSnapshot.
3. **Proxy regen is single-writer = Session A.** Only Session A runs
   `abp generate-proxy -t ng -u http://localhost:44377` (no trailing slash). When Session
   B adds/changes a backend endpoint and needs the Angular proxy, it finishes + commits
   the backend, then asks (via Adrian) for Session A to regen. Never hand-edit proxies;
   commit only the changed `models.ts` + `generate-proxy.json`, discard EOL-only
   `index.ts` no-ops.
4. **Config hub is serialized.** #3 restructures the Configuration hub; #4 and #6 are
   config-hub features. Session B must NOT touch the config-hub UI (its component, rail,
   or routing) until Session A's #3 commit appears in `git log`. Session B front-loads the
   #4/#6 BACKEND (entities, migrations, app services, endpoints) first; adds the hub UI
   after #3 lands.
5. **One shared stack.** Do not rebuild/restart containers without coordinating via
   Adrian. A restart picks up both sessions' files (bind mount) -- fine -- but do not kill
   a build/test in progress. Prefer asking Session A (integrator) to restart.
6. **Stay in your lane on files.** Never edit a file the other lane owns (see table). For
   genuinely shared files (`app.routes.ts`, `internal-nav.config.ts`, shared `_*.scss`),
   coordinate via Adrian before editing.
7. **RPE per item.** /feature-research -> /feature-design (write to `docs/plans/`) ->
   /feature-build. Triage + locked decisions: `2026-06-17-frontend-rework-backlog.md`.

## File ownership

| Surface | Owner |
| --- | --- |
| Configuration hub component / rail / its routing | A (via #3), then B may add #4/#6 UI after #3 lands |
| WCAB Offices / Locations page + scheduling route | A |
| Dashboard: DashboardAppService, DashboardDto, internal-dashboard component, dashboards proxy | A |
| EF migrations + CaseEvaluationDbContext snapshot | B (exclusive) |
| Angular proxy regen output | A (exclusive) |
| Appointment detail change-log surface, info-request history, external detail | B (#14) |
| Appointment entity snapshot fields + attorney self-edit + their migration | B (#9) |
| DocumentType M2M entity/service + doc-types migration | B (#4) |
| Notification template entity/format/sanitization + editor | B (#6) |
| Doctor-availabilities endpoint + week-view component | B (#2) |
| AppointmentDraft entity + worker + wizard CanDeactivate guard + its migration | B (#15) |
| `app.routes.ts`, `internal-nav.config.ts`, shared `_*.scss` | shared -- coordinate via Adrian |

## Coordination signals

- "Is #3 in yet?" -> `git log` (Session B watches for the #3 commit before touching the hub).
- Regen / restart handshakes -> Adrian relays between sessions.
- Integration is continuous (same branch); Session A pushes to origin at safe points.

## Stack

angular http://falkinstein.localhost:4250, api 44377, authserver 44418, sql 1439,
redis 6384. Tenant subdomain falkinstein.localhost. Creds: stafsuper1@gesco.com (staff
supervisor), appatty1@gesco.com (applicant attorney), pw 1q2w3E*r. Verify UI with
screenshots, not just "it compiled."

## Standing constraints

ASCII only. No `Co-Authored-By: Claude`, no "Generated with Claude Code". Never commit
`design_handoff_appointment_portal/`, `design_handoff_part_B/`,
`docker-compose.override.yml`. Never read `.env`. HIPAA: synthetic data only, SSN masked.
