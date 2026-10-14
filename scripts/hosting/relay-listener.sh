#!/usr/bin/env bash
# Azure Relay listener: the Case Tracker reads documents WITHOUT MinIO being open to the internet (2026-10-13).
# AZURE VM ONLY. Design and the Case Tracker side: docs/integration/case-tracker-relay.md.
#
# WHAT IT DOES. Runs Azure Relay Bridge (azbridge) as a container that SHARES THE REVERSE PROXY'S NETWORK NAMESPACE
# and forwards every connection arriving on the hybrid connection to 127.0.0.1:443 there -- i.e. to nginx, which
# terminates TLS for minio.<base> exactly as for any other caller. Consequences, all deliberate:
#   - nginx sees these connections from 127.0.0.1, which the MinIO vhost always allows, so MINIO_ALLOW_DIRECTIVES
#     stays "deny all;" and the S3 endpoint is closed to the internet;
#   - TLS runs end to end between the Case Tracker and nginx; the relay only ever carries ciphertext;
#   - the per-office read-only MinIO users (minio-case-tracker-access.sh) still decide what can be read.
#
# WHY A SUPERVISOR LOOP. A container joined to another container's network namespace keeps the OLD namespace when
# that container is recreated -- and every deploy force-recreates the reverse proxy. So this script watches the
# proxy's container id and re-attaches the listener whenever it changes, or when the listener exits.
#
# AUTHENTICATION. By default azbridge uses Azure's default credential chain, i.e. the VM's managed identity, which the
# fast-path template grants "Azure Relay Listener" on this one hybrid connection: no key on the VM. Fallback only:
# RELAY_LISTEN_CONNECTION_STRING_FILE names a mode-600 file holding AZURE_BRIDGE_CONNECTIONSTRING=<listen-only SAS
# connection string>, passed with --env-file so it never appears on a command line.
#
# PINNING. azbridge ships as an OCI image tarball, not in a registry. Its sha256 is checked before `docker load`
# (runbook), and RELAY_AZBRIDGE_IMAGE_ID records the loaded image's id: this script refuses to start any other image.
#
# FAILURE. Five listener exits in a row, each within a minute of starting, end the script with an error: systemd
# then restarts it a few times and, past its start limit, marks the unit failed, which emails through
# hcs-portal-backup-alert@. Independently, an Azure Monitor alert fires when the hybrid connection has had no
# listener for 15 minutes (fast-path template, relayAlertEmail) -- that one also covers the VM itself being down.
#
# Settings (secrets/env.prod): RELAY_NAMESPACE, RELAY_HYBRID_CONNECTION (default case-tracker-documents),
#   RELAY_AZBRIDGE_IMAGE, RELAY_AZBRIDGE_IMAGE_ID, RELAY_LISTEN_CONNECTION_STRING_FILE (optional).
# Env (all optional): ENV_FILE, PROXY_CONTAINER, RELAY_CONTAINER, CHECK_EVERY (seconds, default 15), and
#   MAX_CYCLES -- TESTS ONLY: stop after that many listener starts (0, the default, runs forever).
set -euo pipefail

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
PROXY_CONTAINER="${PROXY_CONTAINER:-hcs-patient-portal-reverse-proxy-1}"
RELAY_CONTAINER="${RELAY_CONTAINER:-hcs-portal-relay}"
CHECK_EVERY="${CHECK_EVERY:-15}"
MAX_CYCLES="${MAX_CYCLES:-0}"
QUICK_EXIT_SECONDS=60
QUICK_EXITS_ALLOWED=5

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }
env_value() {
  local key="$1"
  { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2- | tr -d '\r' \
    | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/"
}

[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
namespace="$(env_value RELAY_NAMESPACE)"
hybrid="$(env_value RELAY_HYBRID_CONNECTION)"
hybrid="${hybrid:-case-tracker-documents}"
image="$(env_value RELAY_AZBRIDGE_IMAGE)"
image_id="$(env_value RELAY_AZBRIDGE_IMAGE_ID)"
conn_file="$(env_value RELAY_LISTEN_CONNECTION_STRING_FILE)"

[[ "$namespace" =~ ^[A-Za-z][A-Za-z0-9-]{4,48}[A-Za-z0-9]$ ]] || fail "RELAY_NAMESPACE is missing or not a namespace name: [$namespace]"
[[ "$hybrid" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ && ${#hybrid} -le 260 ]] \
  || fail "RELAY_HYBRID_CONNECTION is not a hybrid connection name: [$hybrid]"
[[ -n "$image" ]] || fail "RELAY_AZBRIDGE_IMAGE is not set: the tag docker load printed"
[[ "$image_id" =~ ^sha256:[0-9a-f]{64}$ ]] || fail "RELAY_AZBRIDGE_IMAGE_ID is missing or malformed: the id of the image loaded from the checked tarball"
actual_id="$(docker image inspect -f '{{.Id}}' "$image" 2> /dev/null || true)"
[[ "$actual_id" == "$image_id" ]] || fail "image ${image} is ${actual_id:-absent}, not the pinned ${image_id}; refusing to run it"
if [[ -n "$conn_file" ]]; then
  [[ -f "$conn_file" ]] || fail "RELAY_LISTEN_CONNECTION_STRING_FILE names a file that does not exist: $conn_file"
fi

stop_listener() { docker rm -f "$RELAY_CONTAINER" > /dev/null 2>&1 || true; }
trap 'stop_listener; exit 0' TERM INT

proxy_id() { docker inspect -f '{{if .State.Running}}{{.Id}}{{end}}' "$PROXY_CONTAINER" 2> /dev/null || true; }
listener_running() { [[ "$(docker inspect -f '{{.State.Running}}' "$RELAY_CONTAINER" 2> /dev/null || echo false)" == true ]]; }

endpoint="sb://${namespace}.servicebus.windows.net/"
auth="$([[ -n "$conn_file" ]] && echo "listen-only SAS rule (fallback)" || echo "the VM's managed identity")"
log "relay listener for ${hybrid} on ${endpoint}, signing in as ${auth}"

cycles=0
quick_exits=0
while :; do
  current="$(proxy_id)"
  while [[ -z "$current" ]]; do
    log "waiting for ${PROXY_CONTAINER} to be running"
    sleep "$CHECK_EVERY"
    current="$(proxy_id)"
  done

  stop_listener
  args=(run -d --name "$RELAY_CONTAINER" --network "container:${current}" --restart no)
  [[ -n "$conn_file" ]] && args+=(--env-file "$conn_file")
  args+=("$image" -e "$endpoint" -T "${hybrid}:127.0.0.1:443")
  docker "${args[@]}" > /dev/null || fail "could not start the listener container"
  started="$(date +%s)"
  cycles=$((cycles + 1))
  log "listener started, attached to ${PROXY_CONTAINER} (${current:0:12}); forwarding ${hybrid} -> 127.0.0.1:443"

  while :; do
    sleep "$CHECK_EVERY"
    if [[ "$(proxy_id)" != "$current" ]]; then
      log "the reverse proxy was recreated or stopped; re-attaching the listener"
      quick_exits=0
      break
    fi
    if ! listener_running; then
      log "the listener exited; last lines: $(docker logs --tail 5 "$RELAY_CONTAINER" 2>&1 | tr '\n' ' ' | cut -c1-400)"
      if (( $(date +%s) - started < QUICK_EXIT_SECONDS )); then
        quick_exits=$((quick_exits + 1))
      else
        quick_exits=0
      fi
      (( quick_exits < QUICK_EXITS_ALLOWED )) \
        || { stop_listener; fail "the listener exited ${quick_exits} times in a row within ${QUICK_EXIT_SECONDS}s of starting"; }
      break
    fi
  done

  if (( MAX_CYCLES > 0 && cycles >= MAX_CYCLES )); then
    stop_listener
    log "stopping after ${cycles} listener start(s) (MAX_CYCLES)"
    exit 0
  fi
done
