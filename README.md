# Belmont UI

A helper library for **Castlevania: Belmont's Curse** mod developers (BepInEx 6 IL2CPP): declare your mod's settings in code, and players get them in the game's own menus, built from the game's own settings widgets (the same toggles, sliders and list steppers, the same navigation, sounds and look).

In game the library adds a **MODS** tab to the pause menu's top bar, after MAP, EQUIPMENT, TAROT, JOURNEY and SYSTEM (and after Belmont Minimap's MODS when that mod is installed). On the title screen, where there is no pause menu, the tab goes into Settings next to CONTROLS, GAMEPLAY and the rest. The tab opens on a list of the mods that registered settings, one row each with the mod's title, a one-line summary and a **Settings** button. The button opens that mod's settings as a sub-menu with the mod's title at the top; **B**, **Esc**, **Backspace** or the **Back** row at the end return to the list.

The library is mod agnostic. On its own it only adds the tab, which then says that no mod has registered settings.

Documentation: https://leandrobueno.github.io/BelmontUI/ (AI coding agents: [AGENTS.md](AGENTS.md)). MIT licensed, see [LICENSE](LICENSE).

## Install

`BepInEx/plugins/BelmontUI/BelmontUI.dll`. Mods that use it say so in their requirements; they work without it, minus their entry on the tab.

## Using it from a mod

Three rules make the dependency optional:

1. Declare it as a **soft dependency**, so BepInEx loads the library first when it is there.
2. Check for it by GUID in the chainloader, not with a `try/catch` around a call.
3. Keep **every** reference to `BelmontUi.*` inside a class that is only entered when the check passed. .NET compiles a method the first time it is called, and compiling a method that mentions a type from a missing assembly throws `FileNotFoundException`. A method that only *calls* such a method is fine. Don't give that class fields of a library type either.

```csharp
[BepInPlugin(Guid, "My Mod", Version)]
[BepInDependency("leandrobueno.belmontui", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        // ... Config.Bind as usual ...
        if (IL2CPPChainloader.Instance.Plugins.ContainsKey("leandrobueno.belmontui"))
            MenuGlue.Register();   // compiled only when the library is present
    }
}
```

Declare the rows in one callback, in display order. This is what [Ability Wheel](../AbilityWheel/MenuGlue.cs) uses:

```csharp
BelmontUi.Settings.Register(Plugin.Guid, "Ability Wheel", page =>
{
    page.Summary = "Cast up to 4 Tarot spells: hold the wheel button and press Y/X/B/A.";
    page.Choice(Plugin.Modifier, "Wheel button (gamepad)",
        new[] { "rightShoulder", "leftShoulder", "rightTrigger" },
        new[] { "RB", "LB", "RT" },
        "Hold it and press Y/X/B/A to cast that slot's spell.");
    page.Toggle(Plugin.KeepHomeCard, "Pin Y spell", "...");
    page.Slider(Plugin.HudOpacity, "Wheel opacity", 0.2f, 1f, 0.05f, "{0:0%}", "How solid the wheel's tiles are.");
    page.Key(Plugin.AssignKey, "Add to wheel key (keyboard)", Keys.AssignKeyChoices);
    page.ResetButton();
});
```

### Zero-code menus

```csharp
BelmontUi.Settings.RegisterFromConfig(Plugin.Guid, "Smooth Cutscenes", Config, summary: "Smoother cutscene camera.");
```

Rows come from the config file's own metadata: `bool` entries become toggles, numbers with an `AcceptableValueRange` sliders, strings with an `AcceptableValueList` and enums list rows, labels from the keys (`HudOpacity` becomes "Hud opacity") and hints from the descriptions. Entries that don't fit (free text, numbers without a range) are left out and logged at debug level. A "Restore defaults" row is added unless the optional `extra` callback adds a button of its own.

### Translations

