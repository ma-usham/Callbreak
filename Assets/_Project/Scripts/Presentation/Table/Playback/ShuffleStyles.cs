using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// The ways the deck in the middle of the table is shuffled before a deal (the owner, 6 Oct
    /// 2026: "add some random shuffle"; ADR 039). Each round shows one, chosen at random and never
    /// the one before. Every style lasts <see cref="ShuffleAnimation.Seconds"/>, so the deal and the
    /// bots' wait for it are the same whichever shows.
    /// </summary>
    public enum ShuffleStyle
    {
        /// <summary>The deck splits in two and the halves riffle together (ADR 022).</summary>
        Riffle = 0,

        /// <summary>Small packets drop off the top of the deck onto a new pile beside it.</summary>
        Overhand = 1,

        /// <summary>Three packets slide apart and stack again in a new order.</summary>
        Cut = 2,

        /// <summary>The cards spread over the felt, swirl round and gather into a deck.</summary>
        Wash = 3,
    }

    /// <summary>Picks the shuffle for a deal and builds its animation.</summary>
    public static class ShuffleStyles
    {
        /// <summary>Every style, in <see cref="ShuffleStyle"/> order.</summary>
        public static IReadOnlyList<ShuffleStyle> All { get; } = new ReadOnlyCollection<ShuffleStyle>(new[]
        {
            ShuffleStyle.Riffle, ShuffleStyle.Overhand, ShuffleStyle.Cut, ShuffleStyle.Wash,
        });

        /// <summary>A style chosen at random: any for the first deal, else any but <paramref name="last"/>.</summary>
        public static ShuffleStyle Next(ShuffleStyle? last, System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (last == null)
            {
                return All[random.Next(All.Count)];
            }

            return All[((int)last.Value + 1 + random.Next(All.Count - 1)) % All.Count];
        }

        /// <param name="style">How the deck is shuffled.</param>
        /// <param name="pile">The deck in the middle, already laid out.</param>
        /// <param name="onShuffle">Called once as the cards start to move between piles (the shuffle sound).</param>
        /// <param name="random">Varies the packets and the spread; null for a fixed look.</param>
        public static IEventAnimation Create(ShuffleStyle style, DeckPile pile, Action onShuffle, System.Random random)
        {
            switch (style)
            {
                case ShuffleStyle.Overhand:
                    return new OverhandShuffle(pile, onShuffle, random);
                case ShuffleStyle.Cut:
                    return new CutShuffle(pile, onShuffle, random);
                case ShuffleStyle.Wash:
                    return new WashShuffle(pile, onShuffle, random);
                default:
                    return new ShuffleAnimation(pile, onShuffle);
            }
        }
    }

    /// <summary>What the shuffles share: the deck dropping onto the felt, small hops, and the squared end.</summary>
    internal static class ShuffleMotion
    {
        /// <summary>Every shuffle starts as the riffle does: the deck drops onto the felt as it fades in.</summary>
        public const float AppearEnd = 0.15f;

        /// <summary>How far the deck drops, canvas units.</summary>
        public const float Lift = 18f;

        /// <summary>How far a half of the riffle slides aside, canvas units.</summary>
        public const float Spread = 92f;

        public static void Appear(DeckPile pile, float time)
        {
            float t = EaseUtility.Evaluate(Mathf.Clamp01(time / AppearEnd), Ease.OutQuad);
            for (int i = 0; i < pile.Count; i++)
            {
                CardPlacement rest = pile.Resting(i);
                pile[i].SetAlpha(t);
                pile[i].Placement = new CardPlacement(rest.Position + new Vector2(0f, Lift * (1f - t)), rest.Rotation, rest.Scale, rest.Flat);
            }
        }

        /// <summary>The placement moved by <paramref name="offset"/> and turned by <paramref name="turn"/> degrees.</summary>
        public static CardPlacement Moved(in CardPlacement placement, Vector2 offset, float turn)
        {
            return new CardPlacement(placement.Position + offset, placement.Rotation + turn, placement.Scale, placement.Flat);
        }

        /// <summary>Between two placements along a hop that is <paramref name="height"/> up at its middle.</summary>
        public static CardPlacement Hop(in CardPlacement from, in CardPlacement to, float t, float height)
        {
            t = Mathf.Clamp01(t);
            CardPlacement between = CardPlacement.Lerp(from, to, t);
            return new CardPlacement(between.Position + new Vector2(0f, height * Mathf.Sin(Mathf.PI * t)), between.Rotation, between.Scale, between.Flat);
        }

        /// <summary>The fraction of a phase from <paramref name="start"/> to <paramref name="end"/>, eased.</summary>
        public static float Phase(float time, float start, float end, Ease ease)
        {
            return EaseUtility.Evaluate(Mathf.Clamp01((time - start) / (end - start)), ease);
        }

        /// <summary>The deck in its new order, squared, every card at rest and opaque, ready to be dealt.</summary>
        public static void Square(DeckPile pile, List<CardView> bottomFirst)
        {
            pile.Reorder(bottomFirst);
            for (int i = 0; i < pile.Count; i++)
            {
                pile[i].SetAlpha(1f);
                pile[i].Placement = pile.Resting(i);
            }
        }
    }

    /// <summary>
    /// The overhand shuffle: the deck slides to the left, packets of 6 to 11 cards leave its top one
    /// after another in little hops and land on a new pile to the right, so the first packet ends at
    /// the bottom; then the new pile slides back to the middle.
    /// </summary>
    public sealed class OverhandShuffle : IEventAnimation
    {
        private const float HoldEnd = 0.32f;
        private const float PacketsEnd = 0.95f;
        private const float PacketSeconds = 0.16f;
        private const float HeldTurn = 5f;
        private const float DroppedTurn = -4f;

        private static readonly Vector2 Held = new Vector2(-ShuffleMotion.Spread * 0.6f, 0f);
        private static readonly Vector2 Dropped = new Vector2(ShuffleMotion.Spread * 0.6f, 0f);

        private readonly DeckPile _pile;
        private readonly Action _onShuffle;
        private readonly System.Random _random;
        private readonly List<CardView> _order = new List<CardView>(52);
        private readonly List<int> _packetStarts = new List<int>();
        private int[] _packetOf = Array.Empty<int>();
        private int[] _newIndex = Array.Empty<int>();
        private float _elapsed;
        private int _raised;
        private bool _sounded;
        private bool _finished;

        public OverhandShuffle(DeckPile pile, Action onShuffle, System.Random random)
        {
            _pile = pile;
            _onShuffle = onShuffle;
            _random = random;
        }

        public float Duration => ShuffleAnimation.Seconds;

        public void Begin()
        {
            int count = _pile.Count;
            _packetOf = new int[count];
            _newIndex = new int[count];
            _order.Clear();
            _packetStarts.Clear();

            // Packets come off the top of the deck; each lands on the ones before it.
            int top = count;
            while (top > 0)
            {
                int size = Math.Min(top, 6 + (_random != null ? _random.Next(6) : (_packetStarts.Count * 3) % 6));
                int from = top - size;
                _packetStarts.Add(_order.Count);
                for (int i = from; i < top; i++)
                {
                    _packetOf[i] = _packetStarts.Count - 1;
                    _newIndex[i] = _order.Count;
                    _order.Add(_pile[i]);
                }

                top = from;
            }

            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Apply(_elapsed);
            if (_elapsed < ShuffleAnimation.Seconds)
            {
                return false;
            }

            Finish();
            return true;
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (_order.Count != _pile.Count)
            {
                Begin();
            }

            ShuffleMotion.Square(_pile, _order);
        }

        private float PacketStart(int packet)
        {
            int packets = _packetStarts.Count;
            float spacing = packets > 1 ? (PacketsEnd - PacketSeconds - HoldEnd) / (packets - 1) : 0f;
            return HoldEnd + (packet * spacing);
        }

        private void Apply(float time)
        {
            if (time < ShuffleMotion.AppearEnd)
            {
                ShuffleMotion.Appear(_pile, time);
                return;
            }

            if (time >= HoldEnd && !_sounded)
            {
                _sounded = true;
                _onShuffle?.Invoke();
            }

            // A packet in the air draws over the deck it left and the pile it lands on.
            while (_raised < _packetStarts.Count && time >= PacketStart(_raised))
            {
                int end = _raised + 1 < _packetStarts.Count ? _packetStarts[_raised + 1] : _order.Count;
                for (int k = _packetStarts[_raised]; k < end; k++)
                {
                    _order[k].Rect.SetAsLastSibling();
                }

                if (_raised == 0 && _order.Count > 0)
                {
                    _order[0].SetShadow(true);
                }

                _raised++;
            }

            for (int i = 0; i < _pile.Count; i++)
            {
                CardPlacement rest = _pile.Resting(i);
                CardPlacement held = ShuffleMotion.Moved(rest, Held, HeldTurn);
                CardView card = _pile[i];
                card.SetAlpha(1f);
                if (time < HoldEnd)
                {
                    card.Placement = CardPlacement.Lerp(rest, held, ShuffleMotion.Phase(time, ShuffleMotion.AppearEnd, HoldEnd, Ease.OutCubic));
                    continue;
                }

                CardPlacement squared = _pile.Resting(_newIndex[i]);
                CardPlacement dropped = ShuffleMotion.Moved(squared, Dropped, DroppedTurn);
                float start = PacketStart(_packetOf[i]);
                if (time < start)
                {
                    card.Placement = held;
                }
                else if (time < start + PacketSeconds)
                {
                    card.Placement = ShuffleMotion.Hop(held, dropped, ShuffleMotion.Phase(time, start, start + PacketSeconds, Ease.OutQuad), ShuffleMotion.Lift * 1.4f);
                }
                else if (time < PacketsEnd)
                {
                    card.Placement = dropped;
                }
                else
                {
                    card.Placement = CardPlacement.Lerp(dropped, squared, ShuffleMotion.Phase(time, PacketsEnd, ShuffleAnimation.Seconds, Ease.OutCubic));
                }
            }
        }
    }

    /// <summary>
    /// The three-way cut: the deck's top third slides up, its middle third to the left and its
    /// bottom third to the right, each a little stack of its own; then the left packet comes back
    /// to the middle, the right one lands on it and the top one drops on both.
    /// </summary>
    public sealed class CutShuffle : IEventAnimation
    {
        private const float SplitEnd = 0.45f;
        private const float FlySeconds = 0.18f;
        private const float MiddleBack = 0.5f;
        private const float BottomBack = 0.66f;
        private const float TopBack = 0.82f;

        private static readonly Vector2 Up = new Vector2(0f, ShuffleMotion.Lift * 2.4f);
        private static readonly Vector2 Left = new Vector2(-ShuffleMotion.Spread, -4f);
        private static readonly Vector2 Right = new Vector2(ShuffleMotion.Spread, -4f);

        private readonly DeckPile _pile;
        private readonly Action _onShuffle;
        private readonly System.Random _random;
        private readonly List<CardView> _order = new List<CardView>(52);
        private int _firstMiddle;
        private int _firstTop;
        private bool _sounded;
        private bool _restacked;
        private bool _finished;
        private float _elapsed;

        public CutShuffle(DeckPile pile, Action onShuffle, System.Random random)
        {
            _pile = pile;
            _onShuffle = onShuffle;
            _random = random;
        }

        public float Duration => ShuffleAnimation.Seconds;

        public void Begin()
        {
            int count = _pile.Count;
            int wobble = count >= 9 ? 2 : 0;
            _firstMiddle = Mathf.Clamp((count / 3) + Wobble(wobble), 0, count);
            _firstTop = Mathf.Clamp((2 * count / 3) + Wobble(wobble), _firstMiddle, count);

            // The new order, bottom first: the middle packet, then the bottom one, then the top one.
            _order.Clear();
            for (int i = _firstMiddle; i < _firstTop; i++)
            {
                _order.Add(_pile[i]);
            }

            for (int i = 0; i < _firstMiddle; i++)
            {
                _order.Add(_pile[i]);
            }

            for (int i = _firstTop; i < count; i++)
            {
                _order.Add(_pile[i]);
            }

            // Each packet lies on its own shadow while it is apart.
            if (_firstMiddle < count)
            {
                _pile[_firstMiddle].SetShadow(true);
            }

            if (_firstTop < count)
            {
                _pile[_firstTop].SetShadow(true);
            }

            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Apply(_elapsed);
            if (_elapsed < ShuffleAnimation.Seconds)
            {
                return false;
            }

            Finish();
            return true;
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (_order.Count != _pile.Count)
            {
                Begin();
            }

            ShuffleMotion.Square(_pile, _order);
        }

        private int Wobble(int range) => range > 0 && _random != null ? _random.Next(-range, range + 1) : 0;

        /// <summary>Where a card lies while its packet is apart: the packet's own stack at its spot.</summary>
        private CardPlacement Apart(int index)
        {
            if (index >= _firstTop)
            {
                return ShuffleMotion.Moved(_pile.Resting(index - _firstTop), Up, -2f);
            }

            return index >= _firstMiddle
                ? ShuffleMotion.Moved(_pile.Resting(index - _firstMiddle), Left, 6f)
                : ShuffleMotion.Moved(_pile.Resting(index), Right, -6f);
        }

        /// <summary>When a card's packet flies back to the middle.</summary>
        private float BackAt(int index) => index >= _firstTop ? TopBack : index >= _firstMiddle ? MiddleBack : BottomBack;

        /// <summary>Where a card ends: its place in the new order.</summary>
        private int NewIndex(int index)
        {
            if (index >= _firstTop)
            {
                return index;
            }

            int middle = _firstTop - _firstMiddle;
            return index >= _firstMiddle ? index - _firstMiddle : middle + index;
        }

        private void Apply(float time)
        {
            if (time < ShuffleMotion.AppearEnd)
            {
                ShuffleMotion.Appear(_pile, time);
                return;
            }

            if (time >= SplitEnd && !_sounded)
            {
                _sounded = true;
                _onShuffle?.Invoke();
            }

            if (time >= MiddleBack && !_restacked)
            {
                // The packets come back in their new order, each drawn over the ones before it.
                _restacked = true;
                for (int k = 0; k < _order.Count; k++)
                {
                    _order[k].Rect.SetAsLastSibling();
                }
            }

            for (int i = 0; i < _pile.Count; i++)
            {
                CardView card = _pile[i];
                card.SetAlpha(1f);
                CardPlacement apart = Apart(i);
                if (time < SplitEnd)
                {
                    card.Placement = CardPlacement.Lerp(_pile.Resting(i), apart, ShuffleMotion.Phase(time, ShuffleMotion.AppearEnd, SplitEnd, Ease.OutCubic));
                    continue;
                }

                float back = BackAt(i);
                card.Placement = time < back
                    ? apart
                    : ShuffleMotion.Hop(apart, _pile.Resting(NewIndex(i)), ShuffleMotion.Phase(time, back, back + FlySeconds, Ease.OutQuad), ShuffleMotion.Lift * 0.8f);
            }
        }
    }

    /// <summary>
    /// The wash: the cards spread over the felt in a loose ring, each turned its own way, swirl
    /// round together, and gather back into a squared deck in the order the swirl left them.
    /// </summary>
    public sealed class WashShuffle : IEventAnimation
    {
        private const float SpreadEnd = 0.42f;
        private const float SwirlEnd = 0.82f;
        private const float Swirl = 1.1f;
        private const float SwirlTurn = 35f;

        private readonly DeckPile _pile;
        private readonly Action _onShuffle;
        private readonly System.Random _random;
        private readonly List<CardView> _order = new List<CardView>(52);
        private float[] _angle = Array.Empty<float>();
        private float[] _radius = Array.Empty<float>();
        private float[] _turn = Array.Empty<float>();
        private float[] _phase = Array.Empty<float>();
        private int[] _newIndex = Array.Empty<int>();
        private bool _spread;
        private bool _gathering;
        private bool _finished;
        private float _elapsed;

        public WashShuffle(DeckPile pile, Action onShuffle, System.Random random)
        {
            _pile = pile;
            _onShuffle = onShuffle;
            _random = random;
        }

        public float Duration => ShuffleAnimation.Seconds;

        public void Begin()
        {
            int count = _pile.Count;
            _angle = new float[count];
            _radius = new float[count];
            _turn = new float[count];
            _phase = new float[count];
            _newIndex = new int[count];
            var random = _random ?? new System.Random(count);
            for (int i = 0; i < count; i++)
            {
                _angle[i] = (float)(random.NextDouble() * Math.PI * 2.0);
                _radius[i] = 28f + ((float)random.NextDouble() * 96f);
                _turn[i] = -50f + ((float)random.NextDouble() * 100f);
                _phase[i] = (float)random.NextDouble();
            }

            // The deck gathers in the order the swirl leaves the cards round the middle.
            var byAngle = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                byAngle.Add(i);
            }

            byAngle.Sort((a, b) => Mathf.Repeat(_angle[a] + Swirl, Mathf.PI * 2f).CompareTo(Mathf.Repeat(_angle[b] + Swirl, Mathf.PI * 2f)));
            _order.Clear();
            for (int k = 0; k < count; k++)
            {
                _newIndex[byAngle[k]] = k;
                _order.Add(_pile[byAngle[k]]);
            }

            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Apply(_elapsed);
            if (_elapsed < ShuffleAnimation.Seconds)
            {
                return false;
            }

            Finish();
            return true;
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (_order.Count != _pile.Count)
            {
                Begin();
            }

            ShuffleMotion.Square(_pile, _order);
        }

        /// <summary>A card on the felt: round the middle at its angle and radius, in the table's perspective (wider than deep).</summary>
        private CardPlacement OnFelt(int index, float swirl)
        {
            float angle = _angle[index] + (Swirl * swirl);
            float radius = _radius[index] * (1f + (0.1f * Mathf.Sin(2f * Mathf.PI * (swirl + _phase[index]))));
            CardPlacement rest = _pile.Resting(index);
            var at = new Vector2(Mathf.Cos(angle) * radius * 1.3f, Mathf.Sin(angle) * radius * 0.75f);
            return new CardPlacement(at, rest.Rotation + _turn[index] + (SwirlTurn * swirl), rest.Scale, rest.Flat);
        }

        private void Apply(float time)
        {
            if (time < ShuffleMotion.AppearEnd)
            {
                ShuffleMotion.Appear(_pile, time);
                return;
            }

            if (!_spread)
            {
                // Apart, every card lies on its own shadow; the deck's own comes back as it squares.
                _spread = true;
                _onShuffle?.Invoke();
                for (int i = 0; i < _pile.Count; i++)
                {
                    _pile[i].SetShadow(true);
                }
            }

            if (time >= SwirlEnd && !_gathering)
            {
                _gathering = true;
                for (int k = 0; k < _order.Count; k++)
                {
                    _order[k].Rect.SetAsLastSibling();
                }
            }

            for (int i = 0; i < _pile.Count; i++)
            {
                CardView card = _pile[i];
                card.SetAlpha(1f);
                if (time < SpreadEnd)
                {
                    card.Placement = CardPlacement.Lerp(_pile.Resting(i), OnFelt(i, 0f), ShuffleMotion.Phase(time, ShuffleMotion.AppearEnd, SpreadEnd, Ease.OutCubic));
                }
                else if (time < SwirlEnd)
                {
                    card.Placement = OnFelt(i, ShuffleMotion.Phase(time, SpreadEnd, SwirlEnd, Ease.InOutSine));
                }
                else
                {
                    card.Placement = CardPlacement.Lerp(OnFelt(i, 1f), _pile.Resting(_newIndex[i]), ShuffleMotion.Phase(time, SwirlEnd, ShuffleAnimation.Seconds, Ease.OutCubic));
                }
            }
        }
    }
}
