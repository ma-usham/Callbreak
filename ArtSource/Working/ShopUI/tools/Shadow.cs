using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// Soft shadows as their own sprites: the blurred silhouette of a sprite, fitted (blur, offset,
    /// opacity) to the mockup's shadow around the same object, and written padded around it.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageShadow = @"  fitshadow --sprite png --screen png --rect x,y,w,h --bg #hex --color #hex --region x,y,w,h [--exclude ..]
           [--sigma 4] [--dy 3] [--op 0.5]
          blur (sigma, mockup px), offset (dx, dy) and opacity of the sprite's blurred silhouette that
          darken a flat bg like the mockup in the region (the sprite's own pixels are left out)
  shadowsprite --sprite png --out f --sigma s --dx a --dy b --color #hex --op o [--unit 2.3] [--pad 3]
          the shadow sprite (sprite px): the silhouette blurred, shifted, padded by pad x sigma on each
          side; prints the rect to give it (relative to the sprite's own rect)
";

        private static bool ShadowCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "fitshadow": rc = FitShadow(o); return true;
                case "shadowsprite": rc = ShadowSprite(o); return true;
                default: return false;
            }
        }

        /// <summary>Separable gaussian blur of a float field.</summary>
        private static float[] Blur(float[] a, int w, int h, double sigma)
        {
            if (sigma <= 0.01) return (float[])a.Clone();
            int r = (int)Math.Ceiling(sigma * 3);
            var k = new double[2 * r + 1];
            double sum = 0;
            for (int i = -r; i <= r; i++) { k[i + r] = Math.Exp(-i * i / (2 * sigma * sigma)); sum += k[i + r]; }
            for (int i = 0; i < k.Length; i++) k[i] /= sum;
            var tmp = new float[w * h];
            var outp = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double acc = 0;
                for (int i = -r; i <= r; i++) { int xx = x + i; if (xx < 0 || xx >= w) continue; acc += a[y * w + xx] * k[i + r]; }
                tmp[y * w + x] = (float)acc;
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double acc = 0;
                for (int i = -r; i <= r; i++) { int yy = y + i; if (yy < 0 || yy >= h) continue; acc += tmp[yy * w + x] * k[i + r]; }
                outp[y * w + x] = (float)acc;
            }

            return outp;
        }

        private static int FitShadow(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            Img m = Img.Load(o.Need("screen"));
            double[] rect = o.Nums("rect", 4, null);
            int[] reg = o.Ints("region", 4, null);
            var bg = Img.ParseHex(o.Need("bg"));
            var col = Img.ParseHex(o.Need("color"));
            var excl = new List<double[]>();
            if (o.Has("exclude"))
                foreach (string part in o.Get("exclude").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    excl.Add(part.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            // the sprite's alpha at mockup resolution on a padded grid around the region
            int pad = 30;
            int gx = reg[0] - pad, gy = reg[1] - pad, gw = reg[2] + 2 * pad, gh = reg[3] + 2 * pad;
            var (R, G, B, A) = RenderAt(s, rect, gx, gy, gw, gh);
            double bestE = double.MaxValue, bs = o.Num("sigma", 4), bdx = 0, bdy = o.Num("dy", 3), bop = o.Num("op", 0.5);
            var pts = new List<(int gxI, int gyI, float r, float g, float b)>();
            for (int y = 0; y < reg[3]; y++)
            for (int x = 0; x < reg[2]; x++)
            {
                int mx = reg[0] + x, my = reg[1] + y;
                int gi = (y + pad) * gw + x + pad;
                int grow = (int)o.Num("grow", 2);
                bool near = false;
                for (int yy = -grow; yy <= grow && !near; yy++)
                for (int xx = -grow; xx <= grow && !near; xx++)
                {
                    int gj = (y + pad + yy) * gw + x + pad + xx;
                    if (gj >= 0 && gj < A.Length && A[gj] > 0.02f) near = true;
                }

                if (near) continue;
                if (excl.Any(e => mx + 0.5 >= e[0] && mx + 0.5 < e[0] + e[2] && my + 0.5 >= e[1] && my + 0.5 < e[1] + e[3])) continue;
                int k = my * m.W + mx;
                pts.Add((x + pad, y + pad, m.R[k], m.G[k], m.B[k]));
            }

            var cache = new Dictionary<double, float[]>();
            float[] BlurAt(double sg) { if (!cache.TryGetValue(sg, out var b)) { b = Blur(A, gw, gh, sg); cache[sg] = b; } return b; }
            double Err(double sg, double dx, double dy, double op)
            {
                float[] b = BlurAt(Math.Round(sg * 4) / 4);
                double e = 0;
                foreach (var p in pts)
                {
                    double sx = p.gxI - dx, sy = p.gyI - dy;
                    int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
                    double tx = sx - x0, ty = sy - y0;
                    double V(int xx, int yy) => xx < 0 || yy < 0 || xx >= gw || yy >= gh ? 0 : b[yy * gw + xx];
                    double a = op * ((V(x0, y0) * (1 - tx) + V(x0 + 1, y0) * tx) * (1 - ty) + (V(x0, y0 + 1) * (1 - tx) + V(x0 + 1, y0 + 1) * tx) * ty);
                    double pr = bg.r + (col.r - bg.r) * a, pg = bg.g + (col.g - bg.g) * a, pb = bg.b + (col.b - bg.b) * a;
                    double dr = Img.ToSrgb01((float)pr) - Img.ToSrgb01(p.r), dg = Img.ToSrgb01((float)pg) - Img.ToSrgb01(p.g), db = Img.ToSrgb01((float)pb) - Img.ToSrgb01(p.b);
                    e += (dr * dr + dg * dg + db * db) / 3;
                }

                return Math.Sqrt(e / pts.Count) * 255;
            }

            double e0 = Err(bs, 0, 0, 0);
            bestE = Err(bs, bdx, bdy, bop);
            foreach (double step in new[] { 2.0, 1.0, 0.5, 0.25 })
            {
                bool moved = true; int guard = 0;
                while (moved && guard++ < 40)
                {
                    moved = false;
                    foreach (var (a, b2, c2, d2) in new[] { (step, 0.0, 0.0, 0.0), (-step, 0.0, 0.0, 0.0), (0.0, step, 0.0, 0.0), (0.0, -step, 0.0, 0.0), (0.0, 0.0, step, 0.0), (0.0, 0.0, -step, 0.0), (0.0, 0.0, 0.0, step / 10), (0.0, 0.0, 0.0, -step / 10) })
                    {
                        double sg = Math.Max(0.5, bs + a), dx = bdx + b2, dy = bdy + c2, op = Math.Clamp(bop + d2, 0, 1);
                        double e = Err(sg, dx, dy, op);
                        if (e < bestE - 1e-6) { bestE = e; bs = sg; bdx = dx; bdy = dy; bop = op; moved = true; }
                    }
                }
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} px; no shadow {1:0.00}; shadow sigma {2:0.00} dx {3:0.00} dy {4:0.00} op {5:0.000}: {6:0.00} (mockup px, sRGB levels)",
                pts.Count, e0, bs, bdx, bdy, bop, bestE));
            return 0;
        }

        private static int ShadowSprite(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            double unit = o.Num("unit", 2.3);
            double sigma = o.Num("sigma", 4) * unit, dx = o.Num("dx", 0) * unit, dy = o.Num("dy", 0) * unit, op = o.Num("op", 0.5);
            int pad = (int)Math.Ceiling(o.Num("pad", 3) * sigma + Math.Max(Math.Abs(dx), Math.Abs(dy)));
            var col = Img.ParseHex(o.Need("color"));
            int W = s.W + 2 * pad, H = s.H + 2 * pad;
            if (W % 2 == 1) W++;
            if (H % 2 == 1) H++;
            var a = new float[W * H];
            int ox = (int)Math.Round(pad + dx), oy = (int)Math.Round(pad + dy);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int tx = x + ox, ty = y + oy;
                if (tx < 0 || ty < 0 || tx >= W || ty >= H) continue;
                a[ty * W + tx] = s.A[y * s.W + x];
            }

            float[] b = Blur(a, W, H, sigma);
            var n = new Img(W, H, 0f);
            for (int i = 0; i < n.Count; i++) { n.R[i] = col.r; n.G[i] = col.g; n.B[i] = col.b; n.A[i] = (float)Math.Clamp(b[i] * op, 0, 1); }
            n.Save(o.Need("out"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}  {1}x{2}  sigma {3:0.0} px, offset {4:0.0},{5:0.0} px, opacity {6:0.000}; rect = the sprite's rect grown by {7:0.00} mockup px on each side ({8} px)",
                o.Need("out"), W, H, sigma, dx, dy, op, pad / unit, pad));
            return 0;
        }
    }
}