The tab caption is **MODS** in every language. BelmontUI's own texts (Settings, Back, the reset row, the empty-list line) come in all eleven of the game's languages and follow the game's language, also when the player changes it while the game runs. Where the game has the same term, the text is the game's own (Back, Settings, the Default settings dialog's title and button).

A mod gives its own texts per language with `LocalizedText`. Every builder method, `Register` and `RegisterFromConfig` has an overload taking it, and `SettingsMenu.LocalizedTitle` / `LocalizedSummary` set the title and summary later. A plain string still works everywhere and shows the same in every language.

```csharp
var enabled = new LocalizedText("Enabled")
{
    ["fr"] = "Activé",
    ["de"] = "Aktiviert",
    ["ja"] = "有効",
    ["zh-CN"] = "启用",
};
BelmontUi.Settings.Register(Plugin.Guid, "My Mod", menu =>
{
    menu.Toggle(Plugin.Enabled, enabled);
    menu.Choice(Plugin.Mode, new LocalizedText("Mode") { ["fr"] = "Mode" },
        new[] { "Always", "WhileHeld" },
        new LocalizedText[]
        {
            new LocalizedText("Always") { ["fr"] = "Toujours" },
            new LocalizedText("While held") { ["fr"] = "En maintenant" },
        });
    menu.ResetButton();                       // translated by the library
}, summary: new LocalizedText("What my mod does.") { ["fr"] = "Ce que fait mon mod." });
```

- Locale codes are the game's: `en`, `fr`, `de`, `es`, `it`, `pt-BR`, `ru`, `ja`, `ko`, `zh-CN`, `zh-TW` (case and `_`/`-` don't matter; `pt` covers `pt-BR`).
- Fallback: the exact language, then the code without region, then `en`, then the plain text given to the constructor. Simplified and Traditional Chinese never stand in for each other.
- A `Dictionary<string, string>` converts to `LocalizedText` too, and so does a collection initializer: `new LocalizedText { { "en", "Go" }, { "fr", "Aller" } }`.
- `Settings.Language` is the current locale code, lower case as the game reports it (`pt-br`, `zh-cn`), and `Settings.LanguageChanged` fires when it changes, for texts a mod builds itself.
- The game's fonts are fixed atlases holding only the characters the game's own texts use (171 Latin characters, 179 Cyrillic, about 1,650 Japanese, 1,040 Korean, 2,150 Simplified and 2,070 Traditional Chinese; the non-Latin fonts have no lower-case `j`). A character outside them shows as a blank box, so check rare characters before shipping.
- Choice values, key names and slider formats are not translated: values are what the config file stores.

## API

Everything public uses plain .NET types (`ConfigEntryBase`, strings, numbers, delegates) and the library's own `LocalizedText`: no interop types, so a consumer's surface doesn't move when the interop assemblies are regenerated. Values apply to the `ConfigEntry` as soon as a control changes; the config files are saved when the player leaves a sub-menu, when the tab loses focus and when the menu closes.

Every public type and member has XML documentation; the build writes `BelmontUI.xml` next to `BelmontUI.dll`, so a mod referencing the dll gets the descriptions in IntelliSense.

`BelmontUi.Settings` (static)

| Member | |
|---|---|
| `Register(guid, title, Action<SettingsMenu> build, summary = null)` | Declares a mod's menu; registering the same GUID again replaces it. |
| `RegisterFromConfig(guid, title, ConfigFile config, Action<SettingsMenu> extra = null, summary = null)` | A menu from `Config.Bind` metadata, see above. |
| `Get(guid)` | The menu, or null. |
| `Unregister(guid)` | Removes the menu. |
| `Language`, `LanguageChanged` | The game's current locale code (`"en"`, `"fr"`, `"zh-CN"`, ...), and an event raised with the new code when the player changes language. |

Every method that takes a title, summary, label, hint, caption or option names has an overload taking `LocalizedText` instead (see Translations).

`BelmontUi.SettingsMenu`

| Member | |
|---|---|
| `Id`, `Title`, `Summary` | The GUID; the name on the list and at the top of the sub-menu; one line under the name on the list. `Title` and `Summary` can be changed at any time and read in the current language. |
| `LocalizedTitle`, `LocalizedSummary` | The same two texts as `LocalizedText`, per language. |
| `Toggle(entry, label, hint)` | A `bool` entry on the game's checkbox row. |
| `Slider(entry, label, min, max, step, format, hint)` | A `float` (or `double`) entry on the game's slider row; `step` is the Left / Right increment (0: free), `format` a `string.Format` pattern for the value shown next to it (`"{0:0%}"`). |
| `IntSlider(entry, label, min, max, step, hint)` | An `int` entry on a whole-number slider. |
| `Choice(entry, label, values, labels, hint)` | A string or enum entry on the game's list row; `values` are what the entry holds (strings, or enum member names), `labels` what the player sees for each (default: the values). |
| `Key(entry, label, keys, hint)` | A string entry holding an Input System `Key` name, as a list of keys. |
| `Button(label, Action onPress, hint, text)` | A row with a button in the game's style. |
| `ResetButton(label, hint, text)` | "Restore defaults" with a "Reset" button; plays the Settings screen's reset sound. |
| `ResetAll()`, `Save()` | Every entry of the menu back to its default and saved; save now. |

