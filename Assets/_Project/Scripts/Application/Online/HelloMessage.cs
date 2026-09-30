namespace Callbreak.Application.Online
{
    /// <summary>
    /// The first thing a member sends (GDD §25 versioning, §27 identity): its protocol version,
    /// its stable player id (so a reconnect finds its seat again) and how it appears at the table.
    /// </summary>
    public sealed class HelloMessage
    {
        public HelloMessage(ushort protocolVersion, string playerId, string name, int avatarId, string deckThemeId)
        {
            ProtocolVersion = protocolVersion;
            PlayerId = playerId ?? string.Empty;
            Name = name ?? string.Empty;
            AvatarId = avatarId;
            DeckThemeId = deckThemeId ?? string.Empty;
        }

        public ushort ProtocolVersion { get; }

        public string PlayerId { get; }

        public string Name { get; }

        public int AvatarId { get; }

        public string DeckThemeId { get; }

        public void WriteTo(WireWriter w)
        {
            w.UInt16(ProtocolVersion);
            w.String(PlayerId);
            w.String(Name);
            w.Byte((byte)AvatarId);
            w.String(DeckThemeId);
        }

        public static HelloMessage Read(WireReader r)
        {
            ushort version = r.UInt16();
            string playerId = r.String();
            string name = r.String();
            int avatar = r.Byte();
            string deck = r.String();
            return new HelloMessage(version, playerId, name, avatar, deck);
        }
    }
}
