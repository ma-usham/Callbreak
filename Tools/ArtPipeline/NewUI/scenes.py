"""Vector scenes for the V1 rebuild: the menu backdrop and the table scene, as flat layered SVG.

Usage: python -P scenes.py <out-dir> [bleed-x bleed-y]
Writes menu.svg and table.svg (1920 x 1080, plus the bleed on each side when given; see set_bleed). Every shape is drawn here: faceted peaks with two-tone
snow, haze between ranges, a three-tier pagoda, prayer flags on curved strings, rhododendrons, a
hillside village and lake, and (for the table) a carved round table in perspective.
"""
import math
import os
import random
import sys

W, H = 1920, 1080

# The game's backdrop frame has a margin round the 1920 x 1080 picture (2230 x 1255 frame pixels,
# for 21:9 and 4:3 screens). set_bleed widens a scene by that much on each side: whatever fills the
# margin draws on its own random numbers (EXT), so the picture inside stays exactly as it was.
BX, BY = 0.0, 0.0
EXT = random.Random(7)


def set_bleed(bx, by):
    global BX, BY
    BX, BY = bx, by
    EXT.seed(7)


def full(fill):
    """A rect over the whole picture, its margin included."""
    return f'<rect x="{f(-BX - 2)}" y="{f(-BY - 2)}" width="{f(W + 2 * BX + 4)}" height="{f(H + 2 * BY + 4)}" fill="{fill}"/>'


def extend(line, side):
    """Continues a skyline from one end (side -1 left, 1 right) over the margin, level, with a gentle wander."""
    x, y = line[-1] if side > 0 else line[0]
    pts = [(x + side * i * 40, y + EXT.uniform(-3, 3)) for i in range(1, int((BX + 80) / 40) + 2)]
    return line + pts if side > 0 else pts[::-1] + line


