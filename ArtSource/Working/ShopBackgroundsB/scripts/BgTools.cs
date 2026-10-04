// Image helpers for the shop background plates (ShopBackgroundsB). C# 5 only: compiled by
// Windows PowerShell's Add-Type (see bg.ps1). Float RGBA, straight alpha, 0..255.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public class Img
{
    public int W, H;
    public float[] R, G, B, A;

    public Img(int w, int h)
    {
        W = w; H = h;
        R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; A = new float[w * h];
        for (int i = 0; i < w * h; i++) A[i] = 255f;
    }

    public static Img Load(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (Image im = Image.FromStream(fs, false, false))
        using (Bitmap src = new Bitmap(im))
        {
            int w = src.Width, h = src.Height;
            Img o = new Img(w, h);
            BitmapData d = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] row = new byte[w * 4];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    o.B[i] = row[x * 4]; o.G[i] = row[x * 4 + 1]; o.R[i] = row[x * 4 + 2]; o.A[i] = row[x * 4 + 3];
                }
            }
            src.UnlockBits(d);
            return o;
        }
    }

    static byte Clamp(float v) { return v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)(v + 0.5f); }

    public Bitmap ToBitmap()
    {
        Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] row = new byte[W * 4];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                row[x * 4] = Clamp(B[i]); row[x * 4 + 1] = Clamp(G[i]); row[x * 4 + 2] = Clamp(R[i]); row[x * 4 + 3] = Clamp(A[i]);
            }
            Marshal.Copy(row, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), W * 4);
        }
        bmp.UnlockBits(d);
        return bmp;
    }

    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        using (Bitmap b = ToBitmap())
        {
            if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                ImageCodecInfo codec = null;
                foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") codec = c;
                EncoderParameters ps = new EncoderParameters(1);
                ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 94L);
                using (Bitmap rgb = new Bitmap(W, H, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(rgb)) { g.Clear(Color.White); g.DrawImage(b, 0, 0, W, H); }
                    rgb.Save(path, codec, ps);
                }
            }
            else b.Save(path, ImageFormat.Png);
        }
    }

    // Opaque RGB, 24-bit PNG (alpha dropped).
    public void Save24(string path)
    {
        using (Bitmap b = new Bitmap(W, H, PixelFormat.Format24bppRgb))
        {
            BitmapData d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            byte[] row = new byte[d.Stride];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    row[x * 3] = Clamp(B[i]); row[x * 3 + 1] = Clamp(G[i]); row[x * 3 + 2] = Clamp(R[i]);
                }
                Marshal.Copy(row, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), d.Stride);
            }
            b.UnlockBits(d);
            b.Save(path, ImageFormat.Png);
        }
    }

    public Img Clone()
    {
        Img o = new Img(W, H);
        Array.Copy(R, o.R, R.Length); Array.Copy(G, o.G, G.Length); Array.Copy(B, o.B, B.Length); Array.Copy(A, o.A, A.Length);
        return o;
    }

    public Img Crop(int x0, int y0, int w, int h)
    {
        Img o = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = Math.Min(W - 1, Math.Max(0, x0 + x)), sy = Math.Min(H - 1, Math.Max(0, y0 + y));
                int i = y * w + x, j = sy * W + sx;
                o.R[i] = R[j]; o.G[i] = G[j]; o.B[i] = B[j]; o.A[i] = A[j];
            }
        return o;
    }

    public static Img Solid(int w, int h, float r, float g, float b, float a)
    {
        Img o = new Img(w, h);
        for (int i = 0; i < w * h; i++) { o.R[i] = r; o.G[i] = g; o.B[i] = b; o.A[i] = a; }
        return o;
    }

    // Copies src into this image at (x0, y0), replacing pixels (no blending).
    public void Paste(Img src, int x0, int y0)
    {
        for (int y = 0; y < src.H; y++)
        {
            int ty = y0 + y; if (ty < 0 || ty >= H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int tx = x0 + x; if (tx < 0 || tx >= W) continue;
                int i = ty * W + tx, j = y * src.W + x;
                R[i] = src.R[j]; G[i] = src.G[j]; B[i] = src.B[j]; A[i] = src.A[j];
            }
        }
    }

    // Porter-Duff "over": src (straight alpha) composited onto this image at (x0, y0).
    public void Over(Img src, int x0, int y0)
    {
        for (int y = 0; y < src.H; y++)
        {
            int ty = y0 + y; if (ty < 0 || ty >= H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int tx = x0 + x; if (tx < 0 || tx >= W) continue;
                int i = ty * W + tx, j = y * src.W + x;
                float sa = src.A[j] / 255f; if (sa <= 0f) continue;
                float da = A[i] / 255f;
                float oa = sa + da * (1f - sa);
                if (oa <= 0f) { R[i] = G[i] = B[i] = A[i] = 0f; continue; }
                R[i] = (src.R[j] * sa + R[i] * da * (1f - sa)) / oa;
                G[i] = (src.G[j] * sa + G[i] * da * (1f - sa)) / oa;
                B[i] = (src.B[j] * sa + B[i] * da * (1f - sa)) / oa;
                A[i] = oa * 255f;
            }
        }
    }

    // this = this * (1 - m) + other * m, m = mask 0..1 (same size).
    public void LerpTo(Img other, float[] m)
    {
        for (int i = 0; i < W * H; i++)
        {
            float t = m[i]; if (t <= 0f) continue;
            R[i] += (other.R[i] - R[i]) * t; G[i] += (other.G[i] - G[i]) * t; B[i] += (other.B[i] - B[i]) * t; A[i] += (other.A[i] - A[i]) * t;
        }
    }

    // Separable Lanczos-3 resize in premultiplied alpha.
    public Img Resize(int nw, int nh)
    {
        float[][] ch = new float[][] { Premul(R), Premul(G), Premul(B), (float[])A.Clone() };
        float[][] tmp = new float[4][];
        for (int c = 0; c < 4; c++) tmp[c] = new float[nw * H];
        Kernel kx = new Kernel(W, nw);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < nw; x++)
            {
                int s = kx.Start[x]; float[] wts = kx.W[x];
                for (int c = 0; c < 4; c++)
                {
                    float acc = 0f; float[] src = ch[c]; int b = y * W;
                    for (int k = 0; k < wts.Length; k++) acc += src[b + Math.Min(W - 1, Math.Max(0, s + k))] * wts[k];
                    tmp[c][y * nw + x] = acc;
                }
            }
        Img o = new Img(nw, nh);
        float[][] outc = new float[][] { o.R, o.G, o.B, o.A };
        Kernel ky = new Kernel(H, nh);
        for (int y = 0; y < nh; y++)
        {
            int s = ky.Start[y]; float[] wts = ky.W[y];
            for (int x = 0; x < nw; x++)
                for (int c = 0; c < 4; c++)
                {
                    float acc = 0f; float[] src = tmp[c];
                    for (int k = 0; k < wts.Length; k++) acc += src[Math.Min(H - 1, Math.Max(0, s + k)) * nw + x] * wts[k];
                    outc[c][y * nw + x] = acc;
                }
        }
        for (int i = 0; i < nw * nh; i++)
        {
            float a = Math.Max(0f, Math.Min(255f, o.A[i])); o.A[i] = a;
            if (a > 0.01f) { float k = 255f / a; o.R[i] *= k; o.G[i] *= k; o.B[i] *= k; }
            else { o.R[i] = o.G[i] = o.B[i] = 0f; }
            o.R[i] = Math.Max(0f, Math.Min(255f, o.R[i])); o.G[i] = Math.Max(0f, Math.Min(255f, o.G[i])); o.B[i] = Math.Max(0f, Math.Min(255f, o.B[i]));
        }
        return o;
    }

    float[] Premul(float[] c)
    {
        float[] o = new float[c.Length];
        for (int i = 0; i < c.Length; i++) o[i] = c[i] * A[i] / 255f;
        return o;
    }

    class Kernel
    {
        public int[] Start; public float[][] W;
        public Kernel(int n, int m)
        {
            Start = new int[m]; W = new float[m][];
            double scale = (double)m / n;
            double support = scale < 1 ? 3.0 / scale : 3.0;
            double fscale = scale < 1 ? scale : 1.0;
            for (int i = 0; i < m; i++)
            {
                double center = (i + 0.5) / scale - 0.5;
                int s = (int)Math.Floor(center - support) + 1, e = (int)Math.Floor(center + support);
                float[] w = new float[e - s + 1]; double sum = 0;
                for (int k = s; k <= e; k++) { double v = L3((k - center) * fscale); w[k - s] = (float)v; sum += v; }
                for (int k = 0; k < w.Length; k++) w[k] = (float)(w[k] / sum);
                Start[i] = s; W[i] = w;
            }
        }
        static double L3(double x)
        {
            x = Math.Abs(x); if (x < 1e-9) return 1; if (x >= 3) return 0;
            double px = Math.PI * x; return 3 * Math.Sin(px) * Math.Sin(px / 3) / (px * px);
        }
    }

    // Gaussian blur of one float plane (separable, clamped edges).
    public static float[] BlurPlane(float[] src, int w, int h, double sigma)
    {
        if (sigma <= 0.01) return (float[])src.Clone();
        int r = (int)Math.Ceiling(sigma * 3);
        float[] k = new float[2 * r + 1]; double sum = 0;
        for (int i = -r; i <= r; i++) { double v = Math.Exp(-i * i / (2 * sigma * sigma)); k[i + r] = (float)v; sum += v; }
        for (int i = 0; i < k.Length; i++) k[i] = (float)(k[i] / sum);
        float[] tmp = new float[w * h], o = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int i = -r; i <= r; i++) acc += src[y * w + Math.Min(w - 1, Math.Max(0, x + i))] * k[i + r];
                tmp[y * w + x] = acc;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int i = -r; i <= r; i++) acc += tmp[Math.Min(h - 1, Math.Max(0, y + i)) * w + x] * k[i + r];
                o[y * w + x] = acc;
            }
        return o;
    }

    public Img Blur(double sigma)
    {
        Img o = new Img(W, H);
        o.R = BlurPlane(R, W, H, sigma); o.G = BlurPlane(G, W, H, sigma); o.B = BlurPlane(B, W, H, sigma); o.A = (float[])A.Clone();
        return o;
    }

    // Unsharp mask on RGB.
    public Img Sharpen(double sigma, float amount, float threshold)
    {
        Img bl = Blur(sigma); Img o = Clone();
        for (int i = 0; i < W * H; i++)
        {
            float dr = R[i] - bl.R[i], dg = G[i] - bl.G[i], db = B[i] - bl.B[i];
            if (Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db) < threshold * 3) continue;
            o.R[i] = Math.Max(0f, Math.Min(255f, R[i] + amount * dr));
            o.G[i] = Math.Max(0f, Math.Min(255f, G[i] + amount * dg));
            o.B[i] = Math.Max(0f, Math.Min(255f, B[i] + amount * db));
        }
        return o;
    }

    // Fills a strip along one side (0 left, 1 right, 2 top, 3 bottom) with the mirror image of
    // the pixels just inside it.
    public void MirrorStrip(int side, int width)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int sx = x, sy = y;
                if (side == 0) { if (x >= width) continue; sx = 2 * width - 1 - x; }
                else if (side == 1) { if (x < W - width) continue; sx = 2 * (W - width) - 1 - x; }
                else if (side == 2) { if (y >= width) continue; sy = 2 * width - 1 - y; }
                else { if (y < H - width) continue; sy = 2 * (H - width) - 1 - y; }
                sx = Math.Max(0, Math.Min(W - 1, sx)); sy = Math.Max(0, Math.Min(H - 1, sy));
                int i = y * W + x, j = sy * W + sx;
                R[i] = R[j]; G[i] = G[j]; B[i] = B[j]; A[i] = A[j];
            }
    }

    // Hue-selective saturation scale (HSV, value kept): pixels whose hue is within [h0, h1] degrees
    // get saturation x k, fading to no change over 'fade' degrees outside the band.
    public void ScaleSatHue(double h0, double h1, double fade, double k)
    {
        for (int i = 0; i < W * H; i++)
        {
            double r = R[i], g = G[i], b = B[i];
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            if (mx <= 0 || mx - mn < 1e-6) continue;
            double d = mx - mn, h;
            if (mx == r) h = 60 * (((g - b) / d) % 6); else if (mx == g) h = 60 * ((b - r) / d + 2); else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
            double w;
            if (h >= h0 && h <= h1) w = 1; else
            {
                double dist = h < h0 ? h0 - h : h - h1;
                w = dist >= fade ? 0 : 1 - dist / fade;
                w = w * w * (3 - 2 * w);
            }
            if (w <= 0) continue;
            double f = 1 - (1 - k) * w;           // saturation factor
            // value kept: each channel moves toward mx by the factor
            R[i] = (float)(mx - (mx - r) * f); G[i] = (float)(mx - (mx - g) * f); B[i] = (float)(mx - (mx - b) * f);
        }
    }

    // Luma-preserving saturation: c' = L + k (c - L), L = Rec.601 luma.
    public void ScaleSatLuma(float k)
    {
        for (int i = 0; i < W * H; i++)
        {
            float l = 0.299f * R[i] + 0.587f * G[i] + 0.114f * B[i];
            R[i] = Math.Max(0f, Math.Min(255f, l + k * (R[i] - l)));
            G[i] = Math.Max(0f, Math.Min(255f, l + k * (G[i] - l)));
            B[i] = Math.Max(0f, Math.Min(255f, l + k * (B[i] - l)));
        }
    }

    public float[] Luma()
    {
        float[] l = new float[W * H];
        for (int i = 0; i < W * H; i++) l[i] = 0.299f * R[i] + 0.587f * G[i] + 0.114f * B[i];
        return l;
    }

    // Mean R,G,B (and pixel count) inside a rect, optionally only where mask > 0.5.
    public double[] Mean(int x0, int y0, int w, int h, float[] mask)
    {
        double r = 0, g = 0, b = 0; long n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(H, y0 + h); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(W, x0 + w); x++)
            {
                int i = y * W + x;
                if (mask != null && mask[i] < 0.5f) continue;
                r += R[i]; g += G[i]; b += B[i]; n++;
            }
        if (n == 0) return new double[] { 0, 0, 0, 0 };
        return new double[] { r / n, g / n, b / n, n };
    }

    public static double MeanAbsDiff(Img a, Img b, int x0, int y0, int w, int h)
    {
        double s = 0; long n = 0;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (x < 0 || y < 0 || x >= a.W || y >= a.H || x >= b.W || y >= b.H) continue;
                int i = y * a.W + x, j = y * b.W + x;
                s += Math.Abs(a.R[i] - b.R[j]) + Math.Abs(a.G[i] - b.G[j]) + Math.Abs(a.B[i] - b.B[j]); n += 3;
            }
        return n == 0 ? 0 : s / n;
    }

    // Best integer shift (dx, dy) of b against a inside a tile, on luminance gradients (NCC).
    public static double[] TileShift(Img a, Img b, int x0, int y0, int w, int h, int range)
    {
        float[] ga = Grad(a), gb = Grad(b);
        double best = -2; int bx = 0, by = 0;
        for (int dy = -range; dy <= range; dy++)
            for (int dx = -range; dx <= range; dx++)
            {
                double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; long n = 0;
                for (int y = y0; y < y0 + h; y += 2)
                    for (int x = x0; x < x0 + w; x += 2)
                    {
                        int xb = x + dx, yb = y + dy;
                        if (x < 0 || y < 0 || x >= a.W || y >= a.H || xb < 0 || yb < 0 || xb >= b.W || yb >= b.H) continue;
                        double va = ga[y * a.W + x], vb = gb[yb * b.W + xb];
                        sa += va; sb += vb; saa += va * va; sbb += vb * vb; sab += va * vb; n++;
                    }
                if (n < 10) continue;
                double cov = sab / n - (sa / n) * (sb / n), da = saa / n - (sa / n) * (sa / n), db = sbb / n - (sb / n) * (sb / n);
                double ncc = cov / Math.Sqrt(Math.Max(1e-9, da * db));
                if (ncc > best) { best = ncc; bx = dx; by = dy; }
            }
        return new double[] { bx, by, best };
    }

    static Dictionary<Img, float[]> gradCache = new Dictionary<Img, float[]>();
    static float[] Grad(Img im)
    {
        float[] g;
        if (gradCache.TryGetValue(im, out g)) return g;
        float[] l = BlurPlane(im.Luma(), im.W, im.H, 1.0);
        g = new float[im.W * im.H];
        for (int y = 1; y < im.H - 1; y++)
            for (int x = 1; x < im.W - 1; x++)
            {
                int i = y * im.W + x;
                float gx = l[i + 1] - l[i - 1], gy = l[i + im.W] - l[i - im.W];
                g[i] = (float)Math.Sqrt(gx * gx + gy * gy);
            }
        gradCache[im] = g;
        return g;
    }

    public static void ClearCache() { gradCache.Clear(); }

    // Gradient-magnitude map box-downsampled by f.
    static float[] GradDown(Img im, int f, out int w, out int h)
    {
        float[] g = Grad(im);
        w = im.W / f; h = im.H / f;
        float[] o = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0f;
                for (int j = 0; j < f; j++) for (int i = 0; i < f; i++) s += g[(y * f + j) * im.W + x * f + i];
                o[y * w + x] = s / (f * f);
            }
        return o;
    }

    static float Bilin(float[] p, int w, int h, double x, double y)
    {
        if (x < 0 || y < 0 || x > w - 1.001 || y > h - 1.001) return float.NaN;
        int x0 = (int)x, y0 = (int)y; float tx = (float)(x - x0), ty = (float)(y - y0);
        int i = y0 * w + x0;
        return (p[i] * (1 - tx) + p[i + 1] * tx) * (1 - ty) + (p[i + w] * (1 - tx) + p[i + w + 1] * tx) * ty;
    }

    static double NccAt(float[] pa, int wa, int ha, float[] pb, int wb, int hb, double s, double tx, double ty, int step, int[] roi)
    {
        double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; long n = 0;
        int x0 = roi == null ? 0 : roi[0], y0 = roi == null ? 0 : roi[1];
        int x1 = roi == null ? wa : roi[0] + roi[2], y1 = roi == null ? ha : roi[1] + roi[3];
        for (int y = y0; y < y1; y += step)
            for (int x = x0; x < x1; x += step)
            {
                float vb = Bilin(pb, wb, hb, s * x + tx, s * y + ty);
                if (float.IsNaN(vb)) continue;
                double va = pa[y * wa + x];
                sa += va; sb += vb; saa += va * va; sbb += vb * vb; sab += va * vb; n++;
            }
        if (n < 200) return -2;
        double cov = sab / n - (sa / n) * (sb / n), da = saa / n - (sa / n) * (sa / n), db = sbb / n - (sb / n) * (sb / n);
        return cov / Math.Sqrt(Math.Max(1e-9, da * db));
    }

    // Finds s, tx, ty with b(s*x + tx, s*y + ty) ~ a(x, y) (full-resolution px), searching scales
    // sMin..sMax and shifts tx in [txMin, txMax], ty in [tyMin, tyMax]. roi = [x, y, w, h] in a
    // (full px) or null. Returns { s, tx, ty, ncc }.
    public static double[] FitSimilarity(Img a, Img b, double sMin, double sMax, double txMin, double txMax, double tyMin, double tyMax, int[] roi)
    {
        int[] fs = new int[] { 8, 4, 2, 1 };
        double bs = 1, btx = 0, bty = 0, best = -3;
        double sLo = sMin, sHi = sMax, sSt = 0.01, xLo = txMin, xHi = txMax, yLo = tyMin, yHi = tyMax;
        for (int lev = 0; lev < fs.Length; lev++)
        {
            int f = fs[lev], wa, ha, wb, hb;
            float[] pa = GradDown(a, f, out wa, out ha), pb = GradDown(b, f, out wb, out hb);
            int[] r = roi == null ? null : new int[] { roi[0] / f, roi[1] / f, roi[2] / f, roi[3] / f };
            int step = lev == 0 ? 1 : 2;
            double tStep = lev == 0 ? 1.0 : 0.5;   // in this level's px
            best = -3;
            for (double s = sLo; s <= sHi + 1e-9; s += sSt)
                for (double ty = yLo / f; ty <= yHi / f + 1e-9; ty += tStep)
                    for (double tx = xLo / f; tx <= xHi / f + 1e-9; tx += tStep)
                    {
                        double v = NccAt(pa, wa, ha, pb, wb, hb, s, tx, ty, step, r);
                        if (v > best) { best = v; bs = s; btx = tx * f; bty = ty * f; }
                    }
            // narrow the search for the next level
            sSt = sSt / 4; sLo = bs - sSt * 6; sHi = bs + sSt * 6;
            double win = 2.0 * f;
            xLo = btx - win; xHi = btx + win; yLo = bty - win; yHi = bty + win;
        }
        return new double[] { bs, btx, bty, best };
    }

    // Dense displacement field that maps b onto a: b(x + dx, y + dy) ~ a(x, y). Measured by block
    // matching (gradient NCC, every 2nd pixel) on tiles of 'tile' px every 'step' px, searching
    // +-range around (gx, gy); tiles with NCC below minNcc, or centred where 'exclude' > 0.5, are
    // filled by diffusion from their neighbours; the field is then smoothed (sigma in tiles) and
    // interpolated bilinearly. Returns { dx[], dy[], tileInfo } (tileInfo: valid count, median ncc).
    public static object[] FlowField(Img a, Img b, int tile, int step, int range, double minNcc, float[] exclude, int gx, int gy, double smooth)
    {
        float[] ga = Grad(a), gb = Grad(b);
        int nx = (a.W - tile) / step + 1, ny = (a.H - tile) / step + 1;
        double[] fx = new double[nx * ny], fy = new double[nx * ny]; bool[] ok = new bool[nx * ny];
        List<double> nccs = new List<double>(); int valid = 0;
        for (int ty = 0; ty < ny; ty++)
            for (int tx = 0; tx < nx; tx++)
            {
                int x0 = tx * step, y0 = ty * step, cx = x0 + tile / 2, cy = y0 + tile / 2;
                int k = ty * nx + tx;
                if (exclude != null && exclude[cy * a.W + cx] > 0.5f) continue;
                double best = -2; int bx = 0, by = 0;
                double[,] score = new double[2 * range + 1, 2 * range + 1];
                for (int dy = -range; dy <= range; dy++)
                    for (int dx = -range; dx <= range; dx++)
                    {
                        double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; int n = 0;
                        for (int y = y0; y < y0 + tile; y += 2)
                        {
                            int yb = y + gy + dy; if (yb < 0 || yb >= b.H) continue;
                            for (int x = x0; x < x0 + tile; x += 2)
                            {
                                int xb = x + gx + dx; if (xb < 0 || xb >= b.W) continue;
                                double va = ga[y * a.W + x], vb = gb[yb * b.W + xb];
                                sa += va; sb += vb; saa += va * va; sbb += vb * vb; sab += va * vb; n++;
                            }
                        }
                        double v = -2;
                        if (n > tile * tile / 8)
                        {
                            double cov = sab / n - (sa / n) * (sb / n), da = saa / n - (sa / n) * (sa / n), db = sbb / n - (sb / n) * (sb / n);
                            if (da > 4 && db > 4) v = cov / Math.Sqrt(da * db);
                        }
                        score[dy + range, dx + range] = v;
                        if (v > best) { best = v; bx = dx; by = dy; }
                    }
                if (best < minNcc) continue;
                // parabolic sub-pixel refinement
                double sxp = 0, syp = 0;
                if (bx > -range && bx < range)
                {
                    double l = score[by + range, bx + range - 1], c = score[by + range, bx + range], r = score[by + range, bx + range + 1];
                    double den = l - 2 * c + r; if (Math.Abs(den) > 1e-9) sxp = Math.Max(-0.5, Math.Min(0.5, 0.5 * (l - r) / den));
                }
                if (by > -range && by < range)
                {
                    double u = score[by + range - 1, bx + range], c = score[by + range, bx + range], d = score[by + range + 1, bx + range];
                    double den = u - 2 * c + d; if (Math.Abs(den) > 1e-9) syp = Math.Max(-0.5, Math.Min(0.5, 0.5 * (u - d) / den));
                }
                fx[k] = gx + bx + sxp; fy[k] = gy + by + syp; ok[k] = true; valid++; nccs.Add(best);
            }
        // reject outliers: compare to the median of valid neighbours (3x3)
        bool[] ok2 = (bool[])ok.Clone();
        for (int ty = 0; ty < ny; ty++)
            for (int tx = 0; tx < nx; tx++)
            {
                int k = ty * nx + tx; if (!ok[k]) continue;
                List<double> lx = new List<double>(), ly = new List<double>();
                for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
                    {
                        int xx = tx + i, yy = ty + j; if (xx < 0 || yy < 0 || xx >= nx || yy >= ny || (i == 0 && j == 0)) continue;
                        int kk = yy * nx + xx; if (!ok[kk]) continue; lx.Add(fx[kk]); ly.Add(fy[kk]);
                    }
                if (lx.Count < 3) continue;
                lx.Sort(); ly.Sort();
                double mx = lx[lx.Count / 2], my = ly[ly.Count / 2];
                if (Math.Abs(fx[k] - mx) > 3 || Math.Abs(fy[k] - my) > 3) { ok2[k] = false; valid--; }
            }
        ok = ok2;
        // fill invalid tiles by diffusion
        double[] vx = (double[])fx.Clone(), vy = (double[])fy.Clone();
        bool any = false; for (int k = 0; k < nx * ny; k++) if (ok[k]) any = true;
        if (!any) { for (int k = 0; k < nx * ny; k++) { vx[k] = gx; vy[k] = gy; } }
        else
        {
            for (int k = 0; k < nx * ny; k++) if (!ok[k]) { vx[k] = gx; vy[k] = gy; }
            for (int it = 0; it < 400; it++)
            {
                double[] nxv = (double[])vx.Clone(), nyv = (double[])vy.Clone();
                for (int ty = 0; ty < ny; ty++)
                    for (int tx = 0; tx < nx; tx++)
                    {
                        int k = ty * nx + tx; if (ok[k]) continue;
                        double s1 = 0, s2 = 0; int c = 0;
                        if (tx > 0) { s1 += vx[k - 1]; s2 += vy[k - 1]; c++; }
                        if (tx < nx - 1) { s1 += vx[k + 1]; s2 += vy[k + 1]; c++; }
                        if (ty > 0) { s1 += vx[k - nx]; s2 += vy[k - nx]; c++; }
                        if (ty < ny - 1) { s1 += vx[k + nx]; s2 += vy[k + nx]; c++; }
                        nxv[k] = s1 / c; nyv[k] = s2 / c;
                    }
                vx = nxv; vy = nyv;
            }
        }
        // smooth in tile space
        float[] sxv = new float[nx * ny], syv = new float[nx * ny];
        for (int k = 0; k < nx * ny; k++) { sxv[k] = (float)vx[k]; syv[k] = (float)vy[k]; }
        sxv = BlurPlane(sxv, nx, ny, smooth); syv = BlurPlane(syv, nx, ny, smooth);
        // dense bilinear interpolation (tile centres at x0 + tile/2)
        float[] dxs = new float[a.W * a.H], dys = new float[a.W * a.H];
        for (int y = 0; y < a.H; y++)
        {
            double ty = (y - tile / 2.0) / step; ty = Math.Max(0, Math.Min(ny - 1.001, ty));
            int ty0 = (int)ty; double fty = ty - ty0; int ty1 = Math.Min(ny - 1, ty0 + 1);
            for (int x = 0; x < a.W; x++)
            {
                double tx = (x - tile / 2.0) / step; tx = Math.Max(0, Math.Min(nx - 1.001, tx));
                int tx0 = (int)tx; double ftx = tx - tx0; int tx1 = Math.Min(nx - 1, tx0 + 1);
                int k00 = ty0 * nx + tx0, k01 = ty0 * nx + tx1, k10 = ty1 * nx + tx0, k11 = ty1 * nx + tx1;
                dxs[y * a.W + x] = (float)((sxv[k00] * (1 - ftx) + sxv[k01] * ftx) * (1 - fty) + (sxv[k10] * (1 - ftx) + sxv[k11] * ftx) * fty);
                dys[y * a.W + x] = (float)((syv[k00] * (1 - ftx) + syv[k01] * ftx) * (1 - fty) + (syv[k10] * (1 - ftx) + syv[k11] * ftx) * fty);
            }
        }
        nccs.Sort();
        double med = nccs.Count > 0 ? nccs[nccs.Count / 2] : 0;
        return new object[] { dxs, dys, new double[] { valid, nx * ny, med } };
    }

    // RGBA resample out(x, y) = b(s*x + tx, s*y + ty), bilinear in premultiplied alpha; outside b
    // = transparent. Only columns [x0, x1) of the output are written (others transparent).
    public static Img WarpRGBA(Img b, int w, int h, double s, double tx, double ty, int x0, int x1)
    {
        Img o = new Img(w, h);
        Array.Clear(o.A, 0, o.A.Length);
        float[] pr = new float[b.W * b.H], pg = new float[b.W * b.H], pb = new float[b.W * b.H];
        for (int i = 0; i < b.W * b.H; i++) { float a = b.A[i] / 255f; pr[i] = b.R[i] * a; pg[i] = b.G[i] * a; pb[i] = b.B[i] * a; }
        for (int y = 0; y < h; y++)
            for (int x = x0; x < x1; x++)
            {
                double sx = s * x + tx, sy = s * y + ty;
                if (sx < 0 || sy < 0 || sx > b.W - 1.001 || sy > b.H - 1.001) continue;
                int i = y * w + x;
                float a = Bilin(b.A, b.W, b.H, sx, sy);
                if (a < 0.01f) continue;
                o.A[i] = a;
                o.R[i] = Bilin(pr, b.W, b.H, sx, sy) * 255f / a;
                o.G[i] = Bilin(pg, b.W, b.H, sx, sy) * 255f / a;
                o.B[i] = Bilin(pb, b.W, b.H, sx, sy) * 255f / a;
            }
        return o;
    }

    // out(x, y) = b(x + dx, y + dy), bilinear, clamped at b's edges.
    public static Img WarpField(Img b, float[] dx, float[] dy, int w, int h)
    {
        Img o = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                double sx = Math.Max(0, Math.Min(b.W - 1.001, x + dx[i])), sy = Math.Max(0, Math.Min(b.H - 1.001, y + dy[i]));
                o.R[i] = Bilin(b.R, b.W, b.H, sx, sy); o.G[i] = Bilin(b.G, b.W, b.H, sx, sy); o.B[i] = Bilin(b.B, b.W, b.H, sx, sy); o.A[i] = 255f;
            }
        return o;
    }

    // Resamples b into a's frame with b(s*x + tx, s*y + ty) -> out(x, y) (bilinear on RGB). Source
    // positions up to 'clamp' px outside b repeat b's edge; further out = transparent.
    public static Img Warp(Img b, int w, int h, double s, double tx, double ty, double clamp)
    {
        Img o = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                double sx = s * x + tx, sy = s * y + ty;
                if (sx < -clamp || sy < -clamp || sx > b.W - 1 + clamp || sy > b.H - 1 + clamp) { o.A[i] = 0; continue; }
                // just outside the source: reflect about its edge (no smeared streaks)
                if (sx < 0) sx = -sx; if (sy < 0) sy = -sy;
                if (sx > b.W - 1.001) sx = 2 * (b.W - 1.001) - sx; if (sy > b.H - 1.001) sy = 2 * (b.H - 1.001) - sy;
                sx = Math.Max(0, Math.Min(b.W - 1.001, sx)); sy = Math.Max(0, Math.Min(b.H - 1.001, sy));
                o.R[i] = Bilin(b.R, b.W, b.H, sx, sy); o.G[i] = Bilin(b.G, b.W, b.H, sx, sy); o.B[i] = Bilin(b.B, b.W, b.H, sx, sy); o.A[i] = 255f;
            }
        return o;
    }

    // Low-frequency colour match: per-channel gain and offset fitted between blurred copies of src
    // and dst inside the mask (so misaligned detail does not flatten the fit).
    public static double[] FitColorSmooth(Img src, Img dst, float[] mask, double sigma)
    {
        Img a = src.Blur(sigma), b = dst.Blur(sigma);
        return FitColor(a, b, mask);
    }

    // Fills transparent pixels from their opaque neighbours, growing inward ring by ring.
    public void FillHoles()
    {
        bool any = true; int pass = 0;
        while (any && pass < 4000)
        {
            any = false; pass++;
            float[] nr = (float[])R.Clone(), ng = (float[])G.Clone(), nb = (float[])B.Clone(), na = (float[])A.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (A[i] >= 254.5f) continue;
                    float sr = 0, sg = 0, sb = 0; int n = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            int j = yy * W + xx; if (A[j] < 254.5f) continue;
                            sr += R[j]; sg += G[j]; sb += B[j]; n++;
                        }
                    if (n == 0) { any = true; continue; }
                    float t = A[i] / 255f;
                    nr[i] = R[i] * t + sr / n * (1 - t); ng[i] = G[i] * t + sg / n * (1 - t); nb[i] = B[i] * t + sb / n * (1 - t); na[i] = 255f;
                    any = true;
                }
            R = nr; G = ng; B = nb; A = na;
        }
    }

    // Fraction of pixels that are (near) pure magenta inside a rect.
    public double MagentaFraction(int x0, int y0, int w, int h)
    {
        long n = 0, m = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(H, y0 + h); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(W, x0 + w); x++)
            {
                int i = y * W + x; n++;
                if (R[i] > 200 && B[i] > 200 && G[i] < 80) m++;
            }
        return n == 0 ? 0 : (double)m / n;
    }

    // Per-channel gain and offset that map this image onto ref inside the mask (least squares).
    public static double[] FitColor(Img src, Img dst, float[] mask)
    {
        double[] k = new double[6];
        float[][] s = new float[][] { src.R, src.G, src.B }, d = new float[][] { dst.R, dst.G, dst.B };
        for (int c = 0; c < 3; c++)
        {
            double sx = 0, sy = 0, sxx = 0, sxy = 0, n = 0;
            for (int i = 0; i < src.W * src.H; i++)
            {
                if (mask[i] < 0.5f) continue;
                double x = s[c][i], y = d[c][i];
                sx += x; sy += y; sxx += x * x; sxy += x * y; n++;
            }
            double den = n * sxx - sx * sx;
            double gain = den > 1e-6 ? (n * sxy - sx * sy) / den : 1.0;
            double off = (sy - gain * sx) / Math.Max(1, n);
            k[c * 2] = gain; k[c * 2 + 1] = off;
        }
        return k;
    }

    public void ApplyColor(double[] k)
    {
        float[][] ch = new float[][] { R, G, B };
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < W * H; i++)
                ch[c][i] = (float)Math.Max(0, Math.Min(255, ch[c][i] * k[c * 2] + k[c * 2 + 1]));
    }

    // |a - b| * gain, for looking at differences.
    public static Img Diff(Img a, Img b, float gain)
    {
        Img o = new Img(a.W, a.H);
        for (int i = 0; i < a.W * a.H; i++)
        {
            o.R[i] = Math.Min(255f, Math.Abs(a.R[i] - b.R[i]) * gain);
            o.G[i] = Math.Min(255f, Math.Abs(a.G[i] - b.G[i]) * gain);
            o.B[i] = Math.Min(255f, Math.Abs(a.B[i] - b.B[i]) * gain);
        }
        return o;
    }

    public Img FlattenOn(float r, float g, float b)
    {
        Img o = Solid(W, H, r, g, b, 255f); o.Over(this, 0, 0); return o;
    }

    // Alpha levels: lo -> 0, hi -> 255 (ChatGPT cut-outs come back with 250-254 inside and a faint
    // haze outside).
    public void AlphaLevels(float lo, float hi)
    {
        for (int i = 0; i < W * H; i++)
        {
            float a = (A[i] - lo) / (hi - lo) * 255f;
            A[i] = a <= 0 ? 0 : a >= 255 ? 255 : a;
        }
    }

    // Bounding box (x, y, w, h) of pixels with alpha above t inside the given columns.
    public int[] AlphaBox(float t, int x0, int x1)
    {
        int minx = W, miny = H, maxx = -1, maxy = -1;
        for (int y = 0; y < H; y++)
            for (int x = x0; x < x1; x++)
                if (A[y * W + x] > t) { if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
        return maxx < 0 ? new int[] { 0, 0, 0, 0 } : new int[] { minx, miny, maxx - minx + 1, maxy - miny + 1 };
    }

    // Rightmost (or leftmost) column with alpha above t in the given row range, for each side.
    public int ExtentInRows(float t, int y0, int y1, bool fromLeft, int xLimit)
    {
        int best = fromLeft ? -1 : W;
        for (int y = y0; y < y1; y++)
            for (int x = 0; x < W; x++)
            {
                if (A[y * W + x] <= t) continue;
                if (fromLeft && x < xLimit && x > best) best = x;
                if (!fromLeft && x >= xLimit && x < best) best = x;
            }
        return best;
    }

    public string Stats()
    {
        double sa = 0; int opaque = 0, clear = 0;
        for (int i = 0; i < W * H; i++) { sa += A[i]; if (A[i] >= 254.5f) opaque++; if (A[i] < 0.5f) clear++; }
        return string.Format("{0}x{1} meanA {2:F1} opaque {3:P1} clear {4:P1}", W, H, sa / (W * H), (double)opaque / (W * H), (double)clear / (W * H));
    }
}

