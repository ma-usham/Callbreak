#!/usr/bin/env bash
# Runs C# in the open Editor through the Unity CLI, holding the shared Editor lock, retrying once
# after activating the Editor window if it stopped answering. Usage:
#   Tools/Unity/ueval.sh [--compile] 'C# statements; return "something";'
# --compile runs the per-assembly compile check first, then asks the Editor to recompile and
# waits for it. Output: the eval result, then any new console errors.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
compile=0
if [ "${1:-}" = "--compile" ]; then compile=1; shift; fi
code="${1:?C# code required}"

# --- the shared Editor lock (other agents drive the same Editor) -----------------------------
lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  # A lock older than 15 minutes belongs to a run that died.
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 5; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S)" > "$lock/owner"
trap 'rm -rf "$lock"' EXIT

poke() { powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1; }

run() { $U "$@" 2>&1; }

if [ $compile -eq 1 ]; then
  if ! node "$here/compile-check.js" --quiet; then echo "compile check failed: fix the errors above before the Editor sees them"; exit 3; fi
  r=$(run recompile)
  case "$r" in *timed\ out*) poke; r=$(run recompile) ;; esac
  for i in $(seq 1 90); do
    s=$(run recompile_status | tr -d '\r\n')
    case "$s" in
      *'"status": "completed"'*|*up_to_date*) break ;;
      *'"failed": true'*) echo "Editor compile failed: $s" | cut -c1-2000; exit 3 ;;
      *timed\ out*) poke ;;
    esac
    sleep 2
  done
fi

before=$(run console_status | grep -o '"error": [0-9]*' | head -1 | grep -o '[0-9]*')
out=$(run eval --timeout 600 --code "$code")
case "$out" in
  *timed\ out*|*COMMAND_FAILED*)
    poke
    out=$(run eval --timeout 600 --code "$code") ;;
esac
echo "$out" | grep -E '"result"|"success"|error|Exception' | cut -c1-3000
after=$(run console_status | grep -o '"error": [0-9]*' | head -1 | grep -o '[0-9]*')
if [ -n "$before" ] && [ -n "$after" ] && [ "$after" -gt "$before" ]; then
  echo "new console errors:"
  run console --level error --tail $((after - before)) | grep -oE '"message":\s*"[^"]{0,400}' | head -20
fi
