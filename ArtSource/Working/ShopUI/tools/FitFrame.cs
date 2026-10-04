using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// fitframe: the rounded frame (rect, corner radius, band widths and band colours) that reproduces
    /// the frame of a panel on the mockup, compared on the pixels near its edge only.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageFitFrame = @"  fitframe --screen png --rect x,y,w,h --radius r --bands ""w;w;w"" [--fixcolors ""#hex;#hex""] [--exclude ""x,y,w,h;..""]
           [--fillband 6] [--iters 3] [--nofit] [--fitwidths]
          rect, radius and band widths (mockup px) at which a frame of flat bands lands on the mockup's
          frame; band colours and the fill colour are measured as the mean mockup colour under each band
";

        private static bool FitFrameCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            if (cmd != "fitframe") return false;
            rc = FitFrame(o);
            return true;
        }

        private sealed class FrameParams
        {
            public double X, Y, W, H, R;
            public double[] Bands;
            public FrameParams Clone() => new FrameParams { X = X, Y = Y, W = W, H = H, R = R, Bands = (double[])Bands.Clone() };
        }

        private static int FitFrame(Opts o)
        {
            Img m = Img.Load(o.Need("screen"));
            double[] r0 = o.Nums("rect", 4, null);
            var p = new FrameParams
            {
                X = r0[0], Y = r0[1], W = r0[2], H = r0[3], R = o.Num("radius", 20),
                Bands = o.Need("bands").Split(';').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
            };
            double fillBand = o.Num("fillband", 6);
            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            int nb = p.Bands.Length;
            var cols = new double[nb + 1][];
            for (int i = 0; i <= nb; i++) cols[i] = new double[] { 0.8, 0.7, 0.6 };
            bool Excluded(int x, int y) => excl.Any(e => x + 0.5 >= e[0] && x + 0.5 < e[0] + e[2] && y + 0.5 >= e[1] && y + 0.5 < e[1] + e[3]);

            // per-pixel band coverage (nb bands + fill within fillBand) at mockup resolution
            (int x0, int y0, int w, int h, float[][] cov, float[] alpha) Coverage(FrameParams q)
            {
                int x0 = (int)Math.Floor(q.X) - 2, y0 = (int)Math.Floor(q.Y) - 2;
                int w = (int)Math.Ceiling(q.W) + 5, h = (int)Math.Ceiling(q.H) + 5;
                var cov = new float[nb + 1][];
                for (int i = 0; i <= nb; i++) cov[i] = new float[w * h];
                var alpha = new float[w * h];
                double[] radii = { q.R, q.R, q.R, q.R };
                double total = q.Bands.Sum();
                const int ss = 4;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // quick reject: far inside
                    double cx = x0 + x + 0.5 - q.X, cy = y0 + y + 0.5 - q.Y;
                    double dEdge = Math.Min(Math.Min(cx, q.W - cx), Math.Min(cy, q.H - cy));
                    if (dEdge > total + fillBand + q.R + 2) continue;
                    for (int j = 0; j < ss; j++)
                    for (int i = 0; i < ss; i++)
                    {
                        double px = x0 + x + (i + 0.5) / ss - q.X, py = y0 + y + (j + 0.5) / ss - q.Y;
                        double d = InsideDist(px, py, q.W, q.H, radii);
                        if (d <= 0) continue;
                        double acc = 0;
                        int band = nb;
                        for (int b = 0; b < nb; b++)
                        {
                            if (d < acc + q.Bands[b]) { band = b; break; }
                            acc += q.Bands[b];
                        }

                        if (band == nb && d > total + fillBand) { alpha[y * w + x] += 0; continue; }
                        cov[band][y * w + x] += 1f / (ss * ss);
                        alpha[y * w + x] += 1f / (ss * ss);
                    }
                }

                return (x0, y0, w, h, cov, alpha);
            }

            double Error(FrameParams q, bool measure)
            {
                var (x0, y0, w, h, cov, alpha) = Coverage(q);
                if (measure)
                {
                    for (int b = 0; b <= nb; b++)
                    {
                        double sr = 0, sg = 0, sb = 0, sw = 0;
                        for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            float c = cov[b][y * w + x];
                            if (c < 0.75f) continue;
                            int mx = x0 + x, my = y0 + y;
                            if (mx < 0 || my < 0 || mx >= m.W || my >= m.H || Excluded(mx, my)) continue;
                            int k = my * m.W + mx;
                            sr += m.R[k] * c; sg += m.G[k] * c; sb += m.B[k] * c; sw += c;
                        }

                        if (sw > 0) cols[b] = new[] { sr / sw, sg / sw, sb / sw };
                    }
                }

                double e = 0; int n = 0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int t = y * w + x;
                    if (alpha[t] <= 0) continue;
                    int mx = x0 + x, my = y0 + y;
                    if (mx < 0 || my < 0 || mx >= m.W || my >= m.H || Excluded(mx, my)) continue;
                    int k = my * m.W + mx;
                    double rr = 0, gg = 0, bb = 0;
                    for (int b = 0; b <= nb; b++) { float c = cov[b][t]; rr += cols[b][0] * c; gg += cols[b][1] * c; bb += cols[b][2] * c; }
                    float a = alpha[t];
                    rr += m.R[k] * (1 - a); gg += m.G[k] * (1 - a); bb += m.B[k] * (1 - a);
                    double dr = Img.ToSrgb01((float)rr) - Img.ToSrgb01(m.R[k]), dg = Img.ToSrgb01((float)gg) - Img.ToSrgb01(m.G[k]), db = Img.ToSrgb01((float)bb) - Img.ToSrgb01(m.B[k]);
                    e += (dr * dr + dg * dg + db * db) / 3; n++;
                }

                return n > 0 ? Math.Sqrt(e / n) * 255 : 1e9;
            }

            double best = Error(p, true);
            Console.WriteLine($"start: rms {best:0.00}");
            if (!o.Has("nofit"))
            {
                int iters = (int)o.Num("iters", 3);
                bool fitW = o.Has("fitwidths");
                for (int it = 0; it < iters; it++)
                {
                    foreach (double step in new[] { 0.5, 0.25, 0.1 })
                    {
                        bool moved = true; int guard = 0;
                        while (moved && guard++ < 40)
                        {
                            moved = false;
                            int nParams = 5 + (fitW ? nb : 0);
                            for (int k = 0; k < nParams; k++)
                            foreach (int sgn in new[] { -1, 1 })
                            {
                                var q = p.Clone();
                                double s = sgn * step;
                                switch (k)
                                {
                                    case 0: q.X += s; break;
                                    case 1: q.Y += s; break;
                                    case 2: q.W += s; break;
                                    case 3: q.H += s; break;
                                    case 4: q.R += s * 2; break;
                                    default: q.Bands[k - 5] = Math.Max(0.3, q.Bands[k - 5] + s); break;
                                }

                                double e = Error(q, false);
                                if (e < best - 1e-6) { best = e; p = q; moved = true; }
                            }
                        }
                    }

                    best = Error(p, true);
                }
            }

            string Hx(double[] c) => Img.Hex((float)c[0], (float)c[1], (float)c[2]);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "rect [{0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}]  radius {4:0.00}  bands {5}  rms {6:0.00}",
                p.X, p.Y, p.W, p.H, p.R, string.Join(";", p.Bands.Select(b => b.ToString("0.00", CultureInfo.InvariantCulture))), best));
            Console.WriteLine("colours (bands, then fill): " + string.Join(" ", Enumerable.Range(0, nb + 1).Select(b => Hx(cols[b]))));
            return 0;
        }
    }
}
