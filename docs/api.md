---
title: API reference
nav_order: 3
---

# API reference
{: .no_toc }

Version 0.2.2. Everything is in the `BelmontUi` namespace. The public surface uses plain .NET types only (`ConfigEntryBase`, `ConfigFile`, strings, numbers, `Action`), so it does not change when the game's interop assemblies are regenerated. The dll ships with `BelmontUI.xml`, so your IDE shows these descriptions as tooltips.

1. TOC
{:toc}

## What is public

| Type | Public members | For |
|---|---|---|
| `Settings` (static) | `Register`, `RegisterFromConfig`, `Get`, `Unregister` | your mod |
| `SettingsMenu` (sealed, no public constructor) | `Id`, `Title`, `Summary`, `Toggle`, `Slider`, `IntSlider`, `Choice`, `Key`, `Button`, `ResetButton`, `ResetAll`, `Save` | your mod |
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

The game's checkbox row. `entry` must be a `bool`; checked means `true`. The checkbox row has no place to show `hint`; it is kept for future use.

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
| `hint` | Not shown by the slider row; kept for future use. |

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
| `hint` | Shown on the list row's description line. |

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

A list row whose options are keys. `entry` must be a `string` holding an Input System `Key` name. `keys` lists the choices: names such as `"F3"`, `"Digit1"`, `"Q"`, plus `"Mouse4"` and `"Mouse5"` for the mouse side buttons. They are shown as "F3", "1", "Q", "Mouse 4". There is no "press a key" capture; offer only keys the game and your mod don't already use. `hint` is shown on the description line.

```csharp
menu.Key(Plugin.ToggleKey, "Toggle key", new[] { "F5", "F6", "F7", "F8", "Mouse4" });
```

### Button

```csharp
SettingsMenu Button(string label, Action onPress, string hint = null, string text = null)
```

A row with a button in the game's style: the label on the left, a copy of the pause menu's System-tab button where other rows have their control. `onPress` runs at once on Unity's main thread when the row is pressed (A, Enter or a click). The rows re-read their entries afterwards, so values it changed show immediately. An exception from `onPress` is caught and logged as `[ui] <guid>: '<label>' failed: ...`. `text` is the caption on the button and defaults to the label. `hint` is kept for future use.

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

## Texts

Labels, hints, summaries and captions are shown as given. BelmontUI translates nothing; pass your own translations if you want them.

## When values are saved

A control writes its `ConfigEntry` the moment it changes, so your mod's `SettingChanged` handlers fire at once. Config files are written when the player leaves a sub-menu, when the tab loses focus and when the menu closes, and whenever `ResetAll()` or `Save()` runs.

## `BelmontUiPlugin`

The plugin class. `BelmontUiPlugin.Guid` is `"leandrobueno.belmontui"` and `BelmontUiPlugin.Version` is `"0.2.2"`. Copy the GUID string into a constant of your own instead, as the examples do. That keeps every `BelmontUi.*` reference out of your plugin class.
