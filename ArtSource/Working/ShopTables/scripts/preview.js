// Shop product previews (mockup 21's three table tiles) from ChatGPT's renders on #00FF00.
//
// node preview.js <mockup21.png> <outDir> <diagDir> <master.png> <name0> [<variant.png> <name> <tileIndex>]...
//
// Canvas: the tile's picture area, mockup rect [tile centre x - 131, 262, 262, 275] (y 262..537: the table
// and its contact shadow, down to the name label), x 2.3 -> 603 x 633 px.
// The master (Himalayan Ivory) is keyed and placed so its table bbox equals the mockup table's
// (x 408.5..656.5, y 279.5..487.5 in tile 1; the same tile-relative box in tiles 2 and 3).
// Variants are keyed, aligned to the master (global shift + smooth local warp on gradient NCC) and
// take the master's alpha, so all three have the identical silhouette. Each is colour-matched to its
// own mockup tile (per-channel affine fit on blurred pixels inside the table). The contact shadow is a
// soft ellipse fitted to tile 1's darkening around the table, the same for all three.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const { keyGreen } = require('./key.js');
const args = process.argv.slice(2);
const [mockp, outDir, diagDir, masterp, name0] = args;
const variants = [];
for (let k = 5; k + 2 < args.length + 1; k += 3) if (args[k]) variants.push({ path: args[k], name: args[k + 1], tile: +args[k + 2] });
fs.mkdirSync(outDir, { recursive: true }); fs.mkdirSync(diagDir, { recursive: true });
const mock = P.read(mockp);
const MW = mock.w;
const TILE_X = [399.8, 683.4, 971.7], TILE_W = [266.1, 268.5, 266.3]; // tile rects from ShopTablesLayout
// table bbox in tile 1 (mockup px) and its offset from the tile's left edge
const TB = { x0: 408.5, x1: 656.5, y0: 279.5, y1: 487.5 };
const CW = 603, CH = 633, S = 2.3; // canvas px, scale
const canvasX = t => TILE_X[t] + TILE_W[t] / 2 - 131; // canvas origin x (mockup px) for tile t: centred on the tile; y origin 262
const CY = 262;
const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const report = {};

// ---------- master: key and place ----------
function keyed(path) { const im = P.read(path); const k = keyGreen(im); return { im, k }; }
const M = keyed(masterp);
const W0 = M.im.w, H0 = M.im.h;
let bx0 = 1e9, bx1 = -1, by0 = 1e9, by1 = -1;
for (let y = 0; y < H0; y++) for (let x = 0; x < W0; x++) if (M.k.alpha[y * W0 + x] > 0.5) { bx0 = Math.min(bx0, x); bx1 = Math.max(bx1, x); by0 = Math.min(by0, y); by1 = Math.max(by1, y); }
// p1 px -> canvas px (tile 1 geometry; the canvas is tile-relative so the same for all tiles)
const sx = (TB.x1 - TB.x0) * S / (bx1 + 1 - bx0), sy = (TB.y1 - TB.y0) * S / (by1 + 1 - by0);
const ox = (TB.x0 - canvasX(0)) * S - bx0 * sx, oy = (TB.y0 - CY) * S - by0 * sy;
report.master = { bbox: [bx0, by0, bx1, by1], scaleX: +sx.toFixed(5), scaleY: +sy.toFixed(5), aspectStretch: +(sy / sx).toFixed(4) };
// resample a keyed image into the canvas (premultiplied, area-weighted via supersampling 3x3)
function place(k, w, h) {
  const A = new Float32Array(CW * CH), C = [new Float32Array(CW * CH), new Float32Array(CW * CH), new Float32Array(CW * CH)];
  const pm = [0, 1, 2].map(c => k.fg[c].map((v, i) => v * k.alpha[i]));
  for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) {
    let a = 0, r = 0, g = 0, b = 0;
    for (let j = 0; j < 3; j++) for (let i = 0; i < 3; i++) {
      const fx = ((x + (i + 0.5) / 3) - ox) / sx - 0.5, fy = ((y + (j + 0.5) / 3) - oy) / sy - 0.5;
      if (fx < -0.5 || fy < -0.5 || fx > w - 0.5 || fy > h - 0.5) continue;
      a += O.sample(k.alpha, w, h, fx, fy); r += O.sample(pm[0], w, h, fx, fy); g += O.sample(pm[1], w, h, fx, fy); b += O.sample(pm[2], w, h, fx, fy);
    }
    const o = y * CW + x; A[o] = a / 9; C[0][o] = r / 9; C[1][o] = g / 9; C[2][o] = b / 9; // premultiplied
  }
  return { A, C };
}
const MP = place(M.k, W0, H0);
const alphaM = MP.A;

