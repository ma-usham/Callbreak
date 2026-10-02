using Callbreak.Application.Online;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The hand-over's messages on the wire (ADR 030): who the next host is, what that player alone is
    /// sent, and the report a player brings the new host. Each survives the wire whole; one that is
    /// malformed is refused; and a report at every limit still fits what a new host takes.
    /// </summary>
    public sealed class SuccessionWireTests
    {
        [TestCase(2, "device-7")]
        [TestCase(NextHostNotice.NoSeat, "")]
        public void NextHost_SurvivesTheWire(int nextSeat, string nextAddress)
        {
            var notice = new NextHostNotice(3, 1, "device-2", nextSeat, nextAddress);
            var w = new WireWriter();
            notice.WriteTo(w);
            var reader = new WireReader(w.ToArray());

            NextHostNotice back = NextHostNotice.Read(reader);

            Assert.IsTrue(reader.AtEnd);
            Assert.AreEqual(notice, back);
            Assert.AreEqual(3, back.Generation);
            Assert.AreEqual(1, back.HostSeat);
            Assert.AreEqual("device-2", back.HostAddress);
            Assert.AreEqual(nextSeat, back.NextSeat);
            Assert.AreEqual(nextSeat >= 0, back.HasNext);
        }

        [TestCase(4)]
        [TestCase(200)]
        public void NextHost_SeatThatIsNoSeat_IsRefused(byte seat)
        {
            var w = new WireWriter();
            w.UInt16(1);
            w.Byte(0);
            w.String("device-1");
            w.Byte(seat);
            w.String("device-2");

            Assert.Throws<WireFormatException>(() => NextHostNotice.Read(new WireReader(w.ToArray())));
        }

        [Test]
        public void Package_SurvivesTheWire_IdentitiesForThePeopleDealtHandsForTheBots()
        {
            CardSet bot = CardSet.Parse("AS KS QS 2H 3H 4H 5D 6D 7D 8C 9C TC JC");
            var package = new SuccessionPackage(2, 4, new[] { "id-south", null, "id-north", null }, new[] { CardSet.Empty, bot, CardSet.Empty, CardSet.Empty });
            var w = new WireWriter();
            package.WriteTo(w);
            var reader = new WireReader(w.ToArray());

            SuccessionPackage back = SuccessionPackage.Read(reader);

            Assert.IsTrue(reader.AtEnd);
            Assert.AreEqual(2, back.Generation);
            Assert.AreEqual(4, back.Round);
            Assert.AreEqual("id-south", back.IdentityOf(Seat.South));
            Assert.AreEqual("id-north", back.IdentityOf(Seat.North));
            Assert.IsNull(back.IdentityOf(Seat.East));
            Assert.IsFalse(back.TryGetDealtHand(Seat.South, out _), "a person's seat carries no cards");
            Assert.IsTrue(back.TryGetDealtHand(Seat.East, out CardSet east));
            Assert.AreEqual(bot, east);
            Assert.IsTrue(back.TryGetDealtHand(Seat.West, out CardSet west));
            Assert.IsTrue(west.IsEmpty);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void Package_SeatOfNoKnownKind_IsRefused(byte kind)
        {
            var w = new WireWriter();
            w.UInt16(1);
            w.Byte(1);
            w.Byte(kind);
            Assert.Throws<WireFormatException>(() => SuccessionPackage.Read(new WireReader(w.ToArray())));
        }

        [Test]
        public void Package_PersonWithoutAnIdentity_IsRefused()
        {
            var w = new WireWriter();
            w.UInt16(1);
            w.Byte(1);
            w.Byte(1);
            w.String(string.Empty);
            Assert.Throws<WireFormatException>(() => SuccessionPackage.Read(new WireReader(w.ToArray())));
        }

        [Test]
        public void Report_IsAHelloFollowedByTheLastUpdateAndItsView()
        {
            MatchEngine engine = Engine("report-1", "Hari");
            engine.Start();
            PlayerView view = engine.ViewFor(Seat.East);
            var hello = new HelloMessage(Protocol.Version, "identity-of-asha", "Asha", 3, "classic-crimson");

            var reader = new WireReader(ClientMessages.Report(hello, 41, view));

            Assert.AreEqual((byte)ClientMessageKind.Report, reader.Byte());
            HelloMessage helloBack = HelloMessage.Read(reader);
            Assert.AreEqual("identity-of-asha", helloBack.Identity);
            Assert.AreEqual("Asha", helloBack.Name);
            Assert.AreEqual(41u, reader.UInt32());
            Assert.AreEqual(view, WireCodec.ReadPlayerView(reader));
            Assert.IsTrue(reader.AtEnd);
        }

        [Test]
        public void Report_EveryFieldAtItsLimitAtTheEndOfAMatch_FitsWhatANewHostTakes()
        {
            // Four names of 96 bytes (32 three-byte letters), a match id of 64 bytes, and the biggest
            // view there is: the last round's 13 tricks, five rounds of scores and the final standings.
            string name = new string('क', 32);
            MatchEngine engine = Engine(new string('m', 64), name);
            engine.Start();
            for (int guard = 0; guard < 5000 && engine.Phase != Phase.MatchOver; guard++)
            {
                Advance(engine);
            }

            PlayerView view = engine.ViewFor(Seat.South);
            var hello = new HelloMessage(Protocol.Version, new string('i', Protocol.MaxIdentityBytes), name, 9, new string('d', Protocol.MaxIdBytes));

            byte[] report = ClientMessages.Report(hello, uint.MaxValue, view);

            Assert.AreEqual(Phase.MatchOver, view.Phase);
            Assert.Greater(report.Length, Protocol.MaxClientPayloadBytes, "longer than any other message a member sends");
            Assert.LessOrEqual(report.Length, 1536, "under 1.5 KB");
            Assert.LessOrEqual(report.Length, Protocol.MaxReportBytes);
        }

        private static MatchEngine Engine(string matchId, string name)
        {
            var seats = new[]
            {
                new SeatProfile(name, "0", "classic-crimson", false),
                new SeatProfile(name, "1", "classic-crimson", false),
                new SeatProfile(name, "2", "classic-crimson", true),
                new SeatProfile(name, "3", "classic-crimson", true),
            };
            return new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(13), new MatchSetup(matchId, seats));
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
    }
}
