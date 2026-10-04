#!/usr/bin/env bash
# Builds a Development APK, Builds/Android/Callbreak.apk (Google's test ads), and checks the AdMob
# app id in its merged manifest. The open Editor exports a development Gradle project
# (Callbreak.Editor.AndroidApkBuild, into Temp/AndroidApk); Unity's own Gradle and JDK then build it
# with `assembleDebug`, signed with the Android debug key. The project's keystore and Player
# Settings are left alone. The whole build holds the shared Editor lock (the Gradle half compiles
# IL2CPP's C++ and needs the machine's memory), so other agents wait.
# Usage: Tools/Unity/android-apk.sh
# Exit code 0 = the APK was built and its manifest has the AdMob app id; 6 = anything else.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
U="unity command --caller plugin --skill unity-cli --no-banner --result-only"
report="$repo/Temp/AndroidApk-report.txt"
project="$repo/Temp/AndroidApk"
apk="$repo/Builds/Android/Callbreak.apk"
unity_data="${UNITY_DATA:-D:/Applications/UnityEditor/6000.3.18f1/Editor/Data}"
android="$unity_data/PlaybackEngines/AndroidPlayer"
app_id="ca-app-pub-1025159441369051~3176202879"

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 6; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) android-apk" > "$lock/owner"
# A long build must not look like a dead lock to the others.
( while [ -d "$lock" ]; do touch "$lock" 2>/dev/null; sleep 60; done ) &
keeper=$!
trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT

if ! node "$here/compile-check.js" --quiet --android; then echo "compile check (Android) failed"; exit 6; fi
rm -f "$report"
# A background Editor may not tick, and the export starts on its next tick.
powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1
$U eval --timeout 60 --code 'return Callbreak.Editor.AndroidApkBuild.ScheduleExport();' 2>&1 | grep -E '"result"|"message"' | head -2
deadline=$(( $(date +%s) + 2400 ))
while [ ! -f "$report" ]; do
  if [ $(date +%s) -gt $deadline ]; then echo "the export did not finish in 40 minutes"; exit 6; fi
  sleep 10
done
cat "$report"
echo
grep -q '^Succeeded' "$report" || exit 6

# The debug variant signs with the Android debug key (AGP's own debug signing config), whatever
# keystore the exported project names.
echo "android.buildTypes.debug.signingConfig = android.signingConfigs.debug" >> "$project/launcher/build.gradle"
echo "gradle assembleDebug ($(date +%H:%M:%S))"
gradle_log="$repo/Temp/AndroidApk-gradle.log"
JAVA_HOME="$android/OpenJDK" "$android/OpenJDK/bin/java.exe" -Xmx64m \
  -cp "$android/Tools/gradle/lib/gradle-launcher-9.1.0.jar" org.gradle.launcher.GradleMain \
  --no-daemon --console=plain -p "$project" assembleDebug > "$gradle_log" 2>&1
gradle_exit=$?
grep -E "BUILD (SUCCESSFUL|FAILED)|What went wrong|^> |error:" "$gradle_log" | grep -v "^> Task" | head -30
if [ $gradle_exit -ne 0 ]; then echo "Gradle failed (exit $gradle_exit); the log is $gradle_log"; exit 6; fi

built="$project/launcher/build/outputs/apk/debug/launcher-debug.apk"
[ -f "$built" ] || { echo "no APK at $built"; exit 6; }
mkdir -p "$(dirname "$apk")"
cp -f "$built" "$apk"
aapt2="$(ls "$android"/SDK/build-tools/*/aapt2.exe 2>/dev/null | tail -1)"
manifest="$repo/Temp/AndroidApk-manifest.txt"
"$aapt2" dump xmltree --file AndroidManifest.xml "$apk" > "$manifest" 2>&1
echo "$apk ($(du -h "$apk" | cut -f1)); AdMob in its manifest:"
grep -A1 -E 'com\.google\.android\.gms\.ads\.(APPLICATION_ID|AdActivity)|permission\.AD_ID' "$manifest" | grep -o 'Raw: "[^"]*"' | sort -u
grep -q "$app_id" "$manifest" && exit 0
echo "the AdMob app id is missing from the APK's manifest ($manifest)"
exit 6
