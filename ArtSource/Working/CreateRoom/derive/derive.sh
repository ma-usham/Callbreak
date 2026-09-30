#!/usr/bin/env bash
# Placeholder control sprites for screens 02-04, derived from the mockups, that arttool's own cut
# could not produce: controls whose text must be erased from flat art, and the segmented-control
# track, whose right half is hidden under the selected pill on the mockup. The generated Controls
# kit has replaced them on the screens (ArtSource/Kit/Controls.map.csv); the placeholders stay on
# disk. Still in use: the room-code pill's soft shadow (screen 04), solved here from the mockup.
#   Usage: ArtSource/Working/CreateRoom/derive/derive.sh [CreateRoom|JoinRoom|PrivateLobby|all]
#          ArtSource/Working/CreateRoom/derive/derive.sh tool <command> [options]   (run the helper directly)
# The sprites are written to Assets/_Project/Art/Screens/<Name>/ (and ArtSource/Exports/Screens/<Name>/,
# with their rows in ArtSource/handoff.csv) and are referenced from the screens' screen.json with
# "use". Re-run after changing a rect below, then `Tools/art job <Name>` and `Tools/Unity/screen.sh <Name>`.
#
# The helper (tool/Program.cs, built in a temp folder against arttool's image code) takes mockup pixels:
#   mirrorpill --screen NN --rect x,y,w,h [--radius r] [--erase "x,y,w,h[,mode];..."] [--nomirror] --scale 2 --out file
#     the shape (a pill, or a roundrect when --radius is given) fills the image exactly; each erase rect
#     is filled per row from the column left and right of it (mode 1: copy the left column, 2: the
#     right one); without --nomirror the right half is the left half mirrored; edge pixels take the
#     colour 1 px inside the edge.
#   softshadow --screen NN --rect x,y,w,h [--radius r] --pad p --scale 2 --out file
#     the control's soft drop shadow solved against the mockup's own surroundings; the image is the
#     rect grown by p on every side (place it with "shadowUse").
#   fiticon --screen NN --sprite file --rect x,y,w,h [--ink light] [--mode diff] [--thr 0.5] [--stretch]
#     the rect at which a shared icon sprite sits on this mockup's own glyph without changing its aspect
#     (--stretch also prints the rect that maps glyph box onto glyph box, for the title divider).
#   fitlogo --screen NN --sprite file --srows a,b --region x,y,w,h
#     the same for the logo, by its crimson wordmark.
#   pairs --screen NN --capture file --rects "x,y,w,h;..." [--scale 3] --out file
#     a comparison sheet: each mockup area above the same area of a 1920 x 1080 capture.
#   circle / outline / dump / rgb / alpha: the other measuring helpers used for the rects in screen.json.
set -eu
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
what="${1:-all}"
build="${TEMP:-/tmp}/callbreak-derive"
mkdir -p "$build"
cp "$here/tool/Program.cs" "$build/Program.cs"
art="$(cygpath -w "$repo/Tools/ArtTool" 2>/dev/null || echo "$repo/Tools/ArtTool")"
cat > "$build/derive.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>derive</AssemblyName>
    <RootNamespace>ArtTool</RootNamespace>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Optimize>true</Optimize>
    <NoWarn>\$(NoWarn);CA1416</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$art\\Img.cs" Link="Img.cs" />
    <Compile Include="$art\\Inpaint.cs" Link="Inpaint.cs" />
    <Compile Include="$art\\Shapes.cs" Link="Shapes.cs" />
    <Compile Include="$art\\Matte.cs" Link="Matte.cs" />
  </ItemGroup>
</Project>
EOF
dotnet build "$build/derive.csproj" -c Release -nologo -v q >/dev/null || { echo "derive tool build failed" >&2; exit 1; }
tool() { dotnet "$build/bin/Release/net10.0-windows/derive.dll" "$@"; }
if [ "$what" = "tool" ]; then shift; cd "$repo"; tool "$@"; exit $?; fi

# sprite <Screen> <Sprite>: the Assets path (Windows form).
sprite() { echo "$(cygpath -w "$repo/Assets/_Project/Art/Screens/$1" 2>/dev/null || echo "$repo/Assets/_Project/Art/Screens/$1")\\$2.png"; }

