using System;
using System.Collections.Generic;
using Callbreak.Application.Analytics;
using Callbreak.Application.Ports;
using Callbreak.Application.Recording;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Analytics
{
    public sealed class ProductAnalyticsTests
    {
        [TestCase(ConsentState.Unknown)]
        [TestCase(ConsentState.Required)]
        public void Track_UnknownOrUnansweredConsent_DropsEvents(ConsentState state)
        {
            var consent = new Consent(new ConsentSignals(state, null, string.Empty));
            var sink = new Sink();
            var collection = new List<bool>();
            using (var analytics = new ProductAnalytics(sink, collection.Add, consent))
            {
                analytics.Track("room_joined");
                Assert.AreEqual(0, sink.Events.Count);
                Assert.IsFalse(collection[collection.Count - 1]);
            }
        }

        [Test]
        public void Track_GdprOrMissingGdprFlagWithTcf_DropsEvents()
        {
            foreach (bool? gdpr in new bool?[] { true, null })
            {
                var consent = new Consent(new ConsentSignals(ConsentState.Obtained, gdpr, "11111111111"));
                var sink = new Sink();
                using (var analytics = new ProductAnalytics(sink, _ => { }, consent))
                {
                    analytics.Track("room_joined");
                    Assert.AreEqual(0, sink.Events.Count);
                }
            }
        }

        [Test]
        public void Changed_AllowedThenRevoked_StopsAndDoesNotReplayEvents()
        {
            var consent = new Consent(ConsentSignals.None);
            var sink = new Sink();
            var collection = new List<bool>();
            using (var analytics = new ProductAnalytics(sink, collection.Add, consent))
            {
                analytics.Track("room_created", ("turn_seconds", 15));
                consent.Set(new ConsentSignals(ConsentState.NotRequired, false, string.Empty));
                analytics.Track("room_joined");
                consent.Set(new ConsentSignals(ConsentState.Required, true, string.Empty));
                analytics.Track("room_joined");
                Assert.AreEqual(1, sink.Events.Count);
                Assert.AreEqual("room_joined", sink.Events[0]);
                CollectionAssert.AreEqual(new[] { false, true, false }, collection);
            }
        }

        [Test]
        public void Dispose_DisablesCollectionAndUnsubscribes()
        {
            var consent = Allowed();
            var sink = new Sink();
            var collection = new List<bool>();
            var analytics = new ProductAnalytics(sink, collection.Add, consent);
            analytics.Dispose();
            int count = collection.Count;
            consent.Set(ConsentSignals.None);
            analytics.Track("room_joined");
            Assert.AreEqual(count, collection.Count);
            Assert.IsFalse(collection[count - 1]);
            Assert.AreEqual(0, sink.Events.Count);
        }

        [Test]
        public void Track_ExistingGameEvents_PreservesTheirParameters()
        {
            var sink = new Sink();
            using (var analytics = new ProductAnalytics(sink, _ => { }, Allowed()))
            {
                analytics.Track("match_start", ("mode", "quick_match"), ("difficulty", "none"));
                analytics.Track("bot_fill", ("seats", 2));
                analytics.Track("round_end", ("round", 1), ("call", 3), ("won", 4));
                analytics.Track("auto_play", ("kind", "card"));
                analytics.Track("reconnect", ("ok", true), ("secs", 12));
                analytics.Track("match_end", ("rank", 1), ("duration", 200), ("abandoned", false));
                analytics.Track("room_created", ("turn_seconds", 15));
                analytics.Track("room_joined");
                Assert.AreEqual(8, sink.Events.Count);
                Assert.AreEqual(("turn_seconds", (object)15), sink.Parameters[0]);
            }
        }

        [TestCase("offline")]
        [TestCase("private_room")]
        [TestCase("quick_match")]
        public void Track_MatchMode_IsAccepted(string mode)
        {
            var sink = new Sink();
            using (var analytics = new ProductAnalytics(sink, _ => { }, Allowed()))
            {
                analytics.Track("match_start", ("mode", mode));
                Assert.AreEqual(1, sink.Events.Count);
            }
        }

        [TestCase("name")]
        [TestCase("hand")]
        [TestCase("cards")]
        [TestCase("player_id")]
        [TestCase("room_code")]
        public void Track_UnexpectedParameter_DropsEntireEvent(string key)
        {
            var sink = new Sink();
            using (var analytics = new ProductAnalytics(sink, _ => { }, Allowed()))
            {
                analytics.Track("match_start", ("mode", "offline"), (key, "private value"));
                Assert.AreEqual(0, sink.Events.Count);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("private_event")]
        public void Track_UnknownEvent_DropsEvent(string name)
        {
            var sink = new Sink();
            using (var analytics = new ProductAnalytics(sink, _ => { }, Allowed()))
            {
                analytics.Track(name);
                Assert.AreEqual(0, sink.Events.Count);
            }
        }

        [Test]
        public void Track_FreeTextOrUnsupportedTypeOrDuplicateKey_DropsEvent()
        {
            var sink = new Sink();
            using (var analytics = new ProductAnalytics(sink, _ => { }, Allowed()))
            {
                analytics.Track("match_start", ("mode", "a player's name"));
                analytics.Track("round_end", ("call", new object()));
                analytics.Track("round_end", ("call", "3"));
                analytics.Track("round_end", ("call", 3), ("call", 4));
                analytics.Track("round_end", ("call", null));
                Assert.AreEqual(0, sink.Events.Count);
            }
        }

        [Test]
        public void Track_SdkThrows_DoesNotThrowOrLogPayload()
        {
            var reports = new List<string>();
            using (var analytics = new ProductAnalytics(new Sink { Throws = true }, _ => { }, Allowed(), reports.Add))
            {
                Assert.DoesNotThrow(() => analytics.Track("room_joined"));
                Assert.AreEqual(1, reports.Count);
                Assert.IsFalse(reports[0].Contains("private SDK details"));
            }
        }

        [Test]
        public void Changed_ConsentReadThrows_DisablesCollection()
        {
            var consent = Allowed();
            var sink = new Sink();
            var collection = new List<bool>();
            using (var analytics = new ProductAnalytics(sink, collection.Add, consent))
            {
                consent.Throws = true;
                consent.Set(ConsentSignals.None);
                analytics.Track("room_joined");
                Assert.IsFalse(collection[collection.Count - 1]);
                Assert.AreEqual(0, sink.Events.Count);
            }
        }

        private static Consent Allowed() => new Consent(new ConsentSignals(ConsentState.NotRequired, false, string.Empty));

        private sealed class Consent : IRecordingConsent
        {
            private ConsentSignals _current;
            public bool Throws;
            public Consent(ConsentSignals current) { _current = current; }
            public event Action Changed;
            public ConsentSignals Current => Throws ? throw new InvalidOperationException() : _current;
            public void Set(ConsentSignals value) { _current = value; Changed?.Invoke(); }
        }

        private sealed class Sink : IAnalytics
        {
            public readonly List<string> Events = new List<string>();
            public (string key, object value)[] Parameters;
            public bool Throws;
            public void Track(string eventName, params (string key, object value)[] parameters)
            {
                if (Throws) { throw new InvalidOperationException("private SDK details"); }
                Events.Add(eventName);
                if (parameters.Length > 0) { Parameters = parameters; }
            }
        }
    }
}
