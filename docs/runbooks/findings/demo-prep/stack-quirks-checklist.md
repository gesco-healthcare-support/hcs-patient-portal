---
title: Stack quirks + pre-demo checklist (ABP / Angular / Hangfire / EF Core / MinIO / OpenIddict)
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Stack quirks most likely to surface during Tuesday demo

Top 18 quirks across the 5 demo flows, ranked by probability. Every
claim cites a primary doc or upstream issue.

## Pre-demo checklist (derived from the quirks below)

1. **`docker compose up -d` at least 60 seconds before the demo**
   (mitigates clock-skew + Hangfire SQL-restart issues).
2. **Open the demo in an incognito window** (mitigates stale
   `__tenant` cookie + expired refresh-token stuck-spinning state).
3. **Confirm Firm Name field deploys** with the registration role-
   conditional logic.
4. **Build Angular via `npx ng build --configuration development`
   then `npx serve`** -- never `ng serve` (mitigates CORE_OPTIONS
   NullInjectorError).
5. **Diff the latest migration against the BUG-036 baseline** to
   confirm `HasFilter` survives migration regen.
6. **Don't restart containers after the approval modal demo**
   (Hangfire InvisibilityTimeout is 30 min; packets get stuck).
7. **Demo from a single tab** to avoid two-tab refresh-token race
   logging out the user mid-presentation.

## 1. Registration (ABP Razor) -- role-conditional Firm Name

| # | Quirk | Demo symptom | Workaround / "say this" |
|---|---|---|---|
| 1.1 | Custom property must be forwarded by an `AccountAppService.RegisterAsync` override; UI-only override silently drops the value. | Firm Name appears in form, user submits, row has no firm name. | "We forward Firm Name through an overridden `RegisterAsync` -- deliberate ABP override pattern." |
| 1.2 | Extension properties stored as JSON in `ExtraProperties` -- queryable only via JSON functions, not first-class columns. | Filter/sort on Firm Name returns wrong ordering compared to dedicated columns. | "Firm Name is an extension property serialized to JSON; we promote it to a real column post-demo if needed." |
| 1.3 | Tenant resolution priority can pick up a stale `__tenant` cookie from a previous demo run -- new user lands on the wrong tenant. | New registration shows up under host or wrong tenant. | "Use incognito for the demo." |

## 2. Login (OpenIddict OIDC + PKCE -> Angular SPA)

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 2.1 | Concurrent `/connect/token` refresh from two tabs consumes the rolling refresh token; second request silently logs the user out. | Two tabs open, user bounced to login mid-demo. | Demo from a single tab. |
| 2.2 | When the refresh token itself expires, ABP Angular's interceptor does NOT auto-redirect to login -- the SPA hangs until manual cookie clear. | Stale browser tab from yesterday: clicking spins forever. | Fresh incognito for demo. |
| 2.3 | Logout clears auth cookie but does NOT revoke already-issued access tokens; copies still pass `IsAuthenticated` until they expire. | "Why does my JWT still work after I logged out?" | "Short access-token lifetime is the standard OIDC mitigation; full revocation requires reference tokens." |
| 2.4 | `JwtBearer` defaults to 5-minute `ClockSkew`; AuthServer/HttpApi.Host container clock drift at startup can reject tokens. | First login after cold compose-up returns 401. | Bring up stack 60+ seconds before demo. |

## 3. Appointment listing (multi-role filtering)

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 3.1 | The `IMultiTenant` data filter does not apply when `CurrentTenant.Id` is null (host context) -- queries return zero or all tenants depending on shape. | Page loaded before tenant resolution shows empty list. | Confirm `__tenant` cookie/route is set before rendering. |
| 3.2 | `IDataFilter.Disable<IMultiTenant>()` only crosses tables in shared-database mode; DB-per-tenant requires `CurrentTenant.Change(id)`. | Cross-tenant admin filter returns only host rows. | "We're shared-DB through Phase 1." |

