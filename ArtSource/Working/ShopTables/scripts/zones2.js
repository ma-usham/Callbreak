// Material zones of Table_HimalayanIvory, second version: the key band is found geometrically.
//   interior  flood fill of the ivory surface + holes (from zones.js: interior.bin)
//   band      per parameter angle t: from the interior edge out to the band's outer cream border line
//             (the last cream pixel within 70 px along the normal, + 2.5 px), median-smoothed
//   motif     gold-ish pixels inside the interior (the knot motifs)
//   surface   interior minus motifs
//   wood      everything else (lip and drum)
// node zones2.js <Table_HimalayanIvory.png> <zonesDir (reads interior.bin, writes zone2_*.bin)>
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , tp, zdir] = process.argv;
const t = P.read(tp);
const W = t.w, H = t.h, N = W * H, d = t.data;
const E = { cx: 1114.251, cy: 742.242, a: 780.172, b: 301.418 };
const interior = new Float32Array(fs.readFileSync(zdir + '/interior.bin').buffer.slice(0));
const A = new Float32Array(N); for (let i = 0; i < N; i++) A[i] = d[i * 4 + 3] / 255;
const px = (x, y) => { const xi = Math.round(x), yi = Math.round(y); if (xi < 0 || yi < 0 || xi >= W || yi >= H) return null; const i = (yi * W + xi) * 4; return [d[i], d[i + 1], d[i + 2], yi * W + xi]; };
const pt = (th, s) => [E.cx + s * E.a * Math.cos(th), E.cy + s * E.b * Math.sin(th)];
const dsPerPx = th => 1 / Math.hypot(E.a * Math.cos(th), E.b * Math.sin(th));
const cream = c => c && c[0] > 200 && c[1] > 135 && c[2] > 70 && (c[0] - c[2]) / c[0] < 0.68;
const K = 3600, sIn = new Float64Array(K), sOut = new Float64Array(K);
for (let k = 0; k < K; k++) {
  const th = k / K * 2 * Math.PI, ds = dsPerPx(th);
  // interior edge
  let s = 0.85;
  while (s < 1.2) { const c = px(...pt(th, s)); if (!c || !interior[c[3]]) break; s += ds * 0.25; }
  sIn[k] = s;
  // outer cream border of the band
  let last = s, gap = 0;
  for (let p = 0; p < 70; p += 0.5) {
    const c = px(...pt(th, s + p * ds));
    // the dark groove between the band and the lip ends the band
    if (c && p > 4 && 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2] < 70) break;
    if (cream(c)) { last = s + p * ds; gap = 0; } else { gap += 0.5; if (gap > 14 && p > 8) break; }
  }
  sOut[k] = last + 2.5 * ds;
}
// median filter over +-1.5 deg, then a light mean
const med = (arr, R) => { const o = new Float64Array(K); for (let k = 0; k < K; k++) { const w = []; for (let q = -R; q <= R; q++) w.push(arr[(k + q + K) % K]); w.sort((a, b) => a - b); o[k] = w[w.length >> 1]; } return o; };
const mean = (arr, R) => { const o = new Float64Array(K); for (let k = 0; k < K; k++) { let s = 0; for (let q = -R; q <= R; q++) s += arr[(k + q + K) % K]; o[k] = s / (2 * R + 1); } return o; };
// band width in px as a smooth function of angle: robust Fourier fit (8 harmonics, Tukey weights)
const widthPx = new Float64Array(K);
for (let k = 0; k < K; k++) widthPx[k] = (sOut[k] - sIn[k]) / dsPerPx(k / K * 2 * Math.PI);
const NH = 8, NB = 2 * NH + 1;
const basis = th => { const f = [1]; for (let h = 1; h <= NH; h++) { f.push(Math.cos(h * th), Math.sin(h * th)); } return f; };
function wfit(wts) {
  const M = Array.from({ length: NB }, () => new Float64Array(NB)), v = new Float64Array(NB);
  for (let k = 0; k < K; k++) { const f = basis(k / K * 2 * Math.PI), w = wts[k]; for (let a = 0; a < NB; a++) { v[a] += w * f[a] * widthPx[k]; for (let b = 0; b < NB; b++) M[a][b] += w * f[a] * f[b]; } }
  for (let i = 0; i < NB; i++) { let p = i; for (let q = i + 1; q < NB; q++) if (Math.abs(M[q][i]) > Math.abs(M[p][i])) p = q; [M[i], M[p]] = [M[p], M[i]]; [v[i], v[p]] = [v[p], v[i]]; for (let q = i + 1; q < NB; q++) { const f = M[q][i] / M[i][i]; for (let j = i; j < NB; j++) M[q][j] -= f * M[i][j]; v[q] -= f * v[i]; } }
  const c = new Float64Array(NB); for (let i = NB - 1; i >= 0; i--) { let s = v[i]; for (let j = i + 1; j < NB; j++) s -= M[i][j] * c[j]; c[i] = s / M[i][i]; }
  return c;
}
let wts = new Float64Array(K).fill(1), coef = wfit(wts);
const evalW = k => basis(k / K * 2 * Math.PI).reduce((a, f, j) => a + f * coef[j], 0);
for (let it = 0; it < 8; it++) {
  const res = []; for (let k = 0; k < K; k++) res.push(Math.abs(widthPx[k] - evalW(k)));
  const sorted = res.slice().sort((a, b) => a - b), mad = Math.max(sorted[K >> 1] * 1.4826, 0.5), c = 4.685 * mad;
  for (let k = 0; k < K; k++) { const u = res[k] / c; wts[k] = u < 1 ? (1 - u * u) ** 2 : 0; }
  coef = wfit(wts);
}
// interior edge: per angle, except where a knot motif touches the band (the flood stops at the motif):
// there the robust fit of the edge (px offset from the base ellipse) replaces it
const offPx = new Float64Array(K);
for (let k = 0; k < K; k++) offPx[k] = (sIn[k] - 1) / dsPerPx(k / K * 2 * Math.PI);
const saveW = widthPx.slice();
for (let k = 0; k < K; k++) widthPx[k] = offPx[k];
wts = new Float64Array(K).fill(1); coef = wfit(wts);
for (let it = 0; it < 8; it++) {
  const res = []; for (let k = 0; k < K; k++) res.push(Math.abs(widthPx[k] - evalW(k)));
  const sorted = res.slice().sort((a, b) => a - b), mad = Math.max(sorted[K >> 1] * 1.4826, 0.5), c = 4.685 * mad;
  for (let k = 0; k < K; k++) { const u = res[k] / c; wts[k] = u < 1 ? (1 - u * u) ** 2 : 0; }
  coef = wfit(wts);
}
const sInS = new Float64Array(K);
let replaced = 0;
for (let k = 0; k < K; k++) {
  const fit = evalW(k), ds = dsPerPx(k / K * 2 * Math.PI);
  if (Math.abs(offPx[k] - fit) > 3) { sInS[k] = 1 + fit * ds; replaced++; } else sInS[k] = sIn[k];
}
console.log('interior edge: angles replaced by the fit (motif contact)', replaced);
// back to the band-width fit
for (let k = 0; k < K; k++) widthPx[k] = saveW[k];
wts = new Float64Array(K).fill(1); coef = wfit(wts);
for (let it = 0; it < 8; it++) {
  const res = []; for (let k = 0; k < K; k++) res.push(Math.abs(widthPx[k] - evalW(k)));
  const sorted = res.slice().sort((a, b) => a - b), mad = Math.max(sorted[K >> 1] * 1.4826, 0.5), c = 4.685 * mad;
  for (let k = 0; k < K; k++) { const u = res[k] / c; wts[k] = u < 1 ? (1 - u * u) ** 2 : 0; }
  coef = wfit(wts);
}
const sOutS = new Float64Array(K);
// the band's outer edge: the raw per-angle width measured from the raw interior edge, smoothed by the fit;
// at motif-contact angles the raw width is relative to the motif, so use the fitted edge + fitted width
for (let k = 0; k < K; k++) sOutS[k] = sInS[k] + evalW(k) * dsPerPx(k / K * 2 * Math.PI);
let rr = 0, nr = 0; for (let k = 0; k < K; k++) if (wts[k] > 0.5) { rr += (widthPx[k] - evalW(k)) ** 2; nr++; }
console.log('band width fit: inliers', nr, 'of', K, 'rms', Math.sqrt(rr / nr).toFixed(2), 'px');
// band weight per pixel: elliptical coordinates (t, s) of each pixel near the rim
const band = new Float32Array(N);
// per pixel: inner (interior) / band / wood from elliptical coordinates; inner = flood interior OR
// inside the (motif-corrected) interior edge
const inner = new Float32Array(N);
for (let i = 0; i < N; i++) inner[i] = interior[i];
for (let y = 380; y < H; y++) for (let x = 230; x < 2000; x++) {
  const i = y * W + x; if (A[i] <= 0 || interior[i]) continue;
  const u = (x - E.cx) / E.a, v = (y - E.cy) / E.b;
  const s = Math.hypot(u, v); if (s > 1.25) continue;
  let th = Math.atan2(v, u); if (th < 0) th += 2 * Math.PI;
  const k = Math.round(th / (2 * Math.PI) * K) % K;
  const ds = dsPerPx(th);
  const inPx = (sInS[k] - s) / ds; // > 0 inside the interior edge
  const outPx = (sOutS[k] - s) / ds; // > 0 inside the band's outer edge
  const fin = Math.min(Math.max(inPx + 0.5, 0), 1);
  inner[i] = fin;
  band[i] = Math.min(Math.max(outPx + 0.5, 0), 1) * (1 - fin);
}
// motif: gold-ish pixels in the inner region
const sm = (e0, e1, v) => { const z = Math.min(Math.max((v - e0) / (e1 - e0), 0), 1); return z * z * (3 - 2 * z); };
const motif = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (inner[i] <= 0) continue;
  const r = d[i * 4], g = d[i * 4 + 1], b = d[i * 4 + 2];
  const sat = r > 0 ? (r - b) / r : 0, L = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  motif[i] = sm(0.42, 0.6, sat) * sm(95, 140, L) * sm(150, 190, r);
}
const motifS = O.boxBlur(motif, W, H, 1);
const zS = new Float32Array(N), zB = new Float32Array(N), zM = new Float32Array(N), zW = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (A[i] <= 0) continue;
  const m = Math.min(motifS[i] * 1.2, 1) * inner[i];
  zM[i] = m; zS[i] = inner[i] - m; zB[i] = band[i]; zW[i] = Math.max(0, 1 - inner[i] - band[i]);
}
// soften the interior/band seam by 1 px
const names = { surface: zS, band: zB, motif: zM, wood: zW };
for (const [n, z] of Object.entries(names)) fs.writeFileSync(zdir + '/zone2_' + n + '.bin', Buffer.from(z.buffer));
const pv = P.create(W, H);
for (let i = 0; i < N; i++) {
  const c = [zS[i] * 60 + zB[i] * 250 + zM[i] * 255 + zW[i] * 110, zS[i] * 120 + zB[i] * 210 + zM[i] * 80 + zW[i] * 60, zS[i] * 255 + zB[i] * 40 + zM[i] * 200 + zW[i] * 30];
  for (let k = 0; k < 3; k++) pv.data[i * 4 + k] = Math.round(c[k] * A[i] + 255 * (1 - A[i]));
  pv.data[i * 4 + 3] = 255;
}
P.write(zdir + '/zones2_preview.png', pv, { rgb: true, level: 3 });
const widths = [0, 45, 90, 135, 180, 225, 270, 315].map(deg => { const k = Math.round(deg / 360 * K) % K; return deg + ':' + ((sOutS[k] - sIn[k]) / dsPerPx(k / K * 2 * Math.PI)).toFixed(1) + 'px'; });
console.log('band width by angle (y down):', widths.join(' '));
let a = [0, 0, 0, 0]; for (let i = 0; i < N; i++) { a[0] += zS[i] * A[i]; a[1] += zB[i] * A[i]; a[2] += zM[i] * A[i]; a[3] += zW[i] * A[i]; }
console.log('areas surface', a[0].toFixed(0), 'band', a[1].toFixed(0), 'motif', a[2].toFixed(0), 'wood', a[3].toFixed(0));
