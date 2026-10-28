"""Cuts the V2 UI kit out of its Figma export sheets and renders the kit's drop shadows.

The sheets are the 'Unity Export' section of the Figma file's 'V2 · Design System' page
(figma-kit-sheet.js builds them): every piece on pure black and on pure white, exported at 2x.
Figma's PNG export flattens transparency, so each piece's alpha is recovered from the pair:
over black a pixel is a*C, over white a*C + (1 - a)*255, so a = 1 - (white - black)/255 and
C = black/a.

Drop shadows are not on the sheets. The game draws a shadow as its own sprite under each face
(ScreenLayoutBuilder 'shadowUse'), so each shadow is rendered here from the V2 effect: the
shape, offset relative to the drop the layout applies, Gaussian blur with sigma = radius / 2
(Figma's blur radius is two standard deviations), in the effect's colour and opacity.

Writes the sprites under Assets/_Project/Art and the importer manifest ArtSource/Kit/V2.json
(pixels per unit and 9-slice borders, which KitImporter applies).

    python Tools/ArtPipeline/V2/kit.py --black kit_black.png --white kit_white.png
"""
import argparse
import json
import math
import os

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ART = os.path.join(REPO, 'Assets', '_Project', 'Art')


MANIFEST_COMMENT = ('V2 UI kit, written by Tools/ArtPipeline/V2/kit.py and art.py: ppu is pixels per unit; '
                    'nine is left, top, right, bottom in sprite pixels; mipmaps marks the portraits, which keep '
                    'theirs; shadow is how far (spread) beyond the face it was rendered for (face, w x h) a '
                    'shadow reaches and how far below it lies (drop), in canvas units.')


def merge_manifest(path, entries):
    """Writes these entries into the importer manifest: each replaces the entry with its path, in
    place, new ones go at the end, and the entries the other script wrote stay."""
    existing = json.load(open(path, encoding='utf-8')).get('sprites', []) if os.path.exists(path) else []
    new = {e['path']: e for e in entries}
    merged = [new.pop(e['path'], e) for e in existing]
    merged += [e for e in entries if e['path'] in new]
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'comment': MANIFEST_COMMENT, 'sprites': merged}, f, indent=2, ensure_ascii=False)
        f.write('\n')
    return len(merged)


def recover_alpha(black, white):
    """RGBA float [0, 1] from the same pixels rendered over black and over white."""
    b = black[..., :3].astype(np.float64)
    w = white[..., :3].astype(np.float64)
    a = 1.0 - np.clip((w - b).mean(axis=2), 0.0, 255.0) / 255.0
    rgb = np.zeros_like(b)
    seen = a > 0.5 / 255.0
    rgb[seen] = np.clip(b[seen] / (a[seen, None] * 255.0), 0.0, 1.0)
    a[~seen] = 0.0
    return np.dstack([rgb, a])


def to_png(rgba, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.clip(np.round(rgba * 255.0), 0, 255).astype(np.uint8)
    Image.fromarray(data, 'RGBA').save(path, optimize=True)


def rounded_rect_mask(w, h, radii, pad, scale, spread=0.0):
    """Anti-aliased coverage of a rounded rectangle (canvas units) inside a canvas of
    (w + 2 pad) x (h + 2 pad), at 'scale' pixels per unit; radii are tl, tr, br, bl."""
    W, H = int(round((w + 2 * pad) * scale)), int(round((h + 2 * pad) * scale))
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float64)
    x = (xs + 0.5) / scale - pad - w / 2.0
    y = (ys + 0.5) / scale - pad - h / 2.0
    tl, tr, br, bl = radii
    r = np.where(x < 0, np.where(y < 0, tl, bl), np.where(y < 0, tr, br)).astype(np.float64)
    qx = np.abs(x) - (w / 2.0 - r)
    qy = np.abs(y) - (h / 2.0 - r)
    outside = np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0))
    inside = np.minimum(np.maximum(qx, qy), 0.0)
    d = (outside + inside - r - spread) * scale    # signed distance in pixels
    return np.clip(0.5 - d, 0.0, 1.0)


def hex_rgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def render_shadow(mask_at, effects, drop, scale):
    """Composites the drop shadows of one shape. mask_at(spread) returns the shape's coverage
    on the padded canvas; each effect is moved by its offset less the drop the layout applies."""
    acc = None
    for e in effects:
        m = mask_at(e.get('spread', 0.0))
        dy = int(round((e.get('y', 0) - drop) * scale))
        dx = int(round(e.get('x', 0) * scale))
        m = np.roll(np.roll(m, dy, axis=0), dx, axis=1)
        a = gaussian_filter(m, sigma=e['radius'] / 2.0 * scale, mode='constant') * e['alpha']
        rgba = np.dstack([np.broadcast_to(hex_rgb(e['color']), a.shape + (3,)), a])
        if acc is None:
            acc = rgba
        else:
            # source over: the later effect over the earlier ones
            out_a = rgba[..., 3] + acc[..., 3] * (1 - rgba[..., 3])
            num = rgba[..., :3] * rgba[..., 3:4] + acc[..., :3] * acc[..., 3:4] * (1 - rgba[..., 3:4])
            rgb = np.where(out_a[..., None] > 0, num / np.maximum(out_a[..., None], 1e-9), 0)
            acc = np.dstack([rgb, out_a])
    return acc


