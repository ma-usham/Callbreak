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
// Files beside it: icons.json (every icon and where it is drawn), sheets/S<n>.json (the contact
// sheets), accepted.json (the attempt each sprite is made from), uses.js -> uses.json (every place an
// icon is shown on a built screen), dl.sh (the shared download lock).
//
//   measure  [--only A,B]                          ink box, ink and ground colours of every icon on its mockup
//   sheet    --sheet S1 [--review]                 contact sheet of enlarged crops for ChatGPT (-> attach/)
//   strip    --images a.png,b.png [--captions ..] [--scale 2] --out f.png    sprites in a row on white
//   split    --sheet S1 --in chatgpt/S1_a1.png --attempt a1[,a1,a2,..]       cut ChatGPT's sheet into cells
//   make     --name Icon_Back --src cell.png [--attempt a1] [--grow px] [--max-grow px] [--hole-grow px]
//            [--scale f] [--ink-height px] [--uniform] [--symmetric]
//                                                  fit one cell to its mockup crop -> fitted/, check/
//   build    [--only A,B]                          make every icon of accepted.json again
//   export                                         copy the fitted sprites to Assets/_Project/Art/UI/Icons and
//                                                  ArtSource/Exports/Icons, write ArtSource/Kit/Icons.json
//   place    [--only A,B]                          the rectangle of each sprite on every screen that shows it
//   map                                            write ArtSource/Kit/Icons.map.csv (run uses.js first)
//   handoff                                        the family's rows for ArtSource/handoff.csv (-> handoff_rows.csv)
//   review   [--names A,B] [--out f.png]           mockup crop beside the sprite on the same ground, enlarged
//   audit                                          the exported files: size, margins, specks, fringe, copies
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
        // Read the file's own pixels: drawing it onto another bitmap would pass through premultiplied alpha
        // and lose the colour of nearly transparent pixels.
        using var src = new Bitmap(path);
        return FromBitmap(src);
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

    public Img Clone2()
    {
        var c = new Img(W, H, 0f);
        Array.Copy(R, c.R, R.Length); Array.Copy(G, c.G, G.Length); Array.Copy(B, c.B, B.Length); Array.Copy(A, c.A, A.Length);
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
    public bool KeepWhite;           // multi-coloured: white enclosed by the icon is part of it (a white mark on a badge)
    public string[] Palette;         // multi-coloured, flat: the colours it is drawn in (measured on the mockup)
    public string Tone;              // multi-coloured, shaded: "mock" moves the drawn colours onto the mockup's
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
                KeepWhite = n["keepWhite"] != null && (bool)n["keepWhite"],
                Palette = n["palette"]?.AsArray().Select(v => (string)v).ToArray(),
                Tone = (string)n["tone"],
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

    public static MockIcon Read(IconDef d)
    {
        MockIcon m = Read(d.Screen, d.Rect, d.Mode == "color", d.Disc);
        if (d.Mode == "color" && d.KeepWhite && m.Box != null) m.FillEnclosed();
        return m;
    }

    /// <summary>Whatever the silhouette encloses counts as the icon (a white mark that matches the ground).</summary>
    public void FillEnclosed()
    {
        int w = Region.W, h = Region.H;
        var outside = new bool[Region.Count];
        var stack = new Stack<int>();
        void Push(int x, int y) { int i = y * w + x; if (outside[i] || Cover[i] >= 0.5f) return; outside[i] = true; stack.Push(i); }
        for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
        for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
        while (stack.Count > 0)
        {
            int i = stack.Pop(); int x = i % w, y = i / w;
            if (x > 0) Push(x - 1, y); if (x < w - 1) Push(x + 1, y); if (y > 0) Push(x, y - 1); if (y < h - 1) Push(x, y + 1);
        }
        for (int i = 0; i < Cover.Length; i++) if (!outside[i] && Inside[i]) Cover[i] = 1f;
    }

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
            // How far each pixel has moved from the ground, relative to the nearest pixel that is fully the
            // icon (a pixel well off the ground whose neighbours are as far off as it is).
            Cover = new float[n];
            var dist = new float[n];
            for (int i = 0; i < n; i++)
            {
                float dr = r.R[i] - Ground.R[i], dg = r.G[i] - Ground.G[i], db = r.B[i] - Ground.B[i];
                dist[i] = MathF.Sqrt(dr * dr + dg * dg + db * db);
                // The mockups draw a light fringe around dark strokes; what is lighter than the ground is not the icon
                // (a white mark enclosed by the icon is added back by FillEnclosed).
                if (r.Lum(i) >= Ground.Lum(i) - 0.01f) dist[i] = 0f;
            }
            var nearest = new int[n];
            var queue = new Queue<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    nearest[i] = -1;
                    if (dist[i] < 0.12f || !Inside[i]) continue;
                    bool core = true;
                    for (int dy = -1; dy <= 1 && core; dy++)
                        for (int dx = -1; dx <= 1 && core; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h || dist[yy * w + xx] < 0.8f * dist[i]) core = false;
                        }
                    if (core) { nearest[i] = i; queue.Enqueue(i); }
                }
            int[] ox = { 1, -1, 0, 0, 1, 1, -1, -1 }, oy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(); int x = i % w, y = i / w;
                for (int k = 0; k < 8; k++)
                {
                    int xx = x + ox[k], yy = y + oy[k];
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                    int j = yy * w + xx;
                    if (nearest[j] >= 0) continue;
                    nearest[j] = nearest[i]; queue.Enqueue(j);
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (nearest[i] < 0) continue;
                int c = nearest[i];
                float vr = r.R[c] - Ground.R[i], vg = r.G[c] - Ground.G[i], vb = r.B[c] - Ground.B[i];
                float den = vr * vr + vg * vg + vb * vb;
                float num = (r.R[i] - Ground.R[i]) * vr + (r.G[i] - Ground.G[i]) * vg + (r.B[i] - Ground.B[i]) * vb;
                float a = den < 1e-5f ? 0f : Math.Clamp(num / den, 0f, 1f);
                Cover[i] = a < 0.04f ? 0f : a;
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

    /// <summary>
    /// The single-ink icon enlarged to w2 x h2 px with its edges tightened: the coverage is enlarged smoothly,
    /// then its transition is steepened around one half, so the outline to trace is unambiguous.
    /// </summary>
    public Img TileCrisp(double x, double y, double w, double h, int w2, int h2, double gain)
    {
        int tx0 = (int)Math.Floor(x), ty0 = (int)Math.Floor(y), tw = (int)Math.Ceiling(x + w) - tx0, th = (int)Math.Ceiling(y + h) - ty0;
        // Coverage as plain grey (no gamma), so the enlargement interpolates the coverage itself.
        using var grey = new Bitmap(tw, th, PixelFormat.Format32bppArgb);
        for (int yy = 0; yy < th; yy++)
            for (int xx = 0; xx < tw; xx++)
            {
                int rx = tx0 + xx - X0, ry = ty0 + yy - Y0;
                float c = rx >= 0 && ry >= 0 && rx < Region.W && ry < Region.H ? Cover[ry * Region.W + rx] : 0f;
                int v = 255 - (int)MathF.Round(Math.Clamp(c, 0f, 1f) * 255f);
                grey.SetPixel(xx, yy, Color.FromArgb(255, v, v, v));
            }
        using var big = new Bitmap(w2, h2, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(big))
        using (var attrs = new ImageAttributes())
        {
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(grey, new Rectangle(0, 0, w2, h2), 0, 0, tw, th, GraphicsUnit.Pixel, attrs);
        }
        var t = new Img(w2, h2);
        for (int yy = 0; yy < h2; yy++)
            for (int xx = 0; xx < w2; xx++)
            {
                float c = 1f - big.GetPixel(xx, yy).R / 255f;
                c = (float)Math.Clamp((c - 0.5) * gain + 0.5, 0.0, 1.0);
                int i = yy * w2 + xx;
                t.R[i] = 1f + (Ink.r - 1f) * c; t.G[i] = 1f + (Ink.g - 1f) * c; t.B[i] = 1f + (Ink.b - 1f) * c;
            }
        return t;
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
        if (args.Length == 0) { Console.WriteLine("icontool measure | sheet | strip | split | make | build | export | place | map | handoff | review | audit   (see the header of icontool.cs)"); return 0; }
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
                case "build": return Build.Make(Get("only"));
                case "export": return Build.Export();
                case "place": Place.Run(Get("only")); return 0;
                case "map": return Place.Map();
                case "handoff": return Handoff.Run();
                case "audit": return Audit.Run();
                case "review": return Review.Run(Get("names"), Get("out", "ArtSource/Working/Kit/Icons/review/review.png"), int.Parse(Get("zoom", "0")), Get("attempt"));
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
        public double Crisp;              // > 0: single-ink tiles are drawn with their edges tightened by this gain (a clear outline to trace)
        public List<string> Cells = new List<string>();
        public List<string> Captions = new List<string>();
        public List<double?> CellCrisp = new List<double?>();   // per-cell override of Crisp
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
        if (n["crisp"] != null) s.Crisp = (double)n["crisp"];
        foreach (JsonNode c in n["cells"].AsArray())
        {
            if (c is JsonObject o) { s.Cells.Add((string)o["icon"]); s.Captions.Add((string)o["caption"]); s.CellCrisp.Add(o["crisp"] != null ? (double)o["crisp"] : (double?)null); }
            else { s.Cells.Add((string)c); s.Captions.Add(null); s.CellCrisp.Add(null); }
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
                var dest = new Rectangle((int)Math.Round(px), (int)Math.Round(py), (int)Math.Round(tw), (int)Math.Round(th));
                double crispGain = s.CellCrisp[i] ?? s.Crisp;
                if (crispGain > 0 && !colour)
                {
                    using Bitmap crisp = m.TileCrisp(tx0, ty0, tx1 - tx0, ty1 - ty0, dest.Width, dest.Height, crispGain).ToBitmap();
                    g.DrawImageUnscaled(crisp, dest.X, dest.Y);
                }
                else g.DrawImage(tile, dest, 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel, attrs);
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
    public const int Up = 4;          // distance-field samples per cell pixel
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
        return SdFromMask(inside, uw, uh);
    }

    /// <summary>The signed distance field (cell px, negative inside) of a mask sampled Up times per cell px.</summary>
    private static float[] SdFromMask(bool[] inside, int uw, int uh)
    {
        float[] toInside = Edt(inside, uw, uh, true), toOutside = Edt(inside, uw, uh, false);
        var sd = new float[uw * uh];
        for (int i = 0; i < sd.Length; i++)
            sd[i] = (inside[i] ? -(toOutside[i] - 0.5f) : toInside[i] - 0.5f) / Up;
        // The threshold puts the outline on the sample grid; a light blur of the field turns the
        // one-sample steps of a nearly straight edge back into a straight edge.
        var tmp = new float[sd.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < uh; y++)
                for (int x = 0; x < uw; x++)
                {
                    int i = y * uw + x;
                    tmp[i] = 0.25f * sd[y * uw + Math.Max(x - 1, 0)] + 0.5f * sd[i] + 0.25f * sd[y * uw + Math.Min(x + 1, uw - 1)];
                }
            for (int y = 0; y < uh; y++)
                for (int x = 0; x < uw; x++)
                {
                    int i = y * uw + x;
                    sd[i] = 0.25f * tmp[Math.Max(y - 1, 0) * uw + x] + 0.5f * tmp[i] + 0.25f * tmp[Math.Min(y + 1, uh - 1) * uw + x];
                }
        }
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

    /// <summary>
    /// Thins (or, with a negative amount, widens) only what the shape encloses: marks cut out of a solid body,
    /// such as the rows of a list. The outline of the shape itself stays where it is. Amount in cell px.
    /// </summary>
    public void GrowIntoHoles(double amount)
    {
        int uw = W * Up, uh = H * Up;
        // Outside: every sample that is not ink and can be reached from the border of the cell.
        var outside = new bool[uw * uh];
        var stack = new Stack<int>();
        void Push(int x, int y) { int i = y * uw + x; if (outside[i] || Sd[i] < 0) return; outside[i] = true; stack.Push(i); }
        for (int x = 0; x < uw; x++) { Push(x, 0); Push(x, uh - 1); }
        for (int y = 0; y < uh; y++) { Push(0, y); Push(uw - 1, y); }
        while (stack.Count > 0)
        {
            int i = stack.Pop(); int x = i % uw, y = i / uw;
            if (x > 0) Push(x - 1, y); if (x < uw - 1) Push(x + 1, y); if (y > 0) Push(x, y - 1); if (y < uh - 1) Push(x, y + 1);
        }
        // The new shape: the ink, plus every enclosed sample closer to the ink than the amount. Its distance
        // field is built afresh, so the outline can still be moved as a whole afterwards.
        var inside = new bool[uw * uh];
        int holes = 0, filled = 0;
        for (int i = 0; i < Sd.Length; i++)
        {
            bool hole = Sd[i] >= 0 && !outside[i];
            if (hole) holes++;
            inside[i] = Sd[i] < 0 || (hole && Sd[i] < amount);
            if (hole && inside[i]) filled++;
        }
        Sd = SdFromMask(inside, uw, uh);
        Console.WriteLine($"   enclosed marks: {holes / (double)(Up * Up):0} cell px, of which {filled / (double)(Up * Up):0} filled by growing the ink {amount:0.00} cell px into them");
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

    /// <summary>The cell position that a pose puts at a mockup position.</summary>
    public static (double cx, double cy) ToCell(Pose p, double mx, double my)
    {
        double cos = Math.Cos(p.Rot), sin = Math.Sin(p.Rot), dx = mx - p.Tx, dy = my - p.Ty;
        return ((cos * dx + sin * dy) / p.Sx, (-sin * dx + cos * dy) / p.Sy);
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
            // --attempt a1 names every cell alike; --attempt a1,a1,a2,a2 names them cell by cell (a sheet with second attempts).
            string[] perCell = attempt.Split(',');
            string file = Path.Combine(outDir, $"{s.Cells[c]}_{(perCell.Length > 1 ? perCell[Math.Min(c, perCell.Length - 1)] : attempt)}.png");
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
        public string Name; public Pose Pose; public double Rms, RmsBest, Iou, Weight, Aspect, SourceScale, EdgeMean, EdgeMax, OuterMean, OuterMax;
        public int W, H; public double[] MockRect; public double[] InkRect; public string File;
    }

    public static int Run(string name, string src, Dictionary<string, string> opt)
    {
        IconDef d = Spec.Get(name);
        MockIcon m = MockIcon.Read(d);
        Img cell = Img.Load(Tool.Full(src));
        bool colour = d.Mode == "color";
        ColourCell cc = colour ? ColourCell.From(cell, d.KeepWhite) : null;
        if (colour && d.Palette != null) cc.SnapTo(d.Palette.Select(Img.Hex).ToArray());
        Shape s = colour ? Shape.FromAlpha(cc.Alpha, cell.W, cell.H, (0f, 0f, 0f), (1f, 1f, 1f)) : Shape.FromInkCell(cell);
        if (!colour && (opt.ContainsKey("symmetric") || d.Symmetric)) s.Symmetrise();
        if (opt.TryGetValue("hole-grow", out string hgs))
        {
            // Given in mockup px; the size of the drawing on the mockup is known well enough from the two boxes.
            double unit0 = Math.Sqrt((m.Box[2] - m.Box[0]) / (s.Box[2] - s.Box[0]) * (m.Box[3] - m.Box[1]) / (s.Box[3] - s.Box[1]));
            s.GrowIntoHoles(double.Parse(hgs) / unit0);
        }
        (float r, float g, float b) mockInk = m.Ink;
        double Err(Pose q) => colour ? Fit.CoverError(s, q, m) : Fit.Error(s, q, m, mockInk);

        // 1. Where the redrawn shape sits on the mockup, first rigidly, then with its own aspect, weight and softness.
        bool rotate = d.Angle != null;
        Pose p3 = Fit.Solve(s, m, mockInk, colour, 3, null, rotate, d.Angle ?? 0);
        Pose p6 = Fit.Solve(s, m, mockInk, colour, 6, p3, rotate);
        double rmsBest = Math.Sqrt(Err(p6));
        Pose asDrawn = p6; asDrawn.Grow = 0;
        Console.WriteLine($"   rms with the outline as drawn (grow 0): {Math.Sqrt(Err(asDrawn)):0.00}; with the fitted grow {p6.Grow:+0.00;-0.00}: {rmsBest:0.00}; rigid fit only: {Math.Sqrt(Err(p3)):0.00}");

        // 2. What is baked into the sprite: position, size and aspect from the fit; the weight only as far as asked.
        double grow = opt.TryGetValue("grow", out string gs) ? double.Parse(gs) : p6.Grow;
        if (opt.TryGetValue("max-grow", out string mg)) grow = Math.Min(grow, double.Parse(mg));
        bool keepAspect = opt.ContainsKey("uniform");
        Pose p = p6; p.Grow = grow;
        if (Math.Abs(grow - p6.Grow) > 1e-6)
        {
            // A weight that is set by hand changes where the outline sits: place and size are fitted again around it.
            p = Fit.Solve(s, m, mockInk, colour, 4, p, rotate);
        }
        if (keepAspect) { Pose u = p3; u.Grow = grow; u.Blur = p6.Blur; p = Fit.Solve(s, m, mockInk, colour, 3, u, rotate); }
        double rms = Math.Sqrt(Err(p));
        if (colour)
        {
            if (d.Tone == "mock") cc.ToneTo(m);
        }

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
        double pad = 4;   // mockup px of slack around the box for the first pass
        if (opt.TryGetValue("scale", out string scs))
        {
            // Another screen draws the icon larger than the one it is fitted to: the sprite is exported at
            // that size (same shape, same weight in proportion), so that no use enlarges it.
            double f = double.Parse(scs);
            double bcx = (s.Box[0] + s.Box[2]) / 2, bcy = (s.Box[1] + s.Box[3]) / 2;
            double mcx = crisp.Tx + bcx * crisp.Sx, mcy = crisp.Ty + bcy * crisp.Sy;
            crisp.Sx *= f; crisp.Sy *= f; crisp.Grow *= f;
            crisp.Tx = mcx - bcx * crisp.Sx; crisp.Ty = mcy - bcy * crisp.Sy;
            pad += Math.Max(m.Box[2] - m.Box[0], m.Box[3] - m.Box[1]) * Math.Max(0, f - 1);
        }
        if (opt.TryGetValue("ink-height", out string ihs))
        {
            // Measured the way the sprite is trimmed (where the ink becomes visible at @2x), and aimed just
            // under the target so that every symbol of a set rounds to the same even height.
            double target = double.Parse(ihs) - 0.4, total = 1;
            pad = Math.Max(m.Box[2] - m.Box[0], m.Box[3] - m.Box[1]) + double.Parse(ihs) / Scale2x;
            for (int round = 0; round < 4; round++)
            {
                double px0 = m.Box[0] - pad, py0 = m.Box[1] - pad;
                int pw0 = (int)Math.Ceiling((m.Box[2] - m.Box[0] + 2 * pad) * Scale2x), ph0 = (int)Math.Ceiling((m.Box[3] - m.Box[1] + 2 * pad) * Scale2x);
                float[] probe = Fit.Render(s, crisp, px0, py0, Scale2x, pw0, ph0, 4);
                double[] pb = Bounds.HalfBox(probe, pw0, ph0, 0, 0, 0.02f);
                double f = target / (pb[3] - pb[1]);
                if (Math.Abs(f - 1) < 0.0015) break;
                double bcx = (s.Box[0] + s.Box[2]) / 2, bcy = (s.Box[1] + s.Box[3]) / 2;
                double mcx = crisp.Tx + bcx * crisp.Sx, mcy = crisp.Ty + bcy * crisp.Sy;
                crisp.Sx *= f; crisp.Sy *= f; crisp.Grow *= f; total *= f;
                crisp.Tx = mcx - bcx * crisp.Sx; crisp.Ty = mcy - bcy * crisp.Sy;
            }
            Console.WriteLine($"   ink height set to {ihs} px: x{total:0.000} of the mockup size");
        }
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
        if (colour)
        {
            // Its own colours: the mean over each sprite pixel of the (bled) drawing.
            const int N = 3;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0;
                    for (int j = 0; j < N; j++)
                        for (int i = 0; i < N; i++)
                        {
                            (double cx, double cy) = Fit.ToCell(crisp, ox + (x + (i + 0.5) / N) / Scale2x, oy + (y + (j + 0.5) / N) / Scale2x);
                            (float cr, float cg, float cb) = cc.At(cx, cy);
                            r += cr; g += cg; b += cb;
                        }
                    int k = y * w + x;
                    sprite.R[k] = r / (N * N); sprite.G[k] = g / (N * N); sprite.B[k] = b / (N * N);
                }
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
        // The same for the outer outline alone: marks enclosed by the shape (the rows of a list) are left out,
        // because a mockup that draws them faintly has no outline there to compare with.
        bool[] band = OuterBand(covSoft, m.Cover, m.Inside, mw, mh, 3);
        (res.OuterMean, res.OuterMax) = EdgeDeviation(covSoft, m.Cover, band, mw, mh);
        if (!colour)
        {
            // The ink the mockup itself uses for this icon: the colour that, laid over the ground with the
            // fitted coverage, comes closest to the crop (least squares in linear light).
            double nr = 0, ng = 0, nb = 0, dn = 0;
            for (int i = 0; i < covSoft.Length; i++)
            {
                if (!m.Inside[i]) continue;
                double c = covSoft[i];
                if (c < 0.05) continue;
                nr += c * (m.Region.R[i] - m.Ground.R[i] * (1 - c)); ng += c * (m.Region.G[i] - m.Ground.G[i] * (1 - c)); nb += c * (m.Region.B[i] - m.Ground.B[i] * (1 - c));
                dn += c * c;
            }
            var own = ((float)Math.Clamp(nr / dn, 0, 1), (float)Math.Clamp(ng / dn, 0, 1), (float)Math.Clamp(nb / dn, 0, 1));
            // The opacity at which the family ink reads as that colour on this ground (linear-light blend).
            int mid = (mh / 2) * mw + mw / 2;
            double gl = m.Ground.Lum(mid), il = 0.2126 * inkCol.r + 0.7152 * inkCol.g + 0.0722 * inkCol.b, ol = 0.2126 * own.Item1 + 0.7152 * own.Item2 + 0.0722 * own.Item3;
            double opacity = Math.Clamp((gl - ol) / Math.Max(1e-6, gl - il), 0, 1);
            Console.WriteLine($"   mockup ink here {Img.Hex(own.Item1, own.Item2, own.Item3)} (sprite ink {Img.Hex(inkCol.r, inkCol.g, inkCol.b)}; opacity {opacity:0.000} over this ground reads the same)");
            File.WriteAllText(Path.Combine(Spec.Work, "fitted", $"{name}_{attempt}.ink"), FormattableString.Invariant($"{Img.Hex(own.Item1, own.Item2, own.Item3)} {opacity:0.000}"));
        }
        Console.WriteLine($"{name}  {w}x{h}  ink on {d.Screen}: {inkL:0.00},{inkT:0.00} {inkR - inkL:0.00}x{inkB - inkT:0.00}  sprite rect {ox:0.00},{oy:0.00} {w / Scale2x:0.00}x{h / Scale2x:0.00}");
        Console.WriteLine($"   rms {rms:0.00} (best {rmsBest:0.00})  iou {res.Iou:0.000}  weight {res.Weight:0.000}  edge mean {res.EdgeMean:0.00} max {res.EdgeMax:0.00} px" +
                          $"  aspect {res.Aspect:0.000}  fit grow {p6.Grow:+0.00;-0.00} baked {p.Grow:+0.00;-0.00}  blur {p6.Blur:0.00}  source x{res.SourceScale:0.00}" + (rotate ? $"  tilt on the mockup {p.Rot * 180 / Math.PI:+0.0;-0.0} deg" : ""));
        Check.Write(d, m, s, p, sprite, res, attempt);
        File.WriteAllText(Path.ChangeExtension(outFile, ".txt"),
            FormattableString.Invariant($"{name} {attempt}\nsize {w}x{h}\nscreen {d.Screen}\nink_rect {inkL:0.00},{inkT:0.00},{inkR - inkL:0.00},{inkB - inkT:0.00}\nsprite_rect {ox:0.00},{oy:0.00},{w / Scale2x:0.00},{h / Scale2x:0.00}\nrms {rms:0.00}\niou {res.Iou:0.000}\nweight {res.Weight:0.000}\nedge_mean {res.EdgeMean:0.00}\nedge_max {res.EdgeMax:0.00}\nouter_mean {res.OuterMean:0.00}\nouter_max {res.OuterMax:0.00}\naspect {res.Aspect:0.000}\ngrow_fit {p6.Grow:0.00}\ngrow_baked {p.Grow:0.00}\nblur {p6.Blur:0.00}\nsource_scale {res.SourceScale:0.00}\n"));
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

    /// <summary>The pixels within <paramref name="width"/> px of what lies outside both shapes (reached from the border).</summary>
    private static bool[] OuterBand(float[] a, float[] b, bool[] inside, int w, int h, int width)
    {
        var dist = new int[w * h];
        var queue = new Queue<int>();
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;
        bool Ink(int i) => inside[i] && (a[i] >= 0.5f || b[i] >= 0.5f);
        void Seed(int x, int y) { int i = y * w + x; if (dist[i] >= 0 || Ink(i)) return; dist[i] = 0; queue.Enqueue(i); }
        for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }
        for (int i = 0; i < dist.Length; i++) if (!inside[i] && dist[i] < 0) { dist[i] = 0; queue.Enqueue(i); }
        int[] ox = { 1, -1, 0, 0 }, oy = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(); int x = i % w, y = i / w;
            for (int d = 0; d < 4; d++)
            {
                int xx = x + ox[d], yy = y + oy[d];
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                int j = yy * w + xx;
                if (dist[j] >= 0) continue;
                bool ink = Ink(j);
                // Through open ground the distance stays 0; into the ink it counts up to the band's width.
                if (!ink && dist[i] > 0) continue;       // ground behind ink is enclosed: not reached
                int dj = ink ? dist[i] + 1 : 0;
                if (dj > width) continue;
                dist[j] = dj; queue.Enqueue(j);
            }
        }
        var band = new bool[w * h];
        for (int i = 0; i < band.Length; i++) band[i] = inside[i] && dist[i] >= 0;
        return band;
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
    /// <summary>The sprite drawn over a ground image whose pixel (0, 0) is mockup (x0, y0) at the given scale.</summary>
    public static Img SpriteOver(Img sprite, double[] mockRect, Img ground, double x0, double y0, double scale)
    {
        var o = ground.Clone2();
        double perMock = sprite.W / mockRect[2];          // sprite px per mockup px (2.3 unless the sprite is shown at another size)
        double k = perMock / scale;                       // sprite px per output px
        int n = Math.Max(2, (int)Math.Ceiling(k * 1.5));
        for (int y = 0; y < o.H; y++)
            for (int x = 0; x < o.W; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        double sx = (x0 + (x + (i + 0.5) / n) / scale - mockRect[0]) * perMock;
                        double sy = (y0 + (y + (j + 0.5) / n) / scale - mockRect[1]) * sprite.H / mockRect[3];
                        int px = (int)Math.Floor(sx), py = (int)Math.Floor(sy);
                        if (px < 0 || py < 0 || px >= sprite.W || py >= sprite.H) continue;
                        int si = py * sprite.W + px;
                        float sa = sprite.A[si];
                        r += sprite.R[si] * sa; g += sprite.G[si] * sa; b += sprite.B[si] * sa; a += sa;
                    }
                float inv = 1f / (n * n);
                int t = y * o.W + x;
                a *= inv;
                o.R[t] = o.R[t] * (1 - a) + r * inv; o.G[t] = o.G[t] * (1 - a) + g * inv; o.B[t] = o.B[t] * (1 - a) + b * inv;
            }
        return o;
    }

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
        if (d.Mode == "color")
        {
            // A multi-coloured sprite is shown with its own pixels, placed by its sprite rect.
            b = SpriteOver(sprite, res.MockRect, groundZ, m.X0, m.Y0, z);
            c1 = SpriteOver(sprite, res.MockRect, m.Ground, m.X0, m.Y0, 1.0);
            if (p.Blur >= 0.12)
            {
                float[] br = Fit.Gauss(c1.R, mw, mh, p.Blur), bg = Fit.Gauss(c1.G, mw, mh, p.Blur), bb = Fit.Gauss(c1.B, mw, mh, p.Blur);
                Array.Copy(br, c1.R, br.Length); Array.Copy(bg, c1.G, bg.Length); Array.Copy(bb, c1.B, bb.Length);
            }
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

/// <summary>
/// A redrawn multi-coloured icon on a flat white ground: its silhouette as alpha, and its colours with the
/// white of the ground taken out of the edge pixels and carried outwards, so resampling never pulls white in.
/// </summary>
internal sealed class ColourCell
{
    public int W, H;
    public float[] Alpha;
    public Img Colour;      // defined everywhere (bled from the inside)
    public bool[] Core;     // pixels that are fully the icon
    public bool[] Ground;   // pixels that are the flat ground around the icon
    private Img _source;    // the cell as drawn

    public static ColourCell From(Img cell, bool keepWhite)
    {
        int w = cell.W, h = cell.H, n = cell.Count;
        var c = new ColourCell { W = w, H = h, Alpha = new float[n], Colour = new Img(w, h), Core = new bool[n] };
        bool hasAlpha = false;
        for (int i = 0; i < n && !hasAlpha; i++) if (cell.A[i] < 0.98f) hasAlpha = true;
        var ground = new bool[n];        // pixels that are the flat ground (or fully transparent)
        var white = new float[n];        // how far a pixel is from white, 0..~1.7
        for (int i = 0; i < n; i++)
        {
            float dr = 1f - cell.R[i], dg = 1f - cell.G[i], db = 1f - cell.B[i];
            white[i] = MathF.Sqrt(dr * dr + dg * dg + db * db);
        }
        if (hasAlpha)
        {
            for (int i = 0; i < n; i++) { c.Alpha[i] = Math.Clamp((cell.A[i] - 12f / 255f) / (238f / 255f), 0f, 1f); c.Core[i] = c.Alpha[i] >= 0.995f; ground[i] = c.Alpha[i] <= 0.005f; }
        }
        else
        {
            const float Near = 0.10f;     // closer to white than this: ground (or a white part of the icon)
            var isWhite = new bool[n];
            for (int i = 0; i < n; i++) isWhite[i] = white[i] < Near;
            if (keepWhite)
            {
                // Only white that is connected to the image edge is ground; enclosed white is part of the icon.
                var stack = new Stack<int>();
                void Push(int x, int y) { int i = y * w + x; if (ground[i] || !isWhite[i]) return; ground[i] = true; stack.Push(i); }
                for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
                for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
                while (stack.Count > 0)
                {
                    int i = stack.Pop(); int x = i % w, y = i / w;
                    if (x > 0) Push(x - 1, y); if (x < w - 1) Push(x + 1, y); if (y > 0) Push(x, y - 1); if (y < h - 1) Push(x, y + 1);
                }
            }
            else Array.Copy(isWhite, ground, n);
            // Core: not ground and not next to ground (2 px), so its colour holds no white from the edge blend.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (ground[i]) continue;
                    bool core = true;
                    for (int dy = -2; dy <= 2 && core; dy++)
                        for (int dx = -2; dx <= 2 && core; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h || ground[yy * w + xx]) core = false;
                        }
                    c.Core[i] = core;
                }
        }

        c.Ground = ground; c._source = cell;
        // Colours: the core keeps its own; everything else takes the colour of the nearest core pixel.
        var nearest = new int[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++) { nearest[i] = c.Core[i] ? i : -1; if (c.Core[i]) queue.Enqueue(i); }
        int[] ox = { 1, -1, 0, 0, 1, 1, -1, -1 }, oy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(); int x = i % w, y = i / w;
            for (int k = 0; k < 8; k++)
            {
                int xx = x + ox[k], yy = y + oy[k];
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                int j = yy * w + xx;
                if (nearest[j] >= 0) continue;
                nearest[j] = nearest[i]; queue.Enqueue(j);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int s = nearest[i] < 0 ? i : nearest[i];
            c.Colour.R[i] = cell.R[s]; c.Colour.G[i] = cell.G[s]; c.Colour.B[i] = cell.B[s];
        }
        if (!hasAlpha)
        {
            // Alpha of the edge band: how far the pixel has moved from white towards its core colour.
            for (int i = 0; i < n; i++)
            {
                if (c.Core[i]) { c.Alpha[i] = 1f; continue; }
                float fr = 1f - c.Colour.R[i], fg = 1f - c.Colour.G[i], fb = 1f - c.Colour.B[i];
                float den = fr * fr + fg * fg + fb * fb;
                if (den < 1e-4f) { c.Alpha[i] = ground[i] ? 0f : 1f; continue; }
                float num = (1f - cell.R[i]) * fr + (1f - cell.G[i]) * fg + (1f - cell.B[i]) * fb;
                float a = Math.Clamp(num / den, 0f, 1f);
                c.Alpha[i] = ground[i] && a < 0.06f ? 0f : a;
            }
            // Ground far from the icon is never part of it, whatever its colour says.
            var nearCore = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!c.Core[y * w + x]) continue;
                    for (int dy = -5; dy <= 5; dy++)
                        for (int dx = -5; dx <= 5; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < w && yy < h) nearCore[yy * w + xx] = true;
                        }
                }
            for (int i = 0; i < n; i++) if (!nearCore[i]) c.Alpha[i] = 0f;
        }
        return c;
    }

    /// <summary>
    /// Redraws a flat-coloured icon in a palette. Only pixels well inside a shape decide which palette colour
    /// the shape is (the drawn edges carry darker rims and soft mixes that would mislabel single pixels);
    /// every other pixel takes the label of the nearest such pixel. The alpha of a pixel is how far it has
    /// moved from the white ground towards its shape's drawn colour, so a narrow gap that the drawing left
    /// light grey stays a gap. Where two colours meet inside a shape (a white mark on a badge) the drawn
    /// transition between them is kept.
    /// </summary>
    public void SnapTo((float r, float g, float b)[] palette)
    {
        int n = W * H, k = palette.Length;
        Img src = _source;
        var sr = new float[n]; var sg = new float[n]; var sb = new float[n];      // the drawing in display space
        for (int i = 0; i < n; i++) { sr[i] = Img.ToSrgb(src.R[i]); sg[i] = Img.ToSrgb(src.G[i]); sb[i] = Img.ToSrgb(src.B[i]); }
        var pal = new (double r, double g, double b)[k];
        bool hasWhite = false;
        for (int j = 0; j < k; j++)
        {
            pal[j] = (Img.ToSrgb(palette[j].r), Img.ToSrgb(palette[j].g), Img.ToSrgb(palette[j].b));
            if (1 - pal[j].r + 1 - pal[j].g + 1 - pal[j].b < 0.2) hasWhite = true;
        }
        // Coverage of pixel i if it were colour c over white: the projection of (white - pixel) on (white - c).
        double Cover((double r, double g, double b) c, int i)
        {
            double vr = 1 - c.r, vg = 1 - c.g, vb = 1 - c.b, len = vr * vr + vg * vg + vb * vb;
            return len < 0.02 ? 1.0 : ((1 - sr[i]) * vr + (1 - sg[i]) * vg + (1 - sb[i]) * vb) / len;
        }
        double Dist2((double r, double g, double b) c, int i)
        {
            double dr = c.r - sr[i], dg = c.g - sg[i], db = c.b - sb[i];
            return dr * dr + dg * dg + db * db;
        }
        // Light pixels that connect to the image border through light pixels are outside the icon: the ground,
        // the faint end of a soft edge, or a smudge in the ground that is barely off-white. None of them is a
        // white part of the icon (a white mark counts only where the icon encloses it).
        var outsideLight = new bool[n];
        {
            var light = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float dr = 1f - src.R[i], dg = 1f - src.G[i], db = 1f - src.B[i];
                light[i] = MathF.Sqrt(dr * dr + dg * dg + db * db) < 0.25f;
            }
            var stack = new Stack<int>();
            void Push(int x, int y) { int i = y * W + x; if (outsideLight[i] || !light[i]) return; outsideLight[i] = true; stack.Push(i); }
            for (int x = 0; x < W; x++) { Push(x, 0); Push(x, H - 1); }
            for (int y = 0; y < H; y++) { Push(0, y); Push(W - 1, y); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(); int x = i % W, y = i / W;
                if (x > 0) Push(x - 1, y); if (x < W - 1) Push(x + 1, y); if (y > 0) Push(x, y - 1); if (y < H - 1) Push(x, y + 1);
            }
        }
        var centre = ((double r, double g, double b)[])pal.Clone();
        var label = new int[n];
        var depth = new int[n];
        var queue = new Queue<int>();
        int[] ox = { 1, -1, 0, 0, 1, 1, -1, -1 }, oy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        bool[] deep = null;
        for (int round = 0; round < 3; round++)
        {
            // Shape pixels: not ground, and at least half covered by one of the colours (with a white entry
            // in the palette, every enclosed pixel is a shape pixel).
            for (int i = 0; i < n; i++)
            {
                bool shape = !Ground[i] && !outsideLight[i];
                if (shape && !hasWhite)
                {
                    double best = 0;
                    for (int j = 0; j < k; j++) best = Math.Max(best, Cover(centre[j], i));
                    shape = best >= 0.5;
                }
                depth[i] = shape ? -1 : 0;
                if (!shape) queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(); int x = i % W, y = i / W;
                for (int d = 0; d < 8; d++)
                {
                    int xx = x + ox[d], yy = y + oy[d];
                    if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                    int j = yy * W + xx;
                    if (depth[j] >= 0) continue;
                    depth[j] = depth[i] + 1; queue.Enqueue(j);
                }
            }
            // Deep pixels: at least 4 px inside (or as deep as a thin shape goes) and flat in colour.
            deep = new bool[n];
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (depth[i] < 2) continue;
                    int m = 0;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < W && yy < H && depth[yy * W + xx] > m) m = depth[yy * W + xx];
                        }
                    if (depth[i] < Math.Min(4, Math.Max(2, m - 1))) continue;
                    float lo = 9f, hi = -9f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int j = (y + dy) * W + x + dx;
                            float v = sr[j] + sg[j] + sb[j];
                            if (v < lo) lo = v; if (v > hi) hi = v;
                        }
                    deep[i] = hi - lo < 0.12f;
                }
            // The drawn colours: each palette entry moved onto the deep pixels nearest to it.
            for (int it = 0; it < 8; it++)
            {
                var sum = new (double r, double g, double b, int n)[k];
                for (int i = 0; i < n; i++)
                {
                    if (!deep[i]) continue;
                    int best = 0; double bd = double.MaxValue;
                    for (int j = 0; j < k; j++) { double d = Dist2(centre[j], i); if (d < bd) { bd = d; best = j; } }
                    sum[best].r += sr[i]; sum[best].g += sg[i]; sum[best].b += sb[i]; sum[best].n++;
                }
                for (int j = 0; j < k; j++) if (sum[j].n > 0) centre[j] = (sum[j].r / sum[j].n, sum[j].g / sum[j].n, sum[j].b / sum[j].n);
            }
        }
        for (int j = 0; j < k; j++)
            Console.WriteLine($"   palette {j}: drawn #{(int)Math.Round(centre[j].r * 255):X2}{(int)Math.Round(centre[j].g * 255):X2}{(int)Math.Round(centre[j].b * 255):X2} -> {Img.Hex(palette[j].r, palette[j].g, palette[j].b)}");
        // Labels: deep pixels by their own colour, everything else from the nearest deep pixel.
        for (int i = 0; i < n; i++)
        {
            label[i] = -1;
            if (!deep[i]) continue;
            int best = 0; double bd = double.MaxValue;
            for (int j = 0; j < k; j++) { double d = Dist2(centre[j], i); if (d < bd) { bd = d; best = j; } }
            label[i] = best; queue.Enqueue(i);
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(); int x = i % W, y = i / W;
            for (int d = 0; d < 8; d++)
            {
                int xx = x + ox[d], yy = y + oy[d];
                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                int j = yy * W + xx;
                if (label[j] >= 0) continue;
                label[j] = label[i]; queue.Enqueue(j);
            }
        }
        var outR = new float[n]; var outG = new float[n]; var outB = new float[n];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x, a = Math.Max(0, label[i]), b = -1;
                // Another colour of the same shape within 2 px: a colour-to-colour edge.
                if (depth[i] >= 1)
                    for (int dy = -2; dy <= 2 && b < 0; dy++)
                        for (int dx = -2; dx <= 2 && b < 0; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            int j = yy * W + xx;
                            if (depth[j] >= 2 && label[j] != a && label[j] >= 0) b = label[j];
                        }
                double t = 0;
                if (b >= 0 && depth[i] >= 2)
                {
                    double vr = centre[b].r - centre[a].r, vg = centre[b].g - centre[a].g, vb = centre[b].b - centre[a].b;
                    double len = vr * vr + vg * vg + vb * vb;
                    t = len < 1e-9 ? 0 : Math.Clamp(((sr[i] - centre[a].r) * vr + (sg[i] - centre[a].g) * vg + (sb[i] - centre[a].b) * vb) / len, 0, 1);
                    Alpha[i] = 1f;
                }
                else
                {
                    b = a;
                    if (Ground[i]) Alpha[i] = 0f;
                    else if (hasWhite && depth[i] >= 3) Alpha[i] = 1f;
                    else
                    {
                        double c = Cover(centre[a], i);
                        Alpha[i] = c < 0.05 ? 0f : c > 0.96 ? 1f : (float)c;
                    }
                }
                outR[i] = (float)(palette[a].r + (palette[b].r - palette[a].r) * t);
                outG[i] = (float)(palette[a].g + (palette[b].g - palette[a].g) * t);
                outB[i] = (float)(palette[a].b + (palette[b].b - palette[a].b) * t);
            }
        Array.Copy(outR, Colour.R, n); Array.Copy(outG, Colour.G, n); Array.Copy(outB, Colour.B, n);
    }

    /// <summary>Moves the drawing's colours onto the mockup's: same mean and spread per channel (a painted, shaded icon).</summary>
    public void ToneTo(MockIcon m)
    {
        int n = W * H;
        double[] mc = new double[3], sc = new double[3], mm = new double[3], sm = new double[3];
        int nc = 0, nm = 0;
        for (int i = 0; i < n; i++)
        {
            if (!Core[i]) continue;
            mc[0] += Colour.R[i]; mc[1] += Colour.G[i]; mc[2] += Colour.B[i]; nc++;
        }
        for (int i = 0; i < m.Region.Count; i++)
        {
            if (!m.Inside[i] || m.Cover[i] < 0.97f) continue;
            mm[0] += m.Region.R[i]; mm[1] += m.Region.G[i]; mm[2] += m.Region.B[i]; nm++;
        }
        if (nc == 0 || nm == 0) return;
        for (int k = 0; k < 3; k++) { mc[k] /= nc; mm[k] /= nm; }
        for (int i = 0; i < n; i++)
        {
            if (!Core[i]) continue;
            sc[0] += Math.Pow(Colour.R[i] - mc[0], 2); sc[1] += Math.Pow(Colour.G[i] - mc[1], 2); sc[2] += Math.Pow(Colour.B[i] - mc[2], 2);
        }
        for (int i = 0; i < m.Region.Count; i++)
        {
            if (!m.Inside[i] || m.Cover[i] < 0.97f) continue;
            sm[0] += Math.Pow(m.Region.R[i] - mm[0], 2); sm[1] += Math.Pow(m.Region.G[i] - mm[1], 2); sm[2] += Math.Pow(m.Region.B[i] - mm[2], 2);
        }
        var gain = new double[3];
        for (int k = 0; k < 3; k++) { sc[k] = Math.Sqrt(sc[k] / nc); sm[k] = Math.Sqrt(sm[k] / nm); gain[k] = sc[k] < 1e-5 ? 1 : Math.Clamp(sm[k] / sc[k], 0.6, 1.6); }
        Console.WriteLine($"   tone: drawn mean {Img.Hex((float)mc[0], (float)mc[1], (float)mc[2])} -> mockup mean {Img.Hex((float)mm[0], (float)mm[1], (float)mm[2])}  gain {gain[0]:0.00} {gain[1]:0.00} {gain[2]:0.00}");
        for (int i = 0; i < n; i++)
        {
            Colour.R[i] = (float)Math.Clamp((Colour.R[i] - mc[0]) * gain[0] + mm[0], 0, 1);
            Colour.G[i] = (float)Math.Clamp((Colour.G[i] - mc[1]) * gain[1] + mm[1], 0, 1);
            Colour.B[i] = (float)Math.Clamp((Colour.B[i] - mc[2]) * gain[2] + mm[2], 0, 1);
        }
    }

    /// <summary>The colour at a cell position (bilinear).</summary>
    public (float r, float g, float b) At(double cx, double cy)
    {
        double qx = Math.Clamp(cx - 0.5, 0, W - 1), qy = Math.Clamp(cy - 0.5, 0, H - 1);
        int x0 = (int)Math.Floor(qx), y0 = (int)Math.Floor(qy), x1 = Math.Min(x0 + 1, W - 1), y1 = Math.Min(y0 + 1, H - 1);
        float fx = (float)(qx - x0), fy = (float)(qy - y0);
        int a = y0 * W + x0, b = y0 * W + x1, c = y1 * W + x0, d = y1 * W + x1;
        float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
        return (Colour.R[a] * w00 + Colour.R[b] * w10 + Colour.R[c] * w01 + Colour.R[d] * w11,
                Colour.G[a] * w00 + Colour.G[b] * w10 + Colour.G[c] * w01 + Colour.G[d] * w11,
                Colour.B[a] * w00 + Colour.B[b] * w10 + Colour.B[c] * w01 + Colour.B[d] * w11);
    }
}

