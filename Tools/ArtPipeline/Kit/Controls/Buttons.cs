using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ArtTool;

namespace KitControls
{
    /// <summary>
    /// The commands written for the crimson call-to-action buttons of the inner screens (the plain
    /// pill, the Game Menu's box), the soft shadow under them and the chevron on them: measuring
    /// them on the mockups, and bringing ChatGPT's drawing onto what was measured.
    /// </summary>
    internal static partial class Program
    {
        private const string Usage3 = @"kitcontrols, button commands
  edges   --screen 06 --rect x,y,w,h                 exact edges and corner radius of a dark button on a light ground
  motif   (--screen 06 | --in file) --rect x,y,w,h --plain x0,x1 [--thr 5]
                                                     how far the ornaments at the two ends reach, and how strong they are
  chev    (--screen 06 --rect x,y,w,h | --in sprite) ink box, arm angle and stroke weight of a light chevron
  bshadow --screen 06 --rect x,y,w,h --radius r [--exclude ""x,y,w,h;..""] [--color #2A1A12] [--band 1,16] [--far 20,28]
          [--shadow file --face W,H [--nine L,T,R,B] [--drop d --opacity a]]
                                                     blur, drop and opacity of the soft shadow round a button (and how a given shadow sprite does there)
  mkshadow --size W,H --radius r --sigma s --spread p [--color #hex] [--hollow] --out f
                                                     a soft shadow sprite for a face of that size (the face's box sits p px inside it)
  retone  --in a --out b --plain x0,x1 (--stops ""0:#hex;..;1:#hex"" | --screen 06 --col x0,x1 --rows top,height [--rowclamp a,b])
          [--gain 1,1,1] [--radius r --edge e [--edge0 d]]
          [--from lo,hi --light r,g,b --dark r,g,b [--core c] [--ground g --close r --soft s]]
                                                     put a button's top-to-bottom shading on the mockup's, keeping its ornaments
                                                     (--from..: the ornaments in the mockup's own lighter and darker colours, on the darker ground the mockup gives the cluster)
  ring    --in a --out b --inset d --width w --radius r --stops ""0:#hex;..;1:#hex""
                                                     a band along a rounded outline (a dark edge, a keyline), coloured top to bottom
  vprof   (--screen 06 | --in file) --col x0,x1 --rows top,height [--at ""0.07,0.25,0.5,0.75,0.93""]
                                                     the plain colour of a button at chosen heights (fractions of its height)";

        private static int ButtonsCommand(string cmd, Opts o)
        {
            switch (cmd)
            {
                case "help3": Console.WriteLine(Usage3); return 0;
                case "edges": return Edges(o);
                case "motif": return MotifCmd(o);
                case "chev": return Chevron(o);
                case "bshadow": return ButtonShadow(o);
                case "mkshadow": return MakeShadow(o);
                case "retone": return Retone(o);
                case "ring": return Ring(o);
                case "gshadow": return GlyphShadow(o);
                case "vprof": return VProfile(o);
                default:
                    Console.Error.WriteLine($"unknown command '{cmd}'\n{Usage}\n\n{Usage2}\n\n{Usage3}");
                    return 2;
            }
        }

        private static double SLum(Img m, int x, int y)
        {
            x = Math.Clamp(x, 0, m.W - 1); y = Math.Clamp(y, 0, m.H - 1);
            int i = y * m.W + x;
            return 0.2126 * Img.ToSrgb01(m.R[i]) + 0.7152 * Img.ToSrgb01(m.G[i]) + 0.0722 * Img.ToSrgb01(m.B[i]);
        }

        /// <summary>
        /// Where a line of samples that starts on the light ground crosses into the dark element:
        /// the place where the brightness is midway between the two, to a fraction of a pixel.
        /// </summary>
        private static double Crossing(Func<int, double> lum, int from, int to)
        {
            int step = to > from ? 1 : -1;
            double g = (lum(from) + lum(from + step) + lum(from + 2 * step)) / 3;
            int k = from;
            while (k != to && lum(k) > g * 0.55) k += step;
            if (k == to) return double.NaN;
            var ins = new List<double>();
            for (int q = 4; q < 12; q++) ins.Add(lum(k + q * step));
            ins.Sort();
            double li = ins[ins.Count / 2], thr = (g + li) / 2;
            int a = from;
            for (int q = from; q != k + step; q += step) if (lum(q) >= thr) a = q;
            int b = a + step;
            double la = lum(a), lb = lum(b);
            double t = la - lb > 1e-9 ? (la - thr) / (la - lb) : 0.5;
            return a + 0.5 + step * t;
        }

        // ------------------------------------------------------------------------------------
        // edges: the box and the corner radius of a dark button on a light ground
        private static int Edges(Opts o)
        {
            Img m = Img.Load(MockupPath(o.Need("screen")));
            double[] r = o.Nums("rect", 4);
            int x0 = (int)Math.Floor(r[0]), x1 = (int)Math.Ceiling(r[0] + r[2]), y0 = (int)Math.Floor(r[1]), y1 = (int)Math.Ceiling(r[1] + r[3]);
            int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            const int outPx = 9;
            var left = new Dictionary<int, double>(); var right = new Dictionary<int, double>();
            var top = new Dictionary<int, double>(); var bottom = new Dictionary<int, double>();
            for (int y = y0 + 1; y < y1 - 1; y++)
            {
                int yy = y;
                double a = Crossing(x => SLum(m, x, yy), x0 - outPx, cx), b = Crossing(x => SLum(m, x, yy), x1 + outPx, cx);
                if (!double.IsNaN(a)) left[y] = a;
                if (!double.IsNaN(b)) right[y] = b;
            }

            for (int x = x0 + 1; x < x1 - 1; x++)
            {
                int xx = x;
                double a = Crossing(y => SLum(m, xx, y), y0 - outPx, cy), b = Crossing(y => SLum(m, xx, y), y1 + outPx, cy);
                if (!double.IsNaN(a)) top[x] = a;
                if (!double.IsNaN(b)) bottom[x] = b;
            }

            double Med(IEnumerable<double> v) { var l = v.OrderBy(q => q).ToList(); return l.Count == 0 ? double.NaN : l[l.Count / 2]; }
            double h = r[3], w = r[2];
            double L = Med(left.Where(p => Math.Abs(p.Key + 0.5 - (r[1] + h / 2)) < h * 0.08).Select(p => p.Value));
            double R = Med(right.Where(p => Math.Abs(p.Key + 0.5 - (r[1] + h / 2)) < h * 0.08).Select(p => p.Value));
            bool Side(double x) { double f = (x + 0.5 - r[0]) / w; return (f > 0.22 && f < 0.40) || (f > 0.60 && f < 0.78); }
            double T = Med(top.Where(p => Side(p.Key)).Select(p => p.Value));
            double B = Med(bottom.Where(p => Side(p.Key)).Select(p => p.Value));
            Console.WriteLine(FormattableString.Invariant($"edges: left {L:0.00}  top {T:0.00}  right {R:0.00}  bottom {B:0.00}   box [{L:0.00}, {T:0.00}, {R - L:0.00}, {B - T:0.00}]"));

            // Corner points: a scan is kept only where it crosses the outline squarely enough.
            var pts = new List<(double x, double y)>[4];
            for (int c = 0; c < 4; c++) pts[c] = new List<(double, double)>();
            double reach = (B - T) * 0.5;
            void AddRows(Dictionary<int, double> d, bool isLeft)
            {
                foreach (var p in d)
                {
                    if (!d.TryGetValue(p.Key - 1, out double pa) || !d.TryGetValue(p.Key + 1, out double pb)) continue;
                    if (Math.Abs(pb - pa) / 2 > 1.2) continue;
                    double y = p.Key + 0.5;
                    bool upper = y < T + reach;
                    if (!upper && y <= B - reach) continue;
                    pts[(isLeft ? 0 : 1) + (upper ? 0 : 2)].Add((p.Value, y));
                }
            }

            void AddCols(Dictionary<int, double> d, bool isTop)
            {
                foreach (var p in d)
                {
                    if (!d.TryGetValue(p.Key - 1, out double pa) || !d.TryGetValue(p.Key + 1, out double pb)) continue;
                    if (Math.Abs(pb - pa) / 2 > 1.2) continue;
                    double x = p.Key + 0.5;
                    bool lft = x < L + reach;
                    if (!lft && x <= R - reach) continue;
                    pts[(lft ? 0 : 1) + (isTop ? 0 : 2)].Add((x, p.Value));
                }
            }

            AddRows(left, true); AddRows(right, false); AddCols(top, true); AddCols(bottom, false);
            string[] names = { "top-left", "top-right", "bottom-left", "bottom-right" };
            double Rms(List<(double x, double y)> p, double rad)
            {
                var s = new Shape("roundrect", L, T, R - L, B - T, rad);
                double e = 0;
                foreach (var q in p) { double d = s.Sdf(q.x, q.y); e += d * d; }
                return Math.Sqrt(e / Math.Max(1, p.Count));
            }

            var all = new List<(double x, double y)>();
            for (int c = 0; c < 4; c++)
            {
                all.AddRange(pts[c]);
                double best = double.MaxValue, bestR = 0;
                for (double rad = 1; rad <= reach + 1e-9; rad += 0.1) { double e = Rms(pts[c], rad); if (e < best) { best = e; bestR = rad; } }
                Console.WriteLine(FormattableString.Invariant($"  {names[c],-13} radius {bestR:0.0}  (rms {best:0.00} px over {pts[c].Count} points)"));
            }

            double bestAll = double.MaxValue, bestRAll = 0;
            for (double rad = 1; rad <= reach + 1e-9; rad += 0.1) { double e = Rms(all, rad); if (e < bestAll) { bestAll = e; bestRAll = rad; } }
            Console.WriteLine(FormattableString.Invariant($"  all four      radius {bestRAll:0.0}  (rms {bestAll:0.00} px)   radius / height {bestRAll / (B - T):0.000}   a full pill would be {(B - T) / 2:0.0} (rms {Rms(all, (B - T) / 2):0.00})"));
            if (o.Has("at"))
            {
                foreach (double rad in o.Nums("at", 0)) Console.WriteLine(FormattableString.Invariant($"  at radius {rad:0.0}: rms {Rms(all, rad):0.00} px"));
            }

            return 0;
        }

        // ------------------------------------------------------------------------------------
        // motif: the woven ornaments at the two ends of a button
        private static int MotifCmd(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            double[] r = o.Nums("rect", 4, new double[] { 0, 0, m.W, m.H });
            int[] plain = o.Ints("plain", 2);
            double thr = o.Num("thr", 5);
            int yA = (int)Math.Round(r[1] + r[3] * 0.25), yB = (int)Math.Round(r[1] + r[3] * 0.75);
            int rows = yB - yA;
            var baseR = new double[rows]; var baseG = new double[rows]; var baseB = new double[rows];
            for (int j = 0; j < rows; j++)
            {
                var vr = new List<double>(); var vg = new List<double>(); var vb = new List<double>();
                for (int x = plain[0]; x < plain[1]; x++)
                {
                    int i = (yA + j) * m.W + x;
                    vr.Add(m.R[i]); vg.Add(m.G[i]); vb.Add(m.B[i]);
                }

                vr.Sort(); vg.Sort(); vb.Sort();
                baseR[j] = vr[vr.Count / 2]; baseG[j] = vg[vg.Count / 2]; baseB[j] = vb[vb.Count / 2];
            }

            int xs = (int)Math.Floor(r[0]), xe = (int)Math.Ceiling(r[0] + r[2]);
            var dev = new double[xe - xs];
            for (int x = xs; x < xe; x++)
            {
                double e = 0;
                for (int j = 0; j < rows; j++)
                {
                    int i = (yA + j) * m.W + x;
                    double dr = (Img.ToSrgb01(m.R[i]) - Img.ToSrgb01((float)baseR[j])) * 255, dg = (Img.ToSrgb01(m.G[i]) - Img.ToSrgb01((float)baseG[j])) * 255, db = (Img.ToSrgb01(m.B[i]) - Img.ToSrgb01((float)baseB[j])) * 255;
                    e += (dr * dr + dg * dg + db * db) / 3;
                }

                dev[x - xs] = Math.Sqrt(e / rows);
            }

            // The ornament ends where the columns stay plain for 8 px.
            int End(bool fromLeft)
            {
                int n = dev.Length, last = -1, quiet = 0;
                int startSkip = (int)Math.Round(r[3] * 0.12);           // the first columns are the rounded end itself
                for (int q = startSkip; q < n / 2; q++)
                {
                    int idx = fromLeft ? q : n - 1 - q;
                    if (dev[idx] > thr) { last = q; quiet = 0; }
                    else if (++quiet >= 8 && last >= 0) break;
                }

                return last;
            }

            int le = End(true), re = End(false);
            Console.WriteLine(FormattableString.Invariant($"ornaments (columns that differ from the plain colour by more than {thr:0.#} levels, rows {yA}..{yB}): left reaches {le + 1} px from the left edge ({(le + 1) / r[3]:0.00} of the height), right {re + 1} px from the right edge ({(re + 1) / r[3]:0.00})"));
            var sb = new StringBuilder("  deviation every 4 px, left end:  ");
            for (int q = 0; q < Math.Min(dev.Length, 112); q += 4) sb.Append(FormattableString.Invariant($"{dev[q]:0} "));
            sb.Append("\n  deviation every 4 px, right end: ");
            for (int q = 0; q < Math.Min(dev.Length, 112); q += 4) sb.Append(FormattableString.Invariant($"{dev[dev.Length - 1 - q]:0} "));
            Console.WriteLine(sb.ToString());

            // How strong: the ratio of each ornament pixel to its row's plain colour.
            void Stats(string name, int a, int b)
            {
                var lr = new List<double>(); var lg = new List<double>(); var lb = new List<double>();
                for (int x = a; x < b; x++)
                {
                    for (int j = 0; j < rows; j++)
                    {
                        int i = (yA + j) * m.W + x;
                        if (m.A[i] < 0.999f) continue;
                        lr.Add(Math.Log(Math.Max(1e-4, m.R[i]) / Math.Max(1e-4, baseR[j]), 2));
                        lg.Add(Math.Log(Math.Max(1e-4, m.G[i]) / Math.Max(1e-4, baseG[j]), 2));
                        lb.Add(Math.Log(Math.Max(1e-4, m.B[i]) / Math.Max(1e-4, baseB[j]), 2));
                    }
                }

                string P(List<double> v) { v.Sort(); double q(double f) => v.Count == 0 ? 0 : v[(int)Math.Clamp(f * (v.Count - 1), 0, v.Count - 1)]; return FormattableString.Invariant($"{q(0.05):+0.00;-0.00} {q(0.25):+0.00;-0.00} {q(0.5):+0.00;-0.00} {q(0.75):+0.00;-0.00} {q(0.95):+0.00;-0.00}"); }
                Console.WriteLine($"  {name}: log2(pixel / plain) at 5/25/50/75/95%:  R {P(lr)}   G {P(lg)}   B {P(lb)}");
            }

            int skip = (int)Math.Round(r[3] * 0.12);
            if (le > skip) Stats("left ornament ", xs + skip, xs + le + 1);
            if (re > skip) Stats("right ornament", xe - re - 1, xe - skip);
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // vprof: the plain colour at chosen heights
        private static int VProfile(Opts o)
        {
            Img m = o.Has("in") ? Img.Load(o.Get("in")) : Img.Load(MockupPath(o.Need("screen")));
            int[] col = o.Ints("col", 2);
            double[] rw = o.Nums("rows", 2);
            double[] at = o.Nums("at", 0, new[] { 0.07, 0.25, 0.5, 0.75, 0.93 });
            var sb = new StringBuilder();
            foreach (double f in at)
            {
                double y = rw[0] + rw[1] * f;
                double[] c = PlainAt(m, col[0], col[1], y);
                sb.Append(FormattableString.Invariant($"{f:0.###}:{Img.Hex((float)c[0], (float)c[1], (float)c[2])} "));
            }

            Console.WriteLine(sb.ToString().TrimEnd());
            return 0;
        }

        /// <summary>The median colour (linear) of columns x0..x1 at a fractional row: between the two nearest row centres.</summary>
        private static double[] PlainAt(Img m, int x0, int x1, double y)
        {
            double sy = y - 0.5;
            int ya = (int)Math.Floor(sy);
            double f = sy - ya;
            double[] Row(int yy)
            {
                yy = Math.Clamp(yy, 0, m.H - 1);
                var vr = new List<double>(); var vg = new List<double>(); var vb = new List<double>();
                for (int x = x0; x < x1; x++) { int i = yy * m.W + x; vr.Add(m.R[i]); vg.Add(m.G[i]); vb.Add(m.B[i]); }
                vr.Sort(); vg.Sort(); vb.Sort();
                return new[] { vr[vr.Count / 2], vg[vg.Count / 2], vb[vb.Count / 2] };
            }

            double[] a = Row(ya), b = Row(ya + 1);
            return new[] { a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f };
        }

        // ------------------------------------------------------------------------------------
        // chev: a light chevron on a dark ground (mockup) or a sprite with alpha
        private static int Chevron(Opts o)
        {
            float[] al; int W, H; double ox = 0, oy = 0;
            if (o.Has("in"))
            {
                Img s = Img.Load(o.Get("in"));
                W = s.W; H = s.H; al = (float[])s.A.Clone();
            }
            else
            {
                Img m = Img.Load(MockupPath(o.Need("screen")));
                double[] r = o.Nums("rect", 4);
                int x0 = (int)Math.Floor(r[0]), y0 = (int)Math.Floor(r[1]);
                W = (int)Math.Ceiling(r[0] + r[2]) - x0; H = (int)Math.Ceiling(r[1] + r[3]) - y0;
                ox = x0; oy = y0;
                var lum = new double[W * H];
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) lum[y * W + x] = SLum(m, x0 + x, y0 + y);
                var border = new List<double>();
                for (int x = 0; x < W; x++) { border.Add(lum[x]); border.Add(lum[(H - 1) * W + x]); }
                for (int y = 0; y < H; y++) { border.Add(lum[y * W]); border.Add(lum[y * W + W - 1]); }
                border.Sort();
                double g = border[border.Count / 2], wv = lum.Max();
                al = new float[W * H];
                for (int i = 0; i < al.Length; i++) al[i] = (float)Math.Clamp((lum[i] - g) / Math.Max(1e-6, wv - g), 0, 1);
                // What is left of the ground's own grain is not ink.
                for (int i = 0; i < al.Length; i++) al[i] = al[i] < 0.12f ? 0f : al[i];
            }

            // Ink box (coverage 0.5 crossings of the row / column maxima).
            var colMax = new double[W]; var rowMax = new double[H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { double a = al[y * W + x]; if (a > colMax[x]) colMax[x] = a; if (a > rowMax[y]) rowMax[y] = a; }
            (double lo, double hi) Span(double[] v)
            {
                int a = Array.FindIndex(v, q => q >= 0.5), b = Array.FindLastIndex(v, q => q >= 0.5);
                if (a < 0) return (0, 0);
                double lo = a > 0 ? a - (v[a] - 0.5) / Math.Max(1e-6, v[a] - v[a - 1]) + 0.5 : a;
                double hi = b < v.Length - 1 ? b + 0.5 + (v[b] - 0.5) / Math.Max(1e-6, v[b] - v[b + 1]) : b + 1;
                return (lo, hi);
            }

            (double xl, double xr) = Span(colMax); (double yt, double yb) = Span(rowMax);
            double iw = xr - xl, ih = yb - yt;
            double area = al.Sum(a => (double)a);
            (double angle, double thick, int n) Arm(double f0, double f1)
            {
                var ys = new List<double>(); var cs = new List<double>(); var ws = new List<double>();
                for (int y = 0; y < H; y++)
                {
                    double yc = y + 0.5;
                    if (yc < yt + ih * f0 || yc > yt + ih * f1) continue;
                    double sw = 0, sxw = 0;
                    for (int x = 0; x < W; x++) { double a = al[y * W + x]; sw += a; sxw += a * (x + 0.5); }
                    if (sw < 0.3) continue;
                    ys.Add(yc); cs.Add(sxw / sw); ws.Add(sw);
                }

                if (ys.Count < 2) return (0, 0, ys.Count);
                double my = ys.Average(), mc = cs.Average(), num = 0, den = 0;
                for (int i = 0; i < ys.Count; i++) { num += (ys[i] - my) * (cs[i] - mc); den += (ys[i] - my) * (ys[i] - my); }
                double slope = num / den;                                   // dx per dy
                double ang = Math.Atan2(1, Math.Abs(slope));                // from the horizontal
                return (ang * 180 / Math.PI, ws.Average() * Math.Sin(ang), ys.Count);
            }

            var up = Arm(0.18, 0.40); var dn = Arm(0.60, 0.82);
            double t = (up.thick + dn.thick) / 2;
            Console.WriteLine(FormattableString.Invariant($"chevron: ink box [{ox + xl:0.00}, {oy + yt:0.00}, {iw:0.00}, {ih:0.00}]  (width / height {iw / ih:0.000})  ink area {area:0.0} px2"));
            Console.WriteLine(FormattableString.Invariant($"  arms: upper {up.angle:0.0} deg from the horizontal, stroke {up.thick:0.00} px; lower {dn.angle:0.0} deg, stroke {dn.thick:0.00} px   stroke / height {t / ih:0.000}   opening {up.angle + dn.angle:0.0} deg"));
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // Soft shadows
        private static float[] GaussBlur(float[] src, int w, int h, double sigma)
        {
            if (sigma < 0.05) return (float[])src.Clone();
            int rad = (int)Math.Ceiling(sigma * 3.2);
            var k = new double[2 * rad + 1];
            double sum = 0;
            for (int i = -rad; i <= rad; i++) { k[i + rad] = Math.Exp(-i * i / (2 * sigma * sigma)); sum += k[i + rad]; }
            for (int i = 0; i < k.Length; i++) k[i] /= sum;
            var tmp = new float[src.Length]; var dst = new float[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double a = 0;
                    for (int i = -rad; i <= rad; i++) { int xx = Math.Clamp(x + i, 0, w - 1); a += src[y * w + xx] * k[i + rad]; }
                    tmp[y * w + x] = (float)a;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double a = 0;
                    for (int i = -rad; i <= rad; i++) { int yy = Math.Clamp(y + i, 0, h - 1); a += tmp[yy * w + x] * k[i + rad]; }
                    dst[y * w + x] = (float)a;
                }
            }

            return dst;
        }

        /// <summary>
        /// The soft shadow round a button on a mockup, as the game would draw it: the button's
        /// shape blurred (sigma), moved down (drop) and laid on the ground at an opacity. Fitted
        /// to the mockup's pixels in a band just outside the button; the ground colour is read
        /// further out. With --shadow, how an existing shadow sprite does in the same place,
        /// scaled as the screen builder scales it (like the face, by height).
        /// </summary>
        private static int ButtonShadow(Opts o)
        {
            Img mock = Img.Load(MockupPath(o.Need("screen")));
            double[] r = o.Nums("rect", 4);
            double radius = o.Num("radius", r[3] / 2);
            double[] band = o.Nums("band", 2, new[] { 1.0, 16.0 });
            double[] far = o.Nums("far", 2, new[] { 20.0, 28.0 });
            var col = Img.ParseHex(o.Get("color", "#2A1A12"));
            double[] C = { col.r, col.g, col.b };
            var excl = o.Has("exclude")
                ? o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(q => q.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList()
                : new List<double[]>();
            bool Excluded(double px, double py) { foreach (double[] e in excl) if (px >= e[0] && px < e[0] + e[2] && py >= e[1] && py < e[1] + e[3]) return true; return false; }

            int pad = (int)Math.Ceiling(far[1]) + 2;
            Box view = Box.Around(r[0], r[1], r[2], r[3], pad).Clamp(mock.W, mock.H);
            var face = new Shape("roundrect", r[0], r[1], r[2], r[3], radius);
            string sides = o.Get("sides", "tblr");

            // Ground: the median of the far ring, side by side (a panel is rarely one flat colour).
            var gr = new List<double>(); var gg = new List<double>(); var gb = new List<double>();
            var pts = new List<(int i, double x, double y, double d)>();
            for (int y = 0; y < view.H; y++)
            {
                for (int x = 0; x < view.W; x++)
                {
                    double px = view.X + x + 0.5, py = view.Y + y + 0.5;
                    if (Excluded(px, py)) continue;
                    double d = face.Sdf(px, py);
                    int i = (view.Y + y) * mock.W + view.X + x;
                    if (d >= far[0] && d <= far[1]) { gr.Add(mock.R[i]); gg.Add(mock.G[i]); gb.Add(mock.B[i]); }
                    if (d >= band[0] && d <= band[1])
                    {
                        bool isTop = py < r[1], isBottom = py > r[1] + r[3], isLeft = px < r[0], isRight = px > r[0] + r[2];
                        bool keep = (isTop && sides.Contains('t')) || (isBottom && sides.Contains('b')) || (isLeft && !isTop && !isBottom && sides.Contains('l')) || (isRight && !isTop && !isBottom && sides.Contains('r'));
                        if (keep) pts.Add((i, px, py, d));
                    }
                }
            }

            gr.Sort(); gg.Sort(); gb.Sort();
            double[] G = { gr[gr.Count / 2], gg[gg.Count / 2], gb[gb.Count / 2] };
            string gHex = Img.Hex((float)G[0], (float)G[1], (float)G[2]);
            double Level(double lin) => Img.ToSrgb01((float)Math.Clamp(lin, 0, 1)) * 255;

            double none = 0;
            foreach (var p in pts)
            {
                double dr = Level(mock.R[p.i]) - Level(G[0]), dg = Level(mock.G[p.i]) - Level(G[1]), db = Level(mock.B[p.i]) - Level(G[2]);
                none += (dr * dr + dg * dg + db * db) / 3;
            }

            none = Math.Sqrt(none / Math.Max(1, pts.Count));
            Console.WriteLine(FormattableString.Invariant($"ground {gHex} (far ring {far[0]:0.#}..{far[1]:0.#} px out, {gr.Count} px); band {band[0]:0.#}..{band[1]:0.#} px out, {pts.Count} px; with no shadow at all the band is {none:0.00} levels rms from the ground"));

            // The darkening seen in the band, by distance and side.
            var sbp = new StringBuilder("  seen (levels darker than the ground, mean of R,G,B) by distance out:");
            foreach (char side in "btlr")
            {
                sbp.Append($"\n    {(side == 'b' ? "below" : side == 't' ? "above" : side == 'l' ? "left " : "right")}: ");
                for (double d0 = band[0]; d0 < band[1]; d0 += 1.5)
                {
                    double acc = 0; int n = 0;
                    foreach (var p in pts)
                    {
                        if (p.d < d0 || p.d >= d0 + 1.5) continue;
                        bool isTop = p.y < r[1], isBottom = p.y > r[1] + r[3];
                        char s = isBottom ? 'b' : isTop ? 't' : p.x < r[0] ? 'l' : 'r';
                        if (s != side) continue;
                        // Straight parts only: the corners mix the sides.
                        if ((s == 'b' || s == 't') && (p.x < r[0] + radius || p.x > r[0] + r[2] - radius)) continue;
                        acc += (Level(G[0]) - Level(mock.R[p.i]) + Level(G[1]) - Level(mock.G[p.i]) + Level(G[2]) - Level(mock.B[p.i])) / 3; n++;
                    }

                    sbp.Append(n > 0 ? FormattableString.Invariant($"{d0 + 0.75:0.#}:{acc / n:0.0} ") : "");
                }
            }

            Console.WriteLine(sbp.ToString());

            // Model: blurred face shape, moved down.
            float[] Mask(double sigma, double drop)
            {
                var cov = new float[view.W * view.H];
                for (int y = 0; y < view.H; y++)
                    for (int x = 0; x < view.W; x++)
                        cov[y * view.W + x] = face.Coverage(view.X + x + 0.5, view.Y + y + 0.5 - drop);
                return GaussBlur(cov, view.W, view.H, sigma);
            }

            (double op, double rms) Solve(Func<(int i, double x, double y, double d), double> alphaAt, double[] sc)
            {
                // observed = G + op * a * (C - G)  ->  least squares for op, in linear light.
                double num = 0, den = 0;
                foreach (var p in pts)
                {
                    double a = alphaAt(p);
                    for (int ch = 0; ch < 3; ch++)
                    {
                        double obs = (ch == 0 ? mock.R[p.i] : ch == 1 ? mock.G[p.i] : mock.B[p.i]) - G[ch];
                        double mdl = a * (sc[ch] - G[ch]);
                        num += obs * mdl; den += mdl * mdl;
                    }
                }

                double op = den > 1e-12 ? Math.Clamp(num / den, 0, 1) : 0;
                return (op, RmsAt(alphaAt, sc, op));
            }

            double RmsAt(Func<(int i, double x, double y, double d), double> alphaAt, double[] sc, double op)
            {
                double e = 0;
                foreach (var p in pts)
                {
                    double a = alphaAt(p) * op;
                    double dr = Level(G[0] * (1 - a) + sc[0] * a) - Level(mock.R[p.i]), dg = Level(G[1] * (1 - a) + sc[1] * a) - Level(mock.G[p.i]), db = Level(G[2] * (1 - a) + sc[2] * a) - Level(mock.B[p.i]);
                    e += (dr * dr + dg * dg + db * db) / 3;
                }

                return Math.Sqrt(e / Math.Max(1, pts.Count));
            }

            double best = double.MaxValue, bs = 0, bd = 0, bo = 0;
            var table = new List<(double s, double d, double op, double e)>();
            for (double sigma = 1; sigma <= (o.Has("nofit") ? 0 : 12.01); sigma += 0.5)
            {
                for (double drop = 0; drop <= 10.01; drop += 0.5)
                {
                    float[] mk = Mask(sigma, drop);
                    var res = Solve(p => mk[(int)(p.y - view.Y) * view.W + (int)(p.x - view.X)], C);
                    table.Add((sigma, drop, res.op, res.rms));
                    if (res.rms < best) { best = res.rms; bs = sigma; bd = drop; bo = res.op; }
                }
            }

            if (!o.Has("nofit")) Console.WriteLine(FormattableString.Invariant($"  best soft shadow ({o.Get("color", "#2A1A12")}): blur sigma {bs:0.#} px, drop {bd:0.#} px, opacity {bo:0.000}  -> {best:0.00} levels rms"));
            foreach (var q in table.OrderBy(v => v.e).Skip(1).Take(5)) Console.WriteLine(FormattableString.Invariant($"    next: sigma {q.s:0.#} drop {q.d:0.#} opacity {q.op:0.000} -> {q.e:0.00}"));
            if (o.Has("sigma"))
            {
                double s0 = o.Num("sigma", bs);
                foreach (var q in table.Where(v => Math.Abs(v.s - s0) < 1e-6).OrderBy(v => v.e).Take(3)) Console.WriteLine(FormattableString.Invariant($"    at sigma {q.s:0.#}: drop {q.d:0.#} opacity {q.op:0.000} -> {q.e:0.00}"));
            }

            if (o.Has("shadow"))
            {
                Img sh = Img.Load(o.Get("shadow"));
                double[] faceSize = o.Nums("face", 2);
                int[] nine = o.Ints("nine", 4, new[] { 0, 0, 0, 0 });
                double scale = r[3] / faceSize[1];                              // mockup px per sprite px
                double spreadX = (sh.W - faceSize[0]) / 2 * scale, spreadY = (sh.H - faceSize[1]) / 2 * scale;
                double sr = 0, sg = 0, sb2 = 0, sw = 0;
                for (int i = 0; i < sh.Count; i++) { sr += sh.R[i] * sh.A[i]; sg += sh.G[i] * sh.A[i]; sb2 += sh.B[i] * sh.A[i]; sw += sh.A[i]; }
                double[] sc = { sr / sw, sg / sw, sb2 / sw };
                float[] Alpha(double drop)
                {
                    double[] rect = { r[0] - spreadX, r[1] - spreadY + drop, r[2] + 2 * spreadX, r[3] + 2 * spreadY };
                    Img drawn = RenderAt(sh, nine, 1 / scale, view.X, view.Y, 1, view.W, view.H, rect);
                    return drawn.A;
                }

                double bestS = double.MaxValue, bdS = 0, boS = 0;
                for (double drop = 0; drop <= 12.01; drop += 0.5)
                {
                    float[] a = Alpha(drop);
                    var res = Solve(p => a[(int)(p.y - view.Y) * view.W + (int)(p.x - view.X)], sc);
                    if (res.rms < bestS) { bestS = res.rms; bdS = drop; boS = res.op; }
                }

                Console.WriteLine(FormattableString.Invariant($"  {System.IO.Path.GetFileName(o.Get("shadow"))} ({sh.W}x{sh.H} for a {faceSize[0]:0}x{faceSize[1]:0} face, colour {Img.Hex((float)sc[0], (float)sc[1], (float)sc[2])}; spread here {spreadX:0.0} x {spreadY:0.0} px): best drop {bdS:0.#} px, opacity {boS:0.000} -> {bestS:0.00} levels rms"));
                if (o.Has("drop"))
                {
                    double d0 = o.Num("drop", 6), o0 = o.Num("opacity", 0.45);
                    float[] a = Alpha(d0);
                    Console.WriteLine(FormattableString.Invariant($"    as used now (drop {d0:0.#}, opacity {o0:0.##}): {RmsAt(p => a[(int)(p.y - view.Y) * view.W + (int)(p.x - view.X)], sc, o0):0.00} levels rms"));
                }
            }

            return 0;
        }

        /// <summary>
        /// The soft dark halo round a small light glyph (the chevron on a button): the glyph's own
        /// shape blurred (sigma), moved down (drop) and laid on the ground at an opacity, fitted to
        /// the mockup's pixels next to the glyph (the glyph itself drawn on top, as in the game).
        /// </summary>
        private static int GlyphShadow(Opts o)
        {
            Img sprite = Img.Load(o.Need("sprite"));
            Img mock = Img.Load(MockupPath(o.Need("screen")));
            double[] r = o.Nums("rect", 4);
            var col = Img.ParseHex(o.Get("color", "#2A0004"));
            double[] C = { col.r, col.g, col.b };
            Box view = Box.Around(r[0], r[1], r[2], r[3], o.Num("pad", 6)).Clamp(mock.W, mock.H);
            double[] erase = { r[0] - 3, r[1] - 3, r[2] + 6, r[3] + 6 };
            Img ground = ErasedView(mock, view, erase);
            Img glyph = RenderAt(sprite, null, 2.3, view.X, view.Y, 1, view.W, view.H, r);
            double Level(double lin) => Img.ToSrgb01((float)Math.Clamp(lin, 0, 1)) * 255;
            double best = double.MaxValue, bs = 0, bd = 0, bo = 0;
            foreach (double sigma in new[] { 0.4, 0.6, 0.8, 1.0, 1.25, 1.5, 2.0 })
            {
                foreach (double drop in new[] { 0.0, 0.25, 0.5, 0.75, 1.0, 1.5 })
                {
                    Img shifted = RenderAt(sprite, null, 2.3, view.X, view.Y, 1, view.W, view.H, new[] { r[0], r[1] + drop, r[2], r[3] });
                    float[] sh = GaussBlur(shifted.A, view.W, view.H, sigma);
                    double Err(double op)
                    {
                        double e = 0; int n = 0;
                        for (int i = 0; i < ground.Count; i++)
                        {
                            double a = sh[i] * op, ga = glyph.A[i];
                            double[] cc = new double[3];
                            double[] gr = { ground.R[i], ground.G[i], ground.B[i] }, gl = { glyph.R[i], glyph.G[i], glyph.B[i] }, mk = { mock.R[(view.Y + i / view.W) * mock.W + view.X + i % view.W], mock.G[(view.Y + i / view.W) * mock.W + view.X + i % view.W], mock.B[(view.Y + i / view.W) * mock.W + view.X + i % view.W] };
                            for (int ch = 0; ch < 3; ch++)
                            {
                                double v = gr[ch] * (1 - a) + C[ch] * a;
                                v = v * (1 - ga) + gl[ch] * ga;
                                double d = Level(v) - Level(mk[ch]);
                                e += d * d;
                            }

                            n += 3;
                        }

                        return Math.Sqrt(e / n);
                    }

                    // Opacity by golden-section search.
                    double lo = 0, hi = 1;
                    for (int it = 0; it < 30; it++)
                    {
                        double m1 = hi - (hi - lo) * 0.618, m2 = lo + (hi - lo) * 0.618;
                        if (Err(m1) < Err(m2)) hi = m2; else lo = m1;
                    }

                    double op = (lo + hi) / 2, e0 = Err(op);
                    if (e0 < best) { best = e0; bs = sigma; bd = drop; bo = op; }
                }
            }

            double none;
            {
                double e = 0; int n = 0;
                for (int i = 0; i < ground.Count; i++)
                {
                    double ga = glyph.A[i];
                    double[] gr = { ground.R[i], ground.G[i], ground.B[i] }, gl = { glyph.R[i], glyph.G[i], glyph.B[i] };
                    int mi = (view.Y + i / view.W) * mock.W + view.X + i % view.W;
                    double[] mk = { mock.R[mi], mock.G[mi], mock.B[mi] };
                    for (int ch = 0; ch < 3; ch++) { double v = gr[ch] * (1 - ga) + gl[ch] * ga; double d = Level(v) - Level(mk[ch]); e += d * d; }
                    n += 3;
                }

                none = Math.Sqrt(e / n);
            }

            Console.WriteLine(FormattableString.Invariant($"glyph halo ({o.Get("color", "#2A0004")}): blur sigma {bs:0.##} mockup px, drop {bd:0.##} px, opacity {bo:0.000} -> {best:0.00} levels rms over the view (without a halo {none:0.00})"));
            return 0;
        }

        /// <summary>
        /// A soft shadow sprite: the face's rounded rectangle blurred, on a canvas "spread" px
        /// larger on every side, in one colour, alpha 1 where the blur is complete. Drop and
        /// opacity are the node's (shadowDrop, shadowOpacity).
        /// </summary>
        private static int MakeShadow(Opts o)
        {
            int[] size = o.Ints("size", 2);
            double radius = o.Num("radius", size[1] / 2.0), sigma = o.Num("sigma", 8);
            int spread = o.Int("spread", (int)Math.Ceiling(sigma * 3));
            var c = Img.ParseHex(o.Get("color", "#2A1A12"));
            int W = size[0] + 2 * spread, H = size[1] + 2 * spread;
            // Blurred on a wider canvas, so that the clamped borders do not brighten the tails.
            int pad = (int)Math.Ceiling(sigma * 3.2) + 2;
            int bw = W + 2 * pad, bh = H + 2 * pad;
            var shape = new Shape("roundrect", pad + spread, pad + spread, size[0], size[1], radius);
            var cov = new float[bw * bh];
            for (int y = 0; y < bh; y++) for (int x = 0; x < bw; x++) cov[y * bw + x] = shape.Coverage(x + 0.5, y + 0.5);
            float[] bl = GaussBlur(cov, bw, bh, sigma);
            var n = new Img(W, H, 0f);
            double edgeMax = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float a = bl[(y + pad) * bw + x + pad];
                    // The last two pixels fade to nothing, so the sprite has no visible border.
                    int e = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y));
                    if (e == 0) edgeMax = Math.Max(edgeMax, a);
                    if (e < 2) a *= (e + 0.5f) / 2.5f;
                    if (o.Has("hollow"))
                    {
                        // Nothing under the face's inside (it is covered anyway): less overdraw.
                        var inner = new Shape("roundrect", spread + sigma * 3, spread + sigma * 3, size[0] - sigma * 6, size[1] - sigma * 6, Math.Max(0, radius - sigma * 3));
                        if (inner.W > 0 && inner.H > 0) a *= 1f - inner.Coverage(x + 0.5, y + 0.5);
                    }

                    int i = y * W + x;
                    n.A[i] = a; n.R[i] = c.r; n.G[i] = c.g; n.B[i] = c.b;
                }
            }

            n.Save(o.Need("out"));
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  {W}x{H} for a {size[0]}x{size[1]} face (radius {radius:0.##}), blur sigma {sigma:0.##}, spread {spread}; alpha at the canvas edge {edgeMax * 255:0.0}/255"));
            return 0;
        }

        /// <summary>
        /// A band that follows a rounded rectangle's outline: between "inset" and "inset + width"
        /// px inside the canvas box (corner radius "radius" at the box itself), in a colour that
        /// runs from top to bottom through the given stops. Drawn over whatever is there: the dark
        /// edge and the gold keyline of the Game Menu's crimson box.
        /// </summary>
        private static int Ring(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double inset = o.Num("inset", 0), width = o.Num("width", 2), radius = o.Num("radius", 0);
            var stops = o.Need("stops").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s =>
            {
                string[] q = s.Split(':');
                var c = Img.ParseHex(q[1]);
                return (f: double.Parse(q[0], CultureInfo.InvariantCulture), r: (double)Img.ToSrgb01(c.r), g: (double)Img.ToSrgb01(c.g), b: (double)Img.ToSrgb01(c.b));
            }).OrderBy(s => s.f).ToList();
            var outer = new Shape("roundrect", inset, inset, m.W - 2 * inset, m.H - 2 * inset, Math.Max(0, radius - inset));
            double i2 = inset + width;
            var inner = new Shape("roundrect", i2, i2, m.W - 2 * i2, m.H - 2 * i2, Math.Max(0, radius - i2));
            // "--outer L,T,R,B,radius --inner L,T,R,B,radius": a band whose two edges are their own
            // rounded rectangles (insets from the canvas box per side): a lip that is heavier at
            // the bottom than at the top.
            if (o.Has("outer"))
            {
                double[] a = o.Nums("outer", 5), b = o.Nums("inner", 5);
                outer = new Shape("roundrect", a[0], a[1], m.W - a[0] - a[2], m.H - a[1] - a[3], a[4]);
                inner = new Shape("roundrect", b[0], b[1], m.W - b[0] - b[2], m.H - b[1] - b[3], b[4]);
                inset = a[1]; i2 = b[1];
            }
            for (int y = 0; y < m.H; y++)
            {
                double f = (y + 0.5) / m.H;
                int k = 0;
                while (k < stops.Count - 2 && f > stops[k + 1].f) k++;
                double span = stops[k + 1].f - stops[k].f, t = span > 1e-9 ? Math.Clamp((f - stops[k].f) / span, 0, 1) : 0;
                float cr = Img.ToLinear((float)(stops[k].r + (stops[k + 1].r - stops[k].r) * t));
                float cg = Img.ToLinear((float)(stops[k].g + (stops[k + 1].g - stops[k].g) * t));
                float cb = Img.ToLinear((float)(stops[k].b + (stops[k + 1].b - stops[k].b) * t));
                for (int x = 0; x < m.W; x++)
                {
                    float a = Math.Clamp(outer.Coverage(x + 0.5, y + 0.5) - inner.Coverage(x + 0.5, y + 0.5), 0f, 1f);
                    if (a <= 0f) continue;
                    int i = y * m.W + x;
                    float pa = m.A[i], oa = a + pa * (1 - a);
                    m.R[i] = (cr * a + m.R[i] * pa * (1 - a)) / oa; m.G[i] = (cg * a + m.G[i] * pa * (1 - a)) / oa; m.B[i] = (cb * a + m.B[i] * pa * (1 - a)) / oa;
                    m.A[i] = oa;
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  band {inset:0.##}..{i2:0.##} px inside the outline (corner radius {radius:0.##} at the outline)"));
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // retone: the mockup's top-to-bottom shading, the drawing's ornaments
        /// <summary>
        /// Every pixel keeps its ratio to the plain colour of its own row (measured on the plain
        /// columns of the drawing), and the row's plain colour becomes the target's: the mockup's,
        /// read at the same height on a plain stretch of the mockup's button, or given as stops.
        /// "--gain" raises or lowers the ornaments' contrast per channel; "--radius r --edge e"
        /// fades the ornament out over the last e px before the rounded outline.
        /// </summary>
        private static int Retone(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int[] plain = o.Ints("plain", 2);
            double[] gain = o.Nums("gain", 3, new[] { 1.0, 1.0, 1.0 });
            double[] lim = o.Nums("limit", 2, new[] { 0.2, 5.0 });
            // The drawing's own plain colour by row.
            var bR = new double[m.H]; var bG = new double[m.H]; var bB = new double[m.H]; var ok = new bool[m.H];
            for (int y = 0; y < m.H; y++)
            {
                var vr = new List<double>(); var vg = new List<double>(); var vb = new List<double>();
                for (int x = plain[0]; x < plain[1]; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] < 0.999f) continue;
                    vr.Add(m.R[i]); vg.Add(m.G[i]); vb.Add(m.B[i]);
                }

                if (vr.Count < (plain[1] - plain[0]) / 2) continue;
                vr.Sort(); vg.Sort(); vb.Sort();
                bR[y] = vr[vr.Count / 2]; bG[y] = vg[vg.Count / 2]; bB[y] = vb[vb.Count / 2]; ok[y] = true;
            }

            int first = Array.IndexOf(ok, true), last = Array.LastIndexOf(ok, true);
            if (first < 0) throw new InvalidOperationException("no opaque plain columns");
            for (int y = 0; y < m.H; y++)
            {
                if (ok[y]) continue;
                int src = y < first ? first : last;
                if (y > first && y < last) { src = y; while (!ok[src]) src--; }
                bR[y] = bR[src]; bG[y] = bG[src]; bB[y] = bB[src];
            }

            // The target by row.
            var tR = new double[m.H]; var tG = new double[m.H]; var tB = new double[m.H];
            if (o.Has("stops"))
            {
                var stops = o.Get("stops").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s =>
                {
                    string[] q = s.Split(':');
                    var c = Img.ParseHex(q[1]);
                    return (f: double.Parse(q[0], CultureInfo.InvariantCulture), r: (double)Img.ToSrgb01(c.r), g: (double)Img.ToSrgb01(c.g), b: (double)Img.ToSrgb01(c.b));
                }).OrderBy(s => s.f).ToList();
                for (int y = 0; y < m.H; y++)
                {
                    double f = (y + 0.5) / m.H;
                    int k = 0;
                    while (k < stops.Count - 2 && f > stops[k + 1].f) k++;
                    double span = stops[k + 1].f - stops[k].f, t = span > 1e-9 ? Math.Clamp((f - stops[k].f) / span, 0, 1) : 0;
                    tR[y] = Img.ToLinear((float)(stops[k].r + (stops[k + 1].r - stops[k].r) * t));
                    tG[y] = Img.ToLinear((float)(stops[k].g + (stops[k + 1].g - stops[k].g) * t));
                    tB[y] = Img.ToLinear((float)(stops[k].b + (stops[k + 1].b - stops[k].b) * t));
                }
            }
            else
            {
                Img mock = Img.Load(MockupPath(o.Need("screen")));
                int[] col = o.Ints("col", 2);
                double[] rw = o.Nums("rows", 2);
                // Each sprite row is the mean of the mockup's plain colour over the stretch of the
                // mockup it covers (several samples), clamped to the button's own rows.
                // "--rowclamp a,b": the mockup rows (centres) that are the plain face; rows beyond
                // them (an edge line, a keyline) take the nearest face row's colour.
                double[] rc = o.Nums("rowclamp", 2, new[] { rw[0] + 0.5, rw[0] + rw[1] - 0.5 });
                for (int y = 0; y < m.H; y++)
                {
                    double ar = 0, ag = 0, ab = 0;
                    const int n = 4;
                    for (int j = 0; j < n; j++)
                    {
                        double my = rw[0] + (y + (j + 0.5) / n) / m.H * rw[1];
                        my = Math.Clamp(my, rc[0], rc[1]);
                        double[] c = PlainAt(mock, col[0], col[1], my);
                        ar += c[0]; ag += c[1]; ab += c[2];
                    }

                    tR[y] = ar / n; tG[y] = ag / n; tB[y] = ab / n;
                }
            }

            // "--radius r --edge e [--edge0 d]": nothing of the drawing within d px of the rounded
            // outline (there the row's own colour), all of it from e px inside.
            double edge = o.Num("edge", 0), edge0 = o.Num("edge0", 0), radius = o.Num("radius", 0);
            Shape outline = new Shape("roundrect", 0, 0, m.W, m.H, radius);
            // Ornament mode: the drawing says only how much lighter or darker than its row a pixel
            // is (its red channel: the drawing's green and blue are next to nothing, so their own
            // ratios are noise); the colours of "lighter" and "darker" are the mockup's.
            //   --from lo,hi     the drawing's log2(red / row red) at its dark and its light end
            //   --light r,g,b    the mockup's log2(pixel / row) at the light end, per channel
            //   --dark r,g,b     the same at the dark end
            bool ornament = o.Has("from");
            double[] from = o.Nums("from", 2, new[] { -1.0, 1.0 });
            double[] light = o.Nums("light", 3, new[] { 1.0, 1.0, 1.0 }), dark = o.Nums("dark", 3, new[] { -1.0, -1.0, -1.0 });
            double[] tl = o.Nums("tlimit", 2, new[] { -1.4, 1.3 });
            double core = o.Num("core", 0);
            // "--ground g --close r [--soft s]": on the mockup the woven cluster sits on a ground
            // that is darker than the button (the lattice between the diamonds, their crosses, the
            // gaps up to the little side motif), while the drawing leaves those at the button's own
            // colour. The cluster's footprint is the drawing's light shapes closed over gaps up to
            // 2r wide (and up to the outline); inside it, whatever is not light takes the dark
            // colours at strength g.
            double groundK = o.Num("ground", 0), closeR = o.Num("close", 12), softS = o.Num("soft", 2);
            float[] foot = null;
            if (ornament && groundK > 0)
            {
                double hi0 = Math.Max(1e-6, from[1] - core);
                var brightMask = new bool[m.Count];
                for (int y = 0; y < m.H; y++)
                {
                    for (int x = 0; x < m.W; x++)
                    {
                        int i = y * m.W + x;
                        if (m.A[i] < 0.5f) { brightMask[i] = true; continue; }       // beyond the outline counts as a shape to close up to
                        double mod = Math.Log(Math.Clamp(Math.Max(1e-5, m.R[i]) / Math.Max(1e-5, bR[y]), lim[0], lim[1]), 2);
                        brightMask[i] = Math.Max(0, mod - core) / hi0 >= 0.35;
                    }
                }

                int rr = (int)Math.Ceiling(closeR);
                bool[] Morph(bool[] src, bool dilate)
                {
                    var dst = new bool[src.Length];
                    for (int y = 0; y < m.H; y++)
                    {
                        for (int x = 0; x < m.W; x++)
                        {
                            bool hit = false;
                            for (int dy = -rr; dy <= rr && !hit; dy++)
                            {
                                int yy = y + dy;
                                for (int dx = -rr; dx <= rr; dx++)
                                {
                                    if (dx * dx + dy * dy > closeR * closeR) continue;
                                    int xx = x + dx;
                                    // Beyond the canvas is like beyond the outline.
                                    bool v = xx < 0 || yy < 0 || xx >= m.W || yy >= m.H ? true : src[yy * m.W + xx];
                                    if (v == dilate) { hit = true; break; }
                                }
                            }

                            dst[y * m.W + x] = dilate ? hit : !hit;
                        }
                    }

                    return dst;
                }

                bool[] closed = Morph(Morph(brightMask, true), false);
                // Only where light shapes are near: a plain stretch next to the outline is no footprint.
                int limitX = o.Int("footx", m.W);
                var fm = new float[m.Count];
                for (int y = 0; y < m.H; y++) for (int x = 0; x < m.W; x++) { int i = y * m.W + x; fm[i] = closed[i] && m.A[i] >= 0.5f && (x < limitX || x >= m.W - limitX) ? 1f : 0f; }
                foot = GaussBlur(fm, m.W, m.H, softS);
            }
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] <= 0f) continue;
                    double k = 1;
                    if (edge > 0)
                    {
                        double d = -outline.Sdf(x + 0.5, y + 0.5);            // px inside the outline
                        k = Smooth((float)Math.Clamp((d - edge0) / Math.Max(1e-6, edge - edge0), 0, 1));
                    }

                    if (ornament)
                    {
                        double mod = Math.Log(Math.Clamp(Math.Max(1e-5, m.R[i]) / Math.Max(1e-5, bR[y]), lim[0], lim[1]), 2);
                        // "--core c": what differs from the row by less than c is grain, not ornament.
                        double hi = Math.Max(1e-6, from[1] - core), lo = Math.Max(1e-6, -from[0] - core);
                        double t = mod >= 0 ? Math.Min(tl[1], Math.Max(0, mod - core) / hi) : Math.Max(tl[0], -Math.Max(0, -mod - core) / lo);
                        t *= k;
                        if (foot != null)
                        {
                            // Light shapes on the footprint's darker ground; the drawing's own
                            // darker strokes (t below 0) deepen the ground a little further.
                            // Outside the footprint nothing is darker than the button: what the
                            // drawing has there is grain and a faint halo round the cluster.
                            double sl = Math.Clamp(t, 0, tl[1]);
                            double gd = foot[i] * k * (groundK * (1 - Math.Min(1.0, sl)) + (1 - groundK) * Math.Min(1.0, Math.Max(0, -t)));
                            m.R[i] = (float)Math.Clamp(tR[y] * Math.Pow(2, sl * light[0] + gd * dark[0]), 0, 1);
                            m.G[i] = (float)Math.Clamp(tG[y] * Math.Pow(2, sl * light[1] + gd * dark[1]), 0, 1);
                            m.B[i] = (float)Math.Clamp(tB[y] * Math.Pow(2, sl * light[2] + gd * dark[2]), 0, 1);
                            continue;
                        }

                        double[] e = t >= 0 ? light : dark;
                        double s = Math.Abs(t);
                        m.R[i] = (float)Math.Clamp(tR[y] * Math.Pow(2, s * e[0]), 0, 1);
                        m.G[i] = (float)Math.Clamp(tG[y] * Math.Pow(2, s * e[1]), 0, 1);
                        m.B[i] = (float)Math.Clamp(tB[y] * Math.Pow(2, s * e[2]), 0, 1);
                        continue;
                    }

                    double Out(double v, double b, double t, double g)
                    {
                        double ratio = Math.Clamp(Math.Max(1e-5, v) / Math.Max(1e-5, b), lim[0], lim[1]);
                        return t * Math.Pow(ratio, g * k);
                    }

                    m.R[i] = (float)Math.Clamp(Out(m.R[i], bR[y], tR[y], gain[0]), 0, 1);
                    m.G[i] = (float)Math.Clamp(Out(m.G[i], bG[y], tG[y], gain[1]), 0, 1);
                    m.B[i] = (float)Math.Clamp(Out(m.B[i], bB[y], tB[y], gain[2]), 0, 1);
                }
            }

            m.Save(o.Need("out"));
            int mid = m.H / 2;
            Console.WriteLine(FormattableString.Invariant($"{o.Need("out")}  {m.W}x{m.H}  plain columns {plain[0]}..{plain[1]}: top {Img.Hex((float)bR[first], (float)bG[first], (float)bB[first])}>{Img.Hex((float)tR[0], (float)tG[0], (float)tB[0])}  middle {Img.Hex((float)bR[mid], (float)bG[mid], (float)bB[mid])}>{Img.Hex((float)tR[mid], (float)tG[mid], (float)tB[mid])}  bottom {Img.Hex((float)bR[last], (float)bG[last], (float)bB[last])}>{Img.Hex((float)tR[m.H - 1], (float)tG[m.H - 1], (float)tB[m.H - 1])}  gain {gain[0]:0.##},{gain[1]:0.##},{gain[2]:0.##}"));
            return 0;
        }
    }
}