def padding(effects, drop):
    ext = max(1.5 * e['radius'] + e.get('spread', 0) + abs(e.get('y', 0) - drop) + abs(e.get('x', 0)) for e in effects)
    return int(math.ceil(ext)) + 2


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--black', required=True)
    p.add_argument('--white', required=True)
    p.add_argument('--sheet', default=os.path.join(HERE, 'kit-sheet.json'))
    p.add_argument('--spec', default=os.path.join(HERE, 'kit.json'))
    p.add_argument('--manifest', default=os.path.join(REPO, 'ArtSource', 'Kit', 'V2.json'))
    p.add_argument('--review', default=None, help='folder for review images (optional)')
    args = p.parse_args()

    spec = json.load(open(args.spec, encoding='utf-8'))
    sheet = json.load(open(args.sheet, encoding='utf-8'))
    s = spec['scale']
    black = np.asarray(Image.open(args.black).convert('RGB'))
    white = np.asarray(Image.open(args.white).convert('RGB'))
    if black.shape != white.shape:
        raise SystemExit('the black and white sheets differ in size')

    boxes = {b[0]: b[1:] for b in sheet['boxes']}
    faces = {}
    manifest = []
    for face in spec['faces']:
        x, y, w, h = boxes[face['id']]
        crop = (slice(y * s, (y + h) * s), slice(x * s, (x + w) * s))
        rgba = recover_alpha(black[crop], white[crop])
        if face.get('flip'):
            rgba = rgba[:, ::-1]
        faces[face['id']] = (rgba, w, h)
        for out in face['outs']:
            to_png(rgba, os.path.join(ART, out))
            entry = {'path': out, 'ppu': spec['ppu']}
            if 'nine' in face:
                entry['nine'] = [v * s for v in face['nine']]
            manifest.append(entry)
        print(f"{face['id']:18s} {w}x{h} -> {', '.join(face['outs'])}")

    for sh in spec['shadows']:
        effects = spec['effects'][sh['effects']]
        drop = sh['drop']
        pad = padding(effects, drop)
        shape = sh['shape']
        rmax = max(e['radius'] for e in effects)
        nine = None
        if 'face' in shape:
            alpha, w, h = faces[shape['face']][0][..., 3], faces[shape['face']][1], faces[shape['face']][2]
            P = pad * s

            def mask_at(spread, alpha=alpha, P=P):
                return np.pad(alpha, P)
        else:
            radii = shape.get('radii', [shape.get('r', 0)] * 4)
            r = max(radii)
            core = int(math.ceil(r + 1.5 * rmax + 2))
            w = shape['w'] or 2 * core + 2
            h = shape['h'] or 2 * core + 2
            if sh['slice'] == 'both':
                nine = [core + pad] * 4
            elif sh['slice'] == 'vertical':
                nine = [0, core + pad, 0, core + pad]
            elif sh['slice'] == 'horizontal':
                face_nine = next(f['nine'] for f in spec['faces'] if f['id'] == sh['face'])
                side = max(face_nine[0], int(math.ceil(r + 1.5 * rmax))) + pad
                nine = [side, 0, side, 0]

            def mask_at(spread, w=w, h=h, radii=radii, pad=pad):
                return rounded_rect_mask(w, h, radii, pad, s, spread)

        rgba = render_shadow(mask_at, effects, drop, s)
        to_png(rgba, os.path.join(ART, sh['out']))
        entry = {'path': sh['out'], 'ppu': spec['ppu']}
        if nine:
            entry['nine'] = [v * s for v in nine]
        entry['shadow'] = {'spread': pad, 'drop': drop, 'slice': sh['slice'], 'face': [w, h]}
        manifest.append(entry)
        print(f"{os.path.basename(sh['out']):34s} shape {w}x{h} pad {pad} drop {drop} slice {sh['slice']} nine {nine}")

    total = merge_manifest(args.manifest, manifest)
    print(f'{len(manifest)} sprites -> {os.path.relpath(args.manifest, REPO)} ({total} in all)')

    if args.review:
        os.makedirs(args.review, exist_ok=True)
        tiles = []
        for e in manifest:
            im = Image.open(os.path.join(ART, e['path'])).convert('RGBA')
            bg = Image.new('RGBA', (im.width + 16, im.height + 16), (128, 128, 128, 255))
            bg.alpha_composite(im, (8, 8))
            tiles.append(bg)
        width = 2400
        x = y = row = 0
        placed = []
        for t in tiles:
            if x + t.width > width:
                x, y, row = 0, y + row, 0
            placed.append((t, x, y))
            x += t.width
            row = max(row, t.height)
        out = Image.new('RGBA', (width, y + row), (40, 40, 40, 255))
        for t, x, y in placed:
            out.alpha_composite(t, (x, y))
        out.convert('RGB').save(os.path.join(args.review, 'kit_on_grey.png'))


if __name__ == '__main__':
    main()
