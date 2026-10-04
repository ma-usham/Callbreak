// Builds the table silhouette (outline.json + TableMask.png) from:
//  - the far outline traced by outer4.js (t = 180..360 deg, the lip's top edge)
//  - side points measured on BG_Table below the rim (left 742..920, right 742..950)
//  - the bottom corners (hidden by flowers) from corners.json if present, else a provisional lean
// node build_mask.js <outer4.json> <outDir> [corners.json]
'use strict';
const fs = require('fs');
const P = require('./png.js');
const { fillPolygon, catmull } = require('./raster.js');
const [, , outerJ, outDir, cornersJ] = process.argv;
const W = 2230, H = 1255;
const outer = JSON.parse(fs.readFileSync(outerJ, 'utf8')).filter(p => p.t >= 180 && p.t <= 360);
// measured on BG_Table (luminance profiles across the edge), frame px
const rightMeasured = [[1974.7, 742.2], [1977.9, 771.4], [1978.5, 785], [1977.5, 800], [1976.5, 815], [1975.5, 830], [1975.2, 845], [1975, 860], [1972.5, 878], [1969.5, 890], [1965, 904], [1961.5, 920], [1957, 930], [1953, 940], [1950, 950]];
const leftMeasured = [[256.3, 742.2], [252, 760], [251.5, 780], [252, 800], [254, 820], [254.5, 850], [257.5, 880], [262, 900], [267, 920]];
let corners = null;
if (cornersJ && fs.existsSync(cornersJ)) corners = JSON.parse(fs.readFileSync(cornersJ, 'utf8'));
const lean = 0.3;
const rightHidden = corners ? corners.right : [[1950 - lean * 50, 1000], [1950 - lean * 150, 1100], [1950 - lean * 250, 1200], [1950 - lean * 350, 1300]];
const leftHidden = corners ? corners.left : [[267 + lean * 380, 1300], [267 + lean * 280, 1200], [267 + lean * 180, 1100], [267 + lean * 80, 1000]];
const right = catmull(rightMeasured.concat(rightHidden), 6);
const left = catmull(leftHidden.concat(leftMeasured.slice().reverse()), 6);
// polygon: far outline (left -> right over the top), right side down, bottom (below frame), left side up
const poly = [];
for (const p of outer) poly.push([p.x, p.y]);
for (const p of right.slice(1)) poly.push(p);
poly.push([right[right.length - 1][0], 1320]);
poly.push([left[0][0], 1320]);
for (const p of left.slice(0, left.length - 1)) poly.push(p);
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(outDir + '/outline.json', JSON.stringify(poly.map(p => [+p[0].toFixed(2), +p[1].toFixed(2)])));
const cov = fillPolygon(W, H, poly);
const img = P.create(W, H);
let area = 0, minx = W, maxx = 0, miny = H, maxy = 0;
for (let i = 0; i < W * H; i++) {
  const a = Math.round(cov[i] * 255);
  img.data[i * 4] = img.data[i * 4 + 1] = img.data[i * 4 + 2] = 255; img.data[i * 4 + 3] = a;
  area += cov[i];
  if (a > 0) { const x = i % W, y = (i / W) | 0; if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
}
P.write(outDir + '/TableMask.png', img);
fs.writeFileSync(outDir + '/mask_cov.bin', Buffer.from(cov.buffer));
console.log('area px', area.toFixed(0), 'bbox', minx, miny, maxx, maxy, 'vertices', poly.length);
