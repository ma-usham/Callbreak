using System.Text;
using Callbreak.Application.Sessions;
using Callbreak.Bootstrap;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Editor
{
    /// <summary>
    /// Plays the local seat in Play mode, for end-to-end checks (Tools/Unity/playcheck.sh): it
    /// makes the call with the chips, taps the lowest legal card whenever the table shows the
    /// local turn and presses Next Round on the round results, all through the real views, as a
    /// player would. <c>Start()</c> from an eval step, <c>Report()</c> to read what it did.
    /// </summary>
    public static class AutoPlayer
    {
        private const double StepSeconds = 0.25;
        private const double RetapSeconds = 3.0;
        private const string CallChip = "Chip1";

        private static bool _running;
        private static double _nextStep;
        private static double _lastTapAt;
        private static int _lastTapHandCount;
        private static int _calls;
        private static int _taps;
        private static int _nextPresses;
        private static string _lastProblem = string.Empty;

        public static string Start()
        {
            if (!EditorApplication.isPlaying)
            {
                return "not in Play mode";
            }

            if (!_running)
            {
                _running = true;
                _calls = 0;
                _taps = 0;
                _nextPresses = 0;
                _lastTapHandCount = -1;
                _lastProblem = string.Empty;
                EditorApplication.update += Update;
            }

            return "auto player on";
        }

        public static string Stop()
        {
            if (_running)
            {
                _running = false;
                EditorApplication.update -= Update;
            }

            return Report();
        }

        /// <summary>What the player did so far and where the match stands.</summary>
        public static string Report()
        {
            var text = new StringBuilder();
            text.Append(_running ? "running" : "stopped");
            text.Append(" calls=").Append(_calls).Append(" taps=").Append(_taps).Append(" next=").Append(_nextPresses);
            TableLifetimeScope scope = Object.FindFirstObjectByType<TableLifetimeScope>();
            if (scope != null && scope.Container != null)
            {
                var session = scope.Container.Resolve<IMatchSession>();
                var router = scope.Container.Resolve<ScreenRouter>();
                text.Append(" status=").Append(session.Status)
                    .Append(" phase=").Append(session.View.Phase)
                    .Append(" round=").Append(session.View.Round)
                    .Append(" tricks=").Append(session.View.CompletedTricks.Count)
                    .Append(" screen=").Append(router.Current).Append('/').Append(router.Depth);
            }
            else
            {
                text.Append(" no table");
            }

            if (_lastProblem.Length > 0)
            {
                text.Append(" problem=").Append(_lastProblem);
            }

            return text.ToString();
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextStep)
            {
                return;
            }

            _nextStep = now + StepSeconds;
            TableLifetimeScope scope = Object.FindFirstObjectByType<TableLifetimeScope>();
            if (scope == null || scope.Container == null)
            {
                return;
            }

            try
            {
                Step(scope.Container, now);
            }
            catch (System.Exception e)
            {
                _lastProblem = e.GetType().Name + ": " + e.Message;
            }
        }

        private static void Step(IObjectResolver table, double now)
        {
            var router = table.Resolve<ScreenRouter>();
            if (router.Current == ScreenId.RoundResults)
            {
                if (Press(router.Get<UIScreen>(ScreenId.RoundResults), "NextButton"))
                {
                    _nextPresses++;
                }

                return;
            }

            if (router.Current != ScreenId.Gameplay)
            {
                return;
            }

            BiddingPanelView panel = Object.FindFirstObjectByType<BiddingPanelView>();
            if (panel != null && CanPress(panel, CallChip))
            {
                Press(panel, CallChip);
                if (Press(panel, "ConfirmButton"))
                {
                    _calls++;
                }

                return;
            }

            var played = table.Resolve<PlayedTable>();
            var session = table.Resolve<IMatchSession>();
            if (!played.State.TryGetObligation(out PlayObligation obligation) || session.View.Phase != Phase.Playing || !session.View.IsMyTurn)
            {
                return;
            }

            // One tap per turn; again only if the hand has not changed for a while.
            int handCount = session.View.Hand.Count;
            if (handCount == _lastTapHandCount && now < _lastTapAt + RetapSeconds)
            {
                return;
            }

            HandView hand = Object.FindFirstObjectByType<HandView>();
            Card card = obligation.Legal.Lowest();
            CardView view = hand != null ? hand.ViewOf(card) : null;
            if (view == null)
            {
                _lastProblem = "no card view for " + card;
                return;
            }

            var corners = new Vector3[4];
            ((RectTransform)view.transform).GetWorldCorners(corners);
            Vector2 position = (corners[1] + corners[2]) * 0.5f;
            position.y -= 30f;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position,
                pressPosition = position,
                button = PointerEventData.InputButton.Left,
            };
            hand.OnPointerDown(pointer);
            hand.OnPointerUp(pointer);
            _lastTapAt = now;
            _lastTapHandCount = handCount;
            _taps++;
        }

        private static bool CanPress(Component root, string buttonName)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == buttonName && button.IsInteractable())
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Press(Component root, string buttonName)
        {
            if (root == null)
            {
                return false;
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == buttonName && button.IsInteractable())
                {
                    button.onClick.Invoke();
                    return true;
                }
            }

            return false;
        }
    }
}
