using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ArtTool;

namespace KitPanels
{
    /// <summary>Colour matching for pills and discs: by row against the mockup, and by distance from the centre.</summary>
    internal static partial class Program
    {
        /// <summary>
        /// A pill that stretches sideways has one colour per row. This measures that colour in the
        /// extraction (the straight middle of the piece) and on the mockup (columns free of text),
        /// and adds the difference, smoothed, to every pixel of the row: the top-to-bottom shading
        /// becomes the mockup's, while the clean outline, rim and alpha of the extraction stay.
        /// </summary>
        private static int RowMatch(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            Img mock = Img.Load(MockupPath(o.Need("screen")));
            double y0 = o.Num("y0", 0), y1 = o.Num("y1", 0);
            double[] sx = Range(o.Get("sx", "0.4-0.6"));
            double[] trust = Range(o.Get("trust", "0.1-0.9"));
            double smooth = o.Num("smooth", 0.04);
            var cols = new List<int>();
            foreach (string part in o.Need("x").Split(','))
            {
                double[] r = Range(part);
                for (int x = (int)r[0]; x <= (int)r[1]; x++) cols.Add(x);
            }

            int h = m.H;
            var ps = new double[h][];   // sprite profile
            var pm = new double[h][];   // mockup profile at the same relative height
            for (int r = 0; r < h; r++)
            {
                var px = new List<(byte r, byte g, byte b)>();
                for (int x = (int)(m.W * sx[0]); x < (int)(m.W * sx[1]); x++)
                {
                    int i = r * m.W + x;
                    if (m.A[i] >= 0.99f) px.Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i])));
                }

                if (px.Count > 0) { (int rr, int gg, int bb) c = Pct(px, 50); ps[r] = new double[] { c.rr, c.gg, c.bb }; }

                // Mockup rows under this sprite row (area average over the rows it covers, bilinear at the ends).
                double my = y0 + (r + 0.5) / h * (y1 - y0) - 0.5;
                int ya = (int)Math.Floor(my);
                double f = my - ya;
                var acc = new double[3];
                foreach (int x in cols)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        int yy = Math.Clamp(ya + k, 0, mock.H - 1);
                        double wgt = k == 0 ? 1 - f : f;
                        int i = yy * mock.W + Math.Clamp(x, 0, mock.W - 1);
                        acc[0] += S8(mock.R[i]) * wgt; acc[1] += S8(mock.G[i]) * wgt; acc[2] += S8(mock.B[i]) * wgt;
                    }
                }

                pm[r] = new[] { acc[0] / cols.Count, acc[1] / cols.Count, acc[2] / cols.Count };
            }

            // Offsets, trusted only away from the rim rows; held constant beyond.
            int ra = (int)Math.Round(h * trust[0]), rb = Math.Max(ra + 1, (int)Math.Round(h * trust[1]));
            var d = new double[h][];
            for (int r = 0; r < h; r++)
            {
                int q = Math.Clamp(r, ra, rb - 1);
                while (ps[q] == null && q < rb - 1) q++;
                d[r] = ps[q] == null ? new double[3] : new[] { pm[q][0] - ps[q][0], pm[q][1] - ps[q][1], pm[q][2] - ps[q][2] };
            }

            int rad = Math.Max(0, (int)Math.Round(h * smooth));
            var ds = new double[h][];
            for (int r = 0; r < h; r++)
            {
                var acc = new double[3]; int n = 0;
                for (int k = -rad; k <= rad; k++)
                {
                    int q = Math.Clamp(r + k, 0, h - 1);
                    acc[0] += d[q][0]; acc[1] += d[q][1]; acc[2] += d[q][2]; n++;
                }

                ds[r] = new[] { acc[0] / n, acc[1] / n, acc[2] / n };
            }

            float[][] ch = { m.R, m.G, m.B };
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = r * m.W + x;
                    if (m.A[i] <= 0f) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        double v = Img.ToSrgb01(ch[c][i]) * 255.0 + ds[r][c];
                        ch[c][i] = Img.ToLinear((float)Math.Clamp(v / 255.0, 0, 1));
                    }
                }
            }

            m.Save(o.Need("out"));
            string At(int r) => $"{(int)Math.Round(ds[r][0]),4}{(int)Math.Round(ds[r][1]),4}{(int)Math.Round(ds[r][2]),4}";
            Console.WriteLine($"{o.Need("out")}  rows matched to {Path.GetFileName(MockupPath(o.Need("screen")))} x {o.Need("x")} y {y0}-{y1}; offsets top{At(ra)}  middle{At(h / 2)}  bottom{At(rb - 1)}");
            return 0;
        }

        private static double[] Range(string s)
        {
            string[] p = s.Split('-');
            double a = double.Parse(p[0], CultureInfo.InvariantCulture);
            return new[] { a, p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : a };
        }

        /// <summary>
        /// The colour and alpha of a round piece by distance from its centre (median over all
        /// directions, so tick marks and highlights on one side drop out). With --out the piece
        /// is redrawn from that profile: exactly round and the same all the way round, as a ring
        /// that is filled radially at runtime has to be. --rmin/--rmax cut the profile to a ring.
        /// </summary>
        private static int Radial(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] c = o.Nums("center", 2, new[] { m.W / 2.0, m.H / 2.0 });
            int nr = (int)Math.Ceiling(Math.Max(m.W, m.H) / 2.0) + 2;
            var bins = new List<(byte r, byte g, byte b)>[nr];
            var alphas = new List<float>[nr];
            for (int k = 0; k < nr; k++) { bins[k] = new List<(byte, byte, byte)>(); alphas[k] = new List<float>(); }
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    double dx = x + 0.5 - c[0], dy = y + 0.5 - c[1];
                    int k = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
                    if (k >= nr) continue;
                    int i = y * m.W + x;
                    alphas[k].Add(m.A[i]);
                    if (m.A[i] >= 0.5f) bins[k].Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i])));
                }
            }

            var col = new double[nr][];
            var alp = new double[nr];
            for (int k = 0; k < nr; k++)
            {
                alphas[k].Sort();
                alp[k] = alphas[k].Count > 0 ? alphas[k][alphas[k].Count / 2] : 0;
                (int r, int g, int b) p = bins[k].Count > 0 ? Pct(bins[k], 50) : (0, 0, 0);
                col[k] = new double[] { p.r, p.g, p.b };
            }

            // The very centre has too few pixels of its own: it takes the first ring that has some.
            if (alphas[0].Count < 4 && nr > 2) { alp[0] = alp[1]; col[0] = col[1]; }

            double rmin = o.Num("rmin", -1), rmax = o.Num("rmax", 1e9);
            if (!o.Has("out"))
            {
                var sb = new StringBuilder();
                for (int k = 0; k < nr; k++) sb.Append($"{k}:{(int)col[k][0]:X2}{(int)col[k][1]:X2}{(int)col[k][2]:X2}/{(int)Math.Round(alp[k] * 255)} ");
                Console.WriteLine(sb.ToString());
                return 0;
            }

            // Colours where the profile is transparent come from the nearest visible radius.
            int first = Array.FindIndex(alp, a => a >= 0.5), last = Array.FindLastIndex(alp, a => a >= 0.5);
            var n = new Img(m.W, m.H, 0f);
            double Lerp(double[] arr, double r) { int k = Math.Clamp((int)Math.Floor(r), 0, nr - 2); double f = Math.Clamp(r - k, 0, 1); return arr[k] + (arr[k + 1] - arr[k]) * f; }
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    double dx = x + 0.5 - c[0], dy = y + 0.5 - c[1];
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    int i = y * m.W + x;
                    double a = Lerp(alp, r);
                    // Hard limits of the ring, anti-aliased over one pixel.
                    a *= Math.Clamp(r - rmin + 0.5, 0, 1) * Math.Clamp(rmax - r + 0.5, 0, 1);
                    double rc = Math.Clamp(r, Math.Max(first, rmin), Math.Min(last, rmax));
                    int k = Math.Clamp((int)Math.Floor(rc), 0, nr - 2);
                    double f = Math.Clamp(rc - k, 0, 1);
                    if (alp[k] < 0.5) { k = k + 1; f = 0; }
                    int k1 = alp[Math.Min(nr - 1, k + 1)] >= 0.5 ? Math.Min(nr - 1, k + 1) : k;
                    n.R[i] = Img.ToLinear((float)((col[k][0] + (col[k1][0] - col[k][0]) * f) / 255.0));
                    n.G[i] = Img.ToLinear((float)((col[k][1] + (col[k1][1] - col[k][1]) * f) / 255.0));
                    n.B[i] = Img.ToLinear((float)((col[k][2] + (col[k1][2] - col[k][2]) * f) / 255.0));
                    n.A[i] = (float)Math.Clamp(a, 0, 1);
                    if (n.A[i] <= 0.002f) { n.A[i] = 0; n.R[i] = 0; n.G[i] = 0; n.B[i] = 0; }
                }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  redrawn round about {c[0]:0.#},{c[1]:0.#}; visible radii {first}-{last}{(o.Has("rmin") || o.Has("rmax") ? $", kept {Math.Max(first, rmin):0.#}-{Math.Min(last, rmax):0.#}" : "")}");
            return 0;
        }
    }
}

