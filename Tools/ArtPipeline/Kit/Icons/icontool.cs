#:property PublishAot=false
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property Nullable=disable
#:property ImplicitUsings=disable
#:property InvariantGlobalization=true
#:property NoWarn=CA1416
// icontool - the Icons kit pipeline (family "Icons", see ArtSource/Prompts/Icons_Kit.md).
// A single-file program: run it with   Tools/ArtPipeline/Kit/Icons/icons.sh <command> [options]
//
//   measure  [--only A,B]                         ink box, ink and ground colours of every icon on its mockup
//   sheet    --sheet S1 [--review]                contact sheet of enlarged crops for ChatGPT (attach/)
//   split    --sheet S1 --in chatgpt/S1_a1.png --attempt a1      cut ChatGPT's redrawn sheet into cells
//   make     --name Icon_Back --src cell.png [...]             fit a cell to the mockup and export the sprite
//   place    [--only A,B]                         where each final sprite lands on every screen that uses it
//   review   --out file.png --names A,B,...       review sheet of finished sprites against their crops
//
// All rectangles are mockup pixels (1672 x 941, top-left origin). Alpha and blending are solved in
// linear light, because the project renders in the Linear colour space and uGUI blends there.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
return Tool.Run(args);

internal sealed class Img
{
    public readonly int W, H;
    public readonly float[] R, G, B, A;
    private static readonly float[] Lut = BuildLut();

    public Img(int w, int h, float alpha = 1f)
    {
        W = w; H = h;
        R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; A = new float[w * h];
        if (alpha != 0f) Array.Fill(A, alpha);
    }

    public int Count => W * H;
    private static float[] BuildLut() { var l = new float[256]; for (int i = 0; i < 256; i++) l[i] = ToLinear(i / 255f); return l; }
    public static float ToLinear(float s) => s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f);
    public static float ToSrgb(float l) => l <= 0f ? 0f : l >= 1f ? 1f : l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
    public static byte ToByte(float l) => (byte)Math.Clamp((int)MathF.Round(ToSrgb(l) * 255f), 0, 255);
    public float Lum(int i) => 0.2126f * R[i] + 0.7152f * G[i] + 0.0722f * B[i];

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
                img.B[i] = Lut[row[x * 4]]; img.G[i] = Lut[row[x * 4 + 1]]; img.R[i] = Lut[row[x * 4 + 2]]; img.A[i] = row[x * 4 + 3] / 255f;
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
                row[x * 4] = ToByte(B[i]); row[x * 4 + 1] = ToByte(G[i]); row[x * 4 + 2] = ToByte(R[i]);
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

    public Img Crop(int x0, int y0, int w, int h)
    {
        var c = new Img(w, h, 0f);
        for (int y = 0; y < h; y++)
        {
            int sy = y0 + y; if (sy < 0 || sy >= H) continue;
            for (int x = 0; x < w; x++)
            {
                int sx = x0 + x; if (sx < 0 || sx >= W) continue;
                int s = sy * W + sx, t = y * w + x;
                c.R[t] = R[s]; c.G[t] = G[s]; c.B[t] = B[s]; c.A[t] = A[s];
            }
        }
        return c;
    }

    public void Fill(float r, float g, float b, float a = 1f)
    {
        Array.Fill(R, r); Array.Fill(G, g); Array.Fill(B, b); Array.Fill(A, a);
    }

    /// <summary>Alpha-blends src (straight alpha) over this opaque image in linear light.</summary>
    public void Over(Img src, int x0, int y0)
    {
        for (int y = 0; y < src.H; y++)
        {
            int ty = y0 + y; if (ty < 0 || ty >= H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int tx = x0 + x; if (tx < 0 || tx >= W) continue;
                int s = y * src.W + x, t = ty * W + tx;
                float a = src.A[s]; if (a <= 0f) continue;
                R[t] = R[t] * (1f - a) + src.R[s] * a; G[t] = G[t] * (1f - a) + src.G[s] * a; B[t] = B[t] * (1f - a) + src.B[s] * a;
                A[t] = A[t] + (1f - A[t]) * a;
            }
        }
    }

    public void Paste(Img src, int x0, int y0)
    {
        for (int y = 0; y < src.H; y++)
        {
            int ty = y0 + y; if (ty < 0 || ty >= H) continue;
            for (int x = 0; x < src.W; x++)
            {
                int tx = x0 + x; if (tx < 0 || tx >= W) continue;
                int s = y * src.W + x, t = ty * W + tx;
                R[t] = src.R[s]; G[t] = src.G[s]; B[t] = src.B[s]; A[t] = src.A[s];
            }
        }
    }

    /// <summary>Resize through GDI+ (premultiplied, sRGB bytes): for previews and enlarged crops only.</summary>
    public Img Resize(int w, int h, bool nearest = false)
    {
        using Bitmap src = ToBitmap();
        using var pre = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(pre))
        using (var attrs = new ImageAttributes())
        {
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = nearest ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = nearest ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        using Bitmap straight = pre.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
        return FromBitmap(straight);
    }

    public static (float r, float g, float b) Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return (ToLinear(Convert.ToByte(hex.Substring(0, 2), 16) / 255f), ToLinear(Convert.ToByte(hex.Substring(2, 2), 16) / 255f), ToLinear(Convert.ToByte(hex.Substring(4, 2), 16) / 255f));
    }

    public static string Hex(float r, float g, float b) => $"#{ToByte(r):X2}{ToByte(g):X2}{ToByte(b):X2}";
}

