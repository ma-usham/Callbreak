// Builds a material variant of the gameplay table with IDENTICAL geometry to Table_HimalayanIvory.
//
// node variant.js <Ivory.png> <zonesDir> <variant.png (ChatGPT, any size, on #00FF00)> <out.png> <config.json>
//
// config: {
//   "surface": "variant" | "gain",   // playing surface: ChatGPT's aligned pixels, or the Ivory's pixels with a colour gain
//   "surfaceTarget": [r,g,b],         // mean colour the surface is corrected to (from mockup 21, relit to the scene)
//   "goldGain": [r,g,b] | "variant",  // per-channel gain on the Ivory's gold band + motifs, or zone-wise ratio to the variant
//   "woodGain": [r,g,b] | "variant",  // the same for the lip and drum
//   "radius": 6                       // low-pass radius for the "variant" ratios
// }
// Alignment: the variant is upscaled to the frame, shifted globally, then warped by a smooth field of
// local shifts (gradient NCC on table tiles). Alpha = the Ivory's alpha (TableMask) everywhere.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , ivp, zdir, varp, outp, cfgp] = process.argv;
const cfg = JSON.parse(fs.readFileSync(cfgp, 'utf8'));
const iv = P.read(ivp);
const W = iv.w, H = iv.h, N = W * H;
const I = O.planes(iv).c;
const A = new Float32Array(N);
for (let i = 0; i < N; i++) A[i] = iv.data[i * 4 + 3] / 255;
const rd = n => new Float32Array(fs.readFileSync(zdir + '/' + n).buffer.slice(0));
const wS = rd('zone_surface.bin'), wG = rd('zone_gold.bin'), wW = rd('zone_wood.bin');
const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

