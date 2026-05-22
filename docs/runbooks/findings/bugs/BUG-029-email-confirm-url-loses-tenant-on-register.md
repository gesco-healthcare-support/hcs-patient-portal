---
id: BUG-029
title: First email-confirmation URL after register loses tenant subdomain, breaking the user's first click
severity: high
status: fixed
found: 2026-05-21 hardening HRD-P1.A.1
flow: registration-email-verification
component: src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs (or wherever the post-register confirmation email is composed) + TenantUrlComposer
---

# BUG-029 - First email-confirmation URL after register loses tenant subdomain

> **Verification 2026-05-22: FIXED (confidence 98%).**
>
> Closed by PR #210 (`fix(notifications): centralize tenant-aware email URL composition`, merged 2026-05-21 into `feat/replicate-old-app`, commit `c53b12e`) and promoted to `main` via PR #222 (merged 2026-05-22, commit `be2749f`). PR #210 explicitly states "Closes BUG-029" and migrated **all 16 leaky URL-composition sites** (4 AppServices + 12 notification handlers) to a new `IAccountUrlBuilder` service that takes a non-nullable `Guid tenantId` -- the compiler now enforces "every email link names a tenant." Identified the deeper root cause this doc only hypothesized: `ICurrentTenant.Change(Guid?)` defaults `Name = null` (ABP framework convention), so every ambient-context reader saw a null tenant name and `TenantUrlComposer` no-op'd.
>
> Live-verified in PR #222: registration / forgot-password / IT-Admin invite / internal-user welcome flows all emit tenant-subdomain URLs (AuthServer-log-verified) **and** an end-to-end click-through from a real inbox returned `302 -> /Account/Login?flash=email-verified` with the user marked confirmed. Screenshot at `.github/pr-media/bug-029-email-verified-landing.png`.
>
> Hardcoded `"http://falkinstein.localhost:..."` fallbacks (3 in app code + 2 in the settings provider) were also deleted -- a missing `App__SelfUrl` / `App__AngularUrl` env var now throws `InvalidOperationException` at first email-send instead of silently emitting demo-tenant URLs.
>
> Frontmatter `status: fixed` was flipped between sessions. The detailed citations above are added to the doc body so future readers see the full evidence trail without grepping PRs. Companion finding [[OBS-21]] (`superseded-by-bug-029`) becomes implicitly resolved with this closure.

## Symptom

After a fresh `docker compose down -v && up -d --build` on the `main` stack, registered `patient1@gesco.com` via the SPA at `http://falkinstein.localhost:4200`. The Hangfire job queued during the register POST (Job Id=1, CreatedAt 16:50:56) contains an email body with this confirmation URL:

```
http://localhost:44368/Account/EmailConfirmation?userId=b726a379-32fc-7a7b-cecf-3a215d9f543f&confirmationToken=CfDJ8Lh0%2B3UEH7lMiwDHOsBU2XBLVnyGK3cvFlhTc5WhFT1xIFfE1SP1d2A9RHsnAwZFjD9lmGyb%2FO4cyAR6sMkIzR5LKUchYg6DjiIkFXC8qd89NGVO5F1P1RT1a%2FvotpsVt79aX2eADCLNgFO%2FtuiQBP1yPNz9323rkuXxBNAgjxf5GEhDLi29LW8tKuiv2IiKmkRHwaSzCMqqv9tFHBaWu52D5PG%2FTm%2BhXSQt1GW3mz%2BxTxEIovF73lp2oVvmRv5pcA%3D%3D
```

Host is `localhost`, no tenant subdomain.

Clicking that URL hits `EmailConfirmationModel.OnGetAsync`. AuthServer log shows:

```
EmailConfirmationModel: user b726a379-32fc-7a7b-cecf-3a215d9f543f not found; redirecting with generic flash.
```

User-visible flash: "That verification link doesn't work anymore. Resend below." (302 to `/Account/Login?flash=verification-invalid`)

Yet `SELECT Id, UserName, TenantId, EmailConfirmed FROM AbpUsers WHERE Id='b726a379-...'` shows the row exists, with `TenantId = D10D5438...` = Falkinstein. The user IS in the DB; the lookup misses because `ICurrentTenant.Id` is null on a `localhost` (host) request.