/// <summary>
/// The accepted attempt of every icon (accepted.json) and what is made from it: the sprites in
/// Assets/_Project/Art/UI/Icons and ArtSource/Exports/Icons, and the import manifest ArtSource/Kit/Icons.json.
/// </summary>
internal static class Build
{
    public sealed class Entry
    {
        public string Name, Attempt = "a1", Status = "ready", Note;
        public Dictionary<string, string> Options = new Dictionary<string, string>();
    }

    public static List<Entry> Load()
    {
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(Spec.Here, "accepted.json")), null, opts);
        var list = new List<Entry>();
        foreach (JsonNode n in root["icons"].AsArray())
        {
            var e = new Entry { Name = (string)n["name"], Attempt = (string)n["attempt"] ?? "a1", Status = (string)n["status"] ?? "ready", Note = (string)n["note"] };
            e.Options["attempt"] = e.Attempt;
            if (n["inkHeight"] != null) e.Options["ink-height"] = ((double)n["inkHeight"]).ToString(CultureInfo.InvariantCulture);
            if (n["grow"] != null) e.Options["grow"] = ((double)n["grow"]).ToString(CultureInfo.InvariantCulture);
            if (n["maxGrow"] != null) e.Options["max-grow"] = ((double)n["maxGrow"]).ToString(CultureInfo.InvariantCulture);
            if (n["holeGrow"] != null) e.Options["hole-grow"] = ((double)n["holeGrow"]).ToString(CultureInfo.InvariantCulture);
            if (n["scale"] != null) e.Options["scale"] = ((double)n["scale"]).ToString(CultureInfo.InvariantCulture);
            if (n["uniform"] != null && (bool)n["uniform"]) e.Options["uniform"] = "true";
            list.Add(e);
        }
        return list;
    }

    /// <summary>Fits every accepted cell again (after a change to the pipeline) and refreshes the check images.</summary>
    public static int Make(string only)
    {
        string[] names = only?.Split(',');
        foreach (Entry e in Load())
        {
            if (names != null && !names.Contains(e.Name)) continue;
            string src = Path.Combine(Spec.Work, "chatgpt", "cells", $"{e.Name}_{e.Attempt}.png");
            Maker.Run(e.Name, src, e.Options);
        }
        return 0;
    }

    /// <summary>Copies the accepted sprites into the game and the exports folder and writes the import manifest.</summary>
    public static int Export()
    {
        string art = Path.Combine(Spec.Repo, "Assets", "_Project", "Art", "UI", "Icons");
        string exports = Path.Combine(Spec.Repo, "ArtSource", "Exports", "Icons");
        Directory.CreateDirectory(art); Directory.CreateDirectory(exports);
        var sb = new StringBuilder();
        sb.Append("{\n  \"sprites\": [\n");
        List<Entry> list = Load();
        for (int i = 0; i < list.Count; i++)
        {
            Entry e = list[i];
            string fitted = Path.Combine(Spec.Work, "fitted", $"{e.Name}_{e.Attempt}.png");
            if (!File.Exists(fitted)) { Console.WriteLine($"{e.Name}: {fitted} missing - run build first"); return 1; }
            File.Copy(fitted, Path.Combine(art, e.Name + ".png"), true);
            File.Copy(fitted, Path.Combine(exports, e.Name + ".png"), true);
            sb.Append($"    {{ \"path\": \"UI/Icons/{e.Name}.png\", \"ppu\": 200 }}{(i < list.Count - 1 ? "," : "")}\n");
            using var bmp = new Bitmap(fitted);
            Console.WriteLine($"{e.Name,-26} {bmp.Width,3}x{bmp.Height,-3} {e.Attempt}  {e.Status}");
        }
        sb.Append("  ]\n}\n");
        string manifest = Path.Combine(Spec.Repo, "ArtSource", "Kit", "Icons.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest));
        File.WriteAllText(manifest, sb.ToString());
        Console.WriteLine($"{list.Count} sprites -> {art}, {exports}; manifest {manifest}");
        return 0;
    }
}

