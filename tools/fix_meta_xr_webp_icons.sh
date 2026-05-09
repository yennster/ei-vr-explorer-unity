#!/usr/bin/env bash
# Meta XR SDK Core occasionally ships WebP files with a .png extension
# inside Editor/BuildingBlocks/Icons/. Unity's PNG importer chokes on them
# and the build fails with:
#
#   Could not create asset from Packages/com.meta.xr.sdk.core/Editor/BuildingBlocks/Icons/ovr_icon_break_bb_connection.png:
#   File could not be read
#
# This script walks Library/PackageCache for any com.meta.xr.sdk.core
# entries, finds .png files that are actually WebP, and converts them in
# place using macOS's `sips`. Idempotent — files already in real PNG
# format are skipped.
#
# Run from the unity-app/ directory after Unity reimports the package
# (e.g. after a fresh clone or a Library/ reset).

set -euo pipefail

if ! command -v sips >/dev/null 2>&1; then
  echo "sips not found — this script is macOS-only." >&2
  exit 1
fi

ROOT="${1:-Library/PackageCache}"
if [ ! -d "$ROOT" ]; then
  echo "$ROOT not found. cd into unity-app/ first or pass the path explicitly." >&2
  exit 1
fi

converted=0
checked=0
while IFS= read -r f; do
  checked=$((checked+1))
  if file "$f" | grep -q "Web/P\|RIFF"; then
    echo "Converting: $f"
    if sips -s format png "$f" --out "$f.tmp" >/dev/null 2>&1; then
      mv "$f.tmp" "$f"
      converted=$((converted+1))
    else
      echo "  ↑ failed" >&2
      rm -f "$f.tmp"
    fi
  fi
done < <(find "$ROOT" -path "*com.meta.xr.sdk*" -name "*.png" 2>/dev/null)

echo
echo "Checked $checked PNG files in Meta XR. Converted $converted WebP→PNG."