def f(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


def poly(points):
    return "M" + " L".join(f"{f(x)} {f(y)}" for x, y in points) + " Z"


def jag(a, b, rough, depth, rnd, perp=True):
    """A jagged line from a to b by midpoint displacement (perpendicular to the segment)."""
    pts = [a, b]
    disp = rough * math.dist(a, b)
    for _ in range(depth):
        out = [pts[0]]
        for p, q in zip(pts, pts[1:]):
            mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            dx, dy = q[0] - p[0], q[1] - p[1]
            n = math.hypot(dx, dy) or 1
            ox, oy = (-dy / n, dx / n) if perp else (0, 1)
            d = rnd.uniform(-disp, disp)
            out += [(mx + ox * d, my + oy * d), q]
        pts = out
        disp *= 0.55
    return pts


def ridge(ctrl, rough, depth, rnd):
    """A skyline through control points, roughened between them (vertical displacement)."""
    out = [ctrl[0]]
    for a, b in zip(ctrl, ctrl[1:]):
        out += jag(a, b, rough, depth, rnd, perp=False)[1:]
    return out


def above(line, sy):
    """The part of a top-down polyline above y = sy, ending on the crossing point."""
    out = [line[0]]
    for p, q in zip(line, line[1:]):
        if q[1] <= sy:
            out.append(q)
            continue
        t = (sy - p[1]) / (q[1] - p[1]) if q[1] != p[1] else 0
        out.append((p[0] + (q[0] - p[0]) * t, sy))
        return out
    return out


def snow_edge(a, b, rnd, fingers):
    """The lower edge of a snow cap, from a to b: a wandering line with a few streaks running down gullies."""
    n = 22
    out = []
    drift = 0.0
    for i in range(1, n):
        t = i / n
        x = a[0] + (b[0] - a[0]) * t
        y = a[1] + (b[1] - a[1]) * t
        drift = max(-0.25, min(0.6, drift + rnd.uniform(-0.18, 0.2)))
        y += drift * fingers * 0.5
        if rnd.random() < 0.2:
            w = abs(b[0] - a[0]) / n * rnd.uniform(0.7, 1.1)
            out.append((x - w, y))
            out.append((x - w * 0.25, y + fingers * rnd.uniform(0.25, 0.5)))
            out.append((x + w * 0.2, y + fingers * rnd.uniform(0.3, 0.6)))
            out.append((x + w, y))
        else:
            out.append((x, y))
    return out


def peak(px, py, wl, wr, base, rnd, rock, rock_dark, snow, snow_dark, snow_frac=0.42, rough=0.07):
    """A faceted mountain: light left face, dark right face, two-tone snow cap."""
    mid = (px + wr * 0.18 + rnd.uniform(-10, 10), base)
    left = jag((px, py), (px - wl, base), rough, 5, rnd)
    right = jag((px, py), (px + wr, base), rough, 5, rnd)
    inner = jag((px, py), mid, rough * 1.4, 5, rnd)
    sy = py + (base - py) * snow_frac
    out = []
    out.append(f'<path d="{poly(left + [mid] + inner[::-1])}" fill="{rock}"/>')
    out.append(f'<path d="{poly(inner + [(px + wr, base)] + right[::-1])}" fill="{rock_dark}"/>')
    la, ia, ra = above(left, sy), above(inner, sy), above(right, sy)
    fingers = (base - py) * 0.12
    out.append(f'<path d="{poly(la + snow_edge(la[-1], ia[-1], rnd, fingers) + ia[::-1])}" fill="{snow}"/>')
    out.append(f'<path d="{poly(ia + snow_edge(ia[-1], ra[-1], rnd, fingers) + ra[::-1])}" fill="{snow_dark}"/>')
    return "".join(out)


def band(y0, y1, color, a0, a1, a2, gid, defs):
    """A horizontal haze band: transparent, then a0..a1, then transparent (vertical gradient)."""
    defs.append(
        f'<linearGradient id="{gid}" x1="0" y1="0" x2="0" y2="1">'
        f'<stop offset="0" stop-color="{color}" stop-opacity="{a0}"/>'
        f'<stop offset="0.5" stop-color="{color}" stop-opacity="{a1}"/>'
        f'<stop offset="1" stop-color="{color}" stop-opacity="{a2}"/></linearGradient>')
    return f'<rect x="{f(-20 - BX)}" y="{f(y0)}" width="{f(W + 40 + 2 * BX)}" height="{f(y1 - y0)}" fill="url(#{gid})"/>'


def hills(ctrl, rough, rnd, color, bottom=H + 20):
    line = ridge(ctrl, rough, 6, rnd)
    if BX and line[0][0] <= 0:
        line = extend(line, -1)
    if BX and line[-1][0] >= W:
        line = extend(line, 1)
    if bottom >= H:
        bottom += BY
    return f'<path d="{poly(line + [(line[-1][0], bottom), (line[0][0], bottom)])}" fill="{color}"/>'


def conifer(x, y, h, color, rnd):
    w = h * rnd.uniform(0.32, 0.4)
    tiers = 3
    pts = [(x, y - h)]
    for i in range(1, tiers + 1):
        ty = y - h + h * i / tiers
        tw = w * i / tiers
        pts.append((x + tw / 2, ty))
        if i < tiers:
            pts.append((x + tw * 0.22, ty))
    right = pts[1:]
    left = [(2 * x - px, py) for px, py in right[::-1]]
    return f'<path d="{poly([pts[0]] + right + left)}" fill="{color}"/>'


def house(x, y, w, h, wall, wall_dark, roof, roof_dark, window):
    """A small Newari house: white walls, a terracotta pitched roof, a dark window."""
    rh = h * 0.55
    out = [f'<rect x="{f(x)}" y="{f(y - h)}" width="{f(w * 0.62)}" height="{f(h)}" fill="{wall}"/>',
           f'<rect x="{f(x + w * 0.62)}" y="{f(y - h)}" width="{f(w * 0.38)}" height="{f(h)}" fill="{wall_dark}"/>',
           f'<path d="{poly([(x - w * 0.08, y - h), (x + w * 0.5, y - h - rh), (x + w * 1.08, y - h)])}" fill="{roof}"/>',
           f'<path d="{poly([(x + w * 0.5, y - h - rh), (x + w * 1.08, y - h), (x + w * 0.5, y - h)])}" fill="{roof_dark}"/>',
           f'<rect x="{f(x + w * 0.2)}" y="{f(y - h * 0.62)}" width="{f(w * 0.18)}" height="{f(h * 0.3)}" fill="{window}"/>']
    return "".join(out)


def pagoda(cx, ground, s, c):
    """A three-tier Newari pagoda on a stepped stone plinth, s = scale (1 = about 520 px wide)."""
    out = []
    y = ground
    # Plinth: four steps, each a top face and a front face with block joints.
    for i, w in enumerate([520, 456, 392, 328]):
        w *= s
        hgt = 28 * s
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - hgt)}" width="{f(w)}" height="{f(hgt)}" fill="{c["stone"]}"/>')
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - hgt)}" width="{f(w)}" height="{f(5 * s)}" fill="{c["stone_top"]}"/>')
        out.append(f'<rect x="{f(cx + w * 0.18)}" y="{f(y - hgt)}" width="{f(w * 0.32)}" height="{f(hgt)}" fill="{c["stone_dark"]}" opacity="0.35"/>')
        for j in range(1, int(w / (46 * s))):
            jx = cx - w / 2 + j * 46 * s + (i % 2) * 23 * s
            out.append(f'<rect x="{f(jx)}" y="{f(y - hgt + 6 * s)}" width="{f(1.6 * s)}" height="{f(hgt - 8 * s)}" fill="{c["stone_dark"]}" opacity="0.5"/>')
        if i == 0:
            out.append(f'<rect x="{f(cx - 40 * s)}" y="{f(y - hgt)}" width="{f(80 * s)}" height="{f(hgt)}" fill="{c["stone_dark"]}" opacity="0.45"/>')
        y -= hgt

    def body(w, h, doors):
        nonlocal y
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - h)}" width="{f(w)}" height="{f(h)}" fill="{c["wood"]}"/>')
        out.append(f'<rect x="{f(cx + w * 0.12)}" y="{f(y - h)}" width="{f(w * 0.38)}" height="{f(h)}" fill="{c["wood_dark"]}"/>')
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - h)}" width="{f(w)}" height="{f(5 * s)}" fill="{c["trim"]}"/>')
        dw = w / (doors * 1.6 + 0.6)
        for d in range(doors):
            dx = cx - w / 2 + dw * 0.6 + d * dw * 1.6
            out.append(f'<rect x="{f(dx - 2 * s)}" y="{f(y - h * 0.82 - 2 * s)}" width="{f(dw + 4 * s)}" height="{f(h * 0.72 + 2 * s)}" fill="{c["gold"]}" opacity="0.8"/>')
            out.append(f'<rect x="{f(dx)}" y="{f(y - h * 0.82)}" width="{f(dw)}" height="{f(h * 0.72)}" fill="{c["door"]}"/>')
            out.append(f'<rect x="{f(dx + dw / 2 - 0.8 * s)}" y="{f(y - h * 0.82)}" width="{f(1.6 * s)}" height="{f(h * 0.72)}" fill="{c["wood"]}"/>')
        y -= h

    def roof(wb, wt, h):
        nonlocal y
        lip = 10 * s
        # The eaves' dark underside and the red trim strip under the roof.
        out.append(f'<rect x="{f(cx - wb / 2 + lip)}" y="{f(y - 7 * s)}" width="{f(wb - 2 * lip)}" height="{f(9 * s)}" fill="{c["eave"]}"/>')
        for k in range(-3, 4):
            sx = cx + k * wb / 8
            out.append(f'<path d="{poly([(sx, y), (sx + 6 * s, y), (sx + 22 * s * (1 if k >= 0 else -1), y - 16 * s), (sx + 16 * s * (1 if k >= 0 else -1), y - 16 * s)])}" fill="{c["eave"]}"/>')
        top = y - h
        pts = [(cx - wb / 2 - 6 * s, y - 14 * s), (cx - wb / 2 + 14 * s, y - 12 * s), (cx - wt / 2, top), (cx + wt / 2, top),
               (cx + wb / 2 - 14 * s, y - 12 * s), (cx + wb / 2 + 6 * s, y - 14 * s), (cx + wb / 2 - 4 * s, y - 4 * s), (cx - wb / 2 + 4 * s, y - 4 * s)]
        out.append(f'<path d="{poly(pts)}" fill="{c["roof"]}"/>')
        out.append(f'<path d="{poly([(cx, top), (cx + wt / 2, top), (cx + wb / 2 - 14 * s, y - 12 * s), (cx + wb / 2 + 6 * s, y - 14 * s), (cx + wb / 2 - 4 * s, y - 4 * s), (cx + wb * 0.12, y - 4 * s)])}" fill="{c["roof_dark"]}"/>')
        out.append(f'<path d="{poly([(cx - wb / 2 + 4 * s, y - 4 * s), (cx + wb / 2 - 4 * s, y - 4 * s), (cx + wb / 2 - 6 * s, y + 2 * s), (cx - wb / 2 + 6 * s, y + 2 * s)])}" fill="{c["trim"]}"/>')
        y = top

    body(230 * s, 118 * s, 3)
    roof(420 * s, 236 * s, 52 * s)
    body(178 * s, 74 * s, 2)
    roof(332 * s, 186 * s, 44 * s)
    body(130 * s, 56 * s, 1)
    roof(258 * s, 30 * s, 66 * s)
    # The gilt finial.
    for i, (w, h) in enumerate([(22, 10), (16, 14), (26, 8), (12, 22), (18, 8), (8, 20)]):
        out.append(f'<rect x="{f(cx - w * s / 2)}" y="{f(y - h * s)}" width="{f(w * s)}" height="{f(h * s)}" rx="{f(3 * s)}" fill="{c["gold"] if i % 2 == 0 else c["gold_dark"]}"/>')
        y -= h * s
    out.append(f'<path d="{poly([(cx - 4 * s, y), (cx + 4 * s, y), (cx, y - 22 * s)])}" fill="{c["gold"]}"/>')
    return "".join(out)


