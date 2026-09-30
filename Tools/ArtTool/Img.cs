using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ArtTool
{
    /// <summary>
    /// A float RGBA image in linear light with straight (not premultiplied) alpha. Everything that
    /// blends or solves for alpha works in linear light, because the project renders in the Linear
    /// colour space and uGUI blends there; files on disk are 8-bit sRGB.
    /// </summary>
    internal sealed class Img
    {
        public readonly int W;
        public readonly int H;
        public readonly float[] R;
        public readonly float[] G;
        public readonly float[] B;
        public readonly float[] A;

        private static readonly float[] ToLinearLut = BuildLut();

        public Img(int w, int h, float alpha = 1f)
        {
            W = w;
            H = h;
            R = new float[w * h];
            G = new float[w * h];
            B = new float[w * h];
            A = new float[w * h];
            if (alpha != 0f)
            {
                Array.Fill(A, alpha);
            }
        }

        public int Count => W * H;

        public static float ToLinear(byte v) => ToLinearLut[v];

        public static float ToLinear(float srgb01)
        {
            return srgb01 <= 0.04045f ? srgb01 / 12.92f : MathF.Pow((srgb01 + 0.055f) / 1.055f, 2.4f);
        }

        public static float ToSrgb01(float linear)
        {
            if (linear <= 0f) return 0f;
            if (linear >= 1f) return 1f;
            return linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
        }

        public static byte ToSrgbByte(float linear) => (byte)Math.Clamp((int)MathF.Round(ToSrgb01(linear) * 255f), 0, 255);

        private static float[] BuildLut()
        {
            var lut = new float[256];
            for (int i = 0; i < 256; i++) lut[i] = ToLinear(i / 255f);
            return lut;
        }

        public static Img Load(string path)
        {
            using var src = new Bitmap(path);
            using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }

            return FromBitmap(bmp);
        }

        public static Img FromBitmap(Bitmap bmp)
        {
            var img = new Img(bmp.Width, bmp.Height);
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var row = new byte[bmp.Width * 4];
            for (int y = 0; y < bmp.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), row, 0, row.Length);
                for (int x = 0; x < bmp.Width; x++)
                {
                    int i = y * bmp.Width + x;
                    img.B[i] = ToLinearLut[row[x * 4]];
                    img.G[i] = ToLinearLut[row[x * 4 + 1]];
                    img.R[i] = ToLinearLut[row[x * 4 + 2]];
                    img.A[i] = row[x * 4 + 3] / 255f;
                }
            }

            bmp.UnlockBits(d);
            return img;
        }

        public Bitmap ToBitmap()
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var row = new byte[W * 4];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    row[x * 4] = ToSrgbByte(B[i]);
                    row[x * 4 + 1] = ToSrgbByte(G[i]);
                    row[x * 4 + 2] = ToSrgbByte(R[i]);
                    row[x * 4 + 3] = (byte)Math.Clamp((int)MathF.Round(A[i] * 255f), 0, 255);
                }

                Marshal.Copy(row, 0, IntPtr.Add(d.Scan0, y * d.Stride), row.Length);
            }

            bmp.UnlockBits(d);
            return bmp;
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using Bitmap bmp = ToBitmap();
            bmp.Save(path, ImageFormat.Png);
        }

        public Img Clone()
        {
            var c = new Img(W, H, 0f);
            Array.Copy(R, c.R, R.Length);
            Array.Copy(G, c.G, G.Length);
            Array.Copy(B, c.B, B.Length);
            Array.Copy(A, c.A, A.Length);
            return c;
        }

        /// <summary>Copies a region; pixels outside the source come out transparent black.</summary>
        public Img Crop(int x0, int y0, int w, int h)
        {
            var c = new Img(w, h, 0f);
            for (int y = 0; y < h; y++)
            {
                int sy = y0 + y;
                if (sy < 0 || sy >= H) continue;
                for (int x = 0; x < w; x++)
                {
                    int sx = x0 + x;
                    if (sx < 0 || sx >= W) continue;
                    int s = sy * W + sx, t = y * w + x;
                    c.R[t] = R[s];
                    c.G[t] = G[s];
                    c.B[t] = B[s];
                    c.A[t] = A[s];
                }
            }

            return c;
        }

        /// <summary>Writes <paramref name="src"/> into this image at (x0, y0), replacing pixels.</summary>
        public void Paste(Img src, int x0, int y0)
        {
            for (int y = 0; y < src.H; y++)
            {
                int ty = y0 + y;
                if (ty < 0 || ty >= H) continue;
                for (int x = 0; x < src.W; x++)
                {
                    int tx = x0 + x;
                    if (tx < 0 || tx >= W) continue;
                    int s = y * src.W + x, t = ty * W + tx;
                    R[t] = src.R[s];
                    G[t] = src.G[s];
                    B[t] = src.B[s];
                    A[t] = src.A[s];
                }
            }
        }

        /// <summary>Alpha-blends <paramref name="src"/> (straight alpha) over this opaque image in linear light.</summary>
        public void Over(Img src, int x0, int y0, float opacity = 1f)
        {
            for (int y = 0; y < src.H; y++)
            {
                int ty = y0 + y;
                if (ty < 0 || ty >= H) continue;
                for (int x = 0; x < src.W; x++)
                {
                    int tx = x0 + x;
                    if (tx < 0 || tx >= W) continue;
                    int s = y * src.W + x, t = ty * W + tx;
                    float a = src.A[s] * opacity;
                    if (a <= 0f) continue;
                    R[t] = R[t] * (1f - a) + src.R[s] * a;
                    G[t] = G[t] * (1f - a) + src.G[s] * a;
                    B[t] = B[t] * (1f - a) + src.B[s] * a;
                }
            }
        }

        /// <summary>High-quality resize through GDI+ bicubic in premultiplied alpha (sRGB bytes).</summary>
        public Img Resize(int w, int h)
        {
            using Bitmap src = ToBitmap();
            using var pre = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(pre))
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }

            using Bitmap straight = pre.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
            return FromBitmap(straight);
        }

        public float Lum(int i) => 0.2126f * R[i] + 0.7152f * G[i] + 0.0722f * B[i];

        public static (float r, float g, float b) ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return (ToLinear(r), ToLinear(g), ToLinear(b));
        }

        public static string Hex(float r, float g, float b)
        {
            return $"#{ToSrgbByte(r):X2}{ToSrgbByte(g):X2}{ToSrgbByte(b):X2}";
        }
    }
}
