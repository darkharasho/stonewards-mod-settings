using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings.Patches
{
    /// <summary>
    /// Adds a "Mod Settings" button to the title screen, in the empty slot left of Latest Patch Note and above
    /// Roadmap, and opens <see cref="ModSettingsPanel"/> in the main menu's UI document.
    /// </summary>
    internal static class MainMenuIntegration
    {
        private static readonly AccessTools.FieldRef<MainMenu, SettingsScreen> SettingsScreenRef =
            AccessTools.FieldRefAccess<MainMenu, SettingsScreen>("settingsScreen");
        private static readonly AccessTools.FieldRef<MainMenu, MainMenuScreen> MainMenuScreenRef =
            AccessTools.FieldRefAccess<MainMenu, MainMenuScreen>("mainMenuScreen");
        private static readonly AccessTools.FieldRef<BaseUIScreen, List<VisualElement>> NavigableElements =
            AccessTools.FieldRefAccess<BaseUIScreen, List<VisualElement>>("navigableElements");

        private static MainMenu _menu;
        private static Button _modsButton;
        private static ModSettingsPanel _panel;

        public static void Attach(MainMenu menu)
        {
            var root = menu.GetComponent<UIDocument>()?.rootVisualElement;
            var screen = root?.Q<VisualElement>("MainMenuScreen");
            var roadmap = screen?.Q<Button>("MainMenu_Roadmap_Btn");
            var patchNote = screen?.Q<Button>("MainMenu_PatchNote_Btn");
            var discord = screen?.Q<Button>("MainMenu_Discord_Btn");
            if (roadmap == null || patchNote == null || discord == null)
            {
                Plugin.Log.LogWarning("Main menu layout not recognised; the Mod Settings button was not added");
                return;
            }

            _menu = menu;
            _modsButton = GameUi.CloneButton(roadmap, "MOD SETTINGS");
            _modsButton.clicked += Open;
            var s = _modsButton.style;
            s.position = Position.Absolute;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0f;
            screen.Add(_modsButton);
            var mainMenuScreen = MainMenuScreenRef(menu);
            if (mainMenuScreen != null)
                NavigableElements(mainMenuScreen).Add(_modsButton);
            new SlotPlacer(_modsButton, screen, roadmap, patchNote, discord);

            _panel?.Root.RemoveFromHierarchy();
            _panel = new ModSettingsPanel(GameUi.StyleSheetsOf(SettingsScreenRef(menu)?.SettingsScreenElement));
            _panel.CloseRequested += Close;
            root.Add(_panel.Root);
        }

        private static void Open()
        {
            if (_menu == null || _panel == null || _panel.IsOpen)
                return;
            AudioManager.Instance?.PlayUIButton();
            MainMenuScreenRef(_menu)?.SetNavigable(false);
            _panel.Open();
            if (InputManager.Instance != null)
                InputManager.Instance.OnMainMenuReturn += OnReturn;
        }

        private static void OnReturn()
        {
            if (KeyRebindEditor.ConsumesEscape)
                KeyRebindEditor.CancelListening();
            else
                Close();
        }

        private static void Close()
        {
            if (_panel == null || !_panel.IsOpen)
                return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnMainMenuReturn -= OnReturn;
            AudioManager.Instance?.PlayUIButton();
            _panel.Close();
            if (_menu != null)
                _menu.RestoreMainMenuNavigation();
            var button = _modsButton;
            button?.schedule.Execute(() => button.Focus()).ExecuteLater(0L);
        }

        public static void Detach(MainMenu menu)
        {
            if (menu != _menu)
                return;
            if (_panel != null && _panel.IsOpen)
            {
                if (InputManager.Instance != null)
                    InputManager.Instance.OnMainMenuReturn -= OnReturn;
                _panel.Close();
            }
            _menu = null;
            _panel = null;
            _modsButton = null;
        }

        /// <summary>
        /// Keeps the button in the grid slot formed by the three corner buttons: Roadmap's left edge, Latest
        /// Patch Note's row, and the same gap to Patch Note as between Roadmap and Discord. Positions are read
        /// from the laid-out buttons, so it follows resolution and aspect ratio changes.
        /// </summary>
        private sealed class SlotPlacer
        {
            private readonly VisualElement _button;
            private readonly VisualElement _container;
            private readonly VisualElement _roadmap;
            private readonly VisualElement _patchNote;
            private readonly VisualElement _discord;
            // Difference between style left/top and the laid-out position (the container's border and padding).
            private Vector2 _offset;

            public SlotPlacer(VisualElement button, VisualElement container, VisualElement roadmap, VisualElement patchNote, VisualElement discord)
            {
                _button = button;
                _container = container;
                _roadmap = roadmap;
                _patchNote = patchNote;
                _discord = discord;
                foreach (var watched in new[] { container, roadmap, patchNote, discord })
                    watched.RegisterCallback<GeometryChangedEvent>(_ => Place());
                button.RegisterCallback<GeometryChangedEvent>(_ => Correct());
            }

            private Rect Target()
            {
                var roadmap = _container.WorldToLocal(_roadmap.worldBound);
                var patchNote = _container.WorldToLocal(_patchNote.worldBound);
                var discord = _container.WorldToLocal(_discord.worldBound);
                var gap = discord.xMin - roadmap.xMax;
                var width = patchNote.xMin - gap - roadmap.xMin;
                if (gap < 0f || width < roadmap.width * 0.5f)
                    width = roadmap.width;
                return new Rect(roadmap.xMin, patchNote.yMin, width, patchNote.height);
            }

            private void Place()
            {
                if (_roadmap.layout.width <= 0f || _patchNote.layout.width <= 0f)
                    return;
                var target = Target();
                var s = _button.style;
                s.left = target.x - _offset.x;
                s.top = target.y - _offset.y;
                s.width = target.width;
                s.height = target.height;
            }

            private void Correct()
            {
                if (_roadmap.layout.width <= 0f)
                    return;
                var actual = _container.WorldToLocal(_button.worldBound).position;
                var error = actual - Target().position;
                if (error.sqrMagnitude < 0.25f)
                    return;
                _offset += error;
                Place();
            }
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Start")]
    internal static class MainMenuStartPatch
    {
        private static void Postfix(MainMenu __instance)
        {
            try
            {
                MainMenuIntegration.Attach(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Could not add the Mod Settings button to the main menu: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(MainMenu), "OnDestroy")]
    internal static class MainMenuDestroyPatch
    {
        private static void Prefix(MainMenu __instance) => MainMenuIntegration.Detach(__instance);
    }
}