// ---------- mockup tile sampling at canvas resolution ----------
function mockAt(t) {
  const X0 = canvasX(t), out = [new Float32Array(CW * CH), new Float32Array(CW * CH), new Float32Array(CW * CH)];
  const mp = O.planes(mock).c;
  for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) {
    const fx = X0 + (x + 0.5) / S - 0.5, fy = CY + (y + 0.5) / S - 0.5;
    for (let c = 0; c < 3; c++) out[c][y * CW + x] = O.sample(mp[c], MW, mock.h, fx, fy);
  }
  return out;
}
// tile background per row: average of the tile's inner side margins (mockup px), at canvas rows
function tileBg(t) {
  const X0 = TILE_X[t], bg = [new Float32Array(CH), new Float32Array(CH), new Float32Array(CH)];
  for (let y = 0; y < CH; y++) {
    const my = Math.round(CY + (y + 0.5) / S - 0.5);
    for (let c = 0; c < 3; c++) {
      let s = 0; for (const dx of [4, 5, 261, 262]) s += mock.data[(my * MW + Math.round(X0 + dx)) * 4 + c];
      bg[c][y] = s / 4;
    }
  }
  return bg;
}

// ---------- alignment of a variant to the master ----------
function lumOf(C, A) { const L = new Float32Array(CW * CH); for (let i = 0; i < CW * CH; i++) L[i] = A[i] > 0 ? (0.2126 * C[0][i] + 0.7152 * C[1][i] + 0.0722 * C[2][i]) / Math.max(A[i], 1e-3) : 0; return L; }
function gradOf(L) { const g = new Float32Array(CW * CH); for (let y = 1; y < CH - 1; y++) for (let x = 1; x < CW - 1; x++) { const i = y * CW + x; g[i] = Math.hypot(L[i + 1] - L[i - 1], L[i + CW] - L[i - CW]); } return g; }
function alignVariant(VP) {
  // structure from the alpha edge (silhouette) + luminance gradients inside the rim/apron
  const GM = gradOf(lumOf(MP.C, alphaM)), GA = gradOf(alphaM.map(v => v * 255));
  const GV = gradOf(lumOf(VP.C, VP.A)), GVA = gradOf(VP.A.map(v => v * 255));
  const comb = (g, ga) => g.map((v, i) => v + ga[i]);
  const gm = comb(GM, GA), gv = comb(GV, GVA);
  function ncc(tx, ty, T, dx, dy, TH) {
    TH = TH || T;
    let sa = 0, sb = 0, sab = 0, saa = 0, sbb = 0, n = 0;
    for (let y = ty; y < ty + TH; y++) for (let x = tx; x < tx + T; x++) {
      const xx = x + dx, yy = y + dy; if (xx < 1 || yy < 1 || xx >= CW - 1 || yy >= CH - 1) continue;
      const a = gm[y * CW + x], b = gv[yy * CW + xx]; sa += a; sb += b; sab += a * b; saa += a * a; sbb += b * b; n++;
    }
    const cov = sab / n - sa * sb / n / n, va = saa / n - (sa / n) ** 2, vb = sbb / n - (sb / n) ** 2;
    return va < 1 ? -2 : cov / Math.sqrt(va * vb + 1e-9);
  }
  // global
  let best = -2, gx = 0, gy = 0;
  for (let dy = -10; dy <= 10; dy++) for (let dx = -10; dx <= 10; dx++) { const v = ncc(20, 30, CW - 40, dx, dy, CH - 120); if (v > best) { best = v; gx = dx; gy = dy; } }
  // local tiles
  const T = 48, samples = [];
  for (let ty = 20; ty + T < CH - 60; ty += 24) for (let tx = 10; tx + T < CW - 10; tx += 24) {
    let cnt = 0; for (let y = ty; y < ty + T; y += 3) for (let x = tx; x < tx + T; x += 3) if (alphaM[y * CW + x] > 0.5) cnt++;
    if (cnt < (T / 3) * (T / 3) * 0.4) continue;
    let b = -2, bx = 0, by = 0; const Sx = {};
    for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) { const v = ncc(tx, ty, T, gx + dx, gy + dy); Sx[dx + ',' + dy] = v; if (v > b) { b = v; bx = dx; by = dy; } }
    if (b < 0.5 || Math.abs(bx) === 4 || Math.abs(by) === 4) continue;
    samples.push({ x: tx + T / 2, y: ty + T / 2, dx: gx + bx, dy: gy + by, q: b });
  }
  const kept = samples.filter(s => { const nb = samples.filter(t => Math.hypot(t.x - s.x, t.y - s.y) < 100); const md = k => { const v = nb.map(t => t[k]).sort((a, b) => a - b); return v[v.length >> 1]; }; return Math.abs(s.dx - md('dx')) <= 1 && Math.abs(s.dy - md('dy')) <= 1; });
  const N = CW * CH, dxs = new Float32Array(N), dys = new Float32Array(N), wts = new Float32Array(N);
  for (const s of kept) { const i = Math.round(s.y) * CW + Math.round(s.x); dxs[i] = s.dx * s.q; dys[i] = s.dy * s.q; wts[i] = s.q; }
  const bxs = O.boxBlur(dxs, CW, CH, 16), bys = O.boxBlur(dys, CW, CH, 16), bws = O.boxBlur(wts, CW, CH, 16);
  const fx = new Float32Array(N), fy = new Float32Array(N), fw = new Float32Array(N);
  for (let i = 0; i < N; i++) if (bws[i] > 1e-6) { fx[i] = bxs[i] / bws[i]; fy[i] = bys[i] / bws[i]; fw[i] = Math.min(1, bws[i] * 200); }
  const DX = kept.length ? O.boxBlur(O.pushPull(fx, fw, CW, CH), CW, CH, 10) : new Float32Array(N).fill(gx);
  const DY = kept.length ? O.boxBlur(O.pushPull(fy, fw, CW, CH), CW, CH, 10) : new Float32Array(N).fill(gy);
  const C = [0, 1, 2].map(() => new Float32Array(N)), A = new Float32Array(N);
  for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) {
    const i = y * CW + x, X = x + DX[i], Y = y + DY[i];
    A[i] = O.sample(VP.A, CW, CH, X, Y);
    for (let c = 0; c < 3; c++) C[c][i] = O.sample(VP.C[c], CW, CH, X, Y);
  }
  // silhouette IoU before/after alignment
  let i0 = 0, u0 = 0, i1 = 0, u1 = 0;
  for (let i = 0; i < N; i++) { i0 += Math.min(alphaM[i], VP.A[i]); u0 += Math.max(alphaM[i], VP.A[i]); i1 += Math.min(alphaM[i], A[i]); u1 += Math.max(alphaM[i], A[i]); }
  let mx = 0; for (const s of kept) mx = Math.max(mx, Math.hypot(s.dx, s.dy));
  return { C, A, info: { globalShift: [gx, gy], tiles: samples.length, kept: kept.length, maxShift: +mx.toFixed(2), iouBefore: +(i0 / u0).toFixed(4), iouAfter: +(i1 / u1).toFixed(4) } };
}

