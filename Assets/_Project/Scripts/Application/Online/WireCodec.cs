using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The rules engine's types on the wire (GDD §25-26): a card is a byte, a hand a ulong, scores
    /// are tenths. A <see cref="PlayerView"/> travels whole, so a client never has to reconstruct
    /// state from events, and it can only ever contain what its seat may know. Reading validates
    /// everything through the engine's own constructors; bad data is a
    /// <see cref="WireFormatException"/>.
    /// </summary>
    public static class WireCodec
    {
        private const int MaxRounds = 32;

        private enum EventKind : byte
        {
            MatchStarted = 1,
            RoundStarted = 2,
            Redealt = 3,
            HandDealt = 4,
            CallTurnStarted = 5,
            CallPlaced = 6,
            PlayTurnStarted = 7,
            LegalHint = 8,
            CardPlayed = 9,
            TrickCompleted = 10,
            RoundScored = 11,
            ReadyConfirmed = 12,
            MatchEnded = 13,
        }

        public static void Write(WireWriter w, Seat seat) => w.Byte((byte)seat);

        public static Seat ReadSeat(WireReader r)
        {
            byte value = r.Byte();
            if (value >= SeatExtensions.Count)
            {
                throw new WireFormatException("Seat " + value + " does not exist.");
            }

            return (Seat)value;
        }

        public static void Write(WireWriter w, Card card) => w.Byte((byte)card.Id);

        public static Card ReadCard(WireReader r)
        {
            byte id = r.Byte();
            if (id >= Card.Count)
            {
                throw new WireFormatException("Card " + id + " does not exist.");
            }

            return Card.FromId(id);
        }

        public static void Write(WireWriter w, CardSet cards) => w.UInt64(cards.Bits);

        public static CardSet ReadCardSet(WireReader r)
        {
            ulong bits = r.UInt64();
            try
            {
                return CardSet.FromBits(bits);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new WireFormatException("A card set uses bits that are not cards.", e);
            }
        }

        public static void Write(WireWriter w, TurnId turn) => w.Int32(turn.Value);

        public static TurnId ReadTurnId(WireReader r)
        {
            int value = r.Int32();
            if (value < 0)
            {
                throw new WireFormatException("A turn id cannot be negative.");
            }

            return new TurnId(value);
        }

        public static void Write(WireWriter w, SeatProfile profile)
        {
            w.String(profile.Name);
            w.String(profile.AvatarId);
            w.String(profile.DeckThemeId);
            w.Bool(profile.IsBot);
        }

        public static SeatProfile ReadSeatProfile(WireReader r)
        {
            string name = r.String(Protocol.MaxNameBytes);
            string avatar = r.String(Protocol.MaxIdBytes);
            string deck = r.String(Protocol.MaxIdBytes);
            bool isBot = r.Bool();
            return Guard(() => new SeatProfile(name, avatar, deck, isBot));
        }

        public static void Write(WireWriter w, Trick trick)
        {
            Write(w, trick.Leader);
            w.Byte((byte)trick.Direction);
            w.Byte((byte)trick.Count);
            for (int i = 0; i < trick.Count; i++)
            {
                Write(w, trick[i]);
            }
        }

        public static Trick ReadTrick(WireReader r)
        {
            Seat leader = ReadSeat(r);
            byte direction = r.Byte();
            int count = r.Count(Trick.Size);
            var cards = new Card[count];
            for (int i = 0; i < count; i++)
            {
                cards[i] = ReadCard(r);
            }

            return Guard(() =>
            {
                var trick = new Trick(leader, (Direction)direction);
                for (int i = 0; i < cards.Length; i++)
                {
                    trick = trick.With(cards[i]);
                }

                return trick;
            });
        }

        public static void Write(WireWriter w, CompletedTrick completed)
        {
            w.Byte((byte)completed.Index);
            Write(w, completed.Trick);
        }

        public static CompletedTrick ReadCompletedTrick(WireReader r)
        {
            int index = r.Byte();
            Trick trick = ReadTrick(r);
            return Guard(() => new CompletedTrick(index, trick));
        }

        public static void Write(WireWriter w, RoundResult round)
        {
            w.Byte((byte)round.Round);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                SeatRoundResult line = round[(Seat)s];
                w.Byte((byte)line.Call);
                w.Byte((byte)line.Won);
                w.Int32(line.DeltaTenths);
                w.Int32(line.TotalTenths);
            }
        }

        public static RoundResult ReadRoundResult(WireReader r)
        {
            int round = r.Byte();
            var lines = new SeatRoundResult[SeatExtensions.Count];
            for (int s = 0; s < lines.Length; s++)
            {
                int call = r.Byte();
                int won = r.Byte();
                int delta = r.Int32();
                int total = r.Int32();
                var seat = (Seat)s;
                lines[s] = Guard(() => new SeatRoundResult(seat, call, won, delta, total));
            }

            return Guard(() => new RoundResult(round, lines));
        }

        public static void Write(WireWriter w, ScoreSheet scores)
        {
            w.Byte((byte)scores.Rounds.Count);
            for (int i = 0; i < scores.Rounds.Count; i++)
            {
                Write(w, scores.Rounds[i]);
            }
        }

        public static ScoreSheet ReadScoreSheet(WireReader r)
        {
            int count = r.Count(MaxRounds);
            ScoreSheet sheet = ScoreSheet.Empty;
            for (int i = 0; i < count; i++)
            {
                RoundResult round = ReadRoundResult(r);
                ScoreSheet previous = sheet;
                sheet = Guard(() => previous.Append(round));
            }

            return sheet;
        }

        public static void Write(WireWriter w, Standing standing)
        {
            Write(w, standing.Seat);
            w.Byte((byte)standing.Rank);
            w.Bool(standing.IsShared);
            w.Int32(standing.TotalTenths);
            w.Byte((byte)standing.RoundsMet);
            w.Int32(standing.BestRoundTenths);
        }

        public static Standing ReadStanding(WireReader r)
        {
            Seat seat = ReadSeat(r);
            int rank = r.Byte();
            bool shared = r.Bool();
            int total = r.Int32();
            int met = r.Byte();
            int best = r.Int32();
            return Guard(() => new Standing(seat, rank, shared, total, met, best));
        }

        /// <summary>One seat's whole information set (GDD §26 PlayerViewSnapshot).</summary>
        public static void Write(WireWriter w, PlayerView view)
        {
            Write(w, view.Viewer);
            w.String(view.Rules.Id);
            w.String(view.MatchId);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                Write(w, view.Seats[s]);
            }

            w.Byte((byte)view.Phase);
            w.Byte((byte)view.Round);
            Write(w, view.Dealer);
            Write(w, view.FirstDealer);
            Write(w, view.CurrentSeat);
            Write(w, view.CurrentTurn);
            Write(w, view.Hand);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                w.Byte((byte)(view.CallOf(seat) - CallRules.NotCalled));
                w.Byte((byte)view.TricksWonBy(seat));
                w.Byte((byte)view.HandCountOf(seat));
                w.Bool(view.IsReady(seat));
            }

            Write(w, view.CurrentTrick);
            w.Byte((byte)view.CompletedTricks.Count);
            for (int i = 0; i < view.CompletedTricks.Count; i++)
            {
                Write(w, view.CompletedTricks[i]);
            }

            Write(w, view.Scores);
            w.Byte((byte)view.FinalStandings.Count);
            for (int i = 0; i < view.FinalStandings.Count; i++)
            {
                Write(w, view.FinalStandings[i]);
            }
        }

        public static PlayerView ReadPlayerView(WireReader r)
        {
            Seat viewer = ReadSeat(r);
            RuleSet rules = RuleSetOf(r.String(Protocol.MaxIdBytes));
            string matchId = r.String(Protocol.MaxIdBytes);
            var seats = new SeatProfile[SeatExtensions.Count];
            for (int s = 0; s < seats.Length; s++)
            {
                seats[s] = ReadSeatProfile(r);
            }

            byte phase = r.Byte();
            if (phase > (byte)Phase.Closed)
            {
                throw new WireFormatException("Phase " + phase + " does not exist.");
            }

            int round = r.Byte();
            Seat dealer = ReadSeat(r);
            Seat firstDealer = ReadSeat(r);
            Seat currentSeat = ReadSeat(r);
            TurnId turn = ReadTurnId(r);
            CardSet hand = ReadCardSet(r);
            var calls = new int[SeatExtensions.Count];
            var won = new int[SeatExtensions.Count];
            var counts = new int[SeatExtensions.Count];
            var ready = new bool[SeatExtensions.Count];
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                calls[s] = r.Byte() + CallRules.NotCalled;
                won[s] = r.Byte();
                counts[s] = r.Byte();
                ready[s] = r.Bool();
            }

            Trick trick = ReadTrick(r);
            int completedCount = r.Count(Deck.HandSize);
            var completed = new CompletedTrick[completedCount];
            for (int i = 0; i < completedCount; i++)
            {
                completed[i] = ReadCompletedTrick(r);
            }

            ScoreSheet scores = ReadScoreSheet(r);
            int standingCount = r.Count(SeatExtensions.Count);
            var standings = new Standing[standingCount];
            for (int i = 0; i < standingCount; i++)
            {
                standings[i] = ReadStanding(r);
            }

            return Guard(() => new PlayerView(
                viewer, rules, matchId, seats, (Phase)phase, round, dealer, firstDealer, currentSeat, turn, hand,
                calls, won, counts, ready, trick, completed, scores, standings));
        }

        public static void Write(WireWriter w, MatchEvent e)
        {
            switch (e)
            {
                case MatchStarted started:
                    w.Byte((byte)EventKind.MatchStarted);
                    w.String(started.MatchId);
                    w.String(started.RuleSetId);
                    Write(w, started.FirstDealer);
                    for (int s = 0; s < SeatExtensions.Count; s++)
                    {
                        Write(w, started.Seats[s]);
                    }

                    break;
                case RoundStarted round:
                    w.Byte((byte)EventKind.RoundStarted);
                    w.Byte((byte)round.Round);
                    Write(w, round.Dealer);
                    break;
                case Redealt redealt:
                    w.Byte((byte)EventKind.Redealt);
                    w.Byte((byte)redealt.Round);
                    w.Byte((byte)redealt.Reason);
                    break;
                case HandDealt dealt:
                    w.Byte((byte)EventKind.HandDealt);
                    Write(w, dealt.Seat);
                    Write(w, dealt.Hand);
                    break;
                case CallTurnStarted callTurn:
                    w.Byte((byte)EventKind.CallTurnStarted);
                    Write(w, callTurn.Seat);
                    Write(w, callTurn.TurnId);
                    break;
                case CallPlaced call:
                    w.Byte((byte)EventKind.CallPlaced);
                    Write(w, call.Seat);
                    w.Byte((byte)call.Call);
                    w.Bool(call.IsAuto);
                    break;
                case PlayTurnStarted playTurn:
                    w.Byte((byte)EventKind.PlayTurnStarted);
                    Write(w, playTurn.Seat);
                    Write(w, playTurn.TurnId);
                    w.Bool(playTurn.LedSuit.HasValue);
                    w.Byte(playTurn.LedSuit.HasValue ? (byte)playTurn.LedSuit.Value : (byte)0);
                    break;
                case LegalHint hint:
                    w.Byte((byte)EventKind.LegalHint);
                    Write(w, hint.Seat);
                    Write(w, hint.TurnId);
                    Write(w, hint.Legal);
                    break;
                case CardPlayed played:
                    w.Byte((byte)EventKind.CardPlayed);
                    Write(w, played.Seat);
                    Write(w, played.Card);
                    w.Bool(played.IsAuto);
                    w.Bool(played.IsForced);
                    break;
                case TrickCompleted trick:
                    w.Byte((byte)EventKind.TrickCompleted);
                    Write(w, trick.Completed);
                    break;
                case RoundScored scored:
                    w.Byte((byte)EventKind.RoundScored);
                    Write(w, scored.Result);
                    break;
                case ReadyConfirmed readyConfirmed:
                    w.Byte((byte)EventKind.ReadyConfirmed);
                    Write(w, readyConfirmed.Seat);
                    break;
                case MatchEnded ended:
                    w.Byte((byte)EventKind.MatchEnded);
                    w.Byte((byte)ended.Standings.Count);
                    for (int i = 0; i < ended.Standings.Count; i++)
                    {
                        Write(w, ended.Standings[i]);
                    }

                    break;
                default:
                    throw new ArgumentException("No wire form for " + e.GetType().Name + ".", nameof(e));
            }
        }

        public static MatchEvent ReadEvent(WireReader r)
        {
            byte kind = r.Byte();
            switch ((EventKind)kind)
            {
                case EventKind.MatchStarted:
                {
                    string matchId = r.String(Protocol.MaxIdBytes);
                    string ruleSetId = r.String(Protocol.MaxIdBytes);
                    Seat firstDealer = ReadSeat(r);
                    var seats = new SeatProfile[SeatExtensions.Count];
                    for (int s = 0; s < seats.Length; s++)
                    {
                        seats[s] = ReadSeatProfile(r);
                    }

                    return Guard(() => new MatchStarted(matchId, ruleSetId, firstDealer, seats));
                }

                case EventKind.RoundStarted:
                {
                    int round = r.Byte();
                    Seat dealer = ReadSeat(r);
                    return Guard(() => new RoundStarted(round, dealer));
                }

                case EventKind.Redealt:
                {
                    int round = r.Byte();
                    byte reason = r.Byte();
                    return Guard(() => new Redealt(round, (RedealReason)reason));
                }

                case EventKind.HandDealt:
                {
                    Seat seat = ReadSeat(r);
                    CardSet hand = ReadCardSet(r);
                    return Guard(() => new HandDealt(seat, hand));
                }

                case EventKind.CallTurnStarted:
                {
                    Seat seat = ReadSeat(r);
                    TurnId turn = ReadTurnId(r);
                    return Guard(() => new CallTurnStarted(seat, turn));
                }

                case EventKind.CallPlaced:
                {
                    Seat seat = ReadSeat(r);
                    int call = r.Byte();
                    bool isAuto = r.Bool();
                    return Guard(() => new CallPlaced(seat, call, isAuto));
                }

                case EventKind.PlayTurnStarted:
                {
                    Seat seat = ReadSeat(r);
                    TurnId turn = ReadTurnId(r);
                    bool hasLed = r.Bool();
                    byte led = r.Byte();
                    if (led > (byte)Suit.Spades)
                    {
                        throw new WireFormatException("Suit " + led + " does not exist.");
                    }

                    return Guard(() => new PlayTurnStarted(seat, turn, hasLed ? (Suit?)led : null));
                }

                case EventKind.LegalHint:
                {
                    Seat seat = ReadSeat(r);
                    TurnId turn = ReadTurnId(r);
                    CardSet legal = ReadCardSet(r);
                    return Guard(() => new LegalHint(seat, turn, legal));
                }

                case EventKind.CardPlayed:
                {
                    Seat seat = ReadSeat(r);
                    Card card = ReadCard(r);
                    bool isAuto = r.Bool();
                    bool isForced = r.Bool();
                    return Guard(() => new CardPlayed(seat, card, isAuto, isForced));
                }

                case EventKind.TrickCompleted:
                {
                    CompletedTrick completed = ReadCompletedTrick(r);
                    return Guard(() => new TrickCompleted(completed));
                }

                case EventKind.RoundScored:
                {
                    RoundResult result = ReadRoundResult(r);
                    return Guard(() => new RoundScored(result));
                }

                case EventKind.ReadyConfirmed:
                {
                    Seat seat = ReadSeat(r);
                    return Guard(() => new ReadyConfirmed(seat));
                }

                case EventKind.MatchEnded:
                {
                    int count = r.Count(SeatExtensions.Count);
                    var standings = new Standing[count];
                    for (int i = 0; i < count; i++)
                    {
                        standings[i] = ReadStanding(r);
                    }

                    return Guard(() => new MatchEnded(standings));
                }

                default:
                    throw new WireFormatException("Event kind " + kind + " is unknown.");
            }
        }

        /// <summary>The rule set a wire id names. Only rule sets this build ships can be played.</summary>
        public static RuleSet RuleSetOf(string id)
        {
            if (id == RuleSet.NepaliStandard.Id)
            {
                return RuleSet.NepaliStandard;
            }

            throw new WireFormatException("Rule set '" + id + "' is not in this build.");
        }

        /// <summary>Runs an engine constructor; what it refuses is bad wire data.</summary>
        private static T Guard<T>(Func<T> build)
        {
            try
            {
                return build();
            }
            catch (ArgumentException e)
            {
                throw new WireFormatException("The message holds a value the rules do not allow.", e);
            }
            catch (InvalidOperationException e)
            {
                throw new WireFormatException("The message holds a value the rules do not allow.", e);
            }
        }
    }
}
