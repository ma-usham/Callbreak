// Material variant of the gameplay table with IDENTICAL geometry to Table_HimalayanIvory.
//
// node variant2.js <Ivory.png> <zonesDir> <chatgptVariant.png> <out.png> <config.json> [report.json]
//
// The ChatGPT variant (an edit of the Ivory table on #00FF00, 1672 x 941) is upscaled to the frame and
// aligned to the Ivory: a global shift, then a smooth field of local shifts measured on rim and drum
// tiles (gradient NCC). Then, per material zone of the Ivory (zones.js):
//   surface  colour = target x the Ivory's low-frequency shading; texture = the variant's detail
//            (variant minus its own low-pass), scaled to the target's luminance
//   gold     (band + knot motifs) the variant's aligned pixels, re-coloured by a two-colour palette map
//            (the variant's own ground/line colours -> the target ground/line colours, shading kept)
//   wood     (lip + drum) the Ivory's own pixels x a per-channel gain, so the carving is the Ivory's
// Alpha is the Ivory's alpha (TableMask) everywhere.
//
// config: { "surface": [r,g,b], "surfaceShadeRadius": 60, "surfaceDetailRadius": 40,
//           "band":  { "to": [[ground rgb], [line rgb]] },
//           "motif": { "to": [[ground rgb], [line rgb]] },
//           "woodGain": [r,g,b] }
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , ivp, zdir, varp, outp, cfgp, repp] = process.argv;
const cfg = JSON.parse(fs.readFileSync(cfgp, 'utf8'));
const iv = P.read(ivp);
const W = iv.w, H = iv.h, N = W * H;
const I = O.planes(iv).c;
const A = new Float32Array(N);
for (let i = 0; i < N; i++) A[i] = iv.data[i * 4 + 3] / 255;
const rd = n => new Float32Array(fs.readFileSync(zdir + '/' + n).buffer.slice(0));
const wS = rd('zone2_surface.bin'), wB = rd('zone2_band.bin'), wM = rd('zone2_motif.bin'), wW = rd('zone2_wood.bin');
const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const lum3 = (r, g, b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
const report = {};

// ---------------- alignment ----------------
const vimg = P.read(varp);
const lum = c => { const L = new Float32Array(N); for (let i = 0; i < N; i++) L[i] = lum3(c[0][i], c[1][i], c[2][i]); return L; };
const grad = L => { const g = new Float32Array(N); for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; g[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + W] - L[i - W]); } return g; };
const GI = grad(lum(I));
const ok = new Uint8Array(N);
for (let i = 0; i < N; i++) ok[i] = A[i] === 1 && wB[i] + wW[i] > 0.5 ? 1 : 0;
function ncc(GV, tx, ty, T, dx, dy) {
  let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
  for (let y = ty; y < ty + T; y++) for (let x = tx; x < tx + T; x++) {
    const i = y * W + x; if (!ok[i]) continue;
    const xx = x + dx, yy = y + dy; if (xx < 1 || yy < 1 || xx >= W - 1 || yy >= H - 1) continue;
    const a = GI[i], b = GV[yy * W + xx]; sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
  }
  if (n < 50) return -2;
  const cov = sab / n - sa * sb / n / n, va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
  return cov / Math.sqrt(va * vb + 1e-9);
}
let V0 = O.upscaleShift(vimg, W, H, 0, 0).c;
let GV = grad(lum(V0));
let best = -2, gx = 0, gy = 0;
const bigTiles = [[300, 650], [1800, 650], [600, 950], [1500, 950], [1000, 1050], [900, 430], [1250, 430]];
for (let dy = -12; dy <= 12; dy++) for (let dx = -12; dx <= 12; dx++) {
  let s = 0, n = 0;
  for (const [tx, ty] of bigTiles) { const v = ncc(GV, tx, ty, 120, dx, dy); if (v > -2) { s += v; n++; } }
  if (n && s / n > best) { best = s / n; gx = dx; gy = dy; }
}
report.globalShift = [gx, gy];
V0 = O.upscaleShift(vimg, W, H, gx, gy).c;
GV = grad(lum(V0));
const T = 64, ST = 32, RG = 5, samples = [];
for (let ty = 400; ty + T <= H; ty += ST) for (let tx = 220; tx + T <= 2010; tx += ST) {
  let cnt = 0; for (let y = ty; y < ty + T; y += 4) for (let x = tx; x < tx + T; x += 4) cnt += ok[y * W + x];
  if (cnt < (T / 4) * (T / 4) * 0.6) continue;
  let b = -2, bx = 0, by = 0; const S = {};
  for (let dy = -RG; dy <= RG; dy++) for (let dx = -RG; dx <= RG; dx++) { const v = ncc(GV, tx, ty, T, dx, dy); S[dx + ',' + dy] = v; if (v > b) { b = v; bx = dx; by = dy; } }
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
const bxs = O.boxBlur(dxs, W, H, 24), bys = O.boxBlur(dys, W, H, 24), bws = O.boxBlur(wts, W, H, 24);
const fx = new Float32Array(N), fy = new Float32Array(N), fw = new Float32Array(N);
for (let i = 0; i < N; i++) if (bws[i] > 1e-6) { fx[i] = bxs[i] / bws[i]; fy[i] = bys[i] / bws[i]; fw[i] = Math.min(1, bws[i] * 300); }
const DX = O.boxBlur(O.pushPull(fx, fw, W, H), W, H, 16), DY = O.boxBlur(O.pushPull(fy, fw, W, H), W, H, 16);
const V = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; for (let c = 0; c < 3; c++) V[c][i] = O.sample(V0[c], W, H, x + DX[i], y + DY[i]); }
const GVw = grad(lum(V));
// residual check: re-measure shifts after warping on the kept tiles
const resid = [];
for (const s of kept) {
  let b = -2, bx = 0, by = 0;
  for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) { const v = ncc(GVw, s.x - T / 2, s.y - T / 2, T, dx, dy); if (v > b) { b = v; bx = dx; by = dy; } }
  resid.push(Math.hypot(bx, by));
}
let fmax = 0; for (let i = 0; i < N; i++) if (A[i] > 0) fmax = Math.max(fmax, Math.hypot(DX[i], DY[i]));
report.alignment = {
  tiles: samples.length, kept: kept.length,
  measuredShiftRms: +Math.sqrt(kept.reduce((a, s) => a + s.dx * s.dx + s.dy * s.dy, 0) / kept.length).toFixed(2),
  maxFieldShift: +fmax.toFixed(2),
  residualAfterWarp: { zero: resid.filter(r => r === 0).length, one: resid.filter(r => r > 0 && r <= 1.5).length, more: resid.filter(r => r > 1.5).length },
  meanTileNccBefore: +(kept.reduce((a, s) => a + ncc(GV, s.x - T / 2, s.y - T / 2, T, 0, 0), 0) / kept.length).toFixed(3),
  meanTileNccAfter: +(kept.reduce((a, s) => a + ncc(GVw, s.x - T / 2, s.y - T / 2, T, 0, 0), 0) / kept.length).toFixed(3),
};
// ChatGPT's own silhouette vs TableMask (how well it kept the shape), after alignment
let inter = 0, uni = 0;
for (let i = 0; i < N; i++) {
  const r = V[0][i], g = V[1][i], b = V[2][i];
  const vin = !(g > 170 && r < 120 && b < 120 && g - Math.max(r, b) > 90) ? 1 : 0;
  const tin = A[i] >= 0.5 ? 1 : 0;
  if (vin && tin) inter++; if (vin || tin) uni++;
}
report.chatgptSilhouetteIoU = +(inter / uni).toFixed(4);

