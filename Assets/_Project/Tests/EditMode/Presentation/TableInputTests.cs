using System;
using Callbreak.Core.Commands;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The table and the call panel act only while nothing covers them (GDD §11 screens 09-13 and
    /// 16: online the match runs on under the results, the scorecard, the menu and the settings),
    /// and a call that is refused says why (screen 07).
    /// </summary>
    public sealed class TableInputTests
    {
        private static readonly TableStrings Strings = new TableStrings();

        [Test]
        public void TheTableTakesInput_OnlyWhileNothingCoversIt()
        {
            Assert.IsTrue(TableInput.IsOpen(ScreenId.Gameplay), "the bare table");
            Assert.IsTrue(TableInput.IsOpen(ScreenId.None), "a table with no screen over it at all");

            // Every page that opens over a running match.
            foreach (ScreenId over in new[]
            {
                ScreenId.Scorecard, ScreenId.RoundResults, ScreenId.MatchResults, ScreenId.Settings, ScreenId.HowToPlay, ScreenId.MatchMenu,
            })
            {
                Assert.IsFalse(TableInput.IsOpen(over), over + " covers the table");
            }

            // And any screen added later covers it too, until somebody decides otherwise.
            foreach (ScreenId screen in Enum.GetValues(typeof(ScreenId)))
            {
                if (screen != ScreenId.Gameplay && screen != ScreenId.None)
                {
                    Assert.IsFalse(TableInput.IsOpen(screen), screen.ToString());
                }
            }
        }

        [Test]
        public void ARefusedCall_SaysWhy()
        {
            Assert.AreEqual(CallRefusal.LateKey, CallRefusal.KeyFor(RejectReason.StaleTurn));
            Assert.AreEqual(CallRefusal.LateKey, CallRefusal.KeyFor(RejectReason.NotYourTurn));
            Assert.AreEqual(CallRefusal.InvalidKey, CallRefusal.KeyFor(RejectReason.InvalidCall));
            Assert.AreEqual(CallRefusal.NotSentKey, CallRefusal.KeyFor(RejectReason.WrongPhase), "an online session that cannot act answers WrongPhase");

            // Every reason has a line in the shipped table, and none is left without words.
            foreach (RejectReason reason in Enum.GetValues(typeof(RejectReason)))
            {
                if (reason == RejectReason.None)
                {
                    continue;
                }

                string key = CallRefusal.KeyFor(reason);
                Assert.IsTrue(Strings.Has(key), reason + ": " + key + " is missing from Assets/_Project/Localization/UI.csv");
                Assert.IsNotEmpty(Strings.Get(key));
            }
        }
    }
}
