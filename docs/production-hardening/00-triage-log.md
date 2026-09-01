# Triage log

Committed and append-only. Every finding NOT fixed, with the evidence for why.

**Why this file matters most.** A successor inheriting this epic will re-run the same scanners and
see the same numbers this document was written against. Without this log they re-investigate every
item already settled, or worse, "fix" a false positive and call it progress. That is not a
hypothetical risk, and the finished tally proves it: of the six original Sonar BLOCKERs, **five
issues -- four distinct findings -- were false alarms, and exactly ONE was a real defect.** All six
are now triaged. A mechanical sweep would have changed working code in four places, and in one of
them (the packet renderer) it would have broken PDF generation outright. Per-rule breakdown with the
command that produces it is in the phase 1.4 entry below.

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

## REAL -- Sonar `typescript:S6268` (BLOCKER), phase 1.2. Fixed, but the issue stays OPEN.

**Where:** `angular/src/app/shared/ui/icon/icon.component.ts:58`
**Verdict:** the one genuinely real BLOCKER of the six so far. Fixed in `ad4cb0d7` (#502).
**Why it is in a log of things NOT fixed:** because the ISSUE is not resolved even though the DEFECT
is. The fix constrains every input but deliberately keeps the `bypassSecurityTrustHtml` call, and the
rule fires on the call's presence, so `AZ76eJ5Af85aalgSOdg0` remains OPEN and will keep appearing in
the BLOCKER count. A successor who sees it open and assumes nothing was done will redo this work.

**What was real and what was not.** Three values are interpolated into the trusted markup:

- `name` -- **LIVE path from server data.** `DashboardActivityItemDto.icon` (the only icon field in
  the entire generated proxy tree) reaches it through an unchecked `as IconName` cast at
  `src/app/dashboard/internal-dashboard.component.ts:291`. The registry is a plain object literal, so
  a lookup keyed on an inherited member returned a function or object that `?? ''` cannot catch, and
  native-code text rendered into the page. Real, and reachable.
- `size` -- no live path (all 45 call sites pass numeric literals), but the pre-fix test PROVED a
  string value closes the `width` attribute and injects a working `onload`. Demonstrated, never live.
- `label` -- no dynamic binding exists anywhere; the existing escaping is correct for a
  double-quoted attribute. Pinned by a test, not changed.

**Practical consequence had it shipped untouched:** occasional stray internal text on the dashboard.
It could not execute code and could not leak data.

**THE DISTINCTION THAT KEEPS 1.1 AND 1.2 CONSISTENT.** 1.1 declined to add a guard; 1.2 added one to
`size` even though nothing feeds it. That is not a reversal:

> **Structural guarantees do not need guards. Conventions do.**

On 1.1 the redirect origin is fixed by a STRUCTURAL property -- the WHATWG URL Standard guarantees a
special-scheme path serialises with a leading `/` -- so no future caller can break it and a guard
would defend against something that cannot happen. On `size` the safety rested on CALLER DISCIPLINE
across 45 sites and a TypeScript annotation that is erased at runtime, so one
`[size]="cfg.iconSize"` commit makes it live. The test is that, not "did the scanner complain".

**Action:** the transition is **Accept (won't fix)**, NOT False Positive. The code genuinely does
disable sanitization, so dismissing it as a false positive would be a lie in the record -- unlike
1.1, where the scanner was simply wrong. Needs admin rights. It did NOT fire on #502: Sonar matched
the edit to the pre-existing issue rather than raising a new one, so the PR gate passed with 0 issues
attributed and no transition was required to merge.

**FOLLOW-UP QUEUED -- remove the bypass entirely.** Restructure the registry from raw SVG fragment
strings into structured data rendered by Angular template bindings, which deletes the
`bypassSecurityTrustHtml` call rather than securing it, and closes S6268 by construction. Approved by
Adrian as SEPARATE work. Deliberately given no phase-1 section: once the issue is accepted it is no
longer a blocker, and inventing a slot would distort the phase.

---

## FALSE POSITIVE -- Sonar `typescript:S2699` (BLOCKER), phase 1.3

**Where:** `angular/src/app/shared/auth/full-logout.spec.ts:47`
**Claim:** "Add at least one assertion to this test case."
**Verdict:** False positive. The test asserts, and the assertion has teeth. Proven by mutation, not
argued. Issue key `AZ-Au9J_eUr9gP0iFbsb`.

**The phase file's original premise was also wrong** and has been corrected in
[01-blockers.md](01-blockers.md): it read "The test passes unconditionally. It is worse than no
test." It does not pass unconditionally.

**Evidence.** The flagged `it(...)` opens at `:47` and its assertion is at `:55`:

```ts
await expectAsync(performFullLogout(injectorFor(oauth))).toBeResolved();
```

`toBeResolved()` fails the spec if the promise rejects. "Never rejects" is precisely the documented
contract of `performFullLogout` (`full-logout.ts:27-29`) -- a failed revocation must still land the
user on the login page -- so this is the correct assertion for that test, not a missing one.

**Why the rule fires.** S2699 looks for `expect(` and does not recognise `expectAsync`. Supporting
correspondence, each counted by command:

```bash
# open S2699 issues project-wide -> 1
curl -s ".../api/issues/search?...&rules=typescript:S2699&resolved=false"
# occurrences of expectAsync in every spec under src -> 1
grep -rn "expectAsync" --include=*.spec.ts src/
```

Both point at the same file. The single test in the application written with `expectAsync` is the
single test the rule flagged. That is corroboration at n=1, not proof, which is why the mutation
below is the actual evidence.

**Mutation proof.** A `throw` was added after the try/catch in `performFullLogout`, breaking the
never-reject contract, and the scoped spec was re-run:

```
performFullLogout never rejects even if both revocation and the fallback throw FAILED
    Expected a promise to be resolved but it was rejected with Error: mutation: performFullLogout now rejects.
TOTAL: 8 FAILED, 0 SUCCESS
```

The flagged spec fails on its own assertion. Mutation reverted; `git diff` on the implementation is
empty and a residue grep returns 0.

**Action:** mark **False Positive** in SonarCloud citing this entry. No implementation change.

**FOUR OF THE SIX ORIGINAL BLOCKER ISSUES ARE NOW FALSE POSITIVES** -- which is THREE distinct
findings, because the two `secrets:S7539` PowerShell hits are the same false positive twice. Stating
it both ways deliberately: "three" and "four" are both true of different units, and an unqualified
number here is exactly what a successor would re-check and distrust.

Per-rule state, counted by the API rather than by memory:

```bash
for r in secrets:S7539 tssecurity:S6105 typescript:S2699 typescript:S6268 python:S8392; do
  curl -s ".../api/issues/search?componentKeys=...&rules=$r&severities=BLOCKER"
done
```

| Rule               | n   | Verdict                                                              |
| ------------------ | --- | -------------------------------------------------------------------- |
| `secrets:S7539`    | 2   | false positive, still OPEN in SonarCloud (not yet marked)            |
| `tssecurity:S6105` | 1   | false positive, RESOLVED/FALSE-POSITIVE                              |
| `typescript:S2699` | 1   | false positive (this entry), still OPEN                              |
| `typescript:S6268` | 1   | **the one real defect**, fixed in `ad4cb0d7`, OPEN pending an Accept |
| `python:S8392`     | 1   | not yet triaged (1.4)                                                |

So of six flagged BLOCKERs, exactly ONE has so far turned out to be a real defect, and that one was
narrower than advertised. This is a finding about the tool, not the code: a mechanical sweep would
have changed working code in three places to satisfy a scanner. Open BLOCKERs today:

```bash
curl -s ".../api/issues/search?...&resolved=false&severities=BLOCKER&ps=1"   # total -> 5
```

**THE REAL GAP WAS NEXT TO IT, and no scanner raised it.** Sign-out expires `__tenant` and
`XSRF-TOKEN` before redirecting, because the end-session flow does not clear them and, quoting
`full-logout.ts:22-23`, "a stale `__tenant` can leak the prior user's tenant into a fresh
registration on the same browser". On a shared machine in a medical office that is a patient-data
boundary. Before this task NOTHING tested either cookie -- counted by command:

```bash
git grep -l "__tenant\|XSRF-TOKEN" e19c46ed -- 'angular/src/**/*.spec.ts' | wc -l   # -> 0
```

and `full-logout.ts:33` is the only place `__tenant` is cleared (the other two references, at
`app.config.ts:149,151`, are comments). Five characterization specs now cover it, including the
inverse case that the theme and culture cookies are deliberately PRESERVED, so a future
"clear everything on logout" change fails loudly instead of resetting every user's preferences.

The cookie specs passed on first run, which is the point of a characterization test: the clearing
works today and is now pinned. Had they failed, that would have been a live defect on the leak path
rather than a test to iterate on.

**WHAT FULL LOGOUT GUARANTEES, AND WHAT CAN HONESTLY BE ASSERTED.** From the doc comment at
`full-logout.ts:4-30`, five guarantees. Coverage after this task, stated precisely, because two of
them are NOT OBSERVABLE under the current test architecture and pretending otherwise would be the
exact false confidence this item was raised about:

| #   | Guarantee                                          | Covered?                                                                                       |
| --- | -------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| 1   | Revoke access and refresh tokens (RFC 7009)        | Yes -- spy asserted called once                                                                |
| 2   | angular-oauth2-oidc clears its local token storage | **NOT OBSERVABLE.** `logOut()` is a test double; the real storage clearing is library-internal |
| 3   | Redirect to the discovered `end_session_endpoint`  | **NOT OBSERVABLE**, same reason                                                                |
| 4   | Expire `__tenant` and `XSRF-TOKEN` first           | Yes -- 5 new specs, real `document.cookie`                                                     |
| 5   | Never reject                                       | Yes -- the very assertion S2699 says does not exist                                            |

For 2 and 3 the honest ceiling is **asserting that we delegate**, not that the effect happened.
Do not let a future change quietly upgrade "we called `logOut()`" into "the token store was
cleared" -- observing that needs a real `OAuthService` against a fake storage, which is a different
and larger test architecture. Called from 8 sites.

**A SEPARATE DEFECT FOUND WHILE READING THIS, not fixed here.** In angular-oauth2-oidc 20.0.2,
`revokeTokenAndLogout()` returns early when local storage holds no access token:

```js
// fesm2022/angular-oauth2-oidc.mjs:2954, early return at :2958
const accessToken = this.getAccessToken();
if (!accessToken) {
  return Promise.resolve();
}
// :2642  getAccessToken() { return this._storage ? this._storage.getItem('access_token') : null; }
```

It RESOLVES rather than throwing, and `performFullLogout` catches only, so the `logOut()` fallback
never fires either. Guarantees 1, 2 and 3 all silently fail while the function reports success --
clicking Sign Out in that state expires the two cookies and does nothing else.

**Stated as a caveat, not a finding:** the mechanical behaviour above is certain and verified at the
library source. What is NOT established is the user-facing exposure. The end-session redirect does
not occur, so the server-side session is not explicitly terminated; whether it remains valid depends
on its own lifetime, which is **unmeasured**. Do not repeat "the SSO cookie survives" as fact -- that
is an inference from the redirect not happening, not an observation.

Queued as its own item 1.8, to be done immediately AFTER 1.3 and not folded into it: 1.3 preserves
behaviour, 1.8 changes it on an auth path, and the epic's decision rule separates those deliberately.
These cookie specs become 1.8's safety net, which is the whole reason for that ordering.

---

## NOT A DEFECT -- Sonar `python:S8392` (BLOCKER), phase 1.4

**Where:** `docker/packet-renderer/app.py:116`
**Claim:** "Avoid binding the application to all network interfaces."
**Verdict:** Not a defect. The renderer is unreachable from the host or the LAN in production, the
`0.0.0.0` bind is REQUIRED for it to work at all, and the flagged line is not even the production
bind. No code change. Changing it to loopback would break packet generation outright.

**First: the flagged line is not what production runs.** `app.py:114-117`:

```python
if __name__ == "__main__":
    # Local debugging only; the container runs gunicorn (see Dockerfile CMD).
    app.run(host="0.0.0.0", port=int(os.environ.get("PORT", "3001")))
```

It sits inside an `if __name__ == "__main__":` guard that the container never takes. The real bind is
`docker/packet-renderer/Dockerfile:63`:

```dockerfile
CMD ["gunicorn", "--bind", "0.0.0.0:3001", "--workers", "2", "--timeout", "120", "app:app"]
```

So even a "fix" at line 116 would change nothing about how the service actually listens. Sonar
flagged dev-only dead code.

**Second: nothing publishes the port.** Checked every compose file rather than only the one the phase
file named:

```bash
for f in docker-compose.yml docker-compose.prod.yml docker-compose.prod.localseed.yml; do
  awk '/^  packet-renderer:/{f=1} f&&/^  [a-z][a-z0-9-]*:$/&&!/packet-renderer/{f=0} f' "$f" \
    | grep -nE "ports:|expose:"
done
```

| File                                | Result                                                          |
| ----------------------------------- | --------------------------------------------------------------- |
| `docker-compose.prod.yml`           | **no `ports:`, no `expose:`** -- not reachable from host or LAN |
| `docker-compose.prod.localseed.yml` | none either (the override adds no port)                         |
| `docker-compose.yml` (dev)          | `- "127.0.0.1:${PACKET_RENDERER_PORT:-3001}:3001"`              |

The dev publish is bound to **127.0.0.1**, not `0.0.0.0`, so even in development it is loopback-only
and not exposed to the LAN. That is a deliberately careful binding, not an oversight.

**Third: no indirect exposure.** No reverse proxy routes to it --
`grep -rniE "packet.renderer|:3001" docker/nginx-proxy/ angular/nginx.conf` returns nothing. The only
way in is the compose network.

**Fourth, and the reason a "fix" would be actively harmful: the bind is necessary.** The API reaches
the renderer by compose service name, `docker-compose.prod.yml:264`:

```yaml
PacketRenderer__Url: "http://packet-renderer:3001"
```

Consumed by `WeasyPrintPacketRenderer` / `GenerateAppointmentPacketJob`. Binding to `127.0.0.1`
inside the container would make it unreachable from the api container and packet generation would
stop. This is exactly the trap the phase file warned about: "binding loopback in a container is a
classic way to break a working service in the name of a scanner."

**On the PHI concern.** It is true that this sidecar renders PHI-bearing PDFs, which is why the
question was worth asking rather than waving away. But the exposure premise does not hold: in
production the service has no host port at all, so there is no network path to it from outside the
compose network. The PHI stakes raise the cost of being wrong, and the answer was checked in four
independent places for that reason.

**Action:** mark **False Positive** in SonarCloud citing this entry. No code change. Optional tidy,
NOT required and not done here: delete the dead `if __name__ == "__main__":` block, which would also
silence the rule at source -- but it is genuinely useful for running the renderer standalone while
developing templates, so removing it costs a real convenience to satisfy a scanner. Left alone.

**THIS COMPLETES THE TRIAGE OF ALL SIX ORIGINAL BLOCKERS.** Final tally, counted per rule by the API:

```bash
for r in secrets:S7539 tssecurity:S6105 typescript:S2699 typescript:S6268 python:S8392; do
  curl -s ".../api/issues/search?componentKeys=...&rules=$r&severities=BLOCKER"
done
```

| Rule               | n   | Item | Verdict                                       |
| ------------------ | --- | ---- | --------------------------------------------- |
| `secrets:S7539`    | 2   | --   | false alarm (grep pattern, not a credential)  |
| `tssecurity:S6105` | 1   | 1.1  | false alarm (origin not caller-controlled)    |
| `typescript:S2699` | 1   | 1.3  | false alarm (rule blind to `expectAsync`)     |
| `python:S8392`     | 1   | 1.4  | false alarm (this entry)                      |
| `typescript:S6268` | 1   | 1.2  | **REAL** -- the only one, fixed in `ad4cb0d7` |

**FIVE of the six issues were false alarms** -- four distinct findings, since the two PowerShell hits
are one false positive twice. **Exactly ONE was a real defect**, and it was narrower than advertised
(stray text, not executable script). A mechanical sweep of this list would have changed working code
in four places, and in this case would have broken PDF generation.

That is the single most useful number in this epic for anyone deciding how much to trust a scanner's
headline severity count.

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
