#!/usr/bin/env node
// po.js: image helpers for the Play Online art batch that kitcontrols and shopui do not have.
// Images: linear-light colour, straight alpha (png.js). Colours are compared in sRGB levels (0..255).
// Run from the repo root: node ArtSource/Working/PlayOnline/tools/po.js <command> [options]
'use strict';
const P = require('./png.js');

const USAGE = `po.js <command> [options]
  rowmatch --in png --out f --screen png --rect x,y,w,h --cols "a-b;c-d" [--rows "y0-y1"] [--thr 0.98] [--smooth 1.5]
           [--strength 1] [--masked [--erode 1.5] [--minsamples 6]] [--print]
           the sprite (drawn at the mockup rect) gets, row by row, the mean colour of the mockup's same row over
           the given mockup columns: an sRGB offset per row (smoothed), applied to the whole row. --masked: only
           where the sprite is opaque (an icon's own pixels)
  colmatch  the same, column by column (--rows are the sampled rows, --cols limits the columns)
  midflat  --in png --out f --x0 a --x1 b      every row: columns x0..x1-1 take that row's mean colour and alpha
  ringpaint --in png --out f --center cx,cy --bands "r0-r1;.." --screen png --mcenter mx,my --mbands "R0-R1;.."
           [--step 5] [--skip "a0-a1;.."] [--keepin r] [--outer R] [--smooth 1] [--minbands 1] [--maxbands 0,2]
           rings of a disc sprite (radii in sprite px) painted with the mockup's colours in its rings (radii in
           mockup px) angle by angle (0 = right, 90 = down); min/max bands take the darkest/lightest point
           along each ray (the core of a thin line); inside keepin the sprite is kept, outside --outer cleared
  ringfit  --screen png --center cx,cy --from r0 --to r1 [--step 5] [--skip "a0-a1"] [--light]
           the circle through the dip (or bump) of a ring along rays
  radprof  --screen png --center cx,cy --rmax R [--step 0.5] [--angles "a0-a1;.."]   mean colour against radius
  vprof    --screen png --cols "a-b;.." --from y0 --to y1 [--step 1]   mean colour of each row over the columns
  hprof2   --screen png --rows "a-b;.." --from x0 --to x1 [--step 1]   mean colour of each column over the rows
  pillframe --in png --out f --bands "w:#top>#mid>#bottom;.." [--unit 2.3] [--fill keep|#top>#bottom] [--insety d]
           a pill filling the image: bands (mockup px, outer edge inwards) in vertical gradients; inside them
           the picture is kept (or filled); cleared outside (anti-aliased)
  compose  --screen png --layers "spr.png@x,y,w,h[@nine=L,T,R,B][@unit=2.3][@flipx][@flipy][@op=0.5];.."
           [--base mockup|#hex|plate.png] [--region x,y,w,h] [--exclude "x,y,w,h;.."] [--measure x,y,w,h] --out f
           the region rebuilt from the sprites (mockup resolution, linear-light blending like Unity in Linear
           colour space) and the rms error against the mockup in --measure (excluded rects left out)
  premix   --in png --out f --over #hex      the sprite flattened on a colour
  fill     --in png --out f --color #hex     every pixel takes the colour, alpha kept
`;

// ---------------------------------------------------------------------------------------------------
function opts(argv) {
  const d = {};
  for (let i = 0; i < argv.length; i++) {
    if (!argv[i].startsWith('--')) continue;
    const k = argv[i].slice(2);
    if (i + 1 < argv.length && !argv[i + 1].startsWith('--')) d[k] = argv[++i]; else d[k] = 'true';
  }
  return {
    has: k => k in d,
    get: (k, def) => (k in d ? d[k] : def),
    need: k => { if (!(k in d)) throw new Error('missing --' + k); return d[k]; },
    num: (k, def) => (k in d ? parseFloat(d[k]) : def),
    nums: (k, n) => { if (!(k in d)) throw new Error('missing --' + k); const a = d[k].split(',').map(Number); if (n && a.length !== n) throw new Error(`--${k} needs ${n} numbers`); return a; },
  };
}
const S = l => P.toSrgb01(l) * 255;
const L = s => P.toLinear(Math.max(0, Math.min(1, s / 255)));
const ranges = s => s.split(';').filter(Boolean).map(p => { const i = p.indexOf('-', 1); return [parseFloat(p.slice(0, i)), parseFloat(p.slice(i + 1))]; });
const rects = s => (s ? s.split(';').filter(Boolean).map(p => p.split(',').map(Number)) : []);
const hex = c => '#' + [c[0], c[1], c[2]].map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0').toUpperCase()).join('');
const luma = c => 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];

