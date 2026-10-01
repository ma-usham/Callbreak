#!/usr/bin/env bash
# Runs the table simulation (Callbreak.Editor.TableSimulation.Run) in the open Editor, holding the
# shared Editor lock, and prints the report's summary line and its failures. The CLI often stops
# waiting on the Editor's main thread while the run goes on, so unlike ueval.sh this never asks a
# second time: it waits for the report file to change instead. Usage:
#   Tools/Unity/usim.sh [--reduce] [--captures]
# --reduce runs it with Reduce motion (report-reduced.txt); --captures also writes the pictures
# (Temp/Captures/Sim_*.png, about 3 minutes instead of seconds).
# Exit code 0 = no check failed; 8 = some failed; 6 = no report (the run did not finish).
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
reduce=false
captures=false
for a in "$@"; do
  case "$a" in
    --reduce) reduce=true ;;
    --captures) captures=true ;;
  esac
done
report="Temp/TableSimulation/report.txt"
[ "$reduce" = true ] && report="Temp/TableSimulation/report-reduced.txt"

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 6; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) usim" > "$lock/owner"
( while [ -d "$lock" ]; do touch "$lock" 2>/dev/null; sleep 60; done ) &
keeper=$!
trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT

U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
waited_play=0
while true; do
  pm=$($U editor_status 2>&1 | grep -o '"playMode": "[a-z]*"' | sed -E 's/.*: "([a-z]*)"/\1/')
  if [ -z "$pm" ] || [ "$pm" = "stopped" ]; then break; fi
  if [ $waited_play -ge 900 ]; then echo "the Editor has been in Play mode for 15 minutes; not running"; exit 6; fi
  [ $((waited_play % 60)) -eq 0 ] && echo "waiting: the Editor is in Play mode"
  sleep 5; waited_play=$((waited_play + 5))
done

# A background Editor does not tick: bring it forward so the run is not starved.
powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1
before=$(stat -c %Y "$report" 2>/dev/null || echo 0)
$U eval --timeout 900 --code "return Callbreak.Editor.TableSimulation.Run($reduce, $captures).Split((char)10)[0];" >/dev/null 2>&1
for i in $(seq 1 300); do
  now=$(stat -c %Y "$report" 2>/dev/null || echo 0)
  [ "$now" != "$before" ] && break
  sleep 3
done

if [ ! -f "$report" ] || [ "$(stat -c %Y "$report")" = "$before" ]; then echo "no new report in $report"; exit 6; fi
head -1 "$report"
grep -E "^FAIL" "$report" | sort -u | cut -c1-400
grep -qE "^FAIL" "$report" && exit 8
exit 0
