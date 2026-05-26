---
title: Container log sweep (last hour)
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Container log sweep (snapshot 2026-05-25 23:43 PT)

Last-hour sweep across all 7 containers. Verdict: **demo-ready with
2 items to verify**.

## main-api-1 (verdict: minor)

WARN
- `XSRF-TOKEN SameSite=None / Secure` x26 -- cosmetic dev cookie; HTTP dev origin.
- `EF Skip/Take without OrderBy` x4 -- pre-existing pagination advisory.
- `Overriding HTTP_PORTS 8080 / HTTPS_PORTS ''` x1 -- boot-time only.
- `Volo.Abp.Authorization.AbpAuthorizationException` x1 at 23:40:44 -- pending-count fetched before token attached (OBS-40 pattern, expected).

ERROR
- `Cannot allocate memory: '/app/src/HealthcareSupport.CaseEvaluation.Domain.Shared'` at 22:53:13 -- 2x `IOException` from `FileSystemEnumerator` (virtual-file-provider) coincident with Hangfire `Hash` cleanup. **Likely transient WSL2 ENOMEM**. Container kept serving requests for the next 50+ minutes without recurrence.

SLOW: 0 EF queries >500ms. 0 request actions >500ms in last hour.

Patterns NOT present (verified): `ObjectDisposedException`,
`AbpDbConcurrencyException`, Hangfire retries, 5xx responses.

## main-authserver-1 (verdict: clean)

WARN: 9x XSRF-TOKEN cookie warning + 1x boot port-override + 1x
"Ldap login feature is not enabled!" (informational).
ERROR/FATAL: none.
SLOW: 0.

## main-sql-server-1 (verdict: minor)

- 23:29:19: `Login failed for user 'sa'. Reason: Failed to open the
  explicitly specified database 'Falkinstein'.` Single occurrence;
  no follow-up failure. Tenant uses shared DB (verified: 0 rows in
  `SaasTenantConnectionStrings`), so ABP fell back to default
  connection. Self-recovering.

## main-redis-1 (verdict: clean)

Idle last hour; last `Background saving` cycle 07:25 succeeded.
No errors.

## main-minio-1 (verdict: clean)

Idle last hour. Historical boot: default-creds warning + single-
drive warning (both expected single-node dev).

## main-gotenberg-1 (verdict: clean)

Idle last hour. **Cold-start latency was 13.13s on first call**;
subsequent calls 450ms-2.4s. **Recommendation: pre-warm one PDF
before demo** to avoid the 13-second LibreOffice daemon warm-up
landing on stage.

## Items to verify before Tuesday morning

1. **API memory transient (22:53):** Check WSL2 memory cap
   (`~/.wslconfig`, currently 4GB per terminal-env rule) and confirm
   demo machine has headroom. If recurs, restart `main-api-1` as
   first move.
2. **Gotenberg cold-start:** Fire one PDF conversion during pre-
   demo setup to pre-warm LibreOffice. Saves 13 seconds on first
   real packet generation in front of audience.

## Patterns confirmed CLEAN

- `ObjectDisposedException` (BUG-036 fix region): NOT recurring.
- `AbpDbConcurrencyException`: not present.
- Hangfire automatic retries: not present.
- HTTP 5xx responses: not present.
- EF queries over 500ms: none.
- Slow controller actions: none.
