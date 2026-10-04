using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// A light glow (drawn by ChatGPT on black) placed behind something on the mockup: fit its centre,
    /// size and strength against the mockup's light around the object, then write the sprite
    /// (one warm colour, alpha from the glow's brightness).
    /// </summary>
    internal static partial class Program
    {
        private const string UsageGlow = @"  fitglow --glow png --screen png --center cx,cy --size S --fill #hex --color #hex --region x,y,w,h
           [--exclude ""x,y,w,h;..""] [--mask sprite.png@x,y,w,h] [--k 0.5]
          centre, size (mockup px) and strength k of the glow (alpha = k x brightness) that lights the fill
          like the mockup inside the region, leaving out the excluded rects and the opaque pixels of mask
  glowsprite --glow png --out f --size S --k 0.5 --color #hex [--unit 2.3] [--gamma 1]
          the glow sprite: colour, alpha = k x brightness, S x unit px square
";

        private static bool GlowCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "fitglow": rc = FitGlow(o); return true;
                case "glowsprite": rc = GlowSprite(o); return true;
                default: return false;
            }
        }

        private static float SampleL(Img g, double u, double v)
        {
            // u, v in 0..1; bilinear on luminance
            double fx = u * g.W - 0.5, fy = v * g.H - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            float L(int x, int y) => x < 0 || y < 0 || x >= g.W || y >= g.H ? 0f : g.Lum(y * g.W + x);
            return (float)((L(x0, y0) * (1 - tx) + L(x0 + 1, y0) * tx) * (1 - ty) + (L(x0, y0 + 1) * (1 - tx) + L(x0 + 1, y0 + 1) * tx) * ty);
        }

        private static int FitGlow(Opts o)
        {
            Img g = Img.Load(o.Need("glow"));
            Img m = Img.Load(o.Need("screen"));
            double[] c = o.Nums("center", 2, null);
            double S = o.Num("size", 200), k = o.Num("k", 0.5);
            var fill = Img.ParseHex(o.Need("fill"));
            var col = Img.ParseHex(o.Need("color"));
            int[] reg = o.Ints("region", 4, null);
            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            // mask: an opaque sprite placed at a rect (the coin pile): its pixels are left out
            bool[] masked = new bool[reg[2] * reg[3]];
            if (o.Has("mask"))
            {
                string[] q = o.Get("mask").Split('@');
                Img ms = Img.Load(q[0]);
                double[] r = q[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                for (int y = 0; y < reg[3]; y++)
                for (int x = 0; x < reg[2]; x++)
                {
                    double mx = reg[0] + x + 0.5, my = reg[1] + y + 0.5;
                    double u = (mx - r[0]) / r[2], v = (my - r[1]) / r[3];
                    // grow the mask by ~2 px: test the neighbourhood
                    bool hit = false;
                    for (int dy = -2; dy <= 2 && !hit; dy++)
                    for (int dx = -2; dx <= 2 && !hit; dx++)
                    {
                        double uu = (mx + dx - r[0]) / r[2], vv = (my + dy - r[1]) / r[3];
                        if (uu < 0 || vv < 0 || uu >= 1 || vv >= 1) continue;
                        int px = (int)(uu * ms.W), py = (int)(vv * ms.H);
                        if (ms.A[py * ms.W + px] > 0.05f) hit = true;
                    }

                    masked[y * reg[2] + x] = hit;
                }
            }

            var pts = new List<(double x, double y, float r, float gg, float b)>();
            for (int y = 0; y < reg[3]; y++)
            for (int x = 0; x < reg[2]; x++)
            {
                if (masked[y * reg[2] + x]) continue;
                int mx = reg[0] + x, my = reg[1] + y;
                if (excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3])) continue;
                int i = my * m.W + mx;
                pts.Add((mx + 0.5, my + 0.5, m.R[i], m.G[i], m.B[i]));
            }

            double Err(double cx, double cy, double s, double kk)
            {
                double e = 0;
                foreach (var p in pts)
                {
                    double u = (p.x - (cx - s / 2)) / s, v = (p.y - (cy - s / 2)) / s;
                    double a = (u < 0 || v < 0 || u >= 1 || v >= 1) ? 0 : Math.Min(1, kk * SampleL(g, u, v));
                    double pr = fill.r + (col.r - fill.r) * a, pg = fill.g + (col.g - fill.g) * a, pb = fill.b + (col.b - fill.b) * a;
                    double dr = Img.ToSrgb01((float)pr) - Img.ToSrgb01(p.r), dg = Img.ToSrgb01((float)pg) - Img.ToSrgb01(p.gg), db = Img.ToSrgb01((float)pb) - Img.ToSrgb01(p.b);
                    e += (dr * dr + dg * dg + db * db) / 3;
                }

                return Math.Sqrt(e / pts.Count) * 255;
            }

            double best = Err(c[0], c[1], S, 0);
            Console.WriteLine($"{pts.Count} px; error with no glow: {best:0.00}");
            double bx = c[0], by = c[1], bs = S, bk = k;
            best = Err(bx, by, bs, bk);
            foreach (double step in new[] { 8.0, 4.0, 2.0, 1.0, 0.5 })
            {
                bool moved = true; int guard = 0;
                while (moved && guard++ < 30)
                {
                    moved = false;
                    bool fixS = o.Has("fixsize");
                    foreach (var (dx, dy, ds0, dk) in new[] { (step, 0.0, 0.0, 0.0), (-step, 0.0, 0.0, 0.0), (0.0, step, 0.0, 0.0), (0.0, -step, 0.0, 0.0),
                                                             (0.0, 0.0, 2 * step, 0.0), (0.0, 0.0, -2 * step, 0.0), (0.0, 0.0, 0.0, step / 40), (0.0, 0.0, 0.0, -step / 40) })
                    {
                        double ds = fixS ? 0 : ds0;
                        double e = Err(bx + dx, by + dy, Math.Max(20, bs + ds), Math.Max(0, bk + dk));
                        if (e < best - 1e-6) { best = e; bx += dx; by += dy; bs = Math.Max(20, bs + ds); bk = Math.Max(0, bk + dk); moved = true; }
                    }
                }
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "glow: centre {0:0.0},{1:0.0}  size {2:0.0}  k {3:0.000}  error {4:0.00}  -> rect [{5:0.0}, {6:0.0}, {2:0.0}, {2:0.0}]",
                bx, by, bs, bk, best, bx - bs / 2, by - bs / 2));
            return 0;
        }

        private static int GlowSprite(Opts o)
        {
            Img g = Img.Load(o.Need("glow"));
            double S = o.Num("size", 200), k = o.Num("k", 0.5), unit = o.Num("unit", 2.3);
            var col = Img.ParseHex(o.Need("color"));
            int N = (int)Math.Ceiling(S * unit);
            if (N % 2 == 1) N++;
            var n = new Img(N, N, 0f);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // area-average the glow over this pixel's footprint
                double acc = 0; int cnt = 0;
                int f = Math.Max(1, (int)Math.Ceiling((double)g.W / N));
                for (int j = 0; j < f; j++)
                for (int i = 0; i < f; i++) { acc += SampleL(g, (x + (i + 0.5) / f) / N, (y + (j + 0.5) / f) / N); cnt++; }
                int t = y * N + x;
                n.R[t] = col.r; n.G[t] = col.g; n.B[t] = col.b;
                n.A[t] = (float)Math.Clamp(k * Math.Pow(Math.Max(0, acc / cnt), o.Num("gamma", 1)), 0, 1);
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {N}x{N}  k {k}");
            return 0;
        }
    }
}
