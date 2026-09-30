#!/bin/sh
# Fails if anything mkdocs.yml excludes from the documentation site was built into it anyway.
#
# USAGE: sh scripts/docs/check-site-exclusions.sh <built-site-dir>
#
# WHY THIS EXISTS. The exclusions live in mkdocs.yml (exclude_docs), where one deleted line would
# publish a page that must not be published. This list is kept here on purpose, INDEPENDENT of
# mkdocs.yml, so that deleting an exclusion there fails the build instead of publishing silently.
# Keep the two in step.
#
# One list, two callers: the "Docs: Build site" job in .github/workflows/docs-site.yml, and
# scripts/hosting/build-docs-site.sh, which builds the copy the portal serves at /docs/. Each checks
# both the built files and the search index, because a page can leak through either.
#
# POSIX sh, not bash: the server build runs it inside the slim Python builder image. The ::error::
# prefix is a GitHub Actions annotation; elsewhere it is just a marker on the line.
set -u

site="${1:?usage: check-site-exclusions.sh <built-site-dir>}"
index="$site/search/search_index.json"
failed=0

for path in \
  runbooks/DEMO-LOGINS \
  security/THREAT-MODEL \
  security/SESSION-KEY-ENCRYPTION \
  production-hardening \
  findings \
  runbooks/findings \
  runbooks/investigations \
  plans \
  parity-research \
  feedback-research \
  research \
  superpowers
do
  if [ -e "$site/$path" ] || [ -e "$site/$path.md" ] || [ -e "$site/$path.html" ]; then
    echo "::error::excluded path was built into the site: $path" >&2
    failed=1
  fi
  if [ -f "$index" ] && grep -q "\"location\":\"$path[/#\"]" "$index"; then
    echo "::error::excluded path is in the search index: $path" >&2
    failed=1
  fi
done

test -s "$index" || { echo "::error::search index missing: $index" >&2; failed=1; }
exit "$failed"
