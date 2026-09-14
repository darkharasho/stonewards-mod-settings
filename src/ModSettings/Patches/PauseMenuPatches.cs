using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UIElements;

namespace ModSettings.Patches
{
    /// <summary>
    /// Adds a "Mods" button under the pause menu's Settings button and opens <see cref="ModSettingsPanel"/>
    /// in the same UI document, borrowing the settings screen's style sheets so controls match the game.
    /// </summary>
    internal static class PauseMenuIntegration
    {
        private static readonly AccessTools.FieldRef<UIPauseMenu, Button> SettingsButton =
            AccessTools.FieldRefAccess<UIPauseMenu, Button>("settingsButton");
        private static readonly AccessTools.FieldRef<UIPauseMenu, SettingsScreen> SettingsScreenRef =
            AccessTools.FieldRefAccess<UIPauseMenu, SettingsScreen>("settingsScreen");
        private static readonly AccessTools.FieldRef<BaseMenu, List<VisualElement>> NavigableElements =
            AccessTools.FieldRefAccess<BaseMenu, List<VisualElement>>("navigableElements");
        private static readonly Action<BaseMenu, bool> SetNavigable =
            AccessTools.MethodDelegate<Action<BaseMenu, bool>>(AccessTools.Method(typeof(BaseMenu), "SetNavigable"));

        private static UIPauseMenu _menu;
        private static Button _modsButton;

        public static ModSettingsPanel Panel { get; private set; }

        public static void Attach(UIPauseMenu menu)
        {
            var settingsButton = SettingsButton(menu);
            var root = menu.GetComponent<UIDocument>()?.rootVisualElement;
            if (settingsButton?.parent == null || root == null)
            {
                Plugin.Log.LogWarning("Pause menu layout not recognised; the Mods button was not added");
                return;
            }

            _menu = menu;
            _modsButton = GameUi.CloneButton(settingsButton, "Mods");
            _modsButton.clicked += Open;
            var parent = settingsButton.parent;
            parent.Insert(parent.IndexOf(settingsButton) + 1, _modsButton);
            NavigableElements(menu).Add(_modsButton);
            GameUi.GrowAncestorsToFit(_modsButton, root);

            Panel?.Root.RemoveFromHierarchy();
            Panel = new ModSettingsPanel(GameUi.StyleSheetsOf(SettingsScreenRef(menu)?.SettingsScreenElement));
            Panel.CloseRequested += Close;
            root.Add(Panel.Root);
        }

        /// <summary>Returns true if the panel handled Escape/back, so the pause menu should ignore it.</summary>
        public static bool HandleBack()
        {
            if (KeyRebindEditor.ConsumesEscape)
            {
                KeyRebindEditor.CancelListening();
                return true;
            }
            if (Panel == null || !Panel.IsOpen)
                return false;
            Close();
            return true;
        }

        /// <summary>The pause menu was hidden some other way (e.g. the game state changed).</summary>
        public static void ForceClose()
        {
            if (Panel == null || !Panel.IsOpen)
                return;
            Panel.Close();
            if (_menu != null)
                SetNavigable(_menu, true);
        }

        private static void Open()
        {
            if (_menu == null || Panel == null)
                return;
            AudioManager.Instance?.PlayUIButton();
            SetNavigable(_menu, false);
            Panel.Open();
        }

        private static void Close()
        {
            AudioManager.Instance?.PlayUIButton();
            ForceClose();
            var button = _modsButton;
            button?.schedule.Execute(() => button.Focus()).ExecuteLater(0L);
        }
    }

    [HarmonyPatch(typeof(UIPauseMenu), nameof(UIPauseMenu.Init))]
    internal static class PauseMenuInitPatch
    {
        private static void Postfix(UIPauseMenu __instance)
        {
            try
            {
                PauseMenuIntegration.Attach(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Could not add the Mods button to the pause menu: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(UIPauseMenu), nameof(UIPauseMenu.OnPauseExitButton))]
    internal static class PauseMenuBackPatch
    {
        private static bool Prefix() => !PauseMenuIntegration.HandleBack();
    }

    [HarmonyPatch(typeof(UIPauseMenu), nameof(UIPauseMenu.PauseShow))]
    internal static class PauseMenuHidePatch
    {
        private static void Postfix(bool _Show)
        {
            if (!_Show)
                PauseMenuIntegration.ForceClose();
        }
    }
}