def flags(points, colors, rnd, size=1.0):
    """Prayer flags hanging along a polyline string."""
    out = [f'<path d="M{f(points[0][0])} {f(points[0][1])} ' + " ".join(f"L{f(x)} {f(y)}" for x, y in points[1:]) + '" stroke="#6B5444" stroke-width="1.6" fill="none"/>']
    i = 0
    for p, q in zip(points, points[1:]):
        seg = math.dist(p, q)
        n = max(1, int(seg / (30 * size)))
        for k in range(n):
            t = (k + 0.5) / n
            x, y = p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t
            ang = math.degrees(math.atan2(q[1] - p[1], q[0] - p[0])) * 0.6 + rnd.uniform(-6, 6)
            w, h = 22 * size, 30 * size
            col = colors[i % len(colors)]
            i += 1
            out.append(f'<g transform="translate({f(x)} {f(y)}) rotate({f(ang)})"><path d="M{f(-w / 2)} 0 L{f(w / 2)} 0 L{f(w / 2 + 2 * size)} {f(h)} L{f(-w / 2 + 2 * size)} {f(h - 3 * size)} Z" fill="{col}"/></g>')
    return "".join(out)


def string(a, b, sag, n=16):
    return [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + sag * 4 * t * (1 - t)) for t in [i / n for i in range(n + 1)]]


