#!/usr/bin/env bash
# Compiles the project's first-party scripts with Unity's own Roslyn against Unity's assemblies,
# to catch compile errors before the open Editor imports them (errors would put it in Safe Mode).
set -u
U="${UNITY_DATA:-/d/Applications/UnityEditor/6000.3.18f1/Editor/Data}"
P="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$P/Temp/compile-check"
mkdir -p "$OUT"
DOTNET="$U/NetCoreRuntime/dotnet.exe"
[ -x "$DOTNET" ] || DOTNET="dotnet"
CSC="$U/DotNetSdkRoslyn/csc.dll"

refs=()
refs+=("-r:$(cygpath -w "$U/NetStandard/ref/2.1.0/netstandard.dll")")
for f in "$U"/Managed/UnityEngine/*.dll; do refs+=("-r:$(cygpath -w "$f")"); done
for n in UnityEngine.UI Unity.TextMeshPro Unity.InputSystem Unity.RenderPipelines.Core.Runtime Unity.RenderPipelines.Universal.Runtime; do
  refs+=("-r:$(cygpath -w "$P/Library/ScriptAssemblies/$n.dll")")
done

runtime=$(find "$P/Assets/_Project/Scripts" -name '*.cs' -not -path '*/Editor/*' | while read -r f; do cygpath -w "$f"; done)
editor=$(find "$P/Assets/_Project/Scripts" -name '*.cs' -path '*/Editor/*' | while read -r f; do cygpath -w "$f"; done)

echo "== runtime scripts"
"$DOTNET" "$(cygpath -w "$CSC")" -nologo -nostdlib -target:library -langversion:9.0 -nowarn:CS1701,CS1702 \
  -define:UNITY_EDITOR "${refs[@]}" -out:"$(cygpath -w "$OUT/runtime.dll")" $runtime 2>&1 | head -40
echo "exit=${PIPESTATUS[0]}"

echo "== editor scripts"
"$DOTNET" "$(cygpath -w "$CSC")" -nologo -nostdlib -target:library -langversion:9.0 -nowarn:CS1701,CS1702 \
  -define:UNITY_EDITOR "${refs[@]}" -r:"$(cygpath -w "$OUT/runtime.dll")" -out:"$(cygpath -w "$OUT/editor.dll")" $editor 2>&1 | head -60
echo "exit=${PIPESTATUS[0]}"
