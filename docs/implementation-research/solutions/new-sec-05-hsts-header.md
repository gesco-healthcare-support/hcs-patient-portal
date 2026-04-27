# NEW-SEC-05: Emit Strict-Transport-Security header

## Source gap IDs

- NEW-SEC-05 -- track 10 Part 2: [../../gap-analysis/10-deep-dive-findings.md](../../gap-analysis/10-deep-dive-findings.md) (lines 92-96, 257)
- Supporting context: [../../gap-analysis/README.md](../../gap-analysis/README.md) (executive summary line 11: "no HSTS header" is listed among the 5 MVP-blocking NEW defects)

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:92-95` -- the `Configure<AbpSecurityHeadersOptions>(options => { options.Headers["X-Frame-Options"] = "DENY"; });` block. This is the one customization on top of ABP's default headers. No `AddHsts(...)` call exists anywhere in this module.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:302` -- pipeline step `app.UseAbpSecurityHeaders();` fires for every request. Per ABP docs (https://abp.io/docs/latest/framework/ui/mvc-razor-pages/security-headers, accessed 2026-04-24), this middleware emits X-Content-Type-Options, X-XSS-Protection, X-Frame-Options, and optional CSP. HSTS is NOT in ABP's default set.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:290-326` (full `OnApplicationInitialization`) -- the production path runs identical middleware to development; no `if (!env.IsDevelopment()) { ... }` branch exists today. `UseHttpsRedirection` is also absent.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:305-308` -- identical `AbpSecurityHeadersOptions` block to HttpApi.Host. Also no HSTS.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:313-354` (full `OnApplicationInitialization`) -- middleware order is `UseForwardedHeaders` -> `UseDeveloperExceptionPage` (dev only) -> `UseAbpRequestLocalization` -> `UseErrorPage` (prod only) -> `UseCorrelationId` -> `UseRouting` -> `MapAbpStaticAssets` -> `UseAbpStudioLink` -> `UseAbpSecurityHeaders` -> `UseCors` -> auth -> `UseAuditing` -> `UseConfiguredEndpoints`. The production-only branch (line 328-331) currently only wires `UseErrorPage()`.
- Grep across `src/**`: `UseHsts|UseHttpsRedirection|HstsOptions|Strict-Transport-Security` returns zero hits (confirmed 2026-04-24). No HSTS anywhere.
- Appsettings files present at `HttpApi.Host/appsettings{.json,.Development.json,.secrets.json}` and symmetrical for AuthServer. No HSTS-related keys. The production rollout will need to pass production `appsettings.Production.json` (not yet in repo) to set `ASPNETCORE_ENVIRONMENT=Production`, which trips the non-dev branches.
- `ConfigureAuthentication` (line 157-185) already reads `AuthServer:RequireHttpsMetadata` from config, confirming the project expects production to run on HTTPS. HSTS is the browser-side enforcement of that expectation.
- `CaseEvaluationHttpApiHostModule` already depends on `AbpAspNetCoreSecurityModule` transitively via `AbpSwashbuckleModule` / `AbpIdentityAspNetCoreModule`. Adding `AddHsts` needs no new NuGet package.

## Live probes

- Probe 1: `curl -skI https://localhost:44327/` returned HTTP 302 with headers `X-Content-Type-Options: nosniff`, `X-XSS-Protection: 1; mode=block`, `X-Frame-Options: DENY`, and NO `Strict-Transport-Security` header. Confirms the track-10 claim that NEW omits HSTS. Full log: [../probes/new-sec-05-hsts-header-2026-04-24T19-42-33.md](../probes/new-sec-05-hsts-header-2026-04-24T19-42-33.md).
- Probe 2: `curl -skI https://localhost:44327/swagger/index.html` returned HTTP 404 -- the Swagger path resolves via redirect, not direct GET -- but the response still carried the same three ABP headers and no HSTS. Same probe log.
- Probe 3: `curl -skI https://localhost:44368/` (AuthServer root) returned HTTP 200 with three ABP headers, antiforgery cookies, no HSTS. Proves AuthServer also omits HSTS. Same probe log.
- Probe 4: `curl -skI https://localhost:44368/.well-known/openid-configuration` returned HTTP 400 (Bad Request for HEAD on this endpoint) but the response carried identical headers with no HSTS. Same probe log.

## OLD-version reference

- Track 10 Part 2 NEW-SEC-05 states OLD sends `Strict-Transport-Security: max-age=31536000`. We did not re-probe OLD in this research session (OLD is on a separate host at `P:\PatientPortalOld\...`, not running as part of Phase 1.5 services). OLD's exact placement is not load-bearing for the NEW fix -- the target is to emit HSTS in NEW, not to mirror OLD's configuration line-for-line.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict -- standard ABP module/pipeline shape.
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant -- irrelevant to this change (HTTP header is tenant-agnostic).
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), no `ng serve` (ADR-005) -- unaffected.
- HIPAA applicability: HSTS prevents HTTPS downgrade on browser-reachable surfaces, reducing risk of PHI exposure via a session stripped to plaintext HTTP.
- Capability-specific constraints:
  - MUST NOT emit HSTS to `localhost` in dev (Microsoft: "HSTS settings are highly cacheable by browsers"). The standard `UseHsts()` middleware already excludes loopback addresses; we also wrap the call in `if (!env.IsDevelopment())`.
  - MUST work for both HttpApi.Host (browser-reachable Swagger UI, OAuth redirect flows) and AuthServer (browser-reachable login pages). Both services get the same treatment.

