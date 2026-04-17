[Home](../../INDEX.md) > [Issues](../) > Research > SEC-04

# SEC-04: CORS Policy Wide Open -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 230-250
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` (`App:CorsOrigins`)
- `src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json` (`App:CorsOrigins`)

---

## Current state (verified 2026-04-17)

```csharp
policy.WithOrigins(corsOrigins)
    .WithAbpExposedHeaders()
    .SetIsOriginAllowedToAllowWildcardSubdomains()
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();
```

Origin list includes `https://*.CaseEvaluation.com` (wildcard subdomain) + localhost. This is the verbatim ABP scaffolding default (confirmed against [ABP template source](https://github.com/abpframework/abp/blob/dev/templates/app/aspnet-core/src/MyCompanyName.MyProjectName.HttpApi.Host/MyProjectNameHttpApiHostModule.cs)) -- not "ABP telling you this is safe", ABP telling you "fill in `App:CorsOrigins` and live with the rest." With wildcard subdomain + credentials, any XSS on any current or future subdomain (including forgotten staging/marketing hosts) can read authenticated API responses.

---

## Official documentation

- [Enable CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0) -- "Allowing cross-origin credentials is a security risk. A website at another domain can send a signed-in user's credentials to the app on the user's behalf without the user's knowledge." Also: "CORS is not a security feature... An API isn't safer by allowing CORS."
- [ABP template `MyProjectNameHttpApiHostModule.cs`](https://github.com/abpframework/abp/blob/dev/templates/app/aspnet-core/src/MyCompanyName.MyProjectName.HttpApi.Host/MyProjectNameHttpApiHostModule.cs) -- confirms the 4-method stack is ABP's generated default.
- [ABP Microservice CORS Configuration](https://abp.io/docs/latest/solution-templates/microservice/cors-configuration) -- same pattern in the microservice template.

## Community findings

- [PortSwigger Web Security Academy -- CORS](https://portswigger.net/web-security/cors) -- wildcard subdomain trust + XSS anywhere under the wildcard = full authenticated data theft.
- [OWASP WSTG -- Testing CORS](https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/11-Client-side_Testing/07-Testing_Cross_Origin_Resource_Sharing) -- recommends explicit origin allowlists, never mixing wildcard origins with credentials, anchored subdomain regex.
- [SecureLayer7 -- OWASP Top 10 Misconfig #5: CORS](https://blog.securelayer7.net/owasp-top-10-security-misconfiguration-5-cors-vulnerability-patch/) -- practical exploit scenarios.
- [Code Maze -- AnyOrigin + AllowCredentials](https://code-maze.com/aspnetcore-how-to-fix-cors-error-with-anyorigin-and-allowcredentials/) -- ASP.NET Core runtime refuses literal `AllowAnyOrigin()` + `AllowCredentials()`; `WithOrigins` + wildcard-subdomain is the workaround ABP took, but same practical risk surface.
- [ABP forum #1940 -- CORS Policy](https://abp.io/support/questions/1940/Cors-Policy) -- developers treat this stack as "the answer" not a starting point.

## Recommended approach

1. Replace `https://*.CaseEvaluation.com` with an explicit host list (`app`, `admin`, etc.). Wildcard is the biggest risk -- implicit trust anchor for every future/unclaimed subdomain.
2. Narrow `AllowAnyMethod` to only `GET, POST, PUT, PATCH, DELETE, OPTIONS` (what the Angular proxy issues).
3. Narrow `AllowAnyHeader` to what OpenIddict + ABP proxy actually send: `Authorization`, `Content-Type`, `Accept`, `X-Requested-With`, `__tenant`, `__RequestVerificationToken`, `Abp.TenantId`, plus whatever `WithAbpExposedHeaders` adds. MEDIUM confidence on exact header set -- verify by opening DevTools on a running Angular proxy call.

## Gotchas / blockers

- Tightening headers can break ABP localisation/tenant resolution if you forget `__tenant` or `.AspNetCore.Culture` cookies -- test multi-tenant login after any change.
- OpenIddict PKCE flow runs against AuthServer (44368), not HttpApi.Host (44327). This CORS policy governs the API host only; AuthServer has its own CORS.
- SignalR hubs (if added later) need `x-signalr-user-agent`; allowlist as needed.
- Ownership of `*.CaseEvaluation.com` matters: orphaned subdomain CNAMEs become attack vectors under a wildcard policy.

## Open questions

- Does production actually need wildcard subdomains, or is multi-tenant host-header-based (one `app.CaseEvaluation.com` switching tenant via `__tenant` header)?
- Is LeptonX admin served from a different host needing its own CORS entry?
- Does `abp generate-proxy` output any non-standard headers that a strict allowlist would break?

## Related

- [docs/issues/SECURITY.md#sec-04](../SECURITY.md#sec-04-cors-policy-is-wide-open)
