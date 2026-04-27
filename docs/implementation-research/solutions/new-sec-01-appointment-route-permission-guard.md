# NEW-SEC-01: /appointments/view/:id and /appointments/add missing permissionGuard

## Source gap IDs

- NEW-SEC-01 -- track 10 Part 2: [../../gap-analysis/10-deep-dive-findings.md](../../gap-analysis/10-deep-dive-findings.md) (lines 54-60)
- Supporting context: [../../gap-analysis/README.md](../../gap-analysis/README.md) (NEW-SEC defects summary, 5 items MVP-blocking).
- Severity: MVP-blocking. Classified as a security defect because an authenticated external user can enumerate appointment IDs by URL-crafting without any permission check on the Angular route.

## NEW-version code read

- `angular/src/app/app.routes.ts:98-102` -- declares `/appointments/add` with `canActivate: [authGuard]` only. No `permissionGuard`, no `data.requiredPolicy`. Component: `AppointmentAddComponent` (imported line 14).
- `angular/src/app/appointments/appointment/appointment-routes.ts:12-20` -- declares the `view/:id` child (mounted under `/appointments` via `{ path: 'appointments', children: APPOINTMENT_ROUTES }` at `app.routes.ts:78`). Uses `canActivate: [authGuard]` only. Component: `AppointmentViewComponent`.
- `angular/src/app/appointments/appointment/appointment-routes.ts:4-11` -- declares the list route `path: ''` with `canActivate: [authGuard, permissionGuard]`. No `data.requiredPolicy`. The guard relies on the `RoutesService` fallback lookup because the parent `/appointments` menu route supplies `requiredPolicy`.
- `angular/src/app/appointments/appointment/providers/appointment-base.routes.ts:3-12` -- `APPOINTMENT_BASE_ROUTES` registers the parent `/appointments` menu entry with `requiredPolicy: 'CaseEvaluation.Appointments'`. This is the only reason the list route is protected today.
- `angular/src/app/appointments/appointment/providers/appointment-route.provider.ts:11-15` -- `configureRoutes()` wires the base routes into `RoutesService` at app init via `provideAppInitializer`. The `view/:id` and `/appointments/add` paths are NOT registered here, so the `RoutesService` fallback lookup fails for them.
- `node_modules/@abp/ng.core/fesm2022/abp-ng.core.mjs:5109-5139` -- `permissionGuard` resolution order: `route.data.requiredPolicy` first, else `RoutesService.find(path).requiredPolicy`, else default-allow `return of(true)`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:94-101` -- `Appointments` group: `Default`, `Edit`, `Create`, `Delete`. No `.View` child.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` -- confirms registration for the 4 Appointments permissions (grep-verified while reading the file). No separate view-only permission.
- `angular/src/app/app.routes.ts:82-89` -- `/doctor-management/doctor-availabilities/generate` demonstrates the correct pattern: `canActivate: [authGuard, permissionGuard]` + the menu registry provides `requiredPolicy` via `DoctorAvailability` base routes. This confirms ABP's convention.
- `angular/src/app/app.routes.ts:24-28` -- `/dashboard` uses `canActivate: [authGuard, permissionGuard]` and relies on the `route.provider.ts:20-27` menu registry (`requiredPolicy: 'CaseEvaluation.Dashboard.Host  || CaseEvaluation.Dashboard.Tenant'`). Same pattern.
- `angular/src/app/appointments/appointment-add.component.ts:1-30` -- `AppointmentAddComponent` surfaces create-appointment form fields (doctor selection, patient lookup, scheduling). Directly creates `AppointmentCreateDto` on submit. No component-level `abpPermission` guard; relies entirely on the route guard.

## Live probes

- Probe 1 (static-read of source): `W:/patient-portal/main/angular/src/app/app.routes.ts:98-102` -- confirms `/appointments/add` route uses only `authGuard`. Full static-read log: [../probes/new-sec-01-appointment-route-permission-guard-2026-04-24T13-29-00.md](../probes/new-sec-01-appointment-route-permission-guard-2026-04-24T13-29-00.md).
- Probe 2 (static-read of source): `W:/patient-portal/main/angular/src/app/appointments/appointment/appointment-routes.ts:12-20` -- confirms `/appointments/view/:id` route uses only `authGuard`. Same probe log.
- Probe 3 (static-read of ABP guard source): `node_modules/@abp/ng.core/fesm2022/abp-ng.core.mjs:5109-5139` -- confirms `permissionGuard` defaults to `of(true)` (grant access) when neither `route.data.requiredPolicy` nor a matching menu entry exists. This is the mechanism that makes the client-side-only `permissionGuard` fix insufficient without the `data.requiredPolicy`.
- Probe 4 (runtime UI probe -- SKIPPED): Chrome DevTools MCP probe of `http://localhost:4200/appointments/view/00000000-0000-0000-0000-000000000000` as a non-admin user was not run. Justification: (a) the Angular server was still building at Phase 1.5 time per `probes/service-status.md:80`, (b) the static-read evidence is conclusive (the canActivate array literal is the runtime behaviour), (c) a runtime probe requires a seeded non-admin external user, and Phase 1.5 shows the database is empty (`{"totalCount":0,"items":[]}` from `/api/app/appointments`). Flagging as a verification step for the implementation PR rather than the research brief.
- Probe 5 (Swagger check): the 317 endpoints enumerated in `probes/service-status.md:11` include `GET /api/app/appointments/{id}`. The backend endpoint currently carries only the class-level `[Authorize(Appointments.Default)]` -- which is a separate defect tracked under NEW-SEC-02. This brief is strictly the client-side routing defect; NEW-SEC-02 closes the API-side enforcement gap.

