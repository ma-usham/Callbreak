"""The shop's themes (the owner, 30 Oct 2026): each a menu background and a matching cloth with its border,
as vector art in the menu backdrop's style (scenes.py). Figma: page "V1 Rebuild · Themes" (268:2).

Usage: python -P themes.py <out-dir> [bleed-x bleed-y]
Writes, for each theme (THEMES): bg_<key>.svg and cloth_<key>.svg, 1920 x 1080 plus the bleed on each side
when given (scenes.set_bleed), and frame_<key>.svg, the cloth's border (384 x 384, as cloths.frame).

Backgrounds keep the menu readable: the middle calm (logo, card fan, Play Online, the two ivory buttons,
the tagline), the top corners quiet (profile, wallet, round buttons), and nothing that matters in the
bottom-left corner, where the banner sits (ADR 052). The subject stands at the sides. Whatever fills the
bleed draws on scenes.EXT, so the 1920 x 1080 picture inside stays as Figma shows it; without bleed each
background is Figma's, except Mithila Garden, which lost its painted border (drawn round the 1920 x 1080
screen, it would show as a band inside the picture on 21:9 and 4:3).

A cloth is two pictures, as the dhaka cloths are: the base (the radial colour, a faint twill, a warm light,
the vignette round the 1920 x 1080 picture, darker into the bleed; a plain centre) and its border, which the
game lays round the screen. The border is a 384-unit square: its 168-unit corners stay put and the 48 units
between them repeat along each edge (Image.Type.Tiled, cloths.FRAME_BORDER). So every edge pattern repeats
every 48 units, and it is also whole at every multiple of 24 from the corners: Unity tiles an edge from its
left (or bottom) corner and cuts the last tile short, and the screen's sides (1080 - 2 x 168 = 31 x 24) end
on half a tile on every phone of 16:9 or wider. Motifs therefore sit inside 24-unit cells and the lines
between them read the same at each cell's ends, so that half tile only shifts the rhythm by a step.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cloths  # noqa: E402
import scenes as S  # noqa: E402
import shop_scenes as P  # noqa: E402

W, H, f, poly = S.W, S.H, S.f, S.poly

# The bounding box of S.full without bleed: the skies' gradients keep it when the picture grows.
FULL = (-2, -2, W + 4, H + 4)


# ---------------------------------------------------------------- helpers

def bleed():
    return bool(S.BX or S.BY)


def lg(gid, stops, x2=0, y2=1, box=None):
    """A linear gradient over its shape's box; with bleed, over box (the shape's box without bleed), so a shape
    that grows into the margin keeps its colours where they were and runs on in its end colours."""
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    if box and bleed():
        x, y, w, h = box
        return (f'<linearGradient id="{gid}" gradientUnits="userSpaceOnUse" x1="{f(x)}" y1="{f(y)}" '
                f'x2="{f(x + w * x2)}" y2="{f(y + h * y2)}">{s}</linearGradient>')
    return f'<linearGradient id="{gid}" x1="0" y1="0" x2="{x2}" y2="{y2}">{s}</linearGradient>'


def rg(gid, stops, cx=0.5, cy=0.5, r=0.5):
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return f'<radialGradient id="{gid}" cx="{cx}" cy="{cy}" r="{r}">{s}</radialGradient>'


def glow(defs, gid, col, a, cx, cy, rx, ry):
    defs.append(rg(gid, [(0, col, a), (1, col, 0)]))
    return f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(rx)}" ry="{f(ry)}" fill="url(#{gid})"/>'


def smooth_d(pts):
    """A Catmull-Rom curve through pts, as cubic Beziers (open)."""
    d = f"M{f(pts[0][0])} {f(pts[0][1])}"
    for i in range(len(pts) - 1):
        p0 = pts[i - 1] if i > 0 else pts[i]
        p1, p2 = pts[i], pts[i + 1]
        p3 = pts[i + 2] if i + 2 < len(pts) else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        d += f" C{f(c1[0])} {f(c1[1])} {f(c2[0])} {f(c2[1])} {f(p2[0])} {f(p2[1])}"
    return d


def run_on(p, q):
    """How a line that ends at p (coming from q) runs on over the bleed, as a cubic (c1, c2, end) leaving p the
    way the line does: at a side edge it eases out level beyond the margin, at the foot it bends down off the
    frame. None when p ends inside the picture or there is no bleed."""
    if not bleed():
        return None
    dx, dy = p[0] - q[0], p[1] - q[1]
    n = math.hypot(dx, dy) or 1
    dx, dy = dx / n, dy / n
    if p[0] <= 0 or p[0] >= W:
        side = -1 if p[0] <= 0 else 1
        ex = -S.BX - 40 if side < 0 else W + S.BX + 40
        span = abs(ex - p[0])
        if dx * side < 0.3:
            dx, dy = float(side), 0.0
        s = dy / abs(dx)
        ey = p[1] + max(-0.3, min(0.3, s)) * span * 0.4
        k = min(span / 3 / abs(dx), span / 2)
        return (p[0] + dx * k, p[1] + dy * k), (ex - side * span / 3, ey), (ex, ey)
    if p[1] >= H:
        ey = H + S.BY + 20
        if ey <= p[1] + 1:
            return None
        if dy < 0.25:
            dx, dy = 0.0, 1.0
        span = ey - p[1]
        ex = p[0] + dx / dy * span * 0.6
        return (p[0] + dx / dy * span / 3, p[1] + span / 3), (ex, ey - span / 3), (ex, ey)
    return None


def _bez(p0, c1, c2, p1, n=16):
    out = []
    for i in range(1, n + 1):
        t = i / n
        a, b, c, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        out.append((a * p0[0] + b * c1[0] + c * c2[0] + d * p1[0], a * p0[1] + b * c1[1] + c * c2[1] + d * p1[1]))
    return out


def ext_line(pts):
    """pts with their run-ons over the bleed (run_on), as points, for along()."""
    out = list(pts)
    a, z = run_on(pts[0], pts[1]), run_on(pts[-1], pts[-2])
    if a:
        out = _bez(pts[0], *a)[::-1] + out
    if z:
        out = out + _bez(pts[-1], *z)
    return out


def run_on_d(pts, d, a=None, z=None):
    """The path d through pts (it starts at pts[0] and ends at pts[-1]) with the run-ons over the bleed added at
    each end that has one; a and z are the ends' outer neighbours (the tangents), by default pts[1] and pts[-2].
    Returns (d, first point, last point)."""
    a = run_on(pts[0], a or pts[1])
    z = run_on(pts[-1], z or pts[-2])
    head = f"M{f(pts[0][0])} {f(pts[0][1])}"
    if a:
        c1, c2, e = a
        d = f"M{f(e[0])} {f(e[1])} C{f(c2[0])} {f(c2[1])} {f(c1[0])} {f(c1[1])} {f(pts[0][0])} {f(pts[0][1])}" + d[len(head):]
    if z:
        c1, c2, e = z
        d += f" C{f(c1[0])} {f(c1[1])} {f(c2[0])} {f(c2[1])} {f(e[0])} {f(e[1])}"
    return d, (a[2] if a else pts[0]), (z[2] if z else pts[-1])


def hill(pts, col, bottom=H + 20, extra=""):
    """A smooth hill through pts, filled down to the bottom; with bleed its ends run on over the margin."""
    if not bleed():
        return f'<path d="{smooth_d(pts)} L{f(pts[-1][0])} {f(bottom)} L{f(pts[0][0])} {f(bottom)} Z" fill="{col}"{extra}/>'
    if bottom >= H:
        bottom += S.BY
    d, first, last = run_on_d(pts, smooth_d(pts))
    return f'<path d="{d} L{f(last[0])} {f(bottom)} L{f(first[0])} {f(bottom)} Z" fill="{col}"{extra}/>'


def smooth_closed(pts):
    """A smooth closed shape through pts (smooth_d ... Z); with bleed, ends at the picture's foot run on off it."""
    d, _, _ = run_on_d(pts, smooth_d(pts))
    return d + " Z"


def wave(x0, x1, y, amp, n, rnd, phase=0.0):
    return [(x0 + (x1 - x0) * i / n, y + amp * math.sin(i * 1.3 + phase) + rnd.uniform(-amp, amp) * 0.4) for i in range(n + 1)]


def bleed_points(n, x0, x1, y0, y1, m=3):
    """Points for the bleed at the density of n points over the box x0..x1, y0..y1: the box grows over the margin
    on each side that lies at the picture's edge, and its own points (the scene's numbers) are left out, as are
    points within m of the picture, so that what is drawn there stays out of it. EXT."""
    if not bleed():
        return []
    X0, X1 = (x0 - S.BX if x0 <= 0 else x0), (x1 + S.BX if x1 >= W else x1)
    Y0, Y1 = (y0 - S.BY if y0 <= 0 else y0), (y1 + S.BY if y1 >= H else y1)
    count = int(n * (X1 - X0) * (Y1 - Y0) / ((x1 - x0) * (y1 - y0)))
    out = []
    for _ in range(count):
        x, y = S.EXT.uniform(X0, X1), S.EXT.uniform(Y0, Y1)
        if not (x0 <= x <= x1 and y0 <= y <= y1) and not (-m < x < W + m and -m < y < H + m):
            out.append((x, y))
    return out


def stars(rnd, n, x0, x1, y0, y1, avoid=None, col="#FFFFFF"):
    out = []
    for _ in range(n):
        x, y = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        if avoid and ((x - avoid[0]) / avoid[2]) ** 2 + ((y - avoid[1]) / avoid[3]) ** 2 < 1:
            continue
        r = rnd.choice([0.8, 1.0, 1.2, 1.5, 1.8, 2.2])
        out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{col}" opacity="{f(rnd.uniform(0.35, 0.95))}"/>')
    return "".join(out)


def bleed_stars(n, x0, x1, y0, y1, col="#FFFFFF"):
    """stars() over the bleed, at the same density."""
    out = []
    for x, y in bleed_points(n, x0, x1, y0, y1):
        r = S.EXT.choice([0.8, 1.0, 1.2, 1.5, 1.8, 2.2])
        out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{col}" opacity="{f(S.EXT.uniform(0.35, 0.95))}"/>')
    return "".join(out)


def string_on(a, b, sag, cx, sag2):
    """S.string(a, b, sag) continued from b to x = cx, leaving b at the same slope (no kink)."""
    slope = ((b[1] - a[1]) - 4 * sag) / (b[0] - a[0])
    return S.string(b, (cx, b[1] + slope * (cx - b[0]) - 4 * sag2), sag2)


def sparkle(x, y, r, col, op=1.0):
    k = r * 0.22
    return f'<path d="M{f(x)} {f(y - r)} L{f(x + k)} {f(y - k)} L{f(x + r)} {f(y)} L{f(x + k)} {f(y + k)} L{f(x)} {f(y + r)} L{f(x - k)} {f(y + k)} L{f(x - r)} {f(y)} L{f(x - k)} {f(y - k)} Z" fill="{col}" opacity="{f(op)}"/>'


def flower5(x, y, r, petal, centre, ang=0.0, op=1.0):
    """A flat five-petal flower (rhododendron / generic), one path for the petals."""
    d = ""
    for k in range(5):
        a = math.radians(ang + k * 72)
        px, py = x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55
        d += f"M{f(px + r * 0.45)} {f(py)} A{f(r * 0.45)} {f(r * 0.45)} 0 1 0 {f(px - r * 0.45)} {f(py)} A{f(r * 0.45)} {f(r * 0.45)} 0 1 0 {f(px + r * 0.45)} {f(py)} Z"
    return (f'<g opacity="{f(op)}"><path d="{d}" fill="{petal}"/>'
            f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 0.28)}" fill="{centre}"/></g>')


def leaf_d(x, y, L, ang, w=0.3):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)

    def p(u, v):
        return f"{f(x + u * ca - v * sa)} {f(y + u * sa + v * ca)}"
    ww = L * w
    return f"M{p(0, 0)} C{p(L * 0.3, -ww)} {p(L * 0.75, -ww * 0.8)} {p(L, 0)} C{p(L * 0.75, ww * 0.8)} {p(L * 0.3, ww)} {p(0, 0)} Z"


def lift(defs, gid, colour, alpha, cx=960, cy=560, rx=620, ry=470):
    return glow(defs, gid, colour, alpha, cx, cy, rx, ry)


def rays(cx, cy, n, r, col, op, spread=2.0, start=180, end=360, rnd=None, r0=0):
    """n rays fanning from (cx, cy) out to r; with r0, only their run from r0 out to r."""
    out = []
    for k in range(n):
        a = math.radians(start + (end - start) * (k + 0.5) / n)
        w = math.radians(spread)
        if r0:
            out.append(f'<path d="M{f(cx + math.cos(a - w) * r0)} {f(cy + math.sin(a - w) * r0)} L{f(cx + math.cos(a - w) * r)} {f(cy + math.sin(a - w) * r)} L{f(cx + math.cos(a + w) * r)} {f(cy + math.sin(a + w) * r)} L{f(cx + math.cos(a + w) * r0)} {f(cy + math.sin(a + w) * r0)} Z" fill="{col}" opacity="{f(op)}"/>')
            continue
        out.append(f'<path d="M{f(cx)} {f(cy)} L{f(cx + math.cos(a - w) * r)} {f(cy + math.sin(a - w) * r)} L{f(cx + math.cos(a + w) * r)} {f(cy + math.sin(a + w) * r)} Z" fill="{col}" opacity="{f(op)}"/>')
    return "".join(out)


# ---------------------------------------------------------------- 1. Himalayan Sunrise

def puffs(xs, ys, xe, ye, base, depth, rnd, rmin, rmax):
    """A run of cloud puffs from (xs, ys) to (xe, ye) exactly, on a level base, filled down to base + depth."""
    rs = []
    while 2 * sum(rs) < xe - xs:
        rs.append(rnd.uniform(rmin, rmax))
    k = (xe - xs) / (2 * sum(rs))
    d = f"M{f(xs)} {f(base + depth)} L{f(xs)} {f(ys)}"
    x = xs
    for i, r in enumerate(rs):
        r *= k
        x += 2 * r
        d += f" A{f(r)} {f(r * 0.55)} 0 0 1 {f(x)} {f(ye if i == len(rs) - 1 else base + rnd.uniform(-8, 8))}"
    return d + f" L{f(xe)} {f(base + depth)} Z"


def cloud_bank(y, rnd, top, shade, x0=-80, x1=W + 80, rmin=40, rmax=90, depth=None):
    """A bank of cloud seen from above: a bumpy top edge, a soft shade under each row of puffs. With bleed it
    runs on over the margin (EXT), from its own ends, and down off the frame."""
    depth = 400 + S.BY if depth is None else depth
    ends = []

    def edge(dy):
        d = f"M{f(x0)} {f(y + depth)} L{f(x0)} {f(y + dy)}"
        x = x0
        yy = y + dy
        while x < x1:
            r = rnd.uniform(rmin, rmax)
            yy = y + dy + rnd.uniform(-8, 8)
            d += f" A{f(r)} {f(r * 0.55)} 0 0 1 {f(x + 2 * r)} {f(yy)}"
            x += 2 * r
        ends.append((x, yy))
        return d + f" L{f(x)} {f(y + depth)} Z"
    state = rnd.getstate()
    out = f'<path d="{edge(16)}" fill="{shade}"/>'
    rnd.setstate(state)
    out += f'<path d="{edge(0)}" fill="{top}"/>'
    if S.BX:
        st = S.EXT.getstate()
        for k, (dy, col) in enumerate(((16, shade), (0, top))):
            S.EXT.setstate(st)
            out += f'<path d="{puffs(-S.BX - 140, y + dy + S.EXT.uniform(-8, 8), x0, y + dy, y + dy, depth - dy, S.EXT, rmin, rmax)}" fill="{col}"/>'
            out += f'<path d="{puffs(ends[k][0], ends[k][1], W + S.BX + 140, y + dy, y + dy, depth - dy, S.EXT, rmin, rmax)}" fill="{col}"/>'
    return out


