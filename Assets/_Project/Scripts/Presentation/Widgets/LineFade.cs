using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Fades a thin rule out towards one or both ends, as the hairlines beside the title medals
    /// and under the rules do on the mockups: transparent at the very end, solid after the given
    /// length (canvas units). Works on a simple (unsliced) Image.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class LineFade : BaseMeshEffect
    {
        [SerializeField] private float _left;
        [SerializeField] private float _right;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4)
            {
                return;
            }

            // A simple Image's quad: bottom-left, top-left, top-right, bottom-right.
            UIVertex bottomLeft = default, topLeft = default, topRight = default, bottomRight = default;
            vh.PopulateUIVertex(ref bottomLeft, 0);
            vh.PopulateUIVertex(ref topLeft, 1);
            vh.PopulateUIVertex(ref topRight, 2);
            vh.PopulateUIVertex(ref bottomRight, 3);
            float width = bottomRight.position.x - bottomLeft.position.x;
            if (width <= 0f)
            {
                return;
            }

            float left = Mathf.Clamp(_left, 0f, width);
            float right = Mathf.Clamp(_right, 0f, width - left);

            vh.Clear();
            AddColumn(vh, bottomLeft, topLeft, bottomRight, topRight, 0f, left > 0f ? 0f : 1f);
            AddColumn(vh, bottomLeft, topLeft, bottomRight, topRight, left / width, 1f);
            AddColumn(vh, bottomLeft, topLeft, bottomRight, topRight, 1f - right / width, 1f);
            AddColumn(vh, bottomLeft, topLeft, bottomRight, topRight, 1f, right > 0f ? 0f : 1f);
            for (int column = 0; column < 3; column++)
            {
                int i = column * 2;
                vh.AddTriangle(i, i + 1, i + 3);
                vh.AddTriangle(i + 3, i + 2, i);
            }
        }

        private static void AddColumn(VertexHelper vh, UIVertex bottomLeft, UIVertex topLeft, UIVertex bottomRight, UIVertex topRight, float t, float alpha)
        {
            vh.AddVert(Lerp(bottomLeft, bottomRight, t, alpha));
            vh.AddVert(Lerp(topLeft, topRight, t, alpha));
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t, float alpha)
        {
            UIVertex v = a;
            v.position = Vector3.LerpUnclamped(a.position, b.position, t);
            v.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            Color32 colour = a.color;
            colour.a = (byte)Mathf.RoundToInt(colour.a * alpha);
            v.color = colour;
            return v;
        }
    }
}
