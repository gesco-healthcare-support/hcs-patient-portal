#!/usr/bin/env bash
# The portal's public TLS certificate, issued and renewed by Let's Encrypt on the Azure VM (2026-10-13).
#
# WHY. The office server uses a wildcard certificate from the internal CA, which no browser outside the office
# trusts. The Azure VM is public, so it needs a publicly trusted certificate, and one that renews itself: a Let's
# Encrypt certificate lasts 90 days, and a lapsed one takes the whole site down for everyone at once.
#
# TWO WAYS TO PROVE THE DOMAIN, chosen by ACME_CHALLENGE (env file or environment; default dns-01):
#
#   dns-01 (the plan when the base domain is DELEGATED to an Azure DNS zone). lego writes a TXT record into the
#     zone and removes it afterwards, signing in as the VM's own managed identity, which the fast-path template
#     grants DNS Zone Contributor on that one zone. No secret is stored for it. One certificate covers the four
#     names nginx serves:   <base>   *.<base>   *.api.<base>   *.auth.<base>
#     DNS-01 is the only challenge Let's Encrypt accepts for wildcard names.
#
#   http-01 (the fallback, 2026-10-15, when the public names are A records at the registrar instead of a delegated
#     zone, so nothing on the VM can write DNS). Let's Encrypt fetches a token from
#     http://<name>/.well-known/acme-challenge/ on port 80, which nginx serves from a webroot this script hands to
#     lego. HTTP-01 cannot issue wildcards, so the certificate lists every name EXPLICITLY:
#       <base>, the hosts nginx answers itself (www, api, auth, health, minio = TenantNaming.ProxyReservedSlugs),
#       admin.<base>, admin.api.<base>, admin.auth.<base>, and for every office in the host database
#       <office>.<base>, <office>.api.<base>, <office>.auth.<base>.
#     The office list is read from SaasTenants on every run. A run that finds a name the stored certificate lacks
#     (an office was added) asks for a new certificate at once instead of waiting for the renewal window. So after
#     creating an office, run this script (or wait for the daily timer): until then the new office's names are not
#     on the certificate. Let's Encrypt allows at most 100 names, which is about 30 offices; the run refuses more.
#     On a fresh VM there is no certificate yet, and nginx will not start without one, so the first run writes a
#     short-lived self-signed placeholder and stops; start the stack, then run again.
#
# WHAT IT DOES, in order. Each step stops the run on failure, and a failed run alerts by email through
# hcs-portal-backup-alert@ (OnFailure= on hcs-portal-tls-renew.service).
#   1. Pre-flight: the env file, BASE_DOMAIN, the challenge, where nginx reads the certificate (TLS_CERT_PATH /
#      TLS_KEY_PATH), and the names (http-01: from the office list).
#   2. dns-01: finds the VM's subscription and resource group from the Azure instance metadata service
#      (overridable). http-01: requires nginx to be running and proves it serves the challenge webroot on port 80,
#      before Let's Encrypt is asked anything (its failed-validation limit is 5 per name per hour).
#   3. Runs lego. It issues on the first run and renews afterwards, only once the certificate is in the last third
#      of its life (or when Let's Encrypt's renewal info says so). On other days it changes nothing. If the stored
#      certificate lacks a wanted name, it is set aside first so lego issues a new one.
#   4. Checks what lego produced: unexpired, every wanted name present, private key matching the certificate, and
#      a chain that ends where the Case Tracker's Java runtime can follow it (THE CHAIN, below).
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
#   scripts/hosting/renew-tls-cert.sh --staging   issue against Let's Encrypt's STAGING CA, to prove the challenge
#                                                 path end to end without touching rate limits. Staging
#                                                 certificates are NOT trusted, so this mode never installs anything.
#
# THE CHAIN (2026-10-20). Let's Encrypt can offer more than one chain for the same certificate. The one served on
# 2026-10-20 is leaf -> YR2 -> Root YR, where that Root YR certificate is the cross-sign issued by ISRG Root X1. The
# Case Tracker calls this server from a stock Java runtime whose trust store has ISRG Root X1 but not Root YR. So a
# chain that stops at Root YR would pass browsers and curl, which trust Root YR, and break every Case Tracker call
# (the changes feed, attendance and document reads) at once. lego is therefore asked for the chain whose top
# certificate is issued by ACME_PREFERRED_CHAIN. lego falls back to the default chain, silently, when none matches,
# so step 4 also refuses a chain that does not end there: nothing is installed, the run fails (and so alerts), and
# the certificate in place keeps being served, normally with about 30 days left. Change ACME_PREFERRED_CHAIN only
# once the Case Tracker trusts the new root.
#
# State lives in secrets/acme/ (the ACME account key and the certificate's private key), which is why it sits under
# secrets/: copy secrets/ off the VM with everything else. Losing it costs nothing worse than a fresh issuance.
#
# Env (all optional): ENV_FILE, ACME_DIR, LEGO_IMAGE, ACME_EMAIL and ACME_CHALLENGE (both also read from the env
#   file), MIN_DAYS_LEFT, COMPOSE_FILE, TLS_PROBE_HOST (default 127.0.0.1), TLS_VERIFY_WAIT (seconds between checks
#   of the served certificate, default 5), ACME_PREFERRED_CHAIN (default "ISRG Root X1") and
#   ACME_STAGING_PREFERRED_CHAIN (default "(STAGING) Pretend Pear X1", the staging CA's counterpart, so --staging
#   proves the same check). dns-01 only: AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP (default: this
#   VM's, from instance metadata). http-01 only: ACME_WEBROOT (default docker/nginx-proxy/acme-webroot, the
#   directory compose mounts into nginx), HOST_DB_NAME (default CaseEvaluation). TESTS ONLY: TLS_SERVED_CERT_FILE
#   reads the "served" certificate from this file instead of connecting to nginx.
set -euo pipefail
export LC_ALL=C

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
ACME_DIR="${ACME_DIR:-secrets/acme}"
ACME_WEBROOT="${ACME_WEBROOT:-docker/nginx-proxy/acme-webroot}"
HOST_DB_NAME="${HOST_DB_NAME:-CaseEvaluation}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
MIN_DAYS_LEFT="${MIN_DAYS_LEFT:-14}"
TLS_PROBE_HOST="${TLS_PROBE_HOST:-127.0.0.1}"
TLS_VERIFY_WAIT="${TLS_VERIFY_WAIT:-5}"
# lego v5.5.2 (2026-09-23), pinned by digest so the tool cannot change without a commit.
LEGO_IMAGE="${LEGO_IMAGE:-goacme/lego:v5.5.2@sha256:1944e8c36055beec47c7de6f15202b41128be75eea0ffa257f0c14d93c5155fd}"
LE_PRODUCTION="https://acme-v02.api.letsencrypt.org/directory"
LE_STAGING="https://acme-staging-v02.api.letsencrypt.org/directory"
CERT_NAME="portal"
# The single-label hosts nginx answers with an EXACT server_name. Must equal TenantNaming.ProxyReservedSlugs; a test
# compares the two, so a new one cannot be added on one side only.
PROXY_HOSTS=(api auth health minio www)
LE_MAX_NAMES=100

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
compose() { docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"; }
proxy_running() { [[ -n "$(compose ps -q --status running reverse-proxy 2> /dev/null)" ]]; }
# The DNS names a certificate covers, one per line.
sans_of() {
  local cert="$1"
  openssl x509 -noout -ext subjectAltName -in "$cert" 2>/dev/null | tr ',' '\n' | sed -n 's/^[[:space:]]*DNS://p'
}
# The wanted names the certificate in $1 does not cover, space-separated; empty when it covers them all.
missing_names() {
  local sans n out=""
  sans="$(sans_of "$1")"
  # A here-string, not `printf | grep -q`. Under pipefail that pipe can report a covered name as missing: bash's
  # printf writes line by line, grep -q exits on the first match, and a later line then hits a closed pipe, so the
  # pipeline fails even though grep found the name. Measured: 1 false miss in 1,500 runs on a busy machine, which
  # is how CI failed on 2026-10-23. A here-string has no pipe, so grep's own answer is the only status.
  for n in "${names[@]}"; do
    grep -qxF -- "$n" <<< "$sans" || out+="${out:+ }$n"
  done
  printf '%s' "$out"
}

# Every office's subdomain label, one per line: the lowercased tenant name IS the slug (TenantNaming.DeriveSlug
# validates rather than transforms). The query runs inside the SQL container with the container's own sa password,
# so the password never passes through this script or a command line on the host.
office_slugs() {
  local query="SET NOCOUNT ON; SELECT LOWER(Name) FROM SaasTenants WHERE IsDeleted = 0 ORDER BY LOWER(Name);"
  compose exec -T sql-server bash -c \
    'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -d "$0" -Q "$1"' \
    "$HOST_DB_NAME" "$query" < /dev/null
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
challenge="${ACME_CHALLENGE:-$(env_value ACME_CHALLENGE)}"
challenge="${challenge:-dns-01}"
[[ "$challenge" == dns-01 || "$challenge" == http-01 ]] \
  || fail "ACME_CHALLENGE must be dns-01 or http-01, not [$challenge]"
command -v openssl > /dev/null || fail "openssl is not installed"
log "     challenge: ${challenge}"

if [[ "$challenge" == dns-01 ]]; then
  names=("$base" "*.$base" "*.api.$base" "*.auth.$base")
else
  names=("$base")
  for h in "${PROXY_HOSTS[@]}"; do names+=("${h}.${base}"); done
  names+=("admin.${base}" "admin.api.${base}" "admin.auth.${base}")
  if [[ -s "$cert_path" ]]; then
    offices="$(office_slugs)" || fail "cannot read the office list from ${HOST_DB_NAME}.SaasTenants: is sql-server running?"
    while IFS= read -r slug; do
      slug="$(printf '%s' "$slug" | tr -d '\r' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
      [[ -z "$slug" ]] && continue
      [[ "$slug" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$ ]] \
        || fail "office [$slug] in SaasTenants is not a valid DNS label, so it cannot have a certificate name"
      for reserved in admin "${PROXY_HOSTS[@]}"; do
        [[ "$slug" == "$reserved" ]] && fail "office [$slug] collides with a reserved host name"
      done
      names+=("${slug}.${base}" "${slug}.api.${base}" "${slug}.auth.${base}")
    done <<< "$offices"
  fi
  (( ${#names[@]} <= LE_MAX_NAMES )) \
    || fail "${#names[@]} names, but Let's Encrypt allows at most ${LE_MAX_NAMES} on one certificate: split the certificate or move to DNS-01 wildcards"
fi
log "     ${#names[@]} names: ${names[*]}"

# ---------------------------------------------------------------- 2. dns-01: the zone / http-01: the webroot
if [[ "$challenge" == dns-01 ]]; then
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
else
  log "2/7 the HTTP-01 webroot"
  challenge_dir="${ACME_WEBROOT}/.well-known/acme-challenge"
  mkdir -p "$challenge_dir"
  chmod 755 "$ACME_WEBROOT" "${ACME_WEBROOT}/.well-known" "$challenge_dir"
  if [[ ! -s "$cert_path" || ! -s "$key_path" ]]; then
    # nginx refuses to start without the certificate files its 443 servers name, and HTTP-01 needs nginx answering
    # on port 80. A short-lived self-signed pair breaks that loop; it is replaced by the first real issuance.
    san=""; for n in "${names[@]}"; do san+="${san:+,}DNS:$n"; done
    mkdir -p "$(dirname "$cert_path")" "$(dirname "$key_path")"
    ( umask 077; openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj "/CN=${base}" -addext "subjectAltName=${san}" \
        -keyout "$key_path" -out "$cert_path" 2>/dev/null ) || fail "could not write the placeholder certificate"
    log "DONE (placeholder): no certificate existed, so a 2-day self-signed one is in ${cert_path} for nginx to start"
    log "     with. Start the stack, then run this script again to issue the real certificate."
    exit 0
  fi
  proxy_running || fail "the reverse proxy is not running: HTTP-01 needs nginx answering on port 80"
  # Prove nginx serves the webroot BEFORE asking Let's Encrypt: a missing mount or location would otherwise only
  # show up as a failed validation, and those are rate-limited.
  token="probe-$(openssl rand -hex 16)"
  printf '%s' "$token" > "${challenge_dir}/${token}"
  probe_reply="$(curl -s --max-time 10 -H "Host: ${base}" "http://${TLS_PROBE_HOST}/.well-known/acme-challenge/${token}" 2>/dev/null || true)"
  rm -f "${challenge_dir}/${token}"
  [[ "$probe_reply" == "$token" ]] \
    || fail "nginx does not serve ${ACME_WEBROOT} at /.well-known/acme-challenge/ on port 80 (is the acme-webroot volume mounted, and is the template's port-80 location in place?)"
  log "     nginx serves the challenge path on port 80"
fi

# ---------------------------------------------------------------- 3. lego
if $staging; then
  server="$LE_STAGING"; data="${ACME_DIR}/staging"
  preferred_chain="${ACME_STAGING_PREFERRED_CHAIN:-(STAGING) Pretend Pear X1}"
else
  server="$LE_PRODUCTION"; data="$ACME_DIR"
  preferred_chain="${ACME_PREFERRED_CHAIN:-ISRG Root X1}"
fi
[[ "$preferred_chain" =~ ^[[:print:]]+$ ]] || fail "the preferred chain must be a certificate common name, not [$preferred_chain]"
log "3/7 lego against $($staging && echo STAGING || echo production) Let's Encrypt"
( umask 077; mkdir -p "$data" )
chmod 700 "$ACME_DIR" "$data"
data_abs="$(cd "$data" && pwd)"
stored="${data}/certificates/${CERT_NAME}.crt"
if [[ -s "$stored" ]]; then
  lacking="$(missing_names "$stored")"
  if [[ -n "$lacking" ]]; then
    # lego only renews inside the renewal window; a new name (an office added) must not wait for it.
    stamp="$(date -u +%Y%m%dT%H%M%SZ)"
    for f in "${data}/certificates/${CERT_NAME}".{crt,key,issuer.crt,json}; do
      [[ -e "$f" ]] && mv "$f" "${f}.superseded-${stamp}"
    done
    log "     the stored certificate lacks ${lacking}; set aside (.superseded-${stamp}) so lego issues a new one"
  fi
fi
lego_args=(run --accept-tos --server "$server" --path /data --cert.name "$CERT_NAME" --key-type rsa2048
  --preferred-chain "$preferred_chain")
[[ -n "$email" ]] && lego_args+=(--email "$email")
for n in "${names[@]}"; do lego_args+=(--domains "$n"); done
if [[ "$challenge" == dns-01 ]]; then
  # Host networking, so the managed-identity token request reaches the metadata address directly.
  docker run --rm --network host -v "${data_abs}:/data" \
    -e AZURE_AUTH_METHOD=msi -e AZURE_AUTH_MSI_TIMEOUT=10s \
    -e AZURE_SUBSCRIPTION_ID="$subscription" -e AZURE_RESOURCE_GROUP="$resource_group" -e AZURE_ZONE_NAME="$base" \
    "$LEGO_IMAGE" "${lego_args[@]}" --dns azuredns < /dev/null \
    || fail "lego failed (see its output above); the installed certificate was not touched"
else
  webroot_abs="$(cd "$ACME_WEBROOT" && pwd)"
  docker run --rm -v "${data_abs}:/data" -v "${webroot_abs}:/webroot" \
    "$LEGO_IMAGE" "${lego_args[@]}" --http --http.webroot /webroot < /dev/null \
    || fail "lego failed (see its output above); the installed certificate was not touched"
fi

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
  local cert="$1" key="$2" lacking
  openssl x509 -noout -checkend 0 -in "$cert" > /dev/null || fail "${cert} has already expired"
  lacking="$(missing_names "$cert")"
  [[ -z "$lacking" ]] || fail "${cert} does not cover ${lacking}"
  [[ "$(openssl x509 -noout -pubkey -in "$cert" | openssl sha256)" == "$(openssl pkey -pubout -in "$key" | openssl sha256)" ]] \
    || fail "${key} is not the private key of ${cert}"
}
check_pair "$new_cert" "$new_key"
log "     $(openssl x509 -noout -enddate -in "$new_cert"), all ${#names[@]} names, key matches"
# The issuer common name of the LAST certificate in a PEM bundle: the top of the chain nginx sends (THE CHAIN, above).
chain_top_issuer() {
  awk '/-----BEGIN CERTIFICATE-----/ { pem = "" } { pem = pem $0 "\n" } /-----END CERTIFICATE-----/ { last = pem }
       END { printf "%s", last }' "$1" \
    | openssl x509 -noout -issuer -nameopt multiline,utf8 2> /dev/null | sed -n 's/^[[:space:]]*commonName[[:space:]]*= //p'
}
top_issuer="$(chain_top_issuer "$new_cert" || true)"
[[ "$top_issuer" == "$preferred_chain" ]] \
  || fail "the chain lego obtained ends at a certificate issued by [${top_issuer:-nothing readable}], not [${preferred_chain}]: the Case Tracker's Java runtime cannot follow it, so nothing was installed and the certificate in place is still served. See THE CHAIN in this script's header."
log "     the chain ends at a certificate issued by ${preferred_chain}"
if $staging; then
  log "DONE (staging): issuance works end to end with ${challenge}. Nothing was installed."
  exit 0
fi

# ---------------------------------------------------------------- 5. install in place and reload
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
