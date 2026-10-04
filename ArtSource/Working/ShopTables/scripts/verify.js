// node verify.js <BG_Table.png> <Back.png> <Table.png> <Front.png> [recon.bin] [diffOut.png]
// Composites Back + Table + Front (straight alpha, 8-bit like the files) and compares with BG_Table.
// Also reports how far the Table layer is from BG_Table where it is opaque and not reconstructed.
'use strict';
const fs = require('fs');
const P = require('./png.js');
const [, , bgp, backp, tabp, frontp, reconp, diffp] = process.argv;
const bg = P.read(bgp), bk = P.read(backp), tb = P.read(tabp), fr = P.read(frontp);
const W = bg.w, H = bg.h, N = W * H;
const recon = reconp && reconp !== '-' && fs.existsSync(reconp) ? new Float32Array(fs.readFileSync(reconp).buffer.slice(0)) : null;
let sum = 0, max = 0, n = 0, sumT = 0, maxT = 0, nT = 0, over2 = 0, over8 = 0;
const regions = { table: [0, 0, 0], outside: [0, 0, 0], frontArea: [0, 0, 0] };
const diff = P.create(W, H);
for (let i = 0; i < N; i++) {
  const ta = tb.data[i * 4 + 3] / 255, fa = fr.data[i * 4 + 3] / 255;
  let d = 0;
  for (let c = 0; c < 3; c++) {
    let v = bk.data[i * 4 + c];
    v = ta * tb.data[i * 4 + c] + (1 - ta) * v;
    v = fa * fr.data[i * 4 + c] + (1 - fa) * v;
    const e = Math.abs(v - bg.data[i * 4 + c]);
    d = Math.max(d, e); sum += e; n++;
    if (ta > 0) { regions.table[0] += e; regions.table[1]++; regions.table[2] = Math.max(regions.table[2], e); }
    else { regions.outside[0] += e; regions.outside[1]++; regions.outside[2] = Math.max(regions.outside[2], e); }
    if (fa > 0) { regions.frontArea[0] += e; regions.frontArea[1]++; regions.frontArea[2] = Math.max(regions.frontArea[2], e); }
  }
  if (d > max) max = d;
  if (d > 2) over2++;
  if (d > 8) over8++;
  const g = Math.min(255, Math.round(d * 10));
  diff.data[i * 4] = g; diff.data[i * 4 + 1] = g; diff.data[i * 4 + 2] = g; diff.data[i * 4 + 3] = 255;
  // table vs BG where opaque and not reconstructed
  if (ta >= 1 && (!recon || recon[i] === 0) && fa === 0) {
    for (let c = 0; c < 3; c++) { const e = Math.abs(tb.data[i * 4 + c] - bg.data[i * 4 + c]); sumT += e; nT++; if (e > maxT) maxT = e; }
  }
}
const f = r => (r[1] ? (r[0] / r[1]).toFixed(4) : '-') + ' (max ' + r[2] + ')';
console.log('composite vs BG_Table: mean abs diff per channel', (sum / n).toFixed(4), 'max', max, '| px > 2 levels:', over2, '| px > 8 levels:', over8);
console.log('  inside table:', f(regions.table), ' outside table:', f(regions.outside), ' where front alpha > 0:', f(regions.frontArea));
console.log('table layer vs BG_Table where opaque, not reconstructed, no flower: mean', (sumT / nT).toFixed(4), 'max', maxT, 'channels', nT);
if (diffp) P.write(diffp, diff, { rgb: true, level: 3 });
