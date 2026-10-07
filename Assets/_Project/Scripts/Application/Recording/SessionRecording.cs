using System;

namespace Callbreak.Application.Recording
{
    /// <summary>
    /// The session recording on phones (ADR 044): it starts the recorder (Smartlook) only while the
    /// player's privacy choice allows it (<see cref="RecordingConsentPolicy"/>), and stops it as soon
    /// as the choice no longer does. It is built with the app, so a player whose choice from an earlier
    /// session still holds is recorded from the launch; a first launch waits for Google's consent
    /// information, which the ads update when the menu opens.
    /// <para>
    /// Screens hide themselves with <see cref="HideScreen"/> (the name question and the Profile screen,
    /// whose fields hold the player's name): while any handle is open the recording keeps no picture
    /// of the screen. A recording that starts meanwhile starts hidden. Should the recorder fail to
    /// hide the screen, the recording stops until the last handle goes, so a hidden screen is never
    /// recorded.
    /// </para>
    /// It sends nothing of its own: no events, no player id, no name, no cards; the recorder records
    /// what the screen shows, and the table never shows another seat's cards face up. Every call and
    /// every <see cref="IRecordingConsent.Changed"/> comes on the main thread; nothing it is given
    /// makes it throw.
    /// </summary>
    public sealed class SessionRecording : ISessionRecording, IDisposable
    {
        private readonly ISessionRecorder _recorder;
        private readonly IRecordingConsent _consent;
        private readonly Action<string> _log;

        private int _hidden;
        private bool _recording;
        private bool _disposed;
        private string _lastReport;

        /// <param name="recorder">The SDK; only started once the choice allows it.</param>
        /// <param name="consent">The player's privacy choice, read now and whenever it may have changed.</param>
        /// <param name="log">Where the recording's state goes, a line each time it changes (no player data, no key); nowhere when null.</param>
        public SessionRecording(ISessionRecorder recorder, IRecordingConsent consent, Action<string> log = null)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _log = log ?? (_ => { });
            _consent.Changed += Apply;
            Apply();
        }

        /// <summary>The recorder is recording.</summary>
        public bool IsRecording => _recording;

        /// <summary>At least one screen is hidden from the recording.</summary>
        public bool IsScreenHidden => _hidden > 0;

        public IDisposable HideScreen()
        {
            _hidden++;
            if (_hidden == 1 && _recording && !TrySetHidden(true))
            {
                // Better no recording than a recorded name field.
                TryStop();
                _recording = false;
                Report("Session recording: off while a hidden screen is up (the screen could not be hidden).");
            }

            return new Handle(this);
        }

        /// <summary>Hears no more changes of the choice. A running recording is left to the SDK, which stops with the app.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _consent.Changed -= Apply;
        }

        private void Release()
        {
            if (_hidden == 0)
            {
                return;
            }

            _hidden--;
            if (_hidden > 0)
            {
                return;
            }

            if (_recording)
            {
                TrySetHidden(false);
            }
            else
            {
                // A recording stopped (or held back) while the screen was hidden may go on now.
                Apply();
            }
        }

        /// <summary>Starts or stops the recorder as the player's choice now says.</summary>
        private void Apply()
        {
            if (_disposed)
            {
                return;
            }

            ConsentSignals signals = ReadConsent();
            bool allowed = RecordingConsentPolicy.Allows(signals);
            if (allowed == _recording)
            {
                if (!allowed)
                {
                    Report("Session recording: off (" + Why(signals) + ").");
                }

                return;
            }

            if (!allowed)
            {
                TryStop();
                _recording = false;
                Report("Session recording: off (" + Why(signals) + ").");
                return;
            }

            bool hidden = _hidden > 0;
            if (!TryStart(hidden))
            {
                Report("Session recording: off (the recorder did not start; tried again when the choice next changes).");
                return;
            }

            _recording = true;
            Report(hidden ? "Session recording: on (the screen is hidden for now)." : "Session recording: on.");
        }

        private ConsentSignals ReadConsent()
        {
            try
            {
                return _consent.Current ?? ConsentSignals.None;
            }
            catch (Exception e)
            {
                _log("Session recording: the privacy choice could not be read: " + e.Message);
                return ConsentSignals.None;
            }
        }

        private static string Why(ConsentSignals signals)
        {
            switch (signals.State)
            {
                case ConsentState.Unknown:
                    return "the player's privacy choice is not known yet";
                case ConsentState.Required:
                    return "the consent form is not answered yet";
                default:
                    return "the player's privacy choice does not allow it";
            }
        }

        private bool TryStart(bool hidden)
        {
            try
            {
                return _recorder.Start(hidden);
            }
            catch (Exception e)
            {
                _log("Session recording: the recorder failed to start: " + e.Message);
                return false;
            }
        }

        private void TryStop()
        {
            try
            {
                _recorder.Stop();
            }
            catch (Exception e)
            {
                _log("Session recording: the recorder failed to stop: " + e.Message);
            }
        }

        private bool TrySetHidden(bool hidden)
        {
            try
            {
                return _recorder.SetHidden(hidden);
            }
            catch (Exception e)
            {
                _log("Session recording: the screen could not be " + (hidden ? "hidden" : "shown again") + ": " + e.Message);
                return false;
            }
        }

        /// <summary>Logs a state once, not again until it changes (the choice is re-read often).</summary>
        private void Report(string line)
        {
            if (line == _lastReport)
            {
                return;
            }

            _lastReport = line;
            _log(line);
        }

        /// <summary>One <see cref="HideScreen"/>; the screen shows again once every handle is disposed.</summary>
        private sealed class Handle : IDisposable
        {
            private SessionRecording _owner;

            public Handle(SessionRecording owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                SessionRecording owner = _owner;
                _owner = null;
                owner?.Release();
            }
        }
    }
}
