"""The five player portraits for the V1 rebuild, as flat vector art in the kit's style: the same people
as the painted avatars they replace (Art/UI/Avatars), drawn with clean shapes, one shadow tone per
material, thin deep-brown outlines and the dhaka weave as stepped squares.

Usage: python -P avatars.py <work-dir> [--write]
Writes, into the work dir (and with --write over the game's Art/UI/Avatars sprites):
  Avatar_Default.svg/.png, Avatar_01 ... Avatar_04   512 x 512, the portrait in a full circle on cream
  Avatar_Framed_00 ... _04.svg/.png                   384 x 384, the same portrait inside a thin gold ring
                                                     (00 is Default, then 01-04)
- Default: a round, full-faced man with a warm smile, in a crimson and navy dhaka topi worn tilted,
  a black vest open over a cream daura with its crossed collar.
- 01: a woman with a soft oval face, a centre parting and a long braid over her shoulder, a bindi and
  a gold stud, a teal blouse and a red sari pallu.
- 02: a broad, heavy man with a square jaw and a moustache, a round navy knit beanie low on his brow,
  a maroon bakhu wrap with a gold edge.
- 03: a young woman with a heart-shaped face, a black bob swooping from a side parting over one eye,
  a gold hoop, a cream kurta and a teal shawl with a woven band.
- 04: a lean young man with an angular jaw and tousled hair, a thin moustache and goatee, a navy
  jacket with a dhaka band down its opening.
Each head is drawn facing front about x = 256 and then turned a little (Head): its middle slides toward
one side, so the far cheek narrows and the near one widens; the turns alternate across the five. The
light comes from the upper left: each face, neck and garment has one shadow shape on its right and
under what overhangs it. Only gold has a gradient.
"""
import math
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render import render  # noqa: E402

S = 512
CX = 256

# The kit's colours (Figma variables) and the portraits' own.
INK = "#131211"
LINE = "#3E230F"          # outlines
LW = 3.5                  # outline width at 512
FEATURE = "#24140B"       # lids, irises, lash wings
CRIMSON, CRIMSON_2, CRIMSON_3 = "#9A0815", "#741111", "#581113"
IVORY = "#FDFBFA"
GOLD = ("#F7DE8E", "#D9A646", "#9C6A1E")
BACKDROP = "#F6E7CC"
CREAM, CREAM_SHADE = "#F4E9D7", "#DCC7A8"
NAVY, NAVY_SHADE = "#27365C", "#1A2440"
TEAL, TEAL_SHADE = "#1F6E6C", "#154F4E"
HAIR, HAIR_SHEEN = "#1D1714", "#3F342D"
LIPS, LIPS_DARK, LIP_LINE = "#B4523F", "#933629", "#5E1D14"