// ---------- align the variant ----------
const vimg = P.read(varp);
let V0 = vimg.w === W ? O.planes(vimg).c : O.upscaleShift(vimg, W, H, 0, 0).c;
const lum = c => { const L = new Float32Array(N); for (let i = 0; i < N; i++) L[i] = 0.2126 * c[0][i] + 0.7152 * c[1][i] + 0.0722 * c[2][i]; return L; };
const grad = L => { const g = new Float32Array(N); for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; g[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + W] - L[i - W]); } return g; };
// rank-normalise gradients so different materials compare (structure, not contrast)
const GI = grad(lum(I));
function nccAt(GV, tx, ty, T, dx, dy, ok) {
  let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
  for (let y = ty; y < ty + T; y++) for (let x = tx; x < tx + T; x++) {
    const i = y * W + x; if (!ok[i]) continue;
    const xx = x + dx, yy = y + dy; if (xx < 1 || yy < 1 || xx >= W - 1 || yy >= H - 1) continue;
    const a = GI[i], b = GV[yy * W + xx];
    sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
  }
  if (n < 50) return -2;
  const cov = sab / n - sa * sb / n / n, va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
  return cov / Math.sqrt(va * vb + 1e-9);
}
const ok = new Uint8Array(N);
for (let i = 0; i < N; i++) ok[i] = A[i] === 1 && wS[i] < 0.5 ? 1 : 0; // rim and drum carry the structure
let GV = grad(lum(V0));
// global shift on a coarse grid of big tiles
let best = -2, gx = 0, gy = 0;
for (let dy = -12; dy <= 12; dy++) for (let dx = -12; dx <= 12; dx++) {
  let s = 0, n = 0;
  for (const [tx, ty] of [[300, 650], [1800, 650], [600, 950], [1500, 950], [1000, 1050], [900, 430], [1250, 430]]) { const v = nccAt(GV, tx, ty, 120, dx, dy, ok); if (v > -2) { s += v; n++; } }
  if (n && s / n > best) { best = s / n; gx = dx; gy = dy; }
}
console.log('global shift', gx, gy, 'ncc', best.toFixed(3));
// re-sample with the global shift: out(x, y) = variant(x + gx, y + gy) in frame px
V0 = O.upscaleShift(vimg, W, H, gx * (vimg.w === W ? 1 : 1), gy).c;
GV = grad(lum(V0));
// local shifts on 64 px tiles, step 32
const T = 64, ST = 32, RG = 5, samples = [];
for (let ty = 400; ty + T <= H; ty += ST) for (let tx = 220; tx + T <= 2010; tx += ST) {
  let cnt = 0; for (let y = ty; y < ty + T; y += 4) for (let x = tx; x < tx + T; x += 4) cnt += ok[y * W + x];
  if (cnt < (T / 4) * (T / 4) * 0.6) continue;
  let b = -2, bx = 0, by = 0; const S = {};
  for (let dy = -RG; dy <= RG; dy++) for (let dx = -RG; dx <= RG; dx++) { const v = nccAt(GV, tx, ty, T, dx, dy, ok); S[dx + ',' + dy] = v; if (v > b) { b = v; bx = dx; by = dy; } }
  if (b < 0.45 || Math.abs(bx) === RG || Math.abs(by) === RG) continue;
  const f = (dx, dy) => S[dx + ',' + dy], den = (p, q, r) => p - 2 * q + r;
  const sx = bx + (den(f(bx - 1, by), b, f(bx + 1, by)) < 0 ? (f(bx - 1, by) - f(bx + 1, by)) / (2 * den(f(bx - 1, by), b, f(bx + 1, by))) : 0);
  const sy = by + (den(f(bx, by - 1), b, f(bx, by + 1)) < 0 ? (f(bx, by - 1) - f(bx, by + 1)) / (2 * den(f(bx, by - 1), b, f(bx, by + 1))) : 0);
  samples.push({ x: tx + T / 2, y: ty + T / 2, dx: sx, dy: sy, q: b });
}
const kept = samples.filter(s => {
  const nb = samples.filter(t => Math.hypot(t.x - s.x, t.y - s.y) < 130);
  const md = k => { const v = nb.map(t => t[k]).sort((a, b) => a - b); return v[v.length >> 1]; };
  return Math.abs(s.dx - md('dx')) < 1.2 && Math.abs(s.dy - md('dy')) < 1.2;
});
const dxs = new Float32Array(N), dys = new Float32Array(N), wts = new Float32Array(N);
for (const s of kept) { const i = Math.round(s.y) * W + Math.round(s.x); dxs[i] = s.dx * s.q; dys[i] = s.dy * s.q; wts[i] = s.q; }
const bx = O.boxBlur(dxs, W, H, 24), by = O.boxBlur(dys, W, H, 24), bw = O.boxBlur(wts, W, H, 24);
const fx = new Float32Array(N), fy = new Float32Array(N), fw = new Float32Array(N);
for (let i = 0; i < N; i++) if (bw[i] > 1e-6) { fx[i] = bx[i] / bw[i]; fy[i] = by[i] / bw[i]; fw[i] = Math.min(1, bw[i] * 300); }
const DX = O.boxBlur(O.pushPull(fx, fw, W, H), W, H, 16), DY = O.boxBlur(O.pushPull(fy, fw, W, H), W, H, 16);
const V = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; for (let c = 0; c < 3; c++) V[c][i] = O.sample(V0[c], W, H, x + DX[i], y + DY[i]); }
let mx = 0, ms = 0; for (const s of kept) { const m = Math.hypot(s.dx, s.dy); mx = Math.max(mx, m); ms += m * m; }
console.log('local tiles', samples.length, 'kept', kept.length, 'residual shift rms', Math.sqrt(ms / Math.max(kept.length, 1)).toFixed(2), 'max', mx.toFixed(2));
// alignment quality after warping: NCC at zero shift on the kept tiles
let q0 = 0, q1 = 0, nq = 0;
const GVw = grad(lum(V));
for (const s of kept) { q0 += nccAt(GV, s.x - T / 2, s.y - T / 2, T, 0, 0, ok); q1 += nccAt(GVw, s.x - T / 2, s.y - T / 2, T, 0, 0, ok); nq++; }
console.log('mean tile NCC before warp', (q0 / nq).toFixed(3), 'after warp', (q1 / nq).toFixed(3));

