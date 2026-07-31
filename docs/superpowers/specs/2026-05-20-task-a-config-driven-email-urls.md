---
title: "Task A: Config-Driven Email URLs (BUG-014 Fix)"
date: 2026-05-20
status: draft
branch: feat/parallel-worktree-stacks
task-of: parallel-worktree-stacks (A of 6)
closes:
  - BUG-014
related:
  - OBS-9 (will be reversed once Task B also lands)
  - BUG-015 (sibling Task B)
---

# Task A: Config-Driven Email URLs (BUG-014 Fix)

Single-PR plan: this task is one of six commits on `feat/parallel-worktree-stacks`.
All six tasks ship together as one PR to `main` at the end. Per-task smoke tests
are verification gates, not merge gates.

## 1. Problem

Every outgoing email bakes URLs as `http://falkinstein.localhost:4200/...` and
`http://falkinstein.localhost:44368/...` regardless of which Docker stack
generated them. Source of the literal: `CaseEvaluationSettingDefinitionProvider.cs:43,53`
declares the SettingDefinition default as those exact strings. The `_settingProvider
.GetOrNullAsync(...)` call returns the literal default whenever no per-tenant
override exists (and Phase 1A has no per-tenant override seeded). The four
`Default*` consts that look like a fallback safety net never fire because the
SettingDefinition default is non-null.

Effect on parallel Docker stacks: emails from `replicate-old-app`'s stack (on
offset ports) point users at `main`'s URLs (canonical ports). Cross-stack
hijack on every email-driven flow (registration verification, password reset,
packet ready, booking confirmation, document accepted/rejected, reminder
digests). This is one of the two technical blockers cited in `OBS-9.md:18`
for the abandoned 2026-05-14 parallel-stack attempt.

## 2. Solution -- two layers

### Layer 1: Config override (eliminates the source literal at runtime)

**Mechanism (revised 2026-05-20 after T4 smoke FAIL):** Inject `IConfiguration`
into `CaseEvaluationSettingDefinitionProvider` and source the SettingDefinition
defaults from `App:AngularUrl` / `AuthServer:Authority`. Both are already
env-var-driven in `docker-compose.yml` (`App__AngularUrl`,
`AuthServer__Authority`). Add `App__AngularUrl: "http://localhost:${NG_PORT:-4200}"`
to AuthServer + api blocks.

```csharp
// CaseEvaluationSettingDefinitionProvider.cs (new constructor + read)
public CaseEvaluationSettingDefinitionProvider(IConfiguration configuration)
{
    _configuration = configuration;
}

// In Define(...):
var portalDefault = _configuration["App:AngularUrl"]?.TrimEnd('/')
    ?? "http://falkinstein.localhost:4200";
Define(context, PortalBaseUrl, defaultValue: portalDefault);

var authServerDefault = _configuration["AuthServer:Authority"]?.TrimEnd('/')
    ?? "http://falkinstein.localhost:44368";
Define(context, AuthServerBaseUrl, defaultValue: authServerDefault);
```

Tenant-less values; subdomain composed in Layer 2. Literal fallback retains
the 2026-05-06 Falkinstein-targeted behavior when `App:AngularUrl` is absent
(non-Docker dev paths).

**Why NOT the initially-proposed `Settings:` config-prefix mechanism:** ABP's
`ConfigurationSettingValueProvider` looks up `Configuration[$"Settings:{settingName}"]`
as a flat key (verified via ABP source at
`framework/src/Volo.Abp.Settings/Volo/Abp/Settings/ConfigurationSettingValueProvider.cs`).
Setting names contain literal dots
(`CaseEvaluation.Notifications.PortalBaseUrl`), so to override via env var,
the env-var name itself must contain those literal dots
(`Settings__CaseEvaluation.Notifications.PortalBaseUrl`). Docker silently
drops env vars whose names contain dots (POSIX env-var-name restriction).
Confirmed empirically by T4 smoke test 2026-05-20: with the dot-form var
declared in compose, `docker exec ... env` inside the container showed it
ABSENT. The `Settings:Abp.Mailing.Smtp.*` precedent works in
`appsettings.json` only because JSON keys can hold dots; the env-var route
cannot. The IConfiguration-injection mechanism above is BUG-014.md's
original recommendation; my "smaller alternative" did not survive the
empirical smoke test.