// ---------- zones of the preview (from the master): top interior, key band ring, body ----------
const ZONES = (() => {
  const N = CW * CH;
  const st = [0, 1, 2].map(c => MP.C[c].map((v, i) => (alphaM[i] > 1e-3 ? v / alphaM[i] : 0)));
  // flood fill of the ivory top from its centre
  const sx0 = Math.round(CW / 2), sy0 = Math.round((360 - CY) * S);
  const ref = [st[0][sy0 * CW + sx0], st[1][sy0 * CW + sx0], st[2][sy0 * CW + sx0]];
  const fl = new Uint8Array(N), stack = [sy0 * CW + sx0]; fl[stack[0]] = 1;
  while (stack.length) {
    const p = stack.pop(), x = p % CW, y = (p / CW) | 0;
    for (const q of [x > 0 ? p - 1 : -1, x < CW - 1 ? p + 1 : -1, y > 0 ? p - CW : -1, y < CH - 1 ? p + CW : -1]) {
      if (q < 0 || fl[q] || alphaM[q] < 0.99) continue;
      const dg = Math.abs(st[0][q] - ref[0]) + Math.abs(st[1][q] - ref[1]) + Math.abs(st[2][q] - ref[2]);
      const dl = Math.abs(st[0][q] - st[0][p]) + Math.abs(st[1][q] - st[1][p]) + Math.abs(st[2][q] - st[2][p]);
      if (dg < 70 && dl < 30) { fl[q] = 1; stack.push(q); }
    }
  }
  // holes (motifs) -> interior: fill everything not reachable from the canvas border without crossing fl
  const out = new Uint8Array(N), s2 = [];
  for (let x = 0; x < CW; x++) for (const y of [0, CH - 1]) { const i = y * CW + x; if (!fl[i] && !out[i]) { out[i] = 1; s2.push(i); } }
  for (let y = 0; y < CH; y++) for (const x of [0, CW - 1]) { const i = y * CW + x; if (!fl[i] && !out[i]) { out[i] = 1; s2.push(i); } }
  while (s2.length) { const p = s2.pop(), x = p % CW, y = (p / CW) | 0; for (const q of [x > 0 ? p - 1 : -1, x < CW - 1 ? p + 1 : -1, y > 0 ? p - CW : -1, y < CH - 1 ? p + CW : -1]) if (q >= 0 && !out[q] && !fl[q]) { out[q] = 1; s2.push(q); } }
  const interior = new Float32Array(N); for (let i = 0; i < N; i++) interior[i] = out[i] ? 0 : 1;
  const ringW = +(process.env.RING || 22);
  const dil = O.boxBlur(interior, CW, CH, ringW).map(v => (v > 0.01 ? 1 : 0));
  const band = new Float32Array(N), body = new Float32Array(N);
  for (let i = 0; i < N; i++) { if (alphaM[i] <= 0) continue; if (interior[i]) continue; if (dil[i]) band[i] = 1; else body[i] = 1; }
  const soft = z => O.boxBlur(z, CW, CH, 1);
  const zi = soft(interior), zb = soft(band), zo = soft(body);
  for (let i = 0; i < N; i++) { const t = zi[i] + zb[i] + zo[i]; if (t > 0) { zi[i] /= t; zb[i] /= t; zo[i] /= t; } }
  return { interior: zi, band: zb, body: zo, hard: { interior, band, body } };
})();
// ---------- colour match to the mockup tile ----------
// band and body: per-channel mean gain per zone. Top interior: only the playing surface is corrected
// (pixels near the surface's own colour, Gaussian weight), so the gold/brass motifs keep their colour.
function trimmedMean(px) {
  let m = [0, 1, 2].map(c => px.reduce((s, p) => s + p[c], 0) / px.length), sd = 60;
  for (let it = 0; it < 6; it++) {
    const keep = px.filter(p => Math.hypot(p[0] - m[0], p[1] - m[1], p[2] - m[2]) < 2 * sd);
    if (keep.length < 20) break;
    m = [0, 1, 2].map(c => keep.reduce((s, p) => s + p[c], 0) / keep.length);
    sd = Math.max(8, Math.sqrt(keep.reduce((s, p) => s + (p[0] - m[0]) ** 2 + (p[1] - m[1]) ** 2 + (p[2] - m[2]) ** 2, 0) / keep.length));
  }
  return { m, sd };
}
function colourMatch(C, A, t) {
  const N = CW * CH;
  const straight = [0, 1, 2].map(c => C[c].map((v, i) => (A[i] > 1e-3 ? v / A[i] : 0)));
  const mk = mockAt(t);
  const inner = O.boxBlur(A.map(v => (v > 0.99 ? 1 : 0)), CW, CH, 3).map(v => (v > 0.999 ? 1 : 0));
  const gains = {};
  const res = [0, 1, 2].map(() => new Float32Array(N));
  for (const z of ['band', 'body']) {
    const h = ZONES.hard[z];
    const mv = [0, 0, 0], mm = [0, 0, 0]; let n = 0;
    for (let i = 0; i < N; i++) if (h[i] && inner[i]) { for (let c = 0; c < 3; c++) { mv[c] += straight[c][i]; mm[c] += mk[c][i]; } n++; }
    const g = [0, 1, 2].map(c => clamp((mm[c] + 2) / (mv[c] + 2), 0.25, 4));
    gains[z] = { variantMean: mv.map(v => +(v / n).toFixed(1)), mockupMean: mm.map(v => +(v / n).toFixed(1)), gain: g.map(v => +v.toFixed(3)) };
    const w = ZONES[z];
    for (let i = 0; i < N; i++) if (w[i] > 0) for (let c = 0; c < 3; c++) res[c][i] += w[i] * straight[c][i] * g[c];
  }
  {
    const h = ZONES.hard.interior, pv = [], pm = [];
    for (let i = 0; i < N; i += 2) if (h[i] && inner[i]) { pv.push([straight[0][i], straight[1][i], straight[2][i]]); pm.push([mk[0][i], mk[1][i], mk[2][i]]); }
    const V = trimmedMean(pv), Mk = trimmedMean(pm);
    const sig = Math.max(20, 2.5 * V.sd);
    const g = [0, 1, 2].map(c => clamp((Mk.m[c] + 2) / (V.m[c] + 2), 0.25, 4));
    gains.surface = { variantMean: V.m.map(v => +v.toFixed(1)), mockupMean: Mk.m.map(v => +v.toFixed(1)), gain: g.map(v => +v.toFixed(3)), sigma: +sig.toFixed(1) };
    const w = ZONES.interior;
    for (let i = 0; i < N; i++) {
      if (w[i] <= 0) continue;
      const d = Math.hypot(straight[0][i] - V.m[0], straight[1][i] - V.m[1], straight[2][i] - V.m[2]);
      const k = Math.exp(-d * d / (2 * sig * sig));
      for (let c = 0; c < 3; c++) res[c][i] += w[i] * straight[c][i] * (1 + k * (g[c] - 1));
    }
  }
  const out = res.map(p => p.map(v => clamp(v, 0, 255)));
  return { out, coef: gains };
}

