using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IAdRewards"/> over the <c>ad-rewards</c> save file (mockup 22; the designer's
    /// rewarded-coins-prompts.md and its prototype). The three ads of a set pay 20, 30 and 50 coins
    /// into the <see cref="IWallet"/>, each the moment its ad completes. A set's deadline is its first
    /// completed ad plus <see cref="ResetAfter"/>, on the wall clock (UTC) and kept in the file with
    /// the progress, so time away counts and a restart loses nothing. From the deadline on, the set
    /// reads as a new one (no event: a page showing the countdown reads the properties as it ticks);
    /// the coins earned stay in the wallet.
    /// <para>
    /// Nothing extra is handed out. An ad that fails, is cancelled or isn't available credits nothing
    /// and doesn't advance. A second watch while one runs does nothing. An ad that outlasts its set
    /// pays what it was started for, and the next one starts a new set. Progress is saved before the
    /// coins are added, so an app killed in between can't pay the same ad twice. A clock set backwards
    /// never resets the set or moves its deadline: the set waits until the clock reaches the deadline
    /// it was given, so setting the clock back, and forward again, hands out no extra set (the price
    /// is a longer wait while the clock is behind). A clock moved forward can't be told from time
    /// away; only trusted time, with the real ad network's verified rewards, can stop that.
    /// </para>
    /// A file from a newer app version is ignored and never overwritten, and no ad is shown: progress
    /// this build couldn't save would let a set be watched again.
    /// </summary>
    public sealed class AdRewards : IAdRewards
    {
        public const string SaveKey = "ad-rewards";

        /// <summary>How long after its first completed ad a set resets (the designer's spec: five hours).</summary>
        public static readonly TimeSpan ResetAfter = TimeSpan.FromHours(5);

        /// <summary>9999-12-31 23:59:59 UTC, the last second a <see cref="DateTimeOffset"/> can hold.</summary>
        private const long MaxUnixSeconds = 253402300799;

        private static readonly ReadOnlyCollection<int> Payouts = new ReadOnlyCollection<int>(new[] { 20, 30, 50 });

        private readonly IRewardedAds _ads;
        private readonly IWallet _wallet;
        private readonly ISaveStore _store;
        private readonly Func<DateTime> _utcNow;
        private readonly AdRewardsData _data;
        private readonly bool _readOnly;
        private bool _watching;

        /// <param name="utcNow">The wall clock; a time without a kind is taken as UTC.</param>
        public AdRewards(IRewardedAds ads, IWallet wallet, ISaveStore store, Func<DateTime> utcNow)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            AdRewardsData loaded = VersionedSave.Load<AdRewardsData>(store, SaveKey, AdRewardsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new AdRewardsData();
            if (Repair(_data, Now()) && !_readOnly)
            {
                _store.Save(SaveKey, _data);
            }
        }

        public event Action Changed;

        public IReadOnlyList<int> Rewards => Payouts;

        public int Watched => HasExpired(Now()) ? 0 : _data.watched;

        public int NextReward
        {
            get
            {
                int watched = Watched;
                return watched < Payouts.Count ? Payouts[watched] : 0;
            }
        }

        public DateTime? ResetsAtUtc => _data.watched == 0 || HasExpired(Now()) ? (DateTime?)null : FromUnixSeconds(_data.resetsAtUnixSeconds);

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved and no ad is shown.</summary>
        public bool IsReadOnly => _readOnly;

        /// <summary>
        /// Shows the next ad and credits its reward when it completes; 0 when nothing was credited
        /// (every ad of the set watched, no ad available, the ad failed, or a watch already running).
        /// A cancelled watch ends in <see cref="OperationCanceledException"/>, crediting nothing.
        /// </summary>
        public async Task<int> WatchNextAsync(CancellationToken ct)
        {
            if (_watching || _readOnly)
            {
                return 0;
            }

            ct.ThrowIfCancellationRequested();
            int step = Watched;
            if (step >= Payouts.Count || !_ads.IsAvailable)
            {
                return 0;
            }

            bool completed;
            _watching = true;
            try
            {
                completed = await _ads.ShowAsync(ct);
            }
            finally
            {
                _watching = false;
            }

            return completed ? Credit(step) : 0;
        }

        /// <summary>The ad of <paramref name="step"/> completed: saves the progress, then pays.</summary>
        private int Credit(int step)
        {
            if (step == 0)
            {
                // The set's first completed ad starts its five hours.
                _data.resetsAtUnixSeconds = ToUnixSeconds(Now() + ResetAfter);
            }

            _data.watched = step + 1;
            _store.Save(SaveKey, _data);
            int reward = Payouts[step];
            _wallet.Add(reward);
            Changed?.Invoke();
            return reward;
        }

        private bool HasExpired(DateTime now) => _data.watched > 0 && ToUnixSeconds(now) >= _data.resetsAtUnixSeconds;

        private DateTime Now()
        {
            DateTime now = _utcNow();
            return now.Kind == DateTimeKind.Local ? now.ToUniversalTime() : DateTime.SpecifyKind(now, DateTimeKind.Utc);
        }

        private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

        private static DateTime FromUnixSeconds(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

        /// <summary>
        /// Brings a loaded file up to the current schema and makes it consistent. True when the
        /// progress had no usable deadline, which is then set from now: that has to be saved, or each
        /// restart would push it back.
        /// </summary>
        private static bool Repair(AdRewardsData data, DateTime now)
        {
            data.schemaVersion = AdRewardsData.CurrentVersion;
            data.watched = Math.Max(0, Math.Min(data.watched, Payouts.Count));
            if (data.watched == 0)
            {
                data.resetsAtUnixSeconds = 0;
                return false;
            }

            if (data.resetsAtUnixSeconds <= 0 || data.resetsAtUnixSeconds > MaxUnixSeconds)
            {
                // Ads watched but no deadline: the five hours start now, rather than a new set.
                data.resetsAtUnixSeconds = ToUnixSeconds(now + ResetAfter);
                return true;
            }

            return false;
        }
    }
}
