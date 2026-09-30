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
    public double? Angle;            // the mockup draws it tilted by about this many degrees (clockwise); the sprite is upright
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
                Angle = n["angle"] != null ? (double)n["angle"] : (double?)null,
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
        // Only what lies within 3 px of solid ink counts as the icon's edge; farther away, faint
        // leftovers of the ground (a button's shading the fit did not flatten) stay white.
        var nearInk = new bool[Region.Count];
        for (int yy = 0; yy < Region.H; yy++)
            for (int xx = 0; xx < Region.W; xx++)
            {
                if (Cover[yy * Region.W + xx] < 0.4f) continue;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int ax = xx + dx, ay = yy + dy;
                        if (ax >= 0 && ay >= 0 && ax < Region.W && ay < Region.H) nearInk[ay * Region.W + ax] = true;
                    }
            }
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
                    if (c < 0.03f || !nearInk[ri]) c = 0f;
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
                case "split": return Splitter.Run(Get("sheet"), Get("in"), Get("attempt", "a1"));
                case "make": return Maker.Run(Get("name"), Get("src"), opt);
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

    /// <summary>A path given on the command line: absolute, or relative to the repository root.</summary>
    public static string Full(string path) => Path.IsPathRooted(path) ? path : Path.Combine(Spec.Repo, path);

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
            double k = colour
                ? Math.Min(s.MaxScale, Math.Min(availW / (tx1 - tx0), availH / (ty1 - ty0)))
                : Math.Min(s.MaxScale, Math.Min(availW / bw, availH / bh));
            float tw = (float)((tx1 - tx0) * k), th = (float)((ty1 - ty0) * k);
            // Centre the ink box (not the tile) in the cell, above the caption.
            float px = cell.X + cell.Width / 2f - (float)(((m.Box[0] + m.Box[2]) / 2 - tx0) * k);
            float py = cell.Y + (cell.Height - captionH) / 2f - (float)(((m.Box[1] + m.Box[3]) / 2 - ty0) * k);
            using (Bitmap tile = m.Tile(tx0, ty0, tx1 - tx0, ty1 - ty0, colour).ToBitmap())
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(tile, new Rectangle((int)Math.Round(px), (int)Math.Round(py), (int)Math.Round(tw), (int)Math.Round(th)), 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel, attrs);
            }
            string caption = s.Captions[i] ?? d.Name.Replace("Icon_", "").ToLowerInvariant();
            float inkBottom = py + (float)((m.Box[3] - ty0) * k);
            float captionY = Math.Min(cell.Bottom - captionH, inkBottom + Math.Max(font.Height * 0.9f, (float)(pad * k * 0.6)));
            g.DrawString($"{i + 1}  {caption}", font, brush, new RectangleF(cell.X, captionY, cell.Width, captionH), fmt);
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

/// <summary>
/// A redrawn icon: the matte of one cell of ChatGPT's sheet, kept as a signed distance field so it can be
/// rendered crisp at any size, position and weight.
/// </summary>
internal sealed class Shape
{
    public const int Up = 3;          // distance-field samples per cell pixel
    public int W, H;                  // cell size in px
    public float[] Alpha;             // the matte (W x H)
    public float[] Sd;                // signed distance in cell px, negative inside; (W*Up) x (H*Up)
    public (float r, float g, float b) Ink, Ground;
    public double[] Box;              // left, top, right, bottom of the half-coverage box, cell px

