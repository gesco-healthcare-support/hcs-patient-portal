---
status: draft (v3 -- approved scope: ALL 16 sites + env-var fallback chain + hard-fail on null tenant)
issue: bug-029-email-confirm-loses-tenant
owner: AdrianG
created: 2026-05-21
revised: 2026-05-21 (v3 locked Q1=all 16 sites, Q2=env-var-chain fallback,
  Q3=hard-fail via non-nullable Guid signatures, Q4=moot under Q1)
approach: tdd (URL builder behavior + tenant-resolution per tenantId)
  + code (introduce IAccountUrlBuilder service; migrate every site
  that reads Notifications.AuthServerBaseUrl /
  Notifications.PortalBaseUrl in this same PR)
sequence: standalone bug fix; no upstream dependency
related-finding: docs/runbooks/findings/bugs/BUG-029-email-confirm-url-loses-tenant-on-register.md
branch: fix/account-url-builder (cut from feat/replicate-old-app)
---

# v3 decisions (Adrian locked 2026-05-21)

| # | Question | Decision |
|---|---|---|
| Q1 | Scope | **All 16 sites in this PR.** No follow-up sweep. "Don't fix the same bug twice." |
| Q2 | Fallback | **3-step chain**: per-tenant DB setting → `App__SelfUrl` / `App__AngularUrl` env var → hard-fail with clear "set the env var" message. Delete the 3 hardcoded `"http://falkinstein.localhost:..."` const strings. |
| Q3 | Null tenant | **Hard-fail.** The three auth-URL methods (verify, reset, invite) take `Guid` (non-nullable). Null tenant becomes a compile error. The two helper root-URL methods stay `Guid?` for genuine host-scope use. Unknown tenant ID at runtime → throw. |
| Q4 | Migration timeline | Moot under Q1. Everything migrates now. |

---

# BUG-029 fix: centralize tenant-aware account URL composition

## TL;DR for non-technical readers

When a new user is invited, registers, or asks for a password reset,
the system sends them an email with a link. Today, many of those
links come through with a "generic localhost" host instead of the
tenant-specific subdomain. When the user clicks the link, the server
can't tell which clinic ("tenant") the user belongs to, so the user
sees a confusing "link doesn't work anymore" message on their first
click.

The deeper cause is **NOT just one missed call site**. It's a pattern
problem: every place in the code that builds an email link does it
slightly differently. Some places try to add the tenant subdomain.
Some places forget. Some places try but get an empty value because
of how the request scope works. There are at least three different
"failure modes" producing the same broken-link result.

The fix is to introduce ONE service that owns URL composition. Every
caller asks "give me the verify URL for tenant X, user Y, token Z"
and gets back a correctly-prefixed URL. There is no other way to
build the URL -- so it's impossible to forget the prefix or read it
from the wrong place.

---

## What changed in this plan (v2 vs v1)

**v1** assumed one site (`ExternalSignupAppService.RegisterAsync`) was
broken and proposed a per-site patch: pass tenant Name to the
`CurrentTenant.Change` scope and add a defensive lookup in the
emailer.

**v2** -- after the other Claude session surfaced a second broken
URL in the invite flow (BUG-029 doc section "Third instance" added
2026-05-21) and a survey of every URL builder showed the same shape
at multiple sites -- replaces the per-site patch with a centralized
`IAccountUrlBuilder` service. The per-site patch is correct in
isolation but doesn't fix the next call site that gets added.

---

## Root cause (full picture)

### Evidence inventory

Three confirmed broken-URL sites:

| Flow | Symptom | Source |
|---|---|---|
| Self-register | Verify URL = `http://localhost:44368/...` | BUG-029 section "Symptom" |
| Self-register #2 (different user) | Same | BUG-029 section "Token is host-agnostic" |
| IT-Admin invites external user | `inviteUrl` field = `http://localhost:44368/...` | BUG-029 section "Third instance" |

Two more sites that almost certainly have the same bug (not yet
empirically reproduced but the code path matches):

