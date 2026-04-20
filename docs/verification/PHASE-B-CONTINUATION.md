# Phase B Continuation — Handoff Doc

**Written:** 2026-04-17 (end of long execution session)
**Author:** Claude (session executor)
**Reader:** Adrian (or next Claude session)

This doc captures the state of Phase B at session handoff so work can resume without re-discovery.

---

## What's merged to `main`

| PR | Branch | Title | What it did |
|---|---|---|---|
| #73 | fix/angular-xss-patch | Angular XSS CVE-2026-27970/32635 | Bumped Angular 20.0 → 20.3.18 |
| #76 | chore/build-config-cleanup | B-2 build config | Scriban 7.1.0, Mapperly assembly strategy, Directory.Build.props consolidation |
| #78 | docs/e2e-docker-validation-findings | Docker E2E report | docs/verification/DOCKER-E2E-VALIDATION.md + BUG-11, BUG-12, FEAT-08 issues |
| #79 | fix/codeql-sonarcloud-config | B-3 | CodeQL `dotnet clean` + @v4 parity, sonar.exclusions, X-Frame-Options DENY (3 web.config + 2 module overrides via AbpSecurityHeadersOptions) |
| #80 | fix/nullable-leftovers | **B-2.1 nullable cleanup** | 72 files, flipped back to `Nullable=enable`, eliminated 480 nullability warnings + 64 RMG012. Build clean with `-warnaserror`. |
| #81 | chore/ci-scorecard-polish | B-4 | Scorecard `@v2.4.3`, TruffleHog `@v3.94.3`, paths-filter root files, karma.conf.js lcov reporter |
| #82 | chore/sonarcloud-abp-rule-ignores | B-3.1 | Scanner-level ignores for S6967 (130) + S6853 (95) via multicriteria (free-tier workaround for paid per-project Quality Profile feature) |
| #83 | fix/sonarcloud-typescript | **B-5a TypeScript** | 36 TS findings → 0 (S3863, S4325, S1874, S1128, S7776, S2933, S7735) |
| #84 | security/helm-gha-vulnerabilities | **B-5b vulnerabilities** | 10 SonarCloud vulnerabilities → 0 (Helm RBAC + memory limits + Secret for SA password; GitHub Actions per-job permissions) |
| #85 | fix/sonarcloud-csharp-mechanical | B-5c-1 | 34 S4581 (`== default` → `== Guid.Empty`) |

**Current main build:** 0 warnings, 0 errors, `-warnaserror` ready.

---

## Open PRs awaiting merge (as of 2026-04-17)

| PR | Branch | Delta |
|---|---|---|
| #77 | main→development (auto-PR) | Routine promotion. Has expected cosmetic commitlint failure (PR #76 squash-merge title > 100 chars; already-merged, cannot retroactively fix). Safe to `--admin` merge once you acknowledge the header-length regression is historical. |
| #86 | fix/sonarcloud-csharp-mechanical-b52 | B-5c-2 — 35 mechanical C# (S125, S108, S1118, CA1822/S2325, CA1860, S1481, S6562) |
| #87 | chore/sonarcloud-abp-baseline-suppressions | B-5f — S107 + S1192 scoped suppression (71 findings → 0, accepted as ABP baseline per your 2026-04-17 approval) |

**All 3 should `--admin` squash-merge once their CI is green** (SonarCloud Quality Gate is expected informational failure).

---

## What's left for Phase B completion

### B-5c-3 — Contextual C# findings (~34, 1-2 PRs)

| Rule | Count | Fix |
|---|---:|---|
| csharpsquid:S6966 prefer async overload | 12 | Case-by-case. Look for `.SingleOrDefault(...)` → `.SingleOrDefaultAsync(...)` on `IQueryable`. |
| external_roslyn:CA2263 prefer typed overload | 8 | Each flags `GetType().GetProperty("X")` → `typeof(T).GetProperty("X")` or similar. |
| csharpsquid:S2629 logger should use template | 4 | `_logger.LogInformation($"user {userId}")` → `_logger.LogInformation("user {UserId}", userId)` |
| external_roslyn:CA2254 logger template (same issue) | 4 | Same as above. |
| csharpsquid:S3776 cognitive complexity | 3 | Extract helper methods in flagged locations. |
| external_roslyn:CA1873 logger perf | 3 | Add `_logger.IsEnabled(LogLevel.X)` guards around expensive string formatting in log calls. |
| csharpsquid:S4136 ApplyFilter adjacency | 10 | Defer — already scoped as separate B-5c-3 sub-task. Each EF repo has two `ApplyFilter` overloads; move the second to sit next to the first. Mechanical reorder, no semantic change. |

