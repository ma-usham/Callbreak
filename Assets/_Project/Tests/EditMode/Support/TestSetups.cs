using Callbreak.Core.Match;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>Seat profiles named after the GDD FIG 2.1 table: You (S), Suman (E), Asha (N), Bikash (W).</summary>
    public static class TestSetups
    {
        public static MatchSetup Humans(Seat? firstDealer = null, string matchId = "test-match")
        {
            return With(false, false, false, false, firstDealer, matchId);
        }

        public static MatchSetup AllBots(Seat? firstDealer = null, string matchId = "bot-match")
        {
            return With(true, true, true, true, firstDealer, matchId);
        }

        public static MatchSetup With(bool southBot, bool eastBot, bool northBot, bool westBot, Seat? firstDealer = null, string matchId = "test-match")
        {
            return new MatchSetup(
                matchId,
                new[]
                {
                    new SeatProfile("You", "avatar-0", "classic-crimson", southBot),
                    new SeatProfile("Suman", "avatar-1", "himalayan-ivory", eastBot),
                    new SeatProfile("Asha", "avatar-2", "kathmandu-gold", northBot),
                    new SeatProfile("Bikash", "avatar-3", "classic-crimson", westBot),
                },
                firstDealer);
        }
    }
}
