# 02 — Research plan: rubric, sources, methodology

**Read after:** `README.md` + `01-CURRENT-STATE.md`
**Goal of this file:** tell you exactly what questions to research, which sources to consult, how to reconcile contradictions, and how to reach a defensible verdict.

## 10 research questions

Each question must be answered in `VERDICT.md` with at least one primary source + two secondary sources + a confidence label (HIGH/MEDIUM/LOW). If you cannot meet the source bar on a specific question, mark it LOW confidence and explain what additional research would raise it.

### Q1. What is GitLab Flow, according to GitLab's own definition?

Read GitLab's canonical whitepaper / docs page on GitLab Flow. Note publication date and whether it has been revised. Quote the exact description of branch semantics, especially:
- Is `main` the trunk? Is `master` the legacy name? Is there a separate `development` branch in canonical GitLab Flow?
- Are environment branches part of GitLab Flow, or an extension?
- How does GitLab's whitepaper describe "promotion" between branches?

Primary: `docs.gitlab.com` — look for pages titled "Introduction to GitLab Flow", "GitLab Flow", or similar. Also the historical PDF whitepaper if still published. Also `about.gitlab.com/blog/` posts referencing Flow.

Cross-check: Wayback Machine archives of the earliest GitLab Flow description (circa 2014 by Sytse Sijbrandij / Sid himself). The canonical model may have evolved.

### Q2. What are the documented variants of GitLab Flow?

GitLab's docs describe multiple variants. Enumerate all of them by name and semantic. Commonly cited variants:

1. **Production branch.** One long-lived branch that represents what's deployed; feature branches merge to trunk; trunk is merged to production branch when released.
2. **Environment branches.** Separate branches per environment (staging, production, possibly more); code flows strictly in one direction (feature → trunk → staging → production OR some documented order).
3. **Release branches.** For projects that ship multiple versions in parallel.
4. **Upstream-first.** Rule that bug fixes must land on upstream before downstream.

For each variant, document:
- The canonical direction of merges / promotion.
- Which branch is the "integration trunk" (where features first land).
- What "main" means in that variant.
- When (circa which year / version of the whitepaper) it was introduced.

### Q3. In the "environment branches" variant, what is the canonical direction?

This is the crux. Research: in the environment-branches GitLab Flow variant, do feature branches merge to `main` (with env branches downstream) OR do they merge to `development` / `pre-production` (with `main` / `production` downstream)?

Primary: GitLab's own page on environment branches. Quote exact text.

Cross-check: at minimum 3 independent secondary sources. Suggested:
- Atlassian's Git tutorials comparing GitFlow, GitHub Flow, and GitLab Flow.
- Martin Fowler's "Patterns for Managing Source Code Branches" (2020 update).
- DevOps / SRE blogs from the last 3 years that implement GitLab Flow in production.
- The `gitlab-org/gitlab` repo's own `CONTRIBUTING.md` (does GitLab itself follow GitLab Flow?).

Reconciliation: if the 3+ sources describe different directions, that's itself a finding. Report it explicitly.

### Q4. Is "promote main to development" standard terminology?

If GitLab Flow's environment-branches variant has `main` as the integration trunk and `development`/`staging`/`production` as downstream env branches, then a PR titled "promote main to development" would be standard: it pushes trunk state into the dev env branch.

If GitLab Flow has `development` as the integration trunk and `main` as the stable release branch, then "promote main to development" is either (a) wrong, (b) a sync-back pattern (hotfixes land on main directly in emergencies, then sync back to dev), or (c) custom Gesco terminology.

Research which of (a), (b), (c) is most likely. Primary: GitLab docs. Secondary: search GitHub for public repos with an `auto-pr-dev.yml`-like workflow that produces similar PR titles.

### Q5. What does canonical GitLab Flow branch protection look like?

Research the branch-protection configuration a correctly-implemented GitLab Flow repo has on each branch:

- On the integration trunk (where features land): what checks are required? Can admins bypass?
- On downstream env branches: what checks are required? Are they more or less strict than trunk?
- On release/production branches: full lockdown? Tag-only?

Primary: GitLab docs on "Protected branches" in the context of Flow.

Cross-check: examples from well-regarded open-source projects using GitLab Flow. Look for projects that document their branching in CONTRIBUTING or a branching guide. Candidates to investigate:

- `gitlab-org/gitlab` itself.
- Mid-size open-source projects on GitLab (5k+ stars) that document their branching.
- Companies publicly blogging about GitLab Flow at scale (GitLab's own "How we use GitLab Flow" blog, Meltano, Sourcegraph blog posts, etc.).

### Q6. How does "feature → main → env branches" compare to "feature → development → main → env branches" operationally?

This is the pragmatics question. Both patterns exist in industry. Research:

- Which pattern does GitLab itself use on its own `gitlab-org/gitlab` repo? (Primary: their CONTRIBUTING.md and MR practices.)
- What do experienced devops engineers recommend for a team with Gesco's shape (solo dev, 4 environments, no automated deploy pipeline to prod yet)?
- What are the failure modes of each?
- Does the choice affect Conventional Commits / squash-merging / PR title-as-commit-subject (Gesco follows these)?

Secondary: reputable engineering blogs (Thoughtworks Technology Radar, ShipIt!, The Pragmatic Engineer, High Scalability) from the last 3 years.

### Q7. Given the observed signals, which pattern does Gesco actually implement?

Assemble a decision table:

- Evidence that Gesco implements Pattern A (feature → main → env-branches-downstream).
- Evidence that Gesco implements Pattern B (feature → development → env-branches-downstream-from-dev).
- Evidence that Gesco implements a custom Pattern C (hybrid / mistaken).

The Phase-B PR targets (all to `main` per `01-CURRENT-STATE.md` §3) + the auto-PR direction (main → development per §2) both lean Pattern A. The `~/.claude/rules/code-standards.md` rule text leans Pattern B. PR #99 (targeted development, merged successfully) leans Pattern B.

Your job: decide which signal is authoritative for the INTENDED model. Hint: the actually-deployed workflow (auto-pr-dev.yml) is the source of truth for what the repo does; the rule text may be stale documentation from an earlier plan.

### Q8. If there is a mis-wire, what is the minimum correction?

Enumerate correction paths depending on the verdict:

- **If verdict is Pattern A (feature → main) and the code-standards rule is wrong:**
  - Fix: update `~/.claude/rules/code-standards.md` to say "branch from main, PR back into main; auto-promote to development; promotion through staging to production."
  - No repo-level changes needed; PR #99 was mis-targeted but the squash commit will naturally propagate to main at next dev→main sync (if one exists).
  - Verify: does a development → main sync workflow exist? If not, PR #99's content lives on development indefinitely and must be manually cherry-picked to main.

- **If verdict is Pattern B (feature → development) and the auto-PR direction is wrong:**
  - Fix: invert `auto-pr-dev.yml` so it produces "promote development to main" PRs instead.
  - Branch protection may need adjustment: `main` should have the strict checks only if it's trunk; under Pattern B, `production` should be the strictest.
  - Also rename / re-examine `production` and `staging` branch roles.

- **If verdict is hybrid (current state is a legitimate variant):**
  - Document the variant, its rationale, and the trade-offs. Update `code-standards.md` to match the variant precisely.

Include specific `gh` / `git` commands for the correction.

### Q9. What branch should PR-1A (Phase B-6 Tier-1 Appointments, the next PR after #99) target?

This is the concrete output Adrian needs. Given the verdict, which branch should the in-flight Phase B-6 work target from here on out?

- If Pattern A: PR-1A should retarget to `main`. PR #99 is a mis-target but its content will propagate on next sync.
- If Pattern B: PR-1A should continue to target `development` as PR #99 did. No correction needed.

### Q10. What docs / rules need to change downstream of the verdict?

List every file that must be updated to reflect the verdict. Candidates:

- `~/.claude/rules/code-standards.md` (global rule)
- `docs/verification/PHASE-B-CONTINUATION.md` (references branch protection and promotion)
- `docs/plans/2026-04-20-phase-b6-tier1.md` (frontmatter `base-branch:` value)
- `CLAUDE.md` root (if it describes the branching model)
- `.github/pull_request_template.md` (if it references branches)
- Other Gesco-project repos' equivalents (Case Tracking, MRR AI, Digital Forms — for when they come into doc scope)

Adrian will make these changes himself after reading your `VERDICT.md`. Your job is to produce the changelist, not execute it.

## Authoritative source list

You must consult at least one from each of the seven categories below. Prefer sources dated within the last 3 years; flag older sources explicitly.

### Category 1 — GitLab's own documentation (PRIMARY)

- [GitLab Flow documentation on docs.gitlab.com](https://docs.gitlab.com/ee/topics/gitlab_flow.html) — the canonical description (may have moved; search for "GitLab Flow" on docs.gitlab.com if the URL 404s).
- [about.gitlab.com/blog](https://about.gitlab.com/blog/) — search for "GitLab Flow" tag. The original 2014 post by Sid Sijbrandij if archived.
- [gitlab-org/gitlab CONTRIBUTING.md](https://gitlab.com/gitlab-org/gitlab/-/blob/master/CONTRIBUTING.md) — how GitLab itself uses GitLab Flow on their own codebase.
- The [GitLab Handbook](https://handbook.gitlab.com) for engineering workflow sections.

### Category 2 — Industry primary sources on branching strategies

- [Martin Fowler — Patterns for Managing Source Code Branches](https://martinfowler.com/articles/branching-patterns.html) (2020, likely updated since).
- [Martin Fowler — Deployment Pipeline](https://martinfowler.com/bliki/DeploymentPipeline.html) — Pattern 3 (environment-specific stages downstream) is Gesco's stated model.
- [Atlassian — Gitflow Workflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow) (for contrast).
- [Atlassian — GitLab Flow](https://www.atlassian.com/git/tutorials/comparing-workflows) — Atlassian's summary; note it may be less authoritative than GitLab's own.
- [GitHub — GitHub Flow](https://docs.github.com/en/get-started/using-github/github-flow) (for contrast).

### Category 3 — Microsoft / Azure DevOps / Google Cloud / AWS DevOps guidance

- [Microsoft DevOps branching strategies](https://learn.microsoft.com/en-us/azure/devops/repos/git/git-branching-guidance).
- [Google Cloud — Developer Workflow / Branching](https://cloud.google.com/architecture/) — search for branching strategies.
- [AWS DevOps branching](https://docs.aws.amazon.com/prescriptive-guidance/latest/choosing-git-branch-approach/) if a current prescriptive guide exists.

### Category 4 — Reputable engineering blogs (last 3 years)

- [Thoughtworks Technology Radar](https://www.thoughtworks.com/radar) — search for "branching strategies" or "trunk-based".
- [High Scalability blog](http://highscalability.com) — case studies.
- [The Pragmatic Engineer](https://blog.pragmaticengineer.com) — search "git branching" or "GitLab Flow".
- [Increment Magazine](https://increment.com) — articles on git / release engineering.

### Category 5 — Community perspectives

- Stack Overflow: search "gitlab flow vs git flow" with `created:2023..` filter for recent consensus.
- Reddit r/devops, r/programming, r/experienced_devs — search "GitLab Flow" for community experience reports.
- Hacker News: search "gitlab flow" on hn.algolia.com for threads with engineer discussion.
- dev.to: top-voted articles on GitLab Flow (filter by date).

### Category 6 — Individual expert practitioners

- Kent C. Dodds on branching (though his focus is more testing than DevOps).
- Charity Majors (Honeycomb) on deployment pipelines and branching.
- DHH / Jason Fried (37signals) on simplicity in branching.
- Thoughtbot engineering blog on git workflow choices.
- Shopify / Stripe / Airbnb engineering blog posts on branching.

### Category 7 — Cross-reference with similar-shape repos

Search GitHub for public repositories that:
- Have 4 long-lived branches (main, development, staging, production).
- Are .NET / enterprise shop.
- Document their branching in a `CONTRIBUTING.md` or `docs/branching.md`.

Examples to seek (verify they exist and are current):
- Tracee, Falco, or other CNCF projects with multiple environment branches.
- Any gov-tech / healthcare-tech project publicly documenting their branching.

This category is lowest-confidence (anecdotal) but useful for calibration.

## Methodology

### Zero-trust verification per `~/.claude/rules/zero-trust-verification.md`

Every claim in `VERDICT.md` must be independently verified against 2+ sources. When sources disagree, the disagreement is itself a finding — report it.

Do not rely on Claude's training data. The GitLab Flow whitepaper has evolved; what was true in 2019 may not be true in 2026. Every URL must be visited via WebFetch or WebSearch at research time and the content verified against the claim.

### Source dating

Every source cited in `VERDICT.md` must include its publication or last-modified date. Sources older than 3 years must be explicitly marked "may be outdated" and ideally cross-referenced with a newer source on the same point.

### Primary-source preference

When GitLab's own docs say one thing and a blog post says another, GitLab's docs win unless there's evidence the docs are out of date AND the blog post is more recent AND the blog post's source is credible (industry engineer with public track record). Quote both, explain the disagreement, default to primary.

### Contradiction handling

If research reveals that "GitLab Flow" genuinely has multiple canonical interpretations (possible — the name has loosened over a decade), your `VERDICT.md` must:

1. Document each canonical variant with its source.
2. Show how each variant maps to Gesco's current state.
3. Recommend which variant Gesco should formally adopt going forward.
4. Describe the migration cost of each.

The verdict is then not "which variant is correct" (that's subjective) but "which variant is Gesco implementing today AND does that match the intent."

### Zero-doubt bar

Adrian's explicit bar: "there should be no doubt that this is the correct and accurate way."

If you finish research and still have doubt, the correct action is NOT to ship a tentative verdict — it's to explicitly document what additional research / discussion would resolve the doubt, flag it as OPEN_QUESTION at the top of `VERDICT.md`, and recommend Adrian pause the verdict pending that resolution.

### Scope discipline

Do NOT expand the verdict to include:

- Git Flow vs GitHub Flow vs GitLab Flow comparison essays (except as context).
- Recommendations for Gesco to adopt a totally different strategy (like trunk-based).
- Opinions on squash-merge vs merge-commit vs rebase (that's separate; covered by `rules/commit-format.md`).

Stay focused on the specific 10 questions.

## Time estimate

A thorough investigation of this, done properly, is 3-6 hours of dedicated session work. If you find yourself closing the loop in under 2 hours, you probably have not consulted enough sources. If you find yourself at 8+ hours without a verdict, you're rabbit-holing — surface the specific blocker to Adrian and stop.
