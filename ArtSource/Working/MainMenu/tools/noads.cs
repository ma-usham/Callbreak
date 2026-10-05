#:property PublishAot=false
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property Nullable=disable
#:property ImplicitUsings=disable
#:property InvariantGlobalization=true
#:property NoWarn=CA1416
// noads - turns ChatGPT's No Ads glyph into the main menu's sprite (run with noads.sh, see make.sh).
//
//   stats   --in f.png                                       size, ink box, colour of the opaque pixels
//   make    --src chatgpt.png --out sprite.png --diameter D [--ink #231C19]
//           the glyph's ring is D mockup px across; exported at @2x (x 2.3) in one flat ink, alpha from the
//           drawing's coverage (solved in linear light, as the project renders), 2 px clear margin
//   compose --disc disc.png --glyph sprite.png --out f.png [--bg #F4EBE1] [--zoom 4]
//           the glyph centred on the disc over a flat ground, both at their @2x size, enlarged for review
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
return NoAds.Run(args);

internal sealed class Img
{
    public readonly int W, H;
    public readonly float[] R, G, B, A;   // linear light, straight alpha

    public Img(int w, int h)
    {
        W = w; H = h;
        R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; A = new float[w * h];
    }

    public static float ToLinear(float s) => s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f);
    public static float ToSrgb(float l) => l <= 0f ? 0f : l >= 1f ? 1f : l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
    public static byte ToByte(float l) => (byte)Math.Clamp((int)MathF.Round(ToSrgb(l) * 255f), 0, 255);

    public static Img Load(string path)
    {
        using var bmp = new Bitmap(path);
        var img = new Img(bmp.Width, bmp.Height);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var row = new byte[bmp.Width * 4];
        for (int y = 0; y < bmp.Height; y++)
        {
            Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), row, 0, row.Length);
            for (int x = 0; x < bmp.Width; x++)
            {
                int i = y * bmp.Width + x;
                img.B[i] = ToLinear(row[x * 4] / 255f); img.G[i] = ToLinear(row[x * 4 + 1] / 255f); img.R[i] = ToLinear(row[x * 4 + 2] / 255f);
                img.A[i] = row[x * 4 + 3] / 255f;
            }
        }
        bmp.UnlockBits(d);
        return img;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var row = new byte[W * 4];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                row[x * 4] = ToByte(B[i]); row[x * 4 + 1] = ToByte(G[i]); row[x * 4 + 2] = ToByte(R[i]);
                row[x * 4 + 3] = (byte)Math.Clamp((int)MathF.Round(A[i] * 255f), 0, 255);
            }
            Marshal.Copy(row, 0, IntPtr.Add(d.Scan0, y * d.Stride), row.Length);
        }
        bmp.UnlockBits(d);
        bmp.Save(path, ImageFormat.Png);
    }

    public static (float r, float g, float b) Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return (ToLinear(Convert.ToInt32(hex.Substring(0, 2), 16) / 255f), ToLinear(Convert.ToInt32(hex.Substring(2, 2), 16) / 255f), ToLinear(Convert.ToInt32(hex.Substring(4, 2), 16) / 255f));
    }
}

internal static class NoAds
{
    private const double Scale2x = 2.3;
    private const int Margin = 2;

