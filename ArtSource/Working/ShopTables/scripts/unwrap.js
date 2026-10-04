// node unwrap.js <in.png> <out.png> t0deg t1deg s0 s1 cols rows
// Elliptical unwrap around the key band's inner ellipse: column = parameter angle t (y down, so
// 180..360 is the far half), row = scale s (1 = inner band edge). Bilinear sampling.
'use strict';
const P = require('./png.js');
const [, , inp, outp, t0s, t1s, s0s, s1s, cs, rs] = process.argv;
const E = { cx: 1114.251, cy: 742.242, a: 780.172, b: 301.418 };
const im = P.read(inp);
const cols = +cs, rows = +rs, t0 = +t0s * Math.PI / 180, t1 = +t1s * Math.PI / 180, s0 = +s0s, s1 = +s1s;
const out = P.create(cols, rows);
function samp(x, y, c) {
  x = Math.min(Math.max(x, 0), im.w - 1.001); y = Math.min(Math.max(y, 0), im.h - 1.001);
  const x0 = Math.floor(x), y0 = Math.floor(y), tx = x - x0, ty = y - y0;
  const i = (y0 * im.w + x0) * 4;
  return (im.data[i + c] * (1 - tx) + im.data[i + 4 + c] * tx) * (1 - ty) + (im.data[i + im.w * 4 + c] * (1 - tx) + im.data[i + im.w * 4 + 4 + c] * tx) * ty;
}
for (let j = 0; j < rows; j++) {
  const s = s0 + (s1 - s0) * j / (rows - 1);
  for (let k = 0; k < cols; k++) {
    const t = t0 + (t1 - t0) * k / (cols - 1);
    const x = E.cx + s * E.a * Math.cos(t), y = E.cy + s * E.b * Math.sin(t);
    const o = (j * cols + k) * 4;
    for (let c = 0; c < 3; c++) out.data[o + c] = Math.round(samp(x, y, c));
    out.data[o + 3] = 255;
  }
}
// tick marks every 10 degrees along the top, and s=1.0/1.05/1.10/1.15 along the left
for (let deg = Math.ceil(+t0s / 10) * 10; deg <= +t1s; deg += 10) {
  const k = Math.round((deg - +t0s) / (+t1s - +t0s) * (cols - 1));
  for (let j = 0; j < 6; j++) { const o = (j * cols + k) * 4; out.data[o] = 0; out.data[o + 1] = 255; out.data[o + 2] = 255; }
}
for (const sv of [1.0, 1.05, 1.1, 1.15, 1.2]) {
  if (sv < s0 || sv > s1) continue;
  const j = Math.round((sv - s0) / (s1 - s0) * (rows - 1));
  for (let k = 0; k < 10; k++) { const o = (j * cols + k) * 4; out.data[o] = 0; out.data[o + 1] = 255; out.data[o + 2] = 0; }
}
P.write(outp, out, { rgb: true, level: 3 });
console.log('wrote', outp);
