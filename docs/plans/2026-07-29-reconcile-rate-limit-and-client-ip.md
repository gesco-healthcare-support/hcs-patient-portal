---
status: complete
date: 2026-07-29
---

# Reconcile rate limit + real client IP

## Goal

Close two of the three risks left open when the Case Tracker epic went code-complete:

1. The reconcile GET (`/api/integration/...`) is anonymous and unthrottled while returning claim
   numbers, injury dates, body parts, employer/insurer details and attorney contacts. A leaked
   token permits unbounded enumeration.
2. Every "per-IP" rate-limit partition in the API collapses to a single global bucket in
   production, so the controls that exist do not do what they claim.

Third risk (reconcile hostname) is docs-only and folded in here.

TLS to the Case Tracker is explicitly OUT of scope: it reduces to one question for their team
(http vs https, who signed the cert, do they require a client cert). No code until they answer.

## Context

- `ConfigureForwardedHeaders` (`CaseEvaluationHttpApiHostModule.cs:463`) enables
  `ForwardedHeaders.XForwardedProto` only. `XForwardedFor` is NOT processed.
- nginx forwards `X-Forwarded-For` via `$proxy_add_x_forwarded_for`
  (`docker/nginx-proxy/default.conf.template`), and the `api` service publishes no ports
  (`docker-compose.prod.yml:231`), so nginx is its sole ingress.
- Therefore `Connection.RemoteIpAddress` is the nginx container IP for all production traffic.
  Affected partitions: external-signup register (15/hour), password-reset secondary (50/hour),
  document-upload-by-code IP fallback.
- `RemoteIpAddress` is read only in `CaseEvaluationHttpApiHostModule` (verified by grep across
  `src/`), so the blast radius inside our own code is the limiters. ABP's audit log reads it
  internally and will start recording real client IPs -- an improvement, but a behaviour change.
- Rate-limiter structure to mirror: `ConfigurePasswordResetRateLimiter`
  (`CaseEvaluationHttpApiHostModule.cs:597`) builds a chained `GlobalLimiter` from two
  `PartitionedRateLimiter`s; each path-matches and otherwise returns `GetNoLimiter`.
  `OnRejected` already emits `Retry-After` for every partition.
- Reconcile controller: `HttpApi.Host/Controllers/Integration/CaseTrackerReconcileController.cs`,
  `[Route("api/integration")]`.

## Why XForwardedFor is safe here

nginx `$proxy_add_x_forwarded_for` appends the real `$remote_addr` to the END of any
client-supplied header. .NET's `ForwardedHeadersMiddleware` reads right-to-left and consumes
`ForwardLimit` entries (default 1). So a spoofed `X-Forwarded-For: 1.2.3.4` arrives as
`1.2.3.4, <real>` and the real address wins. Keep `ForwardLimit` at its default; do not raise it.

Refs: nginx `$proxy_add_x_forwarded_for`, and
https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer

## Decisions taken (Adrian, 2026-07-29)

- Add the limiter AND fix `XForwardedFor` (not one or the other).
- Pin the reconcile hostname in the contract; do NOT change the nginx proxy.
- No TLS/client-cert code; one question to Levon instead.

## Tasks

### T1 -- process X-Forwarded-For (approach: code)

`ConfigureForwardedHeaders`: `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`.
Leave both allowlists cleared and `ForwardLimit` at default. Update the doc comment to record
why the append-plus-ForwardLimit-1 interaction makes this non-spoofable in this topology.

### T2 -- rate-limit /api/integration (approach: code)

In `CaseEvaluationHttpApiHostModule`:

- `public const string IntegrationPathPrefix = "/api/integration";`
- `internal static bool IsIntegrationPath(HttpContext)` -- prefix match, mirroring
  `IsExternalSignupRegisterPath`.
- `internal static string ResolveIntegrationPartitionKey(HttpContext)` -- `ip:<addr>` else
  `global`, mirroring `ResolvePasswordResetIpPartitionKey`.
- A `GetFixedWindowLimiter` partition in the PRIMARY partitioner keyed
  `integration:<key>`, `PermitLimit = 300`, `Window = 1 hour`, `QueueLimit = 0`.

Prefix-scoped rather than route-scoped so any future `/api/integration` endpoint inherits the
limit by default. 300/hour: each call is ~8-10 indexed reads on one office database, so a
post-outage repair sweep has headroom, while enumeration is capped instead of unbounded. One
constant to change if their sweep needs more.

### T3 -- tests (approach: test-after)

`test/.../HttpApiHost/CaseEvaluationHttpApiHostModuleTests.cs`:

- UPDATE `ConfigureForwardedHeaders_ProcessesXForwardedProto` -- it pins the flags to
  `XForwardedProto` exactly and WILL fail. Assert both flags.
- ADD an assertion that `ForwardLimit` is 1, since the anti-spoofing argument depends on it.
- ADD path-match cases for `IsIntegrationPath` (match, non-match, casing).
- ADD partition-key cases for `ResolveIntegrationPartitionKey` (ip, global fallback).

### T4 -- contract doc (approach: code)

`docs/integration/case-tracker-api-contract.md`:

- Dated revision block at the top.
- Pin the reconcile host: `https://admin.api.<base>/api/integration/offices/{tenantId}/appointments/{id}`.
  `api.<base>` does NOT match nginx's `*.api.<base>` and DOES match `*.<base>`, so it reaches the
  Angular container; `admin` is the reserved slug yielding Host context
  (`HostAwareDomainTenantResolveContributor.cs:31`).
- Revise the two "no rate limit" statements (~line 440 and ~line 477) to state 300/hour per
  source IP with `Retry-After` on 429.

### T5 -- module CLAUDE.md (approach: code)

`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CLAUDE.md` says "Three anonymous endpoint
families are rate-limited". Now four. Also note the forwarded-headers change.

### T6 -- Levon pack (approach: code, not repo)

Add: the TLS question (http vs https / who signed the cert / client cert required?), the pinned
reconcile hostname, and the rate-limit change. Replaces the "no rate limit, as agreed" line.

## Acceptance (EARS)

- WHEN a request arrives at `/api/integration/*` with X-Forwarded-For set by nginx, THE SYSTEM
  SHALL partition the rate limit on the appended real client address, not the proxy address.
- WHEN one source IP exceeds 300 requests per hour to `/api/integration/*`, THE SYSTEM SHALL
  respond 429 with a `Retry-After` header.
- WHEN a request arrives at any path outside the four rate-limited prefixes, THE SYSTEM SHALL
  apply no limit.
- WHEN a client supplies its own `X-Forwarded-For`, THE SYSTEM SHALL use the address nginx
  appended rather than the client-supplied value.

## Validation loop

Backend only -- no Angular in this diff.

    dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
    dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
    dotnet test HealthcareSupport.CaseEvaluation.slnx
    python .claude/scripts/verify_structure.py

## Open / carried

- UNVERIFIED: whether the deployed wildcard certificate carries a `*.api.<base>` SAN. TLS
  wildcards match exactly one label, so `*.<base>` alone would not cover `admin.api.<base>`.
  Needs a check on the box over VPN before Levon is told the hostname is usable.
- The reconcile GET stays anonymous; the token remains the only authn. An nginx source-IP
  allow-list was considered and not taken.
