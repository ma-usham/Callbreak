using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace DailyRewards
{
    /// <summary>
    /// The day tiles: ChatGPT's interior (fill texture, corner motifs) re-toned onto the mockup's smooth
    /// fill, frames drawn at the measured line widths, and the today-overlay solved from two looks.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageTiles = @"  retone  --in sprite.png --out f --screen mockup.png --rect x,y,w,h [--radius r] [--inset 4] [--exclude ""x,y,w,h;..""]
          [--gain 1,1,1] [--deg 2]
          keep the sprite's detail (motifs, texture) but put its smooth fill on the mockup's: both are fitted
          with a low-degree polynomial in x and y over the tile's inside (inset mockup px from the edge,
          excluded rects left out); out = mockupFit + gain x (sprite - spriteFit), per sRGB channel
  overframe --in base.png --out f --size w,h --radius r [--bands ""w:#hex[>#hex];...""] [--clean c]
          draw bands (mockup px, from the outer edge inwards; #top>#bottom gradient, /#side: the colour of the left and right sides) over a sprite whose
          box is the frame's outer edge (unit = sprite px / w); alpha outside the rounded rect is cleared
          (anti-aliased); --clean c first replaces the base's own outline (c mockup px deep) with its colour further in
  solveov --base n.png --target t.png --out f [--floor 0] [--cap 1] [--maxerr 0] [--smooth 0]
          the overlay O (colour, alpha) with the least alpha that turns base into target where both are
          opaque (O over base = target, within maxerr sRGB levels), alpha at least floor; where only the
          target is opaque it is copied
  over    --base a.png --top b.png --out f [--op 1]       b over a (same size)
  place   --in s.png --out f --canvas W,H --at x,y [--scale 1]   the sprite drawn into an empty canvas at a sub-pixel offset
  rmsimg  --a a.png --b b.png [--rect x,y,w,h]           rms / mean difference of two same-size images (sRGB levels, opaque px)
";

        private static bool TileCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "retone": rc = Retone(o); return true;
                case "overframe": rc = OverFrame(o); return true;
                case "solveov": rc = SolveOverlay(o); return true;
                case "over": rc = OverCmd(o); return true;
                case "place": rc = PlaceCmd(o); return true;
                case "rmsimg": rc = RmsImg(o); return true;
                default: return false;
            }
        }

        // ------------------------------------------------------------------------------------------
        internal static float S(float lin) => Img.ToSrgb01(lin) * 255f;
        internal static float L(float srgb255) => Img.ToLinear(Math.Clamp(srgb255, 0f, 255f) / 255f);

        /// <summary>Bilinear sample of an image (linear light) at a continuous position (pixel centres at +0.5).</summary>
        internal static (float r, float g, float b, float a) Sample(Img m, double x, double y)
        {
            x -= 0.5; y -= 0.5;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = (float)(x - x0), fy = (float)(y - y0);
            float r = 0, g = 0, b = 0, a = 0;
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                int xx = Math.Clamp(x0 + i, 0, m.W - 1), yy = Math.Clamp(y0 + j, 0, m.H - 1);
                float w = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
                int k = yy * m.W + xx;
                r += m.R[k] * w; g += m.G[k] * w; b += m.B[k] * w; a += m.A[k] * w;
            }

            return (r, g, b, a);
        }

        /// <summary>Signed distance (positive inside) from the edge of a rounded rect [0,w]x[0,h], radius r.</summary>
        internal static double Inside(double x, double y, double w, double h, double r)
        {
            double qx = Math.Abs(x - w / 2) - (w / 2 - r), qy = Math.Abs(y - h / 2) - (h / 2 - r);
            double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            double outside = Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
            return -outside;
        }

        private static List<double[]> Rects(string s) => (s ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Nums).ToList();

        // ------------------------------------------------------------------------------------------
        private static int Retone(Opts o)
        {
            Img sp = Img.Load(o.Need("in"));
            Img m = Img.Load(o.Need("screen"));
            double[] r = o.Nums("rect", 4);
            double rad = o.Num("radius", 16), inset = o.Num("inset", 4);
            int deg = (int)o.Num("deg", 2);
            double[] gain = o.Nums("gain", 3, new[] { 1.0, 1.0, 1.0 });
            var excl = Rects(o.Get("exclude"));
            double ux = sp.W / r[2], uy = sp.H / r[3];

            // samples at mockup pixel centres inside the tile (inset), not excluded
            var pts = new List<(double u, double v, float[] mk, float[] spc)>();
            for (int my = (int)Math.Floor(r[1]); my < r[1] + r[3]; my++)
            for (int mx = (int)Math.Floor(r[0]); mx < r[0] + r[2]; mx++)
            {
                double lx = mx + 0.5 - r[0], ly = my + 0.5 - r[1];
                if (Inside(lx, ly, r[2], r[3], rad) < inset) continue;
                if (excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3])) continue;
                int k = my * m.W + mx;
                // the sprite averaged over the mockup pixel's footprint
                float sr = 0, sg = 0, sb = 0; int n = 0;
                for (double yy = ly * uy - uy / 2 + 0.5; yy < ly * uy + uy / 2; yy += 1)
                for (double xx = lx * ux - ux / 2 + 0.5; xx < lx * ux + ux / 2; xx += 1)
                {
                    var s = Sample(sp, xx, yy);
                    sr += s.r; sg += s.g; sb += s.b; n++;
                }

                pts.Add((lx / r[2] - 0.5, ly / r[3] - 0.5, new[] { S(m.R[k]), S(m.G[k]), S(m.B[k]) }, new[] { S(sr / n), S(sg / n), S(sb / n) }));
            }

            var terms = new List<(int px, int py)>();
            for (int a = 0; a <= deg; a++)
            for (int b = 0; a + b <= deg; b++)
                terms.Add((a, b));
            double[] Basis(double u, double v) => terms.Select(t => Math.Pow(u, t.px) * Math.Pow(v, t.py)).ToArray();

            double[][] fitM = new double[3][], fitS = new double[3][];
            for (int c = 0; c < 3; c++)
            {
                fitM[c] = LeastSquares(pts.Select(p => Basis(p.u, p.v)).ToList(), pts.Select(p => (double)p.mk[c]).ToList());
                fitS[c] = LeastSquares(pts.Select(p => Basis(p.u, p.v)).ToList(), pts.Select(p => (double)p.spc[c]).ToList());
            }

            double Eval(double[] coef, double u, double v)
            {
                double[] bs = Basis(u, v);
                double s = 0;
                for (int i = 0; i < bs.Length; i++) s += coef[i] * bs[i];
                return s;
            }

            var outImg = sp.Clone();
            double err = 0; int ne = 0;
            for (int y = 0; y < sp.H; y++)
            for (int x = 0; x < sp.W; x++)
            {
                int k = y * sp.W + x;
                double u = (x + 0.5) / sp.W - 0.5, v = (y + 0.5) / sp.H - 0.5;
                float[] cur = { S(sp.R[k]), S(sp.G[k]), S(sp.B[k]) };
                float[] res = new float[3];
                for (int c = 0; c < 3; c++)
                    res[c] = (float)(Eval(fitM[c], u, v) + gain[c] * (cur[c] - Eval(fitS[c], u, v)));
                outImg.R[k] = L(res[0]); outImg.G[k] = L(res[1]); outImg.B[k] = L(res[2]);
            }

            foreach (var p in pts)
            {
                for (int c = 0; c < 3; c++)
                {
                    double pred = Eval(fitM[c], p.u, p.v) + gain[c] * (p.spc[c] - Eval(fitS[c], p.u, p.v));
                    err += (pred - p.mk[c]) * (pred - p.mk[c]); ne++;
                }
            }

            outImg.Save(o.Need("out"));
            string Hex3(double[][] f, double u, double v) => $"#{(int)Math.Round(Eval(f[0], u, v)):X2}{(int)Math.Round(Eval(f[1], u, v)):X2}{(int)Math.Round(Eval(f[2], u, v)):X2}";
            Console.WriteLine($"{o.Need("out")}  {pts.Count} samples; mockup fill top {Hex3(fitM, 0, -0.4)} mid {Hex3(fitM, 0, 0)} bottom {Hex3(fitM, 0, 0.4)}; " +
                              $"sprite fill top {Hex3(fitS, 0, -0.4)} mid {Hex3(fitS, 0, 0)} bottom {Hex3(fitS, 0, 0.4)}; rms on the samples {Math.Sqrt(err / Math.Max(1, ne)):F2}");
            return 0;
        }

        internal static double[] LeastSquares(List<double[]> rows, List<double> y)
        {
            int n = rows[0].Length;
            var a = new double[n, n];
            var b = new double[n];
            for (int k = 0; k < rows.Count; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    b[i] += rows[k][i] * y[k];
                    for (int j = 0; j < n; j++) a[i, j] += rows[k][i] * rows[k][j];
                }
            }

            for (int i = 0; i < n; i++) a[i, i] += 1e-9;
            // Gaussian elimination
            for (int i = 0; i < n; i++)
            {
                int p = i;
                for (int r = i + 1; r < n; r++) if (Math.Abs(a[r, i]) > Math.Abs(a[p, i])) p = r;
                for (int c = 0; c < n; c++) (a[i, c], a[p, c]) = (a[p, c], a[i, c]);
                (b[i], b[p]) = (b[p], b[i]);
                for (int r = i + 1; r < n; r++)
                {
                    double f = a[r, i] / a[i, i];
                    for (int c = i; c < n; c++) a[r, c] -= f * a[i, c];
                    b[r] -= f * b[i];
                }
            }

            var xs = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = b[i];
                for (int c = i + 1; c < n; c++) s -= a[i, c] * xs[c];
                xs[i] = s / a[i, i];
            }

            return xs;
        }

        // ------------------------------------------------------------------------------------------
        private static int OverFrame(Opts o)
        {
            Img b = Img.Load(o.Need("in"));
            double[] size = o.Nums("size", 2);
            double rad = o.Num("radius", 16);
            double unit = b.W / size[0];
            var bands = o.Get("bands", "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s =>
            {
                string[] p = s.Split(':');
                string[] sideSplit = p[1].Split('/');
                string[] c = sideSplit[0].Split('>');
                string[] ca = c[0].Split('@');
                string[] cb = (c.Length > 1 ? c[1] : c[0]).Split('@');
                float alA = ca.Length > 1 ? float.Parse(ca[1], CultureInfo.InvariantCulture) : 1f;
                float alB = cb.Length > 1 ? float.Parse(cb[1], CultureInfo.InvariantCulture) : alA;
                var side = sideSplit.Length > 1 ? Img.ParseHex(sideSplit[1]) : (-1f, -1f, -1f);
                return (w: double.Parse(p[0], CultureInfo.InvariantCulture), top: Img.ParseHex(ca[0]), bot: Img.ParseHex(cb[0]), aTop: alA, aBot: alB, side: side);
            }).ToList();
            double[] edges = new double[bands.Count + 1];
            for (int i = 0; i < bands.Count; i++) edges[i + 1] = edges[i] + bands[i].w;
            // --clean c: the base's own outline (up to c mockup px in from the edge) is replaced by the
            // base's colour found further in, at depth c + 0.5, along the inward normal
            double clean = o.Num("clean", 0);
            if (clean > 0)
            {
                var src = b.Clone();
                for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    double mx = (x + 0.5) / unit, my = (y + 0.5) / unit;
                    double d = Inside(mx, my, size[0], size[1], rad);
                    if (d >= clean) continue;
                    const double h = 0.05;
                    double gx = Inside(mx + h, my, size[0], size[1], rad) - Inside(mx - h, my, size[0], size[1], rad);
                    double gy = Inside(mx, my + h, size[0], size[1], rad) - Inside(mx, my - h, size[0], size[1], rad);
                    double gl = Math.Sqrt(gx * gx + gy * gy);
                    if (gl < 1e-9) continue;
                    double step = clean + 0.5 - d;
                    var s = Sample(src, (mx + gx / gl * step) * unit, (my + gy / gl * step) * unit);
                    int k = y * b.W + x;
                    b.R[k] = s.r; b.G[k] = s.g; b.B[k] = s.b;
                    b.A[k] = 1f;
                }
            }

            var outImg = b.Clone();
            const int ss = 5;
            for (int y = 0; y < b.H; y++)
            for (int x = 0; x < b.W; x++)
            {
                int k = y * b.W + x;
                double t = (y + 0.5) / b.H;
                float cr = 0, cg = 0, cb2 = 0, ca = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    double mx = (x + (sx + 0.5) / ss) / unit, my = (y + (sy + 0.5) / ss) / unit;
                    double d = Inside(mx, my, size[0], size[1], rad);
                    if (d < 0) continue;
                    int bi = -1;
                    for (int i = 0; i < bands.Count; i++) if (d >= edges[i] && d < edges[i + 1]) { bi = i; break; }
                    // premultiplied: a band (alpha al) over the base (its own alpha)
                    float ba = b.A[k];
                    float pr = b.R[k] * ba, pg = b.G[k] * ba, pb = b.B[k] * ba, pa = ba;
                    if (bi >= 0)
                    {
                        var bd = bands[bi];
                        float al = (float)(bd.aTop + (bd.aBot - bd.aTop) * t);
                        float lr = (float)(bd.top.r + (bd.bot.r - bd.top.r) * t), lg = (float)(bd.top.g + (bd.bot.g - bd.top.g) * t), lb = (float)(bd.top.b + (bd.bot.b - bd.top.b) * t);
                        if (bd.side.Item1 >= 0)
                        {
                            // the left and right sides take the side colour, by how vertical the edge is there
                            const double hh = 0.05;
                            double gx = Inside(mx + hh, my, size[0], size[1], rad) - Inside(mx - hh, my, size[0], size[1], rad);
                            double gy = Inside(mx, my + hh, size[0], size[1], rad) - Inside(mx, my - hh, size[0], size[1], rad);
                            double gl = Math.Sqrt(gx * gx + gy * gy);
                            float wx = gl > 1e-9 ? (float)(Math.Abs(gx) / gl) : 0f;
                            wx = wx * wx;
                            lr += (bd.side.Item1 - lr) * wx; lg += (bd.side.Item2 - lg) * wx; lb += (bd.side.Item3 - lb) * wx;
                        }
                        pr = pr * (1 - al) + lr * al; pg = pg * (1 - al) + lg * al; pb = pb * (1 - al) + lb * al;
                        pa = pa * (1 - al) + al;
                    }

                    cr += pr; cg += pg; cb2 += pb; ca += pa;
                }

                if (ca <= 1e-6f) { outImg.A[k] = 0; continue; }
                outImg.R[k] = cr / ca; outImg.G[k] = cg / ca; outImg.B[k] = cb2 / ca;
                outImg.A[k] = ca / (ss * ss);
            }

            outImg.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {b.W}x{b.H}  unit {unit:F3}  frame {size[0]}x{size[1]} r {rad}, bands {string.Join(";", bands.Select(q => q.w))}");
            return 0;
        }

        // ------------------------------------------------------------------------------------------
        private static int SolveOverlay(Opts o)
        {
            Img n = Img.Load(o.Need("base")), t = Img.Load(o.Need("target"));
            if (n.W != t.W || n.H != t.H) throw new ArgumentException("base and target differ in size");
            float floor = (float)o.Num("floor", 0), cap = (float)o.Num("cap", 1), maxErr = (float)o.Num("maxerr", 0);
            var ov = new Img(n.W, n.H, 0f);
            var alpha = new float[n.Count];
            for (int k = 0; k < n.Count; k++)
            {
                if (t.A[k] <= 0.002f) { alpha[k] = 0; continue; }
                if (n.A[k] < 0.999f)
                {
                    // the target reaches where the base is not opaque (anti-aliased rim or outside): copy it
                    alpha[k] = -1;
                    continue;
                }

                float a = floor;
                float[] tc = { S(t.R[k]), S(t.G[k]), S(t.B[k]) }, nc = { S(n.R[k]), S(n.G[k]), S(n.B[k]) };
                for (int c = 0; c < 3; c++)
                {
                    float tl = L(tc[c] > nc[c] ? tc[c] - maxErr : tc[c] + maxErr), nl = L(nc[c]);
                    if (Math.Abs(tc[c] - nc[c]) <= maxErr) continue;
                    float need = tl > nl ? (tl - nl) / Math.Max(1e-6f, 1 - nl) : (nl - tl) / Math.Max(1e-6f, nl);
                    a = Math.Max(a, need);
                }

                alpha[k] = Math.Min(cap, a);
            }

            int smooth = (int)o.Num("smooth", 0);
            if (smooth > 0)
            {
                // max filter then box blur, so the alpha never drops below what a pixel needs
                var mx = (float[])alpha.Clone();
                for (int y = 0; y < n.H; y++)
                for (int x = 0; x < n.W; x++)
                {
                    float best = alpha[y * n.W + x];
                    if (best < 0) continue;
                    for (int dy = -smooth; dy <= smooth; dy++)
                    for (int dx = -smooth; dx <= smooth; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= n.W || yy >= n.H) continue;
                        best = Math.Max(best, alpha[yy * n.W + xx]);
                    }

                    mx[y * n.W + x] = best;
                }

                alpha = mx;
            }

            for (int k = 0; k < n.Count; k++)
            {
                if (alpha[k] < 0)
                {
                    // copy the target as it is (with its own alpha): it shows where the base does not
                    ov.R[k] = t.R[k]; ov.G[k] = t.G[k]; ov.B[k] = t.B[k]; ov.A[k] = t.A[k];
                    continue;
                }

                float a = alpha[k];
                ov.A[k] = a;
                if (a <= 1e-4f) { ov.R[k] = t.R[k]; ov.G[k] = t.G[k]; ov.B[k] = t.B[k]; continue; }
                ov.R[k] = Math.Clamp((t.R[k] - n.R[k] * (1 - a)) / a, 0f, 1f);
                ov.G[k] = Math.Clamp((t.G[k] - n.G[k] * (1 - a)) / a, 0f, 1f);
                ov.B[k] = Math.Clamp((t.B[k] - n.B[k] * (1 - a)) / a, 0f, 1f);
            }

            ov.Save(o.Need("out"));
            var inner = alpha.Where(v => v >= 0).ToArray();
            Console.WriteLine($"{o.Need("out")}  {n.W}x{n.H}  alpha mean {inner.Average():F3} median {inner.OrderBy(v => v).ElementAt(inner.Length / 2):F3}");
            return 0;
        }

        private static int PlaceCmd(Opts o)
        {
            Img s = Img.Load(o.Need("in"));
            int[] canvas = o.Ints("canvas", 2);
            double[] at = o.Nums("at", 2);
            double k = o.Num("scale", 1);
            var r = new Img(canvas[0], canvas[1], 0f);
            for (int y = 0; y < r.H; y++)
            for (int x = 0; x < r.W; x++)
            {
                double sx = (x + 0.5 - at[0]) / k, sy = (y + 0.5 - at[1]) / k;
                if (sx < -0.5 || sy < -0.5 || sx > s.W + 0.5 || sy > s.H + 0.5) continue;
                // premultiplied bilinear
                double fx = sx - 0.5, fy = sy - 0.5;
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                double tx = fx - x0, ty = fy - y0;
                double pr = 0, pg = 0, pb = 0, pa = 0;
                for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    int xx = x0 + i, yy = y0 + j;
                    double w = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
                    if (xx < 0 || yy < 0 || xx >= s.W || yy >= s.H) continue;
                    int q = yy * s.W + xx;
                    pr += s.R[q] * s.A[q] * w; pg += s.G[q] * s.A[q] * w; pb += s.B[q] * s.A[q] * w; pa += s.A[q] * w;
                }

                int d = y * r.W + x;
                if (pa <= 1e-6) continue;
                r.R[d] = (float)(pr / pa); r.G[d] = (float)(pg / pa); r.B[d] = (float)(pb / pa); r.A[d] = (float)pa;
            }

            r.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {r.W}x{r.H}");
            return 0;
        }

        private static int OverCmd(Opts o)
        {
            Img a = Img.Load(o.Need("base")), b = Img.Load(o.Need("top"));
            float op = (float)o.Num("op", 1);
            var r = a.Clone();
            for (int k = 0; k < a.Count; k++)
            {
                float ba = b.A[k] * op, aa = a.A[k];
                float oa = ba + aa * (1 - ba);
                if (oa <= 0) { r.A[k] = 0; continue; }
                r.R[k] = (b.R[k] * ba + a.R[k] * aa * (1 - ba)) / oa;
                r.G[k] = (b.G[k] * ba + a.G[k] * aa * (1 - ba)) / oa;
                r.B[k] = (b.B[k] * ba + a.B[k] * aa * (1 - ba)) / oa;
                r.A[k] = oa;
            }

            r.Save(o.Need("out"));
            Console.WriteLine(o.Need("out"));
            return 0;
        }

        private static int RmsImg(Opts o)
        {
            Img a = Img.Load(o.Need("a")), b = Img.Load(o.Need("b"));
            int[] r = o.Ints("rect", 4, new[] { 0, 0, a.W, a.H });
            double s2 = 0, s1 = 0; int n = 0;
            for (int y = r[1]; y < r[1] + r[3]; y++)
            for (int x = r[0]; x < r[0] + r[2]; x++)
            {
                int k = y * a.W + x;
                if (a.A[k] < 0.99f || b.A[k] < 0.99f) continue;
                foreach (var (p, q) in new[] { (a.R[k], b.R[k]), (a.G[k], b.G[k]), (a.B[k], b.B[k]) })
                {
                    double d = S(p) - S(q);
                    s2 += d * d; s1 += Math.Abs(d); n++;
                }
            }

            Console.WriteLine($"rms {Math.Sqrt(s2 / Math.Max(1, n)):F2}  mean {s1 / Math.Max(1, n):F2}  ({n / 3} px)");
            return 0;
        }
    }
}
