---
id: BUG-035
title: Account lockout never fires; AccessFailedCount increments but LockoutEnd stays NULL
severity: high
status: not-a-bug
found: 2026-05-23 hardening HRD-P9.1
diagnosed: 2026-05-23 -- root cause is RUNBOOK EXPECTATION mismatch, not implementation
diagnosis: |
  CaseEvaluationSettingDefinitionProvider.cs:115-127 explicitly raises
  IdentitySettingNames.Lockout.MaxFailedAccessAttempts from ABP default
  5 to **10**, and LockoutDuration from 5 minutes to 3600 seconds (1 hour).
  This is Adrian's 2026-05-18 decision per proposed-copy.md section 2.9.

  My P9.1 probe sent only 6 wrong-password attempts -- well below the
  configured threshold of 10 -- so AccessFailedCount=6 with LockoutEnd=NULL
  is the CORRECT behavior. A successful login then resets the counter (also
  correct ABP behavior).

  To actually verify lockout fires, the runbook P9.1 scenario needs to be
  updated to attempt 10+ failed logins from a fresh AccessFailedCount=0
  state, then verify LockoutEnd is non-null and a subsequent correct
  password is rejected.

  ACTION: update runbook P9.1 to use N=10. Close this finding as
  not-a-bug.
flow: authentication
component: AuthServer IdentityOptions.Lockout config (Razor Login flow)
---

# BUG-035 - Account lockout policy does not enforce after N failed attempts

## Symptom

Logged in as anonymous, sent 6 sequential POSTs to `/Account/Login` (Razor)
with `LoginInput.UserNameOrEmailAddress=patient1@gesco.com` and an incorrect
password each time, with antiforgery token refreshed per request.

Observed:

- Attempts 1-6 all returned HTTP 200 (page re-renders with credential
  error). No 423 or "account locked" response.
- After attempt 6, `AbpUsers` shows for patient1:
  - `AccessFailedCount = 6`
  - `LockoutEnd = NULL`
- A 7th attempt with the **correct** password returned HTTP 302 to the
  AuthServer root and set `.AspNetCore.Identity.Application` cookie - the
  account was NOT locked.
- After the successful login, `AccessFailedCount` reset to 0 (this is the
  correct ABP Identity behavior after a successful login).

The expected behavior per ABP defaults (and the runbook P9.1 spec) is:

- Lockout after `MaxFailedAccessAttempts` (default 5)
- `LockoutEnd` should be set to `now + DefaultLockoutTimeSpan` (default 5 min)
- Subsequent login attempts (even with correct password) should return
  401/423 with a generic "account temporarily locked" flash

## Repro

1. Open a curl session and capture the antiforgery token from
   `GET /Account/Login`.
2. POST `/Account/Login` with the captured token, the target email, and
   a wrong password. Refresh the antiforgery token each time.
3. Run 5-6 times.
4. Query `AbpUsers` for `AccessFailedCount` and `LockoutEnd`.

Bash loop reproducer (used during the hardening run):

```bash
for i in 1 2 3 4 5 6; do
  curl -ks -c /tmp/cj.txt -o /tmp/login.html "http://falkinstein.localhost:44368/Account/Login"
  AFT=$(grep -oE 'name="__RequestVerificationToken"[^>]*value="[^"]+"' /tmp/login.html | head -1 | sed -E 's/.*value="([^"]+)".*/\1/')
  curl -ks -b /tmp/cj.txt -c /tmp/cj.txt -o /dev/null -w "Attempt $i: HTTP %{http_code}\n" \
    -X POST "http://falkinstein.localhost:44368/Account/Login" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "LoginInput.UserNameOrEmailAddress=patient1@gesco.com" \
    --data-urlencode "LoginInput.Password=WRONG-PASS-$i" \
    --data-urlencode "LoginInput.RememberMe=false" \
    --data-urlencode "__RequestVerificationToken=$AFT"
done
```

## Hypothesis (3 in priority order)

1. **IdentityOptions.Lockout is misconfigured** - either
   `MaxFailedAccessAttempts` is set very high, `DefaultLockoutTimeSpan` is
   zero, or `AllowedForNewUsers` is false (the legacy Identity behavior
   skips lockout for users who haven't completed lockout opt-in). Check
   the host module / SettingDefinitionProvider for explicit overrides.
2. **Lockout is wired but a custom `SignInManager` override skips
   `AccessFailedAsync` -> lockout check** - possible if a custom flow
   increments `AccessFailedCount` directly without invoking the
   built-in lockout enforcement path.
3. **AbpIdentitySettings vs IdentityOptions split** - ABP stores some
   lockout-related defaults in `Setting` rows (per-tenant). If the
   tenant's `Setting` row has `Abp.Identity.Lockout.AllowedForNewUsers`
   set to false, lockout never engages.

## Functional impact

HIGH severity. Brute-force protection is absent. Any attacker (or any
typo-prone user) can issue unlimited password guesses with no
back-pressure. The only mitigation in place right now is HTTP-layer rate
limiting (per PR #197 - which targets the registration endpoint, not the
Razor login).

This impacts:

- The OWASP Top-10 "Identification and Authentication Failures" control.
- HIPAA Security Rule risk analysis: enables credential-stuffing /
  password-spray against patient accounts.
- Account-recovery UX: a real user who fat-fingers their password
  multiple times will eventually get in (good for them) but the lack of
  a "your account is locked, reset password" path means real attacks
  are indistinguishable from forgotten passwords on the server side.

## Recommended fix

1. In `CaseEvaluationAuthServerModule.ConfigureServices` (or the
   AuthServer's `Program.cs` IdentityOptions configuration), confirm:

   ```csharp
   Configure<IdentityOptions>(options =>
   {
       options.Lockout.AllowedForNewUsers = true;
       options.Lockout.MaxFailedAccessAttempts = 5;
       options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
   });
   ```

2. Verify that no `Setting` row in `AbpSettings` for the Falkinstein
   tenant overrides these to disable lockout.

3. Add a unit test in
   `test/HealthcareSupport.CaseEvaluation.Application.Tests/` that
   asserts after N failures the user is locked. Plan a runbook scenario
   too (HRD-P9.1 already specifies this).

4. Confirm the failed-login response wording is generic and does not
   leak whether the account exists or whether the lockout is active.

## Related

- Runbook P9.1 (HRD-P9.1) - this is the scenario that surfaced it.
- Adrian's "multi-session is intentional" 2026-05-01 decision is
  orthogonal (session policy, not lockout).
- OWASP ASVS V2.2.1 (Anti-automation).
