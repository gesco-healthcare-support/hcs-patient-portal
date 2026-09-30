# Angular Application (`angular/src/app/`)

Angular 20 standalone-component SPA that consumes the `HttpApi.Host` REST surface.
All feature modules live here; `proxy/` is auto-generated.

## What Lives Here

One directory per feature under `angular/src/app/` (see the directory listing) plus
`shared/` (cross-cutting helpers) and `proxy/` (auto-generated; never edit by hand).

For `shared/` sub-tree detail, see `angular/src/app/shared/CLAUDE.md`.

**Root files:**

- `app.component.ts` -- root standalone component
- `app.config.ts` -- app-wide providers (auth, HTTP interceptors, locale, address DI)
- `app.routes.ts` -- top-level lazy route tree
- `route.provider.ts` -- ABP menu registration for Home, Dashboard, User Management, Change Logs and Reports

## Conventions

**Standalone components only.** No NgModules. Components declare their own imports.
Each feature's routes live in its `*-routes.ts` file and are mounted lazily from `app.routes.ts`.
The `{feature}/providers/` folders register ABP MENU entries, not routes: no layout renders
them, but `permissionGuard` falls back to them for a route with no `data.requiredPolicy`, so
they are not dead code (see docs/frontend/ROUTING-AND-NAVIGATION.md).

**Abstract + concrete component pattern -- Doctors only.** Only `doctors/` still has the ABP
Suite pair (`doctor.abstract.component.ts` + `doctor.component.ts`); do not delete its abstract
file, because Suite regeneration depends on it. Every other list page is a custom standalone
component (see docs/frontend/COMPONENT-PATTERNS.md).

**Route guards.** Use ABP `permissionGuard` in lazy route config. The root path (`/`) is
guarded by `postLoginRedirectGuard`, which is registered as `canMatch` (not `canActivate`) --
it must stay `canMatch` so the guard runs before the route is matched, enabling redirect
before the component activates. See `shared/CLAUDE.md` for implementation detail.

**Never edit `proxy/`.** Regenerate with `abp generate-proxy -t ng` (HttpApi.Host running)
after backend DTO or AppService changes. See root CLAUDE.md + docs/decisions/005-no-ng-serve-vite-workaround.md.

## Gotchas

### Blob downloads -- NEVER `window.open`

`window.open` opens a new tab with no Bearer token, causing 401/500 for authenticated
endpoints. Always use `HttpClient.get` with `responseType: 'blob'` -- create a temporary
`<a>` anchor, click it programmatically, then revoke the object URL. Applies to both
`AppointmentDocumentsComponent` and `AppointmentPacketComponent`.

### AddressValidationProvider -- abstract class DI token, not an interface

`AddressValidationProvider` is an abstract class used as the DI token (Angular cannot
inject interfaces). `SmartyAddressProvider` is NOT decorated with `@Injectable`; it is
instantiated by a `useFactory` in `app.config.ts`. The factory checks `addressValidation.smartyKey`
(exported from `src/environments/environment*.ts`) and falls back to `MockAddressProvider` when the
key is empty. To swap vendors, replace the
factory -- do not try to inject `SmartyAddressProvider` directly.

### AppointmentAddComponent -- FormGroup lives here only

The routed booking page is `appointments/wizard/appointment-wizard.component.ts`
(`AppointmentWizardComponent`), which EXTENDS `AppointmentAddComponent`. The reactive
`FormGroup`, every cascade subscription, and the submit call live in `AppointmentAddComponent`;
the wizard adds only the stepper, per-step validation, drafts and the leave prompt. The nine
section components in `appointments/sections/` render controls only: they receive the form (or a
slice of state) as inputs and own no form-building logic or HTTP calls.

### AppLookupSelectComponent, performFullLogout, SsnInputComponent

See `angular/src/app/shared/CLAUDE.md` for detailed rules on each.

### AppointmentPacketComponent -- polling; must stop on destroy

The component polls the packet status every 5 seconds via `setInterval`. `stopPolling()` MUST
be called in `ngOnDestroy` (already implemented). If you copy this pattern to another
component, reproduce the `ngOnDestroy` cleanup or you will create a memory leak / runaway
requests after navigation.

## Notable single-component features

**users** (`InternalUsersHubComponent`, `users/internal-users-hub.component.ts`) -- one hub
mounted at `/users/invite`, `/users/pending`, `/users/internal` and `/users/tenants`, each route
gated by its own policy. It replaced `InternalUsersFormComponent` (`internal-users/`) and
`InviteExternalUserComponent` (`external-users/`), which are still on disk but no route or
component uses them.

- **Creating staff:** the role allow-list (`CREATABLE_INTERNAL_ROLES` in `users/users-hub.util.ts`:
  Staff Supervisor, Intake Staff) mirrors the backend `CreatableRoleNames`. Staff are HOST logins,
  so the request carries no office: `InternalUsersAppService` creates the user at host level and
  office access is granted later on `/host/intake-assignments`. The temporary password is never
  shown; it is emailed.
- **Inviting external users:** at host scope the invite requires a practice (office) choice; inside
  an office the current office is used. The invite link can be copied from the result as a
  fallback when mail does not arrive. When the email already has an account the server issues
  nothing and says so. `ExternalUserType` is NUMERIC (`Patient=1, ClaimExaminer=2,
ApplicantAttorney=3, DefenseAttorney=4`); the dropdown order in `INVITE_ROLE_OPTIONS`
  (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) differs from the numeric order on
  purpose -- do not reorder.

## Related

- docs/frontend/ANGULAR-ARCHITECTURE.md
- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/COMPONENT-PATTERNS.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/ROUTING-AND-NAVIGATION.md
- docs/decisions/005-no-ng-serve-vite-workaround.md
