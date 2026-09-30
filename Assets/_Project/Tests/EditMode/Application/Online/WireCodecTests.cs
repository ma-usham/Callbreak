using System.Collections.Generic;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    public sealed class WireCodecTests
    {
        [Test]
        public void WholeMatch_EveryEventAndEveryView_SurvivesTheWire()
        {
            MatchEngine engine = NewEngine(seed: 7);
            var events = new List<MatchEvent>();
            var kinds = new HashSet<string>();
            int views = 0;
            engine.Start();
            for (int guard = 0; guard < 5000 && engine.Phase != Phase.MatchOver; guard++)
            {
                events.Clear();
                engine.DrainEvents(events);
                foreach (MatchEvent e in events)
                {
                    var w = new WireWriter();
                    WireCodec.Write(w, e);
                    MatchEvent back = WireCodec.ReadEvent(new WireReader(w.ToArray()));
                    Assert.AreEqual(e.GetType(), back.GetType());
                    Assert.AreEqual(e.ToString(), back.ToString());
                    Assert.AreEqual(e.Visibility, back.Visibility);
                    kinds.Add(e.GetType().Name);
                }

                for (int s = 0; s < 4; s++)
                {
                    PlayerView view = engine.ViewFor((Seat)s);
                    var w = new WireWriter();
                    WireCodec.Write(w, view);
                    byte[] bytes = w.ToArray();
                    var reader = new WireReader(bytes);
                    Assert.AreEqual(view, WireCodec.ReadPlayerView(reader));
                    Assert.IsTrue(reader.AtEnd, "nothing is left over");
                    views++;
                }

                Advance(engine);
            }

            events.Clear();
            engine.DrainEvents(events);
            foreach (MatchEvent e in events)
            {
                var w = new WireWriter();
                WireCodec.Write(w, e);
                Assert.AreEqual(e.ToString(), WireCodec.ReadEvent(new WireReader(w.ToArray())).ToString());
                kinds.Add(e.GetType().Name);
            }

            Assert.AreEqual(Phase.MatchOver, engine.Phase);
            Assert.Greater(views, 1000);
            foreach (string kind in new[]
            {
                nameof(MatchStarted), nameof(RoundStarted), nameof(HandDealt), nameof(CallTurnStarted), nameof(CallPlaced),
                nameof(PlayTurnStarted), nameof(LegalHint), nameof(CardPlayed), nameof(TrickCompleted), nameof(RoundScored),
                nameof(ReadyConfirmed), nameof(MatchEnded),
            })
            {
                Assert.IsTrue(kinds.Contains(kind), kind + " was exercised");
            }
        }

        [Test]
        public void Redealt_RoundTrips()
        {
            var w = new WireWriter();
            WireCodec.Write(w, new Redealt(2, RedealReason.NoSpade));

            var back = (Redealt)WireCodec.ReadEvent(new WireReader(w.ToArray()));

            Assert.AreEqual(2, back.Round);
            Assert.AreEqual(RedealReason.NoSpade, back.Reason);
        }

        [Test]
        public void View_FinalStandings_SurviveTheWire()
        {
            MatchEngine engine = NewEngine(seed: 11);
            engine.Start();
            for (int guard = 0; guard < 5000 && engine.Phase != Phase.MatchOver; guard++)
            {
                Advance(engine);
            }

            PlayerView view = engine.ViewFor(Seat.West);
            var w = new WireWriter();
            WireCodec.Write(w, view);

            PlayerView back = WireCodec.ReadPlayerView(new WireReader(w.ToArray()));

            Assert.AreEqual(4, back.FinalStandings.Count);
            Assert.AreEqual(view, back);
        }

        [Test]
        public void View_OnTheWire_NeverContainsAnotherSeatsCards()
        {
            MatchEngine engine = NewEngine(seed: 3);
            engine.Start();
            PlayerView south = engine.ViewFor(Seat.South);
            var w = new WireWriter();
            WireCodec.Write(w, south);
            byte[] bytes = w.ToArray();

            // The only 8-byte card set in a fresh view is the viewer's own hand: no other seat's
            // hand appears anywhere in the payload.
            for (int s = 1; s < 4; s++)
            {
                ulong other = HandOf(engine, (Seat)s).Bits;
                Assert.IsFalse(Contains(bytes, other), "the hand of seat " + s + " is not in South's view");
            }

            Assert.IsTrue(Contains(bytes, south.Hand.Bits), "South's own hand is");
        }

        [Test]
        public void Reader_TruncatedMessage_IsAFormatErrorNotACrash()
        {
            MatchEngine engine = NewEngine(seed: 5);
            engine.Start();
            var w = new WireWriter();
            WireCodec.Write(w, engine.ViewFor(Seat.South));
            byte[] bytes = w.ToArray();

            for (int length = 0; length < bytes.Length; length += 7)
            {
                var cut = new byte[length];
                System.Array.Copy(bytes, cut, length);
                Assert.Throws<WireFormatException>(() => WireCodec.ReadPlayerView(new WireReader(cut)), "length " + length);
            }
        }

        [Test]
        public void Reader_Garbage_IsAFormatErrorNotACrash()
        {
            var rng = new Pcg32Random(99);
            for (int i = 0; i < 300; i++)
            {
                var bytes = new byte[1 + rng.NextInt(120)];
                for (int b = 0; b < bytes.Length; b++)
                {
                    bytes[b] = (byte)rng.NextInt(256);
                }

                try
                {
                    WireCodec.ReadEvent(new WireReader(bytes));
                }
                catch (WireFormatException)
                {
                }

                try
                {
                    WireCodec.ReadPlayerView(new WireReader(bytes));
                }
                catch (WireFormatException)
                {
                }

                try
                {
                    TableUpdate.Read(new WireReader(bytes));
                }
                catch (WireFormatException)
                {
                }
            }
        }

        [Test]
        public void Writer_Primitives_RoundTrip()
        {
            var w = new WireWriter();
            w.Byte(200);
            w.Bool(true);
            w.UInt16(65535);
            w.Int32(-123456789);
            w.UInt32(4000000000);
            w.UInt64(0xFEDCBA9876543210UL);
            w.String("नमस्ते Asha");
            w.String(null);

            var r = new WireReader(w.ToArray());

            Assert.AreEqual(200, r.Byte());
            Assert.IsTrue(r.Bool());
            Assert.AreEqual(65535, r.UInt16());
            Assert.AreEqual(-123456789, r.Int32());
            Assert.AreEqual(4000000000, r.UInt32());
            Assert.AreEqual(0xFEDCBA9876543210UL, r.UInt64());
            Assert.AreEqual("नमस्ते Asha", r.String());
            Assert.AreEqual(string.Empty, r.String());
            Assert.IsTrue(r.AtEnd);
        }

        [Test]
        public void RoomSnapshot_RoundTrips()
        {
            var seats = new[]
            {
                new RoomSnapshot.Member(true, "Hari", 2, "kathmandu-gold", true, false, false, true),
                new RoomSnapshot.Member(true, "Asha", 4, "classic-crimson", false, true, false, false),
                RoomSnapshot.Member.Empty,
                RoomSnapshot.Member.Empty,
            };
            var w = new WireWriter();
            new RoomSnapshot("AB2CDE", new RoomSettings("Hari's Table", 15), seats, true).WriteTo(w);

            RoomSnapshot back = RoomSnapshot.Read(new WireReader(w.ToArray()));

            Assert.AreEqual("AB2CDE", back.Code);
            Assert.AreEqual("Hari's Table", back.Settings.Name);
            Assert.AreEqual(15, back.Settings.TurnSeconds);
            Assert.AreEqual(5, back.Settings.Rounds);
            Assert.IsTrue(back.InMatch);
            Assert.AreEqual("Asha", back.Seats[1].Name);
            Assert.AreEqual(4, back.Seats[1].AvatarId);
            Assert.IsTrue(back.Seats[1].IsReady);
            Assert.IsFalse(back.Seats[1].IsConnected);
            Assert.IsTrue(back.Seats[0].IsHost);
            Assert.IsFalse(back.Seats[2].Occupied);
        }

        [Test]
        public void TableStatus_RoundTrips()
        {
            var connections = new[] { SeatConnection.Connected, SeatConnection.Away, SeatConnection.Reconnecting, SeatConnection.Left };
            var w = new WireWriter();
            new TableStatus(Seat.North, 12500, 30000, TableStatus.NoClock, connections).WriteTo(w);

            TableStatus back = TableStatus.Read(new WireReader(w.ToArray()));

            Assert.AreEqual(Seat.North, back.TimerSeat);
            Assert.AreEqual(12500, back.TimerRemainingMs);
            Assert.AreEqual(30000, back.TimerDurationMs);
            Assert.AreEqual(TableStatus.NoClock, back.SummaryRemainingMs);
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(connections[s], back.ConnectionOf((Seat)s));
            }
        }

        private static MatchEngine NewEngine(ulong seed)
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "0", "classic-crimson", false),
                new SeatProfile("Suman", "1", "himalayan-ivory", true),
                new SeatProfile("Asha", "2", "kathmandu-gold", true),
                new SeatProfile("Bikash", "3", "classic-crimson", true),
            };
            return new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(seed), new MatchSetup("codec-" + seed, seats));
        }

        private static void Advance(MatchEngine engine)
        {
            if (engine.Phase == Phase.RoundSummary)
            {
                for (int s = 0; s < 4; s++)
                {
                    engine.Execute(new ReadyNext((Seat)s));
                }

                return;
            }

            PlayerView view = engine.ViewFor(engine.CurrentSeat);
            if (engine.Phase == Phase.Calling)
            {
                engine.Execute(new PlaceCall(view.Viewer, 2, view.CurrentTurn));
            }
            else if (engine.Phase == Phase.Playing)
            {
                engine.Execute(new PlayCard(view.Viewer, view.LegalCards().Lowest(), view.CurrentTurn));
            }
        }

        private static CardSet HandOf(MatchEngine engine, Seat seat) => engine.ViewFor(seat).Hand;

        private static bool Contains(byte[] bytes, ulong value)
        {
            for (int i = 0; i + 8 <= bytes.Length; i++)
            {
                ulong at = 0;
                for (int b = 7; b >= 0; b--)
                {
                    at = (at << 8) | bytes[i + b];
                }

                if (at == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
