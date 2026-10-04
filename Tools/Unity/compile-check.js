#!/usr/bin/env node
// Compiles every first-party assembly (Assets/_Project/**/*.asmdef) with Unity's own Roslyn, in
// dependency order, against the Editor's engine DLLs and the package DLLs Unity already built in
// Library/ScriptAssemblies. It catches compile errors before the open Editor sees them: one broken
// file stops every recompile for everyone sharing the Editor.
//
// Usage: node Tools/Unity/compile-check.js [--quiet] [--player | --android]
//   default   the Editor's view: UNITY_EDITOR defined and the UnityEditor modules referenced by
//             every assembly (runtime code may use UnityEditor inside #if UNITY_EDITOR)
//   --android the same, as an Editor whose build target is Android sees it: UNITY_ANDROID defined
//             and Editor assemblies referencing the Android build extensions (build post-processors)
//   --player  an Android player build's view: no UNITY_EDITOR, no editor references, and no
//             Editor-only or test assemblies - catches editor API leaking into runtime code
// Exit code 0 = every assembly compiles; 1 = errors (printed as file(line,col): error CSxxxx ...).
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const repo = path.resolve(__dirname, '..', '..');
const unityData = process.env.UNITY_DATA || 'D:/Applications/UnityEditor/6000.3.18f1/Editor/Data';
const dotnet = fs.existsSync(path.join(unityData, 'NetCoreRuntime', 'dotnet.exe')) ? path.join(unityData, 'NetCoreRuntime', 'dotnet.exe') : 'dotnet';
const csc = path.join(unityData, 'DotNetSdkRoslyn', 'csc.dll');
const scriptAssemblies = path.join(repo, 'Library', 'ScriptAssemblies');
const quiet = process.argv.includes('--quiet');
const player = process.argv.includes('--player');
const android = process.argv.includes('--android');
// Each run has its own output folder: several agents run this check at the same time.
const out = path.join(repo, 'Temp', player ? 'compile-check-player' : android ? 'compile-check-android' : 'compile-check', String(process.pid));
fs.mkdirSync(out, { recursive: true });
process.on('exit', () => { try { fs.rmSync(out, { recursive: true, force: true }); } catch (e) { /* best effort */ } });

function walk(dir, pred, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, pred, acc);
    else if (pred(p)) acc.push(p);
  }
  return acc;
}

// First-party assemblies and the sources each one owns (a folder belongs to the nearest asmdef).
const asmdefs = walk(path.join(repo, 'Assets', '_Project'), p => p.endsWith('.asmdef')).map(file => {
  const json = JSON.parse(fs.readFileSync(file, 'utf8'));
  return { file, dir: path.dirname(file), name: json.name, json };
});
const byName = new Map(asmdefs.map(a => [a.name, a]));
const allCs = walk(path.join(repo, 'Assets', '_Project'), p => p.endsWith('.cs'));
for (const a of asmdefs) a.sources = [];
for (const cs of allCs) {
  let best = null;
  for (const a of asmdefs) {
    if (cs.startsWith(a.dir + path.sep) && (!best || a.dir.length > best.dir.length)) best = a;
  }
  if (best) best.sources.push(cs);
}

const moduleDlls = walk(path.join(unityData, 'Managed', 'UnityEngine'), p => p.endsWith('.dll'));
const engineRefs = moduleDlls.filter(p => path.basename(p).startsWith('UnityEngine'));
const editorRefs = moduleDlls.filter(p => path.basename(p).startsWith('UnityEditor'));
const netfxShims = walk(path.join(unityData, 'NetStandard', 'compat', '2.1.0', 'shims', 'netfx'), p => p.endsWith('.dll'));
const netstandard = path.join(unityData, 'NetStandard', 'ref', '2.1.0', 'netstandard.dll');
const nunit = walk(path.join(repo, 'Library', 'PackageCache'), p => /com\.unity\.ext\.nunit@[^\\/]+[\\/].*nunit\.framework\.dll$/i.test(p))[0];

// Precompiled DLLs an assembly names in precompiledReferences (with overrideReferences, as Unity
// resolves them: by file name, anywhere in the packages or Assets). Indexed on first use only.
let precompiled = null;
function findPrecompiled(name) {
  if (!precompiled) {
    precompiled = new Map();
    for (const root of [path.join(repo, 'Library', 'PackageCache'), path.join(repo, 'Packages'), path.join(repo, 'Assets')]) {
      if (!fs.existsSync(root)) continue;
      for (const dll of walk(root, p => p.toLowerCase().endsWith('.dll'))) {
        const key = path.basename(dll).toLowerCase();
        if (!precompiled.has(key)) precompiled.set(key, dll);
      }
    }
  }
  return precompiled.get(name.toLowerCase());
}

const order = [];
const seen = new Set();
function visit(a) {
  if (seen.has(a.name)) return;
  seen.add(a.name);
  for (const r of a.json.references || []) if (byName.has(r)) visit(byName.get(r));
  order.push(a);
}
asmdefs.forEach(visit);