// ---------------- normalised low-pass inside a zone ----------------
function zoneLP(X, w, R) {
  const num = O.boxBlur(X.map((v, i) => v * w[i]), W, H, R), den = O.boxBlur(w, W, H, R);
  return { v: num.map((v, i) => (den[i] > 1e-4 ? v / den[i] : 0)), d: den };
}
const wSA = wS.map((v, i) => (v > 0.9 && A[i] === 1 ? 1 : 0));

// ---------------- surface ----------------
const tS = cfg.surface;
const shR = cfg.surfaceShadeRadius || 60, dtR = cfg.surfaceDetailRadius || 40;
const LI = lum(I);
let mI = 0, nI = 0; for (let i = 0; i < N; i++) if (wSA[i]) { mI += LI[i]; nI++; } mI /= nI;
const shadeLP = zoneLP(LI, wSA, shR);
const shade = O.pushPull(shadeLP.v.map(v => v / mI), shadeLP.d.map(v => (v > 0.05 ? 1 : 0)), W, H);
const mV = [0, 0, 0]; for (let i = 0; i < N; i++) if (wSA[i]) for (let c = 0; c < 3; c++) mV[c] += V[c][i]; for (let c = 0; c < 3; c++) mV[c] /= nI;
const sDetail = clamp(lum3(...tS) / Math.max(lum3(...mV), 1), 0.4, 2.5);
const vLP = [0, 1, 2].map(c => { const z = zoneLP(V[c], wSA, dtR); return O.pushPull(z.v, z.d.map(v => (v > 0.05 ? 1 : 0)), W, H); });
report.surface = { variantMean: mV.map(v => +v.toFixed(1)), target: tS, detailScale: +sDetail.toFixed(3) };
const Sx = [0, 1, 2].map(c => new Float32Array(N));
for (let i = 0; i < N; i++) for (let c = 0; c < 3; c++) Sx[c][i] = tS[c] * shade[i] + (V[c][i] - vLP[c][i]) * sDetail;

