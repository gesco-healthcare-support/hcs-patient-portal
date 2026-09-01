# Phase 1 -- Blockers

**Change class:** deliberate behaviour change. **Test written WITH the fix, not before.**
Assert the desired behaviour, watch it fail, then fix.

Sonar reports 6 BLOCKER issues. **All six are now triaged, and five of them are false alarms** --
four distinct findings, because the two PowerShell hits are the same false positive twice. **Exactly
one (1.2) was a real defect**, and it was narrower than advertised. See
[00-triage-log.md](00-triage-log.md). State of each, counted per rule by the API:

| Rule               | n   | Item | Verdict                                                               |
| ------------------ | --- | ---- | --------------------------------------------------------------------- |
| `secrets:S7539`    | 2   | --   | FALSE ALARM (grep pattern, not a credential); not yet marked          |
| `tssecurity:S6105` | 1   | 1.1  | FALSE ALARM -- marked RESOLVED/FALSE-POSITIVE                         |
| `typescript:S6268` | 1   | 1.2  | **REAL**, fixed in `ad4cb0d7`; issue stays open pending an Accept     |
| `typescript:S2699` | 1   | 1.3  | FALSE ALARM (rule does not recognise `expectAsync`); not yet marked   |
| `python:S8392`     | 1   | 1.4  | FALSE ALARM (unpublished port; flagged line is dead code); not marked |

**That ratio is the most transferable finding in this epic.** A mechanical sweep of this list would
have changed working code in FOUR places to satisfy a tool -- and in the packet-renderer case it
would have broken PDF generation, because the binding the rule objects to is the one that makes the
service reachable from its sibling container. The headline BLOCKER count overstates what is wrong
with this system by a wide margin, and the triage log is the only honest ledger of which items
mattered. All are small; this phase is hours, not days.

Order within the phase is by exploitability once the app is public.

---

## 1.1 Open redirect in the tenancy bootstrap

- **Rule:** `tssecurity:S6105`
- **Where:** `angular/src/tenant-bootstrap.ts:65`
- **Message:** "Change this code to not perform client-side redirection based on user-controlled data."

**Why this one is first.** Tenancy resolves from the HTTP Host header and nothing else, so this
file sits directly on the tenant-resolution path that every request traverses. A client-side
redirect built from user-controlled input is an open redirect: an attacker crafts a link on a
legitimate office subdomain that bounces the victim to a host they control. Against a portal where
users are conditioned to arrive via emailed links and then sign in, that is a credible phishing
primitive, and the victim sees a real `*.gesco.com`-shaped origin before the bounce.

**Research owed before fixing:** what exactly feeds the redirect target, whether it is reachable
pre-authentication, and whether the value is ever legitimately external. Expect the fix to be an
allowlist of known office hosts rather than sanitisation.

**Acceptance (EARS):** WHEN a redirect target resolves to a host outside the configured tenant host
set, THE SYSTEM SHALL refuse the redirect and route to the default landing page.

**Test:** Angular spec asserting a crafted external target is rejected and an in-tenant target is
honoured.

### OUTCOME: TRIAGED-NO-FIX (2026-08-31)

**The reported open redirect does not exist.** Ruled by Adrian: mark False Positive in SonarCloud,
no guard, no code change. Full evidence in [00-triage-log.md](00-triage-log.md).

In one line: the destination authority is the literal `admin` plus deployment config, and the only
caller-influenceable parts (`pathname`, `search`, `hash`) land strictly after the leading `/` that
the WHATWG URL Standard guarantees for any http(s) document, so they cannot relocate the origin.

**The EARS criterion above was NOT met as a built mechanism, deliberately.** Its antecedent is
unreachable through any caller-controlled input, so it is vacuously satisfied and there is no
explicit refusal branch. Building one would have been changing code to satisfy a scanner.

