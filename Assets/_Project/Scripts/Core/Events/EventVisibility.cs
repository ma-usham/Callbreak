using System;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// Who may receive an event (GDD §17 "Hidden by construction. Engine events are tagged Public or
    /// Private(seat). Transports only route what they are given"). A private event goes to its seat only;
    /// nobody else, human or bot, may ever receive it.
    /// </summary>
    public readonly struct EventVisibility : IEquatable<EventVisibility>
    {
        // 0 = public; otherwise the private seat's number + 1.
        private readonly byte _code;

        private EventVisibility(byte code)
        {
            _code = code;
        }

        /// <summary>Everyone at the table may receive the event.</summary>
        public static EventVisibility Public => default;

        /// <summary>True for <see cref="Public"/>.</summary>
        public bool IsPublic => _code == 0;

        /// <summary>The only seat allowed to receive a private event.</summary>
        /// <exception cref="InvalidOperationException">The event is public.</exception>
        public Seat Seat
        {
            get
            {
                if (_code == 0)
                {
                    throw new InvalidOperationException("A public event has no private seat.");
                }

                return (Seat)(_code - 1);
            }
        }

        /// <summary>An event only <paramref name="seat"/> may receive.</summary>
        /// <param name="seat">The owner.</param>
        /// <returns>The visibility.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The seat is not defined.</exception>
        public static EventVisibility Private(Seat seat)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
            }

            return new EventVisibility((byte)((int)seat + 1));
        }

        /// <summary>Whether a player sitting at <paramref name="viewer"/> may receive the event.</summary>
        /// <param name="viewer">The receiving seat.</param>
        /// <returns>True for public events and for the private seat itself.</returns>
        public bool IsVisibleTo(Seat viewer)
        {
            return _code == 0 || _code == (int)viewer + 1;
        }

        /// <inheritdoc />
        public bool Equals(EventVisibility other)
        {
            return _code == other._code;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is EventVisibility other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _code;
        }

        /// <summary>"public" or e.g. "private(South)".</summary>
        /// <returns>The visibility's text.</returns>
        public override string ToString()
        {
            return _code == 0 ? "public" : "private(" + Seat + ")";
        }
    }
}
