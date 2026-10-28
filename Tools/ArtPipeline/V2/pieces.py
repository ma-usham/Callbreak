"""Draws the V2 menu pieces that V2 makes of plain shapes, from their Figma geometry.

- UI/Decor/Deco_SearchArc.png: the spinner (Figma "V2 · Screens", 05 Matchmaking "SearchArc" and
  17 Scene Loading "Ring"): a thin ring (inner radius 0.93 of the outer) with a crimson arc from
  twelve o'clock to 0.31 rad past three. V2 paints the ring in black at 70%, a colour variable it
  lost (as on its other hairlines), so the ring takes V2's line colour, #C9AE8C, at that strength.
  The spinner turns the whole picture, so the ring and the arc are one.
- UI/Decor/Deco_CoinGlow.png: the glow behind a reward's coins (26 Daily Rewards and 22 Earn Coins
  "CoinGlow"): #F2DEA8, 75% at the middle fading to nothing at the edge.
- UI/Buttons/UI_ButtonIcon_Crimson.png and UI/Icons/Icon_Copy_Ivory.png: Private Lobby's copy
  button (04, the room code's "Button/Icon", Style=Crimson, Size=S) and its glyph in ivory.
- UI/Decor/Deco_ModeDivider_Vertical.png and Deco_ProfileDividerVertical.png: the vertical dividers
  (24 Play Online "ModeDivider", 14 Profile "StatsDivider", 22 Name Entry "CharacterDivider"): a
  plain 2-unit line of #C9AE8C at 80%, where the old ones had a diamond in the middle. (Profile and
  Name Entry paint theirs black at 80%, the lost colour variable again.)
- UI/Panels/UI_DayTile_Bonus.png: Day 7's tile (26 Daily Rewards "Tile6"), the light card with
  V2's gold frame ("Tile6Gold", 4 px of #DDB565 inside the edge) drawn into it.

Each file keeps its size, so its import settings (ArtSource/Kit/V2.json) stand.

    python Tools/ArtPipeline/V2/pieces.py
"""
import math
import os

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

from kit import ART, REPO, hex_rgb, merge_manifest

SUPER = 4      # supersampling for the ring's edges


def save(rgba, path):
    data = np.clip(np.round(rgba * 255.0), 0, 255).astype(np.uint8)
    Image.fromarray(data, 'RGBA').save(path, optimize=True)


def spinner(size, inner=0.93, start=-math.pi / 2, end=0.31, ring='#C9AE8C', ring_alpha=0.7, arc='#A3121F'):
    n = size * SUPER
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float64)
    x = (xs + 0.5) / n - 0.5
    y = (ys + 0.5) / n - 0.5
    radius = np.hypot(x, y) * 2.0
    in_ring = (radius <= 1.0) & (radius >= inner)
    angle = np.arctan2(y, x)                     # clockwise from three o'clock (y grows down), as Figma's
    sweep = (angle - start) % (2 * math.pi)
    in_arc = in_ring & (sweep <= (end - start) % (2 * math.pi))
    rgba = np.zeros((n, n, 4))
    rgba[in_ring, :3] = hex_rgb(ring)
    rgba[in_ring, 3] = ring_alpha
    rgba[in_arc, :3] = hex_rgb(arc)
    rgba[in_arc, 3] = 1.0
    # Average down through premultiplied colour, so the edges blend cleanly.
    pre = np.dstack([rgba[..., :3] * rgba[..., 3:4], rgba[..., 3]])
    pre = pre.reshape(size, SUPER, size, SUPER, 4).mean(axis=(1, 3))
    a = pre[..., 3:4]
    rgb = np.where(a > 1e-6, pre[..., :3] / np.maximum(a, 1e-6), 0.0)
    return np.dstack([rgb, a])


def glow(size, colour='#F2DEA8', alpha=0.75):
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    radius = np.hypot((xs + 0.5) / size - 0.5, (ys + 0.5) / size - 0.5) * 2.0
    rgba = np.zeros((size, size, 4))
    rgba[..., :3] = hex_rgb(colour)
    rgba[..., 3] = np.clip(1.0 - radius, 0.0, 1.0) * alpha
    return rgba


def vertical(stops, n):
    """Colours down a column of n pixels through (position, '#hex') stops."""
    t = (np.arange(n) + 0.5) / n
    pos = [p for p, _ in stops]
    rgb = np.array([hex_rgb(c) for _, c in stops])
    return np.stack([np.interp(t, pos, rgb[:, k]) for k in range(3)], axis=1)


