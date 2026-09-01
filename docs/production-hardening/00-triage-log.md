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

## FALSE POSITIVE -- Sonar `tssecurity:S6105` (BLOCKER), phase 1.1

**Where:** `angular/src/tenant-bootstrap.ts:65` (the `replace` call in
`detectTenantSlugAndMaybeRedirect`)
**Claim:** "Change this code to not perform client-side redirection based on user-controlled data."
**Verdict:** False positive as to the security concern. The redirect destination's ORIGIN cannot be
influenced by any caller. No open redirect exists. No behaviour change made.

**What the redirect composes.** One expression, from six inputs:

```ts
const target = `${loc.protocol}//${ADMIN_SLUG}.${baseHost}${port}${loc.pathname}${loc.search}${loc.hash}`;
```

| Component                      | Source                                    | Caller-influenceable?   |
| ------------------------------ | ----------------------------------------- | ----------------------- |
| `ADMIN_SLUG`                   | module literal `'admin'`                  | No                      |
| `baseHost`                     | deployment config (chain below)           | No                      |
| `protocol` / `port`            | the loaded document's own scheme and port | No (browser-normalised) |
| `pathname` / `search` / `hash` | the current URL                           | **Yes**                 |

**Q1 -- what feeds the target?** `baseHost` is operator-set deployment config, traced end to end:
`docker-compose.prod.yml:333` `APP_BASE_HOST: "${BASE_DOMAIN}"` -> `angular/prod-dynamic-env.envsh`
(nginx sources it at container start, writes `dynamic-env.json`) -> served same-origin by
`angular/nginx.conf:22` (`try_files $uri =404`, `no-cache`) -> fetched by relative path in
`angular/src/main.ts:21` and merged with `Object.assign(environment, await res.json())` ->
`main.ts:38` -> passed to the function at `main.ts:39`. Nothing on that chain reads a query
parameter, a fragment, `localStorage`, `postMessage`, or any other attacker-reachable input.
In the repo `angular/dynamic-env.json` is `{}`, so dev and tests fall back to `'localhost'`.

**Q2 -- reachable pre-authentication?** Yes. It runs inside the async IIFE in `main.ts` before
`bootstrapApplication`, so before any guard or token exists. This raises the stakes of the question
but does not by itself make it a defect.

**Q3 -- is the value ever legitimately external?** No. The destination host is always
`admin.<baseHost>` -- always first-party. There is no case, legitimate or otherwise, in which this
function navigates off the deployment's own domain.

**Why the caller-controlled parts cannot relocate the origin.** For a document with a special
scheme (http/https), `location.pathname` always begins with `/` per the WHATWG URL specification.
The authority component of the composed URL therefore terminates at that `/`, and `pathname`,
`search` and `hash` are appended strictly after it. The classic breakouts all fail for that reason:
`//evil.example.net` becomes a path, not an authority, and `@evil.example.net` lands after the
authority so it cannot act as a userinfo separator.

This is now pinned by executable tests rather than argued in prose --
`angular/src/tenant-bootstrap.spec.ts`, describe block "a crafted external target is rejected --
destination origin is not caller-controlled": nine cases feeding crafted pathnames
(`//evil.example.net/signin`, `///evil.example.net`, `/@evil.example.net`, `/..//evil.example.net`,
`/%2f%2fevil.example.net`, `/\evil.example.net`), plus crafted query and fragment, each asserting
`new URL(target).origin` is still `https://admin.portal.example.com`.

**Q3a -- is it a link in a redirect chain?** No. The carried `search`/`hash` reach the admin surface,
so the next question is whether anything downstream redirects to a value read out of them. Nothing
does: the only SPA navigations are `patient-profile-redesign.component.ts:240`
(`${issuer}/Account/Manage`) and `appointment-view.component.ts:224` (`/appointments/view/${id}`),
both composed from internal values. `returnUrl` appears only as a field on a generated proxy DTO
(`app/proxy/external-account/models.ts:15`) and is never consumed as a redirect target in the SPA.

**Why Sonar flags it.** The taint engine sees `window.location.*` (source) reach
`window.location.replace` (sink) and cannot reason about WHERE in the composed string the tainted
values land. It is right that tainted data reaches the sink; it is wrong that this makes the
destination attacker-selectable.

**Action:** mark **False Positive** in SonarCloud citing this entry. No production behaviour change.
Ruled by Adrian 2026-08-31: false positive, no guard, no code change.

The transition is "False Positive", NOT "Safe". S6105 is a VULNERABILITY issue, and only Security
Hotspots offer Safe / Fixed / Acknowledged. "Safe" is correct for the `secrets:S7539` hotspots
earlier in this log and wrong here; the two are different object types in SonarCloud with different
workflows. Two separate issue keys exist for the same finding -- `AaBaLBBTO0wh9AbG_-jm` on PR #498
at line 95 (the one the PR quality gate reads) and `AZ4ihTDXRucw6R5zqNpc` on `main` at line 65.
Marking needs project admin rights; Adrian holds them.
Two non-behavioural changes were made and are worth recording:

1. A `TenantBootstrapLocation` seam (an optional second parameter defaulting to `window.location`)
   so the redirect path is unit-testable at all. `window.location.replace` cannot be spied on in
   Chrome, and calling the real one navigates the karma runner out of its own suite.
2. The tests above, which turn this dismissal into something a successor can re-run in seconds
   instead of re-deriving. Their teeth were checked by mutation: reintroducing an
   attacker-controlled destination makes six of them fail.

**Not done, deliberately -- open for Session A.** A defence-in-depth guard (compose with the `URL`
API, verify the resulting origin is `admin.<baseHost>`, and on mismatch drop path/query/hash and go
to the admin root) would satisfy the phase-1.1 EARS criterion literally and would additionally
contain OPERATOR misconfiguration -- e.g. `BASE_DOMAIN=gesco.com@evil.com` or a trailing slash would
compose an off-origin URL today, because nothing validates `baseHost` after the
`Object.assign` merge. That is a config-integrity concern, not the reported vulnerability, and the
attacker in it is the operator. It was NOT built, because the epic's rule is not to change code to
satisfy a scanner and the reported defect is absent. Session A's call.

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