/** Bilinear sample at (x, y) in pixel coordinates (pixel centres at +0.5), sRGB 0..255. */
function sample(m, x, y) {
  const fx = x - 0.5, fy = y - 0.5, x0 = Math.floor(fx), y0 = Math.floor(fy), tx = fx - x0, ty = fy - y0;
  let r = 0, g = 0, b = 0;
  for (let k = 0; k < 4; k++) {
    const xx = Math.min(m.w - 1, Math.max(0, x0 + (k & 1))), yy = Math.min(m.h - 1, Math.max(0, y0 + (k >> 1)));
    const w = ((k & 1) ? tx : 1 - tx) * ((k >> 1) ? ty : 1 - ty), i = yy * m.w + xx;
    r += w * S(m.r[i]); g += w * S(m.g[i]); b += w * S(m.b[i]);
  }
  return [r, g, b];
}

function gauss(v, ok, sigma, circular) {
  const n = v.length, o = new Array(n).fill(NaN), rad = Math.max(1, Math.ceil(sigma * 3));
  for (let i = 0; i < n; i++) {
    let s = 0, ws = 0;
    for (let d = -rad; d <= rad; d++) {
      let j = i + d;
      if (circular) j = ((j % n) + n) % n; else if (j < 0 || j >= n) continue;
      if (!ok[j]) continue;
      const w = sigma <= 0 ? (d === 0 ? 1 : 0) : Math.exp(-0.5 * d * d / (sigma * sigma));
      s += w * v[j]; ws += w;
    }
    if (ws > 0) o[i] = s / ws;
  }
  for (let i = 0; i < n; i++) {
    if (!Number.isNaN(o[i])) continue;
    for (let d = 1; d < n; d++) {
      const a = circular ? ((i - d) % n + n) % n : i - d, b = circular ? (i + d) % n : i + d;
      if (a >= 0 && a < n && !Number.isNaN(o[a]) && ok[a]) { o[i] = o[a]; break; }
      if (b >= 0 && b < n && !Number.isNaN(o[b]) && ok[b]) { o[i] = o[b]; break; }
    }
    if (Number.isNaN(o[i])) o[i] = 0;
  }
  return o;
}

function inArc(a, a0, a1) {
  a = ((a % 360) + 360) % 360; a0 = ((a0 % 360) + 360) % 360; a1 = ((a1 % 360) + 360) % 360;
  return a0 <= a1 ? a >= a0 && a <= a1 : a >= a0 || a <= a1;
}