def f(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


def poly(points):
    return "M" + " L".join(f"{f(x)} {f(y)}" for x, y in points) + " Z"


def spline(points, closed=True, k=1.0):
    """A smooth path through points (Catmull-Rom, as cubic Beziers). A point (x, y, 0) is a sharp corner."""
    n = len(points)
    pts = [(p[0], p[1]) for p in points]

    def tangent(i):
        if len(points[i]) > 2:
            return 0.0, 0.0
        a = pts[(i - 1) % n] if closed else pts[max(i - 1, 0)]
        b = pts[(i + 1) % n] if closed else pts[min(i + 1, n - 1)]
        return (b[0] - a[0]) * k / 6, (b[1] - a[1]) * k / 6

    d = f"M{f(pts[0][0])} {f(pts[0][1])}"
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        (ax, ay), (bx, by) = tangent(i), tangent(j)
        d += (f" C{f(pts[i][0] + ax)} {f(pts[i][1] + ay)} {f(pts[j][0] - bx)} {f(pts[j][1] - by)}"
              f" {f(pts[j][0])} {f(pts[j][1])}")
    return d + (" Z" if closed else "")


def sym(right, cx=CX):
    """A smooth closed shape symmetric about x = cx, from its right side: (dx, y) points from the top
    centre down to the bottom centre (dx, y, 0 for a corner)."""
    pts = [(cx + p[0], p[1]) + tuple(p[2:]) for p in right]
    pts += [(cx - p[0], p[1]) + tuple(p[2:]) for p in reversed(right[1:-1])]
    return spline(pts)


def remap(d, fn):
    """The path d (absolute M/L/C/Q/Z commands) with every point (x, y) moved to fn(x, y)."""
    out, nums = [], []

    def flush():
        for i in range(0, len(nums), 2):
            x, y = fn(nums[i], nums[i + 1])
            out.append(f"{f(x)} {f(y)}")
        nums.clear()

    for tok in d.replace(",", " ").split():
        if tok[0].isalpha():
            assert tok[0] in "MLCQZ", tok
            flush()
            out.append(tok[0])
            tok = tok[1:]
            if not tok:
                continue
        nums.append(float(tok))
    flush()
    d = " ".join(out)
    for c in "MLCQ":
        d = d.replace(c + " ", c)
    return d


def mirror(d, cx=CX):
    return remap(d, lambda x, y: (2 * cx - x, y))


def rotate(d, deg, ox, oy):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return remap(d, lambda x, y: (ox + (x - ox) * c - (y - oy) * s, oy + (x - ox) * s + (y - oy) * c))


def taper(p0, c, p1, t0, t1, below=1.0):
    """A shape along the quadratic curve p0-c-p1, t0 thick at p0 and t1 at p1 (a brow, a lock of hair)."""
    top, bot = [], []
    for k in range(25):
        s = k / 24
        px = (1 - s) ** 2 * p0[0] + 2 * (1 - s) * s * c[0] + s * s * p1[0]
        py = (1 - s) ** 2 * p0[1] + 2 * (1 - s) * s * c[1] + s * s * p1[1]
        tx = 2 * (1 - s) * (c[0] - p0[0]) + 2 * s * (p1[0] - c[0])
        ty = 2 * (1 - s) * (c[1] - p0[1]) + 2 * s * (p1[1] - c[1])
        n = math.hypot(tx, ty) or 1
        nx, ny = -ty / n, tx / n
        if ny > 0:
            nx, ny = -nx, -ny
        t = (t0 + (t1 - t0) * s) / 2
        top.append((px + nx * t, py + ny * t))
        bot.append((px - nx * t * below, py - ny * t * below))
    return poly(top + bot[::-1])


def outline(d, width=LW, colour=LINE):
    return f'<path d="{d}" fill="none" stroke="{colour}" stroke-width="{f(width)}" stroke-linejoin="round" stroke-linecap="round"/>'


def stroke(d, colour, width, opacity=1.0):
    op = f' opacity="{opacity}"' if opacity < 1 else ""
    return f'<path d="{d}" fill="none" stroke="{colour}" stroke-width="{f(width)}" stroke-linecap="round" stroke-linejoin="round"{op}/>'


def fill(d, colour, opacity=1.0):
    op = f' opacity="{opacity}"' if opacity < 1 else ""
    return f'<path d="{d}" fill="{colour}"{op}/>'


def shifted(d, colour, dx, dy):
    return f'<path d="{d}" fill="{colour}" transform="translate({f(dx)} {f(dy)})"/>'


def circle(x, y, r, colour, line=None, width=2.0):
    s = f' stroke="{line}" stroke-width="{f(width)}"' if line else ""
    return f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{colour}"{s}/>'


class Pic:
    """One portrait's defs, with unique ids."""

    def __init__(self, tag):
        self.tag, self.defs, self.n = tag, [], 0

    def id(self, kind):
        self.n += 1
        return f"{self.tag}{kind}{self.n}"

    def clip(self, d):
        cid = self.id("c")
        self.defs.append(f'<clipPath id="{cid}"><path d="{d}"/></clipPath>')
        return cid

    def rim(self, d, colour, dx, dy, opacity=1.0):
        """The part of shape d that d shifted by (dx, dy) leaves bare: a crescent of shadow on its far side."""
        mid = self.id("m")
        self.defs.append(f'<mask id="{mid}" maskUnits="userSpaceOnUse" x="-60" y="-60" width="640" height="640">'
                         f'<rect x="-60" y="-60" width="640" height="640" fill="#fff"/>'
                         f'<path d="{d}" transform="translate({f(dx)} {f(dy)})" fill="#000"/></mask>')
        op = f' opacity="{opacity}"' if opacity < 1 else ""
        return f'<path d="{d}" fill="{colour}" mask="url(#{mid})"{op}/>'

    def part(self, d, colour, shades=(), line=True, width=LW):
        """A filled shape, its shadow shapes clipped to it, and its outline over both."""
        out = [fill(d, colour)]
        if shades:
            out.append(f'<g clip-path="url(#{self.clip(d)})">{"".join(shades)}</g>')
        if line:
            out.append(outline(d, width))
        return "".join(out)

    def gold(self, x1=0, y1=0, x2=1, y2=1):
        gid = self.id("g")
        self.defs.append(f'<linearGradient id="{gid}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">'
                         f'<stop offset="0" stop-color="{GOLD[0]}"/><stop offset="0.5" stop-color="{GOLD[1]}"/>'
                         f'<stop offset="1" stop-color="{GOLD[2]}"/></linearGradient>')
        return f"url(#{gid})"

    def dhaka(self, cell, x0=0.0, y0=0.0, angle=0.0):
        """The stepped-diamond dhaka weave as squares: crimson diamonds, each with a navy cross at its heart,
        in a lattice of stepped cream lines (a 10 x 10 cell tile)."""
        pid, n = self.id("dk"), 10
        cells = []
        for j in range(n):
            for i in range(n):
                ring = min(abs(i - 5) + abs(j - 5), min(i, n - i) + min(j, n - j))
                colour = NAVY if ring <= 1 else CREAM if ring == 5 else None
                if colour:
                    cells.append(f'<rect x="{f(i * cell)}" y="{f(j * cell)}" width="{f(cell + 0.05)}" height="{f(cell + 0.05)}" fill="{colour}"/>')
        size = n * cell
        self.defs.append(f'<pattern id="{pid}" patternUnits="userSpaceOnUse" width="{f(size)}" height="{f(size)}" '
                         f'patternTransform="translate({f(x0)} {f(y0)}) rotate({f(angle)})">'
                         f'<rect width="{f(size)}" height="{f(size)}" fill="{CRIMSON}"/>{"".join(cells)}</pattern>')
        return f"url(#{pid})"


class Head:
    """A head turned a little toward side s (1: the viewer's right, -1: left). Everything on it is drawn
    facing front about x = CX; x() slides each point's x toward s, by shift at the middle and by nothing
    beyond reach either side, so the far half of the face narrows and the near half widens."""

    def __init__(self, s, shift, reach):
        self.s, self.shift, self.reach = s, shift, reach

    def x(self, x):
        v = (x - CX) / self.reach
        return x + self.s * self.shift * (1 - v * v) ** 2 if abs(v) < 1 else x

    def path(self, d):
        return remap(d, lambda x, y: (self.x(x), y))

    def far(self, side):
        return side == self.s


# ---- Faces ---------------------------------------------------------------------------------------

def eye(pic, x, y, w, h, side, iris, look=0.0, liner=False, smile=0.0, crease=None):
    """An almond eye: white, a dark iris tucked under a heavy upper lid, a catchlight, the lid's crease.
    side -1 is the eye on the viewer's left (its outer corner to the left); smile lifts the lower lid."""
    o = side
    ox, oy = x + o * w / 2, y - h * 0.2
    ix, iy = x - o * w / 2, y + h * 0.12
    upper = f"M{f(ix)} {f(iy)} C{f(ix + o * w * 0.16)} {f(y - h * 0.95)} {f(x + o * w * 0.26)} {f(y - h)} {f(ox)} {f(oy)}"
    lower = f" C{f(x + o * w * 0.3)} {f(y + h * (0.62 - smile))} {f(x - o * w * 0.22)} {f(y + h * (0.68 - smile))} {f(ix)} {f(iy)} Z"
    almond = upper + lower
    out = []
    if crease:
        out.append(stroke(f"M{f(ix + o * w * 0.22)} {f(y - h * 0.98)} C{f(ix + o * w * 0.42)} {f(y - h * 1.42)} "
                          f"{f(x + o * w * 0.32)} {f(y - h * 1.38)} {f(ox - o * w * 0.04)} {f(y - h * 0.86)}", crease, 2.6))
    out.append(fill(almond, IVORY))
    cy = y - h * 0.06
    out.append(f'<g clip-path="url(#{pic.clip(almond)})">{circle(x + look, cy, iris, FEATURE)}'
               f'{circle(x + look - iris * 0.34, cy - iris * 0.3, iris * 0.28, IVORY)}</g>')
    out.append(stroke(f"M{f(ox - o * 3)} {f(oy + 3)} Q{f(x + o * w * 0.24)} {f(y + h * (0.6 - smile))} {f(x - o * w * 0.04)} {f(y + h * (0.58 - smile))}",
                      FEATURE, 1.8, 0.45))
    out.append(stroke(upper, FEATURE, 5.6))
    if liner:
        out.append(fill(f"M{f(ox - o * 9)} {f(oy - 3.5)} L{f(ox + o * 10)} {f(oy - 9)} L{f(ox - o * 1)} {f(oy + 2.4)} Z", FEATURE))
    return "".join(out)


def eyes(pic, hd, y, dx, w, h, iris, **kw):
    """Both eyes, placed on the turned head: the far one a little narrower; both glance toward the turn."""
    out = []
    for o in (-1, 1):
        k = 0.88 if hd.far(o) else 1.02
        out.append(eye(pic, hd.x(CX + o * dx), y, w * k, h, o, iris, look=hd.s * 1.5, **kw))
    return "".join(out)


def brows(hd, y_in, y_out, dx_in, dx_out, arch, t0, t1, colour=HAIR, lift=(0, 0)):
    """Two tapered brows, thick at the inner end, arched two thirds of the way out."""
    out = []
    for o, up in ((-1, lift[0]), (1, lift[1])):
        p0, p1 = (CX + o * dx_in, y_in - up), (CX + o * dx_out, y_out - up)
        c = (p0[0] + (p1[0] - p0[0]) * 0.62, min(p0[1], p1[1]) - arch)
        out.append(fill(hd.path(taper(p0, c, p1, t0, t1)), colour))
    return "".join(out)


def nose(hd, tip, w, top, shade):
    """The nose as one shadow: a sliver down its right side into the wing and a crescent under the tip;
    the wings as two short soft lines."""
    x = CX
    side = taper((x + 4, top), (x + w * 0.34, tip - 22), (x + w * 0.42, tip - 4), 2, 8, below=0.2)
    under = (f"M{f(x - w * 0.44)} {f(tip - 2)} C{f(x - w * 0.3)} {f(tip + 9)} {f(x + w * 0.3)} {f(tip + 9)} {f(x + w * 0.46)} {f(tip - 3)}"
             f" C{f(x + w * 0.24)} {f(tip + 2)} {f(x - w * 0.24)} {f(tip + 2)} {f(x - w * 0.44)} {f(tip - 2)} Z")
    wings = "".join(stroke(hd.path(f"M{f(x + o * w * 0.3)} {f(tip - 13)} C{f(x + o * w * 0.52)} {f(tip - 9)} {f(x + o * w * 0.54)} {f(tip + 1)} {f(x + o * w * 0.36)} {f(tip + 3)}"), shade, 3.4)
                    for o in (-1, 1))
    return fill(hd.path(side), shade) + fill(hd.path(under), shade) + wings


def smile(hd, y, w, lift, lip, lift_r=None, width=5.0, dimples=True):
    """A closed-mouth smile: the line between the lips with its corners lifted (lift, or lift and lift_r
    for a half-smile) and the lower lip as a tone under its middle."""
    lr = lift if lift_r is None else lift_r
    l, r = CX - w / 2, CX + w / 2
    mid = y + 0.42 * (lift + lr) / 2
    line = f"M{f(l)} {f(y - lift)} C{f(CX - w * 0.28)} {f(y + lift * 0.9)} {f(CX + w * 0.28)} {f(y + lr * 0.9)} {f(r)} {f(y - lr)}"
    a, b = CX - w * 0.3, CX + w * 0.3
    lower = (f"M{f(a)} {f(mid - 3)} Q{f(CX)} {f(mid + 4)} {f(b)} {f(mid - 3)} Q{f(CX)} {f(mid + 24)} {f(a)} {f(mid - 3)} Z")
    out = [fill(hd.path(lower), lip), stroke(hd.path(line), LIP_LINE, width)]
    if dimples:
        out.append(stroke(hd.path(f"M{f(l - 3)} {f(y - lift - 5)} Q{f(l - 4)} {f(y - lift + 2)} {f(l + 3)} {f(y - lift + 3)}"), LIP_LINE, 3))
        out.append(stroke(hd.path(f"M{f(r + 3)} {f(y - lr - 5)} Q{f(r + 4)} {f(y - lr + 2)} {f(r - 3)} {f(y - lr + 3)}"), LIP_LINE, 3))
    return "".join(out)


def lips(hd, y, w, lift=3.0):
    """Closed lips smiling: a darker upper lip with its bow, a lighter lower lip, the line between."""
    x, l, r, yc = CX, CX - w / 2, CX + w / 2, y - lift
    line = f"M{f(l)} {f(yc)} C{f(x - w * 0.25)} {f(y + 5)} {f(x + w * 0.25)} {f(y + 5)} {f(r)} {f(yc)}"
    upper = line + (f" C{f(x + w * 0.3)} {f(y - 7)} {f(x + w * 0.12)} {f(y - 9)} {f(x)} {f(y - 5)}"
                    f" C{f(x - w * 0.12)} {f(y - 9)} {f(x - w * 0.3)} {f(y - 7)} {f(l)} {f(yc)} Z")
    lower = line + f" C{f(x + w * 0.3)} {f(y + 17)} {f(x - w * 0.3)} {f(y + 17)} {f(l)} {f(yc)} Z"
    return fill(hd.path(lower), LIPS) + fill(hd.path(upper), LIPS_DARK) + stroke(hd.path(line), LIP_LINE, 2.8)


def ear(x, y, h, side, wide=1.0):
    """An ear on the viewer's left (side -1) or right (1), its top at y, tucked behind the face's edge at x:
    its shape and the shadowed curl inside it. A far ear is drawn narrower (wide < 1)."""
    o, w = side, h * 0.42 * wide
    d = (f"M{f(x)} {f(y + 4)} C{f(x + o * w * 0.9)} {f(y - 6)} {f(x + o * w * 1.25)} {f(y + h * 0.25)} {f(x + o * w * 1.05)} {f(y + h * 0.55)}"
         f" C{f(x + o * w * 0.9)} {f(y + h * 0.82)} {f(x + o * w * 0.55)} {f(y + h)} {f(x - o * 2)} {f(y + h * 0.95)} Z")
    curl = (f"M{f(x + o * w * 0.25)} {f(y + h * 0.18)} C{f(x + o * w * 0.75)} {f(y + h * 0.12)} {f(x + o * w * 0.85)} {f(y + h * 0.4)} {f(x + o * w * 0.7)} {f(y + h * 0.62)}"
            f" C{f(x + o * w * 0.6)} {f(y + h * 0.76)} {f(x + o * w * 0.4)} {f(y + h * 0.8)} {f(x + o * w * 0.25)} {f(y + h * 0.74)}"
            f" C{f(x + o * w * 0.45)} {f(y + h * 0.62)} {f(x + o * w * 0.58)} {f(y + h * 0.38)} {f(x + o * w * 0.25)} {f(y + h * 0.18)} Z")
    return d, curl


def ears(hd, u, y, h, sides=(-1, 1)):
    """The ears at u either side of the template's middle: the near one full, the far one narrower."""
    return [ear(hd.x(CX + o * u), y, h, o, 0.6 if hd.far(o) else 1.0) for o in sides]


def head(pic, face, skin, shade, ear_list=(), over=(), rim=(-22, -10)):
    """The ears, then the face with its crescent shadow and the shadows cast on it (over), outlined."""
    out = [pic.part(d, skin, [fill(curl, shade)]) for d, curl in ear_list]
    out.append(pic.part(face, skin, [pic.rim(face, shade, *rim)] + list(over)))
    return "".join(out)


def neck(pic, x0, x1, y0, y1, skin, shade, face, drop=18):
    """The neck, shadowed under the jaw and down its right side."""
    d = f"M{f(x0)} {f(y0)} L{f(x0 - 4)} {f(y1)} L{f(x1 + 4)} {f(y1)} L{f(x1)} {f(y0)} Z"
    return pic.part(d, skin, [shifted(face, shade, 6, drop), f'<rect x="{f(x1 - 14)}" y="{f(y0)}" width="40" height="{f(y1 - y0)}" fill="{shade}"/>'])


def shoulders(y_neck, x_neck, x_out, y_out, notch=None, cx=CX):
    """The bust's silhouette: from the neck at (cx +- x_neck, y_neck) over the shoulders and off the circle,
    with a V-neck down to notch (x, y) when given."""
    d = (f"M{f(cx - x_neck)} {f(y_neck)} C{f(cx - x_neck - 60)} {f(y_neck + 6)} {f(cx - x_out + 30)} {f(y_out - 26)} {f(cx - x_out)} {f(y_out)}"
         f" C{f(cx - x_out - 34)} {f(y_out + 26)} {f(cx - x_out - 54)} {f(y_out + 70)} {f(cx - x_out - 64)} 540"
         f" L{f(cx + x_out + 64)} 540 C{f(cx + x_out + 54)} {f(y_out + 70)} {f(cx + x_out + 34)} {f(y_out + 26)} {f(cx + x_out)} {f(y_out)}"
         f" C{f(cx + x_out - 30)} {f(y_out - 26)} {f(cx + x_neck + 60)} {f(y_neck + 6)} {f(cx + x_neck)} {f(y_neck)}")
    return d + (f" L{f(notch[0])} {f(notch[1])} Z" if notch else " Z")


# ---- The five ------------------------------------------------------------------------------------

def lowered(svg, dy):
    """Clothes drawn for a neck at one height, set dy lower."""
    return f'<g transform="translate(0 {f(dy)})">{svg}</g>'


def default(pic):
    """The round-faced, smiling man in the tilted dhaka topi, a black vest open over a cream daura."""
    skin, shade = "#C98B5C", "#A56B43"
    hd = Head(-1, 22, 160)
    face = hd.path(sym([(0, 88), (76, 94), (114, 126), (126, 174), (128, 222), (120, 264), (98, 300), (58, 322), (0, 330)]))
    cap = rotate(hd.path("M112 136 C120 100 134 66 148 32 C214 22 298 20 366 26 C380 64 392 100 400 136"
                         " C312 152 200 152 112 136 Z"), -4, 256, 140)
    out = []
    # Short hair at the back and sides, under the topi.
    out.append(pic.part(hd.path("M116 132 C108 176 112 216 126 242 L386 242 C400 216 404 176 396 132 Z"), HAIR))
    # Neck, with a soft fold under the chin; the daura with its V and crossed collar; the vest, open over it.
    out.append(neck(pic, 198, 314, 270, 410, skin, shade, face, drop=7))
    out.append(stroke(hd.path("M222 340 Q256 350 290 340"), shade, 3.5))
    clothes = []
    body = shoulders(340, 54, 222, 390, notch=(256, 408))
    clothes.append(pic.part(body, CREAM, [pic.rim(body, CREAM_SHADE, -26, -8)]))
    under = poly([(304, 340), (256, 408), (274, 418), (330, 346)])
    over = poly([(208, 340), (300, 480), (278, 492), (182, 346)])
    clothes.append(pic.part(under, CREAM, [shifted(over, CREAM_SHADE, 8, 6)]))
    clothes.append(pic.part(over, CREAM))
    vest_l = "M198 344 C144 348 86 358 50 384 C16 410 -4 460 -14 540 L236 540 C228 490 216 420 214 366 Z"
    vest_r = mirror(vest_l)
    vest, vest_shade = "#2B2523", "#171312"
    clothes.append(pic.part(vest_l, vest, [pic.rim(vest_l, vest_shade, -16, -4)]))
    clothes.append(pic.part(vest_r, vest, [pic.rim(vest_r, vest_shade, -22, -4)]))
    out.append(lowered("".join(clothes), 12))
    # Head.
    out.append(head(pic, face, skin, shade, ears(hd, 122, 162, 76), [shifted(cap, shade, 4, 13)]))
    burn = "M118 134 C114 164 118 192 128 212 C134 190 142 164 158 138 Z"
    out.append(pic.part(hd.path(burn), HAIR) + pic.part(hd.path(mirror(burn)), HAIR))
    # A warm, wide smile.
    out.append(brows(hd, 168, 162, 22, 84, 8, 15, 6))
    out.append(eyes(pic, hd, 196, 48, 50, 17, 11, smile=0.32, crease=shade))
    out.append(nose(hd, 242, 46, 206, shade))
    out.append(smile(hd, 278, 86, 10, "#B0614A"))
    # The dhaka topi, tilted, its right side in shade.
    out.append(pic.part(cap, pic.dhaka(7, 120, 22, -4), [pic.rim(cap, INK, -36, 0, 0.3)]))
    return "".join(out)


def woman_braid(pic):
    """The woman with the centre parting and long braid, bindi and gold stud, teal blouse and red pallu."""
    skin, shade = "#CF9466", "#AD7149"
    hd = Head(1, 20, 138)
    face = hd.path(sym([(0, 86), (66, 92), (100, 124), (110, 170), (106, 216), (94, 258), (70, 294), (36, 316), (0, 324)]))
    out = []
    back = hd.path(spline([(142, 280, 0), (122, 220), (122, 150), (142, 88), (192, 48), (256, 36), (320, 48), (370, 88),
                           (390, 150), (388, 222), (374, 270), (350, 296, 0), (256, 300)]))
    out.append(pic.part(back, HAIR))
    out.append(neck(pic, 222, 290, 270, 410, skin, shade, face))
    clothes = []
    body = shoulders(344, 46, 216, 392)
    clothes.append(pic.part(body, TEAL, [pic.rim(body, TEAL_SHADE, -24, -6)]))
    # Her neckline, then the pallu over her left shoulder (the viewer's right) with its border.
    clothes.append(pic.part("M212 344 C230 372 282 372 300 344 Z", skin, [shifted(face, shade, 6, 6)]))
    pallu = "M312 342 C374 346 438 360 472 386 C508 414 522 470 526 540 L168 540 C208 480 262 402 312 342 Z"
    border = "M312 342 C262 402 208 480 168 540 L210 540 C246 486 294 416 336 356 Z"
    clothes.append(pic.part(pallu, CRIMSON, [pic.rim(pallu, CRIMSON_3, -30, -10), fill(border, TEAL),
                                             stroke("M324 349 C278 409 228 484 190 540", GOLD[1], 8), outline(border, 2.5),
                                             stroke("M400 384 C370 430 348 490 336 540", CRIMSON_3, 7),
                                             stroke("M450 400 C428 450 418 500 414 540", CRIMSON_3, 6)]))
    out.append(lowered("".join(clothes), 6))
    # The braid over her right shoulder (the viewer's left), in front of the blouse.
    for k in range(8):
        y = 264 + k * 34
        x = 148 + k * 6 + k * k * 0.4
        w = 48 - k * 1.8
        lean = 1 if k % 2 == 0 else -1
        lobe = spline([(x - w / 2, y + 2), (x - w * 0.36, y + 28), (x + lean * w * 0.12, y + 46, 0), (x + w * 0.46, y + 24),
                       (x + w * 0.34, y - 2), (x + lean * w * 0.1, y - 6)])
        out.append(pic.part(lobe, HAIR, [fill(taper((x - w * 0.26, y + 4), (x - w * 0.02 * lean, y + 18), (x + w * 0.18, y + 32), 7, 2), HAIR_SHEEN)]))
    # Head: her right ear (the viewer's left, the near one) under her hair, with a gold stud.
    front = hd.path(spline([(150, 212, 0), (156, 160), (180, 114), (218, 90), (256, 84, 0), (294, 90), (332, 114), (356, 160),
                            (362, 212, 0), (378, 170), (380, 120), (358, 74), (310, 46), (256, 40), (202, 46), (154, 74),
                            (132, 120), (134, 170)]))
    out.append(head(pic, face, skin, shade, ears(hd, 106, 172, 64, sides=(-1,)), [shifted(front, shade, 5, 11)]))
    out.append(circle(hd.x(CX - 106) - 12, 232, 6.5, pic.gold(), LINE))
    # A calm smile.
    out.append(brows(hd, 160, 154, 20, 76, 9, 10, 3.5))
    out.append(eyes(pic, hd, 190, 44, 48, 17, 11, liner=True, smile=0.22, crease=shade))
    out.append(circle(hd.x(CX), 156, 6, CRIMSON))
    out.append(nose(hd, 236, 32, 204, shade))
    out.append(lips(hd, 270, 58, 5))
    out.append(pic.part(front, HAIR, [fill(hd.path(taper((248, 50), (188, 66), (152, 150), 9, 2)), HAIR_SHEEN),
                                      fill(hd.path(taper((264, 50), (326, 66), (362, 150), 9, 2)), HAIR_SHEEN),
                                      stroke(hd.path("M256 84 L256 42"), HAIR_SHEEN, 3)]))
    return "".join(out)


def man_beanie(pic):
    """The broad, heavy man with the moustache, the round navy beanie low on his brow, the maroon wrap."""
    skin, shade = "#B97A4C", "#965D37"
    hd = Head(-1, 18, 168)
    face = hd.path(sym([(0, 100), (88, 104), (124, 132), (132, 180), (134, 228), (130, 270), (116, 306), (72, 328), (0, 334)]))
    dome = hd.path("M108 144 C100 78 168 32 256 30 C344 32 412 78 404 144 Z")
    cuff = hd.path("M104 110 C190 100 322 100 408 110 C412 126 412 142 408 158 C322 168 190 168 104 158 C100 142 100 126 104 110 Z")
    out = []
    out.append(neck(pic, 186, 326, 270, 410, skin, shade, face, drop=12))
    clothes = []
    maroon, maroon_shade = CRIMSON_2, CRIMSON_3
    body = shoulders(338, 70, 232, 380, notch=(262, 424))
    clothes.append(pic.part(body, maroon, [pic.rim(body, maroon_shade, -30, -8)]))
    clothes.append(stroke("M190 338 L258 420", pic.gold(0, 0, 1, 0), 12) + stroke("M198 334 L264 414", LINE, 2.5))
    wrap = "M322 338 C336 386 266 440 160 540 L540 540 C536 470 520 420 484 396 C448 372 384 344 322 338 Z"
    clothes.append(pic.part(wrap, maroon, [pic.rim(wrap, maroon_shade, -30, -10), stroke("M420 392 C390 440 362 490 350 540", maroon_shade, 8)]))
    clothes.append(stroke("M322 338 C336 386 266 440 160 540", pic.gold(0, 0, 1, 0), 13) + stroke("M329 340 C344 390 274 446 170 546", LINE, 2.5))
    out.append(lowered("".join(clothes), 10))
    # Head.
    out.append(head(pic, face, skin, shade, ears(hd, 128, 170, 80), [shifted(cuff, shade, 4, 12)]))
    side = "M120 156 C116 182 118 204 124 222 C130 202 138 180 150 160 Z"
    out.append(pic.part(hd.path(side), HAIR) + pic.part(hd.path(mirror(side)), HAIR))
    # Heavy brows, a full moustache over a steady half-smile.
    out.append(brows(hd, 178, 174, 22, 86, 5, 17, 9))
    out.append(eyes(pic, hd, 204, 50, 46, 15, 10.5, crease=shade))
    out.append(nose(hd, 252, 54, 216, shade))
    out.append(stroke(hd.path("M208 250 Q194 272 202 296"), shade, 4) + stroke(hd.path("M304 250 Q318 272 310 296"), shade, 4))
    out.append(smile(hd, 292, 56, 1, "#9C5A40", lift_r=6, width=4.5, dimples=False))
    out.append(fill(hd.path("M256 260 C278 256 302 260 318 278 C304 275 290 276 274 274 C266 273 260 272 256 274"
                            " C252 272 246 273 238 274 C222 276 208 275 194 278 C210 260 234 256 256 260 Z"), HAIR))
    ribs = "".join(stroke(hd.path(f"M{f(256 + k * 24)} 108 C{f(256 + k * 23)} 72 {f(256 + k * 14)} 42 {f(256 + k * 4)} 32"), NAVY_SHADE, 3.5)
                   for k in range(-6, 7))
    out.append(pic.part(dome, NAVY, [ribs, pic.rim(dome, NAVY_SHADE, -28, 0)]))
    cuff_ribs = "".join(stroke(hd.path(f"M{f(x)} 98 L{f(x)} 170"), NAVY_SHADE, 3.5) for x in range(110, 410, 13))
    out.append(pic.part(cuff, NAVY, [cuff_ribs, pic.rim(cuff, NAVY_SHADE, -28, 0)]))
    return "".join(out)


def woman_bob(pic):
    """The young woman with the bob swooping over one eye, the gold hoop, cream kurta and teal shawl."""
    skin, shade = "#C58658", "#A2663E"
    hd = Head(1, 20, 140)
    face = hd.path(sym([(0, 88), (68, 94), (104, 126), (112, 170), (106, 214), (90, 254), (64, 290), (30, 312), (0, 320)]))
    out = []
    back = hd.path(spline([(128, 300, 0), (110, 230), (112, 150), (140, 86), (194, 46), (262, 36), (330, 50), (382, 96),
                           (404, 170), (406, 250), (396, 306, 0), (300, 318), (200, 314)]))
    out.append(pic.part(back, HAIR))
    out.append(neck(pic, 224, 290, 270, 410, skin, shade, face))
    clothes = []
    body = shoulders(344, 40, 214, 394)
    clothes.append(pic.part(body, CREAM, [pic.rim(body, CREAM_SHADE, -24, -6)]))
    # Stand collar and placket with small embroidered diamonds.
    collar = "M218 340 C238 350 274 350 294 340 L296 360 C274 370 238 370 216 360 Z"
    clothes.append(pic.part(collar, CREAM, [shifted(collar, CREAM_SHADE, -12, 0)]))
    clothes.append(stroke("M256 366 L256 440", LINE, 2.5))
    for k in range(5):
        y = 376 + k * 14
        for x in (245, 267):
            clothes.append(fill(f"M{x} {y - 4.5} L{x + 4.5} {y} L{x} {y + 4.5} L{x - 4.5} {y} Z", GOLD[2]))
    # The teal shawl over both shoulders, the red woven band on her left (the viewer's right).
    shawl_l = "M212 348 C156 352 92 364 56 390 C20 416 2 466 -8 540 L192 540 C186 480 190 410 212 348 Z"
    shawl_r = "M300 348 C356 352 420 364 456 390 C492 416 510 466 520 540 L330 540 C326 480 318 410 300 348 Z"
    clothes.append(pic.part(shawl_l, TEAL, [pic.rim(shawl_l, TEAL_SHADE, -22, -6), stroke("M140 392 C140 440 150 490 158 540", TEAL_SHADE, 7)]))
    band = "M352 352 C374 360 396 370 410 378 C418 430 424 490 426 540 L380 540 C378 486 370 420 352 352 Z"
    clothes.append(pic.part(shawl_r, TEAL, [pic.rim(shawl_r, TEAL_SHADE, -26, -6), fill(band, pic.dhaka(4.4, 354, 352)), outline(band, 2.5)]))
    out.append(lowered("".join(clothes), 4))
    # Head: her right ear (the viewer's left, the near one), tucked, with the hoop.
    front = hd.path(spline([(202, 72, 0), (222, 100), (254, 122), (290, 140), (318, 158), (338, 180), (352, 212), (358, 256),
                            (364, 298), (376, 318, 0), (402, 308, 0), (410, 246), (406, 168), (384, 96), (330, 46), (262, 34),
                            (200, 46), (154, 78), (128, 128), (122, 184), (138, 222, 0), (144, 176), (156, 128), (178, 94)]))
    out.append(head(pic, face, skin, shade, ears(hd, 108, 172, 62, sides=(-1,)), [shifted(front, shade, 5, 9)], rim=(-20, -10)))
    out.append(f'<circle cx="{f(hd.x(CX - 108) - 10)}" cy="244" r="10" fill="none" stroke="{pic.gold()}" stroke-width="4.5"/>')
    # A bright smile; then the swoop of hair over her left eye (the far one).
    out.append(brows(hd, 156, 150, 20, 72, 10, 10, 4))
    out.append(eyes(pic, hd, 186, 44, 48, 17, 11, liner=True, smile=0.26, crease=shade))
    out.append(nose(hd, 230, 32, 198, shade))
    out.append(lips(hd, 264, 60, 7))
    out.append(pic.part(front, HAIR, [fill(hd.path(taper((210, 52), (340, 56), (384, 200), 11, 3)), HAIR_SHEEN),
                                      fill(hd.path(taper((222, 84), (318, 92), (364, 186), 6, 2)), HAIR_SHEEN),
                                      fill(hd.path(taper((194, 58), (150, 86), (140, 170), 7, 2)), HAIR_SHEEN)]))
    return "".join(out)


def man_swept(pic):
    """The lean young man with tousled hair, thin moustache and goatee, navy jacket with a dhaka band."""
    skin, shade = "#C88A5A", "#A46A41"
    hd = Head(-1, 20, 128)
    face = hd.path(sym([(0, 88), (60, 94), (92, 124), (100, 170), (96, 214), (90, 252), (76, 288, 0), (42, 318), (0, 332)]))
    out = []
    out.append(neck(pic, 226, 288, 270, 410, skin, shade, face))
    clothes = []
    body = shoulders(344, 42, 214, 394)
    clothes.append(pic.part(body, CREAM, [pic.rim(body, CREAM_SHADE, -20, -6)]))
    # Shirt collar and button.
    shirt_collar = "M222 336 C240 346 272 346 290 336 L294 362 L256 374 L218 362 Z"
    clothes.append(pic.part(shirt_collar, CREAM, [shifted(shirt_collar, CREAM_SHADE, -12, 0)]))
    clothes.append(stroke("M256 374 L256 540", LINE, 2.5))
    clothes.append(circle(262, 398, 4.5, CREAM_SHADE, LINE))
    jacket_l = "M198 342 C150 348 92 360 56 386 C20 412 2 462 -8 540 L236 540 C234 500 232 460 230 420 L206 366 Z"
    jacket_r = mirror(jacket_l)
    clothes.append(pic.part(jacket_l, NAVY, [pic.rim(jacket_l, NAVY_SHADE, -16, -4)]))
    clothes.append(pic.part(jacket_r, NAVY, [pic.rim(jacket_r, NAVY_SHADE, -22, -4)]))
    # The dhaka band standing round the neck and down the jacket's opening.
    band_l = spline([(186, 326, 0), (214, 314, 0), (228, 352), (240, 420, 0), (212, 428, 0), (198, 368)])
    weave = pic.dhaka(3.8, 180, 300)
    clothes.append(pic.part(band_l, weave) + pic.part(mirror(band_l), weave, [pic.rim(mirror(band_l), INK, -10, 0, 0.3)]))
    out.append(lowered("".join(clothes), 8))
    # Head; tousled hair in pointed locks over the forehead and the sides.
    hair = hd.path(spline([(150, 212, 0), (138, 178), (132, 140), (138, 104), (156, 74), (182, 52), (178, 32, 0), (210, 40),
                           (240, 28), (268, 36, 0), (296, 26), (330, 34), (352, 50), (376, 46, 0), (380, 74), (390, 104),
                           (386, 140), (374, 176), (366, 214, 0), (354, 186), (340, 154),
                           (326, 172, 0), (314, 136), (290, 130), (272, 160, 0), (258, 126), (234, 126), (214, 154, 0),
                           (204, 126), (184, 136), (168, 162), (160, 190)]))
    out.append(head(pic, face, skin, shade, ears(hd, 94, 168, 62), [shifted(hair, shade, 5, 12)], rim=(-16, -9)))
    # A confident half-smile under a thin moustache; a goatee on the chin.
    out.append(brows(hd, 162, 156, 18, 74, 9, 14, 5, lift=(0, 5)))
    out.append(eyes(pic, hd, 192, 42, 46, 14, 10.5, crease=shade))
    out.append(nose(hd, 238, 34, 204, shade))
    out.append(fill(hd.path(taper((252, 254), (234, 249), (216, 260), 5.5, 1.5)), HAIR) + fill(hd.path(taper((260, 254), (278, 249), (296, 260), 5.5, 1.5)), HAIR))
    out.append(smile(hd, 270, 52, 1, "#A9654B", lift_r=7, width=4, dimples=False))
    out.append(fill(hd.path("M236 298 C244 292 268 292 276 298 C280 316 270 334 256 340 C242 334 232 316 236 298 Z"), HAIR))
    out.append(fill(hd.path("M249 282 L263 282 L261 294 L251 294 Z"), HAIR))
    out.append(pic.part(hair, HAIR, [fill(hd.path(taper((152, 176), (152, 92), (212, 48), 9, 2)), HAIR_SHEEN),
                                     fill(hd.path(taper((192, 128), (222, 70), (300, 40), 8, 2)), HAIR_SHEEN),
                                     fill(hd.path(taper((246, 120), (300, 74), (370, 80), 8, 2)), HAIR_SHEEN),
                                     fill(hd.path(taper((300, 132), (348, 112), (388, 150), 6, 2)), HAIR_SHEEN)]))
    return "".join(out)


PEOPLE = [default, woman_braid, man_beanie, woman_bob, man_swept]
NAMES = ["Default", "01", "02", "03", "04"]


def _inner(index):
    """The portrait's defs and body in 512 space, clipped to the circle (r 256) over its backdrop."""
    pic = Pic(f"p{index}")
    body = PEOPLE[index](pic)
    pic.defs.append(f'<clipPath id="{pic.tag}disc"><circle cx="256" cy="256" r="256"/></clipPath>')
    return pic.defs, f'<g clip-path="url(#{pic.tag}disc)"><rect width="512" height="512" fill="{BACKDROP}"/>{body}</g>'


def portrait(index):
    """The portrait (0 Default, 1-4 the others) as a 512 x 512 SVG."""
    defs, body = _inner(index)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{S}" height="{S}" viewBox="0 0 {S} {S}">'
            f'<defs>{"".join(defs)}</defs>{body}</svg>')