    /// <summary>Matte of a flat single-ink drawing on a flat ground (ChatGPT's redraw on white).</summary>
    public static Shape FromInkCell(Img cell)
    {
        int w = cell.W, h = cell.H, n = cell.Count;
        var fr = new List<float>(); var fg = new List<float>(); var fb = new List<float>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x < 3 || y < 3 || x >= w - 3 || y >= h - 3) { int i = y * w + x; fr.Add(cell.R[i]); fg.Add(cell.G[i]); fb.Add(cell.B[i]); }
        fr.Sort(); fg.Sort(); fb.Sort();
        var ground = (fr[fr.Count / 2], fg[fg.Count / 2], fb[fb.Count / 2]);
        var dist = new float[n];
        float max = 0f;
        for (int i = 0; i < n; i++)
        {
            float dr = cell.R[i] - ground.Item1, dg = cell.G[i] - ground.Item2, db = cell.B[i] - ground.Item3;
            dist[i] = MathF.Sqrt(dr * dr + dg * dg + db * db);
            if (dist[i] > max) max = dist[i];
        }
        // Ink: the median colour of everything at least 70 % as far from the ground as the farthest pixel.
        var ir = new List<float>(); var ig = new List<float>(); var ib = new List<float>();
        for (int i = 0; i < n; i++) if (dist[i] >= 0.7f * max) { ir.Add(cell.R[i]); ig.Add(cell.G[i]); ib.Add(cell.B[i]); }
        ir.Sort(); ig.Sort(); ib.Sort();
        var ink = (ir[ir.Count / 2], ig[ig.Count / 2], ib[ib.Count / 2]);
        var a = new float[n];
        float kr = ground.Item1 - ink.Item1, kg = ground.Item2 - ink.Item2, kb = ground.Item3 - ink.Item3;
        float den = kr * kr + kg * kg + kb * kb;
        for (int i = 0; i < n; i++)
        {
            float num = (ground.Item1 - cell.R[i]) * kr + (ground.Item2 - cell.G[i]) * kg + (ground.Item3 - cell.B[i]) * kb;
            float c = den < 1e-6f ? 0f : num / den;
            a[i] = Math.Clamp((c - 0.04f) / 0.92f, 0f, 1f);
        }
        return FromAlpha(a, w, h, ink, ground);
    }

    public static Shape FromAlpha(float[] alpha, int w, int h, (float, float, float) ink, (float, float, float) ground)
    {
        var s = new Shape { W = w, H = h, Alpha = alpha, Ink = ink, Ground = ground };
        s.Sd = BuildSd(alpha, w, h);
        s.Box = Bounds.HalfBox(alpha, w, h);
        return s;
    }

    private static float[] BuildSd(float[] alpha, int w, int h)
    {
        int uw = w * Up, uh = h * Up;
        var inside = new bool[uw * uh];
        for (int y = 0; y < uh; y++)
        {
            double sy = (y + 0.5) / Up - 0.5;
            int y0 = (int)Math.Floor(sy); double fy = sy - y0;
            int ya = Math.Clamp(y0, 0, h - 1), yb = Math.Clamp(y0 + 1, 0, h - 1);
            for (int x = 0; x < uw; x++)
            {
                double sx = (x + 0.5) / Up - 0.5;
                int x0 = (int)Math.Floor(sx); double fx = sx - x0;
                int xa = Math.Clamp(x0, 0, w - 1), xb = Math.Clamp(x0 + 1, 0, w - 1);
                double v = (alpha[ya * w + xa] * (1 - fx) + alpha[ya * w + xb] * fx) * (1 - fy) + (alpha[yb * w + xa] * (1 - fx) + alpha[yb * w + xb] * fx) * fy;
                inside[y * uw + x] = v >= 0.5;
            }
        }
        float[] toInside = Edt(inside, uw, uh, true), toOutside = Edt(inside, uw, uh, false);
        var sd = new float[uw * uh];
        for (int i = 0; i < sd.Length; i++)
            sd[i] = (inside[i] ? -(toOutside[i] - 0.5f) : toInside[i] - 0.5f) / Up;
        return sd;
    }

    /// <summary>Euclidean distance (in samples) from every sample to the nearest one whose flag equals <paramref name="target"/>.</summary>
    private static float[] Edt(bool[] flag, int w, int h, bool target)
    {
        const float Inf = 1e12f;
        var f = new float[w * h];
        for (int i = 0; i < f.Length; i++) f[i] = flag[i] == target ? 0f : Inf;
        int m = Math.Max(w, h);
        var d = new float[m]; var g = new float[m]; var v = new int[m]; var z = new float[m + 1];
        void Pass(int len)
        {
            int k = 0; v[0] = 0; z[0] = -Inf; z[1] = Inf;
            for (int q = 1; q < len; q++)
            {
                float s;
                while (true)
                {
                    int p = v[k];
                    s = ((g[q] + (float)q * q) - (g[p] + (float)p * p)) / (2f * q - 2f * p);
                    if (s <= z[k] && k > 0) k--; else break;
                }
                k++; v[k] = q; z[k] = s; z[k + 1] = Inf;
            }
            k = 0;
            for (int q = 0; q < len; q++)
            {
                while (z[k + 1] < q) k++;
                int p = v[k];
                d[q] = (float)(q - p) * (q - p) + g[p];
            }
        }
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++) g[y] = f[y * w + x];
            Pass(h);
            for (int y = 0; y < h; y++) f[y * w + x] = d[y];
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) g[x] = f[y * w + x];
            Pass(w);
            for (int x = 0; x < w; x++) f[y * w + x] = MathF.Sqrt(d[x]);
        }
        return f;
    }

    /// <summary>Signed distance (cell px) at a cell position; beyond the cell it keeps growing.</summary>
    public float Distance(double cx, double cy)
    {
        int uw = W * Up, uh = H * Up;
        double ux = cx * Up - 0.5, uy = cy * Up - 0.5;
        double qx = Math.Clamp(ux, 0, uw - 1), qy = Math.Clamp(uy, 0, uh - 1);
        int x0 = (int)Math.Floor(qx), y0 = (int)Math.Floor(qy);
        int x1 = Math.Min(x0 + 1, uw - 1), y1 = Math.Min(y0 + 1, uh - 1);
        double fx = qx - x0, fy = qy - y0;
        double v = (Sd[y0 * uw + x0] * (1 - fx) + Sd[y0 * uw + x1] * fx) * (1 - fy) + (Sd[y1 * uw + x0] * (1 - fx) + Sd[y1 * uw + x1] * fx) * fy;
        double ox = ux - qx, oy = uy - qy;
        if (ox != 0 || oy != 0) v += Math.Sqrt(ox * ox + oy * oy) / Up;
        return (float)v;
    }

    /// <summary>Makes the shape mirror-symmetric about the vertical line through the middle of its box.</summary>
    public void Symmetrise()
    {
        int uw = W * Up, uh = H * Up;
        double axis = (Box[0] + Box[2]) / 2 * Up - 0.5;   // in sample coordinates
        var m = new float[Sd.Length];
        for (int y = 0; y < uh; y++)
            for (int x = 0; x < uw; x++)
            {
                double mx = 2 * axis - x;
                float other = Distance((mx + 0.5) / Up, (y + 0.5) / Up);
                m[y * uw + x] = 0.5f * (Sd[y * uw + x] + other);
            }
        Sd = m;
    }
}

