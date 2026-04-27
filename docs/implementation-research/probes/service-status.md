# Service status (Phase 1.5)

**Timestamp:** 2026-04-24 ~12:35 local.

## Running endpoints

| Service | Scheme | Port | Probe URL | Status |
|---|---|---|---|---|
| AuthServer | **HTTPS** | 44368 | `https://localhost:44368/.well-known/openid-configuration` | HTTP 200 |
| AuthServer (health) | HTTPS | 44368 | `https://localhost:44368/health-status` | HTTP 200 |
| HttpApi.Host | **HTTPS** | 44327 | `https://localhost:44327/swagger/v1/swagger.json` | HTTP 200 (317 paths) |
| Angular | HTTP | 4200 | `http://localhost:4200/` | HTTP 200 (277 build artifacts in `dist/CaseEvaluation/browser/`; `npx serve` running in background shell `biaz13pb4`) |

## Smoke test of password grant

Confirmed 2026-04-24 ~12:45:

```
curl -sk -X POST https://localhost:44368/connect/token \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
```

Returns JSON with `access_token`, `token_type`, `expires_in: 3599`, `id_token`,
`refresh_token`. Authenticated read probe `GET /api/app/appointments` with
Bearer token returns `{"totalCount":0,"items":[]}` (confirms zero seed data --
matches track 10 Part 3).

**Track-10 live finding reproduced:** `GET /api/multi-tenancy/tenants` with host
admin Bearer still returns **HTTP 404**. This still blocks the tenant-onboarding
demo path; briefs touching tenant provisioning should cite this probe.

## Important correction to the gap-analysis memory

The inherited `reference_gap_analysis.md` entry and track 10 noted NEW services
as HTTP:

> NEW: `http://localhost:44327` (ABP HttpApi.Host), `http://localhost:44368`
> (AuthServer / OpenIddict). All HTTP in this deployment, not HTTPS despite
> config declaring HTTPS.

Current dotnet-run launch profile binds both services to **HTTPS** with a
self-signed dev cert. Probes MUST use `curl -k https://...` (the `-k` flag
bypasses certificate validation for the localhost dev cert). Plain
`http://localhost:44368/...` and `http://localhost:44327/...` return connection
refused. This supersedes the memory note for this research session.

## Probe guidance for Phase 2 subagents

- Read-only probes: `curl -sk https://localhost:44327/api/app/<entity>/...`
  after obtaining a token.
- OIDC discovery: `curl -sk https://localhost:44368/.well-known/openid-configuration`.
- Password grant (seeded admin, LocalDB only):
  ```
  curl -sk -X POST https://localhost:44368/connect/token \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
  ```
  Store the returned access_token in a local shell var; never write it into a brief.
  Probe logs redact the token value to `Bearer <REDACTED>`.
- Angular routing probes: optional; skip if Angular is still building and rely on
  source analysis under `angular/src/app/**/*.routes.ts` instead.

## OIDC grant types advertised (live)

Confirmed via `GET /.well-known/openid-configuration`:
- `authorization_code`, `implicit`, `password`, `client_credentials`, `refresh_token`
- `urn:ietf:params:oauth:grant-type:device_code`
- `urn:ietf:params:oauth:grant-type:token-exchange`
- `LinkLogin`
- (9 grant types total; track 05 initially listed 6, track 10 flagged the 9 count.
  Confirmed at 9 in this session.)

## Background process IDs

Retained so the Phase 6 cleanup can stop them cleanly:

- AuthServer: shell `b9t9q2h3t` (dotnet run AuthServer)
- HttpApi.Host: shell `bitugrm20` (dotnet run HttpApi.Host)
- Angular build: shell `bbc0gz15t` (ng build, exits when done)
- Angular serve: pending (launched once build completes)

Phase 6 cleanup stops all via TaskStop. If Adrian needs to stop them manually later:
Task Manager -> dotnet.exe / node.exe processes owning ports 44368 / 44327 / 4200.
