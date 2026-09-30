using Callbreak.Application.Rooms;

namespace Callbreak.Presentation.Screens
{
    /// <summary>The string-table key of the message each room error shows (GDD §11 screens 02-04).</summary>
    public static class RoomMessages
    {
        public static string KeyFor(RoomError error)
        {
            switch (error)
            {
                case RoomError.NotFound:
                    return "ui.error.room_not_found";
                case RoomError.Full:
                    return "ui.error.room_full";
                case RoomError.AlreadyStarted:
                    return "ui.error.match_started";
                case RoomError.UpdateRequired:
                    return "ui.error.update_required";
                case RoomError.Offline:
                    return "ui.toast.no_internet";
                case RoomError.RateLimited:
                    return "ui.error.too_many_attempts";
                case RoomError.HostLeft:
                    return "ui.toast.host_left";
                case RoomError.Kicked:
                    return "ui.toast.removed_from_room";
                default:
                    return "ui.toast.connect_failed";
            }
        }
    }
}
