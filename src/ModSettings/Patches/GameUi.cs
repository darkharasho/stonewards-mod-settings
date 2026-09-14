using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings.Patches
{
    /// <summary>Helpers for adding elements to the game's UI documents without knowing their exact layout.</summary>
    internal static class GameUi
    {
        /// <summary>Copies the look of a game button: its classes, inline styles and a text label child if it uses one.</summary>
        public static Button CloneButton(Button source, string text)
        {
            var button = new Button { name = "ModSettingsButton" };
            foreach (var cls in source.GetClasses())
                button.AddToClassList(cls);
            CopyInlineStyle(source.style, button.style);

            var sourceLabel = source.Q<Label>();
            if (string.IsNullOrEmpty(source.text) && sourceLabel != null)
            {
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                foreach (var cls in sourceLabel.GetClasses())
                    label.AddToClassList(cls);
                CopyInlineStyle(sourceLabel.style, label.style);
                button.Add(label);
            }
            else
            {
                button.text = text;
            }
            return button;
        }

        /// <summary>
        /// Visual properties set inline in the game's UXML (e.g. the blue main menu buttons). Unset properties
        /// copy as unset, so class styles still apply.
        /// </summary>
        private static void CopyInlineStyle(IStyle from, IStyle to)
        {
            to.backgroundColor = from.backgroundColor;
            to.backgroundImage = from.backgroundImage;
            to.unityBackgroundImageTintColor = from.unityBackgroundImageTintColor;
            to.unitySliceLeft = from.unitySliceLeft;
            to.unitySliceRight = from.unitySliceRight;
            to.unitySliceTop = from.unitySliceTop;
            to.unitySliceBottom = from.unitySliceBottom;
            to.unitySliceScale = from.unitySliceScale;
            to.borderTopWidth = from.borderTopWidth;
            to.borderBottomWidth = from.borderBottomWidth;
            to.borderLeftWidth = from.borderLeftWidth;
            to.borderRightWidth = from.borderRightWidth;
            to.borderTopColor = from.borderTopColor;
            to.borderBottomColor = from.borderBottomColor;
            to.borderLeftColor = from.borderLeftColor;
            to.borderRightColor = from.borderRightColor;
            to.borderTopLeftRadius = from.borderTopLeftRadius;
            to.borderTopRightRadius = from.borderTopRightRadius;
            to.borderBottomLeftRadius = from.borderBottomLeftRadius;
            to.borderBottomRightRadius = from.borderBottomRightRadius;
            to.color = from.color;
            to.fontSize = from.fontSize;
            to.unityFont = from.unityFont;
            to.unityFontDefinition = from.unityFontDefinition;
            to.unityFontStyleAndWeight = from.unityFontStyleAndWeight;
            to.unityTextAlign = from.unityTextAlign;
            to.unityTextOutlineColor = from.unityTextOutlineColor;
            to.unityTextOutlineWidth = from.unityTextOutlineWidth;
            to.textShadow = from.textShadow;
            to.letterSpacing = from.letterSpacing;
            to.paddingTop = from.paddingTop;
            to.paddingBottom = from.paddingBottom;
            to.paddingLeft = from.paddingLeft;
            to.paddingRight = from.paddingRight;
        }

        public static List<StyleSheet> StyleSheetsOf(VisualElement element)
        {
            var sheets = new List<StyleSheet>();
            for (var i = 0; i < (element?.styleSheets.count ?? 0); i++)
                sheets.Add(element.styleSheets[i]);
            return sheets;
        }

        /// <summary>
        /// Grows fixed-height ancestors of <paramref name="element"/> (up to, not including, <paramref name="stopAt"/>)
        /// whenever their children no longer fit, e.g. the pause menu box after a button is added.
        /// </summary>
        public static void GrowAncestorsToFit(VisualElement element, VisualElement stopAt)
        {
            for (var ancestor = element.parent; ancestor != null && ancestor != stopAt; ancestor = ancestor.parent)
            {
                var target = ancestor;
                target.RegisterCallback<GeometryChangedEvent>(_ => GrowToFit(target));
            }
        }

        private static void GrowToFit(VisualElement element)
        {
            var top = float.MaxValue;
            var bottom = float.MinValue;
            foreach (var child in element.Children())
            {
                var cs = child.resolvedStyle;
                if (cs.display == DisplayStyle.None || cs.position == Position.Absolute)
                    continue;
                top = Mathf.Min(top, child.layout.yMin - cs.marginTop);
                bottom = Mathf.Max(bottom, child.layout.yMax + cs.marginBottom);
            }
            if (bottom < top)
                return;

            var s = element.resolvedStyle;
            var overflow = Mathf.Max(0f, bottom - (element.layout.height - s.paddingBottom - s.borderBottomWidth))
                + Mathf.Max(0f, s.borderTopWidth + s.paddingTop - top);
            if (overflow < 0.5f)
                return;
            element.style.maxHeight = StyleKeyword.None;
            element.style.height = element.layout.height + overflow;
        }
    }
}
