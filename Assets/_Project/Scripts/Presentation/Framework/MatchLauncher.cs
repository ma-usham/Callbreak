using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Every way from the menu into a match goes through here: play modes open with
    /// <see cref="Enter"/>, so the rules primer is shown exactly once before a player's first
    /// match (GDD §11 screen 12), and matches start with <see cref="Launch"/>, so a second tap can
    /// never start two scene loads. While the name is still being asked (ADR 022, an invite link
    /// on a new phone), the primer waits for the answer instead of opening behind the name panel.
    /// </summary>
    public sealed class MatchLauncher : IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;

        private ScreenId _pending = ScreenId.None;
        private bool _starting;
        private bool _primerWaitsForName;

        public MatchLauncher(ScreenRouter router, ISettingsService settings, IAppFlow flow, IProfileService profile)
        {
            _router = router;
            _settings = settings;
            _flow = flow;
            _profile = profile;
        }

        /// <summary>True while How to Play is shown on the way into a play mode (or waits for the name to be answered).</summary>
        public bool HasPending => _pending != ScreenId.None;

        /// <summary>Opens a play mode's first screen; the very first time, How to Play comes first.</summary>
        public void Enter(ScreenId screen)
        {
            if (!_settings.HowToPlaySeen && _router.Has(ScreenId.HowToPlay))
            {
                _pending = screen;
                if (FirstRun.AsksName(_profile))
                {
                    WaitForName();
                    return;
                }

                _router.Push(ScreenId.HowToPlay);
                return;
            }

            _router.Push(screen);
        }

        public void Dispose() => StopWaitingForName();

        private void WaitForName()
        {
            if (!_primerWaitsForName)
            {
                _primerWaitsForName = true;
                _profile.Changed += OnProfileChanged;
            }
        }

        private void StopWaitingForName()
        {
            if (_primerWaitsForName)
            {
                _primerWaitsForName = false;
                _profile.Changed -= OnProfileChanged;
            }
        }

        /// <summary>The name was answered: the primer opens, on the way to the play mode that waits.</summary>
        private void OnProfileChanged()
        {
            if (FirstRun.AsksName(_profile))
            {
                return;
            }

            StopWaitingForName();
            if (_pending != ScreenId.None && _router.Has(ScreenId.HowToPlay))
            {
                _router.Push(ScreenId.HowToPlay);
            }
        }

        /// <summary>"Got It" on the primer: carries on to the play mode it interrupted, if any.</summary>
        public bool ContinuePending()
        {
            ScreenId screen = _pending;
            if (screen == ScreenId.None)
            {
                return false;
            }

            _pending = ScreenId.None;
            _router.Replace(screen);
            return true;
        }

        public void CancelPending()
        {
            _pending = ScreenId.None;
            StopWaitingForName();
        }

        /// <summary>Loads the table for <paramref name="request"/>.</summary>
        public void Launch(MatchLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_starting)
            {
                return;
            }

            _starting = true;
            _ = StartAsync(request);
        }

        private async Task StartAsync(MatchLaunchRequest request)
        {
            try
            {
                await _flow.StartMatchAsync(request, CancellationToken.None);
            }
            catch (Exception e)
            {
                _starting = false;
                Debug.LogException(e);
            }
        }
    }
}
