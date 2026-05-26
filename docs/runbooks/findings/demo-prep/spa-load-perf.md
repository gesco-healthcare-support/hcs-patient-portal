---
title: SPA bundle size + load performance
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# SPA bundle + load performance

## Numbers

| Metric | Value |
|---|---|
| Initial-paint payload (uncompressed) | ~2.47 MB |
| Over the wire with npx serve gzip | ~600-800 KB |
| JS files on initial paint | 12 |
| CSS sheets on initial paint | 6 |
| Largest single file | main.js (1.72 MB) |
| Total dist/ folder | 35 MB (dev build, uncompressed) |
| Warm-cache navigation duration | 1.16s (measured) |

## main.js dominant cost

`main.js` (1.72 MB uncompressed) bundles:
- Angular 20 runtime
- RxJS 7.8
- ABP `@abp/ng.core, oauth, components, theme.shared,
  feature-management, setting-management`
- LeptonX theme
- ngx-mask
- @rxweb/reactive-form-validators
- angular-oauth2-oidc

esbuild builder does not split named vendor chunks by default. With
gzip this is ~400-500 KB over the wire.

## Lazy-loading is aggressive

Every route in `app.routes.ts` uses `loadComponent` / `loadChildren`.
All 18 feature modules + every ABP @volo admin module (gdpr,
identity, saas, audit-logs, openiddictpro, text-template-management,
file-management, language-management, setting-management) lazy.

Only minor static import: `AppointmentAddComponent` is statically
imported into `app.routes.ts:15` so it ships in main.js regardless
of route visit.

## Pocket answer if viewer says "that took a while"

> "Yeah -- first load is heavier than steady-state because the SPA
> downloads the full app shell once and then caches it. The
> framework is Angular 20 plus the ABP Commercial admin packages --
> about 600 KB over the wire compressed. Every subsequent page is
> lazy-loaded on demand, so once you're in, navigation is instant.
> Production hosting will add CDN + HTTP/2 multiplexing, which
> typically cuts cold start by another 30-40%."

## Post-demo improvement opportunities

1. Split main.js vendor bundle (manual chunking via esbuild
   external-dependencies). Drop main.js to ~600-800 KB.
2. Drop FontAwesome v4-shims (24 KB) and trim FA to icon subset
   used by app.
3. Verify gzip/brotli at production serving layer (IIS
   httpCompression on `.js`/`.css`); without it, wire size triples.

## Pre-demo recommendation

Open the SPA + login once during pre-demo setup so all chunks are
in browser cache. Subsequent navigation = sub-1.2s.
