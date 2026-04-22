#!/usr/bin/env bash
# run.sh
#
# Documentation-style helper that prints the three-tab start order for the
# current worktree. We intentionally do NOT background services here: Kestrel
# and OpenIddict emit load-bearing boot-time messages (port binding, cert
# trust, openid metadata warmup) that need to be visible to the developer.
# Three dedicated terminal tabs is the workflow we settled on.
#
# Per CLAUDE.md: never use `ng serve` -- use `ng build --configuration local`
# plus `npx serve -s dist/CaseEvaluation/browser -p <NG_PORT>`.
set -euo pipefail

WT="$(pwd)"
BASENAME="$(basename "$WT")"
AUTH_FILE="$WT/src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.Local.json"

if [ -f "$AUTH_FILE" ] && command -v jq >/dev/null; then
  API_FILE="$WT/src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.Local.json"
  AUTH="$(jq -r '.Kestrel.Endpoints.Https.Url' "$AUTH_FILE" | sed -E 's|https://localhost:||')"
  API="$(jq -r '.Kestrel.Endpoints.Https.Url' "$API_FILE" | sed -E 's|https://localhost:||')"
  NG="$(jq -r '.App.AngularUrl' "$AUTH_FILE" | sed -E 's|http://localhost:||')"
else
  AUTH=44368; API=44327; NG=4200
fi

cat <<INFO
Starting order for worktree: $BASENAME  (AUTH=$AUTH  API=$API  NG=$NG)

Tab 1 (AuthServer):
  cd "$WT"
  DOTNET_ENVIRONMENT=Development dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer
  # wait for: "Now listening on: https://localhost:$AUTH"

Tab 2 (HttpApi.Host):
  cd "$WT"
  DOTNET_ENVIRONMENT=Development dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
  # wait for: "Now listening on: https://localhost:$API"

Tab 3 (Angular):
  cd "$WT/angular"
  npx ng build --configuration local --watch
  # in a second sub-tab:
  npx serve -s dist/CaseEvaluation/browser -p $NG

Smoke test:
  curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:$AUTH/.well-known/openid-configuration   # 200
  curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:$API/swagger/index.html                  # 200
  curl -s  -o /dev/null -w "%{http_code}\n" http://localhost:$NG/                                      # 200
INFO