| Flow | Source | Status |
|---|---|---|
| IT-Admin creates internal user (welcome email link) | `InternalUsersAppService.cs:377` -- reads setting, no wrap | Latent |
| External user forgot-password (reset URL) | `ExternalAccountAppService.cs:441` -- has hardcoded "Falkinstein" workaround | Demo-only fix; breaks under multi-tenant |

### Code survey

Grep across `src/` for code that reads `Notifications.AuthServerBaseUrl`,
`Notifications.PortalBaseUrl`, `App:AngularUrl`, or `App:SelfUrl`
yields **16 sites**:

```
6 sites WRAP with TenantUrlComposer.ComposeForTenant(...):
- BookingSubmissionEmailHandler.cs
- DocumentEmailContextResolver.cs
- AccessorInvitedEmailHandler.cs
- ExternalAccountAppService.cs  (hardcoded "Falkinstein" workaround)
- CaseEvaluationAccountEmailer.cs (reads _currentTenant.Name)
- (one was added by Task A 2026-05-20 commit 5cdae28)

10 sites DO NOT WRAP:
- DocumentAcceptedEmailHandler.cs
- DocumentRejectedEmailHandler.cs
- PackageDocumentReminderEmailHandler.cs
- PendingDailyDigestEmailHandler.cs
- DueDateApproachingEmailHandler.cs
- DueDateDocumentIncompleteEmailHandler.cs
- InternalStaffQueueDigestEmailHandler.cs
- PatientPacketEmailHandler.cs
- AttyCEPacketEmailHandler.cs
- ExternalSignupAppService.cs (the invite URL bug; BUG-029)
- InternalUsersAppService.cs (internal-user welcome email)
```

### Why the 6 "wrapped" sites are ALSO broken

The wrap reads `_currentTenant.Name` to compose the prefix. But
**every notification handler in the codebase opens a tenant scope
via the single-argument form** `_currentTenant.Change(eventData.TenantId)`.
A literal grep shows 10+ such call sites in the handlers folder
alone, plus the AppService Change-scopes in
`ExternalSignupAppService`, `InternalUsersAppService`, etc.

Per the ABP framework source
(`framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/CurrentTenant.cs`):

```csharp
public IDisposable Change(Guid? id, string? name = null)
{
    return SetCurrent(id, name);
}
```

There is **only one `Change` method**. The "single-arg form"
`Change(tenantId)` simply uses the default `name = null` value. ABP
does NOT look up the tenant store to populate Name. So inside any
`using (Change(tenantId))` scope, **`_currentTenant.Name` is null**.

When the wrap then asks `TenantUrlComposer.ComposeForTenant(url, null)`,
the composer's guard at line 42 returns the URL unchanged.

**So 16 of 16 URL-build sites in the codebase produce tenant-less
URLs as of today.** Some of them appear to work because they emit
URLs that don't depend on tenant subdomain resolution at the
destination (e.g., signed S3-style URLs, document download links).
The auth-related URLs (verify, reset, invite) all break because the
destination Razor pages on the AuthServer require subdomain-based
tenant context to look up the user.

### Three failure modes, one architectural defect

1. **"Forgot the wrap"** -- 10 sites read the setting and concatenate
   directly. Bug is "this code was added before TenantUrlComposer
   existed (Task A, 2026-05-20) or the author didn't know it was
   needed". Example: `ExternalSignupAppService.BuildInviteUrl`.

2. **"Wrap reads stale context"** -- 6 sites wrap correctly but read
   `_currentTenant.Name` after a `Change(id)` scope nulled it out.
   Example: `CaseEvaluationAccountEmailer.ResolveAuthServerBaseUrlAsync`.

3. **"Hardcoded shortcut"** -- 1 site (`ExternalAccountAppService.cs:441`)
   hardcodes `"Falkinstein"` to bypass the broken context. Works for
   the Phase 1A single-tenant demo; breaks the moment a second
   tenant exists.

