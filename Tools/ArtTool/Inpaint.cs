using System;
using System.Collections.Generic;

namespace ArtTool
{
    /// <summary>
    /// Hole filling for flat and softly graded art (parchment, ivory panels, satin buttons, hazy
    /// sky): a push-pull pyramid gives every hole pixel a smooth guess from its surroundings, a
    /// harmonic relaxation removes the pyramid's blockiness, and grain borrowed from the
    /// surroundings keeps the fill from looking airbrushed. It is not meant for detailed painted
    /// scenery; that gets a generated plate instead.
    /// </summary>
    internal static class Inpaint
    {
        /// <param name="img">Image whose hole pixels are replaced in place.</param>
        /// <param name="hole">Per pixel, 1 = fill this pixel, 0 = keep it (soft values blend).</param>
        /// <param name="valid">Per pixel, 1 = may be used as a source (null: every non-hole pixel).</param>
        public static void Fill(Img img, float[] hole, float[] valid = null, bool grain = true, int relax = 160, int seed = 17)
        {
            int w = img.W, h = img.H, n = w * h;
            bool any = false;
            for (int i = 0; i < n; i++) if (hole[i] > 0.001f) { any = true; break; }
            if (!any) return;

            var wt = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = valid == null ? 1f : valid[i];
                wt[i] = Math.Clamp((1f - hole[i]) * v, 0f, 1f);
            }

            float[] fr = (float[])img.R.Clone(), fg = (float[])img.G.Clone(), fb = (float[])img.B.Clone();
            PushPull(fr, fg, fb, wt, w, h);

            // Relax the hole towards a harmonic (membrane) surface. Neighbours that are neither
            // source nor hole (outside the allowed area) do not take part.
            var inHole = new bool[n];
            var usable = new bool[n];
            for (int i = 0; i < n; i++)
            {
                inHole[i] = hole[i] > 0.001f;
                usable[i] = inHole[i] || wt[i] > 0.5f;
            }