/// <summary>One icon of the family: where it is drawn largest on the mockups, and every other place it is used.</summary>
internal sealed class IconDef
{
    public string Name;
    public string Mode = "ink";      // ink: one flat ink colour; color: keeps its own colours
    public string Ink;               // ink colour of the final sprite (default: the family charcoal)
    public string Screen;            // mockup the sprite is fitted to
    public double[] Rect;            // loose rect on that mockup: the whole icon and only ground around it
    public double[] Disc;            // the round button it sits on (x, y, w, h), when the loose rect leaves it
    public bool Symmetric;           // mirror-symmetric about its vertical axis
    public string Note;
    public List<(string screen, double[] rect, string old)> Uses = new List<(string, double[], string)>();
}

internal static class Spec
{
    public static string Repo;
    public static string FamilyInk = "#0E0B0B";
    public static List<IconDef> Icons = new List<IconDef>();
    private static readonly Dictionary<string, Img> Mockups = new Dictionary<string, Img>();

    public static string Here => Path.Combine(Repo, "Tools", "ArtPipeline", "Kit", "Icons");
    public static string Work => Path.Combine(Repo, "ArtSource", "Working", "Kit", "Icons");

    public static void Load()
    {
        string dir = AppContext.BaseDirectory;
        Repo = Directory.GetCurrentDirectory();
        while (Repo != null && !File.Exists(Path.Combine(Repo, "AGENTS.md"))) Repo = Path.GetDirectoryName(Repo);
        if (Repo == null) throw new InvalidOperationException("run from inside the repository");
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(Here, "icons.json")), null, opts);
        if (root["ink"] != null) FamilyInk = (string)root["ink"];
        foreach (JsonNode n in root["icons"].AsArray())
        {
            var d = new IconDef
            {
                Name = (string)n["name"],
                Mode = (string)n["mode"] ?? "ink",
                Ink = (string)n["ink"],
                Screen = (string)n["screen"],
                Rect = n["rect"].AsArray().Select(v => (double)v).ToArray(),
                Disc = n["disc"]?.AsArray().Select(v => (double)v).ToArray(),
                Symmetric = n["symmetric"] != null && (bool)n["symmetric"],
                Note = (string)n["note"],
            };
            if (n["uses"] != null)
                foreach (JsonNode u in n["uses"].AsArray())
                    d.Uses.Add(((string)u["screen"], u["rect"].AsArray().Select(v => (double)v).ToArray(), (string)u["old"]));
            Icons.Add(d);
        }
    }

    public static IconDef Get(string name) => Icons.FirstOrDefault(i => i.Name == name) ?? throw new ArgumentException($"no icon '{name}' in icons.json");

    public static string MockupPath(string screen)
    {
        if (File.Exists(screen)) return screen;
        string dir = Path.Combine(Repo, "Assets", "ArtRefrences", "game-screens");
        return Directory.GetFiles(dir, "*.png").FirstOrDefault(f => Path.GetFileName(f).StartsWith(screen, StringComparison.OrdinalIgnoreCase))
               ?? throw new FileNotFoundException($"no mockup matches '{screen}'");
    }

    public static Img Mockup(string screen)
    {
        if (!Mockups.TryGetValue(screen, out Img m)) Mockups[screen] = m = Img.Load(MockupPath(screen));
        return m;
    }
}

