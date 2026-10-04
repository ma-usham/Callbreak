using System.Threading;
using Callbreak.Application.Economy;
using Callbreak.Application.Sessions;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A Stake Match outlives its host (ADR 030): when the host leaves during the match, the next host in
    /// line carries it on, the same match settles each person's stake at its end, and the host who left
    /// has lost its own.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void StakeMatch_TheHostLeavesMidMatch_TheNextInLineCarriesOn_AndTheGuestIsStillPaidAtTheEnd()
        {
            UseQuickMatch(listingDelay: 0.5);
            StakePlayer hari = NewStakePlayer("Hari", 50);
            StakePlayer asha = NewStakePlayer("Asha", 50);
            Tap(hari.Quick);
            Tap(asha.Quick);
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted && asha.Quick.Search.IsCompleted, 30);
            hari.Match.Start();
            asha.Match.Start();
            SettleQuick(1);
            StakePlayer host = hari.Quick.Service.Host != null ? hari : asha;
            StakePlayer guest = host == hari ? asha : hari;
            PlayQuickUntil(() => MidTrick(host.Quick, 2), 20000);
            string matchId = host.Match.View.MatchId;

            host.Match.LeaveAsync(CancellationToken.None);
            SettleQuickUntil(() => guest.Quick.Service.Host != null && guest.Match.Status == SessionStatus.Running, 5);
            PlayQuick(20000);

            Assert.AreEqual(SessionStatus.Ended, guest.Match.Status, "the match went on at the next host in line");
            Assert.AreEqual(matchId, guest.Match.View.MatchId, "the same match");
            Assert.AreEqual(450 + StakeRules.PrizeFor(50, PlaceOf(guest.Match)), guest.Wallet.Coins, "its end pays the guest by place");
            Assert.AreEqual(450, host.Wallet.Coins, "the host who left lost its stake and won nothing");
            Assert.IsTrue(host.Stakes.TryGetStake(matchId, out StakedMatch left));
            Assert.AreEqual(StakeState.Forfeited, left.State);
            Assert.That(_peerFaults, Is.Empty);
        }
    }
}
