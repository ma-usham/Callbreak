"""The Background shop's scenes as vector art, in the menu backdrop's style (scenes.py).

Usage: python -P shop_scenes.py <out-dir> [bleed-x bleed-y]   writes pokhara.svg, monastery.svg and
durbar.svg (1920 x 1080, plus the bleed on each side when given: scenes.set_bleed).
Like the menu backdrop (scenes.menu, the Himalayan Vista, the default until 30 Oct 2026), each scene keeps
its subject to the sides and the middle calm, where the logo and the menu buttons stand.
- Durbar Square, the default since 30 Oct 2026 (the owner's pick): golden hour in a Kathmandu durbar
  square, brick temples on both sides, prayer flags, pigeons on the paving.
- Pokhara Lakeside: Phewa lake under the Annapurnas and Machhapuchhre, their reflection, the Tal Barahi
  temple on its island, painted doonga boats, paragliders off Sarangkot, bougainvillea on the bank.
- Monastery Garden: a white stupa with the Buddha's eyes and a gilt spire, prayer flags fanning from it,
  a gompa with a golden roof, prayer wheels, cypresses, peach blossom and marigolds in late light.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scenes as S  # noqa: E402

W, H, f, poly = S.W, S.H, S.f, S.poly


def grad(gid, stops, vertical=True):
    x2, y2 = ("0", "1") if vertical else ("1", "0")
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return f'<linearGradient id="{gid}" x1="0" y1="0" x2="{x2}" y2="{y2}">{s}</linearGradient>'


def doonga(x, y, L, hull, trim, ang, rnd, rower=False, ripple="#EAF1F2"):
    """A painted Phewa doonga seen a little from above: a long narrow hull with upturned ends, its
    dark inside showing, a trim stripe and thwarts; a faint reflection and ripples under it."""
    h = L * 0.11
    g = [f'<g transform="translate({f(x)} {f(y)}) rotate({f(ang)})">']
    # Reflection and ripples first, under the hull.
    g.append(f'<path d="M{f(-L * 0.46)} {f(h * 0.5)} Q0 {f(h * 2.4)} {f(L * 0.46)} {f(h * 0.5)} Z" fill="#2F4A5A" opacity="0.18"/>')
    for k, (dx, w) in enumerate([(-0.3, 0.22), (0.12, 0.3), (0.42, 0.14)]):
        g.append(f'<rect x="{f(L * dx - L * w / 2)}" y="{f(h * (1.5 + k * 0.5))}" width="{f(L * w)}" height="{f(max(1.6, h * 0.12))}" rx="1" fill="{ripple}" opacity="0.7"/>')
    # Inside (the far gunwale is higher than the near one).
    g.append(f'<path d="M{f(-L / 2)} {f(-h * 0.7)} Q0 {f(-h * 1.15)} {f(L / 2)} {f(-h * 0.7)} Q0 {f(-h * 0.1)} {f(-L / 2)} {f(-h * 0.7)} Z" fill="#4A2A17"/>')
    for t in (-0.22, 0.02, 0.24):
        g.append(f'<rect x="{f(L * t - h * 0.12)}" y="{f(-h * 0.78)}" width="{f(h * 0.24)}" height="{f(h * 0.5)}" fill="#8A5A34"/>')
    # Hull: near gunwale to keel.
    g.append(f'<path d="M{f(-L / 2)} {f(-h * 0.7)} Q0 {f(-h * 0.1)} {f(L / 2)} {f(-h * 0.7)} Q{f(L * 0.36)} {f(h * 0.9)} 0 {f(h * 0.9)} Q{f(-L * 0.36)} {f(h * 0.9)} {f(-L / 2)} {f(-h * 0.7)} Z" fill="{hull}"/>')
    g.append(f'<path d="M{f(-L / 2)} {f(-h * 0.7)} Q0 {f(-h * 0.1)} {f(L / 2)} {f(-h * 0.7)} Q0 {f(h * 0.22)} {f(-L / 2)} {f(-h * 0.7)} Z" fill="{trim}"/>')
    g.append(f'<path d="M{f(-L * 0.42)} {f(h * 0.15)} Q0 {f(h * 0.95)} {f(L * 0.42)} {f(h * 0.15)}" stroke="#000000" stroke-opacity="0.18" stroke-width="{f(h * 0.25)}" fill="none"/>')
    if rower:
        # A rower sitting in the stern half, paddle dipping on the near side.
        rx = L * 0.18
        g.append(f'<path d="M{f(rx - h * 0.9)} {f(-h * 2.7)} L{f(rx + h * 0.2)} {f(h * 1.5)}" stroke="#5A3A22" stroke-width="{f(h * 0.22)}" stroke-linecap="round"/>')
        g.append(f'<path d="M{f(rx + h * 0.05)} {f(h * 1.1)} l{f(h * 0.25)} {f(h * 0.7)} l{f(-h * 0.35)} {f(h * 0.1)} Z" fill="#5A3A22"/>')
        g.append(f'<rect x="{f(rx - h * 0.55)}" y="{f(-h * 2.6)}" width="{f(h * 1.1)}" height="{f(h * 2.1)}" rx="{f(h * 0.45)}" fill="#C8432F"/>')
        g.append(f'<circle cx="{f(rx)}" cy="{f(-h * 3.05)}" r="{f(h * 0.48)}" fill="#6B4430"/>')
        g.append(f'<path d="M{f(rx - h * 0.52)} {f(-h * 3.15)} Q{f(rx)} {f(-h * 3.9)} {f(rx + h * 0.52)} {f(-h * 3.15)} Z" fill="#E9DCC6"/>')
    g.append('</g>')
    return "".join(g)


def paraglider(x, y, w, colors, ang):
    """A paraglider: a curved canopy in two colours, its lines, and the pilot hanging below."""
    h = w * 0.28
    g = [f'<g transform="translate({f(x)} {f(y)}) rotate({f(ang)})">']
    for sx in (-0.42, -0.14, 0.14, 0.42):
        g.append(f'<path d="M{f(w * sx)} {f(-h * 0.35 if abs(sx) > 0.3 else -h * 0.75)} L0 {f(h * 2.6)}" stroke="#55606B" stroke-width="0.9" opacity="0.7"/>')
    g.append(f'<path d="M{f(-w / 2)} 0 Q0 {f(-h * 2)} {f(w / 2)} 0 L{f(w * 0.44)} {f(h * 0.3)} Q0 {f(-h * 1.35)} {f(-w * 0.44)} {f(h * 0.3)} Z" fill="{colors[0]}"/>')
    g.append(f'<path d="M{f(-w * 0.16)} {f(-h * 0.92)} Q0 {f(-h * 1.02)} {f(w * 0.16)} {f(-h * 0.92)} L{f(w * 0.15)} {f(-h * 0.5)} Q0 {f(-h * 0.6)} {f(-w * 0.15)} {f(-h * 0.5)} Z" fill="{colors[1]}"/>')
    g.append(f'<circle cx="0" cy="{f(h * 2.75)}" r="{f(w * 0.045)}" fill="#3A3A44"/>')
    g.append('</g>')
    return "".join(g)


def island_temple(cx, wl, rnd):
    """Tal Barahi: a small pagoda on a wooded island, its reflection below."""
    out = []
    out.append(f'<path d="M{cx - 120} {wl + 4} Q{cx - 60} {wl - 30} {cx} {wl - 34} Q{cx + 70} {wl - 30} {cx + 130} {wl + 4} Z" fill="#4E6B47"/>')
    for _ in range(12):
        x = cx + rnd.uniform(-110, 115)
        out.append(S.conifer(x, wl + 2, rnd.uniform(26, 46), rnd.choice(["#3E6142", "#355A3A", "#47704A"]), rnd))
    out.append(S.pagoda(cx - 8, wl - 10, 0.2, S.TEMPLE))
    # Reflection: a dark soft band and a hint of the roofs.
    out.append(f'<path d="M{cx - 120} {wl + 4} Q{cx} {wl + 40} {cx + 130} {wl + 4} Z" fill="#2F4A44" opacity="0.28"/>')
    out.append(f'<rect x="{cx - 34}" y="{wl + 10}" width="52" height="5" fill="#8C3220" opacity="0.25"/>')
    out.append(f'<rect x="{cx - 26}" y="{wl + 22}" width="36" height="4" fill="#8C3220" opacity="0.2"/>')
    return "".join(out)


def along(ctrl, x):
    """The y of a polyline through ctrl at x (clamped to its ends)."""
    for (x0, y0), (x1, y1) in zip(ctrl, ctrl[1:]):
        if min(x0, x1) <= x <= max(x0, x1):
            return y0 + (y1 - y0) * (x - x0) / ((x1 - x0) or 1)
    return ctrl[0][1] if x < ctrl[0][0] else ctrl[-1][1]


def pokhara():
    rnd = random.Random(41)
    defs, b = [], []
    wl = 700  # the lake's far waterline
    defs.append(grad("psky", [(0, "#AFCDE6", 1), (0.5, "#D9E7F0", 1), (0.8, "#F2ECE2", 1), (1, "#F6EEE3", 1)]))
    defs.append('<radialGradient id="psun" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FFF4DD" stop-opacity="0.9"/><stop offset="1" stop-color="#FFF4DD" stop-opacity="0"/></radialGradient>')
    b.append(S.full("url(#psky)"))
    b.append('<circle cx="1180" cy="430" r="460" fill="url(#psun)"/>')
    for cx, cy, sc in [(760, 120, 0.6), (1180, 80, 0.45), (1700, 150, 0.7)]:
        b.append(S.cloud(cx, cy, sc, rnd))
    if S.BX:
        b.append(S.cloud(-130, 130, 0.5, S.EXT))
        b.append(S.cloud(2090, 100, 0.55, S.EXT))

    # The Annapurna range across the back, Machhapuchhre's sharp fishtail on the right.
    mts = []
    mts.append(S.hills([(-20, 600), (200, 520), (420, 560), (640, 500), (900, 560), (1180, 520), (1420, 540), (1700, 500), (1940, 540)], 0.05, rnd, "#D8DEEA", wl))
    for px, py, wl_, wr, rock, dark, sn, snd, base in [
            (330, 400, 300, 280, "#B9C3D6", "#A2AEC6", "#FFFFFF", "#DDE4F0", 640),
            (720, 360, 330, 300, "#B3BED3", "#9CA9C2", "#FFFFFF", "#DAE2EF", 650),
            (1040, 420, 240, 260, "#BCC6D8", "#A6B2C9", "#FFFFFF", "#DFE6F1", 650),
            (1800, 380, 260, 300, "#B3BED3", "#9CA9C2", "#FFFFFF", "#DAE2EF", 650),
            (1566, 250, 120, 190, "#A6B2C9", "#8794B0", "#FFFFFF", "#D3DBEA", 660),
            (1500, 205, 200, 230, "#A6B2C9", "#8794B0", "#FFFFFF", "#D3DBEA", 660)]:
        mts.append(S.peak(px, py, wl_, wr, base, rnd, rock, dark, sn, snd, 0.5 if py > 300 else 0.56))
    if S.BX:
        mts.append(S.peak(-130, 410, 280, 260, 640, S.EXT, "#B9C3D6", "#A2AEC6", "#FFFFFF", "#DDE4F0", 0.5))
        mts.append(S.peak(2150, 400, 280, 300, 650, S.EXT, "#B3BED3", "#9CA9C2", "#FFFFFF", "#DAE2EF", 0.5))
    mts.append(S.band(520, 690, "#EEF2F4", 0, 0.8, 0.45, "phaze", defs))
    # The far shore: low wooded hills along the water with a lakeside town.
    shore = S.hills([(-20, 668), (240, 650), (520, 668), (820, 656), (1100, 672), (1400, 650), (1700, 664), (1940, 652)], 0.03, rnd, "#7F9C8E", wl + 2)
    mts.append(shore)
    town = []
    for _ in range(24):
        x = rnd.uniform(1180, 1900)
        town.append(S.house(x, wl - 2 + rnd.uniform(-6, 2), rnd.uniform(16, 26), rnd.uniform(10, 16), "#F4EEE6", "#DCD2C6", rnd.choice(["#C2603D", "#B4553A", "#3F78A8"]), "#8E3F28", "#5A3424"))
    for _ in range(26):
        x = rnd.uniform(0, 1200)
        town.append(S.conifer(x, wl + 2, rnd.uniform(18, 34), rnd.choice(["#5E7E6C", "#557562"]), rnd))
    for _ in range(int(S.BX / 34)):
        x = S.EXT.uniform(W - 20, W + S.BX)
        town.append(S.house(x, wl - 2 + S.EXT.uniform(-6, 2), S.EXT.uniform(16, 26), S.EXT.uniform(10, 16), "#F4EEE6", "#DCD2C6", S.EXT.choice(["#C2603D", "#B4553A", "#3F78A8"]), "#8E3F28", "#5A3424"))
    for _ in range(int(S.BX / 12)):
        x = S.EXT.uniform(-20 - S.BX, 0)
        town.append(S.conifer(x, wl + 2, S.EXT.uniform(18, 34), S.EXT.choice(["#5E7E6C", "#557562"]), S.EXT))
    mts.append("".join(town))
    b.append("".join(mts))

    # Phewa: the water, the mirrored range fading as it comes toward us, ripples.
    defs.append(grad("lake", [(0, "#C9DCE4", 1), (0.35, "#A9C6D3", 1), (1, "#7FA6B8", 1)]))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="{wl}" width="{f(W + 40 + 2 * S.BX)}" height="{f(H - wl + 20 + S.BY)}" fill="url(#lake)"/>')
    b.append(f'<g transform="matrix(1 0 0 -1 0 {2 * wl})" opacity="0.42">{"".join(mts)}</g>')
    defs.append(grad("lfade", [(0, "#A9C6D3", 0), (0.25, "#9DBCCB", 0.45), (0.6, "#88AEC0", 0.85), (1, "#7FA6B8", 1)]))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="{wl}" width="{f(W + 40 + 2 * S.BX)}" height="{f(H - wl + 20 + S.BY)}" fill="url(#lfade)"/>')
    b.append(f'<rect x="{f(-20 - S.BX)}" y="{wl}" width="{f(W + 40 + 2 * S.BX)}" height="3" fill="#EEF4F5" opacity="0.8"/>')
    # Sarangkot's slopes, drawn now so their reflection lies under the ripples.
    hill_a = [(-20, 380), (120, 360), (300, 420), (470, 520), (620, 640), (760, 762)]
    hill_b = [(-20, 520), (200, 540), (420, 620), (600, 720), (700, 772)]
    sarangkot = S.hills(hill_a, 0.04, rnd, "#5E8A5C", 762) + S.hills(hill_b, 0.03, rnd, "#4F7A4E", 772)
    b.append(f'<g transform="matrix(1 0 0 -1 0 1534)" opacity="0.28">{sarangkot}</g>')
    for _ in range(40):
        y = rnd.uniform(wl + 14, H - 40)
        x = rnd.uniform(-20, W)
        w = rnd.uniform(40, 160) * (0.6 + (y - wl) / 500)
        b.append(f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(1.4 + (y - wl) / 220)}" rx="1" fill="#EAF2F4" opacity="{f(rnd.uniform(0.35, 0.7))}"/>')
    for _ in range(int((S.BX + S.BY) / 14)):
        y = S.EXT.uniform(wl + 14, H + S.BY - 20)
        x = S.EXT.uniform(-20 - S.BX, W + S.BX) if y > H - 40 else S.EXT.choice([S.EXT.uniform(-20 - S.BX, -60), S.EXT.uniform(W - 40, W + S.BX)])
        w = S.EXT.uniform(40, 160) * (0.6 + (y - wl) / 500)
        b.append(f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(1.4 + (y - wl) / 220)}" rx="1" fill="#EAF2F4" opacity="{f(S.EXT.uniform(0.35, 0.7))}"/>')

    # Tal Barahi on its island, right of the buttons.
    b.append(island_temple(1470, wl + 70, rnd))

    # Sarangkot: the wooded hill on the left, dropping to the water, with houses and its paragliders.
    b.append(sarangkot)
    trees = []
    for _ in range(60):
        x = rnd.uniform(-10, 720)
        top = along(hill_a, x)
        if top + 30 > 748:
            continue
        trees.append((x, rnd.uniform(top + 30, min(top + 180, 752))))
    for x, y in sorted(trees, key=lambda t: t[1]):
        b.append(S.conifer(x, y, rnd.uniform(30, 58) * (0.8 + (y - 380) / 1400), rnd.choice(["#3F6B45", "#355E3C", "#4A7A4B"]), rnd))
    more = [(S.EXT.uniform(-20 - S.BX, -10), S.EXT.uniform(410, 752)) for _ in range(int(S.BX / 6))]
    for x, y in sorted(more, key=lambda t: t[1]):
        b.append(S.conifer(x, y, S.EXT.uniform(30, 58) * (0.8 + (y - 380) / 1400), S.EXT.choice(["#3F6B45", "#355E3C", "#4A7A4B"]), S.EXT))
    for _ in range(9):
        x = rnd.uniform(40, 460)
        y = along(hill_a, x) + rnd.uniform(70, 120)
        w = rnd.uniform(22, 34)
        b.append(S.house(x, y, w, w * 0.62, "#F4ECE1", "#DCCFBF", rnd.choice(["#B9593A", "#A84E33"]), "#8E3F28", "#5A3424"))
    for x, y, w, cols, a in [(300, 210, 70, ("#E0452B", "#F3D27A"), -8), (500, 300, 52, ("#2F68B2", "#F3EFE7"), 6), (150, 330, 44, ("#E6B733", "#C8322D"), -4)]:
        b.append(paraglider(x, y, w, cols, a))
    # A low bank on the right with a few trees.
    bank = [(1500, 1100), (1600, 900), (1760, 800), (1940, 762)]
    rx, by = W + 20 + S.BX, 1100 + S.BY
    b.append(f'<path d="M{f(1480 - 0.4 * S.BY)} {f(by)} L1480 1100 C1540 980 1620 860 1760 800 Q1850 768 1940 760 L{f(rx)} 756 L{f(rx)} {f(by)} Z" fill="#56804F"/>')
    b.append('<path d="M1480 1100 C1540 980 1620 860 1760 800 Q1850 768 1940 760 L1940 772 Q1850 780 1764 812 C1630 870 1552 990 1498 1100 Z" fill="#79A266"/>')
    if S.BX or S.BY:
        b.append(f'<path d="M1938 760 L{f(rx)} 756 L{f(rx)} 768 L1938 772 Z M1480 1100 L1498 1100 L{f(1498 - 0.4 * S.BY)} {f(by)} L{f(1480 - 0.4 * S.BY)} {f(by)} Z" fill="#79A266"/>')
    for x in sorted(rnd.uniform(1700, 1940) for _ in range(10)):
        b.append(S.conifer(x, along(bank, x) + rnd.uniform(14, 40), rnd.uniform(40, 70), rnd.choice(["#3F6B45", "#355E3C"]), rnd))
    for x in sorted(S.EXT.uniform(1940, W + S.BX) for _ in range(int(S.BX / 30))):
        b.append(S.conifer(x, 762 + S.EXT.uniform(14, 60), S.EXT.uniform(40, 70), S.EXT.choice(["#3F6B45", "#355E3C"]), S.EXT))

    # Boats: one being rowed out, the rest tied up along the near bank.
    b.append(doonga(1500, 890, 170, "#2F68B2", "#E6B733", -3, rnd, rower=True))
    b.append(doonga(1430, 1010, 150, "#C8322D", "#F3EFE7", 4, rnd))
    # A soft light patch where the menu's tagline sits.
    defs.append('<radialGradient id="pfoot" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#EEF3F2" stop-opacity="0.7"/><stop offset="1" stop-color="#EEF3F2" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="1050" rx="620" ry="120" fill="url(#pfoot)"/>')
    # The near bank, bottom left: grass, a plank jetty and the moored doongas.
    lx = -20 - S.BX
    b.append(f'<path d="M{f(lx)} 905 L-20 900 Q160 880 360 930 Q520 975 640 1100 L{f(640 + 0.5 * S.BY)} {f(by)} L{f(lx)} {f(by)} Z" fill="#5C8650"/>')
    b.append(f'<path d="M-20 900 Q160 880 360 930 Q520 975 640 1100 L610 1100 Q500 990 350 948 Q170 904 -20 920 Z" fill="#7DA466"/>')
    if S.BX or S.BY:
        b.append(f'<path d="M{f(lx)} 905 L-20 900 L-20 920 L{f(lx)} 925 Z M640 1100 L{f(640 + 0.5 * S.BY)} {f(by)} L{f(610 + 0.5 * S.BY)} {f(by)} L610 1100 Z" fill="#7DA466"/>')
    b.append('<path d="M330 935 L560 880 L572 892 L346 952 Z" fill="#8E6A47"/><path d="M346 952 L572 892 L572 900 L348 960 Z" fill="#5E4330"/>')
    for k in range(6):
        px = 360 + k * 40
        py = 940 - k * 9.6
        b.append(f'<rect x="{f(px)}" y="{f(py)}" width="5" height="34" fill="#5E4330"/>')
    for x, y, L, hull, trim, a in [(470, 990, 190, "#2F8A58", "#E6B733", -14), (560, 1050, 200, "#E6B733", "#C8322D", -16), (690, 960, 170, "#C8322D", "#2F68B2", -12)]:
        b.append(doonga(x, y, L, hull, trim, a, rnd))
    # Bougainvillea over the bank's corner and on the right.
    b.append(S.bush(70, 1040, 260, rnd, "#4E7A45", "#36592F", "#C2286E", "#EE76A8", "#5E0B2E", 5))
    b.append(S.bush(1840, 1060, 280, rnd, "#4E7A45", "#36592F", "#C2286E", "#EE76A8", "#5E0B2E", 5))
    if S.BX:
        b.append(S.bush(-200, 1010, 250, S.EXT, "#4E7A45", "#36592F", "#C2286E", "#EE76A8", "#5E0B2E", 5))
        b.append(S.bush(2110, 1030, 270, S.EXT, "#4E7A45", "#36592F", "#C2286E", "#EE76A8", "#5E0B2E", 5))
    # Keep the middle calm for the logo and the buttons.
    defs.append('<radialGradient id="plift" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#F6F2EA" stop-opacity="0.55"/><stop offset="1" stop-color="#F6F2EA" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="500" rx="560" ry="460" fill="url(#plift)"/>')
    return S.svg("".join(b), defs)


def stupa(cx, ground, s, rnd):
    """A Boudha-style stupa: three whitewashed terraces, the dome with saffron arcs, the harmika with
    the Buddha's eyes, thirteen gilt steps, the parasol and finial. Returns (svg, spire top)."""
    out = []
    y = ground
    for i, w in enumerate([560, 470, 380]):
        w *= s
        hgt = 34 * s
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - hgt)}" width="{f(w)}" height="{f(hgt)}" fill="#F3EDE3"/>')
        out.append(f'<rect x="{f(cx + w * 0.16)}" y="{f(y - hgt)}" width="{f(w * 0.34)}" height="{f(hgt)}" fill="#DCD2C4"/>')
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - hgt)}" width="{f(w)}" height="{f(5 * s)}" fill="#FFFFFF"/>')
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - 6 * s)}" width="{f(w)}" height="{f(6 * s)}" fill="#C9BBA8"/>')
        # Niches with butter lamps along the terrace.
        for k in range(int(w / (58 * s))):
            nx = cx - w / 2 + 22 * s + k * 58 * s
            out.append(f'<path d="M{f(nx)} {f(y - 8 * s)} v{f(-14 * s)} q{f(6 * s)} {f(-8 * s)} {f(12 * s)} 0 v{f(14 * s)} Z" fill="#B89C74" opacity="0.7"/>')
            out.append(f'<circle cx="{f(nx + 6 * s)}" cy="{f(y - 13 * s)}" r="{f(2.4 * s)}" fill="#F3C25A"/>')
        y -= hgt
    # The dome.
    rx, ry = 190 * s, 150 * s
    out.append(f'<path d="M{f(cx - rx)} {f(y)} A{f(rx)} {f(ry)} 0 0 1 {f(cx + rx)} {f(y)} Z" fill="#FBF8F2"/>')
    out.append(f'<path d="M{f(cx + rx * 0.18)} {f(y - ry * 0.98)} A{f(rx)} {f(ry)} 0 0 1 {f(cx + rx)} {f(y)} L{f(cx + rx * 0.35)} {f(y)} Q{f(cx + rx * 0.5)} {f(y - ry * 0.6)} {f(cx + rx * 0.18)} {f(y - ry * 0.98)} Z" fill="#E6DED1"/>')
    out.append(f'<rect x="{f(cx - rx)}" y="{f(y - 4 * s)}" width="{f(2 * rx)}" height="{f(6 * s)}" fill="#D7CBB8"/>')
    y -= ry * 0.97
    # The harmika: a gilt-edged box with the eyes on its face.
    hw, hh = 104 * s, 84 * s
    out.append(f'<rect x="{f(cx - hw / 2 - 6 * s)}" y="{f(y - 8 * s)}" width="{f(hw + 12 * s)}" height="{f(10 * s)}" fill="#C99A3E"/>')
    out.append(f'<rect x="{f(cx - hw / 2)}" y="{f(y - hh)}" width="{f(hw)}" height="{f(hh - 6 * s)}" fill="#F7F1E6"/>')
    out.append(f'<rect x="{f(cx + hw * 0.3)}" y="{f(y - hh)}" width="{f(hw * 0.2)}" height="{f(hh - 6 * s)}" fill="#E2D8C8"/>')
    ey = y - hh * 0.5
    for sgn in (-1, 1):
        ex = cx + sgn * hw * 0.24
        out.append(f'<path d="M{f(ex - 15 * s)} {f(ey - 13 * s)} Q{f(ex)} {f(ey - 22 * s)} {f(ex + 15 * s)} {f(ey - 13 * s)}" stroke="#2A1C14" stroke-width="{f(3.2 * s)}" fill="none" stroke-linecap="round"/>')
        out.append(f'<path d="M{f(ex - 15 * s)} {f(ey)} Q{f(ex)} {f(ey - 10 * s)} {f(ex + 15 * s)} {f(ey)} Q{f(ex)} {f(ey + 6 * s)} {f(ex - 15 * s)} {f(ey)} Z" fill="#FFFFFF" stroke="#2A1C14" stroke-width="{f(2 * s)}"/>')
        out.append(f'<circle cx="{f(ex + sgn * -1.5 * s)}" cy="{f(ey - 1.5 * s)}" r="{f(4.4 * s)}" fill="#2A4D8F"/><circle cx="{f(ex + sgn * -1.5 * s)}" cy="{f(ey - 1.5 * s)}" r="{f(2 * s)}" fill="#14100C"/>')
    # The curl between the eyes and the urna above.
    out.append(f'<path d="M{f(cx - 4 * s)} {f(ey - 4 * s)} q{f(10 * s)} {f(-2 * s)} {f(6 * s)} {f(8 * s)} q{f(-4 * s)} {f(8 * s)} {f(4 * s)} {f(12 * s)}" stroke="#2A1C14" stroke-width="{f(2.8 * s)}" fill="none" stroke-linecap="round"/>')
    out.append(f'<circle cx="{f(cx)}" cy="{f(ey - 24 * s)}" r="{f(3 * s)}" fill="#C8322D"/>')
    out.append(f'<rect x="{f(cx - hw / 2 - 8 * s)}" y="{f(y - hh - 10 * s)}" width="{f(hw + 16 * s)}" height="{f(12 * s)}" fill="#D7A84A"/>')
    y -= hh + 10 * s
    # Thirteen gilt steps.
    for i in range(13):
        w = (88 - i * 4.4) * s
        out.append(f'<rect x="{f(cx - w / 2)}" y="{f(y - 11 * s)}" width="{f(w)}" height="{f(10 * s)}" fill="{"#D7A84A" if i % 2 == 0 else "#C08F35"}"/>')
        out.append(f'<rect x="{f(cx + w * 0.18)}" y="{f(y - 11 * s)}" width="{f(w * 0.32)}" height="{f(10 * s)}" fill="#A87A2C" opacity="0.5"/>')
        y -= 11 * s
    # The parasol and the finial.
    out.append(f'<path d="M{f(cx - 46 * s)} {f(y + 2 * s)} Q{f(cx)} {f(y - 30 * s)} {f(cx + 46 * s)} {f(y + 2 * s)} Z" fill="#D7A84A"/>')
    out.append(f'<path d="M{f(cx)} {f(y - 14 * s)} Q{f(cx + 30 * s)} {f(y - 8 * s)} {f(cx + 46 * s)} {f(y + 2 * s)} L{f(cx)} {f(y + 2 * s)} Z" fill="#B88A35"/>')
    for k in range(-4, 5):
        out.append(f'<rect x="{f(cx + k * 10 * s - 1 * s)}" y="{f(y + 2 * s)}" width="{f(2 * s)}" height="{f(8 * s)}" fill="#A87A2C"/>')
    y -= 22 * s
    for w, h in [(16, 10), (10, 14), (18, 9), (8, 16)]:
        out.append(f'<rect x="{f(cx - w * s / 2)}" y="{f(y - h * s)}" width="{f(w * s)}" height="{f(h * s)}" rx="{f(3 * s)}" fill="#E0B354"/>')
        y -= h * s
    out.append(f'<path d="M{f(cx - 4 * s)} {f(y)} L{f(cx + 4 * s)} {f(y)} L{f(cx)} {f(y - 18 * s)} Z" fill="#E0B354"/>')
    return "".join(out), (cx, y + 30 * s)


