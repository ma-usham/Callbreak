using System;
using System.Collections.Generic;
using Callbreak.Bots;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// Full matches with a bot in every seat (GDD §9 "a bot can never make an illegal move"): every call is in
    /// range, every card is from <c>view.LegalCards()</c>, every command is accepted and every GDD §22 invariant
    /// holds after each one. <see cref="BotMatches.Play"/> asserts all of that.
    /// </summary>
    public sealed class BotFuzzTests
    {
        [Test]
        public void Play_MixedLevelsUnderEveryLegalityCombination_OnlyLegalMoves()
        {
            // The fast share of the fuzz: one short match per combination of the four legality switches.
            int match = 0;
            foreach (RuleSet legality in RuleVariants.LegalityCombinations())
            {
                RuleSet rules = Variant(legality.With(rounds: 1), match);
                PlayerView final = BotMatches.Play(rules, 4000UL + (ulong)match, Table(match, hardSamples: 4));

                Assert.AreEqual(Phase.MatchOver, final.Phase);
                Assert.AreEqual(4, final.FinalStandings.Count);
                match++;
            }

            Assert.AreEqual(16, match);
        }

        [Test]
        public void Play_SameSeedAndBots_SameMatch()
        {
            PlayerView first = BotMatches.Play(RuleSet.NepaliStandard.With(rounds: 2), 99UL, Table(3, hardSamples: 8));
            PlayerView second = BotMatches.Play(RuleSet.NepaliStandard.With(rounds: 2), 99UL, Table(3, hardSamples: 8));

            Assert.AreEqual(first, second, "same deal and same decision seed: the same calls and cards");
        }

        [Test]
        [Explicit("1,000 full matches; run with --filter \"TestCategory=Slow\"")]
        [Category("Slow")]
        public void Play_ThousandMixedMatches_OnlyLegalMoves()
        {
            const int Matches = 1000;
            var rules = new List<RuleSet>(RuleVariants.LegalityCombinations());
            var timings = new DecisionTimings();
            for (int match = 0; match < Matches; match++)
            {
                // Three matches in four use the shipped rules; the fourth walks through the variants.
                RuleSet ruleSet = match % 4 == 3 ? Variant(rules[(match / 4) % rules.Count], match) : RuleSet.NepaliStandard;
                PlayerView final = BotMatches.Play(ruleSet, 70000UL + (ulong)match, Table(match, hardSamples: 16), timings);

                Assert.AreEqual(Phase.MatchOver, final.Phase);
                Assert.AreEqual(ruleSet.Rounds, final.History.Count);
            }

            TestContext.Out.WriteLine("Fuzz: " + Matches + " matches. " + timings.Describe(BotLevel.Easy) + "; "
                + timings.Describe(BotLevel.Normal) + "; " + timings.Describe(BotLevel.Hard) + " (16 samples)");
        }

        /// <summary>
        /// The four seats of match <paramref name="match"/>: Easy, Normal and Hard in rotation, so every table
        /// mixes all three levels (one of them twice) and every seat plays every level.
        /// </summary>
        private static IBotStrategy[] Table(int match, int hardSamples)
        {
            var seats = new IBotStrategy[4];
            for (int s = 0; s < 4; s++)
            {
                switch ((match + s) % 3)
                {
                    case 0:
                        seats[s] = new EasyStrategy();
                        break;
                    case 1:
                        seats[s] = new NormalStrategy();
                        break;
                    default:
                        seats[s] = new HardStrategy(hardSamples);
                        break;
                }
            }

            return seats;
        }

        /// <summary>Also varies direction, call range, forced-move auto-play and the big-call bonus.</summary>
        private static RuleSet Variant(RuleSet rules, int match)
        {
            return rules.With(
                direction: match % 2 == 0 ? Direction.Anticlockwise : Direction.Clockwise,
                minCall: match % 5 == 4 ? 2 : 1,
                maxCall: match % 3 == 2 ? 13 : 8,
                bigCallBonus: match % 7 == 6,
                autoPlayForcedMoves: match % 3 == 1,
                autoPlayLastTrick: match % 4 != 1);
        }
    }
}