/// <summary>Where a shape sits on a mockup: mock = (Tx + Sx * cellX, Ty + Sy * cellY), plus weight and softness.</summary>
internal struct Pose
{
    public double Tx, Ty, Sx, Sy;
    public double Rot;      // radians, clockwise on screen: mock = T + R(Rot) * (Sx * cellX, Sy * cellY)
    public double Grow;     // how far the outline is pushed out, mockup px (negative: pulled in)
    public double Blur;     // softness of the mockup rendering, mockup px (only used when comparing)
}

internal static class Fit
{
    private static readonly float[] SrgbLut = BuildSrgb();
    private static float[] BuildSrgb() { var l = new float[4097]; for (int i = 0; i <= 4096; i++) l[i] = Img.ToSrgb(i / 4096f); return l; }
    private static float Srgb(float lin) => SrgbLut[(int)(Math.Clamp(lin, 0f, 1f) * 4096f)];

    /// <summary>Coverage of the shape on a pixel grid: origin (ox, oy) and scale px-per-mockup-px, size w x h.</summary>
    public static float[] Render(Shape s, Pose p, double ox, double oy, double scale, int w, int h, int sub = 3)
    {
        var cov = new float[w * h];
        double unit = Math.Sqrt(p.Sx * p.Sy);            // mockup px per cell px
        double ramp = scale * sub;                        // sub-samples per mockup px -> ramp of one sub-sample
        double cos = Math.Cos(p.Rot), sin = Math.Sin(p.Rot);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double acc = 0;
                for (int j = 0; j < sub; j++)
                {
                    double dy = oy + (y + (j + 0.5) / sub) / scale - p.Ty;
                    for (int i = 0; i < sub; i++)
                    {
                        double dx = ox + (x + (i + 0.5) / sub) / scale - p.Tx;
                        double cx = (cos * dx + sin * dy) / p.Sx;
                        double cy = (-sin * dx + cos * dy) / p.Sy;
                        double d = s.Distance(cx, cy) * unit - p.Grow;      // mockup px
                        acc += Math.Clamp(0.5 - d * ramp, 0.0, 1.0);
                    }
                }
                cov[y * w + x] = (float)(acc / (sub * sub));
            }
        return cov;
    }

    public static float[] Gauss(float[] a, int w, int h, double sigma)
    {
        if (sigma < 0.12) return a;
        int r = (int)Math.Ceiling(sigma * 3);
        var k = new float[2 * r + 1]; float sum = 0;
        for (int i = -r; i <= r; i++) { k[i + r] = (float)Math.Exp(-0.5 * i * i / (sigma * sigma)); sum += k[i + r]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;
        var t = new float[a.Length]; var o = new float[a.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0;
                for (int i = -r; i <= r; i++) v += a[y * w + Math.Clamp(x + i, 0, w - 1)] * k[i + r];
                t[y * w + x] = v;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0;
                for (int i = -r; i <= r; i++) v += t[Math.Clamp(y + i, 0, h - 1) * w + x] * k[i + r];
                o[y * w + x] = v;
            }
        return o;
    }

    /// <summary>Mean squared sRGB difference (0..255 scale) between the mockup crop and the shape drawn on its ground.</summary>
    public static double Error(Shape s, Pose p, MockIcon m, (float r, float g, float b) ink)
    {
        int w = m.Region.W, h = m.Region.H;
        float[] cov = Gauss(Render(s, p, m.X0, m.Y0, 1.0, w, h), w, h, p.Blur);
        double e = 0; int n = 0;
        for (int i = 0; i < cov.Length; i++)
        {
            if (!m.Inside[i]) continue;
            float c = cov[i];
            float r = Srgb(m.Ground.R[i] + (ink.r - m.Ground.R[i]) * c) - Srgb(m.Region.R[i]);
            float g = Srgb(m.Ground.G[i] + (ink.g - m.Ground.G[i]) * c) - Srgb(m.Region.G[i]);
            float b = Srgb(m.Ground.B[i] + (ink.b - m.Ground.B[i]) * c) - Srgb(m.Region.B[i]);
            e += r * r + g * g + b * b; n++;
        }
        return e / Math.Max(1, n * 3) * 255.0 * 255.0;
    }

    /// <summary>Same comparison for a coverage-only match (multi-coloured icons): silhouette against silhouette.</summary>
    public static double CoverError(Shape s, Pose p, MockIcon m)
    {
        int w = m.Region.W, h = m.Region.H;
        float[] cov = Gauss(Render(s, p, m.X0, m.Y0, 1.0, w, h), w, h, p.Blur);
        double e = 0; int n = 0;
        for (int i = 0; i < cov.Length; i++)
        {
            if (!m.Inside[i]) continue;
            float d = cov[i] - m.Cover[i];
            e += d * d; n++;
        }
        return e / Math.Max(1, n) * 255.0 * 255.0;
    }

    /// <summary>
    /// Finds the pose that lands the shape on the mockup icon. level: 3 = position and one scale;
    /// 4 = plus its own width/height ratio; 6 = plus weight and softness. With rotate, the tilt is solved too.
    /// </summary>
    public static Pose Solve(Shape s, MockIcon m, (float r, float g, float b) ink, bool silhouette, int level, Pose? start = null, bool rotate = false, double angle = 0)
    {
        double bw = s.Box[2] - s.Box[0], bh = s.Box[3] - s.Box[1];
        double mw = m.Box[2] - m.Box[0], mh = m.Box[3] - m.Box[1];
        double ccx = (s.Box[0] + s.Box[2]) / 2, ccy = (s.Box[1] + s.Box[3]) / 2;
        // Parameters: centre x, centre y (mockup px), ln scale, rotation, ln aspect, grow, blur.
        double[] x0;
        if (start == null)
        {
            // A tilted drawing has a larger box than the upright shape: start from the areas instead.
            double sc = Math.Sqrt(mw / bw * mh / bh);
            if (rotate && Math.Abs(angle) > 1)
            {
                double am = 0, ac = 0;
                for (int i = 0; i < m.Cover.Length; i++) am += m.Cover[i];
                for (int i = 0; i < s.Alpha.Length; i++) ac += s.Alpha[i];
                if (ac > 0 && am > 0) sc = Math.Sqrt(am / ac);
            }
            x0 = new[] { (m.Box[0] + m.Box[2]) / 2, (m.Box[1] + m.Box[3]) / 2, Math.Log(sc), angle * Math.PI / 180, 0.0, 0.0, 0.6 };
        }
        else
        {
            Pose p0 = start.Value;
            double cos0 = Math.Cos(p0.Rot), sin0 = Math.Sin(p0.Rot);
            double ux = p0.Sx * ccx, uy = p0.Sy * ccy;
            x0 = new[] { p0.Tx + cos0 * ux - sin0 * uy, p0.Ty + sin0 * ux + cos0 * uy, Math.Log(Math.Sqrt(p0.Sx * p0.Sy)), p0.Rot, Math.Log(p0.Sx / p0.Sy), p0.Grow, p0.Blur };
        }
        double[] step = { 0.6, 0.6, 0.02, 0.03, 0.02, 0.15, 0.2 };
        var free = new List<int> { 0, 1, 2 };
        if (rotate) free.Add(3);
        if (level >= 4) free.Add(4);
        if (level >= 6) { free.Add(5); free.Add(6); }
        Pose Make(double[] v)
        {
            double sc = Math.Exp(v[2]), asp = Math.Exp(Math.Clamp(v[4], -0.12, 0.12));
            var q = new Pose { Sx = sc * Math.Sqrt(asp), Sy = sc / Math.Sqrt(asp), Rot = v[3] };
            double cos = Math.Cos(q.Rot), sin = Math.Sin(q.Rot);
            double ux = q.Sx * ccx, uy = q.Sy * ccy;
            q.Tx = v[0] - (cos * ux - sin * uy); q.Ty = v[1] - (sin * ux + cos * uy);
            q.Grow = Math.Clamp(v[5], -0.8, 0.8);
            q.Blur = Math.Clamp(v[6], 0.0, 1.6);
            return q;
        }
        double Cost(double[] v) => silhouette ? CoverError(s, Make(v), m) : Error(s, Make(v), m, ink);
        double[] best = NelderMead(Cost, x0, step, free, 320);
        best = NelderMead(Cost, best, step.Select(t => t * 0.35).ToArray(), free, 240);
        return Make(best);
    }

    private static double[] NelderMead(Func<double[], double> f, double[] x0, double[] step, List<int> free, int maxIter)
    {
        int n = free.Count, full = x0.Length;
        var pts = new double[n + 1][]; var val = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            pts[i] = (double[])x0.Clone();
            if (i > 0) pts[i][free[i - 1]] += step[free[i - 1]];
            val[i] = f(pts[i]);
        }
        for (int it = 0; it < maxIter; it++)
        {
            Array.Sort(val, pts);
            if (Math.Abs(val[n] - val[0]) < 1e-5 * (Math.Abs(val[0]) + 1e-9)) break;
            var c = new double[full];
            for (int i = 0; i < n; i++) for (int k = 0; k < full; k++) c[k] += pts[i][k] / n;
            double[] At(double t) { var r = new double[full]; for (int k = 0; k < full; k++) r[k] = c[k] + t * (pts[n][k] - c[k]); return r; }
            double[] xr = At(-1); double fr = f(xr);
            if (fr < val[0])
            {
                double[] xe = At(-2); double fe = f(xe);
                if (fe < fr) { pts[n] = xe; val[n] = fe; } else { pts[n] = xr; val[n] = fr; }
            }
            else if (fr < val[n - 1]) { pts[n] = xr; val[n] = fr; }
            else
            {
                double[] xc = At(fr < val[n] ? -0.5 : 0.5); double fc = f(xc);
                if (fc < Math.Min(fr, val[n])) { pts[n] = xc; val[n] = fc; }
                else
                {
                    for (int i = 1; i <= n; i++)
                    {
                        for (int k = 0; k < full; k++) pts[i][k] = pts[0][k] + 0.5 * (pts[i][k] - pts[0][k]);
                        val[i] = f(pts[i]);
                    }
                }
            }
        }
        Array.Sort(val, pts);
        return pts[0];
    }
}