public static class Seams
{
    // Joins src into dst (same frame and size) with a minimum-difference seam inside a band.
    // vertical = true: the seam runs top to bottom, x in [b0, b1), rows [r0, r1); srcLow = true
    // means src is used at lower x (left of the seam). vertical = false: the seam runs left to right,
    // y in [b0, b1), columns [r0, r1); srcLow = true means src is used above the seam.
    // Outside the band (inside rows r0..r1) the src side is copied as is. Returns the seam positions.
    public static int[] Join(Img dst, Img src, bool vertical, int b0, int b1, int r0, int r1, bool srcLow, int feather)
    {
        int n = r1 - r0, m = b1 - b0;
        double[] cost = new double[n * m];
        for (int k = 0; k < n; k++)
            for (int j = 0; j < m; j++)
            {
                int x = vertical ? b0 + j : r0 + k, y = vertical ? r0 + k : b0 + j;
                int i = y * dst.W + x;
                double d = Math.Abs(dst.R[i] - src.R[i]) + Math.Abs(dst.G[i] - src.G[i]) + Math.Abs(dst.B[i] - src.B[i]);
                if (src.A[i] < 250 || dst.A[i] < 250) d += 2000;
                cost[k * m + j] = d;
            }
        // smooth the cost along the band a little, so the seam avoids textured spots
        double[] sm = new double[n * m];
        for (int k = 0; k < n; k++)
            for (int j = 0; j < m; j++)
            {
                double s = 0; int c = 0;
                for (int dk = -2; dk <= 2; dk++) for (int dj = -2; dj <= 2; dj++)
                    {
                        int kk = k + dk, jj = j + dj; if (kk < 0 || kk >= n || jj < 0 || jj >= m) continue;
                        s += cost[kk * m + jj]; c++;
                    }
                sm[k * m + j] = s / c;
            }
        double[] e = new double[n * m]; int[] from = new int[n * m];
        for (int j = 0; j < m; j++) e[j] = sm[j];
        for (int k = 1; k < n; k++)
            for (int j = 0; j < m; j++)
            {
                double best = double.MaxValue; int bj = j;
                for (int dj = -1; dj <= 1; dj++)
                {
                    int jj = j + dj; if (jj < 0 || jj >= m) continue;
                    if (e[(k - 1) * m + jj] < best) { best = e[(k - 1) * m + jj]; bj = jj; }
                }
                e[k * m + j] = best + sm[k * m + j]; from[k * m + j] = bj;
            }
        int[] seam = new int[n];
        double bestEnd = double.MaxValue; int cur = 0;
        for (int j = 0; j < m; j++) if (e[(n - 1) * m + j] < bestEnd) { bestEnd = e[(n - 1) * m + j]; cur = j; }
        for (int k = n - 1; k >= 0; k--) { seam[k] = b0 + cur; cur = from[k * m + cur]; }
        // blend
        int lo = srcLow ? 0 : b0, hi = srcLow ? b1 : (vertical ? dst.W : dst.H);
        for (int k = 0; k < n; k++)
        {
            int s = seam[k];
            int start = srcLow ? 0 : b0, end = srcLow ? b1 : (vertical ? dst.W : dst.H);
            for (int p = start; p < end; p++)
            {
                double t; // weight of src
                double dpos = p - s;
                if (srcLow) t = dpos <= -feather ? 1 : dpos >= feather ? 0 : 0.5 - dpos / (2.0 * feather);
                else t = dpos >= feather ? 1 : dpos <= -feather ? 0 : 0.5 + dpos / (2.0 * feather);
                int x = vertical ? p : r0 + k, y = vertical ? r0 + k : p;
                int i = y * dst.W + x;
                if (src.A[i] < 1) continue;
                if (dst.A[i] < 1) t = 1;   // nothing there yet: no blend with empty pixels
                if (t <= 0) continue;
                float tf = (float)t;
                dst.R[i] += (src.R[i] - dst.R[i]) * tf; dst.G[i] += (src.G[i] - dst.G[i]) * tf; dst.B[i] += (src.B[i] - dst.B[i]) * tf;
                dst.A[i] = Math.Max(dst.A[i], src.A[i] * tf);
            }
        }
        return seam;
    }