def blossom(x, y, r, ang, petal, petal_light, centre, rnd):
    """One five-petal rhododendron blossom, petals overlapping, with a dark throat and stamens."""
    out = []
    for k in range(5):
        a = ang + k * 72 + rnd.uniform(-8, 8)
        L = r * rnd.uniform(0.92, 1.08)
        out.append(f'<g transform="translate({f(x)} {f(y)}) rotate({f(a)})"><path d="M0 0 C{f(L * 0.2)} {f(-L * 0.55)} {f(L * 0.95)} {f(-L * 0.6)} {f(L)} 0 C{f(L * 0.95)} {f(L * 0.6)} {f(L * 0.2)} {f(L * 0.55)} 0 0 Z" fill="{petal}"/>'
                   f'<path d="M{f(L * 0.25)} {f(-L * 0.12)} C{f(L * 0.4)} {f(-L * 0.38)} {f(L * 0.75)} {f(-L * 0.4)} {f(L * 0.82)} {f(-L * 0.12)}" stroke="{petal_light}" stroke-width="{f(r * 0.1)}" fill="none" opacity="0.5" stroke-linecap="round"/></g>')
    out.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r * 0.3)}" fill="{centre}"/>')
    for k in range(5):
        a = math.radians(ang + 36 + k * 72)
        out.append(f'<path d="M{f(x)} {f(y)} L{f(x + math.cos(a) * r * 0.62)} {f(y + math.sin(a) * r * 0.62)}" stroke="{centre}" stroke-width="{f(r * 0.06)}"/>'
                   f'<circle cx="{f(x + math.cos(a) * r * 0.66)}" cy="{f(y + math.sin(a) * r * 0.66)}" r="{f(r * 0.07)}" fill="#F3D27A"/>')
    return "".join(out)


def leaf(x, y, L, ang, col, rib):
    w = L * 0.24
    return (f'<g transform="translate({f(x)} {f(y)}) rotate({f(ang)})"><path d="M0 0 C{f(L * 0.3)} {f(-w)} {f(L * 0.75)} {f(-w * 0.9)} {f(L)} 0 C{f(L * 0.75)} {f(w * 0.9)} {f(L * 0.3)} {f(w)} 0 0 Z" fill="{col}"/>'
            f'<path d="M{f(L * 0.05)} 0 L{f(L * 0.9)} 0" stroke="{rib}" stroke-width="{f(max(1, L * 0.025))}" opacity="0.6"/></g>')


