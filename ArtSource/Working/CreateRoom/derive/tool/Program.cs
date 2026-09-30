using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArtTool
{
    // Helper for screens 02-04 (run through ../derive.sh; not part of the game). It reuses arttool's
    // image code (Img, Shape, Inpaint) and adds what those screens needed beyond `Tools/art`:
    //   mirrorpill  derive a control sprite (text erased from the columns beside it, optional mirror)
    //   softshadow  a control's soft drop shadow solved against the mockup's own surroundings
    //   pairs       a comparison sheet: mockup crops above the same areas of a capture
    //   circle      fit a circle/ellipse to a disc's outline (--local: ignore the soft shadow)
    //   outline     outer edges of an outlined box from a rough rect
    //   fiticon     place a shared icon sprite on the mockup's own glyph without changing its aspect
    //   fitlogo     place a shared logo sprite on the mockup's own logo without changing its aspect
    //   dump / rgb  pixel values around a point; alpha: the alpha channel of a sprite as a map
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var o = new Dictionary<string, string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("--")) { o[args[i].Substring(2)] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true"; }
            }

            string Get(string k, string d = null) => o.TryGetValue(k, out string v) ? v : d;
            double[] Nums(string k) => Get(k).Split(',').Select(double.Parse).ToArray();
            Img img = Img.Load(Mock(Get("screen")));
            switch (args[0])
            {
                case "circle":
                {
                    double[] c = Nums("c");
                    double r = double.Parse(Get("r"));
                    double from = double.Parse(Get("from", "0")), to = double.Parse(Get("to", "360"));
                    bool bright = Get("mode", "dark") == "bright";
                    double thr = double.Parse(Get("thr", "8"));
                    double reach = double.Parse(Get("reach", "8"));
                    Local = o.ContainsKey("local");
                    Circle(img, c[0], c[1], r, from, to, bright, thr, reach, o.ContainsKey("v"));
                    return 0;
                }
                case "dump":
                {
                    double[] rr = Nums("rect");
                    for (int y = (int)rr[1]; y < rr[1] + rr[3]; y++)
                    {
                        var sb = new System.Text.StringBuilder($"{y,4}:");
                        for (int x = (int)rr[0]; x < rr[0] + rr[2]; x++) sb.Append($" {L(img, x, y),3:0}");
                        Console.WriteLine(sb.ToString());
                    }

                    Console.WriteLine("      " + string.Join(" ", Enumerable.Range((int)rr[0], (int)rr[2]).Select(x => $"{x % 1000,3}")));
                    return 0;
                }
                case "mirrorpill":
                {
                    // A symmetric copy of a pill/roundrect control: its left half (text erased by
                    // interpolating between the columns either side of --erase) mirrored onto the
                    // right half, cut with an analytic shape at --scale, edge colour taken just
                    // inside the edge. Framing: the shape fills the image exactly.
                    double[] r = Nums("rect");
                    double radius = double.Parse(Get("radius", "-1"));
                    double sc = double.Parse(Get("scale", "2"));
                    string shapeType = radius < 0 ? "pill" : "roundrect";
                    Img m = img.Clone();
                    if (o.ContainsKey("erase"))
                    {
                        foreach (string er in Get("erase").Split(';'))
                        {
                            double[] e = er.Split(',').Select(double.Parse).ToArray();
                            int ex0 = (int)Math.Floor(e[0]), ex1 = (int)Math.Ceiling(e[0] + e[2]), ey0 = (int)Math.Floor(e[1]), ey1 = (int)Math.Ceiling(e[1] + e[3]);
                            int xl = ex0 - 1, xr = ex1;
                            for (int y = ey0; y < ey1; y++)
                                for (int x = ex0; x < ex1; x++)
                                {
                                    // mode 0: interpolate between the columns either side; 1: copy the left column; 2: copy the right one
                                    int mode = e.Length > 4 ? (int)e[4] : 0;
                                    float t = mode == 1 ? 0f : mode == 2 ? 1f : (float)(x - xl) / (xr - xl);
                                    int i = y * m.W + x, a = y * m.W + xl, b = y * m.W + xr;
                                    m.R[i] = img.R[a] * (1 - t) + img.R[b] * t;
                                    m.G[i] = img.G[a] * (1 - t) + img.G[b] * t;
                                    m.B[i] = img.B[a] * (1 - t) + img.B[b] * t;
                                }
                        }
                    }

                    double cx = r[0] + r[2] / 2;
                    bool mirror = !o.ContainsKey("nomirror");
                    int W2 = (int)Math.Round(r[2] * sc), H2 = (int)Math.Round(r[3] * sc);
                    var shape = new Shape(shapeType, r[0], r[1], r[2], r[3], radius < 0 ? 0 : radius);
                    var outImg = new Img(W2, H2);
                    double sx = r[2] / W2, sy = r[3] / H2;
                    for (int j = 0; j < H2; j++)
                        for (int i = 0; i < W2; i++)
                        {
                            double u = r[0] + (i + 0.5) * sx, v = r[1] + (j + 0.5) * sy;
                            float a = shape.Coverage(u, v, sx);
                            double su = u, sv = v;
                            double d = shape.Sdf(u, v);
                            if (d > -1.0)
                            {
                                // move to 1 px inside the edge along the normal
                                double gx = shape.Sdf(u + 0.01, v) - shape.Sdf(u - 0.01, v), gy = shape.Sdf(u, v + 0.01) - shape.Sdf(u, v - 0.01);
                                double gl = Math.Sqrt(gx * gx + gy * gy);
                                if (gl > 1e-9) { gx /= gl; gy /= gl; }
                                double k = d + 1.0;
                                su = u - gx * k; sv = v - gy * k;
                            }

                            if (mirror && su > cx) su = 2 * cx - su;
                            (float cr, float cg, float cb) = Bilinear(m, su, sv);
                            int o2 = j * W2 + i;
                            outImg.R[o2] = cr; outImg.G[o2] = cg; outImg.B[o2] = cb; outImg.A[o2] = a;
                        }

                    outImg.Save(Get("out"));
                    Console.WriteLine($"{Get("out")} {W2}x{H2} ({shapeType} {r[0]},{r[1]} {r[2]}x{r[3]} r {radius})");
                    return 0;
                }
                case "fitlogo":
                {
                    // Fits a shared logo sprite onto the mockup's own logo by its crimson letters:
                    // --sprite path --srows a,b (sprite rows of the wordmark) --region x,y,w,h (mockup
                    // box holding only the wordmark). Prints the rect to "use" it at.
                    Img sp = Img.Load(Get("sprite"));
                    double[] sr = Nums("srows");
                    double[] rg = Nums("region");
                    double thr = double.Parse(Get("thr", "0.22"));
                    bool Crimson(float r, float g, float b) => Img.ToSrgb01(r) - Img.ToSrgb01(g) > thr && Img.ToSrgb01(r) > 0.25;
                    // sprite mask bounds within rows
                    int sx0 = int.MaxValue, sx1 = -1, sy0 = int.MaxValue, sy1 = -1; double scy = 0, sk = 0;
                    for (int y = (int)sr[0]; y < (int)sr[1]; y++)
                        for (int x = 0; x < sp.W; x++)
                        {
                            int i = y * sp.W + x;
                            if (sp.A[i] < 0.5f || !Crimson(sp.R[i], sp.G[i], sp.B[i])) continue;
                            sx0 = Math.Min(sx0, x); sx1 = Math.Max(sx1, x); scy += y; sk++; sy0 = Math.Min(sy0, y); sy1 = Math.Max(sy1, y);
                        }

                    int mx0 = int.MaxValue, mx1 = -1, my0 = int.MaxValue, my1 = -1; double mcy = 0, mk = 0;
                    for (int y = (int)rg[1]; y < rg[1] + rg[3]; y++)
                        for (int x = (int)rg[0]; x < rg[0] + rg[2]; x++)
                        {
                            int i = y * img.W + x;
                            if (!Crimson(img.R[i], img.G[i], img.B[i])) continue;
                            mx0 = Math.Min(mx0, x); mx1 = Math.Max(mx1, x); mcy += y; mk++; my0 = Math.Min(my0, y); my1 = Math.Max(my1, y);
                        }

                    double s = (mx1 + 1 - mx0) / (double)(sx1 + 1 - sx0);
                    double ox = mx0 - sx0 * s;
                    double oy = (mcy / mk + 0.5) - (scy / sk + 0.5) * s;
                    Console.WriteLine($"sprite wordmark x {sx0}..{sx1} y {sy0}..{sy1} cy {scy / sk:0.0}; mockup x {mx0}..{mx1} y {my0}..{my1} cy {mcy / mk:0.0}; scale {s:0.00000} (h ratio {(my1 + 1 - my0) / (double)(sy1 + 1 - sy0):0.00000})");
                    // refine: scale and offsets by mask difference
                    double Err(double ss, double oxx, double oyy)
                    {
                        double e = 0;
                        for (int y = (int)rg[1]; y < rg[1] + rg[3]; y++)
                            for (int x = (int)rg[0]; x < rg[0] + rg[2]; x++)
                            {
                                int i = y * img.W + x;
                                float mm = Crimson(img.R[i], img.G[i], img.B[i]) ? 1f : 0f;
                                double u = (x + 0.5 - oxx) / ss, v = (y + 0.5 - oyy) / ss;
                                int su = (int)Math.Floor(u), sv = (int)Math.Floor(v);
                                float pm = 0f;
                                if (su >= 0 && sv >= 0 && su < sp.W && sv < sp.H && sv >= sr[0] && sv < sr[1])
                                {
                                    int j = sv * sp.W + su;
                                    pm = sp.A[j] >= 0.5f && Crimson(sp.R[j], sp.G[j], sp.B[j]) ? 1f : 0f;
                                }

                                e += (mm - pm) * (mm - pm);
                            }

                        return e;
                    }

                    double best = Err(s, ox, oy);
                    for (int pass = 0; pass < 3; pass++)
                    {
                        double step = pass == 0 ? 1.0 : pass == 1 ? 0.4 : 0.15;
                        for (int it = 0; it < 12; it++)
                        {
                            bool improved = false;
                            foreach (var (ds, dx, dy) in new[] { (0.004, 0.0, 0.0), (-0.004, 0.0, 0.0), (0.0, step, 0.0), (0.0, -step, 0.0), (0.0, 0.0, step), (0.0, 0.0, -step) })
                            {
                                double ns = s * (1 + ds * step), nox = ox + dx, noy = oy + dy;
                                // keep the centre fixed when scaling
                                if (ds != 0) { double cxm = (mx0 + mx1 + 1) / 2.0, cym = mcy / mk; nox = cxm - (cxm - ox) * ns / s; noy = cym - (cym - oy) * ns / s; }
                                double e2 = Err(ns, nox, noy);
                                if (e2 < best) { best = e2; s = ns; ox = nox; oy = noy; improved = true; }
                            }

                            if (!improved) break;
                        }
                    }

                    Console.WriteLine($"fit scale {s:0.00000}  rect [{ox:0.00}, {oy:0.00}, {sp.W * s:0.00}, {sp.H * s:0.00}]  mismatch px {best:0}");
                    return 0;
                }
                case "fiticon":
                {
                    // Where to draw a shared icon sprite so that its glyph sits on this mockup's
                    // glyph WITHOUT changing the sprite's aspect (the mockups redraw the same icon a
                    // little differently on every screen). --sprite path --rect x,y,w,h (a loose box
                    // around the mockup's glyph and nothing else) [--ink dark|light] [--mode diff] [--thr 0.5].
                    // The glyph box is where the ink coverage crosses --thr, measured the same way on
                    // the mockup and on the sprite's alpha; one scale fits width and height (least
                    // squares) and the centres coincide. Prints the rect for a plain "use" node.
                    Img sp = Img.Load(Get("sprite"));
                    double[] r = Nums("rect");
                    bool dark = Get("ink", "dark") != "light";
                    double thr = double.Parse(Get("thr", "0.5"));
                    const int ring = 4;
                    Box box = Box.Around(r[0], r[1], r[2], r[3], ring).Clamp(img.W, img.H);
                    Img m = img.Crop(box.X, box.Y, box.W, box.H);
                    var hole = new float[m.Count];
                    for (int y = 0; y < box.H; y++)
                        for (int x = 0; x < box.W; x++)
                        {
                            double px = box.X + x + 0.5, py = box.Y + y + 0.5;
                            hole[y * box.W + x] = px >= r[0] && px < r[0] + r[2] && py >= r[1] && py < r[1] + r[3] ? 1f : 0f;
                        }

                    Img bg = m.Clone();
                    Inpaint.Fill(bg, hole, null, grain: false, relax: 240);
                    // --mode diff: a multi-coloured glyph (the gold spade): coverage from its difference to the background.
                    float[] cov = Get("mode") == "diff"
                        ? Matte.DiffAlpha(m, bg, 0.03f, 0.09f, true)
                        : Matte.InkCoverage(m, bg, Matte.InkColor(m, bg, dark));
                    for (int i = 0; i < cov.Length; i++) cov[i] *= hole[i];
                    double[] mb = GlyphBox(cov, box.W, box.H, thr);
                    double[] sb = GlyphBox(sp.A, sp.W, sp.H, thr);
                    if (mb == null || sb == null) { Console.WriteLine("no glyph found"); return 1; }
                    double mw = mb[2] - mb[0], mh = mb[3] - mb[1], sw = sb[2] - sb[0], sh = sb[3] - sb[1];
                    double s = (mw * sw + mh * sh) / (sw * sw + sh * sh);
                    double mcx = box.X + (mb[0] + mb[2]) / 2, mcy = box.Y + (mb[1] + mb[3]) / 2;
                    double scx = (sb[0] + sb[2]) / 2, scy = (sb[1] + sb[3]) / 2;
                    Console.WriteLine($"mockup glyph [{box.X + mb[0]:0.00}, {box.Y + mb[1]:0.00}, {mw:0.00}, {mh:0.00}] aspect {mw / mh:0.000};  sprite glyph {sw:0.0}x{sh:0.0} of {sp.W}x{sp.H} aspect {sw / sh:0.000}");
                    Console.WriteLine($"scale {s:0.0000}: glyph drawn {s * sw:0.00} x {s * sh:0.00} (mockup {mw:0.00} x {mh:0.00}: dw {s * sw - mw:+0.00;-0.00} dh {s * sh - mh:+0.00;-0.00})");
                    Console.WriteLine($"\"rect\": [{mcx - s * scx:0.00}, {mcy - s * scy:0.00}, {s * sp.W:0.00}, {s * sp.H:0.00}]");
                    if (o.ContainsKey("stretch"))
                    {
                        // The other option: glyph box onto glyph box, each axis with its own scale (a stretched line).
                        double kx = mw / sw, ky = mh / sh;
                        Console.WriteLine($"stretched (x {kx:0.0000}, y {ky:0.0000}, y/x {ky / kx:0.00}): \"rect\": [{box.X + mb[0] - sb[0] * kx:0.00}, {box.Y + mb[1] - sb[1] * ky:0.00}, {sp.W * kx:0.00}, {sp.H * ky:0.00}]");
                    }

                    return 0;
                }
                case "pairs":
                {
                    // A comparison sheet: for every rect (mockup px; "x,y,w,h;x,y,w,h;...") the mockup's
                    // crop above the same area of a 1920 x 1080 capture, both at --scale mockup px.
                    // --capture file --rects ... [--scale 3] --out file
                    Img cap = Img.Load(Get("capture"));
                    double sc = double.Parse(Get("scale", "3"));
                    double k = cap.W / (double)img.W;
                    var rows = new List<Img>();
                    foreach (string rs in Get("rects").Split(';'))
                    {
                        double[] r = rs.Split(',').Select(double.Parse).ToArray();
                        int w = (int)Math.Round(r[2] * sc), h = (int)Math.Round(r[3] * sc);
                        Img a = img.Crop((int)Math.Round(r[0]), (int)Math.Round(r[1]), (int)Math.Round(r[2]), (int)Math.Round(r[3])).Resize(w, h);
                        Img b = cap.Crop((int)Math.Round(r[0] * k), (int)Math.Round(r[1] * k), (int)Math.Round(r[2] * k), (int)Math.Round(r[3] * k)).Resize(w, h);
                        rows.Add(a);
                        rows.Add(b);
                    }

                    int W = rows.Max(x => x.W), H = rows.Sum(x => x.H) + 4 * rows.Count;
                    var sheet = new Img(W, H);
                    for (int i = 0; i < sheet.Count; i++) { sheet.R[i] = 0.2f; sheet.G[i] = 0.2f; sheet.B[i] = 0.25f; }
                    int yy = 0;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        sheet.Paste(rows[i], 0, yy);
                        yy += rows[i].H + (i % 2 == 0 ? 1 : 7);
                    }

                    for (int i = 0; i < sheet.Count; i++) sheet.A[i] = 1f;
                    sheet.Save(Get("out"));
                    Console.WriteLine($"{Get("out")} {W}x{H} ({rows.Count / 2} pairs: mockup above capture)");
                    return 0;
                }
                case "softshadow":
                {
                    // The soft drop shadow of a roundrect/pill control, solved against the mockup's
                    // own surroundings (arttool's Matte.Shadow with the background taken from a ring
                    // beyond --pad), so it carries no offset when the shared panel behind it has a
                    // slightly different ivory. Framing: the shape's rect grown by --pad, at --scale.
                    double[] r = Nums("rect");
                    double radius = double.Parse(Get("radius", "-1"));
                    double sc = double.Parse(Get("scale", "2"));
                    double pad = double.Parse(Get("pad", "8"));
                    var shape = new Shape(radius < 0 ? "pill" : "roundrect", r[0], r[1], r[2], r[3], radius < 0 ? 0 : radius);
                    Box box = Box.Around(r[0], r[1], r[2], r[3], pad + 8).Clamp(img.W, img.H);
                    Img m = img.Crop(box.X, box.Y, box.W, box.H);
                    var element = new float[m.Count];
                    var hole = new float[m.Count];
                    for (int y = 0; y < box.H; y++)
                        for (int x = 0; x < box.W; x++)
                        {
                            double px = box.X + x + 0.5, py = box.Y + y + 0.5;
                            element[y * box.W + x] = shape.Coverage(px, py);
                            hole[y * box.W + x] = shape.Coverage(px, py, 1, pad + 2);
                        }

                    Img bg = m.Clone();
                    Inpaint.Fill(bg, hole, null, grain: false, relax: 240);
                    (float[] sa, (float r, float g, float b) col) = Matte.Shadow(m, bg, element);
                    int W2 = (int)Math.Round((r[2] + 2 * pad) * sc), H2 = (int)Math.Round((r[3] + 2 * pad) * sc);
                    var outImg = new Img(W2, H2);
                    double sx = (r[2] + 2 * pad) / W2, sy = (r[3] + 2 * pad) / H2;
                    for (int j = 0; j < H2; j++)
                        for (int i = 0; i < W2; i++)
                        {
                            double u = r[0] - pad + (i + 0.5) * sx - box.X, v = r[1] - pad + (j + 0.5) * sy - box.Y;
                            double fx = u - 0.5, fy = v - 0.5;
                            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                            float tx = (float)(fx - x0), ty = (float)(fy - y0);
                            float A(int xx, int yy) => sa[Math.Clamp(yy, 0, box.H - 1) * box.W + Math.Clamp(xx, 0, box.W - 1)];
                            float a = (A(x0, y0) * (1 - tx) + A(x0 + 1, y0) * tx) * (1 - ty) + (A(x0, y0 + 1) * (1 - tx) + A(x0 + 1, y0 + 1) * tx) * ty;
                            // fade to nothing over the last 3 mockup px so the sprite never ends in an edge
                            double edge = Math.Min(Math.Min(i + 0.5, W2 - i - 0.5) * sx, Math.Min(j + 0.5, H2 - j - 0.5) * sy);
                            a *= (float)Math.Clamp(edge / 3.0, 0, 1);
                            int o2 = j * W2 + i;
                            outImg.R[o2] = col.r; outImg.G[o2] = col.g; outImg.B[o2] = col.b; outImg.A[o2] = Math.Clamp(a, 0f, 1f);
                        }

                    outImg.Save(Get("out"));
                    Console.WriteLine($"{Get("out")} {W2}x{H2} (shadow of {r[0]},{r[1]} {r[2]}x{r[3]}, pad {pad}, colour {Img.Hex(col.r, col.g, col.b)})");
                    return 0;
                }
                case "outline":
                {
                    // Outer edges of an outlined box (dark outline on light surroundings) from a rough
                    // rect: on 7 lines through the middle of each side, walk inward from 6 px outside
                    // to the outline's darkest pixel, then take the 50% crossing between the level
                    // 1.5 px outside that pixel and the pixel itself. Median per side.
                    double[] r = Nums("rect");
                    double reach = double.Parse(Get("reach", "6"));
                    double Side(int side)
                    {
                        var hits = new List<double>();
                        for (int k = 0; k < 7; k++)
                        {
                            double f = 0.35 + 0.05 * k;
                            // side 0 left, 1 right, 2 top, 3 bottom; walk from outside inward
                            double px0, py0, dx, dy;
                            switch (side)
                            {
                                case 0: px0 = r[0] - reach; py0 = r[1] + r[3] * f; dx = 1; dy = 0; break;
                                case 1: px0 = r[0] + r[2] + reach; py0 = r[1] + r[3] * f; dx = -1; dy = 0; break;
                                case 2: px0 = r[0] + r[2] * f; py0 = r[1] - reach; dx = 0; dy = 1; break;
                                default: px0 = r[0] + r[2] * f; py0 = r[1] + r[3] + reach; dx = 0; dy = -1; break;
                            }

                            var s = new List<(double t, double l)>();
                            for (double t = 0; t <= 2 * reach; t += 0.05) s.Add((t, LB(img, px0 + dx * t, py0 + dy * t)));
                            int m = 0;
                            for (int j = 1; j < s.Count; j++) if (s[j].l < s[m].l) m = j;
                            double outside = s.Where(v => v.t >= s[m].t - 1.65 && v.t <= s[m].t - 1.35).Select(v => v.l).DefaultIfEmpty(s[0].l).Average();
                            double half = (outside + s[m].l) / 2;
                            for (int j = m; j > 0; j--)
                            {
                                if (s[j - 1].l >= half && s[j].l < half)
                                {
                                    double t = s[j - 1].t + (s[j].t - s[j - 1].t) * (s[j - 1].l - half) / (s[j - 1].l - s[j].l);
                                    hits.Add(side == 0 ? px0 + t : side == 1 ? px0 - t : side == 2 ? py0 + t : py0 - t);
                                    break;
                                }
                            }
                        }

                        hits.Sort();
                        return hits.Count > 0 ? hits[hits.Count / 2] : double.NaN;
                    }

                    double L0 = Side(0), R0 = Side(1), T0 = Side(2), B0 = Side(3);
                    Console.WriteLine($"outline outer rect [{L0:0.00}, {T0:0.00}, {R0 - L0:0.00}, {B0 - T0:0.00}]  centre {(L0 + R0) / 2:0.00},{(T0 + B0) / 2:0.00}");
                    return 0;
                }
                case "alpha":
                {
                    // alpha statistics: border ring (3 px) max/mean, and a coarse map (max per cell)
                    int cell = int.Parse(Get("cell", "4"));
                    double bmax = 0, bsum = 0; int bk = 0;
                    for (int y = 0; y < img.H; y++)
                        for (int x = 0; x < img.W; x++)
                        {
                            if (Math.Min(Math.Min(x, y), Math.Min(img.W - 1 - x, img.H - 1 - y)) >= 3) continue;
                            float a = img.A[y * img.W + x]; bmax = Math.Max(bmax, a); bsum += a; bk++;
                        }

                    Console.WriteLine($"{img.W}x{img.H} border max {bmax:0.000} mean {bsum / bk:0.0000}");
                    for (int cy = 0; cy < img.H; cy += cell)
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int cx = 0; cx < img.W; cx += cell)
                        {
                            float m = 0;
                            for (int y = cy; y < Math.Min(img.H, cy + cell); y++) for (int x = cx; x < Math.Min(img.W, cx + cell); x++) m = Math.Max(m, img.A[y * img.W + x]);
                            sb.Append(m >= 0.5f ? '#' : m >= 0.1f ? '+' : m >= 0.03f ? ':' : m >= 0.01f ? '.' : ' ');
                        }

                        Console.WriteLine(sb.ToString());
                    }

                    return 0;
                }
                case "rgb":
                {
                    double[] rr = Nums("rect");
                    for (int y = (int)rr[1]; y < rr[1] + rr[3]; y++)
                    {
                        var sb = new System.Text.StringBuilder($"{y,4}:");
                        for (int x = (int)rr[0]; x < rr[0] + rr[2]; x++)
                        {
                            int i = y * img.W + x;
                            sb.Append($" {Img.Hex(img.R[i], img.G[i], img.B[i])}");
                        }

                        Console.WriteLine(sb.ToString());
                    }

                    return 0;
                }
            }

            Console.WriteLine("unknown");
            return 2;
        }

        // The box where a coverage map crosses 'thr' (left, top, right, bottom in its own pixels,
        // sub-pixel by interpolating the per-column / per-row maxima between pixel centres).
        private static double[] GlyphBox(float[] c, int w, int h, double thr)
        {
            var col = new double[w];
            var row = new double[h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = c[y * w + x];
                    if (v > col[x]) col[x] = v;
                    if (v > row[y]) row[y] = v;
                }

            double First(double[] p)
            {
                int i = Array.FindIndex(p, v => v >= thr);
                if (i < 0) return double.NaN;
                double before = i > 0 ? p[i - 1] : 0;
                return i - 0.5 + (thr - before) / Math.Max(1e-6, p[i] - before);     // pixel i spans [i, i + 1)
            }

            double Last(double[] p)
            {
                int i = Array.FindLastIndex(p, v => v >= thr);
                if (i < 0) return double.NaN;
                double after = i < p.Length - 1 ? p[i + 1] : 0;
                return i + 0.5 + (p[i] - thr) / Math.Max(1e-6, p[i] - after);
            }

            double l = First(col), rr = Last(col), t = First(row), b = Last(row);
            return double.IsNaN(l) || double.IsNaN(t) ? null : new[] { l, t, rr, b };
        }

        // A mockup by its number ("02") or any image by path; the repo is found from the working directory.
        private static string Mock(string s)
        {
            if (File.Exists(s)) return s;
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings"))))
            {
                dir = dir.Parent;
            }

            if (dir == null) throw new DirectoryNotFoundException("run from inside the Unity project");
            string mockups = Path.Combine(dir.FullName, "Assets", "ArtRefrences", "game-screens");
            return Directory.GetFiles(mockups, "*.png").First(f => Path.GetFileName(f).StartsWith(s, StringComparison.OrdinalIgnoreCase));
        }

        // sRGB luminance 0-255 of pixel centre (x, y) (integer pixel)
        private static double L(Img img, int x, int y)
        {
            x = Math.Clamp(x, 0, img.W - 1); y = Math.Clamp(y, 0, img.H - 1);
            return Img.ToSrgb01(img.Lum(y * img.W + x)) * 255;
        }

        // Bilinear linear-light colour at a continuous coordinate.
        private static (float, float, float) Bilinear(Img img, double px, double py)
        {
            double fx = px - 0.5, fy = py - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = (float)(fx - x0), ty = (float)(fy - y0);
            int Ix(int x) => Math.Clamp(x, 0, img.W - 1);
            int Iy(int y) => Math.Clamp(y, 0, img.H - 1);
            int a = Iy(y0) * img.W + Ix(x0), b = Iy(y0) * img.W + Ix(x0 + 1), c = Iy(y0 + 1) * img.W + Ix(x0), d = Iy(y0 + 1) * img.W + Ix(x0 + 1);
            float L(float[] ch) => (ch[a] * (1 - tx) + ch[b] * tx) * (1 - ty) + (ch[c] * (1 - tx) + ch[d] * tx) * ty;
            return (L(img.R), L(img.G), L(img.B));
        }

        // Bilinear luminance at a continuous mockup coordinate (pixel (i,j) covers [i,i+1)x[j,j+1)).
        private static double LB(Img img, double px, double py)
        {
            double fx = px - 0.5, fy = py - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            double a = L(img, x0, y0), b = L(img, x0 + 1, y0), c = L(img, x0, y0 + 1), d = L(img, x0 + 1, y0 + 1);
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        }

        public static bool Local;

        private static void Circle(Img img, double cx, double cy, double r, double from, double to, bool bright, double thr, double reach, bool verbose)
        {
            var pts = new List<(double x, double y, double ang)>();
            for (double ang = from; ang < to - 1e-6; ang += 3)
            {
                double rad = ang * Math.PI / 180;
                double dx = Math.Cos(rad), dy = Math.Sin(rad);   // screen coords: y down; 90 = bottom
                // samples from outside inward
                var s = new List<(double t, double l)>();
                for (double t = r + reach; t >= r - reach; t -= 0.1) s.Add((t, LB(img, cx + dx * t, cy + dy * t)));
                double bg = s.Take(15).Average(v => v.l);   // outermost 1.5 px
                // first feature coming inward that departs from bg by thr
                int k = s.FindIndex(v => bright ? v.l > bg + thr : v.l < bg - thr);
                if (k < 0) continue;
                // walk to the local extreme
                int m = k;
                while (m + 1 < s.Count && (bright ? s[m + 1].l >= s[m].l : s[m + 1].l <= s[m].l)) m++;
                double ext = s[m].l;
                double outer = bg;
                if (Local)
                {
                    // the level just outside the feature (within 2.5 px), so a soft shadow does not drag the edge outward
                    outer = s.Where(v => v.t > s[m].t + 1.35 && v.t <= s[m].t + 1.65).Select(v => v.l).DefaultIfEmpty(bg).Average();
                }

                double half = (outer + ext) / 2;
                // crossing between index 0..m of 'half'
                double edge = double.NaN;
                for (int j = 0; j < m; j++)
                {
                    bool cross = bright ? (s[j].l <= half && s[j + 1].l > half) : (s[j].l >= half && s[j + 1].l < half);
                    if (cross)
                    {
                        double f = (half - s[j].l) / (s[j + 1].l - s[j].l);
                        edge = s[j].t + (s[j + 1].t - s[j].t) * f;
                    }
                }

                if (double.IsNaN(edge)) continue;
                pts.Add((cx + dx * edge, cy + dy * edge, ang));
                if (verbose) Console.WriteLine($"  ang {ang,5:0} edge r {edge,6:0.00}  bg {bg,5:0} ext {ext,5:0}");
            }

            if (pts.Count < 5) { Console.WriteLine("too few points"); return; }
            // Kasa circle fit: x^2+y^2 + D x + E y + F = 0
            (double ccx, double ccy, double rr, double rms) FitCircle(List<(double x, double y, double ang)> p)
            {
                double[,] A = new double[3, 3]; double[] b = new double[3];
                foreach (var q in p)
                {
                    double[] row = { q.x, q.y, 1 };
                    double rhs = -(q.x * q.x + q.y * q.y);
                    for (int i = 0; i < 3; i++) { b[i] += row[i] * rhs; for (int j = 0; j < 3; j++) A[i, j] += row[i] * row[j]; }
                }

                double[] sol = Solve(A, b);
                double x0 = -sol[0] / 2, y0 = -sol[1] / 2, R = Math.Sqrt(x0 * x0 + y0 * y0 - sol[2]);
                double e = Math.Sqrt(p.Average(q => Math.Pow(Math.Sqrt((q.x - x0) * (q.x - x0) + (q.y - y0) * (q.y - y0)) - R, 2)));
                return (x0, y0, R, e);
            }

            var fit = FitCircle(pts);
            // drop outliers (> 2.5 rms) and refit
            var kept = pts.Where(q => Math.Abs(Math.Sqrt((q.x - fit.ccx) * (q.x - fit.ccx) + (q.y - fit.ccy) * (q.y - fit.ccy)) - fit.rr) < Math.Max(0.6, 2.5 * fit.rms)).ToList();
            if (kept.Count >= 5) fit = FitCircle(kept);
            Console.WriteLine($"circle centre {fit.ccx:0.00},{fit.ccy:0.00} r {fit.rr:0.00} rms {fit.rms:0.00} (n {kept.Count}/{pts.Count})  rect [{fit.ccx - fit.rr:0.0}, {fit.ccy - fit.rr:0.0}, {2 * fit.rr:0.0}, {2 * fit.rr:0.0}]");

            // Axis-aligned ellipse: a x^2 + b y^2 + c x + d y = 1
            {
                double[,] A = new double[4, 4]; double[] bb = new double[4];
                foreach (var q in kept)
                {
                    double X = q.x - fit.ccx, Y = q.y - fit.ccy;
                    double[] row = { X * X, Y * Y, X, Y };
                    for (int i = 0; i < 4; i++) { bb[i] += row[i]; for (int j = 0; j < 4; j++) A[i, j] += row[i] * row[j]; }
                }

                double[] s = Solve(A, bb);
                double ex = -s[2] / (2 * s[0]), ey = -s[3] / (2 * s[1]);
                double g = 1 + s[0] * ex * ex + s[1] * ey * ey;
                double ax = Math.Sqrt(g / s[0]), ay = Math.Sqrt(g / s[1]);
                ex += fit.ccx; ey += fit.ccy;
                Console.WriteLine($"ellipse centre {ex:0.00},{ey:0.00} rx {ax:0.00} ry {ay:0.00}  rect [{ex - ax:0.0}, {ey - ay:0.0}, {2 * ax:0.0}, {2 * ay:0.0}]");
            }
        }

        private static double[] Solve(double[,] A, double[] b)
        {
            int n = b.Length;
            var M = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) M[i, j] = A[i, j]; M[i, n] = b[i]; }
            for (int c = 0; c < n; c++)
            {
                int p = c;
                for (int r = c + 1; r < n; r++) if (Math.Abs(M[r, c]) > Math.Abs(M[p, c])) p = r;
                for (int j = 0; j <= n; j++) (M[c, j], M[p, j]) = (M[p, j], M[c, j]);
                for (int r = 0; r < n; r++)
                {
                    if (r == c) continue;
                    double f = M[r, c] / M[c, c];
                    for (int j = c; j <= n; j++) M[r, j] -= f * M[c, j];
                }
            }

            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = M[i, n] / M[i, i];
            return x;
        }
    }
}
