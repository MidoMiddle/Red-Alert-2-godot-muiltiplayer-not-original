#!/bin/bash
# Downloads the CC0 ground textures of the winter climate from Poly Haven and converts them to the PNGs of
# game/assets/textures/terrain (docs/assets_registry.md). macOS: needs curl and sips.
# Usage: fetch.sh <download_dir> <out_dir>   (for example: fetch.sh /tmp/polyhaven game/assets/textures/terrain)
set -eu
DL="$1"
OUT="$2"
mkdir -p "$DL" "$OUT"

# polyhaven id | file in the game
LIST='snow_02|snow_powder
asphalt_snow|asphalt_snow
snow_03|dirt_snow
sparse_grass|sparse_grass
coast_sand_03|coast_sand
rock_ground|rock_ground'

echo "$LIST" | while IFS='|' read -r id name; do
  url="https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/$id/${id}_diff_1k.jpg"
  curl -fsSL "$url" -o "$DL/$id.jpg"
  sips -s format png -z 1024 1024 "$DL/$id.jpg" --out "$OUT/$name.png" >/dev/null
  echo "$name.png <- $url"
done
