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

### OUTCOME: 1.8b CLIENT HALF FIXED AND PROVEN LIVE; SERVER HALF REFUTED (2026-09-01)

**The client fix works and is the only part that shipped.** PR #512. The server-side companion was
built, passed every automated check, and was then **refuted by the live test**. It has been removed
from the PR and preserved on `research/endsession-session-revoke` rather than deleted.

**What shipped.** `performFullLogout` now checks `getAccessToken()` first: revoke when a token is
present, otherwise call `logOut()` directly. `revokeTokenAndLogout()` early-returns a RESOLVED
promise on an empty token store (`angular-oauth2-oidc@20.0.2` fesm2022 `:2958`), so the old `catch`
never fired and no end-session request was ever sent. Also removed the chained `navigateToLogin()` at
the 401 call site, and repointed `patient-profile.component.ts:182` off `AuthService.logout()`.

**THE LIVE TRIGGER, which is the most useful thing this item produced.**
`app-http-error.component.ts:115` is the 401 session-timeout call to action. A 401 reaching that
screen means the stored token is gone -- exactly the state in which sign-out did nothing -- and it
then navigated to the AuthServer with the SSO cookie still live, signing the user straight back in.
That is 1.8a's observation with a line number.

**LIVE RE-TEST (2026-09-01, dev stack, tenant `falkinstein`).** Three scenarios, because mirroring
1.8a exactly would have proved nothing: removing ONLY the access token leaves the `id_token` in
place, so `id_token_hint` is still sent and ABP's pre-existing handler does the revocation. The
production state has no id_token either -- ABP's `clearOAuthStorage` removes all three together.

| Scenario                                    | Result                                                                           |
| ------------------------------------------- | -------------------------------------------------------------------------------- |
| A control -- normal sign-out, token present | Sign-in page PASS; new handler correctly declined PASS; **row NOT removed**      |
| C production-faithful -- all 3 tokens null  | Sign-in page PASS; fresh navigation bounced to sign-in PASS; **row NOT revoked** |

Scenario C precondition verified before signing out, as the procedure requires:
`access_token: null | id_token: null | refresh_token: null`.