def bush(cx, cy, r, rnd, leaf_col, leaf_dark, petal, petal_light, centre, trusses=4):
    """A rhododendron: whorls of long ribbed leaves, each crowned with a dome-shaped truss of blossoms."""
    out = []
    spots = []
    for t in range(trusses):
        a = rnd.uniform(math.pi * 1.1, math.pi * 1.9)
        d = rnd.uniform(0.25, 0.75) * r
        spots.append((cx + math.cos(a) * d, cy + math.sin(a) * d * 0.7))
    for sx, sy in spots + [(cx + rnd.uniform(-r, r) * 0.6, cy + rnd.uniform(-0.2, 0.3) * r) for _ in range(4)]:
        for k in range(9):
            ang = rnd.uniform(-200, 20)
            out.append(leaf(sx + rnd.uniform(-8, 8), sy + rnd.uniform(4, 16), r * rnd.uniform(0.26, 0.4), ang, leaf_col if rnd.random() < 0.55 else leaf_dark, "#1F3320"))
    for sx, sy in spots:
        fr = r * rnd.uniform(0.075, 0.095)
        dots = [(rnd.uniform(-1, 1), rnd.uniform(-1, 0.4)) for _ in range(9)]
        dots.sort(key=lambda d: d[1])
        for dx, dy in dots:
            bx, by = sx + dx * fr * 2.2, sy + dy * fr * 1.6
            shade = petal if dy > -0.4 else petal_light
            out.append(blossom(bx, by, fr, rnd.uniform(0, 360), shade if rnd.random() < 0.7 else petal, petal_light, centre, rnd))
    return "".join(out)


def cloud(cx, cy, s, rnd):
    """A flat-bottomed cloud: puffs on a level base."""
    puffs = [(-90, -10, 34), (-48, -34, 42), (0, -46, 50), (52, -30, 40), (92, -8, 30)]
    d = f"M{f(cx - 120 * s)} {f(cy)} "
    for px, py, pr in puffs:
        d += f"A{f(pr * s)} {f(pr * s)} 0 0 1 {f(cx + (px + pr * 0.9) * s)} {f(cy + (py + pr * 0.4) * s)} "
    d += f"L{f(cx + 124 * s)} {f(cy)} Z"
    return (f'<path d="{d}" fill="#FFFFFF" opacity="0.92"/>'
            f'<rect x="{f(cx - 118 * s)}" y="{f(cy - 6 * s)}" width="{f(240 * s)}" height="{f(6 * s)}" rx="{f(3 * s)}" fill="#DCE9F4" opacity="0.9"/>')


def svg(body, defs):
    w, h = W + 2 * BX, H + 2 * BY
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{f(w)}" height="{f(h)}" viewBox="{f(-BX)} {f(-BY)} {f(w)} {f(h)}">'
            f'<defs>{"".join(defs)}</defs>{body}</svg>')


TEMPLE = dict(stone="#CDBCA8", stone_top="#E2D5C5", stone_dark="#A8937D", wood="#6A3520", wood_dark="#4E2516", trim="#B5372A",
              roof="#8C3220", roof_dark="#6E2617", eave="#3A170C", gold="#D7A84A", gold_dark="#A87A2C", door="#2C130A")
FLAGS = ["#2F68B2", "#F3EFE7", "#C8322D", "#2F8A58", "#E6B733"]


