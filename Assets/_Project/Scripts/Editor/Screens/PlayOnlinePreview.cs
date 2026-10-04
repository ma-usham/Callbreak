using System.Collections.Generic;
using System.Globalization;
using Callbreak.Application.Economy;
using Callbreak.Presentation.Screens;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Play Online in the states of its mockups, for the screen captures (the presenter fills it at
    /// run time): 24, 100 coins chosen with its prizes open (what screen.sh compares, as the layout is
    /// measured on 24), and 23, 50 coins chosen with its prizes open; also the screen as it first
    /// opens (50 chosen, prizes closed). The words are the mockups' (the balance is their 250).
    /// </summary>
    public static class PlayOnlinePreview
    {
        public static void Prepare(GameObject instance) => PrepareStake(instance, chip: 1, prizesOpen: true);

        /// <summary>Captures 23's and 24's states and the opening state at 16:9, 21:9 and 4:3 into Temp/Captures.</summary>
        public static string CaptureAll()
        {
            string prefab = ScreenLayoutBuilder.PrefabPath("PlayOnline");
            var states = new[] { ("Stake50", 0, true), ("Stake100", 1, true), ("Opening", 0, false) };
            foreach ((string name, int chip, bool open) in states)
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2520, 1080), new Vector2Int(1920, 1440) })
                {
                    ScreenCaptureTool.Capture(
                        prefab, size.x, size.y, $"{ScreenCaptureTool.OutputFolder}/PlayOnline_{name}_{size.x}x{size.y}.png", go => PrepareStake(go, chip, open));
                }
            }

            return "captured PlayOnline_{Stake50,Stake100,Opening}_{1920x1080,2520x1080,1920x1440}.png";
        }

        public static void PrepareStake(GameObject instance, int chip, bool prizesOpen)
        {
            var view = instance.GetComponent<PlayOnlineView>();
            if (view == null)
            {
                return;
            }

            IReadOnlyList<int> stakes = StakeRules.Stakes;
            int stake = stakes[chip];
            view.SetBalance("250");
            for (int i = 0; i < view.ChipCount && i < stakes.Count; i++)
            {
                view.SetChip(i, Number(stakes[i]));
            }

            view.SetSelectedChip(chip);
            view.SetPlayStake($"Play for {Number(stake)} coins");
            var prizes = new List<string>();
            foreach (int prize in StakeRules.PrizesFor(stake))
            {
                prizes.Add(Number(prize));
            }

            view.SetPrizes($"{Number(stake)}-coin prizes", prizes);
            view.ShowPrizes(prizesOpen, animate: false);
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