## 4. Approval modal -> Hangfire packet generation

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 4.1 | HttpApi.Host restart mid-job leaves fetched jobs in `Processing` until `InvisibilityTimeout` (default 30 min) elapses. | Demo restart leaves a packet "stuck" with no visible progress. | Don't restart containers between approve and packet visibility. |
| 4.2 | `AutomaticRetryAttribute` defaults to 10 attempts with exponential backoff -- a poison job retries 10× before showing as Failed. | Audience sees "Failed -> Retrying" cycling for minutes. | Set `[AutomaticRetry(Attempts = 1)]` on demo-visible jobs. |
| 4.3 | Hangfire SQL storage fails to resume dequeuing after a SQL Server restart on some 1.7.x versions. | After `docker compose restart sql`, packets enqueue but never run. | If SQL bounces, restart API too. |
| 4.4 | Capturing repo/DbContext from `OnCompleted` callback: worker thread sees a disposed instance -> `ObjectDisposedException`. | Exact error pattern we've already hit. | Pass IDs only, open fresh UoW inside the job. |
| 4.5 | Background jobs start with no tenant and no user -- multi-tenant filter blanks results unless wrapped in `CurrentTenant.Change`. | Packet job runs, finds zero appointments, produces empty PDF. | Pass `TenantId` in args, wrap with `CurrentTenant.Change`. |

## 5. Document upload (MinIO)

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 5.1 | MinIO presigned PUT requires raw bytes + `Content-Length`. Sending `multipart/form-data` stores the boundary text inside the object. | Uploaded PDF opens to garbled text starting with `------WebKitFormBoundary`. | Angular sends raw `File` blob, not `FormData`. |
| 5.2 | `Transfer-Encoding: chunked` PUT rejected with `411 MissingContentLength`. | 411 on upload; no Content-Length in HAR. | Read file into Blob first to force measured length. |
| 5.3 | SigV4 region mismatch (default `us-east-1`) or virtual-host vs path-style yields `SignatureDoesNotMatch`. | 403 on first upload of the demo. | Force `forcePathStyle: true` and `region: us-east-1`; sync container clocks. |

## 6. Packet regeneration / EF Core filtered unique index (BUG-036)

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 6.1 | EF Core on SQL Server auto-adds `IS NOT NULL` to unique indexes with nullable columns; `HasFilter(null)` once had a regression silently re-adding the auto-filter. | Migration regen "magically" reintroduces the auto-filter, breaks BUG-036 fix. | Diff every migration regen; assert `HasFilter` survives. |
| 6.2 | Filtered indexes require `SET QUOTED_IDENTIFIER ON`; `dotnet ef migrations script` does not emit it. | Migration runs locally, fails in CI. | Prepend `SET QUOTED_IDENTIFIER ON;` to generated scripts. |
| 6.3 | Two `HasIndex` calls on the same columns with different filters/names: EF emits only the last one. | A second non-filtered lookup index disappears silently after regen. | Pass a distinguishing name as second arg to `HasIndex`. |

## 7. Multi-tenant scaffolding

| # | Quirk | Demo symptom | Workaround |
|---|---|---|---|
| 7.1 | `CurrentTenant.Change(tenantId)` must be wrapped in `using` -- leaking the scope propagates the tenant context to unrelated subsequent operations. | Second API call on the same request lands in the wrong tenant's data. | All `Change` calls in `using` blocks; verify in code review. |
| 7.2 | Angular standalone bootstrap must call `provideAbpCore(...)` exactly once. Two instances of `CORE_OPTIONS` InjectionToken trigger `NullInjectorError`. Vite duplicates the token -- this is the same pathology as our "no ng serve" rule. | White screen, console `NullInjectorError CORE_OPTIONS`. | Pre-build with `npx ng build` then serve via `npx serve`. |

## Source list

ABP docs: docs.abp.io (Account Module, Multi-Tenancy, Unit of Work,
Customizing Application Modules).
ABP support Q&A: support.abp.io (#1117 refresh token, #5506 access
tokens, #3985 disposed context, #5539 NullInjectorError).
GitHub issues: abpframework/abp #5595 (Hangfire/UoW),
abpframework/abp #24390 (refresh-token handling), openiddict-core
#496, HangfireIO/Hangfire #1791, #1795 (SQL restart), dotnet/efcore
#20136 (HasFilter regression), #21070 (QUOTED_IDENTIFIER), #18159,
#33454 (duplicate HasIndex), minio/minio #8111 (multipart), #7792
(chunked), #5083, #7209 (signature).
Microsoft Learn: SQL Server CREATE FILTERED INDEXES, EF Core Indexes.