/// <summary>An icon as it is drawn on a mockup: the crop, the ground under it and how much ink covers each pixel.</summary>
internal sealed class MockIcon
{
    public Img Region;        // the loose rect, integer-aligned
    public int X0, Y0;        // its origin on the mockup
    public Img Ground;        // smooth ground fitted to the pixels around the icon
    public (float r, float g, float b) Ink;
    public float[] Cover;     // ink coverage per pixel (linear light), or silhouette alpha for multi-coloured icons
    public double[] Box;      // ink box on the mockup: left, top, right, bottom (sub-pixel)

    public bool[] Inside;     // false where the loose rect leaves the round button the icon sits on

    public static MockIcon Read(IconDef d) => Read(d.Screen, d.Rect, d.Mode == "color", d.Disc);

    public static MockIcon Read(string screen, double[] rect, bool colour, double[] disc = null)
    {
        Img mock = Spec.Mockup(screen);
        int x0 = (int)Math.Floor(rect[0]), y0 = (int)Math.Floor(rect[1]);
        int x1 = (int)Math.Ceiling(rect[0] + rect[2]), y1 = (int)Math.Ceiling(rect[1] + rect[3]);
        var m = new MockIcon { X0 = x0, Y0 = y0, Region = mock.Crop(x0, y0, x1 - x0, y1 - y0) };
        m.Inside = new bool[m.Region.Count];
        for (int y = 0; y < m.Region.H; y++)
            for (int x = 0; x < m.Region.W; x++)
            {
                bool ok = true;
                if (disc != null)
                {
                    double rx = disc[2] / 2 - 3, ry = disc[3] / 2 - 3;
                    double dx = (x0 + x + 0.5 - (disc[0] + disc[2] / 2)) / rx, dy = (y0 + y + 0.5 - (disc[1] + disc[3] / 2)) / ry;
                    ok = dx * dx + dy * dy <= 1.0;
                }
                m.Inside[y * m.Region.W + x] = ok;
            }
        m.Solve(colour);
        return m;
    }

