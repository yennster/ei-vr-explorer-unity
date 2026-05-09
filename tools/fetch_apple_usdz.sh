#!/usr/bin/env bash
# Download a curated set of free .usdz models from Apple's AR Quick Look gallery
# (https://developer.apple.com/augmented-reality/quick-look/) into ./Assets/Models/USDZ/
# for use as props in the object-detection demo scene.
#
# Apple lets developers use these for evaluation and prototyping — see the
# gallery page for the licensing fine print before using in shipped products.
#
# After download, you'll need to convert each .usdz to a Unity-importable
# format (.fbx or .glb). Two reliable paths on macOS:
#   - Blender (free): File → Import → Universal Scene Description (.usd)
#     then File → Export → glTF 2.0
#   - Pixar usdtools / Reality Composer: Open the .usdz, export as USDC and
#     re-package, or use `usdcat` to extract the asset graph.
#
# Set DEST to override the output directory.

set -euo pipefail

DEST="${DEST:-$(cd "$(dirname "$0")/.." && pwd)/Assets/Models/USDZ}"
mkdir -p "$DEST"

declare -a URLS=(
  "https://developer.apple.com/augmented-reality/quick-look/models/baseball-glove/glove_baseball_mtl_variant.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/seahorse/seahorse_anim_mtl_variant.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/chameleon/chameleon_anim_mtl_variant.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/pancakes/pancakes_photogrammetry.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/hummingbird/hummingbird_anim.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/biplane/toy_biplane_realistic.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/drummertoy/toy_drummer.usdz"
  "https://developer.apple.com/augmented-reality/quick-look/models/stratocaster/fender_stratocaster.usdz"
)

echo "Downloading ${#URLS[@]} .usdz models to $DEST"
for url in "${URLS[@]}"; do
  name=$(basename "$url")
  if [ -f "$DEST/$name" ]; then
    echo "  • $name (already present, skipping)"
    continue
  fi
  echo "  • $name"
  curl -fsSL --retry 3 -o "$DEST/$name.tmp" "$url" && mv "$DEST/$name.tmp" "$DEST/$name"
done

echo
echo "Done. Next: convert each .usdz to .fbx or .glb so Unity can import it."
echo "Blender example (CLI):"
echo "  blender --background --python-expr \\"
echo "    \"import bpy; bpy.ops.wm.usd_import(filepath='$DEST/toy_drummer.usdz'); bpy.ops.export_scene.gltf(filepath='$DEST/toy_drummer.glb')\""
