using System;
using System.Collections.Generic;

namespace ArtTool
{
    /// <summary>
    /// Separates a drawn element from what is behind it. Everything here works on a local region
    /// (a crop of the mockup) together with an estimate of the background under the element.
    /// </summary>
    internal static class Matte
    {
        /// <summary>
        /// Estimates the background inside a region by filling the whole inner rectangle from a
        /// ring of <paramref name="ring"/> pixels around it. <paramref name="region"/> must already
        /// include that ring.
        /// </summary>
        public static Img BackgroundFromRing(Img region, int ring)
        {
            Img bg = region.Clone();
            var hole = new float[region.Count];
            for (int y = ring; y < region.H - ring; y++)
            {
                for (int x = ring; x < region.W - ring; x++) hole[y * region.W + x] = 1f;
            }

            Inpaint.Fill(bg, hole, null, grain: false, relax: 220);
            return bg;
        }

        /// <summary>
        /// Estimates the ink colour of text or a flat icon inside a region: the mean of the most
        /// extreme fifth of pixels that clearly differ from the background.
        /// </summary>
        public static (float r, float g, float b) InkColor(Img m, Img bg, bool dark)
        {
            var cands = new List<(float key, int i)>();
            for (int i = 0; i < m.Count; i++)
            {
                float d = bg.Lum(i) - m.Lum(i);
                if (!dark) d = -d;
                if (d > 0.02f) cands.Add((d, i));
            }

            if (cands.Count == 0) return dark ? (0f, 0f, 0f) : (1f, 1f, 1f);
            cands.Sort((a, b) => b.key.CompareTo(a.key));
            int take = Math.Max(1, cands.Count / 5);
            float r = 0, g = 0, b = 0;
            for (int k = 0; k < take; k++)
            {
                int i = cands[k].i;
                r += m.R[i]; g += m.G[i]; b += m.B[i];
            }

            return (r / take, g / take, b / take);
        }

        /// <summary>
        /// Coverage of a flat-coloured ink over a known background: the projection of
        /// (background - pixel) onto (background - ink), per pixel, clamped to [0, 1].
        /// </summary>
        public static float[] InkCoverage(Img m, Img bg, (float r, float g, float b) ink)
        {
            var c = new float[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                float dr = bg.R[i] - ink.r, dg = bg.G[i] - ink.g, db = bg.B[i] - ink.b;
                float den = dr * dr + dg * dg + db * db;
                if (den < 1e-6f) { c[i] = 0f; continue; }
                float num = (bg.R[i] - m.R[i]) * dr + (bg.G[i] - m.G[i]) * dg + (bg.B[i] - m.B[i]) * db;
                c[i] = Math.Clamp(num / den, 0f, 1f);
            }

            return c;
        }

        /// <summary>
        /// Alpha for a multi-coloured object from its difference to the background, with
        /// smoothstep between <paramref name="lo"/> and <paramref name="hi"/> (linear-light RGB
        /// distance). With <paramref name="solid"/>, everything enclosed by the object's outline is
        /// made opaque, so areas that happen to match the background colour do not turn into holes.
        /// </summary>
        public static float[] DiffAlpha(Img m, Img bg, float lo, float hi, bool solid)
        {
            int w = m.W, h = m.H;
            var a = new float[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                float dr = m.R[i] - bg.R[i], dg = m.G[i] - bg.G[i], db = m.B[i] - bg.B[i];
                float d = MathF.Sqrt(dr * dr + dg * dg + db * db);
                float t = Math.Clamp((d - lo) / Math.Max(1e-5f, hi - lo), 0f, 1f);
                a[i] = t * t * (3 - 2 * t);
            }

            if (!solid) return a;

            // Flood the outside from the region border through low-alpha pixels; whatever the
            // flood cannot reach is inside the outline.
            var outside = new bool[m.Count];
            var stack = new Stack<int>();
            void Push(int x, int y)
            {
                int i = y * w + x;
                if (outside[i] || a[i] >= 0.5f) return;
                outside[i] = true;
                stack.Push(i);
            }

            for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
            for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % w, y = i / w;
                if (x > 0) Push(x - 1, y);
                if (x < w - 1) Push(x + 1, y);
                if (y > 0) Push(x, y - 1);
                if (y < h - 1) Push(x, y + 1);
            }

            for (int i = 0; i < m.Count; i++)
            {
                if (!outside[i]) a[i] = Math.Max(a[i], 1f);
            }

            return a;
        }