def crimson_disc(size):
    """V2's small crimson icon button ("Button/Icon", Style=Crimson, Size=S; 64 units): a crimson
    disc, lit from the top, with a 3-unit gold rim and a 2-unit light line inside its top edge."""
    n = size * SUPER
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float64)
    radius = np.hypot((xs + 0.5) / n - 0.5, (ys + 0.5) / n - 0.5) * 2.0
    unit = 2.0 / 64.0                                            # one canvas unit, as a share of the radius
    face = vertical([(0.0, '#D42433'), (0.5, '#A3121F'), (1.0, '#7A0A14')], n)[:, None, :].repeat(n, axis=1)
    rim = vertical([(0.0, '#F3DFA8'), (0.5, '#DDB565'), (1.0, '#B0833A')], n)[:, None, :].repeat(n, axis=1)
    rgb = np.where((radius > 1.0 - 3 * unit)[..., None], rim, face)
    # The inner shadow's light (white at 30%, 2 units down): the disc minus itself moved down 2 units.
    shifted = np.hypot((xs + 0.5) / n - 0.5, (ys + 0.5) / n - 0.5 - unit) * 2.0
    light = (radius <= 1.0 - 3 * unit) & (shifted > 1.0 - 3 * unit)
    rgb = np.where(light[..., None], rgb * 0.7 + 0.3, rgb)
    rgba = np.dstack([rgb, (radius <= 1.0).astype(np.float64)])
    pre = np.dstack([rgba[..., :3] * rgba[..., 3:4], rgba[..., 3]])
    pre = pre.reshape(size, SUPER, size, SUPER, 4).mean(axis=(1, 3))
    a = pre[..., 3:4]
    return np.dstack([np.where(a > 1e-6, pre[..., :3] / np.maximum(a, 1e-6), 0.0), a])


def vertical_line(path, width_px, colour='#C9AE8C', alpha=0.8):
    """A plain vertical line width_px wide down the middle of the file, the rest clear (keeps the file's size)."""
    w, h = Image.open(path).size
    xs = np.arange(w) + 0.5
    cover = np.clip(width_px / 2.0 + 0.5 - np.abs(xs - w / 2.0), 0.0, 1.0)
    rgba = np.zeros((h, w, 4))
    rgba[..., :3] = hex_rgb(colour)
    rgba[..., 3] = cover[None, :] * alpha
    return rgba


def gold_frame(path, width_px, colour='#DDB565'):
    """The card with a frame of this colour drawn inside its edge, width_px wide (anti-aliased)."""
    rgba = np.asarray(Image.open(path).convert('RGBA')).astype(np.float64) / 255.0
    inside = distance_transform_edt(np.pad(rgba[..., 3] > 0.5, 1))[1:-1, 1:-1]     # pixels to the card's edge
    cover = np.clip(width_px + 0.5 - inside, 0.0, 1.0) * (rgba[..., 3] > 0.0)
    rgba[..., :3] = rgba[..., :3] * (1.0 - cover[..., None]) + hex_rgb(colour) * cover[..., None]
    return rgba


def main():
    arc_path = os.path.join(ART, 'UI', 'Decor', 'Deco_SearchArc.png')
    save(spinner(Image.open(arc_path).size[0]), arc_path)
    glow_path = os.path.join(ART, 'UI', 'Decor', 'Deco_CoinGlow.png')
    save(glow(Image.open(glow_path).size[0]), glow_path)
    tile_path = os.path.join(ART, 'UI', 'Panels', 'UI_DayTile_Bonus.png')
    save(gold_frame(tile_path, 8), tile_path)     # 4 canvas units at 200 pixels per unit
    disc_path = os.path.join(ART, 'UI', 'Buttons', 'UI_ButtonIcon_Crimson.png')
    save(crimson_disc(128), disc_path)            # 64 canvas units at 200 pixels per unit
    # The copy icon on that disc, in ivory: V2's dark glyph does not read on crimson (as with Icon_Play).
    copy = np.asarray(Image.open(os.path.join(ART, 'UI', 'Icons', 'Icon_Copy.png')).convert('RGBA')).astype(np.float64) / 255.0
    copy[..., :3] = hex_rgb('#FFFCF7')
    copy_path = os.path.join(ART, 'UI', 'Icons', 'Icon_Copy_Ivory.png')
    save(copy, copy_path)
    merge_manifest(os.path.join(REPO, 'ArtSource', 'Kit', 'V2.json'), [
        {'path': 'UI/Buttons/UI_ButtonIcon_Crimson.png', 'ppu': 200},
        {'path': 'UI/Icons/Icon_Copy_Ivory.png', 'ppu': 200},
    ])
    lines = [os.path.join(ART, 'UI', 'Decor', f) for f in ('Deco_ModeDivider_Vertical.png', 'Deco_ProfileDividerVertical.png')]
    for p in lines:
        save(vertical_line(p, 4), p)              # 2 canvas units at 200 pixels per unit
    for p in (arc_path, glow_path, tile_path, disc_path, copy_path, *lines):
        print(os.path.relpath(p, os.path.join(ART, '..', '..', '..')).replace('\\', '/'))


if __name__ == '__main__':
    main()
