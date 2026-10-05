#!/usr/bin/env bash
# The No Ads glyph (main menu, 5 Oct 2026) from ChatGPT's original to the game's sprite, and its export copy.
# Re-running it reproduces both files. Prompts and verdicts: ../chatgpt/NoAds_prompts.md.
#   ring  55.5 mockup px across, as on ChatGPT's main menu with the button (../chatgpt/NoAds_MainMenu_a1.png)
#   ink   #0E0B0C, the gear's own (UI/Icons/Icon_Settings.png), so the two read alike side by side
set -eu
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
cd "$repo"
dotnet run --file "$here/noads.cs" -- make --src ArtSource/Working/MainMenu/chatgpt/Icon_NoAds_a1.png \
  --out Assets/_Project/Art/UI/Icons/Icon_NoAds.png --diameter 55.5 --ink "#0E0B0C"
mkdir -p ArtSource/Exports/Icons
cp Assets/_Project/Art/UI/Icons/Icon_NoAds.png ArtSource/Exports/Icons/Icon_NoAds.png