namespace KitPanels
{
    internal static partial class Program
    {
        /// <summary>
        /// Several kit sprites placed at their mockup rectangles over one mockup view, back to
        /// front: the mockup beside the composite, to judge a whole element group at once (a
        /// table with its header and highlighted row, a seat with plate, pill and ring).
        /// Sprite paths without a folder are looked up under Assets/_Project/Art/UI.
        /// </summary>
        private static int Place(Opts o)
        {
            string screen = o.Need("screen");
            double[] view = o.Nums("view", 4);
            double zoom = o.Num("zoom", 2), unit = o.Num("unit", 2.3);
            Img mock = MockupView(screen, view, zoom);
            Img comp = mock.Clone();
            // --clear x,y,w,h;...: flat patches painted first (the fill colour sampled beside them),
            // to hide the mockup's own copy of an element before the sprite is drawn.
            foreach (string item in o.Need("items").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = item.Split('|');
                string path = p[0].Trim();
                if (!File.Exists(path))
                {
                    // This kit's folders first: the old cut sprites in UI/Common share some names.
                    string ui = Path.Combine(_repo, "Assets", "_Project", "Art", "UI");
                    string hit = new[] { "Panels", "Table" }.Select(d => Path.Combine(ui, d, Path.GetFileName(path))).FirstOrDefault(File.Exists)
                        ?? Directory.GetFiles(ui, Path.GetFileName(path), SearchOption.AllDirectories).FirstOrDefault();
                    path = hit ?? throw new FileNotFoundException(path);
                }

                Img sprite = Img.Load(path);
                int[] nine = p[1].Trim() == "-" ? null : p[1].Split(',').Select(v => (int)Math.Round(double.Parse(v, CultureInfo.InvariantCulture))).ToArray();
                double[] r = p[2].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                float opacity = p.Length > 3 ? float.Parse(p[3], CultureInfo.InvariantCulture) : 1f;
                Img drawn = Render(sprite, nine, unit, zoom, r[2], r[3]);
                comp.Over(drawn, (int)Math.Round((r[0] - view[0]) * zoom), (int)Math.Round((r[1] - view[1]) * zoom), opacity);
            }

            var tiles = new List<Img> { mock, comp };
            Img sheet = o.Has("stack") ? Stack(tiles, 6) : Tiles(tiles, 6);
            sheet.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  zoom {zoom:0.##}");
            return 0;
        }
    }
}

namespace KitPanels
{
    internal static partial class Program
    {
        /// <summary>
        /// Redraws a flat piece (one colour inside, a rim along its edge) in exact geometry at
        /// the export size: the colour of every pixel depends only on how far it is inside the
        /// edge, read from the extraction as the median over everything at that depth. Keeps
        /// ChatGPT's fill and rim, drops its wobble, and lets the proportions be the mockup's
        /// when the extraction came back wider or rounder than the element really is.
        /// </summary>
        private static int Reshape(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string type = o.Get("type", "ellipse");
            int w = o.Int("w", m.W), h = o.Int("h", m.H);
            double sradius = o.Num("sradius", type == "pill" ? m.H / 2.0 : 0);
            var src = new Shape(type, 0, 0, m.W, m.H, sradius);
            // Output pixels per source pixel, for depths: the mean of both directions unless given.
            double scale = o.Num("scale", Math.Sqrt((double)w / m.W * h / m.H));
            int nb = (int)Math.Ceiling(Math.Min(m.W, m.H) / 2.0) + 2;
            var bins = new List<(byte r, byte g, byte b)>[nb];
            for (int k = 0; k < nb; k++) bins[k] = new List<(byte, byte, byte)>();
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] < 0.99f) continue;
                    double d = -src.Sdf(x + 0.5, y + 0.5);
                    int k = (int)Math.Floor(d);
                    if (k < 0 || k >= nb) continue;
                    bins[k].Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i])));
                }
            }

            var col = new double[nb][];
            int lastGood = -1;
            for (int k = 0; k < nb; k++)
            {
                if (bins[k].Count >= 8) { (int r, int g, int b) p = Pct(bins[k], 50); col[k] = new double[] { p.r, p.g, p.b }; lastGood = k; }
            }

            if (lastGood < 0) throw new InvalidOperationException("nothing opaque in the piece");
            int firstGood = Array.FindIndex(col, c => c != null);
            for (int k = 0; k < nb; k++)
            {
                if (col[k] != null) continue;
                int q = k < firstGood ? firstGood : k;
                while (q > 0 && col[q] == null) q--;
                col[k] = col[q] ?? col[firstGood];
            }

            double radius = o.Num("radius", type == "pill" ? h / 2.0 : sradius * scale);
            var dst = new Shape(type, 0, 0, w, h, radius);
            var n = new Img(w, h, 0f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double sd = dst.Sdf(x + 0.5, y + 0.5);
                    float a = (float)Math.Clamp(0.5 - sd, 0.0, 1.0);
                    if (a <= 0f) continue;
                    double d = Math.Max(0, -sd) / scale - 0.5;       // depth in source pixels, bin centres at k + 0.5
                    int k = Math.Clamp((int)Math.Floor(d), 0, nb - 2);
                    double f = Math.Clamp(d - k, 0, 1);
                    n.R[i] = Img.ToLinear((float)((col[k][0] + (col[k + 1][0] - col[k][0]) * f) / 255.0));
                    n.G[i] = Img.ToLinear((float)((col[k][1] + (col[k + 1][1] - col[k][1]) * f) / 255.0));
                    n.B[i] = Img.ToLinear((float)((col[k][2] + (col[k + 1][2] - col[k][2]) * f) / 255.0));
                    n.A[i] = a;
                }
            }

            n.Save(o.Need("out"));
            var sb = new StringBuilder();
            for (int k = 0; k < Math.Min(nb, 16); k++) sb.Append($"{k}:{(int)col[k][0]:X2}{(int)col[k][1]:X2}{(int)col[k][2]:X2} ");
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H} -> {type} {w}x{h}, depth scale {scale:0.000}; colour by depth (source px): {sb}... fill {(int)col[lastGood][0]:X2}{(int)col[lastGood][1]:X2}{(int)col[lastGood][2]:X2}");
            return 0;
        }
    }
}