    // For rows y0..y1-1: empty pixels at the right end (fromRight = true) or left end of each row are
    // filled by mirroring the row's last opaque pixels about the boundary.
    public static void MirrorFillEmptyEnds(Img im, int y0, int y1, bool fromRight)
    {
        for (int y = y0; y < y1; y++)
        {
            int b;
            if (fromRight)
            {
                b = im.W - 1; while (b >= 0 && im.A[y * im.W + b] < 254.5f) b--;
                if (b < 0 || b == im.W - 1) continue;
                for (int x = b + 1; x < im.W; x++)
                {
                    int sx = Math.Max(0, 2 * b - x + 1); int i = y * im.W + x, j = y * im.W + sx;
                    im.R[i] = im.R[j]; im.G[i] = im.G[j]; im.B[i] = im.B[j]; im.A[i] = 255f;
                }
            }
            else
            {
                b = 0; while (b < im.W && im.A[y * im.W + b] < 254.5f) b++;
                if (b >= im.W || b == 0) continue;
                for (int x = b - 1; x >= 0; x--)
                {
                    int sx = Math.Min(im.W - 1, 2 * b - x - 1); int i = y * im.W + x, j = y * im.W + sx;
                    im.R[i] = im.R[j]; im.G[i] = im.G[j]; im.B[i] = im.B[j]; im.A[i] = 255f;
                }
            }
        }
    }