def menu():
    rnd = random.Random(11)
    defs, b = [], []
    defs.append('<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#F7F1EA"/><stop offset="0.55" stop-color="#F3E7DA"/><stop offset="1" stop-color="#F1E3D4"/></linearGradient>')
    defs.append('<radialGradient id="sun" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FFF6E6" stop-opacity="1"/><stop offset="1" stop-color="#FFF6E6" stop-opacity="0"/></radialGradient>')
    b.append(full("url(#sky)"))
    b.append('<circle cx="1420" cy="380" r="420" fill="url(#sun)"/>')
    # Farthest range, barely there.
    b.append(hills([(-20, 560), (220, 470), (420, 520), (700, 455), (980, 520), (1250, 470), (1560, 440), (1920, 500), (1940, 520)], 0.05, rnd, "#E4E1E9"))
    # The snow peaks: the great one on the right, lesser ones on the left in more haze.
    for px, py, wl, wr, rock, dark, sn, snd, base in [
        (520, 470, 300, 330, "#CFCFDD", "#BFC0D2", "#FBFBFD", "#E1E3EE", 700),
        (250, 520, 260, 240, "#D3D2DF", "#C5C5D6", "#FBFBFD", "#E3E5EF", 700),
        (1150, 400, 330, 300, "#BFC1D3", "#ABAEC5", "#FFFFFF", "#DCE0EC", 720),
        (1790, 390, 300, 330, "#BFC1D3", "#A9ACC3", "#FFFFFF", "#DCE0EC", 720),
        (1470, 270, 430, 520, "#B3B6CB", "#9A9EB9", "#FFFFFF", "#D6DBE9", 740)]:
        b.append(peak(px, py, wl, wr, base, rnd, rock, dark, sn, snd))
    if BX:
        b.append(peak(-150, 540, 260, 240, 700, EXT, "#D3D2DF", "#C5C5D6", "#FBFBFD", "#E3E5EF"))
        b.append(peak(2140, 450, 300, 300, 720, EXT, "#C3C5D6", "#AFB2C8", "#FFFFFF", "#DCE0EC"))
    b.append(band(560, 760, "#F6EEE6", 0, 0.85, 0.3, "mist1", defs))
    # Mid ranges in blue-green, then the hill the village stands on.
    b.append(hills([(-20, 720), (300, 690), (620, 735), (900, 700), (1240, 730), (1600, 690), (1940, 720)], 0.04, rnd, "#AEB8BB"))
    b.append(hills([(-20, 1010), (520, 960), (820, 890), (1080, 790), (1300, 760), (1560, 735), (1760, 750), (1940, 735)], 0.03, rnd, "#93A39F"))
    # The lake below the village.
    b.append('<path d="M1060 872 C1120 836 1260 826 1420 830 C1600 834 1760 826 1940 812 L1940 884 C1760 896 1520 900 1300 896 C1180 894 1090 890 1060 872 Z" fill="#C7D6DD"/>')
    b.append('<path d="M1120 852 L1300 846 M1400 848 L1640 846 M1700 840 L1900 836" stroke="#E6EEF1" stroke-width="2.5"/>')
    if BX:
        b.append(f'<path d="M1930 812 L{f(W + BX + 20)} 800 L{f(W + BX + 20)} 878 L1930 884 Z" fill="#C7D6DD"/>')
        b.append(f'<path d="M1990 832 L{f(W + BX - 40)} 826" stroke="#E6EEF1" stroke-width="2.5"/>')
    # The village on the slope.
    for i in range(34):
        x = 1100 + rnd.uniform(0, 820)
        y = 790 + (x - 1100) * -0.06 + rnd.uniform(-12, 28)
        w = rnd.uniform(26, 46)
        b.append(house(x, y, w, w * rnd.uniform(0.55, 0.8), "#F4EBE0", "#E1D3C3", rnd.choice(["#B9593A", "#A84E33", "#C46A45"]), "#8E3F28", "#5A3424"))
    for i in range(26):
        x = 1080 + rnd.uniform(0, 860)
        b.append(conifer(x, 810 + rnd.uniform(-30, 30), rnd.uniform(30, 60), rnd.choice(["#6F8473", "#5F7564"]), rnd))
    for i in range(int(BX / 26)):
        x = W + EXT.uniform(-10, BX)
        y = 790 + (x - 1100) * -0.06 + EXT.uniform(-12, 28)
        w = EXT.uniform(26, 46)
        b.append(house(x, y, w, w * EXT.uniform(0.55, 0.8), "#F4EBE0", "#E1D3C3", EXT.choice(["#B9593A", "#A84E33", "#C46A45"]), "#8E3F28", "#5A3424"))
    for i in range(int(BX / 30)):
        x = W + EXT.uniform(-10, BX)
        b.append(conifer(x, 810 + EXT.uniform(-30, 30), EXT.uniform(30, 60), EXT.choice(["#6F8473", "#5F7564"]), EXT))
    b.append(band(780, 960, "#F4EBE2", 0, 0.75, 0.0, "mist2", defs))
    # The pagoda and its prayer flags on the left.
    if BX:
        b.append(flags(string((-20 - BX, 120), (-20, 250), 30), FLAGS[3:] + FLAGS[:3], EXT, 1.0))
    b.append(flags(string((-20, 250), (360, 470), 70), FLAGS, rnd, 1.0))
    b.append(flags(string((360, 470), (760, 600), 46), FLAGS[2:] + FLAGS[:2], rnd, 0.9))
    b.append(pagoda(250, 940, 1.0, TEMPLE))
    b.append(band(900, 1100, "#F3E9DF", 0, 0.9, 1, "ground", defs))
    if BY:
        b.append(f'<rect x="{f(-BX - 2)}" y="1099" width="{f(W + 2 * BX + 4)}" height="{f(BY + 4)}" fill="#F3E9DF"/>')
    if BX:
        b.append(bush(-190, 1060, 280, EXT, "#4E6B45", "#36502F", "#D3242C", "#F06A6E", "#6E0B12", 5))
        b.append(bush(2110, 1040, 300, EXT, "#4E6B45", "#36502F", "#D3242C", "#F06A6E", "#6E0B12", 5))
    # Rhododendrons in the foreground corners.
    b.append(bush(90, 1030, 280, rnd, "#4E6B45", "#36502F", "#D3242C", "#F06A6E", "#6E0B12", 5))
    b.append(bush(360, 1110, 200, rnd, "#56744A", "#3B5634", "#C81F28", "#EE6166", "#6E0B12", 3))
    b.append(bush(1830, 980, 300, rnd, "#4E6B45", "#36502F", "#D3242C", "#F06A6E", "#6E0B12", 5))
    b.append(bush(1580, 1110, 200, rnd, "#56744A", "#3B5634", "#C81F28", "#EE6166", "#6E0B12", 3))
    # A soft lift in the middle, where the logo and buttons stand.
    defs.append('<radialGradient id="lift" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FBF6F0" stop-opacity="0.75"/><stop offset="1" stop-color="#FBF6F0" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="520" rx="640" ry="520" fill="url(#lift)"/>')
    return svg("".join(b), defs)


