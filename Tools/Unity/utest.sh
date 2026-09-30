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
  # Import new and changed files first: a recompile alone once ran a new test file's assembly
  # without that file. The refresh may itself start the compile, so its answer may time out.
  run eval --timeout 120 --code 'UnityEditor.AssetDatabase.Refresh(); return "refreshed";' >/dev/null
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
args=(--mode "$mode" --timeout 1500)
[ -n "$filter" ] && args+=(--filter "$filter")
dialog() { powershell -NoProfile -ExecutionPolicy Bypass -File "$here/editor-dialog.ps1" "$@" 2>/dev/null | tr -d '\r'; }

if [ "$mode" = "PlayMode" ]; then
  # The Test Framework replaces the open scenes with its own and first asks, in a dialog nobody
  # can answer from here, whether to save a modified one. So: refuse to start over unsaved work,
  # and clear away a test scene that an interrupted run left open.
  precheck='bool leftover = false; for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) { var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (s.name.StartsWith("InitTestScene")) { leftover = true; } else if (s.isDirty) { return "unsaved changes in the open scene " + (string.IsNullOrEmpty(s.path) ? "(untitled)" : s.path); } } if (leftover) { UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); } return "ok";'
  # The Editor's main thread may still be importing what the recompile changed: ask again.
  for attempt in 1 2 3 4 5; do
    pre=$(run eval --timeout 60 --code "$precheck")
    case "$pre" in *timed\ out*) poke; sleep 5 ;; *) break ;; esac
  done
  case "$pre" in
    *'"result": "ok"'*) ;;
    *) echo "not starting the PlayMode tests: $(echo "$pre" | grep -oE '"result": "[^"]*"|"message": "[^"]*"' | head -2 | tr '\n' ' ')"; exit 6 ;;
  esac

  # Entering Play mode reloads the domain and drops the request, so a PlayMode run is started
  # and then polled. Whatever happens, the Editor is left in Edit mode.
  trap 'run cancel_tests >/dev/null; run editor_stop >/dev/null; kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT
  run run_tests "${args[@]}" --async_tests true > "$out.start"
  finished=0
  blocked=0
  for i in $(seq 1 450); do
    sleep 2
    run test_status > "$out"
    if grep -qE '"status": "(completed|error|cancelled)"' "$out"; then finished=1; break; fi
    if [ $((i % 15)) -eq 0 ]; then
      # A modal dialog stops the Editor dead while the status still says "running".
      d=$(dialog -DismissTestScene)
      if [ -n "$d" ]; then
        echo "the Editor is showing a dialog:"; echo "$d"
        blocked=$((blocked + 1))
        if [ $blocked -ge 2 ]; then echo "the dialog is still there; someone has to answer it in the Editor"; exit 6; fi
      else
        blocked=0
      fi
    fi
  done
  if [ $finished -ne 1 ]; then echo "the PlayMode run did not finish in 15 minutes"; exit 6; fi
  trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT
else
  run run_tests "${args[@]}" > "$out"
fi

node -e '
const fs = require("fs");
const text = fs.readFileSync(process.argv[1], "utf8");
const start = text.indexOf("{");
let data;
try { data = JSON.parse(text.slice(start)); } catch (e) { console.log("no test result:\n" + text.slice(0, 1500)); process.exit(6); }
// A synchronous run answers in PascalCase, a polled (PlayMode) run in camelCase.
const pick = (o, name) => o[name] !== undefined ? o[name] : o[name[0].toLowerCase() + name.slice(1)];
const s = pick(data, "Summary");
if (!s) { console.log("no test summary:\n" + text.slice(0, 1500)); process.exit(6); }
const n = k => pick(s, k);
console.log(`${process.argv[2]}: ${n("Passed")} passed, ${n("Failed")} failed, ${n("Skipped")} skipped, ${n("Inconclusive")} inconclusive of ${n("Total")} (${pick(data, "Duration")}s)`);
const seen = new Set();
for (const r of pick(data, "Results") || []) {
  const status = pick(r, "Status");
  const name = pick(r, "FullName") || pick(r, "Name");
  // A polled run lists every result twice.
  if (status !== "Passed" && !seen.has(name)) {
    seen.add(name);
    console.log(`  ${status}: ${name}`);
    const message = pick(r, "Message");
    if (message) console.log("    " + String(message).split("\n").slice(0, 8).join("\n    "));
  }
}
if (n("Total") === 0) { console.log("no tests matched"); process.exit(6); }
process.exit(n("Failed") > 0 ? 8 : 0);
' "$out" "$mode"
