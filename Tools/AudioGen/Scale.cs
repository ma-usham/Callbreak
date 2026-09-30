namespace AudioGen;

/// <summary>
/// Sargam in G, just intonation: Sa = G4, the pitch of the common G-medium bansuri. Every tonal cue and
/// both loops use the Sa Re Ga Pa Dha pentatonic (G A B D E) on this Sa, so a sting that fires over the
/// music is always in key with it. G also keeps the melody, the drone and the drums high enough for a
/// phone speaker, which plays very little below about 300 Hz.
/// Notation: S R G M P D N; "." lowers an octave, "'" raises one ("D." = the Dha below Sa, "S'" = high Sa).
/// </summary>
public static class Scale
{
    public const double Sa = 392.00; // G4

    static readonly Dictionary<char, double> Ratio = new()
    {
        ['S'] = 1.0, ['R'] = 9.0 / 8, ['G'] = 5.0 / 4, ['M'] = 4.0 / 3, ['P'] = 3.0 / 2, ['D'] = 5.0 / 3, ['N'] = 15.0 / 8,
    };

    public static readonly char[] Penta = { 'S', 'R', 'G', 'P', 'D' };

    public static double Hz(char swara, int octave) =>
        Ratio.TryGetValue(swara, out var r) ? Sa * r * Math.Pow(2, octave) : throw new ArgumentException($"Unknown swara '{swara}'");

    public static double Hz(string note)
    {
        int oct = 0;
        foreach (char m in note.AsSpan(1)) oct += m == '\'' ? 1 : m == '.' ? -1 : throw new ArgumentException($"Bad note '{note}'");
        return Hz(note[0], oct);
    }

    /// <summary>The pentatonic neighbour above or below (kan grace notes and scoops come from these).</summary>
    public static (char swara, int octave) Step(char swara, int octave, int dir)
    {
        int i = Array.IndexOf(Penta, swara);
        if (i < 0) return (swara, octave);
        i += dir;
        if (i >= Penta.Length) return (Penta[0], octave + 1);
        if (i < 0) return (Penta[^1], octave - 1);
        return (Penta[i], octave);
    }
}