### Layer 2: Tenant subdomain composer (eliminates the tenant literal)

New file `src/HealthcareSupport.CaseEvaluation.Application/Notifications/TenantUrlComposer.cs`
(~20 lines):

```csharp
using System;
using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.Notifications;

internal static class TenantUrlComposer
{
    // Matches the bare "localhost" host token in a URL: at start-of-string or
    // after "//", followed by ":" / "/" / end-of-string. Lifted byte-for-byte
    // from angular/src/tenant-bootstrap.ts:99 so the frontend (subdomain
    // bootstrap) and backend (email URL rendering) share one substitution rule.
    private static readonly Regex LocalhostHost = new(
        @"(^|//)localhost(?=([:/]|$))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Rewrites bare-localhost URLs to <tenantName>.localhost. Idempotent:
    /// URLs already carrying a subdomain pass through unchanged. Null or empty
    /// tenant name returns the input as-is (host-scope flows).
    /// </summary>
    public static string? ComposeForTenant(string? baseUrl, string? tenantName)
    {
        if (string.IsNullOrEmpty(baseUrl)) return baseUrl;
        if (string.IsNullOrEmpty(tenantName)) return baseUrl;
        return LocalhostHost.Replace(baseUrl, $"$1{tenantName!.ToLowerInvariant()}.localhost");
    }
}
```

Then wrap seven existing resolve sites in five files. Pattern (one-liner per
site, `ICurrentTenant _currentTenant` already injected at every site,
verified):

```csharp
// Before
var url = await _settingProvider.GetOrNullAsync(NotificationsPolicy.PortalBaseUrl);

// After
var url = TenantUrlComposer.ComposeForTenant(
    await _settingProvider.GetOrNullAsync(NotificationsPolicy.PortalBaseUrl),
    _currentTenant.Name);
```

Site inventory (verified by grep):

| File | Lines | Setting |
|---|---|---|
| `Application/Emailing/CaseEvaluationAccountEmailer.cs` | `ResolvePortalBaseUrlAsync` body | PortalBaseUrl |
| `Application/Emailing/CaseEvaluationAccountEmailer.cs` | `ResolveAuthServerBaseUrlAsync` body | AuthServerBaseUrl |
| `Application/Notifications/Handlers/DocumentEmailContextResolver.cs:81` | inline read | PortalBaseUrl (feeds ~9 downstream handlers via `ctx.PortalBaseUrl`) |
| `Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs:251` | inline read | PortalBaseUrl |
| `Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs:254` | inline read | AuthServerBaseUrl |
| `Application/Notifications/Handlers/AccessorInvitedEmailHandler.cs:159` | inline read | AuthServerBaseUrl |
| `Application/ExternalAccount/ExternalAccountAppService.cs:431` | inline read | AuthServerBaseUrl |

## 3. Why this is safe in the email pipeline

Verified body-composition timing (see `CaseEvaluationAccountEmailer.cs:102-103, 182-184`
and `SendAppointmentEmailJob.cs:90-94`):

1. Resolve methods are called SYNCHRONOUSLY in the originating request context.
   `_currentTenant.Name` at that moment is the booker's tenant.
2. The resolved URL is substituted into `variables`, then into the email body
   via `TemplateVariableSubstitutor.Substitute(template.BodyEmail, variables)`.
3. The fully-rendered body is enqueued to Hangfire as opaque text.
4. `SendAppointmentEmailJob` executes later and calls `_emailSender.SendAsync(...)`
   with the already-rendered body. No URL re-resolution, no template re-render.
5. The job DOES re-enter tenant scope via `_currentTenant.Change(args.TenantId)`,
   but only for the packet-attachment repository lookup -- the body is already
   text by then.

Composer at the resolve-method boundary captures the correct tenant.

## 4. Architecture impact

- **Domain:** unchanged. No SettingDefinition rewrite.
- **Application:** one new static helper (~20 lines) + 7 one-line wraps. No
  service-class signatures change. `ICurrentTenant` is already in scope at
  every wrap site.