def lodge(x, y, s):
    """A trekkers' stone lodge: grey stone walls, a blue tin roof, one window lit."""
    w, h = 90 * s, 50 * s
    return (f'<rect x="{f(x)}" y="{f(y - h)}" width="{f(w)}" height="{f(h)}" fill="#CFC4C2"/>'
            f'<rect x="{f(x + w * 0.65)}" y="{f(y - h)}" width="{f(w * 0.35)}" height="{f(h)}" fill="#ADA0A6"/>'
            f'<path d="M{f(x - 8 * s)} {f(y - h)} L{f(x + w * 0.3)} {f(y - h - 26 * s)} L{f(x + w + 8 * s)} {f(y - h - 26 * s)} L{f(x + w + 14 * s)} {f(y - h)} Z" fill="#4F6EA6"/>'
            f'<rect x="{f(x + w * 0.18)}" y="{f(y - h * 0.7)}" width="{f(w * 0.16)}" height="{f(h * 0.3)}" fill="#FFD27F"/>'
            f'<rect x="{f(x + w * 0.44)}" y="{f(y - h * 0.62)}" width="{f(w * 0.14)}" height="{f(h * 0.62)}" fill="#5A4652"/>')


def bleed_firs(line, x0, x1, n, colours, h0, h1, dy0, dy1):
    """n firs on EXT along line (ext_line) between x0 and x1."""
    out = []
    for _ in range(max(0, n)):
        x = S.EXT.uniform(x0, x1)
        y = P.along(line, x) + S.EXT.uniform(dy0, dy1)
        out.append(S.conifer(x, y, S.EXT.uniform(h0, h1), S.EXT.choice(colours), S.EXT))
    return "".join(out)


def sunrise():
    """Dawn breaking over the high Himalaya: rose-lit snow, a sea of cloud in the valleys, the sun
    rising in a notch between the peaks behind the menu, griffons riding the first light, a stone lodge
    and its flags on the ridge."""
    rnd = random.Random(101)
    defs, b = [], []
    defs.append(lg("sky", [(0, "#B4ADD3", 1), (0.3, "#D9B6C9", 1), (0.5, "#F2C6AE", 1), (0.66, "#FADBB4", 1), (1, "#FDEBD0", 1)], box=FULL))
    b.append(S.full("url(#sky)"))
    b.append(glow(defs, "sunhalo", "#FFF4D8", 0.95, 960, 560, 900, 520))
    b.append(rays(960, 560, 30, 1100, "#FFF8EA", 0.08, 1.2, 190, 350))
    if bleed():
        b.append(rays(960, 560, 30, 1700, "#FFF8EA", 0.08, 1.2, 190, 350, r0=1100))
    b.append('<circle cx="960" cy="560" r="74" fill="#FFF3D2"/><circle cx="960" cy="560" r="60" fill="#FFFAEA"/>')
    # Farthest range, pale lavender, its snow just touched with pink.
    if S.BX:
        b.append(S.peak(-205, 410, 230, 185, 640, S.EXT, "#CDB4C7", "#B79FBA", "#FFEDE6", "#EBCFD6", 0.5, 0.06))
        b.append(S.peak(2130, 395, 190, 240, 640, S.EXT, "#CDB4C7", "#B79FBA", "#FFEDE6", "#EBCFD6", 0.5, 0.06))
    for px, py, wl, wr in [(120, 400, 260, 240), (560, 380, 260, 280), (1340, 390, 280, 260), (1800, 380, 260, 260)]:
        b.append(S.peak(px, py, wl, wr, 640, rnd, "#CDB4C7", "#B79FBA", "#FFEDE6", "#EBCFD6", 0.5, 0.06))
    # The two great peaks to the sides, lit from the sunrise in the middle: rose on the inner faces.
    b.append(S.peak(420, 200, 360, 420, 680, rnd, "#B88FA5", "#97728F", "#FFE3D9", "#F0BFC4", 0.46, 0.07))
    b.append(S.peak(1530, 230, 430, 380, 680, rnd, "#B88FA5", "#97728F", "#FFE3D9", "#F0BFC4", 0.46, 0.07))
    # Lower peaks framing the notch where the sun stands.
    b.append(S.peak(760, 430, 220, 200, 680, rnd, "#C49CB0", "#A7849F", "#FFE8DF", "#F2C9CC", 0.42, 0.06))
    b.append(S.peak(1170, 450, 200, 240, 680, rnd, "#C49CB0", "#A7849F", "#FFE8DF", "#F2C9CC", 0.42, 0.06))
    b.append(S.band(560, 700, "#FDE6D2", 0, 0.9, 0.6, "mist1", defs))
    # The sea of cloud: banks of puffs filling the valleys, peach on top, lavender in the shade.
    b.append(cloud_bank(650, rnd, "#F8DCD0", "#E2C3CC", rmin=50, rmax=110))
    b.append(cloud_bank(705, rnd, "#FBE6DA", "#E9CCD0", rmin=45, rmax=95))
    b.append(cloud_bank(770, rnd, "#FDF0E6", "#EED6D6", rmin=40, rmax=85))
    b.append(cloud_bank(850, rnd, "#FFF6EE", "#F1DEDB", rmin=36, rmax=75))
    # Dark near ridges to both sides, rising out of the cloud, with fir; they curve away under the menu.
    left = [(-40, 600), (120, 622), (260, 676), (400, 760), (500, 860), (560, 980), (580, 1120)]
    right = [(1330, 1120), (1360, 980), (1440, 860), (1560, 770), (1700, 700), (1840, 660), (1960, 640)]
    b.append(hill(left, "#8E7C9E", 1120))
    b.append(hill(right, "#8E7C9E", 1120))
    for x0, x1 in [(0, 470), (1450, 1920)]:
        for _ in range(26):
            x = rnd.uniform(x0, x1)
            line = left if x < 960 else right
            y = P.along(line, x) + rnd.uniform(4, 30)
            b.append(S.conifer(x, y, rnd.uniform(40, 80), rnd.choice(["#7A6A8D", "#6F6083", "#84739A"]), rnd))
    firs = ["#7A6A8D", "#6F6083", "#84739A"]
    b.append(bleed_firs(ext_line(left), -S.BX - 20, -18, int(S.BX * 26 / 470), firs, 40, 80, 4, 30))
    b.append(bleed_firs(ext_line(right), W + 18, W + S.BX + 20, int(S.BX * 26 / 470), firs, 40, 80, 4, 30))
    left2 = [(-40, 780), (140, 800), (280, 860), (370, 960), (400, 1120)]
    right2 = [(1560, 1120), (1600, 960), (1720, 870), (1860, 830), (1960, 820)]
    b.append(hill(left2, "#6A5A7E", 1120))
    b.append(hill(right2, "#6A5A7E", 1120))
    for _ in range(18):
        x = rnd.choice([rnd.uniform(0, 330), rnd.uniform(1640, 1920)])
        line = left2 if x < 960 else right2
        y = P.along(line, x) + rnd.uniform(6, 30)
        b.append(S.conifer(x, y, rnd.uniform(60, 110), rnd.choice(["#58496C", "#4E4062", "#625377"]), rnd))
    firs = ["#58496C", "#4E4062", "#625377"]
    b.append(bleed_firs(ext_line(left2), -S.BX - 20, -24, int(S.BX * 18 / 610), firs, 60, 110, 6, 30))
    b.append(bleed_firs(ext_line(right2), W + 24, W + S.BX + 20, int(S.BX * 18 / 610), firs, 60, 110, 6, 30))
    # The lodge on the right ridge, its flags strung down the slope.
    b.append(lodge(1700, 712, 1.0))
    b.append('<rect x="1820" y="560" width="5" height="150" fill="#5A4652"/>')
    b.append(S.flags(S.string((1822, 564), (1960, 610), 14), S.FLAGS, rnd, 0.7))
    if S.BX:
        # The string runs on over the margin to a second pole on the ridge.
        run = string_on((1822, 564), (1960, 610), 14, 2160, 20)
        foot = P.along(ext_line(right), 2160) + 6
        b.append(f'<rect x="2157.5" y="{f(run[-1][1] - 4)}" width="5" height="{f(foot - run[-1][1] + 4)}" fill="#5A4652"/>')
        b.append(S.flags(run, S.FLAGS[3:] + S.FLAGS[:3], S.EXT, 0.7))
    b.append(S.flags(S.string((1822, 570), (1600, 760), 30), S.FLAGS, rnd, 0.7))
    # Griffons riding the first light.
    for x, y, s in [(1330, 480, 2.2), (1410, 440, 1.6), (1460, 510, 1.3), (560, 470, 1.5), (500, 500, 1.1)]:
        b.append(P.birds([(x, y)], s))
    b.append(lift(defs, "lift", "#FFF8EC", 0.45, 960, 600, 640, 460))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 2. Laligurans Forest

def flowers_d(items):
    """Five-petal flowers as one petal path and one centre path: items are (x, y, r, angle)."""
    pd, cd = "", ""
    for x, y, r, ang in items:
        for k in range(5):
            a = math.radians(ang + k * 72)
            px, py = x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55
            pr = r * 0.47
            pd += f"M{f(px + pr)} {f(py)} A{f(pr)} {f(pr)} 0 1 0 {f(px - pr)} {f(py)} A{f(pr)} {f(pr)} 0 1 0 {f(px + pr)} {f(py)} Z"
        cr = r * 0.24
        cd += f"M{f(x + cr)} {f(y)} A{f(cr)} {f(cr)} 0 1 0 {f(x - cr)} {f(y)} A{f(cr)} {f(cr)} 0 1 0 {f(x + cr)} {f(y)} Z"
    return pd, cd


def limb(pts, w0, w1, col, light=None):
    """A tapering branch through pts: a few overlapping strokes, thick to thin, and a lit edge."""
    out = []
    n = len(pts) - 1
    for i in range(n):
        w = w0 + (w1 - w0) * i / max(1, n - 1)
        out.append(f'<path d="M{f(pts[i][0])} {f(pts[i][1])} L{f(pts[i + 1][0])} {f(pts[i + 1][1])}" stroke="{col}" stroke-width="{f(w)}" stroke-linecap="round"/>')
    if light:
        out.append(f'<path d="{smooth_d([(x - w0 * 0.22, y) for x, y in pts])}" stroke="{light}" stroke-width="{f(max(2, w1 * 0.4))}" fill="none" stroke-linecap="round" opacity="0.55"/>')
    return "".join(out)


def bend(a, c, n, wob, rnd):
    pts = []
    for i in range(n + 1):
        t = i / n
        x = a[0] + (c[0] - a[0]) * t + (rnd.uniform(-wob, wob) if 0 < i < n else 0)
        y = a[1] + (c[1] - a[1]) * t + (rnd.uniform(-wob, wob) * 0.5 if 0 < i < n else 0)
        pts.append((x, y))
    return pts


def rhodo(x, ground, rnd, s, tips, bark="#6B5242", light="#8E7058", leaf="#3E6A48", leaf_d2="#2B4F35", leaf_l="#5A8A5E",
          petal="#C8202F", petal_l="#E9505C", centre="#6A0D16", bloom=0.7):
    """A gnarled rhododendron tree: a twisting trunk, limbs to each tip, and at every tip a whorl of
    long leaves with a domed truss of blossom in it."""
    out = []
    if S.BY and ground >= H:
        # The trunk goes on down off the frame.
        foot = (x, ground + S.BY + 60)
        out.append(f'<path d="M{f(x)} {f(ground)} L{f(foot[0])} {f(foot[1])}" stroke="{bark}" stroke-width="{f(46 * s)}" stroke-linecap="round"/>'
                   f'<path d="M{f(x - 46 * s * 0.22)} {f(ground)} L{f(x - 46 * s * 0.22)} {f(foot[1])}" stroke="{light}" stroke-width="{f(max(2, 34 * s * 0.4))}" opacity="0.55"/>')
    fork = (x + rnd.uniform(-20, 20) * s, ground - 300 * s)
    out.append(limb(bend((x, ground), fork, 4, 14 * s, rnd), 46 * s, 34 * s, bark, light))
    for tx, ty in tips:
        mid = ((fork[0] + tx) / 2 + rnd.uniform(-40, 40) * s, (fork[1] + ty) / 2 + rnd.uniform(-30, 10) * s)
        out.append(limb(bend(fork, mid, 2, 8 * s, rnd) + bend(mid, (tx, ty), 2, 8 * s, rnd)[1:], 26 * s, 9 * s, bark, light))
    dark, mid_l, lit, fl = "", "", "", []
    for tx, ty in tips:
        # Several whorls round each tip.
        for _ in range(7):
            wx, wy = tx + rnd.uniform(-110, 110) * s, ty + rnd.uniform(-60, 50) * s
            for k in range(8):
                a = k * 45 + rnd.uniform(-14, 14)
                L = rnd.uniform(46, 70) * s
                d = leaf_d(wx, wy, L, a, 0.27)
                if math.sin(math.radians(a)) > 0.3:
                    dark += d
                elif math.sin(math.radians(a)) < -0.3:
                    lit += d
                else:
                    mid_l += d
            if rnd.random() < bloom:
                br = rnd.uniform(11, 15) * s
                for _ in range(8):
                    fl.append((wx + rnd.uniform(-1, 1) * br * 1.9, wy - br * 0.6 + rnd.uniform(-1, 0.6) * br * 1.5, br, rnd.uniform(0, 72)))
    out.append(f'<path d="{dark}" fill="{leaf_d2}"/><path d="{mid_l}" fill="{leaf}"/><path d="{lit}" fill="{leaf_l}"/>')
    fl.sort(key=lambda p: p[1])
    back = [p for i, p in enumerate(fl) if i % 3 == 0]
    front = [p for i, p in enumerate(fl) if i % 3 != 0]
    for items, col in ((back, petal), (front, petal_l)):
        pd, cd = flowers_d(items)
        out.append(f'<path d="{pd}" fill="{col}"/><path d="{cd}" fill="{centre}"/>')
    return "".join(out)


def far_tree(x, g, layer, fir, crown, bloom, rnd):
    """One tree of the misty far forest: a fir, or a blossoming crown on a trunk."""
    if rnd.random() < 0.6:
        return S.conifer(x, g, rnd.uniform(160, 280) * (1 + layer * 0.2), fir, rnd)
    out = f'<rect x="{f(x - 4)}" y="{f(g - 120)}" width="8" height="120" fill="{fir}"/>'
    blobs = "".join(f"M{f(cx + r)} {f(cy)} A{f(r)} {f(r * 0.8)} 0 1 0 {f(cx - r)} {f(cy)} A{f(r)} {f(r * 0.8)} 0 1 0 {f(cx + r)} {f(cy)} Z"
                    for cx, cy, r in [(x + rnd.uniform(-50, 50), g - 130 + rnd.uniform(-30, 20), rnd.uniform(30, 55)) for _ in range(4)])
    out += f'<path d="{blobs}" fill="{crown}"/>'
    dots = "".join(f"M{f(px)} {f(py)} m-6 0 a6 6 0 1 0 12 0 a6 6 0 1 0 -12 0Z" for px, py in [(x + rnd.uniform(-70, 70), g - 130 + rnd.uniform(-50, 30)) for _ in range(6)])
    return out + f'<path d="{dots}" fill="{bloom}"/>'


