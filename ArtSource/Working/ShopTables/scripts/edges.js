// node edges.js <in.png> left|right|top x0 x1 y0 y1 step
// For each row (left/right) or column (top) prints the sub-pixel position of the strongest luminance
// step inside the window, with the luminance on each side, to trace the table's outline.
'use strict';
const P = require('./png.js');
const [, , inp, mode, a0, a1, b0, b1, st] = process.argv;
const im = P.read(inp);
const L = (x, y) => { const i = (y * im.w + x) * 4; return 0.2126 * im.data[i] + 0.7152 * im.data[i + 1] + 0.0722 * im.data[i + 2]; };
const step = +st || 10;
if (mode === 'left' || mode === 'right') {
  const x0 = +a0, x1 = +a1;
  for (let y = +b0; y <= +b1; y += step) {
    // smooth vertically over 3 rows
    const prof = [];
    for (let x = x0; x <= x1; x++) prof.push((L(x, y - 1) + L(x, y) + L(x, y + 1)) / 3);
    let best = -1, bi = 0;
    for (let i = 2; i < prof.length - 2; i++) {
      // outside is light, table is dark: left side light->dark going right; right side dark->light
      const g = mode === 'left' ? (prof[i - 2] + prof[i - 1]) - (prof[i + 1] + prof[i + 2]) : (prof[i + 1] + prof[i + 2]) - (prof[i - 2] + prof[i - 1]);
      if (g > best) { best = g; bi = i; }
    }
    console.log('y', y, 'edge x', x0 + bi, 'grad', (best / 2).toFixed(1), 'out', prof[Math.max(bi - 6, 0)].toFixed(0), 'in', prof[Math.min(bi + 6, prof.length - 1)].toFixed(0) );
  }
} else {
  const y0 = +b0, y1 = +b1;
  for (let x = +a0; x <= +a1; x += step) {
    const prof = [];
    for (let y = y0; y <= y1; y++) prof.push((L(x - 1, y) + L(x, y) + L(x + 1, y)) / 3);
    let best = -1, bi = 0;
    for (let i = 2; i < prof.length - 2; i++) {
      const g = Math.abs((prof[i + 1] + prof[i + 2]) - (prof[i - 2] + prof[i - 1]));
      if (g > best) { best = g; bi = i; }
    }
    console.log('x', x, 'edge y', y0 + bi, 'grad', (best / 2).toFixed(1), 'above', prof[Math.max(bi - 4, 0)].toFixed(0), 'below', prof[Math.min(bi + 4, prof.length - 1)].toFixed(0));
  }
}
