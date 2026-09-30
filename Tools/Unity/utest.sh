#!/usr/bin/env bash
# Runs Unity tests in the open Editor, holding the shared Editor lock (other agents wait; PlayMode
# tests put the Editor in Play mode for the whole run). Usage:
#   Tools/Unity/utest.sh <EditMode|PlayMode> [name-filter] [--no-compile]
# It compile-checks and recompiles first (unless --no-compile), then prints the summary and every
# test that did not pass with its message.
# Exit code 0 = all passed; 8 = tests failed; 6 = the run broke (compile error, timeout, no result).
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
mode="${1:?EditMode or PlayMode}"
filter=""
compile=1
shift
for a in "$@"; do
  if [ "$a" = "--no-compile" ]; then compile=0; else filter="$a"; fi
done

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 6; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) utest $mode" > "$lock/owner"
# A long run must not look like a dead lock to the others.
( while [ -d "$lock" ]; do touch "$lock" 2>/dev/null; sleep 60; done ) &
keeper=$!
trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT

run() { $U "$@" 2>&1; }
poke() { powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1; }

if [ $compile -eq 1 ]; then
  if ! node "$here/compile-check.js" --quiet; then echo "compile check failed"; exit 6; fi
  r=$(run recompile)
  case "$r" in *timed\ out*) poke; r=$(run recompile) ;; esac
  for i in $(seq 1 90); do
    s=$(run recompile_status | tr -d '\r\n')
    case "$s" in
      *'"status": "completed"'*|*up_to_date*) break ;;
      *'"failed": true'*) echo "Editor compile failed: $s" | cut -c1-2000; exit 6 ;;
      *timed\ out*) poke ;;
    esac
    sleep 2
  done
fi

out="$repo/Temp/utest-$mode.json"
if [ -n "$filter" ]; then
  run run_tests --mode "$mode" --filter "$filter" --timeout 1500 > "$out"
else
  run run_tests --mode "$mode" --timeout 1500 > "$out"
fi

node -e '
const fs = require("fs");
const text = fs.readFileSync(process.argv[1], "utf8");
const start = text.indexOf("{");
let data;
try { data = JSON.parse(text.slice(start)); } catch (e) { console.log("no test result:\n" + text.slice(0, 1500)); process.exit(6); }
const s = data.Summary;
if (!s) { console.log("no test summary:\n" + text.slice(0, 1500)); process.exit(6); }
console.log(`${data.Mode}: ${s.Passed} passed, ${s.Failed} failed, ${s.Skipped} skipped, ${s.Inconclusive} inconclusive of ${s.Total} (${data.Duration}s)`);
for (const r of data.Results || []) {
  if (r.Status !== "Passed") {
    console.log(`  ${r.Status}: ${r.FullName}`);
    if (r.Message) console.log("    " + String(r.Message).split("\n").slice(0, 6).join("\n    "));
  }
}
if (s.Total === 0) { console.log("no tests matched"); process.exit(6); }
process.exit(s.Failed > 0 ? 8 : 0);
' "$out"
