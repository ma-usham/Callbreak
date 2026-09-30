namespace Callbreak.Application.Online
{
    /// <summary>
    /// The first thing a member sends (GDD §25 versioning, §27 identity): its protocol version, its
    /// identity in this room (<see cref="RoomIdentity"/>: stable, so a reconnect finds its seat
    /// again, but never the permanent player id) and how it appears at the table.
    /// </summary>
    public sealed class HelloMessage
    {
        public HelloMessage(ushort protocolVersion, string identity, string name, int avatarId, string deckThemeId)
        {
            ProtocolVersion = protocolVersion;
            Identity = identity ?? string.Empty;
            Name = name ?? string.Empty;
            AvatarId = avatarId;
            DeckThemeId = deckThemeId ?? string.Empty;
        }

        public ushort ProtocolVersion { get; }

        /// <summary>This player in this room only (<see cref="RoomIdentity.For"/>); empty never finds a seat again.</summary>
        public string Identity { get; }

        public string Name { get; }

        public int AvatarId { get; }

        public string DeckThemeId { get; }

        public void WriteTo(WireWriter w)
        {
            w.UInt16(ProtocolVersion);
            w.String(Identity);
            w.String(Name);
            w.Byte((byte)AvatarId);
            w.String(DeckThemeId);
        }

        public static HelloMessage Read(WireReader r) => ReadAfterVersion(r, r.UInt16());

        /// <summary>
        /// The rest of a hello whose version the host has already read. The version always comes
        /// first, so a host can answer "update required" to a build whose hello it cannot read.
        /// </summary>
        public static HelloMessage ReadAfterVersion(WireReader r, ushort version)
        {
            string identity = r.String(Protocol.MaxIdentityBytes);
            string name = r.String(Protocol.MaxNameBytes);
            int avatar = r.Byte();
            string deck = r.String(Protocol.MaxIdBytes);
            return new HelloMessage(version, identity, name, avatar, deck);
        }
    }
}
