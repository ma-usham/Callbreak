using System;
using System.Globalization;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Commands
{
    /// <summary>
    /// One entry of a command log, holding any of the three commands, so a match can be replayed from
    /// <c>{seed, setup, commands[]}</c> (GDD §22 "Determinism &amp; replay"). Record every command submitted,
    /// accepted or not: rejected ones are rejected again identically on replay.
    /// </summary>
    public readonly struct RecordedCommand : IEquatable<RecordedCommand>
    {
        private RecordedCommand(CommandKind kind, Seat seat, int call, Card card, TurnId turnId, bool isAuto)
        {
            Kind = kind;
            Seat = seat;
            Call = call;
            Card = card;
            TurnId = turnId;
            IsAuto = isAuto;
        }

        /// <summary>Which command this is.</summary>
        public CommandKind Kind { get; }

        /// <summary>The acting seat.</summary>
        public Seat Seat { get; }

        /// <summary>The call; only for <see cref="CommandKind.PlaceCall"/>.</summary>
        public int Call { get; }

        /// <summary>The card; only for <see cref="CommandKind.PlayCard"/>.</summary>
        public Card Card { get; }

        /// <summary>The turn id; <see cref="TurnId.None"/> for <see cref="CommandKind.ReadyNext"/>.</summary>
        public TurnId TurnId { get; }

        /// <summary>The auto flag of a call or play.</summary>
        public bool IsAuto { get; }

        /// <summary>Records a call.</summary>
        /// <param name="command">The command.</param>
        /// <returns>The log entry.</returns>
        public static RecordedCommand Of(in PlaceCall command)
        {
            return new RecordedCommand(CommandKind.PlaceCall, command.Seat, command.Call, default, command.TurnId, command.IsAuto);
        }

        /// <summary>Records a play.</summary>
        /// <param name="command">The command.</param>
        /// <returns>The log entry.</returns>
        public static RecordedCommand Of(in PlayCard command)
        {
            return new RecordedCommand(CommandKind.PlayCard, command.Seat, 0, command.Card, command.TurnId, command.IsAuto);
        }

        /// <summary>Records a ready.</summary>
        /// <param name="command">The command.</param>
        /// <returns>The log entry.</returns>
        public static RecordedCommand Of(in ReadyNext command)
        {
            return new RecordedCommand(CommandKind.ReadyNext, command.Seat, 0, default, TurnId.None, false);
        }

        /// <summary>Submits the recorded command to <paramref name="engine"/>.</summary>
        /// <param name="engine">The engine to drive.</param>
        /// <returns>The engine's result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        public CommandResult ExecuteOn(MatchEngine engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            switch (Kind)
            {
                case CommandKind.PlaceCall:
                    return engine.Execute(new PlaceCall(Seat, Call, TurnId, IsAuto));
                case CommandKind.PlayCard:
                    return engine.Execute(new PlayCard(Seat, Card, TurnId, IsAuto));
                case CommandKind.ReadyNext:
                    return engine.Execute(new ReadyNext(Seat));
                default:
                    throw new InvalidOperationException("Unknown command kind " + Kind + ".");
            }
        }

        /// <inheritdoc />
        public bool Equals(RecordedCommand other)
        {
            return Kind == other.Kind
                && Seat == other.Seat
                && Call == other.Call
                && Card == other.Card
                && TurnId == other.TurnId
                && IsAuto == other.IsAuto;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is RecordedCommand other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((((int)Kind * 31) + (int)Seat) * 31 + (Call * 64) + Card.Id) * 31 + (TurnId.Value * 2) + (IsAuto ? 1 : 0);
            }
        }

        /// <summary>The same text as the underlying command's <c>ToString</c>.</summary>
        /// <returns>The entry's text.</returns>
        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.PlaceCall:
                    return new PlaceCall(Seat, Call, TurnId, IsAuto).ToString();
                case CommandKind.PlayCard:
                    return new PlayCard(Seat, Card, TurnId, IsAuto).ToString();
                case CommandKind.ReadyNext:
                    return new ReadyNext(Seat).ToString();
                default:
                    return "Unknown kind=" + ((int)Kind).ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
