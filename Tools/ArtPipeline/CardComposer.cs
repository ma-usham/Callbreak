// Composes the game's decks from the vintage card pack's elements (compose-decks.ps1): each
// deck's frame (ADR 014's three styles) redrawn at twice the export size in its own colours, with
// the pack's index glyphs, pips, aces and court figures printed on it in that deck's inks.
// C# 5 only: compiled by Windows PowerShell's Add-Type together with DeckTools.cs.
//
// Coordinates are master pixels (Composer.W x Composer.H), continuous as in DeckTools.cs.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// ------------------------------------------------------------------------------------ ink

// Coverage of black and red ink, 0-1 per pixel: a pack card split into paper and inks, or a
// piece of one. Coverage is linear, so it can be re-sampled and turned without dark fringes.
public sealed class InkLayer
{
    public int W, H;
    public float[] K, Rd;

    public InkLayer(int w, int h)
    {
        W = w; H = h;
        K = new float[w * h]; Rd = new float[w * h];
    }

    public InkLayer Crop(int x0, int y0, int w, int h)
    {
        InkLayer o = new InkLayer(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = x0 + x, sy = y0 + y;
                if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                o.K[y * w + x] = K[sy * W + sx]; o.Rd[y * w + x] = Rd[sy * W + sx];
            }
        return o;
    }

    public InkLayer Rotate180()
    {
        InkLayer o = new InkLayer(W, H);
        int n = W * H;
        for (int i = 0; i < n; i++) { o.K[n - 1 - i] = K[i]; o.Rd[n - 1 - i] = Rd[i]; }
        return o;
    }

    // Re-sampled to w x h (Catmull-Rom, widened when shrinking), coverage held to 0-1.
    public InkLayer Resize(int w, int h)
    {
        double[][] wx = Cut.Weights(w, 0, (double)W / w, 0, W);
        double[][] wy = Cut.Weights(h, 0, (double)H / h, 0, H);
        InkLayer o = new InkLayer(w, h);
        o.K = Resample(K, W, H, wx, wy, w, h);
        o.Rd = Resample(Rd, W, H, wx, wy, w, h);
        return o;
    }

    // The same layer scaled so its width is w (height in proportion).
    public InkLayer ToWidth(double w)
    {
        return Resize((int)Math.Round(w), (int)Math.Round(H * w / W));
    }

    public InkLayer ToHeight(double h)
    {
        return Resize((int)Math.Round(W * h / H), (int)Math.Round(h));
    }

    static float[] Resample(float[] src, int sw, int sh, double[][] wx, double[][] wy, int w, int h)
    {
        float[] t = new float[w * sh];
        for (int j = 0; j < sh; j++)
            for (int u = 0; u < w; u++)
            {
                double[] q = wx[u]; int i0 = (int)q[0]; double s = 0;
                for (int k = 1; k < q.Length; k++)
                {
                    int i = i0 + k - 1; if (i < 0) i = 0; if (i >= sw) i = sw - 1;
                    s += q[k] * src[j * sw + i];
                }
                t[j * w + u] = (float)s;
            }
        float[] o = new float[w * h];
        for (int v = 0; v < h; v++)
        {
            double[] q = wy[v]; int j0 = (int)q[0];
            for (int u = 0; u < w; u++)
            {
                double s = 0;
                for (int k = 1; k < q.Length; k++)
                {
                    int j = j0 + k - 1; if (j < 0) j = 0; if (j >= sh) j = sh - 1;
                    s += q[k] * t[j * w + u];
                }
                o[v * w + u] = (float)Math.Max(0, Math.Min(1, s));
            }
        }
        return o;
    }

    // Bounding box {x0, y0, x1, y1} (x1, y1 exclusive) of total coverage above t, or null.
    public int[] Bounds(float t)
    {
        int x0 = W, y0 = H, x1 = -1, y1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (K[i] + Rd[i] <= t) continue;
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        return x1 < 0 ? null : new int[] { x0, y0, x1 + 1, y1 + 1 };
    }

