using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// fitlayer: the rect (position and uniform size, or free w/h) at which one sprite composited over the
    /// mockup reproduces the mockup best, leaving out excluded rects (e.g. the icon on a button).
    /// </summary>
    internal static partial class Program
    {
        private const string UsageFitLayer = @"  fitlayer --sprite png --screen png --rect x,y,w,h [--nine L,T,R,B --unit 2.3] [--exclude ""x,y,w,h;..""] [--free] [--pad 6]
          position and size (uniform unless --free) of the sprite over the mockup with the lowest rms
";

        private static bool FitLayerCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            if (cmd != "fitlayer") return false;
            rc = FitLayer(o);
            return true;
        }

        private static int FitLayer(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            Img m = Img.Load(o.Need("screen"));
            double[] r0 = o.Nums("rect", 4, null);
            int[] nine = o.Has("nine") ? o.Ints("nine", 4, null) : new[] { 0, 0, 0, 0 };
            double unit = o.Num("unit", 2.3);
            bool free = o.Has("free");
            (float r, float g, float b)? ground = o.Has("ground") ? Img.ParseHex(o.Get("ground")) : null;
            int pad = (int)o.Num("pad", 6);
            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            int rx = (int)Math.Floor(r0[0]) - pad, ry = (int)Math.Floor(r0[1]) - pad;
            int rw = (int)Math.Ceiling(r0[2]) + 2 * pad, rh = (int)Math.Ceiling(r0[3]) + 2 * pad;
            var keep = new bool[rw * rh];
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                int mx = rx + x, my = ry + y;
                keep[y * rw + x] = mx >= 0 && my >= 0 && mx < m.W && my < m.H &&
                                   !excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3]);
            }

            double Loss(double[] r)
            {
                double e = 0; int n = 0;
                const int ss = 3;
                for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    if (!keep[y * rw + x]) continue;
                    int mx = rx + x, my = ry + y, k = my * m.W + mx;
                    double ar = 0, ag = 0, ab = 0, aa = 0;
                    for (int j = 0; j < ss; j++)
                    for (int i = 0; i < ss; i++)
                    {
                        double lx = mx + (i + 0.5) / ss - r[0], ly = my + (j + 0.5) / ss - r[1];
                        if (lx < 0 || ly < 0 || lx >= r[2] || ly >= r[3]) continue;
                        double px = NineMap(lx, r[2], s.W, nine[0], nine[2], unit), py = NineMap(ly, r[3], s.H, nine[1], nine[3], unit);
                        int sx = Math.Clamp((int)px, 0, s.W - 1), sy = Math.Clamp((int)py, 0, s.H - 1), q = sy * s.W + sx;
                        float a = s.A[q];
                        ar += s.R[q] * a; ag += s.G[q] * a; ab += s.B[q] * a; aa += a;
                    }

                    double nn = ss * ss, al = aa / nn;
                    float br = ground.HasValue ? ground.Value.r : m.R[k], bg = ground.HasValue ? ground.Value.g : m.G[k], bb = ground.HasValue ? ground.Value.b : m.B[k];
                    double cr = ar / nn + br * (1 - al), cg = ag / nn + bg * (1 - al), cb = ab / nn + bb * (1 - al);
                    double dr = Img.ToSrgb01((float)cr) - Img.ToSrgb01(m.R[k]), dg = Img.ToSrgb01((float)cg) - Img.ToSrgb01(m.G[k]), db = Img.ToSrgb01((float)cb) - Img.ToSrgb01(m.B[k]);
                    e += (dr * dr + dg * dg + db * db) / 3; n++;
                }

                return Math.Sqrt(e / Math.Max(1, n)) * 255;
            }

            double[] best = (double[])r0.Clone();
            double bestL = Loss(best), startL = bestL;
            foreach (double step in new[] { 1.0, 0.5, 0.25, 0.1 })
            {
                bool moved = true; int guard = 0;
                while (moved && guard++ < 40)
                {
                    moved = false;
                    var cands = new List<double[]>();
                    foreach (int sg in new[] { -1, 1 })
                    {
                        var c = (double[])best.Clone(); c[0] += sg * step; cands.Add(c);
                        c = (double[])best.Clone(); c[1] += sg * step; cands.Add(c);
                        if (free)
                        {
                            c = (double[])best.Clone(); c[2] += sg * step; c[0] -= sg * step / 2; cands.Add(c);
                            c = (double[])best.Clone(); c[3] += sg * step; c[1] -= sg * step / 2; cands.Add(c);
                        }
                        else
                        {
                            c = (double[])best.Clone();
                            double f = 1 + sg * step / Math.Max(c[2], c[3]);
                            double cx = c[0] + c[2] / 2, cy = c[1] + c[3] / 2;
                            c[2] *= f; c[3] *= f; c[0] = cx - c[2] / 2; c[1] = cy - c[3] / 2;
                            cands.Add(c);
                        }
                    }

                    foreach (var c in cands)
                    {
                        double l = Loss(c);
                        if (l < bestL - 1e-6) { bestL = l; best = c; moved = true; }
                    }
                }
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "fit {0}: rect [{1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}]  rms {5:0.00} (start {6:0.00})",
                System.IO.Path.GetFileName(o.Need("sprite")), best[0], best[1], best[2], best[3], bestL, startL));
            return 0;
        }
    }
}
