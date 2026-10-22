---
title: API reference
nav_order: 3
---

# API reference
{: .no_toc }

Version 0.4.0. Everything is in the `BelmontUi` namespace. The public surface uses plain .NET types only (`ConfigEntryBase`, `ConfigFile`, strings, numbers, `Action`), so it does not change when the game's interop assemblies are regenerated. The dll ships with `BelmontUI.xml`, so your IDE shows these descriptions as tooltips.

1. TOC
{:toc}

## What is public

| Type | Public members | For |
|---|---|---|
| `Settings` (static) | `Register`, `RegisterFromConfig`, `Get`, `Unregister`, `Language`, `LanguageChanged` | your mod |
| `SettingsMenu` (sealed, no public constructor) | `Id`, `Title`, `Summary`, `LocalizedTitle`, `LocalizedSummary`, `Header`, `Binding`, `VisibleWhen`, `Toggle`, `Slider`, `IntSlider`, `Choice`, `Key`, `Button`, `ResetButton`, `ResetAll`, `Save` | your mod |
| `LocalizedText` (sealed) | constructors, indexer, `Add`, `Resolve`, `Current`, `Text`, `Locales`, `IsEmpty`, conversions from `string` and `Dictionary<string, string>` | your mod (see Translations) |
| `ControlBinding` (static), `BindingOptions`, `BindingDevices` | the binding format and helpers; what a binding row accepts | your mod |
| `BelmontUiPlugin` | `Guid`, `Version`, `Load` | BepInEx |
| `BelmontUiWatcher` | a Unity component | BelmontUI itself; don't add or call it |

Everything else is internal: the row type and its kinds, the menu's row list, and adding or removing rows other than through the builder methods. A menu can't be created without registering it, and rows can't be removed one by one; register the GUID again instead.

## The model

A mod registers one `SettingsMenu`, identified by its plugin GUID. The tab lists every registered menu by title, with its summary and a **Settings** button. The button opens the menu's rows as a sub-menu, in the order they were added. Rows are added with builder methods, each bound to one BepInEx `ConfigEntry`.

The tab reads the menus when it builds, and rebuilds when a menu changes. Changes made later, even after the tab exists, show up on the next poll (every 30 frames, about half a second at 60 fps).

## `Settings` (static class)

### `Register`

```csharp
public static SettingsMenu Register(string guid, string title, Action<SettingsMenu> build, string summary = null)
```

Registers a mod's menu, runs `build` on it right away, and returns it.

| Parameter | Meaning |
|---|---|
| `guid` | Your plugin's GUID. Registering the same GUID again **replaces** the earlier menu. Empty throws `ArgumentException("A menu needs the mod's GUID")`. |
| `title` | The mod's name on the list and at the top of its sub-menu. Null or empty uses the GUID. |
| `build` | Adds the rows through the builder methods. Null registers an empty menu. |
| `summary` | One line describing the mod, shown on its row of the list. Optional. |

The log gets `[ui] <guid>: settings page '<title>' registered (<n> settings)`, where buttons are not counted. If a row in `build` is invalid, the `ArgumentException` propagates and nothing is registered.

### `RegisterFromConfig`

```csharp
public static SettingsMenu RegisterFromConfig(string guid, string title, ConfigFile config,
    Action<SettingsMenu> extra = null, string summary = null)
```

Builds the rows from the metadata you already gave `Config.Bind`. Every entry already bound in `config` is visited in the file's order:

| Entry type | Needs | Becomes |
|---|---|---|
| `bool` | nothing | Toggle |
| enum | nothing; an `AcceptableValueList` narrows the options | Choice, with labels humanised from the member names |
| `int`, `long`, `short`, `byte` | `AcceptableValueRange` | IntSlider, step = max(1, span / 100) |
| `float`, `double`, `decimal` | `AcceptableValueRange` | Slider, a round step giving 20 to 100 positions, format `{0:0.##}` |
| `string` | `AcceptableValueList` | Choice |
| anything else, or the above without what they need | | left out, logged at debug level: `[ui] <guid>: <Section>.<Key> not on the page (<why>)` |

