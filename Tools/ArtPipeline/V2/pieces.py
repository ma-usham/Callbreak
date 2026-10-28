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
- UI/Table/UI_PromptStrip.png and UI_PromptStrip_Rim.png: the table's prompt (V2 "Prompt Strip",
  62 units high without its tip: a pill of #17140F at 80% with a 1-unit #DDB565 rim at 50%). The
  pill is white, as the prompt tints it (#17140F, crimson as it flashes for a card that cannot be
  played); the rim is its own picture over it, so it stays gold.
- UI/Table/UI_NamePlate.png: the seats' name plate (V2 "Name Plate": a 40-unit pill of #17140F at
  80% with a 1-unit #DDB565 rim at 60%), redrawn over kit.py's cut (run this after it).
- UI/Controls/UI_Spinner.png: V2's "Spinner" (the table's Reconnecting overlay): a #E6D4BD ring
  (inner radius 0.78 of the outer) with a #A3121F arc from twelve o'clock round 225°, here 64
  units across.

The game draws its UI in linear light (Player settings: Linear colour space), so a translucent
colour over the table looks different from Figma's blend, which is in sRGB: V2's dark plates at 80%
come out grey. Their opacities are raised (linear_alpha) so that, over the table's felt, they look
as V2 draws them; the table's dims (black at 60% in V2) are set the same way in code.

Each existing file keeps its size, so its import settings (ArtSource/Kit/V2.json) stand.

    python Tools/ArtPipeline/V2/pieces.py
"""
import math
import os

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

from kit import ART, REPO, hex_rgb, merge_manifest

SUPER = 4      # supersampling for the ring's edges
TABLE_FELT = '#C0AC8E'      # the Himalayan Ivory felt round the seats, as the game draws it


def to_linear(v):
    return np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)


def linear_alpha(alpha, colour, ground=TABLE_FELT):
    """The opacity at which `colour`, blended in linear light over `ground`, looks as Figma's sRGB
    blend at `alpha` does."""
    c, g = np.array(hex_rgb(colour)), np.array(hex_rgb(ground))
    seen = (1.0 - alpha) * g + alpha * c
    return float(np.mean((to_linear(g) - to_linear(seen)) / (to_linear(g) - to_linear(c))))


def over(top, bottom):
    """`top` over `bottom` (straight RGBA, 0-1), in sRGB as Figma composites a layer's parts."""
    ta, ba = top[..., 3:4], bottom[..., 3:4]
    a = ta + ba * (1.0 - ta)
    rgb = np.where(a > 1e-6, (top[..., :3] * ta + bottom[..., :3] * ba * (1.0 - ta)) / np.maximum(a, 1e-6), 0.0)
    return np.dstack([rgb, a])


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


def pill(width_px, height_px, rim_px=0.0, colour='#FFFFFF', alpha=1.0):
    """A pill (ends as round as it is high), anti-aliased; with rim_px, only a band that wide inside its edge."""
    ys, xs = np.mgrid[0:height_px, 0:width_px].astype(np.float64) + 0.5
    r = height_px / 2.0
    # Distance from the pill's spine (a horizontal segment between the ends' centres), less the radius.
    dx = np.maximum(np.abs(xs - width_px / 2.0) - (width_px / 2.0 - r), 0.0)
    d = np.hypot(dx, ys - r) - r
    cover = np.clip(0.5 - d, 0.0, 1.0)
    if rim_px:
        cover -= np.clip(0.5 - (d + rim_px), 0.0, 1.0)
    rgba = np.zeros((height_px, width_px, 4))
    rgba[..., :3] = hex_rgb(colour)
    rgba[..., 3] = cover * alpha
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
    tile = np.asarray(Image.open(tile_path).convert('RGB')).astype(int)
    if np.abs(tile[tile.shape[0] // 2, 4] - np.round(np.array(hex_rgb('#DDB565')) * 255)).max() > 3:   # not framed yet
        save(gold_frame(tile_path, 8), tile_path)     # 4 canvas units at 200 pixels per unit
    disc_path = os.path.join(ART, 'UI', 'Buttons', 'UI_ButtonIcon_Crimson.png')
    save(crimson_disc(128), disc_path)            # 64 canvas units at 200 pixels per unit
    # The copy icon on that disc, in ivory: V2's dark glyph does not read on crimson (as with Icon_Play).
    copy = np.asarray(Image.open(os.path.join(ART, 'UI', 'Icons', 'Icon_Copy.png')).convert('RGBA')).astype(np.float64) / 255.0
    copy[..., :3] = hex_rgb('#FFFCF7')
    copy_path = os.path.join(ART, 'UI', 'Icons', 'Icon_Copy_Ivory.png')
    save(copy, copy_path)
    # The prompt strip: 62 units high and 124 wide at 200 pixels per unit, its round ends sliced.
    strip_path = os.path.join(ART, 'UI', 'Table', 'UI_PromptStrip.png')
    save(pill(248, 124), strip_path)
    rim_path = os.path.join(ART, 'UI', 'Table', 'UI_PromptStrip_Rim.png')
    save(pill(248, 124, rim_px=2, colour='#DDB565', alpha=linear_alpha(0.5, '#DDB565', '#17140F')), rim_path)
    # The name plate: 140 x 40 units, its rim on its fill.
    plate_path = os.path.join(ART, 'UI', 'Table', 'UI_NamePlate.png')
    fill = pill(280, 80, colour='#17140F', alpha=linear_alpha(0.8, '#17140F'))
    rim = pill(280, 80, rim_px=2, colour='#DDB565', alpha=0.6)
    save(over(rim, fill), plate_path)
    spinner_path = os.path.join(ART, 'UI', 'Controls', 'UI_Spinner.png')
    save(spinner(128, inner=0.78, end=math.radians(135), ring='#E6D4BD', ring_alpha=1.0), spinner_path)
    merge_manifest(os.path.join(REPO, 'ArtSource', 'Kit', 'V2.json'), [
        {'path': 'UI/Buttons/UI_ButtonIcon_Crimson.png', 'ppu': 200},
        {'path': 'UI/Controls/UI_Spinner.png', 'ppu': 200},
        {'path': 'UI/Icons/Icon_Copy_Ivory.png', 'ppu': 200},
        {'path': 'UI/Table/UI_PromptStrip.png', 'ppu': 200, 'nine': [62, 0, 62, 0]},
        {'path': 'UI/Table/UI_PromptStrip_Rim.png', 'ppu': 200, 'nine': [62, 0, 62, 0]},
    ])
    lines = [os.path.join(ART, 'UI', 'Decor', f) for f in ('Deco_ModeDivider_Vertical.png', 'Deco_ProfileDividerVertical.png')]
    for p in lines:
        save(vertical_line(p, 4), p)              # 2 canvas units at 200 pixels per unit
    print('prompt and plate opacity over the felt: %.3f; the table dims (black at 60%%): %.3f'
          % (linear_alpha(0.8, '#17140F'), linear_alpha(0.6, '#000000')))
    for p in (arc_path, glow_path, tile_path, disc_path, copy_path, *lines, strip_path, rim_path, plate_path, spinner_path):
        print(os.path.relpath(p, os.path.join(ART, '..', '..', '..')).replace('\\', '/'))


if __name__ == '__main__':
    main()
