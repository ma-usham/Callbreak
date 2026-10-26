// Card-deck cutting helpers for export-decks.ps1. C# 5 only: compiled by Windows PowerShell's
// Add-Type, like ArtTools.cs.
//
// Coordinates are continuous: pixel (i, j) covers [i, i+1) x [j, j+1) and its centre is
// (i + 0.5, j + 0.5). A card is the rounded rectangle [L, R) x [T, B) with corner radius r.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// ------------------------------------------------------------------------------------ image

// A float RGBA image, straight (non-premultiplied) alpha, 0-255 per channel.
public sealed class Img
{
    public int W, H;
    public float[] R, G, B, A;

    public Img(int w, int h)
    {
        W = w; H = h;
        R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; A = new float[w * h];
    }

    // Reads pixel values as stored (LockBits conversion, no drawing, no colour management).
    public static Img Load(string path)
    {
        using (Bitmap bmp = new Bitmap(path))
        {
            int w = bmp.Width, h = bmp.Height;
            Img im = new Img(w, h);
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] row = new byte[w * 4];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    im.B[i] = row[x * 4]; im.G[i] = row[x * 4 + 1]; im.R[i] = row[x * 4 + 2]; im.A[i] = row[x * 4 + 3];
                }
            }
            bmp.UnlockBits(d);
            return im;
        }
    }

    public static byte Q(float v) { return (byte)(v <= 0f ? 0 : (v >= 255f ? 255 : (int)Math.Round(v))); }

    // 8-bit RGBA, straight alpha. With keepRgb the colour of fully transparent pixels is kept
    // (edge colour bled outward, so texture filtering never pulls in black); otherwise 0,0,0,0.
    public byte[] ToRgba8(bool keepRgb)
    {
        byte[] px = new byte[W * H * 4];
        for (int i = 0; i < W * H; i++)
        {
            byte a = Q(A[i]);
            if (a == 0 && !keepRgb) continue;
            px[i * 4] = Q(R[i]); px[i * 4 + 1] = Q(G[i]); px[i * 4 + 2] = Q(B[i]); px[i * 4 + 3] = a;
        }
        return px;
    }

    public void SavePng(string path) { SavePng(path, false); }

    public void SavePng(string path, bool keepRgb)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, Png.Encode(ToRgba8(keepRgb), W, H));
    }

    // Nearest-neighbour or box-filtered copy at 1/k size (k integer >= 1), for smaller previews.
    public Img Shrink(int k)
    {
        if (k <= 1) return Clone();
        int w = W / k, h = H / k;
        Img o = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < k; j++)
                    for (int i = 0; i < k; i++)
                    {
                        int s = (y * k + j) * W + x * k + i; double al = A[s] / 255.0;
                        r += R[s] * al; g += G[s] * al; b += B[s] * al; a += al;
                    }
                int d = y * w + x;
                o.A[d] = (float)(a / (k * k) * 255);
                if (a > 0) { o.R[d] = (float)(r / a); o.G[d] = (float)(g / a); o.B[d] = (float)(b / a); }
            }
        return o;
    }

    public Img Clone()
    {
        Img c = new Img(W, H);
        Array.Copy(R, c.R, R.Length); Array.Copy(G, c.G, G.Length); Array.Copy(B, c.B, B.Length); Array.Copy(A, c.A, A.Length);
        return c;
    }

    public void Fill(float r, float g, float b, float a)
    {
        for (int i = 0; i < W * H; i++) { R[i] = r; G[i] = g; B[i] = b; A[i] = a; }
    }

    // Source-over composite of src (straight alpha) at integer offset, optional uniform scale of
    // src alpha. The destination is assumed opaque.
    public void Over(Img src, int ox, int oy)
    {
        for (int y = 0; y < src.H; y++)
        {
            int dy = y + oy; if (dy < 0 || dy >= H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int dx = x + ox; if (dx < 0 || dx >= W) continue;
                int s = y * src.W + x, d = dy * W + dx;
                float a = src.A[s] / 255f;
                if (a <= 0f) continue;
                R[d] = src.R[s] * a + R[d] * (1f - a);
                G[d] = src.G[s] * a + G[d] * (1f - a);
                B[d] = src.B[s] * a + B[d] * (1f - a);
                A[d] = 255f;
            }
        }
    }

    // Nearest-neighbour enlargement of a region, for pixel-level review.
    public Img Zoom(int x0, int y0, int w, int h, int k)
    {
        Img z = new Img(w * k, h * k);
        for (int y = 0; y < h * k; y++)
            for (int x = 0; x < w * k; x++)
            {
                int sx = x0 + x / k, sy = y0 + y / k, d = y * w * k + x;
                if (sx < 0 || sy < 0 || sx >= W || sy >= H) { z.A[d] = 0; continue; }
                int s = sy * W + sx;
                z.R[d] = R[s]; z.G[d] = G[s]; z.B[d] = B[s]; z.A[d] = A[s];
            }
        return z;
    }
}

// Minimal PNG encoder: 8-bit RGBA (colour type 6), non-interlaced, sRGB chunk, adaptive filters.
public static class Png
{
    static uint[] crcTable;

    static uint Crc(byte[] buf, int off, int len, uint crc)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                crcTable[n] = c;
            }
        }
        for (int i = 0; i < len; i++) crc = crcTable[(crc ^ buf[off + i]) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static void BE(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }

    static void Chunk(Stream s, string type, byte[] data)
    {
        byte[] t = Encoding.ASCII.GetBytes(type);
        BE(s, (uint)data.Length);
        s.Write(t, 0, 4); s.Write(data, 0, data.Length);
        uint c = Crc(t, 0, 4, 0xFFFFFFFFu);
        c = Crc(data, 0, data.Length, c) ^ 0xFFFFFFFFu;
        BE(s, c);
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
    }

    public static byte[] Encode(byte[] rgba, int w, int h)
    {
        int stride = w * 4;
        byte[] raw = new byte[(stride + 1) * h];
        byte[] cand = new byte[stride];
        byte[] best = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int rowOff = y * stride, prevOff = (y - 1) * stride;
            long bestScore = long.MaxValue; int bestF = 0;
            for (int f = 0; f < 5; f++)
            {
                long score = 0;
                for (int i = 0; i < stride; i++)
                {
                    int x = rgba[rowOff + i];
                    int a = i >= 4 ? rgba[rowOff + i - 4] : 0;
                    int b = y > 0 ? rgba[prevOff + i] : 0;
                    int c = (i >= 4 && y > 0) ? rgba[prevOff + i - 4] : 0;
                    int v;
                    if (f == 0) v = x;
                    else if (f == 1) v = x - a;
                    else if (f == 2) v = x - b;
                    else if (f == 3) v = x - ((a + b) >> 1);
                    else v = x - Paeth(a, b, c);
                    byte bv = (byte)(v & 0xFF);
                    cand[i] = bv;
                    score += bv < 128 ? bv : 256 - bv;
                }
                if (score < bestScore) { bestScore = score; bestF = f; Array.Copy(cand, best, stride); }
            }
            raw[y * (stride + 1)] = (byte)bestF;
            Array.Copy(best, 0, raw, y * (stride + 1) + 1, stride);
        }
        byte[] z;
        using (MemoryStream ms = new MemoryStream())
        {
            ms.WriteByte(0x78); ms.WriteByte(0xDA);
            using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
            uint s1 = 1, s2 = 0;
            for (int i = 0; i < raw.Length; i++) { s1 = (s1 + raw[i]) % 65521; s2 = (s2 + s1) % 65521; }
            BE(ms, (s2 << 16) | s1);
            z = ms.ToArray();
        }
        using (MemoryStream o = new MemoryStream())
        {
            byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
            o.Write(sig, 0, 8);
            byte[] ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 24); ihdr[1] = (byte)(w >> 16); ihdr[2] = (byte)(w >> 8); ihdr[3] = (byte)w;
            ihdr[4] = (byte)(h >> 24); ihdr[5] = (byte)(h >> 16); ihdr[6] = (byte)(h >> 8); ihdr[7] = (byte)h;
            ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(o, "IHDR", ihdr);
            Chunk(o, "sRGB", new byte[] { 0 });
            Chunk(o, "IDAT", z);
            Chunk(o, "IEND", new byte[0]);
            return o.ToArray();
        }
    }

    // Reads IHDR: width, height, bit depth, colour type, interlace. Null if not a PNG.
    public static int[] Header(string path)
    {
        byte[] b = new byte[33];
        using (FileStream f = File.OpenRead(path)) { if (f.Read(b, 0, 33) < 33) return null; }
        if (b[0] != 137 || b[1] != 80 || b[12] != (byte)'I' || b[13] != (byte)'H') return null;
        int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return new int[] { w, h, b[24], b[25], b[28] };
    }

    // Chunk types in file order, e.g. "IHDR sRGB IDAT IEND".
    public static string Chunks(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        StringBuilder sb = new StringBuilder();
        int p = 8;
        while (p + 8 <= b.Length)
        {
            int len = (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
            string t = Encoding.ASCII.GetString(b, p + 4, 4);
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(t);
            p += 12 + len;
            if (t == "IEND") break;
        }
        return sb.ToString();
    }
}

// ------------------------------------------------------------------------------- measuring

// Background model: parchment P darkened by a warm shadow. In ratio space d = 1 - c/P, a pure
// shadow pixel is d = a * K with a >= 0 and K the shadow's per-channel shape (blue darkens
// most). A pixel is card when it is off that line (residual) or brighter than the parchment.
public sealed class Bg
{
    public double[] P = new double[3];
    public double[] K = new double[] { 0.60, 0.78, 1.0 };

