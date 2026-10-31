"""Draws the game's icons again as vector art in the V1 Rebuild's style (ADR 065) and writes them over the
icon sprites: each keeps its file, size and GUID, and is fitted into the box the old icon's ink filled, so
every screen shows the new icon where, and as large as, it showed the old one.

Usage: python -P Tools/ArtPipeline/NewUI/icons.py <work-dir> [--write] [Name ...]
Needs Python 3 with numpy and Pillow, and Microsoft Edge (render.py). Without --write it only draws the
sheets in the work dir: icons.svg (every icon at its game size, fitted), compare.png (old beside new on
the menu's ivory and on the table's green). Names limit it to those icons.

The owner, 31 Oct 2026: "implement what are remained in the game. except cards for now". The V1 icons
were flat glyphs (black, a few brown or ivory) and a few paintings (the coin, the gold spade, the bolt,
Play). Each keeps its meaning and silhouette, drawn with the rebuild's palette (Figma's "Callbreak
Colors"): ink #131211, crimson #9A0815, browns, ivory, and the gold of the logo and the Dhaka Ring. The
suit icons are card pieces and stay; the cloth tab icon (Icon_TableOval_*) is vector already.
"""
import math
import os
import re
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from render import render  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
UI = os.path.join(ROOT, "Assets", "_Project", "Art", "UI")

INK = "#131211"            # ink/900
BROWN = "#572E12"          # brown/800, the shop tabs' and the small close's brown
BROWN_DARK = "#3E230F"     # brown/900, the clock
BROWN_SOFT = "#8B674B"     # brown/400, the friends behind
BROWN_MID = "#724E33"      # brown/600, the people behind on Create Room
IVORY = "#F9F0E6"          # surface/ivory
WHITE = "#FDFBFA"          # ivory/50, the chevrons on crimson
CRIMSON = "#9A0815"        # crimson/600
CRIMSON_DARK = "#581113"   # crimson/900
GOLD = '<stop offset="0" stop-color="#F7DE8E"/><stop offset="0.45" stop-color="#D9A646"/><stop offset="1" stop-color="#9C6A1E"/>'

SPADE = ("M31 0 C42.16 14.88 62 26.04 62 42.16 C62 54.56 50.84 62 40.92 58.28 C37.2 57.04 34.72 54.56 33.48 52.08 "
         "C34.72 59.52 38.44 65.72 44.64 69.44 L17.36 69.44 C23.56 65.72 27.28 59.52 28.52 52.08 "
         "C27.28 54.56 24.8 57.04 21.08 58.28 C11.16 62 0 54.56 0 42.16 C0 26.04 19.84 14.88 31 0 Z")


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def rr(x, y, w, h, r):
    """A rounded rectangle as a path (so it can join an even-odd path with its holes)."""
    r = min(r, w / 2, h / 2)
    return (f"M{f(x + r)} {f(y)} H{f(x + w - r)} A{f(r)} {f(r)} 0 0 1 {f(x + w)} {f(y + r)} V{f(y + h - r)} "
            f"A{f(r)} {f(r)} 0 0 1 {f(x + w - r)} {f(y + h)} H{f(x + r)} A{f(r)} {f(r)} 0 0 1 {f(x)} {f(y + h - r)} "
            f"V{f(y + r)} A{f(r)} {f(r)} 0 0 1 {f(x + r)} {f(y)} Z")


def circle(cx, cy, r):
    return f"M{f(cx - r)} {f(cy)} A{f(r)} {f(r)} 0 1 0 {f(cx + r)} {f(cy)} A{f(r)} {f(r)} 0 1 0 {f(cx - r)} {f(cy)} Z"


def spade(cx, cy, height):
    """Figma's spade (the Dhaka Ring's), its middle at cx, cy."""
    s = height / 69.44
    return f'<path transform="translate({f(cx - 31 * s)} {f(cy - 34.72 * s)}) scale({f(s)})" d="{SPADE}"/>'


def diamond(cx, cy, w, h):
    return f"M{f(cx)} {f(cy - h / 2)} L{f(cx + w / 2)} {f(cy)} L{f(cx)} {f(cy + h / 2)} L{f(cx - w / 2)} {f(cy)} Z"