    private void Solve(bool colour)
    {
        Img r = Region;
        int w = r.W, h = r.H, n = r.Count;
        // Ground: a quadratic surface per channel, first through the rect's frame, then through
        // every pixel that stays close to it and is not next to the icon.
        var use = new bool[n];
        var fr = new List<float>(); var fg = new List<float>(); var fb = new List<float>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!Inside[i]) continue;
                bool frame = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                // With a round button the frame is mostly outside it: take the pixels next to the outside too.
                if (!frame)
                    for (int dy = -2; dy <= 2 && !frame; dy++)
                        for (int dx = -2; dx <= 2 && !frame; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < w && yy < h && !Inside[yy * w + xx]) frame = true;
                        }
                if (frame) { fr.Add(r.R[i]); fg.Add(r.G[i]); fb.Add(r.B[i]); }
            }
        fr.Sort(); fg.Sort(); fb.Sort();
        Ground = new Img(w, h);
        Ground.Fill(fr[fr.Count / 2], fg[fg.Count / 2], fb[fb.Count / 2]);
        for (int pass = 0; pass < 3; pass++)
        {
            var near = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float dr = r.R[i] - Ground.R[i], dg = r.G[i] - Ground.G[i], db = r.B[i] - Ground.B[i];
                near[i] = MathF.Sqrt(dr * dr + dg * dg + db * db) < (pass == 0 ? 0.09f : 0.045f);
            }
            // Keep ground pixels only where the 5x5 neighbourhood is ground too (edge blur stays out).
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool ok = true;
                    for (int dy = -2; dy <= 2 && ok; dy++)
                        for (int dx = -2; dx <= 2 && ok; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            if (!near[yy * w + xx] && Inside[yy * w + xx]) ok = false;
                        }
                    use[y * w + x] = ok && Inside[y * w + x];
                }
            if (use.Count(u => u) < 24) break;
            Ground = FitSurface(r, use, pass == 0 ? 1 : 2);
        }

        if (colour)
        {
            // Silhouette of a multi-coloured icon: distance from the ground, anything enclosed is solid.
            Cover = new float[n];
            for (int i = 0; i < n; i++)
            {
                float dr = r.R[i] - Ground.R[i], dg = r.G[i] - Ground.G[i], db = r.B[i] - Ground.B[i];
                float d = MathF.Sqrt(dr * dr + dg * dg + db * db);
                Cover[i] = Math.Clamp((d - 0.03f) / 0.22f, 0f, 1f);
            }
            Ink = (0f, 0f, 0f);
        }
        else
        {
            // Ink colour: the darkest fifth of what clearly differs from the ground.
            var cands = new List<(float d, int i)>();
            for (int i = 0; i < n; i++)
            {
                float d = Ground.Lum(i) - r.Lum(i);
                float dr = r.R[i] - Ground.R[i], dg = r.G[i] - Ground.G[i], db = r.B[i] - Ground.B[i];
                float dist = MathF.Sqrt(dr * dr + dg * dg + db * db);
                if (dist > 0.05f) cands.Add((dist, i));
            }
            if (cands.Count == 0) { Ink = (0f, 0f, 0f); Cover = new float[n]; Box = null; return; }
            cands.Sort((a, b) => b.d.CompareTo(a.d));
            int take = Math.Max(1, cands.Count / 5);
            double ir = 0, ig = 0, ib = 0;
            for (int k = 0; k < take; k++) { int i = cands[k].i; ir += r.R[i]; ig += r.G[i]; ib += r.B[i]; }
            Ink = ((float)(ir / take), (float)(ig / take), (float)(ib / take));
            Cover = new float[n];
            for (int i = 0; i < n; i++)
            {
                float dr = Ground.R[i] - Ink.r, dg = Ground.G[i] - Ink.g, db = Ground.B[i] - Ink.b;
                float den = dr * dr + dg * dg + db * db;
                float num = (Ground.R[i] - r.R[i]) * dr + (Ground.G[i] - r.G[i]) * dg + (Ground.B[i] - r.B[i]) * db;
                Cover[i] = den < 1e-6f ? 0f : Math.Clamp(num / den, 0f, 1f);
            }
        }
        for (int i = 0; i < n; i++) if (!Inside[i]) Cover[i] = 0f;
        Box = Bounds.HalfBox(Cover, w, h, X0, Y0);
    }

    /// <summary>The icon as flat ink on white (single-ink icons) or its own pixels on its ground (multi-coloured),
    /// for the mockup rect (x, y, w, h); whatever lies outside the loose rect or the button is plain.</summary>
    public Img Tile(double x, double y, double w, double h, bool colour)
    {
        int tx0 = (int)Math.Floor(x), ty0 = (int)Math.Floor(y), tw = (int)Math.Ceiling(x + w) - tx0, th = (int)Math.Ceiling(y + h) - ty0;
        var t = new Img(tw, th);
        for (int yy = 0; yy < th; yy++)
            for (int xx = 0; xx < tw; xx++)
            {
                int rx = tx0 + xx - X0, ry = ty0 + yy - Y0, ti = yy * tw + xx;
                bool inRegion = rx >= 0 && ry >= 0 && rx < Region.W && ry < Region.H;
                int ri = inRegion ? ry * Region.W + rx : 0;
                if (colour)
                {
                    int cx = Math.Clamp(rx, 0, Region.W - 1), cy = Math.Clamp(ry, 0, Region.H - 1), ci = cy * Region.W + cx;
                    bool own = inRegion && Inside[ri];
                    t.R[ti] = own ? Region.R[ri] : Ground.R[ci]; t.G[ti] = own ? Region.G[ri] : Ground.G[ci]; t.B[ti] = own ? Region.B[ri] : Ground.B[ci];
                }
                else
                {
                    float c = inRegion ? Cover[ri] : 0f;
                    t.R[ti] = 1f + (Ink.r - 1f) * c; t.G[ti] = 1f + (Ink.g - 1f) * c; t.B[ti] = 1f + (Ink.b - 1f) * c;
                }
            }
        return t;
    }

    /// <summary>Least-squares polynomial surface (order 1 or 2) through the marked pixels, per channel.</summary>
    private static Img FitSurface(Img r, bool[] use, int order)
    {
        int w = r.W, h = r.H;
        int terms = order == 1 ? 3 : 6;
        var ata = new double[terms, terms];
        var atb = new double[3, terms];
        var basis = new double[terms];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!use[i]) continue;
                Basis(x / (double)w - 0.5, y / (double)h - 0.5, order, basis);
                for (int a = 0; a < terms; a++)
                {
                    for (int b = 0; b < terms; b++) ata[a, b] += basis[a] * basis[b];
                    atb[0, a] += basis[a] * r.R[i]; atb[1, a] += basis[a] * r.G[i]; atb[2, a] += basis[a] * r.B[i];
                }
            }
        var coef = new double[3][];
        for (int c = 0; c < 3; c++)
        {
            var rhs = new double[terms];
            for (int a = 0; a < terms; a++) rhs[a] = atb[c, a];
            coef[c] = Solve((double[,])ata.Clone(), rhs);
        }
        var g = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Basis(x / (double)w - 0.5, y / (double)h - 0.5, order, basis);
                double vr = 0, vg = 0, vb = 0;
                for (int a = 0; a < terms; a++) { vr += coef[0][a] * basis[a]; vg += coef[1][a] * basis[a]; vb += coef[2][a] * basis[a]; }
                int i = y * w + x;
                g.R[i] = (float)Math.Clamp(vr, 0, 1); g.G[i] = (float)Math.Clamp(vg, 0, 1); g.B[i] = (float)Math.Clamp(vb, 0, 1);
            }
        return g;
    }

    private static void Basis(double x, double y, int order, double[] b)
    {
        b[0] = 1; b[1] = x; b[2] = y;
        if (order > 1) { b[3] = x * x; b[4] = x * y; b[5] = y * y; }
    }

    private static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        for (int i = 0; i < n; i++) a[i, i] += 1e-9;
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[p, c])) p = r;
            if (p != c)
            {
                for (int k = 0; k < n; k++) (a[c, k], a[p, k]) = (a[p, k], a[c, k]);
                (b[c], b[p]) = (b[p], b[c]);
            }
            double d = a[c, c];
            if (Math.Abs(d) < 1e-14) continue;
            for (int r = c + 1; r < n; r++)
            {
                double f = a[r, c] / d;
                if (f == 0) continue;
                for (int k = c; k < n; k++) a[r, k] -= f * a[c, k];
                b[r] -= f * b[c];
            }
        }
        var x = new double[n];
        for (int r = n - 1; r >= 0; r--)
        {
            double s = b[r];
            for (int k = r + 1; k < n; k++) s -= a[r, k] * x[k];
            x[r] = Math.Abs(a[r, r]) < 1e-14 ? 0 : s / a[r, r];
        }
        return x;
    }
}

