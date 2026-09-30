using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ArtTool;

namespace KitPanels
{
    /// <summary>Clean-up commands added while the kit was being made: fringes, textures, symmetry, curves.</summary>
    internal static partial class Program
    {
        private static int TileCmd(Opts o)
        {
            int maxh = o.Int("maxh", 160), cols = o.Int("cols", 0);
            var tiles = new List<Img>();
            foreach (string f in o.Pos)
            {
                Img m = Img.Load(f);
                if (m.H > maxh) m = m.Resize(Math.Max(1, (int)Math.Round(m.W * (double)maxh / m.H)), maxh);
                Img t = Flat(m.W, m.H, o.Get("bg", "#808080"));
                t.Over(m, 0, 0);
                tiles.Add(t);
            }

            Img sheet;
            if (cols > 0 && tiles.Count > cols)
            {
                var rows = new List<Img>();
                for (int k = 0; k < tiles.Count; k += cols) rows.Add(Tiles(tiles.GetRange(k, Math.Min(cols, tiles.Count - k)), 8, "#404040"));
                sheet = Stack(rows, 0, "#404040");
            }
            else
            {
                sheet = Tiles(tiles, 8, "#404040");
            }

            sheet.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  {tiles.Count} images");
            return 0;
        }

        /// <summary>
        /// Pixels within <c>px</c> of the transparent outside take their colour from the pixels
        /// just inside them; alpha is untouched. ChatGPT's magenta ground leaves a pink or dark
        /// line on the outermost pixels that keying alone cannot remove.
        /// </summary>
        private static int Defringe(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int n = DefringeImg(m, o.Int("px", 2));
            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {n} edge pixels recoloured from inside ({o.Int("px", 2)} px)");
            return 0;
        }

        private static int DefringeImg(Img m, int px)
        {
            var outside = new float[m.Count];
            for (int i = 0; i < m.Count; i++) outside[i] = m.A[i] < 0.5f ? 1f : 0f;
            float[] near = Inpaint.Dilate(outside, m.W, m.H, px);
            // A trimmed piece touches the image border: what lies beyond the border is outside too.
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    if (x < px || y < px || x >= m.W - px || y >= m.H - px) near[y * m.W + x] = 1f;
                }
            }

            var isEdge = new bool[m.Count];
            var done = new bool[m.Count];
            int n = 0;
            for (int i = 0; i < m.Count; i++)
            {
                isEdge[i] = near[i] > 0.5f && m.A[i] > 0f;
                done[i] = !isEdge[i] && m.A[i] >= 0.99f;      // a clean pixel inside: a colour source
                if (isEdge[i]) n++;
            }

