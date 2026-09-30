using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// A computer opponent's decisions (GDD §9). A strategy sees only a <see cref="PlayerView"/>,
    /// the same information a person in that seat has, so it cannot peek at other hands by
    /// construction. It must be deterministic for a given view and random sequence, so replays
    /// reproduce bot moves. Strategies are stateless between calls; memory of the round (voids,
    /// played cards) is rebuilt from the view's completed tricks.
    /// </summary>
    public interface IBotStrategy
    {
        BotLevel Level { get; }

        /// <summary>A call for the viewer's call turn, within the rule set's MinCall…MaxCall.</summary>
        int ChooseCall(PlayerView view, IRandom rng);

        /// <summary>A card from <c>view.LegalCards()</c> for the viewer's play turn.</summary>
        Card ChooseCard(PlayerView view, IRandom rng);
    }

    /// <summary>Bot strengths (GDD §9 table).</summary>
    public enum BotLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }
}
