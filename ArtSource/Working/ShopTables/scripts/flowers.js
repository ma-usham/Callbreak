// Foreground flower matte for BG_Table's two bottom corners.
//  - zones: hand-drawn generous polygons around the defocused foreground bushes (frame px)
//  - core: colour likelihood (red/orange petals or green/olive leaves), smoothed, thresholded,
//          closed, holes filled, restricted to the zones
//  - alpha: the core blurred by the defocus width (the bushes are out of focus)
// node flowers.js <BG_Table.png> <out.bin (Float32 alpha)> [preview.png]
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const { fillPolygon } = require('./raster.js');
const [, , bgp, outp, prevp, maskp] = process.argv;
const bg = P.read(bgp);
const W = bg.w, H = bg.h, N = W * H;
const ZONES = [
  // bottom-left bush: in front of the terrace and the table's lower-left apron
  [[0, 640], [70, 650], [150, 690], [215, 760], [250, 850], [262, 900], [300, 925], [380, 965], [470, 975], [520, 1005], [545, 1060], [530, 1110], [560, 1150], [640, 1170], [740, 1185], [790, 1230], [800, 1255], [0, 1255]],
  // bottom-right bush
  [[2230, 700], [2140, 740], [2050, 790], [1990, 860], [1960, 880], [1900, 880], [1870, 882], [1840, 898], [1800, 922], [1760, 942], [1700, 968], [1650, 1000], [1620, 1080], [1560, 1140], [1500, 1170], [1460, 1215], [1440, 1255], [2230, 1255]],
];
const zone = new Uint8Array(N);
for (const z of ZONES) { const c = fillPolygon(W, H, z); for (let i = 0; i < N; i++) if (c[i] > 0.5) zone[i] = 1; }
const inTable = new Uint8Array(N);
if (maskp) { const mk = P.read(maskp); for (let i = 0; i < N; i++) inTable[i] = mk.data[i * 4 + 3] > 128 ? 1 : 0; }
// sharpness: in-focus table carving has strong local gradients, the defocused flowers do not
const Lum = new Float32Array(N);
for (let i = 0; i < N; i++) Lum[i] = 0.2126 * bg.data[i * 4] + 0.7152 * bg.data[i * 4 + 1] + 0.0722 * bg.data[i * 4 + 2];
const grad = new Float32Array(N);
for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; grad[i] = Math.hypot(Lum[i + 1] - Lum[i - 1], Lum[i + W] - Lum[i - W]) / 2; }
const SH = +(process.env.SH || 12);
const sharpAvg = O.boxBlur(grad, W, H, 1);
const like = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (!zone[i]) continue;
  const r = bg.data[i * 4], g = bg.data[i * 4 + 1], b = bg.data[i * 4 + 2];
  const petal = (r > 140 && g < 0.38 * r && b < 0.32 * r) || (r > 200 && g < 0.68 * r && b < 0.25 * r);
  const leaf = (g >= 0.6 * r && b <= 0.3 * r && r > 60) || (g > r && b < g && g > 30);
  like[i] = (leaf || (petal && (!inTable[i] || sharpAvg[i] < SH))) ? 1 : 0;
}
const sm = O.boxBlur(like, W, H, 4);
let core = new Float32Array(N);
for (let i = 0; i < N; i++) core[i] = zone[i] && sm[i] > 0.45 ? 1 : 0;
// closing: dilate then erode (box radius CR)
const CR = +(process.env.CR || 6), HOLE = +(process.env.HOLE || 20000), DARK = +(process.env.DARK || 80), SMALL = +(process.env.SMALL || 150), DEF = +(process.env.DEF || 5);
const dil = O.boxBlur(core, W, H, CR);
for (let i = 0; i < N; i++) core[i] = dil[i] > 0.02 ? 1 : 0;
const ero = O.boxBlur(core, W, H, CR);
for (let i = 0; i < N; i++) core[i] = ero[i] > 0.98 && zone[i] ? 1 : 0;
// fill small holes: non-core components fully inside a zone with fewer than HOLE pixels
const lab = new Int32Array(N).fill(-1);
let holes = 0;
for (let s0 = 0; s0 < N; s0++) {
  if (core[s0] || lab[s0] >= 0) continue;
  const comp = [s0]; lab[s0] = s0; let touchesOut = !zone[s0], k = 0;
  while (k < comp.length) {
    const i = comp[k++], x = i % W, y = (i / W) | 0;
    const nb = [x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1];
    for (const j of nb) if (j >= 0 && !core[j] && lab[j] < 0) { lab[j] = s0; comp.push(j); if (!zone[j]) touchesOut = true; }
  }
  if (!touchesOut && comp.length <= HOLE) {
    // fill dark gaps (and tiny holes) only: bright table-coloured gaps show the table through the bush
    let bright = 0; for (const i of comp) if (Lum[i] > DARK) bright++;
    if (comp.length <= SMALL || bright / comp.length < 0.5) { for (const i of comp) core[i] = 1; holes += comp.length; }
    else for (const i of comp) if (Lum[i] <= DARK) { core[i] = 1; holes++; }
  }
}
// defocus: blur the binary core (3-pass box r=5 ~ Gaussian sigma 5)
// petal tips and dark leaves the colour rules miss (checked on zoomed crops of BG_Table)
const EXTRA = [
  [[1838, 1060], [1870, 1057], [1873, 1094], [1838, 1097]], // dark red petal over the right apron
  [[1902, 1100], [1925, 1088], [1960, 1090], [1966, 1172], [1930, 1178], [1905, 1150]], // dark leaf at the right edge
  [[432, 1076], [470, 1069], [530, 1071], [533, 1097], [478, 1101], [477, 1163], [452, 1165], [449, 1104], [432, 1098]], // petal edge + orange tip, left apron
];
for (const z of EXTRA) { const c = fillPolygon(W, H, z); for (let i = 0; i < N; i++) if (c[i] > 0.5) core[i] = 1; }
const alpha = O.boxBlur(core, W, H, DEF);
for (let i = 0; i < N; i++) if (alpha[i] < 0.01) alpha[i] = 0;
fs.writeFileSync(outp, Buffer.from(alpha.buffer));
let area = 0; for (let i = 0; i < N; i++) area += alpha[i];
console.log('filled holes px', holes, 'alpha area', area.toFixed(0));
if (prevp) {
  // preview: BG with the matte tinted cyan and zone outlines
  const o = P.create(W, H);
  for (let i = 0; i < N; i++) {
    const t = alpha[i];
    o.data[i * 4] = Math.round(bg.data[i * 4] * (1 - t * 0.7));
    o.data[i * 4 + 1] = Math.round(bg.data[i * 4 + 1] * (1 - t * 0.7) + 255 * t * 0.7);
    o.data[i * 4 + 2] = Math.round(bg.data[i * 4 + 2] * (1 - t * 0.7) + 255 * t * 0.7);
    o.data[i * 4 + 3] = 255;
  }
  P.write(prevp, o, { rgb: true, level: 3 });
}