- **EF Core:** unchanged. No schema, no migration.
- **HttpApi / Angular proxy:** unchanged. No DTO change.
- **DbMigrator:** unchanged. No new seed contributor.
- **Tests:** one new xUnit test file (`TenantUrlComposerTests.cs`, ~50 lines,
  8 cases).
- **Compose:** +4 lines.

## 5. Error handling

| Scenario | Composer behavior | Rationale |
|---|---|---|
| `baseUrl == null` | returns null | Caller already handles null (falls back to const) |
| `baseUrl == ""` | returns "" | Same as null path |
| `tenantName == null` | returns baseUrl unchanged | Host scope (e.g. IT-admin invite) -- no subdomain |
| `tenantName == ""` | returns baseUrl unchanged | Defensive |
| URL already has subdomain (e.g. `http://falkinstein.localhost:4200`) | returns unchanged | Regex anchors on bare-localhost host token only; matches `tenant-bootstrap.ts` behavior |
| URL host is an IP / non-localhost domain | returns unchanged | Regex doesn't match; production URLs unaffected |
| URL is malformed | returns unchanged | Regex no-match -- non-throwing |

No logging in the composer. The caller's existing null-coalescing fallback
to `Default*` const handles the empty-URL edge.

## 6. Testing

### Unit tests -- `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/TenantUrlComposerTests.cs`

xUnit + Shouldly. 8 cases:

| # | Input baseUrl | Input tenant | Expected output |
|---|---|---|---|
| 1 | `http://localhost:4200` | `Falkinstein` | `http://falkinstein.localhost:4200` |
| 2 | `http://localhost:4200` | `null` | `http://localhost:4200` |
| 3 | `http://localhost:4200` | `""` | `http://localhost:4200` |
| 4 | `http://falkinstein.localhost:4200` | `Falkinstein` | `http://falkinstein.localhost:4200` (idempotent) |
| 5 | `http://127.0.0.1:4200` | `Falkinstein` | `http://127.0.0.1:4200` (no localhost) |
| 6 | `http://example.com:4200` | `Falkinstein` | `http://example.com:4200` (real domain) |
| 7 | `http://localhost:4200/some/path?q=1` | `Falkinstein` | `http://falkinstein.localhost:4200/some/path?q=1` |
| 8 | `null` | `Falkinstein` | `null` |

### Smoke test (manual, gating Task A's commit to the branch)

**Two phases.** Phase 1 verifies Layer 1 (env-var override) independently of
Layer 2. Phase 2 verifies the composer end-to-end.

**Phase 1: env-var override pathway.**

1. Bring down replicate-old-app's stack: `cd /w/patient-portal/replicate-old-app && docker compose down` (NO `-v`, keep volume).
2. In `docker-compose.yml` on `feat/parallel-worktree-stacks`, temporarily replace the AuthServer env var with a sentinel:
   ```yaml
   Settings__CaseEvaluation__Notifications__PortalBaseUrl: "http://test.example.com/sentinel-A"
   ```
3. `cd /w/patient-portal/main && docker compose up -d --build`.
4. Log in as `SoftwareThree@gesco.com` (verified Falkinstein patient per 2026-05-14 memory). Trigger resend-verification on a previously-registered unverified user.
5. Tail authserver logs: `docker compose logs -f authserver | grep -i sentinel`.
6. Confirm `http://test.example.com/sentinel-A` appears in the rendered email body.
   - PASS -> Layer 1 works. Continue.
   - FAIL -> `ConfigurationSettingValueProvider` is not in the chain for this stack. STOP, investigate.
7. Restore the real value: `http://localhost:${NG_PORT:-4200}`.

**Phase 2: composer end-to-end.**

1. With the real env var values in place (`http://localhost:${NG_PORT:-4200}`), restart the stack.
2. Trigger the same resend-verification flow as a Falkinstein-scoped user.
3. Grep the rendered email body in logs.
4. Confirm `http://falkinstein.localhost:4200` appears (composer prepended `falkinstein.` to the env-var value).
   - PASS -> composer correctly captures `_currentTenant.Name`. Task A commit goes on the branch.
   - FAIL -> composer isn't being called, OR `_currentTenant.Name` is null/wrong at the resolve site. STOP, investigate.

If both phases pass: commit Task A and continue to Task B.

### Verification gate

