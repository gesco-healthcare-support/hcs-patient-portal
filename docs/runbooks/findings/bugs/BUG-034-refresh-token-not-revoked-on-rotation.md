---
id: BUG-034
title: OAuth refresh_token NOT revoked on rotation; replay attack possible
severity: high
status: fixed
fixed: 2026-05-22 (live-verified replay window shrunk from 30s default to 2s)
last-replayed: 2026-05-23 INITIALLY-misdiagnosed-as-regressed; ACTUALLY-working-as-designed -- my retest fired both refresh requests within ~1 second of rotation, well inside the 2-second RefreshTokenReuseLeeway window per CaseEvaluationAuthServerModule.cs:147-150. The leeway is the intentional fix vs OpenIddict's 30-second default. A correct retest requires waiting 3+ seconds between rotation and old-token reuse to verify the OLD token gets rejected.
found: 2026-05-21 hardening HRD-P9.3
flow: oauth-refresh-token
component: src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs (OpenIddict configuration)
---

# BUG-034 - Refresh token replay vulnerability (rotation without revocation)

## Symptom

OAuth refresh-token rotation test against `http://falkinstein.localhost:44368/connect/token`:

1. Capture `refresh_token_A` from a freshly-logged-in session (clistaff1).
2. Exchange `refresh_token_A` for new tokens via `grant_type=refresh_token`:
   - Status 200, body contains `refresh_token_B` where `B != A` (rotation HAPPENS).