    public double Level(double[] c)
    {
        double num = 0, den = 0;
        for (int k = 0; k < 3; k++) { double d = 1 - c[k] / P[k]; num += d * K[k]; den += K[k] * K[k]; }
        return num / den;
    }

    // Distance (0-255 levels) of c from the shadow line.
    public double Residual(double[] c)
    {
        double a = Level(c), s = 0;
        for (int k = 0; k < 3; k++) { double d = 1 - c[k] / P[k]; double e = (d - a * K[k]) * P[k]; s += e * e; }
        return Math.Sqrt(s);
    }

    public double[] Shadow(double a)
    {
        return new double[] { P[0] * (1 - a * K[0]), P[1] * (1 - a * K[1]), P[2] * (1 - a * K[2]) };
    }

    public const double ResidualLimit = 10.0;   // levels
    public const double BrightLimit = 0.012;    // brighter than parchment by more than 1.2 %

    public bool IsCard(double[] c)
    {
        return Residual(c) > ResidualLimit || Level(c) < -BrightLimit;
    }
}

public sealed class SideEdge
{
    public int Side;            // 0 left, 1 top, 2 right, 3 bottom
    public double Edge;         // continuous sheet coordinate of the card boundary
    public double Alpha;        // coverage of the boundary pixel outside the first card pixel
    public double AlphaResid;   // fit residual of that pixel (levels)
    public double Frame;        // rim-to-frame transition (continuous coordinate), NaN if none
    public double EdgeA, EdgeB; // the same edge measured on each half of the side (tilt check)
    public int FirstCard;       // pixel index of the first card pixel
    public Bg Bg;
    public string Note = "";
}

public sealed class CardMeasure
{
    public double L, T, R, B;
    public SideEdge[] Sides = new SideEdge[4];
    public double[] CornerR = new double[4];   // TL, TR, BR, BL
    public double[] CornerRms = new double[4];
    public double Width { get { return R - L; } }
    public double Height { get { return B - T; } }
}

public static class DeckTools
{
    static double[] Px(Img im, int x, int y)
    {
        int i = y * im.W + x;
        return new double[] { im.R[i], im.G[i], im.B[i] };
    }

    static double Lum(double[] c) { return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; }

    // Pixel coordinate of profile sample j for a side: j = 0 is 'outer' pixels outside 'edge'
    // (the first pixel index expected inside), increasing inward. For the right and bottom
    // sides 'edge' is the last pixel index expected inside.
    static int Pos(int side, int edge, int outer, int j)
    {
        return (side == 0 || side == 1) ? edge - outer + j : edge + outer - j;
    }

    // Average colour of each profile sample over along-side pixels [a0, a1).
    public static double[][] Profile(Img im, int side, int edge, int outer, int n, int a0, int a1)
    {
        double[][] p = new double[n][];
        for (int j = 0; j < n; j++)
        {
            int q = Pos(side, edge, outer, j);
            double r = 0, g = 0, b = 0; int c = 0;
            for (int a = a0; a < a1; a++)
            {
                int x = (side == 0 || side == 2) ? q : a, y = (side == 0 || side == 2) ? a : q;
                if (x < 0 || y < 0 || x >= im.W || y >= im.H) continue;
                int i = y * im.W + x;
                r += im.R[i]; g += im.G[i]; b += im.B[i]; c++;
            }
            p[j] = c > 0 ? new double[] { r / c, g / c, b / c } : new double[] { 0, 0, 0 };
        }
        return p;
    }

    // Background from the outer part of a profile: the brightest sample among the first
    // 'upto' samples is the parchment; the shadow shape K is fitted to clearly shadowed samples.
    static Bg FitBg(double[][] p, int upto)
    {
        Bg bg = new Bg();
        int best = 0;
        for (int j = 0; j < upto; j++) if (Lum(p[j]) > Lum(p[best])) best = j;
        bg.P = (double[])p[best].Clone();
        return bg;
    }

    // First card sample from the outside: it is card and so is one of the next three (the light
    // rim is often followed by a rim-to-frame pixel whose colour happens to look like shadow).
    static int FirstCard(double[][] p, Bg bg, int from)
    {
        for (int j = Math.Max(from, 2); j + 3 < p.Length; j++)
            if (bg.IsCard(p[j]) && (bg.IsCard(p[j + 1]) || bg.IsCard(p[j + 2]) || bg.IsCard(p[j + 3]))) return j;
        return -1;
    }

    // Projection of c onto the segment a -> b: returns {s, perpendicular residual}.
    static double[] Project(double[] c, double[] a, double[] b)
    {
        double gg = 0, cg = 0;
        for (int k = 0; k < 3; k++) { double g = b[k] - a[k]; gg += g * g; cg += (c[k] - a[k]) * g; }
        double s = gg > 1e-9 ? cg / gg : 0, r = 0;
        for (int k = 0; k < 3; k++) { double e = c[k] - (a[k] + s * (b[k] - a[k])); r += e * e; }
        return new double[] { s, Math.Sqrt(r) };
    }

    // Outer boundary of a card on a profile (samples from outside inward; the boundary between
    // samples j-1 and j is at sample coordinate j). Steps:
    //  1. Walk inward along the shadow ramp to the first anomalous sample kA: chromatic (and
    //     confirmed by one of the next three), brighter than the parchment, or brighter than its
    //     outer neighbour by more than 'jump' (the light rim after the dark contact shadow).
    //  2. F = first definite card sample at or after kA (chroma residual > 20 or 20 levels
    //     brighter than the last shadow sample), moved inward while it is itself a mix of shadow
    //     and the next sample (a soft edge spreads over two pixels).
    //  3. Add the coverage of the partial pixels just outside F, each fitted as a mix of the card
    //     colour p[F] and a shadow at least as dark as its outer neighbour (the contact shadow
    //     darkens right at the card).
    // Returns F - sum(coverage); info gets {kA, F, sum}.
    public static double EdgeByCoverage(double[][] p, Bg bg, int from, double jump, double[] info)
    {
        int kA = -1;
        // Start at the brightest sample at least 6 outside the frame (first chromatic sample):
        // from there to the card the shadow only darkens, while samples further out may still be
        // brightening out of a neighbouring card's shadow. The light rim lies within 5 of the
        // frame, so it cannot be mistaken for the start.
        int kF = FirstChroma(p, bg, Math.Max(from, 2));
        int j0 = Math.Max(from, 2) - 1, j1 = kF > 0 ? kF - 6 : j0;
        int jP = j0;
        for (int j = j0; j <= j1; j++) if (Lum(p[j]) > Lum(p[jP])) jP = j;
        // The contact shadow right at the card is browner than the soft shadow further out (chroma
        // residual up to ~13 on Classic Crimson), about as far off the line as Kathmandu Gold's
        // pale rim (~15). So weak chroma alone does not stop the walk. What does:
        //   strong - chroma residual > 20 (crimson, gold);
        //   up     - brighter than the outer neighbour by 'jump' (a light rim after the shadow);
        //   bright - brighter than the parchment;
        //   dark   - darker than the outer neighbour by > 40 in one step (a dark border line; no
        //            shadow ramp falls that fast).
        int kind = 0; // 1 strong, 2 up/bright, 3 dark
        for (int j = jP + 1; j + 3 < p.Length; j++)
        {
            if (bg.Residual(p[j]) > 20) { kA = j; kind = 1; break; }
            if (Lum(p[j]) > Lum(p[j - 1]) + jump || bg.Level(p[j]) < -Bg.BrightLimit) { kA = j; kind = 2; break; }
            if (Lum(p[j]) < Lum(p[j - 1]) - 40) { kA = j; kind = 3; break; }
        }
        if (kA < 2) return double.NaN;
        // Card reference F. A light rim (reached by 'up') is referenced by its brightest sample
        // before the frame starts (within 3). A strong or dark first sample is the card colour
        // itself, unless it lies on the colour line between its neighbours (a partial pixel).
        int F = kA;
        if (kind == 2)
        {
            for (int j = kA + 1; j < Math.Min(p.Length, kA + 3); j++)
            {
                if (bg.Residual(p[j]) > 20) break;
                if (Lum(p[j]) > Lum(p[F])) F = j;
            }
        }
        else if (kA + 1 < p.Length)
        {
            // Partial if it sits on the line between its neighbours, within 10 % of their distance.
            double[] pr = Project(p[kA], p[kA - 1], p[kA + 1]);
            double span = 0;
            for (int k = 0; k < 3; k++) span += (p[kA + 1][k] - p[kA - 1][k]) * (p[kA + 1][k] - p[kA - 1][k]);
            if (pr[0] > 0.05 && pr[0] < 0.95 && pr[1] < 0.10 * Math.Sqrt(span)) F = kA + 1;
        }
        double sum = 0;
        double lumC = Lum(p[F]);
        for (int j = F - 1; j >= Math.Max(1, F - 4); j--)
        {
            double res;
            if (j >= kA)
            {   // between the first anomaly and the reference: a partly covered pixel
                sum += MixAlpha(p[j], p[F], bg, Math.Max(0, bg.Level(p[j - 1])), out res);
                continue;
            }
            // Outside the anomaly. Predict the shadow here from the ramp (mean slope over the
            // three samples further out). Card coverage would pull the pixel toward the card's
            // brightness; if it has not moved that way by more than 3 levels it is shadow, however
            // warm its hue (the contact shadow is browner than the ramp). Otherwise allow at most
            // half coverage.
            double slope = j >= 4 ? (Lum(p[j - 1]) - Lum(p[j - 4])) / 3.0 : 0;
            double pred = Lum(p[j - 1]) + slope;
            if (lumC > Lum(p[j]) ? Lum(p[j]) <= pred + 3 : Lum(p[j]) >= pred - 3) break;
            double a = MixAlpha(p[j], p[F], bg, Math.Max(0, bg.Level(p[j - 1])), out res);
            if (a < 0.05) break;
            sum += Math.Min(0.5, a);
            break;
        }
        if (info != null) { info[0] = kA; info[1] = F; info[2] = sum; }
        return F - sum;
    }

