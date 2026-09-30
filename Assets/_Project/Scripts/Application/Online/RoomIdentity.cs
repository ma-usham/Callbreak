using System.Security.Cryptography;
using System.Text;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// What a member calls itself in one room (GDD §27 identity; L2934: other players' account ids
    /// are deliberately absent). The permanent player id never leaves the device: the hello carries
    /// a one-way hash of it and the room code instead. It is the same every time this player says
    /// hello to this room, so a dropped connection or an app kill finds the seat again, and it is
    /// worth nothing in any other room, because another code gives another value and the hash
    /// cannot be turned back into the player id.
    /// </summary>
    public static class RoomIdentity
    {
        /// <summary>Hex digits in an identity: 128 bits, far beyond guessing and unique within a room.</summary>
        public const int Length = 32;

        private const string Domain = "callbreak/room-identity/1\n";

        /// <summary>The identity of <paramref name="playerId"/> in the room <paramref name="roomCode"/>; empty for an empty player id, which never finds a seat again.</summary>
        public static string For(string playerId, string roomCode)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return string.Empty;
            }

            byte[] input = Encoding.UTF8.GetBytes(Domain + (roomCode ?? string.Empty) + "\n" + playerId);
            byte[] digest;

            // The class itself rather than SHA256.Create(), which can look the algorithm up by name:
            // nothing a code-stripping player build could remove.
            using (var sha = new SHA256Managed())
            {
                digest = sha.ComputeHash(input);
            }

            var text = new StringBuilder(Length);
            for (int i = 0; i < Length / 2; i++)
            {
                text.Append(digest[i].ToString("x2"));
            }

            return text.ToString();
        }
    }
}
