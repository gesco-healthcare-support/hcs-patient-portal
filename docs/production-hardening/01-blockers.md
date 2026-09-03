# Phase 1 -- Blockers

**Change class:** deliberate behaviour change. **Test written WITH the fix, not before.**
Assert the desired behaviour, watch it fail, then fix.

Sonar reports 6 BLOCKER issues. Two are false positives (see
[00-triage-log.md](00-triage-log.md)). The 4 below are real. All are small; this phase is hours,
not days.

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

---

## 1.3 Test with no assertion

- **Rule:** `typescript:S2699`
- **Where:** `angular/src/app/shared/auth/full-logout.spec.ts:47`
- **Message:** "Add at least one assertion to this test case."

**Why a BLOCKER is correct here.** The test passes unconditionally. It is worse than no test: it
reports coverage on the full-logout path and gives false confidence on a security-relevant flow.
This is precisely the failure mode `~/.claude/rules/testing.md` warns about -- a substituted
dependency returning a plausible value proves the code compiles, not that it works.

**Research owed:** what full-logout is meant to guarantee (token revocation, cookie clearing,
end-session redirect) and which of those the spec was intended to cover.

**Acceptance (EARS):** WHEN a full logout completes, THE SYSTEM SHALL have cleared the local token
store and issued the end-session request, and the spec SHALL assert both.

**Note:** this one doubles as a phase-3 item -- it is the first real characterization test on an
auth path.

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
resolves to its own backend afterwards; a mis-ordered catch-all silently swallows real traffic.

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

```bash
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
