using System;
using System.Collections.Generic;
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
    /// <item>Quick Match tables, a few rounds for the spread of the timings. It lists a table with
    /// 3 free seats and times until a search finds it. It sets 1 and times until a search shows 1.
    /// It sets 0 and times until searches no longer return it, then unlists it. The first round also
    /// checks that a private room never shows among the tables.</item>
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

        /// <summary>How many Quick Match round trips one run makes, for the spread of the timings.</summary>
        private const int QuickRounds = 3;

        /// <summary>How long a table may take to show, change or go in searches before the check gives up.</summary>
        private const double QuickWaitSeconds = 40.0;

        /// <summary>The pause between two table searches: EOS limits how often a player may search.</summary>
        private const int QuickPollMilliseconds = 1000;

        private const string Running = "running";
        private const string Stopped = "STOPPED before the end";

        private static readonly StringBuilder Steps = new StringBuilder();
        private static readonly System.Random Codes = new System.Random();
        private static readonly List<double> SearchTimes = new List<double>();

        private static EosPeerNetwork _network;
        private static CancellationTokenSource _cancel;
        private static Stopwatch _clock;
        private static string _verdict = "not run";
        private static int _failedSearches;

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

                // 6. Quick Match tables.
                if (!await RunQuickAsync(network, ct))
                {
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

        /// <summary>
        /// The Quick Match rounds. Each time is measured up to the end of the search that first saw
        /// the change, so it is an upper bound; its resolution is one pause plus one search.
        /// </summary>
        private static async Task<bool> RunQuickAsync(EosPeerNetwork network, CancellationToken ct)
        {
            var listings = new List<double>();
            var found = new List<double>();
            var ones = new List<double>();
            var gone = new List<double>();
            SearchTimes.Clear();
            _failedSearches = 0;
            for (int round = 1; round <= QuickRounds; round++)
            {
                string code = "EOSQUICK" + RoomCode.Generate(Codes.Next);
                string privateCode = null;
                if (round == 1)
                {
                    // A private room listed meanwhile must never show among the tables.
                    privateCode = "EOSPRIVATE" + RoomCode.Generate(Codes.Next);
                    RoomError privateListed = await ListAsync(network, privateCode, ct);
                    if (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    if (privateListed != RoomError.None)
                    {
                        Fail("quick round 1: a private room to tell apart", privateListed + Failure(network));
                        return false;
                    }
                }

                double start = Seconds();
                RoomError listed = await ListQuickAsync(network, code, 3, ct);
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                if (listed != RoomError.None)
                {
                    Fail("quick round " + round + ": list", listed + Failure(network));
                    return false;
                }

                double listing = Seconds() - start;
                start = Seconds();
                QuickTableSearch seen = await SearchUntilAsync(network, search => FreeOf(search, code) == 3, ct);
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                if (FreeOf(seen, code) != 3)
                {
                    Fail("quick round " + round + ": find the new table", "not found with 3 free seats within " + QuickWaitSeconds + " s" + Failure(network));
                    return false;
                }

                double finding = Seconds() - start;
                if (privateCode != null)
                {
                    bool mixed = FreeOf(seen, privateCode) >= 0;
                    PeerLookup byCode = await LookUpAsync(network, code, ct);
                    network.Unlist(privateCode);
                    Note("separation: a private room among the tables=" + mixed + ", a room lookup of a table's code=" + byCode.Error);
                    if (mixed)
                    {
                        Fail("separation", "a private room showed among the Quick Match tables");
                        return false;
                    }
                }

                start = Seconds();
                network.SetQuickFreeSeats(code, 1);
                QuickTableSearch one = await SearchUntilAsync(network, search => FreeOf(search, code) == 1, ct);
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                if (FreeOf(one, code) != 1)
                {
                    Fail("quick round " + round + ": FREE 1", "not seen within " + QuickWaitSeconds + " s" + Failure(network));
                    return false;
                }

                double toOne = Seconds() - start;
                start = Seconds();
                network.SetQuickFreeSeats(code, 0);
                QuickTableSearch none = await SearchUntilAsync(network, search => FreeOf(search, code) < 0, ct);
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                if (none.Error != RoomError.None || FreeOf(none, code) >= 0)
                {
                    Fail("quick round " + round + ": FREE 0", "still found after " + QuickWaitSeconds + " s" + Failure(network));
                    return false;
                }

                double toGone = Seconds() - start;
                network.UnlistQuick(code);
                listings.Add(listing);
                found.Add(finding);
                ones.Add(toOne);
                gone.Add(toGone);
                Note("quick round " + round + ": listed in " + Secs(listing) + ", found after " + Secs(finding)
                    + ", FREE 1 seen after " + Secs(toOne) + ", FREE 0 gone after " + Secs(toGone));
            }

            Note("quick spread over " + QuickRounds + " rounds: listing " + Spread(listings) + ", finding " + Spread(found)
                + ", FREE 1 " + Spread(ones) + ", FREE 0 gone " + Spread(gone) + "; one search " + Spread(SearchTimes)
                + " (median " + Secs(Median(SearchTimes)) + ", " + SearchTimes.Count + " searches, " + _failedSearches + " failed)"
                + "; last EOS refusal: " + (network.LastFailure ?? "none"));
            return true;
        }

        /// <summary>Searches for tables until <paramref name="settled"/> holds of a search that worked, or the wait runs out.</summary>
        private static async Task<QuickTableSearch> SearchUntilAsync(EosPeerNetwork network, Func<QuickTableSearch, bool> settled, CancellationToken ct)
        {
            double deadline = Seconds() + QuickWaitSeconds;
            while (true)
            {
                double start = Seconds();
                QuickTableSearch search;
                using (CancellationTokenSource step = Step(ct))
                {
                    search = await network.FindQuickTablesAsync(step.Token);
                }

                SearchTimes.Add(Seconds() - start);
                if (search.Error != RoomError.None)
                {
                    _failedSearches++;
                }
                else if (settled(search))
                {
                    return search;
                }

                if (Seconds() >= deadline || ct.IsCancellationRequested)
                {
                    return search;
                }

                await Task.Delay(QuickPollMilliseconds, ct);
            }
        }

        /// <summary>The free seats the search shows for <paramref name="code"/>'s table; -1 when it is not among them.</summary>
        private static int FreeOf(QuickTableSearch search, string code)
        {
            if (search.Tables == null)
            {
                return -1;
            }

            foreach (QuickTable table in search.Tables)
            {
                if (table.RoomCode == code)
                {
                    return table.FreeSeats;
                }
            }

            return -1;
        }

        private static async Task<RoomError> ListQuickAsync(EosPeerNetwork network, string code, int freeSeats, CancellationToken ct)
        {
            using (CancellationTokenSource step = Step(ct))
            {
                return await network.ListQuickAsync(code, freeSeats, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), step.Token);
            }
        }

        private static string Secs(double seconds) => seconds.ToString("0.0") + "s";

        private static string Spread(List<double> values)
        {
            if (values.Count == 0)
            {
                return "-";
            }

            double low = double.MaxValue;
            double high = double.MinValue;
            foreach (double value in values)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }

            return low.ToString("0.0") + "-" + high.ToString("0.0") + "s";
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

            var sorted = new List<double>(values);
            sorted.Sort();
            return sorted[sorted.Count / 2];
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