// ---------- zone-wise colour ----------
const R = cfg.radius || 6;
function zoneLP(X, w) { const num = O.boxBlur(X.map((v, i) => v * w[i] * A[i]), W, H, R), den = O.boxBlur(w.map((v, i) => v * A[i]), W, H, R); return num.map((v, i) => (den[i] > 1e-4 ? v / den[i] : 0)); }
function gainFor(spec, w) {
  if (Array.isArray(spec)) return [0, 1, 2].map(c => new Float32Array(N).fill(spec[c]));
  return [0, 1, 2].map(c => { const lv = zoneLP(V[c], w), li = zoneLP(I[c], w); return lv.map((v, i) => clamp((v + 3) / (li[i] + 3), 0.05, 4)); });
}
const gW = gainFor(cfg.woodGain, wW);
// gold band + motifs: either a gain, or a two-colour palette map (cfg.goldPalette):
// { "from": [[ground rgb], [line rgb]], "to": [[ground rgb], [line rgb]] } — each pixel's position t
// between the Ivory's ground and line colours is kept, the colours swapped, and the Ivory's shading
// (luminance relative to the palette colour) re-applied.
let gG = null, goldMap = null;
if (cfg.goldPalette) {
  const [g0, l0] = cfg.goldPalette.from, [g1, l1] = cfg.goldPalette.to;
  const dv = [l0[0] - g0[0], l0[1] - g0[1], l0[2] - g0[2]], dd = dv[0] * dv[0] + dv[1] * dv[1] + dv[2] * dv[2];
  const lumv = c => 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
  goldMap = i => {
    const p = [I[0][i], I[1][i], I[2][i]];
    const t = clamp(((p[0] - g0[0]) * dv[0] + (p[1] - g0[1]) * dv[1] + (p[2] - g0[2]) * dv[2]) / dd, 0, 1);
    const base = [0, 1, 2].map(c => g0[c] + t * dv[c]), tgt = [0, 1, 2].map(c => g1[c] + t * (l1[c] - g1[c]));
    const sh = clamp((lumv(p) + 2) / (lumv(base) + 2), 0.25, 2.5);
    return tgt.map(v => v * sh);
  };
} else gG = gainFor(cfg.goldGain, wG);
// surface
let Sx;
if (cfg.surface === 'variant') {
  // mean colour correction of the variant's surface to the target
  let m = [0, 0, 0], n = 0;
  for (let i = 0; i < N; i++) if (wS[i] > 0.99 && A[i] === 1) { for (let c = 0; c < 3; c++) m[c] += V[c][i]; n++; }
  m = m.map(v => v / n);
  const k = cfg.surfaceTarget ? cfg.surfaceTarget.map((t, c) => t / m[c]) : [1, 1, 1];
  console.log('variant surface mean', m.map(v => v.toFixed(1)).join(','), 'gain to target', k.map(v => v.toFixed(3)).join(','));
  Sx = [0, 1, 2].map(c => V[c].map(v => clamp(v * k[c], 0, 255)));
} else {
  let m = [0, 0, 0], n = 0;
  for (let i = 0; i < N; i++) if (wS[i] > 0.99 && A[i] === 1) { for (let c = 0; c < 3; c++) m[c] += I[c][i]; n++; }
  m = m.map(v => v / n);
  const k = cfg.surfaceTarget.map((t, c) => t / m[c]);
  console.log('ivory surface mean', m.map(v => v.toFixed(1)).join(','), 'gain', k.map(v => v.toFixed(3)).join(','));
  Sx = [0, 1, 2].map(c => I[c].map(v => clamp(v * k[c], 0, 255)));
}
const out = P.create(W, H);
for (let i = 0; i < N; i++) {
  const gm = goldMap && wG[i] > 0 ? goldMap(i) : null;
  for (let c = 0; c < 3; c++) {
    const gv = gm ? gm[c] : (gG ? I[c][i] * gG[c][i] : 0);
    const v = wS[i] * Sx[c][i] + wG[i] * gv + wW[i] * I[c][i] * gW[c][i];
    out.data[i * 4 + c] = Math.round(clamp(v, 0, 255));
  }
  out.data[i * 4 + 3] = iv.data[i * 4 + 3];
}
P.write(outp, out, { level: 9 });
P.write(outp.replace(/\.png$/, '_aligned_variant.png'), O.toImage({ w: W, h: H, c: V }), { rgb: true, level: 3 });
console.log('wrote', outp);
