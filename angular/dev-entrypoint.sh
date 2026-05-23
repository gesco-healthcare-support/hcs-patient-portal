#!/bin/sh
set -e
DIST=/app/dist/CaseEvaluation/browser
INDEX="$DIST/index.html"
ENV_SRC=/app/dynamic-env.json

# BUG-015 (Task B, 2026-05-20): write runtime config to $ENV_SRC from
# container env vars. Replaces the previous bind-mount of
# docker/dynamic-env.json (tracked-in-repo with hardcoded canonical URLs).
# OBS-22 (2026-05-22): with `ng build --watch` removed, the build step below
# runs once. The ensure_dynamic_env background loop is kept as a defensive
# measure so dist always has a current dynamic-env.json if anything ever
# clears dist between build and serve.
NG_PORT_VALUE="${NG_PORT:-4200}"
AUTH_PORT_VALUE="${AUTH_PORT:-44368}"
API_PORT_VALUE="${API_PORT:-44327}"
cat > "$ENV_SRC" <<EOF
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

ensure_dynamic_env() {
  while true; do
    if [ -f "$ENV_SRC" ] && [ -d "$DIST" ] && [ ! -f "$DIST/dynamic-env.json" ]; then
      cp "$ENV_SRC" "$DIST/dynamic-env.json" 2>/dev/null || true
    fi
    sleep 3
  done
}

# Background loop keeps dynamic-env.json present in dist as a defensive
# measure if anything ever clears it.
ensure_dynamic_env &

# OBS-22 (2026-05-22): single-shot build then serve. Previously this ran
# `ng build --watch` + browser-sync via `concurrently`; the watcher drifted
# silent over Docker Desktop bind mounts on Windows often enough that the
# iteration pattern had collapsed to "edit -> docker compose restart angular."
# Removing the watcher matches the actual workflow with no UX loss.
#
# 2026-05-06: explicitly pass --configuration ${NG_CONFIG:-docker} instead of
# yarn-watch's hardcoded "development". Reason: the angular.json "development"
# configuration has no fileReplacements, so the build picks up the default
# environment.ts (which uses https URLs for AuthServer + API). Our containers
# only listen on plain http in dev, so the SPA's OIDC client fails to fetch
# /.well-known/openid-configuration on bootstrap. The "docker" configuration
# carries the http-only environment.docker.ts via fileReplacements.
npx ng build --configuration "${NG_CONFIG:-docker}"
cp "$ENV_SRC" "$DIST/dynamic-env.json" 2>/dev/null || true
exec npx browser-sync start --server "$DIST" --files "$DIST/**/*" \
  --no-ui --no-open --no-notify --port 80 --host 0.0.0.0 --single