    // First chromatic card sample from the outside: off the shadow line by more than the limit,
    // and so is one of the next three. The light rim (white on Classic Crimson and Himalayan
    // Ivory) is not chromatic; the frame (crimson, gold, black) and Kathmandu Gold's gold rim are.
    static int FirstChroma(double[][] p, Bg bg, int from)
    {
        for (int j = Math.Max(from, 2); j + 3 < p.Length; j++)
        {
            if (bg.Residual(p[j]) <= Bg.ResidualLimit) continue;
            if (bg.Residual(p[j + 1]) > Bg.ResidualLimit || bg.Residual(p[j + 2]) > Bg.ResidualLimit || bg.Residual(p[j + 3]) > Bg.ResidualLimit) return j;
        }
        return -1;
    }

    // Sub-pixel outer boundary on a profile (samples from outside inward). kF is the first
    // chromatic card sample. S is the darkest sample within 5 outside kF (the contact shadow, which
    // is darkest right at the card). Samples between S and kF are the light rim; Rc is the
    // brightest of them if clearly brighter than S, else kF. If Rc lies on the colour line from S
    // to the next sample it is itself partly covered and the next sample becomes Rc. The boundary
    // is the 50 % crossing along the colour line S -> Rc. Returns a sample coordinate (sample j's
    // centre is j + 0.5) and fills info {S, Rc, kF}.
    public static double OuterEdge(double[][] p, int kF, double[] info)
    {
        return OuterEdge(p, kF, info, null, 10.0);
    }

    // As above with the contact shadow found by walking inward along the shadow ramp: S is the
    // last sample before the first one that is chromatic (bg given) or brighter than its outer
    // neighbour by more than 'jump' levels. (The darkest sample is not safe: on Classic Crimson
    // the white-to-crimson pixel is a pinkish grey darker than the contact shadow.)
    public static double OuterEdge(double[][] p, int kF, double[] info, Bg bg, double jump)
    {
        int jS = kF - 1;
        for (int j = Math.Max(0, kF - 7); j < kF; j++)
        {
            bool chroma = bg != null && bg.Residual(p[j + 1]) > Bg.ResidualLimit;
            bool up = Lum(p[j + 1]) > Lum(p[j]) + jump && j + 1 >= kF - 4;
            if (chroma || up || j + 1 == kF) { jS = j; break; }
        }
        int jR = kF;
        double best = Lum(p[jS]) + 15;
        for (int j = jS + 1; j < kF; j++) if (Lum(p[j]) > best) { best = Lum(p[j]); jR = j; }
        if (jR + 1 < p.Length)
        {
            double[] pr = Project(p[jR], p[jS], p[jR + 1]);
            if (pr[0] > 0.05 && pr[0] < 0.97 && pr[1] < 6.0) jR = jR + 1;
        }
        double prev = 0, pos = jS + 1;
        for (int j = jS + 1; j <= jR; j++)
        {
            double s = j == jR ? 1.0 : Project(p[j], p[jS], p[jR])[0];
            if (s >= 0.5)
            {
                double f = (0.5 - prev) / Math.Max(1e-6, s - prev);
                if (f < 0) f = 0; if (f > 1) f = 1;
                pos = (j - 1) + 0.5 + f;
                break;
            }
            prev = s;
        }
        if (info != null) { info[0] = jS; info[1] = jR; info[2] = kF; }
        return pos;
    }

    // Sub-pixel boundary on a profile whose first card sample is k1: the 50 % crossing, along
    // the colour line from the darkest contact-shadow sample S (within 3 samples outside k1) to
    // the rim reference Rc. Rc is sample k1, or k1+1 when sample k1 lies on the S -> k1+1 line
    // (then k1 is itself a partly covered pixel). Returns the crossing as a sample coordinate:
    // sample j's centre is j + 0.5, so the boundary between samples j-1 and j is j. info gets
    // {jS, jR, s of the sample before jR}.
    static double Crossing(double[][] p, int k1, double[] info)
    {
        int jS = k1 - 1;
        for (int j = Math.Max(0, k1 - 3); j < k1; j++) if (Lum(p[j]) < Lum(p[jS])) jS = j;
        int jR = k1;
        if (k1 + 1 < p.Length)
        {
            double[] pr = Project(p[k1], p[jS], p[k1 + 1]);
            if (pr[0] > 0.05 && pr[0] < 0.97 && pr[1] < 6.0) jR = k1 + 1;
        }
        double prev = 0; double pos = jR; // default: boundary right at jR's outer side
        for (int j = jS + 1; j <= jR; j++)
        {
            double s = j == jR ? 1.0 : Project(p[j], p[jS], p[jR])[0];
            if (s >= 0.5)
            {
                double f = (0.5 - prev) / Math.Max(1e-6, s - prev);
                if (f < 0) f = 0; if (f > 1) f = 1;
                pos = (j - 1) + 0.5 + f;
                break;
            }
            prev = s;
        }
        if (info != null) { info[0] = jS; info[1] = jR; info[2] = jR - 1 > jS ? Project(p[jR - 1], p[jS], p[jR])[0] : 0; }
        return pos;
    }

    // Converts a sample coordinate (see Crossing) to a continuous sheet coordinate.
    static double SampleToSheet(int side, int edge, int outer, double t)
    {
        int q0 = Pos(side, edge, outer, 0);
        return (side == 0 || side == 1) ? q0 + t : q0 + 1 - t;
    }

    // Coverage of boundary sample c between shadow (level searched from aPrev upward) and card
    // colour I: returns alpha and writes the fit residual.
    public static double MixAlpha(double[] c, double[] I, Bg bg, double aPrev, out double resid)
    {
        double bestRes = double.MaxValue, bestAlpha = 0, bestA = aPrev;
        List<double[]> sols = new List<double[]>();
        for (double a = Math.Max(0, aPrev - 0.02); a <= aPrev + 0.35; a += 0.005)
        {
            double[] O = bg.Shadow(a);
            double gg = 0, cg = 0;
            for (int k = 0; k < 3; k++) { double g = I[k] - O[k]; gg += g * g; cg += (c[k] - O[k]) * g; }
            double al = gg > 1e-6 ? cg / gg : 0;
            if (al < 0) al = 0; if (al > 1) al = 1;
            double s = 0;
            for (int k = 0; k < 3; k++) { double e = c[k] - (O[k] + al * (I[k] - O[k])); s += e * e; }
            s = Math.Sqrt(s);
            sols.Add(new double[] { a, al, s });
            if (s < bestRes) { bestRes = s; bestAlpha = al; bestA = a; }
        }
        // Among near-equal fits prefer the lightest shadow (closest to the outer neighbour).
        foreach (double[] s in sols)
        {
            if (s[2] <= bestRes + 0.75) { bestAlpha = s[1]; resid = s[2]; return bestAlpha; }
        }
        resid = bestRes;
        return bestAlpha;
    }

    // Measures one side. 'edge' is the rough frame boundary (first/last strong-ink pixel index),
    // 'outer' how many pixels outside it the profile may start, a0..a1 the along-side range.
    public static SideEdge MeasureSide(Img im, int side, int edge, int outer, int a0, int a1)
    {
        SideEdge se = new SideEdge(); se.Side = side;
        double al, res; int k1; Bg bg;
        double e = SideCore(im, side, edge, outer, a0, a1, out k1, out al, out res, out bg);
        if (double.IsNaN(e)) { se.Note = " no-card"; se.Edge = double.NaN; se.Bg = bg; return se; }
        se.Bg = bg; se.Alpha = al; se.AlphaResid = res;
        se.FirstCard = Pos(side, edge, outer, k1);
        se.Edge = e;
        se.Frame = SampleToSheet(side, edge, outer, k1);   // outer side of the first chromatic sample
        // Tilt check: the same measurement on each half of the side.
        int mid = (a0 + a1) / 2, k; double a2, r2; Bg b2;
        se.EdgeA = SideCore(im, side, edge, outer, a0, mid, out k, out a2, out r2, out b2);
        se.EdgeB = SideCore(im, side, edge, outer, mid, a1, out k, out a2, out r2, out b2);
        return se;
    }

    static double SideCore(Img im, int side, int edge, int outer, int a0, int a1, out int k1, out double al, out double res, out Bg bg)
    {
        int n = outer + 26;
        double[][] p = Profile(im, side, edge, outer, n, a0, a1);
        al = 0; res = 0;
        // Provisional background from the outermost samples, then the first chromatic sample.
        bg = FitBg(p, Math.Max(2, Math.Min(6, outer - 5)));
        k1 = FirstChroma(p, bg, 3);
        if (k1 < 3) return double.NaN;
        // Refit the background from samples at least 5 px outside the frame, and the shadow shape.
        bg = FitBg(p, Math.Max(2, k1 - 5));
        FitShape(bg, p, Math.Max(2, k1 - 5));
        k1 = FirstChroma(p, bg, 3);
        if (k1 < 3) return double.NaN;
        double[] info = new double[3];
        double t = EdgeByCoverage(p, bg, 3, 5.0, info);
        if (double.IsNaN(t)) return double.NaN;
        al = info[2];                     // summed coverage of partial pixels
        res = info[1] - info[0];          // steps from first anomalous to first definite sample
        k1 = (int)info[1];
        return SampleToSheet(side, edge, outer, t);
    }

