using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Button for a <see cref="KeyboardShortcut"/>: click, then press the key with any modifiers held.
    /// Escape cancels and Delete or Backspace clears. Keys are read from UI Toolkit key events, so this works
    /// whether the game uses the legacy Input manager or the Input System package.
    /// </summary>
    internal static class KeyRebindEditor
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftCommand, KeyCode.RightCommand,
        };

        private static Button _listening;
        private static int _cancelledFrame = -1;

        /// <summary>
        /// True while a button waits for a key, or on the frame Escape cancelled one. The pause menu's own
        /// Escape handling checks this so cancelling a rebind doesn't also close the panel.
        /// </summary>
        public static bool ConsumesEscape => _listening != null || Time.frameCount - _cancelledFrame <= 1;

        public static void CancelListening()
        {
            if (_listening == null)
                return;
            var button = _listening;
            _listening = null;
            _cancelledFrame = Time.frameCount;
            (button.parent?.userData as Action)?.Invoke();
        }

        public static SettingEditor Create(ConfigEntryBase entry, Action<object> set)
        {
            var container = new VisualElement();
            var button = new Button();
            Theme.StyleButton(button, outlined: false);
            container.Add(button);

            Action refresh = () =>
            {
                if (_listening == button)
                    return;
                var shortcut = (KeyboardShortcut)entry.BoxedValue;
                button.text = shortcut.MainKey == KeyCode.None ? "Not bound" : shortcut.ToString();
            };
            // CancelListening restores the label through the container.
            container.userData = refresh;

            button.clicked += () =>
            {
                if (_listening == button)
                    return;
                CancelListening();
                _listening = button;
                button.text = "Press a key...";
                button.Focus();
            };

            button.RegisterCallback<KeyDownEvent>(e =>
            {
                if (_listening != button || e.keyCode == KeyCode.None)
                    return;
                e.StopPropagation();

                if (e.keyCode == KeyCode.Escape)
                {
                    CancelListening();
                    return;
                }
                if (Array.IndexOf(ModifierKeys, e.keyCode) >= 0)
                    return;

                _listening = null;
                if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                    set(KeyboardShortcut.Empty);
                else
                    set(new KeyboardShortcut(e.keyCode, HeldModifiers(e)));
                refresh();
            }, TrickleDown.TrickleDown);

            button.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_listening == button)
                    CancelListening();
            });

            return new SettingEditor { Control = container, Refresh = refresh };
        }

        private static KeyCode[] HeldModifiers(KeyDownEvent e)
        {
            var modifiers = new List<KeyCode>();
            if (e.ctrlKey)
                modifiers.Add(KeyCode.LeftControl);
            if (e.shiftKey)
                modifiers.Add(KeyCode.LeftShift);
            if (e.altKey)
                modifiers.Add(KeyCode.LeftAlt);
            if (e.commandKey)
                modifiers.Add(KeyCode.LeftCommand);
            return modifiers.ToArray();
        }
    }
}