/// <summary>Cuts ChatGPT's redrawn sheet into one cell image per icon.</summary>
internal static class Splitter
{
    public static int Run(string sheetName, string input, string attempt)
    {
        Sheets.Sheet s = Sheets.Load(sheetName);
        Img img = Img.Load(Tool.Full(input));
        int w = img.W, h = img.H, n = img.Count;
        // Ground: the median of the image frame.
        var fr = new List<float>(); var fg = new List<float>(); var fb = new List<float>();
        for (int y = 0; y < h; y += 2)
            for (int x = 0; x < w; x += 2)
                if (x < 6 || y < 6 || x >= w - 6 || y >= h - 6) { int i = y * w + x; fr.Add(img.R[i]); fg.Add(img.G[i]); fb.Add(img.B[i]); }
        fr.Sort(); fg.Sort(); fb.Sort();
        float gr = fr[fr.Count / 2], gg = fg[fg.Count / 2], gb = fb[fb.Count / 2];
        var ink = new bool[n];
        for (int i = 0; i < n; i++)
        {
            float dr = img.R[i] - gr, dg = img.G[i] - gg, db = img.B[i] - gb;
            ink[i] = MathF.Sqrt(dr * dr + dg * dg + db * db) > 0.10f || img.A[i] < 0.5f != (img.A[0] < 0.5f);
        }
        Console.WriteLine($"{Path.GetFileName(input)}  {w}x{h}  ground {Img.Hex(gr, gg, gb)}");
        string outDir = Path.Combine(Spec.Work, "chatgpt", "cells");
        Directory.CreateDirectory(outDir);
        for (int c = 0; c < s.Cells.Count; c++)
        {
            RectangleF cell = Sheets.Cell(s, c, w, h);
            int cx0 = (int)cell.X + 2, cy0 = (int)cell.Y + 2, cx1 = (int)cell.Right - 2, cy1 = (int)cell.Bottom - 2;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = cy0; y < cy1; y++)
                for (int x = cx0; x < cx1; x++)
                    if (ink[y * w + x]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) { Console.WriteLine($"{c + 1,2} {s.Cells[c],-24} EMPTY CELL"); continue; }
            bool touches = x0 <= cx0 || y0 <= cy0 || x1 >= cx1 - 1 || y1 >= cy1 - 1;
            int m = 14;
            int ox = Math.Max(0, x0 - m), oy = Math.Max(0, y0 - m), ow = Math.Min(w, x1 + 1 + m) - ox, oh = Math.Min(h, y1 + 1 + m) - oy;
            Img cut = img.Crop(ox, oy, ow, oh);
            string file = Path.Combine(outDir, $"{s.Cells[c]}_{attempt}.png");
            cut.Save(file);
            Console.WriteLine($"{c + 1,2} {s.Cells[c],-24} ink {x0},{y0} {x1 - x0 + 1}x{y1 - y0 + 1}{(touches ? "  TOUCHES THE CELL EDGE" : "")}  -> {Path.GetFileName(file)}");
        }
        return 0;
    }
}