    // Continuous coordinate of the boundary between sample k1-1 (coverage al) and k1.
    static double Boundary(int side, int edge, int outer, int k1, double al)
    {
        int q = Pos(side, edge, outer, k1);
        if (side == 0 || side == 1) return q - al;      // card occupies [q, ...), partial pixel q-1
        return q + 1 + al;                               // card occupies (..., q+1), partial pixel q+1
    }

    // Fits the shadow shape K to samples that are clearly shadowed (level > 0.06) before 'upto'.
    static void FitShape(Bg bg, double[][] p, int upto)
    {
        double[] acc = new double[3]; int n = 0;
        for (int j = 0; j < upto; j++)
        {
            double[] d = new double[3];
            for (int k = 0; k < 3; k++) d[k] = 1 - p[j][k] / bg.P[k];
            if (d[2] < 0.06) continue;
            if (bg.Residual(p[j]) > Bg.ResidualLimit) continue;
            for (int k = 0; k < 3; k++) acc[k] += d[k] / d[2];
            n++;
        }
        if (n >= 2) bg.K = new double[] { acc[0] / n, acc[1] / n, 1.0 };
    }

    // Rim-to-frame transition: the largest luminance step within 7 samples inside the edge,
    // located where luminance crosses the midpoint of that step.
    static double FrameEdge(double[][] p, int side, int edge, int outer, int k1)
    {
        int best = -1; double drop = 0;
        for (int j = k1; j < Math.Min(p.Length - 1, k1 + 7); j++)
        {
            double d = Lum(p[j]) - Lum(p[j + 1]);
            if (d > drop) { drop = d; best = j; }
        }
        if (best < 0 || drop < 30) return double.NaN;
        // Midpoint between the brightest rim sample and the darkest of the next three.
        double hi = Lum(p[best]), lo = Lum(p[best + 1]);
        for (int j = best + 1; j < Math.Min(p.Length, best + 4); j++) lo = Math.Min(lo, Lum(p[j]));
        double mid = (hi + lo) / 2;
        double f = (Lum(p[best]) - mid) / Math.Max(1e-6, Lum(p[best]) - Lum(p[best + 1]));
        if (f < 0) f = 0; if (f > 1) f = 1;
        // Sample centres: best + 0.5 -> best + 1.5 in the inward direction.
        double inward = best + 0.5 + f;
        int q0 = Pos(side, edge, outer, 0);
        return (side == 0 || side == 1) ? q0 + inward : q0 + 1 - inward;
    }

    // ------------------------------------------------------------------ corners

    // Fits the corner radius of one corner (0 TL, 1 TR, 2 BR, 3 BL) given the straight edges.
    // Scans rows through the steep half of the arc and columns through the flat half, finds the
    // sub-pixel boundary in each the same way as on the sides, and fits a circle tangent to
    // both edges. Returns radius; writes the RMS distance of the points from the fitted arc.
    public static double FitCorner(Img im, CardMeasure m, int corner, double rGuess, out double rms, List<double[]> pts)
    {
        bool left = corner == 0 || corner == 3, top = corner == 0 || corner == 1;
        SideEdge sx = m.Sides[left ? 0 : 2], sy = m.Sides[top ? 1 : 3];
        Bg bg = new Bg();
        for (int k = 0; k < 3; k++) { bg.P[k] = (sx.Bg.P[k] + sy.Bg.P[k]) / 2; bg.K[k] = (sx.Bg.K[k] + sy.Bg.K[k]) / 2; }
        double ex = left ? m.L : m.R, ey = top ? m.T : m.B;
        int span = (int)Math.Ceiling(rGuess * 1.6) + 3;
        List<double[]> P = new List<double[]>();
        // Rows: y from the corner inward by span; scan x from outside inward.
        for (int t = 0; t < span; t++)
        {
            int y = top ? (int)Math.Floor(ey) + t : (int)Math.Ceiling(ey) - 1 - t;
            double yc = y + 0.5;
            double dy = top ? yc - ey : ey - yc; // distance inside the horizontal edge
            if (dy < 0.3) continue;
            double[] e = ScanLine(im, bg, true, y, left ? (int)Math.Floor(ex) - 8 : (int)Math.Ceiling(ex) + 7, left ? 1 : -1, 26);
            if (e == null) continue;
            double inX = left ? e[0] - ex : ex - e[0]; // distance of the boundary inside the vertical edge
            P.Add(new double[] { inX, dy, 0 });
        }
        for (int t = 0; t < span; t++)
        {
            int x = left ? (int)Math.Floor(ex) + t : (int)Math.Ceiling(ex) - 1 - t;
            double xc = x + 0.5;
            double dx = left ? xc - ex : ex - xc;
            if (dx < 0.3) continue;
            double[] e = ScanLine(im, bg, false, x, top ? (int)Math.Floor(ey) - 8 : (int)Math.Ceiling(ey) + 7, top ? 1 : -1, 26);
            if (e == null) continue;
            double inY = top ? e[0] - ey : ey - e[0];
            P.Add(new double[] { dx, inY, 1 });
        }
        // Points (u, v) in corner-local coordinates (u inside the vertical edge, v inside the
        // horizontal edge). Arc: (u - r)^2 + (v - r)^2 = r^2. Row points are used where the arc is
        // steep (r(1 - 1/sqrt2) <= v < r), column points where it is flat. Robust: points more
        // than max(0.8, 2.5 rms) off the fitted arc are dropped and the fit repeated.
        bool[] use = new bool[P.Count];
        for (int k = 0; k < use.Length; k++) use[k] = true;
        double bestR = rGuess, bestS = double.MaxValue;
        for (int pass = 0; pass < 4; pass++)
        {
            bestS = double.MaxValue;
            for (double r = 3; r <= 24; r += 0.02)
            {
                double s = 0; int n = 0;
                for (int k = 0; k < P.Count; k++)
                {
                    if (!use[k]) continue;
                    double err;
                    if (!ArcError(P[k], r, out err)) continue;
                    s += err * err; n++;
                }
                if (n < 5) continue;
                s /= n;
                if (s < bestS) { bestS = s; bestR = r; }
            }
            if (bestS == double.MaxValue) break;
            double lim = Math.Max(0.8, 2.5 * Math.Sqrt(bestS));
            bool changed = false;
            for (int k = 0; k < P.Count; k++)
            {
                double err;
                if (!use[k] || !ArcError(P[k], bestR, out err)) continue;
                if (Math.Abs(err) > lim) { use[k] = false; changed = true; }
            }
            if (!changed) break;
        }
        rms = bestS == double.MaxValue ? double.NaN : Math.Sqrt(bestS);
        if (bestS == double.MaxValue) bestR = double.NaN;
        if (pts != null) for (int k = 0; k < P.Count; k++) if (use[k]) pts.Add(P[k]);
        return bestR;
    }

    // Distance of a corner point from the arc of radius r along its scan direction; false when
    // the point is not on the part of the arc its scan direction measures well.
    static bool ArcError(double[] q, double r, out double err)
    {
        err = 0;
        if (q[2] == 0)
        {
            if (q[1] < r * (1 - 0.7071) || q[1] >= r) return false;
            err = q[0] - (r - Math.Sqrt(r * r - (r - q[1]) * (r - q[1])));
            return true;
        }
        if (q[0] < r * (1 - 0.7071) || q[0] >= r) return false;
        err = q[1] - (r - Math.Sqrt(r * r - (r - q[0]) * (r - q[0])));
        return true;
    }

    // Walks a single row (horiz) or column from 'start' in direction 'dir' for n pixels and
    // returns the sub-pixel boundary coordinate {pos, alpha, resid} or null.
    static double[] ScanLine(Img im, Bg bg, bool horiz, int fixedCoord, int start, int dir, int n)
    {
        double[][] p = new double[n][];
        for (int j = 0; j < n; j++)
        {
            int q = start + dir * j;
            int x = horiz ? q : fixedCoord, y = horiz ? fixedCoord : q;
            if (x < 0 || y < 0 || x >= im.W || y >= im.H) return null;
            p[j] = Px(im, x, y);
        }
        double t = EdgeByCoverage(p, bg, 3, 6.0, null);
        if (double.IsNaN(t)) return null;
        double pos = dir > 0 ? start + t : start + 1 - t;
        return new double[] { pos, 0, 0 };
    }

    // ------------------------------------------------------------------ rough frames

    // Parchment colour of a sheet: per-channel median of the bottom margin strip.
    public static Bg SheetBg(Img im, int y0, int y1)
    {
        List<float>[] ch = { new List<float>(), new List<float>(), new List<float>() };
        for (int y = y0; y < y1; y += 2)
            for (int x = 10; x < im.W - 10; x += 3)
            {
                int i = y * im.W + x;
                ch[0].Add(im.R[i]); ch[1].Add(im.G[i]); ch[2].Add(im.B[i]);
            }
        Bg bg = new Bg();
        for (int k = 0; k < 3; k++) { ch[k].Sort(); bg.P[k] = ch[k][ch[k].Count / 2]; }
        return bg;
    }

