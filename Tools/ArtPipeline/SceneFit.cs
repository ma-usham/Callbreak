// Re-composes a mockup from its extracted parts and measures how closely the result matches,
// so each part's position, size and tilt can be fitted to the reference instead of eyeballed.
// C# 5 only: compiled by Windows PowerShell's Add-Type.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class SceneLayer
{
    public string Name;
    public Bitmap Image;
    public double CenterX, CenterY, Width, Rotation;   // mockup pixels; rotation clockwise, degrees
    public double Aspect;                               // width / height of the image
    public double Height { get { return Width / Aspect; } }
    public bool Visible = true;
}

public class Scene
{
    public Bitmap Plate;
    public List<SceneLayer> Layers = new List<SceneLayer>();

    public SceneLayer Add(string name, Bitmap image, double cx, double cy, double width, double rotation)
    {
        SceneLayer l = new SceneLayer();
        l.Name = name; l.Image = image; l.CenterX = cx; l.CenterY = cy; l.Width = width; l.Rotation = rotation;
        l.Aspect = (double)image.Width / image.Height;
        Layers.Add(l);
        return l;
    }

    public SceneLayer Get(string name)
    {
        foreach (SceneLayer l in Layers) if (l.Name == name) return l;
        return null;
    }

    // Renders the region of interest (mockup pixels) at the given scale.
    public Bitmap Render(RectangleF roi, float scale)
    {
        int w = (int)Math.Ceiling(roi.Width * scale), h = (int)Math.Ceiling(roi.Height * scale);
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-roi.X, -roi.Y);
            g.DrawImage(Plate, new RectangleF(0, 0, 1672f, 941f), new RectangleF(0, 0, Plate.Width, Plate.Height), GraphicsUnit.Pixel);
            foreach (SceneLayer l in Layers)
            {
                if (!l.Visible) continue;
                GraphicsState s = g.Save();
                g.TranslateTransform((float)l.CenterX, (float)l.CenterY);
                g.RotateTransform((float)l.Rotation);
                float lw = (float)l.Width, lh = (float)l.Height;
                g.DrawImage(l.Image, new RectangleF(-lw / 2f, -lh / 2f, lw, lh), new RectangleF(0, 0, l.Image.Width, l.Image.Height), GraphicsUnit.Pixel);
                g.Restore(s);
            }
        }
        return bmp;
    }

    // Mean absolute difference per channel between the render and the reference inside the region.
    public double Error(Bitmap reference, RectangleF roi)
    {
        Rectangle r = Rectangle.Round(roi);
        using (Bitmap render = Render(r, 1f))
        {
            BitmapData a = render.LockBits(new Rectangle(0, 0, render.Width, render.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData b = reference.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = r.Width * 4;
            byte[] ra = new byte[n], rb = new byte[n];
            long sum = 0;
            for (int y = 0; y < r.Height; y++)
            {
                Marshal.Copy(new IntPtr(a.Scan0.ToInt64() + (long)y * a.Stride), ra, 0, n);
                Marshal.Copy(new IntPtr(b.Scan0.ToInt64() + (long)y * b.Stride), rb, 0, n);
                for (int x = 0; x < r.Width; x++)
                {
                    int i = x * 4;
                    sum += Math.Abs(ra[i] - rb[i]) + Math.Abs(ra[i + 1] - rb[i + 1]) + Math.Abs(ra[i + 2] - rb[i + 2]);
                }
            }
            render.UnlockBits(a); reference.UnlockBits(b);
            return (double)sum / (r.Width * r.Height * 3.0);
        }
    }

    // Per-channel gain and offset (r, g, b pairs) that best map one layer's colours onto the
    // reference, judged where the layer is opaque. Pixels that disagree strongly (text or icons
    // lying on top of the layer in the reference) are dropped and the fit repeated.
    public double[] ColorFit(Bitmap reference, string name, RectangleF roi)
    {
        SceneLayer l = Get(name);
        Rectangle r = Rectangle.Round(roi);
        using (Bitmap alone = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(alone))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.TranslateTransform(-r.X, -r.Y);
                g.TranslateTransform((float)l.CenterX, (float)l.CenterY);
                g.RotateTransform((float)l.Rotation);
                float lw = (float)l.Width, lh = (float)l.Height;
                g.DrawImage(l.Image, new RectangleF(-lw / 2f, -lh / 2f, lw, lh), new RectangleF(0, 0, l.Image.Width, l.Image.Height), GraphicsUnit.Pixel);
            }
            BitmapData a = alone.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData b = reference.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = r.Width * r.Height;
            byte[] pa = new byte[n * 4], pb = new byte[n * 4];
            for (int y = 0; y < r.Height; y++)
            {
                Marshal.Copy(new IntPtr(a.Scan0.ToInt64() + (long)y * a.Stride), pa, y * r.Width * 4, r.Width * 4);
                Marshal.Copy(new IntPtr(b.Scan0.ToInt64() + (long)y * b.Stride), pb, y * r.Width * 4, r.Width * 4);
            }
            alone.UnlockBits(a); reference.UnlockBits(b);

            double[] result = new double[7];
            bool[] keep = new bool[n];
            for (int i = 0; i < n; i++)
            {
                // Start from pixels that already roughly agree, so overlaid text and icons
                // cannot drag the first estimate.
                keep[i] = pa[i * 4 + 3] >= 252
                    && Math.Abs(pa[i * 4] - pb[i * 4]) <= 40
                    && Math.Abs(pa[i * 4 + 1] - pb[i * 4 + 1]) <= 40
                    && Math.Abs(pa[i * 4 + 2] - pb[i * 4 + 2]) <= 40;
            }
            for (int round = 0; round < 3; round++)
            {
                long count = 0;
                for (int c = 0; c < 3; c++)
                {
                    double sx = 0, sy = 0, sxx = 0, sxy = 0; long m = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!keep[i]) continue;
                        double x = pa[i * 4 + 2 - c], yv = pb[i * 4 + 2 - c];
                        sx += x; sy += yv; sxx += x * x; sxy += x * yv; m++;
                    }
                    double den = m * sxx - sx * sx;
                    double gain = Math.Abs(den) < 1e-6 ? 1 : (m * sxy - sx * sy) / den;
                    if (gain < 0.7 || gain > 1.3) gain = 1;
                    double offset = m == 0 ? 0 : (sy - gain * sx) / m;
                    result[c * 2] = gain; result[c * 2 + 1] = offset; count = m;
                }
                result[6] = count;
                for (int i = 0; i < n; i++)
                {
                    if (!keep[i]) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        double predicted = pa[i * 4 + 2 - c] * result[c * 2] + result[c * 2 + 1];
                        if (Math.Abs(predicted - pb[i * 4 + 2 - c]) > 24) { keep[i] = false; break; }
                    }
                }
            }
            return result;
        }
    }

    // Coordinate descent over one layer. axes lists what may move: x, y, w (width), r (rotation).
    public double Fit(Bitmap reference, string name, RectangleF roi, string axes, double startStep)
    {
        SceneLayer l = Get(name);
        double best = Error(reference, roi);
        for (double step = startStep; step >= 0.2; step /= 2)
        {
            bool improved = true;
            int guard = 0;
            while (improved && guard++ < 40)
            {
                improved = false;
                for (int p = 0; p < 4; p++)
                {
                    if (axes.IndexOf("xywr"[p]) < 0) continue;
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        double delta = dir * step * (p == 2 ? 2 : (p == 3 ? 0.5 : 1));
                        Nudge(l, p, delta);
                        double e = Error(reference, roi);
                        if (e < best - 1e-6) { best = e; improved = true; }
                        else Nudge(l, p, -delta);
                    }
                }
            }
        }
        return best;
    }

    static void Nudge(SceneLayer l, int p, double d)
    {
        if (p == 0) l.CenterX += d;
        else if (p == 1) l.CenterY += d;
        else if (p == 2) l.Width += d;
        else l.Rotation += d;
    }
}
