[Home](../../INDEX.md) > [Issues](../) > Research > SEC-06

# SEC-06: Scorecard 100 open alerts — Phase-C remediation plan

**Risk tier**: Supply-chain / CI hardening (informational)
**Status**: Deferred to Phase C — Phase B criterion #6 ("Scorecard runs successfully") is met
**Nature**: Alerts from the OpenSSF Scorecard action. None of them block Phase B closure per `docs/verification/PHASE-B-CONTINUATION.md` line 145. This doc tracks the Phase-C cleanup plan so the debt is visible and ordered.

---

## Snapshot verified 2026-04-20

- 89 × **PinnedDependenciesID** — GitHub Actions pinned by tag (`actions/checkout@v4`) instead of full commit SHA; Dockerfile base images pinned by tag (`mcr.microsoft.com/dotnet/aspnet:10.0`) instead of digest.
- 5 × **TokenPermissionsID** — mostly `doc-check.yml` which has no steps and therefore no `permissions:` block; may also include re-detections on workflows without top-level `permissions:`.
- 1 × **CodeReviewID** — branch protection requires approvals but admin-merge bypasses. Scorecard wants real merge-time reviews.
- 1 × **VulnerabilitiesID** — Scorecard's own dependency scan; should clear on next rerun since Dependabot=0.
- 1 × **SASTID** — Scorecard wants SAST in CI; CodeQL is already running, so this should clear on next rerun.
- 1 × **MaintainedID** — passive; passes automatically as long as commits continue.
- 1 × **FuzzingID** — requires OSS-Fuzz integration; .NET support is limited; deferrable indefinitely.
- 1 × **CIIBestPracticesID** — manual signup at bestpractices.dev; informational badge only.

## Why this is Phase C, not Phase B

Per [docs/verification/PHASE-B-CONTINUATION.md](../../verification/PHASE-B-CONTINUATION.md) Phase B criterion #6 is "Scorecard runs successfully" — not "alerts = 0." The criterion was met when PR #81 pinned `ossf/scorecard-action` to `@v2.4.3`. None of the 100 alerts blocks a deploy or weakens code-level security — they are supply-chain hardening signals.

Fixing them now would:
- Add a 1-hour mechanical PR (SHA pinning all actions via StepSecurity secureworkflow).
- Add a 30-minute PR for Dockerfile digest pinning (7 files).
- Add a policy decision for CodeReviewID that reshapes the solo-dev workflow.
- Delay Phase B-6 (the actual critical-path Phase-B finisher — test coverage).

## Phase-C remediation sequence (recommended)

1. **SHA-pin workflow actions** (~89 alerts): run `stepsecurity secureworkflow github.com/gesco-healthcare-support/hcs-patient-portal/<workflow>.yml --enable pin-actions` against each workflow. Commit the rewritten YAMLs. One PR covers all 17 workflows.
2. **Digest-pin Dockerfile base images** (~11 alerts): for each of the 7 Dockerfiles, run `docker pull <image>:<tag>` and read the digest via `docker inspect --format='{{index .RepoDigests 0}}' <image>:<tag>`. Rewrite `FROM <image>:<tag>` to `FROM <image>:<tag>@sha256:<digest>`. Single PR. Do this AFTER confirming Scorecard actually flags Dockerfile base images under `PinnedDependenciesID`.
3. **TokenPermissions sweep** (5 alerts): delete the empty `doc-check.yml` if it's still unused, or add a top-level `permissions: {contents: read}` block. Recheck remaining 4 in case they're stale.
4. **Rerun Scorecard** after #1-#3 to let VulnerabilitiesID / SASTID / MaintainedID auto-clear. `gh workflow run scorecard.yml --ref main`.
5. **CodeReviewID — branch-protection decision**: solo-dev context makes real reviews impractical. Options: (a) install Copilot Code Review or CodeRabbit as a first-party reviewer so merges have actual approvals, (b) accept the alert as known, (c) disable required reviews (lowers security posture). This is a Gesco policy call, not a mechanical fix. Document whatever Adrian decides.
6. **FuzzingID**: defer unless .NET-specific fuzzing (e.g., SharpFuzz) becomes a priority.
7. **CIIBestPracticesID**: defer unless Gesco wants the public badge for external signaling.

## Phase-C success definition

- PinnedDependenciesID = 0.
- TokenPermissionsID = 0.
- VulnerabilitiesID / SASTID / MaintainedID = 0 (auto-cleared).
- CodeReviewID: either = 0 (policy enforced) or documented as accepted risk in a decision-record.
- FuzzingID / CIIBestPracticesID: either fixed or explicitly documented as "out of scope for Phase C."

## Related

- [docs/verification/PHASE-B-CONTINUATION.md](../../verification/PHASE-B-CONTINUATION.md) — Phase-B closure criteria and "deferred to Phase C" notes.
- [docs/plans/PHASE-B6-TEST-COVERAGE-KICKOFF.md](../../plans/PHASE-B6-TEST-COVERAGE-KICKOFF.md) — Phase-B6 handoff (added as part of Phase-B closure).
- Phase-B closure local plan: `C:\Users\RajeevG\.claude\plans\this-was-the-original-mellow-stonebraker.md` Step 5j.
- [P-11](P-11.md) — DoctorAvailabilities S3776 refactor, also Phase-C.
