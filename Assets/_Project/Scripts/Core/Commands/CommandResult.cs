using System;

namespace Callbreak.Core.Commands
{
    /// <summary>
    /// The outcome of executing a command (GDD §22 "Results, not exceptions: an invalid command returns
    /// CommandResult.Rejected(reason)"). Rejection is a normal result for untrusted input; exceptions are
    /// reserved for programming errors.
    /// </summary>
    public readonly struct CommandResult : IEquatable<CommandResult>
    {
        private CommandResult(RejectReason reason)
        {
            Reason = reason;
        }

        /// <summary>The command was applied and its events are in the outbox.</summary>
        public static CommandResult Accepted => default;

        /// <summary>True when the command was applied.</summary>
        public bool IsAccepted => Reason == RejectReason.None;

        /// <summary>Why the command was rejected; <see cref="RejectReason.None"/> when accepted.</summary>
        public RejectReason Reason { get; }

        /// <summary>A rejection for <paramref name="reason"/>.</summary>
        /// <param name="reason">Any reason except None.</param>
        /// <returns>The rejected result.</returns>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is None.</exception>
        public static CommandResult Rejected(RejectReason reason)
        {
            if (reason == RejectReason.None)
            {
                throw new ArgumentException("A rejection needs a reason.", nameof(reason));
            }

            return new CommandResult(reason);
        }

        /// <inheritdoc />
        public bool Equals(CommandResult other)
        {
            return Reason == other.Reason;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is CommandResult other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (int)Reason;
        }

        /// <summary>"Accepted" or "Rejected(StaleTurn)".</summary>
        /// <returns>The result's text.</returns>
        public override string ToString()
        {
            return IsAccepted ? "Accepted" : "Rejected(" + Reason + ")";
        }

        /// <summary>Equality.</summary>
        /// <param name="left">A result.</param>
        /// <param name="right">Another result.</param>
        /// <returns>Whether both have the same reason.</returns>
        public static bool operator ==(CommandResult left, CommandResult right)
        {
            return left.Reason == right.Reason;
        }

        /// <summary>Inequality.</summary>
        /// <param name="left">A result.</param>
        /// <param name="right">Another result.</param>
        /// <returns>Whether the reasons differ.</returns>
        public static bool operator !=(CommandResult left, CommandResult right)
        {
            return left.Reason != right.Reason;
        }
    }
}
