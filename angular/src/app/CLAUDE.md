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
- `route.provider.ts` -- lazy-loaded feature route registration

## Conventions

**Standalone components only.** No NgModules. Components declare their own imports;
features register routes via provider functions in `{feature}/providers/` folders and
are lazily imported from `app.routes.ts`.

**Abstract + concrete component pattern.** List pages have `{entity}.abstract.component.ts`
(ABP Suite base) and `{entity}.component.ts` (concrete). Never delete the abstract file;
ABP Suite regeneration depends on it.

**Route guards.** Use ABP `permissionGuard` in lazy route config. The root path (`/`) is
guarded by `postLoginRedirectGuard`, which is registered as `canMatch` (not `canActivate`) --
it must stay `canMatch` so the guard runs before the route is matched, enabling redirect
before the component activates. See `shared/CLAUDE.md` for implementation detail.

**Never edit `proxy/`.** Regenerate with `abp generate-proxy` after backend DTO or
AppService changes. See root CLAUDE.md + docs/decisions/005-no-ng-serve-vite-workaround.md.

## Gotchas

### Blob downloads -- NEVER `window.open`

`window.open` opens a new tab with no Bearer token, causing 401/500 for authenticated
endpoints. Always use `HttpClient.get` with `responseType: 'blob'` -- create a temporary
`<a>` anchor, click it programmatically, then revoke the object URL. Applies to both
`AppointmentDocumentsComponent` and `AppointmentPacketComponent`.

### AddressValidationProvider -- abstract class DI token, not an interface

`AddressValidationProvider` is an abstract class used as the DI token (Angular cannot
inject interfaces). `SmartyAddressProvider` is NOT decorated with `@Injectable`; it is
instantiated by a `useFactory` in `app.config.ts`. The factory checks `environment.smartyKey`
and falls back to `MockAddressProvider` when the key is absent. To swap vendors, replace the
factory -- do not try to inject `SmartyAddressProvider` directly.

### AppointmentAddComponent -- FormGroup lives here only

The reactive `FormGroup`, every cascade subscription, and every submit/validation call live
exclusively in `AppointmentAddComponent`. The 7 section children
(`appointment-add-*.component.ts`) are template-only: they receive `@Input() form: FormGroup`
plus primitive inputs and render template controls. Sections own no form-building logic.

### AppLookupSelectComponent, performFullLogout, SsnInputComponent

See `angular/src/app/shared/CLAUDE.md` for detailed rules on each.

### AppointmentPacketComponent -- polling; must stop on destroy

The component polls the packet status every 5 seconds via `setInterval`. `stopPolling()` MUST
be called in `ngOnDestroy` (already implemented). If you copy this pattern to another
component, reproduce the `ngOnDestroy` cleanup or you will create a memory leak / runaway
requests after navigation.

## Notable single-component features

**internal-users** (`InternalUsersFormComponent`) -- no abstract base. Branches on
`currentTenant.id`: IT Admin gets an editable picker (GET `/api/app/internal-users/tenants`);
tenant admin gets a disabled pre-filled dropdown. Use `form.getRawValue()` on submit --
`form.value` silently drops disabled controls. Temporary password is never shown (emailed
via Hangfire only). Role allow-list (`Clinic Staff`, `Staff Supervisor`) mirrors backend
`CreatableRoleNames`. After `form.reset()`, re-apply `disable()` when `tenantLocked()`.

**external-users** (`InviteExternalUserComponent`) -- posts a tokenized invite; response
`inviteUrl` is shown with a Copy button as SMTP fallback (do not remove it). `ExternalUserType`
is NUMERIC (`Patient=1, ClaimExaminer=2, ApplicantAttorney=3, DefenseAttorney=4`). Dropdown
order (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) differs from numeric
order intentionally -- do not reorder. Permission: `CaseEvaluation.UserManagement.InviteExternalUser`.

## Related

- docs/frontend/ANGULAR-ARCHITECTURE.md
- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/COMPONENT-PATTERNS.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/ROUTING-AND-NAVIGATION.md
- docs/decisions/005-no-ng-serve-vite-workaround.md
