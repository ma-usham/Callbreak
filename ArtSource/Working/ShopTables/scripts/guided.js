// Colour guided filter (He, Sun, Tang): refines a rough matte p so its edges follow the guide image I.
// guidedFilter(I = [r,g,b] Float32 0..1, p Float32, w, h, r, eps) -> Float32
'use strict';
function box(src, w, h, r) {
  // exact box mean with clamped borders (integral image)
  const S = new Float64Array((w + 1) * (h + 1));
  for (let y = 0; y < h; y++) { let row = 0; for (let x = 0; x < w; x++) { row += src[y * w + x]; S[(y + 1) * (w + 1) + x + 1] = S[y * (w + 1) + x + 1] + row; } }
  const out = new Float32Array(w * h);
  for (let y = 0; y < h; y++) {
    const y0 = Math.max(0, y - r), y1 = Math.min(h - 1, y + r);
    for (let x = 0; x < w; x++) {
      const x0 = Math.max(0, x - r), x1 = Math.min(w - 1, x + r);
      const s = S[(y1 + 1) * (w + 1) + x1 + 1] - S[y0 * (w + 1) + x1 + 1] - S[(y1 + 1) * (w + 1) + x0] + S[y0 * (w + 1) + x0];
      out[y * w + x] = s / ((x1 - x0 + 1) * (y1 - y0 + 1));
    }
  }
  return out;
}
function guidedFilter(I, p, w, h, r, eps) {
  const n = w * h;
  const mr = box(I[0], w, h, r), mg = box(I[1], w, h, r), mb = box(I[2], w, h, r), mp = box(p, w, h, r);
  const prod = (a, b) => { const o = new Float32Array(n); for (let i = 0; i < n; i++) o[i] = a[i] * b[i]; return o; };
  const mrp = box(prod(I[0], p), w, h, r), mgp = box(prod(I[1], p), w, h, r), mbp = box(prod(I[2], p), w, h, r);
  const vrr = box(prod(I[0], I[0]), w, h, r), vrg = box(prod(I[0], I[1]), w, h, r), vrb = box(prod(I[0], I[2]), w, h, r);
  const vgg = box(prod(I[1], I[1]), w, h, r), vgb = box(prod(I[1], I[2]), w, h, r), vbb = box(prod(I[2], I[2]), w, h, r);
  const ar = new Float32Array(n), ag = new Float32Array(n), ab = new Float32Array(n), bb = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const cr = mrp[i] - mr[i] * mp[i], cg = mgp[i] - mg[i] * mp[i], cb = mbp[i] - mb[i] * mp[i];
    const s11 = vrr[i] - mr[i] * mr[i] + eps, s12 = vrg[i] - mr[i] * mg[i], s13 = vrb[i] - mr[i] * mb[i];
    const s22 = vgg[i] - mg[i] * mg[i] + eps, s23 = vgb[i] - mg[i] * mb[i], s33 = vbb[i] - mb[i] * mb[i] + eps;
    // inverse of symmetric 3x3
    const i11 = s22 * s33 - s23 * s23, i12 = s13 * s23 - s12 * s33, i13 = s12 * s23 - s13 * s22;
    const i22 = s11 * s33 - s13 * s13, i23 = s13 * s12 - s11 * s23, i33 = s11 * s22 - s12 * s12;
    const det = s11 * i11 + s12 * i12 + s13 * i13;
    const a1 = (i11 * cr + i12 * cg + i13 * cb) / det, a2 = (i12 * cr + i22 * cg + i23 * cb) / det, a3 = (i13 * cr + i23 * cg + i33 * cb) / det;
    ar[i] = a1; ag[i] = a2; ab[i] = a3; bb[i] = mp[i] - a1 * mr[i] - a2 * mg[i] - a3 * mb[i];
  }
  const Ar = box(ar, w, h, r), Ag = box(ag, w, h, r), Ab = box(ab, w, h, r), Bb = box(bb, w, h, r);
  const q = new Float32Array(n);
  for (let i = 0; i < n; i++) q[i] = Ar[i] * I[0][i] + Ag[i] * I[1][i] + Ab[i] * I[2][i] + Bb[i];
  return q;
}
module.exports = { guidedFilter, box };
