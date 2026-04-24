# Layer 2 Phase B -- Code Cleanup + Check Enablement

**Parent:** [LAYER-2-PLAN.md](./LAYER-2-PLAN.md)
**Active child:** [LAYER-2-PHASE-B-6-PLAN.md](./LAYER-2-PHASE-B-6-PLAN.md)

## Status summary

14 of 15 sub-items merged. `main` builds 0/0 with `-warnaserror`. SonarCloud shows 40 issues (0 BUG, 0 VULN, 40 smells), CodeQL 0 alerts, Dependabot 0 alerts, Scorecard running (100 alerts deferred to Phase C per SEC-06). 61 SonarCloud security hotspots pending Adrian's manual UI disposition (see [../verification/PHASE-B-CONTINUATION.md](../verification/PHASE-B-CONTINUATION.md) section 4). Only B-6 (test coverage 6% -> 60%) remains open.

## Sub-items

| ID | Scope | PR | Status |
|---|---|---|---|
| B-1 | Dependabot cleanup | n/a | DONE |
| B-2 | Build config (Scriban 7.1.0, Mapperly strategy, `Directory.Build.props` consolidation) | #76 | MERGED |
| B-2.1 | Nullable enablement + warning cleanup (480 warnings + 64 RMG012 -> 0) | #80 | MERGED |
| B-3 | CodeQL + SonarCloud config (`dotnet clean`, X-Frame-Options DENY on 5 web.config locations) | #79 | MERGED |
| B-3.1 | ABP false-positive suppressions (S6967 + S6853) | #82 | MERGED |
| B-4 | Scorecard + TruffleHog pins, karma lcov reporter | #81 | MERGED |
| B-5a | TypeScript findings (36 -> 0) | #83 | MERGED |
| B-5b | Helm + GitHub Actions vulnerabilities (10 -> 0) | #84 | MERGED |
| B-5c-1 | C# `== default` -> `== Guid.Empty` (34 findings) | #85 | MERGED |
| B-5c-2 | C# mechanical misc (35 findings) | #86 | MERGED |
| B-5c-3a | C# mechanical async + adjacency + logger template (41 findings) | #89 | MERGED |
| B-5c-3b | C# complexity + extended ABP baseline suppressions | #90 | MERGED |
| B-5d | 1 BUG + ~30 cross-language findings (Web, Shell, Python, CSS, JS, Docker, K8s, TS) | #91 | MERGED |
| B-5f | ABP baseline suppressions (S107 + S1192 scoped to ABP DI patterns) | #87 | MERGED |
| **B-6** | **Test coverage 6% -> 60% overall, >= 80% new-code** | ongoing | **IN PROGRESS** |

## Currently active

[LAYER-2-PHASE-B-6-PLAN.md](./LAYER-2-PHASE-B-6-PLAN.md) -- B-6 Tier 1 in flight.

## Upcoming queue

- None within Phase B. When B-6 closes, Phase B is complete.

## When this level completes

Pop up to [LAYER-2-PLAN.md](./LAYER-2-PLAN.md); that level's `Active child:` pointer moves to drafting `LAYER-2-PHASE-C-PLAN.md` (scope already summarized at Layer 1).

## Links

- Historical handoff (end of 2026-04-17 session): [../verification/PHASE-B-CONTINUATION.md](../verification/PHASE-B-CONTINUATION.md). Many items listed as TODO in that snapshot have since closed; this plan is the current source of truth for per-item status.
- B-6 scope context: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md).

## Last updated

2026-04-24 -- initial creation (14 of 15 sub-items merged; B-6 Tier 1 PR-1D landed, PR-1E in flight).