namespace KitPanels
{
    internal static partial class Program
    {
        /// <summary>
        /// Counts visible pixels whose blue is well above their green: nothing in this kit's
        /// palette (ivory, peach, tan, gold, orange, crimson, dark brown) looks like that, so
        /// such pixels are what is left of the magenta ground a piece was generated on.
        /// </summary>
        private static int Fringe(Opts o)
        {
            int limit = o.Int("limit", 18), total = 0;
            foreach (string p in o.Pos)
            {
                Img m = Img.Load(p);
                int n = 0, worst = 0, wx = -1, wy = -1;
                for (int i = 0; i < m.Count; i++)
                {
                    if (m.A[i] < 0.05f) continue;
                    int d = S8(m.B[i]) - S8(m.G[i]);
                    // Blue above green and at least a third of red: pink or purple. (A crimson has
                    // almost no blue; its few levels of blue over green do not count.)
                    if (d <= limit || S8(m.R[i]) <= S8(m.G[i]) || S8(m.B[i]) * 3 < S8(m.R[i])) continue;
                    n++;
                    if (d > worst) { worst = d; wx = i % m.W; wy = i / m.W; }
                }

                total += n;
                Console.WriteLine($"{Path.GetFileName(p),-40} {m.W}x{m.H}  {(n == 0 ? "clean" : $"{n} pink pixels, worst at {wx},{wy} ({Hex(m, wx, wy)}, blue - green {worst})")}");
            }

            return total == 0 ? 0 : 3;
        }
    }
}