A button row's action runs as soon as the row is pressed (A, Enter or a click), with the button's pressed look, and the rows refresh afterwards. On the title screen before the pause menu has been seen there is no button to copy yet, so the row shows a checkbox there and works the same.

## How the tab is built

Everything is found at run time and copied from the game's own menus, so the library ships no assets of its own:

1. A `BelmontUiWatcher` component polls every half second. In a scene with a `GameMenuManager` (in game) the tab goes into its `gameMenuTabView`, the pause menu's `BasicTabView`; otherwise (title screen) into each `SettingsTabView`. The game rebuilds its menus per scene, so this repeats per scene.
2. The page content is a copy of the Settings screen's GAMEPLAY tab (any settings tab, failing that): its scroll and `VerticalNavigationGroup` stay, its `SettingsViewBase` component and rows go, its title text becomes "MODS" and its long note is cleared. On the pause menu that content is moved into a shell copied from the last native pause-menu page (its root keeps only geometry, canvas and `UIContainer` components), so it shows and hides like its neighbours; a plain `BasicTab` or `SettingTab` component takes over. In the pause menu the rows show in a band from the top of the rows' area down to 7.5 % of the page height above the bottom (the frame line); rows outside it are scaled to zero, and the rows scroll to keep the selected row in view (mouse wheel too). When the tab gets focus, canvas groups under the page are brought to alpha 1 and the first focus logs the page's state (rect, parents, canvas groups, container) to `LogOutput.log`. On the Settings screen the view's apply / discard / reset flow sees a tab with no pending changes.
3. The top-bar toggle is a copy of the bar's last visible toggle, relabelled, settled off, put in the same `UIToggleGroup`, and every poll matched to that toggle while both are off: every rect, layout element, caption text setting and image under it, by hierarchy position. The first sync logs the native and our values side by side. The other toggles are narrowed by n/(n+1) so one more fits: through the bar's `HorizontalLayoutGroup` spacing and `LayoutElement` widths when it has one, else by repositioning.
4. Rows are copies of native rows (`UIBoolSettingBinding`, `UIFloatSettingBinding`, `UIDropdownEnumSettingBinding` templates from whichever Settings tab has them) with the binding component stripped and the Doozy control (`UIToggle`, `UISlider`, `UIListStepper`) driven against the `ConfigEntry`. List rows are copied from the plainest native list row (the fewest graphics outside its stepper control), and plain sprite-less graphics outside the control are switched off on the copies. Controller navigation lands on the same component the native tab's `VerticalNavigationGroup` references for that kind of row (found by hierarchy position on the copy), and the navigation is rebuilt on every focus. Localizers are stripped from the copies so the texts stay ours; the unsaved-changes indicator is hidden; the slider's value label is written in the row's `format` on one line; when the page shows, every row except the one the game selects is put in the Normal state at once and its selection animators play Normal (the copies start in whatever look their native row was last left in); the row's `onValueChangedSFX` is played through FMOD on change. The first list row and its native twin are dumped image by image to the log.
5. Rows of every menu are built once per rebuild: a list row per mod (sorted by title, case-insensitive; a copy of the toggle row with the summary as a smaller copy of its label and a copied System-tab "Settings" button), then each mod's rows and a Back row. The list and the sub-menus only switch which rows are active. While a sub-menu is open, the game's enabled input actions named Close, Cancel or Back are switched off, so B goes back to the list instead of closing the menu; they come back once the key that closed the sub-menu is released, and at once when the tab loses focus or the menu closes. Opening and closing play the Settings screen's open and close sounds.
6. The tab is appended to the view's `tabs`, never in front of another mod's. If the view was already initialised, the tab is initialised by hand and its toggle wired to `GoToTab`. Every poll checks that the tab is still listed and in the toggle group, and puts it back if not. With Belmont Minimap installed the pause-menu bar is left alone until its `TabContent_MODS` tab is there, or for 20 seconds after the bar initialised with the menu closed; adding ours first would make that mod skip its tab.

Every lookup is by component type, not by object name, and any failure logs `[ui] MODS tab not added: ...` and leaves the menu as it was. After a game update that changes the menus, the tab goes missing rather than the menu.

## Compatibility