// ---------------------------------------------------------------------------------------------------
function lineMatch(o, byRow) {
  const s = P.load(o.need('in')), m = P.load(o.need('screen')), r = o.nums('rect', 4);
  const rg = ranges(o.need(byRow ? 'cols' : 'rows'));
  const thr = o.num('thr', 0.98), sigma = o.num('smooth', 1.5), strength = o.num('strength', 1), masked = o.has('masked');
  const erode = o.num('erode', 0), minS = o.num('minsamples', 1);
  const opaqueAt = (mx, my) => { const xx = Math.floor((mx - r[0]) / sx), yy = Math.floor((my - r[1]) / sy); return xx >= 0 && yy >= 0 && xx < s.w && yy < s.h && s.a[yy * s.w + xx] >= thr; };
  const limit = o.has(byRow ? 'rows' : 'cols') ? ranges(o.get(byRow ? 'rows' : 'cols')) : null;
  const n = byRow ? s.h : s.w, len = byRow ? s.w : s.h, sx = r[2] / s.w, sy = r[3] / s.h;
  const d = [new Array(n).fill(0), new Array(n).fill(0), new Array(n).fill(0)], ok = new Array(n).fill(false);
  const inLimit = j => { if (!limit) return true; const mp = byRow ? r[1] + (j + 0.5) * sy : r[0] + (j + 0.5) * sx; return limit.some(q => mp >= q[0] && mp < q[1]); };
  for (let j = 0; j < n; j++) {
    if (!inLimit(j)) continue;
    const mpos = byRow ? r[1] + (j + 0.5) * sy : r[0] + (j + 0.5) * sx;
    let mr = 0, mg = 0, mb = 0, mk = 0;
    for (const q of rg) {
      for (let t = q[0] + 0.25; t < q[1]; t += 0.5) {
        if (masked) {
          const qx = byRow ? t : mpos, qy = byRow ? mpos : t;
          if (!opaqueAt(qx, qy)) continue;
          if (erode > 0 && !(opaqueAt(qx - erode, qy) && opaqueAt(qx + erode, qy) && opaqueAt(qx, qy - erode) && opaqueAt(qx, qy + erode))) continue;
        }
        const c = byRow ? sample(m, t, mpos) : sample(m, mpos, t);
        mr += c[0]; mg += c[1]; mb += c[2]; mk++;
      }
    }
    let pr = 0, pg = 0, pb = 0, pk = 0;
    for (let k = 0; k < len; k++) {
      const tpos = byRow ? r[0] + (k + 0.5) * sx : r[1] + (k + 0.5) * sy;
      if (!rg.some(q => tpos >= q[0] && tpos < q[1])) continue;
      const i = byRow ? j * s.w + k : k * s.w + j;
      if (s.a[i] < thr) continue;
      pr += S(s.r[i]); pg += S(s.g[i]); pb += S(s.b[i]); pk++;
    }
    if (mk < minS || !pk) continue;
    d[0][j] = mr / mk - pr / pk; d[1][j] = mg / mk - pg / pk; d[2][j] = mb / mk - pb / pk; ok[j] = true;
  }
  const dd = d.map(v => gauss(v, ok, sigma, false));
  let sum = 0, cnt = 0;
  for (let j = 0; j < n; j++) {
    if (!inLimit(j)) continue;
    if (ok[j]) { sum += (Math.abs(dd[0][j]) + Math.abs(dd[1][j]) + Math.abs(dd[2][j])) / 3; cnt++; }
    for (let k = 0; k < len; k++) {
      const i = byRow ? j * s.w + k : k * s.w + j;
      if (s.a[i] <= 0) continue;
      s.r[i] = L(S(s.r[i]) + strength * dd[0][j]); s.g[i] = L(S(s.g[i]) + strength * dd[1][j]); s.b[i] = L(S(s.b[i]) + strength * dd[2][j]);
    }
  }
  P.save(s, o.need('out'));
  console.log(`${o.need('out')}  ${s.w}x${s.h}  ${byRow ? 'rows' : 'columns'} matched: ${cnt}/${n}, mean |offset| ${(cnt ? sum / cnt : 0).toFixed(1)} levels`);
  if (o.has('print')) for (let j = 0; j < n; j += Math.max(1, Math.floor(n / 24))) console.log(`  ${String(j).padStart(4)}: ${ok[j] ? '' : '(nearest) '}${dd.map(v => v[j].toFixed(1).padStart(6)).join(' ')}`);
}

function midFlat(o) {
  const s = P.load(o.need('in')), x0 = o.num('x0', 0), x1 = o.num('x1', s.w);
  for (let y = 0; y < s.h; y++) {
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let x = x0; x < x1; x++) { const i = y * s.w + x; pr += s.r[i] * s.a[i]; pg += s.g[i] * s.a[i]; pb += s.b[i] * s.a[i]; pa += s.a[i]; }
    const a = pa / (x1 - x0), rr = pa > 0 ? pr / pa : 0, gg = pa > 0 ? pg / pa : 0, bb = pa > 0 ? pb / pa : 0;
    for (let x = x0; x < x1; x++) { const i = y * s.w + x; s.r[i] = rr; s.g[i] = gg; s.b[i] = bb; s.a[i] = a; }
  }
  P.save(s, o.need('out'));
  console.log(`${o.need('out')}  ${s.w}x${s.h}  columns ${x0}..${x1 - 1} flattened per row`);
}