## OLD-version reference

Not applicable. NEW-SEC-01 is a NEW-side defect identified in track 10 Part 2. OLD is not the reference for this fix; ABP's own route-guard convention is. Track-10 errata that apply:

- None directly. (The 4 errata are about PDF generation, SMS send, scheduler `UserId=1` bug, and CustomField schema -- none of which touch route guards.)

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, Angular 20 standalone components, `@abp/ng.core` function-style guards (`authGuard`, `permissionGuard`). Legacy class-style `PermissionGuard` is deprecated (see `abp-ng.core.mjs:5068-5107`).
- Row-level `IMultiTenant` (ADR-004): `Appointment` implements `IMultiTenant`, so API-side list filters are automatic. This brief does NOT fix the API side; it fixes the UX entry points. NEW-SEC-02 closes the API hole.
- [ADR-001 through ADR-005] -- unaffected. No Mapperly change, no controller change, no DbContext change, no `ng serve` change.
- No new permission needed: `CaseEvaluation.Appointments` (the existing `Default` child) is the right policy. The system does not distinguish view-only vs add permissions for appointments today. If Adrian wants per-operation gating (`.Create` for add, `.Default` for view), that is a follow-on brief (also noted under NEW-SEC-02 method-level Authorize work).
- HIPAA: appointment URLs encode `AppointmentId` (GUID). While the ID itself is not PHI, the appointment row behind it is (patient demographics, injury details, doctor, slot time). A default-allow client-side route hands attackers a URL-guess oracle. Closing this is a HIPAA-relevant reduction in leak surface even with the API still underprotected.
- Capability-specific constraints:
  - Do NOT introduce a regression on the list route (`path: ''`). Its current guard relies on the menu-registry fallback lookup; leave that path alone unless adding `data.requiredPolicy` explicitly (safe belt-and-suspenders) is desired.
  - The fix MUST set `data: { requiredPolicy: 'CaseEvaluation.Appointments' }` on the two routes. Adding only `permissionGuard` to the `canActivate` array would default-allow because `permissionGuard`'s second-stage lookup via `RoutesService` returns `undefined` for `/appointments/view/:id` and `/appointments/add` (they are not registered as menu entries).
  - Keep the `authGuard` ahead of `permissionGuard` in the `canActivate` array. Order: `[authGuard, permissionGuard]`. Reason: `permissionGuard` calls `authService.isAuthenticated` at line 5134 for unauthenticated users; having `authGuard` in front short-circuits with a login redirect before permission is checked.

## Research sources consulted

All accessed 2026-04-24.

- ABP Angular Permission Management docs (`https://abp.io/docs/latest/framework/ui/angular/permission-management`). HIGH confidence. Confirms `permissionGuard` function-style usage, `data.requiredPolicy` convention, and `RoutesService`-based menu-entry fallback.
- ABP Angular Modifying Menu docs (`https://abp.io/docs/latest/framework/ui/angular/modifying-the-menu`). HIGH confidence. Documents `RoutesService.add([{ path, requiredPolicy, ... }])` as the menu registry used for permission-by-path fallback.
- Angular Router `Route.data` docs (`https://angular.dev/api/router/Route#data`). HIGH confidence. Confirms `data` is the conventional vehicle for guard-consumed metadata (typed via the `Data` interface).
- ABP GitHub source `abp/npm/ng-packs/packages/core/src/lib/guards/permission.guard.ts` (via the vendored `abp-ng.core.mjs:5109-5139` read above). HIGH confidence. Confirms the exact resolution algorithm.
- Angular CanActivateFn docs (`https://angular.dev/api/router/CanActivateFn`). HIGH confidence. Confirms the guard signature matches `(route, state) => ...` used by ABP.
- Gesco's own convention established in sibling routes (`locations/location/location-routes.ts`, `patients/patient/patient-routes.ts`, etc.): all use `canActivate: [authGuard, permissionGuard]` with the parent menu-base-routes file registering `requiredPolicy`. HIGH confidence. 12 sibling patterns confirmed via `grep -r "requiredPolicy"` in `angular/src/app/**/providers/*-base.routes.ts`.