By contrast, after navigating to the Razor resend page at `http://falkinstein.localhost:44368/Account/ResendVerification?context=register&email=...&autosend=1`, the same user's _next_ email (delivered to SMTP, no Hangfire row created) contains the working URL:

```
http://falkinstein.localhost:44368/Account/EmailConfirmation?userId=b726a379-32fc-7a7b-cecf-3a215d9f543f&confirmationToken=<different-token>
```

That URL resolves the user correctly (subdomain-resolved tenant = Falkinstein), and the confirmation succeeds (`EmailConfirmed=1`, redirect `/Account/Login?flash=email-verified`).

## Hypothesis

1. **Tenant context not active in the register POST handler.** `ExternalSignupAppService.Register` / `ExternalAccountAppService.RegisterAsync` runs as an anonymous endpoint. The Falkinstein subdomain is in the Host header (`Host: falkinstein.localhost:44327`), but ABP's subdomain tenant resolver may not fire on `/api/public/*` routes (possibly because the controller is annotated `[AllowAnonymous]` or uses a different routing pipeline). At the moment `TenantUrlComposer` is asked to prepend `<tenant>.`, `ICurrentTenant.Name` is null, so it returns the raw `http://localhost:44368` config value unchanged.

2. **Composer regex matches but tenant lookup returns empty.** `TenantUrlComposer` runs and finds `localhost` in the URL, but its tenant-name source (`ICurrentTenant.Name`) is empty string, not null. The composer's branch for empty-string may skip the prepend. The Razor resend page works because it loads under `falkinstein.localhost:44368` host, so subdomain resolution sets the tenant before `OnGetAsync` runs.

3. **Two URL-build code paths.** The register-time URL builder in `ExternalAccountAppService` may bypass `TenantUrlComposer` (older code path), while the resend-flow builder in `CaseEvaluationAccountEmailer` correctly wraps it. The TenantUrlComposer wiring (Task A 2026-05-20, commit 5cdae28) may have been added to one site but missed the other.

## Reproduction (minimal, deterministic)

1. `docker compose down -v && docker compose up -d --build` on `main` worktree.
2. Once stack healthy, open `http://falkinstein.localhost:4200/`, click Sign up, register a fresh Patient with any synthetic email (`@gesco.com`).
3. SQL: `SELECT TOP 1 j.Arguments FROM HangFire.Job j ORDER BY j.Id DESC` — inspect the embedded `<a href>`.
4. Observe: host segment is `localhost`, not `<tenant>.localhost`.
5. Click that link in a browser without the tenant subdomain — AuthServer log shows `user ... not found`, browser shows "verification link doesn't work anymore".
6. Trigger a Resend via the SPA's success-card link OR navigate directly to `/Account/ResendVerification?context=register&email=<email>&autosend=1` (host = `falkinstein.localhost`).
7. Inbox now shows a NEW email with `http://falkinstein.localhost:44368/...` — that one works.

## Token is host-agnostic (workaround proven 2026-05-21 17:17)

A second register (HRD-P1.A.2 -- appatty1@gesco.com) produced the SAME broken URL pattern (`host=localhost`) in Adrian's inbox, and this time only one email arrived (because no Resend page autosend was manually triggered as in the patient1 case). Confirmed: every fresh register sends a broken first email.

I rewrote the broken URL by swapping `localhost` -> `falkinstein.localhost` while keeping the same token, then loaded it. AuthServer log:

```
EmailConfirmationModel: user 8db7b2a5-3dc0-1658-eddf-3a215daec7c7 email confirmed; redirecting to login.
```

DB row updated to `EmailConfirmed=1`. So the token itself encodes no host -- it validates against the user only. The defect is purely in URL composition, not in token issuance or validation.

This proves the root cause is the host segment of the URL, not the token. The fix surface is small (one or two URL builder call sites).

## Third instance: invite-flow URL builder (HRD-P1.B.1)

POST `/api/app/external-signup/invite-external-user` returns 200 with body:

