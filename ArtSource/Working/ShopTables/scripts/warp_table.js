// Aligns ChatGPT's completed table (1672 x 941) to BG_Table locally: upscale with the global offset,
// measure sub-pixel shifts on tiles of clean (flower-free) table, smooth them into a displacement
// field (push-pull + blur), extend it under the flowers, and resample. Writes a frame-size PNG.
// node warp_table.js <BG> <TableMask> <s1.png> <flowerAlpha.bin> <out.png>
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , bgp, maskp, genp, mp, outp] = process.argv;
const bg = P.read(bgp), mk = P.read(maskp);
const W = bg.w, H = bg.h, N = W * H;
const M = new Float32Array(fs.readFileSync(mp).buffer.slice(0));
const near = O.boxBlur(M.map(v => (v > 0 ? 1 : 0)), W, H, 6);
const gen = P.read(genp);
const G0 = O.upscaleShift(gen, W, H, 0.3, -1.2);
const lum = c => { const L = new Float32Array(N); for (let i = 0; i < N; i++) L[i] = 0.2126 * c[0][i] + 0.7152 * c[1][i] + 0.0722 * c[2][i]; return L; };
const LB = lum(O.planes(bg).c), LG = lum(G0.c);
const grad = L => { const g = new Float32Array(N); for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; g[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + W] - L[i - W]); } return g; };
const GB = grad(LB), GG = grad(LG);
const ok = new Uint8Array(N);
for (let i = 0; i < N; i++) ok[i] = mk.data[i * 4 + 3] === 255 && near[i] === 0 ? 1 : 0;
const T = 48, STEP = 24, RG = 4;
const dxs = new Float32Array(N), dys = new Float32Array(N), wts = new Float32Array(N);
let tiles = 0;
const samples = [];
for (let ty = 400; ty + T <= H; ty += STEP) for (let tx = 200; tx + T <= 2030; tx += STEP) {
  let cnt = 0; for (let y = ty; y < ty + T; y += 2) for (let x = tx; x < tx + T; x += 2) cnt += ok[y * W + x];
  if (cnt < (T / 2) * (T / 2) * 0.8) continue;
  let best = -2, bx = 0, by = 0; const S = {};
  for (let dy = -RG; dy <= RG; dy++) for (let dx = -RG; dx <= RG; dx++) {
    let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
    for (let y = ty; y < ty + T; y++) for (let x = tx; x < tx + T; x++) {
      const i = y * W + x; if (!ok[i]) continue;
      const j = (y + dy) * W + x + dx; const a = GB[i], b = GG[j];
      sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
    }
    const cov = sab / n - sa * sb / n / n, va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
    const v = cov / Math.sqrt(va * vb + 1e-9); S[dx + ',' + dy] = v;
    if (v > best) { best = v; bx = dx; by = dy; }
  }
  if (best < 0.5 || Math.abs(bx) === RG || Math.abs(by) === RG) continue;
  const f = (dx, dy) => S[dx + ',' + dy];
  const den = (a, b, c) => (a - 2 * b + c);
  const sx = bx + (den(f(bx - 1, by), best, f(bx + 1, by)) < 0 ? (f(bx - 1, by) - f(bx + 1, by)) / (2 * den(f(bx - 1, by), best, f(bx + 1, by))) : 0);
  const sy = by + (den(f(bx, by - 1), best, f(bx, by + 1)) < 0 ? (f(bx, by - 1) - f(bx, by + 1)) / (2 * den(f(bx, by - 1), best, f(bx, by + 1))) : 0);
  samples.push({ x: tx + T / 2, y: ty + T / 2, dx: sx, dy: sy, q: best });
  tiles++;
}
// robust: drop samples far from the median of their neighbours (within 100 px)
const kept = samples.filter(s => {
  const nb = samples.filter(t => Math.hypot(t.x - s.x, t.y - s.y) < 100);
  const md = k => { const v = nb.map(t => t[k]).sort((a, b) => a - b); return v[v.length >> 1]; };
  return Math.abs(s.dx - md('dx')) < 1.0 && Math.abs(s.dy - md('dy')) < 1.0;
});
for (const s of kept) { const i = Math.round(s.y) * W + Math.round(s.x); dxs[i] = s.dx * s.q; dys[i] = s.dy * s.q; wts[i] = s.q; }
// splat with a wide kernel, then push-pull fill
const bx = O.boxBlur(dxs, W, H, 20), by2 = O.boxBlur(dys, W, H, 20), bw = O.boxBlur(wts, W, H, 20);
const fx = new Float32Array(N), fy = new Float32Array(N), fw = new Float32Array(N);
for (let i = 0; i < N; i++) if (bw[i] > 1e-5) { fx[i] = bx[i] / bw[i]; fy[i] = by2[i] / bw[i]; fw[i] = Math.min(1, bw[i] * 400); }
const DX = O.boxBlur(O.pushPull(fx, fw, W, H), W, H, 12), DY = O.boxBlur(O.pushPull(fy, fw, W, H), W, H, 12);
// resample: out(x, y) = G0(x + DX, y + DY)
const out = P.create(W, H);
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const i = y * W + x;
  for (let c = 0; c < 3; c++) out.data[i * 4 + c] = Math.max(0, Math.min(255, Math.round(O.sample(G0.c[c], W, H, x + DX[i], y + DY[i]))));
  out.data[i * 4 + 3] = 255;
}
P.write(outp, out, { rgb: true, level: 3 });
let mx = 0; for (const s of kept) mx = Math.max(mx, Math.hypot(s.dx, s.dy));
console.log('tiles', tiles, 'kept', kept.length, 'max |shift|', mx.toFixed(2));
const pick = (x, y) => { const i = y * W + x; return DX[i].toFixed(2) + ',' + DY[i].toFixed(2); };
console.log('field at corners: BL(400,1100)', pick(400, 1100), 'BL(600,1200)', pick(600, 1200), 'BR(1800,1100)', pick(1800, 1100), 'BR(1650,1200)', pick(1650, 1200), 'C(1115,1150)', pick(1115, 1150), 'top(1115,450)', pick(1115, 450));
