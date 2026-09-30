using System;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>cosmetics.json: the selected deck. Plain serializable data read and written by <see cref="CosmeticsService"/>.</summary>
    [Serializable]
    public sealed class CosmeticsData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;
        public string selectedDeckId = CosmeticsService.DefaultDeckId;
    }
}
