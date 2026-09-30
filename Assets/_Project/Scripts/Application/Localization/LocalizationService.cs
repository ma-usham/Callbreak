using System;
using System.Collections.Generic;
using System.Globalization;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;

namespace Callbreak.Application.Localization
{
    /// <summary>
    /// <see cref="ILocalization"/> over <see cref="StringTables"/> (GDD §15). The active language
    /// is the one in settings when the tables have strings for it, otherwise English. A key the
    /// active language lacks falls back to English, then to the key itself. It follows settings
    /// live: changing the language (here or through <see cref="ISettingsService"/>) raises
    /// <see cref="LanguageChanged"/> once the visible language actually changes.
    /// </summary>
    public sealed class LocalizationService : ILocalization, IDisposable
    {
        private readonly StringTables _tables;
        private readonly ISettingsService _settings;
        private string _language;

        public LocalizationService(StringTables tables, ISettingsService settings)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _language = Resolve(settings.Language);
            _settings.Changed += OnSettingsChanged;
        }

        public event Action LanguageChanged;

        public string Language => _language;

        public IReadOnlyList<string> Languages => _tables.Languages;

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (_tables.TryGet(_language, key, out string value))
            {
                return value;
            }

            if (_language != StringTables.DefaultLanguage && _tables.TryGet(StringTables.DefaultLanguage, key, out value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// <see cref="Get"/> with <c>{0}</c>-style placeholders filled using the invariant culture
        /// (scores keep "4.1" in every language). A translation whose placeholders are broken falls
        /// back to the English template; if that fails too, the unfilled template is returned. Never throws.
        /// </summary>
        public string Format(string key, params object[] args)
        {
            string template = Get(key);
            if (TryFormat(template, args, out string text))
            {
                return text;
            }

            if (_language != StringTables.DefaultLanguage
                && _tables.TryGet(StringTables.DefaultLanguage, key, out string english)
                && TryFormat(english, args, out text))
            {
                return text;
            }

            return template;
        }

        /// <summary>
        /// Switches to <paramref name="language"/> and saves it in settings. A language the tables
        /// have no strings for is ignored, so the setting never points at an empty column.
        /// </summary>
        public void SetLanguage(string language)
        {
            string code = SettingsService.NormalizeLanguage(language);
            if (code == null || !Contains(_tables.Languages, code))
            {
                return;
            }

            _settings.SetLanguage(code);

            // Settings raise Changed only on a real change; make sure the visible language follows
            // even when settings already held this code.
            OnSettingsChanged();
        }

        public void Dispose()
        {
            _settings.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            string resolved = Resolve(_settings.Language);
            if (resolved == _language)
            {
                return;
            }

            _language = resolved;
            LanguageChanged?.Invoke();
        }

        private string Resolve(string requested)
        {
            string code = SettingsService.NormalizeLanguage(requested);
            return code != null && Contains(_tables.Languages, code) ? code : StringTables.DefaultLanguage;
        }

        private static bool TryFormat(string template, object[] args, out string text)
        {
            try
            {
                text = string.Format(CultureInfo.InvariantCulture, template, args ?? Array.Empty<object>());
                return true;
            }
            catch (FormatException)
            {
                text = null;
                return false;
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