3. **REUSE** `refresh_token_A` (the OLD token, supposedly invalidated by rotation):
   - **Status 200** -- the old token is accepted, returning yet another fresh token pair (let's call it `refresh_token_C`).
4. Use `refresh_token_B` (the rotation output from step 2): status 200, returns yet another fresh token.

Expected per OAuth best practice (RFC 6749 Section 10.4 + RFC 8252):
- Step 3 MUST return `400 invalid_grant` (old token revoked on rotation).
- Step 4 MUST return 200 (new token still valid).

Actual: step 3 returns 200. The old token is not revoked.

## Hypothesis

1. **OpenIddict misconfigured: `DisableRollingRefreshTokens()` enabled.** OpenIddict's rolling-refresh-token behavior (where rotation invalidates the old token) is opt-in via `AddServer(server => server.UseRollingRefreshTokens())`. If the module's bootstrap calls `DisableRollingRefreshTokens()` -- or just doesn't enable it -- the old token stays valid until its expiration.

2. **Token storage doesn't mark old token as redeemed.** The OpenIddict server validates an incoming refresh_token by looking up its row in `OpenIddictTokens` and checking `Status = "valid"`. On rotation, the row should be transitioned to `redeemed`. If the transition is missing, replays succeed.

3. **Reference-token issuance with cache-only revocation.** OpenIddict can issue reference tokens (server-side lookup) or self-contained tokens (signed payloads). If self-contained + DataProtection-encrypted, revocation requires a deny-list. If the deny-list is missing, the rotation is cosmetic.

Most likely (1): the OpenIddict bootstrap in `CaseEvaluationAuthServerModule` either explicitly disables rolling refresh tokens, or the version of `Volo.Abp.OpenIddict.Pro` in use has a default that diverges from upstream. Need to verify.

## Reproduction

```javascript
// Run in browser console after a fresh login at http://falkinstein.localhost:4200/
const oldRefresh = localStorage.getItem('refresh_token');

// Step 2: rotate
const r1 = await fetch('http://falkinstein.localhost:44368/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({
    grant_type: 'refresh_token', refresh_token: oldRefresh,
    client_id: 'CaseEvaluation_App',
    scope: 'offline_access openid profile email phone CaseEvaluation'
  }).toString()
});
const newRefresh = (await r1.json()).refresh_token;
console.log('rotated:', newRefresh !== oldRefresh); // true

// Step 3: REUSE old refresh token
const r2 = await fetch('http://falkinstein.localhost:44368/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({
    grant_type: 'refresh_token', refresh_token: oldRefresh,  // OLD token
    client_id: 'CaseEvaluation_App',
    scope: 'offline_access openid profile email phone CaseEvaluation'
  }).toString()
});
console.log('reuse status:', r2.status); // expected 400, actual 200
```

## Recommended fix

Step 1: Locate OpenIddict server configuration:
```bash
grep -rn "AddServer\|UseRollingRefreshTokens\|DisableRollingRefreshTokens\|OpenIddictServerBuilder" \
  src/HealthcareSupport.CaseEvaluation.AuthServer/
```

Step 2: Ensure the server explicitly enables rolling refresh tokens:
```csharp
builder.AddServer(options =>
{
    // ...existing config...
    options.UseRollingRefreshTokens();           // each rotation invalidates the prior token
    options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
});
```

Step 3: Confirm the `OpenIddictTokens` table is being written. After a rotation, run:
```sql
SELECT Id, ReferenceId, Status, ExpirationDate, RedemptionDate
FROM OpenIddictTokens
WHERE Type = 'refresh_token'
ORDER BY CreationDate DESC
```
The redeemed (old) token's `Status` should be `redeemed` (not `valid`), and `RedemptionDate` should be set.

Step 4: Add integration test in `test/HealthcareSupport.CaseEvaluation.AuthServer.Tests/`:
```csharp
[Fact]
public async Task Refresh_token_should_be_invalidated_after_rotation()
{
    var firstRefresh = await LoginAsync(...);
    var rotated = await RefreshAsync(firstRefresh);
    Assert.NotEqual(firstRefresh, rotated.RefreshToken);
    var reuseAttempt = await RefreshAsync(firstRefresh, expectFailure: true);
    reuseAttempt.Status.ShouldBe(HttpStatusCode.BadRequest);
    reuseAttempt.Error.ShouldBe("invalid_grant");
}
```

## Functional impact

**HIGH SEVERITY -- security vulnerability.**

- An attacker who steals a refresh_token (via XSS, log scraping, malware on user device, etc.) can use it indefinitely until expiration, even after the legitimate user has refreshed and obtained a new token. Both attacker and user can hold valid sessions simultaneously, undetected.
- Defeats the primary purpose of rolling refresh tokens (which is to detect token theft when the legitimate user's next refresh fails, signaling stolen-token use).
- HIPAA-adjacent: any session with PHI access (e.g., patient1 viewing their own appointment) has tokens that, if leaked once, allow indefinite access.
- All current sessions (Phase 5 approvals, Phase 8 scope checks, Phase 6 packet generation) used refresh tokens that are silently still valid even after the user "logged out" if the browser cleared localStorage without explicitly revoking server-side.

## Related

- [[BUG-014]] / [[BUG-029]] -- email URL tenant handling (unrelated but in same security-adjacent area).
- Suite Phase 9.3 (HARDENING-TEST-SUITE.md line 786) -- this finding is explicitly anticipated by the test design.

## Corrected root cause (2026-05-22)

The original hypothesis above ("rolling refresh tokens not enabled")
was wrong. Rolling refresh tokens have been the default in OpenIddict
since the 3.0-beta6 release; the opt-out is
`DisableRollingRefreshTokens()`, which the AuthServer module never
called. The SQL probe of `OpenIddictTokens` confirms rotation IS
running -- redeemed rows are stamped with `RedemptionDate`.

The actual mechanism is
`OpenIddictServerOptions.RefreshTokenReuseLeeway`. Source:
`openiddict-core/src/OpenIddict.Server/OpenIddictServerOptions.cs`:

```csharp
public TimeSpan? RefreshTokenReuseLeeway { get; set; } = TimeSpan.FromSeconds(30);
```

`OpenIddictServerHandlers.Protection.cs` checks this leeway before
rejecting a replay of a redeemed refresh token (see `IsReusableAsync`
helper around line 1361). Within the leeway window, OpenIddict
tolerates the replay -- the deliberate ergonomic deviation noted in
the inline comment at line 1244 ("Special logic is used to avoid
revoking refresh tokens already marked as redeemed to allow for a
small leeway"). The default 30-second window also gave a stolen
refresh token a 30-second free-replay window.

## Fix verified (2026-05-22)

Fix lives at
`CaseEvaluationAuthServerModule.PreConfigureServices`:

```csharp
PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
{
    serverBuilder.SetRefreshTokenReuseLeeway(TimeSpan.FromSeconds(2));
});
```

Two seconds preserves the legitimate concurrent-retry path (network
blips during refresh round-trips) while shrinking the stolen-token
free-replay window from 30 s to 2 s.

### Live verification matrix

Two tests run sequentially against `replicate-old-app` AuthServer
(`http://falkinstein.localhost:44398/connect/token`) on 2026-05-22:

| Test | Scenario | Expected | Actual |
|---|---|---|---|
| A | Login (clistaff1) -> rotate `A`->`B` -> sleep 3 s -> replay `A` | 400 invalid_grant ID2012 | **400 invalid_grant** "The specified refresh token has already been redeemed." ID2012 |
| B | Login (patient1) -> rotate `C`->`D` -> immediate replay (0.66 s) of `C` | 200 + new pair (legitimate concurrent-retry path preserved) | **200** + fresh token pair |

SQL probe of `OpenIddictTokens` post-tests:

```
Status     Cnt
redeemed    2
valid       3
```

Two redeemed rows (Test A's `A`, Test B's `C`) confirm rotation
bookkeeping. Three valid rows (Test A's `B`, Test B's `D`, and the
post-replay-within-leeway token from Test B).

### SPA smoke test

Single Falkinstein SPA login + one round-trip via Chrome DevTools MCP
(scope confirmed with Adrian):

- Navigate `http://falkinstein.localhost:4230/`
- Login as `admin@falkinstein.test`
- Land on `/dashboard`
- Click "Appointments" nav link -> land on `/appointments`
- All XHR/fetch calls returned 200 across the round-trip:
  `/connect/token`, `/api/abp/application-configuration`,
  `/api/abp/application-localization`, `/api/account/profile-picture/`,
  `/api/app/appointments/pending-count`, `/api/app/dashboard`,
  `/api/app/appointments/identity-user-lookup`,
  `/api/app/appointments/appointment-type-lookup`,
  `/api/app/appointments/location-lookup`,
  `/api/app/appointments?skipCount=0&maxResultCount=10`.
- Zero `400 invalid_grant` in the network panel.
- Zero console errors or warnings.

### Followups (out of scope for this close-out)

- Automated integration test that runs the three-step replay against
  a hosted OpenIddict instance. Blocked on the project's missing
  AuthServer integration-test harness (the EF.Tests project has a
  pre-existing Phase 4 license-checker test-host crash that prevents
  spinning one up). Capture as a future hardening item.
- Reduce access-token lifetime. Separate defense-in-depth item.
- Sender-constrained tokens (DPoP / mTLS, RFC 8705 / RFC 9449). Major
  architectural change; not in scope.
