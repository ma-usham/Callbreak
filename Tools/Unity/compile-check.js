#!/usr/bin/env node
// Compiles every first-party assembly (Assets/_Project/**/*.asmdef) with Unity's own Roslyn, in
// dependency order, against the Editor's engine DLLs and the package DLLs Unity already built in
// Library/ScriptAssemblies. It catches compile errors before the open Editor sees them: one broken
// file stops every recompile for everyone sharing the Editor.
//
// Usage: node Tools/Unity/compile-check.js [--quiet]
// Exit code 0 = every assembly compiles; 1 = errors (printed as file(line,col): error CSxxxx ...).
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const repo = path.resolve(__dirname, '..', '..');
const unityData = process.env.UNITY_DATA || 'D:/Applications/UnityEditor/6000.3.18f1/Editor/Data';
const dotnet = fs.existsSync(path.join(unityData, 'NetCoreRuntime', 'dotnet.exe')) ? path.join(unityData, 'NetCoreRuntime', 'dotnet.exe') : 'dotnet';
const csc = path.join(unityData, 'DotNetSdkRoslyn', 'csc.dll');
const scriptAssemblies = path.join(repo, 'Library', 'ScriptAssemblies');
const out = path.join(repo, 'Temp', 'compile-check');
const quiet = process.argv.includes('--quiet');
fs.mkdirSync(out, { recursive: true });

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
  const refs = [netstandard];
  if (!a.json.noEngineReferences) refs.push(...engineRefs);
  if (isEditor || constraints.includes('UNITY_INCLUDE_TESTS')) refs.push(...editorRefs);
  for (const r of a.json.references || []) {
    if (byName.has(r)) { refs.push(path.join(out, r + '.dll')); continue; }
    const dll = path.join(scriptAssemblies, r + '.dll');
    if (fs.existsSync(dll)) refs.push(dll);
    else if (!quiet) console.log(`note: ${a.name}: reference ${r} not built yet by Unity; skipped`);
  }
  for (const p of a.json.precompiledReferences || []) {
    if (/nunit/i.test(p) && nunit) { refs.push(nunit); refs.push(...netfxShims); }
  }
  // Unity adds these to every assembly that has engine references.
  if (!a.json.noEngineReferences) {
    for (const extra of ['UnityEngine.UI.dll', 'Unity.TextMeshPro.dll']) {
      const dll = path.join(scriptAssemblies, extra);
      if (fs.existsSync(dll) && !refs.includes(dll) && (a.json.references || []).some(r => extra.startsWith(r))) refs.push(dll);
    }
  }
  const defines = ['UNITY_EDITOR', 'UNITY_6000_3_OR_NEWER', 'UNITY_2023_1_OR_NEWER', 'UNITY_2022_3_OR_NEWER', 'UNITY_STANDALONE_WIN', 'UNITY_INCLUDE_TESTS', 'DEBUG', 'TRACE'];
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
