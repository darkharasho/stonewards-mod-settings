using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Full-screen overlay listing every mod's settings: mods on the left, the selected mod's settings on the
    /// right grouped by section. Changes apply immediately; files are saved every second and on close.
    /// </summary>
    internal sealed class ModSettingsPanel
    {
        private readonly VisualElement _overlay;
        private readonly ScrollView _modList;
        private readonly ScrollView _content;
        private readonly TextField _search;
        private readonly Toggle _showAdvanced;
        private readonly Label _restartNotice;
        private readonly Button _closeButton;
        private readonly List<Action> _refreshers = new List<Action>();

        private List<ModInfo> _mods = new List<ModInfo>();
        private ModInfo _selected;

        public event Action CloseRequested;

        public VisualElement Root => _overlay;
        public bool IsOpen { get; private set; }

        public ModSettingsPanel(IEnumerable<StyleSheet> styleSheets)
        {
            _overlay = new VisualElement { name = "ModSettingsOverlay" };
            foreach (var sheet in styleSheets)
                _overlay.styleSheets.Add(sheet);
            var o = _overlay.style;
            o.position = Position.Absolute;
            o.left = o.right = o.top = o.bottom = 0f;
            o.alignItems = Align.Center;
            o.justifyContent = Justify.Center;
            o.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            o.color = Theme.Text;
            o.display = DisplayStyle.None;

            var window = new VisualElement { name = "ModSettingsWindow" };
            var w = window.style;
            w.width = Length.Percent(80f);
            w.height = Length.Percent(82f);
            w.maxWidth = 1400f;
            w.backgroundColor = Theme.Window;
            Theme.SetBorder(window, 3f, Theme.WindowBorder, 0f);
            Theme.SetPadding(window, 20f, 24f);
            _overlay.Add(window);

            var header = Row();
            header.style.marginBottom = 16f;
            var title = new Label("MOD SETTINGS");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Theme.Text;
            title.style.textShadow = new TextShadow { offset = new Vector2(2f, 2f), color = Color.black };
            title.style.flexGrow = 1f;
            header.Add(title);

            _search = new TextField { name = "ModSettingsSearch" };
            _search.style.width = 300f;
            _search.RegisterValueChangedCallback(_ => Rebuild(keepSelection: true));
            header.Add(Captioned("Search", _search));

            _showAdvanced = new Toggle { name = "ModSettingsShowAdvanced" };
            _showAdvanced.RegisterValueChangedCallback(_ => Rebuild(keepSelection: true));
            header.Add(Captioned("Advanced", _showAdvanced));

            _closeButton = new Button(() => CloseRequested?.Invoke()) { text = "Back" };
            _closeButton.style.marginLeft = 20f;
            _closeButton.style.minWidth = 110f;
            Theme.StyleButton(_closeButton);
            header.Add(_closeButton);
            Theme.StyleControls(header);
            window.Add(header);

            _restartNotice = new Label("Some changes take effect after restarting the game.");
            _restartNotice.style.color = Theme.Gold;
            _restartNotice.style.marginBottom = 10f;
            _restartNotice.style.display = DisplayStyle.None;
            window.Add(_restartNotice);

            var body = Row();
            body.style.flexGrow = 1f;
            body.style.flexShrink = 1f;
            body.style.alignItems = Align.Stretch;
            window.Add(body);

            _modList = new ScrollView(ScrollViewMode.Vertical) { name = "ModSettingsModList" };
            _modList.style.width = 260f;
            _modList.style.flexShrink = 0f;
            _modList.style.marginRight = 20f;
            _modList.style.paddingRight = 16f;
            _modList.style.borderRightWidth = 2f;
            _modList.style.borderRightColor = Theme.Line;
            Theme.StyleControls(_modList);
            body.Add(_modList);

            _content = new ScrollView(ScrollViewMode.Vertical) { name = "ModSettingsContent" };
            _content.style.flexGrow = 1f;
            Theme.StyleControls(_content);
            body.Add(_content);

            // Saves pending changes while open; the UI scheduler runs on real time, so this works while paused.
            _overlay.schedule.Execute(ConfigWriter.Flush).Every(1000);
        }

        public void Open()
        {
            _mods = SettingsRegistry.Collect();
            _search.SetValueWithoutNotify("");
            _restartNotice.style.display = DisplayStyle.None;
            _selected = null;
            Rebuild(keepSelection: false);
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
            IsOpen = true;
            var focus = _closeButton;
            focus.schedule.Execute(() => focus.Focus()).ExecuteLater(0L);
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            KeyRebindEditor.CancelListening();
            IsOpen = false;
            _overlay.style.display = DisplayStyle.None;
            ConfigWriter.Flush();
        }

        private bool IsVisible(SettingInfo setting)
        {
            if (setting.Advanced && !_showAdvanced.value)
                return false;
            var search = _search.value?.Trim();
            return string.IsNullOrEmpty(search) || setting.Matches(search);
        }

        private void Rebuild(bool keepSelection)
        {
            var visibleMods = _mods.Where(m => m.Settings.Any(IsVisible)).ToList();
            if (!keepSelection || !visibleMods.Contains(_selected))
                _selected = visibleMods.FirstOrDefault();

            _modList.Clear();
            foreach (var mod in visibleMods)
            {
                var target = mod;
                var button = new Button(() =>
                {
                    _selected = target;
                    Rebuild(keepSelection: true);
                }) { text = mod.Name };
                Theme.StyleButton(button, outlined: mod == _selected);
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = button.style.marginRight = 0f;
                button.style.marginBottom = 8f;
                button.style.paddingTop = button.style.paddingBottom = 10f;
                _modList.Add(button);
            }

            _content.Clear();
            _refreshers.Clear();
            if (_selected == null)
            {
                _content.Add(Muted(_mods.Count == 0 ? "No mods with settings are loaded." : "No settings match the search."));
                return;
            }

            var heading = new Label(_selected.Name);
            heading.style.fontSize = 26f;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = Theme.Text;
            _content.Add(heading);
            var meta = Muted($"v{_selected.Version}  ·  {_selected.Guid}");
            meta.style.marginBottom = 6f;
            _content.Add(meta);

            string section = null;
            foreach (var setting in _selected.Settings.Where(IsVisible))
            {
                if (setting.Section != section)
                {
                    section = setting.Section;
                    var sectionLabel = new Label(section.ToUpperInvariant());
                    sectionLabel.style.fontSize = 18f;
                    sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    sectionLabel.style.color = Theme.Gold;
                    sectionLabel.style.marginTop = 18f;
                    sectionLabel.style.marginBottom = 6f;
                    sectionLabel.style.paddingBottom = 4f;
                    sectionLabel.style.borderBottomWidth = 2f;
                    sectionLabel.style.borderBottomColor = Theme.Line;
                    _content.Add(sectionLabel);
                }
                _content.Add(CreateRow(setting));
            }
            Theme.StyleControls(_content.contentContainer);
            RefreshAll();
        }

        private VisualElement CreateRow(SettingInfo setting)
        {
            var row = Row();
            row.style.backgroundColor = Theme.Surface;
            row.style.marginBottom = 6f;
            Theme.SetPadding(row, 10f, 14f);
            Theme.SetBorder(row, 0f, Color.clear, 4f);

            var text = new VisualElement();
            text.style.width = Length.Percent(42f);
            text.style.flexShrink = 0f;
            text.style.paddingRight = 16f;
            var name = new Label(setting.Name);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.fontSize = 16f;
            name.style.color = Theme.Text;
            name.style.whiteSpace = WhiteSpace.Normal;
            text.Add(name);
            var tags = new List<string>();
            if (setting.RestartRequired)
                tags.Add("Restart required");
            if (setting.Advanced)
                tags.Add("Advanced");
            if (setting.ReadOnly)
                tags.Add("Read only");
            if (tags.Count > 0)
            {
                var tagLabel = new Label(string.Join("  ·  ", tags));
                tagLabel.style.color = Theme.Gold;
                tagLabel.style.fontSize = 12f;
                text.Add(tagLabel);
            }
            if (!string.IsNullOrEmpty(setting.Description))
            {
                var description = Muted(setting.Description);
                description.style.fontSize = 13f;
                description.style.marginTop = 4f;
                text.Add(description);
            }
            row.Add(text);

            var editor = SettingEditors.Create(setting, () => OnChanged(setting));
            // Toggles keep their natural size; other controls fill the space before Reset.
            var holder = Row();
            holder.style.flexGrow = 1f;
            if (!(editor.Control is Toggle))
                editor.Control.style.flexGrow = 1f;
            editor.Control.SetEnabled(!setting.ReadOnly);
            holder.Add(editor.Control);
            row.Add(holder);

            var entry = setting.Entry;
            var reset = new Button(() =>
            {
                ConfigWriter.Set(entry, entry.DefaultValue);
                OnChanged(setting);
            }) { text = "Reset", tooltip = "Reset to default" };
            Theme.StyleButton(reset, outlined: false);
            reset.style.marginLeft = 12f;
            reset.style.width = 84f;
            row.Add(reset);

            _refreshers.Add(() =>
            {
                editor.Refresh();
                var isDefault = entry.GetSerializedValue() == BepInEx.Configuration.TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);
                // Hidden rather than removed so the controls stay aligned.
                reset.style.visibility = isDefault || setting.HideDefaultButton || setting.ReadOnly ? Visibility.Hidden : Visibility.Visible;
            });
            return row;
        }

        private void OnChanged(SettingInfo setting)
        {
            if (setting.RestartRequired)
                _restartNotice.style.display = DisplayStyle.Flex;
            // A mod may adjust other settings when one changes, so re-read them all.
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var refresh in _refreshers)
                refresh();
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static VisualElement Captioned(string caption, VisualElement control)
        {
            var row = Row();
            row.style.marginLeft = 20f;
            var label = Muted(caption);
            label.style.marginRight = 8f;
            row.Add(label);
            row.Add(control);
            return row;
        }

        private static Label Muted(string text)
        {
            var label = new Label(text);
            label.style.color = Theme.Muted;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