```json
{
  "inviteUrl": "http://localhost:44368/Account/Register?inviteToken=ed_WCUMI4haqdlm6VfpCXSKFlDOvd_HJJr1rrekoFOI",
  "email": "defatty1@gesco.com",
  "roleName": "Defense Attorney",
  "tenantName": "Falkinstein",
  "expiresAt": "2026-05-28T17:20:31.8027749Z"
}
```

The server KNOWS the tenant ("Falkinstein" is in the response payload) yet the `inviteUrl` host is still raw `localhost`. So the bug is not "tenant unresolved at request time" -- the tenant IS resolved and explicitly named in the same payload. The URL composer is just not consulting the tenant context when building the host segment.

Adrian confirmed by inbox: the email he received contained the broken `localhost` URL. Same workaround works: swap `localhost` -> `falkinstein.localhost` while keeping the same `inviteToken`, and the invite flow proceeds normally.

Evidence count: 3 sites now (post-register verify x2 + post-invite x1). The pattern is consistent across both `ExternalAccountAppService.RegisterAsync` (verify URL) and `ExternalSignupAppService.InviteExternalUserAsync` (invite URL).

Updated fix surface: trace all URL builders that read `App:AngularUrl` / `AuthServer:Authority`. The TenantUrlComposer wrap was added to `CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync` but NOT to:

- (verify URL) ExternalAccountAppService.RegisterAsync (or whichever code path builds the verify URL on the register POST; it's clearly NOT going through `CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync` because BUG-014 fixed that one).
- (invite URL) ExternalSignupAppService.InviteExternalUserAsync.

Search for: `IConfiguration.GetValue("AuthServer:Authority")` and `Settings.GetOrNullAsync(NotificationsPolicy.AuthServerBaseUrl)`. Every call site needs the TenantUrlComposer wrap.

## Recommended fix

Find every site that builds an account-related URL (verify, password reset, invite). Each must either:

a. Read `AuthServer:Authority` and pass through `TenantUrlComposer.WithTenantPrefix(url)`, AND ensure `ICurrentTenant.Name` is populated before that call (by adding a tenant resolution step at the top of the anonymous register handler that reads the Host header).

b. Or simpler: change docker-compose env vars to `App__AngularUrl: "http://falkinstein.localhost:${NG_PORT:-4200}"` + `AuthServer__Authority: "http://falkinstein.localhost:${AUTH_PORT:-44368}"`. This sidesteps the composer entirely. Downside: tenant prefix is hardcoded to `falkinstein`, which the long-term multi-tenant plan rejects. But until Phase 1B (tenant-from-user-record), the hardcoded prefix matches the single-tenant demo target.

The (b) shortcut is what `feat/parallel-worktree-stacks` (commit 5cdae28) attempted to AVOID. The right long-term fix is (a) — ensure tenant resolution runs on the register POST. Worth a 30-min code dive in `ExternalSignupController` / `ExternalAccountController` to find where the anonymous endpoint's middleware stack drops tenant.

## Functional impact

- First-time registrants (the common path) click the email link from their inbox and see a confusing "doesn't work anymore" message on the very first click.
- The system masks this with the resend flow: after the broken click, the user clicks Resend on the redirect page, receives a working link, and proceeds. So the bug is recoverable but creates a high-friction first impression.
- HIPAA-adjacent concern: a user's confirmation URL in their inbox publicly identifies the user's `userId` GUID and a valid confirmation token. If the user forwards or pastes the URL anywhere, the lack of tenant subdomain makes the URL look like a generic localhost link, possibly confusing the recipient. Not severity-blocking but worth noting.

## Related

- [[BUG-014]] hardcoded-email-urls (origin of the env-var-driven approach; this finding is the regression after the wildcard-domain refactor).
- [[BUG-016]] openiddict-subdomain (tenant subdomain handling at the OAuth layer; same conceptual area).
- [[BUG-006]] verify-email-url (original URL-target finding, pre-2026-05-18 era).
- Task A 2026-05-20 commit 5cdae28 (TenantUrlComposer added; this BUG indicates the wrapping isn't reaching every call site OR the tenant isn't resolved when it runs).
