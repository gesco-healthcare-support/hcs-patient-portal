---
id: BUG-028
title: AuthServer WildcardDomainsFormat hardcoded to main-stack ports; offset-port stack login is broken
severity: high (blocks login on the parallel-worktree stack)
status: fixed
fixed: 2026-05-22 (verified via live probes after fix shipped in c73d789)
found: 2026-05-20 (hardening-test slice on `replicate-old-app` stack)
flow: authentication / OpenIddict authorization-code redirect validation
component: src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:103-111
related:
  - BUG-016 (original wildcard fix; closed-by-redesign 2026-05-19)
  - Task A (5cdae28) -- per-stack backend URL config; missed this file
  - Task B (76348fd) -- per-stack SPA dynamic-env; covered SPA side
  - Task D (39c4c33) -- parallel-stack verification; did not exercise SPA login
  - c73d789 (the Option A fix below; landed on feat/replicate-old-app, since merged to main via PR #222)
---

# BUG-028 - WildcardDomainsFormat hardcoded; replicate-old-app login broken

## Symptom

Cold `docker compose up -d --build` on the `replicate-old-app` worktree
(offset ports 4230 / 44398 / 44357). All containers healthy. Health
endpoints respond. dynamic-env.json correctly references only this
stack's ports.

Then navigate `http://falkinstein.localhost:4230/` in a browser. The SPA
correctly redirects to `http://falkinstein.localhost:44398/connect/authorize`
with `redirect_uri=http%3A%2F%2Ffalkinstein.localhost%3A4230`.

OpenIddict rejects with:

```
error: invalid_request
error_description: The specified 'redirect_uri' is not valid for this client application.
error_uri: https://documentation.openiddict.com/errors/ID2043
```

## Root cause

`src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:103-111`:

```csharp
PreConfigure<AbpOpenIddictWildcardDomainOptions>(options =>
{
    options.EnableWildcardDomainSupport = true;
    options.WildcardDomainsFormat.Add("http://{0}.localhost:4200");
    options.WildcardDomainsFormat.Add("http://{0}.localhost:44368");
    options.WildcardDomainsFormat.Add("http://{0}.localhost:44327");
});
```

The three wildcard formats are hardcoded to the MAIN stack's canonical
ports. The replicate-old-app stack runs on offset ports
(`4230 / 44398 / 44357`) but its AuthServer container's `WildcardDomainsFormat`
list still only matches main-stack URLs.

When a tenant-subdomain SPA on `:4230` sends a redirect_uri matching
`http://falkinstein.localhost:4230`, none of the three registered
wildcard patterns match (they all bind to `:4200`), so OpenIddict
falls back to literal-match against `RedirectUris` in the
`OpenIddictApplications` table -- which DBMigrator wrote as
`["http://localhost:4230"]` (correct port, but no subdomain). The
subdomain-version requires the wildcard pattern.

Result: every login attempt on the replicate-old-app stack fails with
`invalid_request` at the authorize endpoint. The user can't get
anywhere -- not register, not log in, not book.

## Why Tasks A/B/D missed this

- **Task A (5cdae28)** parameterized SPA/backend URL config via env
  vars (`App__SelfUrl`, `App__AngularUrl`, `App__CorsOrigins`, etc.)
  but did not touch the wildcard format list.
- **Task B (76348fd)** moved SPA runtime config to `dynamic-env.json`
  loaded per stack; doesn't reach the AuthServer's OpenIddict
  configuration.
- **Task D (39c4c33)** verified the parallel-stack docker-compose
  paths via API health probes but did not exercise SPA-based login,
  so this gap stayed hidden.
- **BUG-016** was the previous incarnation (the entire wildcard
  support was missing pre-2026-05-19). It closed by adding the
  hardcoded format strings. Future-proofing the format list against
  parallel-stack ports was outside that PR's scope.

## Fix

Two options, in increasing scope:

### Option A (minimal -- fix the regression now)

Read the format list from env vars in
`CaseEvaluationAuthServerModule.PreConfigureServices`. Add to the
`docker-compose.yml` env block for the AuthServer service:

```yaml
App__WildcardDomainsFormat__0: "http://{0}.localhost:${NG_PORT:-4200}"
App__WildcardDomainsFormat__1: "http://{0}.localhost:${AUTH_PORT:-44368}"
App__WildcardDomainsFormat__2: "http://{0}.localhost:${API_PORT:-44327}"
```

And in the AuthServer module:

```csharp
PreConfigure<AbpOpenIddictWildcardDomainOptions>(options =>
{
    options.EnableWildcardDomainSupport = true;
    var configured = configuration
        .GetSection("App:WildcardDomainsFormat")
        .Get<string[]>();
    if (configured?.Length > 0)
    {
        foreach (var format in configured)
        {
            options.WildcardDomainsFormat.Add(format);
        }
    }
    else
    {
        // Dev fallback (when running outside docker on canonical ports)
        options.WildcardDomainsFormat.Add("http://{0}.localhost:4200");
        options.WildcardDomainsFormat.Add("http://{0}.localhost:44368");
        options.WildcardDomainsFormat.Add("http://{0}.localhost:44327");
    }
});
```

### Option B (better -- factor out the URL-suite resolver)

Tasks A/B introduced a per-stack URL contract that SPA + AuthServer +
API all need to share. The wildcard format list is the third
consumer that needs it; future features (e.g., signed-link
generation, email body construction) will need it too.

Build a single typed config object (e.g., `StackUrlOptions`) read once
from `App:...` config and referenced everywhere a stack-aware URL
is composed. The wildcard format becomes one consumer of it.

Out of scope for this fix; document as the next iteration target.

## Workaround (for the hardening slice today)

Until this lands:
- Run the hardening test slice on the MAIN stack (canonical ports).
  No login regression there because the hardcoded values match.
- The replicate-old-app stack remains operational for backend probes
  (curl auth healthcheck, curl API swagger) but the SPA-based flows
  are blocked.

## Audit / impact log

The hardening-test-suite slice for this stack stops at Phase 0 because
all subsequent phases require SPA login (register patient, book, approve).
This finding is the blocker for Phase 1.A.1 and beyond on
`replicate-old-app`.

## Verification (when fix lands)

1. `docker compose down && docker compose up -d --build` on
   replicate-old-app worktree.
2. Navigate `http://falkinstein.localhost:4230/` -- expect the
   AuthServer login page (NOT an `invalid_request` error).
3. SQL probe to confirm config propagation:
   ```sh
   docker exec replicate-old-app-authserver-1 sh -c \
     'echo "WildcardDomainsFormat__0=$App__WildcardDomainsFormat__0"; \
      echo "WildcardDomainsFormat__1=$App__WildcardDomainsFormat__1"; \
      echo "WildcardDomainsFormat__2=$App__WildcardDomainsFormat__2"'
   ```
   Expect format strings carrying `4230 / 44398 / 44357`.
4. Same probe on main stack: expect `4200 / 44368 / 44327`.
5. Run the full hardening slice (Phase 0 through 6.1) on both stacks
   in parallel. Both should reach `Phase 6.1 PASS`.

## Fix verified (2026-05-22)

Fix shipped in commit `c73d789 fix(auth-server): drive OpenIddict
wildcard formats from config` (Option A above), landed on
`feat/replicate-old-app` and reached `main` via PR #222.

`CaseEvaluationAuthServerModule.PreConfigureServices` now reads the
wildcard format list from the `App:WildcardDomainsFormat` config
section, falling back to the canonical-port hardcoded list when the
section is empty (dev/canonical-stack convenience).
`docker-compose.yml` passes `${NG_PORT}` / `${AUTH_PORT}` / `${API_PORT}`
through as `App__WildcardDomainsFormat__0..2`, so each worktree's
`.env` controls its own format list.

### Live verification (replicate-old-app stack)

| Probe | Expected per the symptom above | Actual on 2026-05-22 |
|---|---|---|
| `GET /connect/authorize?...&redirect_uri=http%3A%2F%2Ffalkinstein.localhost%3A4230` (bare) | 400 invalid_request ID2043 | **302 redirect (accepted)** |
| Same with PKCE (real SPA shape) | 400 invalid_request ID2043 | **302 redirect (accepted)** |
| Counter-probe: `redirect_uri=:4200` on replicate AuthServer | (sanity) 400 invalid_request | **400 invalid_request ID2043** (correctly rejected -- wildcard now scoped to 4230/44398/44357) |
| Follow the 302 chain | (n/a) | Lands on `/Account/Login?ReturnUrl=...` with `<title>Sign in &#124; Appointment Portal</title>` |
| `docker exec ... env &#124; grep WildcardDomainsFormat` | env vars present | All three present with offset-port values |
| Perl byte-grep AuthServer DLL for `App:WildcardDomainsFormat` | (n/a) | UTF-16 hit, count=1 (new code path in running binary) |

End-to-end positives captured earlier on the same stack:
- Full OIDC code-flow + PKCE login as `admin@falkinstein.test`
  succeeded (token issued, invite endpoint reachable, JSON returned).
- Email-confirmation click-through via Chrome DevTools landed at
  `/Account/Login?flash=email-verified` -- another end-to-end positive
  through the same redirect-URI validation handler.

### Match-algorithm cross-check (from upstream ABP source)

`Volo.Abp.OpenIddict.WildcardDomains.AbpValidateClientRedirectUri.HandleAsync`
calls `CheckWildcardDomainAsync(context.RedirectUri)`, which iterates
`WildcardDomainsFormat`, builds each pattern via
`domainFormat.Replace("{0}", "*")`, then calls
`UrlHelpers.IsSubdomainOf(url, pattern)`:

```csharp
return subdomain.IsAbsoluteUri
       && domain.IsAbsoluteUri
       && subdomain.Scheme == domain.Scheme
       && subdomain.Port == domain.Port
       && subdomain.Host.EndsWith($".{domain.Host}", StringComparison.Ordinal);
```

Ports must be exactly equal -- which is what the original bug was, and
what the config-driven format list now correctly satisfies on both
stacks (canonical and offset).

### Follow-up (out of scope here)

The doc above describes "Option B" (a typed `StackUrlOptions` resolver
shared by SPA + AuthServer + API + email URL builder). Not in scope for
this close-out. Capture as a future iteration if/when a fourth consumer
of the per-stack URL contract appears.
