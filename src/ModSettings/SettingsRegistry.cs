using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ModSettings
{
    internal sealed class ModInfo
    {
        public string Guid;
        public string Name;
        public string Version;
        public List<SettingInfo> Settings = new List<SettingInfo>();
    }

    internal sealed class SettingInfo
    {
        public ModInfo Mod;
        public ConfigEntryBase Entry;
        public string Section;
        public string Name;
        public string Description;
        public int Order;
        public bool ReadOnly;
        public bool Advanced;
        public bool RestartRequired;
        public bool HideDefaultButton;

        public bool Matches(string search)
        {
            return Contains(Name, search) || Contains(Section, search) || Contains(Entry.Definition.Key, search)
                || Contains(Description, search) || Contains(Mod.Name, search);
        }

        private static bool Contains(string text, string search) =>
            text != null && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Finds the settings of every loaded BepInEx plugin: its <see cref="BaseUnityPlugin.Config"/> plus any
    /// other <see cref="ConfigFile"/> fields it holds. Mods can refine how a setting is shown by adding a tag
    /// object to its <see cref="ConfigDescription"/>, using the field names of BepInEx.ConfigurationManager's
    /// ConfigurationManagerAttributes (Browsable, Category, DispName, Order, ReadOnly, IsAdvanced, Description,
    /// HideDefaultButton) plus RestartRequired. Tags are read by name, so mods need no reference to this one.
    /// </summary>
    internal static class SettingsRegistry
    {
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static List<ModInfo> Collect()
        {
            var mods = new List<ModInfo>();
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (plugin.Instance == null)
                    continue;

                var mod = new ModInfo
                {
                    Guid = plugin.Metadata.GUID,
                    Name = plugin.Metadata.Name,
                    Version = plugin.Metadata.Version.ToString(),
                };
                foreach (var file in ConfigFiles(plugin.Instance))
                {
                    foreach (var entry in ((IDictionary<ConfigDefinition, ConfigEntryBase>)file).Values)
                    {
                        var setting = Describe(mod, entry);
                        if (setting != null)
                            mod.Settings.Add(setting);
                    }
                }
                if (mod.Settings.Count == 0)
                    continue;

                // Same ordering as ConfigurationManager: sections by name, then higher Order first, then by name.
                mod.Settings = mod.Settings
                    .OrderBy(s => s.Section, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(s => s.Order)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                mods.Add(mod);
            }
            return mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<ConfigFile> ConfigFiles(BaseUnityPlugin plugin)
        {
            var seen = new HashSet<ConfigFile> { plugin.Config };
            yield return plugin.Config;

            for (var type = plugin.GetType(); type != null && type != typeof(BaseUnityPlugin); type = type.BaseType)
            {
                foreach (var field in type.GetFields(AnyField | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(ConfigFile))
                        continue;
                    var target = field.IsStatic ? null : plugin;
                    if (field.GetValue(target) is ConfigFile file && seen.Add(file))
                        yield return file;
                }
            }
        }

        private static SettingInfo Describe(ModInfo mod, ConfigEntryBase entry)
        {
            var setting = new SettingInfo
            {
                Mod = mod,
                Entry = entry,
                Section = entry.Definition.Section,
                Name = Humanize(entry.Definition.Key),
                Description = entry.Description?.Description,
            };

            var tags = entry.Description?.Tags;
            if (tags == null)
                return setting;

            foreach (var tag in tags)
            {
                if (tag == null)
                    continue;
                if (Read<bool?>(tag, "Browsable") == false)
                    return null;
                setting.Section = Read<string>(tag, "Category") ?? setting.Section;
                setting.Name = Read<string>(tag, "DispName") ?? setting.Name;
                setting.Description = Read<string>(tag, "Description") ?? setting.Description;
                setting.Order = Read<int?>(tag, "Order") ?? setting.Order;
                setting.ReadOnly |= Read<bool?>(tag, "ReadOnly") == true;
                setting.Advanced |= Read<bool?>(tag, "IsAdvanced") == true;
                setting.RestartRequired |= Read<bool?>(tag, "RestartRequired") == true;
                setting.HideDefaultButton |= Read<bool?>(tag, "HideDefaultButton") == true;
            }
            return setting;
        }

        private static T Read<T>(object tag, string name)
        {
            var type = tag.GetType();
            object value;
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                value = field.GetValue(tag);
            }
            else
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead)
                    return default;
                value = property.GetValue(tag);
            }
            return value is T typed ? typed : default;
        }

        /// <summary>"CounterOffsetX" becomes "Counter Offset X"; keys that already have spaces are left alone.</summary>
        internal static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Contains(" "))
                return key;

            var chars = new List<char>(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (c == '_')
                {
                    chars.Add(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && key[i - 1] != '_'
                    && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }
    }
}
