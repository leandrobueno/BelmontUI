# BelmontUI for AI coding agents

BelmontUI is a BepInEx 6 (IL2CPP) library for Castlevania: Belmont's Curse. It adds a **MODS** tab to the game's pause menu and to the title screen's Settings screen. A mod declares its settings once, in `Load()`, and the player gets them in the game's own widgets. This file lets you add that support to a mod without reading BelmontUI's source. Facts are taken from `Settings.cs`, `SettingsMenu.cs`, `SettingRow.cs` and `Plugin.cs` at version 0.4.0. The dll ships `BelmontUI.xml`, so IDE tooltips carry the same descriptions.

## Facts

| | |
|---|---|
| Plugin GUID | `leandrobueno.belmontui` |
| Namespace | `BelmontUi` (lower-case `i`) |
| Public types | `BelmontUi.Settings` (static) and `BelmontUi.SettingsMenu` (builder methods) are the API. `BelmontUiPlugin` (with `Guid`, `Version`) and the `BelmontUiWatcher` component are public but not for consumers. Row types and row lists are internal. |
| Dll installed at | `<game>\BepInEx\plugins\BelmontUI\BelmontUI.dll` |
| Target | net6.0, BepInEx 6 IL2CPP (be.788) |
| Config saved to | the consumer mod's own `BepInEx\config\<mod guid>.cfg`. BelmontUI writes `ConfigEntry.BoxedValue` at once and calls `ConfigFile.Save()` when the player leaves a sub-menu, the tab loses focus or the menu closes. It has no config file of its own. |

## Three rules for the consumer mod

1. **Soft dependency attribute** on the plugin class, so BepInEx loads BelmontUI first when it is present:
   `[BepInDependency("leandrobueno.belmontui", BepInDependency.DependencyFlags.SoftDependency)]`
2. **Check by GUID in the chainloader**, never with `try/catch` around a call:
   `IL2CPPChainloader.Instance.Plugins.ContainsKey("leandrobueno.belmontui")`
3. **Isolate every `BelmontUi.*` reference in one class** (a nested static class works) that is entered only after the check passed. .NET JIT-compiles a method the first time it runs. Compiling a method that mentions a type from a missing assembly throws `FileNotFoundException` for `BelmontUI.dll`, even if that line is never reached. A method that only *calls* the isolated method is safe. The isolated class must not have fields of a BelmontUI type either.

## csproj

```xml
<!-- Optional at run time (soft dependency). Build or install BelmontUI first. -->
<Reference Include="$(GameDir)\BepInEx\plugins\BelmontUI\BelmontUI.dll" Private="false" />
```

`Private="false"` keeps the dll from being copied next to the mod. `GameDir` is the game folder, for example `C:\Program Files (x86)\Steam\steamapps\common\Castlevania Belmont's Curse`.

## Minimal complete example

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

