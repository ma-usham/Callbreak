using System;

namespace Callbreak.Application.Online
{
    /// <summary>One connection as the host's transport names it. The host's own player is <see cref="Local"/>.</summary>
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public PeerId(ulong value)
        {
            Value = value;
        }

        /// <summary>The host device's own player, connected in process.</summary>
        public static PeerId Local => new PeerId(ulong.MaxValue);

        public ulong Value { get; }

        public bool IsLocal => Value == ulong.MaxValue;

        public bool Equals(PeerId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PeerId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => IsLocal ? "local" : "peer " + Value;

        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);

        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);
    }
}
