using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtTool;

namespace KitControls
{
    /// <summary>
    /// The commands written for the Controls family: labelled contact sheets for ChatGPT, cutting
    /// the returned sheet back into its cells, and checking a finished sprite against the mockup
    /// at the place and size the game draws it (sub-pixel, on a background with the mockup's own
    /// element removed, so nothing of the original shows through).
    /// </summary>
    internal static partial class Program
    {
        private const string Usage2 = @"kitcontrols, Controls commands
  lsheet  --def sheets/C1.json --out attach/C1.png        labelled contact sheet of enlarged mockup crops (+ .cells.json)
  cells   --in chatgpt/C1_a1.png --def attach/C1.cells.json --outdir work/C1_a1 [--key auto|magenta|none] [--soft 1]
                                                           key the ground and cut the returned sheet into its cells
  view    --sprite s --screen 11 --rect x,y,w,h [--nine L,T,R,B] [--unit 2.3] [--erase x,y,w,h] [--grow 2] [--pad 10] [--zoom 6]
          [--noerase] [--exclude ""x,y,w,h;..""] --out f    mockup | sprite on the erased ground | difference x6 | on black | on white
                                                           (--exclude: mockup rects left out of the error, e.g. the words in an element delivered empty;
                                                            --under / --over ""file;x,y,w,h[;L,T,R,B[;unit]]|.."": the other sprites of the same control)
  fit     --sprite s --screen 11 --rect x,y,w,h [--nine ..] [--mode move|uniform|free] [--erase ..] [--noerase]
                                                           the rect at which the sprite is closest to the mockup, and how close
  radial  (--in file | --screen 15) --center cx,cy --rmax R [--step 0.25] [--auto 1.5]   colour against radius, and its edges
  rwarp   --in a --out b --map ""s0>d0;s1>d1"" [--center cx,cy] [--size N | --keep] [--avg [--colors ""r0-r1:#hex[@alpha];..""] [--outer R]] [--shift dx,dy --shiftr rA,rB]
                                                           move ring edges to the given radii (and give each flat stretch its colour; exact outer edge;
                                                           --shift moves everything within rA of the centre, fading out to rB)
  warp    --in a --out b [--mapx ""s>d;..""] [--mapy ""s>d;..""] [--size W,H]             move chosen columns / rows to chosen places
  crimson --in a --out b --ramp ""148:#760615;165:#8D0718"" [--gc 5] [--white #FEFEFE] [--whiteto #hex] [--shade cx,cy,angle,amount,rOut]
                                                           put a crimson-and-white element on the mockup colours
  fill    --in a --out b --color #hex [--shape pill|roundrect|ellipse --rect x,y,w,h --radius r] [--outside]   one colour, alpha kept
  vgrad   --in a --out b --stops ""0:#hex;1:#hex""          top-to-bottom gradient, alpha kept
  frame   --in a --out b --src x0,y0,x1,y1 --srcline w --srcradius r --size W,H --line w --radius r [--zonex ""s0,s1>d0,d1""] [--zoney ..] [--shape roundrect|pill|none] [--linecolor #hex [--linecolor2 #hex]] [--hollow]
                                                           an outlined frame at a given size, outline weight and corner radius
  thicken --in a --out b --rect x,y,w,h [--grow 0.5] [--sigma 1.2] [--ink dark|light]   a flat glyph heavier or lighter by a fraction of a pixel
  angular (--in file | --screen 05) --center cx,cy --r R [--width 2]   the dashes of a dashed circle, in degrees
  rays    (--in file | --screen 11) --center cx,cy --from r --to r [--step 0.5] [--scale 1] [--every 45]   colours along rays from a centre
  twotone --in a --out b --a #hex --b #hex [--to1 #hex] --to2 ""0:#hex;1:#hex"" [--center cx,cy --radius r]   a two-colour picture on two new colours
  annulus --in a --out b --r0 a --r1 b --color #hex [--center cx,cy]   a flat ring between two radii
  dashes  --in a --out b --count n --dash deg --start deg --r0 a --r1 b --color #hex [--caps round|butt]   a dashed circle
  place   --sprite s --screen 02 --rect x,y,w,h [--erase ..] [--zoom 1] --out f   the mockup region with the sprite drawn in it (a reference picture)
  hprof   (--in file | --screen 11) --rect x,y,w,h [--axis x|y]   mean colour of each column (or row) of a rect
  swatch  --in a --rect x,y,w,h                            mean / median colour of a region of any image
  alphaof --in a --from b --out c                          the colour of a with the alpha of b
  lerp    --in a --to #hex --amount 0.3 --out b            mix every colour towards one colour
  matte   --in a --out b --ground #hex [--lo 0.03 --hi 0.12]   alpha from the difference to a flat ground colour";

        private static int ControlsCommand(string cmd, Opts o)
        {
            switch (cmd)
            {
                case "help2": Console.WriteLine(Usage2); return 0;
                case "lsheet": return LabelSheet(o);
                case "cells": return Cells(o);
                case "view": return View(o);
                case "fit": return Fit(o);
                case "radial": return Radial(o);
                case "rwarp": return RadialWarp(o);
                case "hprof": return HProfile(o);
                case "swatch": return Swatch(o);
                case "alphaof": return AlphaOf(o);
                case "lerp": return LerpCmd(o);
                case "matte": return MatteCmd(o);
                case "warp": return Warp(o);
                case "fill": return FillCmd(o);
                case "vgrad": return VGrad(o);
                case "crimson": return Crimson(o);
                case "place": return Place(o);
                case "frame": return Frame(o);
                case "thicken": return Thicken(o);
                case "angular": return Angular(o);
                case "rays": return Rays(o);
                case "twotone": return TwoTone(o);
                case "annulus": return Annulus(o);
                case "dashes": return Dashes(o);
                default:
                    return ButtonsCommand(cmd, o);      // Buttons.cs (kc help3)
            }
        }

        // ------------------------------------------------------------------------------------
        // lsheet: a labelled contact sheet. Each crop fades into the sheet's ground at its edges,
        // so the sheet shows elements on one ivory ground and no tile borders for ChatGPT to copy.
        private static int LabelSheet(Opts o)
        {
            string defPath = o.Need("def");
            JsonObject def = JsonNode.Parse(File.ReadAllText(defPath), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).AsObject();
            int W = (int)def["size"][0], H = (int)def["size"][1];
            string ground = def["ground"]?.ToString() ?? "#F7EFE6";
            int feather = def["feather"] != null ? (int)def["feather"] : 16;
            float capSize = def["captionSize"] != null ? (float)def["captionSize"] : 22f;
            int capH = (int)Math.Ceiling(capSize * 1.9);
            int capGap = 10;

            var rows = new List<List<(JsonObject item, Img crop)>>();
            foreach (JsonNode rn in def["rows"].AsArray())
            {
                var row = new List<(JsonObject, Img)>();
                foreach (JsonNode n in rn["items"].AsArray())
                {
                    JsonObject it = n.AsObject();
                    double[] r = it["rect"].AsArray().Select(v => (double)v).ToArray();
                    double margin = it["margin"] != null ? (double)it["margin"] : 8;
                    double scale = it["scale"] != null ? (double)it["scale"] : 6;
                    double[] view = { r[0] - margin, r[1] - margin, r[2] + 2 * margin, r[3] + 2 * margin };
                    row.Add((it, it["cutx"] != null ? SplicedView(it["screen"].ToString(), view, scale, it["cutx"].AsArray()) : MockupView(it["screen"].ToString(), view, scale)));
                }

                rows.Add(row);
            }

            int contentH = rows.Sum(r => r.Max(c => c.crop.H) + capGap + capH);
            if (contentH > H) throw new InvalidOperationException($"the rows need {contentH} px, the sheet is {H} px high: lower the scales");
            double gapY = (H - contentH) / (double)(rows.Count + 1);

            Img sheet = Flat(W, H, ground);
            var g = Img.ParseHex(ground);
            var cells = new JsonArray();
            var captions = new List<(string text, float cx, float y)>();
            double y = gapY;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var row = rows[ri];
                int rowH = row.Max(c => c.crop.H);
                int rowW = row.Sum(c => c.crop.W);
                if (rowW > W) throw new InvalidOperationException($"row {ri + 1} needs {rowW} px, the sheet is {W} px wide: lower the scales");
                double gapX = (W - rowW) / (double)(row.Count + 1);
                double x = gapX;
                double cellTop = y - gapY / 2, cellBottom = y + rowH + capGap + capH + gapY / 2;
                foreach ((JsonObject it, Img crop) in row)
                {
                    int px = (int)Math.Round(x), py = (int)Math.Round(y + (rowH - crop.H) / 2.0);
                    // Feathered paste: alpha ramps from 0 at the crop's edge to 1 `feather` px inside.
                    for (int yy = 0; yy < crop.H; yy++)
                    {
                        for (int xx = 0; xx < crop.W; xx++)
                        {
                            int d = Math.Min(Math.Min(xx, crop.W - 1 - xx), Math.Min(yy, crop.H - 1 - yy));
                            float a = feather <= 0 ? 1f : Smooth((d + 0.5f) / feather);
                            int s = yy * crop.W + xx, t = (py + yy) * W + px + xx;
                            if (px + xx < 0 || px + xx >= W || py + yy < 0 || py + yy >= H) continue;
                            sheet.R[t] = g.r + (crop.R[s] - g.r) * a;
                            sheet.G[t] = g.g + (crop.G[s] - g.g) * a;
                            sheet.B[t] = g.b + (crop.B[s] - g.b) * a;
                        }
                    }

                    double[] r = it["rect"].AsArray().Select(v => (double)v).ToArray();
                    double margin = it["margin"] != null ? (double)it["margin"] : 8;
                    double scale = it["scale"] != null ? (double)it["scale"] : 6;
                    string caption = it["caption"]?.ToString() ?? "";
                    if (caption.Length > 0) captions.Add((caption, px + crop.W / 2f, py + crop.H + capGap + (rowH - crop.H) / 2f));
                    double cutTotal = it["cutx"] != null ? it["cutx"].AsArray().Sum(n => Math.Round((double)n[1]) - Math.Round((double)n[0])) : 0;
                    var cell = new JsonObject
                    {
                        ["name"] = it["name"].ToString(),
                        ["caption"] = caption,
                        ["cell"] = Arr(x - gapX / 2, cellTop, crop.W + gapX, cellBottom - cellTop),
                        ["crop"] = Arr(px, py, crop.W, crop.H),
                        ["element"] = Arr(px + margin * scale, py + margin * scale, (r[2] - cutTotal) * scale, r[3] * scale),
                        ["cut"] = cutTotal,
                        ["screen"] = it["screen"].ToString(),
                        ["rect"] = Arr(r[0], r[1], r[2], r[3]),
                        ["scale"] = scale,
                        ["margin"] = margin,
                    };
                    cells.Add(cell);
                    x += crop.W + gapX;
                }

                y += rowH + capGap + capH + gapY;
            }

            string outPath = o.Need("out");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            using (Bitmap bmp = sheet.ToBitmap())
            {
                using (Graphics gr = Graphics.FromImage(bmp))
                using (var font = new Font("Arial", capSize, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.FromArgb(255, 120, 112, 104)))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                {
                    gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    foreach ((string text, float cx, float cy) in captions) gr.DrawString(text, font, brush, cx, cy, fmt);
                }

                bmp.Save(outPath, ImageFormat.Png);
            }

            var root = new JsonObject { ["sheet"] = Arr(W, H), ["ground"] = ground, ["items"] = cells };
            string cellsPath = Path.ChangeExtension(outPath, ".cells.json");
            File.WriteAllText(cellsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{outPath}  {W}x{H}  {cells.Count} elements in {rows.Count} rows  (+ {Path.GetFileName(cellsPath)})");
            foreach (JsonNode c in cells) Console.WriteLine($"  {c["name"],-22} element at {Fmt(c["element"])}  cell {Fmt(c["cell"])}");
            return 0;
        }

        /// <summary>
        /// An enlarged crop with plain stretches of columns taken out ("cutx": [[x0, x1], ...] in
        /// mockup pixels): a long slider or field becomes a short one with the same ends, so its
        /// caps and rim get more of the sheet. Each seam is cross-faded over a few columns.
        /// </summary>
        private static Img SplicedView(string screen, double[] view, double scale, JsonArray cuts)
        {
            Img mock = Img.Load(MockupPath(screen));
            int x0 = (int)Math.Floor(view[0]), y0 = (int)Math.Floor(view[1]);
            int x1 = (int)Math.Ceiling(view[0] + view[2]), y1 = (int)Math.Ceiling(view[1] + view[3]);
            Img c = mock.Crop(x0, y0, x1 - x0, y1 - y0);
            // Right to left, so earlier cuts keep their coordinates.
            foreach (JsonNode cut in cuts.OrderByDescending(n => (double)n[0]))
            {
                int a = (int)Math.Round((double)cut[0]) - x0, b = (int)Math.Round((double)cut[1]) - x0;
                int gone = b - a;
                if (gone <= 0 || a < 4 || b > c.W - 4) throw new ArgumentException($"cutx {cut[0]}..{cut[1]} does not fit the crop");
                var n = new Img(c.W - gone, c.H);
                const int blend = 6;
                for (int y = 0; y < c.H; y++)
                {
                    for (int x = 0; x < n.W; x++)
                    {
                        int left = x, right = x + gone;      // the same output column seen from before and from after the cut
                        float t = Math.Clamp((x - (a - blend / 2f)) / blend, 0f, 1f);
                        int t2 = y * n.W + x;
                        int il = y * c.W + Math.Min(left, c.W - 1), ir = y * c.W + Math.Min(right, c.W - 1);
                        n.R[t2] = c.R[il] + (c.R[ir] - c.R[il]) * t;
                        n.G[t2] = c.G[il] + (c.G[ir] - c.G[il]) * t;
                        n.B[t2] = c.B[il] + (c.B[ir] - c.B[il]) * t;
                    }
                }

                c = n;
            }

            return c.Resize((int)Math.Round(c.W * scale), (int)Math.Round(c.H * scale));
        }

        private static JsonArray Arr(params double[] v)
        {
            var a = new JsonArray();
            foreach (double d in v) a.Add(Math.Round(d, 2));
            return a;
        }

        private static string Fmt(JsonNode arr) => string.Join(",", arr.AsArray().Select(v => ((double)v).ToString("0.#", CultureInfo.InvariantCulture)));

        // ------------------------------------------------------------------------------------
        // cells: key the ground of a returned sheet and cut it into the cells of the sheet it answers
        private static int Cells(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            JsonObject def = JsonNode.Parse(File.ReadAllText(o.Need("def"))).AsObject();
            double sw = (double)def["sheet"][0], sh = (double)def["sheet"][1];
            double kx = m.W / sw, ky = m.H / sh;
            string key = o.Get("key", "auto");
            if (key == "auto") key = LooksMagenta(m) ? "magenta" : "none";
            if (key == "magenta") KeyMagenta(m, (float)o.Num("soft", 1.0));
            float lo = (float)o.Num("lo", 10) / 255f, hi = (float)o.Num("hi", 250) / 255f;
            for (int i = 0; i < m.Count; i++)
            {
                float a = m.A[i];
                a = a <= lo ? 0f : (a >= hi ? 1f : (a - lo) / (hi - lo));
                m.A[i] = a;
                if (a == 0f) { m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; }
            }

            string outDir = o.Need("outdir");
            Directory.CreateDirectory(outDir);
            Console.WriteLine($"{Path.GetFileName(o.Need("in"))}  {m.W}x{m.H}  (sheet {sw}x{sh}, x{kx:0.000} y{ky:0.000})  key={key}");
            Img over = Flat(m.W, m.H, "#202020");
            over.Over(m, 0, 0);
            using Bitmap ob = over.ToBitmap();
            using Graphics og = Graphics.FromImage(ob);
            using var cellPen = new Pen(Color.FromArgb(255, 0, 200, 255), 2);
            using var boxPen = new Pen(Color.FromArgb(255, 255, 230, 0), 2);
            foreach (JsonNode it in def["items"].AsArray())
            {
                double[] c = it["cell"].AsArray().Select(v => (double)v).ToArray();
                int x0 = Math.Max(0, (int)Math.Round(c[0] * kx)), y0 = Math.Max(0, (int)Math.Round(c[1] * ky));
                int x1 = Math.Min(m.W, (int)Math.Round((c[0] + c[2]) * kx)), y1 = Math.Min(m.H, (int)Math.Round((c[1] + c[3]) * ky));
                int minX = x1, minY = y1, maxX = -1, maxY = -1;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        if (m.A[y * m.W + x] <= 8f / 255f) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                og.DrawRectangle(cellPen, x0, y0, x1 - x0 - 1, y1 - y0 - 1);
                string name = it["name"].ToString();
                if (maxX < 0) { Console.WriteLine($"  {name,-22} EMPTY CELL"); continue; }
                bool touches = minX == x0 || minY == y0 || maxX == x1 - 1 || maxY == y1 - 1;
                Img part = m.Crop(minX, minY, maxX - minX + 1, maxY - minY + 1);
                part.Save(Path.Combine(outDir, name + ".png"));
                og.DrawRectangle(boxPen, minX, minY, maxX - minX, maxY - minY);
                double[] e = it["element"].AsArray().Select(v => (double)v).ToArray();
                double[] r = it["rect"].AsArray().Select(v => (double)v).ToArray();
                Console.WriteLine($"  {name,-22} at {minX},{minY}  {part.W}x{part.H}  (sheet element {e[2] * kx:0}x{e[3] * ky:0}; mockup {r[2]:0.#}x{r[3]:0.#} -> {part.W / r[2]:0.00}x / {part.H / r[3]:0.00}x){(touches ? "   TOUCHES ITS CELL EDGE" : "")}");
            }

            ob.Save(Path.Combine(outDir, "_overview.png"), ImageFormat.Png);
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // Drawing a sprite at a sub-pixel rectangle of the mockup (plain stretch or 9-slice),
        // box-filtered by supersampling, premultiplied, in linear light.
        private static double Map9(double t, double D, double S, int lead, int trail, double unit)
        {
            if (lead + trail <= 0) return t / D * S;
            double l = lead / unit, r = trail / unit;
            if (l + r > D) { double k = D / (l + r); l *= k; r *= k; }
            if (t < l) return l > 0 ? t * lead / l : 0;
            if (t > D - r) return r > 0 ? S - (D - t) * trail / r : S;
            double mid = D - l - r;
            return lead + (mid > 1e-9 ? (t - l) / mid : 0.5) * (S - lead - trail);
        }

        private static Img RenderAt(Img sprite, int[] nine, double unit, double vx, double vy, double zoom, int cw, int ch, double[] rect, int ss = 4)
        {
            var outp = new Img(cw, ch, 0f);
            int L = nine?[0] ?? 0, T = nine?[1] ?? 0, R = nine?[2] ?? 0, B = nine?[3] ?? 0;
            double rx = rect[0], ry = rect[1], rw = rect[2], rh = rect[3];
            int x0 = Math.Max(0, (int)Math.Floor((rx - vx) * zoom)), x1 = Math.Min(cw, (int)Math.Ceiling((rx + rw - vx) * zoom));
            int y0 = Math.Max(0, (int)Math.Floor((ry - vy) * zoom)), y1 = Math.Min(ch, (int)Math.Ceiling((ry + rh - vy) * zoom));
            // Enough samples that a minified sprite is box-filtered rather than point-sampled.
            int need = (int)Math.Ceiling(Math.Max(sprite.W / (rw * zoom), sprite.H / (rh * zoom)) * 1.5);
            ss = Math.Clamp(Math.Max(ss, need), 2, 12);
            var sxs = new double[ss];
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    double ar = 0, ag = 0, ab = 0, aa = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        double my = vy + (y + (j + 0.5) / ss) / zoom;
                        if (my < ry || my >= ry + rh) continue;
                        double sy = Map9(my - ry, rh, sprite.H, T, B, unit) - 0.5;
                        int iy = (int)Math.Floor(sy);
                        double fy = sy - iy;
                        int ya = Math.Clamp(iy, 0, sprite.H - 1), yb = Math.Clamp(iy + 1, 0, sprite.H - 1);
                        for (int i = 0; i < ss; i++)
                        {
                            double mx = vx + (x + (i + 0.5) / ss) / zoom;
                            if (mx < rx || mx >= rx + rw) continue;
                            double sx = Map9(mx - rx, rw, sprite.W, L, R, unit) - 0.5;
                            int ix = (int)Math.Floor(sx);
                            double fx = sx - ix;
                            int xa = Math.Clamp(ix, 0, sprite.W - 1), xb = Math.Clamp(ix + 1, 0, sprite.W - 1);
                            int i00 = ya * sprite.W + xa, i10 = ya * sprite.W + xb, i01 = yb * sprite.W + xa, i11 = yb * sprite.W + xb;
                            double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                            double a = sprite.A[i00] * w00 + sprite.A[i10] * w10 + sprite.A[i01] * w01 + sprite.A[i11] * w11;
                            ar += sprite.R[i00] * sprite.A[i00] * w00 + sprite.R[i10] * sprite.A[i10] * w10 + sprite.R[i01] * sprite.A[i01] * w01 + sprite.R[i11] * sprite.A[i11] * w11;
                            ag += sprite.G[i00] * sprite.A[i00] * w00 + sprite.G[i10] * sprite.A[i10] * w10 + sprite.G[i01] * sprite.A[i01] * w01 + sprite.G[i11] * sprite.A[i11] * w11;
                            ab += sprite.B[i00] * sprite.A[i00] * w00 + sprite.B[i10] * sprite.A[i10] * w10 + sprite.B[i01] * sprite.A[i01] * w01 + sprite.B[i11] * sprite.A[i11] * w11;
                            aa += a;
                        }
                    }

                    int t = y * cw + x;
                    double n = ss * ss;
                    if (aa <= 1e-9) continue;
                    outp.A[t] = (float)(aa / n);
                    outp.R[t] = (float)(ar / aa); outp.G[t] = (float)(ag / aa); outp.B[t] = (float)(ab / aa);
                }
            }

            return outp;
        }

        /// <summary>The mockup region with the element's rectangle filled from its surroundings.</summary>
        private static Img ErasedView(Img mock, Box view, double[] erase)
        {
            if (erase != null)
            {
                // A window inside the erased rect has nothing to fill from: work on a box that has
                // a margin around the erased rect and cut the window out of it afterwards.
                Box need = Box.Around(erase[0], erase[1], erase[2], erase[3], 8).Clamp(mock.W, mock.H);
                Box big = view.Union(need);
                if (big.W != view.W || big.H != view.H)
                {
                    Img whole = ErasedView(mock, big, erase);
                    return whole.Crop(view.X - big.X, view.Y - big.Y, view.W, view.H);
                }
            }

            Img v = mock.Crop(view.X, view.Y, view.W, view.H);
            if (erase == null) return v;
            var hole = new float[v.Count];
            for (int y = 0; y < view.H; y++)
            {
                for (int x = 0; x < view.W; x++)
                {
                    double px = view.X + x + 0.5, py = view.Y + y + 0.5;
                    if (px >= erase[0] && px < erase[0] + erase[2] && py >= erase[1] && py < erase[1] + erase[3]) hole[y * view.W + x] = 1f;
                }
            }

            Inpaint.Fill(v, hole, null, grain: false, relax: 240);
            return v;
        }

        /// <summary>Rectangles (mockup px) left out of the error: the mockup's words inside an element that is delivered empty.</summary>
        private static List<double[]> _exclude = new List<double[]>();

        private static (double mean, double rms, double max) ErrorIn(Img a, Img b, Box view, double[] region)
        {
            double e = 0, e2 = 0, mx = 0; long k = 0;
            for (int y = 0; y < view.H; y++)
            {
                for (int x = 0; x < view.W; x++)
                {
                    double px = view.X + x + 0.5, py = view.Y + y + 0.5;
                    if (px < region[0] || px >= region[0] + region[2] || py < region[1] || py >= region[1] + region[3]) continue;
                    bool skip = false;
                    foreach (double[] ex in _exclude) if (px >= ex[0] && px < ex[0] + ex[2] && py >= ex[1] && py < ex[1] + ex[3]) { skip = true; break; }
                    if (skip) continue;
                    int i = y * view.W + x;
                    double dr = (Img.ToSrgb01(a.R[i]) - Img.ToSrgb01(b.R[i])) * 255, dg = (Img.ToSrgb01(a.G[i]) - Img.ToSrgb01(b.G[i])) * 255, db = (Img.ToSrgb01(a.B[i]) - Img.ToSrgb01(b.B[i])) * 255;
                    double d = (Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db)) / 3;
                    e += d; e2 += (dr * dr + dg * dg + db * db) / 3; mx = Math.Max(mx, d); k++;
                }
            }

            return k == 0 ? (0, 0, 0) : (e / k, Math.Sqrt(e2 / k), mx);
        }

