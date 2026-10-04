// Traces the table's outer edge (the far half and the ends) by dynamic programming on an elliptical
// unwrap of BG_Table: in each column (parameter angle t) pick the row (scale s) of the strongest
// "brown lip inside -> scenery outside" transition, with a smoothness penalty between columns.
// Writes outer.json: [{t, s, x, y}] in frame px.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , inp, outj, t0s, t1s] = process.argv;
const E = { cx: 1114.251, cy: 742.242, a: 780.172, b: 301.418 };
const im = P.read(inp);
const T0 = +(t0s || 172), T1 = +(t1s || 368);
const cols = Math.round((T1 - T0) * 10), S0 = 1.02, S1 = 1.2, rows = 360;
function samp(x, y, c) {
  x = Math.min(Math.max(x, 0), im.w - 1.001); y = Math.min(Math.max(y, 0), im.h - 1.001);
  const x0 = Math.floor(x), y0 = Math.floor(y), tx = x - x0, ty = y - y0, i = (y0 * im.w + x0) * 4;
  return (im.data[i + c] * (1 - tx) + im.data[i + 4 + c] * tx) * (1 - ty) + (im.data[i + im.w * 4 + c] * (1 - tx) + im.data[i + im.w * 4 + 4 + c] * tx) * ty;
}
const U = new Float32Array(cols * rows * 3);
const xy = (k, j) => { const t = (T0 + (T1 - T0) * k / (cols - 1)) * Math.PI / 180, s = S0 + (S1 - S0) * j / (rows - 1); return [E.cx + s * E.a * Math.cos(t), E.cy + s * E.b * Math.sin(t), t, s]; };
for (let k = 0; k < cols; k++) for (let j = 0; j < rows; j++) { const [x, y] = xy(k, j); for (let c = 0; c < 3; c++) U[(k * rows + j) * 3 + c] = samp(x, y, c); }
// pixel distance per row step at column k (for converting windows in px to rows)
const pxPerRow = k => { const [x1, y1] = xy(k, 100), [x2, y2] = xy(k, 101); return Math.hypot(x2 - x1, y2 - y1); };
const cost = new Float32Array(cols * rows);
for (let k = 0; k < cols; k++) {
  const ppr = pxPerRow(k);
  const w = Math.max(2, Math.round(3 / ppr)); // ~3 px windows
  for (let j = 0; j < rows; j++) {
    let ir = 0, ig = 0, ib = 0, or = 0, og = 0, ob = 0, n = 0;
    for (let q = 1; q <= w; q++) {
      const ji = Math.max(j - q, 0), jo = Math.min(j + q, rows - 1);
      ir += U[(k * rows + ji) * 3]; ig += U[(k * rows + ji) * 3 + 1]; ib += U[(k * rows + ji) * 3 + 2];
      or += U[(k * rows + jo) * 3]; og += U[(k * rows + jo) * 3 + 1]; ob += U[(k * rows + jo) * 3 + 2]; n++;
    }
    ir /= n; ig /= n; ib /= n; or /= n; og /= n; ob /= n;
    const diff = Math.hypot(ir - or, ig - og, ib - ob);
    // inner side should look like the brown/orange lip: red clearly above blue, not too light
    const lip = (ir - ib) > 50 && ir < 235 && ig < 175 ? 1 : 0.35;
    cost[k * rows + j] = -diff * lip;
  }
}
// DP with smoothness: allowed jump of up to J rows per column, penalty lambda * |dj|
const J = 6, lambda = 6;
const acc = new Float32Array(cols * rows), from = new Int16Array(cols * rows);
for (let j = 0; j < rows; j++) acc[j] = cost[j];
for (let k = 1; k < cols; k++) for (let j = 0; j < rows; j++) {
  let best = Infinity, bj = j;
  for (let d = -J; d <= J; d++) { const jj = j + d; if (jj < 0 || jj >= rows) continue; const v = acc[(k - 1) * rows + jj] + lambda * Math.abs(d); if (v < best) { best = v; bj = jj; } }
  acc[k * rows + j] = best + cost[k * rows + j]; from[k * rows + j] = bj;
}
let bj = 0, bv = Infinity;
for (let j = 0; j < rows; j++) if (acc[(cols - 1) * rows + j] < bv) { bv = acc[(cols - 1) * rows + j]; bj = j; }
const path = new Array(cols);
for (let k = cols - 1; k >= 0; k--) { path[k] = bj; bj = from[k * rows + bj]; }
const res = [];
for (let k = 0; k < cols; k++) { const [x, y, t, s] = xy(k, path[k]); res.push({ t: +(t * 180 / Math.PI).toFixed(2), s: +s.toFixed(5), x: +x.toFixed(2), y: +y.toFixed(2) }); }
fs.writeFileSync(outj, JSON.stringify(res));
for (let k = 0; k < cols; k += Math.round(cols / 40)) console.log(res[k].t, res[k].s, res[k].x, res[k].y);
