#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
TEMPLATE="$REPO_ROOT/NuGet.Config.template"
OUTPUT="$REPO_ROOT/NuGet.Config"

if [[ ! -f "$TEMPLATE" ]]; then
  echo "Error: NuGet.Config.template not found at $TEMPLATE" >&2
  exit 1
fi

if [[ -f "$OUTPUT" ]]; then
  echo "NuGet.Config already exists. Overwrite? (y/N)"
  read -r answer
  if [[ "$answer" != "y" && "$answer" != "Y" ]]; then
    echo "Aborted."
    exit 0
  fi
fi

if [[ -n "$ABP_NUGET_API_KEY" ]]; then
  echo "Using ABP_NUGET_API_KEY from environment variable."
  KEY="$ABP_NUGET_API_KEY"
else
  echo "Enter your ABP Commercial NuGet API Key (GUID):"
  read -r KEY
fi

if [[ -z "$KEY" ]]; then
  echo "Error: API key cannot be empty." >&2
  exit 1
fi

sed "s/\${ABP_NUGET_API_KEY}/$KEY/" "$TEMPLATE" > "$OUTPUT"
echo "NuGet.Config created at $OUTPUT"
echo "DO NOT commit this file -- it is gitignored."
