#!/usr/bin/env bash
# Generate the OpenIddict token signing/encryption certificate (openiddict.pfx)
# for a NON-development deployment of the Patient Portal.
#
# ABP's AddProductionEncryptionAndSigningCertificate("openiddict.pfx", passphrase)
# (src/.../AuthServer/CaseEvaluationAuthServerModule.cs) loads this file from the
# AuthServer content root (/app in the container) at startup and uses it as BOTH the
# signing and the encryption credential. It is NOT the nginx TLS certificate --
# browsers never see it; it only signs the tokens OpenIddict issues. In Development
# ABP auto-generates a throwaway cert instead, so this file is only needed for
# Staging/Production (ASPNETCORE_ENVIRONMENT != Development).
#
# SECURITY:
#   - The .pfx and its passphrase are SECRETS. Never commit them (see .gitignore).
#     Mount the .pfx into the container at runtime and pass the passphrase via the
#     git-ignored env file (AuthServer__CertificatePassPhrase). Do NOT bake it into
#     the image.
#   - Keep a secure backup. Rotating the signing cert invalidates all live tokens;
#     users must re-authenticate.
#
# USAGE:
#   AUTHSERVER_CERT_PASSPHRASE=... scripts/hosting/gen-openiddict-cert.sh <output.pfx> [days]
#
# Requires: openssl (bundled with Git for Windows / present in the Git Bash env).
set -euo pipefail

OUT="${1:-openiddict.pfx}"
DAYS="${2:-3650}"

if [ -z "${AUTHSERVER_CERT_PASSPHRASE:-}" ]; then
  echo "ERROR: set AUTHSERVER_CERT_PASSPHRASE (the passphrase that protects ${OUT})." >&2
  echo "       Use the SAME value for AuthServer__CertificatePassPhrase in .env.prod." >&2
  exit 1
fi

if [ -e "$OUT" ]; then
  echo "ERROR: ${OUT} already exists. Refusing to overwrite -- that would invalidate" >&2
  echo "       every live token. Remove it deliberately only to rotate the signing key." >&2
  exit 1
fi

TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TMPDIR"' EXIT

# Self-signed RSA-2048 cert + key. OpenIddict uses the RSA key for RS256 signing and
# RSA-OAEP encryption; no TLS server-auth EKU is required (this is not a server cert).
openssl req -x509 -newkey rsa:2048 -sha256 -days "$DAYS" -nodes \
  -keyout "$TMPDIR/key.pem" -out "$TMPDIR/cert.pem" \
  -subj "/CN=CaseEvaluation OpenIddict Token Signing"

# Bundle into a PKCS#12 (.pfx) protected by the passphrase ABP will read.
openssl pkcs12 -export \
  -inkey "$TMPDIR/key.pem" -in "$TMPDIR/cert.pem" \
  -out "$OUT" -passout "pass:${AUTHSERVER_CERT_PASSPHRASE}"

chmod 600 "$OUT" 2>/dev/null || true
echo "Wrote ${OUT} (valid ${DAYS} days, self-signed RSA-2048)."
echo "Next:"
echo "  - mount it read-only at /app/openiddict.pfx in the AuthServer container"
echo "  - set AuthServer__CertificatePassPhrase to the same passphrase in .env.prod"
echo "  - keep a secure backup; never commit the .pfx or its passphrase"
