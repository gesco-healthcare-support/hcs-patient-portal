#!/bin/sh
# Render NuGet.Config from NuGet.Config.template, taking the ABP Commercial feed
# key from a secret FILE rather than a build ARG or an environment variable.
#
# WHY THIS EXISTS (#703)
#   The three backend Dockerfiles used to declare `ARG ABP_NUGET_API_KEY` and
#   substitute it inline. Docker records the value of every declared build
#   argument in the metadata of each layer of that stage, so the key stayed
#   readable via `docker history --no-trunc` on every machine that had ever
#   built these images -- developer workstations and the production host --
#   until the build cache was pruned. The rendered NuGet.Config was also
#   committed to a layer of its own. That is worse than it sounds for this
#   particular key: it is organisation-wide, not scoped to this repository or
#   to one developer, so its blast radius is not this project's.
#
#   A BuildKit secret mount (`RUN --mount=type=secret`) is a tmpfs that exists
#   only for the duration of one RUN and is never written to a layer, and a
#   Compose service secret is a tmpfs inside the running container. Neither
#   appears in image metadata.
#
# THE CALLER'S OBLIGATION
#   Render, restore, and delete must happen in the SAME layer (build) or the
#   SAME process (dev entrypoint). Deleting the file in a later RUN leaves the
#   earlier layer -- and the key inside it -- in the image.
#
# USAGE
#   sh docker/write-nuget-config.sh [SECRET_FILE] [TEMPLATE] [OUTPUT]
#   Defaults: /run/secrets/abp_nuget_key, NuGet.Config.template, NuGet.Config
set -eu

SECRET_FILE="${1:-/run/secrets/abp_nuget_key}"
TEMPLATE="${2:-NuGet.Config.template}"
OUTPUT="${3:-NuGet.Config}"
PLACEHOLDER='${ABP_NUGET_API_KEY}'

die() { echo "write-nuget-config: $*" >&2; exit 1; }

[ -f "$TEMPLATE" ] || die "template not found: $TEMPLATE (run from the repo root)"
# Without this, a template that had lost its placeholder would render a keyless
# feed URL and every Volo.* package would 401 at restore time with nothing here
# to say why.
grep -qF "$PLACEHOLDER" "$TEMPLATE" || die "placeholder $PLACEHOLDER not found in $TEMPLATE"

if [ ! -r "$SECRET_FILE" ]; then
  echo "write-nuget-config: secret not readable: $SECRET_FILE" >&2
  echo "  build time: RUN --mount=type=secret,id=abp_nuget_key ..." >&2
  echo "              compose supplies it via build.secrets; plain docker build" >&2
  echo "              needs --secret id=abp_nuget_key,env=ABP_NUGET_API_KEY" >&2
  echo "  run time:   the service's own secrets: block (dev stages restore on start)" >&2
  die "cannot render $OUTPUT without the ABP feed key"
fi

# tr, not \$(cat), so a trailing newline from `docker secret`/`compose` does not
# end up inside the feed URL.
KEY="$(tr -d '\r\n' < "$SECRET_FILE")"
[ -n "$KEY" ] || die "secret is empty: $SECRET_FILE"

# Substitution is a literal string swap in the shell rather than sed, so a key
# containing a sed delimiter or a regex metacharacter cannot corrupt the output
# or silently produce a feed URL that 401s. `|| [ -n "$line" ]` keeps the final
# line when the template has no trailing newline -- NuGet.Config.template
# currently does not.
umask 077
: > "$OUTPUT"
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    *"$PLACEHOLDER"*)
      printf '%s%s%s\n' "${line%%"$PLACEHOLDER"*}" "$KEY" "${line#*"$PLACEHOLDER"}" ;;
    *)
      printf '%s\n' "$line" ;;
  esac
done < "$TEMPLATE" >> "$OUTPUT"

grep -qF "$PLACEHOLDER" "$OUTPUT" && die "placeholder survived in $OUTPUT"
exit 0
