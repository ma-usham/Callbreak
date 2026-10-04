// Minimal PNG reader/writer for the ShopTables layer work (node, no dependencies).
// read(path)  -> { w, h, data: Uint8Array RGBA (straight alpha) }
// write(path, img, { rgb: false, level: 6 })
'use strict';
const fs = require('fs');
const zlib = require('zlib');

const SIG = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

function read(path) {
  const buf = fs.readFileSync(path);
  if (!buf.subarray(0, 8).equals(SIG)) throw new Error('not a PNG: ' + path);
  let pos = 8, w = 0, h = 0, depth = 0, ctype = 0, interlace = 0;
  const idat = [];
  let palette = null, trns = null;
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const body = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      w = body.readUInt32BE(0); h = body.readUInt32BE(4);
      depth = body[8]; ctype = body[9]; interlace = body[12];
    } else if (type === 'PLTE') palette = body;
    else if (type === 'tRNS') trns = body;
    else if (type === 'IDAT') idat.push(body);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (interlace) throw new Error('interlaced PNG not supported: ' + path);
  if (depth !== 8) throw new Error('bit depth ' + depth + ' not supported: ' + path);
  const chans = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[ctype];
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * chans;
  const px = new Uint8Array(h * stride);
  let prev = new Uint8Array(stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)];
    const src = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const row = px.subarray(y * stride, (y + 1) * stride);
    for (let i = 0; i < stride; i++) {
      const a = i >= chans ? row[i - chans] : 0;
      const b = prev[i];
      const c = i >= chans ? prev[i - chans] : 0;
      let v = src[i];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1; else if (f === 4) v += paeth(a, b, c);
      row[i] = v & 0xff;
    }
    prev = row;
  }
  const data = new Uint8Array(w * h * 4);
  for (let i = 0, n = w * h; i < n; i++) {
    let r, g, b, a = 255;
    if (ctype === 6) { r = px[i * 4]; g = px[i * 4 + 1]; b = px[i * 4 + 2]; a = px[i * 4 + 3]; }
    else if (ctype === 2) { r = px[i * 3]; g = px[i * 3 + 1]; b = px[i * 3 + 2]; if (trns && r === trns.readUInt16BE(0) && g === trns.readUInt16BE(2) && b === trns.readUInt16BE(4)) a = 0; }
    else if (ctype === 0) { r = g = b = px[i]; }
    else if (ctype === 4) { r = g = b = px[i * 2]; a = px[i * 2 + 1]; }
    else if (ctype === 3) { const k = px[i]; r = palette[k * 3]; g = palette[k * 3 + 1]; b = palette[k * 3 + 2]; if (trns && k < trns.length) a = trns[k]; }
    data[i * 4] = r; data[i * 4 + 1] = g; data[i * 4 + 2] = b; data[i * 4 + 3] = a;
  }
  return { w, h, data };
}

function chunk(type, body) {
  const out = Buffer.alloc(12 + body.length);
  out.writeUInt32BE(body.length, 0);
  out.write(type, 4, 'ascii');
  body.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + body.length)), 8 + body.length);
  return out;
}

function write(path, img, opts) {
  opts = opts || {};
  const { w, h, data } = img;
  let rgb = !!opts.rgb;
  if (opts.auto) { rgb = true; for (let i = 3; i < data.length; i += 4) if (data[i] !== 255) { rgb = false; break; } }
  const chans = rgb ? 3 : 4;
  const stride = w * chans;
  const rows = Buffer.alloc(h * (stride + 1));
  const cur = new Uint8Array(stride), prev = new Uint8Array(stride);
  const cand = [new Uint8Array(stride), new Uint8Array(stride), new Uint8Array(stride), new Uint8Array(stride), new Uint8Array(stride)];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) for (let c = 0; c < chans; c++) cur[x * chans + c] = data[(y * w + x) * 4 + c];
    // adaptive filter: minimum sum of absolute differences
    let best = 0, bestSum = Infinity;
    for (let f = 0; f < 5; f++) {
      const o = cand[f];
      let sum = 0;
      for (let i = 0; i < stride; i++) {
        const a = i >= chans ? cur[i - chans] : 0, b = prev[i], c = i >= chans ? prev[i - chans] : 0;
        let v = cur[i];
        if (f === 1) v -= a; else if (f === 2) v -= b; else if (f === 3) v -= (a + b) >> 1; else if (f === 4) v -= paeth(a, b, c);
        v &= 0xff; o[i] = v; sum += v < 128 ? v : 256 - v;
      }
      if (sum < bestSum) { bestSum = sum; best = f; }
    }
    rows[y * (stride + 1)] = best;
    Buffer.from(cand[best].buffer, cand[best].byteOffset, stride).copy(rows, y * (stride + 1) + 1);
    prev.set(cur);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = rgb ? 2 : 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  const z = zlib.deflateSync(rows, { level: opts.level == null ? 6 : opts.level });
  fs.writeFileSync(path, Buffer.concat([SIG, chunk('IHDR', ihdr), chunk('IDAT', z), chunk('IEND', Buffer.alloc(0))]));
}

