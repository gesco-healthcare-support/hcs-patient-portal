#!/bin/sh
# Dev entrypoint for the angular `dev` target (one-shot build + static serve).
#
# Source is bind-mounted at /app and node_modules is a named volume, so this
# runs on every `docker compose up`/`restart angular`:
#   1. yarn install -- populates the node_modules volume; a fast no-op once the
#      install state matches the lockfile.
#   2. ng build (NG_CONFIG=docker -> http URLs via environment.docker.ts).
#   3. write dynamic-env.json into the build output -- the SPA fetches it
#      relative at bootstrap (see src/main.ts); regenerating it here lets the
#      same source serve any stack's ports.
#   4. serve the static build on :80 with SPA (index.html) fallback.
#
# No file watcher: Docker Desktop's bind-mount/inotify bridge on Windows drops
# events (OBS-22), so the iteration loop is `docker compose restart angular`.
set -e

DIST=/app/dist/CaseEvaluation/browser
NG_PORT_VALUE="${NG_PORT:-4200}"
AUTH_PORT_VALUE="${AUTH_PORT:-44368}"
API_PORT_VALUE="${API_PORT:-44327}"

echo "[dev] yarn install (node-modules volume)..."
yarn install --immutable

echo "[dev] ng build --configuration ${NG_CONFIG:-docker}..."
yarn ng build --configuration "${NG_CONFIG:-docker}"

echo "[dev] writing dynamic-env.json (NG=${NG_PORT_VALUE} AUTH=${AUTH_PORT_VALUE} API=${API_PORT_VALUE})"
cat > "$DIST/dynamic-env.json" <<EOF
{
  "production": false,
  "application": {
    "baseUrl": "http://localhost:${NG_PORT_VALUE}",
    "name": "CaseEvaluation",
    "logoUrl": ""
  },
  "oAuthConfig": {
    "issuer": "http://localhost:${AUTH_PORT_VALUE}/",
    "redirectUri": "http://localhost:${NG_PORT_VALUE}",
    "clientId": "CaseEvaluation_App",
    "responseType": "code",
    "scope": "offline_access openid profile email phone CaseEvaluation",
    "requireHttps": false
  },
  "apis": {
    "default": {
      "url": "http://localhost:${API_PORT_VALUE}",
      "rootNamespace": "HealthcareSupport.CaseEvaluation"
    },
    "AbpAccountPublic": {
      "url": "http://localhost:${AUTH_PORT_VALUE}",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
EOF

echo "[dev] serving $DIST on :80"
exec serve -s "$DIST" -l 80