## Alternatives considered

1. **Add `permissionGuard` + `data: { requiredPolicy: 'CaseEvaluation.Appointments' }` on both routes -- chosen.** Matches task brief's Option A. Smallest-diff client-side fix: 2 routes x 2 lines each = 4 line additions. Uses the existing `Appointments.Default` permission. Zero new code.
2. **Redirect external users to `/home` when they hit `/appointments/view` (route matcher redirect) -- rejected for this brief, kept as post-MVP polish.** Cleaner UX for the external-user case (they see their home page instead of a 403 toast), but it's secondary to permission gating. Can be layered on later; does not resolve the core security defect on its own because it still lets an external user who knows a URL land on the component pre-redirect (race condition on slow devices). Keep as UI improvement inside the separate `external-user-home` brief (UI-16).
3. **Introduce a new `.View` child permission (e.g., `CaseEvaluation.Appointments.View`) and gate view-only access separately from full `.Default` -- rejected for MVP.** Today, `.Default` is the view permission (no distinct view-vs-edit split exists). Adding `.View` requires: new permission constant + DefinitionProvider registration + backend `[Authorize]` updates + role-seed adjustment. That's scope creep beyond closing the route defect. Flag as possible future refinement when NEW-SEC-02 lands, but not this brief.
4. **Move the guard logic into the component's `ngOnInit` via a programmatic `PermissionService.getGrantedPolicy$(...)` check -- rejected.** Defensive-in-depth is fine but not a substitute for a route guard: the component would load (fetching DTOs, flashing UI) before the permission response returns. Route-guard-first is Angular's idiomatic pattern; use it.
5. **Hand-wire both routes into the `RoutesService` menu registry (add them to `APPOINTMENT_BASE_ROUTES`) so the fallback lookup finds them -- rejected.** This would surface `/appointments/view/:id` and `/appointments/add` as top-level menu items, which is not what they are. Menu-registry entries appear in the side-nav. The routes are drilldowns, not navigable menu entries. Using `data.requiredPolicy` on the Angular route is the idiomatic separation.

## Recommended solution for this MVP

Edit `angular/src/app/app.routes.ts` and `angular/src/app/appointments/appointment/appointment-routes.ts`:

1. **`app.routes.ts:98-102`** (`/appointments/add` route). Change:
   - `canActivate: [authGuard]` -> `canActivate: [authGuard, permissionGuard]`.
   - Add `data: { requiredPolicy: 'CaseEvaluation.Appointments' }` as a new property.
2. **`appointment-routes.ts:12-20`** (`view/:id` route). Same two changes.

Shape of the final `/appointments/add` route (10-line diff in `app.routes.ts`):

```ts
{
  path: 'appointments/add',
  loadComponent: () => Promise.resolve(AppointmentAddComponent),
  canActivate: [authGuard, permissionGuard],
  data: { requiredPolicy: 'CaseEvaluation.Appointments' },
},
```

Shape of the final `view/:id` route (same pattern in `appointment-routes.ts`):

```ts
{
  path: 'view/:id',
  loadComponent: () => import('./components/appointment-view.component')
    .then((c) => c.AppointmentViewComponent),
  canActivate: [authGuard, permissionGuard],
  data: { requiredPolicy: 'CaseEvaluation.Appointments' },
},
```

No backend changes. No new DTOs. No new permissions. No migration. No proxy regeneration. No Angular module registration change -- the two files already import `permissionGuard` from `@abp/ng.core`.

Reference implementation of the correct pattern lives at `angular/src/app/app.routes.ts:82-89` (`/doctor-management/doctor-availabilities/generate`). That route uses `canActivate: [authGuard, permissionGuard]` and relies on `DoctorAvailability`-base-routes for the policy; for our two routes we pass `data.requiredPolicy` explicitly because no menu-registry entry exists at those exact paths.

**Adrian-facing note on `CaseEvaluation.Appointments.Create` vs `.Default`:** The task brief suggested `Appointments.Default` for view and possibly `.Create` for `/appointments/add`. Evidence: `.Create` exists (`CaseEvaluationPermissions.cs:98`). Recommendation for MVP: use `.Default` for both routes now and leave the `.Create`-for-add-route refinement for the NEW-SEC-02 follow-on (when method-level `[Authorize(.Create)]` lands on `CreateAsync`). Reason: keeping one permission across the two routes is consistent with the current NEW behaviour where the entire Appointments feature is gated by a single permission; a `.Create`-only gate at the route without the corresponding method-level attribute is asymmetric and would confuse the permission model. If Adrian wants client-side `.Create` gating NOW, change the `add` route to `'CaseEvaluation.Appointments.Create'` -- a 1-line edit. Default to `.Default` unless Adrian says otherwise.