**WHY THE SERVER HALF FAILED, pinned rather than guessed.** The handler read the session id from
`HttpContext.User`, on the reasoning that authentication middleware populates it at request start and
`SignInManager.SignOutAsync()` only expires the cookie in the RESPONSE. That reasoning is WRONG. The
AuthServer log shows the handler RAN ("...successfully processed by
`RevokeSessionWithoutTokenHintHandler`") while emitting neither of its own log lines. It has two
silent returns -- hint-present, and principal-not-authenticated -- but ABP logged "No SessionId was
found in the token", so no hint was sent and the first was not taken. Among the paths reachable in
that scenario, the unauthenticated-principal return is the only silent exit. So `HttpContext.User`
is not authenticated by the time the end-session handler runs.

**Recorded because a successor will otherwise try it again:** reading the session id from
`HttpContext.User` inside an OpenIddict end-session handler DOES NOT WORK here. A different source is
needed.

**EVERY AUTOMATED SIGNAL PASSED while the handler did nothing in production** -- 11 frontend specs,
2294 backend tests, build, format, lint, and four unit tests on the handler with a mutation proof
behind them. The unit tests substituted `IHttpContextAccessor` with an authenticated principal, so
they asserted the logic given a premise that is false at runtime. This is the case for insisting a
live test is the acceptance criterion on anything the unit layer cannot observe.

**A SEPARATE FINDING FROM THE CONTROL, and it may matter more -- see 1.8d.** ABP's own handler logged
`Revoking the SessionId(42a0615d-...)` three times and the row REMAINED. The Identity-cookie session
was removed by a different path logging "...during sign out". Six `TaskCanceledException`s appear in
the same window on `RelationalConnection.OpenAsync`. The new handler declined on that path, so it
cannot have prevented ABP's delete -- but whether this is a genuine ABP race or an artefact of
automation speed is NOT established. If real, the session record is inaccurate on the NORMAL sign-out
path too, which is the premise the whole server-side change rested on.

**Corroborates 1.8c, observed rather than derived:** the three session rows from the original 1.8a
run (19:57, 20:00, 20:01) were still in `AbpSessions` hours later, untouched.

### 1.8d Does normal sign-out actually revoke the session record? -- OPEN

Spun out of 1.8b's control scenario above. **Research plus a short live run, no code yet.**

Establish whether ABP's with-hint revocation genuinely fails to remove the `AbpSessions` row, or
whether the browser navigation cancelled the request before it committed. Drive
`/connect/endsession` directly rather than through a UI click, so nothing can cancel it mid-flight.

**Why it blocks the server-side work:** if the normal path already leaves stale rows, fixing only the
no-hint path does not produce accurate session records, and rebuilding the server half first would
rest on a premise that may be false.

#### RESEARCH: MECHANISM ESTABLISHED FROM SOURCE + LOGS (2026-09-01). LIVE CONFIRMATION STILL OWED.

**It is REAL, not an artefact of automation speed, and it affects every ordinary sign-out.** Stated
as the leading conclusion with the evidence below; a live run is still required to close it, because
everything here is source and log reading.

**THE MECHANISM.** `IdentitySessionManager.RevokeAsync(string)` (decompiled,
`Volo.Abp.Identity.Pro.Domain` 10.0.2) does:

```
IdentitySessionRepository.FindAsync(sessionId, default(CancellationToken))
  -> RevokeAsync(IdentitySession session)
       -> DeleteAsync(session.Id, autoSave: false, default(CancellationToken))
```

**`autoSave: false`.** The delete is staged in the change tracker and is persisted only when an
ambient ABP unit of work COMPLETES. `RevokeAsync` neither saves nor opens its own unit of work.

**WHY ONE PATH WORKS AND THE OTHER DOES NOT** -- the contrast is the whole answer:

| Path                                                                                        | Runs in                                             | Row removed? |
| ------------------------------------------------------------------------------------------- | --------------------------------------------------- | ------------ |
| Cookie-auth sign-out event (`AbpAccountPublicWebModule`, the "...during sign out" log line) | inside `LogoutController.GetAsync`, an MVC endpoint | **YES**      |
| `OpenIddictRevokeIdentitySessionOnRevocation` / `...OnLogout`                               | OpenIddict server middleware                        | **NO**       |

The AuthServer log proves the ordering: the `HandleEndSessionRequestContext` handlers complete at
`23:13:57.106-.118`, and `Executing endpoint 'Volo.Abp.OpenIddict.Controllers.LogoutController.GetAsync'`
is at `23:13:57.249`. `app.UseUnitOfWork()` is registered at `CaseEvaluationAuthServerModule.cs:583`,
downstream of `UseAuthentication()` (`:575`). The OpenIddict handlers therefore run with no ambient
unit of work to commit their staged delete; the MVC endpoint runs inside one.

**CANCELLATION IS RULED OUT, twice over.** Both cancellation tokens in that call chain are
`default(CancellationToken)`, not the request's. And the two `POST /connect/revocat` requests each
**finished with HTTP 200** -- nothing was cancelled -- yet the row survived. The six
`TaskCanceledException`s in the window all carry `OpenIddictServerHandlers+Authentication+ValidateScopes`
or `+ValidateAuthorizationRequest` stacks: they belong to the SPA's follow-up `/connect/authorize`,
not to the revocation or end-session path. They were a red herring.

**WHY THREE LOG LINES -- not retries.** `revokeTokenAndLogout()` POSTs to the revocation endpoint
TWICE (once `token_type_hint=access_token`, once `refresh_token`), each firing
`OnRevocation`, and the subsequent end-session fires `OnLogout`. Confirmed in the log as three
separate requests at `:56.954`, `:57.030` and `:57.108`. The log line is emitted BEFORE the await, so
it records intent, not success.

**No matching upstream issue found** searching `abpframework/abp` for IdentitySession revoke/logout.
That is a null result from one query, not proof that none exists.

**CONSEQUENCE FOR TASK 6, and it is the important one.** The refuted handler was registered in the
SAME OpenIddict middleware position. Even with the `HttpContext.User` problem solved, its
`RevokeAsync` would stage a delete that never commits. **The approach is dead in that position**, not
merely mis-sourced. Any future fix must either run inside a unit of work or save explicitly.

**LIVE PROCEDURE THAT WOULD CONFIRM OR REFUTE** (not yet run; needs stack capacity):

1. Sign in and capture the `session_id` claim from the id_token, plus the auth cookie jar.
2. `curl` `GET /connect/endsession?id_token_hint=<token>` with that cookie jar, no redirect
   following, allowed to complete fully -- **no browser navigation that could cancel anything**.
3. Query `AbpSessions` for that session id.
   - **Still present** -> confirms the defect is real and cancellation-independent.
   - **Gone** -> the original observation was an automation artefact and this entry is wrong.
4. Discriminating control: repeat with a fresh session but sign out via `/Account/Logout` (the Razor
   MVC endpoint) instead. Expect the row to be REMOVED, which would confirm the unit-of-work
   explanation rather than a fault in `IdentitySessionManager` itself.

### 1.8a DURATION: ANSWERED FROM SOURCE (2026-09-01)

**The AuthServer SSO session lasts 14 days, and the expiry SLIDES.** Read from source for the pinned
versions, not estimated, and not measured by idling a stack.

**Step 1: nothing in this repository sets a cookie lifetime.** Reproduce with:

```bash
grep -rnE "ExpireTimeSpan|SlidingExpiration|ConfigureApplicationCookie|ValidationInterval|SecurityStampValidator" --include="*.cs" --include="*.json" --include="*.cshtml" .
```

Zero hits outside `docs/`. The only auth-lifetime call anywhere in the codebase is
`serverBuilder.SetAccessTokenLifetime(TimeSpan.FromMinutes(15))` at
`src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:162`, which is an
OpenIddict ACCESS TOKEN, not the SSO cookie. So the cookie runs on framework defaults.

**Step 2: the three-link chain, each link read at source for the pinned version.**

| Link                                                               | Source                                                                                                     | What it does                                                                                                                                          |
| ------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| ABP 10.0.2 `AbpIdentityAspNetCoreModule`                           | `abpframework/abp@10.0.2`, `modules/identity/.../AbpIdentityAspNetCoreModule.cs`                           | `.AddAuthentication(...).AddIdentityCookies()`. Sets NO lifetime; only swaps in `AbpSecurityStampValidator` and adds an `OnRefreshingPrincipal` hook. |
| ASP.NET Core 10 `AddIdentityCookies()` -> `AddApplicationCookie()` | `dotnet/aspnetcore@release/10.0`, `src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs`              | `AddCookie(IdentityConstants.ApplicationScheme, o => { o.LoginPath; o.Events })`. No `ExpireTimeSpan`, no `SlidingExpiration`.                        |
| `CookieAuthenticationOptions` constructor                          | `dotnet/aspnetcore@release/10.0`, `src/Security/Authentication/Cookies/src/CookieAuthenticationOptions.cs` | `ExpireTimeSpan = TimeSpan.FromDays(14); SlidingExpiration = true;`                                                                                   |

**Step 3: no ABP package overrides it. Verified exhaustively, not assumed.** All 728 ABP 10.0.2
assemblies in the local NuGet cache were scanned for the IL member references `set_ExpireTimeSpan`,
`set_SlidingExpiration` and `set_ValidationInterval`:

| Symbol                   | Assemblies referencing it                                                                                                         |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| `set_ExpireTimeSpan`     | exactly ONE: `Volo.Abp.Account.Pro.Public.Web`                                                                                    |
| `set_SlidingExpiration`  | only caching/feature/permission/setting/text-template DOMAIN assemblies (that is `DistributedCacheEntryOptions`, not cookie auth) |
| `set_ValidationInterval` | NONE                                                                                                                              |

The single hit was decompiled (`ilspycmd`). It is three call sites, all
`options.ExpireTimeSpan = TimeSpan.FromMinutes(5)`, and all on bespoke short-lived schemes, never the
SSO cookie: `ConfirmUserModel.ConfirmUserScheme`, `ChangePasswordModel.ChangePasswordScheme`,
`LockedOut.LockedUserScheme`. The module's one `ConfigureApplicationCookie` block sets no lifetime.

**SLIDING IS THE PART THAT MATTERS, more than the number.** `SlidingExpiration = true` means the
handler re-issues the cookie with a fresh 14-day window on any request that arrives more than halfway
through the current one. A session that keeps being used therefore never closes on its own; 14 days
is the idle ceiling, not the maximum age. On a shared workstation the exposure ends when someone
stops using the machine for a fortnight, which in a clinic is never.

**Four things that do NOT bound this, checked so they are not offered later as mitigations:**

| Candidate                                 | Why it does not bound the session                                                                                                                                                                                                     |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The 15-minute access token                | A live SSO cookie mints a fresh one at `/connect/authorize` without credentials. That is the finding, not a limit on it.                                                                                                              |
| The 30-minute security-stamp revalidation | It ends the session only if the stamp CHANGES (password change, `UpdateSecurityStampAsync`). An ordinary signed-in user's stamp does not change. Default confirmed at 30 min in the ASP.NET Core 10 API reference; ABP never sets it. |
| Closing the browser                       | Only if `IsPersistent` is false, which is the "Remember me" checkbox at `Pages/Account/Login.cshtml:81`. The 14-day ticket still governs server-side either way, and a shared workstation's browser is not closed.                    |
| The refresh token                         | OpenIddict 7.2.0 default is also 14 days, and it is not overridden here.                                                                                                                                                              |

**OpenIddict 7.2.0 lifetimes, for context** (`openiddict-core@7.2.0`, `OpenIddictServerOptions.cs`):
access token 1 hour (**overridden to 15 min here**), refresh token 14 days (not overridden), identity
token 20 minutes, authorization code 5 minutes, refresh-token reuse leeway 30 seconds (**overridden to
2 s here**).

**What is still NOT established, stated so it is not overclaimed.** No live `Set-Cookie` or decrypted
ticket was read, because that needs the stack. The chain above is a source-level derivation of the
configured value. It could only be wrong if something outside the 728 scanned ABP assemblies and
outside this repository post-configured `CookieAuthenticationOptions` for
`IdentityConstants.ApplicationScheme`, and no such component is registered.

### 1.8c COULD WE TELL IF IT ALREADY HAPPENED? -- UNDETERMINABLE (2026-09-01)

Research only. Nothing was connected to, queried against, or read from the deployed system; every
statement below comes from this repository, the ABP 10.0.2 packages and first-party sources.

**VERDICT: UNDETERMINABLE. No action follows from this beyond recording why.** Four of the five places
you would look cannot show it at all, and the fifth cannot be used to find affected sessions.

**The conclusion, stated rather than left as a caveat.** `AbpSessions` does retain the residue of a
session that was never cleanly ended -- but rows are removed ONLY on a clean sign-out, on token
revocation, or by the 30-day sweep. Every user who closes a browser without clicking Sign Out leaves
one behind. The false-positive population is therefore _most sessions_, so a lingering row cannot
identify who was affected. It is unusable as a detector, not merely partial.

**The direction matters, and this is a precision rather than a hedge.** You cannot go FROM the session
table TO a list of affected users. If a specific user, time and workstation were already known from
somewhere else, the row would corroborate. Nothing in this system provides that starting point,
because nothing records that Sign Out was pressed.

| Source            | Could it show a failed sign-out?                                                                                                                                       |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AbpSecurityLogs` | **NO.** No logout path in this system writes a `Logout` row (below).                                                                                                   |
| `AbpAuditLogs`    | **NO.** `AbpAuditingOptions.IsEnabledForGetRequests` defaults to `false` and is not overridden here; `/connect/endsession` is `[HttpGet]`, so it is never audited.     |
| `AbpSessions`     | **YES, partially.** The row survives an uncleaned sign-out. Retained until 30 days of inactivity.                                                                      |
| nginx access log  | **WEAK.** `nginx:alpine` writes to stdout; no log volume is mounted (`docker-compose.prod.yml` reverse-proxy block). Captured only by the json-file ring buffer below. |
| Serilog file sink | **NO.** `File("Logs/logs.txt")` with no rotation and no volume mount on the authserver service, so it lives in the container layer and is lost on every redeploy.      |

**Container log retention is a size ring buffer, not a time window.** `docker-compose.prod.yml:21-25`
sets `json-file` with `max-size: 10m`, `max-file: 5` for every service -- 50 MB per container, then
the oldest is discarded. There is no time guarantee at all, so nginx and console output cannot be
relied on to cover any particular past date.

**Why `AbpSecurityLogs` is dead as a signal, which is the counter-intuitive part.** ABP writes a
`Logout` security-log row from its Razor logout page
(`abp@10.0.2 modules/account/.../Pages/Account/Logout.cshtml.cs`), and NOT from the OpenIddict
end-session controller (`modules/openiddict/.../Controllers/EndSessionController.cs`, whose whole body
is `SignInManager.SignOutAsync()` then `SignOut(...)`). The SPA signs out through
`/connect/endsession`. This repo's own `/Account/Logout` override is a bare `AbpPageModel` that does
not write one either. So **no logout path in this system produces a `Logout` row**, and "a Login with
no matching Logout" is the normal pattern for every user. Searching for it would return everyone.

**Why `AbpSessions` works.** The feature is active here because it rides on Dynamic Claims, which are
enabled in both hosts (`CaseEvaluationAuthServerModule.cs:431`, `CaseEvaluationHttpApiHostModule.cs:1084`,
with `app.UseDynamicClaims()` in each).

- Rows are created on the OpenIddict sign-in path by `OpenIddictCreateIdentitySession`
  (`Volo.Abp.Account.Pro.Public.Web.OpenIddict` 10.0.2, decompiled), calling
  `IdentitySessionManager.CreateAsync(...)`.
- Columns are `SessionId, Device, DeviceInfo, TenantId, UserId, ClientId, IpAddresses, SignedIn, LastAccessed`.
- Rows are removed on logout by `OpenIddictRevokeIdentitySessionOnLogout` -- **but only when an
  `id_token_hint` is supplied** (see the 1.8b consequence below).
- Retention: `IdentitySessionCleanupBackgroundWorker` with ABP defaults, none overridden in this repo
  -- cleanup runs hourly and removes rows inactive for **30 days**. Background WORKERS are enabled;
  the `IsJobExecutionEnabled = false` at `CaseEvaluationAuthServerModule.cs:360` is background JOBS,
  a different subsystem.

**What it can and cannot tell you, stated precisely so nobody over-reads it:**

- CAN show, for a session already identified from elsewhere, that it was never cleanly ended, with
  `LastAccessed` marking when it stopped being used and `IpAddresses` / `DeviceInfo` narrowing it to a
  workstation. Corroboration, not discovery.
- CANNOT distinguish "pressed Sign Out and it silently failed" from "closed the browser and walked
  away". Nothing anywhere records that the button was pressed, so the two are identical in the data
  -- and since the second is the ordinary way people leave, the lingering rows are overwhelmingly
  those. That is what makes the signal unusable rather than merely noisy.
- Only covers the last 30 days of inactivity. Anything older has already been deleted by the worker.

**CONSEQUENCE FOR 1.8b, found here rather than in 1.8b's own research.**
`OpenIddictRevokeIdentitySessionOnLogout` derives the session id from `IdentityTokenHintPrincipal`
only, and OpenIddict's `AttachPrincipal` for end-session propagates nothing else -- it does not attach
the cookie-authenticated principal. The planned 1.8b fix drives end-session with no tokens at all,
because in the broken state there are none. Therefore:

- the SSO cookie IS killed -- ABP's controller calls `SignInManager.SignOutAsync()` unconditionally,
  which is the security-critical half and the whole point of the fix; but
- the `AbpSessions` row is NOT revoked, and the AuthServer logs
  `"No SessionId was found in the token during HandleLogoutRequestContext."`

So the fix as scoped leaves a stale session row behind. That row is inert for re-authentication once
the cookie is gone, and the cleanup worker removes it after 30 days, but it keeps appearing in the
admin session list. Raised as an open decision for 1.8b rather than settled here.

One useful side effect: that warning line is a positive marker. After 1.8b ships, its presence in the
AuthServer log distinguishes a hint-less sign-out -- which is exactly the repaired path -- from a
normal one.

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

### 1.6 DataProtection keys unencrypted at rest -- MOVED OUT OF THIS EPIC (2026-09-01)

**No longer a phase 1 item.** Adrian's decision, 2026-09-01: this becomes its own piece of work on
`main`, designed separately rather than fixed here.

**The finding is REAL** and was confirmed by reading both `ConfigureDataProtection` bodies, not by
grep: neither process configures any key protection, and Microsoft's documentation states that
persisting keys to an explicit location deregisters the platform's default at-rest encryption.
Mitigating context: production Redis publishes no port, so the keys are unreachable from outside the
container network.

**Why it left the epic rather than being fixed in it.** The code change is two lines. Everything
behind it is not: on Linux containers the only available mechanisms need an X.509 certificate or
Azure Key Vault distributed to both containers, which raises who holds the certificate, how it is
backed up, and -- the part Adrian specifically wants to design -- what the override is when it is
lost, rotated or compromised. Encrypting the keys converts a confidentiality gap into an
availability dependency, and a lost certificate would make every protected payload permanently
unreadable. That is a design exercise, not a hardening task.

**Everything gathered is written up in
[`docs/security/SESSION-KEY-ENCRYPTION.md`](../security/SESSION-KEY-ENCRYPTION.md)** -- the method
bodies, the documentation quotations, the Redis posture, the options with what each costs here, the
existing-sessions analysis with its confidence label, the override problem, and the open design
questions. Read that rather than re-deriving any of it.

**REQ-APP-01 travels with it** and is repeated there: if the key store is ever moved out of Redis,
that move must precede any cache eviction-policy change, or the keys are destroyed weeks before
anyone notices.

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

**Acceptance (EARS) -- REPLACED and WIDENED, Adrian 2026-09-01.** The original criterion covered
`baseHost` alone; the scope grew to every setting the file carries. Superseding criterion:

> WHEN the runtime configuration merged from `dynamic-env.json` contains any value that is not valid
> for its setting -- a host carrying a scheme, path, credentials or whitespace; a service URL that is
> not an absolute http(s) URL, or that is not https while `production` is true; a boolean field
> carrying a string; or a required text field that is empty -- THE SYSTEM SHALL start, SHALL name
> every offending setting in both a visible on-page message and the browser console, and SHALL NOT
> silently substitute a default.

The final clause is what makes it real: today's only response to a bad `baseHost` is the
`?? 'localhost'` fallback, which the criterion forbids.

**The four decisions, with their reasons:**

| Decision                                              | Reason                                                                                                                                                                                                                                                  |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Validate in BOTH the app and `prod-dynamic-env.envsh` | Four producers write this file and only one emits `baseHost`. The app is the only place that sees every route including a hand-edited file; the script is the only place that can stop a bad deploy before it serves anyone.                            |
| Cover EVERY setting, not just origin-forming ones     | The producers already disagree on types -- `"production": true` in two, `"production": "true"` in the Helm configmap -- and nothing validates any of it.                                                                                                |
| Require https only when `production` is true          | Uses the file's own flag as the switch, so dev keeps working on http while a production deployment cannot be pointed at a plaintext auth server. Always-https would break every dev stack and need an exemption, which is this rule wearing a disguise. |
| Start and warn visibly; never silently substitute     | On a self-hosted LAN deployment the person who sees a blank page is not the person who can read a container log. Falling back does not avoid an outage, it disguises one.                                                                               |

**The script behaves differently from the app, deliberately.** The script FAILS THE CONTAINER START;
the app STARTS AND WARNS. Same principle -- make it visible to whoever is present -- different
audience: the script's reader is the operator running the deploy, and nothing is being served yet.
The script guard cannot fire on a currently-valid deployment, because `${APP_BASE_HOST:-localhost}`
already substitutes on empty, so only malformed values reach it.

**Decided without escalation, recorded so they are not re-litigated:** absent keys are not errors
(three producers omit `baseHost`; the baked environment covers them); URLs must be absolute (a
relative value cannot be an origin); `apis` is iterated rather than hardcoded to its two known names,
because a rule needing manual extension will be wrong within a year; `scope` is checked non-empty
only and deliberately NOT for `openid`, since the Helm producer omits it and encoding that rule would
flag an existing producer from inside a validation change -- that question is backlogged separately;
`logoUrl` may be empty because the production producer emits `""` on purpose.

**Considered and rejected:** requiring the service URLs to share `baseHost`. It would catch a config
pointing at an entirely wrong domain, but the services legitimately live on different subdomains and
a future split-domain deployment would break against it. Brittleness that only appears on a later
deployment is the worst kind, because nobody connects the failure to the rule.

### OUTCOME: FIXED (2026-09-01)

PR [#509](https://github.com/gesco-healthcare-support/hcs-patient-portal/pull/509), squash-merged as
`dc134222`. Five files, +516/-3.

| File                                    | Change                                                                   |
| --------------------------------------- | ------------------------------------------------------------------------ |
| `angular/src/config-validation.ts`      | new; `validateRuntimeConfig(env): string[]`, pure and Angular-free       |
| `angular/src/config-validation.spec.ts` | new; 29 specs                                                            |
| `angular/src/main.ts`                   | validates after the merge; `[config]` console errors plus a fixed banner |
| `angular/prod-dynamic-env.envsh`        | fails the container start on a malformed `APP_BASE_HOST`                 |
| this file                               | the widened criterion and the four decisions                             |

**Validation loop, all green.** eslint exit 0, prettier clean, scoped specs `TOTAL: 29 SUCCESS`,
`npx ng build` exit 0, full frontend suite `TOTAL: 664 SUCCESS` (635 -> 664, no regressions). CI on
the PR: 23 checks pass, 0 failing.

**The shell guard proven both ways**, outside the Angular suite because no spec can reach it:

| `APP_BASE_HOST`              | Result |
| ---------------------------- | ------ |
| `portal.example.com`         | exit 0 |
| `localhost`                  | exit 0 |
| `intranet`                   | exit 0 |
| `https://portal.example.com` | exit 1 |
| `portal.example.com/`        | exit 1 |
| `a@b.com`                    | exit 1 |
| `has space`                  | exit 1 |

**Test-first order was NOT followed, and the evidence was recovered rather than claimed.** The task
was marked `tdd`; the spec and the implementation were written together, so there was no genuine red
step. A spec written alongside its implementation can encode that implementation's mistakes and
still go green, so the red step was reconstructed by mutation: `read()` was changed to always return
undefined (the pre-implementation behaviour) and the suite re-run.

```
TOTAL: 14 FAILED, 15 SUCCESS
```

The 14 failures are exactly the rejection cases; the 15 survivors are the acceptance cases, which
correctly pass because absent means valid. Mutation reverted, residue grep returns 0. **Standing
rule from this:** when the red step is skipped, run the mutation as a matter of course, not as an
apology.

**The regression the specs guard against is an over-strict host rule, not a permissive one.**
`intranet` (single label), `example.com.` (trailing dot) and `a-b.c-d.example` are all asserted VALID
so the rule cannot tighten by accident and break a real deployment.

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
