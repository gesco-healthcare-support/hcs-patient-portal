# Layer 2 -- PR-stage CI

**Parent:** [PROGRAM.md](./PROGRAM.md)
**Active child:** [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md)

## Status summary

PR-stage CI hardening. Identical checks at every gate (Fowler Pattern 3: "same static checks everywhere, environment-specific stages downstream" -- anti-pattern to avoid is graduated gates like "main needs 2 checks, staging needs 7"). 17 GitHub Actions workflows in place covering build, test, lint, format, coverage, security scanning, and branch promotion. Phase A merged; Phase B in progress (only B-6 open; Tier 1 + Tier 2 MERGED 2026-04-24, Tier 3 remains); Phase C is backlog. `main` builds 0/0 with `-warnaserror`; 0 Dependabot alerts; 0 CodeQL alerts; SonarCloud 40 issues (0 BUG, 0 VULN, 40 smells).

## Sub-items

| Phase | Scope | Plan | Status |
|---|---|---|---|
| A | Baseline inventory of every check | n/a (delivered via PRs #60, #64) | MERGED |
| **B** | Code cleanup + check enablement; ship 14 sub-items B-1..B-6 | [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md) | IN PROGRESS |
| C | Hardening (flip informational checks to blocking; Scorecard; P-11 refactor; Angular animations) | TBD after Phase B complete | BACKLOG |

## Currently active

[LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md) -- only B-6 open; B-1..B-5f all merged.

## Upcoming queue

- Phase C -- drafts when Phase B closes.

## Scope summary for un-drafted sub-items

### Phase C -- Hardening

From [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) section 2.5 and Appendix B:

- Flip SonarCloud Quality Gate from informational-only to blocking on PR merge.
- Branch-protection tightening: decide the CodeReviewID policy (Copilot / CodeRabbit as first-party reviewer, OR document solo-dev acceptance with `--admin` squash convention).
- Close 100 Scorecard alerts: SHA-pin all GitHub Actions + Dockerfile base images, add top-level `permissions:` blocks, enable OSS-Fuzz or document deferral, OpenSSF Best Practices badge. Tracked in `docs/issues/research/SEC-06.md`.
- Refactor `DoctorAvailabilitiesAppService.GeneratePreviewAsync` (cognitive 41 -> <=15). Unblocked by B-6 Tier 1 PR-1B already merged. Tracked in `docs/issues/research/P-11.md`.
- Full Angular animations API migration beyond the 1-line `provideAnimations` swap in B-5d.
- Tighten `bootstrapModalDialogRole` suppression from `**/*.component.html` to the 2 specific modal files.

## When this level completes

Pop up to [PROGRAM.md](./PROGRAM.md); that master's `Active child:` pointer moves to drafting `LAYER-3-PLAN.md`.

## Links

- Branching invariant: [../handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md](../handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md)
- CI/CD + Docker history: [../devops/CICD-DOCKER-MASTER-PLAN.md](../devops/CICD-DOCKER-MASTER-PLAN.md)

## Last updated

2026-04-24 -- B-6 Tier 2 MERGED (PR-2D #144); active-child still `LAYER-2-PHASE-B-PLAN.md` while Tier 3 remains.
