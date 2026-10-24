#!/bin/bash
# Downloads the CC0 ground textures of the climates from Poly Haven and converts them to the PNGs of
# game/assets/textures/terrain (docs/assets_registry.md). macOS: needs curl and sips.
# Usage: fetch.sh <download_dir> <out_dir> [name...]   (for example: fetch.sh /tmp/polyhaven game/assets/textures/terrain)
# With names, only those files of the game are fetched (for example: grass_meadow sand_dunes).
set -eu
DL="$1"
OUT="$2"
shift 2
mkdir -p "$DL" "$OUT"

# polyhaven id | file in the game
LIST='snow_02|snow_powder
asphalt_snow|asphalt_snow
snow_03|dirt_snow
sparse_grass|sparse_grass
coast_sand_03|coast_sand
rock_ground|rock_ground
aerial_grass_rock|grass_meadow
grass_ground|grass_field
dirt_aerial_02|dirt_brown
asphalt_02|asphalt
aerial_sand|sand_dunes
mud_cracked_dry_03|clay_cracked
gravelly_sand|sand_gravel
red_mud_stones|rock_red
aerial_beach_01|sand_beach
concrete_pavement|concrete_slabs
concrete_pavers|concrete_pavers'

echo "$LIST" | while IFS='|' read -r id name; do
  if [ "$#" -gt 0 ] && ! printf '%s\n' "$@" | grep -qx "$name"; then
    continue
  fi

  url="https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/$id/${id}_diff_1k.jpg"
  curl -fsSL "$url" -o "$DL/$id.jpg"
  sips -s format png -z 1024 1024 "$DL/$id.jpg" --out "$OUT/$name.png" >/dev/null
  echo "$name.png <- $url"
done