    // Keeps the ink of the components (8-neighbour, total coverage above t) that 'keep' accepts,
    // given each component's {x0, y0, x1, y1, pixel count}; their anti-aliased rims come along
    // (grown by 'grow' px). Everything else is cleared.
    public void KeepComponents(float t, int grow, Predicate<int[]> keep)
    {
        int n = W * H;
        int[] label = new int[n];
        bool[] kept = new bool[n];
        Stack<int> stack = new Stack<int>();
        List<int> members = new List<int>();
        int next = 0;
        for (int start = 0; start < n; start++)
        {
            if (label[start] != 0 || K[start] + Rd[start] <= t) continue;
            next++;
            members.Clear();
            stack.Push(start); label[start] = next;
            int x0 = W, y0 = H, x1 = -1, y1 = -1;
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % W, y = i / W;
                members.Add(i);
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        int k = yy * W + xx;
                        if (label[k] != 0 || K[k] + Rd[k] <= t) continue;
                        label[k] = next; stack.Push(k);
                    }
            }
            if (!keep(new int[] { x0, y0, x1 + 1, y1 + 1, members.Count })) continue;
            foreach (int i in members) kept[i] = true;
        }
        bool[] grown = new bool[n];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!kept[y * W + x]) continue;
                for (int dy = -grow; dy <= grow; dy++)
                    for (int dx = -grow; dx <= grow; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < W && yy < H) grown[yy * W + xx] = true;
                    }
            }
        for (int i = 0; i < n; i++) if (!grown[i]) { K[i] = 0; Rd[i] = 0; }
    }

    // Strokes made bolder by about d px on every side (d <= 1.4): each pixel takes the strongest
    // coverage within reach, a neighbour weighted by how much of it the reach covers.
    public InkLayer Bolden(double d)
    {
        float edge = (float)Math.Max(0, Math.Min(1, d)), diag = (float)Math.Max(0, Math.Min(1, d - 1.41421356 + 1));
        InkLayer o = new InkLayer(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float k = K[i], r = Rd[i];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        float w = dx == 0 || dy == 0 ? edge : diag;
                        int j = yy * W + xx;
                        k = Math.Max(k, K[j] * w); r = Math.Max(r, Rd[j] * w);
                    }
                float s = k + r;
                if (s > 1) { k /= s; r /= s; }
                o.K[i] = k; o.Rd[i] = r;
            }
        return o;
    }

    // Deeper ink: total coverage s becomes 1 - (1 - s)^g (g > 1), the two inks keeping their
    // share; fine hatching reads stronger at small sizes, solid ink stays solid.
    public void Deepen(double g)
    {
        for (int i = 0; i < W * H; i++)
        {
            float s = K[i] + Rd[i];
            if (s <= 0) continue;
            float d = (float)(1 - Math.Pow(1 - Math.Min(1f, s), g)) / s;
            K[i] *= d; Rd[i] *= d;
        }
    }

    // Clears the ink inside the box [0, x1) x [0, y1), fading out over 'feather' px beyond it.
    public void ClearCorner(double x1, double y1, double feather)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double dx = Math.Max(0, x + 0.5 - x1), dy = Math.Max(0, y + 0.5 - y1);
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist >= feather) continue;
                float keep = (float)(dist / feather);
                int i = y * W + x;
                K[i] *= keep; Rd[i] *= keep;
            }
    }

    // The layer cut to the box of its ink (total coverage above t) plus 'pad' px on every side.
    public InkLayer Trim(float t, int pad)
    {
        int[] b = Bounds(t);
        if (b == null) return new InkLayer(1, 1);
        return Crop(b[0] - pad, b[1] - pad, b[2] - b[0] + 2 * pad, b[3] - b[1] + 2 * pad);
    }
}

public static class Ink
{
    // The paper of a pack card: per channel, the median of the lighter half of its middle.
    public static double[] PaperOf(Img im)
    {
        List<int> idx = new List<int>();
        for (int y = im.H / 10; y < im.H * 9 / 10; y++)
            for (int x = im.W / 5; x < im.W * 4 / 5; x++) idx.Add(y * im.W + x);
        idx.Sort(delegate(int a, int b) { return Lum(im, a).CompareTo(Lum(im, b)); });
        List<int> light = idx.GetRange(idx.Count / 2, idx.Count - idx.Count / 2);
        double[] p = new double[3];
        float[][] ch = { im.R, im.G, im.B };
        for (int c = 0; c < 3; c++)
        {
            List<float> v = new List<float>();
            foreach (int i in light) v.Add(ch[c][i]);
            v.Sort();
            p[c] = v[v.Count / 2];
        }
        return p;
    }

    static double Lum(Img im, int i) { return 0.2126 * im.R[i] + 0.7152 * im.G[i] + 0.0722 * im.B[i]; }

