// Walks rays outward from the table centre and records where the ivory surface first meets the gold
// key band (first run of strongly orange pixels). Fits a general conic (rotated ellipse) robustly.
'use strict';
const P = require('./png.js');
const im = P.read(process.argv[2]);
const W = im.w, H = im.h, d = im.data;
const C0 = [1112, 743];
function orange(x, y) {
  const xi = Math.round(x), yi = Math.round(y);
  if (xi < 0 || yi < 0 || xi >= W || yi >= H) return false;
  const i = (yi * W + xi) * 4, r = d[i], g = d[i + 1], b = d[i + 2];
  return r > 120 && (r - b) / r > 0.55 && g / r < 0.75;
}
const pts = [];
const K = 1440;
for (let k = 0; k < K; k++) {
  const th = (k / K) * 2 * Math.PI, ux = Math.cos(th), uy = Math.sin(th);
  let hit = -1;
  for (let r = 150; r < 1000; r += 0.25) {
    const x = C0[0] + ux * r, y = C0[1] + uy * r;
    if (orange(x, y) && orange(x + ux * 0.75, y + uy * 0.75) && orange(x + ux * 1.5, y + uy * 1.5)) { hit = r; break; }
  }
  if (hit > 0) pts.push({ k, th, x: C0[0] + ux * hit, y: C0[1] + uy * hit });
}
// general conic fit: x^2 + B xy + C y^2 + D x + E y + F = 0 (A fixed to 1) via least squares
function solve(M, v) {
  const n = v.length;
  M = M.map(r => r.slice()); v = v.slice();
  for (let i = 0; i < n; i++) {
    let piv = i; for (let k = i + 1; k < n; k++) if (Math.abs(M[k][i]) > Math.abs(M[piv][i])) piv = k;
    [M[i], M[piv]] = [M[piv], M[i]]; [v[i], v[piv]] = [v[piv], v[i]];
    for (let k = i + 1; k < n; k++) { const f = M[k][i] / M[i][i]; for (let j = i; j < n; j++) M[k][j] -= f * M[i][j]; v[k] -= f * v[i]; }
  }
  const s = new Array(n).fill(0);
  for (let i = n - 1; i >= 0; i--) { let t = v[i]; for (let j = i + 1; j < n; j++) t -= M[i][j] * s[j]; s[i] = t / M[i][i]; }
  return s;
}
function fitConic(ps) {
  const n = 5, M = Array.from({ length: n }, () => new Array(n).fill(0)), v = new Array(n).fill(0);
  for (const p of ps) {
    const x = (p.x - 1112) / 500, y = (p.y - 743) / 500;
    const r = [x * y, y * y, x, y, 1], t = -x * x;
    for (let i = 0; i < n; i++) { v[i] += r[i] * t; for (let j = 0; j < n; j++) M[i][j] += r[i] * r[j]; }
  }
  const [B, C, D, E, F] = solve(M, v);
  return { A: 1, B, C, D, E, F };
}
// geometric parameters from conic (normalized coords)
function params(c) {
  const { A, B, C, D, E, F } = c;
  const den = B * B - 4 * A * C;
  const x0 = (2 * C * D - B * E) / den, y0 = (2 * A * E - B * D) / den;
  const num = 2 * (A * E * E + C * D * D - B * D * E + den * F);
  const s = Math.sqrt((A - C) * (A - C) + B * B);
  const a = -Math.sqrt(num * (A + C + s)) / den, b = -Math.sqrt(num * (A + C - s)) / den;
  const ang = B === 0 ? (A < C ? 0 : Math.PI / 2) : Math.atan2(C - A - s, B);
  return { cx: x0 * 500 + 1112, cy: y0 * 500 + 743, a: a * 500, b: b * 500, angDeg: ang * 180 / Math.PI };
}
function dist(c, p) { // algebraic distance scaled to px approx via gradient
  const x = (p.x - 1112) / 500, y = (p.y - 743) / 500;
  const f = x * x + c.B * x * y + c.C * y * y + c.D * x + c.E * y + c.F;
  const gx = 2 * x + c.B * y + c.D, gy = c.B * x + 2 * c.C * y + c.E;
  return f / Math.hypot(gx, gy) * 500;
}
let use = pts.slice(), c = fitConic(use);
for (let it = 0; it < 6; it++) {
  const rs = use.map(p => Math.abs(dist(c, p))).sort((a, b) => a - b);
  const thr = Math.max(1.0, rs[Math.floor(rs.length * 0.7)] * 2);
  use = pts.filter(p => Math.abs(dist(c, p)) < thr);
  c = fitConic(use);
  const pr = params(c);
  console.log('iter', it, 'kept', use.length, '/', pts.length, 'thr', thr.toFixed(2), JSON.stringify(Object.fromEntries(Object.entries(pr).map(([k, v]) => [k, +v.toFixed(3)]))));
}
const rs = use.map(p => dist(c, p));
console.log('rms', Math.sqrt(rs.reduce((a, b) => a + b * b, 0) / rs.length).toFixed(3));
const sect = {};
for (const p of pts) { const s = Math.floor((p.th * 180 / Math.PI) / 15) * 15; (sect[s] = sect[s] || []).push(dist(c, p)); }
console.log(Object.keys(sect).map(k => k + ':' + (sect[k].sort((a, b) => a - b)[Math.floor(sect[k].length / 2)]).toFixed(1)).join(' '));
console.log('CONIC', JSON.stringify(c));