function ringPaint(o) {
  const s = P.load(o.need('in')), m = P.load(o.need('screen'));
  const c = o.nums('center', 2), mc = o.nums('mcenter', 2), bands = ranges(o.need('bands')), mb = ranges(o.need('mbands'));
  if (bands.length !== mb.length) throw new Error('--bands and --mbands need the same count');
  const step = o.num('step', 5), skip = o.has('skip') ? ranges(o.get('skip')) : [];
  const keepin = o.num('keepin', bands[0][0]), outer = o.num('outer', bands[bands.length - 1][1]);
  const minB = new Set(o.has('minbands') ? o.get('minbands').split(',').map(Number) : []), maxB = new Set(o.has('maxbands') ? o.get('maxbands').split(',').map(Number) : []);
  const nb = Math.round(360 / step), col = [];
  for (let k = 0; k < bands.length; k++) {
    const v = [[], [], []], ok = [];
    for (let a = 0; a < nb; a++) {
      const mid = a * step;
      ok[a] = false; v[0][a] = v[1][a] = v[2][a] = 0;
      if (skip.some(q => inArc(mid, q[0], q[1]))) continue;
      let sr = 0, sg = 0, sb = 0, sk = 0;
      const mode = minB.has(k) ? 'min' : maxB.has(k) ? 'max' : 'mean';
      for (let ang = mid - step / 2; ang < mid + step / 2; ang += 0.5) {
        const rad = ang * Math.PI / 180;
        if (mode !== 'mean') {
          let best = [0, 0, 0], bl = mode === 'min' ? Infinity : -Infinity;
          for (let rr = mb[k][0]; rr <= mb[k][1]; rr += 0.125) {
            const p = sample(m, mc[0] + rr * Math.cos(rad), mc[1] + rr * Math.sin(rad)), l = luma(p);
            if (mode === 'min' ? l < bl : l > bl) { bl = l; best = p; }
          }
          sr += best[0]; sg += best[1]; sb += best[2]; sk++;
          continue;
        }
        for (let rr = mb[k][0] + 0.125; rr < mb[k][1]; rr += 0.25) {
          const p = sample(m, mc[0] + rr * Math.cos(rad), mc[1] + rr * Math.sin(rad));
          sr += p[0]; sg += p[1]; sb += p[2]; sk++;
        }
      }
      if (!sk) continue;
      v[0][a] = sr / sk; v[1][a] = sg / sk; v[2][a] = sb / sk; ok[a] = true;
    }
    const sm = o.num('smooth', 1), g = v.map(q => gauss(q, ok, sm, true));
    col[k] = Array.from({ length: nb }, (_, a) => [g[0][a], g[1][a], g[2][a]]);
    console.log(`band ${k} (${bands[k][0]}-${bands[k][1]} px <- mockup ${mb[k][0]}-${mb[k][1]}): ` +
      [0, 1, 2, 3, 4, 5, 6, 7].map(q => { const a = Math.floor(q * nb / 8); return `${(a * step).toFixed(0)}:${hex(col[k][a])}`; }).join(' '));
  }
  const at = (k, deg) => {
    const f = (((deg % 360) + 360) % 360) / step, a0 = Math.floor(f) % nb, a1 = (a0 + 1) % nb, t = f - Math.floor(f);
    const p = col[k][a0], q = col[k][a1];
    return [p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, p[2] + (q[2] - p[2]) * t];
  };
  const src = P.clone(s), SS = 4;
  for (let y = 0; y < s.h; y++) for (let x = 0; x < s.w; x++) {
    const r0 = Math.hypot(x + 0.5 - c[0], y + 0.5 - c[1]);
    if (r0 < keepin - 1.5) continue;
    const i = y * s.w + x;
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let sy = 0; sy < SS; sy++) for (let sx = 0; sx < SS; sx++) {
      const dx = x + (sx + 0.5) / SS - c[0], dy = y + (sy + 0.5) / SS - c[1], rr = Math.hypot(dx, dy);
      if (rr >= outer) continue;
      if (rr < keepin) { const a = src.a[i]; pr += src.r[i] * a; pg += src.g[i] * a; pb += src.b[i] * a; pa += a; continue; }
      let k = bands.findIndex(q => rr >= q[0] && rr < q[1]);
      if (k < 0) k = rr < bands[0][0] ? 0 : bands.length - 1;
      const cc = at(k, Math.atan2(dy, dx) * 180 / Math.PI);
      pr += L(cc[0]); pg += L(cc[1]); pb += L(cc[2]); pa += 1;
    }
    if (pa <= 0) { s.r[i] = s.g[i] = s.b[i] = s.a[i] = 0; continue; }
    s.r[i] = pr / pa; s.g[i] = pg / pa; s.b[i] = pb / pa; s.a[i] = pa / (SS * SS);
  }
  P.save(s, o.need('out'));
  console.log(`${o.need('out')}  ${s.w}x${s.h}  rings painted, cleared outside r ${outer}`);
}