    // Splits a pack card: each pixel as paper + k (black - paper) + r (red - paper) by least
    // squares, k, r >= 0 and k + r <= 1. Coverage under 'floor' is the paper's own grain and
    // stains and becomes 0; above it the scale is stretched back to 1. Transparent pixels (the
    // card's rounded rim) carry no ink.
    public static InkLayer Split(Img im, double[] paper, double[] black, double[] red, double floor)
    {
        double[] a = { black[0] - paper[0], black[1] - paper[1], black[2] - paper[2] };
        double[] b = { red[0] - paper[0], red[1] - paper[1], red[2] - paper[2] };
        double aa = Dot(a, a), ab = Dot(a, b), bb = Dot(b, b), det = aa * bb - ab * ab;
        InkLayer o = new InkLayer(im.W, im.H);
        for (int i = 0; i < im.W * im.H; i++)
        {
            double[] d = { im.R[i] - paper[0], im.G[i] - paper[1], im.B[i] - paper[2] };
            double da = Dot(d, a), db = Dot(d, b);
            double k = (da * bb - db * ab) / det, r = (db * aa - da * ab) / det;
            if (k < 0) { k = 0; r = db / bb; }
            if (r < 0) { r = 0; k = da / aa; }
            k = Math.Max(0, k); r = Math.Max(0, r);
            if (k + r > 1) { double s = k + r; k /= s; r /= s; }
            double total = k + r;
            double kept = total <= floor ? 0 : (total - floor) / (1 - floor);
            double scale = total > 0 ? kept / total : 0;
            double alpha = im.A[i] / 255.0;
            o.K[i] = (float)(k * scale * alpha); o.Rd[i] = (float)(r * scale * alpha);
        }
        return o;
    }

    static double Dot(double[] x, double[] y) { return x[0] * y[0] + x[1] * y[1] + x[2] * y[2]; }

    // A one-ink layer from an image's alpha (a saved wash, such as Himalayan Ivory's mountains).
    public static InkLayer FromAlpha(Img im)
    {
        InkLayer o = new InkLayer(im.W, im.H);
        for (int i = 0; i < im.W * im.H; i++) o.K[i] = im.A[i] / 255f;
        return o;
    }

    // The layer printed in two inks: each pixel's colour is the mix of the inks by their
    // coverage, its alpha their total (straight alpha). The black ink may be shaded from 'top'
    // to 'bottom' over the layer's height (Kathmandu Gold's bronze); pass the same colour twice
    // for a flat ink.
    public static Img Paint(InkLayer l, Color top, Color bottom, Color red)
    {
        Img o = new Img(l.W, l.H);
        for (int y = 0; y < l.H; y++)
        {
            float t = l.H > 1 ? (float)y / (l.H - 1) : 0;
            float kr = top.R + (bottom.R - top.R) * t, kg = top.G + (bottom.G - top.G) * t, kb = top.B + (bottom.B - top.B) * t;
            for (int x = 0; x < l.W; x++)
            {
                int i = y * l.W + x;
                float k = l.K[i], r = l.Rd[i], s = k + r;
                if (s <= 0) continue;
                o.R[i] = (k * kr + r * red.R) / s; o.G[i] = (k * kg + r * red.G) / s; o.B[i] = (k * kb + r * red.B) / s;
                o.A[i] = Math.Min(1f, s) * 255f;
            }
        }
        return o;
    }
}

// ------------------------------------------------------------------------------------ elements

// The pieces of the pack the decks are printed with, already at their master size.
public sealed class PackElements
{
    public Dictionary<string, InkLayer> Pip = new Dictionary<string, InkLayer>();        // suit -> pip of ranks 2-7
    public Dictionary<string, InkLayer> SmallPip = new Dictionary<string, InkLayer>();   // suit -> pip of ranks 7-10
    public Dictionary<string, InkLayer> IndexPip = new Dictionary<string, InkLayer>();   // suit -> the index's suit
    public Dictionary<string, InkLayer> Rank = new Dictionary<string, InkLayer>();       // rank -> index glyph
    public Dictionary<string, InkLayer> Ace = new Dictionary<string, InkLayer>();        // suit -> ornate ace
    public Dictionary<string, InkLayer> Court = new Dictionary<string, InkLayer>();      // "Hearts K" -> upper half
}

// ------------------------------------------------------------------------------------ styles

