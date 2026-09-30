using System;

namespace ArtTool
{
    /// <summary>
    /// Analytic shapes in mockup pixel coordinates (top-left origin, pixel (i, j) covers
    /// [i, i+1) × [j, j+1)). Coverage comes from a signed distance, so edges are anti-aliased
    /// exactly at any output scale.
    /// </summary>
    internal readonly struct Shape
    {
        public readonly string Type;   // rect | roundrect | pill | ellipse
        public readonly double X;
        public readonly double Y;
        public readonly double W;
        public readonly double H;
        public readonly double Radius;

        public Shape(string type, double x, double y, double w, double h, double radius)
        {
            Type = string.IsNullOrEmpty(type) ? "rect" : type;
            X = x;
            Y = y;
            W = w;
            H = h;
            Radius = radius;
        }

        public bool IsGeometric => Type == "rect" || Type == "roundrect" || Type == "pill" || Type == "ellipse";

        public double Left => X;
        public double Top => Y;
        public double Right => X + W;
        public double Bottom => Y + H;

        /// <summary>Signed distance in mockup pixels from (px, py) to the edge, negative inside.</summary>
        public double Sdf(double px, double py)
        {
            double cx = X + W / 2, cy = Y + H / 2;
            double dx = px - cx, dy = py - cy;
            switch (Type)
            {
                case "ellipse":
                {
                    double rx = W / 2, ry = H / 2;
                    if (Math.Abs(rx - ry) < 0.5)
                    {
                        return Math.Sqrt(dx * dx + dy * dy) - (rx + ry) / 2;
                    }

                    // Inigo Quilez's ellipse distance approximation: exact on the axes, close elsewhere.
                    double k0 = Math.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry));
                    double k1 = Math.Sqrt(dx * dx / (rx * rx * rx * rx) + dy * dy / (ry * ry * ry * ry));
                    return k1 < 1e-9 ? -Math.Min(rx, ry) : k0 * (k0 - 1.0) / k1;
                }
                default:
                {
                    double r = Type == "pill" ? Math.Min(W, H) / 2 : (Type == "roundrect" ? Radius : 0);
                    r = Math.Min(r, Math.Min(W, H) / 2);
                    double qx = Math.Abs(dx) - (W / 2 - r);
                    double qy = Math.Abs(dy) - (H / 2 - r);
                    double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
                    return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
                }
            }
        }

        /// <summary>
        /// Coverage of the pixel centred at (px, py) whose size is <paramref name="pixel"/> mockup
        /// pixels, for the shape grown outward by <paramref name="grow"/> mockup pixels.
        /// </summary>
        public float Coverage(double px, double py, double pixel = 1.0, double grow = 0.0)
        {
            double d = (Sdf(px, py) - grow) / pixel;
            return (float)Math.Clamp(0.5 - d, 0.0, 1.0);
        }

        public Shape Grown(double by)
        {
            string t = Type == "rect" ? "roundrect" : Type;
            double r = Type == "rect" ? by : Radius + by;
            return new Shape(t, X - by, Y - by, W + 2 * by, H + 2 * by, r);
        }

        public override string ToString() => $"{Type} {X:0.##},{Y:0.##} {W:0.##}x{H:0.##} r{Radius:0.##}";
    }

    /// <summary>An axis-aligned integer pixel box, used for working regions.</summary>
    internal readonly struct Box
    {
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public Box(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = Math.Max(0, w);
            H = Math.Max(0, h);
        }

        public int Right => X + W;
        public int Bottom => Y + H;
        public bool IsEmpty => W <= 0 || H <= 0;

        public static Box Around(double x, double y, double w, double h, double pad)
        {
            int x0 = (int)Math.Floor(x - pad), y0 = (int)Math.Floor(y - pad);
            int x1 = (int)Math.Ceiling(x + w + pad), y1 = (int)Math.Ceiling(y + h + pad);
            return new Box(x0, y0, x1 - x0, y1 - y0);
        }

        public Box Clamp(int w, int h)
        {
            int x0 = Math.Max(0, X), y0 = Math.Max(0, Y);
            int x1 = Math.Min(w, Right), y1 = Math.Min(h, Bottom);
            return new Box(x0, y0, x1 - x0, y1 - y0);
        }

        public Box Union(Box o)
        {
            if (IsEmpty) return o;
            if (o.IsEmpty) return this;
            int x0 = Math.Min(X, o.X), y0 = Math.Min(Y, o.Y);
            return new Box(x0, y0, Math.Max(Right, o.Right) - x0, Math.Max(Bottom, o.Bottom) - y0);
        }

        public override string ToString() => $"{X},{Y} {W}x{H}";
    }
}
