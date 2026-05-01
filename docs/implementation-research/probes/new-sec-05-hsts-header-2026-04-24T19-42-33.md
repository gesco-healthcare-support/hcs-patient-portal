# Probe log: new-sec-05-hsts-header

**Timestamp (local):** 2026-04-24T19-42-33
**Purpose:** Confirm NEW services omit the `Strict-Transport-Security` response header, as claimed in `docs/gap-analysis/10-deep-dive-findings.md` line 93. Probe both HttpApi.Host (44327) and AuthServer (44368) endpoints.

## Command 1 -- HttpApi.Host root

```
curl -skI https://localhost:44327/
```

## Response 1

Status: 302 Found

Headers (verbatim):

```
HTTP/1.1 302 Found
Date: Fri, 24 Apr 2026 19:42:33 GMT
Server: Kestrel
Location: /swagger
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
X-Frame-Options: DENY
```

## Command 2 -- HttpApi.Host Swagger path

```
curl -skI https://localhost:44327/swagger/index.html
```

## Response 2

Status: 404 Not Found (HEAD on this path does not return the Swagger HTML directly; the landing page is reached via GET redirect to `/swagger`). The point of the probe is headers on the response path, not a 200.

Headers (verbatim):

```
HTTP/1.1 404 Not Found
Date: Fri, 24 Apr 2026 19:42:41 GMT
Server: Kestrel
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
X-Frame-Options: DENY
```

## Command 3 -- AuthServer root

```
curl -skI https://localhost:44368/
```

## Response 3

Status: 200 OK

Headers (verbatim):

```
HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
Date: Fri, 24 Apr 2026 19:42:38 GMT
Server: Kestrel
Cache-Control: no-cache, no-store
Pragma: no-cache
Set-Cookie: .AspNetCore.Antiforgery.KRhZ1BVuhfI=<REDACTED>; path=/; samesite=strict; httponly
Set-Cookie: XSRF-TOKEN=<REDACTED>; expires=Mon, 21 Apr 2036 19:42:38 GMT; path=/; secure; samesite=none
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
X-Frame-Options: DENY
X-Correlation-Id: c0225d1b17a34cdf8c27c998808926ac
```

## Command 4 -- AuthServer OIDC discovery

```
curl -skI https://localhost:44368/.well-known/openid-configuration
```

## Response 4

Status: 400 Bad Request (HEAD is not accepted by this endpoint; GET returns 200 per Phase 1.5 probe). Headers still demonstrate the Kestrel / ABP middleware pipeline ran.

Headers (verbatim):

```
HTTP/1.1 400 Bad Request
Content-Length: 171
Content-Type: application/json;charset=UTF-8
Date: Fri, 24 Apr 2026 19:42:44 GMT
Server: Kestrel
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
X-Frame-Options: DENY
X-Correlation-Id: fa5bc4a3a618401e87266c29ada79ce3
```

## Interpretation

All four responses from both NEW services carry `X-Content-Type-Options`, `X-XSS-Protection`, and `X-Frame-Options` (courtesy of `UseAbpSecurityHeaders` + the explicit `Configure<AbpSecurityHeadersOptions>` blocks at `CaseEvaluationHttpApiHostModule.cs:92-95` and `CaseEvaluationAuthServerModule.cs:305-308`). None of the four responses carry `Strict-Transport-Security`. This confirms the track-10 Part 2 NEW-SEC-05 claim (gap-analysis/10-deep-dive-findings.md:92-96): NEW omits HSTS. The fix is to register `AddHsts` in `ConfigureServices` and call `UseHsts()` in the production branch of `OnApplicationInitialization` in both host modules.

Note on the 302/404/400 statuses: the exact status matters less than the headers. ASP.NET Core emits the configured security headers from `UseAbpSecurityHeaders` regardless of response status, and absence of HSTS is consistent across 200 / 302 / 400 / 404 -- ruling out a "HSTS is emitted only on success responses" false explanation.

## Cleanup

Probes were HEAD-only and read no data. No state mutation. No reversal required.
