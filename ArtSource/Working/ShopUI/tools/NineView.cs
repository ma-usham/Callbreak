using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// nineview: a sprite (optionally 9-sliced) drawn at its mockup rect, composited over the mockup,
    /// and close-ups of chosen spots: mockup above, composite below, zoomed. Also the error there.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageNineView = @"  nineview --sprite png --screen png --rect x,y,w,h [--nine L,T,R,B --unit 2.3] [--spots ""x,y;x,y""|corners] [--size 40]
           [--zoom 8] [--flipx] --out f
          the sprite at its rect over the mockup; close-ups (size x size mockup px) of the spots:
          mockup on top, composite below; prints the rms error in each close-up
";

        private static bool NineViewCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            if (cmd != "nineview") return false;
            rc = NineView(o);
            return true;
        }

        /// <summary>Sprite pixel coordinate for a point inside the rect, through the 9-slice mapping.</summary>
        private static double NineMap(double t, double len, double sLen, double b0, double b1, double unit)
        {
            // t: offset in mockup px from the rect's start; len: rect length (mockup px); sLen: sprite length (px)
            if (b0 <= 0 && b1 <= 0) return t / len * sLen;
            double d0 = b0 / unit, d1 = b1 / unit;           // border sizes in mockup px
            if (d0 + d1 > len) { double f = len / (d0 + d1); d0 *= f; d1 *= f; }
            if (t < d0) return t / d0 * b0;
            if (t > len - d1) return sLen - (len - t) / d1 * b1;
            double mid = len - d0 - d1, sMid = sLen - b0 - b1;
            return b0 + (mid > 0 ? (t - d0) / mid * sMid : 0);
        }

        private static int NineView(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            Img m = Img.Load(o.Need("screen"));
            double[] rect = o.Nums("rect", 4, null);
            int[] nine = o.Has("nine") ? o.Ints("nine", 4, null) : new[] { 0, 0, 0, 0 };
            double unit = o.Num("unit", 2.3);
            bool flipx = o.Has("flipx");
            int size = (int)o.Num("size", 40), z = (int)o.Num("zoom", 8);
            var spots = new List<double[]>();
            string sp = o.Get("spots", "corners");
            if (sp == "corners")
            {
                double h = size / 2.0 - 6;
                spots.Add(new[] { rect[0] + h, rect[1] + h });
                spots.Add(new[] { rect[0] + rect[2] - h, rect[1] + h });
                spots.Add(new[] { rect[0] + h, rect[1] + rect[3] - h });
                spots.Add(new[] { rect[0] + rect[2] - h, rect[1] + rect[3] - h });
            }
            else
                foreach (string q in sp.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    spots.Add(q.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());

            // composite of the whole rect region (+ margin) at mockup resolution
            int rx = (int)Math.Floor(rect[0]) - 2, ry = (int)Math.Floor(rect[1]) - 2;
            int rw = (int)Math.Ceiling(rect[2]) + 5, rh = (int)Math.Ceiling(rect[3]) + 5;
            var comp = m.Crop(Math.Max(0, rx), Math.Max(0, ry), Math.Min(rw, m.W - Math.Max(0, rx)), Math.Min(rh, m.H - Math.Max(0, ry)));
            rx = Math.Max(0, rx); ry = Math.Max(0, ry);
            const int ss = 4;
            for (int y = 0; y < comp.H; y++)
            for (int x = 0; x < comp.W; x++)
            {
                double ar = 0, ag = 0, ab = 0, aa = 0;
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double mx = rx + x + (i + 0.5) / ss - rect[0], my = ry + y + (j + 0.5) / ss - rect[1];
                    if (mx < 0 || my < 0 || mx >= rect[2] || my >= rect[3]) continue;
                    if (flipx) mx = rect[2] - mx;
                    double px = NineMap(mx, rect[2], s.W, nine[0], nine[2], unit), py = NineMap(my, rect[3], s.H, nine[1], nine[3], unit);
                    int kx = Math.Clamp((int)Math.Floor(px), 0, s.W - 1), ky = Math.Clamp((int)Math.Floor(py), 0, s.H - 1);
                    int k = ky * s.W + kx;
                    float a = s.A[k];
                    ar += s.R[k] * a; ag += s.G[k] * a; ab += s.B[k] * a; aa += a;
                }

                int t = y * comp.W + x;
                double n = ss * ss;
                double alpha = aa / n;
                if (alpha <= 0) continue;
                comp.R[t] = (float)(ar / n + comp.R[t] * (1 - alpha));
                comp.G[t] = (float)(ag / n + comp.G[t] * (1 - alpha));
                comp.B[t] = (float)(ab / n + comp.B[t] * (1 - alpha));
            }

            int tile = size * z;
            var outImg = new Img(spots.Count * (tile + 6), 2 * tile + 6, 1f);
            for (int i = 0; i < outImg.Count; i++) { outImg.R[i] = 0.05f; outImg.G[i] = 0.05f; outImg.B[i] = 0.05f; }
            for (int si = 0; si < spots.Count; si++)
            {
                double x0 = spots[si][0] - size / 2.0, y0 = spots[si][1] - size / 2.0;
                double e2 = 0; int ne = 0;
                for (int y = 0; y < tile; y++)
                for (int x = 0; x < tile; x++)
                {
                    int mx = (int)Math.Floor(x0 + (x + 0.5) / z), my = (int)Math.Floor(y0 + (y + 0.5) / z);
                    if (mx < 0 || my < 0 || mx >= m.W || my >= m.H) continue;
                    int k = my * m.W + mx;
                    Put(outImg, si * (tile + 6) + x, y, m.R[k], m.G[k], m.B[k]);
                    int cx = mx - rx, cy = my - ry;
                    float r = m.R[k], g = m.G[k], b = m.B[k];
                    if (cx >= 0 && cy >= 0 && cx < comp.W && cy < comp.H) { int c = cy * comp.W + cx; r = comp.R[c]; g = comp.G[c]; b = comp.B[c]; }
                    Put(outImg, si * (tile + 6) + x, y + tile + 6, r, g, b);
                    if (x % z == 0 && y % z == 0)
                    {
                        double dr = Img.ToSrgb01(r) - Img.ToSrgb01(m.R[k]), dg = Img.ToSrgb01(g) - Img.ToSrgb01(m.G[k]), db = Img.ToSrgb01(b) - Img.ToSrgb01(m.B[k]);
                        e2 += (dr * dr + dg * dg + db * db) / 3; ne++;
                    }
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  spot {0} ({1:0.#},{2:0.#}): rms {3:0.0} levels", si + 1, spots[si][0], spots[si][1], ne > 0 ? Math.Sqrt(e2 / ne) * 255 : 0));
            }

            outImg.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  (top: mockup, bottom: sprite composited at its rect)");
            return 0;
        }
    }
}
