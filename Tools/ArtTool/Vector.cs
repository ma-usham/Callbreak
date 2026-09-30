using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace ArtTool
{
    /// <summary>
    /// Rebuilt geometric UI (Designer Handbook §4 "rebuild as vectors"): pills, chips, toggles,
    /// rings, fields and tiles drawn from measured colours at @2x, so they stay sharp and every
    /// state can be produced from the same numbers.
    /// </summary>
    internal static class Vector
    {
        /// <param name="spec">type (roundrect|pill|ellipse|rect|ring), radius, fill, fill2 (bottom of a vertical gradient),
        /// stroke, strokeWidth, opacity; all lengths in mockup pixels.</param>
        public static Img Make(JsonObject spec, double w, double h, double scale)
        {
            string type = spec["type"]?.ToString() ?? "roundrect";
            double radius = Num(spec, "radius", 0);
            double strokeW = Num(spec, "strokeWidth", 0);
            double opacity = Num(spec, "opacity", 1);
            var fill = Color(spec, "fill", "#FFFFFF");
            var fill2 = spec["fill2"] != null ? Color(spec, "fill2", "#FFFFFF") : fill;
            var stroke = spec["stroke"] != null ? Color(spec, "stroke", "#000000") : fill;
            bool ring = type == "ring";

            int pw = Math.Max(1, (int)Math.Ceiling(w * scale)), ph = Math.Max(1, (int)Math.Ceiling(h * scale));
            var img = new Img(pw, ph);
            string shapeType = ring ? "ellipse" : type;
            var outer = new Shape(shapeType, 0, 0, w, h, radius);
            var inner = new Shape(shapeType == "rect" ? "roundrect" : shapeType, strokeW, strokeW, w - 2 * strokeW, h - 2 * strokeW, Math.Max(0, radius - strokeW));
            double px = 1.0 / scale;
            for (int y = 0; y < ph; y++)
            {
                double my = (y + 0.5) * px;
                double t = h > 0 ? Math.Clamp(my / h, 0, 1) : 0;
                float fr = (float)(fill.r + (fill2.r - fill.r) * t), fg = (float)(fill.g + (fill2.g - fill.g) * t), fb = (float)(fill.b + (fill2.b - fill.b) * t);
                for (int x = 0; x < pw; x++)
                {
                    double mx = (x + 0.5) * px;
                    float ao = outer.Coverage(mx, my, px);
                    float ai = strokeW > 0 ? inner.Coverage(mx, my, px) : ao;
                    int i = y * pw + x;
                    if (ring)
                    {
                        img.R[i] = stroke.r; img.G[i] = stroke.g; img.B[i] = stroke.b;
                        img.A[i] = (float)(Math.Max(0, ao - ai) * opacity);
                        continue;
                    }

                    // Stroke band = outer minus inner; the fill shows through the inner part.
                    float band = Math.Max(0, ao - ai);
                    float a = ao;
                    if (a <= 0f) { img.R[i] = fr; img.G[i] = fg; img.B[i] = fb; img.A[i] = 0f; continue; }
                    float k = band / a;
                    img.R[i] = fr * (1 - k) + stroke.r * k;
                    img.G[i] = fg * (1 - k) + stroke.g * k;
                    img.B[i] = fb * (1 - k) + stroke.b * k;
                    img.A[i] = (float)(a * opacity);
                }
            }

            return img;
        }

        public static string Describe(JsonObject spec)
        {
            return string.Join(" ", new[] { "type", "radius", "fill", "fill2", "stroke", "strokeWidth" }
                .Where(k => spec[k] != null).Select(k => $"{k}={spec[k]}"));
        }

        private static double Num(JsonObject o, string k, double d) => o[k] is JsonValue v ? (double)v : d;

        private static (float r, float g, float b) Color(JsonObject o, string k, string d) => Img.ParseHex(o[k]?.ToString() ?? d);
    }

}
