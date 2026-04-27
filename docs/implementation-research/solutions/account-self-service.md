# Account self-service (email verification + forgot-password)

## Source gap IDs

- 5-G13 (self-service email verification) -- `../gap-analysis/05-auth-authorization.md` line 201
- 5-G14 (self-service forgot-password) -- `../gap-analysis/05-auth-authorization.md` line 202
- A8-12 (no Angular client wrapper for account flows) -- `../gap-analysis/08-angular-proxy-services-models.md` line 212

Gap 5-G13 and 5-G14 are both phrased as "ABP provides; not verified wired" in the
source track. A8-12 notes "OIDC login covered; no client wrapper for account flows".
Live probes + code citations below resolve both phrasings: ABP Account Module IS
wired on both AuthServer and Angular.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:70-73` --
  AuthServer declares `[DependsOn]` on `AbpAccountPublicWebOpenIddictModule`,
  `AbpAccountPublicHttpApiModule`, `AbpAccountPublicApplicationModule`,
  `AbpAccountPublicWebImpersonationModule`. The full ABP Account suite is loaded.
- `src/.../AuthServer/CaseEvaluationAuthServerModule.cs:152` -- `AccountResource`
  is registered as a base type of `CaseEvaluationResource`, so Account UI strings
  resolve in the LeptonX theme.
- `src/.../AuthServer/CaseEvaluationAuthServerModule.cs:189-196` -- `AppUrlOptions`
  wires the Angular callback URL map used by ABP when the Account Module emails
  reset-password and confirm-email links: `PasswordReset` ->
  `account/reset-password`, `EmailConfirmation` -> `account/email-confirmation`,
  plus `Applications["Angular"].RootUrl = http://localhost:4200`.
- `src/.../AuthServer/appsettings.json:4` -- `App:AngularUrl = http://localhost:4200`
  (source of the callback root).
- `src/.../AuthServer/appsettings.json:13-15` -- `AuthServer.Authority =
  https://localhost:44368`, `RequireHttpsMetadata = true`. Dev-cert HTTPS is in
  force; confirmed by probe service-status.md.
- `src/.../AuthServer/Pages/Index.cshtml:12` -- AuthServer Razor root page injects
  `IHtmlLocalizer<AccountResource>` to render Account-scoped UI strings; proves
  Account Web assets are embedded in the host.
- `angular/package.json:36` -- `"@volo/abp.ng.account": "~10.0.2"` is declared as
  a direct Angular dependency; `yarn.lock:2646-2655` resolves both
  `@volo/abp.ng.account@~10.0.2` and `@volo/abp.ng.account.core@~10.0.2`.
- `angular/src/app/app.routes.ts:29-32` --
  `{ path: 'account', loadChildren: () => import('@volo/abp.ng.account/public').then(c => c.createRoutes()) }`.
  Every `/account/**` URL in the SPA is served by the ABP Account Public package
  (login, register, forgot-password, reset-password, email-confirmation, profile).
- `angular/src/app/app.config.ts:19-20,79-80` -- `provideAccountAdminConfig()`
  and `provideAccountPublicConfig()` are listed in the root provider array. The
  account package is DI-registered for both admin and public surfaces.
- `angular/src/app/proxy/generate-proxy.json:340-356` -- the proxy manifest
  already describes the `AbpAccountPublic` remote service group. Running
  `abp generate-proxy` any time the SPA needs typed account DTOs will
  regenerate `proxy/account/**` services (the auto-generated folder, per
  ADR-005 / CLAUDE.md; never hand-edited).
- No custom AppService subclass extends or shadows `IAccountAppService` or
  `IAccountAdminAppService` anywhere under `src/`. The wiring is the stock
  Volo module, not a subclassed variant.

## Live probes

All probes executed against LocalDB with no state mutation. Full log at
`../probes/account-self-service-2026-04-24T12-46-00.md`.

- `GET https://localhost:44327/swagger/v1/swagger.json` -- 200, 2,607,985 bytes,
  317 total paths, 58 `/api/account*` paths. Confirms every ABP Account endpoint
  is registered on HttpApi.Host: `/api/account/register` [POST],
  `/api/account/send-password-reset-code` [POST],
  `/api/account/verify-password-reset-token` [POST],
  `/api/account/reset-password` [POST],
  `/api/account/send-email-confirmation-token` [POST],
  `/api/account/verify-email-confirmation-token` [POST],
  `/api/account/confirm-email` [POST],
  `/api/account/confirmation-state` [GET],
  `/api/account/send-email-confirmation-code` [POST],
  `/api/account/email-confirmation-code-limit` [GET],
  plus `/api/account/my-profile` [GET/PUT],
  `/api/account/my-profile/change-password` [POST], and the full 2FA +
  external-login + user-delegation surface.
