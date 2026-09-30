using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Bots
{
    /// <summary>
    /// Plays one sampled deal to the end of the round (GDD §9 Hard: "rolling out every sample with the Normal
    /// policy"): <see cref="NormalPolicy"/> chooses for every seat, each from its own hand plus the public
    /// knowledge, exactly as a Normal bot would play it. Allocation-free after construction; one instance serves
    /// every sample and candidate of a decision.
    /// </summary>
    internal sealed class Rollout
    {
        private readonly CardTracker _start;
        private readonly CardTracker _table;
        private readonly CardSet[] _hands = new CardSet[SeatExtensions.Count];
        private readonly Seat _viewer;
        private readonly int _spoilPercent;

        internal Rollout(CardTracker start, int spoilPercent)
        {
            _start = start;
            _table = start.Clone();
            _viewer = start.Viewer;
            _spoilPercent = spoilPercent;
        }

        /// <summary>
        /// Plays <paramref name="first"/> for the viewer from <paramref name="deal"/> and the rest of the round
        /// with the Normal policy, and returns the result's value for the viewer (see <see cref="Value"/>).
        /// </summary>
        internal int Run(CardSet[] deal, Card first)
        {
            Reset(deal);
            Play(_viewer, first);
            PlayOut();
            return Value();
        }

        /// <summary>
        /// Plays the whole round from the start with the Normal policy and the given calls (indexed by seat),
        /// and returns the tricks the viewer wins.
        /// </summary>
        internal int RunRound(CardSet[] deal, int[] calls)
        {
            Reset(deal);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                _table.SetCall((Seat)s, calls[s]);
            }

            PlayOut();
            return _table.TricksWonBy(_viewer);
        }

        /// <summary>Tricks a seat had won when the last rollout ended.</summary>
        internal int TricksWonBy(Seat seat)
        {
            return _table.TricksWonBy(seat);
        }

        private void Reset(CardSet[] deal)
        {
            _table.CopyFrom(_start);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                _hands[s] = deal[s];
            }
        }

        private void PlayOut()
        {
            while (_table.TrickIndex < Deck.HandSize)
            {
                Seat seat = _table.CurrentTrick.NextSeat;
                Play(seat, NormalPolicy.ChooseCard(_table, seat, _hands[(int)seat]));
            }
        }

        /// <summary>
        /// The viewer's round score in tenths, times 100, plus <c>SpoilPercent × call × 10</c> for every opponent
        /// that missed its call (GDD §9 Hard: "maximize its own score and spoil opponents' calls"); see
        /// <see cref="HardStrategy.SpoilPercent"/>.
        /// </summary>
        private int Value()
        {
            int value = 100 * ScoreCalculator.RoundScore(_table.CallOf(_viewer), _table.TricksWonBy(_viewer), _table.Rules);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                if (seat != _viewer && _table.TricksWonBy(seat) < _table.CallOf(seat))
                {
                    value += _spoilPercent * _table.CallOf(seat) * 10;
                }
            }

            return value;
        }

        private void Play(Seat seat, Card card)
        {
            _hands[(int)seat] = _hands[(int)seat].Without(card);
            _table.Record(seat, card);
        }
    }
}
