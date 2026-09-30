#!/usr/bin/env bash
# Recompiles in the open Editor, rebuilds the Main menu prefab and scene, captures it at three
# aspect ratios and prints any console errors. Usage: unity-rebuild.sh [--no-compile]
set -u
cd "$(dirname "$0")/../.."
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"

if [ "${1:-}" != "--no-compile" ]; then
  $U recompile >/dev/null 2>&1
  for i in $(seq 1 60); do
    s=$($U recompile_status 2>/dev/null | tr -d '\r')
    case "$s" in *completed*|*up_to_date*) break ;; esac
    sleep 2
  done
  echo "recompile: $s" | cut -c1-200
fi

$U eval --timeout 180 --code 'Callbreak.Editor.MainMenuBuilder.Build(); Callbreak.Editor.ScreenCaptureTool.CaptureMainMenu(); return "built and captured";' 2>&1 | cut -c1-400
$U console_status 2>&1 | grep -o '"counts":{[^}]*}' | head -1
$U console --level error --tail 5 2>&1 | grep -o '"message":"[^"]*"' | head -5
ls -la Temp/Captures/MainMenu_1920x1080.png | awk '{print $6, $7, $8, $5}'