        private static void Prepare(Opts o, out Img sprite, out Img mock, out double[] rect, out int[] nine, out double unit, out double[] erase, out Box view, out double grow)
        {
            sprite = Img.Load(o.Need("sprite"));
            mock = Img.Load(MockupPath(o.Need("screen")));
            _exclude = o.Has("exclude")
                ? o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList()
                : new List<double[]>();
            rect = o.Nums("rect", 4);
            nine = o.Ints("nine", 4);
            unit = o.Num("unit", 2.3);
            grow = o.Num("grow", 2);
            erase = o.Has("noerase") ? null : o.Nums("erase", 4, new[] { rect[0] - grow, rect[1] - grow, rect[2] + 2 * grow, rect[3] + 2 * grow });
            double pad = o.Num("pad", 10);
            double[] basis = erase ?? rect;
            view = Box.Around(Math.Min(basis[0], rect[0]), Math.Min(basis[1], rect[1]), Math.Max(basis[0] + basis[2], rect[0] + rect[2]) - Math.Min(basis[0], rect[0]),
                Math.Max(basis[1] + basis[3], rect[1] + rect[3]) - Math.Min(basis[1], rect[1]), pad).Clamp(mock.W, mock.H);
            // --window x,y,w,h: look at (and measure) only this part of the mockup, e.g. one end of a long control.
            if (o.Has("window"))
            {
                double[] wv = o.Nums("window", 4);
                view = Box.Around(wv[0], wv[1], wv[2], wv[3], 0).Clamp(mock.W, mock.H);
            }
        }

