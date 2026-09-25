#!/usr/bin/env bash
# run.sh
#
# Prints the docker-compose launch command for the current worktree and the
# three host URLs read from the worktree's .env overrides (or compose defaults
# if no overrides are set). The stack is started via `docker compose up -d`;
# we do NOT launch it from this script because it's more useful to observe
# `docker compose up` interactively the first time (build logs are long).
set -euo pipefail

WT="$(pwd)"
BASENAME="$(basename "$WT")"

# Pull per-worktree overrides if set; otherwise fall back to main's defaults.
AUTH="$(. "$WT/.env" 2>/dev/null; echo "${AUTH_PORT:-44368}")"
API="$(. "$WT/.env" 2>/dev/null; echo "${API_PORT:-44327}")"
NG="$(. "$WT/.env" 2>/dev/null; echo "${NG_PORT:-4200}")"

cat <<INFO
Worktree: $BASENAME  (AUTH=$AUTH  API=$API  NG=$NG)

To bring the full stack up:
  docker compose up -d

First-time build can take several minutes. Tail logs in another terminal:
  docker compose logs -f authserver api angular

Smoke-test:
  curl -sf http://localhost:$AUTH/.well-known/openid-configuration
  curl -sf http://localhost:$API/swagger/index.html
  curl -sf http://localhost:$NG/

See docs/runbooks/DOCKER-DEV.md for full operations reference.
INFO
