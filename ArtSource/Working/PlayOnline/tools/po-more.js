// po-more.js: the drawing commands of po.js (lines, assembling sprites, the popover pointer, frames).
'use strict';
const P = require('./png.js');

const usage = `  line     --out f --len L --thick t --color #hex --profile "x:alpha;.." [--unit 2.3] [--pad 1.5] [--caps round|butt]
           [--hi "#hex@a"] [--lo "#hex@a"] [--hw 0.8] [--vertical]
           a straight line (mockup px: length, thickness) with its opacity along the length from the profile (x from
           the line's start, linear between stops), optional light lines above (hi) and below (lo), drawn at unit
           px per mockup px; --vertical: the same line standing up (x of the profile = down from the top)
  assemble --layers "spr.png@x,y,w,h[@flipx][@flipy][@op=a];.." --rect x,y,w,h [--unit 2.3] --out f
           one transparent sprite of the rect (mockup px, times unit) with the layers drawn at their mockup rects
  roundframe --out f --size W,H --radius R --bands "w:#top>#bottom;.." (--fill #top>#bottom | --hollow) [--unit 2.3] [--corners 1111]
           a rounded rectangle drawn from its outer edge inwards (mockup px), like shopui frame2, with 2..n-stop
           vertical gradients per band; --corners TL TR BR BL (0 = square)
`;

function lazy() { return require('./po.js'); }

// signed distance helpers -------------------------------------------------------------------------------
function segDist(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay, t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(px - (ax + t * dx), py - (ay + t * dy));
}

function profileAt(stops, x) {
  if (x <= stops[0][0]) return stops[0][1];
  for (let i = 0; i + 1 < stops.length; i++) if (x <= stops[i + 1][0]) { const u = (x - stops[i][0]) / (stops[i + 1][0] - stops[i][0]); return stops[i][1] + (stops[i + 1][1] - stops[i][1]) * u; }
  return stops[stops.length - 1][1];
}

function colorAlpha(s) { const [h, a] = s.split('@'); return { c: P.parseHex(h), a: a === undefined ? 1 : parseFloat(a) }; }

// ---------------------------------------------------------------------------------------------------
function line(o) {
  const unit = o.num('unit', 2.3), len = o.num('len', 100), th = o.num('thick', 1.5), pad = o.num('pad', 1.5), hw = o.num('hw', 0.8);
  const caps = o.get('caps', 'round'), vert = o.has('vertical');
  const col = P.parseHex(o.need('color'));
  const prof = o.need('profile').split(';').filter(Boolean).map(p => p.split(':').map(Number)).sort((a, b) => a[0] - b[0]);
  const hi = o.has('hi') ? colorAlpha(o.get('hi')) : null, lo = o.has('lo') ? colorAlpha(o.get('lo')) : null;
  const across = th + (hi || lo ? 2 * hw : 0) + 2 * pad;
  let W = Math.ceil((len + 2 * pad) * unit), H = Math.ceil(across * unit);
  if (W % 2) W++;
  if (H % 2) H++;
  const img = P.create(vert ? H : W, vert ? W : H, 0);
  const cy = H / 2 / unit, x0 = (W / unit - len) / 2, SS = 4;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let j = 0; j < SS; j++) for (let i = 0; i < SS; i++) {
      const mx = (x + (i + 0.5) / SS) / unit, my = (y + (j + 0.5) / SS) / unit;   // mockup px in the canvas
      const along = mx - x0, off = my - cy;
      // core: a capsule (round caps) or a bar (butt caps)
      let inCore;
      if (caps === 'round') inCore = segDist(mx, my, x0 + th / 2, cy, x0 + len - th / 2, cy) <= th / 2;
      else inCore = along >= 0 && along <= len && Math.abs(off) <= th / 2;
      const a = profileAt(prof, Math.max(0, Math.min(len, along)));
      let c = null, al = 0;
      if (inCore) { c = col; al = a; }
      else if (along >= 0 && along <= len) {
        if (hi && off < -th / 2 && off >= -th / 2 - hw) { c = hi.c; al = hi.a * a; }
        else if (lo && off > th / 2 && off <= th / 2 + hw) { c = lo.c; al = lo.a * a; }
      }
      if (!c || al <= 0) continue;
      pr += c.r * al; pg += c.g * al; pb += c.b * al; pa += al;
    }
    const n = SS * SS;
    if (pa <= 0) continue;
    const k = vert ? x * img.w + (img.w - 1 - y) : y * W + x;   // vertical: rotate 90 degrees (the profile runs downwards)
    img.r[k] = pr / pa; img.g[k] = pg / pa; img.b[k] = pb / pa; img.a[k] = pa / n;
  }
  if (vert) {
    // the rotation above maps x -> row and y -> column (mirrored); keep the across-profile symmetric, so only check sizes
  }
  P.save(img, o.need('out'));
  console.log(`${o.need('out')}  ${img.w}x${img.h}  line ${len} x ${th} mockup px (+ ${pad} px margin), ${vert ? 'vertical' : 'horizontal'}; ` +
    `rect = the line's box grown by ${((W / unit - len) / 2).toFixed(2)} along and ${((H / unit - th) / 2).toFixed(2)} across`);
}