## Research sources consulted

- ASP.NET Core HSTS guide (enforcing SSL): https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl (accessed 2026-04-24). Establishes UseHsts placement, default MaxAge (30 days), recommended production MaxAge (up to 1 year), and the dev warning.
- HstsBuilderExtensions.UseHsts API reference: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.hstsbuilderextensions.usehsts (accessed 2026-04-24). Confirms the extension is available in ASP.NET Core 10.0 (moniker `aspnetcore-10.0`) and sourced from `Microsoft.AspNetCore.HttpsPolicy`.
- HstsOptions API reference: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.httpspolicy.hstsoptions (accessed 2026-04-24). Confirms properties `MaxAge`, `IncludeSubDomains`, `Preload`, `ExcludedHosts`. Default ExcludedHosts contains `localhost`, `127.0.0.1`, `[::1]`.
- ABP Security Headers docs: https://abp.io/docs/latest/framework/ui/mvc-razor-pages/security-headers (accessed 2026-04-24). Confirms `UseAbpSecurityHeaders` does not include HSTS in its defaults.
- OWASP Secure Headers project: https://owasp.org/www-project-secure-headers/ (accessed 2026-04-24). Recommends `max-age=63072000; includeSubDomains`, advises against `preload` by default.
- Andrew Lock on HSTS in ASP.NET Core: https://andrewlock.net/understanding-the-hsts-header-in-asp-net-core/ (accessed 2026-04-24, referenced as community-reputable source for the "browsers cache the policy so start small" argument).

## Alternatives considered

1. **`app.UseHsts()` + `services.AddHsts(...)` in both host modules, guarded by `!IsDevelopment()`** -- `chosen`. Typed config, per-environment, uses Microsoft's hardened middleware. Zero new NuGet packages.
2. **Add `Strict-Transport-Security` to `AbpSecurityHeadersOptions.Headers` dictionary** -- `rejected`. The ABP middleware applies unconditionally (no environment check, no loopback exclusion). Would emit HSTS in dev against localhost, which browsers cache aggressively -- per Microsoft, this is precisely the scenario UseHsts() is designed to avoid.
3. **Put the header at the reverse-proxy layer (nginx / CloudFront / Azure Front Door)** -- `conditional`. Valid at the edge in production, but MVP deployment target is undefined (Q29 region), and the reverse proxy is not in scope. Defer-and-revisit; app-layer HSTS is still correct belt-and-suspenders once a reverse proxy exists.
4. **Write a custom middleware that emits HSTS only when `X-Forwarded-Proto: https`** -- `rejected`. Duplicates what `UseHsts()` already does. No gain.
5. **Skip HSTS on HttpApi.Host (API-only) per Microsoft's "don't HSTS your API" guidance; only add to AuthServer** -- `rejected`. HttpApi.Host is reachable by browser in this deployment (Swagger UI at `/swagger`, OAuth authorisation-code redirect lands back through the API host for some flows). Emitting HSTS there is harmless to non-browser clients (they ignore the header) and protects the browser-originated traffic.

## Recommended solution for this MVP

Add HSTS middleware to both host modules, guarded by production environment:

- **Where:** `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` and `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs`.
- **What (services):** Inside each module's `ConfigureServices`, register HSTS options via `context.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); options.IncludeSubDomains = true; options.Preload = false; });`. `ExcludedHosts` stays at its default (localhost / 127.0.0.1 / [::1]).
- **What (pipeline):** Inside each module's `OnApplicationInitialization`, before `app.UseRouting()` and inside an `if (!env.IsDevelopment()) { app.UseHsts(); }` guard, wire the middleware.
- **Which ABP primitive:** None. `AddHsts` and `UseHsts` are built into ASP.NET Core (`Microsoft.AspNetCore.HttpsPolicy`, already transitively referenced by the ABP web modules). ABP's own `AbpSecurityHeadersOptions` is untouched -- we add HSTS via the standard ASP.NET Core path so the loopback exclusion and environment guard behave as Microsoft designed.
- **Shape for the 2 files:**
  - HttpApi.Host: add 1 `context.Services.AddHsts(...)` call inside `ConfigureServices` (roughly 5 lines after the existing `Configure<AbpSecurityHeadersOptions>` block) and 1 `if (!env.IsDevelopment()) { app.UseHsts(); }` block inside `OnApplicationInitialization` (after `app.UseAbpRequestLocalization()` and before `app.UseRouting()`).
  - AuthServer: same two changes in `ConfigureServices` (after the existing `AbpSecurityHeadersOptions` Configure block at line 305-308) and `OnApplicationInitialization` (after `app.UseAbpRequestLocalization()` at line 326, before `app.UseCorrelationId()` at line 333).
- **No migration, no DTO, no Angular, no proxy regeneration.** Pure host-module config.
- **Reference implementation:** Microsoft's HSTS walkthrough in https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl is the canonical pattern. ABP has no opinion here because ABP's middleware does not cover HSTS.

## Why this solution beats the alternatives

- Uses Microsoft's battle-tested middleware: loopback exclusion is automatic, per-environment behaviour is idiomatic, and `HstsOptions` is strongly typed.
- Leaves `UseAbpSecurityHeaders` untouched so existing behaviour (X-Frame-Options, X-XSS-Protection, X-Content-Type-Options) is preserved exactly.
- No new NuGet package; zero dependency churn.
- Symmetric treatment of HttpApi.Host and AuthServer so both browser-reachable surfaces are covered. Non-browser API clients ignore HSTS per spec, so no functional impact on mobile / desktop integrations.

## Effort (sanity-check vs inventory estimate)

- Inventory (track 10 Part 2) says XS (1 line).
- Confirmed XS, but closer to 8-10 lines total across two files (one `AddHsts` call and one guarded `UseHsts` call per service module). Still single-PR / ~0.5-day work including test / manual-probe verification.
- The main time sink is verifying production behaviour: a staging deployment with `ASPNETCORE_ENVIRONMENT=Production` must receive the header, and dev must not. Both verifications are single `curl -skI` commands.

## Dependencies

- Blocks: none. HSTS is a leaf security header; nothing in the capability graph depends on it.
- Blocked by: none. The existing production pipeline (HTTPS termination, Kestrel on 44327/44368, `UseAbpSecurityHeaders`) already satisfies the prerequisites.
- Blocked by open question: none. Consolidated open questions Q25-Q27 (security/compliance) do not reference HSTS; Q29 (deployment region) affects reverse-proxy choice but not the app-layer header.

## Risk and rollback

- Blast radius: browser behaviour only. If `max-age=31536000` is shipped and HTTPS breaks (expired certificate, CDN misconfiguration, accidental downgrade to HTTP on an internal hop), browsers that have cached the HSTS policy will refuse HTTP fallback for up to one year. That is the designed security property, not a defect, but it means a mis-deployment cannot be recovered by downgrading to HTTP. It can be recovered by fixing HTTPS.
- Production rollout mitigation: ship with a short max-age (1 day = `TimeSpan.FromDays(1)`) for the first 2-4 weeks, then raise to 1 year once HTTPS stability is proven. This is Microsoft's recommended rollout pattern. (Not required for MVP; flag as a pre-production checklist item.)
- Rollback: delete the `AddHsts` and `UseHsts` lines, redeploy. Existing browsers retain the cached policy until max-age expires; this is unavoidable and by design.
- Dev is protected twice: the `!env.IsDevelopment()` guard AND `UseHsts`'s default `ExcludedHosts` (localhost / 127.0.0.1 / [::1]).

## Open sub-questions surfaced by research

- Production deployment host: will requests route through a reverse proxy (Azure Front Door / CloudFront / nginx)? If yes, HSTS at that edge is better practice than in the app; app-layer HSTS remains belt-and-suspenders. Flag for Q29 when the deployment target is decided, but do not block this capability.
- Should the initial production rollout use a 1-day max-age for the first 2 weeks before escalating to 1 year? Microsoft recommends this. Recommend inclusion in the production-cutover runbook.
- Should `ExcludedHosts` be extended for any internal test domains? Default covers loopback only. If Gesco's staging hostname is (say) `*.gesco.dev`, it should remain on HTTPS anyway, so the default is sufficient.