function ringFit(o) {
  const m = P.load(o.need('screen'));
  let c = o.nums('center', 2);
  const r0 = o.num('from', 10), r1 = o.num('to', 20), step = o.num('step', 5), light = o.has('light');
  const skip = o.has('skip') ? ranges(o.get('skip')) : [];
  for (let it = 0; it < 3; it++) {
    const pts = [];
    for (let a = 0; a < 360; a += step) {
      if (skip.some(q => inArc(a, q[0], q[1]))) continue;
      const rad = a * Math.PI / 180, prof = [];
      for (let r = r0; r <= r1; r += 0.125) prof.push([r, luma(sample(m, c[0] + r * Math.cos(rad), c[1] + r * Math.sin(rad)))]);
      const lo = Math.min(...prof.map(q => q[1])), hi = Math.max(...prof.map(q => q[1])), half = (lo + hi) / 2;
      let sw = 0, sr = 0;
      for (const q of prof) { const w = light ? Math.max(0, q[1] - half) : Math.max(0, half - q[1]); sw += w; sr += w * q[0]; }
      const best = sw > 0 ? sr / sw : (r0 + r1) / 2;
      pts.push([c[0] + best * Math.cos(rad), c[1] + best * Math.sin(rad)]);
    }
    const mx = pts.reduce((s, p) => s + p[0], 0) / pts.length, my = pts.reduce((s, p) => s + p[1], 0) / pts.length;
    let suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
    for (const p of pts) { const u = p[0] - mx, v = p[1] - my; suu += u * u; svv += v * v; suv += u * v; suuu += u * u * u; svvv += v * v * v; suvv += u * v * v; svuu += v * u * u; }
    const det = suu * svv - suv * suv;
    const uc = (0.5 * (suuu + suvv) * svv - 0.5 * (svvv + svuu) * suv) / det, vc = (0.5 * (svvv + svuu) * suu - 0.5 * (suuu + suvv) * suv) / det;
    const R = Math.sqrt(uc * uc + vc * vc + (suu + svv) / pts.length);
    c = [mx + uc, my + vc];
    const rms = Math.sqrt(pts.reduce((s, p) => s + Math.pow(Math.hypot(p[0] - c[0], p[1] - c[1]) - R, 2), 0) / pts.length);
    console.log(`iteration ${it + 1}: centre ${c[0].toFixed(2)},${c[1].toFixed(2)}  radius ${R.toFixed(2)}  (${pts.length} rays, rms ${rms.toFixed(2)} px)`);
  }
}

