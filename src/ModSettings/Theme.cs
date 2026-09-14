using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Inline styles matching the game's menus: near-black window, slate grey controls with a thin gold
    /// outline, and off-white text. Unity's default control skins are light grey with white text, so every
    /// built-in part (inputs, checkmarks, slider trackers, dropdown menus, scrollers) is restyled here.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Window = Rgb(0x17, 0x17, 0x17);
        public static readonly Color WindowBorder = Rgb(0xB9, 0xB9, 0xB9);
        public static readonly Color Surface = Rgb(0x22, 0x24, 0x29);
        public static readonly Color Control = Rgb(0x37, 0x3B, 0x45);
        public static readonly Color ControlHover = Rgb(0x46, 0x4B, 0x57);
        public static readonly Color Field = Rgb(0x14, 0x15, 0x18);
        public static readonly Color Line = Rgb(0x55, 0x5A, 0x64);
        public static readonly Color Gold = Rgb(0xF0, 0xC8, 0x5E);
        public static readonly Color Text = Rgb(0xF2, 0xF2, 0xF2);
        public static readonly Color Muted = Rgb(0xA9, 0xAE, 0xB7);

        private static readonly TextShadow Shadow = new TextShadow { offset = new Vector2(2f, 2f), color = new Color(0f, 0f, 0f, 0.8f) };

        /// <summary>A game-style button: slate fill, gold outline, lighter on hover or focus.</summary>
        public static void StyleButton(Button button, bool outlined = true)
        {
            var s = button.style;
            s.color = Text;
            s.textShadow = Shadow;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            SetPadding(button, 6f, 14f);
            SetBorder(button, 2f, outlined ? Gold : Line, 4f);
            SetHover(button, Control, ControlHover);
        }

        /// <summary>
        /// Restyles Unity's built-in control parts anywhere under <paramref name="root"/>. Call once per set of
        /// new controls: it registers hover callbacks.
        /// </summary>
        public static void StyleControls(VisualElement root)
        {
            root.Query(className: "unity-base-field__label").ForEach(e => e.style.color = Muted);

            root.Query(className: "unity-base-text-field__input").ForEach(e =>
            {
                e.style.backgroundColor = Field;
                e.style.color = Text;
                SetBorder(e, 1f, Line, 3f);
                e.Query<VisualElement>().ForEach(child => child.style.color = Text);
            });

            root.Query<Toggle>().ForEach(t => t.style.backgroundColor = Color.clear);
            root.Query(className: "unity-toggle__input").ForEach(e => e.style.backgroundColor = Color.clear);
            root.Query(className: "unity-toggle__checkmark").ForEach(e =>
            {
                e.style.width = e.style.height = 22f;
                e.style.backgroundColor = Field;
                e.style.unityBackgroundImageTintColor = Gold;
                SetBorder(e, 2f, Gold, 3f);
            });

            root.Query(className: "unity-base-popup-field__input").ForEach(e =>
            {
                SetBorder(e, 1f, Line, 3f);
                SetHover(e, Control, ControlHover);
                SetPadding(e, 4f, 10f);
            });
            root.Query(className: "unity-base-popup-field__text").ForEach(e => e.style.color = Text);
            root.Query(className: "unity-base-popup-field__arrow").ForEach(e => e.style.unityBackgroundImageTintColor = Text);

            root.Query(className: "unity-base-slider__tracker").ForEach(e =>
            {
                e.style.backgroundColor = Field;
                SetBorder(e, 1f, Line, 2f);
            });
            root.Query(className: "unity-base-slider__dragger").ForEach(e =>
            {
                e.style.backgroundColor = Gold;
                SetBorder(e, 0f, Color.clear, 2f);
            });
            root.Query(className: "unity-base-slider__dragger-border").ForEach(e => SetBorder(e, 0f, Color.clear, 0f));

            root.Query(className: "unity-scroller").ForEach(e =>
            {
                e.style.backgroundColor = Color.clear;
                SetBorder(e, 0f, Color.clear, 0f);
            });
            root.Query(className: "unity-scroller__low-button").ForEach(e => e.style.display = DisplayStyle.None);
            root.Query(className: "unity-scroller__high-button").ForEach(e => e.style.display = DisplayStyle.None);
            root.Query(className: "unity-scroller").ForEach(scroller =>
            {
                scroller.Query(className: "unity-base-slider__tracker").ForEach(e => SetBorder(e, 0f, Color.clear, 3f));
                scroller.Query(className: "unity-base-slider__dragger").ForEach(e => e.style.backgroundColor = Line);
            });
        }

        /// <summary>
        /// Dropdown menus open outside the panel, at the root of the UI document, so they get styled when opened.
        /// </summary>
        public static void StyleDropdownMenusWhenOpened(DropdownField dropdown)
        {
            EventCallback<EventBase> styleLater = _ => dropdown.schedule.Execute(() =>
            {
                var root = dropdown.panel?.visualTree;
                if (root == null)
                    return;
                root.Query(className: "unity-base-dropdown__container-outer").ForEach(e =>
                {
                    e.style.backgroundColor = Surface;
                    SetBorder(e, 1f, Gold, 3f);
                });
                root.Query(className: "unity-base-dropdown__container-inner").ForEach(e => e.style.backgroundColor = Surface);
                root.Query(className: "unity-base-dropdown__item").ForEach(e => SetHover(e, Surface, ControlHover));
                root.Query(className: "unity-base-dropdown__label").ForEach(e => e.style.color = Text);
                root.Query(className: "unity-base-dropdown__checkmark").ForEach(e => e.style.unityBackgroundImageTintColor = Gold);
            }).ExecuteLater(0L);
            dropdown.RegisterCallback<PointerDownEvent>(e => styleLater(e), TrickleDown.TrickleDown);
            dropdown.RegisterCallback<NavigationSubmitEvent>(e => styleLater(e), TrickleDown.TrickleDown);
        }

        /// <summary>Background that lightens under the pointer or when focused, replacing stylesheet :hover.</summary>
        public static void SetHover(VisualElement element, Color normal, Color hover)
        {
            var hovered = false;
            var focused = false;
            Action update = () => element.style.backgroundColor = hovered || focused ? hover : normal;
            element.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; update(); });
            element.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; update(); });
            element.RegisterCallback<FocusInEvent>(_ => { focused = true; update(); });
            element.RegisterCallback<FocusOutEvent>(_ => { focused = false; update(); });
            update();
        }

        public static void SetPadding(VisualElement element, float vertical, float horizontal)
        {
            var s = element.style;
            s.paddingTop = s.paddingBottom = vertical;
            s.paddingLeft = s.paddingRight = horizontal;
        }

        public static void SetBorder(VisualElement element, float width, Color color, float radius)
        {
            var s = element.style;
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = width;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = color;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = radius;
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
