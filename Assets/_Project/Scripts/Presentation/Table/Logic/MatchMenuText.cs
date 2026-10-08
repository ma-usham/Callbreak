using Callbreak.Application.Sessions;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The match menu's words that depend on the match (GDD §11 screen 13). An offline match is
    /// paused while the menu is open, so its tag says so, and its first button restarts it (the
    /// owner, 8 Oct 2026: a restart without going back to the main menu; the × resumes); an online
    /// game cannot pause, so its tag just says it is online and the button goes back to the game.
    /// What "Leave match?" warns of (GDD §8 "What happens when…"): offline the match is saved and
    /// can be resumed; online a bot takes the seat and the match counts as a loss. The host of a
    /// private room is the room's server (phase 1), so for the host leaving closes the room for
    /// everybody, and the dialog has to say so; unless the next host in line takes the match over
    /// (ADR 030), when the host leaves like anyone else.
    /// </summary>
    public static class MatchMenuText
    {
        public const string ModeOfflineKey = "ui.match_menu.mode_offline";
        public const string ModeOnlineKey = "ui.match_menu.mode_online";
        public const string ResumeKey = "ui.match_menu.resume";
        public const string RestartKey = "ui.match_menu.restart";
        public const string BackToGameKey = "ui.match_menu.back_to_game";
        public const string LeaveBodyKey = "ui.dialog.leave_match.body";
        public const string LeaveBodyOfflineKey = "ui.dialog.leave_match.body_offline";
        public const string LeaveBodyHostKey = "ui.dialog.leave_match.body_host";

        /// <summary>The tag under the title: "Offline match · Paused" or "Online game".</summary>
        public static string ModeKeyFor(MatchMode mode) => mode == MatchMode.Offline ? ModeOfflineKey : ModeOnlineKey;

        /// <summary>The first button: "Restart Game" offline; "Back to Game" online, where nothing is paused.</summary>
        public static string FirstButtonKeyFor(MatchMode mode) => mode == MatchMode.Offline ? RestartKey : BackToGameKey;

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