let failed = false;
for (const a of order) {
  const constraints = a.json.defineConstraints || [];
  if (constraints.includes('UNITY_SERVER')) continue;          // server-only assembly
  if (a.sources.length === 0) continue;
  const isEditor = (a.json.includePlatforms || []).includes('Editor');
  if (player && (isEditor || constraints.includes('UNITY_INCLUDE_TESTS'))) continue;
  const refs = [netstandard];
  if (!a.json.noEngineReferences) refs.push(...engineRefs);
  // In the Editor every assembly with engine references also sees the UnityEditor modules.
  if (!player && (!a.json.noEngineReferences || isEditor || constraints.includes('UNITY_INCLUDE_TESTS'))) refs.push(...editorRefs);
  if (android && isEditor) refs.push(path.join(unityData, 'PlaybackEngines', 'AndroidPlayer', 'UnityEditor.Android.Extensions.dll'));
  for (const r of a.json.references || []) {
    if (byName.has(r)) { refs.push(path.join(out, r + '.dll')); continue; }
    const dll = path.join(scriptAssemblies, r + '.dll');
    if (fs.existsSync(dll)) refs.push(dll);
    else if (!quiet) console.log(`note: ${a.name}: reference ${r} not built yet by Unity; skipped`);
  }
  for (const p of a.json.precompiledReferences || []) {
    if (/nunit/i.test(p)) {
      if (nunit) { refs.push(nunit); refs.push(...netfxShims); }
      continue;
    }
    // Other plugin DLLs (e.g. Google Mobile Ads) are .NET Framework builds: they need the netfx shims too.
    const dll = findPrecompiled(p);
    if (dll) { refs.push(dll); for (const shim of netfxShims) if (!refs.includes(shim)) refs.push(shim); }
    else if (!quiet) console.log(`note: ${a.name}: precompiled reference ${p} not found; skipped`);
  }
  // Unity adds these to every assembly that has engine references.
  if (!a.json.noEngineReferences) {
    for (const extra of ['UnityEngine.UI.dll', 'Unity.TextMeshPro.dll']) {
      const dll = path.join(scriptAssemblies, extra);
      if (fs.existsSync(dll) && !refs.includes(dll) && (a.json.references || []).some(r => extra.startsWith(r))) refs.push(dll);
    }
  }
  // Unity defines every UNITY_x_OR_NEWER up to the running version.
  const platformDefines = player ? ['UNITY_ANDROID', 'UNITY_64']
    : android ? ['UNITY_EDITOR', 'UNITY_ANDROID', 'UNITY_64', 'UNITY_INCLUDE_TESTS', 'DEBUG', 'TRACE']
    : ['UNITY_EDITOR', 'UNITY_STANDALONE_WIN', 'UNITY_STANDALONE', 'UNITY_64', 'UNITY_INCLUDE_TESTS', 'DEBUG', 'TRACE'];
  const defines = [...platformDefines, 'UNITY_5_3_OR_NEWER', 'UNITY_5_4_OR_NEWER', 'UNITY_5_5_OR_NEWER', 'UNITY_5_6_OR_NEWER', 'UNITY_2017_1_OR_NEWER', 'UNITY_2017_2_OR_NEWER', 'UNITY_2017_3_OR_NEWER', 'UNITY_2017_4_OR_NEWER', 'UNITY_2018_1_OR_NEWER', 'UNITY_2018_2_OR_NEWER', 'UNITY_2018_3_OR_NEWER', 'UNITY_2018_4_OR_NEWER', 'UNITY_2019_1_OR_NEWER', 'UNITY_2019_2_OR_NEWER', 'UNITY_2019_3_OR_NEWER', 'UNITY_2019_4_OR_NEWER', 'UNITY_2020_1_OR_NEWER', 'UNITY_2020_2_OR_NEWER', 'UNITY_2020_3_OR_NEWER', 'UNITY_2021_1_OR_NEWER', 'UNITY_2021_2_OR_NEWER', 'UNITY_2021_3_OR_NEWER', 'UNITY_2022_1_OR_NEWER', 'UNITY_2022_2_OR_NEWER', 'UNITY_2022_3_OR_NEWER', 'UNITY_2023_1_OR_NEWER', 'UNITY_2023_2_OR_NEWER', 'UNITY_6000_0_OR_NEWER', 'UNITY_6000_1_OR_NEWER', 'UNITY_6000_2_OR_NEWER', 'UNITY_6000_3_OR_NEWER'];
  const rsp = path.join(out, a.name + '.rsp');
  const lines = [
    '-nologo', '-nostdlib', '-target:library', '-langversion:9.0', '-nowarn:CS1701,CS1702,CS0649', '-warnaserror-',
    '-define:' + defines.join(';'), '-out:' + path.join(out, a.name + '.dll'),
    ...refs.map(r => '-r:"' + r + '"'),
    ...a.sources.map(s => '"' + s + '"'),
  ];
  fs.writeFileSync(rsp, lines.join('\n'));
  const res = spawnSync(dotnet, [csc, '@' + rsp], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const text = (res.stdout || '') + (res.stderr || '');
  const errors = text.split(/\r?\n/).filter(l => / error /.test(l));
  const warnings = text.split(/\r?\n/).filter(l => / warning /.test(l));
  if (res.status !== 0 || errors.length) {
    failed = true;
    console.log(`FAIL ${a.name} (${a.sources.length} files)`);
    errors.slice(0, 40).forEach(e => console.log('  ' + e.replace(repo + path.sep, '')));
    if (errors.length === 0) console.log(text.slice(0, 2000));
  } else if (!quiet) {
    console.log(`ok   ${a.name} (${a.sources.length} files${warnings.length ? ', ' + warnings.length + ' warnings' : ''})`);
    warnings.slice(0, 5).forEach(w => console.log('  ' + w.replace(repo + path.sep, '')));
  }
}
process.exit(failed ? 1 : 0);