def laligurans():
    """A rhododendron forest in April: gnarled trees in crimson bloom on both sides, misty fir and
    blossom beyond, a mossy path winding into the light, petals drifting down."""
    rnd = random.Random(202)
    defs, b = [], []
    defs.append(lg("fsky", [(0, "#EDF1E6", 1), (0.5, "#E3EBDF", 1), (1, "#D5E2D3", 1)], box=FULL))
    b.append(S.full("url(#fsky)"))
    b.append(glow(defs, "fsun", "#FFF8E2", 0.95, 1000, 330, 780, 470))
    for k in range(6):
        x = 560 + k * 150 + rnd.uniform(-30, 30)
        if bleed():
            # The shafts run on up into the margin and down off the frame, along the same lines.
            y0, y1 = -20 - S.BY, 1100 + S.BY
            d = (f"M{f(x - 160 * S.BY / 1120)} {f(y0)} L{f(x + 60 - 240 * S.BY / 1120)} {f(y0)} "
                 f"L{f(x + 300 + 240 * S.BY / 1120)} {f(y1)} L{f(x + 160 + 160 * S.BY / 1120)} {f(y1)} Z")
        else:
            d = f"M{f(x)} -20 L{f(x + 60)} -20 L{f(x + 300)} 1100 L{f(x + 160)} 1100 Z"
        b.append(f'<path d="{d}" fill="#FFFBEF" opacity="{f(rnd.uniform(0.10, 0.18))}"/>')
    # Far forest in the mist: firs and blossoming crowns, two layers.
    for layer, (fir, crown, bloom, base, n) in enumerate([("#C3D4CB", "#CADBCF", "#E8C3C6", 700, 38), ("#A9C0B4", "#B2C8BA", "#DFA0A6", 740, 30)]):
        for _ in range(n):
            x = rnd.uniform(-40, W + 40)
            g = base + rnd.uniform(-30, 20)
            b.append(far_tree(x, g, layer, fir, crown, bloom, rnd))
        if S.BX:
            for _ in range(int(n * (S.BX + 40) / (W + 80) * 2)):
                x = S.EXT.choice([S.EXT.uniform(-S.BX - 80, -110), S.EXT.uniform(W + 110, W + S.BX + 80)])
                b.append(far_tree(x, base + S.EXT.uniform(-30, 20), layer, fir, crown, bloom, S.EXT))
        b.append(S.band(base - 300, base + 40, "#EFF3EC", 0, 0.8, 0.45, f"fmist{layer}", defs))
    # The forest floor: mossy banks, and the path narrowing into the light.
    b.append(hill([(-40, 760), (300, 742), (700, 770), (1000, 762), (1400, 746), (1960, 760)], "#B9CEA9"))
    b.append(hill([(-40, 840), (360, 808), (760, 846), (1160, 836), (1560, 806), (1960, 826)], "#A3BF93"))
    path_l = [(700, 1100), (760, 1000), (880, 930), (930, 870), (900, 820), (940, 786), (962, 772)]
    path_r = [(980, 772), (992, 790), (968, 822), (1010, 870), (1080, 930), (1180, 1000), (1270, 1100)]
    b.append(f'<path d="{smooth_closed(path_l + path_r)}" fill="#E4D6B9"/>')
    inner = [(800, 1100), (840, 1010), (930, 945), (968, 880), (948, 830), (968, 795), (972, 780), (985, 795), (1000, 830), (1040, 880), (1100, 945), (1180, 1010), (1200, 1100)]
    b.append(f'<path d="{smooth_closed(inner)}" fill="#EEE3CB"/>')
    bank_l = [(-40, 910), (260, 880), (520, 930), (660, 1010), (700, 1120)]
    bank_r = [(1260, 1120), (1320, 1000), (1500, 920), (1700, 890), (1960, 900)]
    b.append(hill(bank_l, "#8FB07F", 1120))
    b.append(hill(bank_r, "#8FB07F", 1120))
    fern = []
    for _ in range(30):
        x = rnd.choice([rnd.uniform(0, 640), rnd.uniform(1300, 1920)])
        y = 930 + rnd.uniform(0, 130)
        for k in range(5):
            fern.append(leaf_d(x, y, rnd.uniform(40, 70), -90 + (k - 2) * 26 + rnd.uniform(-8, 8), 0.18))
    if bleed():
        # Ferns on the banks over the margin: at the sides, and on the banks' feet below the frame.
        lb, rb = ext_line(bank_l), ext_line(bank_r)
        # (a fern spreads 56 to the side and stands 70 tall: none reach into the picture)
        spots = [(S.EXT.choice([S.EXT.uniform(-S.BX, -58), S.EXT.uniform(W + 58, W + S.BX)]), 930 + S.EXT.uniform(0, 130)) for _ in range(int(S.BX / 18))]
        spots += [(S.EXT.choice([S.EXT.uniform(-S.BX, 600), S.EXT.uniform(1340, W + S.BX)]), S.EXT.uniform(H + 72, H + S.BY + 40)) for _ in range(int(S.BY / 3))]
        for x, y in spots:
            if y < P.along(lb if x < 960 else rb, x) + 30:
                continue
            for k in range(5):
                fern.append(leaf_d(x, y, S.EXT.uniform(40, 70), -90 + (k - 2) * 26 + S.EXT.uniform(-8, 8), 0.18))
    b.append(f'<path d="{"".join(fern)}" fill="#6F955F"/>')
    # The trees: crowns under the profile chip on the left and under the button column on the right.
    b.append(rhodo(150, 1110, rnd, 1.0, [(70, 420), (250, 360), (380, 470)]))
    b.append(rhodo(470, 1050, rnd, 0.72, [(420, 600), (560, 560)], bloom=0.8))
    b.append(rhodo(1800, 1110, rnd, 1.0, [(1660, 580), (1840, 530), (1930, 640)]))
    b.append(rhodo(1500, 1050, rnd, 0.7, [(1450, 680), (1580, 640)], bloom=0.8))
    # Petals drifting down and lying on the path.
    pet = []
    for _ in range(70):
        x = rnd.choice([rnd.uniform(0, 620), rnd.uniform(1320, 1920), rnd.uniform(820, 1140)])
        y = rnd.uniform(380, 1060) if not 820 < x < 1140 else rnd.uniform(1046, 1076)
        a = rnd.uniform(0, 180)
        pet.append(leaf_d(x, y, rnd.uniform(8, 13), a, 0.5))
    for x, y in bleed_points(20, 0, 620, 380, 1060, 14):
        pet.append(leaf_d(x, y, S.EXT.uniform(8, 13), S.EXT.uniform(0, 180), 0.5))
    b.append(f'<path d="{"".join(pet[::2])}" fill="#D52A38" opacity="0.9"/><path d="{"".join(pet[1::2])}" fill="#EC5F6A" opacity="0.9"/>')
    b.append(lift(defs, "flift", "#F8FAF2", 0.55, 960, 580, 600, 470))
    b.append(lift(defs, "ffoot", "#F4F2E4", 0.85, 960, 1004, 560, 100))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 3. Golden Terraces

def peepal(cx, ground, s, rnd):
    """A peepal tree over a stone chautara: a broad crown of heart-shaped leaves, warm light on its
    right, a thick fluted trunk, the two-tier resting platform round it."""
    out = []
    for i, (w, h, col, top) in enumerate([(360, 40, "#BFA27C", "#D8C19C"), (260, 36, "#C9AE88", "#E2CCA8")]):
        w, h = w * s, h * s
        y = ground - (40 * s if i else 0)
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - h)}" width="{f(w)}" height="{f(h)}" rx="{f(3 * s)}" fill="{col}"/>')
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - h)}" width="{f(w)}" height="{f(6 * s)}" rx="{f(3 * s)}" fill="{top}"/>')
        out.append(f'<rect x="{f(cx + w * 0.2)}" y="{f(y - h)}" width="{f(w * 0.3)}" height="{f(h)}" fill="#8A6E4E" opacity="0.25"/>')
        joints = "".join(f"M{f(cx - w / 2 + j * 36 * s + (i % 2) * 18 * s)} {f(y - h + 8 * s)} v{f(h - 10 * s)}" for j in range(1, int(w / (36 * s))))
        out.append(f'<path d="{joints}" stroke="#9A7F5C" stroke-width="{f(2 * s)}" opacity="0.6"/>')
    tg = ground - 76 * s
    out.append(limb([(cx, tg), (cx - 6 * s, tg - 110 * s), (cx + 4 * s, tg - 200 * s)], 70 * s, 54 * s, "#6A4B34", "#8A6446"))
    for tx, ty, w in [(-200, -300, 26), (-90, -360, 22), (40, -380, 22), (170, -320, 24), (230, -250, 16), (-250, -230, 16)]:
        out.append(limb(bend((cx, tg - 190 * s), (cx + tx * s, tg + ty * s), 3, 14 * s, rnd), w * s, w * 0.45 * s, "#6A4B34", "#8A6446"))
    # Crown: overlapping leaf masses in three tones, lit from the right where the sun is going down.
    blobs = []
    for _ in range(46):
        a = rnd.uniform(0, math.tau)
        d = rnd.uniform(0.0, 1.0) ** 0.7
        bx = cx + math.cos(a) * d * 290 * s
        by = tg - 330 * s + math.sin(a) * d * 150 * s
        blobs.append((bx, by, rnd.uniform(50, 85) * s))

    def circles(sel, k, dx=0, dy=0):
        return "".join(f"M{f(x + dx + r * k)} {f(y + dy)} A{f(r * k)} {f(r * k * 0.86)} 0 1 0 {f(x + dx - r * k)} {f(y + dy)} A{f(r * k)} {f(r * k * 0.86)} 0 1 0 {f(x + dx + r * k)} {f(y + dy)} Z" for x, y, r in sel)
    out.append(f'<path d="{circles(blobs, 1.0)}" fill="#475A2A"/>')
    upper = [p for p in blobs if p[1] < tg - 300 * s or p[0] > cx + 60 * s]
    out.append(f'<path d="{circles(upper, 0.84, 10 * s, -10 * s)}" fill="#607530"/>')
    lit = [p for p in blobs if p[0] > cx + 20 * s and p[1] < tg - 290 * s]
    out.append(f'<path d="{circles(lit, 0.55, 22 * s, -20 * s)}" fill="#8C9B40"/>')
    # Heart-shaped leaves at the crown's edge, glinting.
    lv = []
    for _ in range(90):
        a = rnd.uniform(math.pi * 0.9, math.pi * 2.15)
        bx = cx + math.cos(a) * rnd.uniform(210, 290) * s
        by = tg - 330 * s + math.sin(a) * rnd.uniform(110, 170) * s
        lv.append(leaf_d(bx, by, rnd.uniform(18, 28) * s, rnd.uniform(60, 120), 0.5))
    out.append(f'<path d="{"".join(lv[::2])}" fill="#A9B04A"/><path d="{"".join(lv[1::2])}" fill="#56692C"/>')
    return "".join(out)


def terraced_hill(px, py, n, ax, ay, dy, fields, risers, rnd, riser_h=9, wob=0.05, more=0):
    """A hill stepped into terraces, seen from across the valley: each step's front edge an ellipse
    under the summit, drawn from the lowest step up, the riser a darker rim under each field. more adds
    that many steps below the lowest (on EXT), for the bleed under the frame."""
    out = []
    for k in list(range(n + more, n, -1)) + list(range(n, 0, -1)):
        r_ = S.EXT if k > n else rnd
        cy = py + k * dy
        rx, ry = k * ax, k * ay
        ph = r_.uniform(0, math.tau)
        pts = []
        for i in range(73):
            t = math.tau * i / 72
            r = 1 + wob * math.sin(3 * t + ph) + wob * 0.6 * math.sin(5 * t + ph * 1.7)
            pts.append((px + math.cos(t) * rx * r, cy + math.sin(t) * ry * r))
        d = "M" + " L".join(f"{f(x)} {f(y)}" for x, y in pts) + " Z"
        dr = "M" + " L".join(f"{f(x)} {f(y + riser_h)}" for x, y in pts) + " Z"
        i = min(len(fields) - 1, int((k - 1) * len(fields) / n))
        out.append(f'<path d="{dr}" fill="{risers[min(i, len(risers) - 1)]}"/><path d="{d}" fill="{fields[i]}"/>')
    return "".join(out)


def haystack(x, y, s):
    """A straw stack round a pole: a cone with a rounded shoulder, the pole's tip above."""
    return (f'<rect x="{f(x - 2 * s)}" y="{f(y - 128 * s)}" width="{f(4 * s)}" height="{f(30 * s)}" fill="#6A4B34"/>'
            f'<path d="M{f(x - 46 * s)} {f(y)} C{f(x - 46 * s)} {f(y - 60 * s)} {f(x - 20 * s)} {f(y - 104 * s)} {f(x)} {f(y - 104 * s)} C{f(x + 20 * s)} {f(y - 104 * s)} {f(x + 46 * s)} {f(y - 60 * s)} {f(x + 46 * s)} {f(y)} Z" fill="#E0AF52"/>'
            f'<path d="M{f(x - 4 * s)} {f(y - 104 * s)} C{f(x - 22 * s)} {f(y - 100 * s)} {f(x - 46 * s)} {f(y - 60 * s)} {f(x - 46 * s)} {f(y)} L{f(x - 14 * s)} {f(y)} C{f(x - 22 * s)} {f(y - 50 * s)} {f(x - 16 * s)} {f(y - 86 * s)} {f(x - 4 * s)} {f(y - 104 * s)} Z" fill="#B9853A"/>'
            f'<path d="M{f(x - 40 * s)} {f(y - 30 * s)} Q{f(x)} {f(y - 40 * s)} {f(x + 44 * s)} {f(y - 30 * s)} M{f(x - 30 * s)} {f(y - 66 * s)} Q{f(x)} {f(y - 76 * s)} {f(x + 32 * s)} {f(y - 66 * s)}" stroke="#A3732F" stroke-width="{f(2.4 * s)}" fill="none" opacity="0.7"/>')


def farmhouse(x, y, s):
    """A hill farmhouse: red-ochre lower walls, white upper walls, a thatched roof, small dark windows,
    maize hung to dry under the eaves."""
    w, h = 220 * s, 120 * s
    out = [f'<rect x="{f(x)}" y="{f(y - h)}" width="{f(w)}" height="{f(h)}" fill="#F2E5CC"/>',
           f'<rect x="{f(x)}" y="{f(y - h * 0.45)}" width="{f(w)}" height="{f(h * 0.45)}" fill="#B85A3A"/>',
           f'<rect x="{f(x)}" y="{f(y - h)}" width="{f(w * 0.3)}" height="{f(h)}" fill="#000000" opacity="0.14"/>',
           f'<path d="M{f(x - 26 * s)} {f(y - h + 6 * s)} C{f(x + w * 0.2)} {f(y - h - 70 * s)} {f(x + w * 0.8)} {f(y - h - 70 * s)} {f(x + w + 26 * s)} {f(y - h + 6 * s)} Z" fill="#C9A35C"/>',
           f'<path d="M{f(x + w * 0.45)} {f(y - h - 52 * s)} C{f(x + w * 0.2)} {f(y - h - 66 * s)} {f(x + w * 0.05)} {f(y - h - 30 * s)} {f(x - 26 * s)} {f(y - h + 6 * s)} L{f(x + w * 0.38)} {f(y - h + 6 * s)} Z" fill="#9A7A40"/>',
           f'<rect x="{f(x + w * 0.44)}" y="{f(y - h * 0.42)}" width="{f(w * 0.14)}" height="{f(h * 0.42)}" fill="#4A2E1F"/>']
    for wx in (0.14, 0.72):
        out.append(f'<rect x="{f(x + w * wx)}" y="{f(y - h * 0.86)}" width="{f(w * 0.12)}" height="{f(h * 0.22)}" fill="#4A2E1F"/>')
    for k in range(6):
        out.append(f'<ellipse cx="{f(x + w * 0.3 + k * w * 0.06)}" cy="{f(y - h * 0.62)}" rx="{f(5 * s)}" ry="{f(13 * s)}" fill="#E9A631"/>')
    return "".join(out)