/// <summary>
/// Review sheets of finished sprites: for every icon the mockup crop and, beside it, the sprite drawn over
/// the same ground at the place and size it is fitted to, both enlarged. One image per batch of icons.
/// </summary>
internal static class Review
{
    public static int Run(string names, string outPath, int zoom, string attemptOverride)
    {
        var entries = Build.Load();
        string[] list = names == null ? entries.Select(e => e.Name).ToArray() : names.Split(',');
        var tiles = new List<Img>(); var captions = new List<string>();
        foreach (string name in list)
        {
            IconDef d = Spec.Get(name);
            Build.Entry e = entries.FirstOrDefault(x => x.Name == name);
            string attempt = attemptOverride ?? e?.Attempt ?? "a1";
            string fitted = Path.Combine(Spec.Work, "fitted", $"{name}_{attempt}.png");
            string info = Path.ChangeExtension(fitted, ".txt");
            if (!File.Exists(fitted) || !File.Exists(info)) { Console.WriteLine($"{name}: no fitted sprite for {attempt}"); continue; }
            double[] rect = File.ReadAllLines(info).First(l => l.StartsWith("sprite_rect ")).Substring(12).Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            Img sprite = Img.Load(fitted);
            MockIcon m = MockIcon.Read(d);
            int mw = m.Region.W, mh = m.Region.H;
            // A sprite exported at another size than this mockup draws it (the suit set) is shown at the mockup's size.
            double inkH = (sprite.H - 2 * Maker.Margin) / Maker.Scale2x, mockH = m.Box[3] - m.Box[1];
            if (Math.Abs(inkH / mockH - 1) > 0.06)
            {
                double f = mockH / inkH, cxm = (m.Box[0] + m.Box[2]) / 2, cym = (m.Box[1] + m.Box[3]) / 2;
                rect = new[] { cxm - rect[2] * f / 2, cym - rect[3] * f / 2, rect[2] * f, rect[3] * f };
            }
            int z = zoom > 0 ? zoom : Math.Max(2, Math.Min(6, 220 / Math.Max(mw, mh)));
            Img a = m.Region.Resize(mw * z, mh * z);
            Img b = Check.SpriteOver(sprite, rect, m.Ground.Resize(mw * z, mh * z), m.X0, m.Y0, z);
            var pair = new Img(a.W * 2 + 6, a.H);
            pair.Fill(0.55f, 0f, 0.55f);
            pair.Paste(a, 0, 0); pair.Paste(b, a.W + 6, 0);
            tiles.Add(pair);
            captions.Add($"{name.Replace("Icon_", "")} {attempt}  {sprite.W}x{sprite.H}  x{z}");
        }
        if (tiles.Count == 0) return 1;
        // Shelf layout, at most 1900 px wide.
        const int MaxW = 1900, Gap = 14, Text = 20;
        var pos = new List<(int x, int y)>();
        int cx = Gap, cy = Gap, rowH = 0, sheetW = 0;
        foreach (Img t in tiles)
        {
            if (cx + t.W + Gap > MaxW && cx > Gap) { cx = Gap; cy += rowH + Text + Gap; rowH = 0; }
            pos.Add((cx, cy));
            cx += t.W + Gap; rowH = Math.Max(rowH, t.H); sheetW = Math.Max(sheetW, cx);
        }
        var sheet = new Img(sheetW, cy + rowH + Text + Gap);
        sheet.Fill(0.12f, 0.12f, 0.12f);
        for (int i = 0; i < tiles.Count; i++) sheet.Paste(tiles[i], pos[i].x, pos[i].y);
        using Bitmap bmp = sheet.ToBitmap();
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
            for (int i = 0; i < tiles.Count; i++) g.DrawString(captions[i], font, Brushes.White, pos[i].x, pos[i].y + tiles[i].H + 2);
        }
        string full = Tool.Full(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        bmp.Save(full, ImageFormat.Png);
        Console.WriteLine($"{full}  {bmp.Width}x{bmp.Height}  ({tiles.Count} icons; left: mockup, right: sprite on the same ground)");
        return 0;
    }
}