// One deck's look: the frame's colours and the inks it is printed in.
public sealed class DeckStyle
{
    public string Name;
    public Color Band;                 // the coloured edge
    public Color EdgeRim;              // a thin line along the very edge (Kathmandu Gold's gold rim)
    public float EdgeRimWidth;         // 0: none
    public Color Corner;               // the cut corners' fill
    public Color Motif;                // the pattern in the cut corners
    public Color[] Lines;              // the frame's lines, outermost first
    public float[] LineWidths;         // master px
    public Color Paper;
    public Color Black, BlackLow, Red; // pips and index: black ink at the top and the bottom of a shape, red ink
    public Color CourtBlack, CourtRed; // the court figures
    public Color Seam;                 // the line between a court's two halves
    public InkLayer[] Watermarks;      // printed on the paper first (Himalayan Ivory's mountains), master size
    public int[][] WatermarkAt;        // their top-left corners
    public Color WatermarkInk;
}

// ------------------------------------------------------------------------------------ composer

public static class Composer
{
    public const int W = 512, H = 804;              // master: twice the export (256 x 402, the cards' 0.637)
    public const float BandWidth = 16.8f;           // 7.5 px of a 228 px card (ADR 014's decks)
    public const float Chamfer = 120.5f;            // x + y of the corner cut at the band's inner edge
    public const double Radius = 31.4;              // corner radius (14 px of a 228 px card)

    // Index: the rank above the suit, in the top left corner and turned in the bottom right; as
    // large as ADR 014's (rank 0.11 of the card's height), since it is all a hand card shows of
    // itself and what a trick card is read by.
    public const double IndexX = 0.195 * W, IndexRankY = 0.137 * H, IndexPipY = 0.262 * H;
    public const double IndexRankH = 0.108 * H, IndexPipW = 0.12 * W;
    public const double IndexBolden = 1.0;          // master px added to every side of the glyphs' strokes

    // Pips: three columns, clear of the index's column; two rows for 2-8, four for 9 and 10. From
    // the 7 on the middle column's pips sit between the outer ones, so they are smaller.
    public const double ColL = 0.355 * W, ColC = 0.5 * W, ColR = 0.645 * W;
    public const double RowTop = 0.305 * H, RowBottom = 0.695 * H;
    public const double PipW = 0.17 * W, SmallPipW = 0.145 * W;

    // Aces: the pack's ornate suit, centred.
    public const double AceW = 0.56 * W;

    // Courts: the pack's figure from the crown to the waist at this scale, centred on its waist and
    // turned below the middle. Where a spear or a flower reaches the index, it stops short of it:
    // the ink is cleared from the index's corner (and, by the turn, from the opposite one).
    public const double CourtScale = 0.92;
    public const double CourtClearX = IndexX + 0.5 * IndexPipW + 10, CourtClearY = IndexPipY + 0.5 * 1.2 * IndexPipW + 10;
    public const double CourtDeepen = 1.5;

    // ------------------------------------------------------------------ frame