# publish <Screen> <Sprite> <mockup number> <notes>: copy to Exports and write the handoff.csv row
# (same columns as arttool's: file,type,size_px,nine_slice_ltrb,used_on,prompt,status,notes).
publish() {
  mkdir -p "$repo/ArtSource/Exports/Screens/$1"
  cp "$repo/Assets/_Project/Art/Screens/$1/$2.png" "$repo/ArtSource/Exports/Screens/$1/$2.png"
  node - "$repo" "Screens/$1/$2.png" "$3" "$4" <<'JS'
const fs = require("fs"), path = require("path");
const [repo, file, usedOn, notes] = process.argv.slice(2);
const png = fs.readFileSync(path.join(repo, "Assets/_Project/Art", file));
const size = png.readUInt32BE(16) + "x" + png.readUInt32BE(20);
const csv = path.join(repo, "ArtSource/handoff.csv");
const raw = fs.readFileSync(csv, "utf8");
const bom = raw.charCodeAt(0) === 0xFEFF ? "﻿" : "";
const eol = raw.includes("\r\n") ? "\r\n" : "\n";
const lines = raw.slice(bom.length).split(/\r?\n/);
while (lines.length && lines[lines.length - 1] === "") lines.pop();
const row = [file, "ui", size, "", usedOn, "derived from mockup", "review", notes.replace(/,/g, ";")].join(",");
const at = lines.findIndex(l => l.startsWith(file + ","));
if (at >= 0) lines[at] = row; else lines.push(row);
fs.writeFileSync(csv, bom + lines.join(eol) + eol);
JS
}

if [ "$what" = "CreateRoom" ] || [ "$what" = "all" ]; then
  # Segmented-control track: left half ("15 sec" erased; the selected pill's first pixels near the
  # centre replaced by the column left of them) mirrored onto the right half.
  tool mirrorpill --screen 02 --rect 602.1,634.4,469.05,59.6 --erase "684,650,82,28;830,628,13,72,1" --scale 2 --out "$(sprite CreateRoom UI_SegmentTrack)"
  # The crimson selected pill with its light halo (1.5 px), "30 sec" erased.
  tool mirrorpill --screen 02 --rect 832.8,638.0,234.25,52.15 --erase "900,645,104,37" --nomirror --scale 2 --out "$(sprite CreateRoom UI_SegmentSelected)"
  # The room-name field with its outer highlight (about 1 px), "Friends' Table" erased.
  tool mirrorpill --screen 02 --rect 562.0,398.9,548.8,63.2 --radius 14.3 --erase "575,408,195,46" --nomirror --scale 2 --out "$(sprite CreateRoom UI_Field_RoomName)"
  publish CreateRoom UI_SegmentTrack 02 "placeholder: segmented-control track; left half mirrored; label erased"
  publish CreateRoom UI_SegmentSelected 02 "placeholder: selected segment pill; label erased"
  publish CreateRoom UI_Field_RoomName 02 "placeholder: text field; text erased"
fi

if [ "$what" = "JoinRoom" ] || [ "$what" = "all" ]; then
  # One code box for all six: the second box with its "B" erased.
  tool mirrorpill --screen 03 --rect 594.39,482.5,108.1,109.0 --radius 13 --erase "626,509,47,59" --nomirror --scale 2 --out "$(sprite JoinRoom UI_CodeBox)"
  publish JoinRoom UI_CodeBox 03 "placeholder: code box (box 2; letter erased); used for all six"
fi

if [ "$what" = "PrivateLobby" ] || [ "$what" = "all" ]; then
  # The room-code pill with its caption, code, hairline and copy glyph erased.
  tool mirrorpill --screen 04 --rect 490.33,282.32,375.9,77.2 --radius 21 --erase "516,300,326,42" --nomirror --scale 2 --out "$(sprite PrivateLobby UI_Pill_RoomCode)"
  # Its soft shadow, solved against this mockup (the shared panel is a little lighter than this
  # mockup, so a shadow solved against the panel would carry a faint box around the pill).
  # It is solved for the kit pill's own outline (Controls.map.csv: its rect and corner radius 27), which
  # the screen places with "shadowSpread": 10.
  tool softshadow --screen 04 --rect 490.13,281.83,376.15,77.55 --radius 27 --pad 10 --scale 2 --out "$(sprite PrivateLobby UI_Pill_RoomCode_Shadow)"
  publish PrivateLobby UI_Pill_RoomCode 04 "placeholder: room-code pill; contents erased (replaced by UI/Controls/UI_Pill_RoomCode.png)"
  publish PrivateLobby UI_Pill_RoomCode_Shadow 04 "the kit room-code pill's soft shadow; solved from the mockup; pad 10 px"
fi