def framed(index, size=384, ring=6.0):
    """The portrait inside a thin gold ring (a gold gradient between two fine deep-brown lines), 384 x 384."""
    defs, body = _inner(index)
    half = size / 2
    outer = half - 0.75
    inner = outer - ring
    scale = (inner - 1.8) / 256
    gid = f"p{index}ring"
    defs.append(f'<linearGradient id="{gid}" x1="0" y1="0" x2="1" y2="1">'
                f'<stop offset="0" stop-color="{GOLD[0]}"/><stop offset="0.45" stop-color="{GOLD[1]}"/>'
                f'<stop offset="1" stop-color="{GOLD[2]}"/></linearGradient>')
    mid = outer - ring / 2
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}">'
            f'<defs>{"".join(defs)}</defs>'
            f'<g transform="translate({f(half)} {f(half)}) scale({scale:.5f}) translate(-256 -256)">{body}</g>'
            f'<circle cx="{f(half)}" cy="{f(half)}" r="{f(inner - 1.2)}" fill="none" stroke="{IVORY}" stroke-width="1.4" opacity="0.8"/>'
            f'<circle cx="{f(half)}" cy="{f(half)}" r="{f(mid)}" fill="none" stroke="url(#{gid})" stroke-width="{f(ring)}"/>'
            f'<circle cx="{f(half)}" cy="{f(half)}" r="{f(outer)}" fill="none" stroke="{LINE}" stroke-width="1.2"/>'
            f'<circle cx="{f(half)}" cy="{f(half)}" r="{f(inner)}" fill="none" stroke="{LINE}" stroke-width="1.2"/></svg>')


