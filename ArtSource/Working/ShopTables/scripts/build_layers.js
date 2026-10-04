// Splits BG_Table into Back (scenery without the table), Table (Himalayan Ivory) and Front (the
// foreground flowers in front of the table), all in BG_Table's 2230 x 1255 frame.
//
// node build_layers.js <BG_Table.png> <TableMask.png> <tableFill.png> <sceneFill.png> <outDir> <diagDir> <flowerAlpha.bin>
//
//  tableFill  ChatGPT's completed table (a1_s1, 1672 x 941): upscaled, shifted by the measured offset
//             (+0.3, -1.2 frame px) and colour-matched to BG with smooth per-channel gain fields.
//  sceneFill  ChatGPT's terrace fill (b2, 1672 x 941): upscaled and colour-matched the same way.
//  M          the flower matte from flowers.js (soft, 0..1).
//
// Table = BG inside TableMask. Under the flowers (smoothstep 0.08 -> 0.45 of M) ChatGPT's table takes
//         over; where M is below that, the faint flower haze is unmixed: (BG - M*Fhat) / (1 - M).
//         Edge pixels of the silhouette are decontaminated against the Back.
// Back  = BG outside the table; inside it the terrace fill, with the flowers (M * Fhat) laid over it
//         so the Back alone still shows the bushes.
// Front = alpha = max(M, minimum alpha that keeps F in 0..255), F = (BG - (1 - alpha) * L) / alpha,
//         where L = Back + Table, so Back + Table + Front reproduces BG.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const O = require('./imgops.js');
const [, , bgp, maskp, tfillp, sfillp, outDir, diagDir, alphap] = process.argv;
fs.mkdirSync(outDir, { recursive: true }); fs.mkdirSync(diagDir, { recursive: true });
const bg = P.read(bgp), mk = P.read(maskp);
const W = bg.w, H = bg.h, N = W * H;
const B = O.planes(bg).c;
const a = new Float32Array(N);
for (let i = 0; i < N; i++) a[i] = mk.data[i * 4 + 3] / 255;
const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const smooth = (e0, e1, v) => { const t = clamp((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };

// ---------- ChatGPT's completed table ----------
const tfillImg = P.read(tfillp);
// a frame-size input is already aligned (warp_table.js); a 1672 x 941 one gets the global offset
const Tg = tfillImg.w === W ? O.planes(tfillImg).c : O.upscaleShift(tfillImg, W, H, 0.3, -1.2).c;
// its own coverage (chroma key on #00FF00), eroded; colours extended outward so edges never pick up green
const own = new Float32Array(N);
for (let i = 0; i < N; i++) { const r = Tg[0][i], g = Tg[1][i], b = Tg[2][i]; own[i] = (g > 170 && r < 120 && b < 120 && g - Math.max(r, b) > 90) ? 0 : 1; }
const ownEro = O.boxBlur(own, W, H, 2);
const wt = new Float32Array(N);
for (let i = 0; i < N; i++) wt[i] = ownEro[i] > 0.999 ? 1 : 0;
for (let c = 0; c < 3; c++) Tg[c] = O.pushPull(Tg[c], wt, W, H);

// ---------- ChatGPT's terrace ----------
const Sg = O.upscaleShift(P.read(sfillp), W, H, 0, 0).c;

// ---------- flower matte ----------
const M = new Float32Array(fs.readFileSync(alphap).buffer.slice(0));
const near = O.boxBlur(M.map(v => (v > 0 ? 1 : 0)), W, H, 10);

// ---------- colour match ChatGPT's pixels to BG (smooth per-channel gain fields) ----------
function gainField(G, trust, R) {
  const out = [];
  for (let c = 0; c < 3; c++) {
    const lb = O.boxBlur(B[c].map((v, i) => v * trust[i]), W, H, R);
    const lg = O.boxBlur(G[c].map((v, i) => v * trust[i]), W, H, R);
    const lw = O.boxBlur(trust, W, H, R);
    const ratio = new Float32Array(N), w2 = new Float32Array(N);
    for (let i = 0; i < N; i++) if (lw[i] > 0.25) { ratio[i] = (lb[i] + 4) / (lg[i] + 4); w2[i] = 1; }
    out.push(O.pushPull(ratio, w2, W, H));
  }
  return out;
}
const trustT = new Float32Array(N), trustS = new Float32Array(N);
for (let i = 0; i < N; i++) {
  trustT[i] = a[i] > 0.999 && near[i] < 0.01 ? 1 : 0;
  trustS[i] = a[i] === 0 && near[i] < 0.01 ? 1 : 0;
}
const gT = gainField(Tg, trustT, 18);
const gS = gainField(Sg, trustS, 24);
const Tc = [0, 1, 2].map(c => Tg[c].map((v, i) => clamp(v * clamp(gT[c][i], 0.3, 2.5), 0, 255)));
const Sc = [0, 1, 2].map(c => Sg[c].map((v, i) => clamp(v * clamp(gS[c][i], 0.3, 2.5), 0, 255)));

// ---------- local flower colour (the matte's core colours diffused outward) ----------
const Fhat = [0, 1, 2].map(c => O.pushPull(B[c], M.map(v => (v > 0.97 ? 1 : 0)), W, H));

// ---------- what lies behind the silhouette's anti-aliased edge ----------
// The scenery just outside the table, extended inward (push-pull from fully outside pixels), is a
// better estimate of what the edge pixels mix with than ChatGPT's terrace.
const Sext = [0, 1, 2].map(c => O.pushPull(B[c], a.map(v => (v === 0 ? 1 : 0)), W, H));
const Bedge = [0, 1, 2].map(c => Sc[c].map((v, i) => (a[i] > 0 && a[i] < 1 ? Sext[c][i] : v)));

// ---------- Back (provisional: flowers inside the silhouette from the diffused flower colour) ----------
const Bk = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
for (let i = 0; i < N; i++) for (let c = 0; c < 3; c++) Bk[c][i] = a[i] > 0 ? M[i] * Fhat[c][i] + (1 - M[i]) * Bedge[c][i] : B[c][i];

// ---------- Table ----------
const R0 = +(process.env.R0 || 0.08), R1 = +(process.env.R1 || 0.45);
const base = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
const flowerish = new Float32Array(N);
for (let i = 0; i < N; i++) {
  if (a[i] <= 0) continue;
  for (let c = 0; c < 3; c++) {
    let v = B[c][i];
    if (M[i] > 0 && M[i] < 0.98) v = clamp((B[c][i] - M[i] * Fhat[c][i]) / (1 - M[i]), 0, 255); // remove the faint flower haze
    if (a[i] < 1) v = clamp((v - (1 - a[i]) * Bedge[c][i]) / a[i], 0, 255); // decontaminate the silhouette edge
    base[c][i] = v;
  }
  // does the unmixed pixel still look like flower (red petal, green or olive leaf) near the bushes?
  if (M[i] > 0) {
    const r = base[0][i], g = base[1][i], b = base[2][i];
    const petal = (r > 130 && g < 0.4 * r && b < 0.34 * r) || (r > 190 && g < 0.7 * r && b < 0.27 * r);
    const leaf = (g >= 0.62 * r && b <= 0.33 * r && r > 50) || (g > 0.95 * r && g > 30);
    // also: far from ChatGPT's clean table in colour
    const d = Math.hypot(r - Tc[0][i], g - Tc[1][i], b - Tc[2][i]);
    flowerish[i] = (petal || leaf) && M[i] > 0.02 ? 1 : 0;
  }
}
const flw = O.boxBlur(flowerish, W, H, 3);
// regional colour difference between the unmixed BG and ChatGPT's clean table near the bushes:
// a residue of leaves or petals shows up as a local colour offset the clean table does not have
const DD = +(process.env.DD || 22), nearM = O.boxBlur(M.map(v => (v > 0 ? 1 : 0)), W, H, 6);
const lpB = [0, 1, 2].map(c => O.boxBlur(base[c], W, H, 3)), lpT = [0, 1, 2].map(c => O.boxBlur(Tc[c], W, H, 3));
const resid = new Float32Array(N);
for (let i = 0; i < N; i++) if (a[i] > 0 && nearM[i] > 0) resid[i] = smooth(DD, DD * 2, Math.hypot(lpB[0][i] - lpT[0][i], lpB[1][i] - lpT[1][i], lpB[2][i] - lpT[2][i]));
const residS = O.boxBlur(resid, W, H, 2);
const R = new Float32Array(N);
// the override terms ramp in with the matte so the table equals BG wherever there are no flowers
for (let i = 0; i < N; i++) { const gate = smooth(0, +(process.env.GATE || 0.12), M[i]); R[i] = Math.max(smooth(R0, R1, M[i]), gate * clamp(flw[i] * 2.5, 0, 1), gate * residS[i]); }
const T = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
for (let i = 0; i < N; i++) {
  if (a[i] <= 0) continue;
  for (let c = 0; c < 3; c++) T[c][i] = R[i] * Tc[c][i] + (1 - R[i]) * base[c][i];
}

// ---------- silhouette edge without flowers: make Back + Table exact by solving the Back pixel ----------
// (the Back is hidden there except through the edge's partial alpha, and every table shares the edge)
for (let i = 0; i < N; i++) {
  if (!(a[i] > 0 && a[i] < 1) || M[i] > 0) continue;
  for (let c = 0; c < 3; c++) Bk[c][i] = clamp((B[c][i] - a[i] * T[c][i]) / (1 - a[i]), 0, 255);
}

// ---------- Front (only where there are flowers) ----------
const Fa = new Float32Array(N), F = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
let topped = 0;
function solveFront(i) {
  Fa[i] = 0;
  if (M[i] <= 0) return;
  const L = [0, 1, 2].map(c => a[i] * T[c][i] + (1 - a[i]) * Bk[c][i]);
  let amin = 0;
  for (let c = 0; c < 3; c++) {
    const bv = B[c][i] / 255, l = L[c] / 255;
    if (bv < l - 1e-6) amin = Math.max(amin, (l - bv) / Math.max(l, 1e-6));
    else if (bv > l + 1e-6) amin = Math.max(amin, (bv - l) / Math.max(1 - l, 1e-6));
  }
  let al = M[i];
  if (amin > al) { al = amin; topped++; }
  Fa[i] = al;
  for (let c = 0; c < 3; c++) F[c][i] = clamp((B[c][i] - (1 - al) * L[c]) / al, 0, 255);
}
for (let i = 0; i < N; i++) solveFront(i);
// Back, final: inside the silhouette lay the solved flowers (not the diffused colour) over the
// terrace, so the Back has no seam along the silhouette; then re-solve the Front where the Back
// shows through the silhouette's anti-aliased edge.
for (let i = 0; i < N; i++) {
  if (a[i] <= 0 || M[i] <= 0) continue;
  const useF = Fa[i] > 0 && Fa[i] <= M[i] + 0.02;
  for (let c = 0; c < 3; c++) Bk[c][i] = M[i] * (useF ? F[c][i] : Fhat[c][i]) + (1 - M[i]) * Bedge[c][i];
}
topped = 0;
for (let i = 0; i < N; i++) if (a[i] > 0 && a[i] < 1) solveFront(i);

// ---------- write ----------
P.write(outDir + '/BG_HimalayanVista_Back.png', O.toImage({ w: W, h: H, c: Bk }), { rgb: true, level: 9 });
P.write(outDir + '/Table_HimalayanIvory.png', O.toImage({ w: W, h: H, c: T }, a), { level: 9 });
P.write(outDir + '/BG_HimalayanVista_Front.png', O.toImage({ w: W, h: H, c: F }, Fa), { level: 9 });
fs.writeFileSync(diagDir + '/recon.bin', Buffer.from(R.buffer));
const Rimg = O.toImage({ w: W, h: H, c: [R.map(v => v * 255), R.map(v => v * 255), R.map(v => v * 255)] });
P.write(diagDir + '/recon.png', Rimg, { rgb: true, level: 3 });
let fa = 0, outsideM = 0; for (let i = 0; i < N; i++) { if (Fa[i] > 0) fa++; if (Fa[i] > 0 && M[i] === 0) outsideM++; }
console.log('front px', fa, 'front px outside the flower matte', outsideM, 'alpha raised above the matte', topped);
