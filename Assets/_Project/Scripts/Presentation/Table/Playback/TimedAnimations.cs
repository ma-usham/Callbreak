using System;
using System.Collections.Generic;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// A beat in which nothing moves: the 0.4 s "You lead" pause before the first trick (GDD §11
    /// screen 07), or the time a call bubble needs to pop (300 ms) before the next event plays.
    /// </summary>
    public sealed class Beat : IEventAnimation
    {
        private float _elapsed;

        public Beat(float seconds)
        {
            Duration = Math.Max(0f, seconds);
        }

        public float Duration { get; }

        public void Begin()
        {
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            return _elapsed >= Duration;
        }

        public void Finish()
        {
            _elapsed = Duration;
        }
    }

    /// <summary>Several animations at once (the four cards of a sweep); it lasts as long as the longest.</summary>
    public sealed class Together : IEventAnimation
    {
        private readonly List<IEventAnimation> _parts = new List<IEventAnimation>(4);
        private readonly List<bool> _done = new List<bool>(4);

        public float Duration { get; private set; }

        public int Count => _parts.Count;

        public Together Add(IEventAnimation part)
        {
            if (part != null)
            {
                _parts.Add(part);
                _done.Add(false);
                Duration = Math.Max(Duration, part.Duration);
            }

            return this;
        }

        public void Begin()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                _parts[i].Begin();
            }
        }

        public bool Tick(float deltaTime)
        {
            bool all = true;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!_done[i])
                {
                    _done[i] = _parts[i].Tick(deltaTime);
                }

                all &= _done[i];
            }

            return all;
        }

        public void Finish()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!_done[i])
                {
                    _parts[i].Finish();
                    _done[i] = true;
                }
            }
        }
    }

    /// <summary>Animations one after another (hold the finished trick, then sweep it).</summary>
    public sealed class InOrder : IEventAnimation
    {
        private readonly List<IEventAnimation> _steps = new List<IEventAnimation>(3);
        private int _index;
        private bool _begun;

        public float Duration { get; private set; }

        public InOrder Then(IEventAnimation step)
        {
            if (step != null)
            {
                _steps.Add(step);
                Duration += step.Duration;
            }

            return this;
        }

        public void Begin()
        {
            _begun = true;
            if (_steps.Count > 0)
            {
                _steps[0].Begin();
            }
        }

        public bool Tick(float deltaTime)
        {
            if (!_begun)
            {
                Begin();
            }

            while (_index < _steps.Count)
            {
                if (!_steps[_index].Tick(deltaTime))
                {
                    return false;
                }

                // The next step starts in the same frame, on a clean clock.
                deltaTime = 0f;
                _index++;
                if (_index < _steps.Count)
                {
                    _steps[_index].Begin();
                }
            }

            return true;
        }

        public void Finish()
        {
            for (; _index < _steps.Count; _index++)
            {
                _steps[_index].Finish();
            }
        }
    }

    /// <summary>Runs a callback once, as a step of a sequence (start the sweep's sound, light the winning card).</summary>
    public sealed class Instant : IEventAnimation
    {
        private readonly Action _action;
        private bool _ran;

        public Instant(Action action)
        {
            _action = action;
        }

        public float Duration => 0f;

        public void Begin()
        {
            Run();
        }

        public bool Tick(float deltaTime)
        {
            Run();
            return true;
        }

        public void Finish()
        {
            Run();
        }

        private void Run()
        {
            if (_ran)
            {
                return;
            }

            _ran = true;
            _action?.Invoke();
        }
    }
}