def terraces_gold():
    """Rice terraces at harvest in the last golden hour: a hill stepped in ripe gold on the right with a
    farmhouse and straw stacks on its shoulder, an old peepal over a stone chautara on the terraced hill
    to the left, ridges in violet haze, the sun going down between them."""
    rnd = random.Random(303)
    defs, b = [], []
    defs.append(lg("gsky", [(0, "#F1D9B9", 1), (0.42, "#F6CF9C", 1), (0.62, "#F9DDAE", 1), (1, "#F6E3BE", 1)], box=FULL))
    b.append(S.full("url(#gsky)"))
    b.append(glow(defs, "gsun", "#FFF1CC", 0.95, 1300, 480, 760, 420))
    b.append('<circle cx="1300" cy="480" r="56" fill="#FFF0C4"/><circle cx="1300" cy="480" r="44" fill="#FFF8E0"/>')
    ridges = []
    for k, (y, col) in enumerate([(440, "#DDB3A4"), (500, "#CFA094"), (560, "#BE8F88")]):
        ridges.append(wave(-40, W + 40, y, 22 - k * 4, 12, rnd, k))
        b.append(hill(ridges[-1], col))
        b.append(S.band(y - 10, y + 90, "#F7D8B4", 0, 0.55, 0.0, f"ghaze{k}", defs))
    for _ in range(14):
        x = rnd.choice([rnd.uniform(80, 520), rnd.uniform(1400, 1880)])
        y = 560 + rnd.uniform(4, 24)
        w = rnd.uniform(12, 18)
        b.append(S.house(x, y, w, w * 0.7, "#F3E4CF", "#D9C2A6", "#A8603E", "#8A4C32", "#5A3A2A"))
    if S.BX:
        # The hamlets go on along the far ridge in the margin.
        line = ext_line(ridges[2])
        for _ in range(int(S.BX / 40)):
            x = S.EXT.choice([S.EXT.uniform(-S.BX, -20), S.EXT.uniform(W + 10, W + S.BX)])
            w = S.EXT.uniform(12, 18)
            b.append(S.house(x, max(560, P.along(line, x)) + S.EXT.uniform(4, 24), w, w * 0.7, "#F3E4CF", "#D9C2A6", "#A8603E", "#8A4C32", "#5A3A2A"))
    # A far terraced hill in the haze, in the middle.
    b.append(terraced_hill(960, 600, 8, 70, 13, 15, ["#DDB98C", "#D8B080", "#D2A776"], ["#C2987A", "#BB9070", "#B48868"], rnd, 4))
    b.append(S.band(600, 760, "#F6D9B2", 0, 0.7, 0.2, "ghaze3", defs))
    # The left hill with the peepal on its top, and the great right hill.
    fields = ["#EDD08A", "#E8C67C", "#E2BB6E", "#DBAF62", "#D3A357", "#CB984D", "#C28C45", "#B9813F"]
    risers = ["#C9A160", "#C29656", "#BA8B4D", "#B18045", "#A6753F", "#9B6A39", "#906034", "#86572F"]
    b.append(terraced_hill(330, 700, 11, 64, 22, 26, fields, risers, rnd, 8, more=3 if bleed() else 0))
    b.append(peepal(330, 735, 0.92, rnd))
    greens = ["#D9CC7A", "#E6C676", "#E2BB6E", "#C9B25E", "#DBAF62", "#CB984D", "#BFA052", "#C28C45", "#B9813F"]
    b.append(terraced_hill(1640, 640, 13, 78, 26, 30, greens, risers, rnd, 9))
    b.append(farmhouse(1560, 690, 0.85))
    for x, y, s in [(1480, 720, 0.6), (1780, 690, 0.7), (1840, 716, 0.55), (1720, 740, 0.75)]:
        b.append(haystack(x, y, s))
    b.append(P.birds([(1080, 380), (1120, 400), (1150, 370), (1190, 392), (1220, 416)], 1.2))
    b.append(lift(defs, "glift", "#FFF8E8", 0.5, 960, 600, 600, 460))
    b.append(lift(defs, "gfoot", "#FCEFD2", 0.85, 960, 1004, 560, 100))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 4. Tihar Lights

def diyo(x, y, s, defs_glow="url(#gw)"):
    """A clay oil lamp, lit: a glow, the bowl, the flame."""
    return (f'<ellipse cx="{f(x)}" cy="{f(y - 14 * s)}" rx="{f(40 * s)}" ry="{f(34 * s)}" fill="{defs_glow}"/>'
            f'<path d="M{f(x - 15 * s)} {f(y - 6 * s)} Q{f(x)} {f(y + 9 * s)} {f(x + 15 * s)} {f(y - 6 * s)} Z" fill="#B8572F"/>'
            f'<path d="M{f(x - 15 * s)} {f(y - 6 * s)} L{f(x + 15 * s)} {f(y - 6 * s)} L{f(x + 13 * s)} {f(y - 3 * s)} L{f(x - 13 * s)} {f(y - 3 * s)} Z" fill="#D9824F"/>'
            f'<path d="M{f(x)} {f(y - 26 * s)} C{f(x + 5 * s)} {f(y - 17 * s)} {f(x + 5 * s)} {f(y - 8 * s)} {f(x)} {f(y - 7 * s)} C{f(x - 5 * s)} {f(y - 8 * s)} {f(x - 5 * s)} {f(y - 17 * s)} {f(x)} {f(y - 26 * s)} Z" fill="#FFC84D"/>'
            f'<ellipse cx="{f(x)}" cy="{f(y - 11 * s)}" rx="{f(1.8 * s)}" ry="{f(3.5 * s)}" fill="#FFF6D8"/>')


def garland(a, b_, sag, rnd, r=6.5, n=None):
    pts = S.string(a, b_, sag, n or max(6, int(math.dist(a, b_) / (r * 1.7))))
    out = []
    for i, (x, y) in enumerate(pts):
        out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{["#F29A1F", "#F7C531", "#E8701A"][i % 3]}"/>')
    return "".join(out)


def strand(x, y0, y1, r=6):
    out = []
    y = y0
    i = 0
    while y < y1:
        out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{["#F29A1F", "#F7C531"][i % 2]}"/>')
        y += r * 1.6
        i += 1
    out.append(f'<path d="M{f(x - r)} {f(y)} L{f(x + r)} {f(y)} L{f(x)} {f(y + r * 2.4)} Z" fill="#C8322D"/>')
    return "".join(out)


def akash_deep(x, y, s):
    """A paper sky lamp hung high on a bamboo pole for Tihar."""
    return (f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="{f(70 * s)}" ry="{f(80 * s)}" fill="url(#gw)"/>'
            f'<path d="M{f(x - 18 * s)} {f(y - 30 * s)} L{f(x + 18 * s)} {f(y - 30 * s)} L{f(x + 26 * s)} {f(y - 12 * s)} L{f(x + 26 * s)} {f(y + 14 * s)} L{f(x + 18 * s)} {f(y + 32 * s)} L{f(x - 18 * s)} {f(y + 32 * s)} L{f(x - 26 * s)} {f(y + 14 * s)} L{f(x - 26 * s)} {f(y - 12 * s)} Z" fill="#F7A93B"/>'
            f'<path d="M{f(x - 8 * s)} {f(y - 30 * s)} L{f(x + 8 * s)} {f(y - 30 * s)} L{f(x + 10 * s)} {f(y + 32 * s)} L{f(x - 10 * s)} {f(y + 32 * s)} Z" fill="#FFD27A"/>'
            f'<rect x="{f(x - 20 * s)}" y="{f(y - 36 * s)}" width="{f(40 * s)}" height="{f(7 * s)}" rx="{f(2 * s)}" fill="#8A3A1E"/>'
            f'<rect x="{f(x - 20 * s)}" y="{f(y + 30 * s)}" width="{f(40 * s)}" height="{f(7 * s)}" rx="{f(2 * s)}" fill="#8A3A1E"/>'
            f'<path d="M{f(x)} {f(y + 37 * s)} L{f(x)} {f(y + 58 * s)}" stroke="#C8322D" stroke-width="{f(3 * s)}"/>')


def firework(x, y, r, col, rnd, n=26):
    out = []
    for k in range(n):
        a = math.tau * k / n + rnd.uniform(-0.04, 0.04)
        r0, r1 = r * 0.35, r * rnd.uniform(0.8, 1.0)
        out.append(f'<path d="M{f(x + math.cos(a) * r0)} {f(y + math.sin(a) * r0)} L{f(x + math.cos(a) * r1)} {f(y + math.sin(a) * r1)}" stroke="{col}" stroke-width="2.4" stroke-linecap="round" opacity="0.75"/>')
        out.append(f'<circle cx="{f(x + math.cos(a) * (r1 + 6))}" cy="{f(y + math.sin(a) * (r1 + 6))}" r="2.6" fill="{col}"/>')
    return "".join(out)


def rangoli(cx, cy, r, sy=0.42):
    """A rangoli in perspective: rings of petals in the festival colours, a lamp at its heart."""
    out = [f'<g transform="translate({f(cx)} {f(cy)}) scale(1 {sy})">']
    out.append(f'<circle r="{f(r)}" fill="#3B1C3E"/>')
    for ring, (rr, n, col, w) in enumerate([(r * 0.92, 16, "#E8701A", 0.22), (r * 0.72, 12, "#D6337A", 0.26), (r * 0.52, 10, "#F7C531", 0.3), (r * 0.32, 8, "#2E9D8F", 0.36)]):
        d = ""
        for k in range(n):
            a = math.tau * (k + ring * 0.5) / n
            d += leaf_d(math.cos(a) * rr * 0.45, math.sin(a) * rr * 0.45, rr * 0.55, math.degrees(a), w)
        out.append(f'<path d="{d}" fill="{col}"/>')
    out.append(f'<circle r="{f(r * 0.14)}" fill="#FFF0C8"/>')
    for k in range(32):
        a = math.tau * k / 32
        out.append(f'<circle cx="{f(math.cos(a) * r * 0.98)}" cy="{f(math.sin(a) * r * 0.98)}" r="{f(r * 0.025)}" fill="#FFFFFF" opacity="0.8"/>')
    out.append('</g>')
    return "".join(out)


def tihar():
    """Tihar night at home: a brick house with a carved window glowing, marigold garlands, rows of
    diyo on the steps and sill, a rangoli at the door, sky lamps on bamboo poles, fireworks over the
    hills and the valley's lights below."""
    rnd = random.Random(404)
    defs, b = [], []
    defs.append(lg("nsky", [(0, "#241B47", 1), (0.45, "#3A2963", 1), (0.7, "#6A3C72", 1), (1, "#7E4570", 1)], box=FULL))
    defs.append(rg("gw", [(0, "#FFB547", 0.55), (1, "#FFB547", 0)]))
    b.append(S.full("url(#nsky)"))
    b.append(stars(rnd, 120, 0, W, 0, 420, (960, 300, 520, 300)))
    b.append(bleed_stars(120, 0, W, 0, 420))
    b.append(firework(230, 330, 80, "#F7C531", rnd))
    b.append(firework(520, 250, 44, "#F27BA8", rnd, 18))
    b.append(firework(1490, 330, 70, "#F7C531", rnd, 22))
    # Hills and the valley's lights.
    b.append(hill(wave(-40, W + 40, 560, 18, 12, rnd), "#3B2654"))
    for _ in range(140):
        x = rnd.uniform(0, W)
        y = 575 + rnd.uniform(0, 60) + (abs(x - 960) / 960) * 20
        r = rnd.uniform(1.2, 2.8)
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{rnd.choice(["#FFD27F", "#FFB65A", "#FFE6A8"])}" opacity="{f(rnd.uniform(0.6, 1))}"/>')
    for x, _ in bleed_points(140, 0, W, 575, 635, 4):
        y = 575 + S.EXT.uniform(0, 60) + min(1.3, abs(x - 960) / 960) * 20
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(S.EXT.uniform(1.2, 2.8))}" fill="{S.EXT.choice(["#FFD27F", "#FFB65A", "#FFE6A8"])}" opacity="{f(S.EXT.uniform(0.6, 1))}"/>')
    b.append(hill(wave(-40, W + 40, 650, 12, 10, rnd, 1), "#2C1C40"))
    for _ in range(60):
        x = rnd.uniform(0, W)
        y = 665 + rnd.uniform(0, 40)
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(rnd.uniform(1.5, 3))}" fill="#FFC86A" opacity="0.9"/>')
    for x, y in bleed_points(60, 0, W, 665, 705, 4):
        b.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(S.EXT.uniform(1.5, 3))}" fill="#FFC86A" opacity="0.9"/>')
    # The courtyard floor: old brick, warm-lit near the lamps.
    defs.append(lg("floor", [(0, "#4A2A3A", 1), (1, "#2A1626", 1)], box=(-20, 740, W + 40, 360)))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="740" width="{f(W + 40 + 2 * S.BX)}" height="{f(360 + S.BY)}" fill="url(#floor)"/>')
    joints = "".join(f"M{f(-20 - S.BX)} {y} L{f(W + 20 + S.BX)} {y}" for y in range(760, 1100 + math.ceil(S.BY), 26))
    b.append(f'<path d="{joints}" stroke="#20101C" stroke-width="2" opacity="0.5"/>')
    b.append(glow(defs, "floorglow", "#FFB547", 0.22, 960, 820, 900, 140))
    # Left: a neighbour's house, then low steps lined with diyo and a sky lamp on its pole. With bleed
    # the house and the steps go on to the left: their walls run over the margin.
    nx = -40
    ext = S.BX + 40 if S.BX else 0
    b.append(f'<rect x="{f(nx - ext)}" y="470" width="{f(300 + ext)}" height="250" fill="#5A2C2C"/>')
    b.append(f'<path d="{"".join(f"M{f(nx - ext)} {y} h{f(300 + ext)}" for y in range(484, 720, 14))}" stroke="#46201F" stroke-width="1.6" opacity="0.6"/>')
    b.append(f'<path d="M{f(nx - 20 - ext)} 470 L{nx + 330} 470 L{nx + 316} 444 L{f(nx - 20 - ext)} 444 Z" fill="#3A1C14"/><path d="M{f(nx - 20 - ext)} 444 L{nx + 340} 444 L{nx + 350} 430 L{f(nx - 20 - ext)} 430 Z" fill="#7A3A22"/>')
    for wx in [w for w in (-320, -200, -80) if S.BX and w + 72 > -S.BX - 60] + [40, 160]:
        b.append(f'<rect x="{wx - 12}" y="520" width="84" height="74" fill="#2E140C"/><rect x="{wx}" y="532" width="60" height="50" fill="#FFB547"/>'
                 f'<path d="M{wx + 30} 532 v50 M{wx} 557 h60" stroke="#4A200F" stroke-width="5"/>')
        b.append(diyo(wx + 10, 606, 0.7) + diyo(wx + 50, 606, 0.7))
    for k in range(-math.ceil(ext / 140), 0):
        b.append(garland((nx + 20 + k * 140, 478), (nx + 160 + k * 140, 478), 26, S.EXT, 5.5))
    for k in range(2):
        b.append(garland((nx + 20 + k * 140, 478), (nx + 160 + k * 140, 478), 26, rnd, 5.5))
    b.append(f'<rect x="{f(-20 - ext)}" y="700" width="{f(560 + ext)}" height="50" fill="#6B3A3A"/><rect x="{f(-20 - ext)}" y="700" width="{f(560 + ext)}" height="8" fill="#8A4E44"/>')
    b.append(f'<rect x="{f(-20 - ext)}" y="750" width="{f(600 + ext)}" height="40" fill="#5A3034"/><rect x="{f(-20 - ext)}" y="750" width="{f(600 + ext)}" height="7" fill="#7A443E"/>')
    for k in range(9):
        b.append(diyo(30 + k * 58, 704, 0.9))
    for k in range(10):
        b.append(diyo(20 + k * 58, 754, 0.95))
    for k in range(math.ceil(ext / 58)):
        # (over the margin, set out a little so that their glow stays off the picture)
        b.append(diyo(-40 - k * 58, 704, 0.9) + diyo(-42 - k * 58, 754, 0.95))
    b.append(garland((-10, 712), (540, 712), 30, rnd, 6))
    if ext:
        b.append(garland((-10 - ext - 40, 712), (-10, 712), 30 * (ext + 40) / 550, S.EXT, 6))
    b.append('<rect x="396" y="230" width="8" height="470" fill="#7A5A3A"/><rect x="368" y="238" width="64" height="5" fill="#7A5A3A"/>')
    b.append(akash_deep(372, 308, 1.0))
    b.append(akash_deep(428, 286, 0.8))
    # Right: a two-storey Newari house, brick, its carved window glowing above a lit doorway.
    hx, hy, hw, hb = 1470, 440, 500, 790
    b.append(f'<rect x="{hx}" y="{hy}" width="{hw}" height="{hb - hy}" fill="#6B3430"/>')
    b.append(f'<path d="{"".join(f"M{hx} {y} h{hw}" for y in range(hy + 14, hb, 14))}" stroke="#4E2422" stroke-width="1.6" opacity="0.6"/>')
    b.append(glow(defs, "wallglow", "#FFB547", 0.35, 1700, 600, 320, 260))
    # The eave: a dark carved underside on struts, the tiled roof's edge above it.
    b.append(f'<path d="M{hx - 40} {hy} L{hx + hw + 40} {hy} L{hx + hw + 20} {hy - 30} L{hx - 20} {hy - 30} Z" fill="#3A1C14"/>')
    b.append(f'<path d="M{hx - 50} {hy - 30} L{hx + hw + 50} {hy - 30} L{hx + hw + 60} {hy - 48} L{hx - 60} {hy - 48} Z" fill="#7A3A22"/>')
    for k in range(7):
        sx = hx + 24 + k * 76
        b.append(f'<path d="M{sx} {hy} L{sx + 8} {hy} L{sx + 30} {hy + 56} L{sx + 22} {hy + 56} Z" fill="#3A1C14"/>')
    # Upper floor: the carved window (a three-part aankhi jhyal) with a lattice lit amber.
    wx, wy, ww, wh = 1580, 500, 260, 120
    b.append(f'<rect x="{wx - 22}" y="{wy - 22}" width="{ww + 44}" height="{wh + 44}" fill="#2E140C"/>')
    b.append(f'<rect x="{wx - 32}" y="{wy - 32}" width="{ww + 64}" height="12" fill="#3A1C14"/>')
    b.append(f'<rect x="{wx}" y="{wy}" width="{ww}" height="{wh}" fill="#FFB547"/>')
    lat = "".join(f"M{f(wx + k * 16)} {wy} L{f(wx + k * 16 + wh)} {wy + wh} M{f(wx + k * 16 + wh)} {wy} L{f(wx + k * 16)} {wy + wh} " for k in range(-9, 18))
    b.append(f'<clipPath id="wclip"><rect x="{wx}" y="{wy}" width="{ww}" height="{wh}"/></clipPath><path d="{lat}" stroke="#4A200F" stroke-width="4.5" clip-path="url(#wclip)"/>')
    b.append(f'<path d="M{wx + ww / 3} {wy} v{wh} M{wx + 2 * ww / 3} {wy} v{wh}" stroke="#2E140C" stroke-width="12"/>')
    b.append(f'<rect x="{wx - 34}" y="{wy + wh + 18}" width="{ww + 68}" height="12" fill="#3A1C14"/>')
    for k in range(7):
        b.append(diyo(wx - 14 + k * 48, wy + wh + 18, 0.8))
    # The floor line, then the ground floor: a lit doorway between two small lit windows.
    b.append(f'<rect x="{hx}" y="660" width="{hw}" height="10" fill="#3A1C14"/>')
    b.append(f'<rect x="1655" y="680" width="100" height="{hb - 680}" fill="#2E140C"/><rect x="1666" y="692" width="78" height="{hb - 692}" fill="#FFC86A"/>')
    b.append(f'<rect x="1666" y="692" width="24" height="{hb - 692}" fill="#C77A2E"/>')
    for wx2 in (1530, 1830):
        b.append(f'<rect x="{wx2 - 10}" y="690" width="70" height="60" fill="#2E140C"/><rect x="{wx2}" y="700" width="50" height="40" fill="#FFB547"/><path d="M{wx2 + 25} 700 v40" stroke="#4A200F" stroke-width="4"/>')
    # Garlands under the eave, strands down the corners and round the door.
    for k in range(4):
        b.append(garland((hx + k * 125, hy + 6), (hx + (k + 1) * 125, hy + 6), 34, rnd, 6))
    for x in (hx + 26, hx + hw - 26):
        b.append(strand(x, hy + 10, hy + 220, 6))
    b.append(garland((1650, 684), (1760, 684), 24, rnd, 5.5))
    b.append(rangoli(1705, 910, 150))
    for k in range(8):
        b.append(diyo(1470 + k * 66, 1010 - abs(k - 3.5) * 6, 1.0))
    b.append(lift(defs, "nlift", "#7E4E8A", 0.35, 960, 580, 640, 470))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 5. Prayer Flag Pass