            for (int it = 0; it < relax; it++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!inHole[i]) continue;
                        float sr = 0, sg = 0, sb = 0; int k = 0;
                        if (x > 0 && usable[i - 1]) { sr += fr[i - 1]; sg += fg[i - 1]; sb += fb[i - 1]; k++; }
                        if (x < w - 1 && usable[i + 1]) { sr += fr[i + 1]; sg += fg[i + 1]; sb += fb[i + 1]; k++; }
                        if (y > 0 && usable[i - w]) { sr += fr[i - w]; sg += fg[i - w]; sb += fb[i - w]; k++; }
                        if (y < h - 1 && usable[i + w]) { sr += fr[i + w]; sg += fg[i + w]; sb += fb[i + w]; k++; }
                        if (k == 0) continue;
                        fr[i] = sr / k; fg[i] = sg / k; fb[i] = sb / k;
                    }
                }
            }

            if (grain)
            {
                AddGrain(img, fr, fg, fb, hole, wt, seed);
            }

            for (int i = 0; i < n; i++)
            {
                float a = Math.Clamp(hole[i], 0f, 1f);
                if (a <= 0f) continue;
                img.R[i] = img.R[i] * (1 - a) + fr[i] * a;
                img.G[i] = img.G[i] * (1 - a) + fg[i] * a;
                img.B[i] = img.B[i] * (1 - a) + fb[i] * a;
            }
        }

        private static void PushPull(float[] r, float[] g, float[] b, float[] wt, int w, int h)
        {
            var levels = new List<(float[] r, float[] g, float[] b, float[] w, int W, int H)>();
            levels.Add((r, g, b, (float[])wt.Clone(), w, h));
            int cw = w, ch = h;
            while (cw > 1 || ch > 1)
            {
                var prev = levels[levels.Count - 1];
                int nw = Math.Max(1, (cw + 1) / 2), nh = Math.Max(1, (ch + 1) / 2);
                var nr = new float[nw * nh];
                var ng = new float[nw * nh];
                var nb = new float[nw * nh];
                var nwt = new float[nw * nh];
                for (int y = 0; y < nh; y++)
                {
                    for (int x = 0; x < nw; x++)
                    {
                        float sr = 0, sg = 0, sb = 0, sw = 0;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            int yy = y * 2 + dy;
                            if (yy >= ch) continue;
                            for (int dx = 0; dx < 2; dx++)
                            {
                                int xx = x * 2 + dx;
                                if (xx >= cw) continue;
                                int s = yy * cw + xx;
                                float ww = prev.w[s];
                                sr += prev.r[s] * ww; sg += prev.g[s] * ww; sb += prev.b[s] * ww; sw += ww;
                            }
                        }

                        int t = y * nw + x;
                        if (sw > 0)
                        {
                            nr[t] = sr / sw; ng[t] = sg / sw; nb[t] = sb / sw;
                        }

                        nwt[t] = Math.Min(1f, sw);
                    }
                }

                levels.Add((nr, ng, nb, nwt, nw, nh));
                cw = nw;
                ch = nh;
            }

            // Push: fill each level's unknowns from the (already filled) coarser level, bilinearly.
            for (int l = levels.Count - 2; l >= 0; l--)
            {
                var fine = levels[l];
                var coarse = levels[l + 1];
                for (int y = 0; y < fine.H; y++)
                {
                    double cy = (y + 0.5) / 2.0 - 0.5;
                    int y0 = (int)Math.Floor(cy);
                    double ty = cy - y0;
                    int ya = Math.Clamp(y0, 0, coarse.H - 1), yb = Math.Clamp(y0 + 1, 0, coarse.H - 1);
                    for (int x = 0; x < fine.W; x++)
                    {
                        int i = y * fine.W + x;
                        float fw = fine.w[i];
                        if (fw >= 1f) continue;
                        double cx = (x + 0.5) / 2.0 - 0.5;
                        int x0 = (int)Math.Floor(cx);
                        double tx = cx - x0;
                        int xa = Math.Clamp(x0, 0, coarse.W - 1), xb = Math.Clamp(x0 + 1, 0, coarse.W - 1);
                        float Lerp(float[] c) =>
                            (float)((c[ya * coarse.W + xa] * (1 - tx) + c[ya * coarse.W + xb] * tx) * (1 - ty) +
                                    (c[yb * coarse.W + xa] * (1 - tx) + c[yb * coarse.W + xb] * tx) * ty);
                        fine.r[i] = fine.r[i] * fw + Lerp(coarse.r) * (1 - fw);
                        fine.g[i] = fine.g[i] * fw + Lerp(coarse.g) * (1 - fw);
                        fine.b[i] = fine.b[i] * fw + Lerp(coarse.b) * (1 - fw);
                        fine.w[i] = 1f;
                    }
                }
            }
        }

        /// <summary>
        /// Adds high-frequency grain to the hole, borrowed pixel by pixel from the source area's own
        /// residual (image minus its blur), skipping outliers such as edges of nearby shapes.
        /// </summary>
        private static void AddGrain(Img img, float[] fr, float[] fg, float[] fb, float[] hole, float[] wt, int seed)
        {
            int w = img.W, h = img.H, n = w * h;
            float[] br = Blur(img.R, w, h, 2), bg = Blur(img.G, w, h, 2), bb = Blur(img.B, w, h, 2);
            var donors = new List<int>();
            var mags = new List<float>();
            for (int i = 0; i < n; i++)
            {
                if (wt[i] < 0.99f) continue;
                float m = Math.Abs(img.R[i] - br[i]) + Math.Abs(img.G[i] - bg[i]) + Math.Abs(img.B[i] - bb[i]);
                donors.Add(i);
                mags.Add(m);
            }

            if (donors.Count < 16) return;
            var sorted = mags.ToArray();
            Array.Sort(sorted);
            float limit = sorted[(int)(sorted.Length * 0.9)] * 1.5f + 1e-5f;
            var good = new List<int>(donors.Count);
            for (int k = 0; k < donors.Count; k++) if (mags[k] <= limit) good.Add(donors[k]);
            if (good.Count < 16) return;

            var rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                if (hole[i] <= 0.001f) continue;
                int d = good[rng.Next(good.Count)];
                fr[i] += img.R[d] - br[d];
                fg[i] += img.G[d] - bg[d];
                fb[i] += img.B[d] - bb[d];
            }
        }

        /// <summary>Three passes of a separable box blur of the given radius (close to a Gaussian).</summary>
        public static float[] Blur(float[] src, int w, int h, int radius)
        {
            var a = (float[])src.Clone();
            var t = new float[a.Length];
            if (radius <= 0) return a;
            for (int pass = 0; pass < 3; pass++)
            {
                BoxH(a, t, w, h, radius);
                BoxV(t, a, w, h, radius);
            }

            return a;
        }

        private static void BoxH(float[] src, float[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0;
                for (int i = -r; i <= r; i++) sum += src[row + Math.Clamp(i, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    dst[row + x] = sum * inv;
                    sum += src[row + Math.Clamp(x + r + 1, 0, w - 1)] - src[row + Math.Clamp(x - r, 0, w - 1)];
                }
            }
        }

        private static void BoxV(float[] src, float[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                for (int i = -r; i <= r; i++) sum += src[Math.Clamp(i, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum * inv;
                    sum += src[Math.Clamp(y + r + 1, 0, h - 1) * w + x] - src[Math.Clamp(y - r, 0, h - 1) * w + x];
                }
            }
        }

        /// <summary>Grows a mask by a square neighbourhood of the given radius (max filter).</summary>
        public static float[] Dilate(float[] mask, int w, int h, int radius)
        {
            if (radius <= 0) return (float[])mask.Clone();
            var t = new float[mask.Length];
            var o = new float[mask.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float m = 0;
                    for (int k = Math.Max(0, x - radius); k <= Math.Min(w - 1, x + radius); k++) m = Math.Max(m, mask[y * w + k]);
                    t[y * w + x] = m;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float m = 0;
                    for (int k = Math.Max(0, y - radius); k <= Math.Min(h - 1, y + radius); k++) m = Math.Max(m, t[k * w + x]);
                    o[y * w + x] = m;
                }
            }

            return o;
        }
    }
}
