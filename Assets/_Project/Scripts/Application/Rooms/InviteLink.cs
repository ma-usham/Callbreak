using System;

namespace Callbreak.Application.Rooms
{
    /// <summary>The invite link of GDD §11 screen 04: <c>callbreak://join/AB2CDE</c>.</summary>
    public static class InviteLink
    {
        private const string Prefix = "callbreak://join/";

        /// <summary>The room code in <paramref name="url"/>, normalised; false when it is not a valid join link.</summary>
        public static bool TryParse(string url, out string roomCode)
        {
            roomCode = null;
            if (string.IsNullOrEmpty(url) || !url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rest = url.Substring(Prefix.Length);
            int end = rest.IndexOfAny(new[] { '?', '#', '/' });
            if (end >= 0)
            {
                rest = rest.Substring(0, end);
            }

            string code = RoomCode.Normalize(rest);
            if (!RoomCode.IsValid(code))
            {
                return false;
            }

            roomCode = code;
            return true;
        }
    }
}
