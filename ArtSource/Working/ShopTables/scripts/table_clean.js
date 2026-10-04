// Builds the clean-table candidate T_c from ChatGPT's completed table (a1_s1): upscaled to the frame,
// shifted by the measured local offset, and colour-matched to BG_Table with a smooth per-channel gain
// field (push-pull filled from trusted, flower-free table pixels). Also writes the colour distance
// |BG - T_c| inside the table as flower evidence.
// node table_clean.js <BG> <TableMask> <s1.png> <outDir>
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , bgp, maskp, genp, outDir] = process.argv;
const bg = P.read(bgp), m = P.read(maskp), gen = P.read(genp);
const W = bg.w, H = bg.h, N = W * H;
const B = O.planes(bg);
const G = O.upscaleShift(gen, W, H, 0.3, -1.2);
const inT = new Uint8Array(N);
for (let i = 0; i < N; i++) inT[i] = m.data[i * 4 + 3] > 250 ? 1 : 0;
// untrusted = likely flowers: saturated red/orange or green-dominant, in the two bottom corners, dilated
const flowerish = new Float32Array(N);
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const i = y * W + x;
  const corner = (x < 760 && y > 860) || (x > 1440 && y > 820);
  if (!corner) continue;
  const r = B.c[0][i], g = B.c[1][i], b = B.c[2][i];
  const red = (r - Math.max(g, b)) / 255, leaf = (g - r) / 255;
  // also anything that differs strongly from ChatGPT's clean table
  const d = Math.hypot(r - G.c[0][i], g - G.c[1][i], b - G.c[2][i]);
  if (red > 0.42 || leaf > -0.02 || d > 70) flowerish[i] = 1;
}
const fl = O.boxBlur(flowerish, W, H, 10);
const trust = new Float32Array(N);
for (let i = 0; i < N; i++) trust[i] = inT[i] && fl[i] < 0.02 ? 1 : 0;
// smooth per-channel gain field: LP(BG)/LP(G) over trusted pixels, filled by push-pull
const gain = [];
for (let c = 0; c < 3; c++) {
  const lb = O.boxBlur(B.c[c].map((v, i) => v * trust[i]), W, H, 18);
  const lg = O.boxBlur(G.c[c].map((v, i) => v * trust[i]), W, H, 18);
  const lw = O.boxBlur(trust, W, H, 18);
  const ratio = new Float32Array(N), wt = new Float32Array(N);
  for (let i = 0; i < N; i++) { if (lw[i] > 0.3) { ratio[i] = (lb[i] + 4) / (lg[i] + 4); wt[i] = 1; } }
  gain.push(O.pushPull(ratio, wt, W, H));
}
const Tc = { w: W, h: H, c: [new Float32Array(N), new Float32Array(N), new Float32Array(N)] };
for (let c = 0; c < 3; c++) for (let i = 0; i < N; i++) Tc.c[c][i] = Math.min(255, Math.max(0, G.c[c][i] * Math.min(Math.max(gain[c][i], 0.3), 2.5)));
fs.mkdirSync(outDir, { recursive: true });
P.write(outDir + '/Tc.png', O.toImage(Tc), { rgb: true, level: 3 });
// evidence image
const ev = P.create(W, H);
let sumD = 0, nD = 0;
for (let i = 0; i < N; i++) {
  const d = inT[i] ? Math.hypot(B.c[0][i] - Tc.c[0][i], B.c[1][i] - Tc.c[1][i], B.c[2][i] - Tc.c[2][i]) : 0;
  if (trust[i]) { sumD += d; nD++; }
  const v = Math.min(255, Math.round(d * 2));
  ev.data[i * 4] = ev.data[i * 4 + 1] = ev.data[i * 4 + 2] = v; ev.data[i * 4 + 3] = 255;
}
P.write(outDir + '/evidence.png', ev, { rgb: true, level: 3 });
fs.writeFileSync(outDir + '/trust.bin', Buffer.from(trust.buffer));
console.log('mean |BG - Tc| on trusted table pixels', (sumD / nD).toFixed(2), 'n', nD);
