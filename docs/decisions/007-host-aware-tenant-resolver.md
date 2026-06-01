# ADR-007: Host-aware subdomain tenant resolver

- Status: Accepted
- Date: 2026-05-11
- Supersedes (partially): ADR-006 -- the assumption block, not the routing intent

## Context

ADR-006 (2026-05-05) decided that the Angular SPA promotes the bare `localhost:4200` to `admin.localhost:4200` so that "admin" is the dedicated subdomain for the Volo SaaS Host surface. The companion comment in `angular/src/tenant-bootstrap.ts` asserted that ABP's stock `DomainTenantResolveContributor` returns null when the slug does not match a registered tenant -- in other words, that "admin" would naturally fall through to Host context because no `admin` row exists in `SaasTenants`.

Empirical evidence (2026-05-11) contradicts that assumption:

```
curl -H "Host: admin.localhost" http://localhost:44327/api/abp/application-configuration
=> HTTP/1.1 404 Not Found
   Abp-Tenant-Resolve-Error: Tenant not found!
   <body>There is no tenant with the tenant id or name: admin</body>
```

ABP's `Volo.Abp.AspNetCore.MultiTenancy.MultiTenancyMiddleware` does not fall through; it short-circuits with HTTP 404 and the `Abp-Tenant-Resolve-Error` header whenever `ITenantConfigurationProvider.GetAsync()` returns null for a slug that was actively resolved from the host.

Result: every request to `admin.localhost:44327` and `admin.localhost:44368` 404'd. SPA bootstrap died on the application-configuration 404; AuthServer login would have died on the same wedge if anyone had attempted it.

## Decision

Replace `options.AddDomainTenantResolver("{0}.localhost")` in both `CaseEvaluationHttpApiHostModule.ConfigureMultiTenancy` and `CaseEvaluationAuthServerModule.ConfigureMultiTenancy` with a custom `HostAwareDomainTenantResolveContributor` that:

1. Parses the slug from the Host header against the same `"{0}.localhost"` template.
2. Treats the literal slug `admin` as a reserved Host marker -- returns from the contributor without setting `context.TenantIdOrName`, so the request runs in Host context (`CurrentTenant.Id == null`).
3. For any other slug, sets `context.TenantIdOrName = slug` and `context.Handled = true`, identical to the stock contributor's behaviour. Volo's middleware then performs the usual tenant-store lookup; unknown slugs still 404 with the standard "Tenant not found!" error, preserving typo protection.

The contributor lives at `src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/HostAwareDomainTenantResolveContributor.cs`. `HttpApi` is the lowest ABP layer that has `Microsoft.AspNetCore.Http` in scope (it carries the controllers). `AuthServer.csproj` now references `HttpApi` to share the class without duplication.

## Consequences

- `admin.localhost:4200`, `admin.localhost:44368`, and `admin.localhost:44327` all run in Host context. Volo SaaS Host admin (the `it.admin@hcs.test` user with role `IT Admin`) can now log in.
- `{tenant}.localhost` resolution unchanged: `falkinstein.localhost:44327/api/abp/application-configuration` still returns 200 for the Falkinstein tenant.
- Typo protection is preserved: `falkinstien.localhost:44327` still 404s with the standard "Tenant not found!" body. Only the single reserved slug `admin` is special-cased.
- Adding a future reserved slug (`internal`, `ops`, etc.) is a one-line constant change in `HostAwareDomainTenantResolveContributor.ReservedHostSlug`. If the reserved set grows beyond one slug we should refactor to a `HashSet<string>` and ideally read it from `IConfiguration`.
- For Phase 2 production hosts (e.g. `falkinstein.qmeame.com`), pass the base host suffix through `IConfiguration` instead of hard-coding `localhost` in the resolver constructor argument; the parse logic already supports arbitrary `{0}.suffix` templates.

## Rejected alternatives

- **Seed a real `admin` tenant in `SaasTenants`.** Wrong semantically: Host context and "a tenant called admin" are different concepts in ABP/Volo. Migrator and SaaS-management UI bloat for no benefit.
- **Drop the SPA promotion entirely and serve Host on bare `localhost:4200`.** Closer to ABP/Volo's defaults, but contradicts ADR-006's stated visual-distinction intent (`admin.` in the URL is a deliberate operator-facing signal) and would require a SPA refactor that touches every component that reads the slug.

## Verification

After the change, both of these must hold:

```
curl -i -H "Host: admin.localhost"        http://localhost:44327/api/abp/application-configuration  # => 200, host context
curl -i -H "Host: falkinstein.localhost"  http://localhost:44327/api/abp/application-configuration  # => 200, tenant context (Falkinstein)
curl -i -H "Host: falkinstien.localhost"  http://localhost:44327/api/abp/application-configuration  # => 404, "Tenant not found!" (typo protection)
```

## References

- `angular/src/tenant-bootstrap.ts` -- the SPA promotion logic and the corrected ADR comment
- `src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/HostAwareDomainTenantResolveContributor.cs` -- the resolver
- ABP source -- `MultiTenancyMiddleware` 404 path: https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.AspNetCore.MultiTenancy/Volo/Abp/AspNetCore/MultiTenancy/MultiTenancyMiddleware.cs
- ABP source -- stock `AbpDomainTenantResolveContributorBase`: https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/AbpDomainTenantResolveContributorBase.cs
- Volo support thread #10261 -- same-shape "admin slug 404" problem with a custom contributor solution: https://abp.io/support/questions/10261/Issue-with-Domain-Based-Tenant-Resolver-Login-Angular--OpenIddict
