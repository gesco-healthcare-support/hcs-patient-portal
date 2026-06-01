# HttpApi Layer

Thin manually-written ASP.NET Core controllers that expose AppService methods over REST. Controllers contain no business logic -- they delegate every call to the injected `I{Entity}AppService`.

## What Lives Here

- `Controllers/` -- one controller per feature, each extending `AbpController` and implementing the feature's `I{Entity}AppService` interface
- `Controllers/CaseEvaluationController.cs` -- localization base class; exists but no controller in this repo currently extends it -- all extend `AbpController` directly
- ABP module definition files at project root

## Conventions

1. **Controllers are manual, not auto-wired.** ABP auto-controller generation is disabled via `[RemoteService(IsEnabled = false)]` on every AppService (see root CLAUDE.md for the attribute rule). Every new AppService requires a matching controller here. See docs/decisions/002-manual-controllers-not-auto.md.

2. **Controllers implement the AppService interface.** Signature: `public class {Entities}Controller : AbpController, I{Entities}AppService`. Each method is a one-line delegation to the injected AppService -- no logic, no DTO construction, no validation in the controller.

3. **Two route namespaces.**
   - `api/app/*` -- authenticated surface; the default for all CRUD controllers.
   - `api/public/*` -- anonymous-by-design surface (password reset, external signup, public document upload). Controllers on this prefix MUST carry `[IgnoreAntiforgeryToken]` because callers (Angular SPA pre-login, AuthServer Razor page) cannot supply an ABP antiforgery cookie.

4. **No authorization attributes on most controllers.** Permissions are enforced at the AppService layer. Adding `[Authorize]` to the controller is redundant and risks diverging from the AppService gate.
   - **Exception -- per-action mixed trust:** `InternalUsersController` and `ExternalUserController` carry `[Authorize]` / `[AllowAnonymous]` per action because individual actions on the same controller serve callers with different trust levels (e.g., `POST /internal-users` is staff-only; `GET /internal-users/tenants` is open for the signup dropdown). Apply the same pattern to any future controller that mixes authenticated and anonymous actions on the same route prefix.

5. **Split-controller pattern for phase-scoped sub-surfaces.** When a new workflow phase adds endpoints to a feature that already has an in-flight controller, create a sibling controller rather than editing the existing one. Examples: `AppointmentApprovalController` (`api/app/appointment-approvals`) sits beside `AppointmentController`; `AppointmentChangeRequestApprovalController` (`api/app/appointment-change-request-approvals`) sits beside `AppointmentChangeRequestController`. A cleanup PR can converge them once the in-flight controller settles.

6. **CaseEvaluationController is not yet the base.** The scaffolded base sets the localization resource but no controller currently extends it. Do not refactor all controllers to extend it without auditing each one first.

7. **Excel download-token CSRF pattern.** Controllers that serve file downloads (currently `WcabOfficeController`) use a two-step flow: `GET .../download-token` issues a short-lived `DownloadTokenResultDto`, then `GET .../as-excel-file?token=...` redeems it. Reuse this pattern for all new file-download endpoints -- do not add direct streaming endpoints without the token guard.

8. **No business logic, no DTO construction, no validation.** Controllers must remain pure passthroughs. Any transform belongs in the AppService or a domain service.

## Key Files

| File | Purpose |
|------|---------|
| `Controllers/CaseEvaluationController.cs` | Localization base (unused as actual base; extend AbpController instead) |
| `Controllers/ExternalSignups/ExternalSignupController.cs` | Establishes the `api/public/*` + `[IgnoreAntiforgeryToken]` pattern |
| `Controllers/ExternalAccount/ExternalAccountController.cs` | Password-reset public surface; rate-limited path prefix constant lives here |
| `Controllers/WcabOffices/WcabOfficeController.cs` | Reference impl for download-token Excel pattern |
| `Controllers/Appointments/AppointmentApprovalController.cs` | Example of split-controller pattern |

## Related Docs

- docs/api/API-ARCHITECTURE.md
- docs/decisions/002-manual-controllers-not-auto.md