- `GET https://localhost:44368/Account/Login` -- 200, 17,206 bytes,
  `text/html; charset=utf-8`. Title: `Login | CaseEvaluation`. Form fields:
  `LoginInput.UserNameOrEmailAddress`, `LoginInput.Password`,
  `LoginInput.RememberMe`, `__RequestVerificationToken`. Confirms Razor Account
  UI is registered, LeptonX theme is rendering, antiforgery is enabled.
- `GET https://localhost:44368/Account/ForgotPassword` -- 200, 15,365 bytes,
  `text/html; charset=utf-8`. Title: `Forgot password? | CaseEvaluation`. Form
  fields: `Email`, `__RequestVerificationToken`. Confirms the forgot-password
  Razor page is reachable; submitting this form is what fires
  `/api/account/send-password-reset-code` inside the Account Module.
- `GET https://localhost:44368/Account/ResetPassword` -- 500 with
  `AbpValidationException: ModelState is not valid`. Expected: the page requires
  query string `?userId=<guid>&resetToken=<base64>&tenantId=<guid>` supplied by
  ABP's email template. The 500 proves the Razor page is registered and bound
  (it got far enough to attempt model validation before rejecting the empty args).
- `GET https://localhost:44368/Account/EmailConfirmation` -- 500 with
  `EntityNotFoundException: Volo.Abp.Identity.IdentityUser, id: 00000000-0000-0000-0000-000000000000`.
  Expected: the page requires `?userId=<guid>&confirmationToken=...&tenantId=<guid>`.
  The 500 proves the page is registered and reached the identity user lookup
  step before failing on the empty default Guid.
- NOT probed (state-mutating): `POST /api/account/send-password-reset-code`,
  `POST /api/account/register`, `POST /api/account/send-email-confirmation-token`.
  `NullEmailSender` would suppress delivery but rate-limit tables + security-log
  rows would still mutate per ABP default behavior. Protocol forbids mutating
  probes for this capability.

## OLD-version reference

- `P:\PatientPortalOld\...\UserAuthenticationController.cs:56-66` -- OLD hosted a
  custom `POST /api/userauthentication/forgot-password` that generated a token,
  wrote it to `ApplicationUserToken`, and emailed a deep link. Replaced by ABP
  Account Module's `POST /api/account/send-password-reset-code` ->
  `verify-password-reset-token` -> `reset-password` sequence.
- `P:\PatientPortalOld\...\UserAuthenticationController.cs:81-91` -- OLD hosted
  `POST /api/userauthentication/verify-email-token` as the confirmation sink.
  Replaced by ABP's `POST /api/account/verify-email-confirmation-token` and
  `POST /api/account/confirm-email`.
- `P:\PatientPortalOld\...\UserAuthenticationDomain.cs:50-113` -- OLD enforced
  `if (!user.IsVerified) { send email and return 400 }` at login time. NEW leaves
  this to OpenIddict + the Account Module's `IdentityUserConfirmationStateDto`
  check (`GET /api/account/confirmation-state`); login still succeeds but
  downstream permission checks can gate unconfirmed users if desired.
- Track-10 erratum applicability: none. Track 10 errata concern PDF renderer,
  SMS, scheduler, and CustomField -- not Account Module wiring.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. `AbpAccountPublicWebOpenIddictModule`
  is the integration contract; we cannot substitute an unrelated auth Razor surface.
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant. ABP Account Module is
  tenant-aware via `__tenant` header/cookie. A new tenant admin (the Doctor user)
  is what triggers the account self-service flow for their external users.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no ng serve (ADR-005). None apply: the Account Module is a Volo package and
  ships its own controllers + DTOs; no project code is added.
- HIPAA applicability: password-reset and email-confirmation emails MUST NOT
  include any patient identifiers; ABP's default templates use only the user's
  display name + link. Any template override must preserve that.
- Capability-specific: email delivery depends on `IEmailSender` having a real
  consumer. `NullEmailSender` is wired today (Track-10 part of email-sender-consumer
  finding). Without CC-01 (email-sender-consumer), the API surface works but the
  user never receives the reset/confirmation email.

