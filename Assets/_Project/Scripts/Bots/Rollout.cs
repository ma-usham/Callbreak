using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Bots
{
    /// <summary>
    /// Plays one sampled deal to the end of the round (GDD §9 Hard: "rolling out every sample with the Normal
    /// policy"): <see cref="NormalPolicy"/> chooses for every seat, each from its own hand plus the public
    /// state, exactly as a Normal bot would play it. Hands are card masks indexed by seat. Allocation-free
    /// after construction; one instance serves every sample and candidate of a decision.
    /// </summary>
    internal sealed class Rollout
    {
        private readonly RoundState _start;
        private readonly RoundState _table;
        private readonly ulong[] _hands = new ulong[SeatExtensions.Count];
        private readonly int _viewer;
        private readonly int _spoilPercent;

        internal Rollout(CardTracker start, int spoilPercent)
        {
            _start = start.State;
            _table = new RoundState(start.Rules);
            _viewer = (int)start.Viewer;
            _spoilPercent = spoilPercent;
        }

        /// <summary>
        /// Plays the card <paramref name="first"/> for the viewer from <paramref name="deal"/> and the rest of the
        /// round with the Normal policy, and returns the result's value for the viewer (see <see cref="Value"/>).
        /// </summary>
        internal int Run(ulong[] deal, int first)
        {
            Reset(deal);
            _hands[_viewer] &= ~(1UL << first);
            _table.Play(_viewer, first);
            PlayOut();
            return Value();
        }

        /// <summary>
        /// Plays the whole round from the start with the Normal policy and the given calls (indexed by seat),
        /// and returns the tricks the viewer wins.
        /// </summary>
        internal int RunRound(ulong[] deal, int[] calls)
        {
            Reset(deal);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                _table.Calls[s] = calls[s];
            }

            PlayOut();
            return _table.Won[_viewer];
        }

        /// <summary>Tricks a seat had won when the last rollout ended.</summary>
        internal int TricksWonBy(Seat seat)
        {
            return _table.Won[(int)seat];
        }

        private void Reset(ulong[] deal)
        {
            _table.CopyFrom(_start);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                _hands[s] = deal[s];
            }
        }

        private void PlayOut()
        {
            RoundState table = _table;
            ulong[] hands = _hands;
            while (table.Completed < Deck.HandSize)
            {
                int seat = table.SeatAt(table.Count);
                ulong hand = hands[seat];
                int card = NormalPolicy.ChooseCard(table, seat, hand, table.Legal(hand));
                hands[seat] = hand & ~(1UL << card);
                table.Play(seat, card);
            }
        }

        /// <summary>
        /// The viewer's round score in tenths, times 100, plus <c>SpoilPercent × call × 10</c> for every opponent
        /// that missed its call (GDD §9 Hard: "maximize its own score and spoil opponents' calls"); see
        /// <see cref="HardStrategy.SpoilPercent"/>.
        /// </summary>
        private int Value()
        {
            int value = 100 * ScoreCalculator.RoundScore(_table.Calls[_viewer], _table.Won[_viewer], _table.Rules);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (s != _viewer && _table.Won[s] < _table.Calls[s])
                {
                    value += _spoilPercent * _table.Calls[s] * 10;
                }
            }

            return value;
        }
    }
}
