// png.js: a minimal PNG reader/writer for the Play Online art batch (8-bit grey, grey+alpha, RGB, RGBA and
// palette, non-interlaced). Images are { w, h, r, g, b, a } with Float32Array channels: colour in LINEAR
// light (sRGB decoded), alpha straight 0..1, the same convention as Tools/ArtTool/Img.cs.
'use strict';
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const LUT = new Float32Array(256);
for (let i = 0; i < 256; i++) LUT[i] = toLinear(i / 255);

function toLinear(s) { return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4); }
function toSrgb01(l) {
  if (l <= 0) return 0;
  if (l >= 1) return 1;
  return l <= 0.0031308 ? l * 12.92 : 1.055 * Math.pow(l, 1 / 2.4) - 0.055;
}
function toByte(l) { return Math.max(0, Math.min(255, Math.round(toSrgb01(l) * 255))); }

function create(w, h, alpha = 1) {
  const n = w * h;
  const img = { w, h, r: new Float32Array(n), g: new Float32Array(n), b: new Float32Array(n), a: new Float32Array(n) };
  if (alpha) img.a.fill(alpha);
  return img;
}

function clone(img) {
  return { w: img.w, h: img.h, r: Float32Array.from(img.r), g: Float32Array.from(img.g), b: Float32Array.from(img.b), a: Float32Array.from(img.a) };
}

function paeth(a, b, c) {
  const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
}

function load(file) {
  const buf = fs.readFileSync(file);
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error(`${file}: not a PNG`);
  let pos = 8, w = 0, h = 0, depth = 0, ctype = 0, interlace = 0, palette = null, trns = null;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos), type = buf.toString('ascii', pos + 4, pos + 8), data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); depth = data[8]; ctype = data[9]; interlace = data[12]; }
    else if (type === 'PLTE') palette = data;
    else if (type === 'tRNS') trns = data;
    else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (depth !== 8) throw new Error(`${file}: bit depth ${depth} not supported`);
  if (interlace) throw new Error(`${file}: interlaced PNG not supported`);
  const ch = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[ctype];
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * ch, out = Buffer.alloc(stride * h);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
    for (let x = 0; x < stride; x++) {
      const v = raw[src + x];
      const left = x >= ch ? out[dst + x - ch] : 0, up = y > 0 ? out[dst - stride + x] : 0, ul = y > 0 && x >= ch ? out[dst - stride + x - ch] : 0;
      let o;
      switch (f) {
        case 0: o = v; break;
        case 1: o = v + left; break;
        case 2: o = v + up; break;
        case 3: o = v + ((left + up) >> 1); break;
        case 4: o = v + paeth(left, up, ul); break;
        default: throw new Error(`${file}: bad filter ${f}`);
      }
      out[dst + x] = o & 255;
    }
  }
  const img = create(w, h, 0);
  for (let i = 0; i < w * h; i++) {
    let R, G, B, A = 255;
    const p = i * ch;
    if (ctype === 0) { R = G = B = out[p]; }
    else if (ctype === 4) { R = G = B = out[p]; A = out[p + 1]; }
    else if (ctype === 2) { R = out[p]; G = out[p + 1]; B = out[p + 2]; }
    else if (ctype === 6) { R = out[p]; G = out[p + 1]; B = out[p + 2]; A = out[p + 3]; }
    else { const k = out[p]; R = palette[k * 3]; G = palette[k * 3 + 1]; B = palette[k * 3 + 2]; A = trns && k < trns.length ? trns[k] : 255; }
    img.r[i] = LUT[R]; img.g[i] = LUT[G]; img.b[i] = LUT[B]; img.a[i] = A / 255;
  }
  return img;
}

const CRC = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; }
  return t;
})();
function crc32(bufs) {
  let c = -1;
  for (const b of bufs) for (let i = 0; i < b.length; i++) c = CRC[(c ^ b[i]) & 255] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const t = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32([t, data]));
  return Buffer.concat([len, t, data, crc]);
}

function save(img, file) {
  const { w, h } = img, stride = w * 4;
  const raw = Buffer.alloc((stride + 1) * h);
  const row = Buffer.alloc(stride);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x, a = Math.max(0, Math.min(255, Math.round(img.a[i] * 255)));
      // fully transparent pixels keep their colour (bleed) only if alpha > 0; otherwise black, like Img.cs
      row[x * 4] = toByte(img.r[i]); row[x * 4 + 1] = toByte(img.g[i]); row[x * 4 + 2] = toByte(img.b[i]); row[x * 4 + 3] = a;
    }
    const o = y * (stride + 1);
    raw[o] = 1;                                                     // filter: Sub
    for (let x = 0; x < stride; x++) raw[o + 1 + x] = (row[x] - (x >= 4 ? row[x - 4] : 0)) & 255;
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  const png = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
  fs.mkdirSync(path.dirname(path.resolve(file)), { recursive: true });
  fs.writeFileSync(file, png);
}

function parseHex(hex) {
  hex = hex.replace('#', '');
  return { r: LUT[parseInt(hex.slice(0, 2), 16)], g: LUT[parseInt(hex.slice(2, 4), 16)], b: LUT[parseInt(hex.slice(4, 6), 16)] };
}

module.exports = { load, save, create, clone, toLinear, toSrgb01, toByte, parseHex, LUT };