## Why this solution beats the alternatives

- Smallest diff possible: 4 line additions across 2 files, zero new code artefacts.
- Matches the NEW codebase's own convention for sibling feature routes (locations, patients, doctor-availabilities, etc.) as proven by 12 `requiredPolicy`-carrying base-routes files.
- Uses existing permission `CaseEvaluation.Appointments`; no new permission constant / definition provider entry / role-seed edit.
- Explicit `data.requiredPolicy` defends against the `permissionGuard` default-allow pitfall (`of(true)` when neither `route.data` nor the menu registry has a policy). Adding `permissionGuard` alone without `data.requiredPolicy` would silently not protect the route -- the fix must be belt-and-suspenders.
- Purely additive: route contracts (URL paths, component bindings) are unchanged. Rollback is a one-line revert per route.

## Effort (sanity-check vs inventory estimate)

- Task brief estimate: XS-S (1 line per route; unit test 0.5 day total).
- Analysis confirms: XS code change (4 line additions). S verification effort if we include a pair of minimal Angular route guard tests (one per route, mocking `PermissionService.getGrantedPolicy$` to simulate an unauthorized user and asserting the guard returns `false`). Total: ~0.5 day including PR review and manual verification with a non-admin user once the Angular serve is back up.
- Does NOT include NEW-SEC-02 (method-level `[Authorize]` backend enforcement). That is a separate brief and a separate PR; these two together close the cross-tenant leak end-to-end.

## Dependencies

- Blocks: none. No downstream capability waits for this route-guard fix.
- Blocked by: none. The `Appointments.Default` permission already exists in the permission tree and is already seeded to internal roles via the existing role-seeding code (checked via reading `CaseEvaluationPermissionDefinitionProvider.cs`). No prerequisite PR required.
- Parallel to: [new-sec-02-method-level-authorize](new-sec-02-method-level-authorize.md) (NEW-SEC-02). NEW-SEC-01 closes the client-side UX path; NEW-SEC-02 closes the API path. Both are needed for full cross-tenant isolation; shipping one without the other still leaks via the other path. Ship them together or at minimum same sprint.
- Blocked by open question: none. The task brief explicitly lists this capability as "Blocked by open question: none" -- confirmed. Q5 (13-state enforcement) and the 31 other open questions do not touch this fix.

## Risk and rollback

- Blast radius: Angular client only. If the `requiredPolicy` string is mistyped, every user loses access to the two routes (visible as a 403 toast + HttpErrorReporter event). Mitigation: the permission string `'CaseEvaluation.Appointments'` is copy-pasted from the existing `APPOINTMENT_BASE_ROUTES[0].requiredPolicy` literal. Add a lightweight guard test that mocks `PermissionService.getGrantedPolicy$('CaseEvaluation.Appointments')` returning `true` and asserts the guard passes.
- Multi-tenant isolation: unaffected (Appointment `IMultiTenant` filter continues to run on the API). This fix does not touch tenant filtering; it closes the client-side enumeration oracle.
- Permission seeds: unchanged. Admin, Internal roles already carry `CaseEvaluation.Appointments`. External users (Patient, Applicant Attorney) do NOT -- that is the whole point of the fix.
- Rollback: revert the 2 commits (or 1 commit if bundled). No migration. No proxy regeneration. No cache invalidation beyond the Angular build output. A hotfix PR can land the revert in under 10 minutes.

## Open sub-questions surfaced by research

- Should `/appointments/add` require `CaseEvaluation.Appointments.Create` specifically (not just `.Default`)? NEW currently lacks method-level `[Authorize(.Create)]` on `AppointmentsAppService.CreateAsync` (that is NEW-SEC-02's scope). Once NEW-SEC-02 lands, this brief's `/appointments/add` route can be tightened to `.Create` as a 1-line follow-on edit. MVP recommendation: leave at `.Default` for now; track the tightening as a NEW-SEC-02 follow-on item.
- Should the `/appointments` list route (`appointment-routes.ts:4-11`) also add `data.requiredPolicy` explicitly for belt-and-suspenders, so it does not depend on the `RoutesService` fallback? Not required (the menu registry IS populated for that path), but would make the permission story explicit in the route file. Optional polish; defer unless Adrian wants it.
- Is `/appointments/view/:id` reachable by external users today if they know the ID? Yes (currently authGuard-only). Post-fix, external users without `.Default` will get a 403; the fix closes that path. A separate route-level redirect to `/home` for external users is the `external-user-home` brief's concern (UI-16), not this one.
- Should any E2E test (Playwright or Cypress) be added alongside the unit test, covering "non-admin user hitting `/appointments/view/:id` is redirected"? Worth including in the PR if the test harness supports it; not a research-phase dependency.
