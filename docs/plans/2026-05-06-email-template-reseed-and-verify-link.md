---
feature: email-template-reseed-and-verify-link
date: 2026-05-06
status: draft
base-branch: feat/replicate-old-app-track-domain
related-issues: [demo-smoke-report-2026-05-05]
supersedes-tasks:
  - docs/plans/2026-05-06-demo-smoke-fixes.md#T8
---

# Plain-HTML email reseed + verify-link diagnosis & fix

## Goal

Two intertwined demo blockers, fixed in one slice:

1. The 6 plain-HTML demo email bodies (UserRegistered + 5 booking-cascade) take effect in the dev DB without a manual SQL delete or fresh DB.
2. Clicking the verification link in the registration email lands on a working confirmation page instead of failing silently.

## Context

- Templates were just swapped to plain HTML in `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/*.html`. `NotificationTemplateDataSeedContributor.cs:90` only inserts rows whose `TemplateCode` is missing -- it never overwrites, so the new bodies are dead on disk until rows are flushed.
- `UserRegisteredEmailHandler.BuildEmailConfirmationUrl` builds `http://falkinstein.localhost:4200/account/email-confirmation?userId=<guid>&confirmationToken=<urlencoded>`. ABP's stock `EmailConfirmationComponent` is registered at that route via `@volo/abp.ng.account/public.createRoutes()`. The component swallows API errors silently (only sets `notValid` when query params are missing, not when `accountService.confirmEmail` rejects).
- Three independent failure modes are plausible: (a) `falkinstein.localhost` not resolving in some browsers / email clients (no hosts-file entry), (b) `http://falkinstein.localhost:4200` missing from CORS allow-lists in `appsettings.json` for AuthServer + HttpApi.Host, (c) ABP's API genuinely rejecting the token (tenant context, encoding, or expiry). Adrian directive 2026-05-06: reproduce via Playwright before patching, no shotgun fixes.
- Phase 1A demo has zero IT-Admin template customizations in flight, so always-overwrite of the 6 demo-critical codes is safe; per-tenant editability returns once more codes onboard.

## Approach

- **Reseed:** narrow the always-overwrite to ONLY codes that have an `EmailBodyResources.TryLoadBody` hit. Codes without an embedded HTML file (the other 53) keep current behavior (insert-only, stub bodies, IT-Admin can edit). This protects future admin customizations the moment a feature phase wires its real body to disk and codifies "disk is canonical for codes whose body file exists".
- **Verify-link:** ship a 3-phase task. T2 reproduces and diagnoses. T3 applies the targeted fix matching the diagnosed failure mode -- branches kept narrow so the fix lands in one PR slice.

## Tasks

### T1 -- Always-overwrite seed for codes with an embedded body

- approach: tdd
- files-touched:
  - `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/NotificationTemplateDataSeedContributor.cs`
  - `test/HealthcareSupport.CaseEvaluation.Application.Tests/NotificationTemplates/NotificationTemplateDataSeedContributorTests.cs` (new) -- if a Domain-layer test fixture exists, prefer that location instead.
- acceptance:
  - Existing tenants with the 6 demo-critical rows get `BodyEmail` + `Subject` overwritten on next DbMigrator run; remaining 53 stub codes are untouched on second-run.
  - Admin-edited row for a code that DOES have an embedded body still gets overwritten (Phase 1A demo behavior; documented as deferred per CLAUDE.md "Phase 2 multi-tenancy adaptation"). Add a TODO comment on the always-overwrite branch citing the deferred trade-off.
  - First run on a fresh DB inserts all 59 rows exactly as before -- no behavior change for net-new tenants.
  - Test asserts: insert-then-edit-disk-then-reseed flips the body to the on-disk content; insert-then-reseed for a stub-only code does NOT touch its body.

### T2 -- Reproduce verify-link failure via Playwright MCP

- approach: code (investigation deliverable)
- files-touched:
  - `docs/research/2026-05-06-verify-link-repro.md` (new) -- captures the reproduction transcript, screenshots, and root-cause attribution.
- acceptance:
  - Repro steps: (1) start full stack via `docker compose up -d --build` (dev), (2) Playwright drives `http://falkinstein.localhost:4200/account/register`, submits a new patient, (3) reads the dispatched verification URL from the AuthServer / HttpApi.Host log stream, (4) opens the URL in the same Playwright session, (5) records page state + network tab + console.
  - Doc names ONE primary failure mode (DNS, CORS, API 4xx/5xx, or token mismatch) with the supporting evidence (HTTP status, response body, console error). If multiple stacked failures, names them in order.
  - Doc lists the exact fix matching the diagnosis (used as input to T3).

### T3 -- Apply targeted fix from T2 diagnosis

- approach: code (TDD if backend logic, code-only if config / hosts file)
- files-touched: TBD by T2. Pre-anticipated branches:
  - **Branch A -- hosts resolution (no DNS for `*.localhost`):** add `falkinstein.localhost` row to the docker-compose dev override that mounts a hosts entry into the container, OR rewrite `BuildEmailConfirmationUrl` to use `localhost:4200/account/email-confirmation?...&__tenant=Falkinstein` (ABP-supported tenant query param). Updates `UserRegisteredEmailHandler.cs` + tenant-resolution validation test.
  - **Branch B -- CORS missing the falkinstein subdomain:** add `http://falkinstein.localhost:4200` to `App:CorsOrigins` and `App:RedirectAllowedUrls` in `src/.../AuthServer/appsettings.json` and `src/.../HttpApi.Host/appsettings.json`. No code change.
  - **Branch C -- API genuinely rejects token:** add an SPA-side wrapper component that catches `confirmEmail` errors and renders a meaningful failure UI ("This link is invalid or has expired -- request a new one"). Files: `angular/src/app/account/email-confirmation/email-confirmation.component.ts` (new) plus a route override in `app.routes.ts` BEFORE the lazy `account` loadChildren so the override wins.
- acceptance:
  - Replay of T2's Playwright reproduction now lands the user on either a "confirmed" UI (success) or a meaningful error UI (failure), never a blank page.
  - `IsEmailConfirmed=true` on the seeded test user after a successful click.
  - No regression in existing `/account/login` / `/account/register` flows.

## Risk / Rollback

- **Blast radius (T1):** demo-critical codes get force-refreshed on every redeploy. If an admin DOES customize those before we phase out the override, their customization is lost. Mitigation: comment + remove-by date once Phase 1B (multi-tenant) ships.
- **Blast radius (T3 Branch A):** if we switch to `localhost:4200?__tenant=Falkinstein`, every other place that builds links for the SPA needs the same treatment OR we live with the inconsistency. Audit `BookingSubmissionEmailHandler`, `AccessorInvitedEmailHandler`, `StatusChangeEmailHandler`, `ChangeRequestSubmittedEmailHandler` for sibling URL-builder calls before merging.
- **Rollback:** revert merge commit. T1 is purely seed logic -- a revert leaves existing rows in their post-overwrite state but stops further overwrites. T3 is config or wrapping component, both reversible.

## Verification

End-to-end re-run after all 3 tasks:
1. Stop docker stack, drop the `NotificationTemplates` rows for the 6 demo-critical codes, restart -> rows reinsert with the new plain-HTML bodies.
2. Edit a one-character change in `UserRegistered.html`, restart DbMigrator -> the row's `BodyEmail` reflects the change. Confirms always-overwrite path.
3. Edit a stub-only template body via the IT-Admin UI, restart DbMigrator -> the admin edit survives. Confirms scope-limited overwrite.
4. Replay Playwright register flow + click verification link -> confirmation succeeds, `IsEmailConfirmed=true`.
