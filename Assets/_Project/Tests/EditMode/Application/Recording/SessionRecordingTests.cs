using System;
using System.Collections.Generic;
using Callbreak.Application.Recording;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The session recording on phones (ADR 044): the recorder starts only once the player's privacy
    /// choice allows it (a choice from an earlier session at once), and stops when the choice no
    /// longer does; a screen that hides itself (the name question, Profile) is never recorded, even
    /// when the recording starts while it is up or the recorder can't hide it. The log says when the
    /// state changes, once. Nothing the recorder or the choice does makes it throw.
    /// </summary>
    public sealed class SessionRecordingTests
    {
        private static readonly ConsentSignals Allowed = new ConsentSignals(ConsentState.NotRequired, null, string.Empty);
        private static readonly ConsentSignals Waiting = new ConsentSignals(ConsentState.Required, true, string.Empty);
        private static readonly ConsentSignals Refused = new ConsentSignals(ConsentState.Obtained, true, "00000000000");
        private static readonly ConsentSignals Consented = new ConsentSignals(ConsentState.Obtained, true, "11111111111");

        private ScriptedRecorder _recorder;
        private ScriptedConsent _consent;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _recorder = new ScriptedRecorder();
            _consent = new ScriptedConsent();
            _log = new List<string>();
        }

        [Test]
        public void AChoiceFromAnEarlierSession_StartsTheRecordingAtOnce()
        {
            _consent.Current = Allowed;

            SessionRecording recording = NewRecording();

            Assert.IsTrue(recording.IsRecording);
            CollectionAssert.AreEqual(new[] { "start" }, _recorder.Calls);
        }

        [Test]
        public void NothingKnownYet_RecordsNothing_UntilTheChoiceAllowsIt()
        {
            SessionRecording recording = NewRecording();

            Assert.IsFalse(recording.IsRecording);
            CollectionAssert.IsEmpty(_recorder.Calls, "the SDK is not even set up");

            _consent.Change(Waiting);
            Assert.IsFalse(recording.IsRecording, "the consent form is up");

            _consent.Change(Consented);
            Assert.IsTrue(recording.IsRecording);
            CollectionAssert.AreEqual(new[] { "start" }, _recorder.Calls);
        }

        [Test]
        public void DoNotConsent_RecordsNothing()
        {
            SessionRecording recording = NewRecording();

            _consent.Change(Refused);

            Assert.IsFalse(recording.IsRecording);
            CollectionAssert.IsEmpty(_recorder.Calls);
        }

        [Test]
        public void AWithdrawnConsent_StopsTheRecording_AndANewOneStartsItAgain()
        {
            _consent.Current = Consented;
            SessionRecording recording = NewRecording();

            _consent.Change(Refused);
            Assert.IsFalse(recording.IsRecording, "withdrawn in Settings' Privacy options");

            _consent.Change(Consented);
            Assert.IsTrue(recording.IsRecording);
            CollectionAssert.AreEqual(new[] { "start", "stop", "start" }, _recorder.Calls);
        }

        [Test]
        public void ARefreshThatChangesNothing_LeavesTheRecorderAlone_AndLogsNothingNew()
        {
            _consent.Current = Allowed;
            NewRecording();
            int lines = _log.Count;

            _consent.Change(Allowed);
            _consent.Change(Allowed);

            CollectionAssert.AreEqual(new[] { "start" }, _recorder.Calls);
            Assert.AreEqual(lines, _log.Count);
        }

        [Test]
        public void TheLog_SaysWhenTheStateChanges_Once()
        {
            SessionRecording recording = NewRecording();
            _consent.Change(ConsentSignals.None);
            _consent.Change(Allowed);
            _consent.Change(Refused);

            Assert.IsFalse(recording.IsRecording);
            Assert.AreEqual(3, _log.Count, string.Join(" | ", _log));
            StringAssert.Contains("not known yet", _log[0]);
            StringAssert.Contains("recording: on", _log[1]);
            StringAssert.Contains("does not allow it", _log[2]);
        }

        [Test]
        public void AHiddenScreen_IsHidden_WhileItsHandleIsOpen()
        {
            _consent.Current = Allowed;
            SessionRecording recording = NewRecording();

            IDisposable handle = recording.HideScreen();
            Assert.IsTrue(recording.IsScreenHidden);
            handle.Dispose();

            Assert.IsFalse(recording.IsScreenHidden);
            CollectionAssert.AreEqual(new[] { "start", "hide", "show" }, _recorder.Calls);
        }

        [Test]
        public void OverlappingHandles_KeepTheScreenHidden_UntilTheLastGoes_AndASecondDisposeDoesNothing()
        {
            _consent.Current = Allowed;
            SessionRecording recording = NewRecording();

            IDisposable first = recording.HideScreen();
            IDisposable second = recording.HideScreen();
            first.Dispose();
            first.Dispose();
            Assert.IsTrue(recording.IsScreenHidden, "the second handle is still open");

            second.Dispose();
            Assert.IsFalse(recording.IsScreenHidden);
            CollectionAssert.AreEqual(new[] { "start", "hide", "show" }, _recorder.Calls);
        }

        [Test]
        public void ARecordingThatStartsWhileAScreenIsHidden_StartsHidden()
        {
            SessionRecording recording = NewRecording();
            IDisposable handle = recording.HideScreen();

            _consent.Change(Allowed);
            Assert.IsTrue(recording.IsRecording);
            CollectionAssert.AreEqual(new[] { "start hidden" }, _recorder.Calls, "hidden from the first frame");

            handle.Dispose();
            CollectionAssert.AreEqual(new[] { "start hidden", "show" }, _recorder.Calls);
        }

        [Test]
        public void HidingWhileNothingRecords_TouchesNoRecorder()
        {
            SessionRecording recording = NewRecording();

            recording.HideScreen().Dispose();

            CollectionAssert.IsEmpty(_recorder.Calls);
        }

        [Test]
        public void AScreenTheRecorderCannotHide_StopsTheRecording_UntilTheScreenGoes()
        {
            _consent.Current = Allowed;
            SessionRecording recording = NewRecording();
            _recorder.HideFails = true;

            IDisposable handle = recording.HideScreen();
            Assert.IsFalse(recording.IsRecording, "better no recording than a recorded name field");
            CollectionAssert.AreEqual(new[] { "start", "hide failed", "stop" }, _recorder.Calls);

            _consent.Change(Consented);
            Assert.IsFalse(recording.IsRecording, "a start while hidden can't hide either");

            handle.Dispose();
            Assert.IsTrue(recording.IsRecording, "back once the screen has gone");
            Assert.AreEqual("start", _recorder.Calls[_recorder.Calls.Count - 1]);
        }

        [Test]
        public void ARecorderThatDoesNotStart_IsTriedAgainOnTheNextChange()
        {
            _recorder.StartFails = true;
            _consent.Current = Allowed;
            SessionRecording recording = NewRecording();
            Assert.IsFalse(recording.IsRecording);

            _recorder.StartFails = false;
            _consent.Change(Consented);

            Assert.IsTrue(recording.IsRecording);
        }

        [Test]
        public void ARecorderOrAChoiceThatThrows_DoesNotThrowIntoTheGame()
        {
            _recorder.Throws = true;
            _consent.Current = Allowed;
            SessionRecording recording = null;

            Assert.DoesNotThrow(() => recording = NewRecording());
            Assert.IsFalse(recording.IsRecording);

            _recorder.Throws = false;
            _consent.Throws = true;
            Assert.DoesNotThrow(() => _consent.Change(Allowed));
            Assert.IsFalse(recording.IsRecording, "a choice that can't be read records nothing");
        }

        [Test]
        public void OnceDisposed_ItHearsNoMoreChanges()
        {
            SessionRecording recording = NewRecording();

            recording.Dispose();
            _consent.Change(Allowed);

            Assert.IsFalse(recording.IsRecording);
            Assert.AreEqual(0, _consent.Listeners);
            Assert.DoesNotThrow(() => recording.HideScreen().Dispose());
        }

        [Test]
        public void ItSendsNothingOfItsOwn()
        {
            _consent.Current = Allowed;
            SessionRecording recording = NewRecording();
            recording.HideScreen().Dispose();
            _consent.Change(Refused);

            // The recorder port has no way to send events, ids or names: these are all the calls there are.
            CollectionAssert.AreEqual(new[] { "start", "hide", "show", "stop" }, _recorder.Calls);
        }

        private SessionRecording NewRecording() => new SessionRecording(_recorder, _consent, _log.Add);

        private sealed class ScriptedRecorder : ISessionRecorder
        {
            public readonly List<string> Calls = new List<string>();

            public bool StartFails { get; set; }

            public bool HideFails { get; set; }

            public bool Throws { get; set; }

            public bool Start(bool hidden)
            {
                ThrowIfAsked();
                if (StartFails || (hidden && HideFails))
                {
                    Calls.Add("start failed");
                    return false;
                }

                Calls.Add(hidden ? "start hidden" : "start");
                return true;
            }

            public void Stop()
            {
                ThrowIfAsked();
                Calls.Add("stop");
            }

            public bool SetHidden(bool hidden)
            {
                ThrowIfAsked();
                if (HideFails)
                {
                    Calls.Add(hidden ? "hide failed" : "show failed");
                    return false;
                }

                Calls.Add(hidden ? "hide" : "show");
                return true;
            }

            private void ThrowIfAsked()
            {
                if (Throws)
                {
                    throw new InvalidOperationException("the SDK broke");
                }
            }
        }

        private sealed class ScriptedConsent : IRecordingConsent
        {
            private ConsentSignals _current = ConsentSignals.None;
            private Action _changed;

            public event Action Changed
            {
                add
                {
                    _changed += value;
                    Listeners++;
                }

                remove
                {
                    _changed -= value;
                    Listeners--;
                }
            }

            public int Listeners { get; private set; }

            public bool Throws { get; set; }

            public ConsentSignals Current
            {
                get
                {
                    if (Throws)
                    {
                        throw new InvalidOperationException("UMP broke");
                    }

                    return _current;
                }

                set => _current = value;
            }

            public void Change(ConsentSignals signals)
            {
                _current = signals;
                _changed?.Invoke();
            }
        }
    }
}
