#!/usr/bin/env bash
# Store (or remove) ONE office's doctor signature image, printed on the QME Form 110 page of the PQME packet
# (2026-11-03). There is no upload screen on purpose: the image is placed by an operator, once per office.
#
# The packet job reads exactly one object per office, host/office-signatures/<office id without hyphens>.png in
# the documents bucket (OfficeSignatureReader). That key is OUTSIDE the tenants/<office id>/* prefix the Case
# Tracker's read-only key covers, and it is not the office logo's key (host/<office id>.png). An office with no such
# object prints the blank signature line, exactly as before.
#
# USAGE (on the server, from the repository root; MinIO must be running):
#   scripts/hosting/office-signature.sh <office id> <png file>    store the image (replaces an existing one)
#   scripts/hosting/office-signature.sh --remove <office id>      delete it; the packet prints the blank line again
#
# The office id is the tenant's Id in the host database: SELECT Id, Name FROM SaasTenants.
# The file must be a PNG (checked by its first eight bytes, not its name), not empty, and at most 200 KB; the
# application applies the same two checks on every read, so a file that passes here is one it will print.
# It must also have a TRANSPARENT background (an alpha channel or a tRNS chunk): the image is laid over the
# signature line and slightly overlaps the line above, so a white background would hide that text. Make the
# white around a scanned signature transparent first, e.g. with any image editor, or with Pillow:
#   im = Image.open(src).convert('RGBA'); im.putdata([(r, g, b, 0) if min(r, g, b) > 235 else (r, g, b, a)
#       for r, g, b, a in im.getdata()]); im.save(dst)
#
# It uses the application's own MinIO credential (MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY, policy
# portal-app-rw: object read/write on the documents bucket, no admin), not the root login. Credentials travel on
# the piped script's stdin, never on a command line. Output is ONLY the object key, the stored size and the
# sha256 of the file: never the image, never base64.
#
# Env (all optional): ENV_FILE, DOCKER_NET, MINIO_URL, MC_IMAGE.
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

MAX_BYTES=204800
PNG_MAGIC=89504e470d0a1a0a

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

# One key from the env file; empty when absent. Never echoed.
env_value() { local key="$1"; { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2- | tr -d '\r'; }

# A value as a single-quoted POSIX shell word, for the script piped into the container.
sq() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\''/g")"; }

GUID_RE='^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
USAGE="usage: $0 <office id> <png file>   |   $0 --remove <office id>"

remove=false
if [[ "${1:-}" == --remove ]]; then
  remove=true
  [[ $# -eq 2 ]] || fail "$USAGE"
  office="$2"
else
  [[ $# -eq 2 ]] || fail "$USAGE"
  office="$1"
  file="$2"
fi

office="$(printf '%s' "$office" | tr 'A-F' 'a-f')"
[[ "$office" =~ $GUID_RE ]] \
  || fail "the office id must be a GUID such as 0f8fad5b-d9cb-469f-a165-70867728950e"
key="host/office-signatures/${office//-/}.png"

[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"
app_key="$(env_value MINIO_APP_ACCESS_KEY)"
app_secret="$(env_value MINIO_APP_SECRET_KEY)"
bucket="$(env_value MINIO_BUCKET_NAME)"
bucket="${bucket:-case-evaluation-documents}"
[[ -n "$app_key" && -n "$app_secret" ]] || fail "MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY not found in $ENV_FILE"

# Run a script inside the pinned mc image on the stack's network, with the alias already set. Extra arguments
# (the volume mount) go to docker run before the image.
run_mc() {
  { printf 'set -eu\n'
    printf 'mc alias set local %s %s %s > /dev/null\n' "$(sq "$MINIO_URL")" "$(sq "$app_key")" "$(sq "$app_secret")"
    cat
  } | docker run --rm -i --network "$DOCKER_NET" "$@" --entrypoint /bin/sh "$MC_IMAGE" -s
}

if $remove; then
  run_mc <<EOF
mc rm $(sq "local/${bucket}/${key}") > /dev/null
EOF
  log "removed ${key}; the PQME packet prints the blank signature line again"
  exit 0
fi

[[ -f "$file" && ! -L "$file" ]] || fail "not a regular file: $file"
size="$(wc -c < "$file" | tr -d ' ')"
[[ "$size" -gt 0 ]] || fail "the file is empty"
[[ "$size" -le "$MAX_BYTES" ]] || fail "the file is ${size} bytes; the limit is ${MAX_BYTES} (200 KB)"
magic="$(head -c 8 "$file" | od -An -tx1 | tr -d ' \n')"
[[ "$magic" == "$PNG_MAGIC" ]] || fail "the file is not a PNG (its first eight bytes are not the PNG signature)"
# The IHDR colour type (byte 25): 4 and 6 carry an alpha channel; any other type needs a tRNS chunk.
color_type="$(od -An -tu1 -j25 -N1 "$file" | tr -d ' \n')"
if [[ "$color_type" != 4 && "$color_type" != 6 ]] && ! grep -qa tRNS "$file"; then
  fail "the PNG has no transparent background; make the white around the signature transparent first (see the header)"
fi
sha="$(sha256sum "$file" | cut -d' ' -f1)"

# Absolute, so the bind mount source is never read as a volume name.
abs_file="$(cd "$(dirname "$file")" && pwd)/$(basename "$file")"

stat_out="$(run_mc -v "${abs_file}:/office-signature.png:ro" <<EOF
mc cp /office-signature.png $(sq "local/${bucket}/${key}") > /dev/null
mc stat --json $(sq "local/${bucket}/${key}")
EOF
)" || fail "MinIO refused the upload (see the error above); nothing was confirmed stored"

stored="$(printf '%s' "$stat_out" | sed -n 's/.*"size":\([0-9][0-9]*\).*/\1/p' | head -1)"
[[ "$stored" == "$size" ]] || fail "stored size [${stored:-unknown}] does not match the file's ${size} bytes"

log "stored: key=${key} size=${stored} sha256=${sha}"
