using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// The Hard bot (GDD §9 "Hard": "a sharp, patient opponent"; "maximize its own score and spoil opponents'
    /// calls"). Both of its decisions sample the hidden hands and play them out with the Normal policy:
    /// <list type="bullet">
    /// <item>Card play is perfect-information Monte Carlo (GDD §9 "Hard: how it thinks"). It deals the unseen
    /// cards to the other seats <see cref="Samples"/> times, consistent with everything observed (hand counts,
    /// played cards, the voids and other exclusions of <see cref="CardTracker"/>, and the calls as hints); for
    /// each legal card it plays every sample to the end of the round with the Normal policy in all four seats,
    /// and picks the card with the best total value: its round score plus <see cref="SpoilPercent"/> of each
    /// call an opponent misses. Every sample is used for every candidate, and interchangeable cards (no unseen
    /// card ranks between them) are tried once.</item>
    /// <item>Calling starts from the same samples instead of the estimator's fixed weights (GDD §9 "estimator
    /// plus self-play calibration"): it plays <see cref="CallSamples"/> sampled deals from the first lead while
    /// trying for every trick, and calls the number with the best average round score. Because it plays its
    /// cards better than the Normal policy that stands in for it, one rollout in
    /// <see cref="CallEdgePercent"/> out of 100 counts as one trick more; that share is the self-play
    /// calibration, the value at which predicted and actual made calls agree.</item>
    /// </list>
    /// <para>
    /// The cost is fixed by the sample counts, never by a clock, and all randomness comes from the
    /// <see cref="IRandom"/> passed in, so the same view and random sequence always give the same decision.
    /// Stateless and thread-safe: buffers are allocated per decision and reused across its rollouts, which
    /// allocate nothing.
    /// </para>
    /// </summary>
    public sealed class HardStrategy : IBotStrategy
    {
        /// <summary>Samples per card decision by default (GDD §9 "64–256 deals").</summary>
        public const int DefaultSamples = 64;

        /// <summary>The largest accepted sample count.</summary>
        public const int MaxSamples = 1024;

        /// <summary>
        /// Deals played per call for each card-play sample: a call is one decision per round and one rollout
        /// rates every possible call, so it can afford four times as many.
        /// </summary>
        public const int CallSamplesPerSample = 4;

        /// <summary>
        /// What an opponent's missed call is worth, in percent of the call's value (call × 10 tenths), next to
        /// the bot's own round score. At 50, spoiling a call of 4 is worth 2.0 points: half a made call of 4, a
        /// quarter of the 8.0 the miss costs that opponent.
        /// </summary>
        public const int SpoilPercent = 50;

        /// <summary>
        /// Share of call rollouts, in percent, in which the bot is credited one more trick than its Normal
        /// stand-in won. Calibrated by self-play against three Normal bots: with this share the predicted rate
        /// of made calls matches the measured one (about 93%).
        /// </summary>
        public const int CallEdgePercent = 33;

        /// <summary>Deals tried per sample to find one that fits the opponents' calls.</summary>
        internal const int DefaultHintTries = 8;

        /// <summary>How far a sampled hand's estimator call may be from the seat's real call and still fit.</summary>
        internal const int HintTolerance = 1;

        private readonly int _spoilPercent;
        private readonly int _hintTries;
        private readonly int _callEdgePercent;

        /// <summary>Creates a Hard bot with <see cref="DefaultSamples"/> samples per card.</summary>
        public HardStrategy()
            : this(DefaultSamples)
        {
        }

        /// <summary>Creates a Hard bot with a given sample count (the decision budget, GDD §9).</summary>
        /// <param name="samples">Deals sampled per card decision, 1–<see cref="MaxSamples"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="samples"/> is out of range.</exception>
        public HardStrategy(int samples)
            : this(samples, SpoilPercent, CallSamplesPerSample * samples, DefaultHintTries, CallEdgePercent)
        {
        }

        /// <summary>Every tuning value, for tests and the arena; <paramref name="callSamples"/> 0 calls like Normal.</summary>
        internal HardStrategy(int samples, int spoilPercent, int callSamples, int hintTries, int callEdgePercent)
        {
            if (samples < 1 || samples > MaxSamples)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), samples, "Samples must be 1-" + MaxSamples + ".");
            }

            if (spoilPercent < 0 || spoilPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(spoilPercent), spoilPercent, "Spoil weight must be 0-100.");
            }

            if (callSamples < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(callSamples), callSamples, "Call samples must not be negative.");
            }

            if (hintTries < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hintTries), hintTries, "At least one deal per sample.");
            }

            if (callEdgePercent < 0 || callEdgePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(callEdgePercent), callEdgePercent, "Call edge must be 0-100.");
            }

            Samples = samples;
            CallSamples = callSamples;
            _spoilPercent = spoilPercent;
            _hintTries = hintTries;
            _callEdgePercent = callEdgePercent;
        }

        /// <inheritdoc />
        public BotLevel Level => BotLevel.Hard;

        /// <summary>Deals sampled per card decision.</summary>
        public int Samples { get; }

        /// <summary>Deals played per call.</summary>
        public int CallSamples { get; }

        /// <summary>
        /// The call with the best average round score over <see cref="CallSamples"/> sampled deals, each played
        /// from the first lead with the Normal policy while the viewer tries for every trick. Seats that have
        /// not called yet call with the estimator on their sampled hand. Ties go to the lower call. Outside
        /// the start of a round it returns the estimator's call.
        /// </summary>
        /// <param name="view">The viewer's view on its call turn.</param>
        /// <param name="rng">Draws for sampling the deals.</param>
        /// <returns>A legal call.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public int ChooseCall(PlayerView view, IRandom rng)
        {
            RequireArguments(view, rng);
            var tricks = new int[Deck.HandSize + 1];
            if (CallSamples == 0 || !SampleTricks(view, rng, tricks))
            {
                return CallEstimator.Call(view.Hand, view.Rules);
            }

            return BestCall(tricks, view.Rules, _callEdgePercent);
        }

        /// <summary>
        /// The legal card with the best total value over <see cref="Samples"/> sampled deals; a tie goes to the
        /// cheaper card. A forced card is returned at once without drawing; a malformed view (hand counts that do
        /// not add up, or a call outside the rule set's range) falls back to the Normal card.
        /// </summary>
        /// <param name="view">The viewer's view on its play turn.</param>
        /// <param name="rng">Draws for sampling the deals.</param>
        /// <returns>A card from <c>view.LegalCards()</c>.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">It is not the viewer's play turn.</exception>
        public Card ChooseCard(PlayerView view, IRandom rng)
        {
            RequireArguments(view, rng);
            CardSet legal = BotStrategies.RequireLegalCards(view);
            if (legal.Count == 1)
            {
                return legal.Lowest();
            }

            var tracker = new CardTracker(view);
            var candidates = new Card[legal.Count];
            int count = Candidates(tracker, legal, candidates);
            if (count == 1)
            {
                return candidates[0];
            }

            if (!EveryCallIsValid(tracker) || !DealSampler.TryCreate(tracker, out DealSampler sampler))
            {
                return NormalPolicy.ChooseCard(tracker, view.Viewer, view.Hand);
            }

            var rollout = new Rollout(tracker, _spoilPercent);
            var deal = new CardSet[SeatExtensions.Count];
            var attempt = new CardSet[SeatExtensions.Count];
            var totals = new long[count];
            for (int i = 0; i < Samples; i++)
            {
                SampleDeal(sampler, tracker, rng, deal, attempt);
                for (int c = 0; c < count; c++)
                {
                    totals[c] += rollout.Run(deal, candidates[c]);
                }
            }

            int best = 0;
            for (int c = 1; c < count; c++)
            {
                if (totals[c] > totals[best])
                {
                    best = c;
                }
            }

            return candidates[best];
        }

        /// <summary>
        /// Plays <see cref="CallSamples"/> sampled deals from the start of the round and counts, per number of
        /// tricks (the index), how often the viewer won that many while trying for every trick. The Normal
        /// policy plays the same way until its call is made, so one rollout shows for every possible call
        /// whether it would have been met. False when a card has already been played or the view is malformed.
        /// </summary>
        internal bool SampleTricks(PlayerView view, IRandom rng, int[] tricks)
        {
            RuleSet rules = view.Rules;
            var tracker = new CardTracker(view);
            if (!tracker.Played.IsEmpty || !DealSampler.TryCreate(tracker, out DealSampler sampler))
            {
                return false;
            }

            var rollout = new Rollout(tracker, 0);
            var deal = new CardSet[SeatExtensions.Count];
            var attempt = new CardSet[SeatExtensions.Count];
            var calls = new int[SeatExtensions.Count];
            for (int i = 0; i < CallSamples; i++)
            {
                SampleDeal(sampler, tracker, rng, deal, attempt);
                for (int s = 0; s < SeatExtensions.Count; s++)
                {
                    var seat = (Seat)s;
                    if (seat == view.Viewer)
                    {
                        // The highest call there is, so the viewer never stops trying.
                        calls[s] = RuleSet.CallLimit;
                    }
                    else
                    {
                        calls[s] = tracker.CallOf(seat) != CallRules.NotCalled ? tracker.CallOf(seat) : CallEstimator.Call(deal[s], rules);
                    }
                }

                tricks[rollout.RunRound(deal, calls)]++;
            }

            return true;
        }

        /// <summary>
        /// The call with the best total round score over a histogram of tricks won (index = tricks), crediting
        /// one more trick in <paramref name="edgePercent"/> of the cases. Ties go to the lower call.
        /// </summary>
        internal static int BestCall(int[] tricks, RuleSet rules, int edgePercent)
        {
            int best = rules.MinCall;
            long bestTotal = long.MinValue;
            for (int call = rules.MinCall; call <= rules.MaxCall; call++)
            {
                long total = 0;
                for (int won = 0; won <= Deck.HandSize; won++)
                {
                    int oneMore = won < Deck.HandSize ? won + 1 : won;
                    long score = ((100 - edgePercent) * ScoreCalculator.RoundScore(call, won, rules))
                        + (edgePercent * ScoreCalculator.RoundScore(call, oneMore, rules));
                    total += tricks[won] * score;
                }

                if (total > bestTotal)
                {
                    best = call;
                    bestTotal = total;
                }
            }

            return best;
        }

        /// <summary>
        /// One card per group of interchangeable legal cards (same suit, and every card ranked between them is in
        /// the viewer's hand or in a completed trick), cheapest first: side suits before spades, then by rank,
        /// then ♣ ♦ ♥. Returns how many were written to <paramref name="into"/>.
        /// </summary>
        internal static int Candidates(CardTracker tracker, CardSet legal, Card[] into)
        {
            CardSet separators = tracker.Unseen | tracker.CurrentTrick.Cards;
            int count = 0;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                bool first = true;
                Card previous = default;
                foreach (Card card in legal.InSuit((Suit)s))
                {
                    if (first || (CardSet.Above(previous) - CardSet.Above(card) - CardSet.Of(card)).Overlaps(separators))
                    {
                        into[count++] = card;
                    }

                    first = false;
                    previous = card;
                }
            }

            for (int i = 1; i < count; i++)
            {
                Card card = into[i];
                int j = i - 1;
                while (j >= 0 && Cost(into[j]) > Cost(card))
                {
                    into[j + 1] = into[j];
                    j--;
                }

                into[j + 1] = card;
            }

            return count;
        }

        /// <summary>
        /// How badly sampled hands fit the opponents' calls (GDD §9 Hard: "calls as hints"): for every opponent
        /// that has called, the distance beyond ±1 between its call and the estimator's call for its original
        /// hand (its sampled cards plus those it has played). 0 means the deal fits.
        /// </summary>
        internal static int CallMismatch(CardTracker tracker, CardSet[] hands)
        {
            int total = 0;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                int call = tracker.CallOf(seat);
                if (seat == tracker.Viewer || call == CallRules.NotCalled)
                {
                    continue;
                }

                int off = Math.Abs(CallEstimator.Call(hands[s] | tracker.PlayedBy(seat), tracker.Rules) - call) - HintTolerance;
                if (off > 0)
                {
                    total += off;
                }
            }

            return total;
        }

        /// <summary>
        /// Fills <paramref name="deal"/> with the first of up to <see cref="DefaultHintTries"/> sampled deals that
        /// fits the opponents' calls, or else the one that fits best.
        /// </summary>
        private void SampleDeal(DealSampler sampler, CardTracker tracker, IRandom rng, CardSet[] deal, CardSet[] attempt)
        {
            if (_hintTries == 1)
            {
                sampler.Deal(rng, deal);
                return;
            }

            int bestMiss = int.MaxValue;
            for (int t = 0; t < _hintTries; t++)
            {
                sampler.Deal(rng, attempt);
                int miss = CallMismatch(tracker, attempt);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    for (int s = 0; s < SeatExtensions.Count; s++)
                    {
                        deal[s] = attempt[s];
                    }
                }

                if (miss == 0)
                {
                    return;
                }
            }
        }

        private static int Cost(Card card)
        {
            return ((card.IsTrump ? 1 : 0) << 8) | ((int)card.Rank << 2) | (int)card.Suit;
        }

        private static bool EveryCallIsValid(CardTracker tracker)
        {
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (!CallRules.IsValid(tracker.CallOf((Seat)s), tracker.Rules))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireArguments(PlayerView view, IRandom rng)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }
        }
    }
}