    public static int Run(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("usage: stats | make | compose (see the header)"); return 2; }
        var o = new Dictionary<string, string>();
        for (int i = 1; i + 1 < args.Length; i += 2) o[args[i].TrimStart('-')] = args[i + 1];
        switch (args[0])
        {
            case "stats": return Stats(o["in"]);
            case "make": return Make(o["src"], o["out"], double.Parse(o["diameter"]), o.TryGetValue("ink", out string ink) ? ink : "#231C19");
            case "compose": return Compose(o["disc"], o["glyph"], o["out"], o.TryGetValue("bg", out string bg) ? bg : "#F4EBE1", o.TryGetValue("zoom", out string z) ? int.Parse(z) : 4);
            default: Console.Error.WriteLine("unknown command " + args[0]); return 2;
        }
    }

    private static float Lum(Img m, int i) => 0.2126f * m.R[i] + 0.7152f * m.G[i] + 0.0722f * m.B[i];

    private static int Stats(string path)
    {
        Img m = Img.Load(path);
        int l = m.W, t = m.H, r = -1, b = -1, n = 0;
        double sr = 0, sg = 0, sb = 0;
        for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                int i = y * m.W + x;
                if (m.A[i] > 0.004f) { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y); }
                if (m.A[i] > 0.9f) { sr += m.R[i]; sg += m.G[i]; sb += m.B[i]; n++; }
            }
        string col = n > 0 ? $"#{Img.ToByte((float)(sr / n)):X2}{Img.ToByte((float)(sg / n)):X2}{Img.ToByte((float)(sb / n)):X2}" : "-";
        Console.WriteLine($"{Path.GetFileName(path)}: {m.W}x{m.H}, alpha box x {l}..{r} y {t}..{b} (margins l {l} t {t} r {m.W - 1 - r} b {m.H - 1 - b}), opaque colour {col} over {n} px");
        return 0;
    }

    /// <summary>
    /// Coverage of the glyph in ChatGPT's picture: 0 on its white ground, 1 on its ink, measured on
    /// luminance in linear light. The ground and ink levels are the picture's own (median of the
    /// brightest and of the darkest pixels), and the faint noise of a generated picture is cut at both ends.
    /// </summary>
    private static float[] Coverage(Img src)
    {
        var lum = new float[src.W * src.H];
        for (int i = 0; i < lum.Length; i++) lum[i] = Lum(src, i);
        var sorted = (float[])lum.Clone();
        Array.Sort(sorted);
        float ink = sorted[(int)(sorted.Length * 0.02)];
        float ground = sorted[(int)(sorted.Length * 0.75)];
        Console.WriteLine($"ink level {Img.ToByte(ink)}, ground level {Img.ToByte(ground)} (sRGB)");
        var cov = new float[lum.Length];
        for (int i = 0; i < lum.Length; i++)
        {
            float c = (ground - lum[i]) / (ground - ink);
            c = (c - 0.06f) / 0.88f;
            cov[i] = Math.Clamp(c, 0f, 1f);
        }
        return cov;
    }

    private static int Make(string srcPath, string outPath, double diameter, string inkHex)
    {
        Img src = Img.Load(srcPath);
        float[] cov = Coverage(src);

        // The ring's box: rows and columns that hold real ink.
        int l = src.W, t = src.H, r = -1, b = -1;
        for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
                if (cov[y * src.W + x] >= 0.5f) { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y); }
        double bw = r - l + 1, bh = b - t + 1;
        double cx = (l + r + 1) / 2.0, cy = (t + b + 1) / 2.0;
        double across = (bw + bh) / 2.0;
        Console.WriteLine($"ring box x {l}..{r} y {t}..{b}: {bw} x {bh} px (aspect {bw / bh:0.000}), centre {cx:0.0},{cy:0.0}");

        // @2x: the ring is diameter x 2.3 sprite px across.
        double k = diameter * Scale2x / across;   // sprite px per source px
        int ink = (int)Math.Ceiling(Math.Max(bw, bh) * k);
        if (ink % 2 == 1) ink++;
        int size = ink + 2 * Margin;
        var (ir, ig, ib) = Img.Hex(inkHex);
        var sprite = new Img(size, size);

        // Each sprite pixel averages the coverage over its footprint in the source (area sampling).
        const int N = 8;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double sum = 0;
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                    {
                        double sx = cx + ((x + (i + 0.5) / N) - size / 2.0) / k;
                        double sy = cy + ((y + (j + 0.5) / N) - size / 2.0) / k;
                        sum += Sample(cov, src.W, src.H, sx, sy);
                    }
                float a = (float)(sum / (N * N));
                a = a < 0.004f ? 0f : a > 0.996f ? 1f : a;
                int o = y * size + x;
                sprite.R[o] = ir; sprite.G[o] = ig; sprite.B[o] = ib; sprite.A[o] = a;
            }

        sprite.Save(outPath);
        Console.WriteLine($"{outPath}: {size}x{size} (ring {diameter} mockup px = {diameter * Scale2x:0.0} sprite px, ink {inkHex})");
        return Stats(outPath);
    }

    /// <summary>Bilinear coverage at a source point; nothing outside the picture.</summary>
    private static double Sample(float[] cov, int w, int h, double x, double y)
    {
        x -= 0.5; y -= 0.5;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double At(int xx, int yy) => xx < 0 || yy < 0 || xx >= w || yy >= h ? 0 : cov[yy * w + xx];
        return At(x0, y0) * (1 - fx) * (1 - fy) + At(x0 + 1, y0) * fx * (1 - fy) + At(x0, y0 + 1) * (1 - fx) * fy + At(x0 + 1, y0 + 1) * fx * fy;
    }

    private static int Compose(string discPath, string glyphPath, string outPath, string bgHex, int zoom)
    {
        Img disc = Img.Load(discPath), glyph = Img.Load(glyphPath);
        int pad = 16;
        int w = disc.W + 2 * pad, h = disc.H + 2 * pad;
        var (br, bg, bb) = Img.Hex(bgHex);
        var canvas = new Img(w, h);
        for (int i = 0; i < w * h; i++) { canvas.R[i] = br; canvas.G[i] = bg; canvas.B[i] = bb; canvas.A[i] = 1f; }
        Over(canvas, disc, pad, pad);
        Over(canvas, glyph, pad + (disc.W - glyph.W) / 2, pad + (disc.H - glyph.H) / 2);
        var big = new Img(w * zoom, h * zoom);
        for (int y = 0; y < h * zoom; y++)
            for (int x = 0; x < w * zoom; x++)
            {
                int s = (y / zoom) * w + x / zoom, d = y * w * zoom + x;
                big.R[d] = canvas.R[s]; big.G[d] = canvas.G[s]; big.B[d] = canvas.B[s]; big.A[d] = 1f;
            }
        big.Save(outPath);
        Console.WriteLine($"{outPath}: {big.W}x{big.H}");
        return 0;
    }

    /// <summary>Blends <paramref name="top"/> over <paramref name="dst"/> at (ox, oy), in linear light as uGUI does here.</summary>
    private static void Over(Img dst, Img top, int ox, int oy)
    {
        for (int y = 0; y < top.H; y++)
            for (int x = 0; x < top.W; x++)
            {
                int dx = ox + x, dy = oy + y;
                if (dx < 0 || dy < 0 || dx >= dst.W || dy >= dst.H) continue;
                int s = y * top.W + x, d = dy * dst.W + dx;
                float a = top.A[s];
                dst.R[d] = top.R[s] * a + dst.R[d] * (1 - a);
                dst.G[d] = top.G[s] * a + dst.G[d] * (1 - a);
                dst.B[d] = top.B[s] * a + dst.B[d] * (1 - a);
            }
    }
}