function radProf(o) {
  const m = P.load(o.need('screen')), c = o.nums('center', 2), rmax = o.num('rmax', 50), step = o.num('step', 0.5);
  const ang = o.has('angles') ? ranges(o.get('angles')) : [[0, 360]], out = [];
  for (let r = 0; r <= rmax + 1e-9; r += step) {
    let sr = 0, sg = 0, sb = 0, k = 0;
    for (const q of ang) for (let a = q[0]; a < q[1]; a += Math.max(0.5, 90 / Math.max(1, r * Math.PI))) {
      const rad = a * Math.PI / 180, p = sample(m, c[0] + r * Math.cos(rad), c[1] + r * Math.sin(rad));
      sr += p[0]; sg += p[1]; sb += p[2]; k++;
    }
    out.push(`${+r.toFixed(2)}:${hex([sr / k, sg / k, sb / k])}`);
  }
  console.log(out.join(' '));
}

function prof(o, rows) {
  const m = P.load(o.need('screen')), rg = ranges(o.need(rows ? 'cols' : 'rows'));
  const from = o.num('from', 0), to = o.num('to', 0), step = o.num('step', 1), out = [];
  for (let p = from; p <= to + 1e-9; p += step) {
    let r = 0, g = 0, b = 0, k = 0;
    for (const q of rg) for (let t = q[0] + 0.5; t < q[1]; t += 1) { const c = rows ? sample(m, t, p + 0.5) : sample(m, p + 0.5, t); r += c[0]; g += c[1]; b += c[2]; k++; }
    out.push(`${+p.toFixed(2)}:${hex([r / k, g / k, b / k])}`);
  }
  console.log(out.join(' '));
}

function stops(s) { return s.split('>').map(h => { const c = P.parseHex(h.trim()); return [S(c.r), S(c.g), S(c.b)]; }); }
function grad(st, t) {
  if (st.length === 1) return st[0];
  t = Math.max(0, Math.min(1, t)) * (st.length - 1);
  const i = Math.min(Math.floor(t), st.length - 2), u = t - i;
  return [0, 1, 2].map(k => st[i][k] + (st[i + 1][k] - st[i][k]) * u);
}

function pillFrame(o) {
  const s = P.load(o.need('in')), unit = o.num('unit', 2.3);
  const bands = o.need('bands').split(';').filter(Boolean).map(b => { const q = b.split(':'); return { w: parseFloat(q[0]) * unit, st: stops(q[1]) }; });
  const fill = o.get('fill', 'keep'), fillSt = fill === 'keep' ? null : stops(fill);
  const insety = o.num('insety', 0) * unit;                                // a pill shorter than the canvas (same width)
  const R = s.h / 2 - insety, x0 = R, x1 = s.w - R, cy = s.h / 2, src = P.clone(s), SS = 4;
  for (let y = 0; y < s.h; y++) for (let x = 0; x < s.w; x++) {
    const i = y * s.w + x;
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let sy = 0; sy < SS; sy++) for (let sx = 0; sx < SS; sx++) {
      const px = x + (sx + 0.5) / SS, py = y + (sy + 0.5) / SS, ax = Math.min(x1, Math.max(x0, px));
      const d = R - Math.hypot(px - ax, py - cy);
      if (d <= 0) continue;
      const t = (py - insety) / (s.h - 2 * insety);
      let acc = 0, k = -1;
      for (let j = 0; j < bands.length; j++) { if (d < acc + bands[j].w) { k = j; break; } acc += bands[j].w; }
      if (k >= 0 || fillSt) { const c = grad(k >= 0 ? bands[k].st : fillSt, t); pr += L(c[0]); pg += L(c[1]); pb += L(c[2]); pa += 1; }
      else { const a = src.a[i]; pr += src.r[i] * a; pg += src.g[i] * a; pb += src.b[i] * a; pa += a; }
    }
    if (pa <= 0) { s.r[i] = s.g[i] = s.b[i] = s.a[i] = 0; continue; }
    s.r[i] = pr / pa; s.g[i] = pg / pa; s.b[i] = pb / pa; s.a[i] = pa / (SS * SS);
  }
  P.save(s, o.need('out'));
  console.log(`${o.need('out')}  ${s.w}x${s.h}  pill frame: ${bands.length} bands, radius ${R.toFixed(1)} px`);
}