def prayer_wheels(x0, x1, ground, s):
    out = [f'<rect x="{f(x0 - 10 * s)}" y="{f(ground - 52 * s)}" width="{f(x1 - x0 + 20 * s)}" height="{f(8 * s)}" fill="#7A2A1E"/>',
           f'<rect x="{f(x0 - 10 * s)}" y="{f(ground - 8 * s)}" width="{f(x1 - x0 + 20 * s)}" height="{f(8 * s)}" fill="#7A2A1E"/>']
    n = int((x1 - x0) / (24 * s))
    for k in range(n):
        x = x0 + k * (x1 - x0) / n + 3 * s
        out.append(f'<rect x="{f(x)}" y="{f(ground - 42 * s)}" width="{f(16 * s)}" height="{f(32 * s)}" rx="{f(3 * s)}" fill="#B5372A"/>')
        out.append(f'<rect x="{f(x)}" y="{f(ground - 42 * s)}" width="{f(16 * s)}" height="{f(5 * s)}" fill="#D7A84A"/>')
        out.append(f'<rect x="{f(x)}" y="{f(ground - 15 * s)}" width="{f(16 * s)}" height="{f(5 * s)}" fill="#D7A84A"/>')
        out.append(f'<rect x="{f(x + 10 * s)}" y="{f(ground - 37 * s)}" width="{f(4 * s)}" height="{f(22 * s)}" fill="#7A2A1E" opacity="0.5"/>')
    return "".join(out)


