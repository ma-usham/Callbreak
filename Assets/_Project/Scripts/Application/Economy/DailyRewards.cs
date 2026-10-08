using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IDailyRewards"/> over the <c>daily-rewards</c> save file. A day is a calendar day on
    /// the device's own clock, so the next reward comes at local midnight, wherever the player is.
    /// Only the last claim is kept (its day and which day of the week it paid); everything else is
    /// read from it against today:
    /// <list type="bullet">
    /// <item>no claim yet, or the last one two or more days ago (a missed day): Day 1 is ready;</item>
    /// <item>the last claim yesterday: the next day of the week is ready (after Day 7, Day 1);</item>
    /// <item>the last claim today: that day is shown as claimed until midnight;</item>
    /// <item>a clock behind the last claim: nothing is ready until the clock passes that day again,
    /// so setting the clock back, and forward again, hands out no extra day. A clock moved forward
    /// can't be told from time passing; only trusted time could stop that.</item>
    /// </list>
    /// The claim is saved before the coins are added, so an app killed in between can't pay the same
    /// day twice. A file from a newer app version is ignored and never overwritten, and nothing is
    /// claimable: a claim this build couldn't save could be paid again.
    /// </summary>
    public sealed class DailyRewards : IDailyRewards
    {
        public const string SaveKey = "daily-rewards";

        /// <summary>The owner's amounts, 8 Oct 2026: mockup 26's, with Day 7's 75 lowered to 60 (200 a week).</summary>
        private static readonly ReadOnlyCollection<int> Amounts = new ReadOnlyCollection<int>(new[] { 10, 15, 20, 25, 30, 40, 60 });

        private readonly IWallet _wallet;
        private readonly ISaveStore _store;
        private readonly Func<DateTime> _localNow;
        private readonly DailyRewardsData _data;
        private readonly bool _readOnly;

        /// <param name="localNow">The device's wall clock in local time; a UTC time is turned into local time.</param>
        public DailyRewards(IWallet wallet, ISaveStore store, Func<DateTime> localNow)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _localNow = localNow ?? throw new ArgumentNullException(nameof(localNow));
            DailyRewardsData loaded = VersionedSave.Load<DailyRewardsData>(store, SaveKey, DailyRewardsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new DailyRewardsData();
            Repair(_data);
        }

        public event Action Changed;

        public IReadOnlyList<int> Rewards => Amounts;

        public int Day => Today(out _);

        public bool CanClaim
        {
            get
            {
                Today(out bool ready);
                return ready;
            }
        }

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved or claimable.</summary>
        public bool IsReadOnly => _readOnly;

        /// <summary>A calendar day as a whole number of days since 1 Jan 0001; consecutive days differ by one.</summary>
        public static int DayNumber(DateTime local) => (int)(local.Date.Ticks / TimeSpan.TicksPerDay);

        public int Claim()
        {
            int day = Today(out bool ready);
            if (!ready)
            {
                return 0;
            }

            _data.lastClaimDay = TodayNumber();
            _data.lastClaimIndex = day;
            _store.Save(SaveKey, _data);
            int reward = Amounts[day];
            _wallet.Add(reward);
            Changed?.Invoke();
            return reward;
        }

        public bool TakeAutoOpen()
        {
            int today = TodayNumber();
            if (!CanClaim || _data.autoOpenDay >= today)
            {
                return false;
            }

            _data.autoOpenDay = today;
            _store.Save(SaveKey, _data);
            return true;
        }

        /// <summary>Today's day of the week, and whether its reward is ready.</summary>
        private int Today(out bool ready)
        {
            int today = TodayNumber();
            int last = _data.lastClaimDay;
            if (last <= 0 || today > last + 1)
            {
                // No claim yet, or a day missed: the week starts at Day 1.
                ready = !_readOnly;
                return 0;
            }

            if (today == last + 1)
            {
                ready = !_readOnly;
                return (_data.lastClaimIndex + 1) % Amounts.Count;
            }

            // Claimed today, or the clock is behind the last claim.
            ready = false;
            return _data.lastClaimIndex;
        }

        private int TodayNumber()
        {
            DateTime now = _localNow();
            return DayNumber(now.Kind == DateTimeKind.Utc ? now.ToLocalTime() : now);
        }

        /// <summary>Brings a loaded file up to the current schema and into range, in memory.</summary>
        private static void Repair(DailyRewardsData data)
        {
            data.schemaVersion = DailyRewardsData.CurrentVersion;
            data.lastClaimDay = Math.Max(0, data.lastClaimDay);
            data.lastClaimIndex = Math.Max(0, Math.Min(data.lastClaimIndex, Amounts.Count - 1));
            data.autoOpenDay = Math.Max(0, data.autoOpenDay);
        }
    }
}
