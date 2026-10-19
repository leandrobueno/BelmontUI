---
title: Recipes
nav_order: 4
---

# Recipes
{: .no_toc }

Every snippet goes inside the isolated glue class described in [Getting started](getting-started#the-soft-dependency-pattern), for example as the body of `MenuGlue.Lib.Register()`. The entries are your own `ConfigEntry<T>` fields from `Config.Bind`.

1. TOC
{:toc}

## A mod with three settings

```csharp
// In Plugin.Load():
Enabled  = Config.Bind("General", "Enabled", true, "Turns the mod on.");
Opacity  = Config.Bind("Hud", "Opacity", 0.8f, "How solid the overlay is.");
Position = Config.Bind("Hud", "Position", "TopRight", "Corner of the screen.");

// In MenuGlue.Lib.Register():
BelmontUi.Settings.Register(Plugin.Guid, "My Overlay", menu =>
{
    menu.Toggle(Plugin.Enabled, "Enabled", "Turns the mod on.");
    menu.Slider(Plugin.Opacity, "Opacity", 0.2f, 1f, 0.05f, "{0:0%}", "How solid the overlay is.");
    menu.Choice(Plugin.Position, "Position",
        new[] { "TopLeft", "TopRight" },
        new[] { "Top left", "Top right" },
        "Corner of the screen.");
}, summary: "A small overlay with your stats.");
```

## Deriving everything from `Config.Bind`

Give your entries ranges and value lists, and BelmontUI builds the menu with no row code:

```csharp
// In Plugin.Load():
Enabled = Config.Bind("General", "Enabled", true, "Turns the mod on.");
Opacity = Config.Bind("Hud", "HudOpacity", 0.8f,
    new ConfigDescription("How solid the overlay is.", new AcceptableValueRange<float>(0.2f, 1f)));
Icons = Config.Bind("Hud", "MaxIcons", 6,
    new ConfigDescription("Icons on screen.", new AcceptableValueRange<int>(1, 12)));
Corner = Config.Bind("Hud", "Corner", ScreenCorner.TopRight, "Corner of the screen."); // enum ScreenCorner { TopLeft, TopRight }
Note = Config.Bind("Debug", "Note", "", "Free text: not shown on the menu.");

// In MenuGlue.Lib.Register(ConfigFile config):
BelmontUi.Settings.RegisterFromConfig(Plugin.Guid, "My Overlay", config, summary: "A small overlay.");
```

This gives the rows "Enabled" (toggle), "Hud opacity" (slider), "Max icons" (int slider), "Corner" (a list showing "Top left" and "Top right"), then **Restore defaults**. `Note` is left out because it is free text. Pass the plugin's `Config` into the glue method, since `Config` is an instance member of your plugin.

Use `extra` to add rows the metadata can't express, after the generated ones. If `extra` adds a button, no **Restore defaults** row is added, so add `ResetButton()` yourself when you want one:

```csharp
BelmontUi.Settings.RegisterFromConfig(Plugin.Guid, "My Overlay", config, extra: menu =>
{
    menu.Key(Plugin.ToggleKey, "Toggle key", new[] { "F5", "F6", "F7", "F8" });
    menu.Button("Recalibrate", Overlay.Recalibrate, text: "Run");
    menu.ResetButton();
});
```

To give an entry a nicer format than `{0:0.##}`, leave its range off in `Config.Bind` (so it is skipped) and add the slider in `extra`.

## A key setting

Keep the key as a `string` holding an Input System `Key` name, and offer a list:

```csharp
// In Plugin.Load():
ToggleKey = Config.Bind("Keys", "ToggleKey", "F5", "Keyboard key that shows or hides the overlay.");

// In MenuGlue.Lib.Register():
menu.Key(Plugin.ToggleKey, "Toggle key",
    new[] { "F5", "F6", "F7", "F8", "Digit1", "Digit2", "Mouse4", "Mouse5" },
    "Keyboard key that shows or hides the overlay.");
```

The player sees "F5", "1", "Mouse 4". To read the key in your mod, parse the name with `Enum.TryParse<UnityEngine.InputSystem.Key>` for keyboard names, and handle `Mouse4`/`Mouse5` yourself.

## A reset button

```csharp
menu.ResetButton();                                                   // "Restore defaults" / "Reset"
menu.ResetButton(hint: "Every overlay setting back to its default."); // custom hint
menu.ResetButton("Defaults", "Back to the original values.", "Restore");
```

It sets every setting row of this menu back to its `DefaultValue`, saves the config file and redraws the rows. `RegisterFromConfig` adds one for you unless `extra` added a button.

## A custom action button

```csharp
menu.Button("Recalibrate", () =>
{
    Overlay.Recalibrate();
    Plugin.Logger.LogInfo("Overlay recalibrated from the menu");
}, "Measures the screen again.", text: "Run");
```

The action runs on the game's main thread, as soon as the row is pressed. If it changes config entries, the rows redraw with the new values. Exceptions are caught and logged as `[ui] <guid>: 'Recalibrate' failed: ...`.

## Replacing a registration at run time

A menu is live: change it and the tab rebuilds on its next poll. Rows can be added later but not removed one by one, so to drop rows, register the menu again.

```csharp
// Rename or re-describe:
var menu = BelmontUi.Settings.Get(Plugin.Guid);
if (menu != null)
{
    menu.Title = "My Overlay (beta)";
    menu.Summary = "Experimental features on.";
}

// Rebuild the rows when a feature switches on or off:
// registering the same GUID again replaces the whole menu.
BelmontUi.Settings.Register(Plugin.Guid, "My Overlay", m =>
{
    m.Toggle(Plugin.Advanced, "Advanced mode");
    if (Plugin.Advanced.Value)
        m.Slider(Plugin.Smoothing, "Smoothing", 0f, 1f, 0.1f);
    m.ResetButton();
});

// Or take it off the tab:
BelmontUi.Settings.Unregister(Plugin.Guid);
```

Keep these calls inside the glue class too, for example as methods on `MenuGlue.Lib` called from your code only when `MenuGlue.Available` is true.
