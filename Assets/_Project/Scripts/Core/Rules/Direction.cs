namespace Callbreak.Core.Rules
{
    /// <summary>
    /// The order of dealing, calling and play (GDD §5 RuleSet "Direction"). Nepali Callbreak is
    /// anticlockwise: the next player sits on your right.
    /// </summary>
    public enum Direction : byte
    {
        /// <summary>Default. In absolute seat numbers the next seat is seat + 1 (mod 4).</summary>
        Anticlockwise = 0,

        /// <summary>House-rule variant. In absolute seat numbers the next seat is seat + 3 (mod 4).</summary>
        Clockwise = 1,
    }
}
