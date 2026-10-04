// Float image helpers for the ShopTables layer work.
'use strict';
const P = require('./png.js');

// float planes [r,g,b] (0..255) from a PNG image
function planes(img) {
  const n = img.w * img.h, r = new Float32Array(n), g = new Float32Array(n), b = new Float32Array(n), a = new Float32Array(n);
  for (let i = 0; i < n; i++) { r[i] = img.data[i * 4]; g[i] = img.data[i * 4 + 1]; b[i] = img.data[i * 4 + 2]; a[i] = img.data[i * 4 + 3] / 255; }
  return { w: img.w, h: img.h, c: [r, g, b], a };
}

// bilinear sample of a float plane at (x, y), clamped
function sample(pl, w, h, x, y) {
  x = Math.min(Math.max(x, 0), w - 1.0001); y = Math.min(Math.max(y, 0), h - 1.0001);
  const x0 = Math.floor(x), y0 = Math.floor(y), tx = x - x0, ty = y - y0, i = y0 * w + x0;
  return (pl[i] * (1 - tx) + pl[i + 1] * tx) * (1 - ty) + (pl[i + w] * (1 - tx) + pl[i + w + 1] * tx) * ty;
}

// resample a (small) image onto the frame (W x H) with scale s = W / img.w and an extra shift:
// out(x, y) = img((x + dx) / s, (y + dy) / s) using pixel-centre convention
function upscaleShift(img, W, H, dx, dy) {
  const p = planes(img), sx = img.w / W, sy = img.h / H;
  const out = { w: W, h: H, c: [new Float32Array(W * H), new Float32Array(W * H), new Float32Array(W * H)] };
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const fx = (x + dx + 0.5) * sx - 0.5, fy = (y + dy + 0.5) * sy - 0.5;
    for (let c = 0; c < 3; c++) out.c[c][y * W + x] = sample(p.c[c], img.w, img.h, fx, fy);
  }
  return out;
}

// separable box blur (approximate Gaussian with 3 passes) of a float plane
function boxBlur(src, w, h, r) {
  if (r < 1) return Float32Array.from(src);
  const tmp = new Float32Array(w * h), out = new Float32Array(w * h);
  const pass = (a, b, horiz) => {
    if (horiz) {
      for (let y = 0; y < h; y++) {
        let acc = 0; const row = y * w;
        for (let x = -r; x <= r; x++) acc += a[row + Math.min(Math.max(x, 0), w - 1)];
        for (let x = 0; x < w; x++) {
          b[row + x] = acc / (2 * r + 1);
          acc += a[row + Math.min(x + r + 1, w - 1)] - a[row + Math.max(x - r, 0)];
        }
      }
    } else {
      for (let x = 0; x < w; x++) {
        let acc = 0;
        for (let y = -r; y <= r; y++) acc += a[Math.min(Math.max(y, 0), h - 1) * w + x];
        for (let y = 0; y < h; y++) {
          b[y * w + x] = acc / (2 * r + 1);
          acc += a[Math.min(y + r + 1, h - 1) * w + x] - a[Math.max(y - r, 0) * w + x];
        }
      }
    }
  };
  let cur = Float32Array.from(src);
  for (let k = 0; k < 3; k++) { pass(cur, tmp, true); pass(tmp, out, false); cur = Float32Array.from(out); }
  return cur;
}

// fill holes of a float plane given weights (0..1) by push-pull (multi-scale normalised averaging)
function pushPull(val, wt, w, h) {
  const levels = [];
  let cv = Float32Array.from(val), cw = Float32Array.from(wt), lw = w, lh = h;
  for (let i = 0; i < cv.length; i++) cv[i] *= cw[i];
  levels.push({ v: cv, wt: cw, w: lw, h: lh });
  while (lw > 2 && lh > 2) {
    const nw = Math.ceil(lw / 2), nh = Math.ceil(lh / 2), nv = new Float32Array(nw * nh), nwt = new Float32Array(nw * nh);
    for (let y = 0; y < nh; y++) for (let x = 0; x < nw; x++) {
      let sv = 0, sw = 0;
      for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const xx = Math.min(2 * x + dx, lw - 1), yy = Math.min(2 * y + dy, lh - 1); sv += cv[yy * lw + xx]; sw += cw[yy * lw + xx]; }
      nv[y * nw + x] = sv; nwt[y * nw + x] = sw;
    }
    // normalise weights to <= 1 so coarse levels act as fill
    for (let i = 0; i < nwt.length; i++) if (nwt[i] > 1) { nv[i] /= nwt[i]; nwt[i] = 1; }
    cv = nv; cw = nwt; lw = nw; lh = nh;
    levels.push({ v: cv, wt: cw, w: lw, h: lh });
  }
  // pull: from coarse to fine, fill missing weight with upsampled coarse values
  for (let l = levels.length - 2; l >= 0; l--) {
    const f = levels[l], c = levels[l + 1];
    for (let y = 0; y < f.h; y++) for (let x = 0; x < f.w; x++) {
      const i = y * f.w + x, wv = f.wt[i];
      if (wv >= 1) continue;
      const cx = Math.min((x + 0.5) / 2 - 0.5, c.w - 1), cy = Math.min((y + 0.5) / 2 - 0.5, c.h - 1);
      const cvv = sample(c.v, c.w, c.h, Math.max(cx, 0), Math.max(cy, 0)) / Math.max(sample(c.wt, c.w, c.h, Math.max(cx, 0), Math.max(cy, 0)), 1e-6);
      f.v[i] = f.v[i] + (1 - wv) * cvv;
      f.wt[i] = 1;
    }
  }
  return levels[0].v;
}

function toImage(pl, alpha) {
  const n = pl.w * pl.h, img = P.create(pl.w, pl.h);
  for (let i = 0; i < n; i++) {
    for (let c = 0; c < 3; c++) img.data[i * 4 + c] = Math.max(0, Math.min(255, Math.round(pl.c[c][i])));
    img.data[i * 4 + 3] = alpha ? Math.max(0, Math.min(255, Math.round(alpha[i] * 255))) : 255;
  }
  return img;
}

module.exports = { planes, sample, upscaleShift, boxBlur, pushPull, toImage };
