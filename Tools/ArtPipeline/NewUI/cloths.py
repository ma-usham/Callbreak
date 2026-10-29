"""The cloths the game is played on (the owner, 29 Oct 2026, ADR 058), as vector art.

Usage: python -P cloths.py <out-dir> [bleed-x bleed-y]
Writes (1920 x 1080, plus the bleed on each side when given; see scenes.set_bleed):
  green.svg, indigo.svg  a dhaka cloth: a deep radial base, the cross-stitch weave tiled over it, a warm
                         light and a faint stitched medallion where the trick lands, a vignette
  dusk.svg               the Himalaya at dusk: alpenglow peaks, dark calm hills, village lights, a pagoda
  frame.svg              the dhaka cloths' border, 384 x 384: gold lines with a row of stitched diamonds
                         between them and a motif in each corner. The game tiles its edges round the
                         screen (a 168-unit border), so the border hugs the screen on every aspect ratio.
These are Figma's Art/Dhaka Cloth · Green 191:59, · Indigo 191:61 (base, Weave, Art/Cloth Overlay 191:626)
and Art/Dusk Himalaya 191:649.
"""
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scenes as S  # noqa: E402

W, H, f = S.W, S.H, S.f

# The radial base of each dhaka cloth, centre to edge (Figma's fills).
CLOTHS = {"green": ("#2E6A46", "#0F2C1C"), "indigo": ("#33457E", "#121A38")}

FRAME = 384          # the frame's size, a whole number of diamond steps round the corners
FRAME_BORDER = 168   # its fixed corners; the 48 between them repeat along the screen's edges


def motif_cells(cx, cy):
    """V1's dhaka motif on a 13 x 13 stitch grid: the diamond's outline, a stitch in from each corner, a four-petal flower."""
    out = []
    for dx in range(-6, 7):
        for dy in range(-6, 7):
            ax, ay, d = abs(dx), abs(dy), abs(dx) + abs(dy)
            if d == 6 or (d == 4 and (dx == 0 or dy == 0)) or (d == 2 and ax != ay) or (ax == 1 and ay == 1) or d == 0:
                out.append((cx + dx, cy + dy, d == 0 or (ax == 1 and ay == 1)))
    return out


def weave(defs):
    """The cross-stitch weave as a tiling pattern: motifs a stitch apart on a checkerboard, 14 stitches of
    5 units (Figma's 140 px tile at half scale), white stitches and gold flower centres."""
    n, c = 14, 5
    white, gold = [], []
    for mx, my in [(0, 0), (7, 7)]:
        for x, y, is_gold in motif_cells(mx, my):
            (gold if is_gold else white).append(f"M{(x % n) * c} {(y % n) * c}h{c}v{c}h-{c}Z")
    defs.append(f'<pattern id="weave" patternUnits="userSpaceOnUse" x="0" y="0" width="{n * c}" height="{n * c}">'
                f'<path d="{"".join(white)}" fill="#FFFFFF" fill-opacity="0.18"/>'
                f'<path d="{"".join(gold)}" fill="#E9C46A" fill-opacity="0.47"/></pattern>')


def dhaka(name):
    inner, outer = CLOTHS[name]
    defs, b = [], []
    # The base: an ellipse of light 960 x 432 round (960, 570), the deep colour beyond it.
    defs.append(f'<radialGradient id="base" gradientUnits="userSpaceOnUse" cx="960" cy="570" r="960" '
                f'gradientTransform="translate(960 570) scale(1 0.45) translate(-960 -570)">'
                f'<stop offset="0" stop-color="{inner}"/><stop offset="1" stop-color="{outer}"/></radialGradient>')
    b.append(S.full("url(#base)"))
    weave(defs)
    b.append(f'<g opacity="0.5">{S.full("url(#weave)")}</g>')
    defs.append('<radialGradient id="glow" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FFF3D6" stop-opacity="0.16"/><stop offset="1" stop-color="#FFF3D6" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="500" rx="780" ry="430" fill="url(#glow)"/>')
    # A faint stitched medallion where the trick lands: three nested stepped diamonds.
    cells = []
    for r in (20, 14, 8):
        for dx in range(-r, r + 1):
            dy = r - abs(dx)
            cells += [(dx, dy), (dx, -dy)]
    g = 7
    d = "".join(f"M{f(960 + x * g - g / 2)} {f(500 + y * g - g / 2)}h{g}v{g}h-{g}Z" for x, y in sorted(set(cells)))
    b.append(f'<path d="{d}" fill="#F3DCA0" opacity="0.13"/>')
    # The vignette, round the 1920 x 1080 picture (darkest beyond it, in the margin).
    defs.append('<radialGradient id="vig" gradientUnits="userSpaceOnUse" cx="960" cy="497" r="1382" '
                'gradientTransform="translate(960 497) scale(1 0.5625) translate(-960 -497)">'
                '<stop offset="0.55" stop-color="#000000" stop-opacity="0"/><stop offset="1" stop-color="#000000" stop-opacity="0.5"/></radialGradient>')
    b.append(S.full("url(#vig)"))
    return S.svg("".join(b), defs)