        /// <summary>
        /// Recovers the element's own colour where it is partly transparent, so its edges carry no
        /// trace of the background it was painted on: F = (M - (1 - α)·B) / α.
        /// </summary>
        public static void Decontaminate(Img m, Img bg, float[] alpha, Img outColor)
        {
            for (int i = 0; i < m.Count; i++)
            {
                float a = alpha[i];
                if (a >= 0.985f || a <= 0.02f)
                {
                    outColor.R[i] = m.R[i]; outColor.G[i] = m.G[i]; outColor.B[i] = m.B[i];
                    continue;
                }

                outColor.R[i] = Math.Clamp((m.R[i] - (1 - a) * bg.R[i]) / a, 0f, 1f);
                outColor.G[i] = Math.Clamp((m.G[i] - (1 - a) * bg.G[i]) / a, 0f, 1f);
                outColor.B[i] = Math.Clamp((m.B[i] - (1 - a) * bg.B[i]) / a, 0f, 1f);
            }
        }

        /// <summary>
        /// Spreads the colour of visible pixels into fully transparent ones, so bilinear filtering
        /// at the sprite's edge never pulls in black.
        /// </summary>
        public static void BleedColor(Img img, float threshold = 0.02f)
        {
            var hole = new float[img.Count];
            bool any = false;
            for (int i = 0; i < img.Count; i++)
            {
                if (img.A[i] <= threshold) { hole[i] = 1f; any = true; }
            }

            if (!any) return;
            float[] a = (float[])img.A.Clone();
            Inpaint.Fill(img, hole, null, grain: false, relax: 20);
            Array.Copy(a, img.A, a.Length);
        }

        /// <summary>
        /// Extracts a soft drop shadow: where the element does not cover the pixel, how far the
        /// mockup is darker than the clean background along the direction of the shadow colour.
        /// Under the element the shadow is continued from outside, since it is hidden there.
        /// Returns the shadow alpha and the colour it was solved for.
        /// </summary>
        public static (float[] alpha, (float r, float g, float b) color) Shadow(Img m, Img bg, float[] elementAlpha)
        {
            int n = m.Count;
            double sr = 0, sg = 0, sb = 0, br = 0, bgg = 0, bb = 0; int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (elementAlpha[i] > 0.001f) continue;
                float dl = bg.Lum(i) - m.Lum(i);
                if (dl < 0.004f) continue;
                sr += bg.R[i] - m.R[i]; sg += bg.G[i] - m.G[i]; sb += bg.B[i] - m.B[i];
                br += bg.R[i]; bgg += bg.G[i]; bb += bg.B[i]; k++;
            }

            var alpha = new float[n];
            if (k < 8) return (alpha, (0.02f, 0.012f, 0.008f));

            // The shadow colour: walk from the mean background along the mean darkening direction
            // until a channel reaches (nearly) black. Warm shadows then stay warm.
            double len = Math.Sqrt(sr * sr + sg * sg + sb * sb);
            double dxr = sr / len, dxg = sg / len, dxb = sb / len;
            double mr = br / k, mg = bgg / k, mb = bb / k;
            double t = double.MaxValue;
            if (dxr > 1e-6) t = Math.Min(t, (mr - 0.004) / dxr);
            if (dxg > 1e-6) t = Math.Min(t, (mg - 0.004) / dxg);
            if (dxb > 1e-6) t = Math.Min(t, (mb - 0.004) / dxb);
            if (t == double.MaxValue || t <= 0) t = 0.5;
            var col = ((float)Math.Clamp(mr - t * dxr, 0, 1), (float)Math.Clamp(mg - t * dxg, 0, 1), (float)Math.Clamp(mb - t * dxb, 0, 1));

            var hidden = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (elementAlpha[i] > 0.001f) { hidden[i] = 1f; continue; }
                float dr = bg.R[i] - col.Item1, dg = bg.G[i] - col.Item2, db = bg.B[i] - col.Item3;
                float den = dr * dr + dg * dg + db * db;
                float num = (bg.R[i] - m.R[i]) * dr + (bg.G[i] - m.G[i]) * dg + (bg.B[i] - m.B[i]) * db;
                alpha[i] = den < 1e-6f ? 0f : Math.Clamp(num / den, 0f, 1f);
            }

            // Continue the shadow under the element from its surroundings.
            var tmp = new Img(m.W, m.H);
            Array.Copy(alpha, tmp.R, n);
            Inpaint.Fill(tmp, hidden, null, grain: false, relax: 60);
            float[] filled = tmp.R;

            // Keep only the soft component: grain in the difference is background texture, not shadow.
            float[] soft = Inpaint.Blur(filled, m.W, m.H, 1);
            for (int i = 0; i < n; i++) soft[i] = Math.Clamp(soft[i], 0f, 1f);
            return (soft, col);
        }
    }
}