## Research sources consulted

- ABP Commercial docs, Account Module overview (HIGH): https://abp.io/docs/10.0/modules/account --
  accessed 2026-04-24. Confirms the API set, Razor pages, and Angular package
  compose a complete self-service UX out of the box.
- ABP Commercial docs, Account Angular UI integration (HIGH):
  https://abp.io/docs/10.0/framework/ui/angular/modules/account -- accessed
  2026-04-24. Confirms `@volo/abp.ng.account/public` exposes `createRoutes()` for
  `/account/**` lazy loading and `provideAccountPublicConfig()` for provider
  registration. Matches `app.routes.ts:29-32` verbatim.
- ABP docs, email templating for Account (HIGH):
  https://abp.io/docs/10.0/modules/account#email-templates -- accessed 2026-04-24.
  Confirms the `PasswordReset` and `EmailConfirmation` `AppUrlOptions` keys (which
  we set at `CaseEvaluationAuthServerModule.cs:194-195`) are what the template
  substitution uses to build the outbound link.
- ABP docs, `IEmailSender` interface + `NullEmailSender` default (HIGH):
  https://abp.io/docs/10.0/framework/infrastructure/emailing -- accessed 2026-04-24.
  Confirms `NullEmailSender` no-ops by default in Development and swapping to
  MailKit or SMTP requires registering `AbpMailKitModule` or configuring
  `AbpMailingOptions` + `SmtpEmailSenderConfiguration`. Dependency for CC-01.
- NuGet package page, `Volo.Abp.Account.Pro.Public.Web` 10.0.2 (MEDIUM):
  https://www.nuget.org/packages/Volo.Abp.Account.Pro.Public.Web/10.0.2 --
  accessed 2026-04-24. Confirms the exact package ABP pulls in when you depend
  on `AbpAccountPublicWebOpenIddictModule`, resolving 5-G13/5-G14 "not verified
  wired" to "wired".
- NPM package page, `@volo/abp.ng.account` 10.0.2 (MEDIUM):
  https://www.npmjs.com/package/@volo/abp.ng.account -- accessed 2026-04-24.
  Version alignment with the backend modules; confirms the public entry point
  ships login, register, forgot-password, reset-password, email-confirmation,
  and profile-management components.
- ABP Community, Account Module rate-limit + lockout defaults (MEDIUM):
  https://abp.io/community/articles/account-module-account-lockout --
  accessed 2026-04-24. Explains why mutating probes would leave persistent
  rate-limit state; rationale for the no-mutation rule on this capability.

## Alternatives considered

- **Use ABP Account Module as-is** (chosen). Wiring already present in
  `CaseEvaluationAuthServerModule.cs` + `app.routes.ts`; no new code required.
  Delivery depends on CC-01.
- **Fork + customize a Razor subset for branded UX** (rejected). Would require
  `Volo.Abp.Account.Pro.Public.Web.Theming` override, ADR-005 static-serve
  constraints on the Angular side, and duplicating Volo flows. No product
  requirement for branded UX per gap-analysis README Q16.
- **Replace with a handwritten controller** (rejected). Violates the intentional
  OIDC + Account Module architectural swap recorded in `gap-analysis/05-auth-authorization.md`
  (lines 216-227). The reason the OLD hand-rolled auth surface was scrapped
  was exactly to let ABP own password-reset, email-confirmation, 2FA, external
  login, and link-login under one module.
- **Add a handwritten `account.service.ts` Angular wrapper to close A8-12**
  (rejected). `@volo/abp.ng.account/public` already provides the routes,
  components, and typed services; a thin handwritten wrapper would duplicate
  types and diverge from ABP's generated proxy on upgrade. The proxy manifest
  at `angular/src/app/proxy/generate-proxy.json:340-356` lists the
  `AbpAccountPublic` remote service; running `abp generate-proxy` will emit
  typed `proxy/account/**` services any time a feature needs to call an
  account endpoint from outside the `/account/**` route tree.
- **No viable alternative** for the delivery step other than implementing
  CC-01 (email-sender-consumer). Without `IEmailSender` wired to a real
  transport, any chosen UX flow is cosmetic.

## Recommended solution for this MVP

Adopt the existing ABP Account Module wiring with **no code changes** in this
capability. Confirmation of wiring is the only deliverable:

