using Callbreak.Application.Sessions;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// What "Leave match?" warns of (GDD §8 "What happens when…", §11 screen 13). Offline the match
    /// is saved and can be resumed. Online a bot takes the seat and the match counts as a loss.
    /// The host of a private room is the room's server (phase 1), so for the host leaving closes
    /// the room for everybody, and the dialog has to say so; unless the next host in line takes the
    /// match over (ADR 030), when the host leaves like anyone else.
    /// </summary>
    public static class MatchMenuText
    {
        public const string LeaveBodyKey = "ui.dialog.leave_match.body";
        public const string LeaveBodyOfflineKey = "ui.dialog.leave_match.body_offline";
        public const string LeaveBodyHostKey = "ui.dialog.leave_match.body_host";

        /// <param name="mode">The match being left.</param>
        /// <param name="hostsTheRoom">This device hosts the private room the match is played in, and its leaving closes the room.</param>
        public static string LeaveBodyKeyFor(MatchMode mode, bool hostsTheRoom)
        {
            if (mode == MatchMode.Offline)
            {
                return LeaveBodyOfflineKey;
            }

            return mode == MatchMode.PrivateRoom && hostsTheRoom ? LeaveBodyHostKey : LeaveBodyKey;
        }
    }
}