The label is the key humanised: `HudOpacity` becomes "Hud opacity" and `HudOffsetX` becomes "Hud offset X". The hint is the entry's description. After the generated rows, `extra` runs and can add more. A **Restore defaults** row is then appended unless `extra` added a button row. A null `config` throws `ArgumentNullException`.

Bind every entry **before** calling it: entries bound later are not on the menu.

### `Get` and `Unregister`

```csharp
public static SettingsMenu Get(string guid)     // the menu, or null when nothing is registered under that GUID
public static void Unregister(string guid)      // removes it from the tab; does nothing if it isn't registered
```

## `SettingsMenu`

You get one from `Register` or `RegisterFromConfig`. It has no public constructor.

| Member | Meaning |
|---|---|
| `string Id` | The GUID it was registered under. |
| `string Title { get; set; }` | Name on the list and at the top of the sub-menu. Can change at any time; empty falls back to `Id`. |
| `string Summary { get; set; }` | One line on the list. Null or empty shows nothing. Can change at any time. |
| `Toggle`, `Slider`, `IntSlider`, `Choice`, `Key`, `Button`, `ResetButton` | Builder methods, below. Each validates the row, appends it and returns the menu for chaining. They can also be called later on a menu from `Get`. |
| `void ResetAll()` | Every entry bound to the menu's rows back to its `Config.Bind` default, then `Save()`. Logs `[ui] <guid>: defaults restored`. |
| `void Save()` | Saves the config files of the entries bound to the menu's rows. The tab already saves on its own; use this to save at another moment. |

Rows can't be removed one by one. To change a menu's rows, register the GUID again.

## Builder methods

### Toggle

```csharp
SettingsMenu Toggle(ConfigEntryBase entry, string label, string hint = null)
```

The game's checkbox row. `entry` must be a `bool`; checked means `true`. `hint` is shown next to the label.

```csharp
menu.Toggle(Plugin.ShowHud, "Show HUD");
```

### Slider

```csharp
SettingsMenu Slider(ConfigEntryBase entry, string label, float min, float max, float step,
                    string format = "{0:0.##}", string hint = null)
```

The game's slider row, with the value written next to it. `entry` must be a `float` or `double` (`decimal` is accepted too).

| Parameter | Meaning |
|---|---|
| `min`, `max` | The range. `max` must be greater than `min`. |
| `step` | The increment of one Left / Right press, and the grid values are snapped to from `min`, so the file gets 0.75 and not 0.7500001. 0 lets the slider move freely. |
| `format` | A `string.Format` pattern with the value as `{0}`. `"{0:0%}"` shows 0.75 as "75%", `"{0:0.00}x"` as "0.75x", `"{0:0}"` as "1". |
| `hint` | Shown next to the label. |

```csharp
menu.Slider(Plugin.Opacity, "Opacity", 0.2f, 1f, 0.05f, "{0:0%}");
menu.Slider(Plugin.Zoom, "Zoom", 0.5f, 3f, 0.25f, "{0:0.00}x");
```

### IntSlider

```csharp
SettingsMenu IntSlider(ConfigEntryBase entry, string label, int min, int max, int step = 1, string hint = null)
```

A whole-number slider. `entry` must be an `int`; `long`, `short` and `byte` work too. `step` is at least 1. The value is shown as a plain number.

```csharp
menu.IntSlider(Plugin.MaxIcons, "Icons on screen", 1, 12);
menu.IntSlider(Plugin.Size, "Size", 16, 128, 8);
```

### Choice

```csharp
SettingsMenu Choice(ConfigEntryBase entry, string label, string[] values, string[] labels = null, string hint = null)
```

The game's list row: the current option between Left / Right arrows. `entry` must be a `string` or an enum.

| Parameter | Meaning |
|---|---|
| `values` | What the entry holds for each option, in display order: the exact strings, or the enum member names. An entry holding something else shows the first option until it is changed. |
| `labels` | What the player sees for each option, one per value. Null shows the values. |
| `hint` | Shown next to the label. |