def without_patterns(svg):
    """The same picture with each dhaka pattern fill drawn out as its squares, clipped to the shape it
    fills: Figma's SVG import drops pattern fills (for the "V1 Rebuild · Icons & Avatars" page)."""
    patterns = {}
    for m in re.finditer(r'<pattern id="([^"]+)" patternUnits="userSpaceOnUse" width="([\d.]+)" height="[\d.]+" '
                         r'patternTransform="([^"]+)">(.*?)</pattern>', svg, re.S):
        patterns[m.group(1)] = (float(m.group(2)), m.group(3), m.group(4))
    svg = re.sub(r'<pattern id="[^"]+".*?</pattern>', "", svg, flags=re.S)
    extra, k = [], 0

    def expand(m):
        nonlocal k
        d, pid = m.group(1), m.group(2)
        size, transform, tile = patterns[pid]
        xs = [float(v) for v in re.findall(r"-?\d+(?:\.\d+)?", d)]
        span = max(abs(v) for v in xs) + 2 * size
        n = int(span / size) + 2
        k += 1
        extra.append(f'<clipPath id="xp{k}"><path d="{d}"/></clipPath>')
        tiles = "".join(f'<g transform="translate({f(i * size)} {f(j * size)})">{tile}</g>' for i in range(-n, n) for j in range(-n, n)
                        if -size * 1.5 < i * size < span and -size * 1.5 < j * size < span)
        return f'<g clip-path="url(#xp{k})"><g transform="{transform}">{tiles}</g></g>'

    svg = re.sub(r'<path d="([^"]+)" fill="url\(#([^)]*dk\d+)\)"/>', expand, svg)
    return svg.replace("</defs>", "".join(extra) + "</defs>", 1)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0]
    os.makedirs(out, exist_ok=True)
    jobs = [(f"Avatar_{name}", portrait(i), 512) for i, name in enumerate(NAMES)]
    jobs += [(f"Avatar_Framed_{i:02d}", framed(i), 384) for i in range(len(NAMES))]
    for name, svg, px in jobs:
        path = os.path.join(out, name + ".svg")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(svg)
        render(path, os.path.join(out, name + ".png"), px, px, transparent=True)
        os.makedirs(os.path.join(out, "figma"), exist_ok=True)
        with open(os.path.join(out, "figma", name + ".svg"), "w", encoding="utf-8") as fh:
            fh.write(without_patterns(svg))
        print("wrote", name, px)
    # --write puts them over the game's sprites, which keep their names, sizes, GUIDs and settings.
    if "--write" in sys.argv:
        avatars = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "_Project", "Art", "UI", "Avatars"))
        for name, _, _ in jobs:
            shutil.copyfile(os.path.join(out, name + ".png"), os.path.join(avatars, name + ".png"))
            print("wrote", os.path.join("Assets", "_Project", "Art", "UI", "Avatars", name + ".png"))