namespace KitPanels
{
    internal static partial class Program
    {
        /// <summary>Colours of a ring on a mockup, by direction: for reading a disc's edge line or rim.</summary>
        private static int RingProbe(Opts o)
        {
            Img mock = Img.Load(MockupPath(o.Need("screen")));
            double[] c = o.Nums("center", 2);
            double r0 = o.Num("r0", 0), r1 = o.Num("r1", 0);
            int n = o.Int("n", 12);
            for (int k = 0; k < n; k++)
            {
                double ang = 2 * Math.PI * k / n;        // 0 = up, clockwise
                var px = new List<(byte r, byte g, byte b)>();
                for (double r = r0; r <= r1 + 1e-6; r += 0.25)
                {
                    for (double da = -0.12; da <= 0.12; da += 0.04)
                    {
                        double x = c[0] + Math.Sin(ang + da) * r, y = c[1] - Math.Cos(ang + da) * r;
                        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
                        if (xi < 0 || yi < 0 || xi >= mock.W || yi >= mock.H) continue;
                        int i = yi * mock.W + xi;
                        px.Add((S8(mock.R[i]), S8(mock.G[i]), S8(mock.B[i])));
                    }
                }

                (int r, int g, int b) lo = Pct(px, 8), mid = Pct(px, 50), hi = Pct(px, 92);
                Console.WriteLine($"{(int)Math.Round(360.0 * k / n),4} deg  dark #{lo.r:X2}{lo.g:X2}{lo.b:X2}  median #{mid.r:X2}{mid.g:X2}{mid.b:X2}  bright #{hi.r:X2}{hi.g:X2}{hi.b:X2}");
            }

            return 0;
        }

