// node features.js <in.png> <out.png> x y w h
// Writes a 2x2 sheet for a region: image | saturation*value-ish "flower colour" score | local sharpness | combined
'use strict';
const P = require('./png.js');
const [, , inp, outp, xs, ys, ws, hs] = process.argv;
const im = P.read(inp);
const x0 = +xs, y0 = +ys, w = +ws, h = +hs;
const px = (x, y) => { const i = (Math.min(Math.max(y, 0), im.h - 1) * im.w + Math.min(Math.max(x, 0), im.w - 1)) * 4; return [im.data[i], im.data[i + 1], im.data[i + 2]]; };
const out = P.create(w * 2, h * 2, [0, 0, 0, 255]);
const set = (ox, oy, x, y, v) => { const o = ((oy + y) * out.w + ox + x) * 4; if (Array.isArray(v)) { out.data[o] = v[0]; out.data[o + 1] = v[1]; out.data[o + 2] = v[2]; } else { const g = Math.max(0, Math.min(255, Math.round(v * 255))); out.data[o] = out.data[o + 1] = out.data[o + 2] = g; } };
// luminance for sharpness
const L = new Float32Array(w * h);
for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const [r, g, b] = px(x0 + x, y0 + y); L[y * w + x] = 0.2126 * r + 0.7152 * g + 0.0722 * b; }
const grad = new Float32Array(w * h);
for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) { const gx = L[y * w + x + 1] - L[y * w + x - 1], gy = L[(y + 1) * w + x] - L[(y - 1) * w + x]; grad[y * w + x] = Math.hypot(gx, gy); }
// box average of gradient over 7x7
const sharp = new Float32Array(w * h);
const R = 3;
for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { let s = 0, n = 0; for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { const xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue; s += grad[yy * w + xx]; n++; } sharp[y * w + x] = s / n; }
for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
  const [r, g, b] = px(x0 + x, y0 + y);
  set(0, 0, x, y, [r, g, b]);
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
  const sat = mx > 0 ? (mx - mn) / mx : 0, val = mx / 255;
  // flower colour score: saturated and bright red/orange, or green-dominant leaves
  const red = (r - Math.max(g, b)) / 255; // red petals
  const leaf = (g - r) / 255 + 0.15; // leaves greener than wood
  const score = Math.max(red * 1.6 * val, leaf * 2.5);
  set(w, 0, x, y, score);
  set(0, h, x, y, Math.min(1, sharp[y * w + x] / 40));
  set(w, h, x, y, [Math.min(255, score * 255), Math.min(255, sharp[y * w + x] / 40 * 255), 0]);
}
P.write(outp, out, { rgb: true, level: 3 });
console.log('wrote', outp);
