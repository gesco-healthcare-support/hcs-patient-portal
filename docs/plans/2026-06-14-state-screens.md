---
status: draft
date: 2026-06-14
slug: state-screens
branch: feat/frontend-rework
surface: global (external + internal) + external compositions
backend: none
related:
  - "design_handoff_appointment_portal/State Screens - Redesign.html"
  - "design_handoff_appointment_portal/components/st-after.jsx"
  - "design_handoff_appointment_portal/styles/st-states.css"
  - "design_handoff_appointment_portal/styles/pp-public.css"
---

# Plan: State Screens (skeleton, empty, error, 403, 404, session-timeout, offline)

## Goal

Build the redesigned shared state screens. The message-type screens (error/403/404/
session-timeout/offline) are built ONCE on a chrome-less layout so they cover both
external and internal users without rework when internal lifecycle pages land next.
Skeleton + empty are shared primitives with surface-specific compositions; build the
primitive plus the external composition now.

## Context and constraints

- Angular 20 standalone components + SCSS; ABP Commercial 10.0.2; brand fixed
  (#055495 / #9dc13b / Roboto) via existing tokens.
- Tokens already ported: `angular/src/styles/_tokens.scss` + `_ap-utilities.scss`
  (imported in `styles.scss`). Reference custom properties; never hard-code token values.
- Icon component exists: `<app-icon name="..." [size]="n" [label]="..."/>`
  (`angular/src/app/shared/ui/icon/icon.component.ts`), names from `ICON_PATHS`.
- Today there is ONE global error surface: ABP `withHttpErrorConfig({ errorScreen:
  { component: HttpErrorComponent, forWhichErrors: [401,403,404,500] } })`
  (`angular/src/app/app.config.ts:157`), LeptonX-themed.
- No wildcard `**` route exists. Session expiry today = hard redirect to AuthServer via
  `performFullLogout` (`shared/auth/full-logout.ts`, called from `app.component.ts`).
- No PHI / no backend changes. ASCII only. Entrance animations transform-only (the
  `sksh` shimmer uses `background-position`, which is safe).

## Approved design decisions

1. Session-timeout = branded in-app screen, then redirect. On a mid-session 401 we show
   the amber "Your session has expired" screen; its "Sign in again" CTA calls
   `performFullLogout` (redirect to AuthServer, tenant subdomain preserved).
2. We own 403/404/500 by swapping ABP's `errorScreen.component` to our own; 401 routed to
   the session-timeout variant. ABP's LeptonX error page is no longer user-visible.
3. Offline = app-level overlay (preserves state, auto-dismiss on reconnect), not a route.

## Architecture

Taxonomy:
- GLOBAL (render on `eLayoutType.empty`, no shell -> identical for both surfaces):
  error, 403, 404, session-timeout, offline.
- SURFACE-SPECIFIC (shared primitive, per-surface composition): skeleton, empty.

Prototype mapping (`components/st-after.jsx`):
- All message states are `StMsg` = the `pp-*` centered card (logo top, `pp-ic` tone
  badge, h1, lead, `pp-actions`, support footer). The SAME shell the Public Pages
  rework will consume -> Task 1 produces a reusable `pp` shell partial.
- Exact copy/tone/icon per state (from the prototype):

  | State   | tone  | icon    | title                      | CTA              |
  |---------|-------|---------|----------------------------|------------------|
  | error   | red   | alert   | Something went wrong       | Try again        |
  | notfound| blue  | search  | Page not found             | Back to home     |
  | session | amber | clock   | Your session has expired   | Sign in again    |
  | offline | amber | alert   | You're offline             | Retry            |
  | 403     | red   | (lock?) | (no prototype - see Q1)    | Back to home     |

Components (new, under `angular/src/app/shared/`):

| Unit | Path | Purpose |
|---|---|---|
| `StateMessageComponent` | `ui/state-message/` | Presentational primitive: inputs `tone`, `icon`, `title`, `lead`, `actions[]`. Renders the `pp` shell. |
| `AppHttpErrorComponent` | `ui/state-message/` | ABP `errorScreen` target: status -> StateMessage variant (403/404/500; 401 -> session). |
| `NotFoundComponent` | `ui/not-found/` | Wildcard `**` route target (client-side bad routes). Thin StateMessage notfound wrapper. |
| `OfflineDetectionService` | `services/` (new folder) | `navigator.onLine` + window online/offline events -> signal. |
| `OfflineOverlayComponent` | `ui/offline/` | StateMessage offline variant, rendered app-level in `AppComponent`. |
| `SkeletonComponent` | `ui/skeleton/` | Shimmer primitive (`sk` base + width/height/shape inputs), reduced-motion safe. |
| `EmptyStateComponent` | `ui/empty-state/` | Icon + title + body + optional CTA. |

## Tasks (one-by-one; commit after each; no backend in any)

### Task 1 - StateMessage primitive + pp shell + icon/utility prerequisites  [test-after]
- Verify `ICON_PATHS` contains: `alert`, `search`, `clock`, `logout`, `refresh`, `home`,
  `inbox`, `plus`. Add any missing from `design_handoff_appointment_portal/components/icons.js`.
- Verify `af-btn` / `af-btn--primary` / `af-btn--lg` / `pp-btn-lg` utilities exist
  (`_ap-utilities.scss` or `styles/after.css`); port from the design if missing.
- Port the `pp-*` shell (gradient bg, `pp-top` logo+tag, `pp-card`, `pp-ic` tones,
  `pp-actions`/`--single`, `pp-foot`) from `styles/pp-public.css` into a shared partial
  `angular/src/styles/_pp-shell.scss` (reused by Public Pages later). No stray hex.
- Build `StateMessageComponent` (OnPush): inputs `tone: 'blue'|'amber'|'red'`,
  `icon: IconName`, `title: string`, `lead: string`,
  `actions: { label; icon?; routerLink?; click? }[]`. Logo + clinic name from
  `BrandingAppService` with a generic fallback; footer support line (see Q2).
- Acceptance: renders all five variants pixel-close (manual harness or test).
- Tests: inputs -> rendered tone class, icon name, title, action count/labels.

### Task 2 - NotFoundComponent + wildcard route  [code]
- `NotFoundComponent` renders StateMessage `notfound` (blue/search); "Back to home" -> `/`.
- Edit `app.routes.ts`: append `{ path: '**', data: { layout: eLayoutType.empty },
  loadComponent: () => NotFoundComponent }` as the LAST entry (after all ABP lazy modules).
- Acceptance: unknown URL (e.g. `/garbage`) shows branded 404 on empty layout (no LeptonX
  sidebar) for both external and internal users.
- Verify: route ordering does not shadow ABP module routes; `eLayoutType.empty` drops shell.

### Task 3 - AppHttpErrorComponent (403/404/500) + swap into withHttpErrorConfig  [test-after]
- FIRST: verify the ABP 10.0.2 `errorScreen` component contract -- how a custom component
  receives the error (status, message). Inspect `@abp/ng.theme.shared` `HttpErrorComponent`
  / `HttpErrorConfig` in `node_modules` (or ABP docs). Do not assume the injection shape.
- Build `AppHttpErrorComponent`: read status -> StateMessage variant: 403 -> forbidden,
  404 -> notfound, 500/other 5xx -> error.
- Edit `app.config.ts`: swap `errorScreen.component` to `AppHttpErrorComponent`; keep
  `forWhichErrors: [401,403,404,500]` (401 handled in Task 4).
- Acceptance: API 403/404/500 render branded screens on both surfaces; LeptonX error page
  never appears.
- Tests: status -> variant mapping unit test.

### Task 4 - Session-timeout (401 path)  [test-after]
- Extend `AppHttpErrorComponent`: 401 -> session variant (amber/clock); CTA -> `performFullLogout`.
- Verify: anonymous-initial loads are already routed to AuthServer by
  `postLoginRedirectGuard` BEFORE any API call, so a 401 reaching the error screen means
  expired-mid-session. Confirm ABP's OAuth 401 handling (token refresh) does not pre-empt
  or double-handle the error screen.
- Acceptance: expire the token in localStorage, trigger a request -> session screen; CTA
  redirects to AuthServer `/Account/Login` on the current tenant subdomain.
- Risk: highest-risk task (401 vs OAuth refresh interplay). Budget extra live verification.

### Task 5 - OfflineDetectionService + overlay  [test-after]
- `OfflineDetectionService` (providedIn root): signal seeded from `navigator.onLine`,
  updated by `fromEvent(window, 'online'|'offline')`.
- `OfflineOverlayComponent`: StateMessage offline variant; Retry re-checks `navigator.onLine`.
- Edit `app.component.ts`: start the service; render the overlay conditionally in the
  template (full-screen, above app). Auto-dismiss when back online.
- Acceptance: DevTools offline -> overlay; back online -> dismiss. Both surfaces.
- Tests: service event -> signal; overlay shows when signal offline.

### Task 6 - SkeletonComponent + external-home/detail loading compositions  [code]
- `SkeletonComponent`: `sk` shimmer primitive with `width`/`height`/`shape`(bar|circle|pill)
  inputs; `sksh` keyframe; `prefers-reduced-motion` disables animation. Port `.sk` /
  `.st-skel-*` / `.st-skel-onlight` from `styles/st-states.css`.
- External-home loading composition: real navbar + hero shimmer (`st-skel-onlight`) +
  2 action-card skeletons + N row skeletons. Replace the `@if (loading())` `ext-empty`
  block (`home/external-home.component.html:201`).
- External-detail loading: replace `@if (isLoading) { <div class="ad-empty">...}`
  (`external-appointment-detail.component.html:14`) with a detail-shaped block skeleton.
- Acceptance: home/detail show shimmer matching the prototype; reduced-motion static.

### Task 7 - EmptyStateComponent + external-home empty composition  [code]
- `EmptyStateComponent`: `icon`, `title`, `body`, optional `cta {label,icon,click}`.
  Port `.st-empty`.
- Replace external-home `@else if (filtered().length === 0)` `ext-empty` block
  (`home/external-home.component.html:206`) with `<app-empty-state>` (inbox icon,
  "No appointment requests yet", CTA -> request appointment).
- Acceptance: empty list shows branded empty state with a working CTA.

## Internal fold-in (why this is built once)

- Tasks 2-5 are global: the wildcard 404, the AppHttpError screen (403/404/500/401), and
  the offline overlay render on `eLayoutType.empty` or app-level, so internal users get
  them today with zero extra work.
- Tasks 1, 6, 7 produce shared primitives (StateMessage, Skeleton, EmptyState). Internal
  page reworks (Prompts 9-16) reuse the primitives and only add their own compositions
  (sidebar-shell skeleton shapes, list empty states) -- no re-build of the screens.

## Risks

- ABP `errorScreen` injection contract is version-specific (Task 3 first step).
- 401 vs OAuth token-refresh interplay (Task 4) -- the riskiest behavior change; the error
  swap has app-wide blast radius.
- Icon registry / `af-btn` utilities may need extending (Task 1) -- verify, do not assume.
- `pp` shell is shared with Public Pages; keep it a reusable partial to avoid duplication.
- LeptonX shell bleed on empty-layout screens -- confirm no sidebar/`redesign-shell` leakage.

## Open questions

1. **403 copy/icon** -- the prototype has no 403 design. Reuse error styling, or define
   "You don't have access" (tone red, `lock` icon)? Need wording + icon confirmation.
2. **Support footer** -- `pp-foot` shows "support@clinic.example". Live from tenant
   settings/`BrandingAppService`, or a generic "contact your clinic" fallback? Default to
   generic fallback unless branding provides it.
3. **Detail/wizard/profile skeletons** -- Task 6 covers home (full) + detail (block).
   Wizard/profile skeletons deferred unless wanted now.

## Verification (overall)

- Per task: Angular unit/integration tests (integration-weighted per testing policy).
- `tsc --noEmit` + `ng lint` clean; no new console errors.
- Live smoke once the stack is up: unknown URL (404), DevTools offline (overlay), expired
  token (session), forced API 403/404/500 (error screens) -- as each of the 7 roles where
  reachable.

## Rollback

- Blast radius: the `errorScreen.component` swap (Task 3/4) affects all users.
- Rollback: revert `app.config.ts` `errorScreen.component` to `HttpErrorComponent` and
  remove the wildcard route; the new components are additive and inert if unreferenced.

## Out of scope

- Internal-surface skeleton/empty compositions (land with internal page reworks).
- Public Pages rework (separate plan; reuses the `pp` shell + StateMessage from Task 1).
- Any backend change.
