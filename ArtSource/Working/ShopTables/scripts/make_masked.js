// Builds the ChatGPT input for the scenery fill: BG_Table scaled to 1672 x 941 with the table area
// (TableMask dilated by 3 frame px) and the foreground flower corners painted flat magenta.
// node make_masked.js <BG_Table.png> <TableMask.png> <out.png> [outRegionMask.png]
'use strict';
const P = require('./png.js');
const { fillPolygon } = require('./raster.js');
const [, , bgp, maskp, outp, regp] = process.argv;
const bg = P.read(bgp), m = P.read(maskp);
const W = bg.w, H = bg.h;
// dilate table mask by 3 px (square)
const t = new Uint8Array(W * H);
for (let i = 0; i < W * H; i++) t[i] = m.data[i * 4 + 3] > 0 ? 1 : 0;
const R = 3, d = new Uint8Array(W * H);
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  if (!t[y * W + x]) continue;
  for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { const xx = x + dx, yy = y + dy; if (xx >= 0 && yy >= 0 && xx < W && yy < H) d[yy * W + xx] = 1; }
}
// foreground flower corners outside the table (generous)
const left = fillPolygon(W, H, [[0, 640], [70, 655], [150, 700], [215, 770], [262, 860], [275, 1255], [0, 1255]]);
const right = fillPolygon(W, H, [[1930, 880], [1965, 860], [2040, 815], [2140, 755], [2230, 720], [2230, 1255], [1920, 1255]]);
const reg = P.create(W, H);
for (let i = 0; i < W * H; i++) {
  const on = d[i] || left[i] > 0.5 || right[i] > 0.5;
  reg.data[i * 4] = reg.data[i * 4 + 1] = reg.data[i * 4 + 2] = 255; reg.data[i * 4 + 3] = on ? 255 : 0;
}
if (regp) P.write(regp, reg, { level: 3 });
const small = P.resize(bg, 1672, 941);
const rsm = P.resize(reg, 1672, 941);
for (let i = 0; i < 1672 * 941; i++) if (rsm.data[i * 4 + 3] > 0) { small.data[i * 4] = 255; small.data[i * 4 + 1] = 0; small.data[i * 4 + 2] = 255; }
for (let i = 0; i < 1672 * 941; i++) small.data[i * 4 + 3] = 255;
P.write(outp, small, { rgb: true, level: 9 });
console.log('wrote', outp);