1. AuthServer: keep the 4 `AbpAccountPublic*` module dependencies and the
   `AppUrlOptions` callback mapping in `CaseEvaluationAuthServerModule.cs`
   unchanged.
2. Angular: keep `@volo/abp.ng.account/public` as the `/account/**` loader in
   `app.routes.ts:29-32` and the two `provideAccount*Config()` entries in
   `app.config.ts:79-80` unchanged.
3. Email delivery: implement capability `email-sender-consumer` (CC-01) and
   swap `NullEmailSender` for MailKit/SMTP in the AuthServer module so the
   Account Module's token emails actually send. Once CC-01 lands, both 5-G13
   and 5-G14 are end-to-end functional without further touches here.
4. Optional branded email templates (post-MVP): override
   `AbpEmailTemplate.PasswordResetLink` and `AbpEmailTemplate.EmailConfirmationLink`
   via `AbpTextTemplateDefinitionProvider` under `Domain/Emailing/`. Out of
   scope for MVP per README Q16 phrasing ("confirm wired and tested"; no
   rebrand requested).

Project touch-points if any remediation is needed:
`src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs`
for module-level dependencies;
`src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json` for
`App:AngularUrl` and `AuthServer:Authority`;
`angular/src/app/app.routes.ts` + `app.config.ts` for the Angular wiring.
No entity, no controller, no proxy, no migration changes are required.

## Why this solution beats the alternatives

- Zero code added; maximum leverage of ABP Commercial features already paid for.
  The bug tracker is `unverified wired`, and the verification itself is the work.
- Upgrade-safe: staying on stock `@volo/abp.ng.account/public` means any Volo
  10.x patch release (security fixes, lockout refinements) applies automatically.
- Preserves the intentional OIDC + Account Module architectural swap recorded
  in the gap-analysis doc; reverting to a hand-rolled controller would
  re-introduce the custom `ApplicationUserToken` table and the single-token-per-user
  enforcement that OIDC supersedes.
- Dependency chain is narrow: only CC-01 (email sender) gates live delivery,
  and CC-01 is needed for other capabilities (scheduler-notifications,
  appointment-change-requests, templates-email-sms) regardless.

## Effort (sanity-check vs inventory estimate)

Inventory says **Low (S)**. Analysis confirms **S** (~0.5 day).
- 0 dev effort on this capability itself beyond evidence capture.
- Actual work to flip the capability from "wired but non-functional" to
  "end-to-end" belongs to CC-01 (email-sender-consumer) and is tracked there.

## Dependencies

- **Blocks:** none. Account self-service is a leaf capability; no other
  capability sits behind it.
- **Blocked by:**
  - `email-sender-consumer` (CC-01) -- password-reset codes and email-confirmation
    tokens are delivered via `IEmailSender`, which currently binds to
    `NullEmailSender` (no-op) in Development. Without CC-01, the endpoints
    return 200 but the user never sees the email.
- **Blocked by open question:**
  `Email verification + forgot password self-service: confirm ABP Account Module is wired and tested. (Track 5)`
  (verbatim, `docs/gap-analysis/README.md:246`). This brief + probe log answer
  "yes, wired". The "and tested" half is closed by the four probes in this file
  plus one manual smoke after CC-01 lands (send code -> check inbox -> reset).

## Risk and rollback

- **Blast radius:** Account Module module dependencies sit on the AuthServer
  host only; a mis-config in `AppUrlOptions` would break inbound email links
  (users land on a 404) but would NOT break login or any other HttpApi.Host
  endpoint. Angular `/account/**` route failure would break the forgot-password
  flow only; other routes use `authGuard` / `permissionGuard` and are isolated.
- **Rollback:** already zero-change; rollback is a no-op. If a future edit to
  `CaseEvaluationAuthServerModule.cs` breaks the Account surface, revert the
  single file on the feature branch; the worktree is clean. No migration to
  revert. No data to unroll.

## Open sub-questions surfaced by research

- Branded email template override in/out of MVP? README Q16 reads "confirm
  wired and tested"; confirmation covered above. If product wants Gesco/HCS
  branding on reset/confirm emails, add a `templates-email-sms` extension
  post-MVP.
- Should `IdentityUserConfirmationStateDto` gate any backend permission (e.g.
  external attorney cannot submit documents until email-confirmed)? Not
  currently enforced. Raise separately if product wants pre-confirmation
  feature gating.