// ---------------------------------------------------------------------------------------------------
function assemble(o) {
  const po = lazy(), unit = o.num('unit', 2.3), rect = o.nums('rect', 4);
  const W = Math.round(rect[2] * unit), H = Math.round(rect[3] * unit), su = W / rect[2], sv = H / rect[3];
  const out = P.create(W, H, 0), cache = {}, SS = 4;
  // premultiplied accumulation
  const pr = new Float64Array(W * H), pg = new Float64Array(W * H), pb = new Float64Array(W * H), pa = new Float64Array(W * H);
  for (const spec of o.need('layers').split(';').filter(Boolean)) {
    const q = spec.split('@'), S = cache[q[0]] || (cache[q[0]] = P.load(q[0])), r = q[1].split(',').map(Number);
    let fx = false, fy = false, op = 1;
    for (const kv of q.slice(2)) { if (kv === 'flipx') fx = true; else if (kv === 'flipy') fy = true; else if (kv.startsWith('op=')) op = parseFloat(kv.slice(3)); }
    const xa = Math.max(0, Math.floor((r[0] - rect[0]) * su) - 1), xb = Math.min(W, Math.ceil((r[0] + r[2] - rect[0]) * su) + 1);
    const ya = Math.max(0, Math.floor((r[1] - rect[1]) * sv) - 1), yb = Math.min(H, Math.ceil((r[1] + r[3] - rect[1]) * sv) + 1);
    for (let y = ya; y < yb; y++) for (let x = xa; x < xb; x++) {
      let ar = 0, ag = 0, ab = 0, aa = 0;
      for (let j = 0; j < SS; j++) for (let i = 0; i < SS; i++) {
        let lx = rect[0] + (x + (i + 0.5) / SS) / su - r[0], ly = rect[1] + (y + (j + 0.5) / SS) / sv - r[1];
        if (lx < 0 || ly < 0 || lx >= r[2] || ly >= r[3]) continue;
        if (fx) lx = r[2] - lx;
        if (fy) ly = r[3] - ly;
        const px = lx / r[2] * S.w, py = ly / r[3] * S.h, gx = px - 0.5, gy = py - 0.5, x0 = Math.floor(gx), y0 = Math.floor(gy), tx = gx - x0, ty = gy - y0;
        for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) {
          const sx = Math.min(S.w - 1, Math.max(0, x0 + dx)), sy = Math.min(S.h - 1, Math.max(0, y0 + dy)), w = (dx ? tx : 1 - tx) * (dy ? ty : 1 - ty), k = sy * S.w + sx, a = S.a[k] * w;
          ar += S.r[k] * a; ag += S.g[k] * a; ab += S.b[k] * a; aa += a;
        }
      }
      const n = SS * SS, a = aa / n * op;
      if (a <= 0) continue;
      const t = y * W + x, inv = 1 - a;
      pr[t] = ar / n * op + pr[t] * inv; pg[t] = ag / n * op + pg[t] * inv; pb[t] = ab / n * op + pb[t] * inv; pa[t] = a + pa[t] * inv;
    }
  }
  for (let t = 0; t < W * H; t++) if (pa[t] > 0) { out.r[t] = pr[t] / pa[t]; out.g[t] = pg[t] / pa[t]; out.b[t] = pb[t] / pa[t]; out.a[t] = pa[t]; }
  P.save(out, o.need('out'));
  console.log(`${o.need('out')}  ${W}x${H}  assembled at [${rect.join(', ')}] x ${unit}`);
}