// ---------- contact shadow: soft ellipse fitted to tile 1 ----------
function shadowField(p) {
  // p: [cx, cy, ax, ay, sigma, opacity] in canvas px
  const N = CW * CH, e = new Float32Array(N);
  for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) { const u = (x - p[0]) / p[2], v = (y - p[1]) / p[3]; e[y * CW + x] = u * u + v * v <= 1 ? 1 : 0; }
  const r = Math.max(1, Math.round(p[4] / 1.6)); // 3-pass box ~ gaussian
  return O.boxBlur(e, CW, CH, r).map(v => v * p[5]);
}
const SHC = [52, 36, 26]; // warm dark shadow colour
function fitShadow() {
  const mk = mockAt(0), bg = tileBg(0);
  // observations: pixels outside the table (master alpha < 0.02, dilated), below the table top, not label
  const outside = O.boxBlur(alphaM.map(v => (v > 0.02 ? 1 : 0)), CW, CH, 3).map(v => (v > 0 ? 0 : 1));
  const obs = [];
  for (let y = Math.round((300 - CY) * S); y < CH - 4; y += 2) for (let x = 4; x < CW - 4; x += 2) {
    const i = y * CW + x; if (!outside[i]) continue;
    // darkening alpha estimate a = (bg - m) / (bg - shadowColour), per channel averaged
    let a = 0; for (let c = 0; c < 3; c++) a += (bg[c][y] - mk[c][i]) / Math.max(bg[c][y] - SHC[c], 1);
    obs.push([x, y, clamp(a / 3, -0.2, 1)]);
  }
  const err = p => { const f = shadowField(p); let e = 0; for (const [x, y, a] of obs) { const d = f[y * CW + x] - a; e += d * d; } return e / obs.length; };
  // coarse search then coordinate descent
  let best = [CW / 2, (472 - CY) * S, 0.46 * CW, 18 * S, 12 * S, 0.5], be = err(best);
  const steps = [8, 8, 10, 6, 6, 0.06];
  for (let round = 0; round < 6; round++) {
    for (let k = 0; k < 6; k++) for (const sgn of [-1, 1]) {
      const p = best.slice(); p[k] += sgn * steps[k];
      if (p[2] < 10 || p[3] < 4 || p[4] < 2 || p[5] < 0.05) continue;
      const e = err(p); if (e < be) { be = e; best = p; }
    }
    if (round % 2 === 1) for (let k = 0; k < 6; k++) steps[k] *= 0.6;
  }
  return { p: best, rmse: Math.sqrt(be), n: obs.length };
}
const SH = fitShadow();
report.shadow = { centre: [+SH.p[0].toFixed(1), +SH.p[1].toFixed(1)], semiAxes: [+SH.p[2].toFixed(1), +SH.p[3].toFixed(1)], sigma: +SH.p[4].toFixed(1), opacity: +SH.p[5].toFixed(3), colour: SHC, fitRmseAlpha: +SH.rmse.toFixed(4), samples: SH.n };
const shadowA = shadowField(SH.p);
// fade the shadow to zero over the outer 18 px of the canvas, so it never ends in a visible edge
for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) { const e = Math.min(x, CW - 1 - x, y, CH - 1 - y); if (e < 18) { const t = e / 18; shadowA[y * CW + x] *= t * t * (3 - 2 * t); } }

