using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Updates
{
    /// <summary>Where <see cref="ForcedUpdate"/> stands.</summary>
    public enum ForcedUpdateState
    {
        /// <summary>No newer version is known: the store is asked at the moments the app offers.</summary>
        Idle,

        /// <summary>The store is being asked.</summary>
        Checking,

        /// <summary>The store's full-screen update is up.</summary>
        Updating,

        /// <summary>"Update required" holds the player: the update went without updating.</summary>
        Blocked,
    }

    /// <summary>
    /// Forced updates (the owner, 5 Oct 2026: "any new Play version"), so that everyone online plays
    /// the same version. At the moments the app offers (the launch, the main menu on top again, the
    /// app back on the main menu) it asks the store (<see cref="IAppUpdates"/>) whether a newer
    /// version is out. It asks at most once per <see cref="MinCheckInterval"/>, only where the caller
    /// says the place allows it (never in a match, in matchmaking or in a room), and never while a
    /// rejoin into a running match may still come: online, with a rejoin ticket that still holds, the
    /// menu offers it (ADR 018).
    /// <list type="bullet">
    /// <item>A newer version the store will install now starts its full-screen immediate update; an
    /// update in progress (left during its download) is brought back, if the store allows it.</item>
    /// <item>No newer version, one the store won't install now (e.g. low storage), or no answer (no
    /// Play Store, an app not installed from it, offline): the player plays on.</item>
    /// <item>An update that goes without updating (the player backed out, or it failed) holds the
    /// player at "Update required", which has one button, Update, and no way to close it. Update asks
    /// the store again and starts the update again; with nothing to install any more, or no answer,
    /// the player plays on.</item>
    /// <item>While it holds, a moment asks the store again (no sooner than
    /// <see cref="MinCheckInterval"/>) only to bring back an update in progress.</item>
    /// </list>
    /// A check whose newer version could not start because the player had moved on doesn't count
    /// against the interval. Every call, and every answer of the store, comes on the main thread;
    /// nothing here throws.
    /// </summary>
    public sealed class ForcedUpdate : IDisposable
    {
        /// <summary>The shortest time between two checks the app starts by itself.</summary>
        public static readonly TimeSpan MinCheckInterval = TimeSpan.FromSeconds(30);

        private const string TitleKey = "ui.dialog.update_required.title";
        private const string BodyKey = "ui.dialog.update_required.body";
        private const string ConfirmKey = "ui.dialog.update_required.confirm";

        private readonly IAppUpdates _store;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _text;
        private readonly IRejoinStore _rejoins;
        private readonly INetworkStatus _network;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private Task _check = Task.CompletedTask;
        private Func<bool> _allowedHere;
        private TaskCompletionSource<bool> _lookAgain;
        private DateTime _lastCheckUtc;
        private bool _checkedBefore;
        private bool _disposed;

        /// <param name="utcNow">The clock that spaces the checks.</param>
        /// <param name="log">Where each step goes, a line each (no player data); nowhere when null.</param>
        public ForcedUpdate(
            IAppUpdates store,
            IDialogs dialogs,
            ILocalization text,
            IRejoinStore rejoins,
            INetworkStatus network,
            Func<DateTime> utcNow,
            Action<string> log = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _rejoins = rejoins ?? throw new ArgumentNullException(nameof(rejoins));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
        }

        public ForcedUpdateState State { get; private set; } = ForcedUpdateState.Idle;

        /// <summary>
        /// A moment where the app may ask the player to update. The store is asked unless a check is
        /// under way (this moment joins it, and its rule judges the answer), the store's update is up,
        /// the last check was less than <see cref="MinCheckInterval"/> ago, the place doesn't allow
        /// it, or a rejoin may still come. While "Update required" holds the player, the store is
        /// asked only to bring back an update in progress. Completes once the answer has been acted
        /// on: the update is up, or the player plays on.
        /// </summary>
        /// <param name="allowedHere">
        /// Whether the player's place allows an update now. Asked before the store is, and again when
        /// it answers, since the player may have moved on meanwhile.
        /// </param>
        public Task CheckAsync(Func<bool> allowedHere)
        {
            if (allowedHere == null)
            {
                throw new ArgumentNullException(nameof(allowedHere));
            }

            if (_disposed)
            {
                return Task.CompletedTask;
            }

            switch (State)
            {
                case ForcedUpdateState.Idle:
                    if (!Due() || !Allows(allowedHere) || RejoinMayCome())
                    {
                        return Task.CompletedTask;
                    }

                    State = ForcedUpdateState.Checking;
                    _allowedHere = allowedHere;
                    _check = CheckThenUpdateAsync();
                    return _check;

                case ForcedUpdateState.Checking:
                    _allowedHere = allowedHere;
                    return _check;

                case ForcedUpdateState.Blocked:
                    if (Due())
                    {
                        _lookAgain?.TrySetResult(true);
                    }

                    return Task.CompletedTask;

                default:
                    return Task.CompletedTask;
            }
        }

        /// <summary>Closes "Update required" and stops asking.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _lifetime.Cancel();
            }
            catch (Exception e)
            {
                _log("App update: stopping: " + e.Message);
            }
        }

        /// <summary>Asks the store; starts its update when it has one and the place still allows it.</summary>
        private async Task CheckThenUpdateAsync()
        {
            AppUpdateCheck answer = await AskStoreAsync();
            if (_disposed)
            {
                return;
            }

            if (!Wanted(answer))
            {
                Stamp();
                LogPlayingOn(answer);
                State = ForcedUpdateState.Idle;
                return;
            }

            if (!Allows(_allowedHere) || RejoinMayCome())
            {
                _log("App update: a newer version is out, but the player has moved on; the next moment asks again.");
                State = ForcedUpdateState.Idle;
                return;
            }

            Stamp();
            _ = UpdateThenHoldAsync(answer);
        }

        /// <summary>The store's update; when it goes without updating, "Update required".</summary>
        private async Task UpdateThenHoldAsync(AppUpdateCheck answer)
        {
            await UpdateAsync(answer);
            if (!_disposed)
            {
                await HoldAsync();
            }
        }

        /// <summary>
        /// Holds the player at "Update required" until the update installs (the app restarts) or there
        /// is nothing to install any more. The dialog stays up while the store is asked again and
        /// under the store's screen.
        /// </summary>
        private async Task HoldAsync()
        {
            _log("App update: the update is no longer in front (left, failed, or the app paused behind it); \"Update required\" holds the player.");
            using (CancellationTokenSource dialog = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                Task<DialogChoice> answer = AskToUpdate(dialog.Token);
                while (true)
                {
                    State = ForcedUpdateState.Blocked;
                    var lookAgain = new TaskCompletionSource<bool>();
                    _lookAgain = lookAgain;
                    Task woke = await Task.WhenAny(answer, lookAgain.Task);
                    _lookAgain = null;
                    if (_disposed)
                    {
                        return;
                    }

                    bool pressed = woke == answer;
                    if (pressed)
                    {
                        if (answer.Status != TaskStatus.RanToCompletion || answer.Result != DialogChoice.Confirm)
                        {
                            // Closed from outside: the dialog layer went away. The next moment asks again.
                            _log("App update: \"Update required\" was closed from outside.");
                            State = ForcedUpdateState.Idle;
                            return;
                        }

                        // Update: the dialog comes straight back while the store is asked again.
                        answer = AskToUpdate(dialog.Token);
                    }

                    AppUpdateCheck again = await AskStoreAsync();
                    if (_disposed)
                    {
                        return;
                    }

                    Stamp();
                    if (pressed ? Wanted(again) : Resumable(again))
                    {
                        await UpdateAsync(again);
                        if (_disposed)
                        {
                            return;
                        }

                        continue;
                    }

                    if (pressed)
                    {
                        LogPlayingOn(again);
                        dialog.Cancel();
                        State = ForcedUpdateState.Idle;
                        return;
                    }
                }
            }
        }

        /// <summary>The store's full-screen update, until it goes without updating.</summary>
        private async Task UpdateAsync(AppUpdateCheck answer)
        {
            State = ForcedUpdateState.Updating;
            _log(answer.Availability == AppUpdateAvailability.InProgress
                ? "App update: bringing back the update in progress."
                : "App update: a newer version is out; starting the update.");
            try
            {
                await _store.StartImmediateAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Stopped.
            }
            catch (Exception e)
            {
                _log("App update: the update could not start: " + e.Message);
            }
        }

        /// <summary>The store's answer; <see cref="AppUpdateAvailability.Failed"/> when it breaks.</summary>
        private async Task<AppUpdateCheck> AskStoreAsync()
        {
            try
            {
                AppUpdateCheck answer = await _store.CheckAsync(_lifetime.Token);
                return answer ?? NoAnswer();
            }
            catch (OperationCanceledException)
            {
                return NoAnswer();
            }
            catch (Exception e)
            {
                _log("App update: the store could not be asked: " + e.Message);
                return NoAnswer();
            }
        }

        private Task<DialogChoice> AskToUpdate(CancellationToken ct)
        {
            try
            {
                return _dialogs.AskAsync(
                    new DialogRequest(_text.Get(TitleKey), _text.Get(BodyKey), _text.Get(ConfirmKey), null, dismissable: false),
                    ct);
            }
            catch (Exception e)
            {
                _log("App update: \"Update required\" could not be shown: " + e.Message);
                return Task.FromResult(DialogChoice.Dismissed);
            }
        }

        /// <summary>A newer version the store will install now, or an update in progress it will bring back.</summary>
        private static bool Wanted(AppUpdateCheck answer)
        {
            return answer.ImmediateAllowed
                && (answer.Availability == AppUpdateAvailability.Available || answer.Availability == AppUpdateAvailability.InProgress);
        }

        /// <summary>An update in progress the store will bring back.</summary>
        private static bool Resumable(AppUpdateCheck answer)
        {
            return answer.ImmediateAllowed && answer.Availability == AppUpdateAvailability.InProgress;
        }

        private static AppUpdateCheck NoAnswer() => new AppUpdateCheck(AppUpdateAvailability.Failed, false);

        private bool Allows(Func<bool> allowedHere)
        {
            try
            {
                return allowedHere();
            }
            catch (Exception e)
            {
                _log("App update: the place could not say whether it allows an update: " + e.Message);
                return false;
            }
        }

        /// <summary>A rejoin into a running match may still come: online, the menu offers one while a ticket holds.</summary>
        private bool RejoinMayCome()
        {
            try
            {
                return _network.IsOnline && _rejoins.TryGet(out _);
            }
            catch (Exception e)
            {
                _log("App update: the rejoin ticket could not be read: " + e.Message);
                return false;
            }
        }

        /// <summary>The last check is long enough ago; a clock set back counts as long enough.</summary>
        private bool Due()
        {
            if (!_checkedBefore)
            {
                return true;
            }

            TimeSpan since = _utcNow() - _lastCheckUtc;
            return since >= MinCheckInterval || since < TimeSpan.Zero;
        }

        private void Stamp()
        {
            _lastCheckUtc = _utcNow();
            _checkedBefore = true;
        }

        private void LogPlayingOn(AppUpdateCheck answer)
        {
            switch (answer.Availability)
            {
                case AppUpdateAvailability.None:
                    _log("App update: this is the newest version.");
                    break;

                case AppUpdateAvailability.Failed:
                    _log("App update: no answer from the store; the player plays on.");
                    break;

                default:
                    _log("App update: the store won't install the newer version now; the player plays on.");
                    break;
            }
        }
    }
}