If smoke test phase 1 fails -> 4-line revert in compose, no code damage.
If smoke test phase 2 fails -> revert the wrap edits in 5 files (composer
file can stay; it's harmless without callers), 4-line revert in compose.

## 7. HIPAA / PHI impact

None. Synthetic test user only (`SoftwareThree@gesco.com` per 2026-05-14
seed). No PHI in code, tests, or smoke-test data. Logging redacts identifiers
where applicable -- composer logs nothing.

## 8. Blast radius

- **Email-rendering hot path:** every email-sending code path in the app
  invokes one of the 7 resolve sites. A composer bug surfaces immediately on
  the next email flow.
- **No data path:** no DB migration, no schema change, no setting-value seed
  change. Settings table content unchanged.
- **Reversibility:** trivial. Revert the commit. No data cleanup required.
- **Frontend:** unchanged.

## 9. Dependencies

None added. `System.Text.RegularExpressions` is in the BCL.

## 10. Out of scope (separate follow-ups)

- **Dead-const cleanup** in the 4 caller files (`CaseEvaluationAccountEmailer.cs:66,71`,
  `BookingSubmissionEmailHandler.cs:77-78`, `AccessorInvitedEmailHandler.cs:55`,
  `ExternalAccountAppService.cs:50`). These consts never fire (per BUG-014.md
  analysis) but the cleanup is a one-shot trivial PR after Task A merges.
- **Phase 1B multi-tenant subdomain validation across tenants** (e.g. Pelton).
  Phase 1A is single-tenant; this is BUG-014.md step 4.
- **OBS-9 reversal documentation.** Task F closes OBS-9 and rewrites the
  DOCKER-DEV.md parallel-stack section.
- **Per-tenant setting cache hot-swap.** Env var changes require container
  restart (ABP reads `IConfiguration` at startup). Documenting this in Task F.

## 11. Files changed

| File | Type | ~Lines | Notes |
|---|---|---:|---|
| `docker-compose.yml` | edit | +4 | Two env vars * two service blocks |
| `src/HealthcareSupport.CaseEvaluation.Application/Notifications/TenantUrlComposer.cs` | new | +25 | Static helper class |
| `src/HealthcareSupport.CaseEvaluation.Application/Emailing/CaseEvaluationAccountEmailer.cs` | edit | +2 | Two resolve methods wrapped |
| `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/DocumentEmailContextResolver.cs` | edit | +1 | Line 81 wrap |
| `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs` | edit | +2 | Lines 251 + 254 wraps |
| `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/AccessorInvitedEmailHandler.cs` | edit | +1 | Line 159 wrap |
| `src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs` | edit | +1 | Line 431 wrap |
| `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/TenantUrlComposerTests.cs` | new | +50 | 8 xUnit cases |
| **Total** | | **~86 lines / 8 files** | |

## 12. Acceptance criteria

- [ ] `dotnet test` for the new `TenantUrlComposerTests` passes 8/8.
- [ ] Existing `dotnet test` suite still passes (no regressions).
- [ ] Smoke test Phase 1 PASS (env-var override reaches email body).
- [ ] Smoke test Phase 2 PASS (composer produces `falkinstein.localhost:4200`).
- [ ] No `console.log` / `Logger.LogX` debug breadcrumbs left in the diff.
- [ ] No PHI in test fixtures or smoke-test data.
- [ ] Commit message follows `commit-format.md`: `feat(notifications): config-driven email URLs + tenant subdomain composer`.
- [ ] Task A commit is on `feat/parallel-worktree-stacks`, not pushed yet
  (push happens once all 6 tasks land + cross-worktree verification passes).

## 13. Open questions

None remaining after the body-composition timing was verified (2026-05-20,
this conversation).

## 14. Confidence

HIGH for the mechanism (verified via ABP source + `appsettings.json:25-34`
prior use of `Settings:Abp.Mailing.Smtp.*` in this codebase). HIGH for the
body-composition timing (verified in `CaseEvaluationAccountEmailer.cs:102-184`
and `SendAppointmentEmailJob.cs:90-94`). MEDIUM-HIGH for the composer regex
(lifted from `tenant-bootstrap.ts:97-100` which is proven in production for
the frontend's identical substitution).

The smoke test catches any wrong-confidence claim before commit.
