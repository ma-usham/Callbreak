// Outer-edge tracer, second attempt. Works per parameter angle t along the elliptical normal:
//  1) s_in: where the ivory first meets the orange key band (median-filtered so motifs drop out)
//  2) groove: darkest point just outside the band
//  3) outer edge: DP over the window [groove + 2 px, groove + maxLip] maximising the colour step,
//     with a bias toward the outermost step and a smoothness penalty.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , inp, outj, t0s, t1s] = process.argv;
const E = { cx: 1114.251, cy: 742.242, a: 780.172, b: 301.418 };
const im = P.read(inp);
const T0 = +(t0s || 175), T1 = +(t1s || 365), DT = 0.1;
function samp(x, y, c) {
  x = Math.min(Math.max(x, 0), im.w - 1.001); y = Math.min(Math.max(y, 0), im.h - 1.001);
  const x0 = Math.floor(x), y0 = Math.floor(y), tx = x - x0, ty = y - y0, i = (y0 * im.w + x0) * 4;
  return (im.data[i + c] * (1 - tx) + im.data[i + 4 + c] * tx) * (1 - ty) + (im.data[i + im.w * 4 + c] * (1 - tx) + im.data[i + im.w * 4 + 4 + c] * tx) * ty;
}
const rgb = (x, y) => [samp(x, y, 0), samp(x, y, 1), samp(x, y, 2)];
const pt = (t, s) => [E.cx + s * E.a * Math.cos(t), E.cy + s * E.b * Math.sin(t)];
// ds per pixel along the normal at t
const dsPerPx = t => 1 / Math.hypot(E.a * Math.cos(t), E.b * Math.sin(t));
const cols = Math.round((T1 - T0) / DT) + 1;
const sIn = new Float64Array(cols);
for (let k = 0; k < cols; k++) {
  const t = (T0 + k * DT) * Math.PI / 180, d = dsPerPx(t);
  let hit = 0.9;
  for (let s = 0.9; s < 1.1; s += d * 0.25) {
    const [x, y] = pt(t, s); const [r, g, b] = rgb(x, y);
    if (r > 120 && (r - b) / r > 0.55 && g / r < 0.75) {
      const [x2, y2] = pt(t, s + d * 1.5); const [r2, g2, b2] = rgb(x2, y2);
      if (r2 > 120 && (r2 - b2) / r2 > 0.5) { hit = s; break; }
    }
  }
  sIn[k] = hit;
}
// median filter over +-4 degrees, then take the max of (median, raw) only where raw is close
const med = new Float64Array(cols), R = Math.round(4 / DT);
for (let k = 0; k < cols; k++) { const w = []; for (let q = Math.max(0, k - R); q <= Math.min(cols - 1, k + R); q++) w.push(sIn[q]); w.sort((a, b) => a - b); med[k] = w[Math.floor(w.length * 0.6)]; }
// groove: darkest luminance within s_in + [3 px, 50 px] scanning outward (first strong minimum)
const lum = (x, y) => { const [r, g, b] = rgb(x, y); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
const groove = new Float64Array(cols);
for (let k = 0; k < cols; k++) {
  const t = (T0 + k * DT) * Math.PI / 180, d = dsPerPx(t);
  // the band is ~12-40 px wide; groove is the first point after the band where luminance < 70
  let gs = med[k] + d * 12, best = 1e9;
  for (let p = 6; p < 70; p += 0.5) {
    const s = med[k] + d * p; const [x, y] = pt(t, s); const L = lum(x, y);
    if (L < best) { best = L; gs = s; }
    if (best < 60 && L > best + 60) break; // passed the groove
  }
  groove[k] = gs;
}
// edge strength map in the window [groove + 2px, groove + 60px]
const NP = 120; // samples at 0.5 px
const cost = new Float32Array(cols * NP);
for (let k = 0; k < cols; k++) {
  const t = (T0 + k * DT) * Math.PI / 180, d = dsPerPx(t);
  const prof = [];
  for (let p = 0; p < NP + 8; p++) { const s = groove[k] + d * (2 + p * 0.5 - 2); const [x, y] = pt(t, s); prof.push(rgb(x, y)); }
  for (let j = 0; j < NP; j++) {
    const jj = j + 4;
    let a = [0, 0, 0], b = [0, 0, 0];
    for (let q = 1; q <= 4; q++) for (let c = 0; c < 3; c++) { a[c] += prof[jj - q][c] / 4; b[c] += prof[Math.min(jj + q, prof.length - 1)][c] / 4; }
    const diff = Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
    cost[k * NP + j] = -diff - 0.35 * j; // bias toward the outermost strong step
  }
}
const J = 3, lambda = 4;
const acc = new Float32Array(cols * NP), from = new Int16Array(cols * NP);
for (let j = 0; j < NP; j++) acc[j] = cost[j];
for (let k = 1; k < cols; k++) for (let j = 0; j < NP; j++) {
  let best = Infinity, bj = j;
  for (let dd = -J; dd <= J; dd++) { const jj = j + dd; if (jj < 0 || jj >= NP) continue; const v = acc[(k - 1) * NP + jj] + lambda * Math.abs(dd); if (v < best) { best = v; bj = jj; } }
  acc[k * NP + j] = best + cost[k * NP + j]; from[k * NP + j] = bj;
}
let bj = 0, bv = Infinity;
for (let j = 0; j < NP; j++) if (acc[(cols - 1) * NP + j] < bv) { bv = acc[(cols - 1) * NP + j]; bj = j; }
const path = new Array(cols);
for (let k = cols - 1; k >= 0; k--) { path[k] = bj; bj = from[k * NP + bj]; }
const res = [];
for (let k = 0; k < cols; k++) {
  const t = (T0 + k * DT) * Math.PI / 180, d = dsPerPx(t);
  const s = groove[k] + d * (2 + path[k] * 0.5);
  const [x, y] = pt(t, s), [xi, yi] = pt(t, med[k]), [xg, yg] = pt(t, groove[k]);
  res.push({ t: +(T0 + k * DT).toFixed(1), s: +s.toFixed(5), x: +x.toFixed(2), y: +y.toFixed(2), xin: +xi.toFixed(2), yin: +yi.toFixed(2), xg: +xg.toFixed(2), yg: +yg.toFixed(2) });
}
fs.writeFileSync(outj, JSON.stringify(res));
for (let k = 0; k < cols; k += 50) console.log(res[k].t, 'out', res[k].x, res[k].y, 'in', res[k].xin, res[k].yin, 'groove', res[k].xg, res[k].yg);