    // The frame on its paper, without the corner rounding (the shared mask gives the alpha).
    public static Img Frame(DeckStyle s)
    {
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(s.Band);
                if (s.EdgeRimWidth > 0)
                {
                    using (Pen rim = new Pen(s.EdgeRim, s.EdgeRimWidth))
                    using (GraphicsPath edge = RoundRect(s.EdgeRimWidth / 2f, Radius - s.EdgeRimWidth / 2f))
                        g.DrawPath(rim, edge);
                }

                using (SolidBrush corner = new SolidBrush(s.Corner))
                    foreach (Matrix m in CornerTransforms())
                    {
                        g.Transform = m;
                        g.FillPolygon(corner, new PointF[] { new PointF(BandWidth * 0.55f, BandWidth * 0.55f), new PointF(Chamfer, 0), new PointF(0, Chamfer) });
                        Motif(g, s.Motif, s.Corner);
                        m.Dispose();
                    }
                g.ResetTransform();

                float d = 0;
                for (int i = 0; i < s.Lines.Length; i++)
                {
                    using (SolidBrush b = new SolidBrush(s.Lines[i])) g.FillPolygon(b, Octagon(d));
                    d += s.LineWidths[i];
                }
                using (SolidBrush paper = new SolidBrush(s.Paper)) g.FillPolygon(paper, Octagon(d));
            }
            Img o = Review.FromBitmap(bmp);
            Grain(o, 0.012f, 7);
            if (s.Watermarks != null)
                for (int i = 0; i < s.Watermarks.Length; i++)
                    o.Over(Ink.Paint(s.Watermarks[i], s.WatermarkInk, s.WatermarkInk, s.WatermarkInk), s.WatermarkAt[i][0], s.WatermarkAt[i][1]);
            return o;
        }
    }

    // The inner panel at d px inside the band: an octagon whose corners are cut at 45 degrees.
    static PointF[] Octagon(float d)
    {
        float e = BandWidth + d, k = Chamfer + d * 1.41421356f;
        return new PointF[]
        {
            new PointF(e, k - e), new PointF(k - e, e), new PointF(W - (k - e), e), new PointF(W - e, k - e),
            new PointF(W - e, H - (k - e)), new PointF(W - (k - e), H - e), new PointF(k - e, H - e), new PointF(e, H - (k - e)),
        };
    }

    static GraphicsPath RoundRect(float inset, double radius)
    {
        float r = (float)radius, x0 = inset, y0 = inset, x1 = W - inset, y1 = H - inset;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(x0, y0, 2 * r, 2 * r, 180, 90);
        p.AddArc(x1 - 2 * r, y0, 2 * r, 2 * r, 270, 90);
        p.AddArc(x1 - 2 * r, y1 - 2 * r, 2 * r, 2 * r, 0, 90);
        p.AddArc(x0, y1 - 2 * r, 2 * r, 2 * r, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Top left as drawn, then mirrored into the other three corners.
    static Matrix[] CornerTransforms()
    {
        return new Matrix[]
        {
            new Matrix(1, 0, 0, 1, 0, 0), new Matrix(-1, 0, 0, 1, W, 0),
            new Matrix(-1, 0, 0, -1, W, H), new Matrix(1, 0, 0, -1, 0, H),
        };
    }

    // The corner pattern (drawn in the top left corner's triangle): a tile turned 45 degrees with
    // an eight-pointed star in it, a four-petal flower along each edge and a dot beyond it, after
    // the Newari lattice of the three decks' corners.
    static void Motif(Graphics g, Color ink, Color ground)
    {
        float o = BandWidth, l = Chamfer - 2 * BandWidth;
        float cx = o + 0.33f * l, cy = o + 0.33f * l;
        using (SolidBrush b = new SolidBrush(ink))
        using (SolidBrush hole = new SolidBrush(ground))
        using (Pen p = new Pen(ink, 3f))
        {
            float r = 21.5f;
            g.DrawPolygon(p, new PointF[] { new PointF(cx, cy - r), new PointF(cx + r, cy), new PointF(cx, cy + r), new PointF(cx - r, cy) });
            float a = 9.8f, q = 13.9f;
            g.FillRectangle(b, cx - a, cy - a, 2 * a, 2 * a);
            g.FillPolygon(b, new PointF[] { new PointF(cx, cy - q), new PointF(cx + q, cy), new PointF(cx, cy + q), new PointF(cx - q, cy) });
            g.FillEllipse(hole, cx - 4f, cy - 4f, 8f, 8f);
            g.FillEllipse(b, cx - 1.8f, cy - 1.8f, 3.6f, 3.6f);
            for (int side = 0; side < 2; side++)
            {
                float fx = side == 0 ? o + 0.69f * l : o + 0.155f * l, fy = side == 0 ? o + 0.155f * l : o + 0.69f * l;
                Flower(g, b, fx, fy, 3.8f);
                float dx = side == 0 ? o + 0.86f * l : o + 0.075f * l, dy = side == 0 ? o + 0.075f * l : o + 0.86f * l;
                g.FillEllipse(b, dx - 2.6f, dy - 2.6f, 5.2f, 5.2f);
            }
        }
    }

    static void Flower(Graphics g, Brush b, float x, float y, float r)
    {
        float s = r * 1.15f;
        g.FillEllipse(b, x - s - r, y - r, 2 * r, 2 * r);
        g.FillEllipse(b, x + s - r, y - r, 2 * r, 2 * r);
        g.FillEllipse(b, x - r, y - s - r, 2 * r, 2 * r);
        g.FillEllipse(b, x - r, y + s - r, 2 * r, 2 * r);
        g.FillEllipse(b, x - r * 0.8f, y - r * 0.8f, 1.6f * r, 1.6f * r);
    }

    // A faint, smooth paper grain: value noise on a 6 px lattice, +-amp of each channel.
    static void Grain(Img im, float amp, int seed)
    {
        const int cell = 6;
        int gw = im.W / cell + 2, gh = im.H / cell + 2;
        float[] lattice = new float[gw * gh];
        Random rnd = new Random(seed);
        for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)(rnd.NextDouble() * 2 - 1);
        for (int y = 0; y < im.H; y++)
            for (int x = 0; x < im.W; x++)
            {
                float fx = (float)x / cell, fy = (float)y / cell;
                int ix = (int)fx, iy = (int)fy;
                float tx = fx - ix, ty = fy - iy;
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                float n = (lattice[iy * gw + ix] * (1 - tx) + lattice[iy * gw + ix + 1] * tx) * (1 - ty)
                        + (lattice[(iy + 1) * gw + ix] * (1 - tx) + lattice[(iy + 1) * gw + ix + 1] * tx) * ty;
                float f = 1 + amp * n;
                int i = y * im.W + x;
                im.R[i] = Math.Min(255f, im.R[i] * f); im.G[i] = Math.Min(255f, im.G[i] * f); im.B[i] = Math.Min(255f, im.B[i] * f);
            }
    }

    // ------------------------------------------------------------------ faces

    static bool IsRed(string suit) { return suit == "Hearts" || suit == "Diamonds"; }

    // One face at master size: frame, the index in two corners, and pips, an ace or a court.
    public static Img Face(DeckStyle s, PackElements e, string suit, string rank)
    {
        Img card = Frame(s);
        bool red = IsRed(suit);
        Color top = red ? s.Red : s.Black, bottom = red ? s.Red : s.BlackLow;

        foreach (bool turn in new bool[] { false, true })
        {
            Print(card, e.Rank[rank], turn ? W - IndexX : IndexX, turn ? H - IndexRankY : IndexRankY, top, bottom, top, turn);
            Print(card, e.IndexPip[suit], turn ? W - IndexX : IndexX, turn ? H - IndexPipY : IndexPipY, top, bottom, top, turn);
        }

        if (rank == "A")
        {
            Print(card, e.Ace[suit], W / 2.0, H / 2.0, top, bottom, top, false);
        }
        else if (rank == "J" || rank == "Q" || rank == "K")
        {
            // Point symmetry about the card's centre: with the waist centred the halves meet.
            InkLayer half = e.Court[suit + " " + rank];
            int[] waist = WaistExtent(half);
            int x = (int)Math.Round(W / 2.0 - (waist[0] + waist[1] + 1) / 2.0);
            half = half.Crop(0, 0, half.W, half.H);
            half.ClearCorner(CourtClearX - x, CourtClearY - (H / 2 - half.H), 6);
            card.Over(Ink.Paint(half, s.CourtBlack, s.CourtBlack, s.CourtRed), x, H / 2 - half.H);
            card.Over(Ink.Paint(half.Rotate180(), s.CourtBlack, s.CourtBlack, s.CourtRed), W - x - half.W, H / 2);
            FillRect(card, x + waist[0] - 6, H / 2.0 - 1.4, x + waist[1] + 7, H / 2.0 + 1.4, s.Seam);
        }
        else
        {
            int n = rank == "10" ? 10 : int.Parse(rank);
            InkLayer pip = n >= 7 ? e.SmallPip[suit] : e.Pip[suit];
            foreach (double[] p in Pips(n))
                Print(card, pip, p[0], p[1], top, bottom, top, p[1] > H / 2.0 + 1);
        }
        return card;
    }

    // Prints a layer centred at (cx, cy), turned half round when 'turn' (the bottom index, the
    // pips below the middle); a shaded ink keeps its light end at the shape's own top.
    static void Print(Img card, InkLayer l, double cx, double cy, Color top, Color bottom, Color red, bool turn)
    {
        InkLayer t = turn ? l.Rotate180() : l;
        card.Over(Ink.Paint(t, turn ? bottom : top, turn ? top : bottom, red), (int)Math.Round(cx - t.W / 2.0), (int)Math.Round(cy - t.H / 2.0));
    }

    // Standard pip positions: centres, master px.
    public static List<double[]> Pips(int n)
    {
        double mid = H / 2.0, q1 = RowTop + (RowBottom - RowTop) / 3, q2 = RowTop + 2 * (RowBottom - RowTop) / 3;
        List<double[]> p = new List<double[]>();
        Action<double, double> add = delegate(double x, double y) { p.Add(new double[] { x, y }); };
        switch (n)
        {
            case 2: add(ColC, RowTop); add(ColC, RowBottom); break;
            case 3: add(ColC, RowTop); add(ColC, mid); add(ColC, RowBottom); break;
            default:
                if (n <= 8)
                {
                    add(ColL, RowTop); add(ColR, RowTop); add(ColL, RowBottom); add(ColR, RowBottom);
                    if (n >= 6) { add(ColL, mid); add(ColR, mid); }
                    if (n == 5) add(ColC, mid);
                    if (n >= 7) add(ColC, (RowTop + mid) / 2);
                    if (n == 8) add(ColC, (mid + RowBottom) / 2);
                }
                else
                {
                    foreach (double y in new double[] { RowTop, q1, q2, RowBottom }) { add(ColL, y); add(ColR, y); }
                    if (n == 9) add(ColC, mid);
                    if (n == 10) { add(ColC, (RowTop + q1) / 2); add(ColC, (q2 + RowBottom) / 2); }
                }
                break;
        }
        return p;
    }

    // The first and last columns of the figure's ink in its last rows (the waist, where the two
    // halves meet).
    static int[] WaistExtent(InkLayer half)
    {
        int x0 = half.W, x1 = 0;
        for (int y = Math.Max(0, half.H - 4); y < half.H; y++)
            for (int i = 0; i < half.W; i++)
            {
                int k = y * half.W + i;
                if (half.K[k] + half.Rd[k] < 0.3f) continue;
                if (i < x0) x0 = i; if (i > x1) x1 = i;
            }
        if (x1 < x0) { x0 = 0; x1 = half.W - 1; }
        return new int[] { x0, x1 };
    }

    // A solid rectangle with anti-aliased top and bottom edges.
    static void FillRect(Img im, double x0, double y0, double x1, double y1, Color c)
    {
        for (int y = (int)Math.Floor(y0); y < (int)Math.Ceiling(y1); y++)
        {
            float cover = (float)(Math.Min(y + 1, y1) - Math.Max(y, y0));
            for (int x = (int)Math.Round(x0); x < (int)Math.Round(x1); x++)
            {
                int i = y * im.W + x;
                im.R[i] += (c.R - im.R[i]) * cover; im.G[i] += (c.G - im.G[i]) * cover; im.B[i] += (c.B - im.B[i]) * cover;
            }
        }
    }

    // The master shrunk to the export size, with the deck's shared mask as alpha.
    public static Img Export(Img master, int w, int h, float[] mask)
    {
        Img o = Cut.Resample(master, 0, 0, 0, 0, master.W, master.H, w, h);
        for (int i = 0; i < w * h; i++) o.A[i] = Img.Q(mask[i] * 255f);
        return o;
    }
}