        /// <summary>
        /// Recolours the rim of a round piece (between two radii from the image centre) with a
        /// top-to-bottom shading, as a rim that catches light from above has. Alpha is kept; the
        /// change fades in over one pixel at the inner radius so the step to the body stays soft.
        /// </summary>
        private static int RingShade(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double rin = o.Num("rin", 0), rout = o.Num("rout", 1e9);
            double[] top = HexBytes(o.Need("top")), bottom = HexBytes(o.Need("bottom"));
            double[] side = o.Has("side") ? HexBytes(o.Get("side")) : new[] { (top[0] + bottom[0]) / 2, (top[1] + bottom[1]) / 2, (top[2] + bottom[2]) / 2 };
            double cx = m.W / 2.0, cy = m.H / 2.0;
            float[][] ch = { m.R, m.G, m.B };
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] <= 0f) continue;
                    double dx = x + 0.5 - cx, dy = y + 0.5 - cy, r = Math.Sqrt(dx * dx + dy * dy);
                    if (r < rin - 0.5 || r > rout + 1.5) continue;
                    double wgt = Math.Clamp(r - rin + 0.5, 0, 1);
                    double t = r > 1e-6 ? dy / r : 0;      // -1 at the top, 0 at the sides, +1 at the bottom
                    for (int c = 0; c < 3; c++)
                    {
                        double target = t < 0 ? side[c] + (top[c] - side[c]) * -t : side[c] + (bottom[c] - side[c]) * t;
                        double v = Img.ToSrgb01(ch[c][i]) * 255.0;
                        ch[c][i] = Img.ToLinear((float)Math.Clamp((v + (target - v) * wgt) / 255.0, 0, 1));
                    }
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  rim {rin}-{rout} px shaded {o.Need("top")} (top) to {o.Need("bottom")} (bottom)");
            return 0;
        }
    }
}

