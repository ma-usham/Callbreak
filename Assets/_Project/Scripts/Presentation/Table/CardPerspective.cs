using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// Narrows the top of a card's image so it reads as lying on the table (mockup 08 draws the
    /// trick cards in the table's perspective: shorter, and narrower at the far edge). The quad is
    /// cut into horizontal strips so the artwork bends evenly instead of creasing along a diagonal.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class CardPerspective : BaseMeshEffect
    {
        private const int Strips = 6;

        [SerializeField, Range(0.5f, 1f)] private float _topScale = 1f;

        /// <summary>Width of the top edge relative to the bottom edge: 1 is a flat, upright card.</summary>
        public float TopScale
        {
            get => _topScale;
            set
            {
                value = Mathf.Clamp(value, 0.5f, 1f);
                if (Mathf.Approximately(_topScale, value))
                {
                    return;
                }

                _topScale = value;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || _topScale >= 0.999f || vh.currentVertCount != 4)
            {
                return;
            }

            // A simple Image is one quad: bottom-left, top-left, top-right, bottom-right.
            UIVertex bl = default, tl = default, tr = default, br = default;
            vh.PopulateUIVertex(ref bl, 0);
            vh.PopulateUIVertex(ref tl, 1);
            vh.PopulateUIVertex(ref tr, 2);
            vh.PopulateUIVertex(ref br, 3);
            float centre = (bl.position.x + br.position.x) * 0.5f;
            vh.Clear();
            for (int s = 0; s <= Strips; s++)
            {
                float t = s / (float)Strips;
                float scale = Mathf.Lerp(1f, _topScale, t);
                UIVertex left = Lerp(bl, tl, t), right = Lerp(br, tr, t);
                left.position.x = centre + (left.position.x - centre) * scale;
                right.position.x = centre + (right.position.x - centre) * scale;
                vh.AddVert(left);
                vh.AddVert(right);
                if (s > 0)
                {
                    int i = s * 2;
                    vh.AddTriangle(i - 2, i, i + 1);
                    vh.AddTriangle(i + 1, i - 1, i - 2);
                }
            }
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }
    }
}
