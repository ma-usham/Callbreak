#!/usr/bin/env bash
# Exports the Android player as a Gradle project (Temp/AndroidExport) in the open Editor and prints
# the build report: every runtime script compiled for Android, the scenes built with their assets,
# the build hooks run. The Editor is busy for the whole build, so this holds the shared Editor lock
# and other agents wait. Usage: Tools/Unity/android-export.sh
# Exit code 0 = the build succeeded; 6 = it failed or did not finish in 40 minutes.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
report="$repo/Temp/AndroidExport-report.txt"

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 6; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) android-export" > "$lock/owner"
# A long build must not look like a dead lock to the others.
( while [ -d "$lock" ]; do touch "$lock" 2>/dev/null; sleep 60; done ) &
keeper=$!
trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT

if ! node "$here/compile-check.js" --quiet --android; then echo "compile check (Android) failed"; exit 6; fi
rm -f "$report"
$U eval --timeout 60 --code 'return Callbreak.Editor.BuildCheck.ScheduleExport();' 2>&1 | grep -E '"result"|"message"' | head -2
deadline=$(( $(date +%s) + 2400 ))
while [ ! -f "$report" ]; do
  if [ $(date +%s) -gt $deadline ]; then echo "the export did not finish in 40 minutes"; exit 6; fi
  sleep 10
done
cat "$report"
echo
# The invite-link filter sits on the game activity, in the unityLibrary module's manifest.
manifest=$(grep -l 'android.intent.action.VIEW' $(find "$repo/Temp/AndroidExport" -name AndroidManifest.xml 2>/dev/null) 2>/dev/null | head -1)
if [ -n "$manifest" ]; then echo "invite-link filter in ${manifest#$repo/}:"; grep -n -B1 -A4 'android.intent.action.VIEW' "$manifest" | head -10; else echo "no manifest has an invite-link (VIEW) filter"; fi
grep -q '^Succeeded' "$report" && exit 0
exit 6