        /// <summary>
        /// Other sprites of the same control, drawn under or over the one being looked at
        /// ("--under" / "--over": "file;x,y,w,h[;L,T,R,B[;unit]]" separated by "|"): a slider is a
        /// track, a fill and a knob, and each is judged in place with the others present.
        /// </summary>
        private static void DrawLayers(Opts o, string key, Img target, double vx, double vy, double zoom)
        {
            if (!o.Has(key)) return;
            foreach (string layer in o.Get(key).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = layer.Split(';');
                Img sp = Img.Load(q[0].Trim());
                double[] r = q[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                int[] nine = q.Length > 2 && q[2].Trim().Length > 0 ? q[2].Split(',').Select(v => (int)Math.Round(double.Parse(v, CultureInfo.InvariantCulture))).ToArray() : null;
                double unit = q.Length > 3 ? double.Parse(q[3], CultureInfo.InvariantCulture) : 2.3;
                target.Over(RenderAt(sp, nine, unit, vx, vy, zoom, target.W, target.H, r), 0, 0);
            }
        }

        private static int View(Opts o)
        {
            Prepare(o, out Img sprite, out Img mock, out double[] rect, out int[] nine, out double unit, out double[] erase, out Box view, out double grow);
            double zoom = o.Num("zoom", Math.Clamp(Math.Floor(520.0 / view.W), 2, 10));
            Img ground = ErasedView(mock, view, erase);
            Img original = mock.Crop(view.X, view.Y, view.W, view.H);

            // The numbers: at mockup resolution.
            Img comp1 = ground.Clone();
            DrawLayers(o, "under", comp1, view.X, view.Y, 1);
            comp1.Over(RenderAt(sprite, nine, unit, view.X, view.Y, 1, view.W, view.H, rect), 0, 0);
            DrawLayers(o, "over", comp1, view.X, view.Y, 1);
            double[] region = erase ?? rect;
            (double mean, double rms, double max) = ErrorIn(comp1, original, view, region);

            // The picture: everything at zoom.
            int zw = (int)Math.Round(view.W * zoom), zh = (int)Math.Round(view.H * zoom);
            Img zOrig = original.Resize(zw, zh);
            Img zGround = ground.Resize(zw, zh);
            Img drawn = RenderAt(sprite, nine, unit, view.X, view.Y, zoom, zw, zh, rect);
            Img zComp = zGround.Clone();
            DrawLayers(o, "under", zComp, view.X, view.Y, zoom);
            zComp.Over(drawn, 0, 0);
            DrawLayers(o, "over", zComp, view.X, view.Y, zoom);
            Img zComp1 = comp1.Resize(zw, zh);      // the 1x composite enlarged the same way as the mockup: a like-for-like look
            Img diff = new Img(zw, zh);
            Img d1 = new Img(view.W, view.H);
            for (int i = 0; i < d1.Count; i++)
            {
                float d = (Math.Abs(Img.ToSrgb01(comp1.R[i]) - Img.ToSrgb01(original.R[i])) + Math.Abs(Img.ToSrgb01(comp1.G[i]) - Img.ToSrgb01(original.G[i])) + Math.Abs(Img.ToSrgb01(comp1.B[i]) - Img.ToSrgb01(original.B[i]))) / 3f;
                float v = Img.ToLinear(Math.Min(1f, d * 6f));
                d1.R[i] = v; d1.G[i] = v; d1.B[i] = v;
            }

            using (Bitmap sb = d1.ToBitmap())
            using (var big = new Bitmap(zw, zh, PixelFormat.Format32bppArgb))
            {
                using (Graphics gg = Graphics.FromImage(big))
                {
                    gg.InterpolationMode = InterpolationMode.NearestNeighbor;
                    gg.PixelOffsetMode = PixelOffsetMode.Half;
                    gg.DrawImage(sb, new Rectangle(0, 0, zw, zh), 0, 0, view.W, view.H, GraphicsUnit.Pixel);
                }

                diff = Img.FromBitmap(big);
            }

            Img black = Flat(zw, zh, "#000000"); black.Over(drawn, 0, 0);
            Img white = Flat(zw, zh, "#FFFFFF"); white.Over(drawn, 0, 0);
            var top = new List<Img> { zOrig, zComp1, zComp };
            var bottom = new List<Img> { diff, black, white };
            Img sheet = o.Has("row") ? Tiles(top.Concat(bottom).ToList(), 6, "#808080") : Stack(new List<Img> { Tiles(top, 6, "#808080"), Tiles(bottom, 6, "#808080") }, 0, "#808080");
            sheet.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  zoom {zoom:0.##}  view {view}  rect {rect[0]:0.##},{rect[1]:0.##},{rect[2]:0.##},{rect[3]:0.##}");
            Console.WriteLine($"  tiles: mockup | sprite at mockup resolution | sprite sharp ; difference x6 | on black | on white");
            Console.WriteLine($"  error against the mockup inside {(erase != null ? "the erased rect" : "the rect")}: mean {mean:0.00}  rms {rms:0.00}  max {max:0.0}  (sRGB levels)");
            return 0;
        }

        /// <summary>
        /// The mockup region around a rect with the sprite drawn at that rect on the erased ground,
        /// as one plain image at --zoom (default 1): a reference picture for an element that is
        /// not on the mockup in that state (the busy spinner in place of a button's chevron).
        /// </summary>
        private static int Place(Opts o)
        {
            Prepare(o, out Img sprite, out Img mock, out double[] rect, out int[] nine, out double unit, out double[] erase, out Box view, out double grow);
            double zoom = o.Num("zoom", 1);
            Img ground = ErasedView(mock, view, erase);
            int zw = (int)Math.Round(view.W * zoom), zh = (int)Math.Round(view.H * zoom);
            Img z = zoom == 1 ? ground : ground.Resize(zw, zh);
            z.Over(RenderAt(sprite, nine, unit, view.X, view.Y, zoom, zw, zh, rect), 0, 0);
            z.Save(o.Need("out"));
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  {zw}x{zh}  view {view}  the sprite is at {(rect[0] - view.X) * zoom:0.##},{(rect[1] - view.Y) * zoom:0.##} {rect[2] * zoom:0.##}x{rect[3] * zoom:0.##} in it"));
            return 0;
        }

        private static int Fit(Opts o)
        {
            Prepare(o, out Img sprite, out Img mock, out double[] rect, out int[] nine, out double unit, out double[] erase, out Box view, out double grow);
            string mode = o.Get("mode", "free");
            Img original = mock.Crop(view.X, view.Y, view.W, view.H);
            Img ground = ErasedView(mock, view, erase);
            double[] region = erase ?? new[] { rect[0] - 1, rect[1] - 1, rect[2] + 2, rect[3] + 2 };
            double Err(double[] r)
            {
                Img c = ground.Clone();
                DrawLayers(o, "under", c, view.X, view.Y, 1);
                c.Over(RenderAt(sprite, nine, unit, view.X, view.Y, 1, view.W, view.H, r), 0, 0);
                DrawLayers(o, "over", c, view.X, view.Y, 1);
                return ErrorIn(c, original, view, region).rms;
            }

            double[] best = (double[])rect.Clone();
            double bestE = Err(best);
            double startE = bestE;
            double aspect = rect[2] / rect[3];
            foreach (double step in new[] { 1.0, 0.5, 0.25, 0.1, 0.05 })
            {
                bool improved = true;
                int guard = 0;
                while (improved && guard++ < 60)
                {
                    improved = false;
                    var moves = new List<double[]>
                    {
                        new[] { step, 0, 0, 0 }, new[] { -step, 0, 0, 0 }, new[] { 0, step, 0, 0 }, new[] { 0, -step, 0, 0 },
                    };
                    if (mode == "free")
                    {
                        // Grow or shrink about the centre, and move single edges.
                        moves.Add(new[] { -step / 2, 0, step, 0 }); moves.Add(new[] { step / 2, 0, -step, 0 });
                        moves.Add(new[] { 0, -step / 2, 0, step }); moves.Add(new[] { 0, step / 2, 0, -step });
                    }
                    else if (mode == "uniform")
                    {
                        moves.Add(new[] { -step / 2, -step / 2 / aspect, step, step / aspect });
                        moves.Add(new[] { step / 2, step / 2 / aspect, -step, -step / aspect });
                    }

                    foreach (double[] mv in moves)
                    {
                        double[] c = { best[0] + mv[0], best[1] + mv[1], best[2] + mv[2], best[3] + mv[3] };
                        if (c[2] < 2 || c[3] < 2) continue;
                        double e = Err(c);
                        if (e < bestE - 1e-4) { bestE = e; best = c; improved = true; }
                    }
                }
            }

            Img fin = ground.Clone();
            DrawLayers(o, "under", fin, view.X, view.Y, 1);
            fin.Over(RenderAt(sprite, nine, unit, view.X, view.Y, 1, view.W, view.H, best), 0, 0);
            DrawLayers(o, "over", fin, view.X, view.Y, 1);
            (double mean, double rms, double max) = ErrorIn(fin, original, view, region);
            Console.WriteLine(FormattableString.Invariant($"fit ({mode}): rect [{best[0]:0.00}, {best[1]:0.00}, {best[2]:0.00}, {best[3]:0.00}]  centre {best[0] + best[2] / 2:0.00},{best[1] + best[3] / 2:0.00}"));
            Console.WriteLine(FormattableString.Invariant($"  rms {startE:0.00} at the given rect -> {rms:0.00}  (mean {mean:0.00}, max {max:0.0})   moved {best[0] - rect[0]:+0.00;-0.00} {best[1] - rect[1]:+0.00;-0.00}  size {best[2] - rect[2]:+0.00;-0.00} {best[3] - rect[3]:+0.00;-0.00}"));
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // radial: mean colour on circles around a centre, for round elements (dots, discs, rings, knobs)
        private static (double r, double g, double b, double a) Bilinear(Img m, double x, double y)
        {
            double sx = x - 0.5, sy = y - 0.5;
            int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
            double fx = sx - ix, fy = sy - iy;
            int xa = Math.Clamp(ix, 0, m.W - 1), xb = Math.Clamp(ix + 1, 0, m.W - 1), ya = Math.Clamp(iy, 0, m.H - 1), yb = Math.Clamp(iy + 1, 0, m.H - 1);
            double r = 0, g = 0, b = 0, a = 0;
            void Add(int i, double w)
            {
                double pa = m.A[i] * w;
                r += m.R[i] * pa; g += m.G[i] * pa; b += m.B[i] * pa; a += pa;
            }

            Add(ya * m.W + xa, (1 - fx) * (1 - fy)); Add(ya * m.W + xb, fx * (1 - fy)); Add(yb * m.W + xa, (1 - fx) * fy); Add(yb * m.W + xb, fx * fy);
            return a > 1e-9 ? (r / a, g / a, b / a, a) : (0, 0, 0, 0);
        }

        private static List<(double rad, double r, double g, double b, double a, double spread)> RadialProfile(Img m, double cx, double cy, double rmax, double step)
        {
            var prof = new List<(double, double, double, double, double, double)>();
            for (double rad = 0; rad <= rmax + 1e-9; rad += step)
            {
                int n = Math.Max(8, (int)Math.Ceiling(2 * Math.PI * rad * 2));
                double r = 0, g = 0, b = 0, a = 0, l = 0, l2 = 0;
                for (int k = 0; k < n; k++)
                {
                    double th = 2 * Math.PI * k / n;
                    var s = Bilinear(m, cx + rad * Math.Cos(th), cy + rad * Math.Sin(th));
                    r += s.r * s.a; g += s.g * s.a; b += s.b * s.a; a += s.a;
                    double lum = (0.2126 * s.r + 0.7152 * s.g + 0.0722 * s.b) * s.a;
                    l += lum; l2 += lum * lum;
                }

                double spread = Math.Sqrt(Math.Max(0, l2 / n - l / n * (l / n)));
                prof.Add(a > 1e-9 ? (rad, r / a, g / a, b / a, a / n, spread) : (rad, 0, 0, 0, 0, spread));
            }

            return prof;
        }

        private static int Radial(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            double[] c = o.Has("center") ? o.Nums("center", 2) : new[] { m.W / 2.0, m.H / 2.0 };
            double rmax = o.Num("rmax", Math.Min(m.W, m.H) / 2.0), step = o.Num("step", 0.25);
            if (o.Has("auto"))
            {
                // The centre about which the picture is most nearly the same all the way round.
                double reach = o.Num("auto", 1.5), bestS = double.MaxValue;
                double[] best = c;
                // Coarse to fine: the symmetry measure is smooth around the true centre.
                double pstep = Math.Max(0.5, rmax / 60);
                foreach (double astep in new[] { Math.Max(0.4, reach / 4), 0.1 })
                {
                    double[] around = best;
                    double span = astep > 0.1 ? reach : 0.5;
                    for (double dy = -span; dy <= span + 1e-9; dy += astep)
                    {
                        for (double dx = -span; dx <= span + 1e-9; dx += astep)
                        {
                            double s = RadialProfile(m, around[0] + dx, around[1] + dy, rmax, pstep).Sum(p => p.spread);
                            if (s < bestS) { bestS = s; best = new[] { around[0] + dx, around[1] + dy }; }
                        }
                    }
                }

                c = best;
                Console.WriteLine(FormattableString.Invariant($"centre {c[0]:0.00},{c[1]:0.00} (most symmetric within {reach} px)"));
            }

            var prof = RadialProfile(m, c[0], c[1], rmax, step);
            var sb = new StringBuilder();
            foreach (var p in prof.Where((p, k) => k % Math.Max(1, (int)Math.Round(o.Num("print", 1) / step)) == 0))
                sb.Append(FormattableString.Invariant($"{p.rad:0.##}:{Img.Hex((float)p.r, (float)p.g, (float)p.b).Substring(1)}{(p.a < 0.995 ? $"/{p.a * 255:0}" : "")} "));
            Console.WriteLine(sb.ToString());

            // Edges: where colour (and alpha) changes fastest along the radius.
            var grad = new double[prof.Count];
            for (int k = 1; k < prof.Count; k++)
            {
                double dr = (Img.ToSrgb01((float)prof[k].r) * prof[k].a - Img.ToSrgb01((float)prof[k - 1].r) * prof[k - 1].a) * 255;
                double dg = (Img.ToSrgb01((float)prof[k].g) * prof[k].a - Img.ToSrgb01((float)prof[k - 1].g) * prof[k - 1].a) * 255;
                double db = (Img.ToSrgb01((float)prof[k].b) * prof[k].a - Img.ToSrgb01((float)prof[k - 1].b) * prof[k - 1].a) * 255;
                double da = (prof[k].a - prof[k - 1].a) * 255;
                grad[k] = Math.Sqrt(dr * dr + dg * dg + db * db + da * da) / step;
            }

            double thr = o.Num("thr", 6);
            for (int k = 2; k < prof.Count - 1; k++)
            {
                if (grad[k] < thr || grad[k] < grad[k - 1] || grad[k] < grad[k + 1]) continue;
                double den = grad[k - 1] - 2 * grad[k] + grad[k + 1];
                double off = Math.Abs(den) > 1e-9 ? Math.Clamp(0.5 * (grad[k - 1] - grad[k + 1]) / den, -0.5, 0.5) : 0;
                double at = prof[k].rad - step / 2 + off * step;
                int ka = Math.Max(0, k - (int)Math.Round(1.5 / step)), kb = Math.Min(prof.Count - 1, k + (int)Math.Round(1.5 / step));
                Console.WriteLine(FormattableString.Invariant($"  edge at r {at:0.00}  ({Img.Hex((float)prof[ka].r, (float)prof[ka].g, (float)prof[ka].b)}/a{prof[ka].a * 255:0} -> {Img.Hex((float)prof[kb].r, (float)prof[kb].g, (float)prof[kb].b)}/a{prof[kb].a * 255:0}, steepness {grad[k]:0})"));
            }

            return 0;
        }

        /// <summary>
        /// Moves the ring edges of a round element to given radii: every output pixel takes the
        /// colour found at the mapped radius in the same direction (or, with --avg, the mean
        /// all the way round at that radius, which also makes the element exactly symmetric).
        /// </summary>
        private static int RadialWarp(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] c = o.Has("center") ? o.Nums("center", 2) : new[] { m.W / 2.0, m.H / 2.0 };
            int size = o.Int("size", Math.Max(m.W, m.H));
            var pairs = new List<(double s, double d)> { (0, 0) };
            foreach (string p in o.Need("map").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = p.Split('>');
                pairs.Add((double.Parse(q[0], CultureInfo.InvariantCulture), double.Parse(q[1], CultureInfo.InvariantCulture)));
            }

            pairs = pairs.OrderBy(p => p.d).ToList();
            double SrcRadius(double d)
            {
                int k = 0;
                while (k < pairs.Count - 2 && d > pairs[k + 1].d) k++;
                double span = pairs[k + 1].d - pairs[k].d;
                double t = span > 1e-9 ? (d - pairs[k].d) / span : 0;
                return pairs[k].s + (pairs[k + 1].s - pairs[k].s) * t;      // beyond the last pair: the last segment continues
            }

            bool avg = o.Has("avg");
            double[] shift = o.Has("shift") ? o.Nums("shift", 2) : null, shiftR = o.Nums("shiftr", 2, new[] { 0.0, 0.0 });
            List<(double rad, double r, double g, double b, double a, double spread)> prof = null;
            double pstep = 0.1;
            if (avg) prof = RadialProfile(m, c[0], c[1], Math.Sqrt(m.W * (double)m.W + m.H * (double)m.H) / 2 + 2, pstep);
            if (avg && o.Has("colors"))
            {
                // "--colors r0-r1:#hex;..." (source radii): each stretch is one flat colour of the
                // element and gets the colour given; between two stretches the mix of the two
                // source colours (the anti-aliased edge) becomes the same mix of the two targets.
                var zones = o.Get("colors").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(z =>
                {
                    string[] q = z.Split(':');
                    double[] rr = q[0].Split('-').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    string[] ca = q[1].Split('@');      // "#hex" or "#hex@alpha": a rim that lets the ground show through
                    return (r0: rr[0], r1: rr[1], dst: Img.ParseHex(ca[0]), alpha: ca.Length > 1 ? double.Parse(ca[1], CultureInfo.InvariantCulture) : 1.0);
                }).OrderBy(z => z.r0).ToList();
                // The source colour of each stretch: the profile's mean over it.
                var srcCol = zones.Select(z =>
                {
                    var pts = prof.Where(pp => pp.rad >= z.r0 && pp.rad <= z.r1 && pp.a > 0.5).ToList();
                    return pts.Count == 0 ? (r: 0.0, g: 0.0, b: 0.0) : (r: pts.Average(pp => pp.r), g: pts.Average(pp => pp.g), b: pts.Average(pp => pp.b));
                }).ToList();
                for (int k = 0; k < prof.Count; k++)
                {
                    var pp = prof[k];
                    int zi = zones.FindIndex(z => pp.rad >= z.r0 && pp.rad <= z.r1);
                    (float r, float g, float b) col;
                    double za;
                    if (zi >= 0) { col = zones[zi].dst; za = zones[zi].alpha; }
                    else
                    {
                        int next = zones.FindIndex(z => z.r0 > pp.rad);
                        if (next <= 0) { var zz = next == 0 ? zones[0] : zones[zones.Count - 1]; col = zz.dst; za = zz.alpha; }
                        else
                        {
                            var sa = srcCol[next - 1]; var sb = srcCol[next];
                            double dr = sb.r - sa.r, dg = sb.g - sa.g, db = sb.b - sa.b, den = dr * dr + dg * dg + db * db;
                            // Two stretches of the same source colour (a rim split off a disc): the edge is simply half-way.
                            double t = den > 1e-5 ? Math.Clamp(((pp.r - sa.r) * dr + (pp.g - sa.g) * dg + (pp.b - sa.b) * db) / den, 0, 1)
                                : Math.Clamp((pp.rad - zones[next - 1].r1) / Math.Max(1e-9, zones[next].r0 - zones[next - 1].r1), 0, 1);
                            var da = zones[next - 1]; var dbb = zones[next];
                            // Mix premultiplied, so a half-transparent rim does not tint its neighbour.
                            double a0 = da.alpha * (1 - t), a1 = dbb.alpha * t;
                            za = a0 + a1;
                            col = za > 1e-9
                                ? ((float)((da.dst.r * a0 + dbb.dst.r * a1) / za), (float)((da.dst.g * a0 + dbb.dst.g * a1) / za), (float)((da.dst.b * a0 + dbb.dst.b * a1) / za))
                                : da.dst;
                        }
                    }

                    // The source's own alpha only matters where it is a hole (the inside of a ring).
                    double srcA = o.Has("outer") ? (zones.All(z => pp.rad < z.r0) ? pp.a : 1.0) : pp.a;
                    prof[k] = (pp.rad, col.r, col.g, col.b, srcA * za, pp.spread);
                }
            }
            // --keep: the picture keeps its size and the centre stays where it is (for a round part
            // of a larger control, e.g. the knob of a toggle); otherwise a square around the centre.
            bool keep = o.Has("keep");
            int outW = keep ? m.W : size, outH = keep ? m.H : size;
            var n = new Img(outW, outH, 0f);
            const int ss = 3;
            double ocx = keep ? c[0] : size / 2.0, ocy = keep ? c[1] : size / 2.0;
            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    double r = 0, g = 0, b = 0, a = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        for (int i = 0; i < ss; i++)
                        {
                            double dx = x + (i + 0.5) / ss - ocx, dy = y + (j + 0.5) / ss - ocy;
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            double s = SrcRadius(d);
                            (double r, double g, double b, double a) v;
                            if (avg)
                            {
                                double f = s / pstep;
                                int k = Math.Clamp((int)Math.Floor(f), 0, prof.Count - 2);
                                double t = Math.Clamp(f - k, 0, 1);
                                double pa = prof[k].a * (1 - t) + prof[k + 1].a * t;
                                v = pa > 1e-9
                                    ? ((prof[k].r * prof[k].a * (1 - t) + prof[k + 1].r * prof[k + 1].a * t) / pa, (prof[k].g * prof[k].a * (1 - t) + prof[k + 1].g * prof[k + 1].a * t) / pa, (prof[k].b * prof[k].a * (1 - t) + prof[k + 1].b * prof[k + 1].a * t) / pa, pa)
                                    : (0, 0, 0, 0);
                            }
                            else
                            {
                                double k = d > 1e-9 ? s / d : 0;
                                double sx = c[0] + dx * k, sy = c[1] + dy * k;
                                if (shift != null)
                                {
                                    // The round part (and what hugs it) moves by "shift"; beyond shiftr[1]
                                    // nothing moves; in between the two readings are blended.
                                    double ex = dx - shift[0], ey = dy - shift[1], ed = Math.Sqrt(ex * ex + ey * ey);
                                    double es = SrcRadius(ed), ek = ed > 1e-9 ? es / ed : 0;
                                    double wgt = Math.Clamp((shiftR[1] - ed) / Math.Max(1e-9, shiftR[1] - shiftR[0]), 0, 1);
                                    sx = (c[0] + ex * ek) * wgt + sx * (1 - wgt);
                                    sy = (c[1] + ey * ek) * wgt + sy * (1 - wgt);
                                }

                                v = sx < -0.5 || sy < -0.5 || sx > m.W + 0.5 || sy > m.H + 0.5 ? (0, 0, 0, 0) : Bilinear(m, sx, sy);
                            }

                            r += v.r * v.a; g += v.g * v.a; b += v.b * v.a; a += v.a;
                        }
                    }

                    int t2 = y * outW + x;
                    if (a <= 1e-9) continue;
                    n.A[t2] = (float)(a / (ss * ss));
                    n.R[t2] = (float)(r / a); n.G[t2] = (float)(g / a); n.B[t2] = (float)(b / a);
                    if (o.Has("outer"))
                    {
                        // An exact circular edge at this output radius.
                        double ddx = x + 0.5 - ocx, ddy = y + 0.5 - ocy;
                        n.A[t2] *= (float)Math.Clamp(o.Num("outer", 0) + 0.5 - Math.Sqrt(ddx * ddx + ddy * ddy), 0, 1);
                    }
                }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {outW}x{outH}  radii {string.Join("  ", pairs.Skip(1).Select(p => FormattableString.Invariant($"{p.s:0.##}->{p.d:0.##}")))}{(avg ? "  (averaged all the way round)" : "")}");
            return 0;
        }

        // ------------------------------------------------------------------------------------
        private static int HProfile(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            int[] r = o.Ints("rect", 4, new[] { 0, 0, m.W, m.H });
            bool alongX = o.Get("axis", "x") == "x";
            int len = alongX ? r[2] : r[3], other = alongX ? r[3] : r[2];
            var sb = new StringBuilder();
            for (int p = 0; p < len; p++)
            {
                double rr = 0, gg = 0, bb = 0, aa = 0;
                for (int q = 0; q < other; q++)
                {
                    int x = alongX ? r[0] + p : r[0] + q, y = alongX ? r[1] + q : r[1] + p;
                    if (x < 0 || y < 0 || x >= m.W || y >= m.H) continue;
                    int i = y * m.W + x;
                    rr += m.R[i] * m.A[i]; gg += m.G[i] * m.A[i]; bb += m.B[i] * m.A[i]; aa += m.A[i];
                }

                string hex = aa > 1e-9 ? Img.Hex((float)(rr / aa), (float)(gg / aa), (float)(bb / aa)).Substring(1) : "------";
                double al = aa / other;
                sb.Append($"{(alongX ? r[0] : r[1]) + p}:{hex}{(al < 0.995 ? $"/{al * 255:0}" : "")} ");
            }

            Console.WriteLine(sb.ToString());
            return 0;
        }

        private static int Swatch(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            foreach (string rs in o.Need("rect").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int[] r = rs.Split(',').Select(v => (int)Math.Round(double.Parse(v.Trim(), CultureInfo.InvariantCulture))).ToArray();
                var px = new List<(byte r, byte g, byte b)>();
                double a = 0; int n = 0;
                for (int y = r[1]; y < r[1] + r[3]; y++)
                {
                    for (int x = r[0]; x < r[0] + r[2]; x++)
                    {
                        if (x < 0 || y < 0 || x >= m.W || y >= m.H) continue;
                        int i = y * m.W + x;
                        a += m.A[i]; n++;
                        if (m.A[i] > 0.5f) px.Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i])));
                    }
                }

