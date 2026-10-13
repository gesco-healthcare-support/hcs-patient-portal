#!/usr/bin/env bash
# The Case Tracker's read access to ONE office's documents in MinIO (2026-10-13).
#
# The Case Tracker reads portal documents in place, over S3, by object key. On the office server its MinIO login
# was made BY HAND on 2026-08-13: one user, one policy, GetObject on falkinstein's prefix only. Nothing recreates
# it, so a new server (the Azure VM) has no such login, and a new office is unreadable to the Case Tracker until
# someone edits a policy by hand. This script makes that step repeatable, one office at a time.
#
# WHAT IT GRANTS: s3:GetObject on <bucket>/tenants/<tenantId>/* and nothing else. No listing, no writing, no
# deleting, no other office, and nothing on the Case Tracker's own bucket (case-tracker-documents), whose future
# is an open decision. That is the same shape as the hand-made policy, scoped per office: on a host whose MinIO
# endpoint is open to every address, a leaked credential then exposes one office, not all of them.
#
# One user per office, named ct-<first 16 hex digits of the office id>, with policy ct-read-<office id>. The
# secret is generated here and written ONLY to secrets/case-tracker-minio-<office id>.txt (mode 600), never to the
# terminal. Hand that file's values to the Case Tracker owner by a private channel.
#
# USAGE (on the server, from the repository root; MinIO must be running):
#   scripts/hosting/minio-case-tracker-access.sh <office id>            create the office's credential
#   scripts/hosting/minio-case-tracker-access.sh <office id> --rotate   new secret for an existing credential
#   scripts/hosting/minio-case-tracker-access.sh <office id> --show     print the policy and user, change nothing
#
# The office id is the tenant's Id in the host database: SELECT Id, Name FROM SaasTenants.
# Creating refuses when the office already has a credential, here or in MinIO, so a second run cannot silently
# replace a secret the Case Tracker is using. Rotating replaces it: the Case Tracker's old secret stops working
# at once, so arrange the change with them first.
#
# Env (all optional): ENV_FILE, DOCKER_NET, MINIO_URL, MC_IMAGE, OUT_DIR.
set -euo pipefail
export LC_ALL=C

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
DOCKER_NET="${DOCKER_NET:-hcs-patient-portal_default}"
MINIO_URL="${MINIO_URL:-http://minio:9000}"
# The same pinned client minio-init uses. NOT pullable from any registry since 2026-09-30: it must already be on
# the host (docker-compose.prod.yml explains, and how to load it from the rescue archive).
MC_IMAGE="${MC_IMAGE:-quay.io/minio/mc:RELEASE.2025-08-13T08-35-41Z}"
OUT_DIR="${OUT_DIR:-secrets}"

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

# One key from the env file; empty when absent. Never echoed.
env_value() { local key="$1"; { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2- | tr -d '\r'; }

# A value as a single-quoted POSIX shell word, for the script piped into the container. Values travel on stdin,
# never on a command line, where any local user could read them from the process list.
sq() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }

tenant="${1:-}"
mode="${2:-create}"
[[ $# -le 2 ]] || fail "too many arguments (expected <office id> [--rotate|--show])"
case "$mode" in
  create|--rotate|--show) ;;
  *) fail "unknown option: $mode (expected --rotate or --show)" ;;
esac
tenant="$(printf '%s' "$tenant" | tr 'A-F' 'a-f')"
[[ "$tenant" =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ ]] \
  || fail "the first argument must be the office id, a GUID such as 0f8fad5b-d9cb-469f-a165-70867728950e"