|                |                                                                                          |
| -------------- | ---------------------------------------------------------------------------------------- |
| Commits        | `450c9a7b` (triage + specs), `dd6d4c74` (S5906 fixup), `fd1f199c` (lint-line correction) |
| Sonar total    | 1280 -> 1280 (unchanged, correct for a no-fix outcome)                                   |
| Sonar BLOCKER  | 6 -> 6 (unchanged; actionable blockers are now 3, false-positive split 3 of 6)           |
| Frontend specs | 603 -> 619 (+16); `tenant-bootstrap.spec.ts` 4 -> 20                                     |

**What the work actually delivered**, since the triage produced no fix: the redirect path had zero
unit tests, which is why a scanner finding on it had no executable rebuttal. It now has 16,
including nine crafted-target cases asserting the destination origin cannot be moved. Their teeth
were checked by mutation -- reintroducing a caller-controlled authority fails 14 of 20 -- so the
dismissal is reproducible in seconds rather than re-derived from scratch by the next person.

**A testability seam was added** (`TenantBootstrapLocation`, an optional parameter defaulting to
`window.location`). Behaviour-identical; the composed target string is character-identical. It is
required rather than stylistic: `window.location.replace` cannot be spied on in Chrome, and calling
the real one navigates the karma runner out of its own suite.

---

## 1.2 Angular sanitization bypassed

- **Rule:** `typescript:S6268`
- **Where:** `angular/src/app/shared/ui/icon/icon.component.ts:58`
- **Message:** "Make sure disabling Angular built-in sanitization is safe here."

**Why it matters.** A `bypassSecurityTrust*` call disables Angular's XSS protection for that
binding. It is safe only if the input is provably developer-controlled. In a shared UI icon
component the input is likely a fixed icon name, which would make this defensible -- but "likely"
is not the bar for an XSS control on a PHI system.

**Research owed:** trace every call site of the component and determine whether the bypassed value
can ever originate from data (a field value, a config row, a query parameter) rather than a
literal in a template. Field configuration is admin-editable, which is a plausible path.

**Acceptance (EARS):** WHEN the icon component receives a name that is not a member of the known
icon set, THE SYSTEM SHALL render nothing rather than the supplied markup.

**Test:** component spec passing a script payload as the icon name and asserting it is not
rendered or executed.

### OUTCOME: FIXED (2026-09-01) -- partly real

