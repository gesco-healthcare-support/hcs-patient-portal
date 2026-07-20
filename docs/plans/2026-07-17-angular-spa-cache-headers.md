---
feature: angular-spa-cache-headers
date: 2026-07-17
status: draft
base-branch: development
related-issues: []
---

## Goal

Give the Angular SPA correct HTTP cache headers (via `angular/nginx.conf`) so browsers never serve a stale app shell after a deploy, and make the `dynamic-env.envsh` durably executable so rebuilding the image cannot re-break runtime config -- then rebuild + redeploy the angular image on the server and re-verify live.

## Context

- Live symptom: opening `https://admin.appointment-portal.pfd.tbc.local/` in Adrian's browser lands on `/error` with CORS failures against the BARE `auth.`/`api.` hosts. A fresh Playwright Chrome (same laptop/network) works perfectly -- login page, 0 console errors, all `admin.`-prefixed calls return 200. So the server, nginx routing, CORS, and the deployed bundle are correct; the failure is a stale client-cached entry point.
- Root cause: `index.html` + `dynamic-env.json` ship with NO `Cache-Control` header (verified via curl on the box). Browsers then apply heuristic caching to the SPA entry point and reuse a stale shell -- the documented #1 Angular-after-deploy cache failure. There is NO service worker in this build (verified: no `ngsw-worker.js`, no registration, Playwright `serviceWorkers: []`), so HTTP caching is the whole story.
- Coupled rebuild-safety issue: `angular/nginx.conf` is baked into the image (`Dockerfile` line 33 `COPY nginx.conf ...`) with no bind-mount, so the cache fix REQUIRES an image rebuild. But `prod-dynamic-env.envsh` is baked at git mode `100644` (non-executable) with no `chmod`, and the nginx entrypoint IGNORES non-executable `.envsh` files (`docker-entrypoint.sh` logs `Ignoring $f, not executable`). The running container only works because of an ephemeral in-container `chmod +x` done earlier this session. A rebuild WITHOUT a Dockerfile fix reverts that -> `dynamic-env.json` falls back to baked localhost URLs -> `/error` for everyone. Both fixes must ship together in this deploy.
- Constraints:
  - Deploy target is `development` only. Do NOT touch or deploy the separate `main`-branch work (`fix/internal-cancel-reject` etc.).
  - Server is live; staff should be able to use the site every day including today. Minimize disruption; only the angular image is rebuilt (api/authserver/db/redis/minio left running).
  - Governance: commit + push to origin so `development` (and later `main`, on Adrian's schedule) stay in sync. No hand-edits on the server clone.
  - ASCII only; commit format `<type>(patient-portal): ...`; no attribution.

## Approach

Two-tier SPA caching in `angular/nginx.conf`, the industry-standard pattern (sources: OneUptime, Infinum Angular handbook, Pratheek Hegde / Medium):

- **Never pin the entry points.** `index.html`, `dynamic-env.json`, and the `/getEnvConfig` alias -> `Cache-Control: no-cache` (revalidate every load; ETag makes it a cheap 304). `no-cache` means "revalidate", not "don't store".
- **Pin content-hashed assets forever.** `main-<hash>.js`, `chunk-<hash>.js`, `polyfills-<hash>.js`, `styles-<hash>.css`, `*-<hash>.css` -> `public, max-age=31536000, immutable`. Safe because the 8-char hash is the URL and changes every rebuild.
- **Revalidate everything else.** Unhashed CSS bundles (`abp-bundle.css`, `bootstrap-*.css`, `dark/dim/light.css`, `font-bundle.css`, `layout-bundle.css`, `ng-bundle.css`), `favicon.ico`, images/fonts -> fall through to `location /` `no-cache`. These keep their names across rebuilds, so pinning them would go stale.

nginx correctness details (sources: GetPageSpeed / nginx trac #2059):
- Every `add_header` uses the `always` keyword so the header is emitted on 304/404 too.
- `add_header` does NOT inherit into a `location` that defines its own -- there are no server-level `add_header`s here, and each location is self-contained, so nothing is silently dropped.
- Hoist `root` + `index` to the `server` block so the regex/exact-match locations reliably resolve files (the current `/getEnvConfig` relies on ambiguous default root).
- Regex location must be quoted because it contains `{}`: `location ~ "-[A-Z0-9]{8}\.(?:js|css)$"`.

Rebuild-safety fix in `angular/Dockerfile`:
- `COPY --chmod=0755 prod-dynamic-env.envsh /docker-entrypoint.d/40-dynamic-env.envsh` so the baked envsh is executable regardless of the repo file's git mode (Windows checkouts don't carry +x). Docker 29 + BuildKit (compose default) supports `--chmod`.

Alternatives rejected:
- `no-store` on the entry point: heavier than needed (forbids storing at all, no 304 benefit); `no-cache` + ETag is the recommended choice.
- Blanket `immutable` on `*.css`: WRONG here -- several CSS bundles are unhashed and would go stale after a rebuild.
- `RUN chmod` instead of `COPY --chmod`: extra layer; `--chmod` is cleaner and supported. Keep `RUN chmod 0755` as the fallback only if a non-BuildKit build is ever forced.
- Caching hashed fonts/images too (media/): deferred (YAGNI) -- correctness-first; revalidation cost on a LAN is negligible.

### Target `angular/nginx.conf`

```nginx
server {
    listen       80;
    listen  [::]:80;
    server_name  _;

    root   /usr/share/nginx/html;
    index  index.html index.htm;

    # Content-hashed build assets (main-<hash>.js, chunk-<hash>.js, polyfills-<hash>.js,
    # styles-<hash>.css, *-<hash>.css). The 8-char hash is the cache key and changes every
    # rebuild, so pin forever; immutable also skips needless revalidation.
    location ~ "-[A-Z0-9]{8}\.(?:js|css)$" {
        add_header Cache-Control "public, max-age=31536000, immutable" always;
        try_files $uri =404;
    }

    # Runtime config the SPA reads at boot -- must always reflect the current deploy.
    location = /dynamic-env.json {
        add_header Cache-Control "no-cache" always;
        try_files $uri =404;
    }

    # ABP env-config alias that also serves the runtime config; preserve its CORS headers.
    location = /getEnvConfig {
        default_type 'application/json';
        add_header 'Access-Control-Allow-Origin' '*' always;
        add_header 'Access-Control-Allow-Methods' 'GET, POST, OPTIONS' always;
        add_header 'Content-Type' 'application/json' always;
        add_header Cache-Control 'no-cache' always;
        try_files $uri /dynamic-env.json;
    }

    # SPA shell + client-side routes + unhashed CSS bundles + favicon. Revalidate every load
    # (cheap 304 via ETag) so a new deploy is picked up immediately; fall back to index.html.
    location / {
        add_header Cache-Control "no-cache" always;
        try_files $uri $uri/ /index.html =404;
    }

    error_page   500 502 503 504  /50x.html;
    location = /50x.html {
    }
}
```

## Tasks

- T1: Rewrite `angular/nginx.conf` to the two-tier caching config above.
  - approach: code
  - files-touched: [angular/nginx.conf]
  - acceptance: config matches the target; `nginx -t` passes inside a built image.
- T2: Make the baked envsh durably executable.
  - approach: code
  - files-touched: [angular/Dockerfile]
  - acceptance: `COPY --chmod=0755 prod-dynamic-env.envsh /docker-entrypoint.d/40-dynamic-env.envsh`; a fresh image build has the file as `-rwxr-xr-x` and the entrypoint logs `Sourcing ...40-dynamic-env.envsh` (not `Ignoring`).
- T3: Commit both files, push `fix/spa-cache-headers`, open PR into `development`.
  - approach: code
  - files-touched: [git]
  - acceptance: PR open against `development`; CI green; commit follows `<type>(patient-portal): ...`, no attribution.
  - STOP: confirm with Adrian before merge + any server change (live app touch).
- T4: Merge to `development` (squash), fast-forward the server clone, rebuild + restart ONLY the angular image.
  - approach: code
  - files-touched: [server: ~/hcs-patient-portal]
  - acceptance: `git -C ~/hcs-patient-portal pull` reaches the merged commit; `docker compose ... build angular` succeeds; `docker compose ... up -d angular` -> angular healthy; api/authserver/db untouched.
- T5: Live verification (see below).
  - approach: code
  - files-touched: []
  - acceptance: all verification checks pass.

## Risk / Rollback

- Blast radius: angular image only (SPA static serving). No change to api / authserver / SQL Server / Redis / MinIO / reverse-proxy or any data volume. A bad `nginx.conf` = angular container fails to start -> SPA unreachable, but auth/api/db keep running.
- Mitigations: validate `nginx -t` on the freshly built image BEFORE `up -d`; if `--chmod` were unsupported the build fails fast (no bad deploy).
- Rollback: the previous angular image is retained (do NOT prune) -> re-`up -d` the prior image tag to revert in seconds; or `git revert` the commit on `development`, pull, rebuild. Never `docker compose down -v` (PHI volumes).

## Verification

Run after T4 (all from this laptop / the box; NOT via Playwright for header/trust checks -- Playwright ignores cert errors and masks nothing here but does not expose headers cleanly):

1. Entrypoint sourced the envsh (rebuild-safety): `docker compose ... logs angular | grep -E "Sourcing|Ignoring .*envsh"` shows `Sourcing /docker-entrypoint.d/40-dynamic-env.envsh`.
2. Runtime config intact after rebuild: `curl -ks https://admin.appointment-portal.pfd.tbc.local/dynamic-env.json` shows the prod baseHost + `auth.`/`api.` URLs (NOT localhost).
3. Cache headers (curl `-D -` on the box):
   - `/` and `/dynamic-env.json` -> `Cache-Control: no-cache`.
   - `/main-<hash>.js` and a `/chunk-<hash>.js` -> `Cache-Control: public, max-age=31536000, immutable`.
   - an unhashed `/abp-bundle.css` -> `Cache-Control: no-cache` (not immutable).
4. End-to-end in a FRESH Playwright Chrome: navigate to `https://admin.appointment-portal.pfd.tbc.local/` -> reaches the Sign in page, 0 console errors, every backend call `admin.`-prefixed and 200 (matches the known-good trace).
5. Confirm with Adrian that a clean load in his own browser now shows the login page (his earlier stale shell needs one clean load to clear).
