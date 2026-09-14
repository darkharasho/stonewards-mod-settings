using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>A control bound to one setting. <see cref="Refresh"/> re-reads the setting's current value.</summary>
    internal sealed class SettingEditor
    {
        public VisualElement Control;
        public Action Refresh;
    }

    /// <summary>Picks and binds a UI Toolkit control for a setting based on its type and acceptable values.</summary>
    internal static class SettingEditors
    {
        private static readonly Type[] IntegerTypes =
            { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ushort), typeof(ulong) };
        private static readonly Type[] RealTypes = { typeof(float), typeof(double), typeof(decimal) };

        /// <param name="changed">Called after the setting was written, so the panel can refresh dependent rows.</param>
        public static SettingEditor Create(SettingInfo setting, Action changed)
        {
            var entry = setting.Entry;
            var type = entry.SettingType;
            var acceptable = entry.Description?.AcceptableValues;

            Action<object> set = value =>
            {
                ConfigWriter.Set(entry, value);
                changed();
            };

            if (type == typeof(bool))
                return CreateToggle(entry, set);
            if (type == typeof(KeyboardShortcut))
                return KeyRebindEditor.Create(entry, set);
            if (acceptable != null && acceptable.GetType().IsGenericType
                && acceptable.GetType().GetGenericTypeDefinition() == typeof(AcceptableValueList<>))
                return CreateDropdown(entry, ((Array)acceptable.GetType().GetProperty("AcceptableValues").GetValue(acceptable)).Cast<object>(), set);
            if (type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), false))
                return CreateDropdown(entry, Enum.GetValues(type).Cast<object>(), set);
            if (acceptable != null && acceptable.GetType().IsGenericType
                && acceptable.GetType().GetGenericTypeDefinition() == typeof(AcceptableValueRange<>))
            {
                var min = acceptable.GetType().GetProperty("MinValue").GetValue(acceptable);
                var max = acceptable.GetType().GetProperty("MaxValue").GetValue(acceptable);
                if (IntegerTypes.Contains(type))
                    return CreateSliderInt(entry, min, max, set);
                if (RealTypes.Contains(type))
                    return CreateSlider(entry, min, max, set);
            }
            return CreateTextField(entry, set);
        }

        private static SettingEditor CreateToggle(ConfigEntryBase entry, Action<object> set)
        {
            var toggle = new Toggle();
            toggle.RegisterValueChangedCallback(e => set(e.newValue));
            return new SettingEditor { Control = toggle, Refresh = () => toggle.SetValueWithoutNotify((bool)entry.BoxedValue) };
        }

        private static SettingEditor CreateDropdown(ConfigEntryBase entry, IEnumerable<object> values, Action<object> set)
        {
            var options = values.ToList();
            var labels = options.Select(v => Label(v, entry.SettingType)).ToList();
            var dropdown = new DropdownField(labels, 0);
            Theme.StyleDropdownMenusWhenOpened(dropdown);
            dropdown.RegisterValueChangedCallback(e =>
            {
                var index = labels.IndexOf(e.newValue);
                if (index >= 0)
                    set(options[index]);
            });
            return new SettingEditor
            {
                Control = dropdown,
                Refresh = () =>
                {
                    var label = Label(entry.BoxedValue, entry.SettingType);
                    // A value outside the list (e.g. an edited file) is shown as-is rather than hidden.
                    if (!labels.Contains(label))
                        dropdown.choices = labels.Concat(new[] { label }).ToList();
                    dropdown.SetValueWithoutNotify(label);
                },
            };
        }

        private static string Label(object value, Type type)
        {
            if (type.IsEnum)
                return SettingsRegistry.Humanize(value.ToString());
            return TomlTypeConverter.ConvertToString(value, type);
        }

        private static SettingEditor CreateSliderInt(ConfigEntryBase entry, object min, object max, Action<object> set)
        {
            var type = entry.SettingType;
            var slider = new SliderInt(ToInt(min), ToInt(max)) { showInputField = true };
            slider.RegisterValueChangedCallback(e => set(Convert.ChangeType(e.newValue, type)));
            return new SettingEditor { Control = slider, Refresh = () => slider.SetValueWithoutNotify(ToInt(entry.BoxedValue)) };
        }

        private static SettingEditor CreateSlider(ConfigEntryBase entry, object min, object max, Action<object> set)
        {
            var type = entry.SettingType;
            var low = Convert.ToSingle(min);
            var high = Convert.ToSingle(max);
            var slider = new Slider(low, high) { showInputField = true };
            // Round to a step that suits the range so the file doesn't fill with float noise.
            var step = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(high - low, 1e-6f))) - 2f);
            slider.RegisterValueChangedCallback(e =>
            {
                var rounded = Mathf.Clamp(Mathf.Round(e.newValue / step) * step, low, high);
                slider.SetValueWithoutNotify(rounded);
                set(Convert.ChangeType(rounded, type));
            });
            return new SettingEditor { Control = slider, Refresh = () => slider.SetValueWithoutNotify(Convert.ToSingle(entry.BoxedValue)) };
        }

        private static int ToInt(object value)
        {
            var number = Convert.ToDouble(value);
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, number));
        }

        /// <summary>Strings, unbounded numbers and anything else BepInEx can serialize, edited as text.</summary>
        private static SettingEditor CreateTextField(ConfigEntryBase entry, Action<object> set)
        {
            var field = new TextField { isDelayed = true };
            Action refresh = () => field.SetValueWithoutNotify(entry.GetSerializedValue());
            field.RegisterValueChangedCallback(e =>
            {
                object value;
                try
                {
                    value = entry.SettingType == typeof(string)
                        ? e.newValue
                        : TomlTypeConverter.ConvertToValue(e.newValue, entry.SettingType);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"'{e.newValue}' is not a valid value for {entry.Definition}: {ex.Message}");
                    refresh();
                    return;
                }
                set(value);
            });
            return new SettingEditor { Control = field, Refresh = refresh };
        }
    }

    /// <summary>
    /// Writes settings through <see cref="ConfigEntryBase.BoxedValue"/>, so the owning mod gets its normal
    /// SettingChanged event right away. Saving the file is batched: dragging a slider would otherwise rewrite
    /// the .cfg on every step.
    /// </summary>
    internal static class ConfigWriter
    {
        private static readonly HashSet<ConfigFile> Dirty = new HashSet<ConfigFile>();

        public static void Set(ConfigEntryBase entry, object value)
        {
            var file = entry.ConfigFile;
            var saveOnSet = file.SaveOnConfigSet;
            try
            {
                file.SaveOnConfigSet = false;
                entry.BoxedValue = value;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Could not set {entry.Definition} to '{value}': {ex}");
            }
            finally
            {
                file.SaveOnConfigSet = saveOnSet;
            }
            // Mods that turned off SaveOnConfigSet save on their own terms.
            if (saveOnSet)
                Dirty.Add(file);
        }

        public static void Flush()
        {
            foreach (var file in Dirty)
            {
                try
                {
                    file.Save();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Could not save {file.ConfigFilePath}: {ex}");
                }
            }
            Dirty.Clear();
        }
    }
}
