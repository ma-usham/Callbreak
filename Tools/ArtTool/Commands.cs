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

namespace ArtTool
{
    /// <summary>Small commands for reading a mockup: zoomed crops, pixel grids, colours, bounds and text ink.</summary>
    internal static class Commands
    {
        public static string MockupPath(string repo, string screen)
        {
            if (File.Exists(screen)) return screen;
            string dir = Path.Combine(repo, "Assets", "ArtRefrences", "game-screens");
            string hit = Directory.GetFiles(dir, "*.png").FirstOrDefault(f => Path.GetFileName(f).StartsWith(screen, StringComparison.OrdinalIgnoreCase));
            if (hit == null) throw new FileNotFoundException($"no mockup matches '{screen}' in {dir}");
            return hit;
        }

        public static double[] Rect(string s)
        {
            double[] v = s.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            if (v.Length != 4) throw new ArgumentException("rect must be x,y,w,h");
            return v;
        }

        /// <summary>Enlarged crop, smooth (bicubic) - for looking at an element.</summary>
        public static void Crop(string src, double[] r, double scale, string outPath)
        {
            using var img = new Bitmap(src);
            int ow = (int)Math.Round(r[2] * scale), oh = (int)Math.Round(r[3] * scale);
            using var bmp = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, new RectangleF(0, 0, ow, oh), new RectangleF((float)r[0], (float)r[1], (float)r[2], (float)r[3]), GraphicsUnit.Pixel);
            }