internal static class Bounds
{
    /// <summary>Box of a coverage map where the row/column maximum crosses one half, sub-pixel.</summary>
    public static double[] HalfBox(float[] c, int w, int h, double ox = 0, double oy = 0, float level = 0.5f)
    {
        var col = new float[w]; var row = new float[h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = c[y * w + x];
                if (v > col[x]) col[x] = v;
                if (v > row[y]) row[y] = v;
            }
        int x0 = Array.FindIndex(col, v => v >= level), x1 = Array.FindLastIndex(col, v => v >= level);
        int y0 = Array.FindIndex(row, v => v >= level), y1 = Array.FindLastIndex(row, v => v >= level);
        if (x0 < 0 || y0 < 0) return null;
        return new[] { ox + Cross(col, x0, -1, level), oy + Cross(row, y0, -1, level), ox + Cross(col, x1, 1, level), oy + Cross(row, y1, 1, level) };
    }

    // Position of the level crossing next to index i (the first or last sample at or above it):
    // samples sit at pixel centres, the crossing is interpolated towards the neighbour outside.
    private static double Cross(float[] v, int i, int dir, float level)
    {
        int j = i + dir;
        float outside = j < 0 || j >= v.Length ? 0f : v[j];
        double t = v[i] - outside <= 1e-6 ? 0.5 : (v[i] - level) / (v[i] - outside);
        double centre = i + 0.5;
        return centre + dir * Math.Min(1.0, t);
    }
}

