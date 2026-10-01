using System;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Asks the player's name when the game starts (ADR 022), before anything else the menu does by
    /// itself, until a name is chosen. The panel sits above the menu and its dialogs, so whatever
    /// else came up (the rules primer, an invite link, an offer to resume) waits underneath. The
    /// name follows the Profile screen's rules and messages; keeping "Player" is an answer too.
    /// Android back does nothing while it is up. Once answered, the main menu shows the name.
    /// </summary>
    public sealed class NamePromptPresenter : IStartable, IDisposable, IBackHandler
    {
        public const string NoteKey = "ui.profile.name_note";

        private readonly NamePromptView _view;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly IOnScreenKeyboard _keyboard;

        private string _draft = string.Empty;
        private bool _started;

        public NamePromptPresenter(
            NamePromptView view,
            IProfileService profile,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
            IOnScreenKeyboard keyboard)
        {
            _view = view;
            _profile = profile;
            _settings = settings;
            _localization = localization;
            _audio = audio;
            _keyboard = keyboard;
        }

        public bool IsOpen => _started && _view.IsShowing;

        public void Start()
        {
            _started = true;
            _view.NameChanged += OnNameChanged;
            _view.Submitted += OnSubmitted;
            _localization.LanguageChanged += Render;

            // The panel rises above the device's keyboard, so the field is never under it.
            IOnScreenKeyboard keyboard = _keyboard;
            _view.KeyboardHeight = keyboard != null ? () => keyboard.Height : (Func<float>)null;
            if (FirstRun.AsksName(_profile))
            {
                _draft = string.Empty;
                _view.ReduceMotion = _settings.ReduceMotion;
                _view.Show(_draft);
                Render();
            }
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _view.NameChanged -= OnNameChanged;
            _view.Submitted -= OnSubmitted;
            _localization.LanguageChanged -= Render;
        }

        /// <summary>The name has to be answered: back is kept from the menu underneath.</summary>
        public bool HandleBack() => IsOpen;

        private void OnNameChanged(string text)
        {
            _draft = text ?? string.Empty;
            Render();
        }

        private void OnSubmitted()
        {
            if (!IsOpen)
            {
                return;
            }

            NameCheck check = _profile.ChooseName(_draft);
            if (!Accepts(check))
            {
                // Say why, even for a field that was left empty.
                RenderCheck(check, true);
                return;
            }

            _audio.Play(SoundCue.UiTap);
            _view.Hide();
        }

        /// <summary>Continue is offered once the name can be used; an empty field shows the note, not an error.</summary>
        private void Render()
        {
            if (!IsOpen)
            {
                return;
            }

            RenderCheck(_profile.CheckName(_draft), _draft.Trim().Length > 0);
        }

        private void RenderCheck(NameCheck check, bool explain)
        {
            _view.SetContinueInteractable(Accepts(check));
            string error = explain ? ErrorKey(check) : null;
            _view.SetMessage(_localization.Get(error ?? NoteKey), error != null);
        }

        private static bool Accepts(NameCheck check) => check == NameCheck.Ok || check == NameCheck.Unchanged;

        /// <summary>The Profile screen's messages (GDD §11 screen 14); null when the name can be used.</summary>
        private static string ErrorKey(NameCheck check)
        {
            switch (check)
            {
                case NameCheck.TooShort:
                    return "ui.error.name_too_short";
                case NameCheck.TooLong:
                    return "ui.error.name_too_long";
                case NameCheck.InvalidCharacters:
                    return "ui.error.name_invalid";
                case NameCheck.NotAllowed:
                    return "ui.error.name_not_allowed";
                default:
                    return null;
            }
        }
    }
}
