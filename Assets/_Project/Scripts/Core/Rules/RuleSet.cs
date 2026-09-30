using System;
using System.Globalization;
using System.Text;
using Callbreak.Core.Match;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// Every rule switch the engine reads (GDD §5 "Rule configuration"). Immutable: build one with the
    /// validating constructor (e.g. from a <c>RuleSetAsset</c> or Remote Config) or derive a variant with
    /// <see cref="With"/>. v1 ships only <see cref="NepaliStandard"/>; the other values exist for tests,
    /// balancing and future private-room house rules.
    /// </summary>
    public sealed class RuleSet : IEquatable<RuleSet>
    {
        /// <summary>Identifier of the <see cref="NepaliStandard"/> preset.</summary>
        public const string NepaliStandardId = "nepali-standard";

        /// <summary>Fewest rounds a match may have (GDD §5 Rounds range 1–10).</summary>
        public const int MinRounds = 1;

        /// <summary>Most rounds a match may have.</summary>
        public const int MaxRounds = 10;

        /// <summary>Upper limit for <see cref="MinCall"/> and <see cref="MaxCall"/> (GDD §5 range 1–13).</summary>
        public const int CallLimit = 13;

        /// <summary>Largest allowed <see cref="OvertrickTenths"/> (GDD §5 range 0–10).</summary>
        public const int MaxOvertrickTenths = 10;

        /// <summary>Creates a validated rule set. Argument meanings follow the GDD §5 table.</summary>
        /// <param name="id">Stable identifier, recorded in <c>MatchStarted</c> and snapshots.</param>
        /// <param name="rounds">Match length, 1–10.</param>
        /// <param name="minCall">Lowest call, 1–13.</param>
        /// <param name="maxCall">Highest call, <paramref name="minCall"/>–13.</param>
        /// <param name="direction">Order of dealing, calling and play.</param>
        /// <param name="mustBeatLedSuit">When following, play higher than the best led-suit card if able.</param>
        /// <param name="mustTrumpWhenVoid">Out of the led suit, play a spade if able.</param>
        /// <param name="mustOvertrump">That spade must beat every spade already in the trick.</param>
        /// <param name="discardWhenCannotOvertrump">If no spade can win, any card is legal; false = still play a spade.</param>
        /// <param name="overtrickTenths">Value of each extra trick in tenths, 0–10.</param>
        /// <param name="redealIfNoSpade">Redeal when any hand has no spade.</param>
        /// <param name="redealIfNoHonour">Redeal when any hand has no card above 10.</param>
        /// <param name="bigCallBonus">A made call of 8 or more scores 13.0.</param>
        /// <param name="autoPlayLastTrick">The 13th trick plays itself.</param>
        /// <param name="autoPlayForcedMoves">Auto-play whenever exactly one card is legal.</param>
        /// <exception cref="ArgumentException"><paramref name="id"/> is null or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A numeric value or the direction is out of range.</exception>
        public RuleSet(
            string id,
            int rounds,
            int minCall,
            int maxCall,
            Direction direction,
            bool mustBeatLedSuit,
            bool mustTrumpWhenVoid,
            bool mustOvertrump,
            bool discardWhenCannotOvertrump,
            int overtrickTenths,
            bool redealIfNoSpade,
            bool redealIfNoHonour,
            bool bigCallBonus,
            bool autoPlayLastTrick,
            bool autoPlayForcedMoves)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A rule set needs an id.", nameof(id));
            }

            if (rounds < MinRounds || rounds > MaxRounds)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds), rounds, "Rounds must be 1-10.");
            }

            if (minCall < 1 || minCall > CallLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(minCall), minCall, "MinCall must be 1-13.");
            }

            if (maxCall < minCall || maxCall > CallLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCall), maxCall, "MaxCall must be MinCall-13.");
            }

            if (direction != Direction.Anticlockwise && direction != Direction.Clockwise)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
            }

            if (overtrickTenths < 0 || overtrickTenths > MaxOvertrickTenths)
            {
                throw new ArgumentOutOfRangeException(nameof(overtrickTenths), overtrickTenths, "OvertrickTenths must be 0-10.");
            }

            Id = id;
            Rounds = rounds;
            MinCall = minCall;
            MaxCall = maxCall;
            Direction = direction;
            MustBeatLedSuit = mustBeatLedSuit;
            MustTrumpWhenVoid = mustTrumpWhenVoid;
            MustOvertrump = mustOvertrump;
            DiscardWhenCannotOvertrump = discardWhenCannotOvertrump;
            OvertrickTenths = overtrickTenths;
            RedealIfNoSpade = redealIfNoSpade;
            RedealIfNoHonour = redealIfNoHonour;
            BigCallBonus = bigCallBonus;
            AutoPlayLastTrick = autoPlayLastTrick;
            AutoPlayForcedMoves = autoPlayForcedMoves;
        }

        /// <summary>
        /// The v1 preset (GDD §5 "Nepali Standard"): 5 rounds, calls 1–8, anticlockwise, must beat, must
        /// trump, must overtrump, free discard when unable to overtrump, 0.1 per extra trick, redeal when a
        /// hand has no spade, no honour redeal, no big-call bonus, last trick auto-plays, forced moves do not.
        /// </summary>
        public static RuleSet NepaliStandard { get; } = new RuleSet(
            NepaliStandardId,
            rounds: 5,
            minCall: 1,
            maxCall: 8,
            direction: Direction.Anticlockwise,
            mustBeatLedSuit: true,
            mustTrumpWhenVoid: true,
            mustOvertrump: true,
            discardWhenCannotOvertrump: true,
            overtrickTenths: 1,
            redealIfNoSpade: true,
            redealIfNoHonour: false,
            bigCallBonus: false,
            autoPlayLastTrick: true,
            autoPlayForcedMoves: false);

        /// <summary>Stable identifier, e.g. "nepali-standard". Variants used in production need their own id.</summary>
        public string Id { get; }

        /// <summary>Seats at the table; fixed at 4 in v1 (GDD §5 PlayerCount).</summary>
        public int PlayerCount => SeatExtensions.Count;

        /// <summary>Rounds in a match (GDD §5, "5 rounds" by default).</summary>
        public int Rounds { get; }

        /// <summary>Lowest legal call (GDD §3).</summary>
        public int MinCall { get; }

        /// <summary>Highest legal call; the UI renders one chip per value (GDD §5, §11 screen 07).</summary>
        public int MaxCall { get; }

        /// <summary>Order of dealing, calling and play (GDD §5).</summary>
        public Direction Direction { get; }

        /// <summary>"Head the trick": when following, beat the highest led-suit card if you can (GDD §3 rule 2).</summary>
        public bool MustBeatLedSuit { get; }

        /// <summary>Out of the led suit, you must play a spade if you hold one (GDD §3 rule 3).</summary>
        public bool MustTrumpWhenVoid { get; }

        /// <summary>The trumping spade must beat every spade already in the trick (GDD §3 rule 3).</summary>
        public bool MustOvertrump { get; }

        /// <summary>
        /// If you cannot beat the highest spade, any card is legal; when false you must still play a spade
        /// (GDD §5).
        /// </summary>
        public bool DiscardWhenCannotOvertrump { get; }

        /// <summary>Value of each trick won beyond the call, in tenths: 1 means +0.1 (GDD §4, §5).</summary>
        public int OvertrickTenths { get; }

        /// <summary>Redeal (same dealer) when any hand has no spade (GDD §2).</summary>
        public bool RedealIfNoSpade { get; }

        /// <summary>Redeal (same dealer) when any hand has no card above 10 (GDD §2 house rule).</summary>
        public bool RedealIfNoHonour { get; }

        /// <summary>Traditional variant: a made call of 8 or more scores 13.0 (GDD §5).</summary>
        public bool BigCallBonus { get; }

        /// <summary>The 13th trick plays itself, since every card is forced (GDD §5, §6).</summary>
        public bool AutoPlayLastTrick { get; }

        /// <summary>Auto-play whenever exactly one card is legal (GDD §5).</summary>
        public bool AutoPlayForcedMoves { get; }

        /// <summary>
        /// Returns a copy with the given values replaced; omitted arguments keep this rule set's values.
        /// The copy is validated like the constructor, e.g.
        /// <c>RuleSet.NepaliStandard.With(mustOvertrump: false)</c>.
        /// </summary>
        /// <param name="id">New id, or null to keep.</param>
        /// <param name="rounds">New match length.</param>
        /// <param name="minCall">New lowest call.</param>
        /// <param name="maxCall">New highest call.</param>
        /// <param name="direction">New direction.</param>
        /// <param name="mustBeatLedSuit">New must-beat switch.</param>
        /// <param name="mustTrumpWhenVoid">New must-trump switch.</param>
        /// <param name="mustOvertrump">New must-overtrump switch.</param>
        /// <param name="discardWhenCannotOvertrump">New free-discard switch.</param>
        /// <param name="overtrickTenths">New overtrick value.</param>
        /// <param name="redealIfNoSpade">New no-spade redeal switch.</param>
        /// <param name="redealIfNoHonour">New no-honour redeal switch.</param>
        /// <param name="bigCallBonus">New big-call switch.</param>
        /// <param name="autoPlayLastTrick">New last-trick auto-play switch.</param>
        /// <param name="autoPlayForcedMoves">New forced-move auto-play switch.</param>
        /// <returns>The derived rule set.</returns>
        public RuleSet With(
            string id = null,
            int? rounds = null,
            int? minCall = null,
            int? maxCall = null,
            Direction? direction = null,
            bool? mustBeatLedSuit = null,
            bool? mustTrumpWhenVoid = null,
            bool? mustOvertrump = null,
            bool? discardWhenCannotOvertrump = null,
            int? overtrickTenths = null,
            bool? redealIfNoSpade = null,
            bool? redealIfNoHonour = null,
            bool? bigCallBonus = null,
            bool? autoPlayLastTrick = null,
            bool? autoPlayForcedMoves = null)
        {
            return new RuleSet(
                id ?? Id,
                rounds ?? Rounds,
                minCall ?? MinCall,
                maxCall ?? MaxCall,
                direction ?? Direction,
                mustBeatLedSuit ?? MustBeatLedSuit,
                mustTrumpWhenVoid ?? MustTrumpWhenVoid,
                mustOvertrump ?? MustOvertrump,
                discardWhenCannotOvertrump ?? DiscardWhenCannotOvertrump,
                overtrickTenths ?? OvertrickTenths,
                redealIfNoSpade ?? RedealIfNoSpade,
                redealIfNoHonour ?? RedealIfNoHonour,
                bigCallBonus ?? BigCallBonus,
                autoPlayLastTrick ?? AutoPlayLastTrick,
                autoPlayForcedMoves ?? AutoPlayForcedMoves);
        }

        /// <inheritdoc />
        public bool Equals(RuleSet other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other != null
                && Id == other.Id
                && Rounds == other.Rounds
                && MinCall == other.MinCall
                && MaxCall == other.MaxCall
                && Direction == other.Direction
                && MustBeatLedSuit == other.MustBeatLedSuit
                && MustTrumpWhenVoid == other.MustTrumpWhenVoid
                && MustOvertrump == other.MustOvertrump
                && DiscardWhenCannotOvertrump == other.DiscardWhenCannotOvertrump
                && OvertrickTenths == other.OvertrickTenths
                && RedealIfNoSpade == other.RedealIfNoSpade
                && RedealIfNoHonour == other.RedealIfNoHonour
                && BigCallBonus == other.BigCallBonus
                && AutoPlayLastTrick == other.AutoPlayLastTrick
                && AutoPlayForcedMoves == other.AutoPlayForcedMoves;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as RuleSet);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Id);
                hash = (hash * 397) ^ Rounds;
                hash = (hash * 397) ^ MinCall;
                hash = (hash * 397) ^ MaxCall;
                hash = (hash * 397) ^ OvertrickTenths;
                hash = (hash * 397) ^ SwitchBits();
                return hash;
            }
        }

        /// <summary>
        /// Every value on one culture-invariant line, e.g.
        /// "nepali-standard rounds=5 calls=1-8 Anticlockwise beat=1 trump=1 overtrump=1 discard=1 ...".
        /// </summary>
        /// <returns>The rule set's text.</returns>
        public override string ToString()
        {
            var text = new StringBuilder(160);
            text.Append(Id);
            text.Append(" rounds=").Append(Rounds.ToString(CultureInfo.InvariantCulture));
            text.Append(" calls=").Append(MinCall.ToString(CultureInfo.InvariantCulture));
            text.Append('-').Append(MaxCall.ToString(CultureInfo.InvariantCulture));
            text.Append(' ').Append(Direction.ToString());
            AppendSwitch(text, "beat", MustBeatLedSuit);
            AppendSwitch(text, "trump", MustTrumpWhenVoid);
            AppendSwitch(text, "overtrump", MustOvertrump);
            AppendSwitch(text, "discard", DiscardWhenCannotOvertrump);
            text.Append(" overtrick=").Append(OvertrickTenths.ToString(CultureInfo.InvariantCulture));
            AppendSwitch(text, "redealNoSpade", RedealIfNoSpade);
            AppendSwitch(text, "redealNoHonour", RedealIfNoHonour);
            AppendSwitch(text, "bigCall", BigCallBonus);
            AppendSwitch(text, "autoLast", AutoPlayLastTrick);
            AppendSwitch(text, "autoForced", AutoPlayForcedMoves);
            return text.ToString();
        }

        private static void AppendSwitch(StringBuilder text, string name, bool value)
        {
            text.Append(' ').Append(name).Append('=').Append(value ? '1' : '0');
        }

        private int SwitchBits()
        {
            int bits = (int)Direction;
            bits = (bits << 1) | (MustBeatLedSuit ? 1 : 0);
            bits = (bits << 1) | (MustTrumpWhenVoid ? 1 : 0);
            bits = (bits << 1) | (MustOvertrump ? 1 : 0);
            bits = (bits << 1) | (DiscardWhenCannotOvertrump ? 1 : 0);
            bits = (bits << 1) | (RedealIfNoSpade ? 1 : 0);
            bits = (bits << 1) | (RedealIfNoHonour ? 1 : 0);
            bits = (bits << 1) | (BigCallBonus ? 1 : 0);
            bits = (bits << 1) | (AutoPlayLastTrick ? 1 : 0);
            bits = (bits << 1) | (AutoPlayForcedMoves ? 1 : 0);
            return bits;
        }
    }
}