            // The colour grows outward from the nearest pixels inside, one ring at a time (not a
            // smooth fill: around a thin outline that would pull the panel's fill into the edge).
            for (int pass = 0; pass < px + 6; pass++)
            {
                var next = new List<(int i, float r, float g, float b)>();
                for (int y = 0; y < m.H; y++)
                {
                    for (int x = 0; x < m.W; x++)
                    {
                        int i = y * m.W + x;
                        if (done[i] || !isEdge[i]) continue;
                        float r = 0, g = 0, b = 0, wsum = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if ((dx == 0 && dy == 0) || xx < 0 || yy < 0 || xx >= m.W || yy >= m.H) continue;
                                int j = yy * m.W + xx;
                                if (!done[j]) continue;
                                float wgt = dx == 0 || dy == 0 ? 1f : 0.5f;
                                r += m.R[j] * wgt; g += m.G[j] * wgt; b += m.B[j] * wgt; wsum += wgt;
                            }
                        }

                        if (wsum > 0f) next.Add((i, r / wsum, g / wsum, b / wsum));
                    }
                }

                if (next.Count == 0) break;
                foreach ((int i, float r, float g, float b) in next) { m.R[i] = r; m.G[i] = g; m.B[i] = b; done[i] = true; }
            }

            for (int i = 0; i < m.Count; i++) if (m.A[i] <= 0f) { m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; }
            return n;
        }

        /// <summary>
        /// Replaces everything that is only fill (within <c>thr</c> levels of the fill colour and
        /// more than <c>grow</c> pixels from anything else) with the exact fill colour: removes a
        /// paper texture that a 9-slice middle cannot carry, leaving ornaments and outline alone.
        /// </summary>
        private static int FlatFill(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double thr = o.Num("thr", 8);
            int grow = o.Int("grow", 3);
            bool any = o.Has("any");   // also recolour the partly transparent edge pixels (plain shapes that get an exact outline afterwards)
            (int r, int g, int b) fill;
            if (o.Has("fill"))
            {
                double[] f = HexBytes(o.Get("fill"));
                fill = ((int)f[0], (int)f[1], (int)f[2]);
            }
            else
            {
                var mid = new List<(byte r, byte g, byte b)>();
                for (int y = m.H * 2 / 5; y < m.H * 3 / 5; y += 2)
                {
                    for (int x = m.W * 2 / 5; x < m.W * 3 / 5; x += 2)
                    {
                        int i = y * m.W + x;
                        if (m.A[i] >= 0.99f) mid.Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i])));
                    }
                }

                fill = Pct(mid, 50);
            }

            var busy = new float[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                if (m.A[i] < 0.99f && !any) { busy[i] = 1f; continue; }
                double d = Math.Max(Math.Abs(S8(m.R[i]) - fill.r), Math.Max(Math.Abs(S8(m.G[i]) - fill.g), Math.Abs(S8(m.B[i]) - fill.b)));
                busy[i] = d > thr ? 1f : 0f;
            }

            // Lone pixels over the threshold are texture, not ornament: keep only busy pixels with busy neighbours.
            var solid = new float[m.Count];
            for (int y = 1; y < m.H - 1; y++)
            {
                for (int x = 1; x < m.W - 1; x++)
                {
                    int i = y * m.W + x;
                    if (busy[i] < 0.5f) continue;
                    int k = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (busy[i + dy * m.W + dx] > 0.5f) k++;
                    solid[i] = k >= 5 ? 1f : 0f;
                }
            }

            // Small islands of busy pixels are texture too: drop blobs under --min pixels.
            int minArea = o.Int("min", 150);
            {
                var seen = new bool[m.Count];
                var stack = new Stack<int>();
                var blob = new List<int>();
                for (int s0 = 0; s0 < m.Count; s0++)
                {
                    if (solid[s0] < 0.5f || seen[s0]) continue;
                    blob.Clear();
                    stack.Push(s0); seen[s0] = true;
                    while (stack.Count > 0)
                    {
                        int i = stack.Pop();
                        blob.Add(i);
                        int x = i % m.W, y = i / m.W;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= m.W || yy >= m.H) continue;
                                int j = yy * m.W + xx;
                                if (solid[j] > 0.5f && !seen[j]) { seen[j] = true; stack.Push(j); }
                            }
                        }
                    }

                    if (blob.Count < minArea) foreach (int i in blob) solid[i] = 0f;
                }
            }

            float[] keep = grow > 0 ? Inpaint.Dilate(solid, m.W, m.H, grow) : solid;
            float fr = Img.ToLinear((byte)fill.r), fg = Img.ToLinear((byte)fill.g), fb = Img.ToLinear((byte)fill.b);
            int flat = 0;
            for (int i = 0; i < m.Count; i++)
            {
                if (keep[i] > 0.5f || (m.A[i] < 0.99f && !any)) continue;
                m.R[i] = fr; m.G[i] = fg; m.B[i] = fb; flat++;
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  fill #{fill.r:X2}{fill.g:X2}{fill.b:X2}  {100.0 * flat / m.Count:0.0}% of the image made plain");
            return 0;
        }

        /// <summary>Point symmetry: the bottom half becomes the top half turned by 180 degrees (or the reverse).</summary>
        private static int Rot180(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            bool fromTop = o.Get("from", "top") != "bottom";
            for (int y = 0; y < m.H / 2; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int a = y * m.W + x, b = (m.H - 1 - y) * m.W + (m.W - 1 - x);
                    int s = fromTop ? a : b, t = fromTop ? b : a;
                    m.R[t] = m.R[s]; m.G[t] = m.G[s]; m.B[t] = m.B[s]; m.A[t] = m.A[s];
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  point-symmetric from the {(fromTop ? "top" : "bottom")} half");
            return 0;
        }

        /// <summary>
        /// Per channel, straight segments through the anchors (source colour to mockup colour);
        /// outside the anchors the curve continues with the nearest anchor's offset. Anchors are
        /// sorted per channel, so give colours whose channels rise together.
        /// </summary>
        private static int Curve(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var pairs = new List<(double[] s, double[] d)>();
            foreach (string a in o.Need("anchors").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = a.Split('>');
                pairs.Add((HexBytes(p[0]), HexBytes(p[1])));
            }

            float[][] chs = { m.R, m.G, m.B };
            for (int c = 0; c < 3; c++)
            {
                var pts = pairs.Select(p => (s: p.s[c], d: p.d[c])).OrderBy(p => p.s).ToList();
                var lut = new float[256];
                for (int v = 0; v < 256; v++)
                {
                    double outv;
                    if (v <= pts[0].s) outv = v + (pts[0].d - pts[0].s);
                    else if (v >= pts[pts.Count - 1].s) outv = v + (pts[pts.Count - 1].d - pts[pts.Count - 1].s);
                    else
                    {
                        int k = 0;
                        while (k < pts.Count - 2 && v > pts[k + 1].s) k++;
                        double span = pts[k + 1].s - pts[k].s;
                        double t = span > 1e-6 ? (v - pts[k].s) / span : 0;
                        outv = pts[k].d + (pts[k + 1].d - pts[k].d) * t;
                    }

                    lut[v] = (float)Math.Clamp(outv, 0, 255);
                }

                for (int i = 0; i < m.Count; i++)
                {
                    if (m.A[i] <= 0f) continue;
                    double v = Img.ToSrgb01(chs[c][i]) * 255.0;
                    int lo = Math.Clamp((int)Math.Floor(v), 0, 255), hi = Math.Min(255, lo + 1);
                    double outv = lut[lo] + (lut[hi] - lut[lo]) * (v - lo);
                    chs[c][i] = Img.ToLinear((float)(outv / 255.0));
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  curve through {pairs.Count} anchors");
            return 0;
        }

        /// <summary>Draws one image over another (straight alpha, linear light) at an offset.</summary>
        private static int Compose(Opts o)
        {
            Img b = Img.Load(o.Need("base"));
            Img t = Img.Load(o.Need("over"));
            int[] at = o.Ints("at", 2, new[] { 0, 0 });
            float op = (float)o.Num("opacity", 1);
            for (int y = 0; y < t.H; y++)
            {
                int ty = at[1] + y;
                if (ty < 0 || ty >= b.H) continue;
                for (int x = 0; x < t.W; x++)
                {
                    int tx = at[0] + x;
                    if (tx < 0 || tx >= b.W) continue;
                    int s = y * t.W + x, d = ty * b.W + tx;
                    float sa = t.A[s] * op, da = b.A[d];
                    float oa = sa + da * (1 - sa);
                    if (oa <= 0f) continue;
                    b.R[d] = (t.R[s] * sa + b.R[d] * da * (1 - sa)) / oa;
                    b.G[d] = (t.G[s] * sa + b.G[d] * da * (1 - sa)) / oa;
                    b.B[d] = (t.B[s] * sa + b.B[d] * da * (1 - sa)) / oa;
                    b.A[d] = oa;
                }
            }

            b.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {b.W}x{b.H}");
            return 0;
        }

        private static int PadCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int w = o.Int("w", m.W), h = o.Int("h", m.H);
            var n = new Img(w, h, 0f);
            n.Paste(m, (w - m.W) / 2, (h - m.H) / 2);
            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H} centred on {w}x{h}");
            return 0;
        }
    }
}