def yak(x, y, s, flip=False, col="#3A2C26", col2="#5A463B"):
    """A yak grazing: a heavy body with a hump, a shaggy skirt, the head low, upturned horns."""
    sx = -1 if flip else 1
    g = [f'<g transform="translate({f(x)} {f(y)}) scale({f(sx * s)} {f(s)})">']
    g.append('<path d="M-70 -40 C-60 -84 30 -96 52 -70 C70 -64 84 -40 80 -10 L72 18 L-66 18 C-80 0 -80 -24 -70 -40 Z" fill="' + col + '"/>')
    skirt = "".join(f"M{x0} 4 L{x0 + 6} 34 L{x0 + 12} 6 " for x0 in range(-68, 70, 12))
    g.append(f'<path d="{skirt}" fill="{col}"/>')
    for lx in (-50, -30, 40, 58):
        g.append(f'<rect x="{lx}" y="10" width="11" height="40" rx="4" fill="{col}"/>')
    g.append('<path d="M60 -50 C90 -50 110 -20 112 8 C112 22 98 30 88 22 C80 10 70 -10 56 -24 Z" fill="' + col + '"/>')
    g.append('<path d="M88 -40 C80 -60 90 -72 104 -70 C96 -64 94 -54 98 -44 Z M100 -38 C108 -56 124 -60 132 -50 C122 -50 114 -44 110 -34 Z" fill="#E9DCC6"/>')
    g.append('<path d="M-40 -80 C0 -92 30 -88 46 -72 C20 -78 -10 -76 -40 -80 Z" fill="' + col2 + '" opacity="0.8"/>')
    g.append('<path d="M-70 -30 C-86 -20 -90 0 -84 18" stroke="' + col + '" stroke-width="8" fill="none" stroke-linecap="round"/>')
    g.append('</g>')
    return "".join(g)


def cairn(x, y, s, rnd):
    """A lhatse: flat stones piled into a cairn, a pole rising from it, a white scarf tied on."""
    out = []
    yy = y
    for k in range(7):
        w = (130 - k * 15) * s
        h = rnd.uniform(18, 26) * s
        out.append(f'<rect x="{f(x - w / 2 + rnd.uniform(-6, 6) * s)}" y="{f(yy - h)}" width="{f(w)}" height="{f(h)}" rx="{f(h * 0.45)}" fill="{rnd.choice(["#9E948C", "#B3A99F", "#8A8079", "#C2B8AD"])}"/>')
        yy -= h * 0.82
    out.append(f'<rect x="{f(x - 4 * s)}" y="{f(yy - 230 * s)}" width="{f(8 * s)}" height="{f(240 * s)}" fill="#6A4B34"/>')
    out.append(f'<path d="M{f(x + 3 * s)} {f(yy - 190 * s)} q{f(30 * s)} {f(8 * s)} {f(48 * s)} {f(30 * s)} q{f(-24 * s)} {f(-6 * s)} {f(-48 * s)} {f(-14 * s)} Z" fill="#F6F1E6"/>')
    return "".join(out), (x, yy - 226 * s)


def flag_pole(x, top, foot, rnd):
    """A pole the flag strings are tied to over the margin, a few stones round its foot (rnd: EXT)."""
    out = [f'<rect x="{f(x - 3)}" y="{f(top - 4)}" width="6" height="{f(foot - top + 4)}" fill="#6A4B34"/>']
    yy = foot + 6
    for k in range(3):
        w, h = 30 - k * 8, rnd.uniform(9, 13)
        out.append(f'<rect x="{f(x - w / 2 + rnd.uniform(-2, 2))}" y="{f(yy - h)}" width="{f(w)}" height="{f(h)}" rx="{f(h * 0.45)}" fill="{rnd.choice(["#9E948C", "#B3A99F", "#8A8079", "#C2B8AD"])}"/>')
        yy -= h * 0.8
    return "".join(out)


def flag_pass():
    """A high pass in the Himalaya: strings of prayer flags streaming from a stone cairn, snow peaks
    behind, a trail over the saddle, yaks grazing the slope, clouds racing by."""
    rnd = random.Random(505)
    defs, b = [], []
    defs.append(lg("psky", [(0, "#8EBBE6", 1), (0.45, "#BCD8F0", 1), (0.75, "#E6F0F6", 1), (1, "#F3F6F4", 1)], box=FULL))
    b.append(S.full("url(#psky)"))
    for cx, cy, sc in [(330, 250, 0.9), (1180, 200, 0.6), (1530, 520, 0.8), (760, 470, 0.5)]:
        b.append(S.cloud(cx, cy, sc, rnd))
    if S.BX:
        b.append(S.cloud(-200, 300, 0.7, S.EXT))
        b.append(S.cloud(2120, 170, 0.6, S.EXT))
    # Great snow peaks (and, in the margin, one more each side, behind them).
    if S.BX:
        b.append(S.peak(-210, 300, 280, 190, 640, S.EXT, "#9A8F8E", "#7A7174", "#FFFFFF", "#D8E3F0", 0.5))
        b.append(S.peak(2150, 290, 210, 300, 640, S.EXT, "#9A8F8E", "#7A7174", "#FFFFFF", "#D8E3F0", 0.5))
    for px, py, wl, wr, base in [(240, 260, 300, 330, 640), (700, 330, 280, 300, 640), (1160, 300, 300, 320, 640), (1660, 250, 340, 330, 640)]:
        b.append(S.peak(px, py, wl, wr, base, rnd, "#9A8F8E", "#7A7174", "#FFFFFF", "#D8E3F0", 0.5))
    b.append(S.band(540, 650, "#F1F5F6", 0, 0.5, 0.0, "pmist", defs))
    b.append(hill([(-40, 630), (300, 618), (640, 646), (960, 636), (1300, 650), (1640, 622), (1960, 632)], "#B7AFB2"))
    b.append(hill([(-40, 676), (420, 660), (800, 690), (1120, 680), (1500, 668), (1960, 676)], "#A99E9A"))
    # The pass: two shoulders of brown hillside meeting in a saddle behind the menu.
    b.append(hill([(-40, 610), (240, 600), (520, 660), (760, 720), (960, 740), (1160, 720), (1420, 650), (1700, 600), (1960, 590)], "#C3A982"))
    shoulder = [(-40, 700), (300, 690), (600, 760), (900, 800), (1200, 790), (1500, 720), (1760, 680), (1960, 670)]
    b.append(hill(shoulder, "#B0956D"))
    # Rock outcrops and scree.
    for _ in range(26):
        x = rnd.choice([rnd.uniform(40, 520), rnd.uniform(1380, 1880)])
        y = 700 + rnd.uniform(0, 80)
        w = rnd.uniform(16, 40)
        b.append(f'<path d="M{f(x)} {f(y)} l{f(w * 0.3)} {f(-w * 0.5)} l{f(w * 0.5)} {f(-w * 0.1)} l{f(w * 0.3)} {f(w * 0.6)} Z" fill="{rnd.choice(["#8A7A68", "#9C8B77", "#7A6B5B"])}"/>')
    slope = [(-40, 820), (340, 800), (720, 860), (1000, 890), (1320, 860), (1660, 790), (1960, 780)]
    b.append(hill(slope, "#9C8160"))
    # The trail over the saddle.
    trail = "M700 1100 C 760 1000 860 930 930 880 C 990 840 1060 810 1120 790 L 1140 796 C 1080 818 1010 852 958 892 C 900 940 820 1010 790 1100 Z"
    if bleed():
        d, _, _ = run_on_d([(700, 1100), (790, 1100)], trail[:-2], (760, 1000), (820, 1010))
        trail = d + " Z"
    b.append(f'<path d="{trail}" fill="#D9C4A0" opacity="0.85"/>')
    bank_l, bank_r = [(-40, 930), (260, 900), (520, 960), (700, 1100)], [(1200, 1100), (1400, 950), (1700, 900), (1960, 900)]
    b.append(hill(bank_l, "#8A7052"))
    b.append(hill(bank_r, "#8A7052"))
    # Tussocks.
    tufts = []
    for _ in range(60):
        x = rnd.choice([rnd.uniform(0, 640), rnd.uniform(1260, 1920)])
        y = rnd.uniform(860, 1080)
        for k in range(4):
            tufts.append(leaf_d(x, y, rnd.uniform(14, 24), -90 + (k - 1.5) * 22, 0.14))
    if bleed():
        lb, rb, sl = ext_line(bank_l), ext_line(bank_r), ext_line(slope)
        for x, y in bleed_points(60, 0, 640, 860, 1080, 26) + bleed_points(60, 1260, W, 860, 1080, 26):
            if y < P.along(sl, x) + 20 or (y > H and y < P.along(lb if x < 960 else rb, x) + 12) or (y > H and 640 < x < 1260):
                continue
            for k in range(4):
                tufts.append(leaf_d(x, y, S.EXT.uniform(14, 24), -90 + (k - 1.5) * 22, 0.14))
    b.append(f'<path d="{"".join(tufts)}" fill="#7C8A4E" opacity="0.85"/>')
    # The cairn on the right and the flag strings streaming from its pole.
    c, top = cairn(1600, 760, 1.0, rnd)
    b.append(c)
    if S.BX:
        # Over the margin the strings that leave the picture are tied to a pole on each side.
        b.append(flag_pole(1960, 516, P.along(ext_line(slope), 1960) + 6, S.EXT))
        b.append(flag_pole(-20, 436, P.along(ext_line(shoulder), -20) + 6, S.EXT))
        b.append(S.flags(S.string((1960, 518), (W + S.BX + 40, 560), 30), S.FLAGS[2:] + S.FLAGS[:2], S.EXT, 0.85))
        b.append(S.flags(S.string((-20, 438), (-S.BX - 40, 470), 26), S.FLAGS[1:] + S.FLAGS[:1], S.EXT, 0.75))
    for end, sag in [((1960, 520), 30), ((1960, 640), 40), ((1960, 780), 50), ((1440, 520), 20), ((1300, 600), 30)]:
        b.append(S.flags(S.string(top, end, sag), S.FLAGS, rnd, 0.85))
    # A second, smaller pole on the left with its own strings.
    b.append('<rect x="452" y="470" width="6" height="250" fill="#6A4B34"/>')
    for end, sag in [((-20, 440), 30), ((-20, 560), 40), ((620, 600), 20)]:
        b.append(S.flags(S.string((455, 474), end, sag), S.FLAGS, rnd, 0.75))
    # Yaks on the slope.
    b.append(yak(260, 830, 0.8))
    b.append(yak(450, 860, 0.62, True, "#4A3A30"))
    b.append(yak(1760, 920, 0.7, True))
    b.append(lift(defs, "plift", "#FBFCF8", 0.5, 960, 600, 600, 450))
    b.append(lift(defs, "pfoot", "#F3EBDB", 0.9, 960, 1004, 560, 100))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 6. Mithila Garden

INK = "#2B1B14"
VERM, TURM, LEAF, INDIGO, PINK, CREAM = "#D9472B", "#F2B630", "#4F8A3C", "#2E3E86", "#E77FA0", "#F6EBD5"


def hatch_ellipse(cx, cy, rx, ry, ang, step, col=INK, w=1.6):
    lines = []
    y = -ry + step / 2
    while y < ry:
        hw = rx * math.sqrt(max(0, 1 - (y / ry) ** 2))
        lines.append(f"M{f(-hw)} {f(y)} L{f(hw)} {f(y)}")
        y += step
    return f'<g transform="translate({f(cx)} {f(cy)}) rotate({f(ang)})"><path d="{"".join(lines)}" stroke="{col}" stroke-width="{f(w)}"/></g>'