namespace KitPanels
{
    internal static partial class Program
    {
        /// <summary>
        /// Finds the drop (mockup px, downward) and opacity with which a round shadow sprite,
        /// scaled so that its disc (--disc, in shadow-sprite pixels) is as large as the piece
        /// (--size, mockup px), best explains what the mockup shows in a band just outside the
        /// piece. The ground colour is read further out. For the engineers' shadowDrop and
        /// shadowOpacity; an estimate, to be confirmed in a capture.
        /// </summary>
        private static int ShadowFit(Opts o)
        {
            Img mock = Img.Load(MockupPath(o.Need("screen")));
            Img sh = Img.Load(o.Need("shadow"));
            double[] c = o.Nums("center", 2);
            double size = o.Num("size", 0), disc = o.Num("disc", sh.W);
            double r0 = size / 2 + o.Num("r0", 1.0), r1 = size / 2 + o.Num("r1", 7.0);
            double k = disc / size;                                   // shadow px per mockup px
            // Radial alpha of the shadow sprite, and its colour.
            int nr = sh.W / 2 + 2;
            var alpha = new double[nr]; var cnt = new int[nr];
            double sr = 0, sg = 0, sb = 0, sw = 0;
            for (int y = 0; y < sh.H; y++)
                for (int x = 0; x < sh.W; x++)
                {
                    int i = y * sh.W + x;
                    double dx = x + 0.5 - sh.W / 2.0, dy = y + 0.5 - sh.H / 2.0;
                    int q = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
                    if (q >= nr) continue;
                    alpha[q] += sh.A[i]; cnt[q]++;
                    sr += sh.R[i] * sh.A[i]; sg += sh.G[i] * sh.A[i]; sb += sh.B[i] * sh.A[i]; sw += sh.A[i];
                }
            for (int q = 0; q < nr; q++) alpha[q] = cnt[q] > 0 ? alpha[q] / cnt[q] : 0;
            double[] scol = { sr / sw, sg / sw, sb / sw };           // linear
            double A(double r) { double q = r * k; int a = (int)Math.Floor(q); return a + 1 >= nr ? 0 : alpha[a] + (alpha[a + 1] - alpha[a]) * (q - a); }

            // Ground colour: median of a ring well outside the shadow.
            var far = new List<(byte r, byte g, byte b)>();
            for (int s = 0; s < 360; s += 3)
            {
                double a = s * Math.PI / 180, r = size / 2 + o.Num("far", 12.0);
                int i = (int)Math.Floor(c[1] - Math.Cos(a) * r) * mock.W + (int)Math.Floor(c[0] + Math.Sin(a) * r);
                far.Add((S8(mock.R[i]), S8(mock.G[i]), S8(mock.B[i])));
            }
            (int r, int g, int b) gnd = Pct(far, 60);
            double[] ground = { Img.ToLinear((byte)gnd.r), Img.ToLinear((byte)gnd.g), Img.ToLinear((byte)gnd.b) };

            var pts = new List<(double x, double y, double[] m)>();
            for (int s = 0; s < 360; s += 6)
                for (double r = r0; r <= r1 + 1e-9; r += 0.5)
                {
                    double a = s * Math.PI / 180, x = c[0] + Math.Sin(a) * r, y = c[1] - Math.Cos(a) * r;
                    int i = (int)Math.Floor(y) * mock.W + (int)Math.Floor(x);
                    pts.Add((x, y, new double[] { mock.R[i], mock.G[i], mock.B[i] }));
                }

            double best = double.MaxValue, bestD = 0, bestK = 0;
            for (double d = 0; d <= 6.01; d += 0.25)
                for (double op = 0.02; op <= 0.9; op += 0.02)
                {
                    double e = 0;
                    foreach (var p in pts)
                    {
                        double dx = p.x - c[0], dy = p.y - (c[1] + d);
                        double a = A(Math.Sqrt(dx * dx + dy * dy)) * op;
                        for (int ch = 0; ch < 3; ch++)
                        {
                            double v = ground[ch] * (1 - a) + scol[ch] * a;
                            double diff = (Img.ToSrgb01((float)v) - Img.ToSrgb01((float)p.m[ch])) * 255;
                            e += diff * diff;
                        }
                    }

                    e = Math.Sqrt(e / (pts.Count * 3));
                    if (e < best) { best = e; bestD = d; bestK = op; }
                }

            double none = 0;
            foreach (var p in pts)
                for (int ch = 0; ch < 3; ch++)
                {
                    double diff = (Img.ToSrgb01((float)ground[ch]) - Img.ToSrgb01((float)p.m[ch])) * 255;
                    none += diff * diff;
                }
            none = Math.Sqrt(none / (pts.Count * 3));
            Console.WriteLine($"ground #{gnd.r:X2}{gnd.g:X2}{gnd.b:X2}; shadow colour {Img.Hex((float)scol[0], (float)scol[1], (float)scol[2])}; best fit: drop {bestD:0.##} mockup px, opacity {bestK:0.##}  (rms {best:0.0} levels; without any shadow {none:0.0}); spread for a node of this size: {size * (sh.W - disc) / 2 / disc:0.0} mockup px");
            return 0;
        }
    }
}