Landed in `ad4cb0d7` (#502). Unlike 1.1 this was a real fix, not a dismissal.

**Verdict: partly real.** Of the three values interpolated into the trusted markup, one had a LIVE
path from server data and two did not.

| Input   | Live path?                                    | Finding                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------- | --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `name`  | **YES** -- server-sourced                     | `DashboardActivityItemDto.icon` (the ONLY icon field in the whole generated proxy tree) reaches it via an unchecked `as IconName` cast at `src/app/dashboard/internal-dashboard.component.ts:291`. The registry is a plain object literal, so a lookup on an inherited member (`constructor`, `toString`, `__proto__`) returned a function or object, which `?? ''` cannot catch. It rendered native-code text into the page. |
| `size`  | No -- all 45 call sites pass numeric literals | But the pre-fix test PROVED a string value closes the `width` attribute and injects a working `onload`. Demonstrated, never live.                                                                                                                                                                                                                                                                                             |
| `label` | No -- no dynamic binding exists anywhere      | Existing escaping is correct for a double-quoted attribute. Pinned, not changed.                                                                                                                                                                                                                                                                                                                                              |

**Practical consequence had it shipped untouched:** occasional stray internal text on the dashboard.
It could not execute code and could not leak data. Worth fixing, not an incident.

**WHY `size` WAS HARDENED THOUGH IT WAS NOT LIVE -- the distinction that keeps this epic coherent.**
It looks like a reversal of 1.1, where a guard was deliberately NOT added. It is not:

> **Structural guarantees do not need guards. Conventions do.**

On 1.1 the redirect origin is fixed by a STRUCTURAL property -- the WHATWG URL Standard guarantees a
special-scheme path serialises with a leading `/`, so no future caller can break it and a guard would
defend against something that cannot happen. On `size` the safety rested on CALLER DISCIPLINE across
45 sites, and the TypeScript `number` annotation is erased at runtime, so one `[size]="cfg.iconSize"`
commit makes it live. That is the test, not "did the scanner complain".

**FOLLOW-UP QUEUED -- remove the sanitizer bypass entirely.** The fix constrains every input but
keeps `bypassSecurityTrustHtml`, so `typescript:S6268` (`AZ76eJ5Af85aalgSOdg0`) stays open
project-wide. Removing it means restructuring the registry from raw SVG fragment strings into
structured data rendered by Angular template bindings, which deletes the bypass rather than securing
it. That is separate work, approved by Adrian as such, and deliberately NOT given a phase-1 section:
once the issue is accepted it is no longer a blocker, and inventing a slot would distort the phase.

If S6268 is ever actioned the transition is **Accept (won't fix)**, NOT False Positive -- the code
genuinely does disable sanitization, so dismissing it as a false positive would be a lie in the
record. It did not fire on #502: Sonar matched the edit to the pre-existing issue rather than raising
a new one, so the PR gate passed with 0 issues attributed.

---

## 1.3 Test with no assertion

- **Rule:** `typescript:S2699`
- **Where:** `angular/src/app/shared/auth/full-logout.spec.ts:47`
- **Message:** "Add at least one assertion to this test case."

**~~Why a BLOCKER is correct here.~~ THIS PREMISE WAS WRONG -- corrected 2026-09-01.** The original
text read: "The test passes unconditionally. It is worse than no test." It does not pass
unconditionally. The flagged `it(...)` opens at `full-logout.spec.ts:47` and asserts at `:55`:

```ts
await expectAsync(performFullLogout(injectorFor(oauth))).toBeResolved();
```

That fails the spec if the promise rejects, which is exactly the contract the test is named for --
`performFullLogout` is documented to never reject so a failed revocation still lands the user on the
login page. `typescript:S2699` looks for `expect(` and does not recognise `expectAsync`.

**Research owed** (answered): full logout guarantees five things, from the doc comment at
`full-logout.ts:4-30` -- (1) revoke access and refresh tokens, (2) let angular-oauth2-oidc clear its
local token storage, (3) redirect to the discovered `end_session_endpoint` to clear the AuthServer
SSO cookie, (4) expire `__tenant` and `XSRF-TOKEN` first, because the end-session flow does not
clear them and a stale `__tenant` leaks the prior user's tenant into a fresh registration on the
same browser, and (5) never reject. Called from 8 sites.

**Acceptance (EARS) -- REPLACED, widened by Adrian 2026-09-01.** Two criteria, both required:

1. WHEN the no-reject spec is run against a deliberately mutated implementation that rejects, THE
   SYSTEM SHALL fail that spec.
2. WHEN a full logout completes, THE SYSTEM SHALL have expired both the tenant cookie and the CSRF
   cookie, and the spec SHALL assert both.

The original criterion -- assert the token store was cleared and the end-session request issued --
was superseded because **it was already satisfied by doing nothing.** Those are library-internal
side effects of `logOut()`, the three pre-existing specs already assert the delegation through the
service double, and nothing in a double-based test can observe the real storage or redirect. Written
as it was, the task could have been closed without adding value.

**Note:** this one doubles as a phase-3 item -- it is the first real characterization test on an
auth path, and the first item in the epic that is NOT test-with-fix (behaviour is preserved, so
tests come first).

### OUTCOME: TRIAGED-NO-FIX plus new coverage (2026-09-01)

Landed in `fd875d67` (#504). The flagged issue was a false positive; the value delivered was the
coverage gap found beside it. No implementation line changed.

**S2699 is a false positive, proven by mutation.** A `throw` was added after the try/catch in
`performFullLogout`, breaking the documented never-reject contract:

```
performFullLogout never rejects even if both revocation and the fallback throw FAILED
    Expected a promise to be resolved but it was rejected with Error: mutation: performFullLogout now rejects.
TOTAL: 8 FAILED, 0 SUCCESS
```

The flagged spec failed on its own assertion, so the assertion exists and has teeth. Mutation
reverted; implementation `git diff` empty, residue grep 0. Full evidence in
[00-triage-log.md](00-triage-log.md). Issue `AZ-Au9J_eUr9gP0iFbsb`, still OPEN pending its marking.

**The real gap, which no scanner raised.** Nothing in the Angular test tree touched either cookie
sign-out clears -- `git grep -l "__tenant\|XSRF-TOKEN" e19c46ed -- 'angular/src/**/*.spec.ts' | wc -l`
returned 0 -- and `full-logout.ts:33` is the only place `__tenant` is cleared anywhere. Five
characterization specs now pin it, including the inverse case that the theme and culture cookies are
deliberately preserved. They passed on first run, which is the correct result for a
behaviour-preserving item: the clearing works and is now guarded.

**Coverage ceiling, stated so it is not quietly overclaimed later.** Of the five documented
guarantees, 1, 4 and 5 are asserted; **2 (local token storage cleared) and 3 (end-session redirect)
are NOT OBSERVABLE** with a test double. Asserting delegation is the honest ceiling. See the triage
log entry.

|                 |                                                                  |
| --------------- | ---------------------------------------------------------------- |
| Commit          | `fd875d67` (#504)                                                |
| Sonar BLOCKER   | 5 -> 5 (unchanged; correct, the issue awaits marking not fixing) |
| Frontend specs  | 630 -> 635 (+5); `full-logout.spec.ts` 3 -> 8                    |
| S2699 on the PR | did not fire -- 0 issues attributed, gate OK                     |

**Spawned item 1.8**, the silent sign-out dead end, deliberately NOT folded in here: 1.3 preserves
behaviour, 1.8 changes it on an auth path, and these cookie specs become 1.8's safety net. That
ordering is the point.

---

## 1.8 Sign-out can silently do nothing

**Placed here, out of numeric order, deliberately.** Sections in this file run in execution order,
not by number (see the ordering note at the top). 1.8 was discovered during 1.3 and is scheduled
immediately after it, because 1.3's cookie specs are its safety net. 1.4 through 1.7 follow.

- **Where:** `angular/src/app/shared/auth/full-logout.ts:41`, via
  `angular-oauth2-oidc@20.0.2` `revokeTokenAndLogout()`
- **Found:** while researching 1.3. No scanner raised it.

**The mechanism, verified at the library source.** `fesm2022/angular-oauth2-oidc.mjs:2954` takes an
early return at `:2958` when local storage holds no access token:

```js
const accessToken = this.getAccessToken(); // :2642 -> _storage.getItem('access_token')
if (!accessToken) {
  return Promise.resolve();
} // no revoke, no logOut(), no redirect
```

It RESOLVES rather than throwing, and `performFullLogout` catches only, so the `logOut()` fallback
never fires either. Clicking Sign Out in that state expires the two cookies and does nothing else.
Guarantees 1, 2 and 3 all fail silently while the function reports success. The trigger state is
ordinary -- an idle period that clears the stored token.

**What is NOT established.** Whether the AuthServer session remains usable afterwards. The
end-session redirect does not occur, so the server-side session is not explicitly terminated;
whether it stays valid depends on its own lifetime, which is **unmeasured**. Do not repeat "the SSO
cookie survives" as fact -- that is inference from the redirect not happening.

**Split into two tasks on Adrian's decision (2026-09-01):**

- **1.8a -- measure first.** Observation only, no code. Reproduce the no-token state on the DEV stack
  with synthetic accounts, sign out through the real UI, then check whether the app signs you back in
  without credentials. The question is whether the server re-issues a session, NOT whether the SPA
  looks logged out. "Could not determine" is a valid outcome.
- **1.8b -- the fix**, scoped only after 1.8a answers. Server session dead means an ordinary
  correctness fix; server session alive escalates beyond an engineering decision.

**Change class:** 1.8a none (observation). 1.8b deliberate behaviour change on an auth path, guarded
by 1.3's specs.

---

## 1.4 Packet renderer binds all interfaces

- **Rule:** `python:S8392`
- **Where:** `docker/packet-renderer/app.py:116`
- **Message:** "Avoid binding the application to all network interfaces."

**Likely not a defect.** Inside a container, binding `0.0.0.0` is the normal and usually necessary
choice, because binding loopback makes the service unreachable from sibling containers. The
question is whether the container port is published to the host or the LAN in production.

**Research owed:** check `docker-compose.prod.yml` for a published port on the packet renderer. If
it is unpublished and reachable only on the compose network, this is a triage-log entry, not a fix.
If it is published, it is a real exposure -- the packet renderer generates PHI-bearing PDFs.

**Do not change this one before checking.** Binding loopback in a container is a classic way to
break a working service in the name of a scanner.

### OUTCOME: TRIAGED-NO-FIX (2026-09-01)

**Not a defect, and a "fix" would have broken packet generation.** Full evidence in
[00-triage-log.md](00-triage-log.md). Four independent checks, all pointing the same way:

1. The flagged line is inside `if __name__ == "__main__":`, commented "Local debugging only", and is
   NOT the production bind. The container runs `gunicorn --bind 0.0.0.0:3001`
   (`docker/packet-renderer/Dockerfile:63`). Editing line 116 would change nothing at runtime.
2. `docker-compose.prod.yml` gives the service **no `ports:` and no `expose:`** -- no host or LAN
   path to it. The `prod.localseed` override adds none either.
3. Dev publishes it as `127.0.0.1:${PACKET_RENDERER_PORT}:3001` -- loopback-only even there.
4. No reverse proxy routes to it, and the API consumes it at `http://packet-renderer:3001`
   (`docker-compose.prod.yml:264`), the compose service name -- so `0.0.0.0` is REQUIRED for
   sibling-container reachability. Loopback would make packet generation fail.

The PHI concern was legitimate to raise -- this sidecar renders patient-bearing PDFs -- which is why
it was checked in four places rather than one. The exposure premise simply does not hold.

**Action:** mark False Positive in SonarCloud. No code change.

**This closes the triage of all six original BLOCKERs:** five of six issues were false alarms (four
distinct findings), and exactly one (1.2) was a real defect.

---

## Admitted from the system design research (2026-08-31)

Routed here by the rule in [09-system-design-intake.md](09-system-design-intake.md): both are
security fixes to files in this repository, and both are small. Evidence for each is in
[10-research-corrections.md](10-research-corrections.md).

### 1.5 No `default_server` on 443 - unmatched hosts land on the AuthServer

- **Where:** `docker/nginx-proxy/default.conf.template`
- **Confirmed at source.** `default_server` appears once, at `:27`, and it is on **port 80**. The
  four 443 blocks are `*.auth` (`:34`), `*.api` (`:59`), exact `minio.` (`:95`) and `*.<base>`
  (`:139`), so an unmatched Host on 443 falls through to the first of them.

**Why it matters once public.** Any hostname pointed at this address that does not match the office
scheme reaches the authorisation server rather than being refused. That is a free reconnaissance
surface and it makes host-based routing assumptions untestable from outside.

**Change class:** deliberate behaviour change - **test with the fix**. A catch-all 443 block
returning 444 or 421 is the usual shape. Verify each of the four legitimate host shapes still
resolves to its own backend afterwards.

**CORRECTION 2026-09-01 -- the risk originally stated here was wrong.** This section used to end
"a mis-ordered catch-all silently swallows real traffic". That is not how nginx behaves, and acting
on it would have sent someone hunting the wrong hazard.

nginx selects a server block by NAME SPECIFICITY, not file order: exact name, then longest
`*.`-prefixed wildcard, then longest suffix wildcard, then regex, and only when nothing matches does
it use the block flagged `default_server` for that address:port. `server_name _` is not special
syntax -- `_` is simply a name no real Host can equal. So a catch-all declared
`listen 443 ssl default_server;` matches ONLY unmatched hosts **wherever it sits in the file**.
File order decides the default only when NO block is flagged, which is precisely the bug above. The
template's own comment at `:14-18` already documents specificity resolving overlap.

**The two real risks, both of which fail LOUD rather than silently:**

1. Two blocks on the same address:port both carrying `default_server` -- nginx refuses to start.
2. A catch-all without `ssl_certificate` / `ssl_certificate_key` -- on 443 the TLS handshake
   completes before Host is read, so the catch-all serves the certificate for absent or unmatched
   SNI. Without them the handshake cannot complete.

Neither can silently misroute traffic, which makes this change materially safer than the original
note implied.

#### OUTCOME: FIXED (2026-09-01)

Landed in `14c3f84b` (#506). One additive server block -- `listen 443 ssl default_server;`,
`http2 on;`, `server_name _;`, the two `ssl_` directives, `return 421;`. No existing block edited.

**421 rather than 444**, decided by Adrian: HTTP/2 is on for every block and one certificate covers
several of these names, so a browser may coalesce connections across them. 421 is the RFC 9110
signal telling such a client to retry on a fresh connection; 444 would give it an uninterpretable
reset. 444 buys little concealment here anyway -- port 80 already answers every host, and the TLS
handshake announces the certificate's names before Host is ever read.

**Proved both directions**, in one throwaway nginx container with `proxy_pass` stubbed to
`return 200 '<block name>'` -- explicitly not the portal stack:

| Host                             | Before               | After               |
| -------------------------------- | -------------------- | ------------------- |
| `office.auth.portal.example.com` | auth block           | auth block (200)    |
| `office.api.portal.example.com`  | api block            | api block (200)     |
| `minio.portal.example.com`       | minio block          | minio block (200)   |
| `office.portal.example.com`      | angular block        | angular block (200) |
| `unmatched.example.net`          | **auth block (200)** | **421**             |

The last row confirms the finding empirically and proves the fix is load-bearing rather than a
no-op; the four above it prove the second acceptance criterion, that legitimate routing is unchanged.
The stub config was generated FROM the rendered template, keeping every `listen`, `server_name` and
`ssl_` line verbatim, so what was tested mirrors the real file rather than a hand-written copy.

**Limit of that proof, stated so it is not overclaimed later:** it establishes server-name SELECTION
only. `proxy_pass` was stubbed, so backend liveness was not exercised. Selection was the thing at
risk in this change; proving the backends respond needs the full stack.

**Port 80 deliberately unchanged**, and it is not a half-measure: an unmatched host there is 301'd to
https, reconnects on 443, and is refused by the new catch-all -- the goal is met one hop later -- and
port 80 never proxies to a backend, so nothing reaches an application either way. Backlogged with the
cost of doing it properly: no SNI on port 80, so refusing there needs name-matched 301 blocks for all
four legitimate shapes plus turning the existing default into a refusal, which is four new blocks
rather than one line.

### 1.6 DataProtection keys appear to be stored unencrypted at rest

- **Where:** `CaseEvaluationAuthServerModule.cs:384`, `CaseEvaluationHttpApiHostModule.cs:101` ->
  `:1103`
- **Partly confirmed.** Keys are persisted to Redis in both processes. **`ProtectKeysWith` does not
  appear anywhere in `src`**, and specifying a custom persistence location deregisters the default
  at-rest protection.

**Read `ConfigureDataProtection` at `CaseEvaluationHttpApiHostModule.cs:1103` before acting** - the
absence of the call is grep evidence, not a reading of the body, and this is exactly the class of
inference this epic is supposed to distrust.

**Why it is here rather than in a later phase.** These keys protect session cookies and
email-confirmation tokens. Loss makes already-protected payloads permanently undecipherable; theft
is a session-forgery primitive. **The ordering constraint in REQ-APP-01 applies:** if the key store
is ever moved out of Redis, that move must precede any cache eviction-policy change, or keys are
destroyed weeks before anyone notices.

---

## 1.7 No startup validation of `baseHost` from the runtime config merge

**Numbered 1.7, not 1.5.** Session A dispatched this as "1.5", but 1.5 and 1.6 were already taken by
the system-design admissions above by the time it was written. Same item, next free number.

**Where:** `angular/src/main.ts:23` (the merge) and `:38` (the read).

**Routed here by [09-system-design-intake.md](09-system-design-intake.md)** -- "startup validation of
required configuration" is in the ENTERS column, and the rule is to slot by nature, not by origin.
So it is a phase 1 security fix, filed with its neighbours.

**Justified as configuration validation on its own merits, NOT as a fix for S6105.** That issue is
closed as a false positive (see 1.1). Framing this as the security fix for it would be doing the
thing the triage gate forbids, one step removed.

**Found during 1.1.** Nothing validates any key after the runtime config merge:

```ts
const res = await fetch('dynamic-env.json', { cache: 'no-store' });
if (res.ok) { Object.assign(environment, await res.json()); }   // main.ts:23 -- no shape check
...
const baseHost = (environment as { baseHost?: string }).baseHost ?? 'localhost';   // main.ts:38
```

`baseHost` is then concatenated directly into a URL authority in `tenant-bootstrap.ts`. Two
failure modes, neither reachable by a web attacker -- the actor here is the OPERATOR:

- `BASE_DOMAIN=gesco.com@evil.com` composes `https://admin.gesco.com@evil.com/...`, where the
  intended host becomes userinfo and the real host is `evil.com`.
- A trailing slash composes `https://admin.evil.com//path`, silently changing the authority.

**The `??` edge (Session A's finding, not mine).** `main.ts:38` uses `??`, which falls back only on
`null`/`undefined`. A `"baseHost": ""` in `dynamic-env.json` therefore SURVIVES the merge as an
empty string rather than defaulting to `'localhost'`. `prod-dynamic-env.envsh` currently masks this
because `${APP_BASE_HOST:-localhost}` turns an empty env var into `localhost`, so the empty value
cannot originate there today -- but nothing in the SPA depends on that shell default holding.

**Why last in phase 1.** The phase is ordered by exploitability once public, and an
operator-misconfiguration path ranks below the three remaining real blockers.

**Change class:** deliberate behaviour change -- **test with the fix**. Validation that rejects
config which is currently accepted is a behaviour change by definition.

**Research owed:** whether validation belongs in `main.ts` before the merge, or in
`prod-dynamic-env.envsh` at generation time, or both; and what the failure mode should be -- a SPA
that refuses to boot on bad config is safe but opaque, so decide deliberately rather than by
default. Check whether any other merged key (the oAuthConfig URLs) deserves the same treatment
before choosing a shape.

**Acceptance (EARS):** WHEN the runtime config supplies a `baseHost` that is not a syntactically
valid host, THE SYSTEM SHALL refuse to use it and fail fast with a diagnosable message rather than
composing a URL from it.

---

## Validation loop for this phase

Angular-only changes for 1.1-1.3, Python for 1.4. Per `~/.claude/rules/testing.md`:

```
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
npx ng lint
```

Set `CHROME_BIN` first on Windows. Scope the spec run with `--include` while iterating, but run it
unscoped before the phase is called done -- template changes break specs that pin selectors.

**Corrected 2026-08-31 (task 1.1).** The third line previously read
`npx eslint --ext .html,.ts src/app`, which does NOT cover `src/tenant-bootstrap.ts` -- that file
sits outside `src/app`. `angular.json`'s lint target uses
`lintFilePatterns ["src/**/*.ts", "src/**/*.html"]`, so `npx ng lint` is the command that actually
lints the files this phase changes.

**Windows caveat, found while running it.** `npx ng lint` (and `yarn lint`, which is the same
thing) exits 1 on a local Windows worktree with "Invalid lint configuration. Nothing to lint.
Please check your lint target pattern(s)." This is environment-specific, NOT a repo defect: CI's
`Frontend: Lint` job runs the identical `yarn lint` with no `continue-on-error` and passes, with the
same `@angular-eslint/builder` 20.0.0 that `yarn.lock` pins. The difference is ubuntu + Node 22 in
CI versus Windows + Node 24 locally; the likely cause is the `lintFilePatterns` glob not resolving
on Windows. Until that is fixed (logged to `docs/backlog.md` for phase 2), lint locally with
`npx eslint <the files you changed>` and let CI run the real gate.

If 1.4 turns out to need a change, add a container smoke test that the renderer still answers from
a sibling service.
