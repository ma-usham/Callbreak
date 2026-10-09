using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Coins collected (the owner, 8 Oct 2026): gold coins pop out of a reward's coin pile, arc up to
    /// the wallet's coin and land in it one after another, each landing with a small bump of the
    /// wallet coin. It lies over the whole screen, last, so the coins fly over everything, and takes
    /// no touches. The coins are made the first time they are needed and kept for the next flight.
    /// Everything runs on unscaled time. With Reduce motion nothing flies: the flight is over at once.
    /// </summary>
    public sealed class CoinFlight : MonoBehaviour
    {
        /// <summary>How long a coin takes from the pile to the wallet, and its pop out of the pile within that.</summary>
        public const float FlightSeconds = 0.75f;
        public const float PopSeconds = 0.2f;

        /// <summary>Between two coins leaving the pile.</summary>
        public const float CoinGap = 0.06f;

        /// <summary>How far a coin's arc bends to the side, as a part of the distance it flies.</summary>
        private const float ArcBend = 0.22f;

        /// <summary>How far a coin pops out of the pile, in the layer's units.</summary>
        private const float PopDistance = 70f;

        [SerializeField] private Sprite _coin;

        private readonly System.Random _random = new System.Random(26);
        private Image[] _coins = Array.Empty<Image>();
        private MotionHandle[] _motions = Array.Empty<MotionHandle>();
        private MotionHandle _bump;
        private RectTransform _target;
        private int _flying;
        private int _landed;
        private int _count;
        private Action<int, int> _onLanded;

        /// <summary>A flight is under way.</summary>
        public bool IsPlaying => _flying > 0;

        /// <summary>How many coins fly for a reward of <paramref name="amount"/>: 6 to 12, more for more.</summary>
        public static int CountFor(int amount) => Mathf.Clamp(amount / 5, 6, 12);

        /// <summary>
        /// Flies <paramref name="count"/> coins from <paramref name="from"/> into <paramref name="to"/>.
        /// <paramref name="onLanded"/> hears each landing, with how many have landed and how many fly;
        /// the last call has both equal. A flight still under way is finished first. With
        /// <paramref name="reduceMotion"/>, or without the parts it needs, the one call comes at once.
        /// </summary>
        public void Play(RectTransform from, RectTransform to, int count, bool reduceMotion, Action<int, int> onLanded)
        {
            Finish();
            count = Mathf.Max(1, count);
            if (reduceMotion || from == null || to == null || _coin == null || !isActiveAndEnabled)
            {
                onLanded?.Invoke(count, count);
                return;
            }

            Prepare(count);
            var layer = (RectTransform)transform;
            Vector2 start = LocalCentre(layer, from);
            Vector2 end = LocalCentre(layer, to);
            Vector2 size = to.rect.size;
            Vector2 pileSize = from.rect.size;
            _target = to;
            _onLanded = onLanded;
            _count = count;
            _landed = 0;
            _flying = count;
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = new Vector2(Spread() * pileSize.x * 0.3f, Spread() * pileSize.y * 0.25f);
                Vector2 popTo = start + jitter + new Vector2(Spread() * PopDistance, (0.6f + (0.4f * Next())) * PopDistance);
                float bend = (Next() < 0.5f ? -1f : 1f) * ArcBend * (0.6f + (0.4f * Next()));
                var flight = new Flight(_coins[i], start + jitter, popTo, end, size, bend);
                flight.Apply(0f);
                _coins[i].gameObject.SetActive(true);
                _motions[i] = LMotion.Create(0f, 1f, FlightSeconds)
                    .WithDelay(i * CoinGap)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithOnComplete(() => Land(flight))
                    .Bind(flight, (t, f) => f.Apply(t));
            }
        }

        /// <summary>Ends a flight under way at once: every coin lands now, and the last landing is heard.</summary>
        public void Finish()
        {
            for (int i = 0; i < _motions.Length; i++)
            {
                UiMotion.Finish(ref _motions[i]);
            }

            UiMotion.Finish(ref _bump);
        }

        private void OnDisable() => Finish();

        private void Land(Flight flight)
        {
            flight.Coin.gameObject.SetActive(false);
            _landed++;
            _flying = Mathf.Max(0, _flying - 1);
            if (_target != null)
            {
                UiMotion.Stop(ref _bump);
                _target.localScale = Vector3.one;
                _bump = UiMotion.Pulse(_target, false, 0.18f, 0.16f);
            }

            Action<int, int> landed = _onLanded;
            if (_flying == 0)
            {
                _onLanded = null;
            }

            landed?.Invoke(_landed, _count);
        }

        private void Prepare(int count)
        {
            if (_coins.Length >= count)
            {
                return;
            }

            int had = _coins.Length;
            Array.Resize(ref _coins, count);
            Array.Resize(ref _motions, count);
            for (int i = had; i < count; i++)
            {
                var coin = new GameObject("Coin" + i, typeof(RectTransform));
                var rect = (RectTransform)coin.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                var image = coin.AddComponent<Image>();
                image.sprite = _coin;
                image.raycastTarget = false;
                image.preserveAspect = true;
                coin.SetActive(false);
                _coins[i] = image;
            }
        }

        /// <summary>A rect's centre in the layer's own space (the layer is anchored at the screen's middle).</summary>
        private static Vector2 LocalCentre(RectTransform layer, RectTransform rect)
        {
            Vector3 world = rect.TransformPoint(rect.rect.center);
            Vector3 local = layer.InverseTransformPoint(world);
            return new Vector2(local.x, local.y) - layer.rect.center;
        }

        private float Next() => (float)_random.NextDouble();

        private float Spread() => (Next() * 2f) - 1f;

        /// <summary>One coin's way: popping out of the pile, then an arc into the wallet, growing to the wallet coin's size.</summary>
        private sealed class Flight
        {
            public readonly Image Coin;
            private readonly RectTransform _rect;
            private readonly Vector2 _start;
            private readonly Vector2 _pop;
            private readonly Vector2 _end;
            private readonly Vector2 _control;
            private readonly Vector2 _size;

            public Flight(Image coin, Vector2 start, Vector2 pop, Vector2 end, Vector2 size, float bend)
            {
                Coin = coin;
                _rect = (RectTransform)coin.transform;
                _start = start;
                _pop = pop;
                _end = end;
                _size = size;
                Vector2 way = end - pop;
                Vector2 side = new Vector2(-way.y, way.x);
                _control = ((pop + end) / 2f) + (side * bend);
            }

            public void Apply(float t)
            {
                float seconds = t * FlightSeconds;
                Vector2 position;
                float scale;
                if (seconds < PopSeconds)
                {
                    // Out of the pile: a quick rise that slows, the coin growing as it comes out.
                    float p = 1f - Mathf.Pow(1f - (seconds / PopSeconds), 3f);
                    position = Vector2.LerpUnclamped(_start, _pop, p);
                    scale = Mathf.Lerp(0.45f, 1.15f, p);
                }
                else
                {
                    // Into the wallet: slow at first, fast at the end, along a gentle arc.
                    float u = (seconds - PopSeconds) / (FlightSeconds - PopSeconds);
                    float e = u * u * (3f - (2f * u));
                    e = e * e;
                    float a = 1f - e;
                    position = (a * a * _pop) + (2f * a * e * _control) + (e * e * _end);
                    scale = Mathf.Lerp(1.15f, 1f, e);
                }

                _rect.anchoredPosition = position;
                _rect.sizeDelta = _size * scale;
            }
        }
    }
}
