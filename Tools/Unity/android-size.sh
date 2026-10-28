#!/usr/bin/env bash
# Measures the Android release build's size: the open Editor exports the release player as a Gradle
# project (Callbreak.Editor.BuildCheck, into Temp/AndroidExport); Unity's own Gradle and JDK then
# build both the universal APK (assembleRelease) and the App Bundle that Google Play takes
# (bundleRelease), signed with the Android debug key so no keystore is needed. Unity's bundletool
# then works out what one phone downloads from Play (an ARM64 phone, and a 32-bit one).
# The project's keystore and Player Settings are left alone. Holds the shared Editor lock.
# Usage: Tools/Unity/android-size.sh <label>     (outputs and the report go to Builds/Size/<label>)
# Exit code 0 = built and measured; 6 = anything else.
set -u
label="${1:-current}"
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cd "$repo"
report="$repo/Temp/AndroidExport-report.txt"
project="$repo/Temp/AndroidExport"
out="$repo/Builds/Size/$label"
unity_data="${UNITY_DATA:-}"
if [ -z "$unity_data" ]; then
  for d in D:/Applications/Unity/6000.3.18f1/Editor/Data D:/Applications/UnityEditor/6000.3.18f1/Editor/Data; do
    [ -d "$d" ] && unity_data="$d" && break
  done
fi
android="$unity_data/PlaybackEngines/AndroidPlayer"
java="$android/OpenJDK/bin/java.exe"
bundletool="$(ls "$android"/Tools/bundletool-all-*.jar 2>/dev/null | tail -1)"

lock="$repo/Temp/unity-editor.lock"
mkdir -p "$repo/Temp"
waited=0
until mkdir "$lock" 2>/dev/null; do
  if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 900 ]; then rm -rf "$lock"; continue; fi
  if [ $waited -ge 1500 ]; then echo "gave up waiting for the Editor lock ($lock)"; exit 6; fi
  [ $((waited % 30)) -eq 0 ] && echo "waiting for the Editor lock (held by: $(cat "$lock/owner" 2>/dev/null))"
  sleep 5; waited=$((waited + 5))
done
echo "${AGENT_NAME:-agent} pid $$ $(date +%H:%M:%S) android-size" > "$lock/owner"
( while [ -d "$lock" ]; do touch "$lock" 2>/dev/null; sleep 60; done ) &
keeper=$!
trap 'kill $keeper 2>/dev/null; rm -rf "$lock"' EXIT

rm -f "$report"
# A background Editor may not tick, and the export starts on its next tick.
powershell -NoProfile -ExecutionPolicy Bypass -File "$here/focus-editor.ps1" >/dev/null 2>&1
unity command eval --timeout 120 --code 'UnityEditor.AssetDatabase.Refresh(); return Callbreak.Editor.BuildCheck.ScheduleExport();' 2>&1 | tail -1
deadline=$(( $(date +%s) + 2400 ))
while [ ! -f "$report" ]; do
  if [ $(date +%s) -gt $deadline ]; then echo "the export did not finish in 40 minutes"; exit 6; fi
  sleep 10
done
head -1 "$report"
grep -q '^Succeeded' "$report" || { cat "$report"; exit 6; }

# Release signed with the debug key (AGP's debug signing config). The exported script can end
# without a newline (the AdMob plugin appends an apply line), so start a new line first.
printf '\n%s\n' "android.buildTypes.release.signingConfig = android.signingConfigs.debug" >> "$project/launcher/build.gradle"
echo "gradle assembleRelease bundleRelease ($(date +%H:%M:%S))"
mkdir -p "$out"
gradle_log="$out/gradle.log"
JAVA_HOME="$android/OpenJDK" "$java" -Xmx64m \
  -cp "$(ls "$android"/Tools/gradle/lib/gradle-launcher-*.jar | tail -1)" org.gradle.launcher.GradleMain \
  --no-daemon --console=plain -p "$project" assembleRelease bundleRelease > "$gradle_log" 2>&1
gradle_exit=$?
grep -E "BUILD (SUCCESSFUL|FAILED)|What went wrong" "$gradle_log" | head -5
if [ $gradle_exit -ne 0 ]; then echo "Gradle failed (exit $gradle_exit); the log is $gradle_log"; exit 6; fi

apk="$project/launcher/build/outputs/apk/release/launcher-release.apk"
aab="$project/launcher/build/outputs/bundle/release/launcher-release.aab"
[ -f "$apk" ] && [ -f "$aab" ] || { echo "missing $apk or $aab"; exit 6; }
cp -f "$apk" "$out/Callbreak.apk"
cp -f "$aab" "$out/Callbreak.aab"
cp -f "$report" "$out/export-report.txt"

# What Play sends one phone: the bundle's split APKs for a device spec, measured by bundletool.
for abi in arm64-v8a armeabi-v7a; do
  spec="$out/device-$abi.json"
  printf '{"supportedAbis":["%s"],"supportedLocales":["en-US"],"screenDensity":420,"sdkVersion":33}\n' "$abi" > "$spec"
  "$java" -jar "$bundletool" build-apks --bundle="$out/Callbreak.aab" --output="$out/device-$abi.apks" \
    --device-spec="$spec" --overwrite > /dev/null 2>&1 || { echo "bundletool build-apks failed ($abi)"; exit 6; }
  "$java" -jar "$bundletool" get-size total --apks="$out/device-$abi.apks" --device-spec="$spec" > "$out/download-$abi.csv" 2>&1
done

python "$here/android-size.py" "$out" | tee "$out/report.txt"