    // Strong card ink: far off the shadow line or much darker than the parchment. The light rim
    // is not strong ink; the frame is.
    static bool Strong(Bg bg, Img im, int i)
    {
        double[] c = { im.R[i], im.G[i], im.B[i] };
        return bg.Residual(c) > 22 || Lum(c) < 0.5 * Lum(bg.P);
    }

    // Frame box of the card inside a search box: first/last column and row whose strong-ink
    // count reaches 'minCount'. Returns {x0, y0, x1, y1} inclusive, or null.
    public static int[] RoughFrame(Img im, Bg bg, int bx, int by, int bw, int bh, int minCount)
    {
        int x0 = Math.Max(0, bx), y0 = Math.Max(0, by), x1 = Math.Min(im.W, bx + bw), y1 = Math.Min(im.H, by + bh);
        int[] col = new int[x1 - x0], row = new int[y1 - y0];
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (Strong(bg, im, y * im.W + x)) { col[x - x0]++; row[y - y0]++; }
        int fx0 = -1, fx1 = -1, fy0 = -1, fy1 = -1;
        for (int i = 0; i < col.Length; i++) if (col[i] >= minCount) { if (fx0 < 0) fx0 = i; fx1 = i; }
        for (int i = 0; i < row.Length; i++) if (row[i] >= minCount) { if (fy0 < 0) fy0 = i; fy1 = i; }
        if (fx0 < 0 || fy0 < 0) return null;
        return new int[] { x0 + fx0, y0 + fy0, x0 + fx1, y0 + fy1 };
    }

    // ------------------------------------------------------------------ whole cards

    // Measures a card from its rough frame box {x0, y0, x1, y1} (inclusive). outerL/T/R/B limit
    // how far each side's profile may reach outside the frame (half the gap to a neighbour).
    public static CardMeasure MeasureCard(Img im, int[] f, int outerL, int outerT, int outerR, int outerB)
    {
        CardMeasure m = new CardMeasure();
        int bw = f[2] - f[0] + 1, bh = f[3] - f[1] + 1;
        int ax0 = f[1] + bh / 5, ax1 = f[1] + bh * 4 / 5;   // rows for left/right
        int ay0 = f[0] + bw / 5, ay1 = f[0] + bw * 4 / 5;   // columns for top/bottom
        m.Sides[0] = MeasureSide(im, 0, f[0], outerL, ax0, ax1);
        m.Sides[2] = MeasureSide(im, 2, f[2], outerR, ax0, ax1);
        m.Sides[1] = MeasureSide(im, 1, f[1], outerT, ay0, ay1);
        m.Sides[3] = MeasureSide(im, 3, f[3], outerB, ay0, ay1);
        m.L = m.Sides[0].Edge; m.R = m.Sides[2].Edge; m.T = m.Sides[1].Edge; m.B = m.Sides[3].Edge;
        return m;
    }

    public static void MeasureCorners(Img im, CardMeasure m, double rGuess)
    {
        for (int c = 0; c < 4; c++)
        {
            bool left = c == 0 || c == 3, top = c == 0 || c == 1;
            if (m.Sides[left ? 0 : 2].Bg == null || m.Sides[top ? 1 : 3].Bg == null || double.IsNaN(m.L + m.T + m.R + m.B))
            { m.CornerR[c] = double.NaN; m.CornerRms[c] = double.NaN; continue; }
            double rms;
            m.CornerR[c] = FitCorner(im, m, c, rGuess, out rms, null);
            m.CornerRms[c] = rms;
        }
    }

    // Rough frames of all cards on a sheet from approximate boxes (x, y, w, h per card), and the
    // per-side outer window: half the gap to the nearest neighbouring frame, at most 'maxOuter'.
    // Returns per card {x0, y0, x1, y1, outerL, outerT, outerR, outerB}.
    public static int[][] Frames(Img im, Bg bg, int[][] boxes, int pad, int minCount, int maxOuter)
    {
        int n = boxes.Length;
        int[][] f = new int[n][];
        for (int i = 0; i < n; i++)
        {
            int[] b = boxes[i];
            f[i] = RoughFrame(im, bg, b[0] - pad, b[1] - pad, b[2] + 2 * pad, b[3] + 2 * pad, minCount);
        }
        int[][] res = new int[n][];
        for (int i = 0; i < n; i++)
        {
            if (f[i] == null) continue;
            int oL = maxOuter, oT = maxOuter, oR = maxOuter, oB = maxOuter;
            for (int j = 0; j < n; j++)
            {
                if (j == i || f[j] == null) continue;
                bool vOverlap = f[j][1] < f[i][3] && f[j][3] > f[i][1];
                bool hOverlap = f[j][0] < f[i][2] && f[j][2] > f[i][0];
                if (vOverlap && f[j][2] < f[i][0]) oL = Math.Min(oL, (f[i][0] - f[j][2]) / 2);
                if (vOverlap && f[j][0] > f[i][2]) oR = Math.Min(oR, (f[j][0] - f[i][2]) / 2);
                if (hOverlap && f[j][3] < f[i][1]) oT = Math.Min(oT, (f[i][1] - f[j][3]) / 2);
                if (hOverlap && f[j][1] > f[i][3]) oB = Math.Min(oB, (f[j][1] - f[i][3]) / 2);
            }
            res[i] = new int[] { f[i][0], f[i][1], f[i][2], f[i][3], oL, oT, oR, oB };
        }
        return res;
    }

    // Debug dump of a side profile with the background model's view of each sample.
    public static string DumpSide(Img im, int side, int edge, int outer, int a0, int a1)
    {
        int n = outer + 12;
        double[][] p = Profile(im, side, edge, outer, n, a0, a1);
        int k1; double al, res; Bg bg;
        double e = SideCore(im, side, edge, outer, a0, a1, out k1, out al, out res, out bg);
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("edge {0} k1 {1} alpha {2} res {3}  P {4} {5} {6}  K {7} {8}\n", F(e), k1, F2(al), F2(res),
            F2(bg.P[0]), F2(bg.P[1]), F2(bg.P[2]), F2(bg.K[0]), F2(bg.K[1]));
        for (int j = 0; j < n; j++)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0,3} @{1,5}  {2,6:F1} {3,6:F1} {4,6:F1}  L {5,6:F1}  lvl {6,6:F3}  res {7,6:F1}  {8}\n",
                j, Pos(side, edge, outer, j), p[j][0], p[j][1], p[j][2], Lum(p[j]), bg.Level(p[j]), bg.Residual(p[j]), bg.Residual(p[j]) > Bg.ResidualLimit ? "CHROMA" : "");
        }
        return sb.ToString();
    }

    public static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
    public static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

    public static string Describe(CardMeasure m)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("L {0} T {1} R {2} B {3}  W {4} H {5}", F(m.L), F(m.T), F(m.R), F(m.B), F(m.Width), F(m.Height));
        for (int s = 0; s < 4; s++)
        {
            SideEdge e = m.Sides[s];
            sb.AppendFormat(" | s{0} a {1} res {2} rim {3} tilt {4}{5}", s, F2(e.Alpha), F2(e.AlphaResid),
                F2(double.IsNaN(e.Frame) ? double.NaN : Math.Abs(e.Frame - e.Edge)), F2(e.EdgeB - e.EdgeA), e.Note);
        }
        sb.AppendFormat(" | r {0} {1} {2} {3}", F2(m.CornerR[0]), F2(m.CornerR[1]), F2(m.CornerR[2]), F2(m.CornerR[3]));
        return sb.ToString();
    }
}

// ------------------------------------------------------------------------------- cutting

// Where to cut one card. L, T, R, B are the measured outer edges and Rt the corner radii (TL, TR,
// BR, BL) of the outline. The clean region is the set of pixels that are wholly card: from the
// first fully covered pixel on each side (CL, CT, CR, CB, inclusive pixel indices), and in each
// corner inside the outline's arc moved inward by the larger of the two side insets.
public sealed class CutSpec
{
    public string Name = "";
    public double L, T, R, B;
    public int CL, CT, CR, CB;
    public double[] Rt = new double[4];
}

public static class Cut
{
    // Is point (x, y) inside the rounded rect [l, r] x [t, b] with corner radii rc (TL TR BR BL)?
    public static bool Inside(double x, double y, double l, double t, double r, double b, double[] rc)
    {
        if (x < l || x > r || y < t || y > b) return false;
        double cx, cy, k;
        if (x < l + rc[0] && y < t + rc[0]) { k = rc[0]; cx = l + k; cy = t + k; return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= k * k; }
        if (x > r - rc[1] && y < t + rc[1]) { k = rc[1]; cx = r - k; cy = t + k; return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= k * k; }
        if (x > r - rc[2] && y > b - rc[2]) { k = rc[2]; cx = r - k; cy = b - k; return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= k * k; }
        if (x < l + rc[3] && y > b - rc[3]) { k = rc[3]; cx = l + k; cy = b - k; return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= k * k; }
        return true;
    }

    // Corner geometry of a cut: for corner c the arc centre (cx, cy), the inner (clean) radius,
    // and the direction signs of the corner (sx, sy: -1 towards left/top).
    static void Corner(CutSpec s, int c, out double cx, out double cy, out double rIn, out int sx, out int sy)
    {
        double r = s.Rt[c];
        bool left = c == 0 || c == 3, top = c == 0 || c == 1;
        double dx = left ? s.CL - s.L : s.R - (s.CR + 1);
        double dy = top ? s.CT - s.T : s.B - (s.CB + 1);
        cx = left ? s.L + r : s.R - r;
        cy = top ? s.T + r : s.B - r;
        rIn = r - Math.Max(0, Math.Max(dx, dy));
        sx = left ? -1 : 1; sy = top ? -1 : 1;
    }