// ---------------- gold (band + motifs): palette map of the variant's own pixels ----------------
function kmeans2(sel) {
  const px = [];
  for (let i = 0; i < N; i++) if (sel(i)) px.push([V[0][i], V[1][i], V[2][i]]);
  // init: darkest and brightest 10 %
  px.sort((a, b) => lum3(...a) - lum3(...b));
  const q = k => px[Math.floor(px.length * k)];
  let c = [q(0.1).slice(), q(0.9).slice()];
  for (let it = 0; it < 30; it++) {
    const s = [[0, 0, 0, 0], [0, 0, 0, 0]];
    for (const p of px) { const d0 = (p[0] - c[0][0]) ** 2 + (p[1] - c[0][1]) ** 2 + (p[2] - c[0][2]) ** 2, d1 = (p[0] - c[1][0]) ** 2 + (p[1] - c[1][1]) ** 2 + (p[2] - c[1][2]) ** 2; const k = d0 <= d1 ? 0 : 1; s[k][0] += p[0]; s[k][1] += p[1]; s[k][2] += p[2]; s[k][3]++; }
    c = s.map((v, k) => (v[3] ? [v[0] / v[3], v[1] / v[3], v[2] / v[3]] : c[k]));
  }
  return { c, n: px.length };
}
function paletteMap(from, to) {
  const [g0, l0] = from, [g1, l1] = to;
  const dv = [l0[0] - g0[0], l0[1] - g0[1], l0[2] - g0[2]], dd = Math.max(dv[0] ** 2 + dv[1] ** 2 + dv[2] ** 2, 1);
  return (r, g, b) => {
    const t = clamp(((r - g0[0]) * dv[0] + (g - g0[1]) * dv[1] + (b - g0[2]) * dv[2]) / dd, -0.15, 1.15);
    const base = [g0[0] + t * dv[0], g0[1] + t * dv[1], g0[2] + t * dv[2]];
    const sh = clamp((lum3(r, g, b) + 3) / (lum3(...base) + 3), 0.3, 2.2);
    const tc = clamp(t, 0, 1);
    return [0, 1, 2].map(k => (g1[k] + tc * (l1[k] - g1[k])) * sh);
  };
}
// the variant's ground = the colour nearer the target ground; line = the other
function orient(km, to) {
  const [a, b] = km.c, g = to[0];
  const da = (a[0] - g[0]) ** 2 + (a[1] - g[1]) ** 2 + (a[2] - g[2]) ** 2, db = (b[0] - g[0]) ** 2 + (b[1] - g[1]) ** 2 + (b[2] - g[2]) ** 2;
  return da <= db ? [a, b] : [b, a];
}
const kmBand = kmeans2(i => wB[i] > 0.9 && A[i] === 1);
const kmMotif = kmeans2(i => wM[i] > 0.6 && A[i] === 1);
const fromBand = orient(kmBand, cfg.band.to), fromMotif = orient(kmMotif, cfg.motif.to);
report.band = { variantGround: fromBand[0].map(v => +v.toFixed(0)), variantLine: fromBand[1].map(v => +v.toFixed(0)), to: cfg.band.to };
report.motif = { variantGround: fromMotif[0].map(v => +v.toFixed(0)), variantLine: fromMotif[1].map(v => +v.toFixed(0)), to: cfg.motif.to };
const mapBand = paletteMap(fromBand, cfg.band.to), mapMotif = paletteMap(fromMotif, cfg.motif.to);

// ---------------- compose ----------------
const gW = cfg.woodGain;
const out = P.create(W, H);
for (let i = 0; i < N; i++) {
  if (A[i] <= 0) { out.data[i * 4 + 3] = 0; continue; }
  const gb = wB[i] > 0 ? mapBand(V[0][i], V[1][i], V[2][i]) : [0, 0, 0], gm = wM[i] > 0 ? mapMotif(V[0][i], V[1][i], V[2][i]) : [0, 0, 0];
  for (let c = 0; c < 3; c++) {
    const v = wS[i] * Sx[c][i] + wB[i] * gb[c] + wM[i] * gm[c] + wW[i] * I[c][i] * gW[c];
    out.data[i * 4 + c] = Math.round(clamp(v, 0, 255));
  }
  out.data[i * 4 + 3] = iv.data[i * 4 + 3];
}
P.write(outp, out, { level: 9 });
P.write(outp.replace(/\.png$/, '_aligned.png'), O.toImage({ w: W, h: H, c: V }), { rgb: true, level: 3 });
if (repp) fs.writeFileSync(repp, JSON.stringify(report, null, 1));
console.log(JSON.stringify(report));
