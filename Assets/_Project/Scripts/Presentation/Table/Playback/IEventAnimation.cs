using Callbreak.Core.Events;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// How one match event shows on the table (GDD §23 "Event playback pipeline"): the deal, a call
    /// bubble, a card flight, a trick sweep. The event player advances it with
    /// <see cref="Tick"/>, at double speed when events pile up, and may cut it short.
    /// </summary>
    /// <remarks>
    /// The GDD sketches these as awaitables. They are ticked instead: the player owns time, so
    /// double speed, skipping and a resync are one code path, nothing allocates per frame, and the
    /// queue can be tested without a player loop.
    /// </remarks>
    public interface IEventAnimation
    {
        /// <summary>Length in seconds at normal speed. Anything longer than 0.3 s can be skipped with a tap.</summary>
        float Duration { get; }

        /// <summary>Called once, before the first <see cref="Tick"/>.</summary>
        void Begin();

        /// <summary>Advances by <paramref name="deltaTime"/> seconds (already scaled); true when it has finished.</summary>
        bool Tick(float deltaTime);

        /// <summary>
        /// Stops at once (skip, or the queue is being cleared). Views need not be at their end
        /// state: the playback brings them to the table's state right after.
        /// </summary>
        void Finish();
    }

    /// <summary>What the event player drives: the table's state and its animations.</summary>
    public interface IEventPlayback
    {
        /// <summary>
        /// The event's turn has come. Applies it to the table's state and returns the animation that
        /// shows it, or null when it shows at once.
        /// </summary>
        IEventAnimation Begin(MatchEvent e);

        /// <summary>The event has finished playing (or was skipped): the views must now match the state.</summary>
        void End(MatchEvent e);
    }
}
