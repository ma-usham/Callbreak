using System;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using Callbreak.Application.Recording;

namespace Callbreak.Application.Analytics
{
    /// <summary>
    /// The existing product events behind a collection gate and an explicit field list. Nothing
    /// is buffered while collection is blocked. UMP's advertising consent is not treated as
    /// analytics consent: until a separate analytics choice exists, GDPR regions collect nothing.
    /// </summary>
    public sealed class ProductAnalytics : IAnalytics, IDisposable
    {
        private readonly IAnalytics _sink;
        private readonly Action<bool> _setCollection;
        private readonly IRecordingConsent _consent;
        private readonly Action<string> _log;
        private bool _allowed;
        private bool _disposed;

        public ProductAnalytics(IAnalytics sink, Action<bool> setCollection, IRecordingConsent consent, Action<string> log = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _setCollection = setCollection ?? throw new ArgumentNullException(nameof(setCollection));
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _log = log ?? (_ => { });
            SetCollection(false);
            _consent.Changed += Apply;
            Apply();
        }

        public void Track(string eventName, params (string key, object value)[] parameters)
        {
            if (_disposed || !_allowed || !ValidEvent(eventName, parameters))
            {
                return;
            }

            try
            {
                _sink.Track(eventName, parameters ?? Array.Empty<(string key, object value)>());
            }
            catch (Exception)
            {
                // SDK exceptions can carry configuration or input data; none belongs in a log.
                _log("Product analytics: an event could not be delivered.");
            }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _consent.Changed -= Apply;
            SetCollection(false);
        }

        private void Apply()
        {
            bool allowed = false;
            try
            {
                ConsentSignals signals = _consent.Current;
                allowed = signals != null
                    && (signals.State == ConsentState.NotRequired || signals.State == ConsentState.Obtained)
                    && signals.GdprApplies != true
                    && signals.PurposeConsents.Length == 0;
            }
            catch (Exception) { /* An unreadable choice blocks collection. */ }

            if (allowed != _allowed) { SetCollection(allowed); }
        }

        private void SetCollection(bool allowed)
        {
            _allowed = false;
            try
            {
                _setCollection(allowed);
                _allowed = allowed;
            }
            catch (Exception)
            {
                _log("Product analytics: collection could not be configured; events are blocked.");
            }
        }

        private static bool ValidEvent(string name, (string key, object value)[] parameters)
        {
            switch (name)
            {
                case "match_start": case "bot_fill": case "round_end": case "auto_play":
                case "reconnect": case "match_end": case "room_created": case "room_joined":
                    break;
                default:
                    return false;
            }

            if (parameters == null) { return true; }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (!keys.Add(parameter.key) || !ValidParameter(name, parameter.key, parameter.value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidParameter(string name, string key, object value)
        {
            bool integer = value is int || value is long;
            switch (name)
            {
                case "match_start":
                    return key == "mode" && (Equals(value, "offline") || Equals(value, "private_room") || Equals(value, "quick_match"))
                        || key == "difficulty" && (Equals(value, "none") || Equals(value, "easy") || Equals(value, "normal") || Equals(value, "hard"));
                case "bot_fill": return key == "seats" && integer;
                case "round_end": return (key == "round" || key == "call" || key == "won") && integer;
                case "auto_play": return key == "kind" && (Equals(value, "call") || Equals(value, "card"));
                case "reconnect": return key == "ok" && value is bool || key == "secs" && integer;
                case "match_end": return (key == "rank" || key == "duration") && integer || key == "abandoned" && value is bool;
                case "room_created": return key == "turn_seconds" && integer;
                default: return false;
            }
        }
    }
}
