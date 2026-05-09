#!/usr/bin/env bash
# Download a curated set of free .glb (binary glTF 2.0) models from the
# official Khronos glTF Sample Models repo into ./Assets/Models/glTF/
# for use as props in the object-detection demo scene.
#
# All 8 models are CC0 / Public Domain or CC-BY 4.0 — see
# https://github.com/KhronosGroup/glTF-Sample-Models for the per-model
# license file. Safe to ship in a portfolio demo with attribution.
#
# Unlike the USDZ path, .glb imports natively into Unity 6 once
# `com.unity.cloud.gltfast` is in the package manifest (it is — see
# Packages/manifest.json). Drop the resulting prefabs into the
# DemoSceneSpawner.propPrefabs array.
#
# Set DEST to override the output directory.

set -euo pipefail

DEST="${DEST:-$(cd "$(dirname "$0")/.." && pwd)/Assets/Models/glTF}"
mkdir -p "$DEST"

BASE="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0"

declare -a MODELS=(
  "Duck/glTF-Binary/Duck.glb"
  "BoomBox/glTF-Binary/BoomBox.glb"
  "DamagedHelmet/glTF-Binary/DamagedHelmet.glb"
  "Avocado/glTF-Binary/Avocado.glb"
  "Lantern/glTF-Binary/Lantern.glb"
  "WaterBottle/glTF-Binary/WaterBottle.glb"
  "AntiqueCamera/glTF-Binary/AntiqueCamera.glb"
  "Corset/glTF-Binary/Corset.glb"
)

echo "Downloading ${#MODELS[@]} .glb models to $DEST"
for m in "${MODELS[@]}"; do
  name=$(basename "$m")
  if [ -f "$DEST/$name" ]; then
    echo "  • $name (already present, skipping)"
    continue
  fi
  echo "  • $name"
  curl -fsSL --retry 3 -o "$DEST/$name.tmp" "$BASE/$m" && mv "$DEST/$name.tmp" "$DEST/$name"
done

echo
echo "Done. In Unity:"
echo "  1. Wait for Editor to import the new files (com.unity.cloud.gltfast"
echo "     converts each .glb into a Prefab automatically)."
echo "  2. Drag the auto-generated prefabs into DemoSceneSpawner → propPrefabs"
echo "     on your ObjectDetection scene."
