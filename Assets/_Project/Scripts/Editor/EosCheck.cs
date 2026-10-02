using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Networking.Eos;
using UnityEditor;

namespace Callbreak.Editor
{
    /// <summary>
    /// Checks the EOS directory end to end, in the playing game (Tools/Unity/playcheck.sh).
    /// <list type="number">
    /// <item>Signs this device in with its device id.</item>
    /// <item>Lists a random code that no real room can have (it is longer than a room code).</item>
    /// <item>Looks the code up and checks the owner is this player.</item>
    /// <item>Takes the code down and checks that a lookup then answers NotFound.</item>
    /// </list>
    /// It runs on its own <see cref="EosPeerNetwork"/>, and starts EOS when the game has not.
    /// <see cref="Start"/> begins, <see cref="Report"/> tells how far it got, and <see cref="Stop"/>
    /// ends it. The report names no credential: the player shows as the first six characters of its
    /// id, and a failure as the SDK's result code.
    /// </summary>
    public static class EosCheck
    {
        /// <summary>How long one step (signing in, listing, one lookup) may take.</summary>
        private const double StepSeconds = 30.0;

        /// <summary>How long a listing may take to show up in lookups, or to drop out of them.</summary>
        private const double SearchSeconds = 20.0;

        private const int PollMilliseconds = 750;

        private const string Running = "running";
        private const string Stopped = "STOPPED before the end";

        private static readonly StringBuilder Steps = new StringBuilder();

        private static EosPeerNetwork _network;
        private static CancellationTokenSource _cancel;
        private static Stopwatch _clock;
        private static string _verdict = "not run";

        /// <summary>Begins the check in the playing game; <see cref="Report"/> tells how it went.</summary>
        public static string Start()
        {
            if (!EditorApplication.isPlaying)
            {
                return "not in Play mode: run it in the playing game (Tools/Unity/playcheck.sh)";
            }

            if (_network != null)
            {
                return "already running; " + Report();
            }

            Steps.Clear();
            _clock = Stopwatch.StartNew();
            if (!EosPlatform.IsConfigured())
            {
                _verdict = "NOT RUN: the EOS config has no complete client credentials (EOS Plugin > EOS Configuration)";
                return Report();
            }

            if (EosPlatform.IsRunning)
            {
                Note("EOS was running");
            }
            else if (EosPlatform.Start())
            {
                // It runs until Play mode ends, when the plugin shuts it down. Its manager must not be destroyed.
                Note("EOS started here");
            }
            else
            {
                _verdict = "FAIL: EOS did not start (its error is in the console)";
                return Report();
            }

            // It never calls TryReceive: that would take packets the game's own network is waiting for.
            var network = new EosPeerNetwork(() => "EosCheck");
            _network = network;
            _cancel = new CancellationTokenSource();
            _verdict = Running;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _ = RunAsync(network, _cancel.Token);
            return "started";
        }

        /// <summary>The verdict, then each step with the seconds since the start.</summary>
        public static string Report()
        {
            var text = new StringBuilder();
            text.Append("verdict=").Append(_verdict);
            if (Steps.Length > 0)
            {
                text.Append(" | ").Append(Steps);
            }

            return text.ToString();
        }

        /// <summary>
        /// Ends the check and takes its listing down. An EOS it started runs on until Play mode ends;
        /// the plugin then shuts it down (see EosPlatform.Start).
        /// </summary>
        public static string Stop()
        {
            if (_verdict == Running)
            {
                _verdict = Stopped;
            }

            _cancel?.Cancel();
            Finish(_network);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            return Report();
        }