    // Copies src into dst only where dst is still empty.
    public static void FillEmpty(Img dst, Img src)
    {
        for (int i = 0; i < dst.W * dst.H; i++)
        {
            if (dst.A[i] >= 1 || src.A[i] < 1) continue;
            dst.R[i] = src.R[i]; dst.G[i] = src.G[i]; dst.B[i] = src.B[i]; dst.A[i] = src.A[i];
        }
    }

    // A copy of im moved by (dx, dy); uncovered pixels transparent.
    public static Img Shifted(Img im, int dx, int dy)
    {
        Img o = new Img(im.W, im.H);
        Array.Clear(o.A, 0, o.A.Length);
        for (int y = 0; y < im.H; y++)
        {
            int sy = y - dy; if (sy < 0 || sy >= im.H) continue;
            for (int x = 0; x < im.W; x++)
            {
                int sx = x - dx; if (sx < 0 || sx >= im.W) continue;
                int i = y * im.W + x, j = sy * im.W + sx;
                o.R[i] = im.R[j]; o.G[i] = im.G[j]; o.B[i] = im.B[j]; o.A[i] = im.A[j];
            }
        }
        return o;
    }

    // Fills rows y0..H-1 from the rows above: a mirror at the seam (continuous) fading over 'feather'
    // rows into a straight copy of the band just above (no symmetric pattern further down).
    public static void BottomFill(Img im, int y0, int feather)
    {
        int h = im.H - y0;
        for (int y = y0; y < im.H; y++)
        {
            float w = feather <= 0 ? 1f : Math.Min(1f, (y - y0) / (float)feather);
            int ym = Math.Max(0, 2 * y0 - 1 - y), yc = Math.Max(0, y - h);
            for (int x = 0; x < im.W; x++)
            {
                int i = y * im.W + x, jm = ym * im.W + x, jc = yc * im.W + x;
                im.R[i] = im.R[jm] * (1 - w) + im.R[jc] * w;
                im.G[i] = im.G[jm] * (1 - w) + im.G[jc] * w;
                im.B[i] = im.B[jm] * (1 - w) + im.B[jc] * w;
                im.A[i] = Math.Max(im.A[jm], im.A[jc]);
            }
        }
    }

