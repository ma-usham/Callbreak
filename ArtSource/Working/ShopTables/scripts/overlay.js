// node overlay.js <in.png> <out.png> x y w h scale <curves.json>
// Draws curves (polylines in frame px) over a crop, for checking fits by eye.
// curves.json: [{ "color": [255,0,0], "pts": [[x,y],...] }, { "ellipse": {cx,cy,a,b}, "color": [...] }]
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , inp, outp, xs, ys, ws, hs, ss, cj] = process.argv;
const im = P.read(inp);
const x0 = +xs, y0 = +ys, cw = +ws, ch = +hs, scale = +ss;
const out = P.crop(im, x0, y0, cw, ch, scale);
for (let i = 3; i < out.data.length; i += 4) out.data[i] = 255;
const curves = JSON.parse(fs.readFileSync(cj, 'utf8'));
function put(fx, fy, col) {
  const px = Math.round((fx - x0) * scale), py = Math.round((fy - y0) * scale);
  if (px < 0 || py < 0 || px >= out.w || py >= out.h) return;
  const o = (py * out.w + px) * 4; out.data[o] = col[0]; out.data[o + 1] = col[1]; out.data[o + 2] = col[2];
}
for (const c of curves) {
  const col = c.color || [255, 0, 0];
  if (c.ellipse) {
    const e = c.ellipse;
    for (let k = 0; k < 20000; k++) { const t = k / 20000 * 2 * Math.PI; put(e.cx + e.a * Math.cos(t), e.cy + e.b * Math.sin(t), col); }
  }
  if (c.pts) {
    if (c.dots) { for (const p of c.pts) for (let a = -1; a <= 1; a++) for (let b = -1; b <= 1; b++) put(p[0] + a / scale, p[1] + b / scale, col); }
    else for (let i = 1; i < c.pts.length; i++) {
      const [ax, ay] = c.pts[i - 1], [bx, by] = c.pts[i];
      const n = Math.ceil(Math.hypot(bx - ax, by - ay) * scale * 2) + 1;
      for (let k = 0; k <= n; k++) put(ax + (bx - ax) * k / n, ay + (by - ay) * k / n, col);
    }
  }
}
P.write(outp, out, { rgb: true, level: 3 });
console.log('wrote', outp, out.w + 'x' + out.h);