    // Is (x, y) in the clean region of cut s?
    static bool InClean(double x, double y, CutSpec s)
    {
        if (x < s.CL || x > s.CR + 1 || y < s.CT || y > s.CB + 1) return false;
        for (int c = 0; c < 4; c++)
        {
            double cx, cy, rIn; int sx, sy;
            Corner(s, c, out cx, out cy, out rIn, out sx, out sy);
            if ((x - cx) * sx > 0 && (y - cy) * sy > 0)
                return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= rIn * rIn;
        }
        return true;
    }

    // Clean mask over a patch whose pixel (i, j) is sheet pixel (x0 + i, y0 + j): a pixel is
    // clean when all four corners of its square lie in the clean region (the region is convex).
    public static bool[] CleanMask(int x0, int y0, int w, int h, CutSpec s)
    {
        bool[] m = new bool[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                double x = x0 + i, y = y0 + j;
                m[j * w + i] = InClean(x, y, s) && InClean(x + 1, y, s) && InClean(x, y + 1, s) && InClean(x + 1, y + 1, s);
            }
        return m;
    }

    // Mean colour of the clean pixels on the straight sides just past corner c's arc: the rim
    // colour that the corner's rim continues.
    static double[] RimColour(Img src, CutSpec s, int c)
    {
        bool left = c == 0 || c == 3, top = c == 0 || c == 1;
        double r = s.Rt[c];
        int col = left ? s.CL : s.CR, row = top ? s.CT : s.CB;
        double acc0 = 0, acc1 = 0, acc2 = 0; int n = 0;
        for (int k = 1; k <= 4; k++)
        {
            int y = top ? (int)Math.Ceiling(s.T + r) + k : (int)Math.Floor(s.B - r) - k;
            int x = left ? (int)Math.Ceiling(s.L + r) + k : (int)Math.Floor(s.R - r) - k;
            int i1 = y * src.W + col, i2 = row * src.W + x;
            acc0 += src.R[i1] + src.R[i2]; acc1 += src.G[i1] + src.G[i2]; acc2 += src.B[i1] + src.B[i2]; n += 2;
        }
        return new double[] { acc0 / n, acc1 / n, acc2 / n };
    }

    // Copies the patch out of the sheet and replaces every pixel outside the clean region: in the
    // four corner squares with the rim colour (so the rim runs on round the arc instead of the
    // frame being dragged outward), elsewhere by growing the clean colours outward (each step:
    // mean of the already-filled 4-neighbours), which extends the rim on the straight sides. So
    // the rim's partly covered pixels, the shadow and the parchment are all replaced by the
    // card's own edge colour before any resampling looks at them.
    public static Img Inpaint(Img src, int x0, int y0, int w, int h, bool[] clean, CutSpec spec)
    {
        Img o = new Img(w, h);
        bool[] have = new bool[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int d = j * w + i, sx = x0 + i, sy = y0 + j;
                o.A[d] = 255f;
                have[d] = clean[d];
                if (!clean[d] || sx < 0 || sy < 0 || sx >= src.W || sy >= src.H) continue;
                int s = sy * src.W + sx;
                o.R[d] = src.R[s]; o.G[d] = src.G[s]; o.B[d] = src.B[s];
            }
        if (spec != null)
        {
            for (int c = 0; c < 4; c++)
            {
                double cx, cy, rIn; int dxs, dys;
                Corner(spec, c, out cx, out cy, out rIn, out dxs, out dys);
                double[] rim = RimColour(src, spec, c);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int d = j * w + i;
                        if (have[d]) continue;
                        double px = x0 + i + 0.5, py = y0 + j + 0.5;
                        if ((px - cx) * dxs <= 0 || (py - cy) * dys <= 0) continue;   // not in this corner square
                        o.R[d] = (float)rim[0]; o.G[d] = (float)rim[1]; o.B[d] = (float)rim[2];
                        have[d] = true;
                    }
            }
        }
        for (int iter = 0; iter < 400; iter++)
        {
            bool[] next = (bool[])have.Clone();
            bool any = false, missing = false;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int d = j * w + i;
                    if (have[d]) continue;
                    double r = 0, g = 0, b = 0; int n = 0;
                    if (i > 0 && have[d - 1]) { r += o.R[d - 1]; g += o.G[d - 1]; b += o.B[d - 1]; n++; }
                    if (i < w - 1 && have[d + 1]) { r += o.R[d + 1]; g += o.G[d + 1]; b += o.B[d + 1]; n++; }
                    if (j > 0 && have[d - w]) { r += o.R[d - w]; g += o.G[d - w]; b += o.B[d - w]; n++; }
                    if (j < h - 1 && have[d + w]) { r += o.R[d + w]; g += o.G[d + w]; b += o.B[d + w]; n++; }
                    if (n == 0) { missing = true; continue; }
                    o.R[d] = (float)(r / n); o.G[d] = (float)(g / n); o.B[d] = (float)(b / n);
                    next[d] = true; any = true;
                }
            have = next;
            if (!missing || !any) break;
        }
        return o;
    }

    static double CatRom(double t)
    {
        t = Math.Abs(t);
        if (t < 1) return (1.5 * t - 2.5) * t * t + 1;
        if (t < 2) return ((-0.5 * t + 2.5) * t - 4) * t + 2;
        return 0;
    }

    // Weights of a separable Catmull-Rom (Keys a = -0.5) resample: output pixel u samples the
    // source at continuous position lo + (u + 0.5) * step; the kernel is widened by 'step' when
    // shrinking (step > 1). Returns per output pixel {firstIndex, w0, w1, ...} in patch indices.
    public static double[][] Weights(int n, double lo, double step, int origin, int len)
    {
        double[][] res = new double[n][];
        double scale = Math.Max(1.0, step), support = 2.0 * scale;
        for (int u = 0; u < n; u++)
        {
            double c = lo + (u + 0.5) * step - origin - 0.5;   // patch index coordinate of the centre
            int i0 = (int)Math.Ceiling(c - support), i1 = (int)Math.Floor(c + support);
            double[] w = new double[i1 - i0 + 2];
            w[0] = i0;
            double sum = 0;
            for (int i = i0; i <= i1; i++) { double v = CatRom((i - c) / scale); w[i - i0 + 1] = v; sum += v; }
            for (int i = 1; i < w.Length; i++) w[i] /= sum;
            res[u] = w;
        }
        return res;
    }

    // Resamples an opaque patch onto W x H so that output [0, W) x [0, H) covers source
    // [L, R) x [T, B). Patch pixel (i, j) is source pixel (x0 + i, y0 + j).
    public static Img Resample(Img patch, int x0, int y0, double L, double T, double R, double B, int W, int H)
    {
        double[][] wx = Weights(W, L, (R - L) / W, x0, patch.W);
        double[][] wy = Weights(H, T, (B - T) / H, y0, patch.H);
        int ph = patch.H, pw = patch.W;
        float[] tr = new float[W * ph], tg = new float[W * ph], tb = new float[W * ph];
        for (int j = 0; j < ph; j++)
            for (int u = 0; u < W; u++)
            {
                double[] w = wx[u]; int i0 = (int)w[0];
                double r = 0, g = 0, b = 0;
                for (int k = 1; k < w.Length; k++)
                {
                    int i = i0 + k - 1; if (i < 0) i = 0; if (i >= pw) i = pw - 1;
                    int s = j * pw + i;
                    r += w[k] * patch.R[s]; g += w[k] * patch.G[s]; b += w[k] * patch.B[s];
                }
                int d = j * W + u;
                tr[d] = (float)r; tg[d] = (float)g; tb[d] = (float)b;
            }
        Img o = new Img(W, H);
        for (int v = 0; v < H; v++)
        {
            double[] w = wy[v]; int j0 = (int)w[0];
            for (int u = 0; u < W; u++)
            {
                double r = 0, g = 0, b = 0;
                for (int k = 1; k < w.Length; k++)
                {
                    int j = j0 + k - 1; if (j < 0) j = 0; if (j >= ph) j = ph - 1;
                    int s = j * W + u;
                    r += w[k] * tr[s]; g += w[k] * tg[s]; b += w[k] * tb[s];
                }
                int d = v * W + u;
                o.R[d] = (float)Math.Max(0, Math.Min(255, r));
                o.G[d] = (float)Math.Max(0, Math.Min(255, g));
                o.B[d] = (float)Math.Max(0, Math.Min(255, b));
                o.A[d] = 255f;
            }
        }
        return o;
    }

    // Anti-aliased coverage (0-1) of the rounded rect [0, W) x [0, H) with corner radius r:
    // ss x ss samples per pixel near the corners, 1 elsewhere. Quantised to 8 bits by the caller.
    public static float[] RoundRectMask(int W, int H, double r, int ss)
    {
        float[] m = new float[W * H];
        double[] rc = { r, r, r, r };
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                bool cross = (i >= r && i + 1 <= W - r) || (j >= r && j + 1 <= H - r);
                if (cross) { m[j * W + i] = 1f; continue; }
                int n = 0;
                for (int b = 0; b < ss; b++)
                    for (int a = 0; a < ss; a++)
                        if (Inside(i + (a + 0.5) / ss, j + (b + 0.5) / ss, 0, 0, W, H, rc)) n++;
                m[j * W + i] = (float)n / (ss * ss);
            }
        return m;
    }

    // The same with elliptical corners of radii rx, ry (compose-decks.ps1: a corner scaled by
    // slightly different factors across and down).
    public static float[] RoundRectMask(int W, int H, double rx, double ry, int ss)
    {
        float[] m = new float[W * H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                bool cross = (i >= rx && i + 1 <= W - rx) || (j >= ry && j + 1 <= H - ry);
                if (cross) { m[j * W + i] = 1f; continue; }
                int n = 0;
                for (int b = 0; b < ss; b++)
                    for (int a = 0; a < ss; a++)
                    {
                        double x = i + (a + 0.5) / ss, y = j + (b + 0.5) / ss;
                        double cx = x < rx ? rx : (x > W - rx ? W - rx : x), cy = y < ry ? ry : (y > H - ry ? H - ry : y);
                        double dx = (x - cx) / rx, dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1) n++;
                    }
                m[j * W + i] = (float)n / (ss * ss);
            }
        return m;
    }

    // The whole cut: clean mask, inpaint, resample, apply the shared alpha mask. Alpha is
    // quantised here so every file gets exactly the same alpha channel.
    public static Img CutCard(Img sheet, CutSpec s, int W, int H, float[] mask, int margin)
    {
        int x0 = (int)Math.Floor(s.L) - margin, y0 = (int)Math.Floor(s.T) - margin;
        int x1 = (int)Math.Ceiling(s.R) + margin, y1 = (int)Math.Ceiling(s.B) + margin;
        int w = x1 - x0, h = y1 - y0;
        bool[] clean = CleanMask(x0, y0, w, h, s);
        Img patch = Inpaint(sheet, x0, y0, w, h, clean, s);
        Img o = Resample(patch, x0, y0, s.L, s.T, s.R, s.B, W, H);
        for (int i = 0; i < W * H; i++) o.A[i] = Img.Q(mask[i] * 255f);
        return o;
    }

    // Colour fidelity of a cut: each opaque output pixel at least 'border' px from the edge is
    // compared with the source sampled bilinearly at the matching position. Returns {mean signed
    // difference R, G, B; mean absolute difference over the three channels}.
    public static double[] Fidelity(Img src, CutSpec s, Img o, int border)
    {
        double sx = (s.R - s.L) / o.W, sy = (s.B - s.T) / o.H;
        double dr = 0, dg = 0, db = 0, ad = 0; long n = 0;
        for (int v = border; v < o.H - border; v++)
            for (int u = border; u < o.W - border; u++)
            {
                int i = v * o.W + u;
                if (o.A[i] < 255) continue;
                double x = s.L + (u + 0.5) * sx - 0.5, y = s.T + (v + 0.5) * sy - 0.5;
                int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
                double fx = x - x0, fy = y - y0;
                int a = y0 * src.W + x0, b = a + 1, c = a + src.W, d = c + 1;
                double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                double r = src.R[a] * w00 + src.R[b] * w10 + src.R[c] * w01 + src.R[d] * w11;
                double g = src.G[a] * w00 + src.G[b] * w10 + src.G[c] * w01 + src.G[d] * w11;
                double bb = src.B[a] * w00 + src.B[b] * w10 + src.B[c] * w01 + src.B[d] * w11;
                dr += o.R[i] - r; dg += o.G[i] - g; db += o.B[i] - bb;
                ad += (Math.Abs(o.R[i] - r) + Math.Abs(o.G[i] - g) + Math.Abs(o.B[i] - bb)) / 3.0;
                n++;
            }
        return new double[] { dr / n, dg / n, db / n, ad / n };
    }

    // Debug view of a patch: sheet pixels, with non-clean pixels tinted magenta.
    public static Img CleanView(Img sheet, CutSpec s, int margin)
    {
        int x0 = (int)Math.Floor(s.L) - margin, y0 = (int)Math.Floor(s.T) - margin;
        int x1 = (int)Math.Ceiling(s.R) + margin, y1 = (int)Math.Ceiling(s.B) + margin;
        int w = x1 - x0, h = y1 - y0;
        bool[] clean = CleanMask(x0, y0, w, h, s);
        Img o = new Img(w, h);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int d = j * w + i, sx = x0 + i, sy = y0 + j;
                if (sx < 0 || sy < 0 || sx >= sheet.W || sy >= sheet.H) continue;
                int q = sy * sheet.W + sx;
                o.R[d] = sheet.R[q]; o.G[d] = sheet.G[q]; o.B[d] = sheet.B[q]; o.A[d] = 255f;
                if (!clean[d]) { o.R[d] = o.R[d] * 0.5f + 127f; o.B[d] = o.B[d] * 0.5f + 127f; o.G[d] *= 0.5f; }
            }
        return o;
    }
}