// ------------------------------------------------------------------------------------ layers

// The decks as the game draws them (ADR 054): a base per deck (its Frame: band, lines, corners,
// paper, at the export size) and one face per card shared by every deck, laid over the base.
// A face is the reference deck's card less that deck's base, in premultiplied alpha and linear
// light: Unity blends UI in linear light (the project's colour space) and averages an sRGB
// texture's mip maps in linear light, so with premultiplied alpha the reference card comes back
// to the 8-bit value, and stays right in every mip map and under texture filtering. Straight
// alpha can't: it averages a fringe's colour without its coverage and the small mip maps drift.
public static class Layers
{
    static readonly double[] Luma = { 0.2126, 0.7152, 0.0722 };

    public static double Lin(double v)
    {
        v /= 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    public static double Srgb(double l)
    {
        l = Math.Max(0, Math.Min(1, l));
        return 255.0 * (l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055);
    }

    // The face of 'card' over 'cardBase' (both 8-bit, the same size): at each pixel the coverage
    // of the ink that explains it best ('inks', the reference deck's), raised where a darker
    // channel needs it, then the premultiplied colour that puts the card back exactly.
    public static Img Lift(Img card, Img cardBase, Color[] inks)
    {
        int n = card.W * card.H;
        double[][] ink = new double[inks.Length][];
        for (int k = 0; k < inks.Length; k++) ink[k] = new double[] { Lin(inks[k].R), Lin(inks[k].G), Lin(inks[k].B) };
        Img o = new Img(card.W, card.H);
        double[] p = new double[3], d = new double[3];
        for (int i = 0; i < n; i++)
        {
            if (cardBase.A[i] <= 0) continue;
            if (Img.Q(card.R[i]) == Img.Q(cardBase.R[i]) && Img.Q(card.G[i]) == Img.Q(cardBase.G[i]) && Img.Q(card.B[i]) == Img.Q(cardBase.B[i])) continue;
            p[0] = Lin(cardBase.R[i]); p[1] = Lin(cardBase.G[i]); p[2] = Lin(cardBase.B[i]);
            d[0] = Lin(card.R[i]) - p[0]; d[1] = Lin(card.G[i]) - p[1]; d[2] = Lin(card.B[i]) - p[2];

            // Least squares per ink: d = a (ink - paper), weighted by luminance.
            double best = double.MaxValue, a = 0;
            for (int k = 0; k < ink.Length; k++)
            {
                double de = 0, ee = 0;
                for (int c = 0; c < 3; c++) { double e = ink[k][c] - p[c]; de += Luma[c] * d[c] * e; ee += Luma[c] * e * e; }
                double ak = ee > 1e-12 ? Math.Max(0, Math.Min(1, de / ee)) : 0, r = 0;
                for (int c = 0; c < 3; c++) { double e = ink[k][c] - p[c], q = d[c] - ak * e; r += Luma[c] * q * q; }
                if (r < best) { best = r; a = ak; }
            }
            if (a > 0.996) a = 1;

            // The least coverage that keeps every darker channel's colour above 0. A channel lighter
            // than the paper (the re-sampling's faint ring beside dark ink) does not raise it: on
            // near-white paper that would take most of the coverage and show the reference deck's
            // paper on the other decks. Premultiplied alpha adds that light instead (a colour above
            // the coverage), so the ring lightens whichever base is under it.
            double need = 0;
            for (int c = 0; c < 3; c++)
                if (d[c] < 0) need = Math.Max(need, -d[c] / Math.Max(p[c], 1e-6));
            int a8 = (int)Math.Round(a * 255);
            if (a8 < 1) a8 = 1;
            if (a8 / 255.0 < need) a8 = Math.Min(255, (int)Math.Ceiling(need * 255 - 1e-9));
            double al = a8 / 255.0;

            o.A[i] = a8;
            o.R[i] = (float)Srgb(Math.Max(0, p[0] + d[0] - (1 - al) * p[0]));
            o.G[i] = (float)Srgb(Math.Max(0, p[1] + d[1] - (1 - al) * p[1]));
            o.B[i] = (float)Srgb(Math.Max(0, p[2] + d[2] - (1 - al) * p[2]));
        }
        return o;
    }

    // A face over a base as the GPU blends them (8-bit textures, linear light): base (1 - a) + face.
    public static Img Over(Img cardBase, Img face)
    {
        Img o = new Img(face.W, face.H);
        for (int i = 0; i < face.W * face.H; i++)
        {
            double al = Img.Q(face.A[i]) / 255.0;
            o.R[i] = Img.Q((float)Srgb(Lin(Img.Q(cardBase.R[i])) * (1 - al) + Lin(Img.Q(face.R[i]))));
            o.G[i] = Img.Q((float)Srgb(Lin(Img.Q(cardBase.G[i])) * (1 - al) + Lin(Img.Q(face.G[i]))));
            o.B[i] = Img.Q((float)Srgb(Lin(Img.Q(cardBase.B[i])) * (1 - al) + Lin(Img.Q(face.B[i]))));
            o.A[i] = cardBase.A[i];
        }
        return o;
    }

    // { largest channel difference (0-255), PSNR (dB), % of pixels more than 3 apart } over the
    // pixels 'b' draws (alpha above 0).
    public static double[] Compare(Img a, Img b)
    {
        double max = 0, sq = 0; int count = 0, off = 0;
        for (int i = 0; i < a.W * a.H; i++)
        {
            if (b.A[i] <= 0) continue;
            double dr = Math.Abs(Img.Q(a.R[i]) - Img.Q(b.R[i])), dg = Math.Abs(Img.Q(a.G[i]) - Img.Q(b.G[i])), db = Math.Abs(Img.Q(a.B[i]) - Img.Q(b.B[i]));
            double m = Math.Max(dr, Math.Max(dg, db));
            max = Math.Max(max, m); sq += dr * dr + dg * dg + db * db; count++;
            if (m > 3) off++;
        }
        double mse = count > 0 ? sq / (3.0 * count) : 0;
        return new double[] { max, mse > 0 ? 10 * Math.Log10(255.0 * 255.0 / mse) : 99, count > 0 ? 100.0 * off / count : 0 };
    }
}