[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
root_user="$(env_value MINIO_ROOT_USER)"
root_password="$(env_value MINIO_ROOT_PASSWORD)"
bucket="$(env_value MINIO_BUCKET_NAME)"
bucket="${bucket:-case-evaluation-documents}"
base_domain="$(env_value BASE_DOMAIN)"
[[ -n "$root_user" && -n "$root_password" ]] || fail "MINIO_ROOT_USER / MINIO_ROOT_PASSWORD not found in $ENV_FILE"

compact="${tenant//-/}"
access_key="ct-${compact:0:16}"   # 19 characters; MinIO allows at most 20
policy="ct-read-${tenant}"
prefix="tenants/${tenant}/"
out="${OUT_DIR}/case-tracker-minio-${tenant}.txt"

policy_json="{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Action\":[\"s3:GetObject\"],\"Resource\":[\"arn:aws:s3:::${bucket}/${prefix}*\"]}]}"

# Run a script inside the pinned mc image on the stack's network, with the admin alias already set.
run_mc() {
  { printf 'set -eu\n'
    printf 'mc alias set local %s %s %s > /dev/null\n' "$(sq "$MINIO_URL")" "$(sq "$root_user")" "$(sq "$root_password")"
    cat
  } | docker run --rm -i --network "$DOCKER_NET" --entrypoint /bin/sh "$MC_IMAGE" -s
}

if [[ "$mode" == --show ]]; then
  log "office ${tenant}: user ${access_key}, policy ${policy}"
  run_mc <<EOF
mc admin policy info local $(sq "$policy") || echo "no policy named ${policy}"
mc admin user info local $(sq "$access_key") || echo "no user named ${access_key}"
EOF
  exit 0
fi

if [[ "$mode" == create ]]; then
  [[ ! -e "$out" ]] || fail "${out} already exists: this office already has a credential. Use --rotate to replace its secret."
else
  [[ -e "$out" ]] || fail "${out} does not exist: create the credential first (no --rotate)."
fi

secret="$(openssl rand -hex 20)"   # 40 characters, MinIO's maximum
[[ "$secret" =~ ^[0-9a-f]{40}$ ]] || fail "could not generate a secret (is openssl installed?)"

# Write the file BEFORE MinIO changes, so a secret MinIO accepts is never one nobody kept. On failure it is removed.
mkdir -p "$OUT_DIR"
tmp="${out}.tmp"
trap 'rm -f "$tmp"' EXIT
( umask 077
  { echo "# Case Tracker read-only MinIO credential for office ${tenant}"
    echo "# Issued $(date -u +%FT%TZ) by scripts/hosting/minio-case-tracker-access.sh. Grants s3:GetObject on"
    echo "# ${bucket}/${prefix}* only. Give these values to the Case Tracker owner by a private channel."
    echo "endpoint=https://minio.${base_domain}"
    echo "bucket=${bucket}"
    echo "prefix=${prefix}"
    echo "access_key=${access_key}"
    echo "secret_key=${secret}"
  } > "$tmp" )

if [[ "$mode" == create ]]; then
  check_user="if mc admin user info local $(sq "$access_key") > /dev/null 2>&1; then echo 'ERROR: MinIO already has user ${access_key}; refusing to replace a secret someone may be using (use --rotate)' >&2; exit 3; fi"
else
  check_user="mc admin user info local $(sq "$access_key") > /dev/null || { echo 'ERROR: MinIO has no user ${access_key} to rotate' >&2; exit 3; }"
fi

# The container prints SECRET_SET the moment MinIO accepts the new secret. From then on the file must be kept even
# if a later step fails, or the only copy of a live secret would be thrown away. (The mc image has no grep, so the
# checks below use case.)
set +e
result="$(run_mc <<EOF
${check_user}
cat > /tmp/ct-policy.json <<'POLICY'
${policy_json}
POLICY
mc admin policy create local $(sq "$policy") /tmp/ct-policy.json > /dev/null
mc admin user add local $(sq "$access_key") $(sq "$secret") > /dev/null
echo SECRET_SET
case "\$(mc admin user info local $(sq "$access_key"))" in
  *$(sq "$policy")*) ;;
  *) mc admin policy attach local $(sq "$policy") --user $(sq "$access_key") > /dev/null ;;
esac
case "\$(mc admin user info local $(sq "$access_key"))" in
  *$(sq "$policy")*) echo POLICY_ATTACHED ;;
  *) echo 'ERROR: the policy is not attached to the user' >&2; exit 4 ;;
esac
EOF
)"
rc=$?
set -e
if [[ "$rc" != 0 || "$result" != *POLICY_ATTACHED* ]]; then
  if [[ "$result" == *SECRET_SET* ]]; then
    mv "$tmp" "$out"; trap - EXIT; chmod 600 "$out"
    fail "MinIO ACCEPTED the new secret for ${access_key}, and it is kept in ${out}, but a later step failed (see above). Run --show, fix the cause, then run with --rotate."
  fi
  fail "MinIO refused the change before any secret was set; nothing changed (see the error above)"
fi
mv "$tmp" "$out"
trap - EXIT
chmod 600 "$out"
if [[ "$mode" == create ]]; then
  log "created ${access_key} for office ${tenant}: s3:GetObject on ${bucket}/${prefix}* only"
else
  log "rotated the secret of ${access_key} for office ${tenant}; the Case Tracker's previous secret no longer works"
fi
log "credential written to ${out} (mode 600); the secret is NOT printed here"
