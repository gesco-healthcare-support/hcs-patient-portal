# Angular Application (`angular/src/app/`)

Angular 20 standalone-component SPA that consumes the `HttpApi.Host` REST surface.
All feature modules live here; `proxy/` is auto-generated.

## What Lives Here

22 feature directories: applicant-attorneys, appointment-documents, appointment-languages,
appointment-packet, appointment-statuses, appointment-types, appointments, dashboard,
defense-attorneys, doctor-availabilities, doctor-management, doctors, external-users,
gdpr-cookie-consent, home, internal-users, locations, patients, states, wcab-offices,
plus `shared/` (cross-cutting) and `proxy/` (auto-generated).

**`shared/` sub-tree:**

- `address/` -- AddressValidationProvider (abstract class DI token), AddressAutocompleteComponent,
  ConfirmAddressDialogComponent, SmartyAddressProvider + MockAddressProvider, StateResolver
- `auth/` -- ExternalUserRoles constants, full-logout helper, postLoginRedirectGuard,
  SessionIdentityWatcherService
- `components/` -- AppLookupSelectComponent, SsnInputComponent, top-header-navbar
- `pipes/` -- SsnMaskPipe (read-only display; formats `***-**-1234`)

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
before the component activates.

**Never edit `proxy/`.** Regenerate with `abp generate-proxy` after backend DTO or
AppService changes. See root CLAUDE.md + docs/decisions/005-no-ng-serve-vite-workaround.md.

## Gotchas

### Blob downloads -- NEVER `window.open`

`window.open` opens a new tab with no Bearer token, causing 401/500 for authenticated
endpoints. ALWAYS use `HttpClient.get` with `responseType: 'blob'`:

```typescript
this.http.get(url, { responseType: 'blob', observe: 'response' }).subscribe((resp) => {
  const objectUrl = URL.createObjectURL(resp.body!);
  // create a temporary <a> and click it, then URL.revokeObjectURL
});
```

Applies to both AppointmentDocumentsComponent and AppointmentPacketComponent.

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

### AppLookupSelectComponent -- use instead of `abp-lookup-select` in OnPush parents

`<abp-lookup-select>` uses Default change detection and does not call `markForCheck()` after
async data loads, so dropdowns stay empty when the parent is OnPush. Use
`AppLookupSelectComponent` (which extends `LookupSelectComponent` and adds the missing
`cdRef.markForCheck()`) in any OnPush parent.

### `performFullLogout` -- do not call `AuthService.logout` directly

`AuthService.logout` (ABP) clears OAuth keys but leaves `__tenant` and `XSRF-TOKEN` cookies.
These cookies must be expired on logout to prevent session leaks and CSRF replay. Always call
`performFullLogout(injector)` from `shared/auth/full-logout.ts`, which expires both cookies
before delegating to `AuthService.logout`.

### SsnInputComponent -- single SSN entry surface; never pre-fill

`SsnInputComponent` is the only UI surface for entering a patient SSN. It starts EMPTY every
time (by design). An empty submit means "leave the stored SSN unchanged." Never pre-fill
with the stored value. `SsnMaskPipe` is read-only display only (`***-**-1234`); it does not
produce input-safe values.

### AppointmentPacketComponent -- polling; must stop on destroy

The component polls the packet status every 5 seconds via `setInterval`. `stopPolling()` MUST
be called in `ngOnDestroy` (already implemented). If you copy this pattern to another
component, reproduce the `ngOnDestroy` cleanup or you will create a memory leak / runaway
requests after navigation.

## Related

- docs/frontend/ANGULAR-ARCHITECTURE.md
- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/COMPONENT-PATTERNS.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/ROUTING-AND-NAVIGATION.md
- docs/decisions/005-no-ng-serve-vite-workaround.md