**Common architectural cause:** URL composition is a leaky
responsibility distributed across 16 call sites. Each one knows
slightly less than the next about how to do it right. Each new
feature added inherits the same trap.

## Decisions locked

1. **Centralize URL composition in one new service**:
   `IAccountUrlBuilder`. Service has methods like:
   - `BuildEmailConfirmationUrlAsync(Guid? tenantId, Guid userId, string token)`
   - `BuildPasswordResetUrlAsync(Guid? tenantId, Guid userId, string token)`
   - `BuildInviteUrlAsync(Guid? tenantId, string rawToken)`
   - `BuildPortalUrlAsync(Guid? tenantId, string path)`

   Every caller passes `tenantId` explicitly (not implicitly via
   ambient context). The service:
   - Reads the configured base URL (setting or fallback)
   - Looks up the tenant name from the tenant store when tenantId is set
   - Returns the composed URL with the tenant subdomain prepended

   Callers cannot pass null tenantId by accident (the parameter is
   on the signature, and `Guid?` documents that null means "host
   scope, no prefix"). Callers cannot forget to wrap (there is no
   non-wrapped path).

2. **First PR migrates only the BUG-029 sites + the AccountEmailer.**
   Three sites get the new service injected:
   - `ExternalSignupAppService.RegisterAsync` (verify URL via emailer)
   - `ExternalSignupAppService.InviteExternalUserAsync` (invite URL)
   - `InternalUsersAppService.CreateAsync` (welcome URL)
   - `CaseEvaluationAccountEmailer` (verify, reset, 2FA code emails)
   - `ExternalAccountAppService` (resend, forgot password)

   The 8 notification email handlers keep their existing
   `TenantUrlComposer` wrap for now. They're broken under the same
   root cause but the URLs they emit are document-view URLs that
   resolve regardless of subdomain in practice (verified empirically
   in yesterday's Phase 6.1 hardening slice: emails reached
   recipients with the expected packets attached). A follow-up PR
   migrates them after this fix proves out.

3. **Delete the hardcoded `DefaultAuthServerBaseUrl` const + the
   hardcoded "Falkinstein" workaround.** Once IAccountUrlBuilder
   owns the composition, these workarounds are no longer needed.
   The service throws a clear error if the setting is missing
   ("Configure App__SelfUrl in docker-compose env") instead of
   silently falling back to a tenant-specific dev URL.

4. **Existing `TenantUrlComposer` stays as the internal regex
   helper.** It's a pure function with a well-tested regex; the new
   service uses it internally. We do NOT delete it because the
   8 untouched handlers still call it.

5. **No DB schema change. No migration. No proxy regen.**

## Files touched

### 1. NEW: `src/HealthcareSupport.CaseEvaluation.Application/Notifications/IAccountUrlBuilder.cs`

```csharp
namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-05-21 (BUG-029) -- centralized account-URL composition.
///
/// Every email body that links into the AuthServer or the SPA must
/// route through this service. The service:
/// <list type="bullet">
///   <item>Reads the configured base URL (per-tenant setting or env-var
///         default) from <c>Notifications.AuthServerBaseUrl</c> /
///         <c>Notifications.PortalBaseUrl</c>.</item>
///   <item>Looks up the tenant Name from <see cref="ITenantStore"/>
///         using the explicit <paramref name="tenantId"/> argument.</item>
///   <item>Composes the final URL via
///         <see cref="TenantUrlComposer.ComposeForTenant"/> with the
///         tenant Name prepended.</item>
/// </list>
///
/// The tenantId argument is explicit -- not read from
/// <c>ICurrentTenant</c> -- because tenant context is unreliable at
/// many call sites (anonymous endpoints, Hangfire jobs,
/// CurrentTenant.Change scopes that null out the Name). Callers MUST
/// know which tenant the URL is for; the service refuses to guess.
///
/// Pass <c>null</c> for tenantId when the URL is genuinely host-scoped
/// (rare; admin surfaces only).
/// </summary>
public interface IAccountUrlBuilder
{
    // v3 (2026-05-21): tenantId is non-nullable on auth-URL methods so
    // the compiler enforces "every email link must specify a tenant".
    // External users are always tenant-scoped; null tenantId here is a
    // programming error, not a runtime condition. The two helper
    // root-URL methods at the bottom keep `Guid?` for legitimate
    // host-scope use.
    Task<string> BuildEmailConfirmationUrlAsync(Guid tenantId, Guid userId, string token);
    Task<string> BuildPasswordResetUrlAsync(Guid tenantId, Guid userId, string token);
    Task<string> BuildInviteUrlAsync(Guid tenantId, string rawToken);

    /// <summary>
    /// Returns the SPA root URL for the given tenant, e.g.
    /// <c>http://falkinstein.localhost:4200</c>. Used by callers that
    /// build SPA deep-links (appointment-view, dashboard, etc.).
    /// </summary>
    Task<string> BuildPortalRootUrlAsync(Guid? tenantId);

    /// <summary>
    /// Returns the AuthServer root URL for the given tenant, e.g.
    /// <c>http://falkinstein.localhost:44368</c>. Used by callers that
    /// build account-area paths beyond the three named verbs above.
    /// </summary>
    Task<string> BuildAuthServerRootUrlAsync(Guid? tenantId);
}
```