// ------------------------------------------------------------------------------- review

public static class Review
{
    public static Bitmap ToBitmap(Img im)
    {
        Bitmap bmp = new Bitmap(im.W, im.H, PixelFormat.Format32bppArgb);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, im.W, im.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] row = new byte[im.W * 4];
        for (int y = 0; y < im.H; y++)
        {
            for (int x = 0; x < im.W; x++)
            {
                int i = y * im.W + x;
                row[x * 4] = Img.Q(im.B[i]); row[x * 4 + 1] = Img.Q(im.G[i]); row[x * 4 + 2] = Img.Q(im.R[i]); row[x * 4 + 3] = Img.Q(im.A[i]);
            }
            Marshal.Copy(row, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), im.W * 4);
        }
        bmp.UnlockBits(d);
        return bmp;
    }

    public static Img FromBitmap(Bitmap bmp)
    {
        Img im = new Img(bmp.Width, bmp.Height);
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] row = new byte[bmp.Width * 4];
        for (int y = 0; y < bmp.Height; y++)
        {
            Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, bmp.Width * 4);
            for (int x = 0; x < bmp.Width; x++)
            {
                int i = y * bmp.Width + x;
                im.B[i] = row[x * 4]; im.G[i] = row[x * 4 + 1]; im.R[i] = row[x * 4 + 2]; im.A[i] = row[x * 4 + 3];
            }
        }
        bmp.UnlockBits(d);
        return im;
    }

    static Img Solid(int w, int h, Color c)
    {
        Img im = new Img(w, h);
        im.Fill(c.R, c.G, c.B, 255);
        return im;
    }

    // Writes text onto an opaque Img (GDI+), returns a new Img.
    public static Img Label(Img im, string[] texts, int[] xs, int[] ys, Color color, float size)
    {
        using (Bitmap bmp = ToBitmap(im))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(color))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                for (int i = 0; i < texts.Length; i++) g.DrawString(texts[i], f, br, xs[i], ys[i]);
            }
            return FromBitmap(bmp);
        }
    }

    // All cards in a grid on a flat colour, labelled underneath.
    public static Img Contact(Img[] cards, string[] labels, int cols, int gap, Color bg, Color ink)
    {
        int cw = cards[0].W, ch = cards[0].H, lab = 18;
        int rows = (cards.Length + cols - 1) / cols;
        Img sheet = Solid(gap + cols * (cw + gap), gap + rows * (ch + lab + gap), bg);
        List<string> t = new List<string>(); List<int> xs = new List<int>(), ys = new List<int>();
        for (int k = 0; k < cards.Length; k++)
        {
            if (cards[k] == null) continue;
            int x = gap + (k % cols) * (cw + gap), y = gap + (k / cols) * (ch + lab + gap);
            sheet.Over(cards[k], x, y);
            t.Add(labels[k]); xs.Add(x); ys.Add(y + ch + 1);
        }
        return Label(sheet, t.ToArray(), xs.ToArray(), ys.ToArray(), ink, 13f);
    }

    // For each card: its four corners (region x region px) enlarged k times, each on black and on
    // white. One row per card: TL-black TL-white TR-black TR-white BR-black ... BL-white.
    public static Img CornerZooms(Img[] cards, string[] labels, int region, int k)
    {
        int tile = region * k, gap = 6, labW = 120;
        int w = labW + 8 * (tile + gap), h = cards.Length * (tile + gap) + gap;
        Img sheet = Solid(w, h, Color.FromArgb(128, 128, 128));
        List<string> t = new List<string>(); List<int> xs = new List<int>(), ys = new List<int>();
        for (int c = 0; c < cards.Length; c++)
        {
            Img cd = cards[c];
            int[][] origin = { new[] { 0, 0 }, new[] { cd.W - region, 0 }, new[] { cd.W - region, cd.H - region }, new[] { 0, cd.H - region } };
            int y = gap + c * (tile + gap);
            for (int q = 0; q < 4; q++)
            {
                Img z = cd.Zoom(origin[q][0], origin[q][1], region, region, k);
                for (int s = 0; s < 2; s++)
                {
                    Img bgTile = Solid(tile, tile, s == 0 ? Color.Black : Color.White);
                    bgTile.Over(z, 0, 0);
                    sheet.Over(bgTile, labW + (q * 2 + s) * (tile + gap), y);
                }
            }
            t.Add(labels[c]); xs.Add(4); ys.Add(y + tile / 2 - 8);
        }
        return Label(sheet, t.ToArray(), xs.ToArray(), ys.ToArray(), Color.White, 14f);
    }

    // A hand: cards overlapped left to right so each shows 'visible' of its width.
    public static Img Hand(Img[] cards, double visible, Color table, int pad)
    {
        int cw = cards[0].W, ch = cards[0].H;
        int step = (int)Math.Round(cw * visible);
        Img sheet = Solid(pad * 2 + step * (cards.Length - 1) + cw, pad * 2 + ch, table);
        for (int k = 0; k < cards.Length; k++) sheet.Over(cards[k], pad + k * step, pad);
        return sheet;
    }

    // Side-by-side stack of images on a flat colour (for combining review strips).
    public static Img Stack(Img[] ims, bool vertical, int gap, Color bg)
    {
        int w = 0, h = 0;
        foreach (Img im in ims) { if (vertical) { w = Math.Max(w, im.W); h += im.H + gap; } else { h = Math.Max(h, im.H); w += im.W + gap; } }
        Img o = Solid(w + gap, h + gap, bg);
        int p = gap;
        foreach (Img im in ims) { if (vertical) { o.Over(im, gap, p); p += im.H + gap; } else { o.Over(im, p, gap); p += im.W + gap; } }
        return o;
    }
}

