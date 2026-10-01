// Finds music loop points that are a whole number of bars apart, for ArtSource/Audio/Sources/sources.json:
// the beat period from the onset autocorrelation at a lag of 16 beats (parabola-refined), the beat
// phase from a comb over the steady part, then the start bar whose rhythm best matches the end's.
// Usage: node Tools/AudioGen/find-loop.js <file.wav> <from s> <to s> <crossfade s> <beat guess s> <beats per bar>
const fs = require('fs');
const [file, from, to, xf, guess, perBar] = process.argv.slice(2).map((v, i) => i === 0 ? v : Number(v));
const b = fs.readFileSync(file);
let p = 12, fmt, data;
while (p + 8 <= b.length) { const id = b.toString('ascii', p, p + 4), size = b.readUInt32LE(p + 4); if (id === 'fmt ') fmt = { ch: b.readUInt16LE(p + 10), rate: b.readUInt32LE(p + 12) }; if (id === 'data') data = b.subarray(p + 8, p + 8 + size); p += 8 + size + (size & 1); }
const n = data.length / 2 / fmt.ch, hop = fmt.rate / 100, frames = Math.floor(n / hop);
const e = new Float64Array(frames);
for (let f = 0; f < frames; f++) { let s = 0; for (let i = f * hop; i < Math.min(n, f * hop + 2 * hop); i++) { let v = 0; for (let c = 0; c < fmt.ch; c++) v += data.readInt16LE((i * fmt.ch + c) * 2) / 32768; v /= fmt.ch; s += v * v; } e[f] = Math.log(s + 1e-9); }
const o = new Float64Array(frames); for (let f = 1; f < frames; f++) o[f] = Math.max(0, e[f] - e[f - 1]);
const f0 = Math.round(from * 100), f1 = Math.round(to * 100);
let mean = 0; for (let f = f0; f < f1; f++) mean += o[f]; mean /= f1 - f0;
const ac = lag => { let s = 0; for (let f = f0; f + lag < f1; f++) s += (o[f] - mean) * (o[f + lag] - mean); return s / (f1 - f0 - lag); };
// Precise period: the best lag near 16 beats, parabola-refined, divided by 16.
const k = 16, centre = Math.round(guess * 100 * k);
let best = centre; for (let lag = Math.round(centre * 0.97); lag <= Math.round(centre * 1.03); lag++) if (ac(lag) > ac(best)) best = lag;
const y0 = ac(best - 1), y1 = ac(best), y2 = ac(best + 1), refine = 0.5 * (y0 - y2) / (y0 - 2 * y1 + y2);
const beat = (best + refine) / k;                    // frames
// Beat phase: comb over the steady part.
let phase = 0, top = -1;
for (let ph = 0; ph < beat; ph += 0.25) { let s = 0; for (let t = f0 + ph; t < f1; t += beat) s += o[Math.round(t)]; if (s > top) { top = s; phase = ph; } }
const bar = beat * perBar, cross = xf * 100, w = Math.round(bar);
const corr = (pa, pb, len) => { let xy = 0, xx = 0, yy = 0; for (let i = 0; i < len; i++) { const u = o[pa + i], v = o[pb + i]; xy += u * v; xx += u * u; yy += v * v; } return xy / Math.sqrt(xx * yy + 1e-30); };
console.log(`${file.split('/').pop()}: beat ${(beat / 100).toFixed(4)} s (${(6000 / beat).toFixed(2)} BPM), bar of ${perBar} = ${(bar / 100).toFixed(3)} s, phase ${(phase / 100).toFixed(3)} s`);
const results = [];
for (let first = f0 + phase; first < f0 + phase + 4 * bar; first += beat) {
  const a = first;
  if (a - cross < f0) continue;
  for (let m = Math.floor((f1 - a) / bar); m >= Math.floor((f1 - a) / bar) - 1 && m > 0; m--) {
    const bEnd = a + m * bar;
    const r = corr(Math.round(a) - w, Math.round(bEnd) - w, 2 * w);
    results.push({ a: a / 100, b: bEnd / 100, m, r });
  }
}
results.sort((x, y) => y.r - x.r);
for (const r of results.slice(0, 5)) console.log(`  loop ${r.a.toFixed(3)}-${r.b.toFixed(3)} s = ${r.m} bars (${(r.b - r.a).toFixed(3)} s), rhythm match ${r.r.toFixed(3)}`);
