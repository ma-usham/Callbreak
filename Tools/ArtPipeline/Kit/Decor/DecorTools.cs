// Helpers of the Decor designer (decorations, avatars, backgrounds): keying, fitting a ChatGPT
// extraction onto its mockup rectangle, colour matching, comparison sheets, wide-background
// assembly. C# 5 only: compiled by Windows PowerShell's Add-Type (see decor.ps1).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public static class DecorTools
{
    // ---------------------------------------------------------------- pixels

    public static Bitmap Load(string path)
    {
        using (Bitmap src = new Bitmap(path))
        {
            Bitmap bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            return bmp;
        }
    }

    public static void Save(Bitmap bmp, string path)
    {
        bmp.Save(path, ImageFormat.Png);
    }

    public static byte[] Read(Bitmap bmp)
    {
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] px = new byte[bmp.Width * bmp.Height * 4];
        for (int y = 0; y < bmp.Height; y++)
        {
            Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), px, y * bmp.Width * 4, bmp.Width * 4);
        }
        bmp.UnlockBits(d);
        return px; // BGRA
    }

    public static Bitmap Write(byte[] px, int w, int h)
    {
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
        {
            Marshal.Copy(px, y * w * 4, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w * 4);
        }
        bmp.UnlockBits(d);
        return bmp;
    }

    static double Clamp01(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
    static byte ToByte(double v) { return (byte)(v < 0 ? 0 : (v > 255 ? 255 : Math.Round(v))); }
    static double Smooth(double t) { t = Clamp01(t); return t * t * (3 - 2 * t); }

    // High-quality resample of a source rectangle (fractional) into a w x h bitmap, premultiplied
    // so transparent edges do not pick up fringes.
    public static Bitmap Resample(Bitmap bmp, double sx, double sy, double sw, double sh, int w, int h)
    {
        Bitmap pre = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(pre))
        using (ImageAttributes attrs = new ImageAttributes())
        {
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(bmp, new Rectangle(0, 0, w, h), (float)sx, (float)sy, (float)sw, (float)sh, GraphicsUnit.Pixel, attrs);
        }
        Bitmap dst = pre.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
        pre.Dispose();
        return dst;
    }

    public static Bitmap Resize(Bitmap bmp, int w, int h)
    {
        return Resample(bmp, 0, 0, bmp.Width, bmp.Height, w, h);
    }

    public static Bitmap Crop(Bitmap bmp, int x, int y, int w, int h)
    {
        Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(bmp, new Rectangle(0, 0, w, h), x, y, w, h, GraphicsUnit.Pixel);
        }
        return dst;
    }

    public static Bitmap FlipH(Bitmap bmp)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height;
        byte[] d = new byte[px.Length];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Buffer.BlockCopy(px, (y * w + x) * 4, d, (y * w + (w - 1 - x)) * 4, 4);
            }
        }
        return Write(d, w, h);
    }

    // Transparent canvas of the given size with the image drawn at (x, y).
    public static Bitmap Place(Bitmap bmp, int w, int h, int x, int y)
    {
        Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.DrawImage(bmp, new Rectangle(x, y, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
        }
        return dst;
    }

    // Straight-alpha "over": sprite (already at the size it is drawn) over an opaque or
    // transparent background, at integer offset.
    public static Bitmap Over(Bitmap bg, Bitmap sprite, int ox, int oy)
    {
        byte[] b = Read(bg), s = Read(sprite);
        int W = bg.Width, H = bg.Height, w = sprite.Width, h = sprite.Height;
        for (int y = 0; y < h; y++)
        {
            int ty = y + oy; if (ty < 0 || ty >= H) continue;
            for (int x = 0; x < w; x++)
            {
                int tx = x + ox; if (tx < 0 || tx >= W) continue;
                int si = (y * w + x) * 4, ti = (ty * W + tx) * 4;
                double sa = s[si + 3] / 255.0, ba = b[ti + 3] / 255.0;
                double oa = sa + ba * (1 - sa);
                if (oa <= 0) { b[ti] = b[ti + 1] = b[ti + 2] = b[ti + 3] = 0; continue; }
                for (int c = 0; c < 3; c++)
                {
                    b[ti + c] = ToByte((s[si + c] * sa + b[ti + c] * ba * (1 - sa)) / oa);
                }
                b[ti + 3] = ToByte(oa * 255);
            }
        }
        return Write(b, W, H);
    }

    public static Bitmap Flat(int w, int h, Color c)
    {
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(c); }
        return bmp;
    }

    // ---------------------------------------------------------------- alpha

    // Bounding box of pixels whose alpha exceeds the threshold: x, y, width, height.
    public static int[] AlphaBounds(Bitmap bmp, int threshold)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height, minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[(y * w + x) * 4 + 3] > threshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return new int[] { 0, 0, 0, 0 };
        return new int[] { minX, minY, maxX - minX + 1, maxY - minY + 1 };
    }

    // Median colour of the outermost ring of pixels: the flat ground ChatGPT put behind a cut-out.
    public static Color BorderColor(Bitmap bmp)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height;
        List<int> r = new List<int>(), g = new List<int>(), b = new List<int>();
        for (int x = 0; x < w; x++)
        {
            foreach (int y in new int[] { 0, 1, h - 2, h - 1 })
            {
                int i = (y * w + x) * 4; b.Add(px[i]); g.Add(px[i + 1]); r.Add(px[i + 2]);
            }
        }
        for (int y = 0; y < h; y++)
        {
            foreach (int x in new int[] { 0, 1, w - 2, w - 1 })
            {
                int i = (y * w + x) * 4; b.Add(px[i]); g.Add(px[i + 1]); r.Add(px[i + 2]);
            }
        }
        r.Sort(); g.Sort(); b.Sort();
        return Color.FromArgb(r[r.Count / 2], g[g.Count / 2], b[b.Count / 2]);
    }

    // Keys out a flat ground colour. Alpha rises from 0 at colour distance lo to 1 at hi (distance
    // = largest channel difference, 0-255); edge pixels are un-mixed from the key so no halo of
    // the key colour remains. With connected = true only ground reachable from the image border
    // is removed (flood fill through pixels below hi), so the same colour inside the object stays.
    public static Bitmap KeyColor(Bitmap bmp, Color key, double lo, double hi, bool connected)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height, n = w * h;
        double[] dist = new double[n];
        double kr = key.R, kg = key.G, kb = key.B;
        for (int i = 0; i < n; i++)
        {
            double db = Math.Abs(px[i * 4] - kb), dg = Math.Abs(px[i * 4 + 1] - kg), dr = Math.Abs(px[i * 4 + 2] - kr);
            dist[i] = Math.Max(dr, Math.Max(dg, db));
        }
        bool[] ground = new bool[n];
        if (connected)
        {
            Queue<int> q = new Queue<int>();
            Action<int> seed = delegate(int i) { if (!ground[i] && dist[i] < hi) { ground[i] = true; q.Enqueue(i); } };
            for (int x = 0; x < w; x++) { seed(x); seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { seed(y * w); seed(y * w + w - 1); }
            while (q.Count > 0)
            {
                int i = q.Dequeue(); int x = i % w, y = i / w;
                if (x > 0) seed(i - 1);
                if (x < w - 1) seed(i + 1);
                if (y > 0) seed(i - w);
                if (y < h - 1) seed(i + w);
            }
        }
        else
        {
            for (int i = 0; i < n; i++) ground[i] = dist[i] < hi;
        }
        for (int i = 0; i < n; i++)
        {
            double a0 = px[i * 4 + 3] / 255.0;
            double a = ground[i] ? Clamp01((dist[i] - lo) / (hi - lo)) : 1.0;
            if (a <= 0.004) { px[i * 4] = px[i * 4 + 1] = px[i * 4 + 2] = px[i * 4 + 3] = 0; continue; }
            if (a < 1.0)
            {
                px[i * 4] = ToByte((px[i * 4] - (1 - a) * kb) / a);
                px[i * 4 + 1] = ToByte((px[i * 4 + 1] - (1 - a) * kg) / a);
                px[i * 4 + 2] = ToByte((px[i * 4 + 2] - (1 - a) * kr) / a);
            }
            px[i * 4 + 3] = ToByte(a * a0 * 255);
        }
        return Write(px, w, h);
    }

    // Stretches alpha so that values at or below lo become clear and at or above hi become solid.
    public static Bitmap AlphaLevels(Bitmap bmp, int lo, int hi)
    {
        byte[] px = Read(bmp);
        int n = bmp.Width * bmp.Height;
        for (int i = 0; i < n; i++)
        {
            int a = px[i * 4 + 3];
            int v = a <= lo ? 0 : (a >= hi ? 255 : (a - lo) * 255 / (hi - lo));
            px[i * 4 + 3] = (byte)v;
            if (v == 0) { px[i * 4] = 0; px[i * 4 + 1] = 0; px[i * 4 + 2] = 0; }
        }
        return Write(px, bmp.Width, bmp.Height);
    }

    // Removes small islands: connected groups of non-transparent pixels smaller than minArea that
    // do not touch the main object (stray specks ChatGPT leaves around a cut-out).
    public static Bitmap DropSpecks(Bitmap bmp, int alphaThreshold, int minArea)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height, n = w * h;
        int[] label = new int[n];
        List<int> sizes = new List<int>(); sizes.Add(0);
        int[] stack = new int[n];
        for (int i = 0; i < n; i++)
        {
            if (label[i] != 0 || px[i * 4 + 3] <= alphaThreshold) continue;
            int id = sizes.Count, sp = 0, count = 0;
            stack[sp++] = i; label[i] = id;
            while (sp > 0)
            {
                int p = stack[--sp]; count++;
                int x = p % w, y = p / w;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        int q = yy * w + xx;
                        if (label[q] == 0 && px[q * 4 + 3] > alphaThreshold) { label[q] = id; stack[sp++] = q; }
                    }
                }
            }
            sizes.Add(count);
        }
        for (int i = 0; i < n; i++)
        {
            if (label[i] != 0 && sizes[label[i]] < minArea) { px[i * 4] = px[i * 4 + 1] = px[i * 4 + 2] = px[i * 4 + 3] = 0; }
            else if (label[i] == 0) { px[i * 4] = px[i * 4 + 1] = px[i * 4 + 2] = px[i * 4 + 3] = 0; }
        }
        return Write(px, w, h);
    }

    public static string Stats(Bitmap bmp)
    {
        byte[] px = Read(bmp);
        long clear = 0, solid = 0, semi = 0, r = 0, g = 0, b = 0;
        int n = bmp.Width * bmp.Height;
        for (int i = 0; i < n; i++)
        {
            byte a = px[i * 4 + 3];
            if (a == 0) clear++;
            else if (a == 255) { solid++; b += px[i * 4]; g += px[i * 4 + 1]; r += px[i * 4 + 2]; }
            else semi++;
        }
        string avg = solid > 0 ? string.Format("#{0:X2}{1:X2}{2:X2}", r / solid, g / solid, b / solid) : "-";
        return string.Format("{0}x{1}  clear {2:P1}  solid {3:P1}  semi {4:P1}  avg solid {5}  corner alpha {6}",
            bmp.Width, bmp.Height, (double)clear / n, (double)solid / n, (double)semi / n, avg, px[3]);
    }

    // ---------------------------------------------------------------- colour

    // Per-channel gain and offset (r gain, r offset, g gain, g offset, b gain, b offset) that bring
    // 'cand' onto 'reference' by least squares, over pixels where the mask alpha (from 'alphaOf',
    // may be null) is at least minAlpha. Both images must have the same size.
    public static double[] FitColor(Bitmap reference, Bitmap cand, Bitmap alphaOf, int minAlpha)
    {
        byte[] a = Read(reference), b = Read(cand);
        byte[] m = alphaOf != null ? Read(alphaOf) : null;
        int n = reference.Width * reference.Height;
        double[] k = new double[6];
        for (int c = 0; c < 3; c++)
        {
            int ch = 2 - c;
            double sx = 0, sy = 0, sxx = 0, sxy = 0; long cnt = 0;
            for (int i = 0; i < n; i++)
            {
                if (m != null && m[i * 4 + 3] < minAlpha) continue;
                double x = b[i * 4 + ch], y = a[i * 4 + ch];
                sx += x; sy += y; sxx += x * x; sxy += x * y; cnt++;
            }
            double den = cnt * sxx - sx * sx;
            if (cnt < 16 || Math.Abs(den) < 1e-6) { k[c * 2] = 1; k[c * 2 + 1] = cnt > 0 ? (sy - sx) / cnt : 0; continue; }
            double gain = (cnt * sxy - sx * sy) / den;
            if (gain < 0.6 || gain > 1.6) { gain = 1; }
            k[c * 2] = gain; k[c * 2 + 1] = (sy - gain * sx) / cnt;
        }
        return k;
    }

    // Offset-only version: mean difference per channel (reference - cand) over the masked pixels.
    public static double[] FitOffset(Bitmap reference, Bitmap cand, Bitmap alphaOf, int minAlpha)
    {
        byte[] a = Read(reference), b = Read(cand);
        byte[] m = alphaOf != null ? Read(alphaOf) : null;
        int n = reference.Width * reference.Height;
        double[] k = new double[] { 1, 0, 1, 0, 1, 0 };
        for (int c = 0; c < 3; c++)
        {
            int ch = 2 - c; double s = 0; long cnt = 0;
            for (int i = 0; i < n; i++)
            {
                if (m != null && m[i * 4 + 3] < minAlpha) continue;
                s += a[i * 4 + ch] - b[i * 4 + ch]; cnt++;
            }
            k[c * 2 + 1] = cnt > 0 ? s / cnt : 0;
        }
        return k;
    }

    public static Bitmap ApplyColor(Bitmap bmp, double[] k)
    {
        byte[] px = Read(bmp);
        int n = bmp.Width * bmp.Height;
        for (int i = 0; i < n; i++)
        {
            if (px[i * 4 + 3] == 0) continue;
            for (int c = 0; c < 3; c++)
            {
                px[i * 4 + 2 - c] = ToByte(px[i * 4 + 2 - c] * k[c * 2] + k[c * 2 + 1]);
            }
        }
        return Write(px, bmp.Width, bmp.Height);
    }

    // Mean colour and mean absolute difference between two same-sized images inside a rectangle,
    // optionally only where alphaOf is at least minAlpha.
    public static string Compare(Bitmap reference, Bitmap cand, Bitmap alphaOf, int minAlpha, int x0, int y0, int rw, int rh)
    {
        byte[] a = Read(reference), b = Read(cand);
        byte[] m = alphaOf != null ? Read(alphaOf) : null;
        int w = reference.Width, h = reference.Height;
        double[] sa = new double[3], sb = new double[3]; double mad = 0; long n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y0 + rh); y++)
        {
            for (int x = Math.Max(0, x0); x < Math.Min(w, x0 + rw); x++)
            {
                int i = (y * w + x) * 4;
                if (m != null && m[i + 3] < minAlpha) continue;
                for (int c = 0; c < 3; c++)
                {
                    sa[c] += a[i + 2 - c]; sb[c] += b[i + 2 - c];
                    mad += Math.Abs(a[i + 2 - c] - b[i + 2 - c]) / 3.0;
                }
                n++;
            }
        }
        if (n == 0) return "no pixels";
        return string.Format("ref {0:F0},{1:F0},{2:F0}  cand {3:F0},{4:F0},{5:F0}  d {6:F1},{7:F1},{8:F1}  meanAbs {9:F1}  (n={10})",
            sa[0] / n, sa[1] / n, sa[2] / n, sb[0] / n, sb[1] / n, sb[2] / n,
            (sb[0] - sa[0]) / n, (sb[1] - sa[1]) / n, (sb[2] - sa[2]) / n, mad / n, n);
    }

    public static Bitmap Difference(Bitmap a, Bitmap b, double gain)
    {
        byte[] pa = Read(a), pb = Read(b);
        byte[] d = new byte[pa.Length];
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            for (int c = 0; c < 3; c++) d[i * 4 + c] = ToByte(Math.Abs(pa[i * 4 + c] - pb[i * 4 + c]) * gain);
            d[i * 4 + 3] = 255;
        }
        return Write(d, a.Width, a.Height);
    }

    // ---------------------------------------------------------------- alignment

    static double[] Lum(byte[] a, int n)
    {
        double[] l = new double[n];
        for (int i = 0; i < n; i++) l[i] = 0.114 * a[i * 4] + 0.587 * a[i * 4 + 1] + 0.299 * a[i * 4 + 2];
        return l;
    }

    static double[] Grad(double[] l, int w, int h)
    {
        double[] g = new double[w * h];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                double gx = l[y * w + x + 1] - l[y * w + x - 1], gy = l[(y + 1) * w + x] - l[(y - 1) * w + x];
                g[y * w + x] = Math.Sqrt(gx * gx + gy * gy);
            }
        }
        return g;
    }

    // For a grid of tiles inside a rectangle, the shift (dx, dy) of 'cand' that best matches
    // 'reference' (on luminance gradients, so colour drift does not dominate), with the error at
    // no shift and at the best shift, and the mean absolute colour difference of the tile.
    public static string ShiftGrid(Bitmap reference, Bitmap cand, int x0, int y0, int rw, int rh, int cols, int rows, int maxShift)
    {
        int w = reference.Width, h = reference.Height;
        byte[] a = Read(reference), b = Read(cand);
        double[] ga = Grad(Lum(a, w * h), w, h), gb = Grad(Lum(b, w * h), w, h);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("tile(x,y,w,h)\tdx\tdy\terr0\terrBest\tmeanAbs");
        int tw = rw / cols, th = rh / rows;
        for (int ty = 0; ty < rows; ty++)
        {
            for (int tx = 0; tx < cols; tx++)
            {
                int ox = x0 + tx * tw, oy = y0 + ty * th;
                int bx = 0, by = 0; double best = double.MaxValue, err0 = 0;
                for (int dy = -maxShift; dy <= maxShift; dy++)
                {
                    for (int dx = -maxShift; dx <= maxShift; dx++)
                    {
                        double s = 0; int n = 0;
                        for (int y = oy; y < oy + th; y += 2)
                        {
                            for (int x = ox; x < ox + tw; x += 2)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                                double d = ga[y * w + x] - gb[yy * w + xx];
                                s += d * d; n++;
                            }
                        }
                        s = n > 0 ? s / n : double.MaxValue;
                        if (dx == 0 && dy == 0) err0 = s;
                        if (s < best) { best = s; bx = dx; by = dy; }
                    }
                }
                double mad = 0; int cnt = 0;
                for (int y = oy; y < oy + th; y++)
                {
                    for (int x = ox; x < ox + tw; x++)
                    {
                        int i = (y * w + x) * 4;
                        mad += (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2])) / 3.0; cnt++;
                    }
                }
                sb.AppendLine(string.Format("{0},{1},{2},{3}\t{4}\t{5}\t{6:F0}\t{7:F0}\t{8:F1}", ox, oy, tw, th, bx, by, err0, best, mad / cnt));
            }
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- backgrounds

    // Pastes 'centre' at (x, y) on 'canvas'. The centre is fully opaque except within 'feather'
    // pixels of its own edge, where it fades out (smoothstep), so the canvas shows through at
    // the seam and the two pictures cross-fade instead of meeting at a hard line.
    public static Bitmap FeatherPaste(Bitmap canvas, Bitmap centre, int x, int y, int feather)
    {
        byte[] c = Read(canvas), s = Read(centre);
        int W = canvas.Width, H = canvas.Height, w = centre.Width, h = centre.Height;
        for (int j = 0; j < h; j++)
        {
            int ty = j + y; if (ty < 0 || ty >= H) continue;
            for (int i = 0; i < w; i++)
            {
                int tx = i + x; if (tx < 0 || tx >= W) continue;
                int edge = Math.Min(Math.Min(i, w - 1 - i), Math.Min(j, h - 1 - j));
                double a = feather <= 0 ? 1 : Smooth((edge + 0.5) / feather);
                int si = (j * w + i) * 4, ti = (ty * W + tx) * 4;
                for (int ch = 0; ch < 3; ch++) c[ti + ch] = ToByte(s[si + ch] * a + c[ti + ch] * (1 - a));
                c[ti + 3] = 255;
            }
        }
        return Write(c, W, H);
    }

    // Unsharp mask on the colour channels (radius in pixels, amount 0-1): brings an upscaled
    // border back towards the crispness of the original centre.
    public static Bitmap Sharpen(Bitmap bmp, int radius, double amount)
    {
        int w = bmp.Width, h = bmp.Height;
        byte[] a = Read(bmp);
        double[][] ch = new double[3][];
        for (int c = 0; c < 3; c++)
        {
            double[] v = new double[w * h];
            for (int i = 0; i < w * h; i++) v[i] = a[i * 4 + c];
            double[] blur = Box(Box(v, w, h, radius), w, h, radius);
            for (int i = 0; i < w * h; i++) a[i * 4 + c] = ToByte(v[i] + (v[i] - blur[i]) * amount);
        }
        return Write(a, w, h);
    }

    static double[] Box(double[] src, int w, int h, int r)
    {
        double[] tmp = new double[w * h], dst = new double[w * h];
        for (int y = 0; y < h; y++)
        {
            double s = 0; int n = 0;
            for (int x = 0; x <= r && x < w; x++) { s += src[y * w + x]; n++; }
            for (int x = 0; x < w; x++)
            {
                tmp[y * w + x] = s / n;
                int xo = x - r, xi = x + r + 1;
                if (xo >= 0) { s -= src[y * w + xo]; n--; }
                if (xi < w) { s += src[y * w + xi]; n++; }
            }
        }
        for (int x = 0; x < w; x++)
        {
            double s = 0; int n = 0;
            for (int y = 0; y <= r && y < h; y++) { s += tmp[y * w + x]; n++; }
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = s / n;
                int yo = y - r, yi = y + r + 1;
                if (yo >= 0) { s -= tmp[yo * w + x]; n--; }
                if (yi < h) { s += tmp[yi * w + x]; n++; }
            }
        }
        return dst;
    }

    // Local-contrast view (value minus its neighbourhood mean, amplified): ghost edges, seams and
    // leftover shapes in pale areas become visible.
    public static Bitmap HighPass(Bitmap bmp, int radius, double gain)
    {
        int w = bmp.Width, h = bmp.Height;
        byte[] a = Read(bmp);
        double[] l = Lum(a, w * h);
        double[] m = Box(l, w, h, radius);
        byte[] o = new byte[a.Length];
        for (int i = 0; i < w * h; i++)
        {
            byte v = ToByte((l[i] - m[i]) * gain + 128);
            o[i * 4] = v; o[i * 4 + 1] = v; o[i * 4 + 2] = v; o[i * 4 + 3] = 255;
        }
        return Write(o, w, h);
    }

    // Counts pixels close to a colour (largest channel difference below tol).
    public static int CountNear(Bitmap bmp, Color c, int tol)
    {
        byte[] px = Read(bmp);
        int n = bmp.Width * bmp.Height, count = 0;
        for (int i = 0; i < n; i++)
        {
            if (Math.Abs(px[i * 4] - c.B) < tol && Math.Abs(px[i * 4 + 1] - c.G) < tol && Math.Abs(px[i * 4 + 2] - c.R) < tol) count++;
        }
        return count;
    }
}
