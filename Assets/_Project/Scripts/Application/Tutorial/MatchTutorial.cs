using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Application.Tutorial
{
    /// <summary>A step of the first-hand tutorial (ADR 084), in the order they come.</summary>
    public enum TutorialStep
    {
        /// <summary>Nothing to show: the match plays on until the next step.</summary>
        None,

        /// <summary>After the deal: one hand, played together. Waits for Next.</summary>
        Welcome,

        /// <summary>The player's call turn: points at the suggested call, then Confirm.</summary>
        Call,

        /// <summary>The player's turn in trick 1: follow the led suit.</summary>
        Follow,

        /// <summary>Trick 1 is over: the highest card of the led suit took it. Waits for Next.</summary>
        TrickWon,

        /// <summary>The player's turn in trick 2, out of the led suit: play a spade.</summary>
        Trump,

        /// <summary>The spade won trick 2; the rest of the hand is the player's. Waits for Next.</summary>
        YouWon,

        /// <summary>The hand is scored: how the call counts. Waits for Next.</summary>
        Score,

        /// <summary>"You're ready": the match goes on as a normal one. Its button ends the tutorial.</summary>
        Ready,
    }

    /// <summary>
    /// The guided first hand of the first practice match (ADR 084), like a short video the player
    /// follows: a caption per step, a pointer on what to tap, Next and Skip. It follows the match's
    /// events in the order the session emits them (only those the local seat may see), so it
    /// knows a step before the table has shown it: <see cref="OpenedAt"/> says after how many
    /// events the step belongs on screen. While a step waits for Next the bots wait too
    /// (<see cref="HoldsBots"/>); a step that waits for Next also ends when the player calls or
    /// plays. Finishing or skipping it marks the tutorial done, once.
    /// </summary>
    public sealed class MatchTutorial
    {
        private readonly TutorialDeal _deal;
        private readonly Seat _local;
        private readonly Action _done;

        private int _round;
        private int _tricks;

        /// <param name="deal">The scripted first hand.</param>
        /// <param name="local">The player's seat.</param>
        /// <param name="done">Remembers that the tutorial has been seen (finished or skipped).</param>
        public MatchTutorial(TutorialDeal deal, Seat local, Action done)
        {
            _deal = deal ?? throw new ArgumentNullException(nameof(deal));
            _local = local;
            _done = done;
        }

        /// <summary>The step changed, or the tutorial ended.</summary>
        public event Action Changed;

        /// <summary>Until the tutorial is finished or skipped.</summary>
        public bool IsActive { get; private set; } = true;

        public TutorialStep Step { get; private set; }

        /// <summary>How many events had been observed when the step opened: it shows once the table has played that many.</summary>
        public int OpenedAt { get; private set; }

        /// <summary>Events observed so far.</summary>
        public int Observed { get; private set; }

        /// <summary>True while the step waits for Next (or the end button).</summary>
        public bool WaitsForNext => Step == TutorialStep.Welcome || Step == TutorialStep.TrickWon
            || Step == TutorialStep.YouWon || Step == TutorialStep.Score || Step == TutorialStep.Ready;

        /// <summary>No bot moves while a caption waits for Next.</summary>
        public bool HoldsBots => IsActive && WaitsForNext;

        /// <summary>The call the pointer is on during <see cref="TutorialStep.Call"/>; 0 otherwise.</summary>
        public int TargetCall => Step == TutorialStep.Call ? _deal.SuggestedCall : 0;

        /// <summary>The card the pointer is on during <see cref="TutorialStep.Follow"/> and <see cref="TutorialStep.Trump"/>.</summary>
        public Card? TargetCard => Step == TutorialStep.Follow ? _deal.PlayerCard(0)
            : Step == TutorialStep.Trump ? _deal.PlayerCard(1)
            : (Card?)null;

        /// <summary>Who won the last guided trick.</summary>
        public Seat TrickWinner { get; private set; }

        /// <summary>The player's line of the scored hand, for <see cref="TutorialStep.Score"/>.</summary>
        public SeatRoundResult Result { get; private set; }

        /// <summary>Follows one event the session delivers to the local seat, before the table plays it.</summary>
        public void Observe(MatchEvent e)
        {
            Observed++;
            if (!IsActive)
            {
                return;
            }

            switch (e)
            {
                case RoundStarted started:
                    _round = started.Round;
                    _tricks = 0;
                    if (_round > 1)
                    {
                        // The next hand is dealt: whatever was left of the tutorial is over.
                        Finish();
                    }

                    break;

                case HandDealt dealt when dealt.Seat == _local && _round == 1:
                    Open(TutorialStep.Welcome);
                    break;

                case CallTurnStarted turn when turn.Seat == _local && _round == 1:
                    Open(TutorialStep.Call);
                    break;

                case PlayTurnStarted turn when turn.Seat == _local && _round == 1 && _tricks < _deal.GuidedTricks:
                    Open(_tricks == 0 ? TutorialStep.Follow : TutorialStep.Trump);
                    break;

                case CallPlaced placed when placed.Seat == _local:
                    EndOnMove();
                    break;

                case CardPlayed played when played.Seat == _local:
                    EndOnMove();
                    break;

                case TrickCompleted completed when _round == 1:
                    _tricks++;
                    if (_tricks <= _deal.GuidedTricks)
                    {
                        TrickWinner = completed.Winner;
                        Open(completed.Winner == _local ? TutorialStep.YouWon : TutorialStep.TrickWon);
                    }

                    break;

                case RoundScored scored when scored.Round == 1:
                    Result = scored.Result[_local];
                    Open(TutorialStep.Score);
                    break;
            }
        }

        /// <summary>Next on a step that waits for it; the last step's button ends the tutorial.</summary>
        public void Next()
        {
            if (!IsActive || !WaitsForNext)
            {
                return;
            }

            if (Step == TutorialStep.Score)
            {
                // The same moment, the next card: it shows at once.
                Step = TutorialStep.Ready;
                Changed?.Invoke();
                return;
            }

            if (Step == TutorialStep.Ready)
            {
                Finish();
                return;
            }

            Close();
        }

        /// <summary>"Skip tutorial": the match goes on as a normal one.</summary>
        public void Skip()
        {
            if (IsActive)
            {
                Finish();
            }
        }

        private void Open(TutorialStep step)
        {
            Step = step;
            OpenedAt = Observed;
            Changed?.Invoke();
        }

        private void Close()
        {
            if (Step == TutorialStep.None)
            {
                return;
            }

            Step = TutorialStep.None;
            Changed?.Invoke();
        }

        /// <summary>The player called or played: the step that asked for it, or a caption they moved on from, is over.</summary>
        private void EndOnMove()
        {
            if (Step != TutorialStep.Score && Step != TutorialStep.Ready)
            {
                Close();
            }
        }

        private void Finish()
        {
            IsActive = false;
            Step = TutorialStep.None;
            _done?.Invoke();
            Changed?.Invoke();
        }
    }
}