```csharp
menu.Choice(Plugin.Corner, "Position",
    new[] { "TopLeft", "TopRight", "BottomLeft" },
    new[] { "Top left", "Top right", "Bottom left" },
    "Corner of the screen.");
```

### Key

```csharp
SettingsMenu Key(ConfigEntryBase entry, string label, string[] keys, string hint = null)
```

A list row whose options are keys. `entry` must be a `string` holding an Input System `Key` name. `keys` lists the choices: names such as `"F3"`, `"Digit1"`, `"Q"`, plus `"Mouse4"` and `"Mouse5"` for the mouse side buttons. They are shown as "F3", "1", "Q", "Mouse 4". There is no "press a key" capture; offer only keys the game and your mod don't already use. `hint` is shown next to the label.

```csharp
menu.Key(Plugin.ToggleKey, "Toggle key", new[] { "F5", "F6", "F7", "F8", "Mouse4" });
```

### Button

```csharp
SettingsMenu Button(string label, Action onPress, string hint = null, string text = null)
```

A row with a button in the game's style: the label on the left, a copy of the pause menu's System-tab button where other rows have their control. `onPress` runs at once on Unity's main thread when the row is pressed (A, Enter or a click). The rows re-read their entries afterwards, so values it changed show immediately. An exception from `onPress` is caught and logged as `[ui] <guid>: '<label>' failed: ...`. `text` is the caption on the button and defaults to the label. `hint` is shown next to the label.

On the title screen, before the pause menu has been opened once, there is no button to copy yet; the row shows a checkbox there and works the same.

```csharp
menu.Button("Recalibrate", Overlay.Recalibrate, text: "Run");
```

### ResetButton

```csharp
SettingsMenu ResetButton(string label = "Restore defaults",
                         string hint = "Every setting of this mod back to its default.", string text = "Reset")
```

A button row that calls `ResetAll()`, with the Settings screen's reset sound.

### Binding

```csharp
SettingsMenu Binding(ConfigEntryBase entry, string label, BindingOptions options = null, string hint = null)
SettingsMenu Binding(ConfigEntryBase entry, LocalizedText label, BindingOptions options = null, LocalizedText hint = null)
```

A row where the player picks a button, key or combination by pressing it. `entry` must be a `string`; it holds a `ControlBinding` text. The row shows the binding as the game's own button icons for the controller in use (`<sprite name="xbox_buttonSouth">` and the like, from the game's TMP sprite assets; PlayStation, Switch and Xbox sets are chosen the way the game chooses its prompts and switch when the player changes controller), and key names for the keyboard. `hint` is a short line next to the label; null shows the library's "Select, then press a button. Hold one and press another for a combination."

