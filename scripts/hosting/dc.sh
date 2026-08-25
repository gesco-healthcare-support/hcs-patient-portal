#!/usr/bin/env bash
# Compose wrapper for the deployed stack (2026-08-25). Run docker compose against this deployment
# WITHOUT having to remember the two flags it does not work without.
#
# WHY THIS EXISTS. There is no `.env` in the repo root, so compose auto-loads nothing. A bare
#   docker compose -f docker-compose.prod.yml up -d
# resolves EVERY secret to a blank string -- MSSQL_SA_PASSWORD, MINIO_ROOT_USER,
# MINIO_ROOT_PASSWORD, TLS_CERT_PATH, TLS_KEY_PATH, BASE_DOMAIN, ABP_NUGET_API_KEY,
# ABP_LICENSE_CODE -- and cheerfully RECREATES the containers with no database password, no TLS
# paths and no base domain. Tenant resolution dies and SQL is unreachable. Compose prints the
# warnings and carries on, so nothing stops you.
#
# The tell that you got it wrong by hand: `ps` prints "variable is not set" warnings and then lists
# NOTHING, because the blank project context matches no running container.
#
# So: use this instead of typing docker compose directly. It is a pass-through -- every compose
# subcommand and flag works unchanged, it just injects `-f` and `--env-file` and refuses to run if
# the environment file is missing.
#
# USAGE (from anywhere; it locates the deploy root itself):
#   ./scripts/hosting/dc.sh ps
#   ./scripts/hosting/dc.sh logs --since 20m api
#   ./scripts/hosting/dc.sh build db-migrator api authserver angular
#   ./scripts/hosting/dc.sh up -d
#   ./scripts/hosting/dc.sh up -d --force-recreate reverse-proxy
#   ./scripts/hosting/dc.sh exec -T sql-server bash -c '...'
#
# Env (all optional): COMPOSE_FILE, ENV_FILE -- same names the sibling hosting scripts use.
set -euo pipefail

# Resolve the deploy root from the script's own location, so this works from any cwd (cron, an ssh
# one-liner, or a shell parked somewhere else). Two levels up from scripts/hosting/.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd -- "${SCRIPT_DIR}/../.." && pwd)"
cd -- "$ROOT_DIR"

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-secrets/env.prod}"

if [ $# -eq 0 ]; then
  echo "usage: $(basename "$0") <docker compose args...>" >&2
  echo "example: $(basename "$0") up -d" >&2
  exit 64
fi

[ -f "$COMPOSE_FILE" ] || {
  echo "ERROR: compose file '${COMPOSE_FILE}' not found in ${ROOT_DIR}." >&2
  exit 1
}

# Fail fast rather than let compose blank every secret. This is the whole point of the script.
[ -f "$ENV_FILE" ] || {
  echo "ERROR: env file '${ENV_FILE}' not found in ${ROOT_DIR}." >&2
  echo "Refusing to run: without it compose resolves every secret to an empty string and would" >&2
  echo "recreate the stack with no DB password, no TLS paths and no BASE_DOMAIN." >&2
  echo "On the server it is created out-of-band from env.prod.example at the REPO ROOT:" >&2
  echo "  cp env.prod.example ${ENV_FILE} && chmod 600 ${ENV_FILE}   # then fill in the values" >&2
  exit 1
}
[ -r "$ENV_FILE" ] || {
  echo "ERROR: env file '${ENV_FILE}' exists but is not readable by $(id -un)." >&2
  echo "It is mode 600 by design; run as the owner (apadmin on the server), not via sudo -u." >&2
  exit 1
}

# A secrets file readable beyond its owner is worth saying out loud, but not worth blocking a
# deploy over -- warn and continue.
# stat -c is GNU; on a platform without it PERMS is empty and the check simply says nothing.
PERMS="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || true)"
case "$PERMS" in
  ''|600|400) ;;
  *) echo "WARNING: ${ENV_FILE} is mode ${PERMS}; it holds live secrets. Consider chmod 600." >&2 ;;
esac

# exec so signals (Ctrl-C on a long build, docker compose logs -f) reach compose directly rather
# than being swallowed by this wrapper.
exec docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