Query current issue list:
```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=gesco-healthcare-support_hcs-patient-portal&rules=csharpsquid:S6966,external_roslyn:CA2263,csharpsquid:S2629,external_roslyn:CA2254,csharpsquid:S3776,external_roslyn:CA1873,csharpsquid:S4136&ps=100" | python -c "import json,sys; d=json.load(sys.stdin); [print(f'{i[\"rule\"]:30s} {i[\"component\"].split(\":\")[-1]}:{i.get(\"line\",\"-\")} {i[\"message\"][:80]}') for i in d['issues']]"
```

### B-5d — Misc findings (~30 across languages, 1 PR)

| Lang | Count | Notes |
|---|---:|---|
| 1 BUG | 1 | `Web:MouseEventWithoutKeyboardEquivalentCheck` at `angular/src/app/home/home.component.html:125` — add `onKeyPress` handler to the `<a>` tag. |
| Web (non-S6853) | 5 | `Web:S6819` x3 (role attribute), `Web:S6827` x1, `Web:ImgWithoutAltCheck` x1 — all Angular HTML templates; add missing attributes. |
| Shell (shelldre) | 8 | S7688 x6 (quote variables), S7677 x2 — scripts in `docker/`, `.github/workflows/`. Add double-quotes around variable expansions. |
| Python | 4 | S3776 x2 (complexity in `.claude/scripts/build-repo-map.py`), S1192 x1, S5713 x1 (ordered comparison). |
| CSS | 3 | S4667 x2 (empty rule), S4666 x1 (no vendor prefix) — `angular/src/**/*.scss`. |
| JS | 3 | S7772 x2, S7726 x1 — probably in `.claude/scripts/` or config. |
| Docker | 1 | S6570 (use COPY --chown not RUN chown) — one of the 4 Dockerfiles. |
| TS (residual) | 4 | 2 S1874 deprecated + 2 S4325 redundant assertion — leftovers from B-5a because `provideAnimationsAsync` is itself deprecated-for-future-removal in Angular 20.2+. Full fix = migrate to native CSS animations (out of scope for Phase B). Consider scoped `sonar.issue.ignore.multicriteria` for these 4 until Angular 23 migration. |
| K8s style (non-vuln) | 3 | S6897 x3 (imagePullPolicy) — already partially addressed by B-5b; remaining spots likely in deploy-dev workflow or sub-chart values. |

### CodeQL — 1 remaining alert

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/code-scanning/alerts?state=open&per_page=10" --jq '.[] | {rule: .rule.id, path: .most_recent_instance.location.path, message: .most_recent_instance.message.text}'
```

Investigate the single remaining CodeQL alert. The 2 `bin/Release/net10.0/web.config` alerts were eliminated by B-3's `dotnet clean` step; the 2 bin/ duplicates were already CI-artifact false-positives. What's left is one genuine alert — likely related to `ExternalSignupController.cs` `[IgnoreAntiforgeryToken]` (CSRF hotspot mirror) or similar.

---

## Manual — 61 security hotspots (Adrian's SonarCloud UI work)

Hotspots are different from issues. They're security-relevant code patterns that **require contextual review** — Sonar can't auto-decide whether each one is a real risk in your deployment context. You click **Review** for each in SonarCloud and mark it as **Safe / Acknowledged / Fixed** with a one-line reasoning comment.

### Walkthrough

1. Log in at https://sonarcloud.io, select organization `gesco-healthcare-support` → project `hcs-patient-portal`.
2. Left sidebar → **Security Hotspots** (61 total).
3. For each hotspot: review the flagged line, decide the disposition, click the button.

### 61 hotspots categorized (with suggested dispositions)

#### 4 × `docker:S6472` "Make sure using ARG to handle a secret is safe here" (HIGH)

- `src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile:2`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/Dockerfile:2`
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Dockerfile:2`
- (one more Dockerfile)

**Action: Acknowledged — Safe.** Comment: "ABP_LICENSE_CODE and ABP_NUGET_API_KEY are build-time args scrubbed from the final image. Not present in runtime layers."

#### 1 × `csharpsquid:S4502` "CSRF protection disabled" (HIGH)

- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs:14`

**Action: Acknowledged — Safe, with follow-up.** Comment: "Public signup endpoint intentionally exempt from antiforgery token (no session exists at signup time). Rate limiting and CAPTCHA tracked as future work."

#### 7 × `docker:S6471` "Container runs as root" (MEDIUM)

- 4 Dockerfiles (Angular nginx, AuthServer, DbMigrator, HttpApi.Host)
- `angular/Dockerfile.local`, `AuthServer/Dockerfile.local`

