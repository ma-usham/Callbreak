using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Callbreak.Application.Localization;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The table's design-time states. The table's cards, fans, pill texts and timer are built at
    /// runtime, so the saved prefab is an empty table; for a capture (ScreenCaptureTool) this puts
    /// a mockup's exact state on an instance through the same view calls the game uses, with the
    /// texts taken from the string table.
    /// </summary>
    public static class TablePreview
    {
        /// <summary>Which mockup's moment to show.</summary>
        public enum Moment
        {
            /// <summary>08: your turn, A♥ lifted, trick 9♥ Q♥ 10♥ on the table.</summary>
            Gameplay,

            /// <summary>07: the deal is done and it is your turn to call.</summary>
            Bidding,
        }

        public const string Deck = "classic-crimson";

        /// <summary>The avatar presets the project scope hands to the table (built with the scenes).</summary>
        public const string AvatarLibraryPath = "Assets/_Project/Config/UI/AvatarLibrary.asset";

        private static readonly string[] Names = { null, "Suman", "Asha", "Bikash" };

        /// <summary>Called by ScreenCaptureTool for the Table prefab: mockup 08.</summary>
        public static void Prepare(GameObject instance)
        {
            Apply(instance.GetComponent<TableView>(), Moment.Gameplay);
        }

        public static void Apply(TableView view, Moment moment)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view), "The Table prefab has no TableView; rebuild it.");
            }

            ILocalization text = EditorStrings.Load();
            bool playing = moment == Moment.Gameplay;
            TableSkin skin = Skin();

            view.SetRound(text.Format("ui.table.round", 1, 5));
            view.SetTrick(null);
            view.Prompt.Hide();
            view.Emotes.Close();

            // Opponents: relative seats 1 right, 2 top, 3 left. Mockup 08 draws eight backs at the top
            // and five at each side.
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                SeatView seat = view.Seat(rel);
                seat.SetName(Names[rel]);
                seat.SetAvatar(skin.Avatar(0));
                seat.SetTurn(false);
                seat.HideBubble();

                // The mockups show three players who are there: no chip, no BOT tag.
                seat.SetChip(null, false);
                var info = new SeatStatusInfo(false, SeatConnection.Connected, false, playing ? 3 : CallRules.NotCalled, false, playing, playing ? 1 : 0);
                seat.SetStatus(SeatStatus.Text(info, text), false);
                seat.Fan.SetBack(skin.Back(Deck));
                seat.Fan.SetCount(rel == 2 ? 8 : 5);
            }

            SeatView you = view.Seat(0);
            you.SetName(text.Get("ui.table.you"));
            you.SetAvatar(skin.Avatar(0));
            you.HideBubble();
            you.SetChip(null, false);
            var mine = new SeatStatusInfo(true, SeatConnection.Connected, !playing, playing ? 4 : CallRules.NotCalled, false, playing, playing ? 2 : 0);
            you.SetStatus(SeatStatus.Text(mine, text), false);
            you.SetTurn(true);
            you.SetTimer(1f, playing ? 18 : 20, false, 0f);

            // The mockups' hands, in the game's one order: ♠ ♥ ♣ ♦ (ADR 021), where they draw ♦ before ♣.
            var hand = new List<Card>();
            HandSorter.Sort(CardSet.Parse(playing ? "A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣" : "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣"), hand);

            view.Hand.Clear();
            view.Hand.SetDeck(skin.Cards, Deck);
            view.Hand.SetCards(hand, false);

            // As drawn: only A♥ is lifted and only 8♥ is subdued (in the game every illegal card is).
            view.Hand.SetHighlights(
                playing ? CardSet.Of(Card.Parse("A♥")) : CardSet.Empty,
                playing ? CardSet.Of(Card.Parse("8♥")) : CardSet.Empty,
                false);

            view.Trick.Clear(view.Cards);
            if (playing)
            {
                // Suman led 9♥, Asha played Q♥, Bikash 10♥ (GDD §13 FIG 13.5).
                Place(view, skin, 1, Card.Parse("9♥"));
                Place(view, skin, 2, Card.Parse("Q♥"));
                Place(view, skin, 3, Card.Parse("10♥"));
            }
        }

        /// <summary>The table's art as the game gets it: the card library and the avatar presets.</summary>
        public static TableSkin Skin()
        {
            return new TableSkin(TableCardBuilder.EnsureLibrary(), AssetDatabase.LoadAssetAtPath<AvatarLibrary>(AvatarLibraryPath));
        }

        private static void Place(TableView view, TableSkin skin, int relativeSeat, Card card)
        {
            CardView cardView = view.Cards.Rent(view.Trick.Area);
            cardView.ShowFace(card, skin.Face(Deck, card), skin.Base(Deck));
            view.Trick.Adopt(relativeSeat, cardView);
            view.Trick.Settle(relativeSeat);
        }
    }

    /// <summary>The string tables on disk, in English, for design-time previews.</summary>
    public sealed class EditorStrings : ILocalization
    {
        private readonly StringTables _tables;

        private EditorStrings(StringTables tables)
        {
            _tables = tables;
        }

        public event Action LanguageChanged
        {
            add { }
            remove { }
        }

        public string Language => StringTables.DefaultLanguage;

        public IReadOnlyList<string> Languages => _tables.Languages;

        public static EditorStrings Load()
        {
            var tables = new StringTables();
            foreach (string table in new[] { "UI", "Rules", "Emotes" })
            {
                string path = $"{LocalizationTools.TableFolder}/{table}.csv";
                if (File.Exists(path))
                {
                    tables.AddTable(table, File.ReadAllText(path));
                }
            }

            return new EditorStrings(tables);
        }

        public string Get(string key)
        {
            return _tables.TryGet(StringTables.DefaultLanguage, key, out string value) ? value : key;
        }

        public string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        public void SetLanguage(string language)
        {
        }
    }
}