def gompa(x, ground, w, h):
    """A gompa: tapering whitewashed walls, black-framed windows, the dark red penbey band under a
    golden roof with victory banners and the Dharma wheel."""
    out = []
    t = w * 0.04
    out.append(f'<path d="{poly([(x, ground), (x + w, ground), (x + w - t, ground - h), (x + t, ground - h)])}" fill="#F4EEE4"/>')
    out.append(f'<path d="{poly([(x + w * 0.62, ground), (x + w, ground), (x + w - t, ground - h), (x + w * 0.62, ground - h)])}" fill="#DED4C6"/>')
    out.append(f'<rect x="{f(x + t)}" y="{f(ground - h - h * 0.16)}" width="{f(w - 2 * t)}" height="{f(h * 0.16)}" fill="#7A2A1E"/>')
    for k in range(int((w - 2 * t) / 14)):
        out.append(f'<circle cx="{f(x + t + 7 + k * 14)}" cy="{f(ground - h - h * 0.08)}" r="2.6" fill="#D7A84A" opacity="0.8"/>')
    out.append(f'<rect x="{f(x + t - 6)}" y="{f(ground - h - h * 0.2)}" width="{f(w - 2 * t + 12)}" height="{f(h * 0.05)}" fill="#3A170C"/>')
    # Windows: two rows, a big door in the middle.
    for row, wy in enumerate([ground - h * 0.78, ground - h * 0.42]):
        for k in range(4):
            if row == 1 and k in (1, 2):
                continue
            wx = x + w * (0.12 + k * 0.21)
            ww, wh = w * 0.1, h * 0.17
            out.append(f'<path d="{poly([(wx - ww * 0.25, wy + wh), (wx + ww * 1.25, wy + wh), (wx + ww * 1.1, wy - wh * 0.1), (wx - ww * 0.1, wy - wh * 0.1)])}" fill="#1E1611"/>')
            out.append(f'<rect x="{f(wx)}" y="{f(wy)}" width="{f(ww)}" height="{f(wh * 0.85)}" fill="#7A2A1E"/>')
            out.append(f'<rect x="{f(wx - ww * 0.2)}" y="{f(wy - wh * 0.32)}" width="{f(ww * 1.4)}" height="{f(wh * 0.2)}" fill="#C8322D"/>')
            out.append(f'<rect x="{f(wx - ww * 0.2)}" y="{f(wy - wh * 0.14)}" width="{f(ww * 1.4)}" height="{f(wh * 0.08)}" fill="#2F68B2"/>')
    dx, dw, dh = x + w * 0.4, w * 0.2, h * 0.38
    out.append(f'<rect x="{f(dx - 6)}" y="{f(ground - dh - 8)}" width="{f(dw + 12)}" height="{f(dh + 8)}" fill="#1E1611"/>')
    out.append(f'<rect x="{f(dx)}" y="{f(ground - dh)}" width="{f(dw)}" height="{f(dh)}" fill="#A8321F"/>')
    out.append(f'<rect x="{f(dx + dw / 2 - 1.5)}" y="{f(ground - dh)}" width="3" height="{f(dh)}" fill="#6E1E12"/>')
    out.append(f'<rect x="{f(dx - 14)}" y="{f(ground - dh - 22)}" width="{f(dw + 28)}" height="12" fill="#C8322D"/>')
    out.append(f'<path d="{poly([(dx - 26, ground - dh - 22), (dx + dw + 26, ground - dh - 22), (dx + dw + 10, ground - dh - 40), (dx - 10, ground - dh - 40)])}" fill="#D7A84A"/>')
    # Golden roof pavilion, banners at the corners, the wheel at the front.
    top = ground - h - h * 0.2
    rw = w * 0.5
    rx0 = x + w / 2 - rw / 2
    out.append(f'<rect x="{f(rx0 + rw * 0.12)}" y="{f(top - h * 0.18)}" width="{f(rw * 0.76)}" height="{f(h * 0.18)}" fill="#7A2A1E"/>')
    out.append(f'<path d="{poly([(rx0 - 14, top - h * 0.16), (rx0 + rw + 14, top - h * 0.16), (rx0 + rw * 0.78, top - h * 0.42), (rx0 + rw * 0.22, top - h * 0.42)])}" fill="#D7A84A"/>')
    out.append(f'<path d="{poly([(x + w / 2, top - h * 0.42), (rx0 + rw * 0.78, top - h * 0.42), (rx0 + rw + 14, top - h * 0.16), (x + w / 2 + rw * 0.2, top - h * 0.16)])}" fill="#B88A35"/>')
    out.append(f'<rect x="{f(x + w / 2 - 5)}" y="{f(top - h * 0.6)}" width="10" height="{f(h * 0.18)}" rx="4" fill="#E0B354"/>')
    for bx in (x + t + 8, x + w - t - 8):
        out.append(f'<rect x="{f(bx - 7)}" y="{f(top - h * 0.24)}" width="14" height="{f(h * 0.24)}" rx="6" fill="#E0B354"/>')
        out.append(f'<rect x="{f(bx - 9)}" y="{f(top - h * 0.12)}" width="18" height="6" fill="#B88A35"/>')
    wx, wy, wr = x + w / 2, top - h * 0.06, h * 0.075
    out.append(f'<circle cx="{f(wx)}" cy="{f(wy - wr)}" r="{f(wr)}" fill="none" stroke="#E0B354" stroke-width="4"/>')
    for k in range(8):
        a = math.radians(k * 22.5)
        out.append(f'<path d="M{f(wx - math.cos(a) * wr)} {f(wy - wr - math.sin(a) * wr)} L{f(wx + math.cos(a) * wr)} {f(wy - wr + math.sin(a) * wr)}" stroke="#E0B354" stroke-width="2"/>')
    for sgn in (-1, 1):
        out.append(f'<ellipse cx="{f(wx + sgn * wr * 1.9)}" cy="{f(wy - wr * 0.5)}" rx="{f(wr * 0.7)}" ry="{f(wr * 0.45)}" fill="#E0B354"/>')
    return "".join(out)