[BepInPlugin(Guid, "My Mod", "1.0.0")]
[BepInDependency(MenuGlue.LibGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string Guid = "author.mymod";
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<float> Opacity;
    internal static ConfigEntry<string> HotKey;

    public override void Load()
    {
        Enabled = Config.Bind("General", "Enabled", true, "Turns the mod on.");
        Opacity = Config.Bind("Hud", "Opacity", 0.8f,
            new ConfigDescription("How solid the overlay is.", new AcceptableValueRange<float>(0.2f, 1f)));
        HotKey = Config.Bind("Keys", "ToggleKey", "F5", "Keyboard key that toggles the overlay.");
        MenuGlue.Register(Config);
    }
}

// Only the nested Lib class may mention BelmontUi.*
internal static class MenuGlue
{
    internal const string LibGuid = "leandrobueno.belmontui";

    internal static void Register(ConfigFile config)
    {
        if (!IL2CPPChainloader.Instance.Plugins.ContainsKey(LibGuid))
            return;                       // no tab; the mod keeps working from its .cfg
        Lib.Register(config);             // JIT-compiled only when the library is present
    }

    private static class Lib
    {
        internal static void Register(ConfigFile config)
        {
            BelmontUi.Settings.Register(Plugin.Guid, "My Mod", menu =>
            {
                menu.Toggle(Plugin.Enabled, "Enabled", "Turns the mod on.");
                menu.Slider(Plugin.Opacity, "Opacity", 0.2f, 1f, 0.05f, "{0:0%}", "How solid the overlay is.");
                menu.Key(Plugin.HotKey, "Toggle key", new[] { "F5", "F6", "F7", "F8" });
                menu.ResetButton();
            }, summary: "What my mod does, in one line.");
        }
    }
}
```

Zero-code alternative as the body of `Lib.Register`, with rows derived from `Config.Bind` metadata:

```csharp
BelmontUi.Settings.RegisterFromConfig(Plugin.Guid, "My Mod", config, summary: "One line.");
```

`RegisterFromConfig` rules: `bool` becomes Toggle. An enum becomes Choice, narrowed by an `AcceptableValueList` if given. `int`, `long`, `short` or `byte` with an `AcceptableValueRange` becomes IntSlider. `float`, `double` or `decimal` with an `AcceptableValueRange` becomes Slider. `string` with an `AcceptableValueList` becomes Choice. Anything else (free text, numbers without a range) is skipped and logged at debug level. Labels come from the key (`HudOpacity` becomes "Hud opacity"), hints from the description. A "Restore defaults" row is appended unless the optional `extra` callback adds a Button row.

## API summary

`BelmontUi.Settings` (static): `Register(guid, title, Action<SettingsMenu> build, string summary = null)`, `RegisterFromConfig(guid, title, ConfigFile config, Action<SettingsMenu> extra = null, string summary = null)`, `Get(guid)`, `Unregister(guid)`. Registering a GUID again replaces the earlier menu, and the tab rebuilds on its next poll.

`BelmontUi.SettingsMenu` has no public constructor; you get it from `Register`. Members: `Id`, `Title` and `Summary` (both settable), the builder methods below, `ResetAll()`, `Save()`. Rows can't be removed one by one; register the GUID again to change them.

Row kinds and their builder methods:

| Kind | Builder | Entry type | Required | Optional |
|---|---|---|---|---|
| Toggle | `Toggle(entry, label, hint)` | `bool` | entry, label | hint (shown next to the label) |
| Slider | `Slider(entry, label, min, max, step, format, hint)` | `float`, `double` | entry, label, max > min, step (0 = continuous) | format (`string.Format` pattern, default `{0:0.##}`), hint (shown next to the label) |
| IntSlider | `IntSlider(entry, label, min, max, step, hint)` | `int`, `long`, `short`, `byte` | entry, label, max > min | step (default 1), hint (shown next to the label) |
| Choice | `Choice(entry, label, values, labels, hint)` | `string` or enum | entry, label, values (non-empty; enum names must exist) | labels (same length as values), hint (shown next to the label) |
| Key | `Key(entry, label, keys, hint)` | `string` | entry, label, keys (Input System `Key` names such as `"F3"`, `"Digit1"`, `"Q"`, `"Mouse4"`) | hint (shown next to the label) |
| Button | `Button(label, onPress, hint, text)` | none | label, onPress | hint, text (caption, default: label) |
| Reset | `ResetButton(label, hint, text)` | none | nothing | all have defaults |
| Binding | `Binding(entry, label, options, hint)` | `string` holding a `ControlBinding` (`"rightShoulder"`, `"rightShoulder+leftShoulder"`, `"F3"`, `""` = unbound) | entry, label | options (`BindingOptions`: Devices, AllowCombinations, Allowed, Reserved, ReservedReason, Validate, TimeoutSeconds), hint (shown next to the label; default: the library's "press a button" line) |
| Header | `Header(title)` | none | title | nothing |
| (any row) | `.VisibleWhen(Func<bool>)` after the builder call | - | - | shows the row only while true; re-checked when the menu opens and on every setting change |

Read a binding in your mod with `BelmontUi.ControlBinding.WasPressedThisFrame(entry.Value)` or `IsHeld(...)`; `Describe` gives plain text, `Display` TMP text with the game's icons. Keep these calls inside the isolated glue class too.

Every builder method validates at once and throws `ArgumentException`. Inside `Register`, it propagates and nothing is registered. Messages you can hit:

- `A row needs a Label`
- `Row 'X' (Toggle) needs a bool entry; 'Key' holds Single` (same pattern for every kind: Slider wants float or double, IntSlider int, Choice string or enum, Key string)
- `Row 'X' (Slider) needs Max greater than Min (1 .. 0)`
- `Row 'X' (Choice) needs Values, at least one` and `needs one Label per Value`
- `Row 'X' (Choice): MyEnum has no member Foo`
- `Row 'X' (Key) needs Keys, at least one key name`
- `Row 'X' (Button) needs OnPress, the action the button runs`
- `A menu needs the mod's GUID` from `Register` with an empty GUID

AbilityWheel wraps `Lib.Register()` in `try/catch` inside the glue class to log such errors. That is fine, because the library is already known to be present there.

## Do not

- Do not reference `BelmontUi.*` from the plugin class or from any code that runs when the library is absent: no fields, no signatures, no unused branches.
- Do not use `try/catch` around the first call as the presence check. The exception is thrown while compiling the caller, before the `try` runs.
- Do not pass interop (`Il2Cpp*`, Unity) types to the API. It takes `ConfigEntryBase`, strings, numbers and `Action` only.
- Do not use a hard `[BepInDependency]` without `SoftDependency`. The mod would refuse to load without the library.
- Do not copy `BelmontUI.dll` into your mod's plugin folder.
- Texts: a plain `string` shows the same in every language. For per-language texts pass a `BelmontUi.LocalizedText` (`new LocalizedText("Enabled") { ["fr"] = "Activé", ["ja"] = "有効" }`); every builder method, `Register` and `RegisterFromConfig` has an overload for it, and `SettingsMenu.LocalizedTitle` / `LocalizedSummary` exist. Locale codes: `en fr de es it pt-BR ru ja ko zh-CN zh-TW`; fallback is exact, code without region, `en`, plain text. `Settings.Language` / `Settings.LanguageChanged` give the game's language. The game's fonts are small fixed atlases: check rare characters (non-Latin fonts lack lower-case `j`). Keep `LocalizedText` inside the isolated class like every other `BelmontUi.*` type.

## Verify in game

1. Put BelmontUI and the mod in `BepInEx\plugins` and start the game.
2. Pause menu top bar: a **MODS** tab after SYSTEM, and after Belmont Minimap's MODS when that mod is installed. The title screen's Settings has the same tab. It opens on a list of registered mods sorted by title, one row each with the title, the `summary` and a **Settings** button. The button opens the mod's rows as a sub-menu titled with the mod's title. B, Esc, Backspace or the **Back** row return to the list; while the sub-menu is open, B does not close the pause menu.
3. `BepInEx\LogOutput.log` should contain:
   - `Belmont UI loaded`
   - `[ui] <your guid>: settings page '<title>' registered (<n> settings)`
   - `[ui] MODS tab added to '...'` once a menu is up
   - `[ui] MODS tab: <n> mod(s) [<titles>], <m> rows in all` when the rows are built
   - `[ui] '<title>' opened (...)` and `[ui] '<title>' closed (...)` around the sub-menu
   - with Minimap: `[ui] Belmont Minimap is installed: waiting for its MODS tab ...` then `[ui] Belmont Minimap's MODS tab is there; adding BelmontUI's MODS tab after it`
4. Failure lines: `[ui] MODS tab not added to '...': <why>` means a game update changed the menus; the game's own menu is left untouched. `[ui] <guid>: row '<label>' not added: ...` names a single row.
5. Remove `BepInEx\plugins\BelmontUI` and start again. The mod must still load, with no `FileNotFoundException` in the log.

## Repository notes

- `*.cs`, `BelmontUI.csproj` and `README.md` are the library. `docs/` is the GitHub Pages site. `AGENTS.md` and `docs/llms.txt` are for agents.
- Build with `dotnet build -c Release`. The game must be installed and started once under BepInEx so the interop assemblies exist. Pass `-p:GameDir="<game folder>"` outside the default Steam path. The output is copied to the game's plugins folder.
- Everything in the tab is found by component type at run time and cloned from the game's widgets; the library ships no assets.
