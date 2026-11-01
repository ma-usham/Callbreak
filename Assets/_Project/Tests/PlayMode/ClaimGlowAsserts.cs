using System.Collections;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Watches a <see cref="ClaimHighlight"/> (ADR 069) on a real screen: its halo's opacity and size, and
    /// its icon's place, turn and size, frame by frame on unscaled time.
    /// </summary>
    internal static class ClaimGlowAsserts
    {
        /// <summary>How long a look watches for movement: more than one breath.</summary>
        private const float Watch = 2.4f;

        /// <summary>The glow of the button named <paramref name="buttonName"/> under <paramref name="screen"/>.</summary>
        public static ClaimHighlight GlowOf(Component screen, string buttonName)
        {
            ClaimHighlight highlight = Find<RectTransform>(screen, buttonName).GetComponentInChildren<ClaimHighlight>(true);
            Assert.IsNotNull(highlight, buttonName + " has its glow");
            return highlight;
        }

        public static RectTransform Glyph(Component root, string name) => Find<RectTransform>(root, name);

        /// <summary>Watches the glow and its glyph for a while: both change.</summary>
        public static IEnumerator AssertMoves(ClaimHighlight highlight, RectTransform glyph, string what)
        {
            Assert.IsTrue(highlight.IsMoving, what + ": moving");
            Look first = Look.Of(highlight, glyph);
            bool glowChanged = false;
            bool glyphChanged = false;
            float until = Time.realtimeSinceStartup + Watch;
            while (Time.realtimeSinceStartup < until && !(glowChanged && glyphChanged))
            {
                yield return null;
                Look now = Look.Of(highlight, glyph);
                glowChanged |= now.GlowDiffers(first);
                glyphChanged |= now.GlyphDiffers(first);
            }

            Assert.IsTrue(glowChanged, what + ": the glow breathes");
            Assert.IsTrue(glyphChanged, what + ": the glyph nudges");
        }

        /// <summary>Watches the glow and its glyph for a while: the glow is lit, and nothing changes.</summary>
        public static IEnumerator AssertStill(ClaimHighlight highlight, RectTransform glyph, string what)
        {
            Assert.IsFalse(highlight.IsMoving, what + ": not moving");
            Assert.IsTrue(highlight.gameObject.activeInHierarchy, what + ": lit");
            Look first = Look.Of(highlight, glyph);
            Assert.Greater(first.GlowAlpha, 0.9f, what + ": at full strength");
            AssertAtRest(glyph, what + ": the glyph rests");
            float until = Time.realtimeSinceStartup + Watch;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Look now = Look.Of(highlight, glyph);
                Assert.IsFalse(now.GlowDiffers(first), what + ": the glow holds still");
                Assert.IsFalse(now.GlyphDiffers(first), what + ": the glyph holds still");
            }
        }

        public static void AssertAtRest(RectTransform glyph, string why)
        {
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, glyph.localEulerAngles.z), 0.01f, why);
            Assert.AreEqual(1f, glyph.localScale.x, 0.001f, why);
            Assert.AreEqual(1f, glyph.localScale.y, 0.001f, why);
        }

        public static T Find<T>(Component root, string name) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(true))
            {
                if (found.name == name)
                {
                    return found;
                }
            }

            Assert.Fail($"{root.name} has no {typeof(T).Name} named {name}");
            return null;
        }

        /// <summary>What the glow and its glyph look like at a moment.</summary>
        private readonly struct Look
        {
            private readonly float _glowScale;
            private readonly Vector3 _glyphPosition;
            private readonly float _glyphTurn;
            private readonly float _glyphScale;

            private Look(float glowAlpha, float glowScale, Vector3 glyphPosition, float glyphTurn, float glyphScale)
            {
                GlowAlpha = glowAlpha;
                _glowScale = glowScale;
                _glyphPosition = glyphPosition;
                _glyphTurn = glyphTurn;
                _glyphScale = glyphScale;
            }

            public float GlowAlpha { get; }

            public static Look Of(ClaimHighlight highlight, RectTransform glyph)
            {
                Transform halo = highlight.transform.Find("Halo");
                Assert.IsNotNull(halo, "the glow has its halo");
                return new Look(
                    halo.GetComponent<Graphic>().canvasRenderer.GetAlpha(),
                    halo.localScale.x,
                    glyph.localPosition,
                    glyph.localEulerAngles.z,
                    glyph.localScale.x);
            }

            public bool GlowDiffers(Look other) =>
                Mathf.Abs(GlowAlpha - other.GlowAlpha) > 0.01f || Mathf.Abs(_glowScale - other._glowScale) > 0.001f;

            public bool GlyphDiffers(Look other) =>
                (_glyphPosition - other._glyphPosition).sqrMagnitude > 0.01f
                || Mathf.Abs(Mathf.DeltaAngle(_glyphTurn, other._glyphTurn)) > 0.1f
                || Mathf.Abs(_glyphScale - other._glyphScale) > 0.001f;
        }
    }
}