def cypress(x, y, h, col, col_dark):
    w = h * 0.2
    return (f'<path d="M{f(x)} {f(y - h)} C{f(x + w * 0.7)} {f(y - h * 0.7)} {f(x + w * 0.6)} {f(y - h * 0.15)} {f(x + w * 0.25)} {f(y)} L{f(x - w * 0.25)} {f(y)} C{f(x - w * 0.6)} {f(y - h * 0.15)} {f(x - w * 0.7)} {f(y - h * 0.7)} {f(x)} {f(y - h)} Z" fill="{col}"/>'
            f'<path d="M{f(x)} {f(y - h)} C{f(x + w * 0.7)} {f(y - h * 0.7)} {f(x + w * 0.6)} {f(y - h * 0.15)} {f(x + w * 0.25)} {f(y)} L{f(x + w * 0.05)} {f(y)} C{f(x + w * 0.3)} {f(y - h * 0.4)} {f(x + w * 0.2)} {f(y - h * 0.8)} {f(x)} {f(y - h)} Z" fill="{col_dark}"/>')


def blossom_tree(x, y, s, rnd, pink, pink_light, trunk="#6B4A3A"):
    """A peach tree in flower: a forked trunk and a crown of overlapping blossom puffs."""
    out = [f'<path d="M{f(x - 8 * s)} {f(y)} L{f(x - 4 * s)} {f(y - 90 * s)} L{f(x - 50 * s)} {f(y - 150 * s)} L{f(x - 42 * s)} {f(y - 156 * s)} L{f(x)} {f(y - 110 * s)} L{f(x + 38 * s)} {f(y - 160 * s)} L{f(x + 46 * s)} {f(y - 154 * s)} L{f(x + 6 * s)} {f(y - 92 * s)} L{f(x + 10 * s)} {f(y)} Z" fill="{trunk}"/>']
    puffs = [(rnd.uniform(-120, 120) * s, rnd.uniform(-230, -120) * s, rnd.uniform(34, 56) * s) for _ in range(16)]
    puffs.sort(key=lambda p: p[1])
    for px, py, r in puffs:
        out.append(f'<circle cx="{f(x + px)}" cy="{f(y + py)}" r="{f(r)}" fill="{pink}"/>')
    for px, py, r in puffs[:9]:
        out.append(f'<circle cx="{f(x + px - r * 0.2)}" cy="{f(y + py - r * 0.25)}" r="{f(r * 0.55)}" fill="{pink_light}"/>')
    for _ in range(40):
        px, py = rnd.uniform(-140, 140) * s, rnd.uniform(-250, -110) * s
        out.append(f'<circle cx="{f(x + px)}" cy="{f(y + py)}" r="{f(rnd.uniform(2.5, 4.5) * s)}" fill="#FFFFFF" opacity="0.8"/>')
    return "".join(out)


