// Material zones of Table_HimalayanIvory, for the colour transfer to the other tables.
// Soft weights per pixel (sum 1 inside the table):
//   surface  the ivory playing surface (inside the gold key band, excluding the motifs)
//   gold     the gold key band, its cream ground and the gold knot motifs
//   wood     the lip, the drum and the carving
// node zones.js <Table_HimalayanIvory.png> <outDir>
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , tp, outDir] = process.argv;
fs.mkdirSync(outDir, { recursive: true });
const t = P.read(tp);
const W = t.w, H = t.h, N = W * H;
const d = t.data;
const A = new Float32Array(N);
for (let i = 0; i < N; i++) A[i] = d[i * 4 + 3] / 255;
// 1) interior: flood fill of ivory-like colour from the centre (global + local colour criteria)
let sr = 0, sg = 0, sb = 0, n = 0;
for (let y = 700; y < 780; y++) for (let x = 1050; x < 1180; x++) { const i = (y * W + x) * 4; sr += d[i]; sg += d[i + 1]; sb += d[i + 2]; n++; }
const ref = [sr / n, sg / n, sb / n];
const flood = new Uint8Array(N);
const st = [740 * W + 1115]; flood[st[0]] = 1;
while (st.length) {
  const p = st.pop(), x = p % W, y = (p / W) | 0, i = p * 4;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
    const q = ny * W + nx; if (flood[q]) continue; const j = q * 4;
    const dg = Math.abs(d[j] - ref[0]) + Math.abs(d[j + 1] - ref[1]) + Math.abs(d[j + 2] - ref[2]);
    const dl = Math.abs(d[j] - d[i]) + Math.abs(d[j + 1] - d[i + 1]) + Math.abs(d[j + 2] - d[i + 2]);
    if (dg < 90 && dl < 26) { flood[q] = 1; st.push(q); }
  }
}
// fill holes of the flood region (the motifs and specks) -> interior
const outside = new Uint8Array(N), st2 = [];
for (let x = 0; x < W; x++) { for (const y of [0, H - 1]) { const i = y * W + x; if (!flood[i] && !outside[i]) { outside[i] = 1; st2.push(i); } } }
for (let y = 0; y < H; y++) { for (const x of [0, W - 1]) { const i = y * W + x; if (!flood[i] && !outside[i]) { outside[i] = 1; st2.push(i); } } }
while (st2.length) {
  const p = st2.pop(), x = p % W, y = (p / W) | 0;
  for (const q of [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1]) if (q >= 0 && !outside[q] && !flood[q]) { outside[q] = 1; st2.push(q); }
}
const interior = new Float32Array(N);
for (let i = 0; i < N; i++) interior[i] = outside[i] ? 0 : 1;
// 2) gold-ness from colour: saturated warm orange/yellow, fairly bright (motifs, key band lines)
const gold = new Float32Array(N), cream = new Float32Array(N);
const sm = (e0, e1, v) => { const x = Math.min(Math.max((v - e0) / (e1 - e0), 0), 1); return x * x * (3 - 2 * x); };
for (let i = 0; i < N; i++) {
  const r = d[i * 4], g = d[i * 4 + 1], b = d[i * 4 + 2];
  const sat = r > 0 ? (r - b) / r : 0, L = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  gold[i] = sm(0.42, 0.6, sat) * sm(95, 140, L) * sm(150, 190, r);
  cream[i] = sm(150, 190, L) * (1 - sm(0.42, 0.6, sat));
}
// 3) band region: within ~60 px outside the interior edge, where the pixels are gold or cream (the
//    key band's lines and ground), as opposed to the darker wood of the lip
const nearInt = O.boxBlur(interior, W, H, 24);
const wS = new Float32Array(N), wG = new Float32Array(N), wW = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (A[i] <= 0) continue;
  if (interior[i]) {
    // inside: surface unless gold (motif)
    const g = gold[i];
    wG[i] = g; wS[i] = 1 - g;
  } else {
    const nearBand = nearInt[i] > 0.02 ? 1 : 0;
    const g = Math.max(gold[i], nearBand * cream[i]);
    wG[i] = nearBand ? g : gold[i] * 0.0; // gold far from the band (lip highlights) stays wood
    wW[i] = 1 - wG[i];
  }
}
// smooth the weights slightly (anti-aliased boundaries)
const s = [O.boxBlur(wS, W, H, 1), O.boxBlur(wG, W, H, 1), O.boxBlur(wW, W, H, 1)];
for (let i = 0; i < N; i++) { const tot = s[0][i] + s[1][i] + s[2][i]; if (tot > 0) for (let k = 0; k < 3; k++) s[k][i] /= tot; }
const names = ['surface', 'gold', 'wood'];
for (let k = 0; k < 3; k++) fs.writeFileSync(outDir + '/zone_' + names[k] + '.bin', Buffer.from(s[k].buffer));
fs.writeFileSync(outDir + '/interior.bin', Buffer.from(interior.buffer));
// preview: surface = blue, gold = yellow, wood = brown
const pv = P.create(W, H);
for (let i = 0; i < N; i++) {
  const c = [s[0][i] * 60 + s[1][i] * 250 + s[2][i] * 110, s[0][i] * 120 + s[1][i] * 210 + s[2][i] * 60, s[0][i] * 255 + s[1][i] * 40 + s[2][i] * 30];
  for (let k = 0; k < 3; k++) pv.data[i * 4 + k] = Math.round(c[k] * A[i] + 255 * (1 - A[i]));
  pv.data[i * 4 + 3] = 255;
}
P.write(outDir + '/zones_preview.png', pv, { rgb: true, level: 3 });
let a0 = 0, a1 = 0, a2 = 0; for (let i = 0; i < N; i++) { a0 += s[0][i] * A[i]; a1 += s[1][i] * A[i]; a2 += s[2][i] * A[i]; }
console.log('zone areas: surface', a0.toFixed(0), 'gold', a1.toFixed(0), 'wood', a2.toFixed(0));
