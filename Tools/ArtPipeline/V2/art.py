"""Fits the V2 icons, decor and portraits into the game's art files.

The pieces come from the 'Unity Export · Art' sheets (figma-art-sheet.js), exported at 3x on black
and on white; their alpha is recovered as kit.py recovers the kit's. Every output keeps the old
file's size and import settings, and the V2 piece is fitted into the old picture's ink box (the
box round its visible pixels): contained, keeping its proportions, or stretched where the art is
sliced across. So every screen shows the new art where, and as large as, it showed the old. Icons
are one colour: their outline is filled with the colour V2 draws them in on the screens. Portraits
are cut into discs, opaque inside the circle, as the game's avatars are (ADR 041).

Writes the files under Assets/_Project/Art and their entries in ArtSource/Kit/V2.json.

    python Tools/ArtPipeline/V2/art.py --black art_black.png --white art_white.png
"""
import argparse
import json
import os
import re

import numpy as np
from PIL import Image

from kit import ART, HERE, REPO, merge_manifest, recover_alpha, to_png


def meta_settings(path):
    """Pixels per unit, the 9-slice border (left, top, right, bottom) and mip maps of a texture's .meta."""
    text = open(path + '.meta', encoding='utf-8').read()
    ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', text).group(1))
    x, y, z, w = (float(v) for v in re.search(r'spriteBorder: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+), w: ([\d.]+)\}', text).groups())
    mips = re.search(r'enableMipMap: (\d)', text).group(1) == '1'
    return ppu, [x, w, z, y], mips


def ink_box(alpha, threshold):
    ys, xs = np.where(alpha > threshold)
    if len(xs) == 0:
        return 0, 0, alpha.shape[1], alpha.shape[0]
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def resize(rgba, size):
    """Resamples straight-alpha RGBA through premultiplied colour, so edges keep no dark fringe."""
    pre = np.dstack([rgba[..., :3] * rgba[..., 3:4], rgba[..., 3]])
    channels = [np.asarray(Image.fromarray(pre[..., i].astype(np.float32), 'F').resize(size, Image.LANCZOS)) for i in range(4)]
    out = np.clip(np.dstack(channels), 0.0, 1.0)
    a = out[..., 3:4]
    out[..., :3] = np.where(a > 1e-4, out[..., :3] / np.maximum(a, 1e-4), 0.0)
    return np.clip(out, 0.0, 1.0)


def disc(rgba):
    """Opaque inside the circle the square fills, with an anti-aliased rim."""
    h, w = rgba.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.hypot(xs + 0.5 - w / 2.0, ys + 0.5 - h / 2.0)
    out = rgba.copy()
    out[..., 3] = np.clip(w / 2.0 - d + 0.5, 0.0, 1.0)
    return out


def hex_rgb(h):
    h = h.lstrip('#')
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--black', required=True)
    p.add_argument('--white', required=True)
    p.add_argument('--sheet', default=os.path.join(HERE, 'art-sheet.json'))
    p.add_argument('--spec', default=os.path.join(HERE, 'art.json'))
    p.add_argument('--manifest', default=os.path.join(REPO, 'ArtSource', 'Kit', 'V2.json'))
    args = p.parse_args()

    spec = json.load(open(args.spec, encoding='utf-8'))
    s = spec['scale']
    boxes = {b[0]: b[1:] for b in json.load(open(args.sheet, encoding='utf-8'))['boxes']}
    black = np.asarray(Image.open(args.black).convert('RGB'))
    white = np.asarray(Image.open(args.white).convert('RGB'))
    pieces = {}
    entries = []
    for item in spec['items']:
        src = item['src']
        if src not in pieces:
            x, y, w, h = boxes[src]
            crop = (slice(y * s, (y + h) * s), slice(x * s, (x + w) * s))
            pieces[src] = recover_alpha(black[crop], white[crop])
        piece = pieces[src]
        if 'crop' in item:
            cx, cy, cw, ch = item['crop']
            piece = piece[cy * s:(cy + ch) * s, cx * s:(cx + cw) * s]
        x0, y0, x1, y1 = ink_box(piece[..., 3], 0.02)
        piece = piece[y0:y1, x0:x1].copy()
        if 'color' in item:
            piece[..., :3] = hex_rgb(item['color'])

        out = os.path.join(ART, item['out'])
        model = os.path.join(ART, item.get('like', item['out']))
        old = np.asarray(Image.open(model).convert('RGBA'))
        height, width = old.shape[:2]
        fit = item.get('fit', 'contain')
        canvas = np.zeros((height, width, 4))
        if fit == 'disc':
            canvas = disc(resize(piece, (width, height)))
        else:
            bx0, by0, bx1, by1 = ink_box(old[..., 3], 20)
            bw, bh = bx1 - bx0, by1 - by0
            if fit == 'stretch':
                size = (bw, bh)
            else:
                k = min(bw / piece.shape[1], bh / piece.shape[0])
                size = (max(1, round(piece.shape[1] * k)), max(1, round(piece.shape[0] * k)))
            placed = resize(piece, size)
            ox = bx0 + (bw - size[0]) // 2
            oy = by0 + (bh - size[1]) // 2
            canvas[oy:oy + size[1], ox:ox + size[0]] = placed
        to_png(canvas, out)

        ppu, nine, mips = meta_settings(model)
        entry = {'path': item['out'], 'ppu': ppu}
        if any(nine):
            entry['nine'] = nine
        if mips:
            entry['mipmaps'] = True
        entries.append(entry)
        print(f"{item['out']:46s} {width}x{height} <- {src} ({fit})")

    total = merge_manifest(args.manifest, entries)
    print(f'{len(entries)} sprites -> {os.path.relpath(args.manifest, REPO)} ({total} in all)')


if __name__ == '__main__':
    main()