def marigolds(x0, x1, y, rnd, rows=2):
    out = [f'<rect x="{f(x0)}" y="{f(y - 6)}" width="{f(x1 - x0)}" height="{f(14 + rows * 10)}" rx="8" fill="#5D7F3E"/>']
    for r in range(rows + 1):
        for k in range(int((x1 - x0) / 16)):
            cx = x0 + 8 + k * 16 + rnd.uniform(-3, 3) + (r % 2) * 8
            if cx > x1 - 6:
                continue
            cy = y + r * 10 + rnd.uniform(-3, 3)
            col = rnd.choice(["#F2A019", "#E8801A", "#F6C12E"])
            out.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(rnd.uniform(6, 8))}" fill="{col}"/><circle cx="{f(cx)}" cy="{f(cy)}" r="2.2" fill="#B5560E"/>')
    return "".join(out)


def birds(pts, s=1.0):
    return "".join(f'<path d="M{f(x - 9 * s)} {f(y - 2 * s)} Q{f(x - 4 * s)} {f(y - 7 * s)} {f(x)} {f(y)} Q{f(x + 4 * s)} {f(y - 7 * s)} {f(x + 9 * s)} {f(y - 2 * s)}" stroke="#5A4A44" stroke-width="{f(1.8 * s)}" fill="none" stroke-linecap="round"/>' for x, y in pts)


