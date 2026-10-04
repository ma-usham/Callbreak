using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// Colour checks: the sprite drawn at its mockup rect (area-sampled, mockup resolution) against
    /// the mockup pixels it covers, on the pixels where the sprite is opaque.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageColors = @"  colorcmp --sprite png --screen png --rect x,y,w,h [--minalpha 0.98] [--exclude ""x,y,w,h;..""] [--mask lum<v|lum>v]
          per-channel mean and 10/50/90 % quantiles of sprite vs mockup where the sprite is opaque,
          the mean difference, and curve anchors (kc curve) that would put the sprite on the mockup
";

        private static bool ColorCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "colorcmp": rc = ColorCmp(o); return true;
                default: return false;
            }
        }

        /// <summary>The sprite rendered onto the mockup grid: premultiplied area average per mockup pixel.</summary>
        private static (float[] r, float[] g, float[] b, float[] a) RenderAt(Img s, double[] rect, int rx, int ry, int rw, int rh)
        {
            var R = new float[rw * rh]; var G = new float[rw * rh]; var B = new float[rw * rh]; var A = new float[rw * rh];
            double sx = s.W / rect[2], sy = s.H / rect[3];
            int ns = Math.Max(2, (int)Math.Ceiling(Math.Max(sx, sy)));
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                double ar = 0, ag = 0, ab = 0, aa = 0;
                for (int j = 0; j < ns; j++)
                for (int i = 0; i < ns; i++)
                {
                    double mx = rx + x + (i + 0.5) / ns, my = ry + y + (j + 0.5) / ns;
                    int px = (int)Math.Floor((mx - rect[0]) * sx), py = (int)Math.Floor((my - rect[1]) * sy);
                    if (px < 0 || py < 0 || px >= s.W || py >= s.H) continue;
                    int k = py * s.W + px;
                    float a = s.A[k];
                    ar += s.R[k] * a; ag += s.G[k] * a; ab += s.B[k] * a; aa += a;
                }

                int t = y * rw + x;
                double n = ns * ns;
                A[t] = (float)(aa / n);
                if (aa > 0) { R[t] = (float)(ar / aa); G[t] = (float)(ag / aa); B[t] = (float)(ab / aa); }
            }

            return (R, G, B, A);
        }

        private static int ColorCmp(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            Img m = Img.Load(o.Need("screen"));
            double[] rect = o.Nums("rect", 4, null);
            float minA = (float)o.Num("minalpha", 0.98);
            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            int rx = (int)Math.Floor(rect[0]), ry = (int)Math.Floor(rect[1]);
            int rw = (int)Math.Ceiling(rect[0] + rect[2]) - rx, rh = (int)Math.Ceiling(rect[1] + rect[3]) - ry;
            var (R, G, B, A) = RenderAt(s, rect, rx, ry, rw, rh);
            var sv = new List<double>[3] { new(), new(), new() };
            var mv = new List<double>[3] { new(), new(), new() };
            var dl = new List<double>();
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                int t = y * rw + x;
                if (A[t] < minA) continue;
                int mx = rx + x, my = ry + y;
                if (mx < 0 || my < 0 || mx >= m.W || my >= m.H) continue;
                if (excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3])) continue;
                int k = my * m.W + mx;
                double[] a = { Img.ToSrgbByte(R[t]), Img.ToSrgbByte(G[t]), Img.ToSrgbByte(B[t]) };
                double[] b = { Img.ToSrgbByte(m.R[k]), Img.ToSrgbByte(m.G[k]), Img.ToSrgbByte(m.B[k]) };
                for (int c = 0; c < 3; c++) { sv[c].Add(a[c]); mv[c].Add(b[c]); }
                dl.Add(Math.Sqrt(((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2])) / 3));
            }

            if (dl.Count == 0) { Console.WriteLine("no opaque pixels in the rect"); return 1; }
            double Q(List<double> l, double q) { var c = l.OrderBy(v => v).ToList(); return c[(int)Math.Clamp(q * (c.Count - 1), 0, c.Count - 1)]; }
            string Hex3(Func<int, double> f) => "#" + string.Concat(Enumerable.Range(0, 3).Select(c => ((int)Math.Round(Math.Clamp(f(c), 0, 255))).ToString("X2")));
            Console.WriteLine($"{System.IO.Path.GetFileName(o.Need("sprite"))} on {System.IO.Path.GetFileName(o.Need("screen"))} [{string.Join(", ", rect.Select(v => v.ToString("0.##", CultureInfo.InvariantCulture)))}]: {dl.Count} opaque px");
            Console.WriteLine($"  mean   sprite {Hex3(c => sv[c].Average())}  mockup {Hex3(c => mv[c].Average())}  diff (sprite - mockup) R {sv[0].Average() - mv[0].Average():+0.0;-0.0} G {sv[1].Average() - mv[1].Average():+0.0;-0.0} B {sv[2].Average() - mv[2].Average():+0.0;-0.0}");
            foreach (double q in new[] { 0.1, 0.5, 0.9 })
                Console.WriteLine($"  q{q * 100,2:0}    sprite {Hex3(c => Q(sv[c], q))}  mockup {Hex3(c => Q(mv[c], q))}");
            Console.WriteLine($"  pixel rms (sRGB levels, per channel) {Math.Sqrt(dl.Select(v => v * v).Average()):0.0}  mean {dl.Average():0.0}");
            Console.WriteLine($"  anchors \"{Hex3(c => Q(sv[c], 0.1))}>{Hex3(c => Q(mv[c], 0.1))};{Hex3(c => Q(sv[c], 0.5))}>{Hex3(c => Q(mv[c], 0.5))};{Hex3(c => Q(sv[c], 0.9))}>{Hex3(c => Q(mv[c], 0.9))}\"");
            return 0;
        }
    }
}