/// <summary>Fits a redrawn icon to its mockup crop, exports the sprite and writes the check images.</summary>
internal static class Maker
{
    public const double Scale2x = 2.3;
    public const int Margin = 2;

    public sealed class Result
    {
        public string Name; public Pose Pose; public double Rms, RmsBest, Iou, Weight, Aspect, SourceScale, EdgeMean, EdgeMax;
        public int W, H; public double[] MockRect; public double[] InkRect; public string File;
    }

    public static int Run(string name, string src, Dictionary<string, string> opt)
    {
        IconDef d = Spec.Get(name);
        MockIcon m = MockIcon.Read(d);
        Img cell = Img.Load(Tool.Full(src));
        Shape s = Shape.FromInkCell(cell);
        if (opt.ContainsKey("symmetric") || d.Symmetric) s.Symmetrise();
        (float r, float g, float b) mockInk = m.Ink;

        // 1. Where the redrawn shape sits on the mockup, first rigidly, then with its own aspect, weight and softness.
        bool rotate = d.Angle != null;
        Pose p3 = Fit.Solve(s, m, mockInk, false, 3, null, rotate, d.Angle ?? 0);
        Pose p6 = Fit.Solve(s, m, mockInk, false, 6, p3, rotate);
        double rmsBest = Math.Sqrt(Fit.Error(s, p6, m, mockInk));
        Pose asDrawn = p6; asDrawn.Grow = 0;
        Console.WriteLine($"   rms with the outline as drawn (grow 0): {Math.Sqrt(Fit.Error(s, asDrawn, m, mockInk)):0.00}; with the fitted grow {p6.Grow:+0.00;-0.00}: {rmsBest:0.00}; rigid fit only: {Math.Sqrt(Fit.Error(s, p3, m, mockInk)):0.00}");

        // 2. What is baked into the sprite: position, size and aspect from the fit; the weight only as far as asked.
        double grow = opt.TryGetValue("grow", out string gs) ? double.Parse(gs) : p6.Grow;
        bool keepAspect = opt.ContainsKey("uniform");
        Pose p = p6; p.Grow = grow;
        if (keepAspect) { p = Fit.Solve(s, m, mockInk, false, 3, p3, rotate); p.Grow = grow; p.Blur = p6.Blur; }
        double rms = Math.Sqrt(Fit.Error(s, p, m, mockInk));

        // 3. Render at @2x on a canvas that keeps Margin px clear around the ink. A drawing that is tilted on
        //    the mockup is exported upright, about the same centre.
        var crisp = p; crisp.Blur = 0;
        if (rotate)
        {
            double bcx = (s.Box[0] + s.Box[2]) / 2 * p.Sx, bcy = (s.Box[1] + s.Box[3]) / 2 * p.Sy;
            double mcx = p.Tx + Math.Cos(p.Rot) * bcx - Math.Sin(p.Rot) * bcy, mcy = p.Ty + Math.Sin(p.Rot) * bcx + Math.Cos(p.Rot) * bcy;
            crisp.Rot = 0; crisp.Tx = mcx - bcx; crisp.Ty = mcy - bcy;
        }
        // --ink-height N: the sprite's ink is N px tall whatever size the mockup draws it (a set of symbols at one height).
        if (opt.TryGetValue("ink-height", out string ihs))
        {
            double target = double.Parse(ihs) / Scale2x;
            double padH = 6;
            int th0 = (int)Math.Ceiling((m.Box[3] - m.Box[1] + 2 * padH) * 4), tw0 = (int)Math.Ceiling((m.Box[2] - m.Box[0] + 2 * padH) * 4);
            float[] probe = Fit.Render(s, crisp, m.Box[0] - padH, m.Box[1] - padH, 4.0, tw0, th0, 2);
            double[] pb = Bounds.HalfBox(probe, tw0, th0, 0, 0, 0.02f);
            double f = target / ((pb[3] - pb[1]) / 4.0);
            double bcx = (s.Box[0] + s.Box[2]) / 2, bcy = (s.Box[1] + s.Box[3]) / 2;
            double mcx = crisp.Tx + bcx * crisp.Sx, mcy = crisp.Ty + bcy * crisp.Sy;
            crisp.Sx *= f; crisp.Sy *= f; crisp.Grow *= f;
            crisp.Tx = mcx - bcx * crisp.Sx; crisp.Ty = mcy - bcy * crisp.Sy;
            Console.WriteLine($"   ink height set to {ihs} px: x{f:0.000} of the mockup size");
        }
        double pad = opt.ContainsKey("ink-height") ? Math.Max(m.Box[2] - m.Box[0], m.Box[3] - m.Box[1]) : 4;   // mockup px of slack around the box for the first pass
        double bx0 = m.Box[0] - pad, by0 = m.Box[1] - pad, bx1 = m.Box[2] + pad, by1 = m.Box[3] + pad;
        int fw = (int)Math.Ceiling((bx1 - bx0) * Scale2x), fh = (int)Math.Ceiling((by1 - by0) * Scale2x);
        float[] first = Fit.Render(s, crisp, bx0, by0, Scale2x, fw, fh, 4);
        double[] ib = Bounds.HalfBox(first, fw, fh, 0, 0, 0.02f);   // where the ink becomes visible
        double inkL = bx0 + ib[0] / Scale2x, inkT = by0 + ib[1] / Scale2x, inkR = bx0 + ib[2] / Scale2x, inkB = by0 + ib[3] / Scale2x;
        int w = Tool.Even((inkR - inkL) * Scale2x) + 2 * Margin, h = Tool.Even((inkB - inkT) * Scale2x) + 2 * Margin;
        double ox = (inkL + inkR) / 2 - w / (2 * Scale2x), oy = (inkT + inkB) / 2 - h / (2 * Scale2x);
        float[] alpha = Fit.Render(s, crisp, ox, oy, Scale2x, w, h, 4);
        var inkCol = Img.Hex(d.Ink ?? Spec.FamilyInk);
        var sprite = new Img(w, h, 0f);
        for (int i = 0; i < alpha.Length; i++)
        {
            float a = alpha[i] < 0.004f ? 0f : alpha[i] > 0.996f ? 1f : alpha[i];
            sprite.R[i] = inkCol.r; sprite.G[i] = inkCol.g; sprite.B[i] = inkCol.b; sprite.A[i] = a;
        }
        string attempt = opt.TryGetValue("attempt", out string at) ? at : "a1";
        string outFile = Path.Combine(Spec.Work, "fitted", $"{name}_{attempt}.png");
        sprite.Save(outFile);

        // 4. Numbers.
        int mw = m.Region.W, mh = m.Region.H;
        var onMock = p; onMock.Blur = 0;
        float[] cov = Fit.Render(s, onMock, m.X0, m.Y0, 1.0, mw, mh, 4);
        float[] covSoft = Fit.Gauss(cov, mw, mh, p.Blur);
        double inter = 0, union = 0, sumC = 0, sumM = 0;
        for (int i = 0; i < cov.Length; i++)
        {
            if (!m.Inside[i]) continue;
            bool a = covSoft[i] >= 0.5f, b = m.Cover[i] >= 0.5f;
            if (a && b) inter++; if (a || b) union++;
            sumC += covSoft[i]; sumM += m.Cover[i];
        }
        var res = new Result
        {
            Name = name, Pose = p, Rms = rms, RmsBest = rmsBest, Iou = union > 0 ? inter / union : 0, Weight = sumM > 0 ? sumC / sumM : 0,
            Aspect = p.Sx / p.Sy, SourceScale = Scale2x * Math.Sqrt(crisp.Sx * crisp.Sy), W = w, H = h, File = outFile,
            MockRect = new[] { ox, oy, w / Scale2x, h / Scale2x }, InkRect = new[] { inkL, inkT, inkR - inkL, inkB - inkT },
        };
        (res.EdgeMean, res.EdgeMax) = EdgeDeviation(covSoft, m.Cover, m.Inside, mw, mh);
        Console.WriteLine($"{name}  {w}x{h}  ink on {d.Screen}: {inkL:0.00},{inkT:0.00} {inkR - inkL:0.00}x{inkB - inkT:0.00}  sprite rect {ox:0.00},{oy:0.00} {w / Scale2x:0.00}x{h / Scale2x:0.00}");
        Console.WriteLine($"   rms {rms:0.00} (best {rmsBest:0.00})  iou {res.Iou:0.000}  weight {res.Weight:0.000}  edge mean {res.EdgeMean:0.00} max {res.EdgeMax:0.00} px" +
                          $"  aspect {res.Aspect:0.000}  fit grow {p6.Grow:+0.00;-0.00} baked {p.Grow:+0.00;-0.00}  blur {p6.Blur:0.00}  source x{res.SourceScale:0.00}" + (rotate ? $"  tilt on the mockup {p.Rot * 180 / Math.PI:+0.0;-0.0} deg" : ""));
        Check.Write(d, m, s, p, sprite, res, attempt);
        File.WriteAllText(Path.ChangeExtension(outFile, ".txt"),
            FormattableString.Invariant($"{name} {attempt}\nsize {w}x{h}\nscreen {d.Screen}\nink_rect {inkL:0.00},{inkT:0.00},{inkR - inkL:0.00},{inkB - inkT:0.00}\nsprite_rect {ox:0.00},{oy:0.00},{w / Scale2x:0.00},{h / Scale2x:0.00}\nrms {rms:0.00}\niou {res.Iou:0.000}\nweight {res.Weight:0.000}\nedge_mean {res.EdgeMean:0.00}\nedge_max {res.EdgeMax:0.00}\naspect {res.Aspect:0.000}\ngrow_fit {p6.Grow:0.00}\ngrow_baked {p.Grow:0.00}\nblur {p6.Blur:0.00}\nsource_scale {res.SourceScale:0.00}\n"));
        return 0;
    }