// ---------- compose, write, check ----------
function compose(name, colours, t) {
  const N = CW * CH, img = P.create(CW, CH);
  for (let i = 0; i < N; i++) {
    const a = alphaM[i], s = shadowA[i] * (1 - a);
    const ao = a + s;
    for (let c = 0; c < 3; c++) img.data[i * 4 + c] = ao > 0 ? Math.round(clamp((colours[c][i] * a + SHC[c] * s) / ao, 0, 255)) : 0;
    img.data[i * 4 + 3] = Math.round(clamp(ao, 0, 1) * 255);
  }
  P.write(outDir + '/Preview_' + name + '.png', img, { level: 9 });
  // check against the mockup tile: composite over the per-row tile background
  const mk = mockAt(t), bg = tileBg(t);
  let sum = 0, n = 0, sumT = 0, nT = 0;
  const cmp = P.create(CW * 2, CH);
  for (let y = 0; y < CH; y++) for (let x = 0; x < CW; x++) {
    const i = y * CW + x, a = img.data[i * 4 + 3] / 255;
    for (let c = 0; c < 3; c++) {
      const v = img.data[i * 4 + c] * a + bg[c][y] * (1 - a);
      const d = Math.abs(v - mk[c][i]); sum += d; n++; if (alphaM[i] > 0.5) { sumT += d; nT++; }
      cmp.data[(y * CW * 2 + x) * 4 + c] = Math.round(mk[c][i]); cmp.data[(y * CW * 2 + CW + x) * 4 + c] = Math.round(v);
    }
    cmp.data[(y * CW * 2 + x) * 4 + 3] = 255; cmp.data[(y * CW * 2 + CW + x) * 4 + 3] = 255;
  }
  P.write(diagDir + '/compare_' + name + '.png', cmp, { rgb: true, level: 3 });
  return { meanAbsDiffCanvas: +(sum / n).toFixed(2), meanAbsDiffTable: +(sumT / nT).toFixed(2) };
}
const cm0 = colourMatch(MP.C, alphaM, 0);
report[name0] = { colourAffine: cm0.coef, check: compose(name0, cm0.out, 0) };
for (const v of variants) {
  const K = keyed(v.path);
  const VP = place(K.k, K.im.w, K.im.h);
  const al = alignVariant(VP);
  // colours of the aligned variant with the master's alpha: un-premultiply by the variant's own alpha
  const straight = [0, 1, 2].map(c => al.C[c].map((x, i) => (al.A[i] > 1e-3 ? x / al.A[i] : 0)));
  // where the master has table but the variant does not (tiny slivers), extend the variant's colours
  const ext = straight.map(p => O.pushPull(p, al.A.map(a => (a > 0.98 ? 1 : 0)), CW, CH));
  const pm = ext.map(p => p.map((x, i) => x * alphaM[i]));
  const cm = colourMatch(pm, alphaM, v.tile);
  report[v.name] = { alignment: al.info, colourAffine: cm.coef, check: compose(v.name, cm.out, v.tile) };
}
fs.writeFileSync(diagDir + '/preview_report.json', JSON.stringify(report, null, 1));
console.log(JSON.stringify(report));