internal static class Tool
{
    public static int Run(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine("icontool measure|sheet|split|make|place|review  (see the header of icontool.cs)"); return 0; }
        try
        {
            Spec.Load();
            var opt = new Dictionary<string, string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string k = args[i].Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) opt[k] = args[++i]; else opt[k] = "true";
            }
            string Get(string k, string d = null) => opt.TryGetValue(k, out string v) ? v : d;
            switch (args[0])
            {
                case "measure": return Measure(Get("only"));
                case "sheet": return Sheets.Build(Get("sheet"), opt.ContainsKey("review"));
                case "strip": return Sheets.Strip(Get("images").Split(','), Get("captions")?.Split(','), double.Parse(Get("scale", "2")), Get("out"));
                default:
                    Console.Error.WriteLine($"unknown command '{args[0]}'");
                    return 2;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"error: {e.Message}\n{e.StackTrace}");
            return 1;
        }
    }

    public static int Even(double v) { int n = (int)Math.Ceiling(v - 1e-6); return n % 2 == 0 ? n : n + 1; }

    private static int Measure(string only)
    {
        string[] names = only?.Split(',');
        Console.WriteLine($"{"icon",-26} {"scr",3}  {"ink box on the mockup (x, y, w, h)",-36} {"@2x ink",9}  {"ink",7} {"ground",7}");
        foreach (IconDef d in Spec.Icons)
        {
            if (names != null && !names.Contains(d.Name)) continue;
            MockIcon m = MockIcon.Read(d);
            if (m.Box == null) { Console.WriteLine($"{d.Name,-26} {d.Screen,3}  no ink"); continue; }
            double w = m.Box[2] - m.Box[0], h = m.Box[3] - m.Box[1];
            int c = (m.Region.H / 2) * m.Region.W + 1;
            Console.WriteLine($"{d.Name,-26} {d.Screen,3}  {m.Box[0],8:0.00} {m.Box[1],8:0.00} {w,7:0.00} {h,7:0.00}    {Even(w * 2.3),4}x{Even(h * 2.3),-4} {Img.Hex(m.Ink.r, m.Ink.g, m.Ink.b)} {Img.Hex(m.Ground.R[c], m.Ground.G[c], m.Ground.B[c])}");
        }
        return 0;
    }
}

/// <summary>Contact sheets: enlarged crops on a labelled grid, one icon per cell.</summary>
internal static class Sheets
{
    public sealed class Sheet
    {
        public string Name;
        public int W = 1536, H = 1024, Cols = 4, Rows = 2;
        public string Background = "#FFFFFF";
        public double Pad = 0.16;         // ground kept around the ink box, as a fraction of its larger side
        public double Fill = 0.62;        // share of the cell the tile may take
        public double MaxScale = 8;
        public List<string> Cells = new List<string>();
        public List<string> Captions = new List<string>();
    }

    public static Sheet Load(string name)
    {
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode n = JsonNode.Parse(File.ReadAllText(Path.Combine(Spec.Here, "sheets", name + ".json")), null, opts);
        var s = new Sheet { Name = name };
        if (n["canvas"] != null) { s.W = (int)n["canvas"][0]; s.H = (int)n["canvas"][1]; }
        if (n["cols"] != null) s.Cols = (int)n["cols"];
        if (n["rows"] != null) s.Rows = (int)n["rows"];
        if (n["background"] != null) s.Background = (string)n["background"];
        if (n["pad"] != null) s.Pad = (double)n["pad"];
        if (n["fill"] != null) s.Fill = (double)n["fill"];
        if (n["maxScale"] != null) s.MaxScale = (double)n["maxScale"];
        foreach (JsonNode c in n["cells"].AsArray())
        {
            if (c is JsonObject o) { s.Cells.Add((string)o["icon"]); s.Captions.Add((string)o["caption"]); }
            else { s.Cells.Add((string)c); s.Captions.Add(null); }
        }
        return s;
    }

    public static RectangleF Cell(Sheet s, int index, double w, double h)
    {
        double cw = w / s.Cols, ch = h / s.Rows;
        return new RectangleF((float)(index % s.Cols * cw), (float)(index / s.Cols * ch), (float)cw, (float)ch);
    }