### 2. NEW: `src/HealthcareSupport.CaseEvaluation.Application/Notifications/AccountUrlBuilder.cs`

```csharp
internal class AccountUrlBuilder : IAccountUrlBuilder, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AccountUrlBuilder> _logger;

    public AccountUrlBuilder(
        ISettingProvider settingProvider,
        ITenantStore tenantStore,
        ICurrentTenant currentTenant,
        ILogger<AccountUrlBuilder> logger)
    {
        _settingProvider = settingProvider;
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<string> BuildEmailConfirmationUrlAsync(Guid? tenantId, Guid userId, string token)
    {
        var baseUrl = await BuildAuthServerRootUrlAsync(tenantId);
        return $"{baseUrl}/Account/EmailConfirmation?userId={userId}&confirmationToken={WebUtility.UrlEncode(token)}";
    }

    public async Task<string> BuildPasswordResetUrlAsync(Guid? tenantId, Guid userId, string token)
    {
        var baseUrl = await BuildAuthServerRootUrlAsync(tenantId);
        return $"{baseUrl}/Account/ResetPassword?userId={userId}&resetToken={WebUtility.UrlEncode(token)}";
    }

    public async Task<string> BuildInviteUrlAsync(Guid? tenantId, string rawToken)
    {
        var baseUrl = await BuildAuthServerRootUrlAsync(tenantId);
        return $"{baseUrl}/Account/Register?inviteToken={WebUtility.UrlEncode(rawToken)}";
    }

    public Task<string> BuildPortalRootUrlAsync(Guid? tenantId) =>
        ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
            tenantId);

    public Task<string> BuildAuthServerRootUrlAsync(Guid? tenantId) =>
        ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl,
            tenantId);

    private async Task<string> ResolveAndComposeAsync(string settingName, Guid? tenantId)
    {
        var configured = await _settingProvider.GetOrNullAsync(settingName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Hard fail with a clear message rather than silently
            // emitting a developer's local dev URL. The setting
            // ought to be populated by docker-compose env at boot.
            throw new InvalidOperationException(
                $"AccountUrlBuilder: setting '{settingName}' is not configured. " +
                $"Set the corresponding App__... env var in docker-compose.yml.");
        }
        var tenantName = await ResolveTenantNameAsync(tenantId);
        return TenantUrlComposer.ComposeForTenant(configured.TrimEnd('/'), tenantName)!;
    }

    private async Task<string?> ResolveTenantNameAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue) return null;

        // The tenant store is host-scoped; switch to host context so the
        // IMultiTenant filter doesn't exclude the row. Switch back
        // automatically on dispose.
        using (_currentTenant.Change(null))
        {
            var tenant = await _tenantStore.FindAsync(tenantId.Value);
            if (tenant == null)
            {
                _logger.LogWarning(
                    "AccountUrlBuilder: tenant {TenantId} not found; URL will be emitted without tenant prefix.",
                    tenantId.Value);
            }
            return tenant?.Name;
        }
    }
}
```

