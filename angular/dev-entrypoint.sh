#!/bin/sh
set -e
DIST=/app/dist/CaseEvaluation/browser
INDEX="$DIST/index.html"
ENV_SRC=/app/dynamic-env.json

ensure_dynamic_env() {
  while true; do
    if [ -f "$ENV_SRC" ] && [ -d "$DIST" ] && [ ! -f "$DIST/dynamic-env.json" ]; then
      cp "$ENV_SRC" "$DIST/dynamic-env.json" 2>/dev/null || true
    fi
    sleep 3
  done
}

# Background loop keeps dynamic-env.json present in dist even if ng build
# clears it on a rebuild.
ensure_dynamic_env &

# Run the watch + serve pair. concurrently keeps both alive in the foreground
# so docker treats their exit as the container's exit.
#
# 2026-05-06: explicitly pass --configuration ${NG_CONFIG:-docker} instead of
# yarn-watch's hardcoded "development". Reason: the angular.json "development"
# configuration has no fileReplacements, so the build picks up the default
# environment.ts (which uses https URLs for AuthServer + API). Our containers
# only listen on plain http in dev, so the SPA's OIDC client fails to fetch
# /.well-known/openid-configuration on bootstrap. The "docker" configuration
# carries the http-only environment.docker.ts via fileReplacements.
exec npx concurrently -k -n watch,serve -c blue,green \
  "npx ng build --watch --configuration ${NG_CONFIG:-docker}" \
  "until [ -f \"$INDEX\" ]; do sleep 1; done; \
   cp \"$ENV_SRC\" \"$DIST/dynamic-env.json\" 2>/dev/null || true; \
   npx browser-sync start --server \"$DIST\" --files \"$DIST/**/*\" \
     --no-ui --no-open --no-notify --port 80 --host 0.0.0.0 --single"
