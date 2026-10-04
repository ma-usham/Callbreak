// node view.js <in.png> <out.png> x y w h [scale] [gridStep]
// Crops a region (frame px), scales it (nearest for scale>=1, area for <1) and draws a labelled grid
// every gridStep frame px, so positions can be read off with the Read tool.
'use strict';
const P = require('./png.js');
const [, , inp, outp, xs, ys, ws, hs, ss, gs] = process.argv;
const im = P.read(inp);
const x0 = +xs, y0 = +ys, cw = +ws, ch = +hs, scale = ss ? +ss : 1, step = gs ? +gs : 0;
let out;
if (scale >= 1) out = P.crop(im, x0, y0, cw, ch, scale);
else out = P.resize(P.crop(im, x0, y0, cw, ch, 1), Math.round(cw * scale), Math.round(ch * scale));
// flatten alpha over a checker so transparency is visible
for (let y = 0; y < out.h; y++) for (let x = 0; x < out.w; x++) {
  const o = (y * out.w + x) * 4, a = out.data[o + 3] / 255;
  const bg = ((x >> 3) + (y >> 3)) & 1 ? 200 : 140;
  for (let c = 0; c < 3; c++) out.data[o + c] = Math.round(out.data[o + c] * a + bg * (1 - a));
  out.data[o + 3] = 255;
}
const FONT = {
  '0': ['111', '101', '101', '101', '111'], '1': ['010', '110', '010', '010', '111'], '2': ['111', '001', '111', '100', '111'],
  '3': ['111', '001', '111', '001', '111'], '4': ['101', '101', '111', '001', '001'], '5': ['111', '100', '111', '001', '111'],
  '6': ['111', '100', '111', '101', '111'], '7': ['111', '001', '010', '010', '010'], '8': ['111', '101', '111', '101', '111'],
  '9': ['111', '101', '111', '001', '111'], '-': ['000', '000', '111', '000', '000'],
};
function put(x, y, col) { if (x < 0 || y < 0 || x >= out.w || y >= out.h) return; const o = (y * out.w + x) * 4; out.data[o] = col[0]; out.data[o + 1] = col[1]; out.data[o + 2] = col[2]; }
function text(s, x, y, col, k) {
  k = k || 2;
  for (let i = 0; i < s.length; i++) { const g = FONT[s[i]]; if (!g) continue; for (let r = 0; r < 5; r++) for (let c = 0; c < 3; c++) if (g[r][c] === '1') for (let a = 0; a < k; a++) for (let b = 0; b < k; b++) put(x + (i * 4 + c) * k + a, y + r * k + b, col); }
}
if (step > 0) {
  const s = out.w / cw;
  for (let gx = Math.ceil(x0 / step) * step; gx < x0 + cw; gx += step) {
    const px = Math.round((gx - x0) * s);
    for (let y = 0; y < out.h; y++) if ((y >> 2) & 1) put(px, y, [0, 255, 255]); else put(px, y, [0, 0, 0]);
    text(String(gx), px + 3, 3, [255, 255, 0]);
  }
  for (let gy = Math.ceil(y0 / step) * step; gy < y0 + ch; gy += step) {
    const py = Math.round((gy - y0) * s);
    for (let x = 0; x < out.w; x++) if ((x >> 2) & 1) put(x, py, [0, 255, 255]); else put(x, py, [0, 0, 0]);
    text(String(gy), 3, py + 3, [255, 255, 0]);
  }
}
P.write(outp, out, { rgb: true, level: 3 });
console.log('wrote', outp, out.w + 'x' + out.h);