def m_leaf(x, y, L, ang, fill=LEAF):
    a = math.radians(ang)
    veins = []
    for k in range(1, 6):
        u = L * k / 6.5
        for sgn in (-1, 1):
            vx, vy = u + L * 0.06, sgn * L * 0.1
            veins.append(f"M{f(x + u * math.cos(a))} {f(y + u * math.sin(a))} L{f(x + vx * math.cos(a) - vy * math.sin(a))} {f(y + vx * math.sin(a) + vy * math.cos(a))}")
    return (f'<path d="{leaf_d(x, y, L, ang, 0.32)}" fill="{fill}" stroke="{INK}" stroke-width="2.6"/>'
            f'<path d="M{f(x)} {f(y)} L{f(x + L * 0.92 * math.cos(a))} {f(y + L * 0.92 * math.sin(a))} {"".join(veins)}" stroke="{INK}" stroke-width="1.4" fill="none"/>')


def m_fish(x, y, L, ang, body, fin):
    """A Mithila fish: a plump body with scales drawn as arcs, a forked tail, a big eye."""
    g = [f'<g transform="translate({f(x)} {f(y)}) rotate({f(ang)})">']
    g.append(f'<path d="M{f(L * 0.4)} 0 L{f(L * 0.62)} {f(-L * 0.2)} L{f(L * 0.58)} 0 L{f(L * 0.62)} {f(L * 0.2)} Z" fill="{fin}" stroke="{INK}" stroke-width="2.6" stroke-linejoin="round"/>')
    g.append(f'<path d="M{f(-L * 0.5)} 0 C{f(-L * 0.3)} {f(-L * 0.3)} {f(L * 0.2)} {f(-L * 0.28)} {f(L * 0.42)} 0 C{f(L * 0.2)} {f(L * 0.28)} {f(-L * 0.3)} {f(L * 0.3)} {f(-L * 0.5)} 0 Z" fill="{body}" stroke="{INK}" stroke-width="2.8"/>')
    sc = ""
    for i in range(4):
        for j in range(-2, 3):
            sx = -L * 0.2 + i * L * 0.12
            sy = j * L * 0.07
            if abs(sy) > L * 0.2 * (1 - abs(sx) / (L * 0.5)) + 2:
                continue
            sc += f"M{f(sx)} {f(sy - L * 0.035)} A{f(L * 0.04)} {f(L * 0.04)} 0 0 1 {f(sx)} {f(sy + L * 0.035)} "
    g.append(f'<path d="{sc}" stroke="{INK}" stroke-width="1.6" fill="none"/>')
    g.append(f'<path d="M{f(-L * 0.28)} {f(-L * 0.17)} Q{f(-L * 0.24)} 0 {f(-L * 0.28)} {f(L * 0.17)}" stroke="{INK}" stroke-width="2" fill="none"/>')
    g.append(f'<circle cx="{f(-L * 0.38)}" cy="{f(-L * 0.04)}" r="{f(L * 0.05)}" fill="{CREAM}" stroke="{INK}" stroke-width="2"/><circle cx="{f(-L * 0.38)}" cy="{f(-L * 0.04)}" r="{f(L * 0.022)}" fill="{INK}"/>')
    g.append(f'<path d="M{f(-L * 0.05)} {f(-L * 0.23)} Q{f(L * 0.08)} {f(-L * 0.36)} {f(L * 0.2)} {f(-L * 0.2)} Z" fill="{fin}" stroke="{INK}" stroke-width="2"/>')
    g.append('</g>')
    return "".join(g)


def m_peacock(x, y, s):
    """A Mithila peacock facing right: a hatched indigo body, a long neck, a crest, a sweeping tail of
    patterned feathers with eyes."""
    out = [f'<g transform="translate({f(x)} {f(y)}) scale({f(s)})">']
    # Tail: a painted mass behind the feathers, hatched, then the feathers sweeping down and back.
    ends = [(math.cos(math.radians(112 + k * 10)) * (280 - abs(k - 3) * 14), math.sin(math.radians(112 + k * 10)) * (280 - abs(k - 3) * 14) + 40) for k in range(7)]
    out.append(f'<path d="M-10 30 {" ".join(f"L{f(x)} {f(y)}" for x, y in ends)} L-30 60 Z" fill="#7DB04F" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>')
    for k in range(7):
        a = math.radians(112 + k * 10)
        L = 280 - abs(k - 3) * 14
        ex, ey = math.cos(a) * L, math.sin(a) * L + 40
        out.append(f'<path d="M-20 40 Q{f(ex * 0.5 - 10)} {f(ey * 0.5 + 30)} {f(ex)} {f(ey)}" stroke="{INK}" stroke-width="3" fill="none"/>')
        out.append(f'<g transform="translate({f(ex)} {f(ey)}) rotate({f(math.degrees(a))})"><path d="M-30 0 C-10 -30 30 -26 40 0 C30 26 -10 30 -30 0 Z" fill="{LEAF}" stroke="{INK}" stroke-width="2.6"/>'
                   f'<ellipse cx="8" cy="0" rx="20" ry="15" fill="{TURM}" stroke="{INK}" stroke-width="2"/><ellipse cx="10" cy="0" rx="10" ry="8" fill="{INDIGO}" stroke="{INK}" stroke-width="1.6"/></g>')
        for t in (0.3, 0.55, 0.78):
            px, py = ex * t, ey * t + 40 * (1 - t)
            out.append(f'<path d="M{f(px)} {f(py)} l{f(-8 * math.sin(a))} {f(8 * math.cos(a))} M{f(px)} {f(py)} l{f(8 * math.sin(a))} {f(-8 * math.cos(a))}" stroke="{INK}" stroke-width="1.6"/>')
    # Legs on the branch.
    out.append(f'<path d="M10 90 L4 150 M34 88 L40 150" stroke="{INK}" stroke-width="4" stroke-linecap="round"/>')
    out.append(f'<path d="M-40 156 Q60 140 170 160" stroke="{INK}" stroke-width="10" fill="none" stroke-linecap="round"/>')
    out.append(f'<path d="M-40 156 Q60 140 170 160" stroke="#8A5A34" stroke-width="5" fill="none" stroke-linecap="round"/>')
    # Body.
    out.append(f'<path d="M-40 40 C-40 -30 40 -40 70 -10 C90 20 70 90 20 100 C-20 104 -40 80 -40 40 Z" fill="{INDIGO}" stroke="{INK}" stroke-width="3"/>')
    out.append(hatch_ellipse(16, 40, 40, 44, -30, 9, CREAM, 1.4))
    out.append(f'<path d="M-30 40 C-10 10 30 10 50 40 C30 30 -10 30 -30 40 Z" fill="{TURM}" stroke="{INK}" stroke-width="2"/>')
    # Neck and head.
    out.append(f'<path d="M40 -10 C50 -60 60 -110 80 -140 C92 -156 116 -150 118 -134 C120 -120 104 -116 98 -100 C88 -70 80 -30 70 -6 Z" fill="{INDIGO}" stroke="{INK}" stroke-width="3"/>')
    for k in range(5):
        out.append(f'<path d="M{f(52 + k * 4)} {f(-30 - k * 20)} l14 6" stroke="{CREAM}" stroke-width="1.6"/>')
    out.append(f'<path d="M116 -138 L140 -128 L116 -124 Z" fill="{TURM}" stroke="{INK}" stroke-width="2"/>')
    out.append(f'<circle cx="102" cy="-136" r="7" fill="{CREAM}" stroke="{INK}" stroke-width="2"/><circle cx="103" cy="-136" r="3" fill="{INK}"/>')
    for k, a in enumerate((-110, -90, -70)):
        ra = math.radians(a)
        ex, ey = 96 + math.cos(ra) * 40, -150 + math.sin(ra) * 40
        out.append(f'<path d="M96 -150 L{f(ex)} {f(ey)}" stroke="{INK}" stroke-width="2"/><circle cx="{f(ex)}" cy="{f(ey)}" r="6" fill="{VERM}" stroke="{INK}" stroke-width="2"/>')
    out.append('</g>')
    return "".join(out)


def m_sun(x, y, r):
    out = []
    for k in range(16):
        a = math.tau * k / 16
        a1, a2 = a - 0.13, a + 0.13
        out.append(f'<path d="M{f(x + math.cos(a1) * r * 1.05)} {f(y + math.sin(a1) * r * 1.05)} L{f(x + math.cos(a) * r * 1.55)} {f(y + math.sin(a) * r * 1.55)} L{f(x + math.cos(a2) * r * 1.05)} {f(y + math.sin(a2) * r * 1.05)} Z" fill="{VERM if k % 2 else TURM}" stroke="{INK}" stroke-width="2.4" stroke-linejoin="round"/>')
    out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{TURM}" stroke="{INK}" stroke-width="3"/>')
    out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 0.82)}" fill="none" stroke="{INK}" stroke-width="1.6" stroke-dasharray="3 5"/>')
    for ex in (-0.32, 0.32):
        out.append(f'<path d="M{f(x + (ex - 0.16) * r)} {f(y - 0.1 * r)} Q{f(x + ex * r)} {f(y - 0.3 * r)} {f(x + (ex + 0.16) * r)} {f(y - 0.1 * r)} Q{f(x + ex * r)} {f(y + 0.04 * r)} {f(x + (ex - 0.16) * r)} {f(y - 0.1 * r)} Z" fill="{CREAM}" stroke="{INK}" stroke-width="2"/>'
                   f'<circle cx="{f(x + ex * r)}" cy="{f(y - 0.12 * r)}" r="{f(0.05 * r)}" fill="{INK}"/>')
    out.append(f'<path d="M{f(x)} {f(y - 0.05 * r)} L{f(x - 0.06 * r)} {f(y + 0.18 * r)} L{f(x + 0.04 * r)} {f(y + 0.2 * r)}" stroke="{INK}" stroke-width="2" fill="none"/>')
    out.append(f'<path d="M{f(x - 0.22 * r)} {f(y + 0.38 * r)} Q{f(x)} {f(y + 0.52 * r)} {f(x + 0.22 * r)} {f(y + 0.38 * r)}" stroke="{VERM}" stroke-width="5" fill="none" stroke-linecap="round"/>')
    return "".join(out)


def m_lotus(x, y, s):
    out = [f'<g transform="translate({f(x)} {f(y)}) scale({f(s)})">']
    for a, col in [(-60, PINK), (60, PINK), (-30, VERM), (30, VERM), (0, PINK)]:
        out.append(f'<g transform="rotate({a})"><path d="M0 0 C-22 -30 -14 -70 0 -86 C14 -70 22 -30 0 0 Z" fill="{col}" stroke="{INK}" stroke-width="2.6"/><path d="M0 -8 L0 -70" stroke="{INK}" stroke-width="1.4"/></g>')
    out.append(f'<path d="M-40 4 Q0 26 40 4 Q0 12 -40 4 Z" fill="{LEAF}" stroke="{INK}" stroke-width="2.4"/>')
    out.append('</g>')
    return "".join(out)


def mithila():
    """The garden painted Mithila-style, as on a courtyard wall: bold ink outlines and fine line-work in
    vermilion, turmeric, leaf green and indigo on cream; a peacock and a smiling sun on the left, a tree
    of life with parrots and a pair of fish on the right. Figma's version had a painted border round the
    1920 x 1080 picture; the game's picture has none (round the screen, it would sit inside the picture on
    21:9 and 4:3), so the paper runs on to every edge and the tree's trunk goes down off the frame."""
    rnd = random.Random(606)
    defs, b = [], []
    b.append(S.full(CREAM))
    # Paper speckle.
    sp = "".join(f"M{f(rnd.uniform(0, W))} {f(rnd.uniform(0, H))} h1.6v1.6h-1.6Z" for _ in range(1600))
    sp += "".join(f"M{f(x)} {f(y)} h1.6v1.6h-1.6Z" for x, y in bleed_points(1600, 0, W, 0, H, 2))
    b.append(f'<path d="{sp}" fill="#C9B48E" opacity="0.5"/>')
    b.append(glow(defs, "mlift", "#FFF8EA", 0.9, 960, 560, 640, 470))
    # Left: the sun, the peacock on its branch, lotus and leaves below.
    b.append(m_sun(200, 330, 62))
    b.append(m_peacock(400, 520, 1.1))
    for x, y, a in [(600, 700, -150), (640, 760, -120), (110, 560, -40)]:
        b.append(m_leaf(x, y, 90, a))
    b.append(m_lotus(620, 880, 0.9))
    # Right: the tree of life with parrots, the fish pair at its foot.
    tx = 1760
    # The trunk runs on down off the frame at its foot's slopes (the same lines with bleed or without).
    foot = H + S.BY + 20
    b.append(f'<path d="M{f(tx - 30 - 0.063 * (foot - 1058))} {f(foot)} L{tx - 30} 1058 C{tx - 20} 900 {tx - 40} 760 {tx - 10} 620 L{tx + 14} 620 C{tx + 30} 760 {tx + 24} 900 {tx + 40} 1058 L{f(tx + 40 + 0.101 * (foot - 1058))} {f(foot)} Z" fill="#8A5A34" stroke="{INK}" stroke-width="3"/>')
    bark = "".join(f"M{tx - 16 + rnd.uniform(-4, 4)} {y} q12 -10 26 0 " for y in range(640, 1050, 26))
    low = random.Random(61)
    bark += "".join(f"M{f(tx - 16 + low.uniform(-4, 4) + (y - 1058) * 0.02)} {y} q12 -10 26 0 " for y in range(1056, int(foot) - 10, 26))
    b.append(f'<path d="{bark}" stroke="{INK}" stroke-width="1.6" fill="none"/>')
    branches = [((tx, 640), (tx - 200, 520)), ((tx, 660), (tx + 210, 540)), ((tx, 700), (tx - 230, 650)), ((tx, 720), (tx + 230, 690)), ((tx, 630), (tx + 20, 470))]
    for (a, c) in branches:
        b.append(f'<path d="M{a[0]} {a[1]} Q{(a[0] + c[0]) / 2} {min(a[1], c[1]) - 30} {c[0]} {c[1]}" stroke="{INK}" stroke-width="12" fill="none" stroke-linecap="round"/>'
                 f'<path d="M{a[0]} {a[1]} Q{(a[0] + c[0]) / 2} {min(a[1], c[1]) - 30} {c[0]} {c[1]}" stroke="#8A5A34" stroke-width="7" fill="none" stroke-linecap="round"/>')
    for (a, c) in branches:
        for k in range(5):
            t = 0.35 + k * 0.15
            px, py = a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t - 20 * math.sin(math.pi * t)
            ang = math.degrees(math.atan2(c[1] - a[1], c[0] - a[0]))
            b.append(m_leaf(px, py, 60, ang - 50 + rnd.uniform(-10, 10), LEAF if k % 2 else "#6FA04E"))
            if k % 2 == 0:
                b.append(m_leaf(px, py, 54, ang + 50 + rnd.uniform(-10, 10)))
        b.append(f'<circle cx="{c[0]}" cy="{c[1]}" r="16" fill="{VERM}" stroke="{INK}" stroke-width="2.6"/><circle cx="{c[0]}" cy="{c[1]}" r="6" fill="{TURM}" stroke="{INK}" stroke-width="1.6"/>')
    for px, py, flip in [(tx - 120, 560, 1), (tx + 120, 590, -1)]:
        b.append(f'<g transform="translate({px} {py}) scale({flip} 1)"><path d="M-30 0 C-30 -30 10 -36 24 -18 C34 -4 24 22 0 26 C-20 28 -30 16 -30 0 Z" fill="{LEAF}" stroke="{INK}" stroke-width="2.6"/>'
                 f'<path d="M-26 10 L-60 40 L-50 44 L-20 20 Z" fill="{LEAF}" stroke="{INK}" stroke-width="2.4"/><path d="M20 -16 Q34 -14 32 0 Q26 -4 20 -6 Z" fill="{VERM}" stroke="{INK}" stroke-width="2"/>'
                 f'<circle cx="12" cy="-12" r="4" fill="{CREAM}" stroke="{INK}" stroke-width="1.6"/><path d="M-20 4 Q0 -6 16 6" stroke="{INK}" stroke-width="1.6" fill="none"/></g>')
    b.append(m_fish(1385, 992, 170, 180, VERM, TURM))
    b.append(m_fish(1565, 992, 170, 0, TURM, VERM))
    b.append(m_lotus(1870, 1000, 0.6))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- 7. Phewa Moonlight

