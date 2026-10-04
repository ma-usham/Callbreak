// node register.js <ref.png> <gen.png> <mask.png> [tile] [range] [out.json]
// Upscales gen to ref's size, then for tiles inside the mask finds the integer shift (dx, dy) that best
// aligns gen to ref (normalised cross-correlation of luminance gradients). Prints a shift grid.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , refp, genp, maskp, ts, rs, outj] = process.argv;
const ref = P.read(refp);
let gen = P.read(genp);
if (gen.w !== ref.w || gen.h !== ref.h) gen = P.resize(gen, ref.w, ref.h);
const mask = maskp && maskp !== '-' ? P.read(maskp) : null;
const W = ref.w, H = ref.h, T = +(ts || 120), R = +(rs || 10);
function gradL(im) {
  const L = new Float32Array(W * H), G = new Float32Array(W * H);
  for (let i = 0; i < W * H; i++) L[i] = 0.2126 * im.data[i * 4] + 0.7152 * im.data[i * 4 + 1] + 0.0722 * im.data[i * 4 + 2];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; G[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + W] - L[i - W]); }
  return G;
}
const GR = gradL(ref), GG = gradL(gen);
const res = [];
for (let ty = 0; ty + T <= H; ty += T) {
  let line = '';
  for (let tx = 0; tx + T <= W; tx += T) {
    let inside = 0;
    if (mask) for (let y = ty; y < ty + T; y += 4) for (let x = tx; x < tx + T; x += 4) if (mask.data[(y * W + x) * 4 + 3] > 250) inside++;
    if (mask && inside < (T / 4) * (T / 4) * 0.9) { line += '   .    '; continue; }
    let best = -2, bdx = 0, bdy = 0;
    for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
      let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
      for (let y = ty + R; y < ty + T - R; y += 2) for (let x = tx + R; x < tx + T - R; x += 2) {
        const yy = y + dy, xx = x + dx;
        if (yy < 0 || xx < 0 || yy >= H || xx >= W) continue;
        const a = GR[y * W + x], b = GG[yy * W + xx];
        sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
      }
      const cov = sab / n - (sa / n) * (sb / n), va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
      const ncc = cov / Math.sqrt(va * vb + 1e-9);
      if (ncc > best) { best = ncc; bdx = dx; bdy = dy; }
    }
    res.push({ tx, ty, dx: bdx, dy: bdy, ncc: +best.toFixed(3) });
    line += (bdx >= 0 ? '+' : '') + bdx + ',' + (bdy >= 0 ? '+' : '') + bdy + '(' + best.toFixed(1).slice(1) + ') ';
  }
  console.log(String(ty).padStart(4), line);
}
if (outj) fs.writeFileSync(outj, JSON.stringify(res));