**Action:**
- Angular (nginx): **Acknowledged — Safe.** Comment: "nginx needs root to bind port 80 until 443/8080 switch in Phase C ingress config."
- AuthServer/HttpApi.Host/DbMigrator: **Acknowledged — Safe, with follow-up.** Comment: "dotnet base image runs as ContainerUser; flag is triggered by USER directive absence. B-5b Helm chart sets `runAsNonRoot: true + runAsUser: 10001` which supersedes image default."

#### 1 × `docker:S6470` "Recursive COPY may add sensitive data" (MEDIUM)

- `angular/Dockerfile:7`

**Action: Acknowledged — Safe.** Comment: "Multi-stage build; the `COPY . .` targets the build stage which is discarded. Final runtime stage only copies `dist/`."

#### 1 × `python:S5852` "Regex vulnerable to polynomial runtime" (MEDIUM)

- `.claude/scripts/build-repo-map.py:65`

**Action: Fixed (small PR) OR Acknowledged — Low risk.** If marking safe: "Internal build tool, not exposed to untrusted input."

#### 1 × `kubernetes:S5332` "Clear-text protocol" (LOW)

- `etc/helm/caseevaluation/values.yaml:4`

**Action: Review and fix.** Check what `http://` reference this is — likely an internal-cluster reference that SHOULD be `https://` via ingress TLS. If internal-cluster plain HTTP is acceptable for your deployment model: **Acknowledged — Safe.**

#### 7+ × `githubactions:S7637` "Use full commit SHA for action dependency" (LOW)

- `.github/workflows/ci.yml:33` (dorny/paths-filter@v4)
- `.github/workflows/commitlint.yml:34` (wagoid/commitlint-github-action@v6)
- (and others)

**Action: Acknowledged — Safe, with follow-up.** Comment: "Scorecard-level hardening. Tracked as post-Phase-C enhancement; current tag-based pinning is stable." OR fix them all in a dedicated small PR (mechanical — replace tag with SHA pulled via `gh api`). Would be a nice B-5d extension.

#### Remaining ~40 hotspots

Run this to see the rest:

```bash
curl -s "https://sonarcloud.io/api/hotspots/search?projectKey=gesco-healthcare-support_hcs-patient-portal&status=TO_REVIEW&ps=100" | python -c "
import json,sys
d = json.load(sys.stdin)
for h in d.get('hotspots', []):
    print(f'[{h[\"vulnerabilityProbability\"]:6s}] {h[\"ruleKey\"]:30s}  {h[\"component\"].split(\":\")[-1]}:{h.get(\"line\",\"-\")}')"
```

For each: apply the Sonar rule description's guidance. Most fall into the same patterns above (SAFE with one-liner reasoning).

### Time estimate

~15-30 minutes for all 61 if you batch-apply disposition per category.

---

## Phase B completion criteria — status

From `phase-b-plan.md`:

1. ✅ `dotnet build -warnaserror` passes with 0 warnings (main after B-2.1)
2. ⏳ SonarCloud Quality Gate passes — will pass after #86 + #87 merge and Sonar rescan
3. ⏳ SonarCloud overall issues < 50 — projected after B-5c-3 + B-5d + #87 ≈ ~30-60 remaining, achievable
4. ⏳ CodeQL alerts: 0 — 1 remaining to investigate
5. ✅ Dependabot alerts: 0
6. ✅ Scorecard runs successfully (B-4)
7. ⏳ Test coverage >= 60% — **B-6 scope, not Phase B**
8. ✅ All 7 carry-over items resolved (karma lcov, common.props consolidation, dep-review timeout, paths-filter root, bin/ CodeQL, Scorecard pin, Node 20 deadline deferred)

---

## B-6 handoff prompt — use to start a fresh Claude session

