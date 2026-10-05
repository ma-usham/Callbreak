using System;
using System.Collections;
using System.Collections.Generic;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Overlays;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Stake Match around a match (the owner, 5 Oct 2026; ADR 034), in the real Boot, Menu and Table
    /// scenes with a scripted online table (<see cref="ScriptedOnlineTable"/>) and the game's own
    /// wallet and stake ledger. The match results show the prize a Stake Match paid, the gold coin and
    /// "+200 coins", on the player's row and on no other; a Quick Match, a stake lost by leaving and a
    /// stake given back show none. The Play Online chips start on the last stake the player chose,
    /// after a Quick Match and after a restart. Saves go to a temporary folder.
    /// </summary>
    public sealed class StakeMatchFlowTests
    {
        private GameFixture _game;
        private ScriptedOnlineTable _table;

        private IWallet Wallet => _game.Services.Resolve<IWallet>();

        private IStakeLedger Stakes => _game.Services.Resolve<IStakeLedger>();

        private IOnlineMatchChoice Choice => _game.Services.Resolve<IOnlineMatchChoice>();

        private MatchResultsView Results => _game.TableRouter.Get<MatchResultsView>(ScreenId.MatchResults);

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _table = new ScriptedOnlineTable();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator AStakeMatchThatPaid_ShowsThePrizeAndTheCoin_OnThePlayersRowOnly()
        {
            yield return _table.Open(_game, seed: StakeTaken(100), choose: ChooseStake(1), playButton: "PlayStakeButton");
            Assert.AreEqual(100, Choice.Stake, "Play for 100 coins");
            Assert.AreEqual(400, Wallet.Coins, "the stake was taken when the match began");

            _table.End();
            yield return UntilResults();

            int prize = StakeRules.PrizeFor(100, _table.Place());
            Assert.IsTrue(Stakes.TryGetStake(ScriptedOnlineTable.MatchId, out StakedMatch staked));
            Assert.AreEqual(StakeState.Paid, staked.State, "the match paid its prize");
            Assert.AreEqual(prize, staked.Prize);
            Assert.AreEqual(400 + prize, Wallet.Coins);

            int row = LocalRow();
            Assert.AreEqual(Text(ResultsText.YouKey), Label("RowName" + row).text, "the player's row");
            AssertPrizeOnlyOn(row);
            Assert.AreEqual(Format(MatchResultsPresenter.StakePrizeKey, CoinsFormat.Coins(prize)), Label("RowPrizeLabel" + row).text);
            Assert.AreEqual("+" + prize + " coins", Label("RowPrizeLabel" + row).text, "the approved copy");
            Image coin = Find("RowPrizeCoin" + row).GetComponent<Image>();
            Assert.IsTrue(coin.gameObject.activeInHierarchy, "with the coin");
            Assert.AreEqual("Icon_Coin", coin.sprite.name, "the shared gold coin");
            Assert.IsTrue(Label("RowPrizeLabel" + row).gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator AQuickMatch_ShowsNoPrize()
        {
            yield return _table.Open(_game);
            Assert.AreEqual(StakeRules.None, Choice.Stake);

            _table.End();
            yield return UntilResults();

            Assert.IsFalse(Stakes.TryGetStake(ScriptedOnlineTable.MatchId, out _), "nothing was staked");
            AssertPrizeOnlyOn(-1);
            Assert.AreEqual(500, Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator AStakeLostByLeaving_ShowsNoPrize()
        {
            yield return _table.Open(_game, seed: StakeTaken(50, (stakes, id) => stakes.Forfeit(id)), playButton: "PlayStakeButton");

            _table.End();
            yield return UntilResults();

            Assert.IsTrue(Stakes.TryGetStake(ScriptedOnlineTable.MatchId, out StakedMatch staked));
            Assert.AreEqual(StakeState.Forfeited, staked.State, "a forfeited stake is not paid at the end");
            AssertPrizeOnlyOn(-1);
            Assert.AreEqual(450, Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator AStakeGivenBack_ShowsNoPrize()
        {
            yield return _table.Open(_game, seed: StakeTaken(50, (stakes, id) => stakes.CallOff(id)), playButton: "PlayStakeButton");

            _table.End();
            yield return UntilResults();

            Assert.IsTrue(Stakes.TryGetStake(ScriptedOnlineTable.MatchId, out StakedMatch staked));
            Assert.AreEqual(StakeState.Refunded, staked.State, "a stake given back is not paid at the end");
            AssertPrizeOnlyOn(-1);
            Assert.AreEqual(500, Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator TheChips_StartOnTheLastStakeChosen_AfterAQuickMatch_AndAfterARestart()
        {
            // The player looks at 100 on the chips, then plays a Quick Match.
            yield return _table.Open(_game, choose: ChooseStake(1));
            Assert.AreEqual(StakeRules.None, Choice.Stake, "a Quick Match");
            Assert.AreEqual(100, Choice.LastStake, "the chips' 100 is remembered");
            Assert.AreEqual(500, Wallet.Coins, "and nothing is staked");

            _table.End();
            yield return UntilResults();
            GameFixture.Press(_game.TableRouter, "MainMenuButton");
            yield return _game.UntilMenu();
            yield return OpenPlayOnline();
            AssertChips(1, "Play for 100 coins", "after the Quick Match the chips are on 100");

            // The game saved it; a restart on the same saves reads it back.
            Assert.IsTrue(_game.Services.Resolve<ISaveStore>().TryLoad(OnlineMatchChoice.SaveKey, out OnlineMatchChoiceData saved), "the chips' stake is saved");
            Assert.AreEqual(100, saved.lastStake);
            yield return _game.TearDown();
            _game = new GameFixture();
            yield return _game.Boot(
                seed: saves => saves.Save(OnlineMatchChoice.SaveKey, saved),
                extra: builder => builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true)));
            Assert.AreEqual(StakeRules.None, Choice.Stake, "a new launch looks for a Quick Match until Play");
            yield return OpenPlayOnline();
            AssertChips(1, "Play for 100 coins", "after a restart the chips are still on 100");

            // And back to 50, which is remembered in turn.
            GameFixture.Press(_game.MenuRouter, "StakeChip0");
            AssertChips(0, "Play for 50 coins", "50 chosen");
            Assert.AreEqual(50, Choice.LastStake);
            Assert.IsTrue(_game.Services.Resolve<ISaveStore>().TryLoad(OnlineMatchChoice.SaveKey, out saved));
            Assert.AreEqual(50, saved.lastStake);
        }

        /// <summary>A stake of <paramref name="stake"/> taken for the scripted table before the game starts, then settled as <paramref name="then"/> says.</summary>
        private static Action<ISaveStore> StakeTaken(int stake, Action<StakeLedger, string> then = null)
        {
            return saves =>
            {
                var stakes = new StakeLedger(saves, new Wallet(saves));
                Assert.IsTrue(stakes.TakeStake(ScriptedOnlineTable.MatchId, stake), "the stake is taken");
                then?.Invoke(stakes, ScriptedOnlineTable.MatchId);
            };
        }

        private static Action<ScreenRouter> ChooseStake(int chip) => router => GameFixture.Press(router, "StakeChip" + chip);

        private IEnumerator UntilResults()
        {
            yield return GameFixture.Until(() => _game.TableRouter != null && _game.TableRouter.Current == ScreenId.MatchResults, "the match results to open");
            yield return null;
        }

        private IEnumerator OpenPlayOnline()
        {
            GameFixture.Press(_game.MenuRouter, "PlayOnlineButton");
            Assert.AreEqual(ScreenId.PlayOnline, _game.MenuRouter.Current, "PLAY ONLINE opens Play Online");
            yield return null;
        }

        private void AssertChips(int chip, string playLabel, string message)
        {
            PlayOnlineView view = _game.MenuRouter.Get<PlayOnlineView>(ScreenId.PlayOnline);
            Assert.AreEqual(chip, view.SelectedChip, message);
            foreach (TMP_Text label in view.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "PlayStakeLabel")
                {
                    Assert.AreEqual(playLabel, label.text, message);
                    return;
                }
            }

            Assert.Fail("Play Online has no PlayStakeLabel.");
        }

        /// <summary>The prize shows on <paramref name="row"/> and on no other row (-1: on none).</summary>
        private void AssertPrizeOnlyOn(int row)
        {
            for (int i = 0; i < MatchResultsView.RowCount; i++)
            {
                Assert.AreEqual(i == row, Find("RowPrize" + i).gameObject.activeInHierarchy, "the prize on row " + i);
            }
        }

        /// <summary>The row of the results that is this player's: the rows follow the standings.</summary>
        private int LocalRow()
        {
            IReadOnlyList<Standing> standings = _table.View.FinalStandings;
            for (int i = 0; i < standings.Count; i++)
            {
                if (standings[i].Seat == _table.Local)
                {
                    return i;
                }
            }

            Assert.Fail("the standings have no row for " + _table.Local);
            return -1;
        }

        private Transform Find(string name)
        {
            foreach (Transform child in Results.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail("Match Results has no " + name + ".");
            return null;
        }

        private TMP_Text Label(string name) => Find(name).GetComponent<TMP_Text>();

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);
    }
}
