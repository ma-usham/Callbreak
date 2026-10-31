namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// How the shop opens next (ADR 067): as itself, or straight on the festival offer's theme in its
    /// preview, as a popup over the main menu whose cross goes back to the menu. The main menu asks
    /// for the offer just before it pushes the shop; the shop takes the request as it opens.
    /// </summary>
    public sealed class ShopOpening
    {
        private bool _offer;

        /// <summary>The next opening of the shop shows the festival offer.</summary>
        public void RequestOffer() => _offer = true;

        /// <summary>True once after <see cref="RequestOffer"/>.</summary>
        public bool TakeOffer()
        {
            bool offer = _offer;
            _offer = false;
            return offer;
        }
    }
}