            Save(bmp, outPath);
            Console.WriteLine($"{outPath}  {ow}x{oh}");
        }

        /// <summary>Enlarged crop with a labelled pixel grid in source coordinates - for reading positions.</summary>
        public static void Grid(string src, double[] r, double scale, int minor, int major, string outPath)
        {
            using var img = new Bitmap(src);
            int x0 = (int)r[0], y0 = (int)r[1], w = (int)r[2], h = (int)r[3];
            int ow = (int)(w * scale), oh = (int)(h * scale);
            using var bmp = new Bitmap(ow, oh);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, new Rectangle(0, 0, ow, oh), new Rectangle(x0, y0, w, h), GraphicsUnit.Pixel);
                using var minorPen = new Pen(Color.FromArgb(70, 0, 160, 255), 1);
                using var majorPen = new Pen(Color.FromArgb(200, 0, 90, 255), 1);
                using var font = new Font("Consolas", 9);
                using var brush = new SolidBrush(Color.FromArgb(255, 0, 60, 200));
                using var back = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
                for (int sx = (int)Math.Ceiling(x0 / (double)minor) * minor; sx < x0 + w; sx += minor)
                {
                    int px = (int)((sx - x0) * scale);
                    if (sx % major == 0)
                    {
                        g.DrawLine(majorPen, px, 0, px, oh);
                        g.FillRectangle(back, px + 1, 0, 32, 13);
                        g.DrawString(sx.ToString(CultureInfo.InvariantCulture), font, brush, px + 1, 0);
                    }
                    else g.DrawLine(minorPen, px, 0, px, oh);
                }

                for (int sy = (int)Math.Ceiling(y0 / (double)minor) * minor; sy < y0 + h; sy += minor)
                {
                    int py = (int)((sy - y0) * scale);
                    if (sy % major == 0)
                    {
                        g.DrawLine(majorPen, 0, py, ow, py);
                        g.FillRectangle(back, 0, py + 1, 32, 13);
                        g.DrawString(sy.ToString(CultureInfo.InvariantCulture), font, brush, 0, py + 1);
                    }
                    else g.DrawLine(minorPen, 0, py, ow, py);
                }
            }

            Save(bmp, outPath);
            Console.WriteLine($"{outPath}  {ow}x{oh}");
        }

        /// <summary>Colour statistics of a region: mean, median, darkest and brightest few percent.</summary>
        public static void Probe(Img img, double[] r)
        {
            Box b = Box.Around(r[0], r[1], r[2], r[3], 0).Clamp(img.W, img.H);
            var idx = new List<int>();
            for (int y = b.Y; y < b.Bottom; y++) for (int x = b.X; x < b.Right; x++) idx.Add(y * img.W + x);
            if (idx.Count == 0) { Console.WriteLine("empty region"); return; }
            idx.Sort((a, c) => img.Lum(a).CompareTo(img.Lum(c)));
            string Mean(IEnumerable<int> set)
            {
                double rr = 0, gg = 0, bb = 0; int k = 0;
                foreach (int i in set) { rr += img.R[i]; gg += img.G[i]; bb += img.B[i]; k++; }
                return Img.Hex((float)(rr / k), (float)(gg / k), (float)(bb / k));
            }

            int n = idx.Count, p = Math.Max(1, n / 30);
            Console.WriteLine($"region {b}: mean {Mean(idx)}  median {Mean(idx.Skip(n / 2).Take(1))}  darkest3% {Mean(idx.Take(p))}  brightest3% {Mean(idx.Skip(n - p))}  (n={n})");
        }

        /// <summary>
        /// Bounds of whatever stands out from the background inside a region (the background is
        /// estimated from a ring around the region), with sub-pixel edges and corner radii.
        /// </summary>
        public static void Bounds(Img img, double[] r, float threshold, int ring)
        {
            Box box = Box.Around(r[0], r[1], r[2], r[3], ring).Clamp(img.W, img.H);
            Img m = img.Crop(box.X, box.Y, box.W, box.H);
            var hole = new float[m.Count];
            for (int y = 0; y < box.H; y++)
                for (int x = 0; x < box.W; x++)
                {
                    double px = box.X + x + 0.5, py = box.Y + y + 0.5;
                    hole[y * box.W + x] = px >= r[0] && px < r[0] + r[2] && py >= r[1] && py < r[1] + r[3] ? 1f : 0f;
                }

            Img bg = m.Clone();
            Inpaint.Fill(bg, hole, null, grain: false, relax: 240);
            var cov = new float[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                float dr = m.R[i] - bg.R[i], dg = m.G[i] - bg.G[i], db = m.B[i] - bg.B[i];
                float d = MathF.Sqrt(dr * dr + dg * dg + db * db);
                cov[i] = hole[i] > 0 ? Math.Clamp(d / threshold, 0f, 1f) : 0f;
            }

            var colMax = new float[box.W];
            var rowMax = new float[box.H];
            for (int y = 0; y < box.H; y++)
                for (int x = 0; x < box.W; x++)
                {
                    colMax[x] = Math.Max(colMax[x], cov[y * box.W + x]);
                    rowMax[y] = Math.Max(rowMax[y], cov[y * box.W + x]);
                }

            int x0 = Array.FindIndex(colMax, v => v >= 0.5f), x1 = Array.FindLastIndex(colMax, v => v >= 0.5f);
            int y0 = Array.FindIndex(rowMax, v => v >= 0.5f), y1 = Array.FindLastIndex(rowMax, v => v >= 0.5f);
            if (x0 < 0 || y0 < 0) { Console.WriteLine("nothing stands out from the background here"); return; }
            double left = box.X + x0, right = box.X + x1 + 1, top = box.Y + y0, bottom = box.Y + y1 + 1;
            Console.WriteLine(FormattableString.Invariant($"bounds x {left:0.#} y {top:0.#} w {right - left:0.#} h {bottom - top:0.#}   (centre {(left + right) / 2:0.#},{(top + bottom) / 2:0.#})"));

            // Corner radius: along each corner's diagonal, the inset where the shape starts.
            double Corner(int sx, int sy, int dx, int dy)
            {
                for (int t = 0; t < Math.Min(right - left, bottom - top) / 2; t++)
                {
                    int x = sx + dx * t - box.X, y = sy + dy * t - box.Y;
                    if (x < 0 || y < 0 || x >= box.W || y >= box.H) break;
                    if (cov[y * box.W + x] >= 0.5f) return t / 0.2929;
                }

                return double.NaN;
            }

            double tl = Corner((int)left, (int)top, 1, 1), tr = Corner((int)right - 1, (int)top, -1, 1);
            double bl = Corner((int)left, (int)bottom - 1, 1, -1), br = Corner((int)right - 1, (int)bottom - 1, -1, -1);
            Console.WriteLine(FormattableString.Invariant($"corner radius estimate  TL {tl:0.#}  TR {tr:0.#}  BL {bl:0.#}  BR {br:0.#}"));
        }

        /// <summary>
        /// Brightness along a row or a column, with the strongest edges located to a tenth of a
        /// pixel (parabola through the gradient peak) - for reading exact element edges.
        /// </summary>
        public static void Profile(Img img, bool isRow, int line, int from, int to, int edges)
        {
            var v = new List<(int p, double l)>();
            for (int p = Math.Max(0, from); p <= Math.Min(isRow ? img.W - 1 : img.H - 1, to); p++)
            {
                int i = isRow ? line * img.W + p : p * img.W + line;
                v.Add((p, Img.ToSrgb01(img.Lum(i)) * 255));
            }

            Console.WriteLine(string.Join(" ", v.Select(t => $"{t.p}:{t.l:0}")));
            var grads = new List<(double pos, double g)>();
            for (int k = 1; k < v.Count - 1; k++)
            {
                double g0 = v[k].l - v[k - 1].l, g1 = v[k + 1].l - v[k].l;
                double gPrev = k >= 2 ? v[k - 1].l - v[k - 2].l : g0;
                if (Math.Abs(g0) >= Math.Abs(gPrev) && Math.Abs(g0) >= Math.Abs(g1) && Math.Abs(g0) > 2)
                {
                    double denom = gPrev - 2 * g0 + g1;
                    double off = Math.Abs(denom) > 1e-9 ? 0.5 * (gPrev - g1) / denom : 0;
                    grads.Add((v[k].p + Math.Clamp(off, -0.5, 0.5), g0));
                }
            }

            foreach (var e in grads.OrderByDescending(e => Math.Abs(e.g)).Take(edges).OrderBy(e => e.pos))
            {
                Console.WriteLine(FormattableString.Invariant($"edge at {e.pos:0.0} ({(e.g > 0 ? "brighter" : "darker")} by {Math.Abs(e.g):0})"));
            }
        }

        /// <summary>
        /// Refines a rough rectangle to the element's exact edges (the strongest brightness step
        /// near each side, averaged over the middle of that side) and fits its corner radius from
        /// how the edge curves away near the top-left and bottom-right corners.
        /// </summary>
        public static void FitRect(Img img, double[] r, int search)
        {
            double Lum(int x, int y) => x < 0 || y < 0 || x >= img.W || y >= img.H ? 0 : Img.ToSrgb01(img.Lum(y * img.W + x)) * 255;

            // Sub-pixel position of the strongest step along a line, near 'around'.
            double Step(bool horizontal, int line, double around, int reach, out double strength)
            {
                int a = (int)Math.Floor(around - reach), b = (int)Math.Ceiling(around + reach);
                double best = 0, pos = around;
                for (int p = a + 1; p <= b; p++)
                {
                    double g = horizontal ? Lum(p, line) - Lum(p - 1, line) : Lum(line, p) - Lum(line, p - 1);
                    if (Math.Abs(g) <= Math.Abs(best)) continue;
                    double g0 = horizontal ? Lum(p - 1, line) - Lum(p - 2, line) : Lum(line, p - 1) - Lum(line, p - 2);
                    double g1 = horizontal ? Lum(p + 1, line) - Lum(p, line) : Lum(line, p + 1) - Lum(line, p);
                    double den = g0 - 2 * g + g1;
                    double off = Math.Abs(den) > 1e-9 ? Math.Clamp(0.5 * (g0 - g1) / den, -0.5, 0.5) : 0;
                    best = g;
                    pos = p + off;          // the boundary between pixel p-1 and p is at coordinate p
                }

                strength = best;
                return pos;
            }

            double Side(bool vertical, double at, double from, double to)
            {
                var hits = new List<double>();
                for (int k = 0; k < 9; k++)
                {
                    int line = (int)Math.Round(from + (to - from) * (0.3 + 0.05 * k));
                    hits.Add(Step(vertical, line, at, search, out _));
                }

                hits.Sort();
                return hits[hits.Count / 2];
            }

            double left = Side(true, r[0], r[1], r[1] + r[3]);
            double right = Side(true, r[0] + r[2], r[1], r[1] + r[3]);
            double top = Side(false, r[1], r[0], r[0] + r[2]);
            double bottom = Side(false, r[1] + r[3], r[0], r[0] + r[2]);
            double w = right - left, h = bottom - top;

            // Corner radius: edge offsets on rows just inside the top (and bottom) edge.
            var samples = new List<(double dy, double off)>();
            double maxR = Math.Min(w, h) / 2;
            for (int dy = 1; dy < Math.Min(maxR, 70); dy++)
            {
                double y = top + dy + 0.5;
                double xl = Step(true, (int)Math.Floor(y), left + dy * 0.6, (int)Math.Max(search, dy * 1.2 + 4), out double s1);
                if (Math.Abs(s1) > 3 && xl - left > -1) samples.Add((dy + 0.5, xl - left));
                double yb = bottom - dy - 0.5;
                double xr = Step(true, (int)Math.Floor(yb), right - dy * 0.6, (int)Math.Max(search, dy * 1.2 + 4), out double s2);
                if (Math.Abs(s2) > 3 && right - xr > -1) samples.Add((dy + 0.5, right - xr));
            }

            double bestR = 0, bestE = double.MaxValue;
            for (double rr = 0; rr <= maxR; rr += 0.5)
            {
                double e = 0; int k = 0;
                foreach (var s in samples)
                {
                    if (s.dy > Math.Max(rr, 6)) continue;
                    double model = s.dy >= rr ? 0 : rr - Math.Sqrt(Math.Max(0, 2 * rr * s.dy - s.dy * s.dy));
                    e += (s.off - model) * (s.off - model); k++;
                }

                if (k == 0) continue;
                e /= k;
                if (e < bestE) { bestE = e; bestR = rr; }
            }

            string shape = Math.Abs(bestR - h / 2) < 1.5 ? "pill" : bestR < 1 ? "rect" : "roundrect";
            Console.WriteLine(FormattableString.Invariant(
                $"rect [{left:0.0}, {top:0.0}, {w:0.0}, {h:0.0}]  radius {bestR:0.0} (fit rms {Math.Sqrt(bestE):0.00} px)  shape {shape}   centre {left + w / 2:0.0},{top + h / 2:0.0}"));
        }

        /// <summary>
        /// Compares a Unity capture with the mockup: whole-screen and per-element error, and every
        /// label's ink box measured the same way in both images.
        /// </summary>
        public static int Compare(string repo, string mockupPath, string capturePath, string layoutPath, string outDir)
        {
            Img mock = Img.Load(mockupPath);
            Img cap = Img.Load(capturePath);
            if (cap.W != mock.W || cap.H != mock.H)
            {
                // Captures at other aspect ratios are compared on their central 16:9 design area.
                double scale = Math.Min(cap.W / 1920.0, cap.H / 1080.0);
                int dw = (int)Math.Round(1920 * scale), dh = (int)Math.Round(1080 * scale);
                cap = cap.Crop((cap.W - dw) / 2, (cap.H - dh) / 2, dw, dh).Resize(mock.W, mock.H);
            }

            Directory.CreateDirectory(outDir);
            var diff = new Img(mock.W, mock.H);
            var blend = new Img(mock.W, mock.H);
            double total = 0;
            for (int i = 0; i < mock.Count; i++)
            {
                float d = (Math.Abs(Img.ToSrgb01(cap.R[i]) - Img.ToSrgb01(mock.R[i])) + Math.Abs(Img.ToSrgb01(cap.G[i]) - Img.ToSrgb01(mock.G[i])) + Math.Abs(Img.ToSrgb01(cap.B[i]) - Img.ToSrgb01(mock.B[i]))) / 3f;
                total += d;
                float v = Img.ToLinear(Math.Min(1f, d * 6f));
                diff.R[i] = v; diff.G[i] = v; diff.B[i] = v;
                blend.R[i] = (cap.R[i] + mock.R[i]) / 2; blend.G[i] = (cap.G[i] + mock.G[i]) / 2; blend.B[i] = (cap.B[i] + mock.B[i]) / 2;
            }

            diff.Save(Path.Combine(outDir, "capture-diff-x6.png"));
            blend.Save(Path.Combine(outDir, "capture-blend.png"));

            // Left half mockup, right half capture: seams show every misplaced edge.
            var split = mock.Clone();
            for (int y = 0; y < mock.H; y++)
                for (int x = mock.W / 2; x < mock.W; x++)
                {
                    int i = y * mock.W + x;
                    split.R[i] = cap.R[i]; split.G[i] = cap.G[i]; split.B[i] = cap.B[i];
                }
            split.Save(Path.Combine(outDir, "capture-split.png"));
            var splitV = mock.Clone();
            for (int y = mock.H / 2; y < mock.H; y++)
                for (int x = 0; x < mock.W; x++)
                {
                    int i = y * mock.W + x;
                    splitV.R[i] = cap.R[i]; splitV.G[i] = cap.G[i]; splitV.B[i] = cap.B[i];
                }
            splitV.Save(Path.Combine(outDir, "capture-split-v.png"));
            File.Copy(capturePath, Path.Combine(outDir, "capture.png"), true);
            var sb = new StringBuilder();
            sb.AppendLine(FormattableString.Invariant($"capture vs mockup, mean abs error (0-255): {total / mock.Count * 255:0.00}"));
            if (layoutPath != null && File.Exists(layoutPath))
            {
                JsonObject layout = JsonNode.Parse(File.ReadAllText(layoutPath)).AsObject();
                foreach (JsonNode jn in layout["nodes"].AsArray())
                {
                    JsonObject o = jn.AsObject();
                    if (o["rect"] is not JsonArray ra) continue;
                    double[] r = ra.Select(v => (double)v).ToArray();
                    string id = o["id"].ToString(), kind = o["kind"]?.ToString() ?? "sprite";
                    if (kind == "group") continue;
                    Box b = Box.Around(r[0], r[1], r[2], r[3], 2).Clamp(mock.W, mock.H);
                    double e = 0; int k = 0;
                    for (int y = b.Y; y < b.Bottom; y++)
                        for (int x = b.X; x < b.Right; x++)
                        {
                            int i = y * mock.W + x;
                            e += (Math.Abs(Img.ToSrgb01(cap.R[i]) - Img.ToSrgb01(mock.R[i])) + Math.Abs(Img.ToSrgb01(cap.G[i]) - Img.ToSrgb01(mock.G[i])) + Math.Abs(Img.ToSrgb01(cap.B[i]) - Img.ToSrgb01(mock.B[i]))) / 3.0;
                            k++;
                        }

                    string line = FormattableString.Invariant($"  {id,-28} {kind,-8} error {(k > 0 ? e / k * 255 : 0),6:0.00}");
                    if (kind == "label")
                    {
                        bool dark = (o["ink"]?.ToString() ?? "dark") != "light";
                        double[] a = InkMeasure.Box(mock, r, dark), c = InkMeasure.Box(cap, r, dark);
                        if (a != null && c != null)
                        {
                            line += FormattableString.Invariant($"   dx {c[0] - a[0],5:0.0} dy {c[1] - a[1],5:0.0}  w {c[2] - c[0],6:0.0}/{a[2] - a[0],-6:0.0} h {c[3] - c[1],5:0.0}/{a[3] - a[1],-5:0.0}");
                        }
                        else line += c == null ? "   NO INK IN CAPTURE" : "   no ink in mockup";
                    }

                    sb.AppendLine(line);
                }
            }

            File.WriteAllText(Path.Combine(outDir, "capture-report.txt"), sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }

        public static void Save(Bitmap bmp, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            bmp.Save(path, ImageFormat.Png);
        }
    }

    /// <summary>The ink box of the text inside a rectangle, measured the same way on any image.</summary>
    internal static class InkMeasure
    {
        public static double[] Box(Img img, double[] r, bool dark)
        {
            const int ring = 4;
            Box box = ArtTool.Box.Around(r[0], r[1], r[2], r[3], ring).Clamp(img.W, img.H);
            Img m = img.Crop(box.X, box.Y, box.W, box.H);
            var hole = new float[m.Count];
            for (int y = 0; y < box.H; y++)
                for (int x = 0; x < box.W; x++)
                {
                    double px = box.X + x + 0.5, py = box.Y + y + 0.5;
                    hole[y * box.W + x] = px >= r[0] && px < r[0] + r[2] && py >= r[1] && py < r[1] + r[3] ? 1f : 0f;
                }

            Img bg = m.Clone();
            Inpaint.Fill(bg, hole, null, grain: false, relax: 240);
            var ink = Matte.InkColor(m, bg, dark);
            float[] c = Matte.InkCoverage(m, bg, ink);
            for (int i = 0; i < c.Length; i++) c[i] *= hole[i];
            var colMax = new float[box.W];
            var rowMax = new float[box.H];
            for (int y = 0; y < box.H; y++)
                for (int x = 0; x < box.W; x++)
                {
                    colMax[x] = Math.Max(colMax[x], c[y * box.W + x]);
                    rowMax[y] = Math.Max(rowMax[y], c[y * box.W + x]);
                }

            const float on = 0.15f;
            int x0 = Array.FindIndex(colMax, v => v >= on), x1 = Array.FindLastIndex(colMax, v => v >= on);
            int y0 = Array.FindIndex(rowMax, v => v >= on), y1 = Array.FindLastIndex(rowMax, v => v >= on);
            if (x0 < 0 || y0 < 0) return null;
            return new[] { box.X + x0 + (1 - colMax[x0]), box.Y + y0 + (1 - rowMax[y0]), box.X + x1 + (double)colMax[x1], box.Y + y1 + (double)rowMax[y1] };
        }
    }
}