/// <summary>
/// Where each finished sprite goes on every screen that shows the icon: the sprite is laid over that
/// mockup's own drawing at one scale (its proportions are kept) and the rectangle of the whole texture is
/// reported, with how well the two agree there.
/// </summary>
internal static class Place
{
    public sealed class Use
    {
        public string Icon, ScreenName, Screen, Node, Old;
        public double[] Rect, Disc, NodeRect;
        public bool Hidden, Swapped;
        // results
        public double[] SpriteRect; public double Iou, EdgeMean, EdgeMax, PxPerMock; public string MockInk; public double Opacity;
        public double[] StretchRect; public double StretchEdgeMean, StretchEdgeMax, StretchAspect;   // fitted with its own width:height on this screen
    }

    public static List<Use> Load()
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(Spec.Here, "uses.json")));
        var list = new List<Use>();
        foreach (JsonNode n in root["uses"].AsArray())
            list.Add(new Use
            {
                Icon = (string)n["icon"], ScreenName = (string)n["screenName"], Screen = (string)n["screen"], Node = (string)n["node"], Old = (string)n["old"],
                Rect = n["rect"].AsArray().Select(v => (double)v).ToArray(),
                Disc = n["disc"]?.AsArray().Select(v => (double)v).ToArray(),
                NodeRect = n["nodeRect"].AsArray().Select(v => (double)v).ToArray(),
                Hidden = n["hidden"] != null && (bool)n["hidden"],
                Swapped = n["swapped"] != null && (bool)n["swapped"],
            });
        return list;
    }

    public static List<Use> Run(string only, bool quiet = false)
    {
        string[] names = only?.Split(',');
        List<Build.Entry> accepted = Build.Load();
        List<Use> uses = Load();
        var done = new List<Use>();
        foreach (Build.Entry e in accepted)
        {
            if (names != null && !names.Contains(e.Name)) continue;
            IconDef d = Spec.Get(e.Name);
            string fitted = Path.Combine(Spec.Work, "fitted", $"{e.Name}_{e.Attempt}.png");
            if (!File.Exists(fitted)) { Console.WriteLine($"{e.Name}: no fitted sprite"); continue; }
            Img sprite = Img.Load(fitted);
            Shape s = Shape.FromAlpha((float[])sprite.A.Clone(), sprite.W, sprite.H, (0f, 0f, 0f), (1f, 1f, 1f));
            bool colour = d.Mode == "color";
            foreach (Use u in uses.Where(x => x.Icon == e.Name))
            {
                if (u.Hidden) { done.Add(u); continue; }
                MockIcon m = MockIcon.Read(u.Screen, u.Rect, colour, u.Disc);
                if (colour && d.KeepWhite) m.FillEnclosed();
                if (m.Box == null) { Console.WriteLine($"{e.Name} on {u.ScreenName}: no ink in {string.Join(",", u.Rect)}"); continue; }
                // One scale, no tilt: a card pip that the mockup draws tilted is still placed upright.
                Pose p = Fit.Solve(s, m, m.Ink, colour, 3);
                // The softness of this mockup's rendering only matters for the comparison.
                Pose soft = Fit.Solve(s, m, m.Ink, colour, 3, p);
                double best = double.MaxValue;
                foreach (double blur in new[] { 0.2, 0.35, 0.5 })
                {
                    Pose q = p; q.Blur = blur;
                    q = Fit.Solve(s, m, m.Ink, colour, 3, q);
                    double err = colour ? Fit.CoverError(s, q, m) : Fit.Error(s, q, m, m.Ink);
                    if (err < best) { best = err; soft = q; }
                }
                p = soft;
                int mw = m.Region.W, mh = m.Region.H;
                var sharp = p; sharp.Blur = 0;
                float[] cov = Fit.Gauss(Fit.Render(s, sharp, m.X0, m.Y0, 1.0, mw, mh, 4), mw, mh, p.Blur);
                double inter = 0, union = 0;
                for (int i = 0; i < cov.Length; i++)
                {
                    if (!m.Inside[i]) continue;
                    bool a = cov[i] >= 0.5f, b = m.Cover[i] >= 0.5f;
                    if (a && b) inter++; if (a || b) union++;
                }
                u.Iou = union > 0 ? inter / union : 0;
                (u.EdgeMean, u.EdgeMax) = Maker.EdgeDeviation(cov, m.Cover, m.Inside, mw, mh);
                u.SpriteRect = new[] { p.Tx, p.Ty, p.Sx * sprite.W, p.Sy * sprite.H };
                u.PxPerMock = 1.0 / p.Sx;
                // The same with the width:height ratio left free: some screens draw the icon a little wider or lower.
                {
                    Pose q = Fit.Solve(s, m, m.Ink, colour, 4, p);
                    var qs = q; qs.Blur = 0;
                    float[] cq = Fit.Gauss(Fit.Render(s, qs, m.X0, m.Y0, 1.0, mw, mh, 4), mw, mh, q.Blur);
                    (u.StretchEdgeMean, u.StretchEdgeMax) = Maker.EdgeDeviation(cq, m.Cover, m.Inside, mw, mh);
                    u.StretchRect = new[] { q.Tx, q.Ty, q.Sx * sprite.W, q.Sy * sprite.H };
                    u.StretchAspect = q.Sx / q.Sy;
                }
                if (!colour)
                {
                    double nr = 0, ng = 0, nb = 0, dn = 0;
                    for (int i = 0; i < cov.Length; i++)
                    {
                        if (!m.Inside[i] || cov[i] < 0.05f) continue;
                        double c = cov[i];
                        nr += c * (m.Region.R[i] - m.Ground.R[i] * (1 - c)); ng += c * (m.Region.G[i] - m.Ground.G[i] * (1 - c)); nb += c * (m.Region.B[i] - m.Ground.B[i] * (1 - c));
                        dn += c * c;
                    }
                    var own = ((float)Math.Clamp(nr / dn, 0, 1), (float)Math.Clamp(ng / dn, 0, 1), (float)Math.Clamp(nb / dn, 0, 1));
                    var ink = Img.Hex(d.Ink ?? Spec.FamilyInk);
                    int mid = (mh / 2) * mw + mw / 2;
                    double gl = m.Ground.Lum(mid), il = 0.2126 * ink.r + 0.7152 * ink.g + 0.0722 * ink.b, ol = 0.2126 * own.Item1 + 0.7152 * own.Item2 + 0.0722 * own.Item3;
                    u.MockInk = Img.Hex(own.Item1, own.Item2, own.Item3);
                    u.Opacity = Math.Clamp((gl - ol) / Math.Max(1e-6, gl - il), 0, 1);
                }
                done.Add(u);
                if (!quiet)
                    Console.WriteLine(FormattableString.Invariant($"{e.Name,-22} {u.Screen} {u.ScreenName + "." + u.Node,-30} rect [{u.SpriteRect[0]:0.00}, {u.SpriteRect[1]:0.00}, {u.SpriteRect[2]:0.00}, {u.SpriteRect[3]:0.00}]  {u.PxPerMock:0.00} px/mock px{(u.PxPerMock < 2.25 ? " (upscaled)" : "")}  iou {u.Iou:0.000}  edge {u.EdgeMean:0.00}/{u.EdgeMax:0.00}  ink {u.MockInk}  | stretched x{u.StretchAspect:0.000}: [{u.StretchRect[0]:0.00}, {u.StretchRect[1]:0.00}, {u.StretchRect[2]:0.00}, {u.StretchRect[3]:0.00}] edge {u.StretchEdgeMean:0.00}/{u.StretchEdgeMax:0.00}"));
            }
        }
        return done;
    }

    /// <summary>Writes ArtSource/Kit/Icons.map.csv: one row per cut sprite that a family icon replaces.</summary>
    public static int Map()
    {
        List<Build.Entry> accepted = Build.Load();
        List<Use> uses = Run(null, quiet: true);
        var sb = new StringBuilder();
        sb.Append("old_sprite,screens,new_sprite,mock_rect_note,nine_ltrb,status\n");
        string Q(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
        int rows = 0;
        foreach (var group in uses.GroupBy(u => (u.Old, u.Icon)).OrderBy(g => g.Key.Icon).ThenBy(g => g.Key.Old))
        {
            Build.Entry e = accepted.First(a => a.Name == group.Key.Icon);
            IconDef d = Spec.Get(e.Name);
            var screens = group.Select(u => $"{u.Screen} {u.ScreenName}").Distinct();
            var notes = new List<string>();
            foreach (Use u in group)
            {
                if (u.Hidden || u.SpriteRect == null) { notes.Add($"{u.ScreenName}.{u.Node}: hidden alternate, same size as the visible one, keep its own position"); continue; }
                // The mockup's own ink at this place, and the Image alpha at which the sprite reads the same over
                // this ground. Only where the shapes agree well: on a poor fit the estimate is too light.
                string tint = d.Mode == "color" || u.MockInk == null || u.Iou < 0.93 ? "" : FormattableString.Invariant($" (mockup ink {u.MockInk}, alpha {u.Opacity:0.00} reads the same)");
                // A screen that already shows the family icon: is its rectangle the one for the sprite as it is now?
                string state = "";
                if (u.Swapped)
                {
                    double off = 0, offStretched = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        off = Math.Max(off, Math.Abs(u.NodeRect[k] - u.SpriteRect[k]));
                        offStretched = Math.Max(offStretched, u.StretchRect == null ? 99 : Math.Abs(u.NodeRect[k] - u.StretchRect[k]));
                    }
                    state = off <= 0.12 ? " [already swapped, rect up to date]"
                        : offStretched <= 0.12 ? " [already swapped, at the stretched rect]"
                        : FormattableString.Invariant($" [already swapped with rect {u.NodeRect[0]:0.##}, {u.NodeRect[1]:0.##}, {u.NodeRect[2]:0.##}, {u.NodeRect[3]:0.##}: update it, the sprite changed since]");
                }
                // Where this screen draws the icon with other proportions, the sprite stretched to them lands closer.
                string stretch = "";
                if (u.StretchRect != null && Math.Abs(u.StretchAspect - 1) > 0.02 && u.EdgeMean - u.StretchEdgeMean >= 0.12)
                    stretch = FormattableString.Invariant($" {{this screen draws it {(u.StretchAspect > 1 ? "wider" : "narrower")}: at its own proportions the outlines are {u.EdgeMean:0.00} px apart; stretched to [{u.StretchRect[0]:0.00}, {u.StretchRect[1]:0.00}, {u.StretchRect[2]:0.00}, {u.StretchRect[3]:0.00}] they are {u.StretchEdgeMean:0.00} px apart}}");
                notes.Add(FormattableString.Invariant($"{u.ScreenName}.{u.Node} rect [{u.SpriteRect[0]:0.00}, {u.SpriteRect[1]:0.00}, {u.SpriteRect[2]:0.00}, {u.SpriteRect[3]:0.00}]{tint}{state}{stretch}"));
            }
            string note = "use without \"shape\" at the rect given (whole texture, 2 px margin included): " + string.Join("; ", notes);
            if (!string.IsNullOrEmpty(e.Note)) note += ". " + e.Note;
            sb.Append($"{group.Key.Old},{Q(string.Join("; ", screens))},UI/Icons/{e.Name}.png,{Q(note)},,{e.Status}\n");
            rows++;
        }
        // Icons that replace nothing yet.
        foreach (Build.Entry e in accepted.Where(a => uses.All(u => u.Icon != a.Name)))
        {
            sb.Append($"-,{Q("not on a built screen yet")},UI/Icons/{e.Name}.png,{Q(string.IsNullOrEmpty(e.Note) ? "new" : e.Note)},,{e.Status}\n");
            rows++;
        }
        string file = Path.Combine(Spec.Repo, "ArtSource", "Kit", "Icons.map.csv");
        File.WriteAllText(file, sb.ToString());
        Console.WriteLine($"{rows} rows -> {file}");
        return 0;
    }
}