        private static async Task RunAsync(EosPeerNetwork network, CancellationToken ct)
        {
            try
            {
                // 1. Sign in.
                bool signedIn = await SignInAsync(network, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (!signedIn)
                {
                    Fail("sign-in", network.LastFailure ?? "no answer within " + StepSeconds + " s");
                    return;
                }

                string self = network.LocalAddress;
                Note("signed in as " + Short(self) + " (device id)");

                // 2. A code nobody lists.
                string code = "EOSCHECK" + RoomCode.Generate(new System.Random().Next);
                PeerLookup before = await LookUpAsync(network, code, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                Note("lookup before listing: " + Describe(before, self));
                if (before.Error != RoomError.NotFound)
                {
                    Fail("lookup before listing", "expected NotFound" + Failure(network));
                    return;
                }

                // 3. List it.
                RoomError listed = await ListAsync(network, code, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                Note("list: " + listed);
                if (listed != RoomError.None)
                {
                    Fail("list", listed + Failure(network));
                    return;
                }

                // 4. Look it up: the owner is this player. A new listing may take a moment to be searchable.
                PeerLookup found = await LookUpUntilAsync(network, code, lookup => lookup.Error != RoomError.NotFound, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                Note("lookup after listing: " + Describe(found, self));
                if (!found.Found)
                {
                    Fail("lookup after listing", "expected this player" + Failure(network));
                    return;
                }

                if (found.Address != self)
                {
                    Fail("lookup after listing", "the owner is another player");
                    return;
                }

                // 5. Take it down: the code is gone.
                network.Unlist(code);
                Note("unlisted");
                PeerLookup after = await LookUpUntilAsync(network, code, lookup => lookup.Error != RoomError.None, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                Note("lookup after unlisting: " + Describe(after, self));
                if (after.Error != RoomError.NotFound)
                {
                    Fail("lookup after unlisting", "expected NotFound" + Failure(network));
                    return;
                }

                _verdict = "PASS";
            }
            catch (OperationCanceledException)
            {
                // Stopped meanwhile: the verdict says so already.
            }
            catch (Exception e)
            {
                Fail("exception", e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                Finish(network);
            }
        }

        private static async Task<bool> SignInAsync(EosPeerNetwork network, CancellationToken ct)
        {
            using (CancellationTokenSource step = Step(ct))
            {
                return await network.SignInAsync(step.Token);
            }
        }

        private static async Task<RoomError> ListAsync(EosPeerNetwork network, string code, CancellationToken ct)
        {
            using (CancellationTokenSource step = Step(ct))
            {
                return await network.ListAsync(code, step.Token);
            }
        }

        private static async Task<PeerLookup> LookUpAsync(EosPeerNetwork network, string code, CancellationToken ct)
        {
            using (CancellationTokenSource step = Step(ct))
            {
                return await network.LookUpAsync(code, step.Token);
            }
        }

        /// <summary>Looks <paramref name="code"/> up until <paramref name="settled"/> holds or the search time runs out.</summary>
        private static async Task<PeerLookup> LookUpUntilAsync(EosPeerNetwork network, string code, Func<PeerLookup, bool> settled, CancellationToken ct)
        {
            double deadline = Seconds() + SearchSeconds;
            while (true)
            {
                PeerLookup lookup = await LookUpAsync(network, code, ct);
                if (settled(lookup) || Seconds() >= deadline || ct.IsCancellationRequested)
                {
                    return lookup;
                }

                await Task.Delay(PollMilliseconds, ct);
            }
        }

        private static CancellationTokenSource Step(CancellationToken ct)
        {
            CancellationTokenSource step = CancellationTokenSource.CreateLinkedTokenSource(ct);
            step.CancelAfter(TimeSpan.FromSeconds(StepSeconds));
            return step;
        }

        /// <summary>Disposes <paramref name="network"/> (its listing goes down) if it is still the check's.</summary>
        private static void Finish(EosPeerNetwork network)
        {
            if (network == null || network != _network)
            {
                return;
            }

            _network = null;
            if (_verdict == Running)
            {
                _verdict = Stopped;
            }

            network.Dispose();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Stop();
            }
        }

        private static void Fail(string step, string why)
        {
            if (_verdict == Running)
            {
                _verdict = "FAIL at " + step + ": " + why;
            }

            Note("failed: " + why);
        }

        private static void Note(string step)
        {
            if (Steps.Length > 0)
            {
                Steps.Append("; ");
            }

            Steps.Append(Seconds().ToString("0.0")).Append("s ").Append(step);
        }

        private static string Failure(EosPeerNetwork network)
        {
            string failure = network.LastFailure;
            return string.IsNullOrEmpty(failure) ? string.Empty : " (EOS: " + failure + ")";
        }

        private static string Describe(PeerLookup lookup, string self)
        {
            if (!lookup.Found)
            {
                return lookup.Error.ToString();
            }

            return lookup.Address == self ? "found, owned by this player" : "found, owned by " + Short(lookup.Address);
        }

        /// <summary>An id's first six characters: enough to tell two apart, not the id.</summary>
        private static string Short(string id) => string.IsNullOrEmpty(id) ? "(none)" : id.Substring(0, Math.Min(6, id.Length)) + "...";

        private static double Seconds() => _clock != null ? _clock.Elapsed.TotalSeconds : 0.0;
    }
}
