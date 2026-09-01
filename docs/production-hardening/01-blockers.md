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