def person(cx, head_y, head_r, shoulders_y, half_width, bottom):
    """A head and a bust with round shoulders."""
    r = min(half_width * 0.8, (bottom - shoulders_y))
    x0, x1 = cx - half_width, cx + half_width
    bust = (f"M{f(x0)} {f(bottom)} V{f(shoulders_y + r)} A{f(r)} {f(r)} 0 0 1 {f(x0 + r)} {f(shoulders_y)} "
            f"H{f(x1 - r)} A{f(r)} {f(r)} 0 0 1 {f(x1)} {f(shoulders_y + r)} V{f(bottom)} Z")
    return circle(cx, head_y, head_r) + " " + bust


def card(cx, cy, w, h, r, angle):
    return f'<path transform="rotate({f(angle)} {f(cx)} {f(cy)})" d="{rr(cx - w / 2, cy - h / 2, w, h, r)}"/>'


class Icon:
    """One icon: its design box and its drawing, with ids made unique by the prefix p."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.defs, self.body = [], []
        self.p = ""

    def svg(self, p):
        text = "<defs>" + "".join(self.defs) + "</defs>" + "".join(self.body)
        return text.replace("{p}", p)

    def layers(self, shapes, gap):
        """Shapes from the back: (geometry, paint, cut width). Each lies over those before it and cuts a
        gap round itself out of them (its geometry, filled and stroked as wide as the cut)."""
        n = len(self.defs)
        for i, (geo, _, _) in enumerate(shapes):
            self.defs.append(f'<g id="{{p}}g{n}_{i}">{geo}</g>')
        for i, (_, paint, _) in enumerate(shapes):
            cuts = "".join(f'<use href="#{{p}}g{n}_{j}" fill="#000" stroke="#000" stroke-width="{f(shapes[j][2] + 2 * gap)}" '
                           f'stroke-linejoin="round" stroke-linecap="round"/>' for j in range(i + 1, len(shapes)))
            if cuts:
                self.defs.append(f'<mask id="{{p}}m{n}_{i}" maskUnits="userSpaceOnUse" x="-500" y="-500" width="2000" height="2000">'
                                 f'<rect x="-500" y="-500" width="2000" height="2000" fill="#fff"/>{cuts}</mask>')
                self.body.append(f'<g mask="url(#{{p}}m{n}_{i})"><use href="#{{p}}g{n}_{i}" {paint}/></g>')
            else:
                self.body.append(f'<use href="#{{p}}g{n}_{i}" {paint}/>')
        return self

    def holes(self, solid, cuts, paint):
        """A solid shape with holes cut through it (the holes' own markup, black in a mask)."""
        k = len(self.defs)
        self.defs.append(f'<mask id="{{p}}h{k}" maskUnits="userSpaceOnUse" x="-500" y="-500" width="2000" height="2000">'
                         f'<rect x="-500" y="-500" width="2000" height="2000" fill="#fff"/><g fill="#000" stroke="#000">{cuts}</g></mask>')
        self.body.append(f'<g mask="url(#{{p}}h{k})" {paint}>{solid}</g>')
        return self

    def add(self, markup):
        self.body.append(markup)
        return self


def fill(colour):
    return f'fill="{colour}"'


def line(colour, width):
    return f'fill="none" stroke="{colour}" stroke-width="{f(width)}" stroke-linecap="round" stroke-linejoin="round"'


# ---------------------------------------------------------------- the icons

def about():
    i = Icon(100, 100)
    i.add(f'<circle cx="50" cy="50" r="44" {line(INK, 9)}/>')
    i.add(f'<g {fill(INK)} stroke="{INK}" stroke-width="2" stroke-linejoin="round"><circle cx="50" cy="28.5" r="6.5"/>'
          '<path d="M39.5 41 H56.5 V69 H61.5 V77.5 H38.5 V69 H43.5 V49.5 H39.5 Z"/></g>')
    return i


def back():
    return Icon(100, 100).add(f'<path d="M85 50 H16 M44 21 L15 50 L44 79" {line(INK, 11)}/>')


def chevron(colour, width, w, h):
    m = width / 2 + 1
    return Icon(w, h).add(f'<path d="M{f(m + 1)} {f(m)} L{f(w - m)} {f(h / 2)} L{f(m + 1)} {f(h - m)}" {line(colour, width)}/>')


def close(colour, width):
    return Icon(100, 100).add(f'<path d="M20 20 L80 80 M80 20 L20 80" {line(colour, width)}/>')


def clock():
    return Icon(100, 100).add(f'<circle cx="50" cy="50" r="40" {line(BROWN_DARK, 10)}/><path d="M50 27 V50 L65 61" {line(BROWN_DARK, 10)}/>')


def copy():
    i = Icon(90, 96)
    return i.layers([(f'<path d="{rr(32, 6, 52, 56, 9)}"/>', line(INK, 9), 9), (f'<path d="{rr(6, 34, 52, 56, 9)}"/>', line(INK, 9), 9)], 0)


def lock():
    i = Icon(66, 82)
    i.add(f'<path d="M18 40 V27 A15 15 0 0 1 48 27 V40" {line(INK, 9)}/>')
    return i.holes(f'<path d="{rr(5, 36, 56, 43, 8)}"/>', f'<path d="{circle(33, 53, 6)}"/><path d="{rr(30.2, 53, 5.6, 13, 2.8)}"/>', fill(INK))


def home():
    return Icon(100, 100).add(f'<path d="M50 9 L94 47 H82 V89 H60 V65 H40 V89 H18 V47 H6 Z" {fill(INK)} stroke="{INK}" stroke-width="5" stroke-linejoin="round"/>')


def settings():
    i = Icon(108, 106)
    cx, cy, outer, inner, teeth = 54, 53, 50, 38, 8
    pts = []
    for k in range(teeth):
        a = 2 * math.pi * k / teeth - math.pi / 2
        for da, r in ((-0.30, inner), (-0.17, outer), (0.17, outer), (0.30, inner)):
            pts.append((cx + r * math.cos(a + da), cy + r * math.sin(a + da)))
    gear = "M" + " L".join(f"{f(x)} {f(y)}" for x, y in pts) + " Z"
    return i.holes(f'<path d="{gear}" stroke="{INK}" stroke-width="5" stroke-linejoin="round"/>', f'<path d="{circle(cx, cy, 15)}"/>', fill(INK))


def shop():
    i = Icon(108, 92)
    # The awning: four scalloped panels, wider at the bottom, a fine gap between them; then the shop with its door.
    top, bottom, x0, x1 = 6, 30, 6, 102
    panels, cuts = [], []
    for k in range(4):
        a, b = x0 + k * (x1 - x0) / 4, x0 + (k + 1) * (x1 - x0) / 4
        ta, tb = 16 + (a - x0) * (76 / 96), 16 + (b - x0) * (76 / 96)
        r = (b - a) / 2
        panels.append(f"M{f(ta)} {f(top)} H{f(tb)} L{f(b)} {f(bottom)} A{f(r)} {f(r * 0.8)} 0 0 1 {f(a)} {f(bottom)} Z")
    for k in range(1, 4):
        a = x0 + k * (x1 - x0) / 4
        cuts.append(f'<path d="M{f(16 + (a - x0) * (76 / 96))} {f(top - 2)} L{f(a)} {f(bottom + 12)}" stroke-width="4" fill="none"/>')
    i.holes(f'<path d="{" ".join(panels)}" stroke="{INK}" stroke-width="3" stroke-linejoin="round"/>', "".join(cuts), fill(INK))
    return i.holes(f'<path d="{rr(14, 50, 80, 38, 5)}"/>', f'<path d="{rr(43, 61, 22, 30, 3)}"/>', fill(INK))


def sound():
    i = Icon(126, 114)
    i.add(f'<path d="M10 40 H31 L60 14 V100 L31 74 H10 Z" {fill(INK)} stroke="{INK}" stroke-width="6" stroke-linejoin="round"/>')
    return i.add(f'<path d="M77 39 A25 25 0 0 1 77 75 M92 22 A46 46 0 0 1 92 92" {line(INK, 10)}/>')


def emote():
    i = Icon(86, 88)
    cx, cy, r = 45, 41, 34
    p1 = (cx + r * math.cos(math.radians(112)), cy + r * math.sin(math.radians(112)))
    p2 = (cx + r * math.cos(math.radians(148)), cy + r * math.sin(math.radians(148)))
    i.add(f'<path d="M{f(p2[0])} {f(p2[1])} A{r} {r} 0 1 1 {f(p1[0])} {f(p1[1])} L10 83 Z" {line(INK, 8)}/>')
    return i.add(f'<g {fill(INK)}><circle cx="31" cy="41" r="5.5"/><circle cx="45" cy="41" r="5.5"/><circle cx="59" cy="41" r="5.5"/></g>')


def pencil():
    i = Icon(76, 76)
    body = "M5 38 L21 29.5 H61 Q70 29.5 70 38 Q70 46.5 61 46.5 H21 Z"
    cuts = '<path d="M23 25 V51" stroke-width="3.5" fill="none"/><path d="M55 25 V51" stroke-width="3.5" fill="none"/>'
    return i.holes(f'<path transform="rotate(-45 38 38)" d="{body}"/>', f'<g transform="rotate(-45 38 38)">{cuts}</g>', fill(INK))


def scorecard(colour, outlined):
    if outlined:
        i = Icon(130, 162)
        i.add(f'<path d="{rr(10, 24, 110, 130, 14)}" {line(colour, 9)}/>')
        i.holes(f'<path d="{rr(40, 6, 50, 30, 8)}"/>', f'<path d="{circle(65, 18, 5)}"/>', fill(colour))
        for k, y in enumerate((66, 92, 118)):
            i.add(f'<circle cx="38" cy="{y}" r="6" {fill(colour)}/><path d="M56 {y} H96" {line(colour, 9)}/>')
        return i
    i = Icon(82, 112)
    rows = "".join(f'<circle cx="23" cy="{y}" r="4.6"/><path d="{rr(32, y - 3.5, 30, 7, 3.5)}"/>' for y in (50, 68, 86))
    i.holes(f'<path d="{rr(5, 14, 72, 94, 10)}"/>', f'<path d="{rr(22, 1, 38, 22, 7)}" stroke-width="7"/>' + rows, fill(colour))
    return i.holes(f'<path d="{rr(25, 4, 32, 18, 5)}"/>', f'<path d="{circle(41, 11, 3.6)}"/>', fill(colour))


def cards_fan(colour):
    i = Icon(120, 112)
    shapes = [(card(cx, 58, 46, 74, 7, a), fill(colour), 0) for cx, a in ((36, -18), (60, 0), (84, 18))]
    return i.layers(shapes, 3.5)


def cards_stacked(colour):
    i = Icon(194, 194)
    i.layers([(card(84, 92, 104, 146, 14, -12), fill(colour), 0), (card(110, 100, 104, 146, 14, 6), fill(colour), 0)], 7)
    i.body[-1] = ""
    i.holes(card(110, 100, 104, 146, 14, 6), f'<g transform="rotate(6 110 100)">{spade(110, 98, 58)}</g>', fill(colour))
    return i


def stat_games():
    i = Icon(142, 152)
    i.layers([(card(56, 72, 82, 116, 12, -12), fill(INK), 0), (card(84, 80, 82, 116, 12, 6), fill(INK), 0)], 5)
    i.body[-1] = ""
    return i.holes(card(84, 80, 82, 116, 12, 6), f'<g transform="rotate(6 84 80)">{spade(84, 78, 44)}</g>', fill(INK))


def rule_call():
    i = Icon(170, 168)
    i.layers([(card(62, 82, 84, 118, 12, -14), line(INK, 9), 9), (card(106, 86, 84, 118, 12, 10), line(INK, 9), 9)], 2)
    return i.add(f'<g transform="rotate(10 106 86)" {fill(INK)}>{spade(106, 84, 40)}</g>')


def rule_follow():
    i = Icon(180, 162)
    i.layers([(card(58, 80, 76, 110, 11, -20), line(INK, 9), 9), (card(90, 76, 76, 110, 11, -3), line(INK, 9), 9),
              (card(120, 80, 76, 110, 11, 14), line(INK, 9), 9)], 2)
    i.add(f'<path transform="rotate(14 120 80)" d="{diamond(120, 80, 30, 40)}" {fill(CRIMSON)}/>')
    return i.add(f'<path d="{diamond(16, 120, 13, 18)} {diamond(16, 146, 13, 18)}" {fill(CRIMSON)}/>')


def star():
    i = Icon(140, 138)
    cx, cy, outer, inner = 70, 73, 64, 27
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        r = outer if k % 2 == 0 else inner
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return i.add(f'<path d="M{" L".join(f"{f(x)} {f(y)}" for x, y in pts)} Z" {fill(INK)} stroke="{INK}" stroke-width="9" stroke-linejoin="round"/>')


def win_rate():
    return Icon(124, 128).add(f'<path d="{rr(6, 70, 30, 54, 6)} {rr(47, 40, 30, 84, 6)} {rr(88, 6, 30, 118, 6)}" {fill(INK)}/>')


def trophy():
    i = Icon(132, 140)
    i.add(f'<path d="M34 10 H98 V44 Q98 76 66 82 Q34 76 34 44 Z" {fill(INK)} stroke="{INK}" stroke-width="4" stroke-linejoin="round"/>')
    i.add(f'<path d="M34 22 H18 Q12 22 12.5 29 Q15 52 37 58 M98 22 H114 Q120 22 119.5 29 Q117 52 95 58" {line(INK, 9)}/>')
    return i.add(f'<path d="M58 80 H74 V104 H58 Z {rr(36, 102, 60, 12, 4)} {rr(26, 114, 80, 18, 5)}" {fill(INK)}/>')


def gift():
    i = Icon(116, 124)
    i.add(f'<path d="M58 40 C44 40 30 37 30 26 C30 14 46 12 58 36 C70 12 86 14 86 26 C86 37 72 40 58 40" {line(INK, 8)}/>')
    i.add(f'<path d="{rr(6, 42, 104, 24, 5)}" {fill(INK)}/>')
    return i.holes(f'<path d="{rr(13, 70, 90, 50, 5)}"/>', spade(58, 94, 34), fill(INK))


def people(colours, heads, gap):
    """Heads with busts from the back; each is (cx, head_y, head_r, shoulders_y, half_width, bottom)."""
    return [(f'<path d="{person(*h)}"/>', fill(c), 0) for h, c in zip(heads, colours)], gap


def friends():
    shapes, gap = people([BROWN_SOFT, BROWN_SOFT, INK], [(26, 33, 14, 54, 23, 94), (110, 33, 14, 54, 23, 94), (68, 24, 18, 50, 29, 94)], 4)
    return Icon(136, 94).layers(shapes, gap)


def difficulty(count):
    if count == 1:
        return Icon(128, 144).add(f'<path d="{person(64, 34, 31, 76, 54, 142)}" {fill(INK)}/>')
    if count == 2:
        shapes, gap = people([INK, INK], [(140, 38, 27, 78, 52, 148), (76, 44, 30, 86, 56, 148)], 6)
        return Icon(212, 150).layers(shapes, gap)
    shapes, gap = people([INK, INK, INK], [(46, 50, 24, 88, 42, 154), (184, 50, 24, 88, 42, 154), (115, 40, 29, 80, 54, 154)], 6)
    return Icon(230, 156).layers(shapes, gap)


def create_room():
    i = Icon(462, 350)
    shapes, gap = people([BROWN_MID, BROWN_MID, INK], [(96, 100, 50, 176, 84, 340), (340, 100, 50, 176, 84, 340), (218, 82, 62, 168, 108, 340)], 14)
    shapes.append((f'<path d="{circle(382, 282, 64)}"/>', fill(CRIMSON), 0))
    i.layers(shapes, gap)
    return i.add(f'<path d="M382 248 V316 M348 282 H416" {line(WHITE, 16)}/>')


def enter_room():
    i = Icon(340, 320)
    i.add(f'<path d="M126 60 V34 Q126 12 148 12 H302 Q324 12 324 34 V286 Q324 308 302 308 H148 Q126 308 126 286 V260" {line(INK, 24)}/>')
    return i.add(f'<path d="M18 160 H214 M152 98 L214 160 L152 222" {line(INK, 26)}/>')


def leave():
    i = Icon(82, 82)
    i.add(f'<path d="M38 12 H19 Q12 12 12 19 V63 Q12 70 19 70 H38" {line(CRIMSON, 9)}/>')
    return i.add(f'<path d="M30 41 H71 M56 26 L71 41 L56 56" {line(CRIMSON, 9)}/>')


def how_to_play():
    i = Icon(112, 120)
    i.add(f'<path d="M16 98 Q16 112 30 112 H102" {line(INK, 8)}/>')
    q = (f'<path d="M45 38 Q45 25 58 25 Q71 25 71 37 Q71 46 62 50 Q58 52.5 58 59 V61" stroke-width="10" fill="none" '
         f'stroke-linecap="round" stroke-linejoin="round"/><path d="{circle(58, 75, 6.5)}"/>')
    return i.holes(f'<path d="{rr(12, 6, 92, 94, 9)}"/>', q, fill(INK))


def no_ads():
    i = Icon(134, 134)
    a = "M23 92 L39.5 42 H53.5 L70 92 H57 L53.6 81 H39.4 L36 92 Z M42.6 70.5 H50.4 L46.5 57.5 Z"
    d = "M73 42 H88 Q113 42 113 67 Q113 92 88 92 H73 Z M85.5 53.5 H88 Q100.5 53.5 100.5 67 Q100.5 80.5 88 80.5 H85.5 Z"
    i.holes(f'<path d="{a} {d}" fill-rule="evenodd"/>', '<path d="M24 24 L110 110" stroke-width="19" fill="none"/>', fill(INK))
    i.add(f'<circle cx="67" cy="67" r="58" {line(INK, 10)}/>')
    return i.add(f'<path d="M26 26 L108 108" {line(INK, 10)}/>')


def monitor(spaded):
    if spaded:
        i = Icon(360, 286)
        i.add(f'<path d="{rr(14, 14, 332, 204, 22)}" {line(INK, 24)}/>')
        i.add(f'<g {fill(INK)}>{spade(180, 114, 92)}</g>')
        return i.add(f'<path d="M160 228 H200 V256 H160 Z {rr(108, 252, 144, 26, 9)}" {fill(INK)}/>')
    i = Icon(116, 102)
    i.add(f'<path d="{rr(6.5, 6.5, 103, 64, 8)}" {line(INK, 9)}/>')
    return i.add(f'<path d="M50 74 H66 V86 H50 Z {rr(30, 84, 56, 12, 4)}" {fill(INK)}/>')


def mountains(colour):
    i = Icon(212, 116)
    shape = "M4 112 L56 40 L76 64 L106 8 L146 70 L166 44 L208 112 Z"
    snow = ('<path d="M82 52 L94 46 L104 56 L114 46 L128 56" stroke-width="6" fill="none" stroke-linejoin="round" stroke-linecap="round"/>'
            '<path d="M42 60 L50 56 L58 62 L64 56" stroke-width="5" fill="none" stroke-linejoin="round" stroke-linecap="round"/>'
            '<path d="M156 58 L163 54 L170 60 L178 56" stroke-width="5" fill="none" stroke-linejoin="round" stroke-linecap="round"/>')
    return i.holes(f'<path d="{shape}" stroke="{colour}" stroke-width="4" stroke-linejoin="round"/>', snow, fill(colour))


def coin():
    i = Icon(132, 132)
    i.defs.append('<linearGradient id="{p}rim" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFD978"/>'
                  '<stop offset="0.5" stop-color="#F0A522"/><stop offset="1" stop-color="#C46E0C"/></linearGradient>')
    i.defs.append('<linearGradient id="{p}face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFCB45"/>'
                  '<stop offset="1" stop-color="#EE9A16"/></linearGradient>')
    i.defs.append('<linearGradient id="{p}sp" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#C5730E"/>'
                  '<stop offset="1" stop-color="#8E4A08"/></linearGradient>')
    i.add('<circle cx="66" cy="66" r="64" fill="#7A3A0A"/><circle cx="66" cy="65" r="61" fill="url(#{p}rim)"/>')
    i.add('<circle cx="66" cy="66" r="49" fill="#B4630C"/><circle cx="66" cy="67" r="47" fill="url(#{p}face)"/>')
    i.add('<g fill="#FFE7A0" opacity="0.9">' + spade(66, 69, 62) + '</g><g fill="url(#{p}sp)">' + spade(66, 67.5, 62) + '</g>')
    return i.add('<path d="M28 42 A44 44 0 0 1 60 22" fill="none" stroke="#FFF4CF" stroke-width="5" stroke-linecap="round" opacity="0.8"/>')


def spade_gold():
    i = Icon(166, 180)
    i.defs.append(f'<linearGradient id="{{p}}g" x1="0" y1="0" x2="0" y2="1">{GOLD}</linearGradient>')
    i.defs.append('<linearGradient id="{p}l" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF2C4"/>'
                  '<stop offset="1" stop-color="#E9BE62"/></linearGradient>')
    # A dark rim, the gold, and a lighter bevel inside it, as the old painted spade had.
    i.add(f'<g fill="#7E5214">{spade(83, 91, 176)}</g>')
    i.add(f'<g fill="url(#{{p}}g)">{spade(83, 89.5, 170)}</g>')
    return i.add(f'<g fill="url(#{{p}}l)" opacity="0.6">{spade(83, 84, 132)}</g>')


def lightning():
    i = Icon(138, 220)
    i.defs.append('<linearGradient id="{p}b" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#C21E2C"/>'
                  '<stop offset="1" stop-color="#5E0E14"/></linearGradient>')
    bolt = "M74 4 H130 L98 84 H134 L32 216 L56 118 H6 Z"
    i.add(f'<path d="{bolt}" fill="url(#{{p}}b)" stroke="{CRIMSON_DARK}" stroke-width="4" stroke-linejoin="round"/>')
    return i.add('<path d="M80 12 L18 110 H50" fill="none" stroke="#E2505A" stroke-width="5" stroke-linejoin="round" stroke-linecap="round" opacity="0.8"/>')


def play():
    i = Icon(108, 120)
    i.defs.append('<linearGradient id="{p}p" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFFBF4"/>'
                  '<stop offset="1" stop-color="#F0CFAE"/></linearGradient>')
    tri = "M14 12 Q14 4 22 8 L98 52 Q106 60 98 68 L22 112 Q14 116 14 108 Z"
    i.add(f'<path d="{tri}" fill="url(#{{p}}p)" stroke="#FFFFFF" stroke-width="4" stroke-linejoin="round"/>')
    return i.add('<path d="M24 22 L84 57" stroke="#FFFFFF" stroke-width="4" stroke-linecap="round" opacity="0.7"/>')


ICONS = {
    "Icon_About": about, "Icon_Back": back,
    "Icon_Cards": lambda: cards_fan(INK),
    "Icon_CardsStacked_Brown": lambda: cards_stacked(BROWN), "Icon_CardsStacked_Ivory": lambda: cards_stacked(IVORY),
    "Icon_Chevron": lambda: chevron(INK, 9, 34, 56), "Icon_Chevron_Thin": lambda: chevron(WHITE, 6.5, 38, 64),
    "Common/Icon_Chevron_Light": lambda: chevron(WHITE, 9, 34, 56),
    "Icon_Clock": clock, "Icon_Close": lambda: close(INK, 13), "Icon_Close_Brown": lambda: close(BROWN, 12),
    "Icon_Coin": coin, "Icon_Copy": copy, "Icon_CreateRoom": create_room, "Icon_DailyReward": gift,
    "Icon_DifficultyEasy": lambda: difficulty(1), "Icon_DifficultyNormal": lambda: difficulty(2), "Icon_DifficultyHard": lambda: difficulty(3),
    "Icon_Emote": emote, "Icon_EnterRoom": enter_room, "Icon_Friends": friends, "Icon_Home": home, "Icon_HowToPlay": how_to_play,
    "Icon_Leave": leave, "Icon_Lightning": lightning, "Icon_Lock": lock,
    "Icon_Mountains_Brown": lambda: mountains(BROWN), "Icon_Mountains_Ivory": lambda: mountains(IVORY),
    "Icon_NoAds": no_ads, "Icon_Offline": lambda: monitor(False), "Icon_OfflineMonitor": lambda: monitor(True),
    "Icon_Pencil": pencil, "Icon_Play": play, "Icon_Rule_Call": rule_call, "Icon_Rule_Follow": rule_follow,
    "Icon_Rule_Score": lambda: scorecard(INK, True), "Icon_Scorecard": lambda: scorecard(INK, False),
    "Icon_Settings": settings, "Icon_Shop": shop, "Icon_Sound": sound, "Icon_Spade_Gold": spade_gold,
    "Icon_StatBestScore": star, "Icon_StatGames": stat_games, "Icon_StatWinRate": win_rate, "Icon_StatWins": trophy,
}


# ---------------------------------------------------------------- fitting and writing

def path_of(name):
    folder, _, file = name.rpartition("/")
    return os.path.join(UI, folder or "Icons", file + ".png")


def ink_box(alpha, threshold=0.06):
    ys, xs = np.nonzero(alpha > threshold)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def sheet(cells, width, height, out, work, background=None):
    """Renders nested SVGs at their cells into one transparent sheet; returns its RGBA (0-1)."""
    body = "".join(f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{vb}">{markup}</svg>' for x, y, w, h, vb, markup in cells)
    source = os.path.join(work, out + ".svg")
    with open(source, "w", encoding="utf-8") as fh:
        fh.write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">{body}</svg>')
    png = os.path.join(work, out + ".png")
    render(source, png, width, height, transparent=True)
    return np.asarray(Image.open(png), dtype=np.float64) / 255.0


def layout(sizes, width):
    """Rows of cells, left to right: (x, y) per size, and the sheet's height."""
    x = y = row = 0
    places = []
    for w, h in sizes:
        if x + w > width:
            x, y, row = 0, y + row + 8, 0
        places.append((x, y))
        x, row = x + w + 8, max(row, h)
    return places, y + row


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    write = "--write" in sys.argv
    work = args[0] if args else tempfile.mkdtemp(prefix="newui-icons-")
    names = [n for n in ICONS if len(args) < 2 or n.split("/")[-1] in args[1:]]
    os.makedirs(work, exist_ok=True)
    icons = {n: ICONS[n]() for n in names}

    # 1. Each design about 600 pixels across, to find the box its ink fills.
    scales = {n: 600 / max(icons[n].w, icons[n].h) for n in names}
    sizes = [(round(icons[n].w * scales[n]), round(icons[n].h * scales[n])) for n in names]
    places, height = layout(sizes, 2400)
    cells = [(x, y, w, h, f"0 0 {icons[n].w} {icons[n].h}", icons[n].svg(f"m{k}_")) for k, (n, (x, y), (w, h)) in enumerate(zip(names, places, sizes))]
    measured = sheet(cells, 2400, height, "measure", work)
    design_box = {}
    for n, (x, y), (w, h) in zip(names, places, sizes):
        x0, y0, x1, y1 = ink_box(measured[y:y + h, x:x + w, 3])
        design_box[n] = tuple(v / scales[n] for v in (x0, y0, x1, y1))

    # 2. Each at its game size, its ink fitted into the old icon's ink box (contained, centred).
    old = {n: np.asarray(Image.open(path_of(n)).convert("RGBA"), dtype=np.float64) / 255.0 for n in names}
    sizes = [(old[n].shape[1], old[n].shape[0]) for n in names]
    places, height = layout(sizes, 2400)
    cells = []
    for k, (n, (x, y), (w, h)) in enumerate(zip(names, places, sizes)):
        ox0, oy0, ox1, oy1 = ink_box(old[n][..., 3])
        dx0, dy0, dx1, dy1 = design_box[n]
        s = min((ox1 - ox0) / (dx1 - dx0), (oy1 - oy0) / (dy1 - dy0))
        cx, cy = (dx0 + dx1) / 2 - ((ox0 + ox1) / 2) / s, (dy0 + dy1) / 2 - ((oy0 + oy1) / 2) / s
        cells.append((x, y, w, h, f"{f(cx)} {f(cy)} {f(w / s)} {f(h / s)}", icons[n].svg(f"f{k}_")))
    fitted = sheet(cells, 2400, height, "icons", work)

    pieces = {}
    for n, (x, y), (w, h) in zip(names, places, sizes):
        pieces[n] = fitted[y:y + h, x:x + w]

    # Each design as an SVG of its own, for Figma's "V1 Rebuild · Icons & Avatars" page.
    os.makedirs(os.path.join(work, "svg"), exist_ok=True)
    for k, n in enumerate(names):
        markup = icons[n].svg(f"i{k}_").replace(' href="#', ' xlink:href="#')
        with open(os.path.join(work, "svg", n.split("/")[-1] + ".svg"), "w", encoding="utf-8") as fh:
            fh.write(f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
                     f'width="{f(icons[n].w)}" height="{f(icons[n].h)}" viewBox="0 0 {f(icons[n].w)} {f(icons[n].h)}">{markup}</svg>')
    compare(names, old, pieces, os.path.join(work, "compare.png"))
    if write:
        for n in names:
            Image.fromarray(np.round(pieces[n] * 255).astype(np.uint8), "RGBA").save(path_of(n), optimize=True)
            print("wrote", os.path.relpath(path_of(n), ROOT))


def compare(names, old, new, out):
    """Old beside new, each pair on the menu's ivory and on the table's green, at the game's size."""
    grounds = [(249, 240, 230), (54, 92, 66)]
    cell = 140
    rows = []
    for n in names:
        row = Image.new("RGB", (cell * 4, cell), (255, 255, 255))
        for g, ground in enumerate(grounds):
            for v, art in enumerate((old[n], new[n])):
                tile = Image.new("RGBA", (cell, cell), ground + (255,))
                im = Image.fromarray(np.round(art * 255).astype(np.uint8), "RGBA")
                im.thumbnail((cell - 12, cell - 12), Image.LANCZOS)
                tile.alpha_composite(im, ((cell - im.width) // 2, (cell - im.height) // 2))
                row.paste(tile.convert("RGB"), ((g * 2 + v) * cell, 0))
        rows.append(row)
    cols = 3
    w, h = cell * 4 + 16, cell + 4
    sheet_im = Image.new("RGB", (cols * w, ((len(rows) + cols - 1) // cols) * h), (255, 255, 255))
    for k, row in enumerate(rows):
        sheet_im.paste(row, ((k % cols) * w, (k // cols) * h))
    sheet_im.save(out)


if __name__ == "__main__":
    main()