def table():
    rnd = random.Random(23)
    defs, b = [], []
    defs.append('<linearGradient id="tsky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7FB2E2"/><stop offset="0.6" stop-color="#C4DCF0"/><stop offset="1" stop-color="#E7F0F6"/></linearGradient>')
    b.append(f'<rect width="{W}" height="{H}" fill="url(#tsky)"/>')
    # Clouds: soft clusters of circles.
    for cx, cy, sc in [(520, 92, 0.8), (980, 60, 0.6), (1360, 110, 0.9), (1760, 70, 0.7)]:
        b.append(cloud(cx, cy, sc, rnd))
    # The snow range: bright peaks across the top.
    for px, py, wl, wr, base in [(420, 120, 260, 300, 330), (760, 90, 300, 280, 330), (1080, 20, 380, 420, 340), (1460, 70, 320, 300, 330), (1800, 110, 260, 280, 330)]:
        b.append(peak(px, py, wl, wr, base, rnd, "#8FA2BE", "#73879F", "#FFFFFF", "#D3DEEC", 0.55))
    b.append(band(250, 360, "#E8F1F7", 0, 0.8, 0.2, "thaze", defs))
    # Forested hills, the lake and the village, the pagoda on the left.
    b.append(hills([(-20, 330), (300, 300), (620, 325), (980, 300), (1300, 318), (1940, 300)], 0.03, rnd, "#5E8A5C", 420))
    defs.append('<linearGradient id="court" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#D9CBB7"/><stop offset="1" stop-color="#B8A486"/></linearGradient>')
    b.append(f'<rect x="-20" y="398" width="{W + 40}" height="{H - 380}" fill="url(#court)"/>')
    for i in range(0, 2000, 120):
        b.append(f'<path d="M{i - 40} 400 L{(i - 960) * 2.2 + 960} 1100" stroke="#A8957A" stroke-width="1.4" opacity="0.5"/>')
    for yy in [430, 480, 550, 650]:
        b.append(f'<rect x="-20" y="{yy}" width="{W + 40}" height="1.4" fill="#A8957A" opacity="0.5"/>')
    b.append('<path d="M1060 350 Q1300 330 1560 336 Q1760 340 1940 330 L1940 380 L1060 380 Z" fill="#88BEDD"/>')
    b.append('<path d="M1100 352 L1300 348 M1420 350 L1660 349 M1720 344 L1900 342" stroke="#C9E3F1" stroke-width="2.5"/>')
    for i in range(30):
        x = 1080 + rnd.uniform(0, 860)
        b.append(house(x, 336 + rnd.uniform(-14, 6), rnd.uniform(22, 36), rnd.uniform(14, 22), "#F6F0E8", "#DDD2C6", rnd.choice(["#C2603D", "#B4553A"]), "#8E3F28", "#5A3424"))
    for i in range(40):
        x = rnd.uniform(-20, 1100)
        b.append(conifer(x, 340 + rnd.uniform(-20, 20), rnd.uniform(40, 80), rnd.choice(["#3F6B45", "#335A3A", "#4B7A4C"]), rnd))
    b.append(flags(string((1940, 110), (1660, 236), 46), FLAGS, rnd, 0.8))
    b.append(pagoda(1590, 334, 0.46, TEMPLE))
    # A stone balustrade behind the table, with flowers spilling over.
    b.append('<rect x="-20" y="330" width="1960" height="70" fill="#D6C9B8"/>')
    for i in range(0, 1960, 64):
        b.append(f'<rect x="{i - 20}" y="330" width="2" height="70" fill="#B9A992"/>')
    b.append('<rect x="-20" y="330" width="1960" height="8" fill="#E8DECF"/><rect x="-20" y="364" width="1960" height="2" fill="#B9A992"/>')
    for px in [70, 400, 1520, 1850]:
        b.append(f'<rect x="{px - 34}" y="290" width="68" height="110" fill="#D2C4B1"/><rect x="{px - 40}" y="284" width="80" height="14" fill="#E6DCCD"/>')
    b.append(bush(470, 352, 150, rnd, "#46703F", "#2F5530", "#E0452B", "#F7955B", "#8E1A10", 3))
    b.append(bush(1700, 350, 170, rnd, "#46703F", "#2F5530", "#E0452B", "#F7955B", "#8E1A10", 4))
    # The carved round table, seen from the players' height.
    cx, cy, rx, ry = 954, 780, 1000, 482
    defs.append('<linearGradient id="wood" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8E4D25"/><stop offset="1" stop-color="#4D240F"/></linearGradient>')
    defs.append('<linearGradient id="side" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#5C2C12"/><stop offset="1" stop-color="#2E1407"/></linearGradient>')
    defs.append('<radialGradient id="top" cx="0.5" cy="0.45" r="0.55"><stop offset="0" stop-color="#F7EBDB"/><stop offset="0.8" stop-color="#EEDCC5"/><stop offset="1" stop-color="#E2CCB0"/></radialGradient>')
    b.append(f'<ellipse cx="{cx}" cy="{cy + 54}" rx="{rx + 10}" ry="{ry + 8}" fill="#1E0F06" opacity="0.28"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy + 36}" rx="{rx}" ry="{ry}" fill="url(#side)"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="url(#wood)"/>')
    # The carved band: a gilt meander between two beads, round the rim.
    b.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx - 16}" ry="{ry - 8}" fill="none" stroke="#D9A75E" stroke-width="3"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx - 60}" ry="{ry - 29}" fill="none" stroke="#D9A75E" stroke-width="3"/>')
    n = 72
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        r1x, r1y = rx - 38, ry - 18.5
        x, y = cx + math.cos(a) * r1x, cy + math.sin(a) * r1y
        tang = math.degrees(math.atan2(math.cos(a) * r1y, -math.sin(a) * r1x))
        sc = 0.45 + 0.55 * (0.5 + 0.5 * math.sin(a))
        k = 9 * sc
        key = f"M{f(-2 * k)} {f(1 * k)} h{f(4 * k)} v{f(-2 * k)} h{f(-3 * k)} v{f(1 * k)} h{f(2 * k)}"
        b.append(f'<g transform="translate({f(x)} {f(y)}) rotate({f(tang)})"><path d="{key}" stroke="#E2B46A" stroke-width="{f(2.2 * sc)}" fill="none" stroke-linejoin="round"/></g>')
    b.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx - 64}" ry="{ry - 31}" fill="#5E2E14"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy + 4}" rx="{rx - 82}" ry="{ry - 40}" fill="url(#top)"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy + 4}" rx="{rx - 128}" ry="{ry - 62}" fill="none" stroke="#C9A27A" stroke-width="2" opacity="0.8"/>')
    b.append(f'<ellipse cx="{cx}" cy="{cy + 4}" rx="{rx - 140}" ry="{ry - 68}" fill="none" stroke="#C9A27A" stroke-width="1" opacity="0.6"/>')
    # V1's carved ornaments on the cloth, left and right: nested diamonds with hooked arms.
    for ox, sgn in [(cx - rx + 220, 1), (cx + rx - 220, -1)]:
        oy = cy + 40
        g = f'<g transform="translate({f(ox)} {f(oy)}) scale({sgn} 0.55)" opacity="0.55">'
        for r0 in [44, 30, 16]:
            g += f'<rect x="{-r0}" y="{-r0}" width="{2 * r0}" height="{2 * r0}" transform="rotate(45)" fill="none" stroke="#B88A5A" stroke-width="5"/>'
        g += '<rect x="-6" y="-6" width="12" height="12" transform="rotate(45)" fill="#B88A5A"/>'
        for a in range(4):
            g += f'<path d="M0 {-44 * 1.41:.1f} l0 -22 l14 0" stroke="#B88A5A" stroke-width="5" fill="none" transform="rotate({a * 90})"/>'
        b.append(g + '</g>')
    # Foreground flowers over the table's near corners.
    b.append(bush(30, 1090, 240, rnd, "#46703F", "#2F5530", "#E0452B", "#F7955B", "#8E1A10", 4))
    b.append(bush(1900, 1090, 240, rnd, "#46703F", "#2F5530", "#E0452B", "#F7955B", "#8E1A10", 4))
    return svg("".join(b), defs)


if __name__ == "__main__":
    out = sys.argv[1]
    if len(sys.argv) > 3:
        set_bleed(float(sys.argv[2]), float(sys.argv[3]))
    os.makedirs(out, exist_ok=True)
    for name, fn in [("menu", menu), ("table", table)]:
        with open(os.path.join(out, name + ".svg"), "w", encoding="utf-8") as fh:
            fh.write(fn())
        print(name, os.path.getsize(os.path.join(out, name + ".svg")))
