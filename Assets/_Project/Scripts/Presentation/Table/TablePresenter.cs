using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table.Playback;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screens 07/08, §23: the table. It queues the session's events and plays them one at a
    /// time (deal, call bubble, card flight, trick sweep), so the table shows the state of the last
    /// <em>played</em> event; a resync clears the queue and rebuilds the table from the session's
    /// view at once. It owns the local hand's input: a legal card flies at once (optimistic) and
    /// input stays locked until that play's own event has played, the session refuses it, or two
    /// seconds pass, when the card rolls back; an illegal tap shakes the card and flashes the prompt
    /// that says what the rules ask for. It also drives the HUD, the turn prompt, the turn clock,
    /// emotes, the sort button and the way into the match menu, scorecard and settings, and pauses
    /// an offline match while one of those covers the table.
    /// </summary>
    public sealed class TablePresenter : IStartable, ITickable, IDisposable, IEventPlayback
    {
        /// <summary>The Emotes table in display order; an emote id is an index into it (GDD §8).</summary>
        public static readonly IReadOnlyList<string> EmoteKeys = new[]
        {
            "emote.namaste", "emote.well_played", "emote.nice_call", "emote.oops", "emote.thinking", "emote.good_game",
        };

        private const string RedealToastKey = "ui.toast.redeal";
        private const string HostLeftToastKey = "ui.toast.host_left";
        private const string WelcomeBackToastKey = "ui.toast.welcome_back";
        private const string LeftTableToastKey = "ui.toast.left_table";
        private const string CallTitleKey = "ui.bidding.title";
        private const float CallBubbleSeconds = 0.3f;
        private const float BubbleHoldSeconds = 1.4f;
        private const float EmoteHoldSeconds = 2.4f;
        private const float LeadBeatSeconds = 0.4f;
        private const double WarningSeconds = 5.0;
        private const float MaxFrameSeconds = 0.25f;
        private const int DealSoundEvery = 6;

        private readonly TableView _view;
        private readonly GameplayScreen _gameplay;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly IHaptics _haptics;
        private readonly IToast _toast;
        private readonly IClock _clock;
        private readonly IDialogs _dialogs;
        private readonly IAppFlow _flow;

        private readonly MatchEventPlayer _player;
        private readonly TableSkin _skin;
        private readonly TableRenderer _renderer;
        private readonly PlayLock _playLock = new PlayLock();
        private readonly EmoteLimiter _emoteLimiter = new EmoteLimiter();
        private readonly bool[] _muted = new bool[SeatExtensions.Count];
        private readonly SeatConnection[] _connections = new SeatConnection[SeatExtensions.Count];
        private readonly Action[] _avatarHandlers = new Action[SeatExtensions.Count];
        private readonly List<Card> _dealOrder = new List<Card>(Deck.HandSize);
        private readonly List<string> _emotePhrases = new List<string>(6);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private CardFlight _optimistic;
        private double _lastNow;
        private bool _started;
        private bool _leaving;
        private int _dealtCards;
        private int _lastTickSecond = -1;
        private bool _warned;
        private string _turnPrompt = string.Empty;

        public TablePresenter(
            TableView view,
            GameplayScreen gameplay,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
            IHaptics haptics,
            IToast toast,
            IClock clock,
            IDialogs dialogs,
            IAppFlow flow,
            CardSpriteProvider cards,
            AvatarLibrary avatars)
        {
            _view = view;
            _gameplay = gameplay;
            _table = table;
            _session = session;
            _router = router;
            _settings = settings;
            _localization = localization;
            _audio = audio;
            _haptics = haptics;
            _toast = toast;
            _clock = clock;
            _dialogs = dialogs;
            _flow = flow;
            _player = new MatchEventPlayer(this);
            _skin = new TableSkin(cards, avatars);
            _renderer = new TableRenderer(view, _skin, session, settings, localization);
        }

        /// <summary>The event queue, for tests and diagnostics.</summary>
        public MatchEventPlayer Player => _player;

        private TableState State => _table.State;

        private Seat Local => _session.LocalSeat;

        private bool ReduceMotion => _settings.ReduceMotion;

        public void Start()
        {
            _session.EventReceived += OnEvent;
            _session.Resynced += OnResynced;
            _session.SeatsChanged += OnSeatsChanged;
            _session.StatusChanged += OnStatusChanged;
            _session.EmoteReceived += OnEmoteReceived;
            _settings.Changed += OnSettingsChanged;
            _localization.LanguageChanged += OnLanguageChanged;
            _router.Changed += OnScreenChanged;
            _gameplay.BackRequested += OnHome;

            _view.HomeClicked += OnHome;
            _view.ScoresClicked += OnScores;
            _view.SettingsClicked += OnSettings;
            _view.EmoteClicked += OnEmoteButton;
            _view.SortClicked += OnSort;
            _view.TableTapped += OnTableTapped;
            _view.ImBackClicked += OnImBack;
            _view.AppPaused += OnAppPaused;
            _view.Hand.CardChosen += OnCardChosen;
            _view.Emotes.EmoteChosen += OnEmoteChosen;
            _view.Emotes.Dismissed += OnEmotesDismissed;
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                int captured = rel;
                _avatarHandlers[rel] = () => OnAvatarClicked(captured);
                _view.Seat(rel).AvatarClicked += _avatarHandlers[rel];
            }

            _lastNow = _clock.Now;
            _started = true;
            _router.ReduceMotion = ReduceMotion;
            if (_router.Has(ScreenId.Gameplay) && _router.Current == ScreenId.None)
            {
                _router.Show(ScreenId.Gameplay);
            }

            _audio.PlayMusic(MusicTrack.Table);
            _view.Emotes.Close();
            _view.SetReconnecting(_session.Status == SessionStatus.Reconnecting);
            RememberConnections();
            RenderAll(false);
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _lifetime.Cancel();
            _lifetime.Dispose();
            _player.Clear();
            _session.EventReceived -= OnEvent;
            _session.Resynced -= OnResynced;
            _session.SeatsChanged -= OnSeatsChanged;
            _session.StatusChanged -= OnStatusChanged;
            _session.EmoteReceived -= OnEmoteReceived;
            _settings.Changed -= OnSettingsChanged;
            _localization.LanguageChanged -= OnLanguageChanged;
            _router.Changed -= OnScreenChanged;
            _gameplay.BackRequested -= OnHome;

            _view.HomeClicked -= OnHome;
            _view.ScoresClicked -= OnScores;
            _view.SettingsClicked -= OnSettings;
            _view.EmoteClicked -= OnEmoteButton;
            _view.SortClicked -= OnSort;
            _view.TableTapped -= OnTableTapped;
            _view.ImBackClicked -= OnImBack;
            _view.AppPaused -= OnAppPaused;
            _view.Hand.CardChosen -= OnCardChosen;
            _view.Emotes.EmoteChosen -= OnEmoteChosen;
            _view.Emotes.Dismissed -= OnEmotesDismissed;
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                _view.Seat(rel).AvatarClicked -= _avatarHandlers[rel];
            }
        }

        public void Tick()
        {
            if (!_started)
            {
                return;
            }

            double now = _clock.Now;
            float deltaTime = Mathf.Clamp((float)(now - _lastNow), 0f, MaxFrameSeconds);
            _lastNow = now;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                OnBackKey();
            }

            _player.Tick(deltaTime);
            TickOptimisticPlay(deltaTime, now);
            TickClock(now);
        }

        // ------------------------------------------------------------------ session

        private void OnEvent(MatchEvent e)
        {
            _player.Enqueue(e);
        }

        private void OnResynced(PlayerView view)
        {
            // Resume, reconnect or a sequence gap: whatever was queued or flying no longer applies.
            _player.Clear();
            _optimistic = null;
            _playLock.Release();
            State.Reset(view);
            _view.Prompt.Hide();
            RenderAll(false);
            _table.NotifyRebuilt();
        }

        private void OnSeatsChanged()
        {
            AnnounceConnections();
            _renderer.RenderSeats(State);
            RenderAway();
        }

        /// <summary>GDD §8 "What others see": a returning player is welcomed back, a leaving one is announced.</summary>
        private void AnnounceConnections()
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                SeatConnection before = _connections[i];
                SeatConnection now = _session.Seats[i].Connection;
                _connections[i] = now;
                if (before == now)
                {
                    continue;
                }

                if (now == SeatConnection.Left)
                {
                    _toast.Show(_localization.Get(LeftTableToastKey));
                }
                else if (now == SeatConnection.Connected && before == SeatConnection.Reconnecting)
                {
                    _toast.Show(_localization.Get(WelcomeBackToastKey));
                }
            }
        }

        private void RememberConnections()
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i] = _session.Seats[i].Connection;
            }
        }

        /// <summary>Two timeouts mark the local seat Away and a bot plays for it until "I'm back" is tapped (GDD §8).</summary>
        private void RenderAway()
        {
            _view.SetAway(_session.Seats[(int)Local].Connection == SeatConnection.Away && _session.Status == SessionStatus.Running);
        }

        private void OnImBack()
        {
            // Only an online match can mark a seat Away; offline there is no clock.
            if (_session is OnlineMatchSession online)
            {
                _audio.Play(SoundCue.UiTap);
                online.ReportPresence();
            }
        }

        /// <summary>
        /// The app went to the background: the match menu opens, which pauses and saves an offline
        /// match (GDD §8); online the game goes on underneath it.
        /// </summary>
        private void OnAppPaused()
        {
            ScreenId top = _router.Current;
            if (_leaving || !_router.Has(ScreenId.MatchMenu) || (top != ScreenId.Gameplay && top != ScreenId.RoundResults))
            {
                return;
            }

            _view.Emotes.Close();
            _router.Push(ScreenId.MatchMenu);
        }

        private void OnStatusChanged()
        {
            SessionStatus status = _session.Status;
            _view.SetReconnecting(status == SessionStatus.Reconnecting);
            if (status == SessionStatus.Failed)
            {
                // The room is gone (the host left, or this player was removed).
                _toast.Show(_localization.Get(HostLeftToastKey));
                LeaveToMenu();
                return;
            }

            RenderAway();
            RenderHandOnly(true);
        }

        private void OnSettingsChanged()
        {
            _router.ReduceMotion = ReduceMotion;
        }

        private void OnLanguageChanged()
        {
            RenderAll(false);
        }

        /// <summary>
        /// Offline, the match waits while a menu, the scorecard or settings covers the table (GDD
        /// §11 screen 13); the round and match results are part of the match and do not pause it.
        /// </summary>
        private void OnScreenChanged(ScreenId changedTo)
        {
            if (!_session.CanPause)
            {
                return;
            }

            // Asked of the router, not the argument: a handler before this one may have moved on.
            ScreenId top = _router.Current;
            bool inMatch = top == ScreenId.Gameplay || top == ScreenId.RoundResults || top == ScreenId.MatchResults || top == ScreenId.None;
            if (inMatch)
            {
                _session.Resume();
            }
            else
            {
                _session.Pause();
            }
        }

        // ------------------------------------------------------------------ playback (IEventPlayback)

        IEventAnimation IEventPlayback.Begin(MatchEvent e)
        {
            switch (e)
            {
                case RoundStarted _:
                    AbandonOptimisticPlay();
                    State.Apply(e);
                    _emoteLimiter.ResetRound();
                    _view.Prompt.Hide();
                    return null;

                case Redealt _:
                    State.Apply(e);
                    _toast.Show(_localization.Get(RedealToastKey));
                    return null;

                case HandDealt dealt when dealt.Seat == Local:
                    // The round start has already cleared the table; the deal fills the fans and the hand.
                    State.Apply(e);
                    _audio.Play(SoundCue.CardShuffle);
                    return ReduceMotion ? null : CreateDeal();

                case CallPlaced placed:
                    State.Apply(e);
                    _audio.Play(SoundCue.CallConfirm);
                    _view.Seat(_renderer.RelativeSeat(placed.Seat)).ShowBubble(
                        placed.Call.ToString(System.Globalization.CultureInfo.InvariantCulture), BubbleHoldSeconds, ReduceMotion);
                    return new Beat(CallBubbleSeconds);

                case PlayTurnStarted turn:
                {
                    // The beat after the fourth call, before the first card of the round.
                    bool firstOfRound = State.Phase == TablePhase.Calling;
                    State.Apply(e);
                    if (!firstOfRound)
                    {
                        return null;
                    }

                    ShowPrompt(turn.Seat == Local ? TurnPrompt.YouLead(_localization) : TurnPrompt.WaitingFor(_renderer.NameOf(turn.Seat), _localization));
                    return new Beat(LeadBeatSeconds);
                }

                case CardPlayed played:
                    return BeginCardPlayed(played);

                case TrickCompleted completed:
                {
                    State.Apply(e);
                    int winner = _renderer.RelativeSeat(completed.Winner);
                    ShowPrompt(TurnPrompt.TrickWon(_renderer.NameOf(completed.Winner), completed.Winner == Local, _localization));
                    if (completed.Winner == Local)
                    {
                        _haptics.Play(HapticPattern.Light);
                    }

                    return TrickSweep.Create(_view.Trick, winner, _view.Seat(winner).Anchor, ReduceMotion, PlayCollectSound);
                }

                case RoundScored scored:
                    State.Apply(e);
                    _view.Prompt.Hide();
                    PlayRoundCues(scored);
                    return null;

                default:
                    State.Apply(e);
                    return null;
            }
        }

        void IEventPlayback.End(MatchEvent e)
        {
            RenderAll(true);
            switch (e)
            {
                case CardPlayed _:
                    _audio.Play(SoundCue.CardPlay);
                    break;

                case TrickCompleted completed:
                    // "Won +1" on the winner's pill.
                    _view.Seat(_renderer.RelativeSeat(completed.Winner)).PulseStatus(ReduceMotion);
                    break;

                case CallTurnStarted callTurn when callTurn.Seat == Local:
                case PlayTurnStarted playTurn when playTurn.Seat == Local:
                    _audio.Play(SoundCue.YourTurn);
                    _haptics.Play(HapticPattern.Light);
                    AnnounceTurnUnderOverlay();
                    break;
            }

            _table.NotifyPlayed(e);
        }

        private IEventAnimation BeginCardPlayed(CardPlayed played)
        {
            int rel = _renderer.RelativeSeat(played.Seat);
            bool reduce = ReduceMotion;
            float seconds = reduce ? CardFlight.ReducedSeconds : CardFlight.Seconds;
            if (rel == 0)
            {
                if (_playLock.IsLocked && _playLock.Pending == played.Card)
                {
                    // The player's own card: it has been flying since the tap. The queue takes the
                    // flight over where it is.
                    _playLock.Release();
                    State.Apply(played);
                    CardFlight flying = _optimistic;
                    _optimistic = null;
                    return flying != null && !flying.IsDone ? flying : null;
                }

                // Another card was played for this seat (the timer, or the forced last trick).
                RollBackOptimisticPlay(false);
                CardView own = _view.Hand.Release(played.Card, !reduce);
                State.Apply(played);
                if (own == null)
                {
                    own = _view.Cards.Rent(_view.Trick.Area);
                    own.ShowFace(played.Card, _skin.Face(_renderer.LocalDeck, played.Card));
                    own.Placement = _view.Trick.Slot(0);
                }

                _view.Trick.Adopt(0, own);
                return new CardFlight(own, _view.Trick.Slot(0), seconds);
            }

            // An opponent's card: one back leaves the fan and turns into the card that was played.
            OpponentHandView fan = _view.Seat(rel).Fan;
            CardView card = fan.Take();
            State.Apply(played);
            if (card == null)
            {
                card = _view.Cards.Rent(fan.Area);
                card.Placement = new CardPlacement(Vector2.zero, 0f, fan.CardScale, 0f);
            }

            card.ShowFace(played.Card, _skin.Face(_renderer.LocalDeck, played.Card));
            _view.Trick.Adopt(rel, card);
            return new CardFlight(card, _view.Trick.Slot(rel), seconds);
        }

        private IEventAnimation CreateDeal()
        {
            HandSorter.Sort(State.Hand, _settings.SortOrder, _dealOrder);
            var targets = new DealAnimation.Target[TableView.SeatCount];
            for (int rel = 0; rel < targets.Length; rel++)
            {
                SessionSeat seat = _session.Seats[(int)_renderer.SeatAt(rel)];
                targets[rel] = new DealAnimation.Target(_view.Seat(rel).Fan, _skin.Back(seat.DeckThemeId));
            }

            Direction direction = State.Rules.Direction;
            int first = _renderer.RelativeSeat(State.Dealer.Next(direction));
            int step = _renderer.RelativeSeat(Local.Next(direction));
            _dealtCards = 0;
            return new DealAnimation(
                _view.Cards,
                _view.Hand,
                _view.Seat(_renderer.RelativeSeat(State.Dealer)).Anchor,
                targets,
                first,
                step,
                _dealOrder,
                FaceOf,
                OnCardDealt);
        }

        private Sprite FaceOf(Card card) => _skin.Face(_renderer.LocalDeck, card);

        private void OnCardDealt()
        {
            if (_dealtCards++ % DealSoundEvery == 0)
            {
                _audio.Play(SoundCue.CardDeal);
            }
        }

        private void PlayCollectSound() => _audio.Play(SoundCue.TrickCollect);

        private void PlayRoundCues(RoundScored scored)
        {
            _audio.Play(SoundCue.RoundEnd);
            if (scored.Result[Local].Met)
            {
                _audio.Play(SoundCue.CallMet);
                _haptics.Play(HapticPattern.Success);
            }
            else
            {
                _audio.Play(SoundCue.CallMissed);
            }
        }

        // ------------------------------------------------------------------ rendering

        private void RenderAll(bool animateHand)
        {
            Card? pending = _playLock.IsLocked ? _playLock.Pending : (Card?)null;
            _renderer.Render(State, pending, CanTakeInput(), animateHand);
            RenderPrompt();
            RenderAway();
        }

        private void RenderHandOnly(bool animate)
        {
            Card? pending = _playLock.IsLocked ? _playLock.Pending : (Card?)null;
            _renderer.RenderHand(State, pending, CanTakeInput(), animate);
        }

        private bool CanTakeInput()
        {
            SessionStatus status = _session.Status;
            return !_playLock.IsLocked && !_leaving && (status == SessionStatus.Running || status == SessionStatus.Starting);
        }

        /// <summary>
        /// The prompt above the hand during play (GDD screen 08 "TURN PROMPT COPY"). While calling,
        /// the call panel and "Asha is calling…" speak instead.
        /// </summary>
        private void RenderPrompt()
        {
            TableState state = State;
            if (state.Phase != TablePhase.Playing)
            {
                _view.Prompt.Hide();
                return;
            }

            if (state.TryGetObligation(out PlayObligation obligation))
            {
                ShowPrompt(TurnPrompt.ForObligation(obligation, _localization));
            }
            else if (state.HasTurn)
            {
                ShowPrompt(TurnPrompt.WaitingFor(_renderer.NameOf(state.TurnSeat), _localization));
            }

            // Between turns the last line stays (the trick winner, or the lead beat).
        }

        private void ShowPrompt(string text)
        {
            _turnPrompt = text;
            _view.Prompt.Show(text);
        }

        /// <summary>Online, a turn can start while the scorecard or a menu covers the table: say so (GDD screens 13, 16).</summary>
        private void AnnounceTurnUnderOverlay()
        {
            ScreenId top = _router.Current;
            if (top == ScreenId.Gameplay || top == ScreenId.None)
            {
                return;
            }

            if (State.IsLocalPlayTurn && !string.IsNullOrEmpty(_turnPrompt))
            {
                _toast.Show(_turnPrompt);
            }
            else if (State.IsLocalCallTurn)
            {
                _toast.Show(_localization.Get(CallTitleKey));
            }
        }

        // ------------------------------------------------------------------ the hand

        private void OnCardChosen(Card card)
        {
            TableState state = State;
            double now = _clock.Now;
            if (!state.IsLocalPlayTurn || !CanTakeInput() || !_playLock.AcceptTap(now))
            {
                return;
            }

            if (!state.Legal.Contains(card))
            {
                // The prompt is the reason: it says what the rules ask for right now.
                if (!ReduceMotion)
                {
                    _view.Hand.Shake(card);
                }

                _view.Prompt.Refuse(ReduceMotion);
                _haptics.Play(HapticPattern.Warning);
                return;
            }

            CardView view = _view.Hand.Release(card, !ReduceMotion);
            if (view == null)
            {
                return;
            }

            // Optimistic: the card leaves at once, and nothing else can be played until this play
            // is settled.
            _playLock.Begin(card, now);
            _view.Trick.Adopt(0, view);
            _optimistic = new CardFlight(view, _view.Trick.Slot(0), ReduceMotion ? CardFlight.ReducedSeconds : CardFlight.Seconds);
            _optimistic.Begin();
            RenderHandOnly(true);
            _ = SendPlayAsync(card);
        }

        private async Task SendPlayAsync(Card card)
        {
            try
            {
                CommandResult result = await _session.PlayCardAsync(card, _lifetime.Token);
                if (!result.IsAccepted && _started && _playLock.IsLocked && _playLock.Pending == card)
                {
                    RollBackOptimisticPlay(true);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_started && _playLock.IsLocked && _playLock.Pending == card)
                {
                    RollBackOptimisticPlay(true);
                }
            }
        }

        private void TickOptimisticPlay(float deltaTime, double now)
        {
            if (_optimistic != null && _optimistic.Tick(deltaTime))
            {
                _optimistic = null;
            }

            if (!_playLock.IsLocked)
            {
                return;
            }

            Card pending = _playLock.Pending;
            if (_playLock.TryExpire(now))
            {
                // Two seconds without an answer: the card comes back and the player may try again.
                ReturnToHand(pending);
                RenderAll(true);
            }
        }

        /// <summary>Takes the optimistic play back (the session refused it, or another card was played for the seat).</summary>
        private void RollBackOptimisticPlay(bool render)
        {
            if (!_playLock.IsLocked)
            {
                return;
            }

            Card card = _playLock.Pending;
            _playLock.Release();
            ReturnToHand(card);
            if (render)
            {
                RenderAll(true);
            }
        }

        /// <summary>Moves the card that flew optimistically from the trick back into the hand; the next render slides it home.</summary>
        private void ReturnToHand(Card card)
        {
            _optimistic = null;
            CardView view = _view.Trick.CardAt(0);
            if (view != null && view.IsFaceUp && view.Card == card && State.Hand.Contains(card))
            {
                _view.Trick.Release(0);
                _view.Hand.Insert(view);
            }
        }

        /// <summary>A new round starts: nothing of the last one is still in flight.</summary>
        private void AbandonOptimisticPlay()
        {
            _optimistic = null;
            _playLock.Release();
        }

        private void OnSort()
        {
            _audio.Play(SoundCue.UiTap);
            _settings.SetSortOrder(HandSorter.Toggle(_settings.SortOrder));
            RenderHandOnly(true);
        }

        private void OnTableTapped()
        {
            if (_view.Emotes.IsOpen)
            {
                _view.Emotes.Close();
                return;
            }

            // A tap skips any animation longer than 0.3 s (GDD §6).
            _player.Skip();
        }

        // ------------------------------------------------------------------ turn clock

        private void TickClock(double now)
        {
            TurnTimerState timer = _session.Timer;
            TableState state = State;
            bool running = timer.IsRunning && timer.Duration > 0 && state.HasTurn && timer.Seat == state.TurnSeat;
            for (int rel = 0; rel < TableView.SeatCount; rel++)
            {
                Seat seat = _renderer.SeatAt(rel);
                SeatView seatView = _view.Seat(rel);
                if (!state.HasTurn || state.TurnSeat != seat)
                {
                    continue;
                }

                if (!running)
                {
                    // Offline there is no clock: the ring is whole and the seconds disc is hidden.
                    seatView.ClearTimer();
                    continue;
                }

                double remaining = Math.Max(0.0, timer.Deadline - now);
                bool warning = remaining <= WarningSeconds;
                int seconds = (int)Math.Ceiling(remaining);
                float pulse = ReduceMotion ? 0f : (float)(now - Math.Floor(now));
                seatView.SetTimer((float)(remaining / timer.Duration), seconds, warning, pulse);
                if (rel != 0)
                {
                    continue;
                }

                if (!warning)
                {
                    _warned = false;
                    _lastTickSecond = -1;
                }
                else if (seconds != _lastTickSecond)
                {
                    // The last five seconds: a soft tick each second and one nudge (GDD §8).
                    _lastTickSecond = seconds;
                    _audio.Play(SoundCue.TimerTick);
                    if (!_warned)
                    {
                        _warned = true;
                        _haptics.Play(HapticPattern.Warning);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ HUD, overlays, emotes

        private void OnHome()
        {
            if (_router.Has(ScreenId.MatchMenu) && _router.Current != ScreenId.MatchMenu)
            {
                _audio.Play(SoundCue.UiTap);
                _view.Emotes.Close();
                _router.Push(ScreenId.MatchMenu);
            }
        }

        private void OnScores()
        {
            if (_router.Has(ScreenId.Scorecard))
            {
                _audio.Play(SoundCue.UiTap);
                _view.Emotes.Close();
                _router.Push(ScreenId.Scorecard);
            }
        }

        private void OnSettings()
        {
            if (_router.Has(ScreenId.Settings))
            {
                _audio.Play(SoundCue.UiTap);
                _view.Emotes.Close();
                _router.Push(ScreenId.Settings);
            }
        }

        /// <summary>Android back (Escape): an open dialog first, then the emote picker, then the screen stack; on the bare table it opens the match menu.</summary>
        private void OnBackKey()
        {
            if (_dialogs.HandleBack())
            {
                return;
            }

            if (_view.Emotes.IsOpen)
            {
                _view.Emotes.Close();
                return;
            }

            _router.Back();
        }

        private void OnEmoteButton()
        {
            if (_view.Emotes.IsOpen)
            {
                _view.Emotes.Close();
                return;
            }

            _audio.Play(SoundCue.UiTap);
            _emotePhrases.Clear();
            for (int i = 0; i < EmoteKeys.Count; i++)
            {
                _emotePhrases.Add(_localization.Get(EmoteKeys[i]));
            }

            _view.Emotes.SetPhrases(_emotePhrases);
            _view.Emotes.SetAvailable(_emoteLimiter.CanSend(_clock.Now));
            _view.Emotes.Open();
        }

        private void OnEmotesDismissed() => _view.Emotes.Close();

        private void OnEmoteChosen(int emoteId)
        {
            _view.Emotes.Close();
            if (emoteId < 0 || emoteId >= EmoteKeys.Count || !_emoteLimiter.TrySend(_clock.Now))
            {
                return;
            }

            _ = SendEmoteAsync(emoteId);
        }

        private async Task SendEmoteAsync(int emoteId)
        {
            try
            {
                await _session.SendEmoteAsync(emoteId, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnEmoteReceived(Seat seat, int emoteId)
        {
            if (emoteId < 0 || emoteId >= EmoteKeys.Count || _muted[(int)seat])
            {
                return;
            }

            _view.Seat(_renderer.RelativeSeat(seat)).ShowBubble(_localization.Get(EmoteKeys[emoteId]), EmoteHoldSeconds, ReduceMotion);
        }

        /// <summary>Tapping an opponent mutes or unmutes their quick phrases for the match (GDD §8).</summary>
        private void OnAvatarClicked(int relativeSeat)
        {
            Seat seat = _renderer.SeatAt(relativeSeat);
            bool muted = !_muted[(int)seat];
            _muted[(int)seat] = muted;
            SeatView seatView = _view.Seat(relativeSeat);
            seatView.SetMuted(muted);
            if (muted)
            {
                seatView.HideBubble();
            }
        }

        private void LeaveToMenu()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            _ = GoToMenuAsync();
        }

        private async Task GoToMenuAsync()
        {
            try
            {
                await _flow.GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
            }
            catch (Exception e)
            {
                _leaving = false;
                Debug.LogException(e);
            }
        }
    }
}
