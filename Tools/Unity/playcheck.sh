#!/usr/bin/env bash
# Runs the game in the open Editor for a moment and reports what happened. Holds the shared Editor
# lock for the whole run (other agents wait), enters Play mode (which starts from the Boot scene),
# then for each step either waits, evaluates C# in the running game, or captures the Game view,
# and always leaves Play mode again. Usage:
#   Tools/Unity/playcheck.sh <step> [<step> ...]
# Steps:
#   wait:<seconds>          let the game run
#   eval:<C# statements>    run in the Editor while playing; must `return` a string
#   shot:<path.png>         capture the Game view, overlay UI included (path relative to the repo)
#   log:<n>                 print the last n console entries of any level
# Output: each step's result, then every console error and exception logged during the run.
# Exit code 0 = ran to the end without console errors; 4 = console errors; 3 = could not play.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
[ $# -gt 0 ] || { echo "usage: playcheck.sh <wait:N|eval:code|shot:path> ..."; exit 2; }

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 5; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) playcheck" > "$lock/owner"

run() { $U "$@" 2>&1; }
mode() { run editor_status | grep -o '"playMode": "[a-z]*"' | grep -o '[a-z]*"$' | tr -d '"'; }
background=""
stop() {
  if [ "$(mode)" != "stopped" ]; then
    # Put the project's Run In Background setting back the way it was.
    [ "$background" = "0" ] && run eval --timeout 30 --code 'UnityEngine.Application.runInBackground = false; return "restored";' >/dev/null
    run editor_stop >/dev/null
    for i in $(seq 1 60); do [ "$(mode)" = "stopped" ] && break; sleep 1; done
  fi
  rm -rf "$lock"
}
trap stop EXIT

if ! node "$here/compile-check.js" --quiet; then echo "compile check failed"; exit 3; fi
run clear_console >/dev/null
r=$(run editor_play)
case "$r" in *timed\ out*) powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1; r=$(run editor_play) ;; esac
playing=0
deadline=$(( $(date +%s) + 90 ))
while [ $(date +%s) -lt $deadline ]; do
  if [ "$(mode)" = "playing" ]; then playing=1; break; fi
  sleep 1
done
if [ $playing -ne 1 ]; then
  echo "the Editor did not enter Play mode: $r" | cut -c1-600
  # The usual reason: a script that does not compile (the Editor refuses to play until it does).
  run recompile_status | tr -d '\r\n' | cut -c1-1200
  echo
  exit 3
fi
# Without this the game stands still on frame 1 whenever the Editor is not the foreground window.
background=$(run eval --timeout 30 --code 'bool was = UnityEngine.Application.runInBackground; UnityEngine.Application.runInBackground = true; return was ? "1" : "0";' | grep -o '"result": "[01]"' | grep -o '[01]')

for step in "$@"; do
  kind="${step%%:*}"; arg="${step#*:}"
  case "$kind" in
    wait) sleep "$arg" ;;
    eval) echo "eval> $(run eval --timeout 60 --code "$arg" | grep -E '"result"|"success": false|error|Exception' | cut -c1-3000)" ;;
    shot) mkdir -p "$(dirname "$arg")"; run eval --timeout 60 --code "UnityEngine.ScreenCapture.CaptureScreenshot(\"$arg\"); return \"queued\";" >/dev/null; sleep 1.5; echo "shot> $arg $([ -f "$arg" ] && echo written || echo MISSING)" ;;
    log) echo "log>"; run console --tail "$arg" | tr -d '\r' | grep -E '"(message|level|type|condition|text)"' | cut -c1-500 ;;
    *) echo "unknown step: $step" ;;
  esac
  touch "$lock" 2>/dev/null
done

errors=$(run console_status | grep -o '"error": [0-9]*' | head -1 | grep -o '[0-9]*')
status=0
if [ -n "$errors" ] && [ "$errors" -gt 0 ]; then
  echo "console errors during the run ($errors):"
  run console --level error --tail 30 | grep -oE '"message":\s*"[^"]{0,700}' | head -30
  status=4
fi
exit $status
