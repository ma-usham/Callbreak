using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
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
using Callbreak.Presentation.Screens;
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
    /// emotes and the way into the match menu, scorecard and settings, and pauses an offline
    /// match while one of those covers the table.
    /// While a page covers the table, nothing on the table answers a touch (<see cref="TableInput"/>).
    /// An event whose playback throws ends like a resync: the error is logged and the table is
    /// rebuilt from the session's view (GDD §17 principle 9).
    /// </summary>
    public sealed class TablePresenter : IStartable, ITickable, IDisposable, IEventPlayback
    {
        /// <summary>The Emotes table in display order; an emote id is an index into it (GDD §8).</summary>
        public static readonly IReadOnlyList<string> EmoteKeys = new[]
        {
            "emote.namaste", "emote.well_played", "emote.nice_call", "emote.oops", "emote.thinking", "emote.good_game",
        };

        private const string RedealToastKey = "ui.toast.redeal";
        private const string HostLostRedealToastKey = "ui.toast.redeal_host_lost";
        private const string HostLeftToastKey = "ui.toast.host_left";
        private const string WelcomeBackToastKey = "ui.toast.welcome_back";
        private const string AutoTagKey = "ui.tag.auto";
        private const string YourTurnBannerKey = "ui.banner.your_turn";
        private const string MainMenuLabelKey = "ui.match_results.main_menu";
        private const string CallTitleKey = "ui.bidding.title";
        private const float CallBeatSeconds = 0.3f;
        private const float BubbleHoldSeconds = 1.4f;
        private const float EmoteHoldSeconds = 2.4f;
        private const float LeadBeatSeconds = 0.4f;
        private const double WarningSeconds = 5.0;
        private const float MaxFrameSeconds = 0.25f;

        // A presenter that follows the table (results, scorecard, call panel) and fails must not
        // stall the table: its error is logged and the others still hear of the event.
        private static readonly Action<Exception> FollowerFailed = Debug.LogException;

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
        private readonly ICosmeticsShop _shop;

        private readonly MatchEventPlayer _player;
        private readonly TableSkin _skin;
        private readonly TableRenderer _renderer;
        private readonly PlayLock _playLock = new PlayLock();
        private readonly EmoteLimiter _emoteLimiter = new EmoteLimiter();
        private readonly SeatConnection[] _connections = new SeatConnection[SeatExtensions.Count];
        private readonly List<Card> _dealOrder = new List<Card>(Deck.HandSize);
        private readonly List<string> _emotePhrases = new List<string>(6);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        // Which shuffle each deal shows, and how it varies (ADR 039). Only the look: the cards dealt
        // come from the match, never from this.
        private readonly System.Random _shuffleRandom = new System.Random();

        private CardFlight _optimistic;
        private ShuffleStyle? _lastShuffle;
        private OpponentHandView[] _fans;
        private double _lastNow;
        private double _dealSoundAt = double.NaN;
        private bool _started;
        private bool _leaving;
        private int _lastTickSecond = -1;
        private bool _warned;

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
            AvatarLibrary avatars,
            ICosmeticsShop shop)
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
            _shop = shop;
            _player = new MatchEventPlayer(this);
            _player.Faulted += OnPlaybackFaulted;
            _skin = new TableSkin(cards, avatars, view.Backdrops);
            _renderer = new TableRenderer(view, _skin, session, settings, localization) { ShowTrickCounter = view.ShowTrickCounter };
        }

        /// <summary>The event queue, for tests and diagnostics.</summary>
        public MatchEventPlayer Player => _player;

        private TableState State => _table.State;

        /// <summary>Nothing covers the table: the only time anything on it answers a touch.</summary>
        private bool TableIsOpen => TableInput.IsOpen(_router.Current);

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
            _view.TableTapped += OnTableTapped;
            _view.ImBackClicked += OnImBack;
            _view.AppPaused += OnAppPaused;
            _view.Hand.CardChosen += OnCardChosen;
            _view.Emotes.EmoteChosen += OnEmoteChosen;
            _view.Emotes.Dismissed += OnEmotesDismissed;

            _lastNow = _clock.Now;
            _started = true;
            _router.ReduceMotion = ReduceMotion;
            _view.Hand.ReduceMotion = ReduceMotion;
            _view.ReduceMotion = ReduceMotion;
            if (_router.Has(ScreenId.Gameplay) && _router.Current == ScreenId.None)
            {
                _router.Show(ScreenId.Gameplay);
            }

            _audio.PlayMusic(MusicTrack.Table);
            _view.Emotes.Close();
            _view.SetReconnecting(_session.Status == SessionStatus.Reconnecting);
            ShowBackdrop();
            RememberConnections();
            RenderAll(false);
        }

        /// <summary>
        /// Behind everything: the background and the table this player has equipped in the shop, as
        /// they are when the table opens, so a change in the shop shows from the next match on. Each
        /// player sees their own, offline and online alike, as with decks (ADR 031); nothing about it
        /// goes over the network.
        /// </summary>
        private void ShowBackdrop()
        {
            if (_skin.HasBackdrops)
            {
                _view.SetBackdrop(_skin.Backdrop(_shop.Equipped(CosmeticKind.Background), _shop.Equipped(CosmeticKind.Table)));
            }
        }

        /// <summary>
        /// The scene's scope disposes this when the scene unloads, which is after Unity has destroyed
        /// the scene's objects: nothing here may touch a view. The event queue is dropped, not
        /// finished (its animations would settle cards that no longer exist), and only subscriptions
        /// are undone. It never throws: an exception here would leave every presenter disposed after
        /// this one still listening to the app-wide services.
        /// </summary>
        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            try
            {
                _player.Abandon();
                _optimistic = null;
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
                _view.TableTapped -= OnTableTapped;
                _view.ImBackClicked -= OnImBack;
                _view.AppPaused -= OnAppPaused;
                _view.Hand.CardChosen -= OnCardChosen;
                _view.Emotes.EmoteChosen -= OnEmoteChosen;
                _view.Emotes.Dismissed -= OnEmotesDismissed;

                // Last: a cancelled await continues at once, and must find this presenter stopped.
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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
            _view.SetEmoteCooldown(_emoteLimiter.Cooldown(now));
        }

        // ------------------------------------------------------------------ session

        private void OnEvent(MatchEvent e)
        {
            _player.Enqueue(e);
        }

        /// <summary>Resume, reconnect or a sequence gap: whatever was queued or flying no longer applies.</summary>
        private void OnResynced(PlayerView view)
        {
            Rebuild(view);
        }

        /// <summary>
        /// An event's playback threw. The player has already dropped its queue; everything the
        /// dropped events would have shown is in the session's view, so the table is rebuilt from
        /// it, exactly as after a resync (GDD §17 principle 9: a resync, not a stop).
        /// </summary>
        private void OnPlaybackFaulted(MatchEvent e, Exception error)
        {
            Debug.LogException(error);
            if (!_started)
            {
                return;
            }

            try
            {
                Rebuild(_session.View);
            }
            catch (Exception second)
            {
                Debug.LogException(second);
            }
        }

        private void Rebuild(PlayerView view)
        {
            try
            {
                _player.Clear();
            }
            catch (Exception e)
            {
                // The animation that was running could not be finished; the rebuild below corrects what it left.
                Debug.LogException(e);
            }

            _optimistic = null;
            _playLock.Release();
            State.Reset(view);
            _view.Prompt.Hide();
            RenderAll(false);
            _table.NotifyRebuilt(FollowerFailed);
        }

        private void OnSeatsChanged()
        {
            AnnounceConnections();
            _renderer.RenderSeats(State);
            RenderAway();
        }

        /// <summary>
        /// GDD §8 "What others see": a returning player is welcomed back. A player who leaves, the
        /// host included, goes without a word (the owner, 3 Oct 2026): a bot plays the seat on. So
        /// does one who drops in Quick Match, and comes back without a word (<see cref="SeatStatus.Shown"/>).
        /// </summary>
        private void AnnounceConnections()
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                SeatConnection before = _connections[i];
                SeatConnection now = SeatStatus.Shown(_session.Seats[i].Connection, _session.Mode);
                _connections[i] = now;
                if (before == now)
                {
                    continue;
                }

                if (now == SeatConnection.Connected && before == SeatConnection.Reconnecting)
                {
                    _toast.Show(_localization.Get(WelcomeBackToastKey));
                }
            }
        }

        private void RememberConnections()
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i] = SeatStatus.Shown(_session.Seats[i].Connection, _session.Mode);
            }
        }

        /// <summary>
        /// Two timeouts mark the local seat Away and a bot plays for it until the player is back
        /// (GDD §8): any touch on the table then says so, in place of the "I'm back" button.
        /// </summary>
        private void RenderAway()
        {
            _view.SetAway(_session.Seats[(int)Local].Connection == SeatConnection.Away && _session.Status == SessionStatus.Running);
        }

        private void OnImBack()
        {
            // Only an online match can mark a seat Away; offline there is no clock. (Not held back
            // under a page like the table's other controls: whatever the player touches, they are here.)
            if (_session is OnlineMatchSession online)
            {
                _audio.Play(SoundCue.UiTap);
                online.ReportPresence();
            }
        }

        /// <summary>
        /// The app went to the background. An offline match pauses itself (GDD §8 "Offline matches
        /// have no timer. The app auto-pauses when it goes to the background"): the match menu
        /// opens, which pauses and saves it. An online match cannot pause, so nothing opens: the
        /// player comes back to the table as it is by then.
        /// </summary>
        private void OnAppPaused()
        {
            ScreenId top = _router.Current;
            if (!_session.CanPause || _leaving || !_router.Has(ScreenId.MatchMenu) || (top != ScreenId.Gameplay && top != ScreenId.RoundResults))
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
                FailToMenu();
                return;
            }

            RenderAway();
            RenderHandOnly(true);
        }

        private void OnSettingsChanged()
        {
            _router.ReduceMotion = ReduceMotion;
            _view.Hand.ReduceMotion = ReduceMotion;
            _view.ReduceMotion = ReduceMotion;
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
            // Asked of the router, not the argument: a handler before this one may have moved on.
            ScreenId top = _router.Current;
            if (!TableInput.IsOpen(top))
            {
                // A page covers the table: the emote picker does not stay open under it.
                _view.Emotes.Close();
            }

            if (!_session.CanPause)
            {
                return;
            }

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

                case Redealt redealt:
                    State.Apply(e);

                    // ADR 030: the host and another player were lost at once, so the round was dealt again.
                    _toast.Show(_localization.Get(redealt.Reason == RedealReason.HostLost ? HostLostRedealToastKey : RedealToastKey));
                    return null;

                case HandDealt dealt when dealt.Seat == Local:
                    // The round start has already cleared the table; the deal fills the fans and the hand.
                    State.Apply(e);
                    if (!ReduceMotion)
                    {
                        return CreateDeal();
                    }

                    // Nothing flies: the dealt table is there at once and fades in (GDD §14).
                    _audio.Play(SoundCue.CardShuffle);
                    RenderAll(false);
                    return new DealFade(_view.Hand, Fans());

                case CallPlaced _:
                    // The call shows on the seat's pill ("0-3"); no bubble pops over the seat (the owner, 3 Oct 2026).
                    State.Apply(e);
                    _audio.Play(SoundCue.CallConfirm);
                    return new Beat(CallBeatSeconds);

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

                // No sound when the player's turn comes (ADR 041: the owner found it irritating);
                // the light buzz and the prompt above the hand say so.
                case CallTurnStarted callTurn when callTurn.Seat == Local:
                case PlayTurnStarted playTurn when playTurn.Seat == Local:
                    _haptics.Play(HapticPattern.Light);
                    AnnounceTurnUnderOverlay();
                    break;
            }

            _table.NotifyPlayed(e, FollowerFailed);
        }

        private IEventAnimation BeginCardPlayed(CardPlayed played)
        {
            int rel = _renderer.RelativeSeat(played.Seat);
            bool reduce = ReduceMotion;
            if (played.IsAuto && !played.IsForced)
            {
                // The timer played for the seat: the card lands as usual, marked "auto" (GDD §8).
                _view.Seat(rel).ShowBubble(_localization.Get(AutoTagKey), BubbleHoldSeconds, reduce);
            }

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
                    own.ShowFace(played.Card, _skin.Face(_renderer.LocalDeck, played.Card), _skin.Base(_renderer.LocalDeck));
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

            card.ShowFace(played.Card, _skin.Face(_renderer.LocalDeck, played.Card), _skin.Base(_renderer.LocalDeck));
            _view.Trick.Adopt(rel, card);
            return new CardFlight(card, _view.Trick.Slot(rel), seconds);
        }

        /// <summary>
        /// The shuffle in the middle of the table, then the deal from that deck (ADR 022): the
        /// scene's 52 cards lie there face down, are shuffled to the shuffle's sound in one of six
        /// ways, chosen at random and never the one before (ADR 039), and leave the top of the deck
        /// one at a time.
        /// </summary>
        private IEventAnimation CreateDeal()
        {
            HandSorter.Sort(State.Hand, _dealOrder);
            var targets = new DealAnimation.Target[TableView.SeatCount];
            for (int rel = 0; rel < targets.Length; rel++)
            {
                // Every card dealt is in this player's own deck, whoever it goes to (ADR 031).
                targets[rel] = new DealAnimation.Target(_view.Seat(rel).Fan, _skin.Back(_renderer.LocalDeck));
            }

            Direction direction = State.Rules.Direction;
            int first = _renderer.RelativeSeat(State.Dealer.Next(direction));
            int step = _renderer.RelativeSeat(Local.Next(direction));
            _dealSoundAt = double.NaN;
            var deck = new DeckPile(
                _view.Cards, _view.Trick.Area, _skin.Back(_renderer.LocalDeck), _dealOrder.Count * TableView.SeatCount, _view.Trick.Slot(0).Scale);
            var deal = new DealAnimation(_view.Cards, _view.Hand, deck, targets, first, step, _dealOrder, FaceOf, _skin.Base(_renderer.LocalDeck), OnCardDealt);
            ShuffleStyle style = ShuffleStyles.Next(_lastShuffle, _shuffleRandom);
            _lastShuffle = style;
            return new InOrder()
                .Then(ShuffleStyles.Create(style, deck, PlayShuffleSound, _shuffleRandom))
                .Then(deal);
        }

        private void PlayShuffleSound() => _audio.Play(SoundCue.CardShuffle);

        private Sprite FaceOf(Card card) => _skin.Face(_renderer.LocalDeck, card);

        /// <summary>The opponents' fans, by relative seat 1 to 3.</summary>
        private OpponentHandView[] Fans()
        {
            if (_fans == null)
            {
                _fans = new OpponentHandView[TableView.SeatCount - 1];
                for (int rel = 1; rel < TableView.SeatCount; rel++)
                {
                    _fans[rel - 1] = _view.Seat(rel).Fan;
                }
            }

            return _fans;
        }

        /// <summary>
        /// GDD §14 "card_deal: each card". Cards that leave the dealer in the same frame (a slow
        /// frame deals several) share one sound: two started at the same instant are one sound,
        /// only louder.
        /// </summary>
        private void OnCardDealt()
        {
            if (_dealSoundAt != _lastNow)
            {
                _dealSoundAt = _lastNow;
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
        /// The prompt above the hand (GDD screen 08 "TURN PROMPT COPY"). During play it names or
        /// explains the turn. While another seat calls it says "Asha is calling…", where the tips
        /// show (the owner, 6 Oct 2026, ADR 040: it stood in the middle of the table); on the local
        /// call turn the call panel speaks instead.
        /// </summary>
        private void RenderPrompt()
        {
            TableState state = State;
            if (state.Phase == TablePhase.Calling)
            {
                if (state.HasTurn && !state.IsLocalCallTurn)
                {
                    ShowPrompt(TurnPrompt.Calling(_renderer.NameOf(state.TurnSeat), _localization));
                }
                else
                {
                    _view.Prompt.Hide();
                }

                return;
            }

            if (state.Phase != TablePhase.Playing)
            {
                _view.Prompt.Hide();
                return;
            }

            if (state.TryGetObligation(out PlayObligation obligation))
            {
                ShowPrompt(PromptFor(obligation, state));
            }
            else if (state.HasTurn)
            {
                ShowPrompt(TurnPrompt.WaitingFor(_renderer.NameOf(state.TurnSeat), _localization));
            }

            // Between turns the last line stays (the trick winner, or the lead beat).
        }

        /// <summary>
        /// What the pill says on the local turn. In a player's first three matches it explains the
        /// rule behind the turn ("Tip: you must beat the highest heart if you can.") where there is
        /// one to explain (GDD §15); after that, and when leading, it names the turn.
        /// </summary>
        private string PromptFor(in PlayObligation obligation, TableState state)
        {
            if (TurnTips.AreOn(_settings.CompletedMatches))
            {
                string tip = TurnTips.ForObligation(obligation, state.Trick, _localization);
                if (tip != null)
                {
                    return tip;
                }
            }

            return TurnPrompt.ForObligation(obligation, _localization);
        }

        private void ShowPrompt(string text)
        {
            _view.Prompt.Show(text);
        }

        /// <summary>
        /// Online, a turn can start while the scorecard or a menu covers the table: say so (GDD
        /// screens 13, 16). The banner is a toast, whose font has no suit symbols, so it says only
        /// that it is the player's turn; the prompt on the table says what to play.
        /// </summary>
        private void AnnounceTurnUnderOverlay()
        {
            ScreenId top = _router.Current;
            if (top == ScreenId.Gameplay || top == ScreenId.None)
            {
                return;
            }

            if (State.IsLocalPlayTurn)
            {
                _toast.Show(_localization.Get(YourTurnBannerKey));
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
            if (!TableIsOpen || !state.IsLocalPlayTurn || !CanTakeInput() || !_playLock.AcceptTap(now))
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

        /// <summary>A tap on the table, the trick area included (the view sends both here).</summary>
        private void OnTableTapped()
        {
            if (!TableIsOpen)
            {
                return;
            }

            // A tap no longer skips an animation (the owner, 5 Oct 2026, replacing GDD §6's tap to
            // skip; ADR 038): the shuffle, the deal and a won trick's sweep always play out. Nor does
            // a tap on the trick area show the previous trick (the owner, the same day, replacing GDD
            // screen 08's peek). A tap on the table only closes the emote panel.
            if (_view.Emotes.IsOpen)
            {
                _view.Emotes.Close();
            }
        }

        // ------------------------------------------------------------------ turn clock

        private void TickClock(double now)
        {
            // While the table reconnects (its connection dropped, or the next host is taking the
            // match over, ADR 030) the clock waits where it stood: the host gives the turn a clock of
            // its own again, and nothing ticks or nudges under the "Reconnecting…" cover meanwhile.
            if (_session.Status == SessionStatus.Reconnecting)
            {
                _warned = false;
                _lastTickSecond = -1;
                return;
            }

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
            if (TableIsOpen && _router.Has(ScreenId.MatchMenu))
            {
                _audio.Play(SoundCue.UiTap);
                _view.Emotes.Close();
                _router.Push(ScreenId.MatchMenu);
            }
        }

        private void OnScores()
        {
            if (TableIsOpen && _router.Has(ScreenId.Scorecard))
            {
                _audio.Play(SoundCue.UiTap);
                _view.Emotes.Close();
                _router.Push(ScreenId.Scorecard);
            }
        }

        private void OnSettings()
        {
            if (TableIsOpen && _router.Has(ScreenId.Settings))
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
            if (!TableIsOpen)
            {
                return;
            }

            if (_view.Emotes.IsOpen)
            {
                _view.Emotes.Close();
                return;
            }

            // ADR 024: while the button shows the wait, it opens nothing.
            if (!_emoteLimiter.CanSend(_clock.Now))
            {
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
            if (!TableIsOpen || emoteId < 0 || emoteId >= EmoteKeys.Count || !_emoteLimiter.TrySend(_clock.Now))
            {
                return;
            }

            // The wait shows on the button from this frame on (ADR 024).
            _view.SetEmoteCooldown(_emoteLimiter.Cooldown(_clock.Now));
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

        /// <summary>
        /// A quick phrase, from any seat: nobody can be muted (the owner, 5 Oct 2026, ADR 038, in
        /// place of GDD §8's tap on an opponent to mute them), so every phrase shows, a bot's too.
        /// </summary>
        private void OnEmoteReceived(Seat seat, int emoteId)
        {
            if (emoteId < 0 || emoteId >= EmoteKeys.Count)
            {
                return;
            }

            // Every phrase, the player's own too, shows in the speech box just above its speaker's avatar (ADR 027).
            string phrase = _localization.Get(EmoteKeys[emoteId]);
            _view.Seat(_renderer.RelativeSeat(seat)).ShowChat(phrase, EmoteHoldSeconds, ReduceMotion);
        }

        /// <summary>
        /// The session failed: the room is gone (the host left, this player was removed, or the
        /// connection could not be restored). A dialog says why, and its one button leads to the
        /// main menu (GDD §27). A session that gives no reason gets the short notice instead.
        /// </summary>
        private void FailToMenu()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            RenderHandOnly(false);
            if (_session is OnlineMatchSession online)
            {
                _ = ExplainFailureAsync(_localization.Get(RoomMessages.KeyFor(online.FailureReason)));
            }
            else
            {
                _toast.Show(_localization.Get(HostLeftToastKey));
                _ = GoToMenuAsync();
            }
        }

        private async Task ExplainFailureAsync(string reason)
        {
            try
            {
                var request = new DialogRequest(reason, string.Empty, _localization.Get(MainMenuLabelKey), null);
                await _dialogs.ConfirmAsync(request, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The scene went away while the dialog was open.
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (!_started)
            {
                // Disposed while the dialog was open (it then closes unanswered): the scene is
                // already on its way out, and a second scene change must not follow it.
                return;
            }

            await GoToMenuAsync();
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
