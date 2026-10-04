// Segments the ivory playing surface by flood fill from the centre and fits an ellipse to its
// outer contour (the inner edge of the gold key band). Prints the fit and residuals.
'use strict';
const P = require('./png.js');
const BG = process.argv[2];
const OUT = process.argv[3];
const im = P.read(BG);
const W = im.w, H = im.h, N = W * H;
const d = im.data;
// reference colour: median-ish of a central patch
let sr = 0, sg = 0, sb = 0, n = 0;
for (let y = 700; y < 780; y++) for (let x = 1050; x < 1180; x++) { const i = (y * W + x) * 4; sr += d[i]; sg += d[i + 1]; sb += d[i + 2]; n++; }
const ref = [sr / n, sg / n, sb / n];
console.log('ivory ref', ref.map(v => v.toFixed(0)).join(','));
const tol = +(process.argv[4] || 40);
const mask = new Uint8Array(N);
const stack = [740 * W + 1115];
mask[stack[0]] = 1;
// fill using a local + global colour criterion (the surface has a soft gradient)
while (stack.length) {
  const p = stack.pop();
  const x = p % W, y = (p / W) | 0;
  const i = p * 4;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    const nx = x + dx, ny = y + dy;
    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
    const q = ny * W + nx;
    if (mask[q]) continue;
    const j = q * 4;
    const dg = Math.abs(d[j] - ref[0]) + Math.abs(d[j + 1] - ref[1]) + Math.abs(d[j + 2] - ref[2]);
    const dl = Math.abs(d[j] - d[i]) + Math.abs(d[j + 1] - d[i + 1]) + Math.abs(d[j + 2] - d[i + 2]);
    if (dg < tol * 2.2 && dl < tol * 0.6) { mask[q] = 1; stack.push(q); }
  }
}
let area = 0, minx = W, maxx = 0, miny = H, maxy = 0;
for (let p = 0; p < N; p++) if (mask[p]) { area++; const x = p % W, y = (p / W) | 0; if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
console.log('area', area, 'bbox', minx, miny, maxx, maxy);
// outer contour: for each angle from the centre, the farthest mask pixel along the ray before a gap > 6px
const cx0 = (minx + maxx) / 2, cy0 = (miny + maxy) / 2;
const pts = [];
for (let k = 0; k < 720; k++) {
  const th = (k / 720) * 2 * Math.PI;
  const ux = Math.cos(th), uy = Math.sin(th);
  let last = 0, gap = 0;
  for (let r = 0; r < 1200; r += 0.5) {
    const x = Math.round(cx0 + ux * r), y = Math.round(cy0 + uy * r);
    if (x < 0 || y < 0 || x >= W || y >= H) break;
    if (mask[y * W + x]) { last = r; gap = 0; } else { gap += 0.5; if (gap > 8) break; }
  }
  pts.push({ th, x: cx0 + ux * last, y: cy0 + uy * last });
}
// algebraic ellipse fit (axis-aligned): A x^2 + C y^2 + D x + E y = 1, solved by least squares
function fit(ps) {
  const M = [[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]], v = [0, 0, 0, 0];
  for (const p of ps) {
    const x = p.x - 1115, y = p.y - 740;
    const r = [x * x, y * y, x, y];
    for (let i = 0; i < 4; i++) { v[i] += r[i]; for (let j = 0; j < 4; j++) M[i][j] += r[i] * r[j]; }
  }
  // gaussian elimination
  for (let i = 0; i < 4; i++) {
    let piv = i; for (let k = i + 1; k < 4; k++) if (Math.abs(M[k][i]) > Math.abs(M[piv][i])) piv = k;
    [M[i], M[piv]] = [M[piv], M[i]]; [v[i], v[piv]] = [v[piv], v[i]];
    for (let k = i + 1; k < 4; k++) { const f = M[k][i] / M[i][i]; for (let j = i; j < 4; j++) M[k][j] -= f * M[i][j]; v[k] -= f * v[i]; }
  }
  const s = [0, 0, 0, 0];
  for (let i = 3; i >= 0; i--) { let t = v[i]; for (let j = i + 1; j < 4; j++) t -= M[i][j] * s[j]; s[i] = t / M[i][i]; }
  const [A, C, D, E] = s;
  const cx = -D / (2 * A), cy = -E / (2 * C);
  const F = 1 + A * cx * cx + C * cy * cy;
  return { cx: cx + 1115, cy: cy + 740, a: Math.sqrt(F / A), b: Math.sqrt(F / C) };
}
function resid(e, p) { // approx distance: radial
  const dx = p.x - e.cx, dy = p.y - e.cy;
  const t = Math.atan2(dy / e.b, dx / e.a);
  const ex = e.cx + e.a * Math.cos(t), ey = e.cy + e.b * Math.sin(t);
  const r1 = Math.hypot(dx, dy), r0 = Math.hypot(ex - e.cx, ey - e.cy);
  return r1 - r0;
}
let use = pts.slice();
let e = fit(use);
for (let it = 0; it < 4; it++) {
  const rs = use.map(p => Math.abs(resid(e, p))).sort((a, b) => a - b);
  const thr = Math.max(2, rs[Math.floor(rs.length * 0.8)] * 1.5);
  use = pts.filter(p => Math.abs(resid(e, p)) < thr);
  e = fit(use);
  console.log('iter', it, 'kept', use.length, 'thr', thr.toFixed(2), 'ellipse', e.cx.toFixed(2), e.cy.toFixed(2), e.a.toFixed(2), e.b.toFixed(2));
}
// residual by angle sectors
const sect = {};
for (const p of pts) { const s = Math.floor(((Math.atan2(p.y - e.cy, p.x - e.cx) * 180 / Math.PI) + 360) % 360 / 15) * 15; (sect[s] = sect[s] || []).push(resid(e, p)); }
console.log(Object.keys(sect).map(k => k + ':' + (sect[k].reduce((a, b) => a + b, 0) / sect[k].length).toFixed(1)).join(' '));
if (OUT) {
  const o = P.create(W, H);
  for (let p = 0; p < N; p++) { o.data[p * 4] = o.data[p * 4 + 1] = o.data[p * 4 + 2] = 255; o.data[p * 4 + 3] = mask[p] ? 255 : 0; }
  P.write(OUT, o, { level: 3 });
}
console.log(JSON.stringify(e));
