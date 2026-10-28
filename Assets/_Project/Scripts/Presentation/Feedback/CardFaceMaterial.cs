using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Feedback
{
    /// <summary>
    /// The material of an image that shows a card's face (ADR 054). A face drawn over its deck's
    /// base is one of the shared faces, stored premultiplied, and needs the premultiplied UI material
    /// (Callbreak/UI/Premultiplied); a back, or a face shown on its own, draws with the default UI
    /// material like any other picture.
    /// </summary>
    public static class CardFaceMaterial
    {
        /// <param name="face">The image that shows the face or the back.</param>
        /// <param name="premultiplied">The premultiplied face material (the card face builder's).</param>
        /// <param name="overBase">True while the image shows a shared face over a deck's base.</param>
        public static void Apply(Image face, Material premultiplied, bool overBase)
        {
            if (face == null)
            {
                return;
            }

            Material wanted = overBase ? premultiplied : null;
            if (face.material != (wanted != null ? wanted : face.defaultMaterial))
            {
                face.material = wanted;
            }
        }
    }
}
