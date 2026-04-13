# ADR-005: Static Serve Workaround for Angular 20 Vite Bug

**Status:** Accepted
**Date:** 2026-04-10
**Verified by:** code-inspect

## Context

Angular 20 uses the `@angular/build:application` builder (backed by esbuild) for
production builds and Vite for the dev server (`@angular/build:dev-server`). When
running `ng serve`, Vite's pre-bundler processes `node_modules` dependencies and may
split large packages across multiple chunks.

ABP Framework's `@abp/ng.core` package registers Angular `InjectionToken` instances
(including `CORE_OPTIONS`) as module-level singletons. When Vite splits `@abp/ng.core`
across chunks, two separate copies of `CORE_OPTIONS` are created. Angular's dependency
injection uses `===` (reference identity) to match tokens. Since the two copies are
different object references, the DI container cannot find the provider, resulting in:

```
NullInjectorError: No provider for CORE_OPTIONS
```

This error occurs only with `ng serve` (Vite dev server). The `ng build` command uses
esbuild directly and does not exhibit this chunk-splitting behavior.

The `angular.json` in this project confirms the builder configuration:
- Build: `@angular/build:application` (esbuild)
- Serve: `@angular/build:dev-server` (Vite)

## Decision

Never use `ng serve`, `yarn start`, or `ng build --watch` for local development.
Instead, use:

```bash
npx ng build --configuration development
npx serve -s dist/CaseEvaluation/browser -p 4200
```

This produces a full development build (with source maps, no optimization, named chunks)
and serves it with a static file server on port 4200.

## Consequences

**Easier:**
- Eliminates the `NullInjectorError: CORE_OPTIONS` crash entirely
- Build output matches what will be deployed (no dev-server-only behaviors)
- Works reliably with all ABP Angular packages

**Harder:**
- No hot module replacement (HMR) -- every change requires a full rebuild
- Rebuild cycle is slower than Vite's near-instant HMR (typically 15-30 seconds for
  a development build vs. sub-second Vite updates)
- New developers may instinctively run `ng serve` and hit the error; the constraint
  must be documented prominently (it is in CLAUDE.md and the Getting Started guide)
- `--watch` mode is not used because it also triggers the Vite pre-bundler

## Alternatives Considered

1. **Configure Vite to not pre-bundle @abp/ng.core** -- Investigated but Angular CLI
   does not expose Vite's `optimizeDeps.exclude` configuration in `angular.json`. There
   is no supported way to customize Vite pre-bundling through the Angular builder.

2. **Downgrade to Angular 19 (webpack builder)** -- Rejected because the project targets
   Angular 20 for long-term support, and reverting would delay adoption of standalone
   components, signals, and other Angular 20 features.

3. **Wait for ABP fix** -- ABP has acknowledged the issue but has not shipped a fix as
   of ABP 10.0.2. This workaround is needed until ABP restructures `@abp/ng.core` to
   be Vite-compatible. This ADR should be revisited when ABP 10.1+ is released.

4. **Use webpack via `@angular-devkit/build-angular:dev-server`** -- Rejected because
   Angular 20 has deprecated the webpack-based builder. It still works but receives no
   new features and will be removed in a future Angular version.
