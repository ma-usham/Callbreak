using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// Flat icons: fit a ChatGPT matte onto the mockup's own drawing of the icon (alpha against alpha),
    /// and render the final sprite at @2x in one flat ink.
    /// </summary>
    internal static partial class Program
    {
        private const string UsageIcons = @"  icomatte --in cell.png --out matte.png [--ground #FFFFFF] [--ink #hex] [--trim 2]
          alpha of a flat dark icon drawn on a light ground (from the ink/ground mix), trimmed
  fiticon  --sprite matte.png --screen png --rect x,y,w,h --ground #hex --ink #hex [--search 4] [--pad 6]
           [--keepratio] [--out check.png] [--zoom 8]
          the rect (mockup px) at which the matte lands on the mockup's icon; IoU and alpha error
  rendericon --sprite matte.png --out f.png --rect x,y,w,h --ink #hex [--unit 2.3] [--margin 2] [--grow 0]
          the sprite at the fitted rect's size x unit, one flat ink, even size, clear margin
";

        private static bool IconCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "icomatte": rc = IcoMatte(o); return true;
                case "fiticon": rc = FitIcon(o); return true;
                case "rendericon": rc = RenderIcon(o); return true;
                default: return false;
            }
        }

        // ---------------------------------------------------------------------------------------
        private static int IcoMatte(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var g = Img.ParseHex(o.Get("ground", "#FFFFFF"));
            float gl = 0.2126f * g.r + 0.7152f * g.g + 0.0722f * g.b;
            // Ink: the darkest 2% of the cell unless given.
            float il;
            if (o.Has("ink"))
            {
                var ink = Img.ParseHex(o.Get("ink"));
                il = 0.2126f * ink.r + 0.7152f * ink.g + 0.0722f * ink.b;
            }
            else
            {
                var lum = new List<float>();
                for (int i = 0; i < m.Count; i++) lum.Add(m.Lum(i));
                lum.Sort();
                il = lum[(int)(lum.Count * 0.02)];
            }

            var n = new Img(m.W, m.H, 0f);
            for (int i = 0; i < m.Count; i++)
            {
                float a = Math.Clamp((gl - m.Lum(i)) / Math.Max(1e-5f, gl - il), 0f, 1f);
                if (a < 0.02f) a = 0f;
                if (a > 0.98f) a = 1f;
                n.A[i] = a;
                n.R[i] = 0; n.G[i] = 0; n.B[i] = 0;
            }

            // trim to the alpha box + margin
            int pad = (int)o.Num("trim", 2);
            int x0 = n.W, y0 = n.H, x1 = -1, y1 = -1;
            for (int y = 0; y < n.H; y++)
            for (int x = 0; x < n.W; x++)
            {
                if (n.A[y * n.W + x] <= 0.02f) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }

            x0 = Math.Max(0, x0 - pad); y0 = Math.Max(0, y0 - pad);
            x1 = Math.Min(n.W - 1, x1 + pad); y1 = Math.Min(n.H - 1, y1 + pad);
            Img c = n.Crop(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            c.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {c.W}x{c.H}  ink lum {Img.ToSrgbByte(il)} ground lum {Img.ToSrgbByte(gl)}  (box {x0},{y0})");
            return 0;
        }

        /// <summary>Ink box of a matte (alpha above 0.5, sub-pixel by the alpha profile at the extremes).</summary>
        private static (double x0, double y0, double x1, double y1) InkBox(Img s)
        {
            double x0 = s.W, y0 = s.H, x1 = 0, y1 = 0;
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                float a = s.A[y * s.W + x];
                if (a <= 0.02f) continue;
                // a pixel with alpha a covers about a of its width at the edge
                x0 = Math.Min(x0, x + 1 - a); x1 = Math.Max(x1, x + a);
                y0 = Math.Min(y0, y + 1 - a); y1 = Math.Max(y1, y + a);
            }

            return (x0, y0, x1, y1);
        }

        /// <summary>The matte's ink box mapped onto rect: alpha sampled per mockup pixel (4x4 supersampling).</summary>
        private static float[] Splat(Img s, (double x0, double y0, double x1, double y1) box, double[] rect, int rx, int ry, int rw, int rh)
        {
            var a = new float[rw * rh];
            double sx = (box.x1 - box.x0) / rect[2], sy = (box.y1 - box.y0) / rect[3];
            const int ss = 4;
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                double acc = 0;
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double mx = rx + x + (i + 0.5) / ss, my = ry + y + (j + 0.5) / ss;
                    double px = box.x0 + (mx - rect[0]) * sx, py = box.y0 + (my - rect[1]) * sy;
                    acc += SampleA(s, px, py);
                }

                a[y * rw + x] = (float)(acc / (ss * ss));
            }

            return a;
        }

        private static double SampleA(Img s, double px, double py)
        {
            // bilinear, pixel centres at +0.5
            double fx = px - 0.5, fy = py - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            double v00 = A(s, x0, y0), v10 = A(s, x0 + 1, y0), v01 = A(s, x0, y0 + 1), v11 = A(s, x0 + 1, y0 + 1);
            return (v00 * (1 - tx) + v10 * tx) * (1 - ty) + (v01 * (1 - tx) + v11 * tx) * ty;
        }

        private static double A(Img s, int x, int y) => x < 0 || y < 0 || x >= s.W || y >= s.H ? 0 : s.A[y * s.W + x];

        private static int FitIcon(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            Img m = Img.Load(o.Need("screen"));
            double[] rect = o.Nums("rect", 4, null);
            var g = Img.ParseHex(o.Need("ground"));
            var ink = Img.ParseHex(o.Need("ink"));
            float gl = 0.2126f * g.r + 0.7152f * g.g + 0.0722f * g.b, il = 0.2126f * ink.r + 0.7152f * ink.g + 0.0722f * ink.b;
            double search = o.Num("search", 4);
            int pad = (int)o.Num("pad", 6);
            bool keep = o.Has("keepratio");
            var box = InkBox(s);

            int rx = (int)Math.Floor(rect[0] - pad - search), ry = (int)Math.Floor(rect[1] - pad - search);
            int rw = (int)Math.Ceiling(rect[2] + 2 * (pad + search)) + 1, rh = (int)Math.Ceiling(rect[3] + 2 * (pad + search)) + 1;
            var ma = new float[rw * rh];
            for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                int mx = rx + x, my = ry + y;
                if (mx < 0 || my < 0 || mx >= m.W || my >= m.H) continue;
                float l = m.Lum(my * m.W + mx);
                ma[y * rw + x] = Math.Clamp((l - gl) / (il - gl), 0f, 1f);
            }

            double Loss(double[] r)
            {
                float[] sa = Splat(s, box, r, rx, ry, rw, rh);
                double e = 0;
                for (int i = 0; i < sa.Length; i++) { double d = sa[i] - ma[i]; e += d * d; }
                return e;
            }

            double[] best = (double[])rect.Clone();
            double bestL = Loss(best);
            double startL = bestL;
            foreach (double step in new[] { 1.0, 0.5, 0.25, 0.1, 0.05 })
            {
                bool moved = true;
                int guard = 0;
                while (moved && guard++ < 60)
                {
                    moved = false;
                    var cands = new List<double[]>();
                    for (int k = 0; k < 4; k++)
                    {
                        foreach (int sgn in new[] { -1, 1 })
                        {
                            var c = (double[])best.Clone();
                            if (k < 2) c[k] += sgn * step;
                            else if (keep)
                            {
                                // grow about the centre, keeping the ratio
                                double f = 1 + sgn * step / Math.Max(c[2], c[3]);
                                double cx = c[0] + c[2] / 2, cy = c[1] + c[3] / 2;
                                c[2] *= f; c[3] *= f; c[0] = cx - c[2] / 2; c[1] = cy - c[3] / 2;
                                if (k == 3) continue;
                            }
                            else { c[k] += sgn * step; c[k - 2] -= sgn * step / 2; }
                            if (Math.Abs(c[0] - rect[0]) > search + 1e-9 || Math.Abs(c[1] - rect[1]) > search + 1e-9) continue;
                            if (Math.Abs(c[2] - rect[2]) > 2 * search + 1e-9 || Math.Abs(c[3] - rect[3]) > 2 * search + 1e-9) continue;
                            cands.Add(c);
                        }
                    }

                    foreach (var c in cands)
                    {
                        double l = Loss(c);
                        if (l < bestL - 1e-9) { bestL = l; best = c; moved = true; }
                    }
                }
            }

            float[] fa = Splat(s, box, best, rx, ry, rw, rh);
            double inter = 0, uni = 0, abs = 0;
            for (int i = 0; i < fa.Length; i++)
            {
                bool p = fa[i] > 0.5f, q = ma[i] > 0.5f;
                if (p && q) inter++;
                if (p || q) uni++;
                abs += Math.Abs(fa[i] - ma[i]);
            }

            double iou = uni > 0 ? inter / uni : 0;
            double rms = Math.Sqrt(bestL / fa.Length);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "fit {0}: rect [{1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}]  w:h {5:0.000} (sprite {6:0.000})  IoU {7:0.000}  alpha rms {8:0.000} (start {9:0.000})  mean |da| {10:0.0000}",
                Path.GetFileName(o.Need("sprite")), best[0], best[1], best[2], best[3], best[2] / best[3], (box.x1 - box.x0) / (box.y1 - box.y0), iou, rms,
                Math.Sqrt(startL / fa.Length), abs / fa.Length));

            if (o.Has("out"))
            {
                int z = (int)o.Num("zoom", 8);
                // panels: mockup | sprite in the mockup's ink on its ground | alpha difference (red = sprite only, green = mockup only)
                var outImg = new Img(rw * z * 3 + 2 * 4, rh * z, 1f);
                for (int i = 0; i < outImg.Count; i++) { outImg.R[i] = 0.2f; outImg.G[i] = 0.2f; outImg.B[i] = 0.2f; }
                for (int y = 0; y < rh * z; y++)
                for (int x = 0; x < rw * z; x++)
                {
                    int sx = x / z, sy = y / z, si = sy * rw + sx;
                    int mx = rx + sx, my = ry + sy;
                    float r0 = 0, g0 = 0, b0 = 0;
                    if (mx >= 0 && my >= 0 && mx < m.W && my < m.H) { int mi = my * m.W + mx; r0 = m.R[mi]; g0 = m.G[mi]; b0 = m.B[mi]; }
                    Put(outImg, x, y, r0, g0, b0);
                    float a = fa[si];
                    Put(outImg, x + rw * z + 4, y, g.r + (ink.r - g.r) * a, g.g + (ink.g - g.g) * a, g.b + (ink.b - g.b) * a);
                    float d = fa[si] - ma[si];
                    float base0 = Math.Min(fa[si], ma[si]) * 0.35f;
                    Put(outImg, x + 2 * (rw * z + 4), y, Math.Clamp(base0 + Math.Max(0, d), 0, 1), Math.Clamp(base0 + Math.Max(0, -d), 0, 1), base0);
                }

                outImg.Save(o.Get("out"));
                Console.WriteLine($"  check: {o.Get("out")}  (mockup | sprite at the fitted rect | red = sprite only, green = mockup only)");
            }

            return 0;
        }

        private static void Put(Img img, int x, int y, float r, float g, float b)
        {
            if (x < 0 || y < 0 || x >= img.W || y >= img.H) return;
            int i = y * img.W + x;
            img.R[i] = r; img.G[i] = g; img.B[i] = b; img.A[i] = 1f;
        }

        private static int RenderIcon(Opts o)
        {
            Img s = Img.Load(o.Need("sprite"));
            double[] rect = o.Nums("rect", 4, null);
            double unit = o.Num("unit", 2.3);
            int margin = (int)o.Num("margin", 2);
            var ink = Img.ParseHex(o.Need("ink"));
            var box = InkBox(s);
            double w = rect[2] * unit, h = rect[3] * unit;
            int W = (int)Math.Ceiling(w + 2 * margin), H = (int)Math.Ceiling(h + 2 * margin);
            if (W % 2 == 1) W++;
            if (H % 2 == 1) H++;
            double ox = (W - w) / 2, oy = (H - h) / 2;
            var n = new Img(W, H, 0f);
            double sx = (box.x1 - box.x0) / w, sy = (box.y1 - box.y0) / h;
            const int ss = 4;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double acc = 0;
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double px = box.x0 + (x + (i + 0.5) / ss - ox) * sx, py = box.y0 + (y + (j + 0.5) / ss - oy) * sy;
                    acc += AreaA(s, px, py, sx, sy);
                }

                int k = y * W + x;
                n.A[k] = (float)Math.Clamp(acc / (ss * ss), 0, 1);
                n.R[k] = ink.r; n.G[k] = ink.g; n.B[k] = ink.b;
            }

            n.Save(o.Need("out"));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}  {1}x{2}  (ink box {3:0.0}x{4:0.0} = rect x {5}, margin {6:0.0}/{7:0.0})",
                o.Need("out"), W, H, w, h, unit, ox, oy));
            return 0;
        }

        /// <summary>Alpha averaged over a sprite-pixel footprint (box filter when shrinking).</summary>
        private static double AreaA(Img s, double px, double py, double fw, double fh)
        {
            if (fw <= 1 && fh <= 1) return SampleA(s, px, py);
            int nx = Math.Max(1, (int)Math.Ceiling(fw)), ny = Math.Max(1, (int)Math.Ceiling(fh));
            double acc = 0;
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
                acc += SampleA(s, px + (i + 0.5) / nx * fw - fw / 2, py + (j + 0.5) / ny * fh - fh / 2);
            return acc / (nx * ny);
        }
    }
}
