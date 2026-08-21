#!/usr/bin/env bash
# Generate a LOCAL wildcard TLS certificate for CHECKPOINT 1 verification of the prod
# stack against a fake domain (default portal.local). Covers the three SANs the
# subdomain-per-service layout needs: *.<domain>, *.api.<domain>, *.auth.<domain>.
#
# Prefers mkcert (installs a trusted local CA, so browsers do NOT warn -- important
# because a headless OIDC redirect over an untrusted cert can fail silently). Falls
# back to a self-signed openssl cert (curl -k works; trust the .crt for the browser).
#
# Output goes to secrets/ (git-ignored + docker-ignored). Never commit these.
#
# USAGE: scripts/hosting/gen-local-certs.sh [domain] [outdir]
set -euo pipefail

DOMAIN="${1:-portal.local}"
OUTDIR="${2:-secrets}"
mkdir -p "$OUTDIR"
CRT="$OUTDIR/wildcard.crt"
KEY="$OUTDIR/wildcard.key"

if command -v mkcert >/dev/null 2>&1; then
  echo "Using mkcert (trusted local CA -- no browser warnings)."
  mkcert -install
  mkcert -cert-file "$CRT" -key-file "$KEY" \
    "*.${DOMAIN}" "*.api.${DOMAIN}" "*.auth.${DOMAIN}" "${DOMAIN}"
else
  echo "mkcert not found -- generating a self-signed cert with openssl."
  echo "  curl -k works; for the browser, trust ${CRT} or install mkcert (scoop/winget)."
  openssl req -x509 -newkey rsa:2048 -sha256 -days 825 -nodes \
    -keyout "$KEY" -out "$CRT" \
    -subj "/CN=${DOMAIN}" \
    -addext "subjectAltName=DNS:*.${DOMAIN},DNS:*.api.${DOMAIN},DNS:*.auth.${DOMAIN},DNS:${DOMAIN}"
fi

echo "Wrote ${CRT} + ${KEY}"
echo "SANs: *.${DOMAIN}, *.api.${DOMAIN}, *.auth.${DOMAIN}, ${DOMAIN}"