                (int r, int g, int b) med = Pct(px, 50), lo = Pct(px, 5), hi = Pct(px, 95);
                Console.WriteLine($"{rs,-20} mean {Mean(px)}  median #{med.r:X2}{med.g:X2}{med.b:X2}  dark5% #{lo.r:X2}{lo.g:X2}{lo.b:X2}  bright5% #{hi.r:X2}{hi.g:X2}{hi.b:X2}  alpha {(n > 0 ? a / n * 255 : 0):0}  (n={px.Count})");
            }

            return 0;
        }

        private static int AlphaOf(Opts o)
        {
            Img a = Img.Load(o.Need("in")), b = Img.Load(o.Need("from"));
            if (a.W != b.W || a.H != b.H) throw new ArgumentException($"sizes differ: {a.W}x{a.H} and {b.W}x{b.H}");
            Matte.BleedColor(a, 0.5f);
            Array.Copy(b.A, a.A, a.A.Length);
            for (int i = 0; i < a.Count; i++) if (a.A[i] <= 0f) { a.R[i] = 0; a.G[i] = 0; a.B[i] = 0; }
            a.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {a.W}x{a.H}");
            return 0;
        }

        private static int LerpCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var c = Img.ParseHex(o.Need("to"));
            float k = (float)o.Num("amount", 0.3);
            // In sRGB, like an editor's colour overlay at k opacity.
            float tr = Img.ToSrgb01(c.r), tg = Img.ToSrgb01(c.g), tb = Img.ToSrgb01(c.b);
            for (int i = 0; i < m.Count; i++)
            {
                if (m.A[i] <= 0f) continue;
                float r = Img.ToSrgb01(m.R[i]), g = Img.ToSrgb01(m.G[i]), b = Img.ToSrgb01(m.B[i]);
                m.R[i] = Img.ToLinear(r + (tr - r) * k); m.G[i] = Img.ToLinear(g + (tg - g) * k); m.B[i] = Img.ToLinear(b + (tb - b) * k);
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H}  mixed {k:0.##} towards {o.Need("to")}");
            return 0;
        }

        /// <summary>
        /// Moves chosen columns and rows to chosen places: "--mapx s0>d0;s1>d1" sends source
        /// column s0 to output column d0 and so on, straight in between (the image's two ends are
        /// implied). For correcting proportions without touching what must stay round: stretch the
        /// plain stretch of a pill, not its caps or its knob.
        /// </summary>
        private static int Warp(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int[] size = o.Ints("size", 2, new[] { m.W, m.H });
            List<(double s, double d)> Pairs(string key, double srcLen, double dstLen)
            {
                var p = new List<(double s, double d)>();
                if (o.Has(key))
                {
                    foreach (string q in o.Get(key).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] v = q.Split('>');
                        p.Add((double.Parse(v[0], CultureInfo.InvariantCulture), double.Parse(v[1], CultureInfo.InvariantCulture)));
                    }
                }

                if (!p.Any(v => v.d <= 0)) p.Add((0, 0));
                if (!p.Any(v => v.d >= dstLen)) p.Add((srcLen, dstLen));
                return p.OrderBy(v => v.d).ToList();
            }

            List<(double s, double d)> px = Pairs("mapx", m.W, size[0]), py = Pairs("mapy", m.H, size[1]);
            Img n = WarpImg(m, size[0], size[1], px, py);
            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H} -> {size[0]}x{size[1]}  x: {string.Join(" ", px.Select(v => FormattableString.Invariant($"{v.s:0.#}>{v.d:0.#}")))}  y: {string.Join(" ", py.Select(v => FormattableString.Invariant($"{v.s:0.#}>{v.d:0.#}")))}");
            return 0;
        }

        private static Img WarpImg(Img m, int outW, int outH, List<(double s, double d)> px, List<(double s, double d)> py)
        {
            int[] size = { outW, outH };
            double Src(List<(double s, double d)> p, double d)
            {
                int k = 0;
                while (k < p.Count - 2 && d > p[k + 1].d) k++;
                double span = p[k + 1].d - p[k].d;
                return p[k].s + (span > 1e-9 ? (d - p[k].d) / span : 0) * (p[k + 1].s - p[k].s);
            }

            var n = new Img(size[0], size[1], 0f);
            const int ss = 4;
            var sxs = new double[size[0] * ss];
            for (int i = 0; i < sxs.Length; i++) sxs[i] = Src(px, (i + 0.5) / ss);
            for (int y = 0; y < size[1]; y++)
            {
                var sys = new double[ss];
                for (int j = 0; j < ss; j++) sys[j] = Src(py, y + (j + 0.5) / ss);
                for (int x = 0; x < size[0]; x++)
                {
                    double r = 0, g = 0, b = 0, a = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        for (int i = 0; i < ss; i++)
                        {
                            var v = Bilinear(m, sxs[x * ss + i], sys[j]);
                            r += v.r * v.a; g += v.g * v.a; b += v.b * v.a; a += v.a;
                        }
                    }

                    int t = y * size[0] + x;
                    if (a <= 1e-9) continue;
                    n.A[t] = (float)(a / (ss * ss));
                    n.R[t] = (float)(r / a); n.G[t] = (float)(g / a); n.B[t] = (float)(b / a);
                }
            }

            return n;
        }

        /// <summary>
        /// A drawn frame (outline + fill: field, dropdown, code box, pill) brought to a given size,
        /// outline weight and corner radius: the outline band and the corner zones are scaled to
        /// their targets, the plain middle takes up the rest, and the outer edge becomes the exact
        /// rounded rectangle. "--src" is the outline's outer edge box in the cell; "--zonex
        /// s0,s1>d0,d1" pins a stretch of columns to a place (a chevron that must keep its shape).
        /// </summary>
        private static int Frame(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] src = o.Nums("src", 4);
            int[] size = o.Ints("size", 2);
            double sl = o.Num("srcline", 0), sr = o.Num("srcradius", 0), line = o.Num("line", 0), radius = o.Num("radius", 0);
            double srr = o.Num("srcradiusright", sr);
            List<(double s, double d)> Axis(double a0, double a1, double len, string zoneKey, double r0, double r1)
            {
                var p = new List<(double s, double d)> { (a0, 0), (a1, len) };
                if (sl > 0 && line > 0) { p.Add((a0 + sl, line)); p.Add((a1 - sl, len - line)); }
                if (r0 > sl && radius > line) { p.Add((a0 + r0, radius)); p.Add((a1 - r1, len - radius)); }
                if (o.Has(zoneKey))
                {
                    foreach (string z in o.Get(zoneKey).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] q = z.Split('>');
                        double[] sv = q[0].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                        double[] dv = q[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                        p.Add((sv[0], dv[0])); p.Add((sv[1], dv[1]));
                    }
                }

                p = p.OrderBy(v => v.d).ToList();
                for (int k = 1; k < p.Count; k++) if (p[k].s < p[k - 1].s) throw new ArgumentException($"the zones overlap: {p[k - 1].s}>{p[k - 1].d} then {p[k].s}>{p[k].d}");
                return p;
            }

            List<(double s, double d)> px = Axis(src[0], src[2], size[0], "zonex", sr, srr), py = Axis(src[1], src[3], size[1], "zoney", sr, sr);
            Img n = WarpImg(m, size[0], size[1], px, py);
            string type = o.Get("shape", "roundrect");
            if (o.Has("linecolor") && line > 0)
            {
                // The outline redrawn at exactly the target weight in one colour: the band is first
                // filled from what lies just inside it (the fill and its highlight), then the line
                // is laid over it. ChatGPT's own line is only a few pixels wide on the sheet and its
                // outer pixels carry the magenta it was drawn on.
                var lc = Img.ParseHex(o.Get("linecolor"));
                var lc2 = o.Has("linecolor2") ? Img.ParseHex(o.Get("linecolor2")) : lc;     // at the bottom edge: a rim lit from above
                var edge = new Shape(type == "none" ? "roundrect" : type, 0, 0, size[0], size[1], radius);
                var hole = new float[n.Count];
                var valid = new float[n.Count];
                var depth = new float[n.Count];
                for (int y = 0; y < n.H; y++)
                {
                    for (int x = 0; x < n.W; x++)
                    {
                        int i = y * n.W + x;
                        depth[i] = (float)-edge.Sdf(x + 0.5, y + 0.5);
                        hole[i] = depth[i] < line + 0.5 ? 1f : 0f;
                        valid[i] = depth[i] >= line + 0.5 ? 1f : 0f;
                    }
                }

                float[] alpha = (float[])n.A.Clone();
                Inpaint.Fill(n, hole, valid, grain: false, relax: 80);
                Array.Copy(alpha, n.A, alpha.Length);
                for (int i = 0; i < n.Count; i++)
                {
                    float k = Math.Clamp((float)line + 0.5f - depth[i], 0f, 1f);
                    if (k <= 0f) continue;
                    float ty = n.H > 1 ? (i / n.W + 0.5f) / n.H : 0f;
                    float cr = lc.r + (lc2.r - lc.r) * ty, cg = lc.g + (lc2.g - lc.g) * ty, cb = lc.b + (lc2.b - lc.b) * ty;
                    n.R[i] += (cr - n.R[i]) * k; n.G[i] += (cg - n.G[i]) * k; n.B[i] += (cb - n.B[i]) * k;
                    n.A[i] = 1f;
                }
            }

            if (type != "none")
            {
                Matte.BleedColor(n, 0.5f);
                var shape = new Shape(type, 0, 0, size[0], size[1], radius);
                // --hollow: only the outline itself (a selection outline drawn around a card).
                bool hollow = o.Has("hollow") && line > 0;
                var inner = new Shape(type, line, line, size[0] - 2 * line, size[1] - 2 * line, Math.Max(0, radius - line));
                for (int y = 0; y < n.H; y++)
                    for (int x = 0; x < n.W; x++)
                        n.A[y * n.W + x] = shape.Coverage(x + 0.5, y + 0.5) * (hollow ? 1f - inner.Coverage(x + 0.5, y + 0.5) : 1f);
                if (hollow && o.Has("linecolor"))
                {
                    var lc = Img.ParseHex(o.Get("linecolor"));
                    for (int i = 0; i < n.Count; i++) { n.R[i] = lc.r; n.G[i] = lc.g; n.B[i] = lc.b; }
                }

                for (int i = 0; i < n.Count; i++) if (n.A[i] <= 0f) { n.R[i] = 0; n.G[i] = 0; n.B[i] = 0; }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {size[0]}x{size[1]}  x: {string.Join(" ", px.Select(v => FormattableString.Invariant($"{v.s:0.#}>{v.d:0.#}")))}  y: {string.Join(" ", py.Select(v => FormattableString.Invariant($"{v.s:0.#}>{v.d:0.#}")))}");
            return 0;
        }

        /// <summary>
        /// Makes a flat glyph on a plain ground (a chevron, a check mark) heavier or lighter by a
        /// fraction of a pixel: its coverage is blurred a little and re-thresholded so that every
        /// edge moves out by --grow pixels (negative: in), which also rounds its corners as a
        /// brush would. Only inside --rect; ink and ground colours are measured there.
        /// </summary>
        private static int Thicken(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int[] r = o.Ints("rect", 4, new[] { 0, 0, m.W, m.H });
            double grow = o.Num("grow", 0.5), sigma = o.Num("sigma", 1.2);
            bool dark = o.Get("ink", "dark") != "light";
            int w = r[2], h = r[3];
            // Ground = median of the rect's border, ink = the most extreme 5% inside it.
            var border = new List<(byte r, byte g, byte b)>();
            var all = new List<(byte r, byte g, byte b)>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (r[1] + y) * m.W + r[0] + x;
                    var px = (S8(m.R[i]), S8(m.G[i]), S8(m.B[i]));
                    all.Add(px);
                    if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) border.Add(px);
                }
            }

            (int r, int g, int b) gnd = Pct(border, 50), ink = Pct(all, dark ? 2 : 98);
            double lg = 0.2126 * gnd.r + 0.7152 * gnd.g + 0.0722 * gnd.b, li = 0.2126 * ink.r + 0.7152 * ink.g + 0.0722 * ink.b;
            var cov = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (r[1] + y) * m.W + r[0] + x;
                    double l = 0.2126 * S8(m.R[i]) + 0.7152 * S8(m.G[i]) + 0.0722 * S8(m.B[i]);
                    cov[y * w + x] = (float)Math.Clamp((lg - l) / (lg - li), 0, 1);
                }
            }

            // Gaussian blur (separable), then a threshold that moves a straight edge by "grow".
            int rad = (int)Math.Ceiling(sigma * 3);
            var k = new double[2 * rad + 1];
            double ks = 0;
            for (int t = -rad; t <= rad; t++) { k[t + rad] = Math.Exp(-t * t / (2 * sigma * sigma)); ks += k[t + rad]; }
            var tmp = new double[w * h];
            var blur = new double[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = 0;
                    for (int t = -rad; t <= rad; t++) v += cov[y * w + Math.Clamp(x + t, 0, w - 1)] * k[t + rad];
                    tmp[y * w + x] = v / ks;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = 0;
                    for (int t = -rad; t <= rad; t++) v += tmp[Math.Clamp(y + t, 0, h - 1) * w + x] * k[t + rad];
                    blur[y * w + x] = v / ks;
                }

            double Phi(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));
            double thr = Phi(-grow / sigma);
            double slope = Math.Exp(-grow * grow / (2 * sigma * sigma)) / (sigma * Math.Sqrt(2 * Math.PI));   // coverage per px at the new edge
            float ir = Img.ToLinear((byte)ink.r), ig = Img.ToLinear((byte)ink.g), ib = Img.ToLinear((byte)ink.b);
            // The ground under the glyph: the rect with the glyph (and 2 px around it) filled in
            // from what surrounds it, so a satin ground keeps its shading where the glyph recedes.
            Img ground = m.Crop(r[0], r[1], w, h);
            var holeMask = new float[w * h];
            for (int i = 0; i < holeMask.Length; i++) holeMask[i] = cov[i] > 0.02f ? 1f : 0f;
            holeMask = Inpaint.Dilate(holeMask, w, h, 2);
            float[] keepA = (float[])ground.A.Clone();
            Inpaint.Fill(ground, holeMask, null, grain: false, relax: 120);
            Array.Copy(keepA, ground.A, keepA.Length);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (r[1] + y) * m.W + r[0] + x, gi = y * w + x;
                    float c0 = cov[gi];
                    float c1 = (float)Math.Clamp(0.5 + (blur[gi] - thr) / slope, 0, 1);
                    if (c0 <= 0.002f && c1 <= 0.002f) continue;
                    m.R[i] = Math.Clamp(ground.R[gi] * (1 - c1) + ir * c1, 0f, 1f);
                    m.G[i] = Math.Clamp(ground.G[gi] * (1 - c1) + ig * c1, 0f, 1f);
                    m.B[i] = Math.Clamp(ground.B[gi] * (1 - c1) + ib * c1, 0f, 1f);
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  glyph in {r[0]},{r[1]} {w}x{h}: ink #{ink.r:X2}{ink.g:X2}{ink.b:X2} on #{gnd.r:X2}{gnd.g:X2}{gnd.b:X2}, edges moved {grow:+0.##;-0.##} px (sigma {sigma})");
            return 0;
        }

        private static double Erf(double x)
        {
            // Abramowitz-Stegun 7.1.26
            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
            double y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return x >= 0 ? y : -y;
        }

        /// <summary>
        /// The dashes of a dashed circle: how dark (mockup) or how opaque (sprite) the ring is at
        /// each angle, as a list of dashes with their start and end in degrees (0 = right, clockwise).
        /// </summary>
        private static int Angular(Opts o)
        {
            bool sprite = o.Has("in");
            Img m = sprite ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            double[] c = o.Nums("center", 2);
            double r = o.Num("r", 0), wdt = o.Num("width", 2), stepDeg = o.Num("step", 0.25);
            int n = (int)Math.Round(360 / stepDeg);
            var v = new double[n];
            for (int k = 0; k < n; k++)
            {
                double th = 2 * Math.PI * k / n, acc = 0; int cnt = 0;
                for (double rr = r - wdt / 2; rr <= r + wdt / 2 + 1e-9; rr += 0.25)
                {
                    // --rx/--ry: an ellipse (the mockups' circles are often a little wider than high).
                    double ex = o.Num("rx", r) + (rr - r), ey = o.Num("ry", r) + (rr - r);
                    var sp = Bilinear(m, c[0] + ex * Math.Cos(th), c[1] + ey * Math.Sin(th));
                    acc += sprite ? sp.a : 1 - Img.ToSrgb01((float)(0.2126 * sp.r + 0.7152 * sp.g + 0.0722 * sp.b));
                    cnt++;
                }

                v[k] = acc / cnt;
            }

            double lo = v.Min(), hi = v.Max(), thr = o.Num("thr", (lo + hi) / 2);
            var runs = new List<(double a0, double a1)>();
            int start = Array.FindIndex(v, x => x < thr);       // begin in a gap
            if (start < 0) { Console.WriteLine("no gaps: a full ring"); return 0; }
            bool on = false; double a0 = 0;
            for (int q = 1; q <= n; q++)
            {
                int k = (start + q) % n;
                bool now = v[k] >= thr;
                double ang = (start + q) * stepDeg;
                if (now && !on) { on = true; a0 = ang; }
                else if (!now && on) { on = false; runs.Add((a0 % 360, ang % 360)); }
            }

            Console.WriteLine(FormattableString.Invariant($"{runs.Count} dashes at r {r:0.##} (value {lo:0.00}..{hi:0.00}, threshold {thr:0.00}); mean dash {runs.Average(x => (x.a1 - x.a0 + 360) % 360):0.0} deg, mean period {360.0 / Math.Max(1, runs.Count):0.0} deg"));
            Console.WriteLine(string.Join(" ", runs.Select(x => FormattableString.Invariant($"{x.a0:0.#}-{x.a1:0.#}"))));
            return 0;
        }

        /// <summary>
        /// Colours along rays from a centre, every 45 degrees (0 = right, 90 = down): for a shadow
        /// or a highlight that is not the same all the way round. "--scale" converts the radii
        /// given in mockup pixels to the image's own pixels (a sprite at 2.3, a cell at its scale).
        /// </summary>
        private static int Rays(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            double[] c = o.Nums("center", 2);
            double from = o.Num("from", 0), to = o.Num("to", 10), step = o.Num("step", 0.5), scale = o.Num("scale", 1);
            for (int deg = 0; deg < 360; deg += o.Int("every", 45))
            {
                var sb = new StringBuilder(FormattableString.Invariant($"{deg,3} deg: "));
                double th = deg * Math.PI / 180;
                for (double r = from; r <= to + 1e-9; r += step)
                {
                    // Average over a small arc (3 samples across 6 degrees) to steady the reading.
                    double rr = 0, gg = 0, bb = 0, aa = 0;
                    for (int k = -1; k <= 1; k++)
                    {
                        double t2 = th + k * 3 * Math.PI / 180;
                        var sp = Bilinear(m, c[0] + r * scale * Math.Cos(t2), c[1] + r * scale * Math.Sin(t2));
                        rr += sp.r * sp.a; gg += sp.g * sp.a; bb += sp.b * sp.a; aa += sp.a;
                    }

                    sb.Append(aa > 1e-9 ? FormattableString.Invariant($"{r:0.#}:{Img.Hex((float)(rr / aa), (float)(gg / aa), (float)(bb / aa)).Substring(1)} ") : FormattableString.Invariant($"{r:0.#}:------ "));
                }

                Console.WriteLine(sb.ToString());
            }

            return 0;
        }

        /// <summary>
        /// A picture made of two flat colours (a silhouette on a disc): every pixel is a mix of the
        /// two, and is rebuilt as the same mix of two new colours, the second of which may run
        /// from top to bottom ("--to2 0:#hex;0.5:#hex;1:#hex"). Inside --radius of the centre only.
        /// </summary>
        private static int TwoTone(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var a = Img.ParseHex(o.Need("a"));
            var b = Img.ParseHex(o.Need("b"));
            var a2 = Img.ParseHex(o.Get("to1", o.Need("a")));
            var stops = o.Need("to2").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(q =>
            {
                string[] v = q.Split(':');
                return v.Length == 1 ? (t: 0.0, c: Img.ParseHex(v[0])) : (t: double.Parse(v[0], CultureInfo.InvariantCulture), c: Img.ParseHex(v[1]));
            }).OrderBy(q => q.t).ToList();
            double[] c = o.Has("center") ? o.Nums("center", 2) : new[] { m.W / 2.0, m.H / 2.0 };
            double radius = o.Num("radius", Math.Max(m.W, m.H));
            float dr = b.r - a.r, dg = b.g - a.g, db = b.b - a.b, den = dr * dr + dg * dg + db * db;
            for (int y = 0; y < m.H; y++)
            {
                // The second colour at this height: 0 = top of the circle, 1 = its bottom.
                double ty = Math.Clamp((y + 0.5 - (c[1] - radius)) / (2 * radius), 0, 1);
                int k = 0;
                while (k < stops.Count - 2 && ty > stops[k + 1].t) k++;
                (float r, float g, float b) b2 = stops[0].c;
                if (stops.Count > 1)
                {
                    float u = (float)Math.Clamp((ty - stops[k].t) / Math.Max(1e-9, stops[k + 1].t - stops[k].t), 0, 1);
                    b2 = (stops[k].c.r + (stops[k + 1].c.r - stops[k].c.r) * u, stops[k].c.g + (stops[k + 1].c.g - stops[k].c.g) * u, stops[k].c.b + (stops[k + 1].c.b - stops[k].c.b) * u);
                }

                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] <= 0f) continue;
                    double dist = Math.Sqrt((x + 0.5 - c[0]) * (x + 0.5 - c[0]) + (y + 0.5 - c[1]) * (y + 0.5 - c[1]));
                    float wgt = (float)Math.Clamp(radius - dist + 0.5, 0, 1);
                    if (wgt <= 0f) continue;
                    float t = Math.Clamp(((m.R[i] - a.r) * dr + (m.G[i] - a.g) * dg + (m.B[i] - a.b) * db) / den, 0f, 1f);
                    float nr = a2.r + (b2.r - a2.r) * t, ng = a2.g + (b2.g - a2.g) * t, nb = a2.b + (b2.b - a2.b) * t;
                    m.R[i] += (nr - m.R[i]) * wgt; m.G[i] += (ng - m.G[i]) * wgt; m.B[i] += (nb - m.B[i]) * wgt;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  two tones {o.Need("a")} / {o.Need("b")} -> {o.Get("to1", o.Need("a"))} / {o.Need("to2")}");
            return 0;
        }

        /// <summary>One flat colour in a ring between two radii (edges anti-aliased), over whatever is there.</summary>
        private static int Annulus(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] c = o.Has("center") ? o.Nums("center", 2) : new[] { m.W / 2.0, m.H / 2.0 };
            double r0 = o.Num("r0", 0), r1 = o.Num("r1", 0);
            var col = Img.ParseHex(o.Need("color"));
            float al = (float)o.Num("alpha", 1);
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    double d = Math.Sqrt((x + 0.5 - c[0]) * (x + 0.5 - c[0]) + (y + 0.5 - c[1]) * (y + 0.5 - c[1]));
                    float k = (float)(Math.Clamp(d - r0 + 0.5, 0, 1) * Math.Clamp(r1 - d + 0.5, 0, 1)) * al;
                    if (k <= 0f) continue;
                    int i = y * m.W + x;
                    float a = m.A[i], oa = k + a * (1 - k);
                    m.R[i] = (col.r * k + m.R[i] * a * (1 - k)) / oa; m.G[i] = (col.g * k + m.G[i] * a * (1 - k)) / oa; m.B[i] = (col.b * k + m.B[i] * a * (1 - k)) / oa;
                    m.A[i] = oa;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  ring {r0:0.##}..{r1:0.##} in {o.Need("color")}"));
            return 0;
        }

        /// <summary>
        /// A dashed circle drawn over the image: --count dashes of --dash degrees each, starting at
        /// --start degrees (0 = right, clockwise), between radii r0 and r1, with round or butt ends.
        /// </summary>
        private static int Dashes(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] c = o.Has("center") ? o.Nums("center", 2) : new[] { m.W / 2.0, m.H / 2.0 };
            double r0 = o.Num("r0", 0), r1 = o.Num("r1", 0), start = o.Num("start", 0), dash = o.Num("dash", 6);
            int count = o.Int("count", 36);
            var col = Img.ParseHex(o.Need("color"));
            double period = 360.0 / count, rm = (r0 + r1) / 2, half = (r1 - r0) / 2;
            bool round = o.Get("caps", "round") == "round";
            double dashLen = dash * Math.PI / 180 * rm, periodLen = period * Math.PI / 180 * rm;
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    double dx = x + 0.5 - c[0], dy = y + 0.5 - c[1], d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < r0 - 1.5 || d > r1 + 1.5) continue;
                    double ang = (Math.Atan2(dy, dx) * 180 / Math.PI - start) % 360;
                    if (ang < 0) ang += 360;
                    double along = (ang % period) * Math.PI / 180 * rm;          // px along the arc from the start of this dash
                    double dist;
                    if (round)
                    {
                        // A stroke of half-width "half" whose centre line runs from half to dashLen - half.
                        double a0 = Math.Min(half, dashLen / 2), a1 = Math.Max(dashLen - half, dashLen / 2);
                        double t = along < a0 ? a0 - along : (along > a1 ? Math.Min(along - a1, periodLen - along + a0) : 0);
                        dist = Math.Sqrt(t * t + (d - rm) * (d - rm)) - half;
                    }
                    else
                    {
                        // Signed distance along the arc: negative inside the dash, so its middle is fully covered.
                        double t = along <= dashLen ? -Math.Min(along, dashLen - along) : Math.Min(along - dashLen, periodLen - along);
                        dist = Math.Max(Math.Abs(d - rm) - half, t);
                    }

                    float k = (float)Math.Clamp(0.5 - dist, 0, 1);
                    if (k <= 0f) continue;
                    int i = y * m.W + x;
                    float a = m.A[i], oa = k + a * (1 - k);
                    m.R[i] = (col.r * k + m.R[i] * a * (1 - k)) / oa; m.G[i] = (col.g * k + m.G[i] * a * (1 - k)) / oa; m.B[i] = (col.b * k + m.B[i] * a * (1 - k)) / oa;
                    m.A[i] = oa;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  {count} dashes of {dash:0.##} deg from {start:0.##} deg, r {r0:0.##}..{r1:0.##}, {o.Need("color")}"));
            return 0;
        }

        /// <summary>Replaces the colour (not the alpha) inside a shape, or everywhere, with one colour.</summary>
        private static int FillCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var c = Img.ParseHex(o.Need("color"));
            Shape? shape = null;
            if (o.Has("shape"))
            {
                double[] r = o.Nums("rect", 4, new double[] { 0, 0, m.W, m.H });
                shape = new Shape(o.Get("shape"), r[0], r[1], r[2], r[3], o.Num("radius", 0));
            }

            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    float k = shape?.Coverage(x + 0.5, y + 0.5) ?? 1f;
                    if (o.Has("outside")) k = 1f - k;
                    if (k <= 0f) continue;
                    m.R[i] += (c.r - m.R[i]) * k; m.G[i] += (c.g - m.G[i]) * k; m.B[i] += (c.b - m.B[i]) * k;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H}  filled {o.Need("color")}");
            return 0;
        }

        /// <summary>Replaces the colour (not the alpha) with a top-to-bottom gradient through the given stops ("0:#hex;0.5:#hex;1:#hex").</summary>
        private static int VGrad(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var stops = o.Need("stops").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s =>
            {
                string[] q = s.Split(':');
                return (t: double.Parse(q[0], CultureInfo.InvariantCulture), c: HexBytes(q[1]));
            }).OrderBy(s => s.t).ToList();
            for (int y = 0; y < m.H; y++)
            {
                double t = m.H > 1 ? (y + 0.5) / m.H : 0;
                int k = 0;
                while (k < stops.Count - 2 && t > stops[k + 1].t) k++;
                double u = Math.Clamp((t - stops[k].t) / Math.Max(1e-9, stops[k + 1].t - stops[k].t), 0, 1);
                float r = Img.ToLinear((float)((stops[k].c[0] + (stops[k + 1].c[0] - stops[k].c[0]) * u) / 255.0));
                float g = Img.ToLinear((float)((stops[k].c[1] + (stops[k + 1].c[1] - stops[k].c[1]) * u) / 255.0));
                float b = Img.ToLinear((float)((stops[k].c[2] + (stops[k + 1].c[2] - stops[k].c[2]) * u) / 255.0));
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    m.R[i] = r; m.G[i] = g; m.B[i] = b;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H}  vertical gradient {o.Need("stops")}");
            return 0;
        }

        /// <summary>
        /// Colour correction for a crimson element with white parts (toggle, slider fill, badge,
        /// disc): ChatGPT returns the satin crimson as a purer, brighter red, and the drift differs
        /// between its light and dark tones, so a per-channel curve cannot undo it. Each pixel is
        /// split into "how white" (from its green, which every shade of the crimson lacks) and the
        /// shade of crimson under it (its red with the white taken out); the shade picks a colour
        /// on a ramp measured on the mockup ("--ramp 148:#760615;165:#8B0718": ChatGPT's red value
        /// to mockup colour) and the white is put back.
        /// </summary>
        private static int Crimson(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            (double v, double[] c)[] ramp = ParseStops(o.Need("ramp"));
            double gc = o.Num("gc", 5);
            double[] w = HexBytes(o.Get("white", "#FEFEFE")), wt = HexBytes(o.Get("whiteto", o.Get("white", "#FEFEFE")));
            double[] shade = o.Has("shade") ? o.Nums("shade", 5) : null;
            for (int i = 0; i < m.Count; i++)
            {
                if (m.A[i] <= 0f) continue;
                double r = Img.ToSrgb01(m.R[i]) * 255.0, g = Img.ToSrgb01(m.G[i]) * 255.0;
                double t = Math.Clamp((g - gc) / (w[1] - gc), 0, 1);
                double rc = t < 0.97 ? Math.Clamp((r - t * w[0]) / (1 - t), 0, 255) : ramp[ramp.Length - 1].v;
                if (shade != null)
                {
                    // A deeper shade on one side of a point ("--shade cx,cy,angle,amount,rOut"): the
                    // mockup's switch is darker in the end cap beside its knob than ChatGPT drew it.
                    double px = i % m.W + 0.5 - shade[0], py = i / m.W + 0.5 - shade[1];
                    double dist = Math.Sqrt(px * px + py * py);
                    double cs = dist > 1e-9 ? (px * Math.Cos(shade[2] * Math.PI / 180) + py * Math.Sin(shade[2] * Math.PI / 180)) / dist : 0;
                    double fall = Math.Clamp((shade[4] - dist) / 6.0, 0, 1);
                    if (cs > 0) rc = Math.Max(0, rc - shade[3] * cs * cs * fall);
                }
                double[] col = new double[3];
                if (rc <= ramp[0].v) for (int c = 0; c < 3; c++) col[c] = ramp[0].c[c] * (rc / Math.Max(1, ramp[0].v));
                else if (rc >= ramp[ramp.Length - 1].v) for (int c = 0; c < 3; c++) col[c] = ramp[ramp.Length - 1].c[c] + (c == 0 ? rc - ramp[ramp.Length - 1].v : 0);
                else
                {
                    int k = 0;
                    while (k < ramp.Length - 2 && rc > ramp[k + 1].v) k++;
                    double u = (rc - ramp[k].v) / (ramp[k + 1].v - ramp[k].v);
                    for (int c = 0; c < 3; c++) col[c] = ramp[k].c[c] + (ramp[k + 1].c[c] - ramp[k].c[c]) * u;
                }

                m.R[i] = Img.ToLinear((float)Math.Clamp((col[0] * (1 - t) + wt[0] * t) / 255.0, 0, 1));
                m.G[i] = Img.ToLinear((float)Math.Clamp((col[1] * (1 - t) + wt[1] * t) / 255.0, 0, 1));
                m.B[i] = Img.ToLinear((float)Math.Clamp((col[2] * (1 - t) + wt[2] * t) / 255.0, 0, 1));
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  crimson ramp {o.Need("ramp")}");
            return 0;
        }

        /// <summary>Alpha from the distance to a flat ground colour (for grounds that are not magenta), colour unmixed.</summary>
        private static int MatteCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var g = Img.ParseHex(o.Need("ground"));
            float lo = (float)o.Num("lo", 0.03), hi = (float)o.Num("hi", 0.12);
            for (int i = 0; i < m.Count; i++)
            {
                float dr = m.R[i] - g.r, dg = m.G[i] - g.g, db = m.B[i] - g.b;
                float d = MathF.Sqrt(dr * dr + dg * dg + db * db);
                float a = Math.Clamp((d - lo) / Math.Max(1e-5f, hi - lo), 0f, 1f);
                if (a <= 0f) { m.A[i] = 0; m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; continue; }
                if (a < 0.996f)
                {
                    m.R[i] = Math.Clamp((m.R[i] - (1 - a) * g.r) / a, 0f, 1f);
                    m.G[i] = Math.Clamp((m.G[i] - (1 - a) * g.g) / a, 0f, 1f);
                    m.B[i] = Math.Clamp((m.B[i] - (1 - a) * g.b) / a, 0f, 1f);
                }

                m.A[i] *= a;
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H}  ground {o.Need("ground")}");
            return 0;
        }
    }
}
