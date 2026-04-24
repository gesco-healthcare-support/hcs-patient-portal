# Kickoff Prompt -- OLD vs NEW Gap Analysis

> Paste everything below the horizontal rule into a fresh Claude Code session. The prompt is self-contained and assumes the new session has not seen this one.

---

You are the orchestrator for a time-boxed, fully-parallel gap analysis between two versions of the Gesco Patient Portal. Today's deliverable is a set of 10 markdown documents at `W:\patient-portal\development\docs\gap-analysis\` (a master README + 9 per-dimension track docs). You will NOT implement code. You will NOT write an implementation plan. The sole output is documentation that lets the user, Adrian, drive per-gap MVP scoping conversations with management.

## Why this matters

Adrian inherited an older version of the portal that has substantially more implemented functionality than the ABP-based rewrite he is currently building. He needs an inventory of what is missing from the new version so he can negotiate MVP scope with management, decide what to re-implement, and track progress. This inventory is the foundation of his next two weeks of work. Precision and evidence matter more than speed; but since all 9 tracks are independent, aggressive parallelism is expected.

## Read these in order BEFORE dispatching anything

1. `W:\patient-portal\development\docs\gap-analysis\00-PLAN.md` -- full plan with per-track scope, method, shared template, seeded architectural differences, and state handoff. This is the spec. Read it fully.
2. `W:\patient-portal\development\CLAUDE.md` -- NEW-version conventions and "never do" list.
3. `P:\PatientPortalOld\_local\CHANGELOG.md` -- the OLD bring-up changelog, including credentials, port assignments, stub procs, and known limitations.

## Running state you inherit

- OLD Angular: `http://localhost:4201`
- OLD API: `http://localhost:59741`
- OLD DB: `(localdb)\MSSQLLocalDB` -- databases `PatientPortalOld_{Main,Log,Cache}`
- NEW Angular: `http://localhost:4200`
- NEW API: `https://localhost:44327` (Swagger at `/swagger/index.html`; use `-k` on curl for the self-signed cert)
- NEW AuthServer: `https://localhost:44368`
- OLD credentials: `{admin,supervisor,staff,patient,adjuster,patatty,defatty}@local.test` / `Admin@123`
- NEW default admin: `admin` / `1q2w3E*`

If any of these is not reachable when you probe it, STOP and ask Adrian before doing anything else. Do not try to restart services; the bring-up was carefully sequenced in the prior session.

## Dispatch strategy

Dispatch all 9 tracks IN PARALLEL in a single assistant turn using the `Agent` tool. Use the subagent types specified in `00-PLAN.md` (`Explore` for read-only tracks 1, 2, 3, 6, 7, 8; `general-purpose` for tracks 4, 5, 9 which need Bash / curl / Chrome DevTools MCP). Each subagent's prompt MUST include:

- The track's scope and method verbatim from `00-PLAN.md`.
- The shared output template verbatim.
- The full state handoff section (ports, credentials, paths).
- The seeded architectural differences table (so the subagent does NOT flag these as gaps).
- The verification criteria the subagent's own output must satisfy.
- Explicit "do not edit source code; do not implement fixes; write one markdown file to the specified path; return a one-paragraph summary" directive.

After the 9 reports come back, do a synthesis pass: read every track doc, produce `README.md` with:

- Executive summary (3 bullets)
- Gap table aggregating all MVP-blocking gaps across tracks (one row per gap: `gap-id | feature | track | severity | evidence-link`)
- Non-MVP gap table (same shape)
- Intentional architectural differences summary (one per topic)
- Extras-in-new table
- Open questions from every track (consolidated, labeled with source track)
- Reproduction cheat sheet (sqlcmd, curl, browser URLs)

## Scoping rules (non-negotiable)

1. OLD = MVP target. Any capability in OLD that NEW lacks is an MVP gap by default, UNLESS it matches one of the seeded intentional architectural differences in `00-PLAN.md`.
2. Every claim cites a file path + line number, a URL, or a captured screenshot path. No unsourced claims.
3. Gap severity ratings are: `MVP-blocking` (capability Adrian's managers will expect in MVP), `non-MVP` (explicitly nice-to-have, rare/admin feature, or very low usage). If you cannot tell, mark `needs-decision` and put it in the Open Questions list.
4. UI track (#9) captures every reachable screen for every one of the 7 OLD roles, and for every role that NEW supports (initially likely only admin). Screenshots go under `docs/gap-analysis/screenshots/<old|new>/<role>/<slug>.png`. The doc's per-role section lists every screen with side-by-side links.
5. When you would duplicate content across tracks (e.g., `Appointments` feature touches schema, domain, app-service, API, UI), each track documents its OWN slice. Cross-references between docs are fine and encouraged.
6. If any subagent reports an infrastructure problem (port unreachable, Swagger returns 500, a screen triggers 500 errors), log it in that track's `Open questions` section and keep going; do not block the parallel dispatch.
7. Do NOT modify source in either repo. The only writes are to `W:\patient-portal\development\docs\gap-analysis\**` -- markdown docs and PNG screenshots.

## Success criteria (the session is done when all hold)

- 10 markdown files exist at `W:\patient-portal\development\docs\gap-analysis\` (README + 9 tracks).
- Every track doc follows the template (Summary / Method / OLD state / NEW state / Delta with 4 subsections / Open questions).
- README contains an aggregated gap table with every MVP-blocking gap across all tracks in one view.
- `screenshots/` folder has at least one screenshot per OLD role.
- The parent session's final message is a 200-word summary with: count of MVP-blocking gaps, count of non-MVP gaps, count of intentional differences called out, count of open questions, a link to the README.

## Stop points

- If NEW version services are unreachable -> stop, ask Adrian.
- If any subagent returns with zero findings for a track where findings are expected -> re-dispatch that one track with clearer prompt before giving up.
- If gap count exceeds ~50 MVP-blocking in total -> pause before writing README and surface the scale to Adrian; he may want to adjust severity thresholds.

## What NOT to do

- Do not propose an implementation plan. Adrian is explicit: gap docs only.
- Do not write code changes.
- Do not restart services.
- Do not modify OLD or NEW source.
- Do not rename or move files in either repo.
- Do not commit anything to git. These docs will be reviewed and possibly committed manually after Adrian reviews them.
- Do not flag items from the seeded-intentional-differences table as gaps.
- Do not ask Adrian questions during the run. Collect questions into per-track `Open questions` sections for him to batch-answer after the run.

## First action

Read the full plan at `W:\patient-portal\development\docs\gap-analysis\00-PLAN.md`. Then read `W:\patient-portal\development\CLAUDE.md` and `P:\PatientPortalOld\_local\CHANGELOG.md`. Probe the 5 running endpoints (old angular, old api, new angular, new api, new authserver) to confirm everything is reachable. Then dispatch the 9 subagents in one assistant turn.