function create(w, h, fill) {
  const data = new Uint8Array(w * h * 4);
  if (fill) for (let i = 0; i < w * h; i++) { data[i * 4] = fill[0]; data[i * 4 + 1] = fill[1]; data[i * 4 + 2] = fill[2]; data[i * 4 + 3] = fill[3]; }
  return { w, h, data };
}

// crop + optional integer nearest upscale, for looking at details with the Read tool
function crop(img, x0, y0, cw, ch, scale) {
  scale = scale || 1;
  const ow = Math.round(cw * scale), oh = Math.round(ch * scale);
  const out = create(ow, oh);
  for (let y = 0; y < oh; y++) for (let x = 0; x < ow; x++) {
    const sx = x0 + Math.floor(x / scale), sy = y0 + Math.floor(y / scale);
    const o = (y * ow + x) * 4;
    if (sx < 0 || sy < 0 || sx >= img.w || sy >= img.h) { out.data[o + 3] = 255; continue; }
    const i = (sy * img.w + sx) * 4;
    out.data[o] = img.data[i]; out.data[o + 1] = img.data[i + 1]; out.data[o + 2] = img.data[i + 2]; out.data[o + 3] = img.data[i + 3];
  }
  return out;
}

// box/bilinear resize (area average when shrinking)
function resize(img, nw, nh) {
  const out = create(nw, nh);
  const sx = img.w / nw, sy = img.h / nh;
  if (sx >= 1 && sy >= 1) {
    for (let y = 0; y < nh; y++) {
      const y0 = y * sy, y1 = (y + 1) * sy;
      for (let x = 0; x < nw; x++) {
        const x0 = x * sx, x1 = (x + 1) * sx;
        let r = 0, g = 0, b = 0, a = 0, wsum = 0;
        for (let yy = Math.floor(y0); yy < Math.ceil(y1); yy++) {
          const wy = Math.min(yy + 1, y1) - Math.max(yy, y0);
          for (let xx = Math.floor(x0); xx < Math.ceil(x1); xx++) {
            const wx = Math.min(xx + 1, x1) - Math.max(xx, x0);
            const wgt = wx * wy, i = (Math.min(yy, img.h - 1) * img.w + Math.min(xx, img.w - 1)) * 4;
            const al = img.data[i + 3] / 255;
            r += img.data[i] * al * wgt; g += img.data[i + 1] * al * wgt; b += img.data[i + 2] * al * wgt; a += al * wgt; wsum += wgt;
          }
        }
        const o = (y * nw + x) * 4;
        if (a > 0) { out.data[o] = Math.round(r / a); out.data[o + 1] = Math.round(g / a); out.data[o + 2] = Math.round(b / a); }
        out.data[o + 3] = Math.round(255 * a / wsum);
      }
    }
    return out;
  }
  for (let y = 0; y < nh; y++) {
    const fy = Math.min(Math.max((y + 0.5) * sy - 0.5, 0), img.h - 1);
    const y0 = Math.floor(fy), y1 = Math.min(y0 + 1, img.h - 1), ty = fy - y0;
    for (let x = 0; x < nw; x++) {
      const fx = Math.min(Math.max((x + 0.5) * sx - 0.5, 0), img.w - 1);
      const x0 = Math.floor(fx), x1 = Math.min(x0 + 1, img.w - 1), tx = fx - x0;
      const o = (y * nw + x) * 4;
      const i00 = (y0 * img.w + x0) * 4, i01 = (y0 * img.w + x1) * 4, i10 = (y1 * img.w + x0) * 4, i11 = (y1 * img.w + x1) * 4;
      const w00 = (1 - tx) * (1 - ty) * img.data[i00 + 3], w01 = tx * (1 - ty) * img.data[i01 + 3], w10 = (1 - tx) * ty * img.data[i10 + 3], w11 = tx * ty * img.data[i11 + 3];
      const ws = w00 + w01 + w10 + w11;
      for (let c = 0; c < 3; c++) out.data[o + c] = ws > 0 ? Math.round((img.data[i00 + c] * w00 + img.data[i01 + c] * w01 + img.data[i10 + c] * w10 + img.data[i11 + c] * w11) / ws) : 0;
      out.data[o + 3] = Math.round(ws);
    }
  }
  return out;
}

module.exports = { read, write, create, crop, resize, crc32 };