def monastery():
    rnd = random.Random(53)
    defs, b = [], []
    defs.append(grad("msky", [(0, "#F1DFC2", 1), (0.5, "#F6EAD8", 1), (1, "#F2E8DA", 1)]))
    defs.append('<radialGradient id="msun" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FFF3D6" stop-opacity="1"/><stop offset="1" stop-color="#FFF3D6" stop-opacity="0"/></radialGradient>')
    b.append(S.full("url(#msky)"))
    b.append('<circle cx="1480" cy="420" r="480" fill="url(#msun)"/>')
    b.append(S.hills([(-20, 560), (260, 500), (560, 540), (860, 490), (1200, 540), (1500, 500), (1940, 530)], 0.05, rnd, "#E6DEDC", 700))
    for px, py, wl, wr, rock, dark, sn, snd, base in [
            (240, 400, 280, 260, "#C9C0C8", "#B4AAB6", "#FFFFFF", "#E6E1E8", 640),
            (640, 340, 320, 300, "#C2B9C3", "#AAA0AF", "#FFFFFF", "#E2DCE6", 650),
            (1080, 380, 280, 300, "#C9C0C8", "#B4AAB6", "#FFFFFF", "#E6E1E8", 650),
            (1760, 360, 300, 300, "#C2B9C3", "#AAA0AF", "#FFFFFF", "#E2DCE6", 650)]:
        b.append(S.peak(px, py, wl, wr, base, rnd, rock, dark, sn, snd, 0.5))
    if S.BX:
        b.append(S.peak(-160, 420, 280, 260, 640, S.EXT, "#C2B9C3", "#AAA0AF", "#FFFFFF", "#E2DCE6", 0.5))
        b.append(S.peak(2140, 400, 280, 300, 650, S.EXT, "#C9C0C8", "#B4AAB6", "#FFFFFF", "#E6E1E8", 0.5))
    b.append(S.band(520, 720, "#F6ECDF", 0, 0.85, 0.4, "mhaze", defs))
    # Terraced hills and the valley's villages.
    b.append(S.hills([(-20, 690), (300, 660), (640, 690), (980, 650), (1320, 676), (1660, 648), (1940, 670)], 0.03, rnd, "#A8B78E", 800))
    for i in range(4):
        y0 = 690 + i * 20
        b.append(f'<path d="M{f(-20 - S.BX)} {y0} L-20 {y0} Q480 {y0 - 28 + i * 4} 960 {y0 - 6} T1940 {y0 - 18} L{f(W + 20 + S.BX)} {y0 - 18}" stroke="#C2CDA6" stroke-width="2" fill="none" opacity="0.6"/>')
    for _ in range(16):
        x = rnd.uniform(620, 1260)
        b.append(S.house(x, 690 + rnd.uniform(-10, 14), rnd.uniform(16, 24), rnd.uniform(10, 14), "#F4EEE6", "#DCD2C6", rnd.choice(["#C2603D", "#B4553A"]), "#8E3F28", "#5A3424"))
    b.append(S.hills([(-20, 760), (400, 735), (800, 760), (1200, 740), (1600, 756), (1940, 738)], 0.025, rnd, "#8DA570", 820))
    for _ in range(30):
        x = rnd.uniform(-20, 1940)
        if 600 < x < 1320:
            continue
        b.append(S.conifer(x, 760 + rnd.uniform(-10, 14), rnd.uniform(30, 56), rnd.choice(["#5C7D4A", "#4F6F40"]), rnd))
    for _ in range(int(S.BX / 22)):
        x = S.EXT.choice([S.EXT.uniform(-20 - S.BX, -20), S.EXT.uniform(W + 20, W + S.BX)])
        b.append(S.conifer(x, 760 + S.EXT.uniform(-10, 14), S.EXT.uniform(30, 56), S.EXT.choice(["#5C7D4A", "#4F6F40"]), S.EXT))
    # The garden's lawn.
    defs.append(grad("lawn", [(0, "#A3B873", 1), (0.5, "#8FA963", 1), (1, "#6F8C4A", 1)]))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="780" width="{f(W + 40 + 2 * S.BX)}" height="{f(H - 760 + S.BY)}" fill="url(#lawn)"/>')
    for _ in range(70):
        x, y = rnd.uniform(-20, W), rnd.uniform(800, H)
        b.append(f'<path d="M{f(x)} {f(y)} l2 -7 l2 7 l3 -9 l2 9" stroke="#7E9A55" stroke-width="1.5" fill="none" opacity="0.6"/>')
    for _ in range(int((S.BX + S.BY) / 6)):
        y = S.EXT.uniform(800, H + S.BY)
        x = S.EXT.uniform(-20 - S.BX, W + S.BX) if y > H else S.EXT.choice([S.EXT.uniform(-20 - S.BX, -20), S.EXT.uniform(W, W + S.BX)])
        b.append(f'<path d="M{f(x)} {f(y)} l2 -7 l2 7 l3 -9 l2 9" stroke="#7E9A55" stroke-width="1.5" fill="none" opacity="0.6"/>')
    if S.BX:
        b.append(cypress(-140, 830, 280, "#4D6E43", "#3C5A35"))
        b.append(cypress(2060, 830, 270, "#4D6E43", "#3C5A35"))
        b.append(cypress(2130, 830, 220, "#4D6E43", "#3C5A35"))
    # Cypresses and peach trees behind the stupa and the gompa.
    for x, h in [(1150, 260), (1210, 320), (1840, 300), (1900, 240), (560, 280), (40, 260)]:
        b.append(cypress(x, 830, h, "#4D6E43", "#3C5A35"))
    b.append(blossom_tree(1260, 840, 0.9, rnd, "#EFA3B5", "#F8CCD6"))
    b.append(blossom_tree(1780, 840, 0.85, rnd, "#EFA3B5", "#F8CCD6"))

    # The stupa on the right, flags fanning from its spire to the ground.
    st, (tx, ty) = stupa(1500, 880, 0.95, rnd)
    for ex, ey, sag in [(1090, 880, 60), (1190, 850, 44), (1910, 880, 60), (1810, 850, 44)]:
        b.append(S.flags(S.string((tx, ty), (ex, ey), sag, 18), S.FLAGS, rnd, 0.55))
    b.append(st)
    b.append(prayer_wheels(1250, 1750, 905, 0.95))
    b.append(birds([(1380, 250), (1420, 226), (1460, 262), (1600, 240), (1640, 214)], 1.0))

    # The gompa on the left with its own cypresses.
    b.append(cypress(90, 860, 300, "#4D6E43", "#3C5A35"))
    b.append(gompa(120, 880, 420, 250))
    b.append(cypress(580, 880, 260, "#4D6E43", "#3C5A35"))
    # The paths: one sweeping to the stupa, one to the gompa's door.
    b.append('<path d="M1340 1100 Q1420 980 1440 905 L1560 905 Q1600 980 1700 1100 Z" fill="#E6DAC5"/>')
    b.append('<path d="M1340 1100 Q1420 980 1440 905 L1452 905 Q1440 990 1372 1100 Z" fill="#CDBEA6"/>')
    b.append('<path d="M240 1100 Q290 960 290 880 L370 880 Q390 960 460 1100 Z" fill="#E6DAC5"/>')
    if S.BY:
        by = 1100 + S.BY
        b.append(f'<path d="M1339 1100 L1701 1100 L{f(1701 + 0.9 * S.BY)} {f(by)} L{f(1339 - 0.6 * S.BY)} {f(by)} Z M239 1100 L461 1100 L{f(461 + 0.4 * S.BY)} {f(by)} L{f(239 - 0.25 * S.BY)} {f(by)} Z" fill="#E6DAC5"/>')
        b.append(f'<path d="M1340 1100 L1372 1100 L{f(1372 - 0.55 * S.BY)} {f(by)} L{f(1340 - 0.6 * S.BY)} {f(by)} Z" fill="#CDBEA6"/>')
    for yy in (930, 980, 1040):
        b.append(f'<path d="M{f(1440 - (yy - 905) * 0.7)} {yy} L{f(1560 + (yy - 905) * 0.95)} {yy}" stroke="#CDBEA6" stroke-width="2"/>')
    # Marigold beds along the paths.
    b.append(marigolds(1690, 1900, 960, rnd))
    b.append(marigolds(40, 240, 935, rnd))
    b.append(marigolds(470, 640, 935, rnd))
    # A soft light patch where the menu's tagline sits.
    defs.append('<radialGradient id="mfoot" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#F6EFE2" stop-opacity="0.85"/><stop offset="1" stop-color="#F6EFE2" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="960" cy="1040" rx="640" ry="130" fill="url(#mfoot)"/>')
    # Flowers in the near corners.
    b.append(S.bush(80, 1060, 270, rnd, "#4E6B45", "#36502F", "#E26F8E", "#F6B3C6", "#7A1E38", 5))
    b.append(S.bush(1880, 1080, 260, rnd, "#4E6B45", "#36502F", "#E8801A", "#F6C12E", "#8E3A0A", 4))
    if S.BX:
        b.append(S.bush(-190, 1050, 260, S.EXT, "#4E6B45", "#36502F", "#E26F8E", "#F6B3C6", "#7A1E38", 5))
        b.append(S.bush(2110, 1060, 260, S.EXT, "#4E6B45", "#36502F", "#E8801A", "#F6C12E", "#8E3A0A", 4))
    defs.append('<radialGradient id="mlift" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FBF5EC" stop-opacity="0.6"/><stop offset="1" stop-color="#FBF5EC" stop-opacity="0"/></radialGradient>')
    b.append('<ellipse cx="900" cy="500" rx="560" ry="470" fill="url(#mlift)"/>')
    return S.svg("".join(b), defs)


