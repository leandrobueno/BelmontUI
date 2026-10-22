---
title: Getting started
nav_order: 2
---

# Getting started
{: .no_toc }

1. TOC
{:toc}

## For players

1. Install [BepInEx 6 for IL2CPP](https://builds.bepinex.dev/projects/bepinex_be) (build be.788 or later) into the game folder and start the game once.
2. Put `BelmontUI.dll` in `BepInEx\plugins\BelmontUI\`.
3. Start the game and open the pause menu. A **MODS** tab is the last tab of the top bar. On the title screen, open **Settings** and the tab is there too.

On its own the tab says that no mod has registered settings. It fills up as you install mods that use it.

### Using the tab

1. The tab opens on a list of the mods that registered settings, sorted by name. Each row shows the mod's name, a one-line description and a **Settings** button.
2. Press **Settings** (A, Enter or a click) to open that mod's settings. The page title changes to the mod's name.
3. Change values with the usual controls: A toggles a checkbox, Left / Right moves sliders and lists. Changes apply at once. On a button row (a controller icon or key name in a frame), press A, then the button, key or combination you want; **B**, **Esc** or **Backspace** cancel. Each setting's description is next to its name.
4. Press **B**, **Esc** or **Backspace**, or choose the **Back** row at the end, to return to the list. The mod's settings are saved to disk then. B goes back to the list here; it does not close the pause menu.

Switching to another tab with LB / RB while a mod's settings are open saves them, and the tab opens on the list again next time. Mods that use BelmontUI work without it as well; you then change their settings in `BepInEx\config\<mod>.cfg`.

## Requirements for mod authors

| | |
|---|---|
| Game | Castlevania: Belmont's Curse (Steam) |
| Loader | BepInEx 6 IL2CPP, build be.788 |
| Target framework | net6.0 |
| Library GUID | `leandrobueno.belmontui` |
| Namespace | `BelmontUi` |
| Dll | `<game>\BepInEx\plugins\BelmontUI\BelmontUI.dll` |

Reference the installed dll from your mod's `.csproj`. `Private="false"` stops the dll from being copied next to your mod:

```xml
<!-- Optional at run time (soft dependency). Install or build BelmontUI first. -->
<Reference Include="$(GameDir)\BepInEx\plugins\BelmontUI\BelmontUI.dll" Private="false" />
```

## The soft-dependency pattern

Players may not have BelmontUI installed. Three rules keep your mod loading in that case.

1. **Declare a soft dependency.** `[BepInDependency("leandrobueno.belmontui", BepInDependency.DependencyFlags.SoftDependency)]` makes BepInEx load BelmontUI before your mod when it is there, and ignore it when it is not.
2. **Check by GUID in the chainloader.** `IL2CPPChainloader.Instance.Plugins.ContainsKey("leandrobueno.belmontui")` tells you whether the library loaded.
3. **Keep every `BelmontUi.*` reference in one class that runs only after the check.** .NET compiles a method the first time it is called. If that method mentions a type from an assembly that is missing, compiling it throws `FileNotFoundException`, before any line of it runs. So a `try/catch` around the call cannot catch it, and a field of a BelmontUI type on your plugin class breaks loading too. A method that only *calls* the isolated method is compiled fine.

### Full example: Ability Wheel's MenuGlue

This is the glue class of Ability Wheel, the first mod to use BelmontUI. The plugin class carries the dependency attribute and calls `MenuGlue.Register()` at the end of `Load()`:

```csharp
[BepInPlugin(Guid, "Ability Wheel", Version)]
[BepInDependency(MenuGlue.LibGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        // ... Config.Bind for every setting ...
        MenuGlue.Register();
    }
}
```

```csharp
using System;
using BepInEx.Unity.IL2CPP;

namespace AbilityWheel;

// Everything that touches the library lives in the nested Lib class: a method is only compiled
// when it's first called, so with the library missing those methods are never compiled and the
// mod loads and works without the tab.
internal static class MenuGlue
{
    internal const string LibGuid = "leandrobueno.belmontui";

    internal static bool Available { get; private set; }

    internal static void Register()
    {
        Available = IL2CPPChainloader.Instance.Plugins.ContainsKey(LibGuid);
        if (!Available)
        {
            Plugin.Logger.LogWarning(
                $"Belmont UI ({LibGuid}) isn't installed: no MODS tab in the Settings menu, edit the config file instead");
            return;
        }
        try
        {
            Lib.Register();
        }
        catch (Exception e)
        {
            // Not the presence check: the library is known to be loaded here. This catches a
            // row that fails validation, so a bad row can't stop the mod from loading.
            Available = false;
            Plugin.Logger.LogError($"[settings] Belmont UI registration failed, the MODS tab is off: {e}");
        }
    }

    private static class Lib
    {
        internal static void Register()
        {
            BelmontUi.Settings.Register(Plugin.Guid, "Ability Wheel", page =>
            {
                page.Summary = "Cast up to 4 Tarot spells: hold the wheel button and press Y/X/B/A.";
                foreach (var row in SettingRows.All())
                {
                    switch (row.Kind)
                    {
                        case "bool":
                            page.Toggle(row.Entry, row.LabelEn, row.HintEn);
                            break;
                        case "float":
                            page.Slider(row.Entry, row.LabelEn, row.Min, row.Max, row.Step, row.Format, row.HintEn);
                            break;
                        default:
                            page.Choice(row.Entry, row.LabelEn, row.Values, row.ChoicesEn, row.HintEn);
                            break;
                    }
                }
                page.ResetButton(hint: "Every Ability Wheel setting back to its default.");
            });
        }
    }
}
```

Why each piece is there:

| Piece | Why |
|---|---|
| `LibGuid` as a `const string` | A constant is copied into the attribute and the check at compile time, so it never loads BelmontUI. |
| The chainloader check | It is the only safe way to ask "is the library there?" without touching its types. |
| The warning when missing | The player learns why there is no menu and where the settings are instead. |
| The nested `Lib` class | It holds the only methods that mention `BelmontUi.*`, and they are compiled only once `Lib.Register()` is first called. |
| The `try/catch` inside `MenuGlue` | It catches `ArgumentException` from a badly declared row. It is not the presence check. |

`SettingRows` is Ability Wheel's own list of its settings; your mod can call the builder methods directly. See [Recipes](recipes) for shorter examples.

Besides value rows and buttons, a menu can hold **binding rows** (`Binding`), where the player presses the button, key or combination to use, and **section headers** (`Header`). `VisibleWhen` shows the row added just before it only while a condition holds. See [Recipes](recipes#a-gamepad-button-picked-by-pressing-it) and the [API reference](api#binding).

## Check it worked

Open the pause menu, go to **MODS**, find your mod's row and press **Settings**. `BepInEx\LogOutput.log` should contain:

```text
[Info   :Belmont UI] [ui] author.mymod: settings page 'My Mod' registered (3 settings)
```

Then remove `BepInEx\plugins\BelmontUI` and start the game again. Your mod must still load. If you see `FileNotFoundException: Could not load file or assembly 'BelmontUI'`, a `BelmontUi.*` reference has escaped the glue class.
