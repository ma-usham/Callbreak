using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// Exact geometry at measured values: rounded frames made of bands, and rings/edges on discs.
    /// Anti-aliasing from signed distances (box filter one pixel wide).
    /// </summary>
    internal static partial class Program
    {
        private const string UsageShapes = @"  frame2  --size W,H --radius R --bands ""w:#hex[>#hex];..."" --fill #hex[>#hex] --out f [--scale 2.3] [--corners 1111]
          a rounded frame drawn from its outer edge inwards: bands of the given widths (mockup px) and
          colours (#top>#bottom for a vertical gradient), then the fill; size and radius in mockup px,
          rendered at scale sprite px per mockup px; --corners TL TR BR BL (0 = square corner)
  ringfix --in png --out f --center cx,cy --rings ""r0-r1:#top>#bottom;..."" --outer R [--keepin r]
          paint annuli (sprite px) with vertical gradients over a disc, keep the picture inside keepin,
          clear everything outside R (anti-aliased)
";

        private static bool ShapeCommand(string cmd, Opts o, out int rc)
        {
            rc = 0;
            switch (cmd)
            {
                case "frame2": rc = Frame2(o); return true;
                case "ringfix": rc = RingFix(o); return true;
                case "stripes": rc = Stripes(o); return true;
                case "leftpanel": rc = LeftPanel(o); return true;
                default: return false;
            }
        }

        private static (float r, float g, float b, float r2, float g2, float b2) ParseGrad(string s)
        {
            string[] p = s.Split('>');
            var a = Img.ParseHex(p[0].Trim());
            var b = p.Length > 1 ? Img.ParseHex(p[1].Trim()) : a;
            return (a.r, a.g, a.b, b.r, b.g, b.b);
        }

        /// <summary>Distance inside a rounded rectangle (positive inside), per-corner radius (0 = square).</summary>
        private static double InsideDist(double x, double y, double w, double h, double[] radii)
        {
            // corner: TL TR BR BL
            double r;
            double cx, cy;
            bool left = x < w / 2, top = y < h / 2;
            r = top ? (left ? radii[0] : radii[1]) : (left ? radii[3] : radii[2]);
            double dl = x, dr = w - x, dt = y, db = h - y;
            double edge = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
            if (r <= 0) return edge;
            cx = left ? r : w - r;
            cy = top ? r : h - r;
            bool inCornerX = left ? x < cx : x > cx, inCornerY = top ? y < cy : y > cy;
            if (inCornerX && inCornerY)
            {
                double dx = x - cx, dy = y - cy;
                return r - Math.Sqrt(dx * dx + dy * dy);
            }

            return edge;
        }

        private static int Frame2(Opts o)
        {
            double[] size = o.Nums("size", 2, null);
            double scale = o.Num("scale", 2.3);
            double R = o.Num("radius", 0);
            string corners = o.Get("corners", "1111");
            double[] radii = corners.Select(c => c == '1' ? R * scale : 0).ToArray();
            var bands = new List<(double w, (float r, float g, float b, float r2, float g2, float b2) c)>();
            foreach (string part in o.Get("bands", "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = part.Split(':');
                bands.Add((double.Parse(q[0], CultureInfo.InvariantCulture) * scale, ParseGrad(q[1])));
            }

            var fill = ParseGrad(o.Need("fill"));
            int W = (int)Math.Round(size[0] * scale), H = (int)Math.Round(size[1] * scale);
            if (o.Has("pixels")) { int[] px = o.Ints("pixels", 2, null); W = px[0]; H = px[1]; }
            var n = new Img(W, H, 0f);
            const int ss = 4;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double ar = 0, ag = 0, ab = 0, aa = 0;
                double t = H > 1 ? (y + 0.5) / H : 0;
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double px = x + (i + 0.5) / ss, py = y + (j + 0.5) / ss;
                    double d = InsideDist(px, py, W, H, radii);
                    if (d <= 0) continue;
                    double acc = 0;
                    (float r, float g, float b, float r2, float g2, float b2) col = fill;
                    foreach (var band in bands)
                    {
                        if (d < acc + band.w) { col = band.c; break; }
                        acc += band.w;
                    }

                    ar += col.r + (col.r2 - col.r) * t; ag += col.g + (col.g2 - col.g) * t; ab += col.b + (col.b2 - col.b) * t; aa += 1;
                }

                int k = y * W + x;
                n.A[k] = (float)(aa / (ss * ss));
                if (aa > 0) { n.R[k] = (float)(ar / aa); n.G[k] = (float)(ag / aa); n.B[k] = (float)(ab / aa); }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {W}x{H}  radius {R * scale:0.0} px, bands {string.Join(" + ", bands.Select(b => b.w.ToString("0.0", CultureInfo.InvariantCulture)))} px");
            return 0;
        }

        /// <summary>
        /// A panel flush with the right, top and bottom of the screen: exact alpha with the top-left and
        /// bottom-left corners rounded, the rest square, and a thin rim along the left edge and the two arcs.
        /// </summary>
        private static int LeftPanel(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double r = o.Num("radius", 60), rimW = o.Num("rim", 0);
            var rim = Img.ParseHex(o.Get("rimcolor", "#FFFFFF"));
            double rimA = o.Num("rimalpha", 1);
            var n = m.Clone();
            const int ss = 4;
            for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                double cov = 0, rc = 0;
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double px = x + (i + 0.5) / ss, py = y + (j + 0.5) / ss;
                    double d; // distance inside the left outline (left edge + the two arcs); top/bottom/right are open
                    if (py < r && px < r) d = r - Math.Sqrt((px - r) * (px - r) + (py - r) * (py - r));
                    else if (py > m.H - r && px < r) d = r - Math.Sqrt((px - r) * (px - r) + (py - (m.H - r)) * (py - (m.H - r)));
                    else d = px;
                    if (d <= 0) continue;
                    cov += 1;
                    if (d < rimW) rc += 1;
                }

                int k = y * m.W + x;
                double a = cov / (ss * ss);
                n.A[k] = (float)a;
                if (cov > 0 && rc > 0)
                {
                    double t = rc / cov * rimA;
                    n.R[k] = (float)(m.R[k] + (rim.r - m.R[k]) * t);
                    n.G[k] = (float)(m.G[k] + (rim.g - m.G[k]) * t);
                    n.B[k] = (float)(m.B[k] + (rim.b - m.B[k]) * t);
                }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {n.W}x{n.H}  left corners r {r} px, rim {rimW} px");
            return 0;
        }

        /// <summary>A horizontal line made of stripes (sprite px from the top), each a colour and an alpha.</summary>
        private static int Stripes(Opts o)
        {
            int[] size = o.Ints("size", 2, null);
            var rows = new List<(double h, (float r, float g, float b) c, double a)>();
            foreach (string part in o.Need("rows").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = part.Split(':');
                string[] ca = q[1].Split('@');
                rows.Add((double.Parse(q[0], CultureInfo.InvariantCulture), Img.ParseHex(ca[0]), ca.Length > 1 ? double.Parse(ca[1], CultureInfo.InvariantCulture) : 1.0));
            }

            double total = rows.Sum(r => r.h);
            double off = (size[1] - total) / 2;
            var n = new Img(size[0], size[1], 0f);
            for (int y = 0; y < size[1]; y++)
            {
                // coverage of each stripe over this pixel row
                double ar = 0, ag = 0, ab = 0, aa = 0, acc = off;
                foreach (var r in rows)
                {
                    double y0 = acc, y1 = acc + r.h;
                    double cov = Math.Max(0, Math.Min(y + 1, y1) - Math.Max(y, y0));
                    double a = cov * r.a;
                    ar += r.c.r * a; ag += r.c.g * a; ab += r.c.b * a; aa += a;
                    acc = y1;
                }

                for (int x = 0; x < size[0]; x++)
                {
                    int k = y * size[0] + x;
                    n.A[k] = (float)Math.Clamp(aa, 0, 1);
                    if (aa > 0) { n.R[k] = (float)(ar / aa); n.G[k] = (float)(ag / aa); n.B[k] = (float)(ab / aa); }
                }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {size[0]}x{size[1]}  stripes {total:0.0} px tall");
            return 0;
        }

        private static int RingFix(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double[] c = o.Nums("center", 2, null);
            double outer = o.Num("outer", 0);
            double keepIn = o.Num("keepin", 0);
            var rings = new List<(double r0, double r1, (float r, float g, float b, float r2, float g2, float b2) col)>();
            foreach (string part in o.Get("rings", "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] q = part.Split(':');
                string[] rr = q[0].Split('-');
                rings.Add((double.Parse(rr[0], CultureInfo.InvariantCulture), double.Parse(rr[1], CultureInfo.InvariantCulture), ParseGrad(q[1])));
            }

            const int ss = 4;
            var n = m.Clone();
            for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                int k = y * m.W + x;
                double ar = 0, ag = 0, ab = 0, aa = 0;
                double t = Math.Clamp((y + 0.5 - (c[1] - outer)) / (2 * outer), 0, 1);
                for (int j = 0; j < ss; j++)
                for (int i = 0; i < ss; i++)
                {
                    double px = x + (i + 0.5) / ss - c[0], py = y + (j + 0.5) / ss - c[1];
                    double d = Math.Sqrt(px * px + py * py);
                    if (outer > 0 && d > outer) continue;
                    var hit = rings.FirstOrDefault(rg => d >= rg.r0 && d < rg.r1);
                    if (hit.r1 > 0)
                    {
                        ar += hit.col.r + (hit.col.r2 - hit.col.r) * t; ag += hit.col.g + (hit.col.g2 - hit.col.g) * t; ab += hit.col.b + (hit.col.b2 - hit.col.b) * t; aa += 1;
                    }
                    else if (keepIn <= 0 || d < keepIn)
                    {
                        float a = m.A[k];
                        ar += m.R[k] * a; ag += m.G[k] * a; ab += m.B[k] * a; aa += a;
                    }
                }

                n.A[k] = (float)(aa / (ss * ss));
                if (aa > 0) { n.R[k] = (float)(ar / aa); n.G[k] = (float)(ag / aa); n.B[k] = (float)(ab / aa); }
                else { n.R[k] = 0; n.G[k] = 0; n.B[k] = 0; }
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {n.W}x{n.H}  rings {rings.Count}, outer {outer}");
            return 0;
        }
    }
}