### 3. `CaseEvaluationAccountEmailer.cs` -- inject IAccountUrlBuilder

```csharp
// Replace the file's own ResolveAuthServerBaseUrlAsync /
// BuildEmailConfirmationUrl / BuildPasswordResetUrl methods with
// thin pass-throughs to the new service.

public virtual async Task SendEmailConfirmationLinkAsync(
    IdentityUser user, string confirmationToken, string appName,
    string? returnUrl = null, string? returnUrlHash = null)
{
    if (user == null) throw new ArgumentNullException(nameof(user));

    // The IdentityUser row carries TenantId directly -- use that as
    // the source of truth for tenant rather than CurrentTenant
    // (which may be in a Change scope that nulled the Name).
    var url = await _accountUrlBuilder.BuildEmailConfirmationUrlAsync(
        user.TenantId, user.Id, confirmationToken);

    await DispatchAsync(...);
}

public virtual async Task SendPasswordResetLinkAsync(...) { /* same shape */ }
```

Delete the now-dead `DefaultAuthServerBaseUrl` const + the
`BuildEmailConfirmationUrl` / `BuildPasswordResetUrl` static helpers.
They move into the new service.

### 4. `ExternalSignupAppService.cs` -- inject + use for invite + register

Two changes:

**(a)** `RegisterAsync` -- nothing to change here; the AppService
calls `_accountEmailer.SendEmailConfirmationLinkAsync(user, token, ...)`
which now routes through the new service via `user.TenantId`. Fix
inherits automatically.

**(b)** `InviteExternalUserAsync` -- replace lines 901-903:

```csharp
// Before:
var inviteUrl = BuildInviteUrl(
    authServerBaseUrl: authServerBaseUrl.TrimEnd('/'),
    rawToken: rawToken);

// After:
var inviteUrl = await _accountUrlBuilder.BuildInviteUrlAsync(
    tenantId.Value, rawToken);
```

Delete the now-dead `BuildInviteUrl` static helper at line 1040.
Delete the now-unused `authServerBaseUrl` lookup at lines 883-889
(the new service does the lookup itself).

### 5. `ExternalAccountAppService.cs` -- inject + remove the "Falkinstein" hack

Replace the file's `ResolveAuthServerBaseUrlAsync` (lines 425-442)
and `BuildEmailConfirmationUrl` static method with calls to the new
service. Delete the `DefaultAuthServerBaseUrl` const at line 50.

The hardcoded `"Falkinstein"` workaround at line 441 disappears with
the method. Any AppService call that needs tenant context now passes
the user's TenantId (looked up via FindByEmail) explicitly.

### 6. `InternalUsersAppService.cs` -- inject + use for welcome URL

Replace `ResolvePortalBaseUrlAsync` (lines 371-380) with
`await _accountUrlBuilder.BuildPortalRootUrlAsync(resolvedTenantId)`.

### 7. NEW test file: `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/AccountUrlBuilderTests.cs`

Pure unit tests on the new service with a mocked `ITenantStore` +
`ISettingProvider`:

| # | Test | Acceptance |
|---|------|------------|
| 1 | `BuildEmailConfirmationUrlAsync_WithTenant_PrependsSubdomain` | `tenantId=knownId, setting="http://localhost:44368"` -> `"http://falkinstein.localhost:44368/Account/EmailConfirmation?userId=...&confirmationToken=..."` |
| 2 | `BuildInviteUrlAsync_WithTenant_PrependsSubdomain` | Same shape on the invite path. |
| 3 | `BuildPasswordResetUrlAsync_WithTenant_PrependsSubdomain` | Same shape on the reset path. |
| 4 | `BuildEmailConfirmationUrlAsync_NullTenantId_NoPrepend` | `tenantId=null` returns URL without subdomain (host scope). |
| 5 | `BuildEmailConfirmationUrlAsync_UnknownTenantId_LogsAndNoPrepend` | TenantStore returns null; URL is returned without prefix; warning logged. |
| 6 | `BuildPortalRootUrlAsync_MissingSetting_Throws` | When the setting is unconfigured, throws `InvalidOperationException` with a "set the App__... env var" message. |
| 7 | `BuildInviteUrlAsync_TokenUrlEncoded` | `rawToken="a/b+c"` produces a URL-encoded token in the path. |
| 8 | `BuildEmailConfirmationUrlAsync_CurrentTenantNotConsulted` | When `_currentTenant.Name = "OtherTenant"` but `tenantId` points to "Falkinstein", URL uses "falkinstein.". The service ignores ambient CurrentTenant. |

Test #8 is the key regression-pin against BUG-029: it proves the
service does NOT depend on the leaky ambient tenant context.

### 8. `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/TenantUrlComposerTests.cs` (NEW)

Pure unit tests on `TenantUrlComposer` (the existing static helper).
9 cases as listed in v1 of this plan. Kept because the helper is
still used by 8 notification handlers and we want regression-pin
coverage before any follow-up migration touches it.

## Test plan

