using Callbreak.Core.Match;
using Callbreak.Core.Randomness;

namespace Callbreak.Bots
{
    /// <summary>
    /// Deals the viewer's unseen cards to the other three seats at random, consistent with everything the
    /// tracker has observed (GDD §9 Hard: "samples deals of the unseen cards that are consistent with everything
    /// observed (played cards, voids)"): each seat gets exactly its hand count and never a card it
    /// <see cref="CardTracker.CannotHold"/>. Hands are card masks (see <see cref="Bits"/>) indexed by seat.
    /// <para>
    /// Cards are grouped by the set of seats that may hold them (7 kinds for 3 seats). Cards only one seat may
    /// hold go straight to it; the rest are dealt one at a time, the more constrained kinds first, each to an
    /// allowed seat with probability proportional to its free places, skipping any seat that would leave the
    /// remainder impossible to deal (Hall's condition over the 7 seat subsets). Without constraints this is
    /// exactly a uniform random deal, and it can never dead-end. All buffers are allocated once per decision;
    /// <see cref="Deal"/> allocates nothing.
    /// </para>
    /// </summary>
    internal sealed class DealSampler
    {
        private const int Others = SeatExtensions.Count - 1;
        private const int Kinds = 1 << Others;
        private const int AnySeat = Kinds - 1;

        private readonly int _viewer;
        private readonly ulong _viewerHand;
        private readonly int[] _seats = new int[Others];
        private readonly int[] _handCounts = new int[Others];
        private readonly ulong[] _byKind = new ulong[Kinds];
        private readonly int[] _free = new int[Others];
        private readonly int[] _left = new int[Kinds];

        private DealSampler(CardTracker tracker)
        {
            _viewer = (int)tracker.Viewer;
            _viewerHand = tracker.Hand.Bits;
            int next = 0;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (s != _viewer)
                {
                    _seats[next] = s;
                    _handCounts[next] = tracker.State.HandCounts[s];
                    next++;
                }
            }
        }

        /// <summary>True when the observed exclusions contradicted each other and were ignored (never expected).</summary>
        internal bool IgnoredConstraints { get; private set; }

        /// <summary>
        /// Prepares sampling for the tracker's viewer. Returns false when the hand counts do not add up to the
        /// unseen cards, which only a malformed view can cause.
        /// </summary>
        internal static bool TryCreate(CardTracker tracker, out DealSampler sampler)
        {
            sampler = new DealSampler(tracker);
            ulong unseen = tracker.Unseen.Bits;
            int total = 0;
            for (int j = 0; j < Others; j++)
            {
                if (sampler._handCounts[j] < 0)
                {
                    return false;
                }

                total += sampler._handCounts[j];
            }

            if (total != Bits.Count(unseen))
            {
                return false;
            }

            ulong[] excluded = tracker.State.Excluded;
            for (ulong rest = unseen; rest != 0UL; rest &= rest - 1UL)
            {
                ulong card = rest & unchecked(~rest + 1UL);
                int kind = 0;
                for (int j = 0; j < Others; j++)
                {
                    if ((excluded[sampler._seats[j]] & card) == 0UL)
                    {
                        kind |= 1 << j;
                    }
                }

                sampler._byKind[kind] |= card;
            }

            for (int kind = 0; kind < Kinds; kind++)
            {
                sampler._left[kind] = Bits.Count(sampler._byKind[kind]);
            }

            for (int j = 0; j < Others; j++)
            {
                sampler._free[j] = sampler._handCounts[j];
            }

            if (sampler._left[0] != 0 || !sampler.HallHolds())
            {
                for (int kind = 0; kind < AnySeat; kind++)
                {
                    sampler._byKind[kind] = 0UL;
                }

                sampler._byKind[AnySeat] = unseen;
                sampler.IgnoredConstraints = true;
            }

            return true;
        }

        /// <summary>
        /// Fills <paramref name="hands"/> (indexed by seat) with one consistent deal: the viewer's own hand and a
        /// sampled hand for each other seat.
        /// </summary>
        internal void Deal(IRandom rng, ulong[] hands)
        {
            hands[_viewer] = _viewerHand;
            for (int kind = 0; kind < Kinds; kind++)
            {
                _left[kind] = 0;
            }

            for (int j = 0; j < Others; j++)
            {
                ulong forced = _byKind[1 << j];
                hands[_seats[j]] = forced;
                _free[j] = _handCounts[j] - Bits.Count(forced);
            }

            // Two-seat kinds first (3, 5, 6), then cards any seat may hold (7).
            _left[3] = Bits.Count(_byKind[3]);
            _left[5] = Bits.Count(_byKind[5]);
            _left[6] = Bits.Count(_byKind[6]);
            _left[AnySeat] = Bits.Count(_byKind[AnySeat]);
            DealKind(3, rng, hands);
            DealKind(5, rng, hands);
            DealKind(6, rng, hands);
            DealKind(AnySeat, rng, hands);
        }

        private void DealKind(int kind, IRandom rng, ulong[] hands)
        {
            // Once only unconstrained cards are left, any seat with a free place keeps the rest dealable.
            bool check = kind != AnySeat;
            for (ulong cards = _byKind[kind]; cards != 0UL; cards &= cards - 1UL)
            {
                _left[kind]--;
                int w0 = Weight(0, kind, check);
                int w1 = Weight(1, kind, check);
                int w2 = Weight(2, kind, check);
                int roll = rng.NextInt(w0 + w1 + w2);
                int seat = roll < w0 ? 0 : roll < w0 + w1 ? 1 : 2;
                hands[_seats[seat]] |= cards & unchecked(~cards + 1UL);
                _free[seat]--;
            }
        }

        private int Weight(int seat, int kind, bool check)
        {
            if ((kind & (1 << seat)) == 0 || _free[seat] == 0)
            {
                return 0;
            }

            if (check)
            {
                _free[seat]--;
                bool feasible = HallHolds();
                _free[seat]++;
                if (!feasible)
                {
                    return 0;
                }
            }

            return _free[seat];
        }

        /// <summary>
        /// Hall's condition: for every group of seats, the cards only that group may hold fit in its free places.
        /// With the totals equal, this is exactly "the remaining cards can still be dealt".
        /// </summary>
        private bool HallHolds()
        {
            for (int group = 1; group < Kinds; group++)
            {
                int cards = 0;
                for (int sub = group; sub > 0; sub = (sub - 1) & group)
                {
                    cards += _left[sub];
                }

                int places = 0;
                for (int j = 0; j < Others; j++)
                {
                    if ((group & (1 << j)) != 0)
                    {
                        places += _free[j];
                    }
                }

                if (cards > places)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
