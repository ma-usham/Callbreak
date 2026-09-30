using System;
using Callbreak.Application.Sessions;
using Callbreak.Core.Events;

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
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            State = new TableState(session.LocalSeat, session.Rules);
        }

        /// <summary>The table after the last played event.</summary>
        public TableState State { get; }

        /// <summary>An event has finished playing; the state already includes it.</summary>
        public event Action<MatchEvent> Played;

        /// <summary>The state was replaced from the session's view (resume, reconnect, resync).</summary>
        public event Action Rebuilt;

        /// <summary>Called by the table presenter once an event's playback has ended.</summary>
        public void NotifyPlayed(MatchEvent e) => Played?.Invoke(e);

        /// <summary>Called by the table presenter after a resync rebuilt the table.</summary>
        public void NotifyRebuilt() => Rebuilt?.Invoke();
    }
}
