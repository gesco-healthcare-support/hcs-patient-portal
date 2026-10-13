#!/usr/bin/env bash
# The portal's public TLS certificate, issued and renewed by Let's Encrypt on the Azure VM (2026-10-13).
#
# WHY. The office server uses a wildcard certificate from the internal CA, which no browser outside the office
# trusts. The Azure VM is public, so it needs a publicly trusted certificate, and one that renews itself: a Let's
# Encrypt certificate lasts 90 days, and a lapsed one takes the whole site down for everyone at once.
#
# HOW. lego (pinned by digest, run as a container, nothing installed on the host) proves control of the domain
# with a DNS-01 challenge: it writes a TXT record into the Azure DNS zone and removes it afterwards. It signs in to
# Azure as the VM's own managed identity, which the fast-path template grants DNS Zone Contributor on that one
# zone. No secret is stored for it. One certificate covers the four names nginx serves:
#   <base>   *.<base>   *.api.<base>   *.auth.<base>
# DNS-01 is the only challenge Let's Encrypt accepts for wildcard names.
#
# WHAT IT DOES, in order. Each step stops the run on failure, and a failed run alerts by email through
# hcs-portal-backup-alert@ (OnFailure= on hcs-portal-tls-renew.service).
#   1. Pre-flight: the env file, BASE_DOMAIN, where nginx reads the certificate (TLS_CERT_PATH / TLS_KEY_PATH).
#   2. Finds the VM's subscription and resource group from the Azure instance metadata service (overridable).
#   3. Runs lego. It issues on the first run and renews afterwards, only once the certificate is in the last third
#      of its life (or when Let's Encrypt's renewal info says so). On other days it changes nothing.
#   4. Checks what lego produced: unexpired, all four names present, private key matching the certificate.
#   5. If it differs from the installed one: keeps the old pair as .prev, writes the new one IN PLACE, checks the
#      nginx configuration and reloads it. In place, because compose bind-mounts the two FILES: a renamed-over file
#      is a new inode the running container never sees, so it would keep serving the old certificate.
#   6. Asks the running nginx which certificate it SERVES and requires it to be the new one. A reload that did not
#      take is caught here, not by a browser 30 days later.
#   7. Requires the served certificate to have at least MIN_DAYS_LEFT days left (default 14). Renewal starts about
#      30 days before expiry, so this only fails when renewal has been failing for two weeks or more.
#
# USAGE (as root, from the repository root; the systemd timer runs it daily):
#   scripts/hosting/renew-tls-cert.sh             issue or renew, install, reload, verify
#   scripts/hosting/renew-tls-cert.sh --staging   issue against Let's Encrypt's STAGING CA, to prove DNS, identity and
#                                                 permissions without touching rate limits. Staging certificates are
#                                                 NOT trusted, so this mode never installs anything.
#
# State lives in secrets/acme/ (the ACME account key and the certificate's private key), which is why it sits under
# secrets/: copy secrets/ off the VM with everything else. Losing it costs nothing worse than a fresh issuance.
#
# Env (all optional): ENV_FILE, ACME_DIR, LEGO_IMAGE, ACME_EMAIL (also read from the env file),
#   AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP (default: this VM's, from instance metadata), MIN_DAYS_LEFT,
#   COMPOSE_FILE, TLS_PROBE_HOST (default 127.0.0.1), TLS_VERIFY_WAIT (seconds between checks of the served
#   certificate, default 5), and TLS_SERVED_CERT_FILE -- TESTS ONLY: read the "served"
#   certificate from this file instead of connecting to nginx.
set -euo pipefail
export LC_ALL=C

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
ACME_DIR="${ACME_DIR:-secrets/acme}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
MIN_DAYS_LEFT="${MIN_DAYS_LEFT:-14}"
TLS_PROBE_HOST="${TLS_PROBE_HOST:-127.0.0.1}"
TLS_VERIFY_WAIT="${TLS_VERIFY_WAIT:-5}"
# lego v5.5.2 (2026-09-23), pinned by digest so the tool cannot change without a commit.
LEGO_IMAGE="${LEGO_IMAGE:-goacme/lego:v5.5.2@sha256:1944e8c36055beec47c7de6f15202b41128be75eea0ffa257f0c14d93c5155fd}"
LE_PRODUCTION="https://acme-v02.api.letsencrypt.org/directory"
LE_STAGING="https://acme-staging-v02.api.letsencrypt.org/directory"
CERT_NAME="portal"

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

staging=false
for arg in "$@"; do
  case "$arg" in
    --staging) staging=true ;;
    *) fail "unknown argument: $arg (expected --staging or nothing)" ;;
  esac
