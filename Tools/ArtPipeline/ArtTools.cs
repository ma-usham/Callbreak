// Image helpers for turning ChatGPT extractions into exports. C# 5 only: compiled by
// Windows PowerShell's Add-Type.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ArtTools
{
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

    static byte[] Read(Bitmap bmp)
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

    static Bitmap Write(byte[] px, int w, int h)
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

    // High-quality resize. Works in premultiplied alpha so transparent edges do not pick up fringes.
    public static Bitmap Resize(Bitmap bmp, int w, int h)
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
            g.DrawImage(bmp, new Rectangle(0, 0, w, h), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        }
        Bitmap dst = pre.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
        pre.Dispose();
        return dst;
    }

    // Places the image centred on a transparent canvas of the given size.
    public static Bitmap Pad(Bitmap bmp, int w, int h)
    {
        Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.DrawImage(bmp, new Rectangle((w - bmp.Width) / 2, (h - bmp.Height) / 2, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
        }
        return dst;
    }

    public static Bitmap OnColor(Bitmap bmp, Color c)
    {
        Bitmap dst = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.Clear(c);
            g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
        }
        return dst;
    }

    // Alpha make-up and average visible colour, to spot fake transparency and colour drift.
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

    // Copies the left half, mirrored, over the right half so both ends match exactly.
    public static Bitmap MirrorLeftHalf(Bitmap bmp)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w / 2; x++)
            {
                int s = (y * w + x) * 4, t = (y * w + (w - 1 - x)) * 4;
                px[t] = px[s]; px[t + 1] = px[s + 1]; px[t + 2] = px[s + 2]; px[t + 3] = px[s + 3];
            }
        }
        return Write(px, w, h);
    }

    // Separable box blur applied three times, close to a Gaussian. Operates on all four channels.
    public static Bitmap Blur(Bitmap bmp, int radius)
    {
        int w = bmp.Width, h = bmp.Height;
        byte[] a = Read(bmp);
        byte[] t = new byte[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            BoxH(a, t, w, h, radius);
            BoxV(t, a, w, h, radius);
        }
        return Write(a, w, h);
    }

    static void BoxH(byte[] src, byte[] dst, int w, int h, int r)
    {
        int win = r * 2 + 1;
        for (int y = 0; y < h; y++)
        {
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += src[(y * w + Clamp(i, 0, w - 1)) * 4 + c];
                for (int x = 0; x < w; x++)
                {
                    dst[(y * w + x) * 4 + c] = (byte)(sum / win);
                    sum += src[(y * w + Clamp(x + r + 1, 0, w - 1)) * 4 + c] - src[(y * w + Clamp(x - r, 0, w - 1)) * 4 + c];
                }
            }
        }
    }

    static void BoxV(byte[] src, byte[] dst, int w, int h, int r)
    {
        int win = r * 2 + 1;
        for (int x = 0; x < w; x++)
        {
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += src[(Clamp(i, 0, h - 1) * w + x) * 4 + c];
                for (int y = 0; y < h; y++)
                {
                    dst[(y * w + x) * 4 + c] = (byte)(sum / win);
                    sum += src[(Clamp(y + r + 1, 0, h - 1) * w + x) * 4 + c] - src[(Clamp(y - r, 0, h - 1) * w + x) * 4 + c];
                }
            }
        }
    }

    static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

    // Patch technique: keeps the original everywhere except where the mask is white, where the
    // generated pixels show through. The mask's red channel is the weight of the generated image.
    public static Bitmap Patch(Bitmap original, Bitmap generated, Bitmap mask)
    {
        int w = original.Width, h = original.Height;
        byte[] o = Read(original), g = Read(generated), m = Read(mask);
        byte[] d = new byte[o.Length];
        for (int i = 0; i < w * h; i++)
        {
            int k = m[i * 4 + 2];
            for (int c = 0; c < 3; c++)
            {
                d[i * 4 + c] = (byte)((o[i * 4 + c] * (255 - k) + g[i * 4 + c] * k + 127) / 255);
            }
            d[i * 4 + 3] = 255;
        }
        return Write(d, w, h);
    }

    // Mean absolute difference per channel (0-255) between two same-sized images, optionally
    // only where the mask's red channel is below the limit.
    public static double MeanDifference(Bitmap a, Bitmap b, Bitmap mask, int maskLimit)
    {
        byte[] pa = Read(a), pb = Read(b);
        byte[] pm = mask != null ? Read(mask) : null;
        long sum = 0, count = 0;
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            if (pm != null && pm[i * 4 + 2] >= maskLimit) continue;
            for (int c = 0; c < 3; c++) sum += Math.Abs(pa[i * 4 + c] - pb[i * 4 + c]);
            count += 3;
        }
        return count == 0 ? 0 : (double)sum / count;
    }

    // Amplified absolute difference, for a visual check of where two images disagree.
    public static Bitmap Difference(Bitmap a, Bitmap b, int gain)
    {
        byte[] pa = Read(a), pb = Read(b);
        byte[] d = new byte[pa.Length];
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            for (int c = 0; c < 3; c++)
            {
                d[i * 4 + c] = (byte)Math.Min(255, Math.Abs(pa[i * 4 + c] - pb[i * 4 + c]) * gain);
            }
            d[i * 4 + 3] = 255;
        }
        return Write(d, a.Width, a.Height);
    }

    // Blends two same-sized images 50/50, for overlay checks.
    public static Bitmap Blend(Bitmap a, Bitmap b)
    {
        byte[] pa = Read(a), pb = Read(b);
        byte[] d = new byte[pa.Length];
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            for (int c = 0; c < 3; c++) d[i * 4 + c] = (byte)((pa[i * 4 + c] + pb[i * 4 + c]) / 2);
            d[i * 4 + 3] = 255;
        }
        return Write(d, a.Width, a.Height);
    }

    // Stretches alpha so that values at or below lo become clear and at or above hi become solid.
    // ChatGPT's cut-outs come back 250-254 inside and with a faint haze outside.
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

    // Overwrites the bottom-right corner block with a mirror image of the bottom-left one.
    // Used to blank a card's lower index: the blank lower-left face is flipped over it.
    public static Bitmap MirrorBottomRight(Bitmap bmp, double fromX, double fromY)
    {
        byte[] px = Read(bmp);
        int w = bmp.Width, h = bmp.Height, x0 = (int)(w * fromX), y0 = (int)(h * fromY);
        for (int y = y0; y < h; y++)
        {
            for (int x = x0; x < w; x++)
            {
                int s = (y * w + (w - 1 - x)) * 4, t = (y * w + x) * 4;
                px[t] = px[s]; px[t + 1] = px[s + 1]; px[t + 2] = px[s + 2]; px[t + 3] = px[s + 3];
            }
        }
        return Write(px, w, h);
    }

    // A soft drop shadow for a cut-out: its silhouette blurred, in one flat colour. The result is
    // padded by three blur radii on every side.
    public static Bitmap Shadow(Bitmap cutout, int blur, Color color, double opacity)
    {
        int pad = blur * 3, w = cutout.Width + pad * 2, h = cutout.Height + pad * 2;
        byte[] src = Read(cutout);
        byte[] px = new byte[w * h * 4];
        for (int y = 0; y < cutout.Height; y++)
        {
            for (int x = 0; x < cutout.Width; x++)
            {
                byte a = src[(y * cutout.Width + x) * 4 + 3];
                int t = ((y + pad) * w + x + pad) * 4;
                px[t] = a; px[t + 1] = a; px[t + 2] = a; px[t + 3] = a;
            }
        }
        using (Bitmap sharp = Write(px, w, h))
        using (Bitmap soft = Blur(sharp, blur))
        {
            byte[] b = Read(soft);
            for (int i = 0; i < w * h; i++)
            {
                int a = (int)Math.Round(b[i * 4 + 3] * opacity);
                b[i * 4] = color.B; b[i * 4 + 1] = color.G; b[i * 4 + 2] = color.R; b[i * 4 + 3] = (byte)a;
            }
            return Write(b, w, h);
        }
    }

    // A filled, anti-aliased disc: rebuilt geometry such as the white ring behind the avatar.
    public static Bitmap Disc(int size, Color color)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        using (SolidBrush brush = new SolidBrush(color))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.FillEllipse(brush, 0.5f, 0.5f, size - 1f, size - 1f);
        }
        return bmp;
    }

    // Applies a per-channel gain and offset (r, g, b pairs), leaving alpha alone: undoes the
    // colour drift an extraction picked up, as measured against the reference.
    public static Bitmap ApplyColor(Bitmap bmp, double[] k)
    {
        byte[] px = Read(bmp);
        int n = bmp.Width * bmp.Height;
        for (int i = 0; i < n; i++)
        {
            if (px[i * 4 + 3] == 0) continue;
            for (int c = 0; c < 3; c++)
            {
                double v = px[i * 4 + 2 - c] * k[c * 2] + k[c * 2 + 1];
                px[i * 4 + 2 - c] = (byte)(v < 0 ? 0 : (v > 255 ? 255 : Math.Round(v)));
            }
        }
        return Write(px, bmp.Width, bmp.Height);
    }

    // Moves the black point: pixels of colour 'from' become 'to', white stays white and everything
    // between scales in proportion. Brings a silhouette icon's ink to the reference's ink.
    public static Bitmap BlackPoint(Bitmap bmp, Color from, Color to)
    {
        double[] k = new double[6];
        int[] f = { from.R, from.G, from.B }, t = { to.R, to.G, to.B };
        for (int c = 0; c < 3; c++)
        {
            k[c * 2] = (255.0 - t[c]) / (255.0 - f[c]);
            k[c * 2 + 1] = t[c] - f[c] * k[c * 2];
        }
        return ApplyColor(bmp, k);
    }

    // Measures a line of text inside a region: left and right ink edges, the cap line and the
    // baseline (rows where the text body starts and ends, ignoring descenders and stray marks),
    // and the full ink box. Ink is whatever differs from the region's typical brightness by more
    // than the threshold, darker or lighter as asked. Returns left, right, capTop, baseline,
    // boxTop, boxBottom in image pixels (right and bottom inclusive), or null when no ink.
    public static double[] InkBox(Bitmap bmp, int x, int y, int w, int h, bool darkInk, int threshold)
    {
        byte[] px = Read(bmp);
        int W = bmp.Width;
        int[] lum = new int[w * h];
        int[] hist = new int[256];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                int o = ((y + j) * W + x + i) * 4;
                int l = (px[o + 2] * 2126 + px[o + 1] * 7152 + px[o] * 722) / 10000;
                lum[j * w + i] = l; hist[l]++;
            }
        }
        // Background = the most common brightness on the far side from the ink.
        int bg = 0, best = -1;
        for (int l = 0; l < 256; l++) if (hist[l] > best) { best = hist[l]; bg = l; }
        int[] rows = new int[h], cols = new int[w];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                int d = darkInk ? bg - lum[j * w + i] : lum[j * w + i] - bg;
                if (d > threshold) { rows[j]++; cols[i]++; }
            }
        }
        int left = -1, right = -1, top = -1, bottom = -1, peak = 0;
        for (int i = 0; i < w; i++) if (cols[i] > 0) { if (left < 0) left = i; right = i; }
        for (int j = 0; j < h; j++) { if (rows[j] > 0) { if (top < 0) top = j; bottom = j; } if (rows[j] > peak) peak = rows[j]; }
        if (left < 0) return null;
        int capTop = top, baseline = bottom;
        for (int j = top; j <= bottom; j++) if (rows[j] >= peak * 0.30) { capTop = j; break; }
        for (int j = bottom; j >= top; j--) if (rows[j] >= peak * 0.45) { baseline = j; break; }
        return new double[] { x + left, x + right, y + capTop, y + baseline, y + top, y + bottom };
    }

    // Replaces the colour of every pixel, keeping its alpha: flat single-colour icons.
    public static Bitmap Recolor(Bitmap bmp, Color c)
    {
        byte[] px = Read(bmp);
        int n = bmp.Width * bmp.Height;
        for (int i = 0; i < n; i++)
        {
            px[i * 4] = c.B; px[i * 4 + 1] = c.G; px[i * 4 + 2] = c.R;
        }
        return Write(px, bmp.Width, bmp.Height);
    }
}
