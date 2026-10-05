#!/bin/sh
# Remove the ABP feed key from everything NuGet records after a restore (#899).
#
# The ABP feed serves the key only as a URL path segment
# (https://nuget.abp.io/<key>/v3/index.json); there is no keyless form. So NuGet
# writes the key into provenance files of its own:
#   - <packages>/<id>/<ver>/.nupkg.metadata   ("source" field), one per package
#   - the NuGet HTTP cache (service index responses)
# Both end up in the layer that ran the restore. Run this in the SAME RUN as the
# restore, while the secret mount is still present, so the layer never holds it.
#
# `source` is informational for later restores and for `dotnet publish
# --no-restore`: restore no-op/up-to-date checks read contentHash and the
# presence of the file, not the source URL. docker/verify-nuget-scrub.sh proves
# that on an image rather than assuming it.
#
# Usage: sh docker/scrub-nuget-key.sh [SECRET_FILE] [ROOT...]
#   Defaults: /run/secrets/abp_nuget_key, the NuGet package + cache folders, and the
#   restore output (obj/ under /src, the stage's WORKDIR, or /artifacts when
#   UseArtifactsOutput is set: project.assets.json and *.nuget.dgspec.json record
#   the feed URL too, which the original issue did not list)
set -eu

SECRET_FILE="${1:-/run/secrets/abp_nuget_key}"
[ "$#" -gt 0 ] && shift
[ -r "$SECRET_FILE" ] || { echo "scrub-nuget-key: secret not readable: $SECRET_FILE" >&2; exit 1; }
KEY="$(tr -d '\r\n' < "$SECRET_FILE")"
[ -n "$KEY" ] || { echo "scrub-nuget-key: secret is empty" >&2; exit 1; }

HOME_DIR="${HOME:-/root}"
if [ "$#" -gt 0 ]; then ROOTS="$*"; else
  ROOTS="$HOME_DIR/.nuget $HOME_DIR/.local/share/NuGet $HOME_DIR/.cache/NuGet /tmp/NuGetScratch /src /artifacts $PWD"
fi

# HTTP/v3 caches only hold feed responses; they are rebuilt on demand.
rm -rf "$HOME_DIR/.local/share/NuGet/v3-cache" "$HOME_DIR/.local/share/NuGet/http-cache" \
       "$HOME_DIR/.cache/NuGet" /tmp/NuGetScratch

# Literal replacement (key is never interpreted as a sed pattern): find the files
# containing it, rewrite each with a shell-side substitution.
for root in $ROOTS; do
  [ -d "$root" ] || continue
  grep -rlF -- "$KEY" "$root" 2>/dev/null | while IFS= read -r file; do
    tmp="$file.scrub.$$"
    # awk -v would mangle backslashes; ENVIRON does not.
    SCRUB_KEY="$KEY" awk '{ k=ENVIRON["SCRUB_KEY"]; out="";
        while ((i = index($0, k)) > 0) { out = out substr($0, 1, i-1) "REDACTED"; $0 = substr($0, i+length(k)) }
        print out $0 }' "$file" > "$tmp"
    cat "$tmp" > "$file"   # keep owner, mode and inode
    rm -f "$tmp"
  done
done

# Fail the build rather than ship a layer that still holds it.
LEFT="$(for root in $ROOTS; do [ -d "$root" ] && grep -rlF -- "$KEY" "$root" 2>/dev/null; done | wc -l)"
[ "$LEFT" -eq 0 ] || { echo "scrub-nuget-key: $LEFT file(s) still contain the key" >&2; exit 1; }
exit 0