done
[[ "$MIN_DAYS_LEFT" =~ ^[0-9]+$ ]] || fail "MIN_DAYS_LEFT must be a whole number of days"

# One key from the env file; empty when absent. Quotes around the value are removed.
env_value() {
  local key="$1"
  { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2- | tr -d '\r' \
    | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/"
}

# ---------------------------------------------------------------- 1. pre-flight
log "1/7 pre-flight"
[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
base="$(env_value BASE_DOMAIN)"
[[ -n "$base" ]] || fail "BASE_DOMAIN is not set in $ENV_FILE"
[[ "$base" =~ ^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$ ]] \
  || fail "BASE_DOMAIN is not a plain lowercase DNS name: [$base]"
cert_path="$(env_value TLS_CERT_PATH)"
key_path="$(env_value TLS_KEY_PATH)"
cert_path="${cert_path:-./secrets/wildcard.crt}"
key_path="${key_path:-./secrets/wildcard.key}"
email="${ACME_EMAIL:-$(env_value ACME_EMAIL)}"
command -v openssl > /dev/null || fail "openssl is not installed"
names=("$base" "*.$base" "*.api.$base" "*.auth.$base")
log "     names: ${names[*]}"

# ---------------------------------------------------------------- 2. where the DNS zone is
log "2/7 locating the DNS zone"
subscription="${AZURE_SUBSCRIPTION_ID:-}"
resource_group="${AZURE_RESOURCE_GROUP:-}"
if [[ -z "$subscription" || -z "$resource_group" ]]; then
  # Instance metadata answers only from inside the VM, to a request carrying this header; no credential involved.
  meta="$(curl -sf --max-time 5 -H 'Metadata: true' \
    'http://169.254.169.254/metadata/instance/compute?api-version=2021-02-01' 2>/dev/null)" \
    || fail "cannot reach the Azure instance metadata service: is this the Azure VM? Set AZURE_SUBSCRIPTION_ID and AZURE_RESOURCE_GROUP to run elsewhere."
  # Each key appears once in the compute document; a plain match avoids depending on a JSON tool.
  meta_value() {
    local key="$1"
    printf '%s' "$meta" | grep -o "\"${key}\": *\"[^\"]*\"" | head -1 | sed 's/.*: *"\(.*\)"/\1/'
  }
  subscription="${subscription:-$(meta_value subscriptionId)}"
  resource_group="${resource_group:-$(meta_value resourceGroupName)}"
fi
[[ "$subscription" =~ ^[0-9a-fA-F-]{36}$ ]] || fail "not a subscription id: [$subscription]"
[[ "$resource_group" =~ ^[A-Za-z0-9._()-]{1,90}$ ]] || fail "not a resource group name: [$resource_group]"
log "     zone ${base} in resource group ${resource_group}"

# ---------------------------------------------------------------- 3. lego
if $staging; then server="$LE_STAGING"; data="${ACME_DIR}/staging"; else server="$LE_PRODUCTION"; data="$ACME_DIR"; fi
log "3/7 lego against $($staging && echo STAGING || echo production) Let's Encrypt"
( umask 077; mkdir -p "$data" )
chmod 700 "$ACME_DIR" "$data"
data_abs="$(cd "$data" && pwd)"
lego_args=(run --accept-tos --server "$server" --path /data --cert.name "$CERT_NAME" --key-type rsa2048 --dns azuredns)
[[ -n "$email" ]] && lego_args+=(--email "$email")
for n in "${names[@]}"; do lego_args+=(--domains "$n"); done
# Host networking, so the managed-identity token request reaches the metadata address directly.
docker run --rm --network host -v "${data_abs}:/data" \
  -e AZURE_AUTH_METHOD=msi -e AZURE_AUTH_MSI_TIMEOUT=10s \
  -e AZURE_SUBSCRIPTION_ID="$subscription" -e AZURE_RESOURCE_GROUP="$resource_group" -e AZURE_ZONE_NAME="$base" \
  "$LEGO_IMAGE" "${lego_args[@]}" < /dev/null \
  || fail "lego failed (see its output above); the installed certificate was not touched"

new_cert="${data}/certificates/${CERT_NAME}.crt"
new_key="${data}/certificates/${CERT_NAME}.key"
[[ -s "$new_cert" && -s "$new_key" ]] || fail "lego reported success but ${new_cert} or ${new_key} is missing"

# ---------------------------------------------------------------- 4. check what lego produced
log "4/7 checking the certificate"
fingerprint() {
  local file="$1"
  openssl x509 -noout -fingerprint -sha256 -in "$file" | cut -d= -f2
}
check_pair() {
  local cert="$1" key="$2" sans
  openssl x509 -noout -checkend 0 -in "$cert" > /dev/null || fail "${cert} has already expired"
  sans="$(openssl x509 -noout -ext subjectAltName -in "$cert" 2>/dev/null | tr ',' '\n' | sed 's/^[[:space:]]*DNS://')"
  for n in "${names[@]}"; do
    printf '%s\n' "$sans" | grep -qxF -- "$n" || fail "${cert} does not cover ${n}"
  done
  [[ "$(openssl x509 -noout -pubkey -in "$cert" | openssl sha256)" == "$(openssl pkey -pubout -in "$key" | openssl sha256)" ]] \
    || fail "${key} is not the private key of ${cert}"
}
check_pair "$new_cert" "$new_key"
log "     $(openssl x509 -noout -enddate -in "$new_cert"), all ${#names[@]} names, key matches"
if $staging; then
  log "DONE (staging): issuance works end to end -- DNS zone, managed identity and role. Nothing was installed."
  exit 0
fi

# ---------------------------------------------------------------- 5. install in place and reload
compose() { docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"; }
proxy_running() { [[ -n "$(compose ps -q --status running reverse-proxy 2> /dev/null)" ]]; }
if [[ -f "$cert_path" && "$(fingerprint "$cert_path" 2> /dev/null)" == "$(fingerprint "$new_cert")" ]]; then
  log "5/7 the installed certificate is already the current one; nothing to install"
else
  log "5/7 installing the new certificate in place"
  if [[ -f "$cert_path" ]]; then
    cp -p "$cert_path" "${cert_path}.prev"
    [[ -f "$key_path" ]] && cp -p "$key_path" "${key_path}.prev"
  fi
  # Key first, with a restrictive umask for a file that does not exist yet; `cat >` keeps an existing file's inode,
  # owner and mode.
  ( umask 077; cat "$new_key" > "$key_path" )
  cat "$new_cert" > "$cert_path"
  check_pair "$cert_path" "$key_path"
  if proxy_running; then
    compose exec -T reverse-proxy nginx -t < /dev/null \
      || fail "nginx rejected its configuration with the new certificate; the previous pair is in ${cert_path}.prev / ${key_path}.prev"
    compose exec -T reverse-proxy nginx -s reload < /dev/null || fail "nginx did not accept the reload"
    log "     nginx reloaded"
  else
    log "     the reverse proxy is not running; it will load the new certificate when it starts"
  fi
fi

# ---------------------------------------------------------------- 6. what nginx actually serves
served_file="$(mktemp)"
trap 'rm -f "$served_file"' EXIT
fetch_served() {
  if [[ -n "${TLS_SERVED_CERT_FILE:-}" ]]; then
    cp "$TLS_SERVED_CERT_FILE" "$served_file"
  else
    openssl s_client -connect "${TLS_PROBE_HOST}:443" -servername "admin.${base}" < /dev/null 2> /dev/null \
      | openssl x509 > "$served_file" 2> /dev/null
  fi
}
if ! proxy_running && [[ -z "${TLS_SERVED_CERT_FILE:-}" ]]; then
  log "6/7 skipped: nothing is serving yet"
  log "7/7 skipped"
  log "DONE: certificate ready in ${cert_path}"
  exit 0
fi
log "6/7 checking the certificate nginx serves"
want="$(fingerprint "$cert_path")"
served=""
for attempt in 1 2 3 4 5 6; do
  fetch_served || true
  served="$(fingerprint "$served_file" 2> /dev/null || true)"
  [[ "$served" == "$want" ]] && break
  if [[ "$attempt" == 3 && -z "${TLS_SERVED_CERT_FILE:-}" ]]; then
    log "     still serving ${served:-nothing}; recreating the reverse proxy"
    compose up -d --force-recreate reverse-proxy < /dev/null || fail "could not recreate the reverse proxy"
  fi
  sleep "$TLS_VERIFY_WAIT"
done
[[ "$served" == "$want" ]] || fail "nginx serves ${served:-no certificate}, not the installed ${want}"
log "     serving ${want}"

# ---------------------------------------------------------------- 7. enough life left
log "7/7 expiry"
openssl x509 -noout -checkend $((MIN_DAYS_LEFT * 86400)) -in "$served_file" > /dev/null \
  || fail "the served certificate expires within ${MIN_DAYS_LEFT} days ($(openssl x509 -noout -enddate -in "$served_file")): renewal has been failing"
log "DONE: serving a certificate valid until $(openssl x509 -noout -enddate -in "$served_file" | cut -d= -f2)"
