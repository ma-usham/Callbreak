using System;
using System.Collections.Generic;
using Callbreak.Core.Events;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// Plays the session's events on the table one at a time, in order (GDD §23): a FIFO queue, one
    /// <see cref="IEventAnimation"/> at a time, and a tap skips anything longer than 0.3 s.
    /// <para>
    /// Double speed is for a table that has fallen behind (GDD §6 "a backlog plays at double
    /// speed", §23 "backlog &gt; 3 → 2× speed"). What a late table has to catch up on is the moves
    /// made after the one it is showing, so those are what is counted: the waiting events from the
    /// next call or chosen card onwards. The events one move brings with it are not a backlog,
    /// however many they are. The fourth card of the twelfth trick arrives together with that
    /// trick's end, the whole last trick (which the engine plays by itself), the round's score and
    /// the bots' "ready": eleven events, all shown at normal speed.
    /// </para>
    /// <para>
    /// A card the engine plays by itself has no thinking time before it, so the player gives it
    /// the pause a forced move has everywhere else (GDD §9 "0.35 s if forced").
    /// </para>
    /// <para>
    /// A resync clears the queue, and the table is rebuilt from the session's view. An event whose
    /// playback throws ends the same way (GDD §17 principle 9: an anomaly ends in a resync, not
    /// in a stop): the queue is dropped and <see cref="Faulted"/> asks for the rebuild.
    /// </para>
    /// </summary>
    public sealed class MatchEventPlayer
    {
        /// <summary>More waiting events of later moves than this play at double speed.</summary>
        public const int BacklogForDoubleSpeed = 3;

        /// <summary>Animations longer than this can be skipped with a tap.</summary>
        public const float SkippableAbove = 0.3f;

        public const float BacklogSpeed = 2f;

        /// <summary>The pause before a card the engine played by itself.</summary>
        public const float ForcedBeatSeconds = 0.35f;

        private readonly IEventPlayback _playback;
        private readonly Queue<MatchEvent> _queue = new Queue<MatchEvent>();
        private IEventAnimation _animation;
        private MatchEvent _current;
        private MatchEvent _ending;
        private bool _begun;
        private float _lead;
        private int _behind;

        public MatchEventPlayer(IEventPlayback playback)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        }

        /// <summary>Raised when an event has finished playing, after the views were brought to its state.</summary>
        public event Action<MatchEvent> Played;

        /// <summary>
        /// The playback of an event threw. The player has dropped that event and everything
        /// waiting, so nothing stalls; the listener logs the error and rebuilds the table from the
        /// session's view, which already holds all of it. Without a listener the error is thrown on.
        /// </summary>
        public event Action<MatchEvent, Exception> Faulted;

        /// <summary>Events waiting behind the one whose turn it is.</summary>
        public int Backlog => _queue.Count;

        /// <summary>
        /// How far the table is behind: the waiting events that belong to later moves than the one
        /// being shown (from the first waiting call or chosen card onwards).
        /// </summary>
        public int Behind => _behind;

        /// <summary>The event whose turn it is (on screen, or waiting out the pause before a forced card), or null.</summary>
        public MatchEvent Current => _current;

        /// <summary>Nothing is playing and nothing waits: the table shows the model's latest state.</summary>
        public bool IsIdle => _current == null && _queue.Count == 0;

        /// <summary>1, or 2 while the table is more than <see cref="BacklogForDoubleSpeed"/> events behind.</summary>
        public float Speed => _behind > BacklogForDoubleSpeed ? BacklogSpeed : 1f;

        public void Enqueue(MatchEvent e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            _queue.Enqueue(e);
            if (_behind > 0 || StartsAMove(e))
            {
                _behind++;
            }
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

            try
            {
                Advance(deltaTime);
            }
            catch (Exception error)
            {
                if (!Fault(error))
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// A tap: cuts short the playing animation, or the pause before a forced card, if it is
        /// longer than 0.3 s. True when it did.
        /// </summary>
        public bool Skip()
        {
            if (_current == null)
            {
                return false;
            }

            if (!_begun)
            {
                if (_lead <= 0f || ForcedBeatSeconds <= SkippableAbove)
                {
                    return false;
                }

                _lead = 0f;
                return true;
            }

            if (_animation == null || _animation.Duration <= SkippableAbove)
            {
                return false;
            }

            try
            {
                _animation.Finish();
                Complete();
            }
            catch (Exception error)
            {
                if (!Fault(error))
                {
                    throw;
                }
            }

            return true;
        }

        /// <summary>
        /// Stops the playing animation and drops every waiting event (resync, reconnect, leaving).
        /// Nothing is played or reported: the caller rebuilds the table from the session's view.
        /// </summary>
        public void Clear()
        {
            IEventAnimation animation = _animation;
            Drop();
            animation?.Finish();
        }

        /// <summary>
        /// Drops the playing animation and every waiting event without finishing anything: for when
        /// the table itself is going away (the scene unloads) and the views an animation would
        /// settle no longer exist. <see cref="Clear"/> is for a table that stays.
        /// </summary>
        public void Abandon()
        {
            Drop();
        }

        /// <summary>A call, or a card somebody chose (or the clock chose for them): the first event of a move.</summary>
        private static bool StartsAMove(MatchEvent e)
        {
            return e is CallPlaced || (e is CardPlayed played && !played.IsForced);
        }

        private static float LeadOf(MatchEvent e)
        {
            return e is CardPlayed played && played.IsForced ? ForcedBeatSeconds : 0f;
        }

        private void Advance(float deltaTime)
        {
            while (true)
            {
                if (_current == null)
                {
                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    _current = _queue.Dequeue();
                    _begun = false;
                    _lead = LeadOf(_current);
                    Recount();
                }

                if (!_begun)
                {
                    if (_lead > 0f)
                    {
                        _lead -= deltaTime * Speed;
                        if (_lead > 0f)
                        {
                            return;
                        }

                        // The pause is over: the event starts on a clean clock, like every other.
                        _lead = 0f;
                        deltaTime = 0f;
                    }

                    _begun = true;
                    _animation = _playback.Begin(_current);
                    if (_animation == null)
                    {
                        Complete();
                        continue;
                    }

                    _animation.Begin();
                }

                if (!_animation.Tick(deltaTime * Speed))
                {
                    return;
                }

                Complete();

                // Whatever time was left in this frame is dropped: the next animation starts clean.
                deltaTime = 0f;
            }
        }

        private void Complete()
        {
            MatchEvent e = _current;
            _animation = null;
            _current = null;
            _begun = false;
            if (e == null)
            {
                return;
            }

            _ending = e;
            _playback.End(e);
            Played?.Invoke(e);
            _ending = null;
        }

        /// <summary>
        /// The playback of an event threw: everything is dropped, so the next tick finds a player
        /// that is idle instead of one stuck on the failed event. False when nobody listens.
        /// </summary>
        private bool Fault(Exception error)
        {
            MatchEvent failed = _current ?? _ending;
            IEventAnimation animation = _animation;
            Drop();
            if (animation != null)
            {
                try
                {
                    // What it was moving is settled if it can be; the rebuild corrects the rest.
                    animation.Finish();
                }
                catch (Exception)
                {
                    // The first failure is the one that is reported.
                }
            }

            Action<MatchEvent, Exception> faulted = Faulted;
            if (faulted == null)
            {
                return false;
            }

            faulted(failed, error);
            return true;
        }

        private void Drop()
        {
            _animation = null;
            _current = null;
            _ending = null;
            _begun = false;
            _lead = 0f;
            _queue.Clear();
            _behind = 0;
        }

        /// <summary>Counts the waiting events from the first one that starts a move.</summary>
        private void Recount()
        {
            int behind = 0;
            bool later = false;
            foreach (MatchEvent e in _queue)
            {
                later |= StartsAMove(e);
                if (later)
                {
                    behind++;
                }
            }

            _behind = behind;
        }
    }
}