    /// <summary>
    /// How far the redrawn outline runs from the mockup outline, in mockup px: for every point of the
    /// half-coverage contour of one, the distance to the contour of the other (both directions), from the
    /// sub-pixel coverage values. Returns the mean and the 98th percentile.
    /// </summary>
    public static (double mean, double max) EdgeDeviation(float[] a, float[] b, bool[] inside, int w, int h)
    {
        List<(double x, double y)> ca = Contour(a, inside, w, h), cb = Contour(b, inside, w, h);
        if (ca.Count == 0 || cb.Count == 0) return (99, 99);
        var all = new List<double>();
        void OneWay(List<(double x, double y)> from, List<(double x, double y)> to)
        {
            foreach (var q in from)
            {
                double best = double.MaxValue;
                foreach (var t in to) { double dx = q.x - t.x, dy = q.y - t.y, dd = dx * dx + dy * dy; if (dd < best) best = dd; }
                all.Add(Math.Sqrt(best));
            }
        }
        OneWay(ca, cb); OneWay(cb, ca);
        all.Sort();
        return (all.Average(), all[(int)(all.Count * 0.98)]);
    }

    // Points where the coverage crosses one half between neighbouring pixel centres.
    private static List<(double x, double y)> Contour(float[] c, bool[] inside, int w, int h)
    {
        var pts = new List<(double, double)>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                if (x + 1 < w && inside[i + 1] && (c[i] >= 0.5f) != (c[i + 1] >= 0.5f))
                    pts.Add((x + 0.5 + (0.5 - c[i]) / (c[i + 1] - c[i]), y + 0.5));
                if (y + 1 < h && inside[i + w] && (c[i] >= 0.5f) != (c[i + w] >= 0.5f))
                    pts.Add((x + 0.5, y + 0.5 + (0.5 - c[i]) / (c[i + w] - c[i])));
            }
        return pts;
    }
}