// ------------------------------------------------------------------------------- index box

public static class IndexBox
{
    // Ink of the corner index on an exported face. Light decks: black or red ink. Dark deck
    // (Kathmandu Gold): bright gold or red ink. Only the region inside the frame and below the
    // corner ornament's diagonal (x + y > diag) is considered.
    static bool Ink(Img im, int x, int y, bool dark)
    {
        int i = y * im.W + x;
        double r = im.R[i], g = im.G[i], b = im.B[i], l = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        if (!dark) return l < 110 || (r - g > 70 && r > 120);
        return (l > 140 && r - b > 60) || (r - g > 90 && r > 130);
    }

    // Returns {rankX0, rankY0, rankX1, rankY1, suitX0, suitY0, suitX1, suitY1} (inclusive px) of
    // the top-left index, or null. Components (8-connected, >= 12 px) inside the search window:
    // the rank is every component starting above y0 + 22 whose centre is left of xMax; the suit is
    // the largest component starting just below the rank and centred under it.
    public static int[] Find(Img im, bool dark, int x0, int y0, int x1, int y1, int diag)
    {
        int w = x1 - x0, h = y1 - y0;
        int[] lab = new int[w * h];
        for (int k = 0; k < lab.Length; k++) lab[k] = -1;
        bool[] ink = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                ink[y * w + x] = (x0 + x) + (y0 + y) > diag && Ink(im, x0 + x, y0 + y, dark);
        List<int[]> comps = new List<int[]>(); // minx, miny, maxx, maxy, count, sumx
        Stack<int> st = new Stack<int>();
        for (int s = 0; s < w * h; s++)
        {
            if (!ink[s] || lab[s] >= 0) continue;
            int id = comps.Count;
            int[] c = { int.MaxValue, int.MaxValue, -1, -1, 0, 0 };
            lab[s] = id; st.Push(s);
            while (st.Count > 0)
            {
                int q = st.Pop(), qx = q % w, qy = q / w;
                c[0] = Math.Min(c[0], qx); c[1] = Math.Min(c[1], qy); c[2] = Math.Max(c[2], qx); c[3] = Math.Max(c[3], qy); c[4]++; c[5] += qx;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = qx + dx, ny = qy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int nq = ny * w + nx;
                        if (ink[nq] && lab[nq] < 0) { lab[nq] = id; st.Push(nq); }
                    }
            }
            comps.Add(c);
        }
        int[] rank = null;
        foreach (int[] c in comps)
        {
            if (c[4] < 12 || c[1] > 22) continue;          // rank glyphs start near the top of the window
            double cx = (double)c[5] / c[4];
            if (cx > 0.62 * w) continue;                  // court crowns, pips to the right
            if (rank == null) rank = new int[] { c[0], c[1], c[2], c[3] };
            else { rank[0] = Math.Min(rank[0], c[0]); rank[1] = Math.Min(rank[1], c[1]); rank[2] = Math.Max(rank[2], c[2]); rank[3] = Math.Max(rank[3], c[3]); }
        }
        if (rank == null) return null;
        double rmid = (rank[0] + rank[2]) / 2.0;
        int[] suit = null; int best = 0;
        foreach (int[] c in comps)
        {
            if (c[4] < 12 || c[1] < rank[3] - 2 || c[1] > rank[3] + 18) continue;
            double cx = (double)c[5] / c[4];
            if (Math.Abs(cx - rmid) > 9) continue;
            if (c[4] > best) { best = c[4]; suit = c; }
        }
        if (suit == null) return null;
        return new int[] { x0 + rank[0], y0 + rank[1], x0 + rank[2], y0 + rank[3], x0 + suit[0], y0 + suit[1], x0 + suit[2], y0 + suit[3] };
    }
}

// ------------------------------------------------------------------------------- frame alignment

public static class FrameAlign
{
    // Luminance profile inward from one side (0 left, 1 top, 2 right, 3 bottom), averaged over the
    // middle half of that side, 'depth' px deep.
    public static double[] Profile(Img im, int side, int depth)
    {
        double[] p = new double[depth];
        bool vert = side == 0 || side == 2;
        int len = vert ? im.H : im.W, a0 = len / 4, a1 = len * 3 / 4;
        for (int k = 0; k < depth; k++)
        {
            double s = 0;
            for (int a = a0; a < a1; a++)
            {
                int x = side == 0 ? k : side == 2 ? im.W - 1 - k : a;
                int y = side == 1 ? k : side == 3 ? im.H - 1 - k : a;
                int i = y * im.W + x;
                s += 0.2126 * im.R[i] + 0.7152 * im.G[i] + 0.0722 * im.B[i];
            }
            p[k] = s / (a1 - a0);
        }
        return p;
    }

    static double At(double[] p, double t)
    {
        if (t <= 0) return p[0];
        if (t >= p.Length - 1) return p[p.Length - 1];
        int i = (int)Math.Floor(t); double f = t - i;
        return p[i] * (1 - f) + p[i + 1] * f;
    }

    // Shift (px, positive = frame further in) that best aligns profile p with reference q. Searched
    // within +-3 px; the error is weighted up 5 % per px of shift so that a repeating pattern (a
    // double gold line) cannot pull the match onto a false minimum far from zero.
    public static double Offset(double[] p, double[] q)
    {
        double best = 0, bestS = double.MaxValue;
        for (double d = -3; d <= 3.0001; d += 0.05)
        {
            double s = 0;
            for (int k = 4; k < q.Length - 4; k++) { double e = At(p, k + d) - q[k]; s += e * e; }
            s *= 1 + 0.05 * Math.Abs(d);
            if (s < bestS) { bestS = s; best = d; }
        }
        return best;
    }
}

// ------------------------------------------------------------------------------- checks

public static class Qa
{
    // Automatic checks on one exported file against the expected size and the shared mask.
    // Returns a list of problems (empty when clean) and fills stats {semiInterior, maskDiff,
    // shadowRim}.
    public static List<string> Check(string path, int W, int H, float[] mask, Bg sheetBg, int[] stats)
    {
        List<string> errs = new List<string>();
        int[] hdr = Png.Header(path);
        if (hdr == null) { errs.Add("not a PNG"); return errs; }
        if (hdr[0] != W || hdr[1] != H) errs.Add(string.Format("size {0}x{1}", hdr[0], hdr[1]));
        if (hdr[2] != 8 || hdr[3] != 6) errs.Add(string.Format("bit depth {0} colour type {1} (want 8 / 6 RGBA)", hdr[2], hdr[3]));
        if (hdr[4] != 0) errs.Add("interlaced");
        Img im = Img.Load(path);
        if (im.W != W || im.H != H) return errs;
        int[] corners = { 0, W - 1, (H - 1) * W, (H - 1) * W + W - 1 };
        foreach (int c in corners) if (im.A[c] != 0) errs.Add("corner pixel not transparent");
        int centre = (H / 2) * W + W / 2;
        if (im.A[centre] != 255) errs.Add("centre pixel not opaque");
        int semiInterior = 0, maskDiff = 0, shadowRim = 0;
        for (int i = 0; i < W * H; i++)
        {
            int want = Img.Q(mask[i] * 255f);
            if ((int)im.A[i] != want) maskDiff++;
            if (mask[i] >= 1f && im.A[i] < 255) semiInterior++;
        }
        // Outer ring: opaque pixels next to the image border or to a non-opaque pixel. A ring
        // pixel is suspected shadow when its colour lies on the sheet's shadow line (darker than
        // the parchment by > 10 %, chroma residual < 10) and it is more than 12 levels darker
        // than the pixel 2 px further in (a halo would be darker than the rim behind it).
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (im.A[i] < 255) continue;
                bool ring = x == 0 || y == 0 || x == W - 1 || y == H - 1 ||
                            im.A[i - 1] < 255 || im.A[i + 1] < 255 || im.A[i - W] < 255 || im.A[i + W] < 255;
                if (!ring || sheetBg == null) continue;
                double[] c = { im.R[i], im.G[i], im.B[i] };
                if (!(sheetBg.Level(c) > 0.10 && sheetBg.Residual(c) < 10)) continue;
                int ix = x < W / 2 ? x + 2 : x - 2, iy = y < H / 2 ? y + 2 : y - 2;
                if (Math.Min(x, W - 1 - x) < Math.Min(y, H - 1 - y)) iy = y; else ix = x;   // step along the nearer border's normal
                int k = iy * W + ix;
                double lin = 0.2126 * im.R[k] + 0.7152 * im.G[k] + 0.0722 * im.B[k];
                double l = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
                if (lin - l > 12) shadowRim++;
            }
        if (semiInterior > 0) errs.Add(semiInterior + " semi-transparent interior pixels");
        if (maskDiff > 0) errs.Add(maskDiff + " alpha values differ from the shared mask");
        if (stats != null) { stats[0] = semiInterior; stats[1] = maskDiff; stats[2] = shadowRim; }
        return errs;
    }
}
