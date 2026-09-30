using Callbreak.Bootstrap;
using Callbreak.Presentation.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Editor
{
    /// <summary>
    /// Presses buttons by name while the game plays in the Editor, for end-to-end checks
    /// (Tools/Unity/playcheck.sh): an open dialog takes the press first, then the top screen of
    /// whichever scene is up. Every call returns a line saying what it found and did.
    /// </summary>
    public static class PlayDriver
    {
        /// <summary>Presses the named button on the open dialog, or else on the top screen.</summary>
        public static string Press(string buttonName)
        {
            if (!EditorApplication.isPlaying)
            {
                return "not in Play mode";
            }

            DialogView dialog = Object.FindFirstObjectByType<DialogView>(FindObjectsInactive.Include);
            if (dialog != null && dialog.IsShowing)
            {
                return PressUnder(dialog, buttonName, "the dialog");
            }

            ScreenRouter router = Router();
            if (router == null)
            {
                return "no screen router (no Menu or Table scene is up)";
            }

            UIScreen screen = router.Get<UIScreen>(router.Current);
            return screen == null ? "no screen is showing" : PressUnder(screen, buttonName, router.Current.ToString());
        }

        /// <summary>Presses the named button anywhere under the named object (the table's HUD is not a router screen).</summary>
        public static string PressIn(string rootName, string buttonName)
        {
            GameObject root = GameObject.Find(rootName);
            return root == null ? "no active object " + rootName : PressUnder(root.transform, buttonName, rootName);
        }

        /// <summary>The screen stack's top and depth, and whether a dialog is open.</summary>
        public static string Where()
        {
            ScreenRouter router = Router();
            DialogView dialog = Object.FindFirstObjectByType<DialogView>(FindObjectsInactive.Include);
            string screen = router != null ? router.Current + "/" + router.Depth : "no router";
            return screen + (dialog != null && dialog.IsShowing ? " +dialog" : string.Empty);
        }

        /// <summary>Dismisses the open dialog, if there is one, and nothing else.</summary>
        public static string CloseDialog()
        {
            DialogView dialog = Object.FindFirstObjectByType<DialogView>(FindObjectsInactive.Include);
            if (dialog == null || !dialog.IsShowing)
            {
                return "no dialog; " + Where();
            }

            ProjectLifetimeScope project = Object.FindFirstObjectByType<ProjectLifetimeScope>();
            project.Container.Resolve<Callbreak.Application.Ports.IDialogs>().HandleBack();
            return "closed the dialog; " + Where();
        }

        /// <summary>Android back, as the game hears it: an open dialog first, then the top screen.</summary>
        public static string Back()
        {
            ProjectLifetimeScope project = Object.FindFirstObjectByType<ProjectLifetimeScope>();
            if (project != null && project.Container != null && project.Container.Resolve<Callbreak.Application.Ports.IDialogs>().HandleBack())
            {
                return "closed the dialog; " + Where();
            }

            ScreenRouter router = Router();
            return router == null ? "no router" : (router.Back() ? "back; " : "nothing to go back to; ") + Where();
        }

        private static ScreenRouter Router()
        {
            TableLifetimeScope table = Object.FindFirstObjectByType<TableLifetimeScope>();
            if (table != null && table.Container != null)
            {
                return table.Container.Resolve<ScreenRouter>();
            }

            MenuLifetimeScope menu = Object.FindFirstObjectByType<MenuLifetimeScope>();
            return menu != null && menu.Container != null ? menu.Container.Resolve<ScreenRouter>() : null;
        }

        private static string PressUnder(Component root, string buttonName, string where)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name != buttonName)
                {
                    continue;
                }

                if (!button.IsInteractable())
                {
                    return buttonName + " on " + where + " cannot be pressed now";
                }

                button.onClick.Invoke();
                return "pressed " + buttonName + " on " + where + "; now " + Where();
            }

            return "no active button " + buttonName + " on " + where;
        }
    }
}
