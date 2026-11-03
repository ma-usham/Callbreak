using System;
using System.Collections;
using System.Threading;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Tutorial;
using Callbreak.Infrastructure.Platform;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// ADR 084: the first practice match a new player accepts teaches the game in its first hand,
    /// on the real Boot, Menu and Table scenes. Saves go to a temporary folder.
    /// </summary>
    public sealed class TutorialFlowTests
    {
        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator FirstPracticeMatch_GuidesTheFirstHand_StepByStep_AndSkipEndsItForGood()
        {
            yield return StartPractice();
            var session = (OfflineMatchSession)_game.Table.Container.Resolve<IMatchSession>();
            TutorialView view = Overlay();
            ILocalization text = _game.Services.Resolve<ILocalization>();
            Assert.IsNotNull(session.Tutorial, "the practice match is the tutorial");

            // The welcome comes once the deal has been played, centred over a dim.
            yield return GameFixture.Until(() => view.IsShown, "the welcome");
            Assert.AreEqual(TutorialStep.Welcome, session.Tutorial.Step);
            Assert.AreEqual(text.Get("ui.tutorial.welcome_title"), view.Title);
            Assert.IsTrue(view.BlocksTable);
            Assert.AreEqual(text.Get("ui.tutorial.next"), view.NextLabel);

            GameFixture.PressIn(view, "NextButton");
            yield return GameFixture.Until(() => session.Tutorial.Step == TutorialStep.Call && view.IsShown, "the call step", 30f);
            Assert.IsFalse(view.BlocksTable, "the call panel takes touches");
            Assert.IsNull(view.NextLabel, "the call step waits for the call, not for Next");
            yield return GameFixture.Until(() => view.IsPointing, "the pointer on the suggested call");

            session.PlaceCallAsync(session.Tutorial.TargetCall, CancellationToken.None).Wait();
            yield return GameFixture.Until(() => session.Tutorial.Step == TutorialStep.Follow && view.IsShown, "the follow step", 30f);
            Assert.AreEqual(text.Get("rules.primer.follow_title"), view.Title);
            StringAssert.Contains(text.Get("ui.tutorial.drag_hint"), view.Body, "it says how a card is played");
            yield return GameFixture.Until(() => view.IsPointing, "the pointer on the card");

            GameFixture.PressIn(view, "SkipButton");
            yield return null;

            Assert.IsFalse(session.Tutorial.IsActive);
            Assert.IsTrue(_game.Settings.TutorialDone, "skipping remembers it");
            yield return GameFixture.Until(() => !view.IsShown, "the caption to go");
            Assert.IsFalse(view.IsPointing);
        }

        [UnityTest]
        public IEnumerator AnyOtherOfflineMatch_HasNoTutorial()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INetworkStatus>(new Network()));
            _ = _game.Flow.StartMatchAsync(new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy), CancellationToken.None);
            yield return _game.UntilTable();

            var session = (OfflineMatchSession)_game.Table.Container.Resolve<IMatchSession>();
            float until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.IsNull(session.Tutorial);
            Assert.IsFalse(Overlay().IsShown);
        }

        /// <summary>A fresh install: the name, the primer's "Got It", then Practice on the offer.</summary>
        private IEnumerator StartPractice()
        {
            yield return _game.Boot(freshInstall: true, extra: builder => builder.RegisterInstance<INetworkStatus>(new Network()));
            yield return null;
            NamePromptView prompt = UnityEngine.Object.FindFirstObjectByType<NamePromptView>(FindObjectsInactive.Include);
            prompt.GetComponentInChildren<TMPro.TMP_InputField>(true).text = "Asha";
            GameFixture.PressIn(prompt, "SaveButton");
            yield return null;
            GameFixture.Press(_game.MenuRouter, "GotItButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilTable();
        }

        private static TutorialView Overlay()
        {
            TutorialView view = UnityEngine.Object.FindFirstObjectByType<TutorialView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "the Table scene has the tutorial overlay");
            return view;
        }

        private sealed class Network : INetworkStatus
        {
            public event Action<bool> Changed
            {
                add { }
                remove { }
            }

            public bool IsOnline => true;
        }
    }
}
