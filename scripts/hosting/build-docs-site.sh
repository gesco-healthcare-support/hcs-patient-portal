#!/usr/bin/env bash
# Builds the documentation site the reverse proxy serves at https://<BASE_DOMAIN>/docs/ (2026-09-29).
#
# WHAT IT DOES
#   1. Builds the builder image from docker/docs-site/Dockerfile (hash-locked MkDocs toolchain).
#   2. Runs `mkdocs build --strict` in it over a READ-ONLY mount of this checkout, into a temporary
#      directory inside the container.
#   3. Runs scripts/docs/check-site-exclusions.sh on that build.
#   4. Only if both pass, replaces the CONTENTS of docker/nginx-proxy/docs-site/, which
#      docker-compose.prod.yml mounts read-only into the reverse proxy.
# A failed build or guard exits non-zero and leaves the served site exactly as it was. Safe to re-run.
#
# RUN IT BEFORE COMPOSE on every deploy (see docs/devops/RUNTIME-AND-DATA-PROFILE.md, "Deployment"):
# it creates docker/nginx-proxy/docs-site/ as the deploy user. If compose starts first, Docker creates
# the missing mount source as root and this script can no longer write into it.
#
# WHY THE CONTENTS AND NOT THE DIRECTORY. A bind mount pins the directory itself. Renaming a freshly
# built directory into place would leave nginx serving the old, deleted one until the container was
# recreated, so the build is copied INTO the existing directory instead.
#
# NETWORK. The MkDocs privacy plugin downloads the theme's fonts and the diagram library at build time,
# so the browser loads nothing from another host. The build therefore needs internet access.
#
# USAGE (from anywhere; it locates the repository root itself):
#   ./scripts/hosting/build-docs-site.sh
# Env (optional): ENV_FILE -- the compose env file holding BASE_DOMAIN (default secrets/env.prod, the
# same name the sibling hosting scripts use).
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd -- "${SCRIPT_DIR}/../.." && pwd)"
cd -- "$ROOT_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
OUT_DIR="docker/nginx-proxy/docs-site"
IMAGE="hcs-docs-builder"

[[ -r "$ENV_FILE" ]] || {
  echo "ERROR: env file '${ENV_FILE}' not found or not readable in ${ROOT_DIR}." >&2
  echo "It must define BASE_DOMAIN: the site is built for https://<BASE_DOMAIN>/docs/." >&2
  exit 1
}

# secrets/env.prod is a compose env file, NOT a shell script, so it is parsed rather than sourced.
# The last BASE_DOMAIN line wins, as in compose; surrounding quotes, spaces and a CR are removed.
BASE_DOMAIN="$(grep -E '^[[:space:]]*BASE_DOMAIN=' "$ENV_FILE" | tail -n 1 | cut -d= -f2- \
  | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")" || true
[[ -n "$BASE_DOMAIN" ]] || {
  echo "ERROR: BASE_DOMAIN is missing or empty in '${ENV_FILE}'. Refusing to build." >&2
  exit 1
}

# The site URL must be the hostname people browse: the privacy plugin writes the diagram library's
# URL as an ABSOLUTE URL on this host, and the /docs/ Content-Security-Policy allows only 'self'.
SITE_URL="https://${BASE_DOMAIN}/docs/"

mkdir -p -- "$OUT_DIR"

# Git Bash on a Windows workstation (local verification) needs a Windows path for the mounts and no
# MSYS path rewriting of the container-side paths. Both settings do nothing on the Linux server.
HOST_ROOT="$(pwd -W 2>/dev/null || pwd)"
export MSYS_NO_PATHCONV=1

echo "Building ${IMAGE} from docker/docs-site/Dockerfile ..."
docker build --quiet -f docker/docs-site/Dockerfile -t "$IMAGE" .github > /dev/null

echo "Building the site for ${SITE_URL} ..."
# --user: the output belongs to whoever runs the deploy, never to root.
# safe.directory: git refuses a repository owned by a different user ("dubious ownership"), which is
#   what a bind mount looks like from inside the container. Set through the environment because HOME
#   is a throwaway directory and the repository is mounted read-only.
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOCS_SITE_URL="$SITE_URL" \
  -e DOCS_PRIVACY_CACHE_DIR=/tmp/privacy-cache \
  -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/repo \
  -v "${HOST_ROOT}:/repo:ro" \
  -v "${HOST_ROOT}/${OUT_DIR}:/out" \
  -w /repo \
  "$IMAGE" sh -c '
    set -eu
    git --version
    echo "revision: $(git rev-parse --short HEAD) ($(git rev-parse --abbrev-ref HEAD))"
    mkdocs build --strict --site-dir /tmp/site
    sh scripts/docs/check-site-exclusions.sh /tmp/site
    find /out -mindepth 1 -delete
    cp -R /tmp/site/. /out/
    echo "pages: $(find /out -name "*.html" | wc -l)"
  '

echo "Done: ${OUT_DIR} now holds the site served at ${SITE_URL}"
