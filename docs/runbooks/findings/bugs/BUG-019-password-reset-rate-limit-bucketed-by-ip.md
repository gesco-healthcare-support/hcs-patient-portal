---
id: BUG-019
title: Password-reset rate limiter on /api/public/external-account/* falls through to per-IP partition when body-only email is supplied; one user can DoS reset for everyone behind same IP
severity: high
status: fixed
fixed: 2026-05-22 (dual-partition limiter; per-account primary + per-IP secondary)
found: 2026-05-21 hardening HRD-P9.2 (rate-limit observed during P9.2.a-e attempts)
flow: password-reset-rate-limiting
component: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs (ConfigurePasswordResetRateLimiter + ResolvePasswordResetEmailPartitionKey + ResolvePasswordResetIpPartitionKey) + src/HealthcareSupport.CaseEvaluation.HttpApi.Host/RateLimiting/PasswordResetEmailPeekMiddleware.cs
---

# BUG-019 - Password-reset rate limit partitioned per-IP for anonymous SPA traffic

> 2026-05-24: renamed from `BUG-035-password-reset-rate-limit-bucketed-by-ip.md` to free `BUG-035` for the lockout-never-fires finding that main concurrently filed during the hardening run (`BUG-035-lockout-never-fires.md`, ultimately reclassified as not-a-bug).

## Symptom

Phase 9.2 probing of `POST /api/public/external-account/send-password-reset-code` and `POST /api/public/external-account/reset-password` triggered HTTP 429 (Too Many Requests) after a small number of calls in the same hardening session. All calls came from the Playwright browser session at `http://falkinstein.localhost:44327` from a single client IP.

Sequence of calls in this session (all anonymous, all with email in the JSON request body, no `?email=` query string):

| # | Endpoint | Email (body) | Status | Notes |
|---|---|---|---|---|
| 1 | `POST /send-password-reset-code` | `patient1@gesco.com` | 204 | Initial trigger; Hangfire job 78 created with the reset URL |
| 2 | `POST /reset-password` (no `confirmPassword`) | n/a (body validation) | 400 ValidationError | Counted against the bucket |
| 3 | `POST /reset-password` (reuse) | n/a (same payload) | 400 ValidationError | Counted against the bucket |
| 4 | `POST /reset-password` (tampered token) | n/a | 400 ValidationError | Counted against the bucket |
| 5 | `POST /send-password-reset-code` | `patient1@gesco.com` (anti-enum real) | 204 | OK |
| 6 | `POST /send-password-reset-code` | `nobody-9p2e@gesco.com` (anti-enum fake) | **429** | Bucket exhausted |
| 7-9 | `POST /reset-password` (with `confirmPassword`) | n/a | **429** | All subsequent calls 429 |

After only 5 successful calls (anti-enum real being the 5th), call #6 to a DIFFERENT (non-existent) email returned 429. Calls 7-9 to `/reset-password` (different endpoint, same prefix) also returned 429.

Confirmation that the limiter uses a shared partition for ANY email when no `?email=` query string is present:

- Call #5 was for `patient1@gesco.com` -> 204.
- Call #6 was for `nobody-9p2e@gesco.com` (a different, non-existent email) -> 429.

The two emails are unrelated, but both went into the same 5-call bucket because the partition key fell through to `ip:<remoteAddress>`.

## Logs

`PartitionLogic` cited from source (file `CaseEvaluationHttpApiHostModule.cs`, lines 532-547):

```csharp
internal static string ResolvePasswordResetPartitionKey(Microsoft.AspNetCore.Http.HttpContext httpContext)
{
    var fromQuery = httpContext.Request.Query["email"].ToString();
    if (!string.IsNullOrWhiteSpace(fromQuery))
    {
        return $"email:{fromQuery.Trim().ToLowerInvariant()}";
    }
    var sub = httpContext.User?.FindFirst("sub")?.Value;
    if (!string.IsNullOrWhiteSpace(sub))
    {
        return $"sub:{sub}";
    }
    var ip = httpContext.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrWhiteSpace(ip))
    {
        return $"ip:{ip}";
```

`RateLimiter config` from the same file, lines 357-366:

```csharp
return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
    partitionKey: $"pwd-reset:{key}",
    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
    {
        PermitLimit = 5,
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0,
        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });
```

`IsPasswordResetPath` (lines 432-435) matches ANY request whose path starts with `/api/public/external-account` — so both `/send-password-reset-code` and `/reset-password` (and any future sibling under that prefix) share the same bucket. The 4 sub-endpoints currently under that prefix:

- `POST /api/public/external-account/send-password-reset-code`
- `POST /api/public/external-account/reset-password`
- `POST /api/public/external-account/resend-email-verification`
- (any others added under the same prefix)

All four sub-endpoints contribute calls to the same 5-call-per-hour-per-IP bucket.

Code comment on lines 320-327 states the body-field-partitioning omission is intentional:

> "Window: 1 hour. Permit: 5. Queue: 0 (over-limit returns 429
>  immediately rather than queueing). Partition key precedence:
>  optional `email` query-string override -> AuthN `sub` claim ->
>  client IP. Body-field partitioning is intentionally NOT..."

(Comment cut off in my read; the omitted reasoning is not in the source.)

`Caller`: The SPA, per browser DevTools observation, calls `POST /api/public/external-account/send-password-reset-code` with body `{"email": "<typed-email>", "appName": "CaseEvaluation"}` and NO query string. So under typical end-user usage from the SPA, the partition falls through to `ip:<remoteAddress>`.

`Status code returned`: HTTP 429 with empty body, content-length 0. No `Retry-After` header observed.

`How long until reset`: The first triggering call this session was at 2026-05-21 18:42:21 UTC. `FixedWindowRateLimiter` with `Window = 1 hour` + `AutoReplenishment = true` -> the bucket replenishes 1 hour after the window started. Estimated unlock: ~2026-05-21 19:42 UTC.

## Reproduction

1. From any client, anonymously POST 5 calls to `http://falkinstein.localhost:44327/api/public/external-account/send-password-reset-code` (body `{"email":"<any-email>","appName":"CaseEvaluation"}`) within 1 hour. The first 5 return 204 (regardless of whether the emails are the same or different).

2. The 6th call (same OR different email, same anonymous session, same client IP) returns 429 within ~20ms with empty body.

3. The 6th call could be for an account that has never been reset before in this hour. It still returns 429.

4. Wait 1 hour. The bucket replenishes. Calls resume.

Concrete test script (was used to reproduce in this session):

```javascript
const url = 'http://falkinstein.localhost:44327/api/public/external-account/send-password-reset-code';
for (let i = 0; i < 6; i++) {
  const r = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `user${i}@example.test`, appName: 'CaseEvaluation' })
  });
  console.log(`#${i + 1} (${'user' + i + '@example.test'}): ${r.status}`);
}
// Output: #1..#5: 204, #6: 429
```

Note: 6 different emails -> still 6th returns 429, because partition is IP-based, not email-based.

## Functional impact

1. **Shared-IP DoS**: a single household, office, school, NAT, VPN, or CGNAT IP can have its password-reset fully blocked for an hour by ANY ONE user (or attacker) burning 5 calls. All other legitimate users behind that IP see 429 until the window replenishes.

2. **Compounds with [[BUG-029]] + [[OBS-25]]**: the host-swap workaround the suite has been using for invite/register URLs leaves users dependent on the resend flow. The resend flow (`/api/public/external-account/resend-email-verification`) shares the same `/api/public/external-account` prefix and thus the same 5-call IP bucket as password-reset. So one user resending verification emails subtracts from another user's password-reset budget for the same IP.

3. **Soft enumeration oracle**: a 429 vs 204 difference lets an attacker probe whether ANY recent password-reset traffic happened from a given IP. In Phase 9.2.e of this run, the real-user call returned 204 and the fake-user call returned 429 — the only differentiator was that the fake-user call came LATER, exhausting the IP-bucket. An off-path attacker can probe their own IP's bucket state to infer recent reset activity. Mild.

4. **Test friction (immediate impact in this session)**: one hardening run that exercises Phase 9.2.a-f exhausts the 5-call budget within ~30 seconds. Subsequent Phase 9.2 sub-scenarios (b, c, d, e, f) all return 429 and cannot be cleanly verified until the next hour. Workaround during dev: pass `?email=<unique>` query string OR call from an authenticated context to flip the partition off-IP. Both workarounds are NOT what end users do, so they don't test the actual code path.

## Anti-checks observed

- The 429 response has NO `Retry-After` header. Caller cannot programmatically determine when to retry without out-of-band knowledge of the 1-hour window.
- The 429 response has empty body. Caller cannot tell from the response shape that it is the IP-shared bucket vs. a per-email bucket (no diagnostic to disambiguate).
- The custom limiter at `IsPasswordResetPath` (line 432) compares prefix `/api/public/external-account` -- but does NOT subdivide by sub-endpoint, so all sibling endpoints under that prefix (including `resend-email-verification`) share one bucket per partition key. A future endpoint added under the same prefix automatically inherits the same shared bucket.

## Related

- [[BUG-029]] -- registration URL tenant subdomain missing. Together, BUG-029 + this finding mean that an invited user whose first email-confirmation link is broken (BUG-029) cannot trivially get a working second email if other users from the same IP have recently triggered any reset / resend / signup flow.
- [[OBS-25]] -- invite acceptance doesn't auto-confirm. Compounded by the same IP-bucket issue for the post-accept verification email.
- [[BUG-018]] -- earlier finding on SMTP misleading error. Different surface, not directly related.
- Suite Phase 9.2.f (HARDENING-TEST-SUITE.md line 778) -- explicitly probes the 5/hour throttle. This finding confirms the throttle fires but reveals the partition key choice is not what the suite assumed.
- Code comment in the limiter source (line 322) says the partition is intentional and references "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit".

## Fix verified (2026-05-22)

Implemented the OWASP-aligned dual-partition design from the Forgot
Password Cheat Sheet:

- **Primary (per-account)**: `FixedWindow 5 / 1h` partitioned by the
  `email` field of the JSON body. The
  `PasswordResetEmailPeekMiddleware` runs immediately before
  `UseRateLimiter()`, enables request-body buffering, parses the
  body (capped at 4 KB to bound memory cost), extracts and
  lowercases the `email`, stashes it in
  `HttpContext.Items["pwd-reset.email"]`, then rewinds the stream so
  MVC's model binding still sees the body. The partitioner's email
  resolver then consults that stash first (fallback chain unchanged:
  `?email=` query -> JWT `sub` -> client IP -> `"global"`).

- **Secondary (per-IP)**: `FixedWindow 50 / 1h` partitioned purely
  by client IP. Generous threshold so NAT/CGNAT/office/hotspot
  populations don't block each other under normal usage, but a
  single IP can't fan out across thousands of unique emails to
  carpet-bomb the system.

- The two limiters are composed with
  `PartitionedRateLimiter.CreateChained` so a request must pass
  BOTH partitions; either alone returning 429 short-circuits.

- `OnRejected` hook emits a `Retry-After` header
  (`MetadataName.RetryAfter` from the lease metadata), addressing
  the prior anti-check that "the 429 response has NO `Retry-After`
  header". Callers can now compute their next-retry time without
  out-of-band knowledge of the window.

### Live verification matrix

Three tests against the running stack, all from one client IP. Each
table row is the response of the indexed call.

**Test A -- shared-IP DoS gone (6 DISTINCT emails)**:

| # | Email | Status |
|---|---|---|
| 1 | testA-1@example.test | 204 |
| 2 | testA-2@example.test | 204 |
| 3 | testA-3@example.test | 204 |
| 4 | testA-4@example.test | 204 |
| 5 | testA-5@example.test | 204 |
| 6 | testA-6@example.test | **204** |

All 6 succeed. The pre-fix shared-IP DoS where call #6 returned 429
is gone.

**Test B -- per-account control (SAME email 6 times)**:

| # | Status | Retry-After |
|---|---|---|
| 1 | 204 | -- |
| 2 | 204 | -- |
| 3 | 204 | -- |
| 4 | 204 | -- |
| 5 | 204 | -- |
| 6 | **429** | **3600** |

Per-email bucket hits its 5/hour cap on call 6 and rejects with the
expected `Retry-After: 3600` (one hour) header.

**Test C -- per-IP secondary cap still in place**:

Burst of 60 distinct emails from the same IP after Tests A + B (which
already consumed 11 of the IP's 50/hour budget). Result: 39 calls
returned 204, then 21 returned 429 starting at call #40 (11 + 39 =
50 = the per-IP cap). The cap fires exactly when expected.

### Code shape

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/RateLimiting/PasswordResetEmailPeekMiddleware.cs`
  (new, ~120 lines). Body peek + stash. Silent no-op on malformed
  JSON / body too large / non-JSON content type / missing field.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`:
  - Renamed `ResolvePasswordResetPartitionKey` to
    `ResolvePasswordResetEmailPartitionKey` and extended it to
    consult the `HttpContext.Items` stash.
  - Added `ResolvePasswordResetIpPartitionKey` (pure-IP).
  - Rewrote `ConfigurePasswordResetRateLimiter` to use
    `PartitionedRateLimiter.CreateChained` with the dual-partition
    scheme.
  - Added `OnRejected` handler for `Retry-After`.
  - Wired `UseMiddleware<PasswordResetEmailPeekMiddleware>()`
    immediately before `UseRateLimiter()` in
    `OnApplicationInitialization`.

### Unit tests

9 new tests in
`test/HealthcareSupport.CaseEvaluation.Application.Tests/HttpApiHost/CaseEvaluationHttpApiHostModuleTests.cs`
covering both partition-resolver helpers:

- Email resolver prefers stash > query > JWT sub > IP > "global".
- Email resolver lowercases + trims query input.
- Email resolver falls through empty/whitespace stash values.
- IP resolver returns `ip:<addr>` or `"global"`.
- IP resolver ignores body stash and JWT sub (proves it's a clean
  secondary, not a duplicate of the primary).

Full Application.Tests run after the change: **559/559 pass**
(550 pre-change + 9 new BUG-035 tests).

### Out of scope (captured here for follow-up)

- **Sub-dividing the path matcher** so `/send-password-reset-code`,
  `/reset-password`, and `/resend-email-verification` each get their
  own per-email bucket. Currently they share the prefix bucket --
  intentional for the per-IP cap (you don't want abuse on one
  endpoint freeing budget on the sibling), but a follow-up could
  scope per-email separately if needed. Capture as a future
  hardening item.
- **Sliding window** instead of fixed window for smoother
  replenishment. Not needed for password-reset cadence.