    public static int Build(string name, bool review)
    {
        Sheet s = Load(name);
        using var bmp = new Bitmap(s.W, s.H, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(ColorTranslator.FromHtml(s.Background));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", s.H / 46f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(255, 150, 150, 150));
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        var log = new StringBuilder();
        for (int i = 0; i < s.Cells.Count; i++)
        {
            IconDef d = Spec.Get(s.Cells[i]);
            MockIcon m = MockIcon.Read(d);
            double bw = m.Box[2] - m.Box[0], bh = m.Box[3] - m.Box[1];
            double pad = Math.Max(3, Math.Max(bw, bh) * s.Pad);
            // The tile: the ink box plus some ground. Single-ink icons are shown as their ink on white
            // (no tile edge at all); multi-coloured ones as the mockup pixels, clipped to the loose rect.
            bool colour = d.Mode == "color";
            double tx0 = Math.Floor(m.Box[0] - pad), ty0 = Math.Floor(m.Box[1] - pad), tx1 = Math.Ceiling(m.Box[2] + pad), ty1 = Math.Ceiling(m.Box[3] + pad);
            if (colour)
            {
                tx0 = Math.Max(Math.Floor(d.Rect[0]), tx0); ty0 = Math.Max(Math.Floor(d.Rect[1]), ty0);
                tx1 = Math.Min(Math.Ceiling(d.Rect[0] + d.Rect[2]), tx1); ty1 = Math.Min(Math.Ceiling(d.Rect[1] + d.Rect[3]), ty1);
            }
            RectangleF cell = Cell(s, i, s.W, s.H);
            float captionH = font.Height * 1.6f;
            double availW = cell.Width * s.Fill, availH = (cell.Height - captionH) * s.Fill;
            double k = Math.Min(s.MaxScale, Math.Min(availW / (tx1 - tx0), availH / (ty1 - ty0)));
            float tw = (float)((tx1 - tx0) * k), th = (float)((ty1 - ty0) * k);
            float px = cell.X + (cell.Width - tw) / 2f, py = cell.Y + (cell.Height - captionH - th) / 2f;
            using (Bitmap tile = m.Tile(tx0, ty0, tx1 - tx0, ty1 - ty0, colour).ToBitmap())
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(tile, new Rectangle((int)Math.Round(px), (int)Math.Round(py), (int)Math.Round(tw), (int)Math.Round(th)), 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel, attrs);
            }
            string caption = s.Captions[i] ?? d.Name.Replace("Icon_", "").ToLowerInvariant();
            g.DrawString($"{i + 1}  {caption}", font, brush, new RectangleF(cell.X, py + th + font.Height * 0.35f, cell.Width, captionH), fmt);
            if (review)
            {
                using var pen = new Pen(Color.FromArgb(200, 0, 140, 255), 1f);
                g.DrawRectangle(pen, px + (float)((m.Box[0] - tx0) * k), py + (float)((m.Box[1] - ty0) * k), (float)(bw * k), (float)(bh * k));
                g.DrawString($"{bw:0.0}x{bh:0.0}  x{k:0.0}  {d.Screen}", font, brush, new RectangleF(cell.X, cell.Y + 4, cell.Width, captionH), fmt);
            }
            log.AppendLine($"{i + 1,2} {d.Name,-26} screen {d.Screen}  tile {tx0:0.0},{ty0:0.0} {tx1 - tx0:0.0}x{ty1 - ty0:0.0}  x{k:0.00}  ink {bw * k:0}x{bh * k:0} px on the sheet");
        }
        string outPath = Path.Combine(Spec.Work, review ? "review" : "attach", $"{name}{(review ? "_review" : "")}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        bmp.Save(outPath, ImageFormat.Png);
        Console.Write(log);
        Console.WriteLine($"{outPath}  {s.W}x{s.H}");
        return 0;
    }

    /// <summary>A row of finished sprites on white, enlarged, with captions: a style reference for ChatGPT.</summary>
    public static int Strip(string[] images, string[] captions, double scale, string outPath)
    {
        var bitmaps = images.Select(p => new Bitmap(Path.IsPathRooted(p) ? p : Path.Combine(Spec.Repo, p))).ToList();
        int gap = 90, top = 70;
        int w = gap + bitmaps.Sum(b => (int)(b.Width * scale) + gap);
        int hMax = bitmaps.Max(b => (int)(b.Height * scale));
        int h = top + hMax + 110;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", 22f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(255, 150, 150, 150));
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        int x = gap;
        for (int i = 0; i < bitmaps.Count; i++)
        {
            int bw = (int)(bitmaps[i].Width * scale), bh = (int)(bitmaps[i].Height * scale);
            g.DrawImage(bitmaps[i], new Rectangle(x, top + (hMax - bh) / 2, bw, bh));
            if (captions != null && i < captions.Length) g.DrawString(captions[i], font, brush, new RectangleF(x - gap / 2f, top + hMax + 30, bw + gap, 40), fmt);
            x += bw + gap;
        }
        string full = Path.IsPathRooted(outPath) ? outPath : Path.Combine(Spec.Repo, outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        bmp.Save(full, ImageFormat.Png);
        foreach (Bitmap b in bitmaps) b.Dispose();
        Console.WriteLine($"{full}  {w}x{h}");
        return 0;
    }
}