// ---------------------------------------------------------------------------------------------------
function bandsOf(o, unit) {
  const po = lazy();
  return o.need('bands').split(';').filter(Boolean).map(b => { const q = b.split(':'); return { w: parseFloat(q[0]), st: po.stops(q[1]) }; });
}

function roundFrame(o) {
  const po = lazy(), unit = o.num('unit', 2.3), [mw, mh] = o.nums('size', 2), R = o.num('radius', 10);
  const corners = (o.get('corners', '1111')).split('').map(Number), bands = bandsOf(o, unit), hollow = o.has('hollow'), fill = hollow ? null : po.stops(o.need('fill'));
  let W = Math.round(mw * unit), H = Math.round(mh * unit);
  const img = P.create(W, H, 0), SS = 4, su = W / mw, sv = H / mh;
  // depth inside a rounded rect (mockup px): distance to the boundary
  const depth = (x, y) => {
    const rTL = corners[0] ? R : 0, rTR = corners[1] ? R : 0, rBR = corners[2] ? R : 0, rBL = corners[3] ? R : 0;
    let d = Math.min(x, y, mw - x, mh - y);
    const c = [[rTL, rTL, rTL], [mw - rTR, rTR, rTR], [mw - rBR, mh - rBR, rBR], [rBL, mh - rBL, rBL]];
    if (rTL && x < rTL && y < rTL) d = rTL - Math.hypot(x - rTL, y - rTL);
    if (rTR && x > mw - rTR && y < rTR) d = rTR - Math.hypot(x - (mw - rTR), y - rTR);
    if (rBR && x > mw - rBR && y > mh - rBR) d = rBR - Math.hypot(x - (mw - rBR), y - (mh - rBR));
    if (rBL && x < rBL && y > mh - rBL) d = rBL - Math.hypot(x - rBL, y - (mh - rBL));
    return d;
  };
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let j = 0; j < SS; j++) for (let i = 0; i < SS; i++) {
      const mx = (x + (i + 0.5) / SS) / su, my = (y + (j + 0.5) / SS) / sv, d = depth(mx, my);
      if (d <= 0) continue;
      let acc = 0, k = -1;
      for (let b = 0; b < bands.length; b++) { if (d < acc + bands[b].w) { k = b; break; } acc += bands[b].w; }
      if (k < 0 && hollow) continue;
      const c = po.grad(k >= 0 ? bands[k].st : fill, my / mh);
      pr += po.L(c[0]); pg += po.L(c[1]); pb += po.L(c[2]); pa += 1;
    }
    if (pa <= 0) continue;
    const t = y * W + x;
    img.r[t] = pr / pa; img.g[t] = pg / pa; img.b[t] = pb / pa; img.a[t] = pa / (SS * SS);
  }
  P.save(img, o.need('out'));
  console.log(`${o.need('out')}  ${W}x${H}  rounded frame ${mw} x ${mh} mockup px, radius ${R}, ${bands.length} bands`);
}

