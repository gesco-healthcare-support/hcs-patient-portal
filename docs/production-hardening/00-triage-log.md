# Triage log

Committed and append-only. Every finding NOT fixed, with the evidence for why.

**Why this file matters most.** A successor inheriting this epic will re-run the same scanners and
see the same numbers this document was written against. Without this log they re-investigate every
item already settled, or worse, "fix" a false positive and call it progress. Two of the six original
Sonar blockers were false positives, so that is not a hypothetical risk.

Never delete an entry. If a dismissal turns out to be wrong, add a superseding entry explaining what
changed -- the reversal is as informative as the original call.

Format: one entry per finding or family. State the evidence, not the conclusion alone.

---

## FALSE POSITIVE -- Sonar `secrets:S7539` x2 (BLOCKER)

**Where:** `scripts/dev/dev-api.ps1:34`, `scripts/dev/dev-authserver.ps1:37`
**Claim:** "Make sure this SQL Server password gets revoked, changed, and removed from the code."
**Verdict:** False positive. No credential is present in either file.

**Evidence.** Both scripts resolve the password at runtime and fail fast if it is missing:

```powershell
$sqlPwd = $env:MSSQL_SA_PASSWORD
if (-not $sqlPwd) {
    $envFile = Join-Path $RepoRoot ".env"
    if (Test-Path $envFile) {
        $line = (Get-Content $envFile | Select-String "^MSSQL_SA_PASSWORD=" | Select-Object -First 1).ToString()
        if ($line) { $sqlPwd = ($line -split "=", 2)[1] }
    }
}
if (-not $sqlPwd) { Write-Error "MSSQL_SA_PASSWORD not set (env var or .env file)"; exit 1 }
```

The flagged line is the `Select-String "^MSSQL_SA_PASSWORD="` search pattern. Sonar's secret
detector matched the shape `<SOMETHING>_PASSWORD=` inside a string literal and concluded it was an
assignment. It is a grep pattern.

Confirmed by counting literal assignments in both files -- zero matches for a password assigned to
anything other than a variable or `$env:` lookup:

```bash
grep -cE '(Password|pwd)\s*=\s*"[^"$]' scripts/dev/dev-api.ps1 scripts/dev/dev-authserver.ps1
# both return 0
```

This is also the pattern `~/.claude/rules/code-standards.md` mandates (env vars plus a git-ignored
`.env` for local dev, fail fast when missing), so the code is not merely innocent, it is correct.

**Action:** mark both "Safe" / won't-fix in SonarCloud with this rationale. No code change.

---

## MISCLASSIFIED -- 109 of 128 "CodeQL alerts" are OpenSSF Scorecard findings

**Claim:** the repository has 128 open CodeQL alerts.
**Verdict:** it has 19. The other 109 are Scorecard results delivered through the same
code-scanning API.

**Evidence.** Grouping open alerts by `rule.id`:

| Count  | Rule id                                                                          | Actually from |
| ------ | -------------------------------------------------------------------------------- | ------------- |
| 97     | `PinnedDependenciesID`                                                           | Scorecard     |
| 7      | `TokenPermissionsID`                                                             | Scorecard     |
| 1 each | `VulnerabilitiesID`, `FuzzingID`, `SASTID`, `CIIBestPracticesID`, `CodeReviewID` | Scorecard     |
| 16     | `cs/exposure-of-sensitive-information`                                           | CodeQL        |
| 3      | `cs/cleartext-storage-of-sensitive-information`                                  | CodeQL        |

Only rule ids prefixed `cs/` are CodeQL C# queries.

**Action:** not a dismissal. The Scorecard items are real and worth doing -- they are supply-chain
hardening, tracked in [02-enforcement.md](02-enforcement.md). But they are a bulk mechanical sweep
across 17 workflow files, not 109 code defects, and must not be counted or scheduled as such.

---

## PENDING TRIAGE -- entries to be added as each family is researched

The following are flagged but not yet investigated. Do not fix before triaging.

- `csharpsquid:S2068` x5 -- "hardcoded credentials". Given the two confirmed false positives above,
  assume nothing; read each site.
- `python:S8392` (BLOCKER) -- `docker/packet-renderer/app.py:116` binds to all interfaces. Inside a
  container this is normally intentional and the container is not published to the host in
  production. Confirm against `docker-compose.prod.yml` before changing.
- 13 of the 19 CodeQL sensitive-information alerts sit in data-seed contributors. Seeding runs via
  DbMigrator, which DOES run in production, so these may be real rather than dev-only. Verify.
