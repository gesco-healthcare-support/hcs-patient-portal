#!/usr/bin/env bash
# Generate the certificate that encrypts the Data Protection key ring (dataprotection.pfx)
# for a NON-development deployment of the Patient Portal.
#
# Both host processes (authserver and api) share one key ring in Redis. Each new key is
# encrypted with this certificate before it is stored (CaseEvaluationKeyRing in
# src/HealthcareSupport.CaseEvaluation.HttpApi/DataProtection/), so BOTH containers mount the
# SAME file with the SAME passphrase. See docs/security/SESSION-KEY-ENCRYPTION.md.
#
# It is a SEPARATE certificate from openiddict.pfx on purpose: rotating the signing
# certificate signs everyone out, rotating this one does not, and one certificate doing both
# jobs would weld those together.
#
# SECURITY:
#   - The .pfx and its passphrase are SECRETS. Never commit them (secrets/ is git-ignored).
#   - Keep a secure backup. LOSING this certificate makes the key ring unreadable: everyone is
#     signed out and outstanding confirmation and reset links stop working (nothing durable or
#     clinical is lost). SESSION-KEY-ENCRYPTION.md section 9 has the reset procedure.
#   - Both containers run as the non-root "app" user (UID/GID 1654), so the file must be
#     group-readable by 1654, and unreadable to everyone else. See the end of this script.
#
# USAGE:
#   DATAPROTECTION_CERT_PASSPHRASE=... scripts/hosting/gen-dataprotection-cert.sh <output.pfx> [days]
#
# Requires: openssl (bundled with Git for Windows / present in the Git Bash env).
set -euo pipefail

OUT="${1:-dataprotection.pfx}"
DAYS="${2:-3650}"
APP_GID=1654

if [[ -z "${DATAPROTECTION_CERT_PASSPHRASE:-}" ]]; then
  echo "ERROR: set DATAPROTECTION_CERT_PASSPHRASE (the passphrase that protects ${OUT})." >&2
  echo "       Use the SAME value for DATAPROTECTION_CERT_PASSPHRASE in secrets/env.prod." >&2
  exit 1
fi

if [[ -e "$OUT" ]]; then
  echo "ERROR: ${OUT} already exists. Refusing to overwrite: keys already encrypted with it" >&2
  echo "       could no longer be read. To rotate, follow SESSION-KEY-ENCRYPTION.md section 9." >&2
  exit 1
fi

TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TMPDIR"' EXIT

# Self-signed RSA-2048. Data Protection uses the key pair only and does not check validity
# dates (measured, SESSION-KEY-ENCRYPTION.md 6.8), so DAYS sets a rotation cadence, not a cliff.
openssl req -x509 -newkey rsa:2048 -sha256 -days "$DAYS" -nodes \
  -keyout "$TMPDIR/key.pem" -out "$TMPDIR/cert.pem" \
  -subj "/CN=CaseEvaluation Data Protection Key Ring"

openssl pkcs12 -export \
  -inkey "$TMPDIR/key.pem" -in "$TMPDIR/cert.pem" \
  -out "$OUT" -passout "pass:${DATAPROTECTION_CERT_PASSPHRASE}"

# Both host containers run as the runtime image's unprivileged `app` account (uid 1654, gid
# 1654) and read this file through its group, so it must end up mode 640, group 1654. Only open
# it to the group once the group IS 1654: widening to 640 while the file still carries the
# operator's own group would let every member of that group read the key. chgrp to a group the
# operator is not in needs root, so failing here is normal; the file is kept 600 and the command
# printed. The same pattern as gen-openiddict-cert.sh.
#
# Success is judged by the file's resulting group and mode, not by the commands' exit codes:
# Git Bash on Windows reports chgrp and chmod as succeeding while changing neither.
chmod 600 "$OUT" 2>/dev/null || true
chgrp "$APP_GID" "$OUT" 2>/dev/null && chmod 640 "$OUT" 2>/dev/null || true
if [[ "$(stat -c '%g %a' "$OUT" 2>/dev/null)" == "${APP_GID} 640" ]]; then
  access="group ${APP_GID}, mode 640: readable by the authserver and api containers"
  access_todo=""
else
  chmod 600 "$OUT" 2>/dev/null || true
  access="group is not ${APP_GID}: NOT yet readable by the authserver and api containers"
  access_todo="sudo chgrp ${APP_GID} ${OUT} && sudo chmod 640 ${OUT}"
fi

echo "Wrote ${OUT} (valid ${DAYS} days, self-signed RSA-2048; ${access})."
if [[ -n "$access_todo" ]]; then
  echo "  - on the Linux host, let both containers (gid ${APP_GID}) read it, or they fail at startup:"
  echo "      ${access_todo}"
fi
echo "Next:"
echo "  - set DATAPROTECTION_PFX_PATH and DATAPROTECTION_CERT_PASSPHRASE in secrets/env.prod"
echo "  - follow SESSION-KEY-ENCRYPTION.md section 9 to retire the existing unencrypted key"
echo "  - keep a secure backup; never commit the .pfx or its passphrase"