function nineMap(t, len, sLen, b0, b1, unit) {
  if (b0 <= 0 && b1 <= 0) return t / len * sLen;
  let d0 = b0 / unit, d1 = b1 / unit;
  if (d0 + d1 > len) { const f = len / (d0 + d1); d0 *= f; d1 *= f; }
  if (t < d0) return t / d0 * b0;
  if (t > len - d1) return sLen - (len - t) / d1 * b1;
  const mid = len - d0 - d1, sMid = sLen - b0 - b1;
  return b0 + (mid > 0 ? (t - d0) / mid * sMid : 0);
}

function compose(o) {
  const m = P.load(o.need('screen'));
  const reg = o.has('region') ? o.nums('region', 4).map(Math.round) : [0, 0, m.w, m.h];
  const [rx, ry, rw, rh] = reg;
  const cache = {};
  const layers = o.need('layers').split(';').filter(Boolean).map(spec => {
    const q = spec.split('@');
    const Lr = { S: cache[q[0]] || (cache[q[0]] = P.load(q[0])), rect: q[1].split(',').map(Number), nine: [0, 0, 0, 0], unit: 2.3, fx: false, fy: false, op: 1 };
    for (const kv of q.slice(2)) {
      if (kv.startsWith('nine=')) Lr.nine = kv.slice(5).split(',').map(Number);
      else if (kv.startsWith('unit=')) Lr.unit = parseFloat(kv.slice(5));
      else if (kv === 'flipx') Lr.fx = true;
      else if (kv === 'flipy') Lr.fy = true;
      else if (kv.startsWith('op=')) Lr.op = parseFloat(kv.slice(3));
    }
    return Lr;
  });
  const comp = P.create(rw, rh, 1), bs = o.get('base', 'mockup');
  const plate = bs.toLowerCase().endsWith('.png') ? P.load(bs) : null, flat = bs.startsWith('#') ? P.parseHex(bs) : null;
  for (let y = 0; y < rh; y++) for (let x = 0; x < rw; x++) {
    const t = y * rw + x;
    if (flat) { comp.r[t] = flat.r; comp.g[t] = flat.g; comp.b[t] = flat.b; continue; }
    const src = plate || m, k = (ry + y) * src.w + rx + x;
    comp.r[t] = src.r[k]; comp.g[t] = src.g[k]; comp.b[t] = src.b[k];
  }
  const ss = 4;
  for (const Lr of layers) {
    const [lx0, ly0, lw, lh] = Lr.rect;
    const xa = Math.max(0, Math.floor(lx0) - rx - 1), xb = Math.min(rw, Math.ceil(lx0 + lw) - rx + 1);
    const ya = Math.max(0, Math.floor(ly0) - ry - 1), yb = Math.min(rh, Math.ceil(ly0 + lh) - ry + 1);
    const Sp = Lr.S;
    for (let y = ya; y < yb; y++) for (let x = xa; x < xb; x++) {
      let ar = 0, ag = 0, ab = 0, aa = 0;
      for (let j = 0; j < ss; j++) for (let i = 0; i < ss; i++) {
        let lx = rx + x + (i + 0.5) / ss - lx0, ly = ry + y + (j + 0.5) / ss - ly0;
        if (lx < 0 || ly < 0 || lx >= lw || ly >= lh) continue;
        if (Lr.fx) lx = lw - lx;
        if (Lr.fy) ly = lh - ly;
        const px = nineMap(lx, lw, Sp.w, Lr.nine[0], Lr.nine[2], Lr.unit), py = nineMap(ly, lh, Sp.h, Lr.nine[1], Lr.nine[3], Lr.unit);
        const fx = px - 0.5, fy = py - 0.5, x0 = Math.floor(fx), y0 = Math.floor(fy), tx = fx - x0, ty = fy - y0;
        for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) {
          const sx = Math.min(Sp.w - 1, Math.max(0, x0 + dx)), sy = Math.min(Sp.h - 1, Math.max(0, y0 + dy));
          const w = (dx ? tx : 1 - tx) * (dy ? ty : 1 - ty), k = sy * Sp.w + sx, a = Sp.a[k] * w;
          ar += Sp.r[k] * a; ag += Sp.g[k] * a; ab += Sp.b[k] * a; aa += a;
        }
      }
      const n = ss * ss, alpha = aa / n * Lr.op;
      if (alpha <= 0) continue;
      const t = y * rw + x, inv = 1 - alpha;
      comp.r[t] = ar / n * Lr.op + comp.r[t] * inv; comp.g[t] = ag / n * Lr.op + comp.g[t] * inv; comp.b[t] = ab / n * Lr.op + comp.b[t] * inv;
    }
  }
  const excl = rects(o.get('exclude')), meas = o.has('measure') ? o.nums('measure', 4) : [rx, ry, rw, rh];
  let e2 = 0, e1 = 0, ne = 0;
  for (let y = 0; y < rh; y++) for (let x = 0; x < rw; x++) {
    const mx = rx + x + 0.5, my = ry + y + 0.5;
    if (mx < meas[0] || my < meas[1] || mx >= meas[0] + meas[2] || my >= meas[1] + meas[3]) continue;
    if (excl.some(e => mx >= e[0] && mx < e[0] + e[2] && my >= e[1] && my < e[1] + e[3])) continue;
    const t = y * rw + x, k = (ry + y) * m.w + rx + x;
    const dr = S(comp.r[t]) - S(m.r[k]), dg = S(comp.g[t]) - S(m.g[k]), db = S(comp.b[t]) - S(m.b[k]);
    e2 += (dr * dr + dg * dg + db * db) / 3; e1 += (Math.abs(dr) + Math.abs(dg) + Math.abs(db)) / 3; ne++;
  }
  if (o.has('out')) P.save(comp, o.need('out'));
  console.log(`${o.get('out', '(no picture)')}  ${rw}x${rh}  error in [${meas.map(v => +(+v).toFixed(2)).join(',')}]: rms ${(ne ? Math.sqrt(e2 / ne) : 0).toFixed(2)}  mean ${(ne ? e1 / ne : 0).toFixed(2)} (sRGB levels, ${ne} px)`);
}