def fishtail(px, py, base, rnd):
    """Machhapuchhre's twin summit, moonlit."""
    out = []
    left = S.jag((px - 30, py + 20), (px - 300, base), 0.05, 5, rnd)
    right = S.jag((px + 24, py), (px + 320, base), 0.05, 5, rnd)
    body = [(px - 30, py + 20)] + left[1:] + [(px + 320, base)] + right[::-1][:-1] + [(px + 24, py), (px + 2, py + 40)]
    out.append(f'<path d="{poly(body)}" fill="#46578A"/>')
    snow = [(px - 30, py + 20), (px - 80, py + 120), (px - 40, py + 150), (px - 10, py + 110), (px + 20, py + 170), (px + 60, py + 130), (px + 90, py + 160), (px + 24, py), (px + 2, py + 40)]
    out.append(f'<path d="{poly(snow)}" fill="#D7E1F3"/>')
    out.append(f'<path d="{poly([(px + 24, py), (px + 90, py + 160), (px + 60, py + 130), (px + 34, py + 150), (px + 10, py + 60)])}" fill="#A6B6DA"/>')
    return "".join(out)


def moonlight():
    """A still night on Phewa lake: a full moon over Machhapuchhre, the Milky Way, wooded hills with a
    few lit windows, painted doonga moored with lanterns, the moon's path across the water."""
    rnd = random.Random(707)
    defs, b = [], []
    defs.append(lg("msky", [(0, "#101A3C", 1), (0.4, "#1E2D5E", 1), (0.62, "#33467C", 1), (1, "#3D5288", 1)], box=FULL))
    b.append(S.full("url(#msky)"))
    # The Milky Way: a soft band and a dust of small stars across it.
    b.append(f'<g transform="rotate(-18 960 300)">{glow(defs, "milky", "#C9C6E8", 0.22, 960, 260, 1100, 120)}</g>')
    b.append(stars(rnd, 260, 0, W, 0, 520, (960, 470, 520, 260)))
    b.append(bleed_stars(260, 0, W, 0, 520))
    mw = []
    for _ in range(260):
        t = rnd.uniform(-1, 1)
        x = 960 + t * 1100 * math.cos(math.radians(-18)) + rnd.gauss(0, 50) * math.sin(math.radians(18))
        y = 260 + t * 1100 * math.sin(math.radians(-18)) + rnd.gauss(0, 50) * math.cos(math.radians(18))
        if 0 < x < W and 0 < y < 520:
            mw.append(f"M{f(x)} {f(y)} h1.4v1.4h-1.4Z")
    if bleed():
        # The band's dust over the margin, where the glow still shows.
        for _ in range(350):
            t = S.EXT.uniform(-1.3, 1.3)
            x = 960 + t * 1100 * math.cos(math.radians(-18)) + S.EXT.gauss(0, 50) * math.sin(math.radians(18))
            y = 260 + t * 1100 * math.sin(math.radians(-18)) + S.EXT.gauss(0, 50) * math.cos(math.radians(18))
            if not (-3 < x < W + 3 and -3 < y) and -S.BX < x < W + S.BX and -S.BY < y < 520 and S.EXT.random() < 1.2 - abs(t) * 0.75:
                mw.append(f"M{f(x)} {f(y)} h1.4v1.4h-1.4Z")
    b.append(f'<path d="{"".join(mw)}" fill="#FFFFFF" opacity="0.7"/>')
    for x, y, r in [(220, 240, 9), (700, 120, 7), (1180, 90, 8), (1600, 470, 6), (430, 430, 6)]:
        b.append(sparkle(x, y, r, "#FFFFFF", 0.9))
    # The moon.
    b.append(glow(defs, "moonhalo", "#E6ECFF", 0.35, 1460, 290, 220, 220))
    b.append('<circle cx="1460" cy="290" r="54" fill="#F6F1DE"/>')
    b.append('<circle cx="1442" cy="276" r="12" fill="#E6DEC4" opacity="0.8"/><circle cx="1478" cy="306" r="9" fill="#E6DEC4" opacity="0.7"/><circle cx="1470" cy="270" r="6" fill="#E6DEC4" opacity="0.6"/>')
    # The range, moonlit: the fishtail on the left, the Annapurnas spread behind (and on over the margin).
    if S.BX:
        b.append(S.peak(-180, 370, 220, 150, 600, S.EXT, "#3E4F82", "#33426F", "#C9D5EE", "#94A5CC", 0.45, 0.06))
        b.append(S.peak(2110, 360, 160, 250, 600, S.EXT, "#3E4F82", "#33426F", "#C9D5EE", "#94A5CC", 0.45, 0.06))
    for px, py, wl, wr in [(360, 360, 280, 260), (980, 330, 320, 300), (1260, 360, 260, 280), (1760, 380, 280, 260)]:
        b.append(S.peak(px, py, wl, wr, 600, rnd, "#3E4F82", "#33426F", "#C9D5EE", "#94A5CC", 0.45, 0.06))
    b.append(fishtail(640, 250, 600, rnd))
    b.append(S.band(470, 640, "#5A6C9E", 0, 0.5, 0.2, "mmist", defs))
    # Wooded hills down to the water, a few lit windows.
    b.append(hill(wave(-40, W + 40, 600, 16, 14, rnd), "#22305C"))
    shore = [(-40, 610), (200, 590), (420, 620), (640, 650), (900, 660), (1940, 660)]
    b.append(hill(shore, "#1A264D"))
    for _ in range(40):
        x = rnd.uniform(0, 620)
        y = P.along([(-40, 610), (200, 590), (420, 620), (640, 650)], x) + rnd.uniform(4, 20)
        b.append(S.conifer(x, y, rnd.uniform(30, 60), rnd.choice(["#141E40", "#18234A"]), rnd))
    b.append(bleed_firs(ext_line(shore), -S.BX - 20, -14, int(S.BX * 40 / 620), ["#141E40", "#18234A"], 30, 60, 4, 20))
    for x, y in [(160, 620), (260, 628), (330, 640), (1660, 646), (1720, 650), (1810, 644)]:
        b.append(f'<rect x="{x - 9}" y="{y - 10}" width="18" height="12" fill="#2B3561"/><rect x="{x - 3}" y="{y - 7}" width="6" height="5" fill="#FFCB72"/>')
    # The lake.
    defs.append(lg("lake", [(0, "#2A3A6C", 1), (1, "#101934", 1)], box=(-20, 660, W + 40, 440)))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="660" width="{f(W + 40 + 2 * S.BX)}" height="{f(440 + S.BY)}" fill="url(#lake)"/>')
    # Reflections: the peaks faint and upside down, the moon's path of light.
    b.append('<path d="M560 660 L640 760 L720 660 Z M900 660 L980 750 L1060 660 Z" fill="#8E9DC6" opacity="0.12"/>')
    path = []
    for k in range(26):
        y = 680 + k * 15
        w = 30 + k * 6 + rnd.uniform(-10, 10)
        path.append(f'<rect x="{f(1460 - w / 2 + rnd.uniform(-10, 10))}" y="{f(y)}" width="{f(w)}" height="3" rx="1.5" fill="#F6F1DE" opacity="{f(max(0.15, 0.75 - k * 0.025))}"/>')
    k = math.ceil((H + 2 - 680) / 15)
    while S.BY and 680 + k * 15 < H + S.BY:
        w = 30 + k * 6 + S.EXT.uniform(-10, 10)
        path.append(f'<rect x="{f(1460 - w / 2 + S.EXT.uniform(-10, 10))}" y="{f(680 + k * 15)}" width="{f(w)}" height="3" rx="1.5" fill="#F6F1DE" opacity="0.15"/>')
        k += 1
    b.append("".join(path))
    ripples = "".join(f"M{f(x)} {f(y)} h{f(rnd.uniform(30, 90))}" for x, y in [(rnd.uniform(0, W), rnd.uniform(690, 1060)) for _ in range(70)] if not (1360 < x < 1560))
    ripples += "".join(f"M{f(x - 92 if x < 0 else x)} {f(y)} h{f(S.EXT.uniform(30, 90))}" for x, y in bleed_points(74, 0, W, 690, H, 4) if not (1320 < x < 1600))
    b.append(f'<path d="{ripples}" stroke="#6C7DB0" stroke-width="2" opacity="0.35" stroke-linecap="round"/>')
    # Doonga moored near the right shore, lanterns lit.
    defs.append(rg("lan", [(0, "#FFB547", 0.6), (1, "#FFB547", 0)]))
    for x, y, L, ang in [(1600, 880, 260, -6), (1790, 930, 240, 4), (1500, 980, 220, 2)]:
        b.append(P.doonga(x, y, L, "#3A3F6A", "#7A4A5A", ang, rnd, False, "#6C7DB0"))
        b.append(f'<ellipse cx="{x + L * 0.3}" cy="{y - L * 0.16}" rx="60" ry="50" fill="url(#lan)"/><rect x="{x + L * 0.3 - 6}" y="{y - L * 0.16 - 10}" width="12" height="16" rx="3" fill="#FFC86A"/>'
                 f'<rect x="{x + L * 0.3 - 1}" y="{y - L * 0.16 + 6}" width="2" height="{L * 0.1}" fill="#2A2F55"/>'
                 f'<rect x="{x + L * 0.3 - 8}" y="{y + 24}" width="16" height="3" rx="1.5" fill="#FFC86A" opacity="0.5"/>')
    b.append(lift(defs, "mlift2", "#4A5C94", 0.35, 960, 560, 640, 460))
    return S.svg("".join(b), defs)


# ---------------------------------------------------------------- cloths: the bases

def twill(col, op, step=9):
    """A faint woven texture: fine diagonal threads both ways, over the bleed too (the same threads)."""
    bx, by = S.BX, S.BY
    k0 = -H - step * math.ceil((bx + by) / step)
    d = [f"M{f(k - by)} {f(-by)} L{f(k + H + by)} {f(H + by)}" for k in range(k0, int(W + bx + by) + step, step)]
    k0 = -2 * step * math.ceil((bx + by) / (2 * step))
    d2 = [f"M{f(k + by)} {f(-by)} L{f(k - H - by)} {f(H + by)}" for k in range(k0, int(W + H + bx + by) + 2 * step, step * 2)]
    return (f'<path d="{"".join(d)}" stroke="{col}" stroke-width="1" opacity="{op}"/>'
            f'<path d="{"".join(d2)}" stroke="{col}" stroke-width="1" opacity="{op * 0.7}"/>')


def cloth_base(defs, inner, outer, thread):
    """The radial base round the 1920 x 1080 picture (Figma's: its box's middle, 0.47 down, 0.7 of its
    size), the deep colour on over the bleed, and the twill."""
    cy, rx, ry = -2 + 0.47 * (H + 4), 0.7 * (W + 4), 0.7 * (H + 4)
    defs.append(f'<radialGradient id="cbase" gradientUnits="userSpaceOnUse" cx="960" cy="{f(cy)}" r="{f(rx)}" '
                f'gradientTransform="translate(960 {f(cy)}) scale(1 {ry / rx:.4f}) translate(-960 {f(-cy)})">'
                f'<stop offset="0" stop-color="{inner}"/><stop offset="1" stop-color="{outer}"/></radialGradient>')
    return S.full("url(#cbase)") + twill(thread, 0.05)


def cloth_finish(defs, warm="#FFF3D6", vig=0.45):
    """A warm light in the middle, and the vignette round the 1920 x 1080 picture, darkest beyond it."""
    rx, ry = 0.72 * (W + 4), 0.72 * (H + 4)
    defs.append(f'<radialGradient id="cvig" gradientUnits="userSpaceOnUse" cx="960" cy="540" r="{f(rx)}" '
                f'gradientTransform="translate(960 540) scale(1 {ry / rx:.4f}) translate(-960 -540)">'
                f'<stop offset="0.55" stop-color="#000000" stop-opacity="0"/><stop offset="1" stop-color="#000000" stop-opacity="{vig}"/></radialGradient>')
    return glow(defs, "cglow", warm, 0.10, 960, 500, 760, 420) + S.full("url(#cvig)")


def cloth(inner, outer, thread, warm="#FFF3D6", vig=0.45, extra=""):
    defs, b = [], []
    b.append(cloth_base(defs, inner, outer, thread))
    b.append(extra)
    b.append(cloth_finish(defs, warm, vig))
    return S.svg("".join(b), defs)


def cloth_sunrise():
    return cloth("#4B5E80", "#1A2234", "#DCE4F2", "#FFE6D6")


def cloth_laligurans():
    return cloth("#245A52", "#0B2622", "#D8EDE4")


def cloth_terraces():
    return cloth("#5E5C2C", "#22200C", "#F3E6B4", "#FFF0C8")


def cloth_tihar():
    return cloth("#4C2452", "#1A0A1F", "#F2D7F0", "#FFD9A8")


def cloth_pass():
    return cloth("#474C50", "#151719", "#E8E4DC", "#F2EEE6", 0.42)


def cloth_mithila():
    return cloth("#7A3A20", "#2C1108", "#FCE2C4", "#FFE3C0")


def cloth_moonlight():
    """Midnight navy, stars scattered thick at the edges and thinning to nothing in the middle; over the bleed
    (the edges of a wider or taller screen) they stay thick."""
    rnd = random.Random(77)
    st = []
    pts = []
    for _ in range(900):
        x, y = rnd.uniform(20, W - 20), rnd.uniform(20, H - 20)
        dx, dy = (x - 960) / 860, (y - 540) / 470
        e = max(abs(dx), abs(dy))
        if e < 0.72 or rnd.random() > (e - 0.72) * 3.5:
            continue
        pts.append((x, y, rnd.choice([1.0, 1.3, 1.6, 2.0])))
    if bleed():
        for _ in range(int(900 * (W + 2 * S.BX) * (H + 2 * S.BY) / ((W - 40) * (H - 40)))):
            x, y = S.EXT.uniform(-S.BX, W + S.BX), S.EXT.uniform(-S.BY, H + S.BY)
            if not (20 <= x <= W - 20 and 20 <= y <= H - 20):
                pts.append((x, y, S.EXT.choice([1.0, 1.3, 1.6, 2.0])))
    for x, y, r in pts:
        st.append(f"M{f(x + r)} {f(y)} A{f(r)} {f(r)} 0 1 0 {f(x - r)} {f(y)} A{f(r)} {f(r)} 0 1 0 {f(x + r)} {f(y)} Z")
    sp = []
    for _ in range(26):
        x, y = rnd.uniform(40, W - 40), rnd.uniform(40, H - 40)
        dx, dy = (x - 960) / 860, (y - 540) / 470
        if max(abs(dx), abs(dy)) < 0.8:
            continue
        sp.append(sparkle(x, y, rnd.uniform(6, 11), "#F2DFA0", 0.4))
    if bleed():
        for _ in range(int(26 * (W + 2 * S.BX) * (H + 2 * S.BY) / ((W - 80) * (H - 80)))):
            x, y = S.EXT.uniform(-S.BX, W + S.BX), S.EXT.uniform(-S.BY, H + S.BY)
            if not (40 <= x <= W - 40 and 40 <= y <= H - 40):
                sp.append(sparkle(x, y, S.EXT.uniform(6, 11), "#F2DFA0", 0.4))
    extra = f'<path d="{"".join(st)}" fill="#E6ECFF" opacity="0.4"/>' + "".join(sp)
    return cloth("#22325F", "#0A1024", "#D8E0F6", "#DCE4FF", 0.42, extra)