- **Always last.** BelmontUI's MODS tab is kept as the last entry of the bar's `tabs`, the last sibling in the toggle row and the last toggle in the toggle group, on both the pause menu and the title screen's Settings. Every poll, if another mod has added a tab after ours, ours is moved back to the end (array, sibling order, navigation index) without touching anyone else's tab; the log gets `[ui] tab moved to the end of 'TabsLayout_GameMenu' (after 'X')` at debug level.
- **Belmont Minimap.** That mod adds its MODS tab only to a pause-menu bar that still has exactly the five native tabs, so the bar is left alone until its `TabContent_MODS` tab is there, or for 20 seconds after the bar initialised with the menu closed. Whether Minimap is installed is decided on the first poll, not at load (this plugin loads before it). If Minimap rewrites the tab array later, ours is put back after its tab.
- **Game updates.** Every lookup is by component type; any failure logs `[ui] MODS tab not added: ...` and leaves the menu as it was.

## Building

Needs the .NET SDK and the game with BepInEx 6 (IL2CPP, be.788) installed and started once, so the interop assemblies exist. `dotnet build -c Release` builds the library and copies `BelmontUI.dll` and `BelmontUI.xml` into the game's `BepInEx/plugins/BelmontUI`. If the game isn't in the default Steam folder, add `-p:GameDir="<game folder>"`. Consumers reference `BepInEx/plugins/BelmontUI/BelmontUI.dll` from there, so build this project first.

## Changelog

- **0.3.0**: the tab is called **MODS** (untranslated) on the pause menu and the title screen. BelmontUI's own texts are translated into the game's eleven languages, reusing the game's own terms where they exist, and follow a language change while the game runs. New `LocalizedText` type and overloads for per-language titles, summaries, labels, hints, captions and option names; `Settings.Language` and `Settings.LanguageChanged`. 0.2.x mods keep working unchanged, and their default reset row is translated too. Fixed: rows on the pause menu are centred like the game's settings rows (they sat at the left edge); slider values showed their minimum until first moved.

- **0.2.2**: the summary on the mod list reads in full: it is placed from a quarter of the row to the button at build time, then moved to just after the mod's name once the name has been drawn (the name measured before the row was ever shown came out several times too wide).
- **0.2.1**: the first opening of the tab showed every sub-menu row under the mod list (the focus step that re-activates faded objects also re-activated rows hidden by the list); the summary starts right after the mod's name and uses the width up to the button.
- **0.2.0**: the tab opens on a list of the registered mods (title, summary, Settings button); each mod's settings are a sub-menu with a Back action (B, Esc, Backspace, or the Back row). `SettingsPage` is now `SettingsMenu` (same builder methods); `Summary` on menus and on `Register` / `RegisterFromConfig`. XML docs for the whole public API, shipped as `BelmontUI.xml`.
- **0.1.9**: fixes the blank page of 0.1.8: rows outside the visible band are hidden one by one instead of masking their container, which has zero height.
- **0.1.8**: button sized to the row (one and a half times its bar, scaled as a whole) and centred on the bar; the selection can no longer end up on the decorative button (it never takes the Selected look, is handed back to its row if it gets the selection, and the row is reselected after a press); the pause-menu page ends above the frame line, clips, and scrolls with the selection and the mouse wheel.
- **0.1.7**: button rows show a copied System-tab button and run at once; rows redraw after Restore defaults (values were set without Doozy's callbacks, so toggles and lists kept their old look); the pause-menu page has room above its title. API: `Button` and `ResetButton` take an optional `text` for the button caption.
- **0.1.6**: when the page shows, every row is put in the Normal state instantly (and again a frame later), except the row the game selects; copied rows no longer mirror the native row's image states.
- **0.1.5**: list rows copied from the plainest native list row (fewest graphics outside the stepper; the first pick had been the outline-colour row with its white swatch), and plain graphics outside the stepper switched off on any template; colour animators of the tab toggle stay on.
- **0.1.4**: controller navigation uses the native row's selectable and is rebuilt on focus; Minimap detection moved off the load path (it never engaged); toggle settled off with callbacks, state and whole subtree matched to the native toggle, animators switched off if it keeps drifting; unsaved-changes indicators hidden, selectables refreshed, list-row images dumped; slider value label on one line.
- **0.1.3**: the tab is kept last in the tab array, the toggle row and the toggle group, every poll.
- **0.1.2**: waits for Belmont Minimap's MODS tab before adding ours; pause-menu page in a shell copied from a native pause-menu page, with canvas groups forced visible on focus and a state log on first focus; toggle matched to the native one down to its children, with a side-by-side log; interop warning on the watcher fixed.

- **0.1.1**: the tab moved to the pause menu's top bar (Settings screen only on the title screen); toggles fitted and aligned; the copied page's title and note fixed; list rows without the white box; slider values in the row's format; the tab is put back when another mod rewrites the tab array.
- **0.1.0**: the Mods Settings tab in the Settings screen; toggle, slider, int slider, list, key and button rows; sections from `Config.Bind` metadata.
