# Probe log: new-sec-01-appointment-route-permission-guard

**Timestamp (local):** 2026-04-24T13:29:00
**Purpose:** Confirm that `/appointments/view/:id` and `/appointments/add` currently use `authGuard` only (no `permissionGuard`, no `data.requiredPolicy`), and confirm ABP's `permissionGuard` resolution order so the remedial diff matches the guard's contract.

## Probe 1 -- static-read `app.routes.ts`

### Command

```
Read W:/patient-portal/main/angular/src/app/app.routes.ts lines 98-102
```

### Response

```
98:  {
99:    path: 'appointments/add',
100:    loadComponent: () => Promise.resolve(AppointmentAddComponent),
101:    canActivate: [authGuard],
102:  },
```

### Interpretation

`/appointments/add` uses `authGuard` only. No `permissionGuard` in `canActivate`. No `data` property. Any authenticated user can reach the add-appointment form.

## Probe 2 -- static-read `appointment-routes.ts`

### Command

```
Read W:/patient-portal/main/angular/src/app/appointments/appointment/appointment-routes.ts lines 12-21
```

### Response

```
12:  {
13:    path: 'view/:id',
14:    loadComponent: () => {
15:      return import('./components/appointment-view.component').then(
16:        (c) => c.AppointmentViewComponent,
17:      );
18:    },
19:    canActivate: [authGuard],
20:  },
21: ];
```

### Interpretation

`view/:id` (child of the `/appointments` parent path declared at `app.routes.ts:78`) uses `authGuard` only. No `permissionGuard`. No `data`. Any authenticated user with a guessed / leaked `AppointmentId` can view the full appointment detail component.

## Probe 3 -- static-read `permissionGuard` source (ABP function)

### Command

```
Read W:/patient-portal/main/angular/node_modules/@abp/ng.core/fesm2022/abp-ng.core.mjs lines 5109-5139
```

### Response (excerpt)

```
5109: const permissionGuard = (route, state) => {
5110:     const router = inject(Router);
5111:     const routesService = inject(RoutesService);
...
5116:     let { requiredPolicy } = route.data || {};
5117:     if (!requiredPolicy) {
5118:         const routeFound = findRoute(routesService, getRoutePath(router, state.url));
5119:         requiredPolicy = routeFound?.requiredPolicy;
5120:     }
5121:     if (!requiredPolicy) {
5122:         return of(true);
5123:     }
...
5128:     return permissionService.getGrantedPolicy$(requiredPolicy).pipe(take(1), map(access => {
5129:         if (access)
5130:             return true;
...
5137:         return false;
5138:     }));
```

### Interpretation

Resolution order:
1. If `route.data.requiredPolicy` is set, use it.
2. Otherwise, look up the current route's path in `RoutesService` (the menu registry) and use that entry's `requiredPolicy`.
3. If neither exists, return `of(true)` (default-allow).

Consequence for this fix: adding `permissionGuard` alone to `/appointments/view/:id` or `/appointments/add` is insufficient, because those paths are NOT registered in `RoutesService` (only the parent `/appointments` menu entry is, in `APPOINTMENT_BASE_ROUTES`). The `RoutesService` lookup would return `undefined`, and the guard would default-allow. The fix MUST also set `data: { requiredPolicy: 'CaseEvaluation.Appointments' }`.

## Probe 4 -- static-read menu registry entry

### Command

```
Read W:/patient-portal/main/angular/src/app/appointments/appointment/providers/appointment-base.routes.ts
```

### Response

```
1: import { ABP, eLayoutType } from '@abp/ng.core';
2:
3: export const APPOINTMENT_BASE_ROUTES: ABP.Route[] = [
4:   {
5:     path: '/appointments',
6:     iconClass: 'fas fa-file-alt',
7:     name: '::Menu:Appointments',
8:     layout: eLayoutType.application,
9:     requiredPolicy: 'CaseEvaluation.Appointments',
10:     breadcrumbText: '::Appointments',
11:   },
12: ];
```

### Interpretation

Only the parent `/appointments` path is menu-registered with `requiredPolicy`. The `view/:id` and `/appointments/add` child paths are NOT. This is why the list route works today (menu-registry fallback finds `'CaseEvaluation.Appointments'` for path `/appointments`) and why the view/add routes fall through to the default-allow branch even if `permissionGuard` were added without `data.requiredPolicy`.

## Probe 5 -- verify the permission constant exists in backend

### Command

```
Read W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs lines 94-101
```

### Response

```
94:    public static class Appointments
95:    {
96:        public const string Default = GroupName + ".Appointments";
97:        public const string Edit = Default + ".Edit";
98:        public const string Create = Default + ".Create";
99:        public const string Delete = Default + ".Delete";
100:   }
```

### Interpretation

`'CaseEvaluation.Appointments'` (the `.Default` constant) is the correct policy string for the fix. `.Create` is also available if Adrian later wants `/appointments/add` tightened to the create-only permission.

## Probe 6 (skipped) -- runtime UI probe via Chrome DevTools MCP

### Reason for skip

Per `probes/service-status.md:79-80`, the Angular serve was pending at Phase 1.5 completion (build still running in shell `bbc0gz15t`). The static-read of `canActivate: [authGuard]` in the TS source is conclusive -- the `canActivate` array literal IS the runtime behaviour. A live probe would also require seeding a non-admin external user (Patient / Applicant Attorney role) with a valid token, which requires state-mutating signup flows that the Live Verification Protocol forbids for state-mutating probes outside the NEW-SEC-02 exception list.

### Mitigation

Implementation PR MUST include a manual runtime verification step:
1. Seed a Patient user (or use an existing one) without `CaseEvaluation.Appointments`.
2. Log in as that user.
3. Attempt `http://localhost:4200/appointments/view/<any-guid>` -- expect redirect to home / login / 403 toast.
4. Attempt `http://localhost:4200/appointments/add` -- expect same.
5. Before the fix, both resolve to the component. After the fix, both are blocked.

Attach the runtime verification output to the PR.

## Cleanup

No mutating probes were run. No state to revert.