Selecting the row (A, Enter or a click) shows "Press new button" (the game's own text) and listens:

- A single press and release stores that control. Holding one control and pressing another stores both, held first: `rightShoulder+leftShoulder`. Both controls must be on the same kind of device.
- B, Esc or Backspace cancel, as on the game's Controls screen (so those three are never bound), and so does `TimeoutSeconds` without input; the old binding stays.
- A control outside `Allowed` or in `Reserved` shows `ReservedReason` ("Already assigned" by default, the game's own text) on the row's line; a `Validate` callback that returns a reason does the same. The row keeps listening.
- While the row listens, and until everything is let go, every input action the game has on is switched off, so the press doesn't also navigate, switch tabs or close the menu.

`BindingOptions`:

| Property | Default | |
|---|---|---|
| `Devices` | `Gamepad` | `Gamepad`, `Keyboard` (keys and Mouse4 / Mouse5) or `Both`. |
| `AllowCombinations` | `true` | Whether hold + press stores a combination. |
| `Allowed` | null (all) | The only controls the player may pick. |
| `Reserved` | null | Controls the player may not pick. |
| `ReservedReason` | "Already assigned" | Shown when a control is refused. |
| `Validate` | null | `Func<string, LocalizedText>`: null accepts the binding, a text refuses it with that reason. |
| `TimeoutSeconds` | 10 | Listening stops after this long without input. |

```csharp
menu.Binding(Plugin.WheelButton, new LocalizedText("Wheel button") { ["fr"] = "Bouton de la roue" },
    new BindingOptions { Devices = BindingDevices.Gamepad, Reserved = new[] { "start" } });
```

### Header

```csharp
SettingsMenu Header(string title)
SettingsMenu Header(LocalizedText title)
```

A title line that groups the rows after it. It has no control and Up / Down skip it.

### VisibleWhen

```csharp
SettingsMenu VisibleWhen(Func<bool> condition)
```

Applies to the row added just before the call (any kind, headers included): the row is shown only while `condition` returns true. The condition is checked when the sub-menu opens and again whenever an entry of a config file bound on the tab changes, for example when the player flips a toggle of the same menu. Hidden rows take no space and Up / Down skip them; if the selected row is hidden, the selection moves to the nearest row still shown. A condition that throws shows the row and logs the error. Calling it before any row was added throws `InvalidOperationException`.

```csharp
menu.Toggle(Plugin.MergedWheels, "One button for both wheels");
menu.Binding(Plugin.WeaponButton, "Weapon button").VisibleWhen(() => !Plugin.MergedWheels.Value);
```

## `ControlBinding` (static class)

The text format of a binding and helpers to use it in your mod. A binding is one control, or two joined with `+` (held first). Gamepad controls are Input System control names on `Gamepad` (`buttonNorth`, `buttonSouth`, `buttonWest`, `buttonEast`, `leftShoulder`, `rightShoulder`, `leftTrigger`, `rightTrigger`, `leftStickPress`, `rightStickPress`, `select`, `start`, `dpad/up`, `dpad/down`, `dpad/left`, `dpad/right`); keyboard controls are Input System `Key` names plus `Mouse4` / `Mouse5`. Empty means unbound.

| Member | |
|---|---|
| `Separator`, `GamepadControls`, `MouseControls` | `"+"` and the control names. |
| `Parse(binding)`, `Make(held, pressed)` | Split into controls; join. |
| `IsGamepad(control)`, `IsKeyboard(control)`, `IsValid(binding)` | Checks. |
| `IsHeld(binding)` | Every control held now. |
| `WasPressedThisFrame(binding)` | The last control pressed this frame, the first one (if any) held. |
| `Describe(binding)` | Plain text ("RB + LB", "R1 + L1", "F3", "-" when unbound). |
| `Display(binding)` | TMP rich text with the game's icons, for your own labels. |

The input methods read the devices directly (Gamepad.current, Keyboard.current, Mouse.current), whatever the game's input actions are doing. Call them on Unity's main thread.

## Descriptions

A row's `hint` (and a mod's summary on the list) is shown between the label and the control: in a column that starts just after the longest label of the page, the same on every row, up to two lines, centred on the label. The font is 68 % of the label's, down to 52 % when needed to fit two lines; longer texts end with "...". A row whose description takes two lines grows by one line plus a little padding above and below (its dark strip, label, control and selection ornaments stay centred); one-line rows keep the game's height. Keep hints to about one short sentence.

## Validation errors

Every builder method checks its row at once and throws `ArgumentException` naming the row and the problem. Inside `Register`, that exception propagates out of `Register` and the menu is not registered.

| Message | Cause |
|---|---|
| `A row needs a Label` | Empty or null label. |
| `Row 'X' (Button) needs OnPress, the action the button runs` | Button without an action. |
| `Row 'X' (Toggle) needs BindsTo, the ConfigEntry it changes` | A null entry. |
| `Row 'X' (Toggle) needs a bool entry; 'Key' holds Int32` | Wrong entry type for a toggle. |
| `Row 'X' (Slider) needs a float or double entry; 'Key' holds Int32 (use IntSlider for whole numbers)` | Wrong entry type for a slider. |
| `Row 'X' (IntSlider) needs an int entry; 'Key' holds Single` | Wrong entry type for an int slider. |
| `Row 'X' (Slider) needs Max greater than Min (1 .. 1)` | Empty or inverted range. |
| `Row 'X' (Choice) needs Values, at least one` | Empty option list. |
| `Row 'X' (Choice) needs one Label per Value (2 labels, 3 values)` | Labels and values differ in length. |
| `Row 'X' (Choice): Corner has no member Middle` | An enum entry with a value that isn't a member name. |
| `Row 'X' (Choice) needs a string or enum entry; 'Key' holds Single` | Wrong entry type for a list. |
| `Row 'X' (Key) needs Keys, at least one key name` | Empty key list. |
| `Row 'X' (Key) needs a string entry; 'Key' holds Int32` | Wrong entry type for a key. |
| `Row 'X' (Binding) needs a string entry; 'Key' holds Int32` | Wrong entry type for a binding. |

## Translations

The tab caption is **MODS** in every language. BelmontUI's own texts (Settings, Back, the reset row's name, button and hint, the empty-list line, On / Off) come in the game's eleven languages; where the game has the same term (Back, Settings, its Default settings dialog), they are the game's own words.

