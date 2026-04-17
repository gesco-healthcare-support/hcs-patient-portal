[Home](../../INDEX.md) > [Issues](../) > Research > ARC-05

# ARC-05: AppointmentAddComponent Eagerly Loaded -- Research

**Severity**: Low
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/app.routes.ts` lines 14, 99-102

---

## Current state (verified 2026-04-17)

```typescript
// Line 14 -- static top-level import (hoisted into root bundle)
import { AppointmentAddComponent } from './appointments/appointment-add.component';
// ... other imports ...

// Line 99-102 -- route wraps in Promise.resolve but damage is done at import time
{
  path: 'appointments/add',
  loadComponent: () => Promise.resolve(AppointmentAddComponent),
  canActivate: [authGuard],
}
```

Wrapping in `loadComponent: () => Promise.resolve(...)` is a no-op from a chunking standpoint -- the chunk splitter only creates a separate chunk for dynamic `import()` expressions. ~1,400-line component pays first-paint cost for every user including anonymous login page.

All other routes use the correct lazy pattern:
```typescript
loadComponent: () => import('./foo.component').then(m => m.FooComponent)
```

---

## Official documentation

- [Angular Routing tutorial (lazy loading with `loadComponent`)](https://angular.dev/guide/routing) -- canonical pattern: `loadComponent: () => import('./path').then(m => m.ComponentName)`. Dynamic `import()` is the chunk split point.
- [Angular standalone component imports](https://angular.dev/guide/components/importing) -- standalone components referenced directly from `loadComponent` without NgModule wrapper.
- [TC39 dynamic import proposal (webpack mirror)](https://webpack.js.org/api/module-methods/#import-1) -- static `import` statements are part of the importing chunk; dynamic `import()` calls are split points.
- [esbuild bundle analyzer](https://esbuild.github.io/analyze/) -- Angular 20 uses esbuild; analyser consumes `metafile.json`.
- [`source-map-explorer` on npm](https://www.npmjs.com/package/source-map-explorer) -- works with Angular 20 given `sourceMap: true` + `namedChunks: true`.

## Community findings

- [Angular Experts -- Hawkeye esbuild analyzer](https://angularexperts.io/blog/hawkeye-esbuild-analyzer/) -- post-17 workflow for analysing esbuild output with `--stats-json`.
- [Medium -- Angular 20 cut bundle size in half](https://medium.com/@dana.c/angular-20-how-i-cut-the-size-of-our-bundle-in-half-d490f8496327) -- `loadComponent` conversions + bundle-size wins.
- [amadousall.com -- Explore Angular bundle with esbuild Bundle Size Analyzer](https://www.amadousall.com/explore-the-content-of-your-angular-bundle-with-esbuild-bundle-size-analyzer/) -- Angular's `stats.json` is NOT the same as esbuild's metafile.
- [Zoaib Khan -- Code splitting in Angular](https://zoaibkhan.com/blog/how-to-add-code-splitting-to-your-angular-app/) -- contrasts static vs dynamic imports and chunk-graph effect.

## Recommended approach

1. Remove the top-level `import` of `AppointmentAddComponent` from `app.routes.ts`.
2. Change the route to `loadComponent: () => import('./appointments/appointment-add.component').then(m => m.AppointmentAddComponent)`. Same shape as other lazy routes.
3. Verify chunk split via `ng build --stats-json` + `source-map-explorer` on emitted JS or esbuild analyser on metafile. Expect a new chunk named after the component + commensurate reduction in `main.js`.
4. INFERENCE (MEDIUM): audit for similar copy-paste patterns -- grep `Promise.resolve(` in any `*.routes.ts` files.

## Gotchas / blockers

- Anything else statically importing `AppointmentAddComponent` (a provider registration, another route, a test harness) will re-hoist it. Grep all static imports after the fix.
- If the component imports heavy shared services (whole `@abp/ng.theme.shared`, large vendor libs) through barrel files, those still end up in main via DI tokens. Bundle-analyse before celebrating.
- `ng serve` is forbidden (Vite pre-bundling breaks ABP DI per project CLAUDE.md); run analyser against `ng build --configuration development|production` output.

## Open questions

- Any other routes similarly wrapping static imports in `Promise.resolve`? Grep `Promise.resolve(` in `*.routes.ts`.
- Current main-bundle size and target? Without a baseline, the fix has no measurable success criterion.

## Related

- [docs/issues/ARCHITECTURE.md#arc-05](../ARCHITECTURE.md#arc-05-appointmentaddcomponent-is-eagerly-loaded)