def frame():
    """The border round the screen, drawn as Figma's overlay draws it round the 1920 x 1080 picture but in a
    384-unit square: corners fixed, the middle 48 units (two diamond steps) repeat along each edge."""
    n = FRAME
    b = [f'<rect x="18" y="18" width="{n - 36}" height="{n - 36}" rx="14" fill="none" stroke="#D2A957" stroke-opacity="0.55" stroke-width="2"/>',
         f'<rect x="60" y="60" width="{n - 120}" height="{n - 120}" rx="10" fill="none" stroke="#D2A957" stroke-opacity="0.35" stroke-width="1.5"/>']
    band = ""
    step = 24
    for x in range(48, n - 40, step):
        for y in (39, n - 39):
            band += f"M{x} {y - 8}L{x + 8} {y}L{x} {y + 8}L{x - 8} {y}Z"
    for y in range(48 + step, n - 40 - step, step):
        for x in (39, n - 39):
            band += f"M{x} {y - 8}L{x + 8} {y}L{x} {y + 8}L{x - 8} {y}Z"
    b.append(f'<path d="{band}" fill="#E7C680" opacity="0.32"/>')
    for cx, cy in [(120, 120), (n - 120, 120), (120, n - 120), (n - 120, n - 120)]:
        c = 6
        d = "".join(f"M{f(cx + x * c - c / 2)} {f(cy + y * c - c / 2)}h{c}v{c}h-{c}Z" for x, y, _ in motif_cells(0, 0))
        b.append(f'<path d="{d}" fill="#E7C680" opacity="0.42"/>')
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{n}" height="{n}" viewBox="0 0 {n} {n}">{"".join(b)}</svg>'