A mod's texts can be translated with `LocalizedText`:

| Member | |
|---|---|
| `new LocalizedText(text)` | A plain text, shown where no version for the current language exists. A `string` converts to this implicitly. |
| `new LocalizedText(IDictionary<string, string>)`, `new LocalizedText(text, dictionary)` | Versions by locale code. A `Dictionary<string, string>` converts implicitly. |
| `this[locale]`, `Add(locale, text)` | Adds or replaces one language's version (object and collection initializers work). The getter returns that exact version or null. |
| `Resolve(locale)`, `Current`, `ToString()` | The text for a language, or for the game's current one. |
| `Text`, `Locales`, `IsEmpty` | The plain text, the codes with a version, and whether there is anything at all. |

Locale codes: `en`, `fr`, `de`, `es`, `it`, `pt-BR`, `ru`, `ja`, `ko`, `zh-CN`, `zh-TW`; case and `_` / `-` don't matter. Resolution order: the exact code, the code without region (`pt` for `pt-BR`; never between the two Chinese), `en`, the plain text, any version.

Overloads that take `LocalizedText` in place of `string`: `Settings.Register(guid, LocalizedText title, build, LocalizedText summary = null)`, `Settings.RegisterFromConfig(guid, LocalizedText title, config, extra, LocalizedText summary = null)`, and on `SettingsMenu`: `Toggle`, `Slider`, `IntSlider`, `Key`, `Button`, `ResetButton(LocalizedText label, ...)` and `Choice(entry, LocalizedText label, string[] values, LocalizedText[] labels = null, LocalizedText hint = null)`. `SettingsMenu.LocalizedTitle` and `LocalizedSummary` hold the per-language title and summary; `Title` and `Summary` read them in the current language and, when set, give every language the same text. The string overloads are unchanged, so code written for 0.2 compiles and runs as before. `ResetButton()` with its default texts is translated by the library, for mods built against 0.2 too.

`Settings.Language` is the game's current locale code, in lower case as the game reports it (`"pt-br"`, `"zh-cn"`; `"en"` until the game's localization has started) and `Settings.LanguageChanged` is raised on the main thread with the new code. The tab rewrites its texts in the new language by itself; the order of the mod list is set when the tab builds, by the titles in the language of that moment.

The game's fonts are fixed atlases holding only the characters the game's texts use: 171 Latin, 179 Cyrillic, about 1,650 Japanese, 1,040 Korean, 2,150 Simplified and 2,070 Traditional Chinese characters; the non-Latin ones have no lower-case `j`. Characters outside them show as blank boxes. Option values, key names and slider formats are not translated.

## When values are saved

A control writes its `ConfigEntry` the moment it changes, so your mod's `SettingChanged` handlers fire at once. Config files are written when the player leaves a sub-menu, when the tab loses focus and when the menu closes, and whenever `ResetAll()` or `Save()` runs.

## `BelmontUiPlugin`

The plugin class. `BelmontUiPlugin.Guid` is `"leandrobueno.belmontui"` and `BelmontUiPlugin.Version` is `"0.4.0"`. Copy the GUID string into a constant of your own instead, as the examples do. That keeps every `BelmontUi.*` reference out of your plugin class.