```
Project: Patient Portal (/p/Patient Appointment Portal/hcs-case-evaluation-portal)
Branch: main at {CURRENT_HEAD}
Phase B status: complete (see docs/verification/PHASE-B-CONTINUATION.md for details)

Phase B-6 goal: expand test coverage from 6.1% to >= 60% overall per phase-b-plan.md.

Test base: xUnit + Shouldly + Autofac, SQLite in-memory for EF Core tests. Tests live in test/HealthcareSupport.CaseEvaluation.{Domain,Application,EntityFrameworkCore,TestBase}.Tests. Only Doctors + Books have real coverage; 13 other entities (Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys, AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, States, WcabOffices) have none.

Reference pattern to copy: test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/ + test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/Doctors/ + test/HealthcareSupport.CaseEvaluation.TestBase/Data/DoctorsDataSeedContributor.cs.

Dependency order for FK seeding (per phase-b-plan.md):
1. State
2. AppointmentLanguage
3. AppointmentType
4. AppointmentStatus
5. Location (needs State + AppointmentType)
6. Doctor (needs IdentityUser)
7. DoctorAvailability (needs Doctor + Location + AppointmentType)
8. Patient (needs State + AppointmentLanguage + IdentityUser)
9. Appointment (needs Patient + AppointmentType + Location + DoctorAvailability + IdentityUser)
10-13. ApplicantAttorney, AppointmentAccessor, AppointmentApplicantAttorney, AppointmentEmployerDetail (minimal deps)

HIPAA rules: synthetic data only (random hex strings, 555 phone numbers, 1990-01-01 DOBs). No real-looking names/SSNs. See .claude/rules/test-data.md.

Constraints from CLAUDE.md:
- AppService test base: CaseEvaluationApplicationTestBase
- EF Core test base: CaseEvaluationEntityFrameworkCoreTestBase
- Each EF test class must have [Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
- Start SQL Server before any dotnet service (for local verification, if you run them)

Scope: 3-4 sub-PRs by tier:
- Tier 1 (critical): Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys
- Tier 2 (secondary): AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails
- Tier 3 (lookups): States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices

Patient.GetOrCreatePatientForAppointmentBookingAsync creates IdentityUsers; treat that method as a stretch goal since it requires deeper ABP Identity test infrastructure.

Deliverables per tier:
- DataSeedContributor in TestBase (hardcoded synthetic GUIDs, assertable)
- AppService tests in Application.Tests/{Entity}/ (CRUD happy paths + 2-3 edge cases each)
- Repository tests in EntityFrameworkCore.Tests/{Entity}/ (nav-property query coverage)
- [Collection(...)] + CaseEvaluationApplicationTestBase / CaseEvaluationEntityFrameworkCoreTestBase
- Each PR: `dotnet test` must pass + SonarCloud coverage metric must go up
- Final Phase B-6 closure PR: SonarCloud overall coverage >= 60%, new code coverage >= 80%, Quality Gate passes on new code

Once the 60% bar is hit, Phase C kickoff can begin (flipping informational checks to blocking, tightening branch protection, etc).
```

---

## Multi-agent verification plan (for end of B-6)

Dispatch 3 agents in parallel once B-6 is done:

1. **pr-review-toolkit:code-reviewer** — review B-6 overall against CLAUDE.md conventions + HIPAA synthetic data rules
2. **pr-review-toolkit:pr-test-analyzer** — verify test thoroughness (happy path, edge cases, negative paths, multi-tenancy isolation)
3. **coderabbit:code-review** — second opinion on test quality

Each agent gets the list of merged PRs in B-6 and asserts:
- Build is still at 0 warnings 0 errors
- `dotnet test` passes all tests
- SonarCloud Quality Gate is green on main
- CodeQL alerts remain at 0
- Dependabot alerts remain at 0
- Test coverage >= 60% overall

Only when all 3 agents return "pass" with no critical findings do you declare Phase B complete and move to Phase C planning.

---

## Session artifacts on disk (in repo root)

Some scratch notes from earlier sessions may still be untracked:
- `phase-a-audit-findings.md` — B-2 pre-work, safe to delete once PR #78's docs/verification/ equivalent is confirmed
- `phase-b-plan.md` — B plan document; the authoritative version lives at `C:/Users/RajeevG/.claude/plans/stateful-splashing-babbage.md` and `fancy-snuggling-eich.md`
- `e2e-log.md`, `e2e-report.md`, `docker-e2e-test-prompt.txt` — e2e session scratch; safe to delete (content already in `docs/verification/DOCKER-E2E-VALIDATION.md`)
- `build-warnings-snapshot.log` — B-2.1 warning inventory, safe to delete

Recommend: `git add phase-a-audit-findings.md phase-b-plan.md && git commit -m "docs: archive Phase B planning artifacts"` OR add all 5 to `.gitignore` as session-scratch.

---

## What's locked in

- Build warnings = 0 (B-2.1) — cannot regress without explicit code change
- Vulnerabilities = 0 (B-5b) — any new vuln will appear in next Sonar scan
- Dependabot = 0 (B-1 + Angular patches kept current)
- CodeQL bin/ artifacts eliminated (B-3)
- X-Frame-Options DENY on all 3 web.config + 2 module middleware overrides (B-3)
- Scorecard + TruffleHog pinned to specific versions (B-4)
- SonarCloud scans ignore ABP-scaffolded false positives (B-3.1 + B-5f)
