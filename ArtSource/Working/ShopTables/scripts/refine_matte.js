// Refines the rough flower matte with a colour guided filter on BG_Table (only inside the corner boxes),
// so the soft edges follow the real colour transitions of the defocused flowers.
// node refine_matte.js <BG_Table.png> <rough.bin> <out.bin> [r] [eps] [preview.png]
'use strict';
const fs = require('fs');
const P = require('./png.js');
const { guidedFilter } = require('./guided.js');
const [, , bgp, roughp, outp, rs, es, prevp] = process.argv;
const bg = P.read(bgp);
const W = bg.w, H = bg.h, N = W * H;
const rough = new Float32Array(fs.readFileSync(roughp).buffer.slice(0));
const r = +(rs || 8), eps = +(es || 0.01);
const out = new Float32Array(N);
// process two boxes (bottom-left, bottom-right) to keep memory small
const boxes = [[0, 600, 900, H - 600], [1380, 640, W - 1380, H - 640]];
for (const [bx, by, bw, bh] of boxes) {
  const n = bw * bh, I = [new Float32Array(n), new Float32Array(n), new Float32Array(n)], p = new Float32Array(n);
  for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) {
    const i = (by + y) * W + bx + x, j = y * bw + x;
    for (let c = 0; c < 3; c++) I[c][j] = bg.data[i * 4 + c] / 255;
    p[j] = rough[i];
  }
  const q = guidedFilter(I, p, bw, bh, r, eps);
  for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) {
    const i = (by + y) * W + bx + x, j = y * bw + x;
    let v = q[j];
    // keep it near the rough matte (no new islands far from flowers)
    const near = rough[i] > 0 ? 1 : 0;
    v = Math.min(Math.max(v, 0), 1) * near;
    if (rough[i] > 0.98) v = Math.max(v, 0.98);
    out[i] = v < 0.015 ? 0 : v > 0.985 ? 1 : v;
  }
}
fs.writeFileSync(outp, Buffer.from(out.buffer));
if (prevp) {
  const o = P.create(W, H);
  for (let i = 0; i < N; i++) { const v = Math.round(out[i] * 255); o.data[i * 4] = o.data[i * 4 + 1] = o.data[i * 4 + 2] = v; o.data[i * 4 + 3] = 255; }
  P.write(prevp, o, { rgb: true, level: 3 });
}
let area = 0; for (let i = 0; i < N; i++) area += out[i];
console.log('refined alpha area', area.toFixed(0));
