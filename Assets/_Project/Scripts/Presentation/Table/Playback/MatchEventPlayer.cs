using System;
using System.Collections.Generic;
using Callbreak.Core.Events;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// Plays the session's events on the table one at a time, in order (GDD §23): a FIFO queue, one
    /// <see cref="IEventAnimation"/> at a time, double speed while more than three events wait
    /// (GDD §6 "a backlog plays at double speed"), and a tap skips any animation longer than 0.3 s.
    /// A resync clears the queue; the table is then rebuilt from the session's view.
    /// </summary>
    public sealed class MatchEventPlayer
    {
        /// <summary>More waiting events than this play at double speed.</summary>
        public const int BacklogForDoubleSpeed = 3;

        /// <summary>Animations longer than this can be skipped with a tap.</summary>
        public const float SkippableAbove = 0.3f;

        public const float BacklogSpeed = 2f;

        private readonly IEventPlayback _playback;
        private readonly Queue<MatchEvent> _queue = new Queue<MatchEvent>();
        private IEventAnimation _animation;
        private MatchEvent _current;

        public MatchEventPlayer(IEventPlayback playback)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        }

        /// <summary>Raised when an event has finished playing, after the views were brought to its state.</summary>
        public event Action<MatchEvent> Played;

        /// <summary>Events waiting behind the one that is playing.</summary>
        public int Backlog => _queue.Count;

        /// <summary>The event on screen now, or null.</summary>
        public MatchEvent Current => _current;

        /// <summary>Nothing is playing and nothing waits: the table shows the model's latest state.</summary>
        public bool IsIdle => _current == null && _queue.Count == 0;

        /// <summary>1, or 2 while the backlog is longer than <see cref="BacklogForDoubleSpeed"/>.</summary>
        public float Speed => _queue.Count > BacklogForDoubleSpeed ? BacklogSpeed : 1f;

        public void Enqueue(MatchEvent e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            _queue.Enqueue(e);
        }

        /// <summary>
        /// Advances playback by <paramref name="deltaTime"/> seconds. Events that show at once are
        /// played through in the same call, until one needs time or the queue is empty.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            while (true)
            {
                if (_current == null)
                {
                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    _current = _queue.Dequeue();
                    _animation = _playback.Begin(_current);
                    if (_animation == null)
                    {
                        Complete();
                        continue;
                    }

                    _animation.Begin();
                }

                if (_animation == null || !_animation.Tick(deltaTime * Speed))
                {
                    return;
                }

                Complete();

                // Whatever time was left in this frame is dropped: the next animation starts clean.
                deltaTime = 0f;
            }
        }

        /// <summary>A tap: cuts the playing animation short if it is longer than 0.3 s. True when it did.</summary>
        public bool Skip()
        {
            if (_animation == null || _animation.Duration <= SkippableAbove)
            {
                return false;
            }

            _animation.Finish();
            Complete();
            return true;
        }

        /// <summary>
        /// Stops the playing animation and drops every waiting event (resync, reconnect, leaving).
        /// Nothing is played or reported: the caller rebuilds the table from the session's view.
        /// </summary>
        public void Clear()
        {
            IEventAnimation animation = _animation;
            _animation = null;
            _current = null;
            _queue.Clear();
            animation?.Finish();
        }

        private void Complete()
        {
            MatchEvent e = _current;
            _animation = null;
            _current = null;
            if (e == null)
            {
                return;
            }

            _playback.End(e);
            Played?.Invoke(e);
        }
    }
}