# The temples' brick and carved wood at golden hour.
BRICK = dict(stone="#D2B48F", stone_top="#E6CDA9", stone_dark="#A9845C", wood="#5E2E19", wood_dark="#45200F", trim="#A8402B",
             roof="#7A3A22", roof_dark="#5E2A17", eave="#33160B", gold="#E0B04E", gold_dark="#A87A2C", door="#2C130A")


def lift(defs, gid, colour, alpha, cx=960, cy=520, rx=640, ry=500):
    defs.append(f'<radialGradient id="{gid}" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="{colour}" stop-opacity="{alpha}"/><stop offset="1" stop-color="{colour}" stop-opacity="0"/></radialGradient>')
    return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="url(#{gid})"/>'


def durbar():
    """Durbar Square at golden hour (the owner's main menu background, 30 Oct 2026; Figma "Art/Menu
    Option · Durbar Square" 240:103587, drawn by the options' menu_variants.py): a tall and a smaller
    brick temple on each side, the town's brick houses behind them, the snow range in the haze, prayer
    flags, birds, pigeons on the paved square. The middle stays light under the menu."""
    rnd = random.Random(61)
    defs, b = [], []
    defs.append(grad("dsky", [(0, "#F3D3A8", 1), (0.55, "#F8E6CC", 1), (1, "#FBF0E1", 1)]))
    b.append(S.full("url(#dsky)"))
    b.append('<circle cx="960" cy="600" r="420" fill="#FFF3DA" opacity="0.45"/>')
    b.append(S.hills([(-20, 560), (300, 520), (700, 548), (1100, 515), (1500, 540), (1940, 520)], 0.04, rnd, "#E8CFB4", 700))
    # The margin's own peaks and houses stand clear of the picture, so it stays as Figma shows it.
    if S.BX:
        b.append(S.peak(-230, 450, 240, 200, 620, S.EXT, "#D9B9A6", "#C7A08C", "#FFF4E6", "#F0D6C2", 0.45))
        b.append(S.peak(2170, 440, 220, 240, 620, S.EXT, "#D9B9A6", "#C7A08C", "#FFF4E6", "#F0D6C2", 0.45))
    for px, py, wl, wr in [(260, 430, 240, 260), (720, 400, 280, 260), (1250, 410, 260, 280), (1700, 430, 260, 240)]:
        b.append(S.peak(px, py, wl, wr, 620, rnd, "#D9B9A6", "#C7A08C", "#FFF4E6", "#F0D6C2", 0.45))
    b.append(S.band(520, 700, "#FBEBD6", 0, 0.85, 0.5, "dhaze", defs))
    # The town behind the square: brick houses with carved windows, a low line both sides.
    for i in range(int(S.BX / 52) + 1 if S.BX else 0):
        for side in (0, 1):
            w = S.EXT.uniform(44, 64)
            x = min(-(i + 1) * 52 + S.EXT.uniform(-6, 6), -w * 1.08) if side == 0 else max(1920 + i * 56 + S.EXT.uniform(-6, 6), 1920 + w * 0.08)
            b.append(S.house(x, 742, w, w * S.EXT.uniform(0.9, 1.3), "#B4553A", "#93432D", "#6E3420", "#4E2416", "#3A1C10"))
    for side in (0, 1):
        for i in range(10):
            x = (20 + i * 52 + rnd.uniform(-6, 6)) if side == 0 else (1360 + i * 56 + rnd.uniform(-6, 6))
            w = rnd.uniform(44, 64)
            hgt = w * rnd.uniform(0.9, 1.3)
            b.append(S.house(x, 742, w, hgt, "#B4553A", "#93432D", "#6E3420", "#4E2416", "#3A1C10"))
    # The temples: a tall one each side and a smaller one beside it.
    b.append(S.pagoda(520, 812, 0.4, BRICK))
    b.append(S.pagoda(1420, 812, 0.38, BRICK))
    b.append(S.pagoda(220, 900, 0.82, BRICK))
    b.append(S.pagoda(1710, 900, 0.78, BRICK))
    # The square: warm paving in rows, light in the middle under the buttons.
    defs.append(grad("pave", [(0, "#E7CFAE", 1), (1, "#D9BB93", 1)]))
    b.append(f'<rect x="{f(-20 - S.BX)}" y="840" width="{f(W + 40 + 2 * S.BX)}" height="{f(H - 820 + S.BY)}" fill="url(#pave)"/>')
    margin = 60 * math.ceil(S.BX / 60)
    for k, yy in enumerate(range(860, H + 40 + math.ceil(S.BY), 26)):
        b.append(f'<rect x="{f(-20 - S.BX)}" y="{yy}" width="{f(W + 40 + 2 * S.BX)}" height="1.6" fill="#B89670" opacity="0.45"/>')
        for x in range(-20 + (k % 2) * 30 - margin, W + 40 + margin, 60):
            b.append(f'<rect x="{x}" y="{yy}" width="1.4" height="26" fill="#B89670" opacity="0.3"/>')
    if S.BX:
        b.append(S.flags(S.string((-20 - S.BX, 200), (-20, 300), 30), S.FLAGS[3:] + S.FLAGS[:3], S.EXT, 0.95))
        b.append(S.flags(S.string((1940, 300), (1940 + S.BX, 200), 30), S.FLAGS, S.EXT, 0.95))
    b.append(S.flags(S.string((-20, 300), (520, 470), 60), S.FLAGS, rnd, 0.95))
    b.append(S.flags(S.string((1940, 300), (1400, 470), 60), S.FLAGS[2:] + S.FLAGS[:2], rnd, 0.95))
    # Figma flies the birds at x 820-1120, where the game's Temple Crown logo stands: they fly right of it.
    b.append(birds([(1280, 230), (1320, 205), (1365, 240), (1460, 215), (1500, 190), (1540, 228)], 1.1))
    # Pigeons on the square, at the sides.
    for _ in range(14):
        x = rnd.choice([rnd.uniform(90, 470), rnd.uniform(1450, 1830)])
        y = rnd.uniform(930, 1050)
        b.append(f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="9" ry="6" fill="#8E8A92"/><circle cx="{f(x + 7)}" cy="{f(y - 5)}" r="4" fill="#7C7880"/>')
    b.append(lift(defs, "dlift", "#FFF6E8", 0.7))
    b.append(lift(defs, "dfoot", "#FBF2E4", 0.8, 960, 1000, 620, 110))
    return S.svg("".join(b), defs)


if __name__ == "__main__":
    out = sys.argv[1]
    if len(sys.argv) > 3:
        S.set_bleed(float(sys.argv[2]), float(sys.argv[3]))
    os.makedirs(out, exist_ok=True)
    for name, fn in [("pokhara", pokhara), ("monastery", monastery), ("durbar", durbar)]:
        with open(os.path.join(out, name + ".svg"), "w", encoding="utf-8") as fh:
            fh.write(fn())
        print(name, os.path.getsize(os.path.join(out, name + ".svg")))
