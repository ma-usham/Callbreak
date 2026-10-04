using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// scene: rebuild a region of the mockup from sprites placed at mockup rects (9-sliced where
    /// given), over the mockup itself or a flat colour, and compare: mockup | rebuild | difference.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageScene = @"  scene --screen png --region x,y,w,h --layers ""spr.png@x,y,w,h[@nine=L,T,R,B][@unit=2.3][@flipx][@op=0.5][@tint=#hex];..""
        [--base mockup|#hex] [--exclude ""x,y,w,h;..""] [--zoom 2] [--out f]
          the sprites composited (in order) at their mockup rects; rms error against the mockup in
          the region; picture: mockup | rebuild | difference x4
";

        private static bool SceneCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            if (cmd != "scene") return false;
            rc = Scene(o);
            return true;
        }

        private sealed class Layer
        {
            public Img S; public double[] Rect; public int[] Nine = { 0, 0, 0, 0 }; public double Unit = 2.3; public bool FlipX; public double Op = 1;
            public (float r, float g, float b)? Tint;
        }

        private static int Scene(Opts o)
        {
            Img m = Img.Load(o.Need("screen"));
            int[] reg = o.Ints("region", 4, null);
            int rw = reg[2], rh = reg[3];
            var layers = new List<Layer>();
            foreach (string spec in o.Need("layers").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = spec.Split('@');
                var L = new Layer { S = Img.Load(q[0]), Rect = q[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray() };
                foreach (string kv in q.Skip(2))
                {
                    if (kv.StartsWith("nine=")) L.Nine = kv.Substring(5).Split(',').Select(int.Parse).ToArray();
                    else if (kv.StartsWith("unit=")) L.Unit = double.Parse(kv.Substring(5), CultureInfo.InvariantCulture);
                    else if (kv == "flipx") L.FlipX = true;
                    else if (kv.StartsWith("op=")) L.Op = double.Parse(kv.Substring(3), CultureInfo.InvariantCulture);
                    else if (kv.StartsWith("tint=")) L.Tint = Img.ParseHex(kv.Substring(5));
                }

                layers.Add(L);
            }

            // base
            var comp = new Img(rw, rh, 1f);
            string bs = o.Get("base", "mockup");
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                int t = y * rw + x, mx = reg[0] + x, my = reg[1] + y;
                if (bs == "mockup") { int k = my * m.W + mx; comp.R[t] = m.R[k]; comp.G[t] = m.G[k]; comp.B[t] = m.B[k]; }
                else { var c = Img.ParseHex(bs); comp.R[t] = c.r; comp.G[t] = c.g; comp.B[t] = c.b; }
            }

            const int ss = 4;
            foreach (var L in layers)
            {
                for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    double ar = 0, ag = 0, ab = 0, aa = 0;
                    for (int j = 0; j < ss; j++)
                    for (int i = 0; i < ss; i++)
                    {
                        double lx = reg[0] + x + (i + 0.5) / ss - L.Rect[0], ly = reg[1] + y + (j + 0.5) / ss - L.Rect[1];
                        if (lx < 0 || ly < 0 || lx >= L.Rect[2] || ly >= L.Rect[3]) continue;
                        if (L.FlipX) lx = L.Rect[2] - lx;
                        double px = NineMap(lx, L.Rect[2], L.S.W, L.Nine[0], L.Nine[2], L.Unit), py = NineMap(ly, L.Rect[3], L.S.H, L.Nine[1], L.Nine[3], L.Unit);
                        // bilinear sample (premultiplied)
                        double fx = px - 0.5, fy = py - 0.5;
                        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                        double tx = fx - x0, ty = fy - y0;
                        for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int sx = Math.Clamp(x0 + dx, 0, L.S.W - 1), sy = Math.Clamp(y0 + dy, 0, L.S.H - 1);
                            double w = (dx == 0 ? 1 - tx : tx) * (dy == 0 ? 1 - ty : ty);
                            int k = sy * L.S.W + sx;
                            double a = L.S.A[k] * w;
                            float cr = L.S.R[k], cg = L.S.G[k], cb = L.S.B[k];
                            if (L.Tint.HasValue) { cr *= L.Tint.Value.r; cg *= L.Tint.Value.g; cb *= L.Tint.Value.b; }
                            ar += cr * a; ag += cg * a; ab += cb * a; aa += a;
                        }
                    }

                    double n = ss * ss;
                    double alpha = aa / n * L.Op;
                    if (alpha <= 0) continue;
                    int t = y * rw + x;
                    double inv = 1 - alpha;
                    comp.R[t] = (float)(ar / n * L.Op + comp.R[t] * inv);
                    comp.G[t] = (float)(ag / n * L.Op + comp.G[t] * inv);
                    comp.B[t] = (float)(ab / n * L.Op + comp.B[t] * inv);
                }
            }

            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            double e2 = 0, e1 = 0; int ne = 0;
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                int mx = reg[0] + x, my = reg[1] + y;
                if (excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3])) continue;
                int t = y * rw + x, k = my * m.W + mx;
                double dr = Img.ToSrgb01(comp.R[t]) - Img.ToSrgb01(m.R[k]), dg = Img.ToSrgb01(comp.G[t]) - Img.ToSrgb01(m.G[k]), db = Img.ToSrgb01(comp.B[t]) - Img.ToSrgb01(m.B[k]);
                e2 += (dr * dr + dg * dg + db * db) / 3; e1 += (Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db)) / 3; ne++;
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "region [{0}]: rms {1:0.00}  mean {2:0.00} (sRGB levels, {3} px)", string.Join(",", reg), Math.Sqrt(e2 / ne) * 255, e1 / ne * 255, ne));
            if (o.Has("out"))
            {
                int z = (int)o.Num("zoom", 2);
                var outImg = new Img(rw * z * 3 + 12, rh * z, 1f);
                for (int i = 0; i < outImg.Count; i++) { outImg.R[i] = 0.05f; outImg.G[i] = 0.05f; outImg.B[i] = 0.05f; }
                for (int y = 0; y < rh * z; y++)
                for (int x = 0; x < rw * z; x++)
                {
                    int sx = x / z, sy = y / z, t = sy * rw + sx, k = (reg[1] + sy) * m.W + reg[0] + sx;
                    Put(outImg, x, y, m.R[k], m.G[k], m.B[k]);
                    Put(outImg, x + rw * z + 6, y, comp.R[t], comp.G[t], comp.B[t]);
                    float d = (Math.Abs(Img.ToSrgb01(comp.R[t]) - Img.ToSrgb01(m.R[k])) + Math.Abs(Img.ToSrgb01(comp.G[t]) - Img.ToSrgb01(m.G[k])) + Math.Abs(Img.ToSrgb01(comp.B[t]) - Img.ToSrgb01(m.B[k]))) / 3 * 4;
                    float dl = Img.ToLinear(Math.Clamp(d, 0, 1));
                    Put(outImg, x + 2 * (rw * z + 6), y, dl, dl, dl);
                }

                outImg.Save(o.Get("out"));
                Console.WriteLine($"  picture: {o.Get("out")} (mockup | rebuild | difference x4)");
            }

            return 0;
        }
    }
}