// ---------------------------------------------------------------------------------------------------
// pointer2: the pointer drawn together with a stretch of the box's top edge on both sides of it, from the outline of
// their union (the box's top edge, the two slanted sides): every point takes the band of its nearest edge (the box's
// bands along the top edge, the pointer's along the slanted sides), so the lines meet in mitred joints, and the box's
// top bands are covered between the base corners (the pointer opens into the box with no line across its base).
function pointer2(o) {
  const po = lazy(), unit = o.num('unit', 2.3), B = o.num('base', 28), Ht = o.num('height', 14), ext = o.num('ext', 10);
  const boxBands = o.need('boxbands').split(';').filter(Boolean).map(b => { const q = b.split(':'); return { w: parseFloat(q[0]), c: po.stops(q[1])[0] }; });
  // --nearbands: the box's top-edge colours right beside the pointer; they turn into --boxbands over the ext
  const nearBands = o.has('nearbands') ? o.get('nearbands').split(';').filter(Boolean).map(b => { const q = b.split(':'); return { w: parseFloat(q[0]), c: po.stops(q[1])[0] }; }) : null;
  const ptrBands = o.need('bands').split(';').filter(Boolean).map(b => { const q = b.split(':'); return { w: parseFloat(q[0]), c: po.stops(q[1])[0] }; });
  const fill = po.stops(o.need('fill'))[0], shift = o.num('shift', 0);
  const boxDepth = boxBands.reduce((s, b) => s + b.w, 0), below = o.num('below', boxDepth + 1);
  const cut = o.num('cut', 0);                                           // the tip cut flat this far below the sides' meeting point
  const mw = B + 2 * ext, mh = Ht + below - cut, W = Math.round(mw * unit), H = Math.round(mh * unit), su = W / mw, sv = H / mh, SS = 4;
  const ax = mw / 2, ay = -cut, lx = ext, rx = ext + B, by = Ht - cut;      // apex (above the canvas when cut), base corners
  const segs = [
    { a: [-1e3, by], b: [lx, by], box: true }, { a: [lx, by], b: [ax, ay], box: false },
    { a: [ax, ay], b: [rx, by], box: false }, { a: [rx, by], b: [mw + 1e3, by], box: true },
  ];
  const inside = (x, y) => y >= by || (y >= ay && Math.abs(x - ax) <= (y - ay) / Ht * (B / 2));
  const img = P.create(W, H, 0);
  const pick = (bands, d) => { let acc = 0; for (const b of bands) { if (d < acc + b.w) return b.c; acc += b.w; } return null; };
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    let pr = 0, pg = 0, pb = 0, pa = 0;
    for (let j = 0; j < SS; j++) for (let i = 0; i < SS; i++) {
      const mx = (x + (i + 0.5) / SS) / su, my = (y + (j + 0.5) / SS) / sv;
      if (!inside(mx, my)) continue;
      let best = 1e9, isBox = true;
      for (const s of segs) { const d = segDist(mx, my, s.a[0], s.a[1], s.b[0], s.b[1]); if (d < best - 1e-9) { best = d; isBox = s.box; } }
      // in the wedge below a base corner every point is nearest to the corner itself: blend the two band sets by the
      // angle, from straight down (the box's top edge) to the slanted side's inward normal
      let w = isBox ? 0 : 1;
      for (const cx of [lx, rx]) {
        const dx = mx - cx, dy = my - by, r = Math.hypot(dx, dy);
        if (r < 1e-9 || Math.abs(r - best) > 1e-6 || dy <= 0) continue;
        const side = cx === lx ? 1 : -1;                                   // inward normal of the side: (side * Ht, B / 2)
        const nx = side * Ht / Math.hypot(Ht, B / 2), ny = (B / 2) / Math.hypot(Ht, B / 2);
        const full = Math.acos(ny), ang = Math.acos(Math.max(-1, Math.min(1, dy / r)));
        if (Math.sign(dx) === side || dx === 0) w = Math.max(0, Math.min(1, ang / full));
      }
      const dBox = best + shift * w;                                     // in the wedge the box's bands slide outwards to meet the pointer's
      let cb = pick(boxBands, dBox) || fill;
      if (nearBands) {
        const s = Math.max(0, Math.min(1, (mx < ax ? lx - mx : mx - rx) / ext)), cn = pick(nearBands, dBox) || fill;
        cb = [0, 1, 2].map(k => cn[k] + (cb[k] - cn[k]) * s);
      }
      const cp = pick(ptrBands, best) || fill;
      const c = [0, 1, 2].map(k => cb[k] + (cp[k] - cb[k]) * w);
      pr += po.L(c[0]); pg += po.L(c[1]); pb += po.L(c[2]); pa += 1;
    }
    if (pa <= 0) continue;
    const t = y * W + x;
    img.r[t] = pr / pa; img.g[t] = pg / pa; img.b[t] = pb / pa; img.a[t] = pa / (SS * SS);
  }
  P.save(img, o.need('out'));
  console.log(`${o.need('out')}  ${W}x${H}  pointer with the box's top edge: canvas ${mw} x ${mh} mockup px; sides meet at (${ax}, ${-cut}), base ${B} at ${Ht} below that; ` +
    `the box's top edge is ${Ht - cut} below the canvas top, ${ext} px of it on each side`);
}