/// <summary>The rows this family adds to ArtSource/handoff.csv, written to the working folder for one append.</summary>
internal static class Handoff
{
    public static int Run()
    {
        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(Spec.Here, "accepted.json")), null, opts);
        List<Place.Use> uses = Place.Load();
        var sb = new StringBuilder();
        string Q(string v) => v.Contains(',') || v.Contains('"') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        int rows = 0;
        foreach (JsonNode n in root["icons"].AsArray())
        {
            string name = (string)n["name"], attempt = (string)n["attempt"], sheet = (string)n["sheet"], note = (string)n["note"];
            int attempts = n["attempts"] != null ? (int)n["attempts"] : 1;
            string fitted = Path.Combine(Spec.Work, "fitted", $"{name}_{attempt}.png");
            string info = Path.ChangeExtension(fitted, ".txt");
            using var bmp = new Bitmap(fitted);
            var screens = uses.Where(u => u.Icon == name).Select(u => u.Screen).Distinct().OrderBy(s => s).ToList();
            IconDef d = Spec.Get(name);
            if (!screens.Contains(d.Screen)) screens.Add(d.Screen);
            // Screens that show the icon without having a node for it (the table HUD behind the bidding panel and the game menu).
            if (n["alsoOn"] != null)
                foreach (JsonNode a in n["alsoOn"].AsArray()) if (!screens.Contains((string)a)) screens.Add((string)a);
            screens.Sort();
            string[] lines = File.ReadAllLines(info);
            string Val(string key) => lines.First(l => l.StartsWith(key + " ")).Substring(key.Length + 1);
            bool enclosed = double.Parse(Val("edge_max"), CultureInfo.InvariantCulture) > 2.0;
            string fit = enclosed
                ? $"fitted to {d.Screen}: outer outlines {Val("outer_mean")} px apart on average ({Val("outer_max")} px at the 98th percentile)"
                : $"fitted to {d.Screen}: outlines {Val("edge_mean")} px apart on average ({Val("edge_max")} px at the 98th percentile), overlap {Val("iou")}";
            string notes = $"Assets: UI/Icons/{name}.png. {fit}" + (string.IsNullOrEmpty(note) ? "" : ". " + note);
            sb.Append($"Icons/{name}.png,icon,{bmp.Width}x{bmp.Height},,{string.Join(" ", screens)},{Q($"Icons_Kit {sheet} {attempt} (contact sheet; {attempts} attempt{(attempts == 1 ? "" : "s")})")},review,{Q(notes)}\r\n");
            rows++;
        }
        string file = Path.Combine(Spec.Work, "handoff_rows.csv");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{rows} rows -> {file}");
        return 0;
    }
}

