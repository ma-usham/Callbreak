using System;
using System.Collections.Generic;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Every user-facing string comes from here (GDD §15): string tables UI, Rules and Emotes in
    /// English, and Nepali once the text-shaping spike is decided. Keys look like
    /// <c>ui.create_room.title</c>. Scores, room codes and player names are never translated.
    /// </summary>
    public interface ILocalization
    {
        /// <summary>The active language code, e.g. "en" or "ne".</summary>
        string Language { get; }

        /// <summary>Languages that have a table and may be offered in Settings.</summary>
        IReadOnlyList<string> Languages { get; }

        /// <summary>Raised after the language changes, so visible text can be refreshed live.</summary>
        event Action LanguageChanged;

        /// <summary>The string for <paramref name="key"/>; English if the active language lacks it, the key itself if nothing has it.</summary>
        string Get(string key);

        /// <summary><see cref="Get"/> with <c>{0}</c>-style placeholders filled, culture-invariant.</summary>
        string Format(string key, params object[] args);

        void SetLanguage(string language);
    }
}