function premix(o) {
  const s = P.load(o.need('in')), c = P.parseHex(o.need('over'));
  for (let i = 0; i < s.r.length; i++) { const a = s.a[i]; s.r[i] = s.r[i] * a + c.r * (1 - a); s.g[i] = s.g[i] * a + c.g * (1 - a); s.b[i] = s.b[i] * a + c.b * (1 - a); s.a[i] = 1; }
  P.save(s, o.need('out'));
}

function fill(o) {
  const s = P.load(o.need('in')), c = P.parseHex(o.need('color'));
  for (let i = 0; i < s.r.length; i++) { s.r[i] = c.r; s.g[i] = c.g; s.b[i] = c.b; }
  P.save(s, o.need('out'));
  console.log(`${o.need('out')}  ${s.w}x${s.h}  filled ${o.need('color')}`);
}

// ---------------------------------------------------------------------------------------------------
const extra = (() => { try { return require('./po-more.js'); } catch (e) { if (e.code === 'MODULE_NOT_FOUND' && /po-more/.test(e.message)) return {}; throw e; } })();
const cmds = {
  rowmatch: o => lineMatch(o, true), colmatch: o => lineMatch(o, false), midflat: midFlat, ringpaint: ringPaint,
  ringfit: ringFit, radprof: radProf, vprof: o => prof(o, true), hprof2: o => prof(o, false), pillframe: pillFrame,
  compose, premix, fill, ...(extra.cmds || {}),
};
module.exports = { opts, sample, gauss, ranges, rects, hex, luma, S, L, stops, grad, nineMap, inArc };

if (require.main === module) {
  const [cmd, ...rest] = process.argv.slice(2);
  if (!cmd || cmd === 'help' || !cmds[cmd]) { process.stdout.write(USAGE + (extra.usage || '')); process.exit(cmd && cmd !== 'help' ? 2 : 0); }
  try { cmds[cmd](opts(rest)); } catch (e) { console.error('error: ' + e.message); process.exit(1); }
}
