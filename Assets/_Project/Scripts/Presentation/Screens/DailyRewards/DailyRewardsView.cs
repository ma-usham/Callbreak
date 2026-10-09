using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 26, Daily Rewards (mockup 26; the owner, 8 Oct 2026): the week's seven day tiles, "Day
    /// n of 7", the crimson Claim button and the line under it, with the wallet at the top right.
    /// Today's tile, while its reward is ready, wears the crimson frame and the light behind its
    /// coins and its name in crimson (the others' names are brown; Day 7's, the weekly bonus, is
    /// always crimson, as the mockup draws them); a claimed day carries the gold check. Renders what
    /// its presenter gives it and reports Back and Claim. A Claim button that is not interactable
    /// shows its disabled look with its label dimmed. A claim's coins fly from today's pile into the
    /// wallet, which counts up as they land (<see cref="CollectCoins"/>).
    /// </summary>
    public sealed class DailyRewardsView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _claimButton;
        [SerializeField] private TMP_Text _balance;
        [SerializeField] private TMP_Text _dayHeading;
        [SerializeField] private TMP_Text[] _dayLabel;
        [SerializeField] private TMP_Text[] _amount;

        // Each tile's looks: today's frame and light, and the claimed check.
        [SerializeField] private GameObject[] _todayFrame;
        [SerializeField] private GameObject[] _coinGlow;
        [SerializeField] private GameObject[] _claimedBadge;

        [SerializeField] private TMP_Text _claimLabel;
        [SerializeField] private TMP_Text _footer;

        // Coins collected (the owner, 8 Oct 2026): the flight over the screen, the wallet's coin it
        // lands in, and each day's coin pile, where the coins come from.
        [SerializeField] private CoinFlight _coinFlight;
        [SerializeField] private RectTransform _walletCoin;
        [SerializeField] private RectTransform[] _coins;

        // The wallet's count-up while the coins land.
        private int _countFrom;
        private int _countTo;
        private bool _firstLanded;

        // As built (read before the first change): today's crimson day name (Day 1's) and the brown of the others (Day 2's).
        private bool _designRead;
        private Color _todayName;
        private Color _otherName;

        public event Action BackClicked;
        public event Action ClaimClicked;

        /// <summary>The first collected coin reached the wallet: the coin sound.</summary>
        public event Action CoinsLanding;

        /// <summary>Every collected coin has landed and the wallet shows the new balance.</summary>
        public event Action CoinsCollected;

        public int DayCount => _dayLabel.Length;

        public void SetBalance(string coins) => SetText(_balance, coins);

        /// <summary>"Day 1 of 7".</summary>
        public void SetHeading(string heading) => SetText(_dayHeading, heading);

        /// <summary>One day tile: its title ("Day 1"), its amount ("+10 coins") and its look.</summary>
        public void SetDay(int index, string title, string amount, DayTileState state)
        {
            ReadDesign();
            SetText(_dayLabel[index], title);
            SetText(_amount[index], amount);
            bool today = state == DayTileState.Today;
            if (index < _dayLabel.Length - 1)
            {
                Color name = today ? _todayName : _otherName;
                if (_dayLabel[index].color != name)
                {
                    _dayLabel[index].color = name;
                }
            }

            SetActive(_todayFrame[index], today);
            SetActive(_coinGlow[index], today);
            SetActive(_claimedBadge[index], state == DayTileState.Claimed);
        }

        /// <summary>The Claim button: its label, and whether it takes a tap (only while today's reward is ready).</summary>
        public void SetClaim(string label, bool interactable)
        {
            SetText(_claimLabel, label);
            _claimButton.interactable = interactable;
            Color color = _claimLabel.color;
            float alpha = interactable ? 1f : DimmedAlpha;
            if (!Mathf.Approximately(color.a, alpha))
            {
                color.a = alpha;
                _claimLabel.color = color;
            }
        }

        /// <summary>The line under the button ("Come back tomorrow for Day 2").</summary>
        public void SetFooter(string text) => SetText(_footer, text);

        /// <summary>
        /// Flies coins from day <paramref name="index"/> into the wallet's coin, the wallet's
        /// number counting up from <paramref name="fromBalance"/> to <paramref name="toBalance"/> as they
        /// land (<see cref="CoinFlight"/>). <see cref="CoinsLanding"/> comes with the first landing,
        /// <see cref="CoinsCollected"/> with the last, when the wallet shows <paramref name="toBalance"/>.
        /// With <paramref name="reduceMotion"/> both come at once.
        /// </summary>
        public void CollectCoins(int index, int fromBalance, int toBalance, bool reduceMotion)
        {
            _countFrom = fromBalance;
            _countTo = toBalance;
            _firstLanded = false;
            if (_coinFlight == null)
            {
                OnCoinLanded(1, 1);
                return;
            }

            RectTransform pile = _coins != null && index >= 0 && index < _coins.Length ? _coins[index] : null;
            _coinFlight.Play(pile, _walletCoin, CoinFlight.CountFor(toBalance - fromBalance), reduceMotion, OnCoinLanded);
        }

        private void OnCoinLanded(int landed, int count)
        {
            if (!_firstLanded)
            {
                _firstLanded = true;
                CoinsLanding?.Invoke();
            }

            float part = count > 0 ? Mathf.Clamp01(landed / (float)count) : 1f;
            SetBalance(CoinsFormat.Coins(_countFrom + Mathf.RoundToInt((_countTo - _countFrom) * part)));
            if (landed >= count)
            {
                CoinsCollected?.Invoke();
            }
        }

        /// <summary>Reads the colours the screen was built with, once, before anything has changed.</summary>
        private void ReadDesign()
        {
            if (_designRead)
            {
                return;
            }

            _designRead = true;
            _todayName = _dayLabel[0].color;
            _otherName = _dayLabel.Length > 2 ? _dayLabel[1].color : _todayName;
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _claimButton.onClick.AddListener(OnClaim);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _claimButton.onClick.RemoveListener(OnClaim);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnClaim() => ClaimClicked?.Invoke();
    }
}
