# Shared -- cross-cutting Angular helpers (address, auth, components, pipes)

Spoke file; load when editing anything under `shared/`. The layer file
(`angular/src/app/CLAUDE.md`) names each sub-tree in one line -- this file
adds the implementation detail needed to use them correctly.

## address/

### AddressValidationProvider (address-validation.provider.ts)

Abstract class used as the DI token. Angular cannot inject an interface, so
the abstract class IS the token. Never add `@Injectable` to either concrete
class -- the provider factory in `app.config.ts` constructs them with `new`,
passing an injected `HttpClient` and the resolved `SmartyConfig`.

Factory logic (in `app.config.ts`):

- `environment.addressValidation.smartyKey` present -> `new SmartyAddressProvider(http, cfg)`
- key absent or empty -> `new MockAddressProvider()`

To swap vendors, replace the factory only. Do not inject `SmartyAddressProvider`
directly; it has no `@Injectable` decorator and will not resolve.

Both providers satisfy the never-throw contract:

- `autocomplete` returns `of([])` on any error or query < 3 chars.
- `validate` returns `{ status: 'error', matchesInput: true }` on provider
  failure, so booking submit is never blocked by a Smarty outage.

### MockAddressProvider (mock-address.provider.ts)

Default when no vendor key is configured. `autocomplete` returns two synthetic
candidates echoing the query (fictional data; never PHI). `validate` uppercases
and normalizes to produce `'corrected'` when the input differs, so the
standardization dialog is exercised in development.

### SmartyAddressProvider (smarty-address.provider.ts)

Uses Smarty's US Autocomplete Pro (type-ahead) and US Street Address (USPS
CASS) endpoints with the embedded website key. Both paths call `catchError`
and return the safe fallback values, so a quota hit or network error never
surfaces to the user.

### resolveStateId (state-resolver.ts)

Pure function (no DI). Accepts a USPS 2-letter code (case-insensitive) or a
full state name and returns the matching State-lookup GUID from the provided
`StateLookupOption[]`. Returns `null` -- and callers leave the State `<select>`
untouched -- when no match is found. Run after every autocomplete pick and
after every standardization result.

### ConfirmAddressDialogComponent (confirm-address-dialog.component.ts)

Inline pre-submit modal. When one or more booking-form address groups return
`status: 'corrected'`, this dialog shows "Use suggested / Keep mine" per
address. The caller builds the `AddressDiffItem[]`, passes it as `@Input`,
and applies the returned `AddressChoice` map before calling the create API.
The dialog emits choices; it never writes to the form directly.

## components/

### AppLookupSelectComponent (app-lookup-select.component.ts)

Patched fork of ABP's `LookupSelectComponent` (forked from
`@volo/abp.commercial.ng.ui` 10.0.2). The only change: `override get()` adds
`this.cdRef.markForCheck()` after `this.datas = items`. Without this, OnPush
parents skip the `@for` binding after the async load, leaving dropdowns
silently empty. IMPORTANT: use `<app-lookup-select>` everywhere a lookup-select
sits inside an OnPush parent; stock `<abp-lookup-select>` will appear to work
and then break when the parent stops receiving mutable inputs.

If ABP upgrades its template in a future release, audit
`AppLookupSelectComponent`'s copied template alongside the upgrade.

### SsnInputComponent (ssn-input.component.ts)

ControlValueAccessor. Key rules:

- Starts EMPTY every time (`writeValue` discards any stored masked value).
  An empty form submit means "leave stored SSN unchanged" -- the backend
  `PatientManager.UpdateAsync` enforces this; do not change it.
- Masks after 1.2s idle or on blur; eye toggle re-reveals typed digits.
- Copy and cut are blocked via `(copy)` and `(cut)` event handlers.
- The "on-file" reveal button is shown only when `patientId` + `currentMaskedSsn`
  are supplied AND the current user is internal OR is the record owner
  (`user.id === patientIdentityUserId`). This mirrors the server-side
  `SsnRevealAccess` predicate; the server re-checks and returns 403 if the
  client-side check is bypassed.
- Raw digit string (`string | null`) is the form value; the pipe `ssnMask` is
  for datatable columns only, never on the entry input.

## pipes/

### SsnMaskPipe (pipes/ssn-mask.pipe.ts)

Read-only display (`***-**-1234`). Pure, standalone. Use in datatables and
detail views where the stored masked last-4 is sufficient. Never bind to an
`<input>` element -- the value it produces (`***-**-XXXX`) is not a valid
digit string and will corrupt the form model.

## auth/

### hasOnlyExternalRoles vs hasAnyExternalRole (external-user-roles.ts)

Two distinct predicates; mixing them causes routing or layout bugs:

- `hasOnlyExternalRoles` -- routing decision (postLoginRedirectGuard). Returns
  true only when EVERY role is an external role and the list is non-empty.
  A mixed-role user (internal + external) returns false -> routed to dashboard.
- `hasAnyExternalRole` -- CSS toggle in app.component (hide LeptonX sidebar).
  Returns true when at least one role is external. A mixed-role user returns
  true here (they get sidebar hidden) while `hasOnlyExternalRoles` returns
  false (they still see dashboard).

External roles: `patient`, `applicant attorney`, `defense attorney`,
`claim examiner`. These mirror the seed contributor in
`Domain/Identity/ExternalUserRoleDataSeedContributor.cs`.

### postLoginRedirectGuard (post-login-redirect.guard.ts)

`CanMatchFn` on the `/` route. Must stay `canMatch` (not `canActivate`) so
the guard fires before the lazy chunk downloads -- prevents the "flash of empty
home shell" that `canActivate` caused. Three outcomes: anonymous calls
`auth.navigateToLogin()` and returns false; internal user returns a `UrlTree`
to `/dashboard`; external user returns `true`.

### performFullLogout (full-logout.ts)

IMPORTANT: never call `AuthService.logout()` directly. The library leaves
`__tenant` and `XSRF-TOKEN` cookies in place. `performFullLogout(injector)`
expires both cookies client-side before delegating to the library. The
AuthServer-side mirror (in `AuthServer/Pages/Account/Logout.cshtml.cs`) also
expires them; both sides run defensively because the SPA and AuthServer
operate on different ports and the cookie may not always be overwritten by
the server-side Set-Cookie.

### SessionIdentityWatcherService (session-identity-watcher.service.ts)

Passive listener only (iframe approach removed in Issue #107). Subscribes to
`OAuthService.events`; if the `sub` claim changes on any token event it calls
`window.location.reload()` to re-bootstrap ConfigStateService, permissions,
and guards against the new identity. Call `.start()` once from `AppComponent`.

## Related

- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/APPOINTMENT-BOOKING-FLOW.md
- docs/frontend/COMPONENT-PATTERNS.md
