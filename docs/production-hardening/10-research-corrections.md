# System design research: corrections from source

Checked 2026-08-31 against `feat/production-hardening` at `5c83553c`. This is the repository
verification the research asked for and could not do itself.

**Why this file exists.** The system-design research (exercise 2) had web access but **no repository
access**. Every statement in it about what this code does is inherited from two baseline documents.
Its own handoff says so plainly and asks that all fourteen of its corrections be treated as
hypotheses to check against source. This is that check, for the subset that could be settled by
reading files.

**Scope of what follows.** Everything here is a file read. Nothing here required a running stack, so
nothing here settles the runtime checks - the curl matrix, cross-office tokens, untyped cache writes
and null-tenant jobs remain open and are listed in section 4.

**Convention preserved from the research:** every claim below names the file and line it was checked
against. Where something is inferred rather than observed, it says so.

---

## 1. The tenant boundary holds, for a reason the research ruled out

The handoff's single highest-priority item, and its framing does not apply here.

**What the research argued.** ABP registers five default resolvers; `DomainTenantResolveContributor`
sets `Handled = true` unconditionally whenever a Host is present; therefore the `__tenant` resolvers
are unreachable _if and only if_ the custom contributor is ordered ahead of them. It states:
_"Whether anyone removed them is irrelevant."_ If registered with a bare `.Add(...)`, it lands last
and `?__tenant=` outranks the hostname on every anonymous request.

**What the source says.** Both processes call `Clear()` before registering anything.

`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:402-411`:

```csharp
Configure<AbpTenantResolveOptions>(options =>
{
    options.TenantResolvers.Clear();                                        // :404
    options.TenantResolvers.Add(new CurrentUserTenantResolveContributor()); // :405
    options.TenantResolvers.Add(                                            // :409
        HostAwareDomainTenantResolveContributor.FromConfiguration(configuration));
});
```

`src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:522-539` does the
same: `Clear()` at `:524`, `CurrentUserTenantResolveContributor` at `:525`, the host-aware
contributor at `:537`.

**Conclusion.** The chain is two resolvers in both processes. QueryString, Route, Header and Cookie
are **not present in either**. `?__tenant=`, an `__tenant` route value, header and cookie have no
resolver to read them, and the `MultiTenancyMiddleware` cookie-persistence concern is moot because
there is no cookie resolver to persist into.

**Removal is exactly what happened, and it is what makes ordering irrelevant** - the inverse of the
research's claim. This settles its checks 1, 2 and 3.

**What is still owed, and why this is not "proven" yet.** This is the registration, not a runtime
assertion. ABP applies `Configure<>` callbacks in module dependency order, so the app module's
`Clear()` should run after `AbpAspNetCoreMultiTenancyModule` adds its defaults - but that is an
inference about framework ordering, which is the exact class of reasoning this document exists to
distrust. **The proof is logging the assembled resolver list at startup, or the curl matrix.** That
is APP-OWN-01 and it is routed to phase 3.

---

## 2. Four claims the source contradicts

### 2.1 The contributor does NOT set `Handled` unconditionally

`src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/HostAwareDomainTenantResolveContributor.cs:65-88`.
`context.Handled = true` appears once, at `:86`, **after** a successful slug match. Three paths
abstain without setting it:

| Line     | Condition                                     |
| -------- | --------------------------------------------- |
| `:69-72` | No `HttpContext`, or `!Request.Host.HasValue` |
| `:75-78` | `ExtractSlug` returned null or empty          |
| `:80-83` | Slug equals the reserved `admin` host slug    |

The research's Tier-2 check 17 predicted this correctly and noted it "makes ordering necessary but
not sufficient". **With `Clear()` in place, abstaining reaches no further resolver at all**, so the
request resolves to null tenant - host context - rather than to an attacker-chosen tenant. That is a
materially different failure from the one the research is guarding against, and a much less severe
one.

### 2.2 ANTI-03 is wrong for this codebase: the port is stripped

The research states the matcher "reads the **raw** host value, **which includes the port**", making a
port-shifting hop "a **silent whole-tenant outage rather than an error**", and raises it to an
anti-requirement.

`ExtractSlug` strips the port before matching, at `:98-99`:

```csharp
var colonIndex = host.IndexOf(':');
var hostWithoutPort = colonIndex >= 0 ? host.Substring(0, colonIndex) : host;
```

**A port-shifting hop is safe here.** ANTI-03 should be struck or restated as a property to preserve
rather than a hazard to avoid. Invariant I2 in Document A changes with it.

### 2.3 The contributor DOES validate against the base domain

The research infers it "does **not** validate" the label and returns the leftmost label
unconditionally (its check 19).

`ExtractSlug` at `:110-111` requires the host to match both the prefix and the suffix of the
configured `DomainFormat`, and `:124` rejects any extracted slug containing a dot:

```csharp
return slug.Contains('.', StringComparison.Ordinal) ? null : slug;
```

So a bare IP, or any host not matching `{0}.api.<base>`, yields null and the contributor abstains.
The dot check additionally prevents `a.b.api.<base>` resolving to a slug of `a.b`.

### 2.4 Entity history is ALREADY an allow-list - the "cheapest order of magnitude" may not exist

**This is the correction that most changes the numbers.**

Document A calls capture-at-source the cheapest order of magnitude available and states that every
downstream infrastructure cost is linear in the audit ratio. APP-OWN-07 asks for entity-history
capture to be made an explicit allow-list. Its check 12 says the 259-rows-per-appointment ratio is
wholly attributable to `EntityHistorySelectors.AddAllEntities` plus
`SaveEntityHistoryWhenNavigationChanges`.

Measured across `src/`:

| Searched                                    | Result         |
| ------------------------------------------- | -------------- |
| `EntityHistorySelectors` / `AddAllEntities` | **no matches** |
| `SaveEntityHistoryWhenNavigationChanges`    | **no matches** |
| `AbpAuditingOptions` (any configuration)    | **no matches** |
| Files carrying `[Audited]`                  | **25**         |
| Files carrying `DisableAuditing`            | **0**          |

Capture is already selective, driven by 25 explicitly annotated types. **The saving the research
prices as the cheapest available appears to be largely already taken**, and the audit ratio it
inherits from the brief needs re-deriving from the real table counts before anything is sized off
it.

The absence of `AbpAuditingOptions` also settles check 21 in the research's favour: `HideErrors` is
at its ABP default, so audit-store failures are hidden. Its Area 2 reasoning stands.

---

## 3. Three claims the source confirms

### 3.1 The reverse proxy is the binding blocker on a second instance - CONFIRMED

`docker/nginx-proxy/default.conf.template` uses literal upstream hostnames with no `resolver`
directive: `:45` `proxy_pass http://authserver:8080;`, `:69` `http://api:8080;`, `:149`
`http://angular:80;`. nginx resolves these once at worker start. REQ-APP-16 and Document A's
finding 5 stand as written.

### 3.2 The 443 fallthrough is real - CONFIRMED

`default_server` appears once, at `:27`, and it is on **port 80**. The four 443 blocks are, in file
order: `*.auth.${BASE_DOMAIN}` (`:34/:36`), `*.api.${BASE_DOMAIN}` (`:59/:61`), exact
`minio.${BASE_DOMAIN}` (`:95/:97`), and `*.${BASE_DOMAIN}` (`:139/:141`).

With no `default_server` on 443, an unmatched Host falls through to the first matching block on that
port - the AuthServer. The research's section 4.2 finding holds.

### 3.3 REQ-APP-01's premise is correct, and the keys look unencrypted at rest

DataProtection is configured in both processes and persisted to Redis when a connection exists:
`CaseEvaluationAuthServerModule.cs:384` (`AddDataProtection().SetApplicationName("CaseEvaluation")`,
comment at `:386`), the Redis package at `HealthcareSupport.CaseEvaluation.AuthServer.csproj:42`, and
`CaseEvaluationHttpApiHostModule.cs:101` calling `ConfigureDataProtection` defined at `:1103`.

So the research's concern - key material in a tier that can evict - is well founded.

**And `ProtectKeysWith` does not appear anywhere in `src`.** Specifying a custom persistence location
deregisters the default at-rest protection, so on this evidence the keys are stored in the clear.
That is the research's check 25 and the answer is the bad one. Confirm by reading the
`ConfigureDataProtection` body before acting.

---

## 4. One claim corrected in the research's favour, and what remains open

**Corrected in their favour:** `proxy_set_header Host $host;` is set explicitly on all four locations
(`:47`, `:71`, `:130`, `:151`). REQ-TEN-01 and ANTI-01 are **already satisfied** by the incumbent
proxy rather than being an open risk. The research flagged host rewriting as a default behaviour to
disprove rather than assume; disproven.

**Still open - these need a running stack or deeper reading, and none is settled by this pass:**

| Research check                                        | What it needs                                                                                                                                                                                                                                                |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 3 - can `__tenant` change the resolved tenant         | The curl matrix. Registration says no; assert it                                                                                                                                                                                                             |
| 4 - does an empty `Host` reach the fallback resolvers | `curl -H 'Host;'`. Expect host context, not a chosen tenant                                                                                                                                                                                                  |
| 5 - is a cross-office token refused                   | Mint at office A, present to office B's API. **`CurrentUserTenantResolveContributor` is first, so an authenticated request resolves from the token claim - the hostname is not authoritative.** Whether that is a defect depends on what trusts the hostname |
| 6 - untyped `IDistributedCache` writes                | grep plus keyspace observation                                                                                                                                                                                                                               |
| 7 - null-tenant tenant-scoped jobs                    | Add the guard and run one sweep cycle                                                                                                                                                                                                                        |
| 8-10 - edition, recovery model, disk trend            | Database access                                                                                                                                                                                                                                              |

---

## 5. What this does to the portfolio number

Document A carries 734-1,084 hours of setup and 24.5-43.5 hours a month, and the handoff asks for
that to be taken to the business as a decision.

**Do not take that number to the business yet.** Four of the claims checked here came back wrong in
the direction that makes the system safer than described, and one - the audit ratio in 2.4 - came
back wrong in the direction that **inflates** the cost model, because a saving priced as available
appears already taken. The aggregate inherits both.

Re-derive the audit ratio from real table counts first. That is one query and it moves a figure the
whole portfolio is sized against.

---

## 6. Inputs the research was missing that this repository has

Both are recorded in its own gap list.

- **`02-runtime-and-data-profile.md`** - only three of four input files reached the research. It is
  committed here at [`../devops/RUNTIME-AND-DATA-PROFILE.md`](../devops/RUNTIME-AND-DATA-PROFILE.md).
  Folding it in is an edit, not a rebuild, and it converts several `UNKNOWN` cells in the capacity
  model to `MEASURED`. **The externally-sent copy anonymises the office name to `<office-a>`;
  preserve that on any resend.**
- **The HIPAA safeguard inventory** the brief promised but the delivered file did not contain. It is
  at [`../security/HIPAA-COMPLIANCE.md`](../security/HIPAA-COMPLIANCE.md) and
  [`../security/THREAT-MODEL.md`](../security/THREAT-MODEL.md). Diffing them against Document B
  Group 2 is the research's check 15 and it may retire several requirements.
