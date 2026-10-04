// Diagnostic + matte refinement: per-pixel flower alpha by projecting (BG - clean) on (Fhat - clean),
// where clean = ChatGPT's table (inside the silhouette) or terrace (outside), both aligned and
// colour-matched, and Fhat = the colour of confident flower pixels diffused outward.
// node alpha_proj.js <BG> <TableMask> <tableFill> <sceneFill> <M.bin> <out.bin> <diagDir>
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const { guidedFilter } = require('./guided.js');
const [, , bgp, maskp, tfillp, sfillp, mp, outp, diagDir] = process.argv;
fs.mkdirSync(diagDir, { recursive: true });
const bg = P.read(bgp), mk = P.read(maskp);
const W = bg.w, H = bg.h, N = W * H;
const B = O.planes(bg).c;
const a = new Float32Array(N);
for (let i = 0; i < N; i++) a[i] = mk.data[i * 4 + 3] / 255;
const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const M = new Float32Array(fs.readFileSync(mp).buffer.slice(0));
const near = O.boxBlur(M.map(v => (v > 0 ? 1 : 0)), W, H, 10);
const Tg = O.upscaleShift(P.read(tfillp), W, H, 0.3, -1.2).c;
const own = new Float32Array(N);
for (let i = 0; i < N; i++) { const r = Tg[0][i], g = Tg[1][i], b = Tg[2][i]; own[i] = (g > 170 && r < 120 && b < 120 && g - Math.max(r, b) > 90) ? 0 : 1; }
const ownEro = O.boxBlur(own, W, H, 2);
for (let c = 0; c < 3; c++) Tg[c] = O.pushPull(Tg[c], ownEro.map(v => (v > 0.999 ? 1 : 0)), W, H);
const Sg = O.upscaleShift(P.read(sfillp), W, H, 0, 0).c;
function gainField(G, trust, R) {
  const out = [];
  for (let c = 0; c < 3; c++) {
    const lb = O.boxBlur(B[c].map((v, i) => v * trust[i]), W, H, R), lg = O.boxBlur(G[c].map((v, i) => v * trust[i]), W, H, R), lw = O.boxBlur(trust, W, H, R);
    const ratio = new Float32Array(N), w2 = new Float32Array(N);
    for (let i = 0; i < N; i++) if (lw[i] > 0.25) { ratio[i] = (lb[i] + 4) / (lg[i] + 4); w2[i] = 1; }
    out.push(O.pushPull(ratio, w2, W, H));
  }
  return out;
}
const trustT = new Float32Array(N), trustS = new Float32Array(N);
for (let i = 0; i < N; i++) { trustT[i] = a[i] > 0.999 && near[i] < 0.01 ? 1 : 0; trustS[i] = a[i] === 0 && near[i] < 0.01 ? 1 : 0; }
const gT = gainField(Tg, trustT, 18), gS = gainField(Sg, trustS, 24);
const C = [0, 1, 2].map(c => new Float32Array(N));
for (let i = 0; i < N; i++) for (let c = 0; c < 3; c++) {
  const t = clamp(Tg[c][i] * clamp(gT[c][i], 0.3, 2.5), 0, 255), s = clamp(Sg[c][i] * clamp(gS[c][i], 0.3, 2.5), 0, 255);
  C[c][i] = a[i] * t + (1 - a[i]) * s;
}
const Fhat = [0, 1, 2].map(c => O.pushPull(B[c], M.map(v => (v > 0.97 ? 1 : 0)), W, H));
// projection alpha + reliability (separation between flower and clean colours)
const ap = new Float32Array(N), rel = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (near[i] <= 0) continue;
  const fx = Fhat[0][i] - C[0][i], fy = Fhat[1][i] - C[1][i], fz = Fhat[2][i] - C[2][i], ff = fx * fx + fy * fy + fz * fz;
  const p = ((B[0][i] - C[0][i]) * fx + (B[1][i] - C[1][i]) * fy + (B[2][i] - C[2][i]) * fz) / Math.max(ff, 1);
  ap[i] = clamp(p, 0, 1);
  rel[i] = clamp((Math.sqrt(ff) - 30) / 60, 0, 1);
}
// blend: reliable -> projection, unreliable -> matte; then a guided filter for edges that follow BG
const mix = new Float32Array(N);
for (let i = 0; i < N; i++) mix[i] = near[i] > 0 ? rel[i] * ap[i] + (1 - rel[i]) * M[i] : 0;
// guided filter in the two corner boxes
const out = new Float32Array(N);
const boxes = [[0, 600, 900, H - 600], [1380, 640, W - 1380, H - 640]];
for (const [bx, by, bw, bh] of boxes) {
  const n = bw * bh, I = [new Float32Array(n), new Float32Array(n), new Float32Array(n)], p = new Float32Array(n);
  for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) { const i = (by + y) * W + bx + x, j = y * bw + x; for (let c = 0; c < 3; c++) I[c][j] = B[c][i] / 255; p[j] = mix[i]; }
  const q = guidedFilter(I, p, bw, bh, 4, 0.004);
  for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) { const i = (by + y) * W + bx + x, j = y * bw + x; out[i] = near[i] > 0 ? clamp(q[j], 0, 1) : 0; }
}
for (let i = 0; i < N; i++) { if (out[i] < 0.02) out[i] = 0; if (out[i] > 0.98) out[i] = 1; }
fs.writeFileSync(outp, Buffer.from(out.buffer));
const g = v => O.toImage({ w: W, h: H, c: [v.map(x => x * 255), v.map(x => x * 255), v.map(x => x * 255)] });
P.write(diagDir + '/ap.png', g(ap), { rgb: true, level: 3 });
P.write(diagDir + '/rel.png', g(rel), { rgb: true, level: 3 });
P.write(diagDir + '/alpha_final.png', g(out), { rgb: true, level: 3 });
console.log('ok');
