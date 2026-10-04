// Polygon rasterisation with anti-aliasing (16 sub-rows per pixel, exact horizontal coverage).
// fillPolygon(w, h, pts) -> Float32Array coverage 0..1 (even-odd rule)
'use strict';
function fillPolygon(w, h, pts, sub) {
  sub = sub || 16;
  const cov = new Float32Array(w * h);
  const n = pts.length;
  let minY = Infinity, maxY = -Infinity;
  for (const p of pts) { if (p[1] < minY) minY = p[1]; if (p[1] > maxY) maxY = p[1]; }
  const y0 = Math.max(0, Math.floor(minY)), y1 = Math.min(h - 1, Math.ceil(maxY));
  const xs = [];
  for (let y = y0; y <= y1; y++) {
    for (let s = 0; s < sub; s++) {
      const sy = y + (s + 0.5) / sub;
      xs.length = 0;
      for (let i = 0; i < n; i++) {
        const [ax, ay] = pts[i], [bx, by] = pts[(i + 1) % n];
        if ((ay <= sy && by > sy) || (by <= sy && ay > sy)) xs.push(ax + (sy - ay) / (by - ay) * (bx - ax));
      }
      xs.sort((a, b) => a - b);
      for (let k = 0; k + 1 < xs.length; k += 2) {
        let xa = Math.max(xs[k], 0), xb = Math.min(xs[k + 1], w);
        if (xb <= xa) continue;
        const ia = Math.floor(xa), ib = Math.floor(xb);
        const row = y * w;
        if (ia === ib) { cov[row + ia] += (xb - xa) / sub; continue; }
        cov[row + ia] += (ia + 1 - xa) / sub;
        for (let x = ia + 1; x < ib && x < w; x++) cov[row + x] += 1 / sub;
        if (ib < w) cov[row + ib] += (xb - ib) / sub;
      }
    }
  }
  for (let i = 0; i < cov.length; i++) if (cov[i] > 1) cov[i] = 1;
  return cov;
}
// Catmull-Rom through points (open), samples per segment
function catmull(points, per) {
  const out = [];
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[Math.max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Math.min(i + 2, points.length - 1)];
    for (let k = 0; k < per; k++) {
      const t = k / per, t2 = t * t, t3 = t2 * t;
      const f = (a, b, c, d) => 0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
      out.push([f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])]);
    }
  }
  out.push(points[points.length - 1]);
  return out;
}
module.exports = { fillPolygon, catmull };
