using System;
using Callbreak.Application.Sessions;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// What the table is showing, shared by the in-match presenters (one per Table scene): the
    /// <see cref="TableState"/> after the last event that has been played back, and the moments it
    /// changes. The table presenter advances it; the call panel, the results and the scorecard
    /// follow it, so nothing reacts to an event before the player has seen it (GDD §23).
    /// </summary>
    public sealed class PlayedTable
    {
        public PlayedTable(IMatchSession session)
            : this(LocalSeatOf(session), session.Rules)
        {
        }

        public PlayedTable(Seat localSeat, RuleSet rules)
        {
            State = new TableState(localSeat, rules);
        }

        /// <summary>The table after the last played event.</summary>
        public TableState State { get; }

        /// <summary>An event has finished playing; the state already includes it.</summary>
        public event Action<MatchEvent> Played;

        /// <summary>The state was replaced from the session's view (resume, reconnect, resync).</summary>
        public event Action Rebuilt;

        /// <summary>
        /// Called by the table presenter once an event's playback has ended. Every follower hears
        /// of it even when one of them fails: with <paramref name="onError"/> a follower's exception
        /// goes there and the rest still run; without it the first exception is thrown on.
        /// </summary>
        public void NotifyPlayed(MatchEvent e, Action<Exception> onError = null)
        {
            Action<MatchEvent> played = Played;
            if (played == null)
            {
                return;
            }

            if (onError == null)
            {
                played(e);
                return;
            }

            foreach (Delegate follower in played.GetInvocationList())
            {
                try
                {
                    ((Action<MatchEvent>)follower)(e);
                }
                catch (Exception error)
                {
                    onError(error);
                }
            }
        }

        /// <summary>Called by the table presenter after a resync rebuilt the table; followers are told as in <see cref="NotifyPlayed"/>.</summary>
        public void NotifyRebuilt(Action<Exception> onError = null)
        {
            Action rebuilt = Rebuilt;
            if (rebuilt == null)
            {
                return;
            }

            if (onError == null)
            {
                rebuilt();
                return;
            }

            foreach (Delegate follower in rebuilt.GetInvocationList())
            {
                try
                {
                    ((Action)follower)();
                }
                catch (Exception error)
                {
                    onError(error);
                }
            }
        }

        private static Seat LocalSeatOf(IMatchSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return session.LocalSeat;
        }
    }
}
