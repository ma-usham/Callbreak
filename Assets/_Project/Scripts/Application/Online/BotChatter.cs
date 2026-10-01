using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// What the bots that fill an online table say (ADR 026): now and then one of the six quick
    /// phrases, at a moment it fits, judged only from what every player can see.
    /// <list type="bullet">
    /// <item>"Namaste!" as the first hand is dealt, and back to a player who says it.</item>
    /// <item>"Nice call!" to someone else's bold call (5 or more, not one the timer made).</item>
    /// <item>"Oops!" from a bot whose ace or king was cut by a spade, or that missed its call.</item>
    /// <item>"Well played!" to a player who met a bold call.</item>
    /// <item>"Good game!" as the last trick of the match is taken (the results cover the table
    /// after that), and back to a player who says it.</item>
    /// <item>Now and then "Thinking…" as a bot's turn begins.</item>
    /// </list>
    /// Each moment is remarked on only by chance, by one bot, a moment later. After a bot has
    /// spoken the table's bots keep quiet for <see cref="QuietSeconds"/>, and a remark whose moment
    /// has passed is not made. The table still holds every phrase to the limits people have.
    /// </summary>
    public sealed class BotChatter
    {
        public const int Namaste = 0;
        public const int WellPlayed = 1;
        public const int NiceCall = 2;
        public const int Oops = 3;
        public const int Thinking = 4;
        public const int GoodGame = 5;

        /// <summary>A call this high is bold enough to remark on.</summary>
        public const int BoldCall = 5;

        /// <summary>After a bot has spoken, the table's bots say nothing for this long.</summary>
        public const double QuietSeconds = 8.0;

        /// <summary>A remark not made within this long of when it was due is not made at all.</summary>
        public const double StaleSeconds = 6.0;

        private readonly Func<Seat, bool> _isBot;
        private readonly IRandom _random;
        private readonly int _rounds;
        private readonly List<Remark> _waiting = new List<Remark>();
        private readonly List<Seat> _candidates = new List<Seat>(SeatExtensions.Count);
        private double _lastSpokeAt = double.NegativeInfinity;
        private int _round;

        /// <param name="isBot">The seats a bot fills: they speak, and they never answer each other.</param>
        /// <param name="random">Decides whether a moment is remarked on, by whom and how soon.</param>
        /// <param name="rounds">The rounds of the match, to know its last trick.</param>
        public BotChatter(Func<Seat, bool> isBot, IRandom random, int rounds)
        {
            _isBot = isBot ?? throw new ArgumentNullException(nameof(isBot));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _rounds = rounds;
        }

        /// <summary>Takes in what the table has just done.</summary>
        public void Observe(MatchEvent e, double now)
        {
            switch (e)
            {
                case RoundStarted started:
                    _round = started.Round;
                    if (started.Round == 1)
                    {
                        Maybe(60, AnyBot(null), Namaste, now, 1.5, 3.5);
                    }

                    break;

                case CallPlaced placed when placed.Call >= BoldCall && !placed.IsAuto:
                    Maybe(35, AnyBot(placed.Seat), NiceCall, now, 0.8, 2.0);
                    break;

                case PlayTurnStarted turn when _isBot(turn.Seat):
                    Maybe(3, turn.Seat, Thinking, now, 0.2, 0.6);
                    break;

                case TrickCompleted done:
                    OnTrick(done.Completed, now);
                    break;

                case RoundScored scored:
                    OnRound(scored.Result, now);
                    break;
            }
        }

        /// <summary>A player said <paramref name="emote"/>: a bot may answer in kind.</summary>
        public void Heard(Seat seat, int emote, double now)
        {
            if (_isBot(seat) || (emote != Namaste && emote != GoodGame))
            {
                return;
            }

            Maybe(60, AnyBot(seat), emote, now, 1.0, 2.5);
        }

        /// <summary>The next remark that is due at <paramref name="now"/>, if the bots are not keeping quiet.</summary>
        public bool TryTake(double now, out Seat seat, out int emote)
        {
            seat = default;
            emote = 0;
            _waiting.RemoveAll(r => now > r.DueAt + StaleSeconds);
            if (now - _lastSpokeAt < QuietSeconds)
            {
                return false;
            }

            int next = -1;
            for (int i = 0; i < _waiting.Count; i++)
            {
                if (_waiting[i].DueAt <= now && (next < 0 || _waiting[i].DueAt < _waiting[next].DueAt))
                {
                    next = i;
                }
            }

            if (next < 0)
            {
                return false;
            }

            Remark remark = _waiting[next];
            _waiting.RemoveAt(next);
            _lastSpokeAt = now;
            seat = remark.Seat;
            emote = remark.Emote;
            return true;
        }

        private void OnTrick(CompletedTrick completed, double now)
        {
            // The match's last trick: the results open over the table next.
            if (_round == _rounds && completed.Index == Deck.HandSize - 1)
            {
                Maybe(85, AnyBot(null), GoodGame, now, 0.5, 1.5);
                return;
            }

            Trick trick = completed.Trick;
            if (!trick.IsTrumped)
            {
                return;
            }

            for (int i = 0; i < trick.Count; i++)
            {
                Seat seat = trick.SeatAt(i);
                Card card = trick[i];
                if (seat != completed.Winner && _isBot(seat) && card.Suit == trick.LedSuit && card.Rank >= Rank.King)
                {
                    Maybe(40, seat, Oops, now, 0.6, 1.5);
                    return;
                }
            }
        }

        private void OnRound(RoundResult result, double now)
        {
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                SeatRoundResult line = result[seat];
                if (!_isBot(seat) && line.Met && line.Call >= BoldCall)
                {
                    Maybe(45, AnyBot(seat), WellPlayed, now, 1.0, 2.5);
                    return;
                }
            }

            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                if (_isBot(seat) && !result[seat].Met)
                {
                    Maybe(20, seat, Oops, now, 1.0, 2.5);
                    return;
                }
            }
        }

        /// <summary>With <paramref name="percent"/>% chance, <paramref name="seat"/> will say <paramref name="emote"/> a moment after <paramref name="now"/>.</summary>
        private void Maybe(int percent, Seat? seat, int emote, double now, double soonest, double latest)
        {
            if (seat == null || _random.NextInt(100) >= percent)
            {
                return;
            }

            double wait = soonest + ((latest - soonest) * (_random.NextInt(1000) / 1000.0));
            _waiting.Add(new Remark(seat.Value, emote, now + wait));
        }

        /// <summary>One of the bots, other than <paramref name="except"/>; null when there is none.</summary>
        private Seat? AnyBot(Seat? except)
        {
            _candidates.Clear();
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                if (seat != except && _isBot(seat))
                {
                    _candidates.Add(seat);
                }
            }

            return _candidates.Count == 0 ? (Seat?)null : _candidates[_random.NextInt(_candidates.Count)];
        }

        private readonly struct Remark
        {
            public Remark(Seat seat, int emote, double dueAt)
            {
                Seat = seat;
                Emote = emote;
                DueAt = dueAt;
            }

            public Seat Seat { get; }

            public int Emote { get; }

            public double DueAt { get; }
        }
    }
}