/// <summary>
/// Checks the exported sprites themselves: even size, clear border, margin around the ink, no stray specks,
/// no colour fringe (the colour under partly transparent pixels is the colour of the solid ones next to
/// them), and that the copy in ArtSource/Exports is the same file. Also writes every sprite on black and on
/// white into one overview image.
/// </summary>
internal static class Audit
{
    public static int Run()
    {
        string art = Path.Combine(Spec.Repo, "Assets", "_Project", "Art", "UI", "Icons");
        string exports = Path.Combine(Spec.Repo, "ArtSource", "Exports", "Icons");
        List<Build.Entry> list = Build.Load();
        int problems = 0;
        var tiles = new List<Img>();
        Console.WriteLine($"{"sprite",-24} {"size",-9} {"margin l,t,r,b",-16} {"parts",5} {"specks",6} {"fringe",7}  export copy");
        foreach (Build.Entry e in list)
        {
            string file = Path.Combine(art, e.Name + ".png");
            if (!File.Exists(file)) { Console.WriteLine($"{e.Name,-24} MISSING"); problems++; continue; }
            Img s = Img.Load(file);
            int w = s.W, h = s.H;
            var notes = new List<string>();
            if (w % 2 != 0 || h % 2 != 0) notes.Add("odd size");
            // Border must be fully transparent.
            float border = 0;
            for (int x = 0; x < w; x++) border = Math.Max(border, Math.Max(s.A[x], s.A[(h - 1) * w + x]));
            for (int y = 0; y < h; y++) border = Math.Max(border, Math.Max(s.A[y * w], s.A[y * w + w - 1]));
            if (border > 0) notes.Add($"border alpha {border:0.00}");
            // Margin: first and last row/column with any alpha.
            int l = w, t = h, r = -1, b = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (s.A[y * w + x] > 0f) { if (x < l) l = x; if (x > r) r = x; if (y < t) t = y; if (y > b) b = y; }
            int mr = w - 1 - r, mb = h - 1 - b;
            if (l < 2 || t < 2 || mr < 2 || mb < 2) notes.Add("margin under 2 px");
            if (l > 3 || t > 3 || mr > 3 || mb > 3) notes.Add("margin over 3 px");
            // Connected parts of the ink (alpha above 10 %); a part smaller than 12 px is a speck.
            var seen = new bool[w * h];
            int parts = 0, specks = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < seen.Length; i++)
            {
                if (seen[i] || s.A[i] <= 0.1f) continue;
                int area = 0; seen[i] = true; stack.Push(i);
                while (stack.Count > 0)
                {
                    int j = stack.Pop(); area++;
                    int x = j % w, y = j / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            int k = yy * w + xx;
                            if (seen[k] || s.A[k] <= 0.1f) continue;
                            seen[k] = true; stack.Push(k);
                        }
                }
                parts++;
                if (area < 12) specks++;
            }
            if (specks > 0) notes.Add($"{specks} specks");
            // Fringe: the largest difference (sRGB, 0-255) between a partly transparent pixel's colour and the
            // nearest solid pixel's colour within 2 px.
            double fringe = 0;
            (int x, int y, float a) fringeAt = (0, 0, 0f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (s.A[i] <= 0.02f || s.A[i] >= 0.98f) continue;
                    double best = double.MaxValue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            int k = yy * w + xx;
                            if (s.A[k] < 0.98f) continue;
                            double d = Math.Max(Math.Abs(Img.ToSrgb(s.R[i]) - Img.ToSrgb(s.R[k])), Math.Max(Math.Abs(Img.ToSrgb(s.G[i]) - Img.ToSrgb(s.G[k])), Math.Abs(Img.ToSrgb(s.B[i]) - Img.ToSrgb(s.B[k])))) * 255;
                            if (d < best) best = d;
                        }
                    if (best != double.MaxValue && best > fringe) { fringe = best; fringeAt = (x, y, s.A[i]); }
                }
            if (fringe > 1.5) Console.WriteLine($"   largest fringe at {fringeAt.x},{fringeAt.y} (alpha {fringeAt.a:0.00}, colour {Img.Hex(s.R[fringeAt.y * w + fringeAt.x], s.G[fringeAt.y * w + fringeAt.x], s.B[fringeAt.y * w + fringeAt.x])})");
            IconDef d0 = Spec.Get(e.Name);
            if (d0.Mode != "color" && fringe > 1.5) notes.Add("colour fringe");
            string copy = Path.Combine(exports, e.Name + ".png");
            bool same = File.Exists(copy) && File.ReadAllBytes(copy).AsSpan().SequenceEqual(File.ReadAllBytes(file));
            if (!same) notes.Add("export copy differs");
            string meta = file + ".meta";
            if (!File.Exists(meta)) notes.Add("no .meta yet (run KitImporter.Apply)");
            else if (!File.ReadAllText(meta).Contains("spritePixelsToUnits: 200")) notes.Add("ppu is not 200");
            if (notes.Count > 0) problems++;
            Console.WriteLine($"{e.Name,-24} {w + "x" + h,-9} {l + "," + t + "," + mr + "," + mb,-16} {parts,5} {specks,6} {fringe,7:0.0}  {(same ? "same" : "DIFFERS")}{(notes.Count > 0 ? "   <-- " + string.Join("; ", notes) : "")}");

            // Overview tile: on black (left) and on white (right), at the sprite's own pixels.
            var tile = new Img(w * 2 + 6, h);
            tile.Fill(0.5f, 0f, 0.5f);
            var onBlack = new Img(w, h); onBlack.Fill(0, 0, 0); onBlack.Over(s, 0, 0);
            var onWhite = new Img(w, h); onWhite.Fill(1, 1, 1); onWhite.Over(s, 0, 0);
            // On black a near-black icon would vanish: show its alpha as light grey instead, so halo and specks show.
            if (d0.Mode != "color" && (d0.Ink ?? Spec.FamilyInk) == Spec.FamilyInk)
                for (int i = 0; i < onBlack.Count; i++) { float v = Img.ToLinear(s.A[i] * 0.85f); onBlack.R[i] = v; onBlack.G[i] = v; onBlack.B[i] = v; }
            tile.Paste(onBlack, 0, 0); tile.Paste(onWhite, w + 6, 0);
            tiles.Add(tile);
        }
        // Overview, shelf layout.
        const int MaxW = 1900, Gap = 10;
        var pos = new List<(int x, int y)>();
        int cx = Gap, cy = Gap, rowH = 0, sheetW = 0;
        foreach (Img t in tiles)
        {
            if (cx + t.W + Gap > MaxW && cx > Gap) { cx = Gap; cy += rowH + Gap; rowH = 0; }
            pos.Add((cx, cy)); cx += t.W + Gap; rowH = Math.Max(rowH, t.H); sheetW = Math.Max(sheetW, cx);
        }
        var sheet = new Img(sheetW, cy + rowH + Gap);
        sheet.Fill(0.2f, 0.2f, 0.2f);
        for (int i = 0; i < tiles.Count; i++) sheet.Paste(tiles[i], pos[i].x, pos[i].y);
        string outFile = Path.Combine(Spec.Work, "review", "alpha_black_white.png");
        sheet.Save(outFile);
        Console.WriteLine($"{list.Count} sprites, {problems} with remarks; overview {outFile} ({sheet.W}x{sheet.H}; each pair: alpha on black, sprite on white)");
        return problems == 0 ? 0 : 1;
    }
}