# ---------------------------------------------------------------- cloths: the borders

N, CORNER, PERIOD = cloths.FRAME, cloths.FRAME_BORDER, 48

# An edge strip is drawn once in (u, v): u along the edge from the frame's top-left, v in from the edge.
STRIPS = {"top": "", "bottom": f"matrix(1 0 0 -1 0 {N})", "left": "matrix(0 1 1 0 0 0)", "right": f"matrix(0 1 -1 0 {N} 0)"}
# A corner is drawn once, top-left, and mirrored into the others.
MIRRORS = ["", f"matrix(-1 0 0 1 {N} 0)", f"matrix(1 0 0 -1 0 {N})", f"matrix(-1 0 0 -1 {N} {N})"]


def strips(body, edges=("top", "bottom", "left", "right"), op=None):
    g = f' opacity="{op}"' if op is not None else ""
    return "".join(f'<g transform="{STRIPS[e]}"{g}>{body}</g>' if STRIPS[e] or g else body for e in edges)


def corners(body):
    return "".join(f'<g transform="{m}">{body}</g>' if m else body for m in MIRRORS)


def along_edge(phase, lo, step=PERIOD):
    """The places phase + k * step on an edge at least lo from either corner. Motifs sit at 12 (mod 24) from the
    corner, inside the 24-unit cells that Unity's half tile keeps whole."""
    return [u for u in range(phase % step, N, step) if lo <= u <= N - lo]


def frame_svg(body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{N}" height="{N}" viewBox="0 0 {N} {N}">{body}</svg>'


def frame_sunrise():
    """Slate blue: a stitched range of peaks round the edges (a peak every 48, snow on its tip), a thin gold
    line inside it, a rising sun in each corner."""
    ink, gold = "#F3CDBD", "#E9C27A"
    tip, feet = 42, 70
    # A tip at 36 and a foot at 12 (mod 48): every 24-unit cell starts and ends half way up a slope, so a
    # half tile only makes one peak or one valley short. The range is only in the repeating run (168..216):
    # it starts and ends half way up on every screen.
    us = [CORNER, CORNER + 12, CORNER + 36, CORNER + PERIOD]
    pk = "M" + " L".join(f"{u} {tip if (u - 36) % 48 == 0 else feet if (u - 12) % 48 == 0 else (tip + feet) / 2}" for u in us)
    snow = "".join(f"M{u - 8} {tip + 9} L{u} {tip} L{u + 8} {tip + 9} Z" for u in us if (u - 36) % 48 == 0)
    edge = (f'<path d="{pk}" stroke="{ink}" stroke-width="3" fill="none" stroke-linejoin="round" stroke-linecap="round"/>'
            f'<path d="{snow}" fill="#FFFFFF" opacity="0.69"/>')
    b = [strips(edge, ("top", "bottom"), 0.32), strips(edge, ("left", "right"), 0.26)]
    b.append(f'<rect x="88" y="88" width="{N - 176}" height="{N - 176}" rx="16" fill="none" stroke="{gold}" stroke-width="2" opacity="0.3"/>')
    sun = f'<g transform="translate(72 60) scale(0.85)" opacity="0.4"><path d="M-30 10 A30 30 0 0 1 30 10 Z" fill="{gold}"/>'
    for k in range(7):
        a = math.radians(-180 + 15 + k * 25)
        sun += f'<path d="M{f(math.cos(a) * 38)} {f(10 + math.sin(a) * 38)} L{f(math.cos(a) * 56)} {f(10 + math.sin(a) * 56)}" stroke="{gold}" stroke-width="4" stroke-linecap="round"/>'
    sun += f'<path d="M-50 18 L50 18" stroke="{ink}" stroke-width="3"/></g>'
    b.append(corners(sun))
    return frame_svg("".join(b))


def frame_laligurans():
    """Deep teal: a thin stem round the edges, a small five-petal blossom and a pair of leaves on it every 48,
    a cluster of blossom in leaves at each corner."""
    vine, leaf, petal, centre = "#9DC4AE", "#8FB89F", "#E8909B", "#F6D27A"
    v = 70
    b = [f'<rect x="{v}" y="{v}" width="{N - 2 * v}" height="{N - 2 * v}" fill="none" stroke="{vine}" stroke-width="2.4" opacity="0.32"/>']
    leaves = "".join(leaf_d(u - 9, v, 22, a, 0.34) for u in along_edge(12, 128) for a in (-38, 38))
    edge = f'<path d="{leaves}" fill="{leaf}" opacity="0.26"/>'
    edge += "".join(flower5(u, v, 10, petal, centre, -90, 0.42) for u in along_edge(36, 136))
    b.append(strips(edge))
    cl = f'<path d="{"".join(leaf_d(v, v, 48, a, 0.3) for a in range(0, 360, 45))}" fill="{leaf}" opacity="0.3"/>'
    for k in range(5):
        a = math.tau * k / 5 - math.pi / 4
        cl += flower5(v + math.cos(a) * 17, v + math.sin(a) * 17, 14, petal, centre, k * 20, 0.5)
    b.append(corners(cl))
    return frame_svg("".join(b))


def ear(x, y, L, ang, col, op):
    """A ripe rice ear: a stem and grains in pairs."""
    a = math.radians(ang)
    out = f"M{f(x)} {f(y)} L{f(x + math.cos(a) * L)} {f(y + math.sin(a) * L)} "
    g = ""
    for k in range(5):
        u = L * (0.3 + k * 0.15)
        px, py = x + math.cos(a) * u, y + math.sin(a) * u
        for sgn in (-1, 1):
            g += leaf_d(px, py, L * 0.2, ang + sgn * 32, 0.42)
    return f'<path d="{out}" stroke="{col}" stroke-width="2" opacity="{op}"/><path d="{g}" fill="{col}" opacity="{op}"/>'


def dashes(v, first, col, op):
    """A dashed line v in from the edge, 16 on and 8 off from first (4 mod 24) to as far from the other end."""
    d = "".join(f"M{u} {v} h16" for u in range(first, N - first, 24))
    return f'<path d="{d}" stroke="{col}" stroke-width="2" fill="none" opacity="{op}"/>'


def frame_terraces():
    """Olive: a ripe rice ear every 48 between two dashed terrace lines, a sheaf in each corner."""
    gold = "#E6C66E"
    b = []
    for v, first, op in ((40, 52, 0.3), (140, 148, 0.18)):
        r = first - v
        b.append(strips(dashes(v, first, gold, op)))
        b.append(corners(f'<path d="M{v} {first} A{r} {r} 0 0 1 {first} {v}" stroke="{gold}" stroke-width="2" fill="none" opacity="{op}"/>'))
    # Each ear leans a little; its foot is set so that it fills its cell (36 mod 48, 12 either side).
    ears = "".join(ear(u + 5, 104, 48, -97, gold, 1) for u in along_edge(36, 150))
    b.append(strips(ears, ("top", "bottom"), 0.34))
    b.append(strips(ears, ("left", "right"), 0.28))
    sheaf = "".join(ear(84, 112, 58, a, gold, 0.4) for a in (-122, -102, -78, -58))
    sheaf += '<rect x="70" y="98" width="28" height="8" rx="3" fill="#C9822E" opacity="0.5"/>'
    b.append(corners(sheaf))
    return frame_svg("".join(b))


def frame_tihar():
    """Plum: a garland of marigolds round the edges in swags 48 wide, knotted in red every 48, and a rangoli
    quarter-flower in each corner (quieter, 0.3)."""
    v, sag, r = 54, 14, 4.2
    flower = (16, 16, 120)   # the rangoli's centre and reach, in the corner
    cols = ["#F29A1F", "#F7C531", "#E8701A"]

    def clear(u, y):
        return min(math.hypot(u - flower[0], y - flower[1]), math.hypot(N - u - flower[0], y - flower[1])) > flower[2] + r

    dots = []
    for u in range(4, N, 8):
        t = ((u - 36) % 48) / 48
        y = v + sag * 4 * t * (1 - t)
        if clear(u, y):
            dots.append(f'<circle cx="{u}" cy="{f(y)}" r="{r}" fill="{cols[((u - 36) // 8) % 3]}"/>')
    knots = "".join(f'<circle cx="{u}" cy="{v}" r="5" fill="#D63A3A"/>' for u in range(36, N, 48) if clear(u, v))
    b = [strips(f'<g opacity="0.42">{"".join(dots)}</g><g opacity="0.5">{knots}</g>')]
    cx, cy, reach = flower
    g = f'<g transform="translate({cx} {cy}) scale({reach / 150:.4f})" opacity="0.3">'
    for rr, n, col, w in [(150, 16, "#E8701A", 0.22), (112, 12, "#D6337A", 0.26), (76, 10, "#F7C531", 0.3), (44, 8, "#2E9D8F", 0.36)]:
        d = ""
        for k in range(n):
            a = math.tau * (k + 0.5) / n
            d += leaf_d(math.cos(a) * rr * 0.45, math.sin(a) * rr * 0.45, rr * 0.55, math.degrees(a), w)
        g += f'<path d="{d}" fill="{col}"/>'
    g += '<circle r="12" fill="#FFF0C8"/></g>'
    b.append(corners(g))
    return frame_svg("".join(b))


def frame_pass():
    """Charcoal: the five flag colours as stripes round the edges (blue, white, red, green, yellow, muted), a
    small outlined diamond every 48 inside them, a knot of interlaced squares in each corner."""
    cols = ["#5E86C2", "#ECE7DD", "#C8504A", "#4E9A6C", "#E3BF4A"]
    b = []
    for k, col in enumerate(cols):
        ins = 46 + k * 9
        b.append(f'<rect x="{ins}" y="{ins}" width="{N - 2 * ins}" height="{N - 2 * ins}" rx="10" fill="none" stroke="{col}" stroke-width="6" opacity="0.30"/>')
    v = 124
    dia = "".join(f"M{u} {v - 10} L{u + 10} {v} L{u} {v + 10} L{u - 10} {v} Z" for u in along_edge(36, 176))
    b.append(strips(f'<path d="{dia}" fill="none" stroke="#D9CBAA" stroke-width="2" opacity="0.26"/>'))
    knot = (f'<g transform="translate({v} {v}) scale(0.8)" opacity="0.36">'
            '<rect x="-30" y="-30" width="60" height="60" fill="none" stroke="#E3BF4A" stroke-width="5"/>'
            '<rect x="-30" y="-30" width="60" height="60" fill="none" stroke="#ECE7DD" stroke-width="5" transform="rotate(45)"/>'
            '<circle r="10" fill="#C8504A"/></g>')
    b.append(corners(knot))
    return frame_svg("".join(b))


def frame_mithila():
    """Rust: two cream lines with a band of turmeric triangles between them (one every 24), a dotted line
    inside, a fish in each corner swimming along the long edges."""
    ln, tm = "#F6E3C2", "#F2B630"
    o, i = 24, 48
    b = [f'<rect x="{c}" y="{c}" width="{N - 2 * c}" height="{N - 2 * c}" fill="none" stroke="{ln}" stroke-width="3" opacity="0.32"/>' for c in (o, i)]

    def tri(u0, u1):
        return "".join(f"M{u} {o} L{u + 24} {o} L{u + 12} {i} Z" for u in range(u0, u1, 24))
    b.append(strips(f'<path d="{tri(o, N - o)}" fill="{tm}" opacity="0.22"/>', ("top", "bottom")))
    b.append(strips(f'<path d="{tri(i, N - i)}" fill="{tm}" opacity="0.22"/>', ("left", "right")))
    v = 120
    dots = "".join(f"M{u - 1} {v} h2" for u in range(v + 6, N - v, 12))
    b.append(strips(f'<path d="{dots}" stroke="{ln}" stroke-width="1.6" stroke-linecap="round" opacity="0.4"/>'))
    b.append(corners(f'<path d="M{v - 1} {v} h2" stroke="{ln}" stroke-width="1.6" stroke-linecap="round" opacity="0.4"/>'))
    fish = f'<g opacity="0.38" transform="translate(110 84) rotate(180) scale(0.55)">'
    fish += f'<path d="M-90 0 C-50 -50 40 -50 70 0 C40 50 -50 50 -90 0 Z" fill="none" stroke="{tm}" stroke-width="5"/>'
    fish += f'<path d="M70 0 L110 -34 L104 0 L110 34 Z" fill="{tm}"/>'
    fish += f'<path d="M-40 -26 Q-30 0 -40 26 M-10 -30 Q0 0 -10 30 M20 -26 Q30 0 20 26" stroke="{ln}" stroke-width="3" fill="none"/>'
    fish += f'<circle cx="-64" cy="-6" r="7" fill="{ln}"/></g>'
    b.append(corners(fish))
    return frame_svg("".join(b))


# Phewa Moonlight's sprinkle, one 48-unit run of it: (u in the run, v, radius; a sparkle when the radius is big).
SPRINKLE = [(12, 74, 4), (4, 102, 1.6), (19, 128, 1.2), (31, 60, 1.8), (38, 94, 1.3), (44, 136, 1.5), (28, 114, 1.1), (8, 148, 1.2), (36, 154, 1.6)]


def frame_moonlight():
    """Midnight navy: a double silver line, a sprinkle of small stars and dots repeating every 48 inside it, a
    crescent moon in each corner."""
    b = [f'<rect x="{c}" y="{c}" width="{N - 2 * c}" height="{N - 2 * c}" rx="14" fill="none" stroke="#C9D3EE" stroke-width="1.8" opacity="{op}"/>'
         for c, op in ((34, 0.3), (46, 0.18))]
    dots, sparks = "", ""
    for u0 in range(0, N, PERIOD):
        for du, v, r in SPRINKLE:
            u = u0 + du
            if not 150 <= u <= N - 150:
                continue
            if r > 3:
                sparks += sparkle(u, v, r, "#F2DFA0", 0.4)
            else:
                dots += f"M{f(u + r)} {v} A{r} {r} 0 1 0 {f(u - r)} {v} A{r} {r} 0 1 0 {f(u + r)} {v} Z"
    b.append(strips(f'<path d="{dots}" fill="#E6ECFF" opacity="0.4"/>{sparks}'))
    cx = cy = 96
    b.append(corners(f'<path d="M{cx + 10} {cy - 34} A36 36 0 1 0 {cx + 26} {cy + 26} A28 28 0 1 1 {cx + 10} {cy - 34} Z" fill="#F2DFA0" opacity="0.4"/>'))
    return frame_svg("".join(b))


# id, art name (BG_<Name>, Table_<Name>, Frame_<Name>), background, cloth, border.
THEMES = [
    ("himalayan-sunrise", "HimalayanSunrise", sunrise, cloth_sunrise, frame_sunrise),
    ("laligurans-forest", "LaliguransForest", laligurans, cloth_laligurans, frame_laligurans),
    ("golden-terraces", "GoldenTerraces", terraces_gold, cloth_terraces, frame_terraces),
    ("tihar-lights", "TiharLights", tihar, cloth_tihar, frame_tihar),
    ("prayer-flag-pass", "PrayerFlagPass", flag_pass, cloth_pass, frame_pass),
    ("mithila-garden", "MithilaGarden", mithila, cloth_mithila, frame_mithila),
    ("phewa-moonlight", "PhewaMoonlight", moonlight, cloth_moonlight, frame_moonlight),
]


if __name__ == "__main__":
    out = sys.argv[1]
    if len(sys.argv) > 3:
        S.set_bleed(float(sys.argv[2]), float(sys.argv[3]))
    os.makedirs(out, exist_ok=True)
    for key, _, bg, cl, fr in THEMES:
        for kind, fn in (("bg", bg), ("cloth", cl), ("frame", fr)):
            p = os.path.join(out, f"{kind}_{key}.svg")
            with open(p, "w", encoding="utf-8") as fh:
                fh.write(fn())
            print(p, os.path.getsize(p))