def dusk():
    rnd = random.Random(41)
    defs, b = [], []
    defs.append('<linearGradient id="dsky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1C2046"/><stop offset="0.25" stop-color="#36366A"/><stop offset="0.38" stop-color="#7A5A86"/><stop offset="0.46" stop-color="#C98273"/><stop offset="0.52" stop-color="#E6A47E"/><stop offset="1" stop-color="#E6A47E"/></linearGradient>')
    # The sky's gradient spans the 1080 rows of the picture; above it the night goes on, below it the glow.
    b.append(f'<rect x="{f(-S.BX - 2)}" y="{f(-S.BY - 2)}" width="{f(W + 2 * S.BX + 4)}" height="{f(S.BY + 4)}" fill="#1C2046"/>')
    b.append(f'<rect x="{f(-S.BX - 2)}" y="0" width="{f(W + 2 * S.BX + 4)}" height="{H}" fill="url(#dsky)"/>')
    b.append(f'<rect x="{f(-S.BX - 2)}" y="{H}" width="{f(W + 2 * S.BX + 4)}" height="{f(S.BY + 4)}" fill="#E6A47E"/>')
    for _ in range(90):
        x, y = rnd.uniform(0, W), rnd.uniform(0, 300)
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(rnd.uniform(0.8, 2))}" fill="#FFFFFF" opacity="{f(rnd.uniform(0.3, 0.9))}"/>')
    for _ in range(int((S.BX + S.BY) / 3)):
        x, y = S.EXT.uniform(-S.BX, W + S.BX), S.EXT.uniform(-S.BY, 300)
        if 0 <= x <= W and y >= 0:
            continue
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(S.EXT.uniform(0.8, 2))}" fill="#FFFFFF" opacity="{f(S.EXT.uniform(0.3, 0.9))}"/>')
    b.append('<path d="M1530 120 A44 44 0 1 0 1590 182 A36 36 0 1 1 1530 120 Z" fill="#F7E7C6"/>')
    # Alpenglow on the peaks: snow lit rose, rock in violet shadow.
    for px, py, wl, wr, base in [(330, 250, 260, 300, 470), (700, 210, 320, 300, 470), (1060, 150, 380, 420, 480), (1440, 210, 320, 300, 470), (1780, 240, 260, 280, 470)]:
        b.append(S.peak(px, py, wl, wr, base, rnd, "#4A4677", "#363461", "#F4CFC4", "#B99AB6", 0.5))
    if S.BX:
        b.append(S.peak(-60, 280, 280, 300, 470, S.EXT, "#4A4677", "#363461", "#F4CFC4", "#B99AB6", 0.5))
        b.append(S.peak(2120, 260, 300, 300, 470, S.EXT, "#4A4677", "#363461", "#F4CFC4", "#B99AB6", 0.5))
    b.append(S.band(400, 520, "#C7889A", 0, 0.45, 0.0, "dmist", defs))
    b.append(S.hills([(-20, 520), (300, 488), (640, 512), (1000, 486), (1340, 506), (1940, 490)], 0.03, rnd, "#2A2C55"))
    # Village lights on the far slopes, warm and small.
    for _ in range(70):
        x = rnd.choice([rnd.uniform(60, 640), rnd.uniform(1260, 1860)])
        y = 520 + rnd.uniform(-14, 30)
        r = rnd.uniform(1.4, 3)
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 3)}" fill="#FFB65A" opacity="0.15"/><circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="#FFD27F"/>')
    for _ in range(int(S.BX / 8)):
        x = S.EXT.choice([S.EXT.uniform(-S.BX, 40), S.EXT.uniform(1880, W + S.BX)])
        y = 520 + S.EXT.uniform(-14, 30)
        r = S.EXT.uniform(1.4, 3)
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 3)}" fill="#FFB65A" opacity="0.15"/><circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="#FFD27F"/>')
    b.append(S.hills([(-20, 600), (420, 570), (900, 596), (1380, 566), (1940, 590)], 0.02, rnd, "#1E2044"))
    b.append(S.pagoda(1640, 610, 0.42, dict(S.TEMPLE, stone="#26284A", stone_top="#30335A", stone_dark="#1A1C38", wood="#1C1D3A", wood_dark="#15162E",
                                                  trim="#2C2E55", roof="#22244A", roof_dark="#191A38", eave="#101126", gold="#C99A4A", gold_dark="#8A6630", door="#FFC86A")))
    if S.BX:
        b.append(S.flags(S.string((W + S.BX + 20, 380), (1940, 430), 24), ["#3B5C99", "#A9A6B8", "#9A4148", "#3E7A5C", "#B79A4A"], S.EXT, 0.75))
    b.append(S.flags(S.string((1940, 430), (1690, 520), 36), ["#3B5C99", "#A9A6B8", "#9A4148", "#3E7A5C", "#B79A4A"], rnd, 0.75))
    defs.append('<linearGradient id="dground" gradientUnits="userSpaceOnUse" x1="0" y1="634" x2="0" y2="1100"><stop offset="0" stop-color="#181A3A"/><stop offset="1" stop-color="#0B0C1E"/></linearGradient>')
    b.append(S.hills([(-20, 650), (500, 636), (980, 652), (1460, 634), (1940, 648)], 0.015, rnd, "url(#dground)"))
    defs.append('<radialGradient id="dcalm" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#5A5C9A" stop-opacity="0.22"/><stop offset="1" stop-color="#5A5C9A" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="560" rx="760" ry="320" fill="url(#dcalm)"/>')
    return S.svg("".join(b), defs)


if __name__ == "__main__":
    out = sys.argv[1]
    if len(sys.argv) > 3:
        S.set_bleed(float(sys.argv[2]), float(sys.argv[3]))
    os.makedirs(out, exist_ok=True)
    for name, fn in [("green", lambda: dhaka("green")), ("indigo", lambda: dhaka("indigo")), ("dusk", dusk), ("frame", frame)]:
        with open(os.path.join(out, name + ".svg"), "w", encoding="utf-8") as fh:
            fh.write(fn())
        print(name, os.path.getsize(os.path.join(out, name + ".svg")))
