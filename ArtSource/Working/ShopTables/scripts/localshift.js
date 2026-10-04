// node localshift.js <ref.png> <gen.png> x y w h [range]
// Best sub-pixel shift (dx, dy) so that gen(x+dx, y+dy) matches ref(x, y) over a rectangle,
// using NCC of luminance gradients; gen is upscaled to ref's size first if needed.
'use strict';
const P = require('./png.js');
const [, , refp, genp, xs, ys, ws, hs, rs] = process.argv;
const ref = P.read(refp);
let gen = P.read(genp);
if (gen.w !== ref.w) gen = P.resize(gen, ref.w, ref.h);
const W = ref.w, H = ref.h, x0 = +xs, y0 = +ys, w = +ws, h = +hs, R = +(rs || 10);
function grad(im) {
  const L = new Float32Array(W * H), G = new Float32Array(W * H);
  for (let i = 0; i < W * H; i++) L[i] = 0.2126 * im.data[i * 4] + 0.7152 * im.data[i * 4 + 1] + 0.0722 * im.data[i * 4 + 2];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; G[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + W] - L[i - W]); }
  return G;
}
const A = grad(ref), B = grad(gen);
function ncc(dx, dy) {
  let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
  for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) {
    const xx = x + dx, yy = y + dy; if (xx < 1 || yy < 1 || xx >= W - 1 || yy >= H - 1) continue;
    const a = A[y * W + x], b = B[yy * W + xx];
    sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
  }
  const cov = sab / n - sa * sb / n / n, va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
  return cov / Math.sqrt(va * vb + 1e-9);
}
let best = -2, bx = 0, by = 0;
const S = {};
for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { const v = ncc(dx, dy); S[dx + ',' + dy] = v; if (v > best) { best = v; bx = dx; by = dy; } }
// parabolic sub-pixel refinement
const f = (dx, dy) => S[dx + ',' + dy] ?? best;
const sx = bx + (f(bx - 1, by) - f(bx + 1, by)) / (2 * (f(bx - 1, by) - 2 * best + f(bx + 1, by)) || 1);
const sy = by + (f(bx, by - 1) - f(bx, by + 1)) / (2 * (f(bx, by - 1) - 2 * best + f(bx, by + 1)) || 1);
console.log(JSON.stringify({ rect: [x0, y0, w, h], dx: +sx.toFixed(2), dy: +sy.toFixed(2), ncc: +best.toFixed(3), zero: +ncc(0, 0).toFixed(3) }));
