namespace Callbreak.Core.Events
{
    /// <summary>
    /// An immutable fact emitted by the engine (GDD §22 event catalogue), drained in order with
    /// <c>MatchEngine.DrainEvents</c>. Route each event only where <see cref="Visibility"/> allows. Match on
    /// the concrete type (<c>case CardPlayed played:</c>). The catalogue is closed: only Core defines events.
    /// </summary>
    public abstract class MatchEvent
    {
        private protected MatchEvent(EventVisibility visibility)
        {
            Visibility = visibility;
        }

        /// <summary>Public, or private to one seat.</summary>
        public EventVisibility Visibility { get; }

        /// <summary>
        /// A stable, culture-invariant line such as "CardPlayed seat=South card=A♥ auto=0 forced=0", with
        /// " [to South]" appended to private events. Two event streams can be compared line by line.
        /// </summary>
        /// <returns>The event's text.</returns>
        public sealed override string ToString()
        {
            string text = Describe();
            return Visibility.IsPublic ? text : text + " [to " + Visibility.Seat + "]";
        }

        private protected abstract string Describe();
    }
}