/// <summary>Side-by-side review images: the mockup crop, the sprite on the same ground, their difference, the alpha on black and white.</summary>
internal static class Check
{
    public static void Write(IconDef d, MockIcon m, Shape s, Pose p, Img sprite, Maker.Result res, string attempt)
    {
        int mw = m.Region.W, mh = m.Region.H;
        int z = Math.Max(3, Math.Min(10, 330 / Math.Max(mw, mh)));
        int pw = mw * z, ph = mh * z, gap = 10;
        (float r, float g, float b) ink = Img.Hex(d.Ink ?? Spec.FamilyInk);
        var crisp = p; crisp.Blur = 0;

        // Panel 1: the mockup crop, enlarged.
        Img a = m.Region.Resize(pw, ph);
        // Panel 2: the sprite as the game draws it: family ink over the same ground, sharp, at z times the mockup.
        float[] covZ = Fit.Render(s, crisp, m.X0, m.Y0, z, pw, ph, 2);
        Img groundZ = m.Ground.Resize(pw, ph);
        var b = new Img(pw, ph);
        for (int i = 0; i < b.Count; i++)
        {
            float c = covZ[i];
            b.R[i] = groundZ.R[i] + (ink.r - groundZ.R[i]) * c; b.G[i] = groundZ.G[i] + (ink.g - groundZ.G[i]) * c; b.B[i] = groundZ.B[i] + (ink.b - groundZ.B[i]) * c;
        }
        // Panel 3: the sprite at the mockup resolution and softness (what a capture at mockup size shows).
        float[] cov1 = Fit.Gauss(Fit.Render(s, crisp, m.X0, m.Y0, 1.0, mw, mh, 4), mw, mh, p.Blur);
        var c1 = new Img(mw, mh);
        for (int i = 0; i < c1.Count; i++)
        {
            float c = cov1[i];
            c1.R[i] = m.Ground.R[i] + (ink.r - m.Ground.R[i]) * c; c1.G[i] = m.Ground.G[i] + (ink.g - m.Ground.G[i]) * c; c1.B[i] = m.Ground.B[i] + (ink.b - m.Ground.B[i]) * c;
        }
        Img c3 = c1.Resize(pw, ph);
        // Panel 4: outline overlay: red = ink only on the mockup, blue = ink only on the sprite, dark = both.
        var o = new Img(mw, mh);
        for (int i = 0; i < o.Count; i++)
        {
            float mc = m.Cover[i], sc = cov1[i];
            float both = Math.Min(mc, sc), onlyM = Math.Max(0, mc - sc), onlyS = Math.Max(0, sc - mc);
            float rest = Math.Max(0f, 1f - both - onlyM - onlyS);
            o.R[i] = rest * 1f + onlyM * 1f + onlyS * 0.0f + both * 0.02f;
            o.G[i] = rest * 1f + onlyM * 0.0f + onlyS * 0.25f + both * 0.02f;
            o.B[i] = rest * 1f + onlyM * 0.0f + onlyS * 1f + both * 0.02f;
        }
        Img o4 = o.Resize(pw, ph, nearest: true);
        // Second row: the sprite itself. On black, as the white copy the game tints onto crimson, on white.
        double k = Math.Min((double)pw / sprite.W, (double)ph / sprite.H);
        int sw = Math.Max(1, (int)(sprite.W * k)), sh = Math.Max(1, (int)(sprite.H * k));
        Img onBlack = new Img(sprite.W, sprite.H); onBlack.Fill(0, 0, 0); onBlack.Over(sprite, 0, 0);
        var crimson = Img.Hex("#861018");
        Img onCrimson = new Img(sprite.W, sprite.H); onCrimson.Fill(crimson.r, crimson.g, crimson.b);
        var white = new Img(sprite.W, sprite.H, 0f);
        for (int i = 0; i < white.Count; i++) { white.R[i] = 1f; white.G[i] = 0.965f; white.B[i] = 0.913f; white.A[i] = sprite.A[i]; }
        onCrimson.Over(white, 0, 0);
        Img onWhite = new Img(sprite.W, sprite.H); onWhite.Fill(1, 1, 1); onWhite.Over(sprite, 0, 0);
        // Alpha stretched: shows halo or stray pixels that the eye misses (alpha > 0 drawn solid).
        var stray = new Img(sprite.W, sprite.H); stray.Fill(1, 1, 1);
        for (int i = 0; i < stray.Count; i++) if (sprite.A[i] > 0f) { float v = sprite.A[i] >= 0.999f ? 0f : 0.55f; stray.R[i] = v; stray.G[i] = v; stray.B[i] = v; }

        int textH = 66;
        var sheet = new Img(gap + 4 * (pw + gap), 2 * (ph + gap) + gap + textH);
        sheet.Fill(0.55f, 0.0f, 0.55f);
        int x = gap;
        foreach (Img panel in new[] { a, b, c3, o4 })
        {
            sheet.Paste(panel, x, gap); x += pw + gap;
        }
        x = gap;
        foreach (Img full in new[] { onBlack, onCrimson, onWhite, stray })
        {
            Img panel = full.Resize(sw, sh, nearest: k >= 1.5);
            var back = new Img(pw, ph); back.Fill(0.2f, 0.2f, 0.2f);
            back.Paste(panel, (pw - sw) / 2, (ph - sh) / 2);
            sheet.Paste(back, x, ph + 2 * gap); x += pw + gap;
        }
        using Bitmap bmp = sheet.ToBitmap();
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
            string line1 = $"{d.Name} {attempt}  {res.W}x{res.H}   top: mockup {d.Screen} x{z} | sprite on its ground, sharp | sprite at mockup size | red: mockup only, blue: sprite only";
            string line2 = "bottom: on black | white copy on crimson | on white | partial alpha in grey";
            string line3 = FormattableString.Invariant($"rms {res.Rms:0.00}  iou {res.Iou:0.000}  weight {res.Weight:0.000}  edge mean {res.EdgeMean:0.00} / p98 {res.EdgeMax:0.00} px  aspect {res.Aspect:0.000}  grow {p.Grow:+0.00;-0.00}  blur {p.Blur:0.00}  source x{res.SourceScale:0.00}");
            int ty = 2 * (ph + gap) + gap;
            g.DrawString(line1, font, Brushes.White, gap, ty);
            g.DrawString(line2, font, Brushes.White, gap, ty + 20);
            g.DrawString(line3, font, Brushes.White, gap, ty + 40);
        }
        string file = Path.Combine(Spec.Work, "check", $"{d.Name}_{attempt}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        bmp.Save(file, ImageFormat.Png);
    }
}
