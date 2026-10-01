using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Flow;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bootstrap;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Editor.Screens;
using Callbreak.Infrastructure.Persistence;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Overlays;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Plays offline matches through the table's real prefabs and presenters without Play mode, and
    /// checks what the player would see against the match as it goes. The prefabs are instantiated
    /// in a preview scene; the session is a real <see cref="OfflineMatchSession"/> with the real
    /// bots; time is a clock this tool advances, so the event playback (deal, flights, sweeps) runs
    /// to the frame; the "player" presses the same buttons and touches the same hand a finger
    /// would. Sounds, haptics, toasts, dialogs and scene changes are recorded instead of played.
    /// The views' own short tweens (the hand sliding into place, a screen fading in) do not run
    /// outside Play mode, so each is completed at the end of the frame that started it.
    /// A thin wrapper around the session lets it also stage what only an online match has (a turn
    /// clock, a reconnect, players going away, bots filling seats) and refuse a call or refuse or
    /// swallow a play; real online sessions fed by hand stage the round summary, the last round's
    /// countdown and a private room's match results. It also checks that every control answers to
    /// 88 px and that nothing on the table answers a touch while a page covers it.
    /// Run: Tools/Unity/ueval.sh 'return Callbreak.Editor.TableSimulation.Run();'
    /// </summary>
    public static class TableSimulation
    {
        public const string OutputFolder = "Temp/TableSimulation";
        public const string ReportPath = OutputFolder + "/report.txt";

        private const float Frame = 1f / 30f;
        private const string ScreensFolder = "Assets/_Project/Prefabs/UI/Screens";

        [MenuItem("Callbreak/Verify/Table Simulation")]
        private static void RunFromMenu() => Debug.Log(Run());

        /// <summary>
        /// Plays one full offline match and a second one that is left and resumed, and writes the
        /// list of checks to <see cref="ReportPath"/>.
        /// </summary>
        /// <param name="reduceMotion">Run with the Reduce motion setting on.</param>
        /// <param name="captures">Also render moments of the match to Temp/Captures/Sim_*.png (use with reduceMotion).</param>
        /// <returns>A one-line summary.</returns>
        public static string Run(bool reduceMotion = false, bool captures = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "not run: the Editor is in Play mode";
            }

            var runner = new Runner(reduceMotion, captures);
            try
            {
                runner.Execute();
            }
            catch (Exception e)
            {
                runner.Fail("no exception", e.ToString());
            }
            finally
            {
                runner.Dispose();
            }

            Directory.CreateDirectory(OutputFolder);
            string suffix = reduceMotion ? "-reduced" : string.Empty;
            string path = $"{OutputFolder}/report{suffix}.txt";
            File.WriteAllText(path, runner.Report());
            return runner.Summary() + " - " + path;
        }

        // ------------------------------------------------------------------ recorded services

        private sealed class ManualClock : IClock
        {
            public double Now { get; set; } = 100.0;
        }

        private sealed class Audio : IAudioService
        {
            public readonly Dictionary<SoundCue, int> Cues = new Dictionary<SoundCue, int>();
            public MusicTrack Music = MusicTrack.None;

            public int Count(SoundCue cue) => Cues.TryGetValue(cue, out int n) ? n : 0;

            public void Play(SoundCue cue) => Cues[cue] = Count(cue) + 1;

            public void PlayMusic(MusicTrack track) => Music = track;

            public bool Ducked;
            public int Ducks;

            public void Duck(bool ducked)
            {
                if (ducked && !Ducked)
                {
                    Ducks++;
                }

                Ducked = ducked;
            }
        }

        private sealed class Haptics : IHaptics
        {
            public readonly Dictionary<HapticPattern, int> Patterns = new Dictionary<HapticPattern, int>();

            public int Count(HapticPattern pattern) => Patterns.TryGetValue(pattern, out int n) ? n : 0;

            public void Play(HapticPattern pattern) => Patterns[pattern] = Count(pattern) + 1;
        }

        private sealed class Toasts : IToast
        {
            public readonly List<string> Shown = new List<string>();

            public void Show(string text) => Shown.Add(text);
        }

        private sealed class Dialogs : IDialogs
        {
            public readonly List<DialogRequest> Requests = new List<DialogRequest>();
            public bool Answer;

            public bool IsOpen => false;

            public Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct)
            {
                Requests.Add(request);
                return Task.FromResult(Answer);
            }

            public Task<DialogChoice> AskAsync(DialogRequest request, CancellationToken ct)
            {
                Requests.Add(request);
                return Task.FromResult(Answer ? DialogChoice.Confirm : DialogChoice.Cancel);
            }

            public bool HandleBack() => false;
        }

        /// <summary>The estimator's answer for the beginner assist: a fixed call, and how often it was asked.</summary>
        private sealed class Advisor : ICallAdvisor
        {
            public int Call = 3;
            public int Asked;

            public int SuggestCall(PlayerView view)
            {
                Asked++;
                return Call;
            }
        }

        /// <summary>The private room a match is played in, as the presenters see it: there or gone, hosted here or not.</summary>
        private sealed class Room : IRoomSession
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public event Action MatchStarting
            {
                add { }
                remove { }
            }

            public event Action<RoomError> Closed;

            public string Code => "SIM123";

            public RoomSettings Settings => null;

            public IReadOnlyList<RoomSeat> Seats => Array.Empty<RoomSeat>();

            public bool IsHost { get; set; }

            public int LocalSeatIndex => 0;

            public IMatchSession Match { get; set; }

            public bool HasListeners => Closed != null;

            public void Close(RoomError reason) => Closed?.Invoke(reason);

            public Task SetReadyAsync(bool ready, CancellationToken ct) => Task.CompletedTask;

            public Task<bool> StartAsync(bool fillWithBots, CancellationToken ct) => Task.FromResult(false);

            public Task KickAsync(int seatIndex, CancellationToken ct) => Task.CompletedTask;

            public Task LeaveAsync(CancellationToken ct) => Task.CompletedTask;

            public void Dispose()
            {
            }
        }

        private sealed class Rooms : IRoomService
        {
            public IRoomSession Current { get; set; }

            public Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct) => throw new NotSupportedException();

            public Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct) => throw new NotSupportedException();
        }

        private sealed class Flow : IAppFlow
        {
            public readonly List<MatchLaunchRequest> Started = new List<MatchLaunchRequest>();
            public readonly List<MenuEntry> Menus = new List<MenuEntry>();

            public event Action<AppState> StateChanged
            {
                add { }
                remove { }
            }

            public AppState State => AppState.InMatch;

            public AppState PreviousState => AppState.Menu;

            public MatchLaunchRequest CurrentLaunch { get; set; }

            public MenuEntry PendingMenuEntry => MenuEntry.MainMenu;

            public Task StartMatchAsync(MatchLaunchRequest request, CancellationToken ct)
            {
                Started.Add(request);
                return Task.CompletedTask;
            }

            public Task GoToMenuAsync(MenuEntry entry, CancellationToken ct)
            {
                Menus.Add(entry);
                return Task.CompletedTask;
            }
        }

        /// <summary>The session the presenters see: the real one, counted, and able to stage what offline play never does.</summary>
        private sealed class SessionProxy : IMatchSession
        {
            private readonly IMatchSession _inner;

            public readonly List<MatchEvent> Events = new List<MatchEvent>();
            public int PlayCalls;
            public int CallCalls;
            public int ReadyCalls;
            public int LastCall;
            public bool RejectNextPlay;
            public bool RejectNextCall;
            public bool SwallowNextPlay;
            public MatchMode? ModeOverride;
            public SessionStatus? StatusOverride;
            public TurnTimerState? TimerOverride;
            public SessionSeat[] SeatsOverride;

            public SessionProxy(IMatchSession inner)
            {
                _inner = inner;
                inner.EventReceived += OnEvent;
                inner.Resynced += OnResynced;
                inner.StatusChanged += OnStatus;
                inner.EmoteReceived += OnEmote;
            }

            public event Action<MatchEvent> EventReceived;

            public event Action<PlayerView> Resynced;

            public event Action SeatsChanged;

            public event Action StatusChanged;

            public event Action TimerChanged;

            public event Action<Seat, int> EmoteReceived;

            public MatchMode Mode => ModeOverride ?? _inner.Mode;

            public Seat LocalSeat => _inner.LocalSeat;

            public RuleSet Rules => _inner.Rules;

            public IReadOnlyList<SessionSeat> Seats => SeatsOverride ?? _inner.Seats;

            public PlayerView View => _inner.View;

            public SessionStatus Status => StatusOverride ?? _inner.Status;

            public TurnTimerState Timer => TimerOverride ?? _inner.Timer;

            public bool CanPause => _inner.CanPause;

            public bool IsPaused => _inner.IsPaused;

            public IMatchSession Inner => _inner;

            public void RaiseStatusChanged() => StatusChanged?.Invoke();

            public void RaiseSeatsChanged() => SeatsChanged?.Invoke();

            public void RaiseTimerChanged() => TimerChanged?.Invoke();

            public void RaiseEmote(Seat seat, int emote) => EmoteReceived?.Invoke(seat, emote);

            /// <summary>What a sequence gap does online: the table is told to rebuild from the current view.</summary>
            public void RaiseResynced() => Resynced?.Invoke(View);

            public void Start() => _inner.Start();

            public void Tick(double now) => _inner.Tick(now);

            public Task<CommandResult> PlaceCallAsync(int call, CancellationToken ct)
            {
                CallCalls++;
                LastCall = call;
                if (RejectNextCall)
                {
                    RejectNextCall = false;
                    return Task.FromResult(CommandResult.Rejected(RejectReason.StaleTurn));
                }

                return _inner.PlaceCallAsync(call, ct);
            }

            public Task<CommandResult> PlayCardAsync(Card card, CancellationToken ct)
            {
                PlayCalls++;
                if (RejectNextPlay)
                {
                    RejectNextPlay = false;
                    return Task.FromResult(CommandResult.Rejected(RejectReason.StaleTurn));
                }

                if (SwallowNextPlay)
                {
                    SwallowNextPlay = false;
                    return new TaskCompletionSource<CommandResult>().Task;
                }

                return _inner.PlayCardAsync(card, ct);
            }

            public Task ReadyNextAsync(CancellationToken ct)
            {
                ReadyCalls++;
                return _inner.ReadyNextAsync(ct);
            }

            public Task SendEmoteAsync(int emoteId, CancellationToken ct) => _inner.SendEmoteAsync(emoteId, ct);

            public void Pause() => _inner.Pause();

            public void Resume() => _inner.Resume();

            public Task LeaveAsync(CancellationToken ct) => _inner.LeaveAsync(ct);

            public void Dispose()
            {
                _inner.EventReceived -= OnEvent;
                _inner.Resynced -= OnResynced;
                _inner.StatusChanged -= OnStatus;
                _inner.EmoteReceived -= OnEmote;
                _inner.Dispose();
            }

            private void OnEvent(MatchEvent e)
            {
                Events.Add(e);
                EventReceived?.Invoke(e);
            }

            private void OnResynced(PlayerView view) => Resynced?.Invoke(view);

            private void OnStatus() => StatusChanged?.Invoke();

            private void OnEmote(Seat seat, int emote) => EmoteReceived?.Invoke(seat, emote);
        }

        // ------------------------------------------------------------------ the run

        private sealed class Runner : IDisposable
        {
            private readonly bool _reduceMotion;
            private readonly bool _captures;
            private readonly List<string> _order = new List<string>();
            private readonly Dictionary<string, int> _passes = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _fails = new Dictionary<string, int>();
            private readonly Dictionary<string, string> _firstFailure = new Dictionary<string, string>();
            private readonly List<string> _notes = new List<string>();
            private readonly HashSet<string> _done = new HashSet<string>();
            private readonly List<MonoBehaviour> _woken = new List<MonoBehaviour>();
            private readonly List<Card> _sorted = new List<Card>(13);

            // The scene.
            private Scene _scene;
            private bool _sceneOpen;
            private Camera _camera;
            private RenderTexture _target;
            private RectTransform _canvas;
            private TableView _table;
            private BiddingPanelView _bidding;
            private GameplayScreen _gameplay;
            private MatchMenuView _menu;
            private RoundResultsView _roundResults;
            private MatchResultsView _matchResults;
            private ScorecardView _scorecard;
            private readonly List<UIScreen> _screens = new List<UIScreen>();

            // Services that outlive a match.
            private readonly ManualClock _clock = new ManualClock();
            private readonly Audio _audio = new Audio();
            private readonly Haptics _haptics = new Haptics();
            private readonly Toasts _toasts = new Toasts();
            private readonly Dialogs _dialogs = new Dialogs();
            private readonly Flow _flow = new Flow();
            private readonly Advisor _advisor = new Advisor();
            private readonly Rooms _rooms = new Rooms();
            private ILocalization _text;
            private Callbreak.Application.Settings.SettingsService _settings;
            private ProfileService _profile;
            private CosmeticsService _cosmetics;
            private StatsService _stats;
            private OfflineMatchStore _matchStore;
            private CardSpriteProvider _cards;
            private AvatarLibrary _avatars;

            // One match.
            private SessionProxy _session;
            private IMatchSession _active;
            private PlayedTable _played;
            private ScreenRouter _router;
            private TablePresenter _tablePresenter;
            private BiddingPresenter _biddingPresenter;
            private MatchMenuPresenter _menuPresenter;
            private RoundResultsPresenter _roundPresenter;
            private MatchResultsPresenter _matchPresenter;
            private ScorecardPresenter _scorecardPresenter;
            private bool _matchRunning;

            // The player's script.
            private TurnId _calledTurn = TurnId.None;
            private int _intendedCall;
            private double _nextTapAt;
            private int _verifiedRound;
            private bool _matchResultsVerified;
            private bool _finished;
            private int _frames;
            private int _playedEvents;
            private bool _leaveMidway;
            private bool _stopAtRoundResults;
            private string _stage = "start";
            private string _expectedError;
            private int _assistAsks;
            private int _dealSounds;
            private double _dealBeganAt = -1.0;
            private PlayerView _lastSummaryView;
            private PlayerView _finalView;

            public Runner(bool reduceMotion, bool captures)
            {
                _reduceMotion = reduceMotion;
                _captures = captures;
            }

            private TableState State => _played.State;

            private Seat Local => _active.LocalSeat;

            // -------------------------------------------------------------- report

            public void Check(bool ok, string name, string detail = null)
            {
                if (!_passes.ContainsKey(name))
                {
                    _order.Add(name);
                    _passes[name] = 0;
                    _fails[name] = 0;
                }

                if (ok)
                {
                    _passes[name]++;
                    return;
                }

                _fails[name]++;
                if (!_firstFailure.ContainsKey(name))
                {
                    _firstFailure[name] = $"[{_stage}, frame {_frames}, t {_clock.Now:0.00}] {detail}";
                }
            }

            public void Fail(string name, string detail) => Check(false, name, detail);

            private void Equal<T>(T expected, T actual, string name)
            {
                Check(EqualityComparer<T>.Default.Equals(expected, actual), name, $"expected <{expected}> but was <{actual}>");
            }

            private void Note(string text) => _notes.Add(text);

            public string Summary()
            {
                int failed = 0, checks = 0;
                foreach (string name in _order)
                {
                    checks += _passes[name] + _fails[name];
                    if (_fails[name] > 0)
                    {
                        failed++;
                    }
                }

                return $"table simulation{(_reduceMotion ? " (reduce motion)" : string.Empty)}: {_order.Count} kinds of check, {checks} checks, {failed} kinds FAILED, {_frames} frames, {_playedEvents} events played";
            }

            public string Report()
            {
                var text = new StringBuilder();
                text.AppendLine(Summary());
                foreach (string note in _notes)
                {
                    text.AppendLine("note: " + note);
                }

                foreach (string name in _order)
                {
                    if (_fails[name] > 0)
                    {
                        text.AppendLine($"FAIL {name}  ({_fails[name]} of {_passes[name] + _fails[name]})  {_firstFailure[name]}");
                    }
                }

                foreach (string name in _order)
                {
                    if (_fails[name] == 0)
                    {
                        text.AppendLine($"ok   {name}  x{_passes[name]}");
                    }
                }

                return text.ToString();
            }

            // -------------------------------------------------------------- scene

            public void Execute()
            {
                UnityEngine.Application.logMessageReceived += OnLog;
                try
                {
                    Play();
                }
                finally
                {
                    UnityEngine.Application.logMessageReceived -= OnLog;
                }
            }

            /// <summary>Anything the game logs as an error during the run counts against it.</summary>
            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    if (_expectedError != null && condition.Contains(_expectedError))
                    {
                        // The one error a stage provoked on purpose.
                        _expectedError = null;
                        return;
                    }

                    string where = string.IsNullOrEmpty(stackTrace) ? string.Empty : " @ " + stackTrace.Split('\n')[0];
                    Fail("nothing is logged as an error", condition + where);
                }
            }

            private void Play()
            {
                BuildScene();
                BuildServices();

                // Match 1: played to the end.
                _stage = "match 1";
                StartMatch(false);
                Check(true, "nothing is logged as an error");
                VerifyStart();
                RunUntil(() => _finished, 3600.0, "match 1 finishes");
                StopMatch();

                // Match 2: left in the middle, then resumed from its save. The first-match tips are
                // over (three matches finished), so the prompt names cards and suits.
                Check(TurnTips.AreOn(_settings.CompletedMatches), "tips are still on after one finished match", "completed " + _settings.CompletedMatches);
                while (TurnTips.AreOn(_settings.CompletedMatches))
                {
                    _settings.IncrementCompletedMatches();
                }

                Equal(TurnTips.MatchesWithTips, _settings.CompletedMatches, "tips switch off with three finished matches");

                _stage = "match 2";
                _finished = false;
                _leaveMidway = true;
                _verifiedRound = 0;
                _matchResultsVerified = false;
                _calledTurn = TurnId.None;
                StartMatch(false);
                RunUntil(() => _finished, 900.0, "match 2 reaches the point where it is left");
                StopMatch();

                _stage = "match 2 resumed";
                _finished = false;
                _leaveMidway = false;
                _stopAtRoundResults = true;
                _calledTurn = TurnId.None;
                StartMatch(true);
                VerifyResumed();
                RunUntil(() => _finished, 900.0, "the resumed round is played out");
                Equal(ScreenId.RoundResults, _router.Current, "the resumed round ends on its results");
                Equal(SessionStatus.Running, _active.Status, "the resumed match is still running at the round results");
                StageOnlineStates();
                StageOnlineSession();
                StageOnlineLastRound();
                StageOnlineResults();
                StageTeardown();
            }

            private void BuildScene()
            {
                _scene = EditorSceneManager.NewPreviewScene();
                _sceneOpen = true;

                var cameraObject = new GameObject("SimCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, _scene);
                _camera = cameraObject.GetComponent<Camera>();
                _camera.scene = _scene;
                _camera.orthographic = true;
                _camera.orthographicSize = UiBuildKit.CanvasHeight / 2f;
                _camera.aspect = UiBuildKit.CanvasWidth / UiBuildKit.CanvasHeight;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 100f;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = UiBuildKit.Hex("#F7EFE5");

                var canvasObject = new GameObject("SimCanvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, _scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _camera;
                _canvas = (RectTransform)canvasObject.transform;
                _canvas.sizeDelta = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
                _canvas.position = Vector3.zero;

                _table = Instantiate<TableView>(ScreenLayoutBuilder.PrefabPath("Table"), _canvas);
                _bidding = Instantiate<BiddingPanelView>(ScreenLayoutBuilder.PrefabPath("BiddingPanel"), _canvas);

                var holder = new GameObject("Screens", typeof(RectTransform));
                var holderRect = (RectTransform)holder.transform;
                holderRect.SetParent(_canvas, false);
                holderRect.anchorMin = Vector2.zero;
                holderRect.anchorMax = Vector2.one;
                holderRect.offsetMin = holderRect.offsetMax = Vector2.zero;

                _gameplay = Instantiate<GameplayScreen>(TableCardBuilder.GameplayScreenPrefabPath, holderRect);
                _menu = Instantiate<MatchMenuView>($"{ScreensFolder}/MatchMenu.prefab", holderRect);
                _roundResults = Instantiate<RoundResultsView>($"{ScreensFolder}/RoundResults.prefab", holderRect);
                _matchResults = Instantiate<MatchResultsView>($"{ScreensFolder}/MatchResults.prefab", holderRect);
                _scorecard = Instantiate<ScorecardView>($"{ScreensFolder}/Scorecard.prefab", holderRect);
                foreach (UIScreen screen in new UIScreen[] { _gameplay, _menu, _roundResults, _matchResults, _scorecard })
                {
                    if (screen != null)
                    {
                        _screens.Add(screen);
                    }
                }

                Check(_table != null, "the Table prefab has a TableView");
                Check(_bidding != null, "the BiddingPanel prefab has a BiddingPanelView");
                Check(_gameplay != null, "the GameplayScreen prefab exists");
                Check(_menu != null, "the MatchMenu prefab has a MatchMenuView");
                if (_roundResults == null || _matchResults == null || _scorecard == null)
                {
                    Note("a results prefab is missing (RoundResults / MatchResults / Scorecard); its checks were skipped");
                }

                if (_gameplay != null)
                {
                    Equal(ScreenId.Gameplay, _gameplay.Id, "GameplayScreen has the Gameplay id");
                }

                if (_table != null)
                {
                    Equal(ScreenId.None, _table.Id, "the table is not a router screen");
                }

                if (_bidding != null)
                {
                    Equal(ScreenId.None, _bidding.Id, "the call panel is not a router screen");
                }

                Wake();
                Canvas.ForceUpdateCanvases();
                VerifyTouchTargets();
            }

            /// <summary>
            /// GDD §16 "Touch targets are ≥ 88 px": every control of the table, the call panel and
            /// the match menu answers to at least 88 canvas px each way (the cards in the hand are the
            /// GDD's one exception and are not buttons), and no two of them answer to the same point.
            /// The areas that catch every other touch (the table, the trick area, the emote picker's
            /// outside) lie under the controls and are left out of the second check.
            /// </summary>
            private void VerifyTouchTargets()
            {
                foreach (Component view in new Component[] { _table, _bidding, _menu })
                {
                    if (view == null)
                    {
                        continue;
                    }

                    var areas = new List<(string name, Rect area)>();
                    foreach (Button button in view.GetComponentsInChildren<Button>(true))
                    {
                        Graphic target = button.targetGraphic;
                        if (target == null)
                        {
                            Fail("every control has something to touch", button.name);
                            continue;
                        }

                        Rect rect = target.rectTransform.rect;
                        Vector4 padding = target.raycastPadding;
                        var area = new Rect(rect.xMin + padding.x, rect.yMin + padding.y, rect.width - padding.x - padding.z, rect.height - padding.y - padding.w);
                        Vector3 scale = target.rectTransform.lossyScale;
                        float width = area.width * scale.x, height = area.height * scale.y;
                        Check(width >= 87.99f && height >= 87.99f, "every control answers to at least 88 px (GDD 16)", $"{view.name}/{button.name}: {width:0.0} x {height:0.0}");
                        if (button.name == "TableTap" || button.name == "Tap" || button.name == "Dismiss")
                        {
                            continue;
                        }

                        Vector3 min = target.rectTransform.TransformPoint(area.min), max = target.rectTransform.TransformPoint(area.max);
                        areas.Add((button.name, Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y))));
                    }

                    for (int i = 0; i < areas.Count; i++)
                    {
                        for (int j = i + 1; j < areas.Count; j++)
                        {
                            Check(!areas[i].area.Overlaps(areas[j].area), "no two controls answer to the same touch", $"{view.name}: {areas[i].name} and {areas[j].name}");
                        }
                    }
                }
            }

            private T Instantiate<T>(string path, Transform parent) where T : Component
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Note("no prefab at " + path);
                    return null;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
                instance.transform.SetParent(parent, false);
                return instance.GetComponent<T>();
            }

            /// <summary>
            /// Outside Play mode Unity does not run Awake and OnEnable on these views, so their
            /// buttons would not be wired to their events: call them as the player loop would.
            /// </summary>
            private void Wake()
            {
                foreach (MonoBehaviour behaviour in _canvas.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!IsOurView(behaviour))
                    {
                        continue;
                    }

                    try
                    {
                        Invoke(behaviour, "Awake");
                        Invoke(behaviour, "OnEnable");
                        _woken.Add(behaviour);
                    }
                    catch (Exception e)
                    {
                        Exception cause = e.InnerException ?? e;
                        Note($"{behaviour.GetType().Name} on '{behaviour.name}' could not be enabled: {cause.GetType().Name}: {cause.Message}");
                    }
                }
            }

            /// <summary>The characters a label draws (labels filled with SetText keep their old text property).</summary>
            private static string Drawn(TMP_Text label)
            {
                label.ForceMeshUpdate(true, true);
                return label.GetParsedText();
            }

            private static bool IsOurView(MonoBehaviour behaviour)
            {
                if (behaviour == null)
                {
                    return false;
                }

                string ns = behaviour.GetType().Namespace ?? string.Empty;
                return ns == "Callbreak.Presentation.Table" || ns == "Callbreak.Presentation.Overlays"
                    || ns == "Callbreak.Presentation.Widgets" || ns == "Callbreak.Presentation.Screens";
            }

            private static void Invoke(object target, string method, params object[] args)
            {
                for (Type type = target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    MethodInfo info = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (info != null)
                    {
                        info.Invoke(target, args);
                        return;
                    }
                }
            }

            private static T Field<T>(object target, string name)
            {
                for (Type type = target.GetType(); type != null; type = type.BaseType)
                {
                    FieldInfo info = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (info != null)
                    {
                        return (T)info.GetValue(target);
                    }
                }

                throw new MissingFieldException(target.GetType().Name, name);
            }

            private void BuildServices()
            {
                string folder = $"{OutputFolder}/save-{DateTime.UtcNow.Ticks}";
                var store = new JsonSaveStore(folder);
                _settings = new Callbreak.Application.Settings.SettingsService(store);
                _settings.SetReduceMotion(_reduceMotion);
                _settings.SetDifficulty(BotDifficulty.Normal);
                _profile = new ProfileService(store, new NameFilter());
                _cosmetics = new CosmeticsService(store);
                _stats = new StatsService(store);
                _matchStore = new OfflineMatchStore(store);
                _text = EditorStrings.Load();
                _cards = TableCardBuilder.EnsureLibrary();
                _avatars = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(TablePreview.AvatarLibraryPath);
                if (_avatars == null)
                {
                    Note("no AvatarLibrary asset at " + TablePreview.AvatarLibraryPath + "; seats keep the prefab's avatar");
                }
            }

            private void StartMatch(bool resume)
            {
                var request = new MatchLaunchRequest(MatchMode.Offline, _settings.Difficulty, resume);
                _flow.CurrentLaunch = request;
                var offline = new OfflineMatchSession(request, _matchStore, new BotFactory(), _profile, _cosmetics, _settings, _stats);
                Equal(resume, offline.IsResumed, resume ? "the saved match is resumed" : "a new match is dealt");
                _session = new SessionProxy(offline);
                StartOn(_session);
                _session.Start();
            }

            /// <summary>The presenters of a Table scene around a session, started as the scene's scope starts them.</summary>
            private void StartOn(IMatchSession session)
            {
                _active = session;
                _played = new PlayedTable(session);
                _router = new ScreenRouter(_screens);

                _tablePresenter = new TablePresenter(
                    _table, _gameplay, _played, session, _router, _settings, _text, _audio, _haptics, _toasts, _clock, _dialogs, _flow, _cards, _avatars);
                _biddingPresenter = new BiddingPresenter(_bidding, _played, session, _router, _settings, _text, _audio, _advisor);
                _menuPresenter = new MatchMenuPresenter(_menu, _router, session, _settings, _text, _dialogs, _flow, _audio, _rooms);
                if (_roundResults != null)
                {
                    _roundPresenter = new RoundResultsPresenter(_roundResults, _avatars, _played, session, _router, _settings, _text, _audio, _clock);
                }

                if (_matchResults != null)
                {
                    _matchPresenter = new MatchResultsPresenter(
                        _matchResults, _avatars, _played, session, _router, _settings, _text, _audio, _haptics, _flow, _rooms, _toasts, _clock);
                }

                if (_scorecard != null)
                {
                    _scorecardPresenter = new ScorecardPresenter(_scorecard, _avatars, _played, session, _router, _text, _audio);
                }

                // As VContainer does: every IStartable, then the session (MatchSessionDriver.PostStart).
                _tablePresenter.Start();
                _biddingPresenter.Start();
                _menuPresenter.Start();
                _roundPresenter?.Start();
                _matchPresenter?.Start();
                _scorecardPresenter?.Start();
                _played.Played += VerifyPlayed;
                _matchRunning = true;
            }

            private void StopMatch()
            {
                if (!_matchRunning)
                {
                    return;
                }

                _matchRunning = false;
                _played.Played -= VerifyPlayed;
                _scorecardPresenter?.Dispose();
                _matchPresenter?.Dispose();
                _roundPresenter?.Dispose();
                _menuPresenter.Dispose();
                _biddingPresenter.Dispose();
                _tablePresenter.Dispose();
                _router.Dispose();
                _active.Dispose();
                _scorecardPresenter = null;
                _matchPresenter = null;
                _roundPresenter = null;

                // The same views serve the next match: empty the table as a new scene would find it.
                _table.Trick.Clear(_table.Cards);
                _table.Hand.Clear();
                for (int rel = 1; rel < TableView.SeatCount; rel++)
                {
                    _table.Seat(rel).Fan.Clear();
                    _table.Seat(rel).HideBubble();
                }

                _table.Seat(0).HideBubble();
                _table.Prompt.Hide();
                Equal(CardViewPool.DeckSize, _table.Cards.Free, "every card is back in the pool after a match");
            }

            public void Dispose()
            {
                try
                {
                    StopMatch();
                }
                catch (Exception e)
                {
                    Fail("teardown", e.ToString());
                }

                // Motions started by the views would otherwise outlive the scene.
                foreach (MonoBehaviour behaviour in _woken)
                {
                    if (behaviour != null)
                    {
                        try
                        {
                            Invoke(behaviour, "OnDisable");
                        }
                        catch (Exception e)
                        {
                            Fail("teardown", e.ToString());
                        }
                    }
                }

                _woken.Clear();
                if (_target != null)
                {
                    _camera.targetTexture = null;
                    _target.Release();
                    UnityEngine.Object.DestroyImmediate(_target);
                    _target = null;
                }

                if (_sceneOpen)
                {
                    _sceneOpen = false;
                    EditorSceneManager.ClosePreviewScene(_scene);
                }
            }

            // -------------------------------------------------------------- time

            private void Step()
            {
                _clock.Now += Frame;
                _frames++;
                _active.Tick(_clock.Now);
                _tablePresenter.Tick();
                _roundPresenter?.Tick();
                _matchPresenter?.Tick();
                SettleTweens();
                VerifyFrame();
            }

            /// <summary>
            /// Finishes the view tweens that only move things into place (hand arrangement, the
            /// prompt's flash, screen transitions): the Editor does not advance them while this
            /// tool runs. Seat bubbles are left alone; finishing theirs would hide them at once.
            /// </summary>
            private void SettleTweens()
            {
                Settle(_table.Hand, "_arrangeMotion");
                Settle(_table.Hand, "_shakeMotion");
                Settle(_table.Prompt, "_motion");
                Settle(_bidding, "_panelMotion");
                if (_router == null)
                {
                    return;
                }

                var transitions = new List<MotionHandle>(Field<Dictionary<UIScreen, MotionHandle>>(_router, "_motions").Values);
                foreach (MotionHandle transition in transitions)
                {
                    if (transition.IsActive())
                    {
                        transition.TryComplete();
                    }
                }
            }

            private static void Settle(object view, string field)
            {
                MotionHandle handle = Field<MotionHandle>(view, field);
                if (handle.IsActive())
                {
                    handle.TryComplete();
                }
            }

            private void Wait(double seconds)
            {
                double until = _clock.Now + seconds;
                while (_clock.Now < until)
                {
                    Step();
                }
            }

            private void RunUntil(Func<bool> done, double maxSeconds, string what)
            {
                double until = _clock.Now + maxSeconds;
                while (!done() && _clock.Now < until)
                {
                    Step();
                    Act();
                }

                Check(done(), what, $"not reached within {maxSeconds:0} simulated seconds; phase {State.Phase}, round {State.Round}, top {_router.Current}, backlog {_tablePresenter.Player.Backlog}");
            }

            // -------------------------------------------------------------- input

            private bool Click(Button button)
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                {
                    return false;
                }

                button.onClick.Invoke();
                SettleTweens();
                return true;
            }

            private static PointerEventData Pointer(Vector2 position)
            {
                return new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            }

            /// <summary>A point on the strip of a hand card that no other card covers.</summary>
            private static Vector2 PointOn(CardView card)
            {
                Rect rect = card.Rect.rect;
                return card.Rect.TransformPoint(new Vector3(rect.xMin + 14f, rect.yMin + rect.height * 0.75f, 0f));
            }

            private void Tap(CardView card)
            {
                Vector2 point = PointOn(card);
                _table.Hand.OnPointerDown(Pointer(point));
                _table.Hand.OnPointerUp(Pointer(point));
                SettleTweens();
            }

            private CardView HandCard(Card card) => _table.Hand.ViewOf(card);

            /// <summary>The face or back a card view shows.</summary>
            private static Sprite SpriteOf(CardView card) => Field<Image>(card, "_face").sprite;

            // -------------------------------------------------------------- the player

            private void Act()
            {
                if (_finished)
                {
                    return;
                }

                ScreenId top = _router.Current;
                TableState state = State;
                if (_stopAtRoundResults && top == ScreenId.RoundResults)
                {
                    _finished = true;
                    return;
                }

                switch (top)
                {
                    case ScreenId.Gameplay:
                        if (state.TricksCompleted >= 7 && _tablePresenter.Player.Current is CardPlayed && Once("resync"))
                        {
                            Resync();
                        }
                        else if (_bidding.IsPanelShown && state.IsLocalCallTurn)
                        {
                            MakeCall();
                        }
                        else if (state.IsLocalPlayTurn && _table.Hand.Interactable && _clock.Now >= _nextTapAt)
                        {
                            PlayTurn();
                        }
                        else if (state.Phase == TablePhase.Playing && !state.IsLocalPlayTurn && _tablePresenter.Player.IsIdle)
                        {
                            WhileWaiting();
                        }
                        else if (state.Phase == TablePhase.Playing && _tablePresenter.Player.Current is TrickCompleted)
                        {
                            SkipTheSweep();
                        }

                        break;

                    case ScreenId.RoundResults:
                        OnRoundResults();
                        break;

                    case ScreenId.MatchResults:
                        OnMatchResults();
                        break;
                }
            }

            private bool Once(string scenario)
            {
                return _done.Add(_stage + ":" + scenario);
            }

            private void MakeCall()
            {
                TableState state = State;
                if (_calledTurn == state.Turn)
                {
                    return;
                }

                _calledTurn = state.Turn;
                RuleSet rules = _active.Rules;
                Button confirm = Field<Button>(_bidding, "_confirmButton");
                TMP_Text confirmLabel = Field<TMP_Text>(_bidding, "_confirmLabel");
                BidChipRow row = Field<BidChipRow>(_bidding, "_chips");
                Button[] chips = Field<Button[]>(row, "_chips");
                TMP_Text[] labels = Field<TMP_Text[]>(row, "_labels");
                RectTransform selection = Field<RectTransform>(row, "_selection");

                // The panel as it opens (GDD screen 07 "Your turn").
                int offered = 0;
                for (int i = 0; i < chips.Length; i++)
                {
                    if (chips[i].gameObject.activeSelf)
                    {
                        Equal((rules.MinCall + i).ToString(CultureInfo.InvariantCulture), Drawn(labels[i]), "a chip shows its call");
                        offered++;
                    }
                }

                Equal(rules.MaxCall - rules.MinCall + 1, offered, "chips run from RuleSet.MinCall to RuleSet.MaxCall");
                Equal(0, _bidding.SelectedCall, "no chip is chosen when the panel opens");
                Check(!selection.gameObject.activeSelf, "the crimson chip is hidden until a chip is chosen");
                Equal(_text.Get("ui.bidding.confirm"), confirmLabel.text, "Confirm reads \"Confirm\" before a choice");
                Check(!confirm.interactable, "Confirm is disabled until a chip is chosen");
                int calls = _session.CallCalls;
                Check(!Click(confirm), "a disabled Confirm cannot be pressed");
                Equal(calls, _session.CallCalls, "no call is sent without a choice");
                Check(!_table.Hand.Interactable, "the hand takes no plays while calling");
                Snapshot("Bidding_open");

                // The beginner assist is off by default, and then the estimator is not even asked.
                Equal(0, _bidding.HintedCall, "without the beginner assist there is no dot");
                Equal(_assistAsks, _advisor.Asked, "with the assist off the estimator is never asked");
                if (Once("assist"))
                {
                    _settings.SetBeginnerAssist(true);
                    _assistAsks++;
                    Equal(_advisor.Call, _bidding.HintedCall, "with the assist on, a dot marks the suggested call");
                    RectTransform hint = Field<RectTransform>(_bidding, "_hint");
                    float chipX = ((RectTransform)chips[_advisor.Call - rules.MinCall].transform).anchoredPosition.x;
                    Check(Mathf.Abs(hint.anchoredPosition.x - chipX) < 0.01f, "the dot sits on the suggested chip", $"dot x {hint.anchoredPosition.x}, chip x {chipX}");
                    Equal(_assistAsks, _advisor.Asked, "the estimator is asked once for the turn");
                    Equal(0, _bidding.SelectedCall, "the dot chooses nothing");
                    Snapshot("Bidding_assist");
                    _settings.SetBeginnerAssist(false);
                    Equal(0, _bidding.HintedCall, "switching the assist off takes the dot away");
                }

                // A page over the table takes every touch; if one got through, the panel does not answer it.
                if (_scorecard != null && Once("covered panel"))
                {
                    Click(chips[1]);
                    int chosen = _bidding.SelectedCall;
                    _router.Push(ScreenId.Scorecard);
                    SettleTweens();
                    Click(chips[0]);
                    Equal(chosen, _bidding.SelectedCall, "a chip tapped under the scorecard is not chosen");
                    Click(confirm);
                    Equal(calls, _session.CallCalls, "Confirm pressed under the scorecard sends nothing");
                    _router.Back();
                    SettleTweens();
                    Equal(ScreenId.Gameplay, _router.Current, "the scorecard closes back to the call");
                }

                // Touching a card while calling only looks at it (GDD screen 07, callout 10).
                if (_table.Hand.Count > 0)
                {
                    CardView looked = HandCard(FirstOf(state.Hand));
                    int plays = _session.PlayCalls;
                    Vector2 point = PointOn(looked);
                    _table.Hand.OnPointerDown(Pointer(point));
                    Check(looked.Placement.Scale > 1.05f, "a card touched while calling is enlarged", "scale " + looked.Placement.Scale);
                    _table.Hand.OnPointerUp(Pointer(point));
                    Equal(1f, looked.Placement.Scale, "the enlarged card settles back on release");
                    Equal(plays, _session.PlayCalls, "a card cannot be played while calling");
                }

                // A first choice, then a change of mind.
                _intendedCall = SensibleCall(state.Hand, rules);
                int other = _intendedCall == rules.MinCall ? rules.MinCall + 1 : _intendedCall - 1;
                Check(Click(chips[other - rules.MinCall]), "a chip can be tapped");
                Equal(other, _bidding.SelectedCall, "the tapped chip is chosen");
                Check(selection.gameObject.activeSelf, "the crimson chip shows on the chosen chip");
                Equal(_text.Format("ui.bidding.confirm_n", other), confirmLabel.text, "Confirm reads \"Confirm {n}\"");
                Check(confirm.interactable, "Confirm is enabled once a chip is chosen");
                Click(chips[_intendedCall - rules.MinCall]);
                Equal(_intendedCall, _bidding.SelectedCall, "choosing another chip moves the choice");
                Equal(
                    ((RectTransform)chips[_intendedCall - rules.MinCall].transform).anchoredPosition,
                    selection.anchoredPosition,
                    "the crimson chip sits on the chosen chip");
                Equal(_text.Format("ui.bidding.confirm_n", _intendedCall), confirmLabel.text, "Confirm follows the choice");
                Snapshot("Bidding_chosen");

                // The session refuses the call: the panel takes input again and says why (GDD screen 07).
                if (Once("refused call"))
                {
                    _session.RejectNextCall = true;
                    Check(Click(confirm), "Confirm can be pressed");
                    Equal(calls + 1, _session.CallCalls, "the refused call was sent once");
                    Check(_bidding.IsPanelShown, "a refused call leaves the panel open");
                    Equal(_text.Get(CallRefusal.LateKey), _bidding.Reason, "a refused call says why on the panel");
                    Check(!Field<GameObject>(_bidding, "_subtitle").activeSelf, "the reason takes the subtitle's place");
                    Check(confirm.interactable && chips[0].interactable, "after a refusal the panel takes input again");
                    Drawable(Field<TMP_Text>(_bidding, "_reason"), "every character of the refusal line can be drawn");
                    Snapshot("Bidding_refused");
                    Click(chips[_intendedCall - rules.MinCall]);
                    Check(_bidding.Reason == null && Field<GameObject>(_bidding, "_subtitle").activeSelf, "choosing again brings the subtitle back");
                    calls = _session.CallCalls;
                }

                Check(Click(confirm), "Confirm can be pressed");
                Equal(calls + 1, _session.CallCalls, "Confirm sends one call");
                Equal(_intendedCall, _session.LastCall, "the call sent is the chosen chip");
                Click(confirm);
                Click(chips[0]);
                Equal(calls + 1, _session.CallCalls, "a confirmed call is final");
            }

            private static int SensibleCall(CardSet hand, RuleSet rules)
            {
                int tricks = 0;
                foreach (Card card in hand)
                {
                    if (card.Rank >= Rank.King || (card.IsTrump && card.Rank >= Rank.Ten))
                    {
                        tricks++;
                    }
                }

                return Mathf.Clamp(tricks, rules.MinCall, rules.MaxCall);
            }

            private static Card FirstOf(CardSet set)
            {
                foreach (Card card in set)
                {
                    return card;
                }

                throw new InvalidOperationException("empty set");
            }

            private void PlayTurn()
            {
                TableState state = State;
                HandView hand = _table.Hand;
                CardSet legal = state.Legal;
                CardSet illegal = state.Hand - legal;
                Check(!legal.IsEmpty, "a play turn always has a legal card");
                if (legal.IsEmpty)
                {
                    return;
                }

                if (_captures && Once("capture turn"))
                {
                    Snapshot("Table_your_turn");
                }

                if (_captures && _table.Prompt.Text.StartsWith("Tip", StringComparison.Ordinal))
                {
                    Snapshot("Table_tip");
                }

                // The last five seconds of a turn clock (online only; staged through the session).
                if (Once("turn clock"))
                {
                    StageTurnClock();
                    return;
                }

                if (Once("prompts"))
                {
                    StagePrompts();
                }

                if (_scorecard != null && Once("covered table"))
                {
                    StageCoveredTable(FirstOf(legal));
                    return;
                }

                // An illegal tap: refused and explained, nothing is sent (GDD screen 08, callout 9).
                if (!illegal.IsEmpty && Once("illegal tap"))
                {
                    int plays = _session.PlayCalls, warnings = _haptics.Count(HapticPattern.Warning);
                    int held = hand.Count;
                    Tap(HandCard(FirstOf(illegal)));
                    Equal(plays, _session.PlayCalls, "an illegal card is never sent");
                    Equal(held, hand.Count, "an illegal card stays in the hand");
                    Equal(warnings + 1, _haptics.Count(HapticPattern.Warning), "an illegal tap gives a warning nudge");
                    Check(_table.Prompt.IsShowing, "the prompt that explains the refusal is showing");
                    Check(_table.Trick.CardAt(0) == null, "an illegal card does not reach the trick");
                    _nextTapAt = _clock.Now + 0.3;
                    return;
                }

                Card card = FirstOf(legal);
                CardView view = HandCard(card);
                Check(view != null, "every legal card has a view in the hand");
                if (view == null)
                {
                    return;
                }

                // The session refuses the play: the card comes back and the player may try again.
                if (Once("refused play"))
                {
                    int plays = _session.PlayCalls, held = hand.Count;
                    _session.RejectNextPlay = true;
                    Tap(view);
                    Equal(plays + 1, _session.PlayCalls, "the refused play was sent once");
                    Equal(held, hand.Count, "a refused card is back in the hand");
                    Check(_table.Trick.CardAt(0) == null, "a refused card leaves the trick");
                    Check(hand.Interactable, "the hand takes input again after a refusal");
                    Check(HandCard(card) != null && HandCard(card).IsGlowing, "the refused card is a legal card in the hand again");
                    _nextTapAt = _clock.Now + 0.3;
                    return;
                }

                // No answer for two seconds: the card rolls back (GDD §23 input lock).
                if (Once("unanswered play"))
                {
                    int plays = _session.PlayCalls, held = hand.Count;
                    _session.SwallowNextPlay = true;
                    Tap(view);
                    Equal(plays + 1, _session.PlayCalls, "the unanswered play was sent once");
                    Equal(held - 1, hand.Count, "the card left the hand at once");
                    Check(!hand.Interactable, "input is locked while a play is unanswered");
                    Wait(1.0);
                    CardSet others = state.Hand - CardSet.Of(card);
                    if (!others.IsEmpty)
                    {
                        Tap(HandCard(FirstOf(others)));
                    }

                    Equal(plays + 1, _session.PlayCalls, "nothing else can be played while locked");
                    Check(_table.Trick.CardAt(0) != null, "the card waits at the trick while unanswered");
                    Wait(PlayLock.TimeoutSeconds - 1.0 + 0.2);
                    Equal(held, hand.Count, "after two seconds without an answer the card is back in the hand");
                    Check(_table.Trick.CardAt(0) == null, "after two seconds the trick slot is free again");
                    Check(hand.Interactable, "the hand takes input again after the roll-back");
                    _nextTapAt = _clock.Now + 0.3;
                    return;
                }

                int before = _session.PlayCalls;
                int count = hand.Count;
                if (Once("flick"))
                {
                    // Drag a legal card up and let go above the hand.
                    Vector2 start = PointOn(view);
                    hand.OnPointerDown(Pointer(start));
                    hand.OnDrag(Pointer(start + new Vector2(0f, 60f)));
                    hand.OnPointerUp(Pointer(start + new Vector2(0f, 420f)));
                    SettleTweens();
                    Equal(before + 1, _session.PlayCalls, "dragging a legal card up plays it");
                }
                else if (legal.Count > 1 && Once("slide"))
                {
                    // Tap-and-slide: press one card, slide to another, release there.
                    Card second = default;
                    int index = 0;
                    foreach (Card candidate in legal)
                    {
                        if (index++ == 1)
                        {
                            second = candidate;
                        }
                    }

                    hand.OnPointerDown(Pointer(PointOn(view)));
                    hand.OnDrag(Pointer(PointOn(HandCard(second))));
                    hand.OnPointerUp(Pointer(PointOn(HandCard(second))));
                    SettleTweens();
                    card = second;
                    view = _table.Trick.CardAt(0);
                    Equal(before + 1, _session.PlayCalls, "sliding to a card and releasing plays that card");
                }
                else
                {
                    Tap(view);
                    Tap(view);
                    Equal(before + 1, _session.PlayCalls, "a double tap sends one card");
                }

                CardView flying = _table.Trick.CardAt(0);
                Check(flying != null && flying.IsFaceUp && flying.Card == card, "the chosen card is on its way to the trick at once", "card " + card);
                Equal(count - 1, hand.Count, "the chosen card has left the hand");
                Check(!hand.Interactable, "the hand is locked until the play is settled");
                _nextTapAt = _clock.Now + 0.2;
            }

            /// <summary>
            /// The scorecard over the local turn (online the match runs on under it, GDD §11 screen 16).
            /// The page takes every touch; the table's own handlers are the second lock: a touch that
            /// got through plays no card, flips no sort order, opens no emotes, mutes nobody, peeks at
            /// nothing and opens nothing from the HUD.
            /// </summary>
            private void StageCoveredTable(Card legal)
            {
                int plays = _session.PlayCalls, held = _table.Hand.Count;
                HandSortOrder order = _settings.SortOrder;
                _router.Push(ScreenId.Scorecard);
                SettleTweens();
                Equal(ScreenId.Scorecard, _router.Current, "the scorecard opens over the local turn");
                Tap(HandCard(legal));
                Equal(plays, _session.PlayCalls, "a card tapped under the scorecard is not played");
                Equal(held, _table.Hand.Count, "a card tapped under the scorecard stays in the hand");
                Click(Field<Button>(_table, "_sortButton"));
                Equal(order, _settings.SortOrder, "the sort button under the scorecard does nothing");
                Click(Field<Button>(_table, "_emoteButton"));
                Check(!_table.Emotes.IsOpen, "the emote button under the scorecard does nothing");
                Click(Field<Button>(_table, "_trickTap"));
                Check(!_table.Trick.IsPeeking, "the trick area under the scorecard shows no peek");
                SeatView opponent = _table.Seat(1);
                Click(Field<Button>(opponent, "_avatarButton"));
                Check(!Field<bool>(opponent, "_muted"), "an avatar tapped under the scorecard is not muted");
                Click(Field<Button>(_table, "_homeButton"));
                Click(Field<Button>(_table, "_settingsButton"));
                Equal(ScreenId.Scorecard, _router.Current, "the HUD under the scorecard opens nothing");
                Equal(2, _router.Depth, "nothing was pushed over the scorecard");
                Snapshot("Table_covered");
                _router.Back();
                SettleTweens();
                Equal(ScreenId.Gameplay, _router.Current, "the scorecard closes back to the table");
                Check(_table.Hand.Interactable, "back on the table, the hand takes input again");
                _nextTapAt = _clock.Now + 0.2;
            }

            private void StageTurnClock()
            {
                SeatView you = _table.Seat(0);
                Image ring = Field<Image>(you, "_timerRing");
                GameObject disc = Field<GameObject>(you, "_timerDisc");
                TMP_Text seconds = Field<TMP_Text>(you, "_timerText");
                Check(ring.enabled, "the local ring shows on the local turn");
                Check(!disc.activeSelf, "offline there is no seconds disc");
                Equal(1f, ring.fillAmount, "offline the ring is whole");

                int ticks = _audio.Count(SoundCue.TimerTick), warnings = _haptics.Count(HapticPattern.Warning);
                _session.TimerOverride = new TurnTimerState(Local, _clock.Now + 12.0, 15.0, true);
                Step();
                Check(disc.activeSelf, "a running clock shows the seconds disc");
                Equal("12", Drawn(seconds), "the seconds left are shown as text");
                Check(Mathf.Abs(ring.fillAmount - (float)((12.0 - Frame) / 15.0)) < 0.01f, "the ring shows the time left", "fill " + ring.fillAmount);
                Color gold = ring.color;
                Equal(ticks, _audio.Count(SoundCue.TimerTick), "no tick before the last five seconds");

                _session.TimerOverride = new TurnTimerState(Local, _clock.Now + 4.5, 15.0, true);
                Wait(1.2);
                Check(ring.color != gold, "the ring turns crimson for the last five seconds");
                Check(_audio.Count(SoundCue.TimerTick) - ticks >= 2, "a tick plays each second of the last five", "ticks " + (_audio.Count(SoundCue.TimerTick) - ticks));
                Equal(warnings + 1, _haptics.Count(HapticPattern.Warning), "one warning nudge in the last five seconds");
                Snapshot("Table_timer_warning");

                _session.TimerOverride = null;
                Step();
                Check(!disc.activeSelf, "the seconds disc goes when the clock stops");
                Equal(1f, ring.fillAmount, "the ring is whole again without a clock");
                Check(ring.color == gold, "the ring is gold again without a clock");
                _nextTapAt = _clock.Now + 0.2;
            }

            /// <summary>Things a player does while an opponent thinks.</summary>
            private void WhileWaiting()
            {
                TableState state = State;
                if (state.TricksCompleted >= 1 && Once("peek"))
                {
                    Peek();
                }
                else if (state.TricksCompleted >= 2 && Once("menu"))
                {
                    MatchMenu();
                }
                else if (state.TricksCompleted >= 3 && Once("emotes"))
                {
                    Emotes();
                }
                else if (state.TricksCompleted >= 4 && Once("sort"))
                {
                    Sort();
                }
                else if (state.TricksCompleted >= 5 && Once("app pause"))
                {
                    AppPause();
                }
                else if (_leaveMidway && state.TricksCompleted >= 6 && Once("leave"))
                {
                    Leave();
                }
            }

            private void Peek()
            {
                Button tap = Field<Button>(_table, "_trickTap");
                TrickAreaView trick = _table.Trick;
                int rented = _table.Cards.Created - _table.Cards.Free;
                int lying = trick.Count;
                Check(Click(tap), "the trick area can be tapped");
                Check(trick.IsPeeking, "a tap on the trick area shows the previous trick");
                Equal(rented + 4, _table.Cards.Created - _table.Cards.Free, "the peek shows four cards");
                Equal(lying, trick.Count, "the peek does not change the current trick");
                Snapshot("Table_peek");
                Click(tap);
                Check(!trick.IsPeeking, "a second tap ends the peek");
                Equal(rented, _table.Cards.Created - _table.Cards.Free, "the peeked cards go back to the pool");

                // Left open, the peek ends when the next card is played.
                Click(tap);
                Check(trick.IsPeeking, "the peek can be opened again");
            }

            private void MatchMenu()
            {
                Button home = Field<Button>(_table, "_homeButton");
                Check(Click(home), "Home can be pressed");
                Equal(ScreenId.MatchMenu, _router.Current, "Home opens the match menu");
                Check(_active.IsPaused, "offline, the match pauses under the menu");
                Equal(_text.Get("ui.match_menu.mode_offline"), Field<TMP_Text>(_menu, "_modeLabel").text, "the menu says \"Offline match · Paused\"");
                Check(Field<GameObject>(_menu, "_pausedNote").activeSelf, "\"Your match is paused.\" shows offline");
                Snapshot("MatchMenu");

                int events = _session.Events.Count;
                Wait(4.0);
                Equal(events, _session.Events.Count, "nothing happens in a paused match");

                ToggleSwitch sound = Field<ToggleSwitch>(_menu, "_soundToggle");
                Check(sound.IsOn, "the Sound switch starts on");
                Click(Field<Button>(sound, "_button"));
                Check(!_settings.Music && !_settings.SoundEffects, "the Sound switch turns music and effects off");
                Click(Field<Button>(sound, "_button"));
                Check(_settings.Music && _settings.SoundEffects, "the Sound switch turns music and effects on");

                if (_scorecard != null)
                {
                    Click(Field<Button>(_menu, "_scorecardButton"));
                    Equal(ScreenId.Scorecard, _router.Current, "Scorecard opens from the menu");
                    Check(_active.IsPaused, "the match stays paused under the scorecard");
                    VerifyScorecard();
                    Click(Field<Button>(_scorecard, "_primaryButton"));
                    Equal(ScreenId.MatchMenu, _router.Current, "the scorecard closes back to the menu");
                }

                Click(Field<Button>(_menu, "_resumeButton"));
                Equal(ScreenId.Gameplay, _router.Current, "Resume Game returns to the table");
                Check(!_active.IsPaused, "the match runs again after Resume");

                // Android back: on the bare table it opens the menu; on the menu it closes it.
                _router.Back();
                Equal(ScreenId.MatchMenu, _router.Current, "Android back on the table opens the match menu");
                Check(_active.IsPaused, "the match pauses for Android back too");
                Click(Field<Button>(_menu, "_closeButton"));
                Equal(ScreenId.Gameplay, _router.Current, "the close button returns to the table");
                Check(!_active.IsPaused, "the match runs again after closing the menu");
            }

            private void Emotes()
            {
                EmotePickerView picker = _table.Emotes;
                Button emote = Field<Button>(_table, "_emoteButton");
                Button[] phrases = Field<Button[]>(picker, "_buttons");
                TMP_Text[] labels = Field<TMP_Text[]>(picker, "_labels");
                Click(emote);
                Check(picker.IsOpen, "the emote button opens the picker");
                Equal(6, phrases.Length, "the picker has six phrases");
                for (int i = 0; i < labels.Length && i < TablePresenter.EmoteKeys.Count; i++)
                {
                    Equal(_text.Get(TablePresenter.EmoteKeys[i]), labels[i].text, "a phrase comes from the Emotes table");
                }

                Snapshot("Table_emotes");
                Check(Click(phrases[2]), "a phrase can be tapped");
                Check(!picker.IsOpen, "choosing a phrase closes the picker");
                SeatView you = _table.Seat(0);
                Check(Field<RectTransform>(you, "_bubble").gameObject.activeSelf, "the phrase shows in a bubble over the seat");
                Equal(_text.Get(TablePresenter.EmoteKeys[2]), Field<TMP_Text>(you, "_bubbleText").text, "the bubble shows the chosen phrase");

                Click(emote);
                Check(picker.IsOpen && !phrases[0].interactable, "a second phrase within three seconds is held back");
                Click(Field<Button>(picker, "_dismiss"));
                Check(!picker.IsOpen, "a tap outside closes the picker");

                // Muting an opponent (GDD §8): tap the avatar.
                SeatView opponent = _table.Seat(2);
                Seat seat = (Seat)(((int)Local + 2) % SeatExtensions.Count);
                GameObject bubble = Field<RectTransform>(opponent, "_bubble").gameObject;
                opponent.HideBubble();
                Click(Field<Button>(opponent, "_avatarButton"));
                _session.RaiseEmote(seat, 0);
                Check(!bubble.activeSelf, "a muted opponent's phrase is not shown");
                Click(Field<Button>(opponent, "_avatarButton"));
                _session.RaiseEmote(seat, 0);
                Check(bubble.activeSelf, "an unmuted opponent's phrase is shown");
                opponent.HideBubble();
                you.HideBubble();
            }

            private void Sort()
            {
                HandSortOrder before = _settings.SortOrder;
                Click(Field<Button>(_table, "_sortButton"));
                Check(_settings.SortOrder != before, "the sort button toggles the saved order");
                HandSorter.Sort(State.Hand, _settings.SortOrder, _sorted);
                bool inOrder = _sorted.Count == _table.Hand.Count;
                for (int i = 0; inOrder && i < _sorted.Count; i++)
                {
                    CardView view = HandCard(_sorted[i]);
                    inOrder = view != null && view.Rect.GetSiblingIndex() == i;
                }

                Check(inOrder, "the hand is laid out in the chosen order");
            }

            private void AppPause()
            {
                Invoke(_table, "OnApplicationPause", true);
                Equal(ScreenId.MatchMenu, _router.Current, "going to the background opens the match menu");
                Check(_active.IsPaused, "going to the background pauses an offline match");
                Check(_matchStore.HasSave, "pausing saves the offline match");
                Click(Field<Button>(_menu, "_resumeButton"));
                Check(!_active.IsPaused, "the match runs again after coming back");
            }

            private void Leave()
            {
                Click(Field<Button>(_table, "_homeButton"));
                _dialogs.Answer = false;
                int asked = _dialogs.Requests.Count;
                Click(Field<Button>(_menu, "_leaveButton"));
                Equal(asked + 1, _dialogs.Requests.Count, "Leave Match asks first");
                DialogRequest request = _dialogs.Requests[_dialogs.Requests.Count - 1];
                Equal(_text.Get("ui.dialog.leave_match.title"), request.Title, "the leave dialog's title");
                Equal(_text.Get("ui.dialog.leave_match.body_offline"), request.Body, "offline, the dialog says the match is saved");
                Equal(_text.Get("ui.dialog.leave_match.confirm"), request.ConfirmLabel, "the leave dialog's confirm label");
                Equal(_text.Get("ui.dialog.leave_match.cancel"), request.CancelLabel, "the leave dialog's cancel label");
                Equal(0, _flow.Menus.Count, "cancelling stays in the match");
                Equal(ScreenId.MatchMenu, _router.Current, "cancelling stays on the menu");

                _dialogs.Answer = true;
                Click(Field<Button>(_menu, "_leaveButton"));
                Equal(1, _flow.Menus.Count, "confirming leaves for the menu scene");
                if (_flow.Menus.Count > 0)
                {
                    Equal(MenuEntry.MainMenu, _flow.Menus[0], "leaving opens the main menu");
                }

                Equal(SessionStatus.Ended, _active.Status, "the session has been left");
                Check(_matchStore.HasSave, "the match left offline is saved for later");
                _flow.Menus.Clear();
                _finished = true;
            }

            /// <summary>A resync in the middle of a card's flight: the queue is dropped and the table is the session's view at once.</summary>
            private void Resync()
            {
                _session.RaiseResynced();
                SettleTweens();
                PlayerView view = _active.View;
                Check(_tablePresenter.Player.IsIdle, "a resync drops the running animation and the queue");
                Equal(view.Hand, State.Hand, "a resync takes the hand from the session's view");
                Equal(view.TrickIndex, State.TricksCompleted, "a resync takes the tricks played from the session's view");
                Equal(view.CurrentTrick.Count, State.Trick.Count, "a resync takes the trick from the session's view");
                VerifyTable("resume");
                _nextTapAt = _clock.Now + 0.2;
            }

            private void SkipTheSweep()
            {
                if (!Once("skip") || _reduceMotion)
                {
                    return;
                }

                MatchEvent sweeping = _tablePresenter.Player.Current;
                Click(Field<Button>(_table, "_tableTap"));
                Step();
                Check(_tablePresenter.Player.Current != sweeping, "a tap on the table skips the trick sweep");
            }

            // -------------------------------------------------------------- results

            private void OnRoundResults()
            {
                if (_roundResults == null)
                {
                    return;
                }

                RoundResult result = State.LastRoundResult;
                RectTransform[] rows = Field<RectTransform[]>(_roundResults, "_row");
                foreach (RectTransform row in rows)
                {
                    if (!row.gameObject.activeSelf)
                    {
                        return;     // still revealing
                    }
                }

                Button next = Field<Button>(_roundResults, "_nextButton");
                if (_verifiedRound == result.Round)
                {
                    return;
                }

                // Let the count-up finish.
                Wait(0.8);
                _verifiedRound = result.Round;
                int rounds = _active.Rules.Rounds;
                bool last = result.Round >= rounds;
                Equal(_text.Format("ui.round_results.round_title", result.Round, rounds), Field<TMP_Text>(_roundResults, "_roundTitle").text, "the round title");
                var order = new Seat[4];
                ResultsText.RoundRowOrder(Local, order);
                TMP_Text[] names = Field<TMP_Text[]>(_roundResults, "_rowName"), bids = Field<TMP_Text[]>(_roundResults, "_rowBid");
                TMP_Text[] wons = Field<TMP_Text[]>(_roundResults, "_rowWon"), deltas = Field<TMP_Text[]>(_roundResults, "_rowDelta");
                TMP_Text[] totals = Field<TMP_Text[]>(_roundResults, "_rowTotal");
                Color negative = Field<Color>(_roundResults, "_negativeColor");
                for (int row = 0; row < 4; row++)
                {
                    SeatRoundResult line = result[order[row]];
                    string name = order[row] == Local ? _text.Get("ui.common.you") : _active.Seats[(int)order[row]].Name;
                    Equal(name, names[row].text, "a round row's name");
                    Equal(line.Call.ToString(CultureInfo.InvariantCulture), bids[row].text, "a round row's bid is the event's");
                    Equal(line.Won.ToString(CultureInfo.InvariantCulture), wons[row].text, "a round row's tricks won are the event's");
                    Equal(ScoreTenths.FormatSigned(line.DeltaTenths), deltas[row].text, "a round row's score is the event's, counted up");
                    Equal(ScoreTenths.Format(line.TotalTenths), totals[row].text, "a round row's total is the event's");
                    Check((deltas[row].color == negative) == (line.DeltaTenths < 0), "a missed call is burgundy");
                    DrawableNote(deltas[row], "RoundResults RowDelta");
                    DrawableNote(totals[row], "RoundResults RowTotal");
                    if (line.DeltaTenths < 0)
                    {
                        Check(deltas[row].text.StartsWith(ScoreTenths.MinusSign, StringComparison.Ordinal), "a negative score uses the true minus sign");
                    }
                }

                RectTransform band = Field<RectTransform>(_roundResults, "_highlightBand");
                Check(band.gameObject.activeSelf && Mathf.Approximately(band.anchoredPosition.y, rows[0].anchoredPosition.y), "the gold band is on the local player's row");
                Equal(_text.Get(last ? "ui.round_results.see_final" : "ui.round_results.next"), Field<TMP_Text>(_roundResults, "_nextLabel").text, "the Next button's label");
                Equal(last ? string.Empty : _text.Format("ui.round_results.footer_next", result.Round + 1), Field<TMP_Text>(_roundResults, "_footer").text, "the round footer");
                Check(next.interactable, "Next Round can be pressed");
                Check(!_active.IsPaused, "the round results do not pause the match");
                Snapshot("RoundResults_" + result.Round);

                if (_scorecard != null && Once("scorecard from results"))
                {
                    Click(Field<Button>(_roundResults, "_scorecardButton"));
                    Equal(ScreenId.Scorecard, _router.Current, "View Scorecard opens the scorecard");
                    VerifyScorecard();
                    Snapshot("Scorecard_round_" + result.Round);
                    Click(Field<Button>(_scorecard, "_backButton"));
                    Equal(ScreenId.RoundResults, _router.Current, "the scorecard closes back to the round results");
                }

                if (Once("back on results"))
                {
                    Click(Field<Button>(_roundResults, "_backButton"));
                    Equal(ScreenId.MatchMenu, _router.Current, "Back on the round results opens the match menu");
                    Click(Field<Button>(_menu, "_resumeButton"));
                    Equal(ScreenId.RoundResults, _router.Current, "the menu closes back to the round results");
                }

                int ready = _session.ReadyCalls;
                Check(Click(next), "Next Round is pressed");
                Equal(ready + 1, _session.ReadyCalls, "Next Round tells the session this seat is ready");
                Click(next);
                Equal(ready + 1, _session.ReadyCalls, "Next Round is sent once");
                Wait(0.2);
                if (last)
                {
                    Equal(ScreenId.MatchResults, _router.Current, "after the last round the match results open");
                }
                else
                {
                    Equal(ScreenId.Gameplay, _router.Current, "the next round returns to the table");
                    Equal(result.Round + 1, State.Round, "the next round has started");
                }
            }

            private void VerifyScorecard()
            {
                TableState state = State;
                ScoreSheet sheet = state.Scores;
                bool over = state.Phase == TablePhase.MatchEnded;
                IReadOnlyList<Standing> standings = over ? state.FinalStandings : Standings.Compute(sheet);
                TMP_Text[] names = Field<TMP_Text[]>(_scorecard, "_rowName"), rounds = Field<TMP_Text[]>(_scorecard, "_rowRound");
                TMP_Text[] totals = Field<TMP_Text[]>(_scorecard, "_rowTotal"), ranks = Field<TMP_Text[]>(_scorecard, "_rowRank");
                for (int row = 0; row < standings.Count; row++)
                {
                    Standing standing = standings[row];
                    string name = standing.Seat == Local ? _text.Get("ui.common.you") : _active.Seats[(int)standing.Seat].Name;
                    Equal(name, names[row].text, "a scorecard row's name (rows sorted by rank)");
                    int sum = 0;
                    for (int i = 0; i < ScorecardView.RoundCount; i++)
                    {
                        string expected = i < sheet.Rounds.Count ? ScoreTenths.Format(sheet.Rounds[i][standing.Seat].DeltaTenths) : ResultsText.Unplayed;
                        Equal(expected, rounds[(row * ScorecardView.RoundCount) + i].text, "a scorecard round cell");
                        if (i < sheet.Rounds.Count)
                        {
                            sum += sheet.Rounds[i][standing.Seat].DeltaTenths;
                        }
                    }

                    Equal(ScoreTenths.Format(sum), totals[row].text, "a scorecard total is the sum of its rounds");
                    DrawableNote(totals[row], "Scorecard RowTotal");
                    for (int i = 0; i < ScorecardView.RoundCount; i++)
                    {
                        DrawableNote(rounds[(row * ScorecardView.RoundCount) + i], "Scorecard RowRound");
                    }

                    Equal(standing.Rank.ToString(CultureInfo.InvariantCulture), ranks[row].text, "a scorecard rank");
                }

                string completed = ResultsText.RoundsCompleted(sheet.Rounds.Count, _text);
                Equal(over ? _text.Format("ui.scorecard.footer_final", completed) : completed, Field<TMP_Text>(_scorecard, "_footer").text, "the scorecard footer");
                Equal(_text.Get(over ? "ui.scorecard.back_to_results" : "ui.scorecard.back_to_game"), Field<TMP_Text>(_scorecard, "_primaryLabel").text, "the scorecard button's label");
            }

            private void OnMatchResults()
            {
                if (_matchResults == null || _matchResultsVerified)
                {
                    return;
                }

                _matchResultsVerified = true;
                TableState state = State;
                IReadOnlyList<Standing> standings = state.FinalStandings;
                Equal(4, standings.Count, "the match results have four standings");
                Standing local = default;
                foreach (Standing standing in standings)
                {
                    if (standing.Seat == Local)
                    {
                        local = standing;
                    }
                }

                Equal(_text.Get(ResultsText.HeadlineKey(local)), Field<TMP_Text>(_matchResults, "_headline").text, "the headline follows the local player's place");
                Equal(local.Rank == 1, Field<GameObject>(_matchResults, "_trophy").activeSelf, "the trophy shows only for a win");
                TMP_Text[] ranks = Field<TMP_Text[]>(_matchResults, "_rowRank"), names = Field<TMP_Text[]>(_matchResults, "_rowName");
                TMP_Text[] totals = Field<TMP_Text[]>(_matchResults, "_rowTotal");
                GameObject[] crowns = Field<GameObject[]>(_matchResults, "_rowCrown");
                for (int row = 0; row < standings.Count; row++)
                {
                    Standing standing = standings[row];
                    string name = standing.Seat == Local ? _text.Get("ui.common.you") : _active.Seats[(int)standing.Seat].Name;
                    Equal(standing.Rank.ToString(CultureInfo.InvariantCulture), ranks[row].text, "a final row's rank");
                    Equal(name, names[row].text, "a final row's name");
                    Equal(ScoreTenths.Format(standing.TotalTenths), totals[row].text, "a final row's total");
                    Check((totals[row].color == Field<Color>(_matchResults, "_negativeColor")) == (standing.TotalTenths < 0), "a negative final total is burgundy, a positive one is not");
                    DrawableNote(totals[row], "MatchResults RowTotal");
                    Equal(standing.Rank == 1, crowns[row].activeSelf, "a crown marks every winner");
                }

                Equal(ResultsText.RoundsCompleted(_active.Rules.Rounds, _text), Field<TMP_Text>(_matchResults, "_footer").text, "the match footer");
                Equal(local.Rank == 1 ? 1 : 0, _audio.Count(SoundCue.Victory), "the victory sound plays for a win only");
                Equal(local.Rank == 1, _audio.Ducked, "the music is ducked under the victory sting");
                Equal(local.Rank == 1 ? 1 : 0, _audio.Ducks, "the music ducks once, for a win only");
                Snapshot("MatchResults");
                Wait(MatchResultsPresenter.VictoryStingSeconds + 0.2);
                Check(!_audio.Ducked, "the music comes back when the sting has played");

                _router.Back();
                Equal(ScreenId.MatchResults, _router.Current, "Android back stays on the match results");
                if (_scorecard != null)
                {
                    Click(Field<Button>(_matchResults, "_scorecardButton"));
                    Equal(ScreenId.Scorecard, _router.Current, "View Scorecard opens from the match results");
                    VerifyScorecard();
                    Snapshot("Scorecard_final");
                    Click(Field<Button>(_scorecard, "_primaryButton"));
                    Equal(ScreenId.MatchResults, _router.Current, "Back to Results returns to the match results");
                }

                Click(Field<Button>(_matchResults, "_playAgainButton"));
                Equal(1, _flow.Started.Count, "Play Again starts one match");
                if (_flow.Started.Count > 0)
                {
                    Equal(MatchMode.Offline, _flow.Started[0].Mode, "Play Again offline starts an offline match");
                    Equal(_settings.Difficulty, _flow.Started[0].Difficulty, "Play Again keeps the difficulty");
                    Check(!_flow.Started[0].ResumeSaved, "Play Again deals a new match");
                }

                Click(Field<Button>(_matchResults, "_mainMenuButton"));
                Equal(0, _flow.Menus.Count, "once leaving, the other buttons do nothing");
                _flow.Started.Clear();
                _finished = true;
            }

            // -------------------------------------------------------------- what the table must show

            private void VerifyStart()
            {
                Equal(ScreenId.Gameplay, _router.Current, "the table starts on the Gameplay screen");
                Equal(MusicTrack.Table, _audio.Music, "the table music plays");
                Check(!Field<GameObject>(_table, "_reconnecting").activeSelf, "no Reconnecting cover on a running match");
                Check(!Field<Button>(_table, "_imBackButton").gameObject.activeSelf, "no I'm back banner on a present player");
                Check(!_table.Emotes.IsOpen, "the emote picker starts closed");
            }

            private void VerifyResumed()
            {
                // Rebuilt at once from the session's view: no event has been played.
                PlayerView view = _active.View;
                Equal(0, _tablePresenter.Player.Backlog, "a resumed match queues no events");
                Equal(view.Round, State.Round, "the resumed round");
                Equal(view.Hand, State.Hand, "the resumed hand");
                VerifyTable("resume");
                Equal(view.Hand.Count, _table.Hand.Count, "the resumed hand is on the table at once");
                Snapshot("Table_resumed");
            }

            private void VerifyFrame()
            {
                HandView hand = _table.Hand;
                TableState state = State;
                Check(!hand.Interactable || state.IsLocalPlayTurn, "the hand takes plays only on the local turn that has been played back");
                Check(_table.Cards.Created <= CardViewPool.DeckSize, "the pool never holds more than 52 cards", "created " + _table.Cards.Created);
                Check(_bidding.IsPanelShown == Field<GameObject>(_bidding, "_panel").activeSelf, "once its slide has settled, the call panel is up exactly while it is shown");
                if (_tablePresenter.Player.Current is HandDealt dealt && dealt.Seat == Local && _dealBeganAt < 0.0)
                {
                    _dealBeganAt = _clock.Now;
                    if (_reduceMotion)
                    {
                        // GDD §14: with Reduce motion the deal is instant, with a 200 ms fade.
                        Equal(13, hand.Count, "with Reduce motion the dealt hand is there at once");
                        Check(hand.Count > 0 && AlphaOf(HandCard(FirstOf(State.Hand))) < 1f, "with Reduce motion the dealt hand fades in");
                    }
                }
                if (_active.CanPause && _active.Status != SessionStatus.Ended)
                {
                    ScreenId top = _router.Current;
                    bool inMatch = top == ScreenId.Gameplay || top == ScreenId.RoundResults || top == ScreenId.MatchResults;
                    Check(_active.IsPaused == !inMatch, "offline, the match is paused exactly while a menu covers the table", "top " + top + " paused " + _active.IsPaused);
                }
            }

            private void VerifyPlayed(MatchEvent e)
            {
                _playedEvents++;
                TableState state = State;
                VerifyTable(e.GetType().Name);
                switch (e)
                {
                    case RoundStarted _:
                        _dealSounds = _audio.Count(SoundCue.CardDeal);
                        break;

                    case HandDealt dealt when dealt.Seat == Local:
                    {
                        Equal(13, _table.Hand.Count, "the deal puts thirteen cards in the hand");
                        Equal(0, _table.Cards.Free, "after the deal all 52 cards are on the table");
                        bool opaque = true;
                        foreach (Card card in state.Hand)
                        {
                            opaque &= AlphaOf(HandCard(card)) >= 1f;
                        }

                        Check(opaque, "after the deal every card of the hand is fully visible");
                        int sounds = _audio.Count(SoundCue.CardDeal) - _dealSounds;
                        if (_reduceMotion)
                        {
                            Equal(0, sounds, "with Reduce motion no card flies and no deal sound plays");
                        }
                        else
                        {
                            Check(sounds >= 40 && sounds <= 52, "the deal sounds for each card (two in one frame share one sound)", "sounds " + sounds);
                        }

                        if (_dealBeganAt >= 0.0)
                        {
                            double seconds = _clock.Now - _dealBeganAt;
                            double expected = _reduceMotion ? 0.4 : 1.6;
                            Check(Math.Abs(seconds - expected) <= 2.5 * Frame, "the deal takes 1.6 s (0.4 s with Reduce motion)", $"{seconds:0.000} s");
                        }

                        _dealBeganAt = -1.0;
                        break;
                    }

                    case CallTurnStarted turn:
                        if (turn.Seat == Local)
                        {
                            Check(_bidding.IsPanelShown, "the call panel opens on the local call turn");
                            Equal(_text.Get("ui.seat.choosing_bid"), StatusOf(turn.Seat), "the local pill reads \"Choosing bid\"");
                        }
                        else
                        {
                            Check(!_bidding.IsPanelShown, "the call panel is hidden on another seat's call turn");
                            TMP_Text calling = Field<TMP_Text>(_bidding, "_callingText");
                            Check(calling.gameObject.activeSelf, "the centre says who is calling");
                            Equal(_text.Format("ui.bidding.calling", _active.Seats[(int)turn.Seat].Name), calling.text, "\"{name} is calling…\"");
                            Equal(_text.Get("ui.seat.calling"), StatusOf(turn.Seat), "the caller's pill reads \"Calling…\"");
                        }

                        break;

                    case CallPlaced placed:
                        Equal(_text.Format(placed.IsAuto ? "ui.seat.bid_auto" : "ui.seat.bid", placed.Call), StatusOf(placed.Seat), "the pill shows the call once placed");
                        Check(BubbleOf(placed.Seat).activeSelf, "a call pops as a bubble over the seat");
                        if (placed.Seat == Local)
                        {
                            Equal(_intendedCall, placed.Call, "the local call is the one confirmed");
                            Check(!_bidding.IsPanelShown, "the call panel closes once the call is placed");
                            Check(Field<GameObject>(_bidding, "_panel").activeSelf, "the call panel slides away rather than vanishing");
                        }

                        break;

                    case PlayTurnStarted turn:
                        Check(_table.Prompt.IsShowing, "a prompt shows during play");
                        Drawable(Field<TMP_Text>(_table.Prompt, "_label"), "every character of the prompt can be drawn");
                        if (turn.Seat == Local)
                        {
                            TMP_Text promptLabel = Field<TMP_Text>(_table.Prompt, "_label");
                            state.TryGetObligation(out PlayObligation asked);
                            string tip = TurnTips.AreOn(_settings.CompletedMatches) ? TurnTips.ForObligation(asked, state.Trick, _text) : null;
                            if (tip != null)
                            {
                                // The first three matches: the rule is explained, suits spelled out.
                                Equal(tip, _table.Prompt.Text, "in the first matches the prompt is the tip for the turn");
                                Check(tip.StartsWith("Tip", StringComparison.Ordinal), "a tip reads as a tip", tip);
                                Drawable(promptLabel, "a tip has no character its label cannot draw");
                            }
                            else
                            {
                                if (!TurnTips.AreOn(_settings.CompletedMatches))
                                {
                                    Check(!_table.Prompt.Text.StartsWith("Tip", StringComparison.Ordinal), "after three finished matches the prompt shows no tip", _table.Prompt.Text);
                                }

                                Check(_table.Prompt.Text.StartsWith("Your turn", StringComparison.Ordinal), "the prompt says it is your turn", _table.Prompt.Text);
                                Equal(TurnPrompt.ForObligation(asked, _text), _table.Prompt.Text, "the prompt states the obligation");
                                if (asked.Kind == ObligationKind.BeatCard)
                                {
                                    Drawable(promptLabel, "a \"beat\" prompt in play has no character its label cannot draw");
                                    Equal(1, SpritesDrawn(promptLabel), "a \"beat\" prompt in play draws its suit from the suit sprites");
                                }
                                else if (asked.Kind == ObligationKind.FollowSuit)
                                {
                                    Drawable(promptLabel, "a \"follow\" prompt in play has no character its label cannot draw");
                                    Equal(1, SpritesDrawn(promptLabel), "a \"follow\" prompt in play draws its suit from the suit sprites");
                                }
                            }

                            Check(_table.Hand.Interactable, "the hand takes input on the local turn");
                            Check(state.TryGetObligation(out PlayObligation _), "the local turn has an obligation");
                            foreach (Card card in state.Hand)
                            {
                                CardView view = HandCard(card);
                                bool legal = state.Legal.Contains(card);
                                Check(view != null && view.IsGlowing == legal, "legal cards glow");
                                Check(view != null && view.IsDimmed == !legal, "cards that cannot be played are subdued");
                            }
                        }
                        else
                        {
                            Equal(_text.Format("ui.prompt.waiting_for", _active.Seats[(int)turn.Seat].Name), _table.Prompt.Text, "the prompt says who is awaited");
                            Check(!_table.Hand.Interactable, "the hand takes no input on another seat's turn");
                        }

                        break;

                    case CardPlayed played:
                    {
                        int rel = played.Seat.RelativeTo(Local);
                        CardView card = _table.Trick.CardAt(rel);
                        Check(card != null && card.IsFaceUp && card.Card == played.Card, "a played card lies face up in its seat's slot");
                        if (card != null)
                        {
                            CardPlacement slot = _table.Trick.Slot(rel);
                            CardPlacement at = card.Placement;
                            Check((at.Position - slot.Position).sqrMagnitude < 0.01f && Mathf.Abs(at.Scale - slot.Scale) < 0.001f && Mathf.Abs(at.Flat - 1f) < 0.001f,
                                "a played card comes to rest exactly at its slot", $"at {at.Position} scale {at.Scale} flat {at.Flat}; slot {slot.Position} scale {slot.Scale}");
                        }

                        Check(!_table.Trick.IsPeeking, "the peek ends when the next card is played");
                        break;
                    }

                    case TrickCompleted completed:
                    {
                        Equal(0, _table.Trick.Count, "the swept trick has left the table");
                        string pill = StatusOf(completed.Winner);
                        Check(pill.Contains("Won " + state.WonBy(completed.Winner)), "the winner's pill counts the trick", pill);
                        bool met = state.WonBy(completed.Winner) >= state.CallOf(completed.Winner);
                        Equal(met, pill.EndsWith("\u2713", StringComparison.Ordinal), "the pill gets its tick exactly when the call is met");
                        for (int rel = 0; rel < TableView.SeatCount; rel++)
                        {
                            Drawable(Field<TMP_Text>(_table.Seat(rel), "_status"), "every character of a status pill can be drawn");
                        }

                        break;
                    }

                    case RoundScored scored:
                        Check(!_table.Prompt.IsShowing, "the prompt is hidden once the round is scored");
                        if (scored.Result.Round == _active.Rules.Rounds && _lastSummaryView == null && _active.View.Phase == Phase.RoundSummary)
                        {
                            // For a staged online table in the last round's summary.
                            _lastSummaryView = _active.View;
                        }

                        break;

                    case MatchEnded _:
                        _finalView = _finalView ?? _active.View;
                        break;
                }
            }

            private static float AlphaOf(CardView card) => card != null ? Field<CanvasGroup>(card, "_group").alpha : 0f;

            private string StatusOf(Seat seat) => Field<TMP_Text>(_table.Seat(seat.RelativeTo(Local)), "_status").text;

            /// <summary>
            /// No empty boxes: every character of a label is in the atlas baked for the label's own
            /// font, or is one its sprite asset draws (the suits). Nothing is left to a fallback font.
            /// </summary>
            private void Drawable(TMP_Text label, string what)
            {
                string text = label.text ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ' ')
                    {
                        continue;
                    }

                    bool inFont = label.font.HasCharacter(c, false, false);
                    bool asSprite = label.spriteAsset != null && label.spriteAsset.spriteCharacterLookupTable.ContainsKey(c);
                    Check(inFont || asSprite, what, $"'{c}' (U+{(int)c:X4}) in \"{text}\": not in {label.font.name} and not a sprite");
                }
            }

            /// <summary>
            /// The same question for a label of another engineer's screen that this table's presenters
            /// fill: reported as a note, once per label kind and character, not as a failure.
            /// </summary>
            private void DrawableNote(TMP_Text label, string where)
            {
                string text = label.text ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ' ' || label.font.HasCharacter(c, false, false))
                    {
                        continue;
                    }

                    if (label.spriteAsset != null && label.spriteAsset.spriteCharacterLookupTable.ContainsKey(c))
                    {
                        continue;
                    }

                    if (_done.Add($"glyph:{where}:{(int)c}"))
                    {
                        Note($"{where}: '{c}' (U+{(int)c:X4}) is not in the atlas of {label.font.name}; it is drawn by a fallback font, or as an empty box without one");
                    }
                }
            }

            /// <summary>How many of a label's characters TextMeshPro draws as sprites.</summary>
            private static int SpritesDrawn(TMP_Text label)
            {
                label.ForceMeshUpdate(true, true);
                TMP_TextInfo info = label.textInfo;
                int sprites = 0;
                for (int i = 0; i < info.characterCount; i++)
                {
                    if (info.characterInfo[i].elementType == TMP_TextElementType.Sprite)
                    {
                        sprites++;
                    }
                }

                return sprites;
            }

            /// <summary>
            /// The prompts that name a suit or a card, for every suit: "Your turn · Follow ♠",
            /// "Your turn · Beat Q♥". A match only happens to show some of them.
            /// </summary>
            private void StagePrompts()
            {
                TablePromptView prompt = _table.Prompt;
                TMP_Text label = Field<TMP_Text>(prompt, "_label");
                RectTransform pill = Field<RectTransform>(prompt, "_pill");
                bool showing = prompt.IsShowing;
                string before = prompt.Text;
                Check(label.spriteAsset != null, "the prompt label has the suit sprites");
                prompt.Show(_text.Get("ui.prompt.lead_any"));
                label.ForceMeshUpdate(true, true);
                float plainHeight = label.textBounds.size.y;
                float lineHeight = label.preferredHeight;
                foreach (Suit suit in new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs })
                {
                    string follow = _text.Format("ui.prompt.follow", TurnPrompt.SuitSymbol(suit).ToString());
                    prompt.Show(follow);
                    Drawable(label, "a \"follow\" prompt has no character its label cannot draw");
                    Equal(1, SpritesDrawn(label), "the suit of a \"follow\" prompt is drawn from the suit sprites");
                    Check(label.preferredHeight <= lineHeight + 0.01f, "a suit does not make the prompt's line taller", $"{label.preferredHeight} against {lineHeight}");
                    Check(pill.sizeDelta.x >= label.GetPreferredValues(follow).x, "the prompt pill is as wide as its text");
                    if (suit == Suit.Spades)
                    {
                        Snapshot("Table_prompt_follow");
                    }

                    foreach (Rank rank in new[] { Rank.Queen, Rank.Ten, Rank.Ace, Rank.Seven })
                    {
                        string beat = _text.Format("ui.prompt.beat", TurnPrompt.CardLabel(new Card(suit, rank)));
                        prompt.Show(beat);
                        Drawable(label, "a \"beat\" prompt has no character its label cannot draw");
                        Equal(1, SpritesDrawn(label), "the suit of a \"beat\" prompt is drawn from the suit sprites");
                        Check(label.textBounds.size.y <= plainHeight * 1.35f, "a suit stays within the height of the prompt's text", $"{label.textBounds.size.y} against {plainHeight}");
                        if (suit == Suit.Hearts && rank == Rank.Queen)
                        {
                            Snapshot("Table_prompt_beat");
                        }
                    }
                }

                if (showing)
                {
                    prompt.Show(before);
                }
                else
                {
                    prompt.Hide();
                }
            }

            private GameObject BubbleOf(Seat seat) => Field<RectTransform>(_table.Seat(seat.RelativeTo(Local)), "_bubble").gameObject;

            /// <summary>The views against the played state: hand, fans, trick, pool, HUD, turn ring.</summary>
            private void VerifyTable(string after)
            {
                TableState state = State;
                HandView hand = _table.Hand;

                // ADR 020: every card at the table is in one deck; offline, the one the player selected.
                string deck = _cosmetics.SelectedDeckId;
                Sprite back = _cards.Back(deck);
                Equal(state.Hand.Count, hand.Count, "the hand shows exactly the cards held (after " + KindOf(after) + ")");
                foreach (Card card in state.Hand)
                {
                    CardView view = HandCard(card);
                    Check(view != null && view.IsFaceUp, "every card held has a face-up view in the hand");
                    bool inDeck = view != null && SpriteOf(view) == _cards.Face(deck, card);
                    Check(inDeck, "every face on the table is in the player's deck (ADR 020)", inDeck ? null : card + " in the hand");
                }

                int onTable = hand.Count;
                for (int rel = 1; rel < TableView.SeatCount; rel++)
                {
                    Seat seat = (Seat)(((int)Local + rel) % SeatExtensions.Count);
                    OpponentHandView fan = _table.Seat(rel).Fan;
                    Equal(state.HandCountOf(seat), fan.Count, "an opponent's fan shows the cards they have left");
                    foreach (CardView card in Field<List<CardView>>(fan, "_cards"))
                    {
                        Check(!card.IsFaceUp, "an opponent's fan holds backs only");
                        bool inDeck = SpriteOf(card) == back;
                        Check(inDeck, "every back on the table is in the player's deck (ADR 020)", inDeck ? null : seat + "'s fan, whose seat is in " + _active.Seats[(int)seat].DeckThemeId);
                    }

                    onTable += fan.Count;
                }

                Trick trick = state.Trick;
                Equal(trick.Count, _table.Trick.Count, "the trick area shows the cards played to the trick");
                for (int i = 0; i < trick.Count; i++)
                {
                    CardView lying = _table.Trick.CardAt(trick.SeatAt(i).RelativeTo(Local));
                    Check(lying != null && lying.IsFaceUp && lying.Card == trick[i], "each trick card lies in the slot of the seat that played it");
                    bool inDeck = lying != null && SpriteOf(lying) == _cards.Face(deck, trick[i]);
                    Check(inDeck, "every face on the table is in the player's deck (ADR 020)", inDeck ? null : trick[i] + " in the trick");
                }

                onTable += _table.Trick.Count + (_table.Trick.IsPeeking ? 4 : 0);
                Equal(onTable, _table.Cards.Created - _table.Cards.Free, "every rented card is in the hand, a fan or the trick");
                Equal(_text.Format("ui.table.round", Math.Max(1, state.Round), state.Rules.Rounds), Field<TMP_Text>(_table, "_roundLabel").text, "the HUD shows the round");
                for (int rel = 0; rel < TableView.SeatCount; rel++)
                {
                    Seat seat = (Seat)(((int)Local + rel) % SeatExtensions.Count);
                    bool turn = state.HasTurn && state.TurnSeat == seat;
                    Equal(turn, Field<Image>(_table.Seat(rel), "_timerRing").enabled, "the gold ring is on the seat whose turn it is");
                    string name = seat == Local ? _text.Get("ui.table.you") : _active.Seats[(int)seat].Name;
                    Equal(name, Field<TMP_Text>(_table.Seat(rel), "_name").text, "a seat shows its player's name");
                }
            }

            private static string KindOf(string after) => after == "resume" ? "a resume" : "an event";

            // -------------------------------------------------------------- online-only states, staged

            private void StageOnlineStates()
            {
                _stage = "staged online states";
                if (_router.Current != ScreenId.Gameplay)
                {
                    _router.Show(ScreenId.Gameplay);
                }

                GameObject cover = Field<GameObject>(_table, "_reconnecting");
                Button imBack = Field<Button>(_table, "_imBackButton");

                // Reconnecting: the cover shows and input is locked.
                _session.StatusOverride = SessionStatus.Reconnecting;
                _session.RaiseStatusChanged();
                Check(cover.activeSelf, "Reconnecting shows the cover over the table");
                Check(!_table.Hand.Interactable, "input is locked while reconnecting");
                Snapshot("Table_reconnecting");
                _session.StatusOverride = null;
                _session.RaiseStatusChanged();
                Check(!cover.activeSelf, "the cover goes once reconnected");

                // Seat chips (GDD §8 "What others see"): the pill keeps the seat's line, the chip says the rest.
                Seat opponent = (Seat)(((int)Local + 1) % SeatExtensions.Count);
                SeatView opponentView = _table.Seat(1);
                Image opponentPill = Field<Image>(opponentView, "_statusPill");
                Color plainPill = Field<Color>(opponentView, "_pillColor");
                string before = StatusOf(opponent);
                int toasts = _toasts.Shown.Count;
                Check(opponentView.Chip == null, "offline, the bots carry no tag (everyone knows)");
                StageConnection(opponent, SeatConnection.Away);
                Equal(before, StatusOf(opponent), "an Away seat keeps its bid line");
                Equal(_text.Get("ui.seat.away"), opponentView.Chip, "an Away seat shows the Away chip");
                Check(!opponentView.ChipIsBusy, "the Away chip has no spinner");
                Check(opponentPill.color == plainPill, "an Away seat's pill is not gold");
                StageConnection(opponent, SeatConnection.Reconnecting);
                Equal(before, StatusOf(opponent), "a reconnecting seat keeps its bid line");
                Equal(_text.Get("ui.seat.reconnecting"), opponentView.Chip, "a reconnecting seat shows the Reconnecting chip");
                Check(opponentView.ChipIsBusy, "the Reconnecting chip has its spinner");
                Drawable(Field<TMP_Text>(opponentView, "_chipText"), "every character of a seat chip can be drawn");
                Equal(toasts, _toasts.Shown.Count, "no toast while a seat is away or reconnecting");
                StageConnection(opponent, SeatConnection.Connected);
                Equal(before, StatusOf(opponent), "the line is the same when the player is back");
                Check(opponentView.Chip == null, "the chip clears when the player is back");
                Equal(_text.Get("ui.toast.welcome_back"), LastToast(), "a returning player is welcomed back");
                StageConnection(opponent, SeatConnection.BotControlled);
                Equal(before, StatusOf(opponent), "a seat a bot took over keeps its bid line");
                Equal(_text.Get("ui.seat.bot_playing"), opponentView.Chip, "a seat a bot took over shows \"Bot playing\"");
                StageConnection(opponent, SeatConnection.Left);
                Equal(_text.Get("ui.toast.left_table"), LastToast(), "a leaving player is announced");
                Equal(_text.Get("ui.seat.bot_playing"), opponentView.Chip, "a seat whose player left shows \"Bot playing\"");
                StageConnection(opponent, SeatConnection.Connected);

                // The local seat marked Away: the banner, and the chip everybody sees.
                StageConnection(Local, SeatConnection.Away);
                Check(imBack.gameObject.activeSelf, "an Away local seat gets the \"I'm back\" banner");
                Equal(_text.Get("ui.seat.away"), _table.Seat(0).Chip, "an Away local seat shows the Away chip too");
                Snapshot("Table_im_back");
                Click(imBack);
                StageConnection(Local, SeatConnection.Connected);
                Check(!imBack.gameObject.activeSelf, "the banner goes once the player is back");
                Check(_table.Seat(0).Chip == null, "the local chip goes once the player is back");
                _session.SeatsOverride = null;
                _session.RaiseSeatsChanged();

                StageBotTags();
                StageLongNames();
                StagePlaybackFailure();

                // The room is gone.
                int menus = _flow.Menus.Count;
                _session.StatusOverride = SessionStatus.Failed;
                _session.RaiseStatusChanged();
                Equal(_text.Get("ui.toast.host_left"), LastToast(), "a closed room says \"Host left\"");
                Equal(menus + 1, _flow.Menus.Count, "a closed room returns to the menu scene");
                if (_flow.Menus.Count > 0)
                {
                    Equal(MenuEntry.MainMenu, _flow.Menus[_flow.Menus.Count - 1], "a closed room returns to the main menu");
                }

                _session.StatusOverride = null;
            }

            private string LastToast() => _toasts.Shown.Count > 0 ? _toasts.Shown[_toasts.Shown.Count - 1] : null;

            private DialogRequest LastDialog() => _dialogs.Requests.Count > 0 ? _dialogs.Requests[_dialogs.Requests.Count - 1] : null;

            /// <summary>
            /// GDD §9 "Online fill-ins show a small BOT tag": the same bots, on a table that is not
            /// offline. The mockups draw no tag, so the picture is made here, with a reconnecting and
            /// an away seat beside it.
            /// </summary>
            private void StageBotTags()
            {
                _session.ModeOverride = MatchMode.QuickMatch;
                _session.RaiseSeatsChanged();
                for (int rel = 1; rel < TableView.SeatCount; rel++)
                {
                    Seat seat = (Seat)(((int)Local + rel) % SeatExtensions.Count);
                    Check(_active.Seats[(int)seat].IsBot, "the staged seats are bots");
                    Equal(_text.Get(SeatStatus.BotTagKey), _table.Seat(rel).Chip, "online, a fill-in bot carries the BOT tag");
                }

                Check(_table.Seat(0).Chip == null, "the local seat has no tag");
                StageConnection((Seat)(((int)Local + 2) % SeatExtensions.Count), SeatConnection.Reconnecting);
                StageConnection((Seat)(((int)Local + 3) % SeatExtensions.Count), SeatConnection.Away);
                Check(_table.Seat(2).ChipIsBusy, "the connection wins over the tag");
                Snapshot("Table_seat_chips");
                _session.ModeOverride = null;
                _session.SeatsOverride = null;
                _session.RaiseSeatsChanged();
                Check(_table.Seat(1).Chip == null, "offline again: no tag");
            }

            /// <summary>
            /// GDD §11 screen 14: names have up to 16 characters, in any script. On the plates they
            /// shrink and then end in an ellipsis. The Devanagari name needs the device's font, which
            /// the game puts behind its own at startup; for the picture it is put there here too.
            /// </summary>
            private void StageLongNames()
            {
                string[] names = { null, "Pushpa Lamichane", "MMMMMMMMMMMMMMMM", "सुमन श्रेष्ठ" };
                var fallbacks = new SystemFontFallbacks();
                try
                {
                    fallbacks.Initialize();
                    if (fallbacks.Added.Count == 0)
                    {
                        Note("no system font to draw the Devanagari name with; its picture shows boxes");
                    }

                    for (int rel = 1; rel < TableView.SeatCount; rel++)
                    {
                        StageName((Seat)(((int)Local + rel) % SeatExtensions.Count), names[rel]);
                    }

                    for (int rel = 1; rel < TableView.SeatCount; rel++)
                    {
                        TMP_Text label = Field<TMP_Text>(_table.Seat(rel), "_name");
                        Equal(names[rel], label.text, "a seat shows the whole name it was given");
                        label.ForceMeshUpdate(true, true);
                        float drawn = label.textBounds.size.x, room = label.rectTransform.rect.width;
                        Check(drawn <= room + 0.5f, "a long name stays within its plate", $"\"{names[rel]}\": {drawn:0.0} of {room:0.0}");
                    }

                    Snapshot("Table_long_names");
                }
                finally
                {
                    foreach (TMP_FontAsset font in new List<TMP_FontAsset>(fallbacks.Added))
                    {
                        TMP_Settings.fallbackFontAssets?.Remove(font);
                        if (font == null)
                        {
                            continue;
                        }

                        foreach (Texture2D atlas in font.atlasTextures ?? Array.Empty<Texture2D>())
                        {
                            UnityEngine.Object.DestroyImmediate(atlas);
                        }

                        UnityEngine.Object.DestroyImmediate(font.material);
                        UnityEngine.Object.DestroyImmediate(font);
                    }

                    _session.SeatsOverride = null;
                    _session.RaiseSeatsChanged();
                }
            }

            private void StageName(Seat seat, string name)
            {
                var seats = new SessionSeat[SeatExtensions.Count];
                for (int i = 0; i < seats.Length; i++)
                {
                    SessionSeat was = _active.Seats[i];
                    seats[i] = i == (int)seat ? new SessionSeat(was.Seat, name, was.AvatarId, was.DeckThemeId, was.IsBot, was.IsLocal, was.Connection) : was;
                }

                _session.SeatsOverride = seats;
                _session.RaiseSeatsChanged();
            }

            /// <summary>
            /// GDD §17 principle 9: an event whose playback throws ends like a resync. The event
            /// queue reports the failure; the table logs it and is rebuilt from the session's view.
            /// </summary>
            private void StagePlaybackFailure()
            {
                _expectedError = "staged playback failure";
                Invoke(_tablePresenter, "OnPlaybackFaulted", new ReadyConfirmed(Local), new InvalidOperationException("staged playback failure"));
                Check(_expectedError == null, "a failed event's error is logged", "nothing was logged");
                _expectedError = null;
                Check(_tablePresenter.Player.IsIdle, "after a failed event nothing is left queued");
                PlayerView view = _active.View;
                Equal(view.Hand, State.Hand, "after a failed event the table is the session's view");
                VerifyTable("resume");
            }

            private void StageConnection(Seat seat, SeatConnection connection)
            {
                var seats = new SessionSeat[SeatExtensions.Count];
                for (int i = 0; i < seats.Length; i++)
                {
                    SessionSeat was = _active.Seats[i];
                    seats[i] = i == (int)seat
                        ? new SessionSeat(was.Seat, was.Name, was.AvatarId, was.DeckThemeId, was.IsBot, was.IsLocal, connection)
                        : was;
                }

                _session.SeatsOverride = seats;
                _session.RaiseSeatsChanged();
            }

            // -------------------------------------------------------------- a real online session, fed by hand

            /// <summary>
            /// An <see cref="OnlineMatchSession"/> given the state the offline match reached, as a host
            /// would send it: what only the online session can do (ready and presence messages, the
            /// summary countdown, the reason of a failure) goes through the presenters for real.
            /// </summary>
            private void StageOnlineSession()
            {
                PlayerView view = _active.View;
                StopMatch();
                _stage = "staged online session";
                Equal(Phase.RoundSummary, view.Phase, "the online table is staged from a round summary");

                var sent = new List<byte[]>();
                var connections = new SeatConnection[SeatExtensions.Count];
                connections[(int)view.Viewer] = SeatConnection.Away;
                var status = new TableStatus(view.Viewer, TableStatus.NoClock, 0, 8000, connections);
                var online = new OnlineMatchSession(MatchMode.PrivateRoom, view, status, sent.Add, _stats, _settings);
                StartOn(online);
                online.Start();
                Equal(1, sent.Count, "an online table tells its host it is ready");
                Check(sent.Count > 0 && SameBytes(ClientMessages.TableReady(), sent[0]), "the first message is TableReady");
                online.Tick(_clock.Now);
                online.OnSync(new TableUpdate(1, null, view, status));
                Equal(SessionStatus.Running, online.Status, "the online session runs once the host has sent the table");
                Wait(0.5);

                // The round summary, with the host's countdown on the button.
                if (_roundResults != null)
                {
                    Equal(ScreenId.RoundResults, _router.Current, "a table joined during the round summary opens the round results");
                    Button next = Field<Button>(_roundResults, "_nextButton");
                    int seconds = Math.Max(0, (int)Math.Ceiling(online.Timer.Deadline - _clock.Now));
                    Check(seconds >= 6 && seconds <= 8, "the summary countdown comes from the host", "seconds " + seconds);
                    Equal(_text.Format("ui.round_results.next_countdown", seconds), Field<TMP_Text>(_roundResults, "_nextLabel").text, "online, Next Round shows the countdown");
                    Wait(1.0);
                    Equal(_text.Format("ui.round_results.next_countdown", seconds - 1), Field<TMP_Text>(_roundResults, "_nextLabel").text, "the countdown counts down");
                    int before = sent.Count;
                    Check(Click(next), "Next Round can be pressed online");
                    Equal(before + 1, sent.Count, "online, Next Round sends one message");
                    Check(SameBytes(ClientMessages.ReadyNext(), sent[sent.Count - 1]), "the message is ReadyNext (\"I'm ready\")");
                    Check(!next.interactable, "Next Round stays pressed while the others get ready");

                    // GDD §8 asks the app to pause itself offline only: online nothing opens.
                    Invoke(_table, "OnApplicationPause", true);
                    Equal(ScreenId.RoundResults, _router.Current, "online, going to the background opens no menu");
                }

                // Marked Away after two timeouts: the banner, and the tap that takes the seat back.
                Button imBack = Field<Button>(_table, "_imBackButton");
                Check(imBack.gameObject.activeSelf, "online, an Away local seat gets the \"I'm back\" banner");
                int messages = sent.Count;
                Check(Click(imBack), "the \"I'm back\" banner can be tapped");
                Equal(messages + 1, sent.Count, "\"I'm back\" sends one message");
                Check(SameBytes(ClientMessages.Presence(), sent[sent.Count - 1]), "the message is Presence");

                // The match menu of an online match: nothing pauses, and leaving costs the match.
                if (_roundResults != null)
                {
                    Click(Field<Button>(_roundResults, "_backButton"));
                    Equal(ScreenId.MatchMenu, _router.Current, "the match menu opens over the online round results");
                    Equal(_text.Get("ui.match_menu.mode_online"), Field<TMP_Text>(_menu, "_modeLabel").text, "online, the menu says the game continues");
                    Check(!Field<GameObject>(_menu, "_pausedNote").activeSelf, "online, the menu has no \"paused\" note");
                    Check(!online.IsPaused, "online, opening the menu pauses nothing");

                    // The host's device is the room (phase 1): leaving closes it for everybody.
                    _dialogs.Answer = false;
                    _rooms.Current = new Room { IsHost = true, Match = online };
                    Click(Field<Button>(_menu, "_leaveButton"));
                    Equal(_text.Get("ui.dialog.leave_match.body_host"), LastDialog()?.Body, "the host is told that leaving closes the room for everyone");
                    Drawable(Field<TMP_Text>(_menu, "_modeLabel"), "every character of the menu's mode tag can be drawn");
                    _rooms.Current = new Room { IsHost = false, Match = online };
                    _dialogs.Answer = false;
                    int asked = _dialogs.Requests.Count;
                    Click(Field<Button>(_menu, "_leaveButton"));
                    Equal(asked + 1, _dialogs.Requests.Count, "online, Leave Match asks first");
                    Equal(_text.Get("ui.dialog.leave_match.body"), _dialogs.Requests[_dialogs.Requests.Count - 1].Body, "online, the dialog says a bot takes the seat and it counts as a loss");
                    Click(Field<Button>(_menu, "_resumeButton"));
                    _rooms.Current = null;
                }

                // The room closes: a dialog gives the reason, and its one button leads to the main menu.
                _dialogs.Answer = true;
                int dialogs = _dialogs.Requests.Count, toasts = _toasts.Shown.Count, menus = _flow.Menus.Count;
                online.OnRoomClosed(Callbreak.Application.Rooms.RoomError.Kicked);
                Equal(SessionStatus.Failed, online.Status, "a closed room fails the session");
                Equal(dialogs + 1, _dialogs.Requests.Count, "a failed online session explains itself in a dialog");
                if (_dialogs.Requests.Count > dialogs)
                {
                    DialogRequest request = _dialogs.Requests[_dialogs.Requests.Count - 1];
                    Equal(_text.Get("ui.toast.removed_from_room"), request.Title, "the failure dialog gives the reason");
                    Equal(string.Empty, request.Body, "the failure dialog has no second line");
                    Equal(_text.Get("ui.match_results.main_menu"), request.ConfirmLabel, "the failure dialog's button reads Main Menu");
                    Check(request.CancelLabel == null, "the failure dialog has one button");
                }

                Equal(toasts, _toasts.Shown.Count, "a failed online session shows no toast");
                Equal(menus + 1, _flow.Menus.Count, "answering the failure dialog leaves for the menu scene");
                if (_flow.Menus.Count > menus)
                {
                    Equal(MenuEntry.MainMenu, _flow.Menus[_flow.Menus.Count - 1], "a failed session returns to the main menu");
                }

                Check(!_table.Hand.Interactable, "a failed session takes no more input");
                StopMatch();
            }

            /// <summary>
            /// The last round's summary online (GDD §11 screen 09): the host counts it down like any
            /// other, and See Final Results shows the seconds.
            /// </summary>
            private void StageOnlineLastRound()
            {
                if (_lastSummaryView == null || _roundResults == null)
                {
                    Note("no last-round summary was reached; the last round's countdown was not staged");
                    return;
                }

                _stage = "staged online last round";
                var status = new TableStatus(_lastSummaryView.Viewer, TableStatus.NoClock, 0, 8000, new SeatConnection[SeatExtensions.Count]);
                var online = new OnlineMatchSession(MatchMode.PrivateRoom, _lastSummaryView, status, _ => { }, _stats, _settings);
                StartOn(online);
                online.Start();
                online.Tick(_clock.Now);
                online.OnSync(new TableUpdate(1, null, _lastSummaryView, status));
                Wait(0.5);
                Equal(ScreenId.RoundResults, _router.Current, "a table joined during the last round's summary opens it");
                int seconds = Math.Max(0, (int)Math.Ceiling(online.Timer.Deadline - _clock.Now));
                Equal(_text.Format("ui.round_results.see_final_countdown", seconds), Field<TMP_Text>(_roundResults, "_nextLabel").text, "online, See Final Results shows the host's countdown");
                Snapshot("RoundResults_final_countdown");
                StopMatch();
            }

            /// <summary>
            /// The match results of a private room (GDD §11 screen 10): Play Again goes back to the
            /// lobby, and if the host has gone meanwhile, the player is told so on the way to the
            /// main menu instead of arriving there without a word.
            /// </summary>
            private void StageOnlineResults()
            {
                if (_finalView == null || _matchResults == null)
                {
                    Note("no finished match to stage the online results with");
                    return;
                }

                foreach (bool hostGone in new[] { false, true })
                {
                    _stage = hostGone ? "staged online results, host gone" : "staged online results";
                    var room = new Room { IsHost = false };
                    _rooms.Current = room;
                    var status = new TableStatus(_finalView.Viewer, TableStatus.NoClock, 0, TableStatus.NoClock, new SeatConnection[SeatExtensions.Count]);
                    var online = new OnlineMatchSession(MatchMode.PrivateRoom, _finalView, status, _ => { }, _stats, _settings);
                    room.Match = online;
                    StartOn(online);
                    online.Start();
                    online.OnSync(new TableUpdate(1, null, _finalView, status));
                    Wait(0.3);
                    Equal(ScreenId.MatchResults, _router.Current, "a finished online match opens its results");
                    Check(room.HasListeners, "the results listen for the room closing");
                    if (hostGone)
                    {
                        room.Close(Callbreak.Application.Rooms.RoomError.HostLeft);
                        _rooms.Current = null;
                    }

                    int toasts = _toasts.Shown.Count, menus = _flow.Menus.Count;
                    Click(Field<Button>(_matchResults, "_playAgainButton"));
                    Equal(menus + 1, _flow.Menus.Count, "Play Again in a private room leaves for the menu scene");
                    MenuEntry entry = _flow.Menus.Count > 0 ? _flow.Menus[_flow.Menus.Count - 1] : MenuEntry.OfflineSetup;
                    if (hostGone)
                    {
                        Equal(toasts + 1, _toasts.Shown.Count, "Play Again after the host left says why");
                        Equal(_text.Get("ui.toast.host_left"), LastToast(), "the player is told the host left");
                        Equal(MenuEntry.MainMenu, entry, "with the room gone, Play Again goes to the main menu");
                    }
                    else
                    {
                        Equal(toasts, _toasts.Shown.Count, "Play Again with the room open shows no toast");
                        Equal(MenuEntry.PrivateLobby, entry, "Play Again in a private room goes back to the lobby");
                    }

                    StopMatch();
                    Check(!room.HasListeners, "the results stop listening to the room when the scene goes");
                }

                _rooms.Current = null;
            }

            private static bool SameBytes(byte[] a, byte[] b)
            {
                if (a == null || b == null || a.Length != b.Length)
                {
                    return false;
                }

                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            // -------------------------------------------------------------- the scene unloads

            /// <summary>
            /// A scene unload in the middle of an animation: Unity disables and destroys the scene's
            /// objects first, and only then does the scope dispose what it created. Nothing may touch
            /// a view by then, nothing may throw (one throwing Dispose would leave the rest undisposed),
            /// and afterwards the app-wide services must find none of the presenters listening.
            /// </summary>
            private void StageTeardown()
            {
                _stage = "teardown";
                StartMatch(false);
                for (int i = 0; i < 900 && _tablePresenter.Player.Current == null; i++)
                {
                    Step();
                }

                MatchEvent playing = _tablePresenter.Player.Current;
                Check(playing != null, "the scene is torn down while an event is being played");
                if (!_reduceMotion)
                {
                    Check(playing is HandDealt, "the scene is torn down in the middle of the deal", playing?.GetType().Name);
                }

                Step();
                Step();
                Step();
                Check(_tablePresenter.Player.Current == playing, "the animation is still running when the scene goes");

                int errors = _fails.TryGetValue("nothing is logged as an error", out int logged) ? logged : 0;
                _played.Played -= VerifyPlayed;
                _matchRunning = false;

                // Unity's order: OnDisable on every behaviour, then the objects are destroyed.
                foreach (MonoBehaviour behaviour in _woken)
                {
                    if (behaviour != null)
                    {
                        Invoke(behaviour, "OnDisable");
                    }
                }

                _woken.Clear();
                UnityEngine.Object.DestroyImmediate(_canvas.gameObject);

                // Then the scope disposes, last made first.
                var disposables = new IDisposable[]
                {
                    _scorecardPresenter, _matchPresenter, _roundPresenter, _menuPresenter, _biddingPresenter, _tablePresenter, _router, _active,
                };
                int disposed = 0;
                string thrown = null;
                foreach (IDisposable disposable in disposables)
                {
                    if (disposable == null)
                    {
                        continue;
                    }

                    try
                    {
                        disposable.Dispose();
                        disposed++;
                    }
                    catch (Exception e)
                    {
                        thrown = thrown ?? disposable.GetType().Name + ": " + e;
                    }
                }

                Check(thrown == null, "disposing the presenters after their views are destroyed throws nothing", thrown);
                Check(disposed >= 5, "every presenter, the router and the session were disposed", "disposed " + disposed);
                Check(_tablePresenter.Player.IsIdle, "the abandoned queue is empty");

                // The services that outlive the scene: a change must reach none of the dead presenters.
                string late = null;
                try
                {
                    _settings.SetMusic(!_settings.Music);
                    _settings.SetSoundEffects(!_settings.SoundEffects);
                    _settings.SetReduceMotion(!_settings.ReduceMotion);
                    _settings.SetSortOrder(HandSorter.Toggle(_settings.SortOrder));
                    _settings.SetReduceMotion(_reduceMotion);
                }
                catch (Exception e)
                {
                    late = e.ToString();
                }

                Check(late == null, "a settings change after the scene is gone reaches no presenter", late);
                int now = _fails.TryGetValue("nothing is logged as an error", out logged) ? logged : 0;
                Equal(errors, now, "nothing is logged while the scene is torn down");
            }

            // -------------------------------------------------------------- pictures

            /// <summary>Renders what the table shows now (with captures on), with every screen transition settled.</summary>
            private void Snapshot(string name)
            {
                if (!_captures || !_done.Add("snapshot:" + name))
                {
                    return;
                }

                SettleTweens();
                const int width = 1920, height = 1080;
                if (_target == null)
                {
                    _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _camera.targetTexture = _target;
                }

                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in _canvas.GetComponentsInChildren<TMP_Text>(false))
                {
                    text.ForceMeshUpdate(true, true);
                }

                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _target };
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                }
                else
                {
                    _camera.Render();
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _target;
                var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(ScreenCaptureTool.OutputFolder);
                File.WriteAllBytes($"{ScreenCaptureTool.OutputFolder}/Sim_{name}.png", pixels.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