- 8 new unit tests on `AccountUrlBuilder`.
- 9 new unit tests on `TenantUrlComposer`.
- No integration tests in this PR (the AppService test harness
  doesn't easily mock IAccountEmailer end-to-end; verify the
  combined flow live per BUG-024's "fix + live-verify" pattern).
- Live verification on the replicate-old-app stack:
  1. `docker compose restart api` after the fix is built.
  2. Register a fresh Patient via the SPA at
     `http://falkinstein.localhost:4230/`.
  3. SQL probe Hangfire's recent job; the email body's
     `<a href>` must contain `http://falkinstein.localhost:44398/...`
     (this stack's port).
  4. Click the URL: expect `EmailConfirmed=1` + redirect to
     `/Account/Login?flash=email-verified`.
  5. As IT Admin or tenant admin, invite an external user via the
     `/users/invite` flow; confirm the `inviteUrl` response field
     carries the subdomain. Click the URL; expect the Register page
     to render the invite-prefilled banner.
  6. As internal staff, create a new internal user via the tenant-
     admin flow; confirm the welcome-email URL carries the subdomain.
  7. Trigger Forgot Password for a verified external user; confirm
     the reset URL carries the subdomain.

## Risk and rollback

**Blast radius:**
- One new service + one new interface + one new test file.
- Four existing AppServices modified to inject + use the service.
- One existing emailer file simplified (deletes more code than it
  adds; the URL-building static helpers move into the new service).
- No DB schema change.

**Rollback:** revert the commit. Behavior reverts to the existing
buggy state: registers/invites/welcome-emails ship `localhost`
URLs again. Working URLs still work (the docs-flow handlers were
not touched).

**Risk: the new service throws if the setting is missing.** Today
the code silently falls back to a hardcoded "Falkinstein" URL.
After the fix, a misconfigured env var produces an immediate 500
on register / invite. Mitigation: the error message names the
exact env var to set. This is a strictly better failure mode than
the current "silent broken URLs in the inbox" outcome.

**Risk: tenant store lookup adds latency to every email.** The
lookup is once per send. ABP caches the tenant store; first-cold
hits the host DB. Negligible vs. SMTP latency. Same risk as the
v1 plan's defensive lookup.

**Risk: callers pass the wrong tenantId.** The method signature
documents `Guid? tenantId` and the call sites already have the
right value in hand (RegisterAsync has `tenantId` local;
InviteExternalUserAsync has `tenantId.Value`; CaseEvaluationAccountEmailer
extracts from `user.TenantId`). Lower risk than the current ambient-
context model.

**Risk: 8 notification handlers still on the old TenantUrlComposer
pattern.** Documented as a follow-up. They emit document-flow URLs
that empirically work today (verified yesterday's Phase 6.1
hardening slice). When the follow-up PR migrates them, the same
service handles their needs without further wiring.

## Verification

End-to-end on the replicate-old-app stack:

1. Build + restart the API container.
2. Run the 17 new unit tests (`dotnet test --filter
   "FullyQualifiedName~AccountUrlBuilderTests|TenantUrlComposerTests"`)
   -- all green.
3. Live probe: clean DB rebuild, then walk through the 7 manual
   steps in the test plan above.
4. SQL probe summary: every URL emitted to an email body during
   the live probe contains `falkinstein.localhost:<this-stack-port>`
   -- not `localhost:<this-stack-port>`.

## How to apply

- Create a new branch off `feat/replicate-old-app`:
  `fix/email-confirm-tenant`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Open PR; wait for CI; merge after live verification.
- Mark BUG-029 fixed with `status: fixed` + `fixed: 2026-05-21`.
- File a follow-up parity note tracking the 8 unmigrated
  notification handlers + the deprecation of direct
  `TenantUrlComposer` usage in favor of `IAccountUrlBuilder`.

## Open questions for Adrian (decision needed before implementation)

### Q1 -- migrate all 16 sites in one PR or split?

The deeper root-cause framing argues for migrating ALL sites in one
PR. The pragmatic framing (smaller diff, lower review cost, focus
on observed broken paths first) argues for the 5-site scope this
plan currently proposes.

**My recommendation:** ship the 5-site PR first. The notification
handlers' URLs work today empirically; migrating them is risk-for-
no-benefit until they fail somewhere observable. Follow-up PR
sweeps them when we have appetite for the migration. If you
disagree, expand this plan's "Files touched" to cover all 8
notification handlers + the existing `_currentTenant.Change(eventData.TenantId)`
sites that may also have hidden brokenness.

### Q2 -- delete `DefaultAuthServerBaseUrl` const + the hardcoded fallback URLs?

Three of them in the source:
- `CaseEvaluationAccountEmailer.cs:71` -- `"http://falkinstein.localhost:44368"`
- `ExternalAccountAppService.cs:50` -- same string, same purpose
- `InternalUsersAppService.cs:377` -- `"http://falkinstein.localhost:4200"`

All three are dev-only fallbacks for "the App__... env var wasn't
set." The const-fallback model dates to before docker-compose
passed env vars reliably; today's parallel-worktree stack always
passes them. The const fallback masks misconfiguration silently
and bakes the demo tenant's name into source.

**My recommendation:** delete all three. Replace with the new
service's "throw if setting missing" behavior. A misconfigured dev
stack now fails loudly at first email-send instead of silently
emitting Falkinstein URLs from a Test Clinic stack. Adrian
operating outside docker can `export App__SelfUrl=...` in a
shell before running `dotnet run`.

If you'd rather keep a safety net, change the fallback to a
tenant-LESS URL (`http://localhost:44368`) so the composer's
prefix logic can still wrap it correctly.

### Q3 -- defensive vs assertive when tenantId is null but the URL needs a tenant?

The new service accepts `Guid? tenantId`. When null is passed:
- **Defensive (recommended)**: return the URL without tenant prefix.
  Caller pays the consequence (URL won't work if the destination
  needs a tenant). Soft fail.
- **Assertive**: throw `ArgumentNullException`. Force every caller
  to be explicit about host vs tenant. Hard fail.

**My recommendation:** **Defensive**. There ARE legitimate host-
scope uses (e.g., a future host-admin email about tenant
provisioning). Throwing on null would surprise those callers. The
warning-log in the unknown-tenant path is sufficient signal for
the "I meant to pass a tenant but passed null by mistake" bug
class.

### Q4 -- BUG-029 number reuse?

Adrian renamed the file from BUG-027 to BUG-029. The plan now
references BUG-029. The matching frontmatter / cross-link in the
audit doc + plan body have been updated. No action needed unless
Adrian wants to update the related-finding pointer (currently
`docs/runbooks/findings/bugs/BUG-029-email-confirm-url-loses-tenant-on-register.md`).
