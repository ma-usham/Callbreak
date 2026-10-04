// node sheet.js <in.png> <path.json> <out.png> [stepDeg] [win] [scale]
// Tiles small zoomed windows centred on points of a traced path, with the path drawn in green,
// so the whole outline can be checked in one image.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , inp, pj, outp, st, wn, sc] = process.argv;
const im = P.read(inp);
const path = JSON.parse(fs.readFileSync(pj, 'utf8'));
const step = +(st || 10), win = +(wn || 40), scale = +(sc || 4);
const picks = [];
let next = path[0].t;
for (const p of path) if (p.t >= next - 1e-6) { picks.push(p); next += step; }
const tile = win * scale, cols = 6, rows = Math.ceil(picks.length / cols);
const out = P.create(cols * tile, rows * tile, [0, 0, 0, 255]);
const pts = path.map(p => [p.x, p.y]);
picks.forEach((p, n) => {
  const ox = (n % cols) * tile, oy = Math.floor(n / cols) * tile;
  const x0 = Math.round(p.x - win / 2), y0 = Math.round(p.y - win / 2);
  for (let y = 0; y < tile; y++) for (let x = 0; x < tile; x++) {
    const sx = x0 + Math.floor(x / scale), sy = y0 + Math.floor(y / scale);
    if (sx < 0 || sy < 0 || sx >= im.w || sy >= im.h) continue;
    const i = (sy * im.w + sx) * 4, o = ((oy + y) * out.w + ox + x) * 4;
    out.data[o] = im.data[i]; out.data[o + 1] = im.data[i + 1]; out.data[o + 2] = im.data[i + 2];
  }
  // draw path segments inside this window
  for (let k = 1; k < pts.length; k++) {
    const [ax, ay] = pts[k - 1], [bx, by] = pts[k];
    if (Math.max(ax, bx) < x0 - 2 || Math.min(ax, bx) > x0 + win + 2 || Math.max(ay, by) < y0 - 2 || Math.min(ay, by) > y0 + win + 2) continue;
    const nn = Math.ceil(Math.hypot(bx - ax, by - ay) * scale * 2) + 1;
    for (let q = 0; q <= nn; q++) {
      const fx = ax + (bx - ax) * q / nn, fy = ay + (by - ay) * q / nn;
      const px = Math.round((fx - x0) * scale), py = Math.round((fy - y0) * scale);
      if (px < 0 || py < 0 || px >= tile || py >= tile) continue;
      const o = ((oy + py) * out.w + ox + px) * 4; out.data[o] = 0; out.data[o + 1] = 255; out.data[o + 2] = 0;
    }
  }
  // frame + label (angle) as a little bar length
  for (let x = 0; x < tile; x++) { const o = (oy * out.w + ox + x) * 4; out.data[o] = 255; out.data[o + 1] = 0; out.data[o + 2] = 255; }
  for (let y = 0; y < tile; y++) { const o = ((oy + y) * out.w + ox) * 4; out.data[o] = 255; out.data[o + 1] = 0; out.data[o + 2] = 255; }
});
P.write(outp, out, { rgb: true, level: 3 });
console.log('tiles', picks.map(p => p.t).join(','));
