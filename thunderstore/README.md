# ModSettings

One settings menu for all your Stonewards mods. Change any mod's options in game instead of editing config files.

## How to use

- Click **Mod Settings** on the title screen, or press **Escape** in game and click **Mods**, under **Settings**.
- Pick a mod on the left and change its settings on the right. Search finds settings across all mods.
- Changes apply right away and are saved to the mod's normal config file. Settings marked **Restart required** take effect the next time you start the game.
- **Reset** puts a setting back to its default. **Back** or **Escape** returns to the menu you came from.

Every installed BepInEx mod shows up automatically, no support from the mod needed. Toggles, dropdowns, sliders (for settings with a range), key bindings and text fields are picked from each setting's type.

## For mod authors

Nothing is required: settings bound with `Config.Bind` appear as they are. To refine how a setting is shown, add a tag object to its `ConfigDescription`. ModSettings reads these public fields by name, so you don't need to reference it, and tags written for BepInEx.ConfigurationManager work as they are:

| Field | Type | Effect |
| --- | --- | --- |
| `DispName` | `string` | Name shown instead of the key. |
| `Category` | `string` | Section shown instead of the config section. |
| `Description` | `string` | Description shown instead of the config description. |
| `Order` | `int?` | Higher values are listed first within a section. |
| `Browsable` | `bool?` | `false` hides the setting. |
| `IsAdvanced` | `bool?` | Only shown with **Advanced** ticked. |
| `ReadOnly` | `bool?` | Shown but can't be changed. |
| `HideDefaultButton` | `bool?` | Hides the **Reset** button. |
| `RestartRequired` | `bool?` | Marks the setting as needing a game restart. |

```csharp
internal sealed class ConfigurationManagerAttributes
{
    public string DispName;
    public int? Order;
    public bool? RestartRequired;
}

Config.Bind("HUD", "CounterOffsetX", 0, new ConfigDescription(
    "Extra horizontal distance from the corner.",
    new AcceptableValueRange<int>(-400, 400),
    new ConfigurationManagerAttributes { DispName = "Counter offset X", Order = 10 }));
```

Use `AcceptableValueRange` for a slider and `AcceptableValueList` for a dropdown. Your mod receives changes through its normal `SettingChanged` events and `.Value`.

## Issues

Report bugs at https://github.com/darkharasho/stonewards-mod-settings/issues. Please attach `BepInEx/LogOutput.log`.