// ---------------------------------------------------------------------------------------------------
// alphaprof: an opacity profile for "line" from the darkest point across a line on one or more mockups (averaged):
// alpha = (luma(ground) - luma(darkest)) / (luma(ground) - luma(core)), sampled every step px along the line.
function alphaProf(o) {
  const po = lazy(), dir = o.get('dir', 'h'), [a, b] = o.nums('span', 2), st = o.num('step', 3), [c0, c1] = o.nums('across', 2);
  const start = o.num('start', a), ms = o.need('screens').split(';').filter(Boolean).map(P.load);
  const lum = h => { const c = P.parseHex(h); return po.luma([po.S(c.r), po.S(c.g), po.S(c.b)]); };
  const Lg = lum(o.need('ground')), Lc = lum(o.need('core')), out = [];
  for (let t = a; t <= b + 1e-9; t += st) {
    let acc = 0;
    for (const m of ms) {
      let bl = 1e9;
      for (let u = c0; u <= c1; u += 0.25) bl = Math.min(bl, po.luma(dir === 'h' ? po.sample(m, t + 0.5, u) : po.sample(m, u, t + 0.5)));
      acc += bl;
    }
    out.push(`${+(t - start).toFixed(2)}:${Math.max(0, Math.min(1, (Lg - acc / ms.length) / (Lg - Lc))).toFixed(3)}`);
  }
  console.log(out.join(';'));
}

// ---------------------------------------------------------------------------------------------------
// lineprof: along a (nearly) horizontal or vertical line, the darkest colour across it, the centre of its dip and the
// dip's size (luma below the brightest point x px), every step px. ray: colours along a ray from a point.
function lineProf(o) {
  const po = lazy(), m = P.load(o.need('screen')), dir = o.get('dir', 'h'), [a, b] = o.nums('span', 2), st = o.num('step', 1), [c0, c1] = o.nums('across', 2), out = [];
  for (let t = a; t <= b + 1e-9; t += st) {
    let best = null, bl = 1e9, sw = 0, sc = 0;
    const prof = [];
    for (let u = c0; u <= c1; u += 0.25) { const p = dir === 'h' ? po.sample(m, t + 0.5, u) : po.sample(m, u, t + 0.5), l = po.luma(p); prof.push([u, l]); if (l < bl) { bl = l; best = p; } }
    const hi = Math.max(...prof.map(q => q[1]));
    for (const [u, l] of prof) { const w = Math.max(0, hi - l); sw += w; sc += w * u; }
    out.push(`${+t.toFixed(2)}:${po.hex(best)}@${(sw ? sc / sw : 0).toFixed(1)}/${(sw * 0.25).toFixed(0)}`);
  }
  console.log(out.join(' '));
}

function ray(o) {
  const po = lazy(), m = P.load(o.need('screen')), [x0, y0] = o.nums('from', 2), a = o.num('deg', 0) * Math.PI / 180, len = o.num('len', 10), st = o.num('step', 0.5), out = [];
  for (let t = 0; t <= len + 1e-9; t += st) out.push(`${+t.toFixed(2)}:${po.hex(po.sample(m, x0 + t * Math.cos(a), y0 + t * Math.sin(a)))}`);
  console.log(out.join(' '));
}

module.exports = { usage: usage + `  lineprof --screen png --dir h|v --span a,b --across c0,c1 [--step 1]   darkest colour across a thin line, its centre and size
  ray      --screen png --from x,y --deg d --len L [--step 0.5]                colours along a ray (0 = right, 90 = down)
  alphaprof --dir h|v --span a,b --across c0,c1 --ground #hex --core #hex --screens "a.png;b.png" [--step 3] [--start a]
           the opacity profile (for line) of a thin line on the mockups: from the darkest point across it, every step px
  pointer2 --out f --base B --height H --ext E --bands "w:#hex;.." --boxbands "w:#hex;.." --fill #hex [--below D] [--shift S] [--unit 2.3]
           the pointer with E px of the box's top edge on each side, drawn from their union's outline (box bands along the
           edge, pointer bands along the slanted sides, mitred joints); canvas (B + 2E) x (H + D), apex at its top centre
`, cmds: { line, assemble, roundframe: roundFrame, pointer2, alphaprof: alphaProf, lineprof: lineProf, ray } };
