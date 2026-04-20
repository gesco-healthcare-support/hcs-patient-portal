[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-08

# FEAT-08: Swagger OAuth Does Not Work From Browser in Docker -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17, Docker E2E 2026-04-16 ISSUE-005)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 181-194
- `etc/docker-compose/docker-compose.yml` `api` service environment

---

## Current state (verified 2026-04-17)

`docker-compose.yml` `api` service:
- `AuthServer__Authority: http://localhost:44368` (public issuer for JWT validation)
- `AuthServer__MetaAddress: http://authserver:8080` (Docker-internal DNS for backend-to-backend OIDC metadata fetch)

`CaseEvaluationHttpApiHostModule.ConfigureSwagger` (lines 181-193) passes `configuration["AuthServer:MetaAddress"]` into `AddAbpSwaggerGenWithOidc`. Swagger UI runs in the user's browser, which cannot resolve `authserver:8080`, so the Authorize button fails before OIDC discovery completes (`net::ERR_NAME_NOT_RESOLVED`).

Angular is unaffected because `docker/dynamic-env.json` hard-codes `http://localhost:44368`.

---

## Official documentation

- [ABP Swagger Integration](https://abp.io/docs/latest/framework/api-development/swagger) -- `AddAbpSwaggerGenWithOidc(configuration, scopes, flows, discoveryEndpoint)`. `discoveryEndpoint` is "the reachable openid-provider endpoint ... when deployed on K8s, should be metadata URL of the reachable DNS over internet". `null` falls back to `Authority`.
- [ABP layered-app Docker Compose deployment](https://abp.io/docs/latest/solution-templates/layered-web-application/deployment/deployment-docker-compose?UI=MVC&DB=EF&Tiered=Yes) -- canonical split of `AuthServer__Authority` (browser-facing) vs `AuthServer__MetaAddress` (container-to-container). Documents `AuthServer__IsOnK8s` (older: `IsContainerizedOnLocalhost`).
- [Microsoft -- Configure JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-9.0) -- when both are set, `MetadataAddress` is used for OIDC fetch, `TokenValidationParameters.ValidIssuer`/`ValidIssuers` validates `iss` claim. Decouples "where I fetch metadata" from "what issuer I trust".
- [OpenAPI 3.0 -- OpenID Connect Discovery](https://swagger.io/docs/specification/v3_0/authentication/openid-connect-discovery/) -- `openIdConnectUrl` is fetched by Swagger UI in the browser; must be browser-reachable. Scopes discovered from document.
- [Microsoft.AspNetCore.Authentication.JwtBearer 10.0.6](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer) -- confirms `JwtBearerOptions.Authority` and `JwtBearerOptions.MetadataAddress` are separate properties.

## Community findings

- [ABP Support #9525 -- Token validation fails with internal MetadataAddress in K8s](https://abp.io/support/questions/9525/Token-Validation-Fails-When-Using-Internal-MetadataAddress-in-Kubernetes-Deployment-IDX10204--IDX10500) -- `Authority` must match `iss` claim (public URL); `MetadataAddress` can be internal; SSL/cert validation is separate hazard.
- [ABP Support #2248 -- Docker Swagger Authorize](https://abp.io/support/questions/2248/Docker-Swagger-Authorize) -- exact class of failure: `localhost`/`authserver` resolves differently inside container vs browser.
- [ABP Support #1121 -- Authenticate through Swagger](https://abp.io/support/questions/1121/Not-able-to-Authenticate-through-Swagger-of-Custom-Module-API) + [#4383 -- Multitenant Swagger authentication](https://abp.io/support/questions/4383/Multitenant-swagger-authentication) -- recurring theme: Swagger UI is a pure browser client.
- [dotnet/aspnetcore #7803 -- MetadataAddress vs Authority with IdentityServer4](https://github.com/dotnet/aspnetcore/issues/7803) -- when both set, `MetadataAddress` wins for metadata; `Authority` drives default issuer expectation.
- [Swashbuckle.AspNetCore #2604 -- OpenID Connect Options for isolated network](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2604) -- mirror of the same problem outside ABP.
- [Scott Brady -- ASP.NET Core Swagger UI with IdentityServer4](https://www.scottbrady.io/identity-server/aspnet-core-swagger-ui-authorization-using-identityserver4) -- canonical Auth Code + PKCE wiring; UI URL must be browser-reachable.

## Recommended approach

**Option A (preferred -- config key)**:
1. Add `AuthServer:SwaggerMetaAddress` (or `AuthServer:PublicAuthority`).
2. Pass into `AddAbpSwaggerGenWithOidc` as `discoveryEndpoint`.
3. Keep `AuthServer:MetaAddress` for backend JWT handler only.
4. In docker-compose.yml set `SwaggerMetaAddress=http://localhost:44368`; in production set to real public HTTPS URL.
5. Pin `TokenValidationParameters.ValidIssuer` to public issuer regardless of which URL the API fetches metadata from.

Matches ABP's own tiered Docker template. Configuration-only change plus one line in `ConfigureSwagger`.

**Option B (proxy metadata through API)**: pass-through endpoint that fetches and returns the AuthServer's metadata document, URL-rewritten (`authorization_endpoint`, `jwks_uri`, `end_session_endpoint`). More code, brittle across OpenIddict upgrades. Reject unless firewall requirement hides AuthServer hostname.

## Gotchas / blockers

- `iss` claim is baked in at token creation time; changing `MetaAddress` doesn't change `iss`. Switching to HTTPS or a different external hostname requires `ValidIssuer`/`ValidIssuers` update or IDX10205 on every request.
- Swagger UI runs OAuth Auth Code + PKCE in the browser; `/swagger/oauth2-redirect.html` must be registered in OpenIddict as an allowed redirect URI for the Swagger client.
- Mixing HTTP/HTTPS between `Authority` and `MetaAddress` triggers `RequireHttpsMetadata` failures in .NET 10 by default; keep both HTTP in dev or set `RequireHttpsMetadata=false` only for local Docker.
- Option B requires rewriting `authorization_endpoint` and `jwks_uri` inside the proxied JSON -- else Swagger UI still hits `http://authserver:8080/connect/authorize` from the browser.
- CORS on AuthServer must allow the browser origin serving Swagger.

## Open questions

- Does the Docker Compose stack expose AuthServer on `http://localhost:44368` for all local developers, or does each dev get a different port? Affects env-var vs per-profile override.
- In production: will AuthServer and API share a public domain (path-routed) or separate hosts? Determines whether `Authority` and `SwaggerMetaAddress` converge.
- Is Swagger UI meant to stay exposed in production, or dev-only? MS explicitly warns against deploying Swagger UI with weakened auth to production.
- Does the current OpenIddict config register `https://<api-host>/swagger/oauth2-redirect.html` as an allowed redirect URI for the API's Swagger client?

## Related

- [SEC-04](SEC-04.md) -- CORS policy must also permit Swagger origin
- [docs/issues/INCOMPLETE-FEATURES.md#feat-08](../INCOMPLETE-FEATURES.md#feat-08-swagger-oauth-does-not-work-from-browser-in-docker)