    // Fills a rectangle by mirroring the pixels on one side of it (0 from the right, 1 from the left,
    // 2 from below, 3 from above).
    public static void MirrorFill(Img im, int x0, int y0, int w, int h, int from)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int sx = x, sy = y;
                if (from == 0) sx = 2 * (x0 + w) - 1 - x;
                else if (from == 1) sx = 2 * x0 - 1 - x;
                else if (from == 2) sy = 2 * (y0 + h) - 1 - y;
                else sy = 2 * y0 - 1 - y;
                sx = Math.Max(0, Math.Min(im.W - 1, sx)); sy = Math.Max(0, Math.Min(im.H - 1, sy));
                int i = y * im.W + x, j = sy * im.W + sx;
                im.R[i] = im.R[j]; im.G[i] = im.G[j]; im.B[i] = im.B[j]; im.A[i] = im.A[j];
            }
    }
}

public static class Masks
{
    // 1 inside the rect, 0 outside, with a linear feather of f px inward from the rect's edges.
    public static float[] Rect(int w, int h, int x0, int y0, int rw, int rh, int f)
    {
        float[] m = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (x < x0 || y < y0 || x >= x0 + rw || y >= y0 + rh) continue;
                int d = Math.Min(Math.Min(x - x0, x0 + rw - 1 - x), Math.Min(y - y0, y0 + rh - 1 - y));
                m[y * w + x] = f <= 0 ? 1f : Math.Min(1f, (d + 0.5f) / f);
            }
        return m;
    }

    public static float[] FromAlpha(Img im) { float[] m = new float[im.W * im.H]; for (int i = 0; i < m.Length; i++) m[i] = im.A[i] / 255f; return m; }

    public static float[] FromLuma(Img im) { float[] l = im.Luma(); for (int i = 0; i < l.Length; i++) l[i] /= 255f; return l; }

    public static float[] Threshold(float[] m, float t) { float[] o = new float[m.Length]; for (int i = 0; i < m.Length; i++) o[i] = m[i] >= t ? 1f : 0f; return o; }

    public static float[] Invert(float[] m) { float[] o = new float[m.Length]; for (int i = 0; i < m.Length; i++) o[i] = 1f - m[i]; return o; }

    // Binary dilation by r px (square), then optional blur.
    public static float[] Dilate(float[] m, int w, int h, int r)
    {
        float[] tmp = new float[w * h], o = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0f;
                for (int k = Math.Max(0, x - r); k <= Math.Min(w - 1, x + r) && v < 1f; k++) v = Math.Max(v, m[y * w + k]);
                tmp[y * w + x] = v;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0f;
                for (int k = Math.Max(0, y - r); k <= Math.Min(h - 1, y + r) && v < 1f; k++) v = Math.Max(v, tmp[k * w + x]);
                o[y * w + x] = v;
            }
        return o;
    }

    public static float[] Erode(float[] m, int w, int h, int r) { return Invert(Dilate(Invert(m), w, h, r)); }

    public static Img ToImg(float[] m, int w, int h)
    {
        Img o = new Img(w, h);
        for (int i = 0; i < w * h; i++) { float v = Math.Max(0f, Math.Min(1f, m[i])) * 255f; o.R[i] = o.G[i] = o.B[i] = v; }
        return o;
    }

    public static float[] Max(float[] a, float[] b) { float[] o = new float[a.Length]; for (int i = 0; i < a.Length; i++) o[i] = Math.Max(a[i], b[i]); return o; }
    public static float[] Mul(float[] a, float[] b) { float[] o = new float[a.Length]; for (int i = 0; i < a.Length; i++) o[i] = a[i] * b[i]; return o; }
}
