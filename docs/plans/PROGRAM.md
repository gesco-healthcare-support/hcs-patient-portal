# Patient Portal -- Program Plan Index

**Parent:** n/a (master)
**Active child:** [LAYER-2-PLAN.md](./LAYER-2-PLAN.md)

Source of truth for where we are in the 4-layer rollout. When a fresh session starts or compacts, read this file first, then walk DOWN via `Active child:` pointers to reach the currently-executing atomic unit.

## The 4-layer model

Source: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) section 2 (Fowler deployment-pipeline, Pattern 3: same static checks everywhere, environment-specific stages downstream).

| Layer | Scope | Status | Plan |
|---|---|---|---|
| 1 | Local git hooks (husky: pre-commit, commit-msg, pre-push) | ACTIVE | n/a (delivered) |
| **2** | **PR-stage CI: build + test + lint + scan on every PR** | **IN PROGRESS** | [LAYER-2-PLAN.md](./LAYER-2-PLAN.md) |
| 3 | Post-merge environment deploys + E2E + integration | DEFERRED | TBD after Layer 2 complete |
| 4 | Production promotion + observability | DEFERRED | TBD after Layer 3 complete |

## Currently active

[LAYER-2-PLAN.md](./LAYER-2-PLAN.md) -- Phase B-6 Tier 1 MERGED 2026-04-24 (PR-1E #135 closed the set). Next atomic unit: draft Tier 2 plan in a fresh plan-mode session.

## Upcoming queue

1. Layer 3 -- Post-merge deploy + E2E + integration pipeline. Starts when Layer 2 closes.
2. Layer 4 -- Production promotion + observability. Starts when Layer 3 closes.

## Scope summary for un-drafted layers

### Layer 3 -- Post-merge environment stages

After code lands on `development` or `staging`, deploy and validate: smoke tests, E2E, integration with external services. Answers "does it work deployed?". Currently `deploy-dev.yml` and `promote-staging.yml` are placeholders that re-run build+test. Real deploys + E2E are blocked on Docker/k8s infra maturity. No Playwright / Cypress / Selenium harness in CI today. Source: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) sections 2.1 and 3.3.

### Layer 4 -- Production gates

Manual promotion staging -> production with approvals, deploy, health checks, observability. No automation; Adrian gates manually initially. Source: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) section 2.1.

## Parallel workstreams (NOT on the Layer 2 critical path)

- [../gap-analysis/README.md](../gap-analysis/README.md) -- MVP gap inventory (PR #130 MERGED 2026-04-24). 12-file research artifact + screenshots informing future feature work.
- [../product/README.md](../product/README.md) -- Phase-1 / Phase-2 product-intent docs (PR #131 MERGED 2026-04-24). Uses a different "Phase N" numbering unrelated to the Layer 2 phasing above; informs future feature work.

These streams do NOT gate Layer 2 completion.

## When this level completes

When all 4 layers are complete, the program is done. Archive this file to `docs/plans/archive/PROGRAM-complete-YYYY-MM-DD.md` and close out any remaining items in `docs/issues/INCOMPLETE-FEATURES.md`.

## Links

- Authoritative B-6 context: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md)
- Phase B handoff: [../verification/PHASE-B-CONTINUATION.md](../verification/PHASE-B-CONTINUATION.md)
- CI/CD + Docker history: [../devops/CICD-DOCKER-MASTER-PLAN.md](../devops/CICD-DOCKER-MASTER-PLAN.md)
- Branching invariant: [../handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md](../handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md)

## Last updated

2026-04-24 -- B-6 Tier 1 MERGED; next atomic unit is Tier 2 planning at Level 3 ([LAYER-2-PHASE-B-6-PLAN.md](./LAYER-2-PHASE-B-6-PLAN.md)).
