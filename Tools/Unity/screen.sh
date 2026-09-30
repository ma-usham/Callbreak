#!/usr/bin/env bash
# One screen, end to end in the open Editor: (optionally recompile,) build its prefab from
# ArtSource/Screens/<Name>/layout.json, capture it at 16:9, 21:9 and 4:3 into Temp/Captures, and
# compare the 16:9 capture with the mockup (report in ArtSource/Screens/<Name>/check/).
# Usage: Tools/Unity/screen.sh <Name> [--compile]
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
name="${1:?screen name required}"
flag="${2:-}"
[ -f "ArtSource/Screens/$name/layout.json" ] || { echo "no ArtSource/Screens/$name/layout.json - run: Tools/art job $name"; exit 2; }
mockup=$(node -e "console.log(JSON.parse(require('fs').readFileSync('ArtSource/Screens/$name/layout.json','utf8')).mockup)")
"$here/ueval.sh" $flag "Callbreak.Editor.Screens.ScreenLayoutBuilder.Build(\"$name\"); Callbreak.Editor.ScreenCaptureTool.CaptureScreen(\"$name\"); return \"built and captured $name\";" || exit $?
[ -f "Temp/Captures/${name}_1920x1080.png" ] || { echo "no capture written"; exit 4; }
"$repo/Tools/art" compare --screen "$mockup" --capture "Temp/Captures/${name}_1920x1080.png" --name "$name" | head -80
