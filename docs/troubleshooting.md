---
title: Troubleshooting
nav_order: 6
---

# Compatibility and troubleshooting
{: .no_toc }

1. TOC
{:toc}

## Log lines to look for

Open `BepInEx\LogOutput.log` in the game folder after starting the game. Lines from the library start with `[ui]`.

| Line | Meaning |
|---|---|
| `Belmont UI loaded` | The library itself loaded. |
| `[ui] <guid>: settings page '<title>' registered (<n> settings)` | A mod registered its menu. |
| `[ui] MODS tab added to '<bar>' (page from '<source>' ...)` | The tab exists in that menu. |
| `[ui] MODS tab: <n> mod(s) [<titles>], <m> rows in all` | The mod list was built with these mods. |
| `[ui] '<title>' opened (...)` / `[ui] '<title>' closed (...)` | A mod's sub-menu was opened or closed. |
| `[ui] sub-menu: the game's back actions are off while it is open: [...]` | The input actions suspended so B returns to the list. Logged once. |
| `[ui] Belmont Minimap is installed: waiting for its MODS tab before adding BelmontUI's MODS tab` | Minimap is there; BelmontUI waits for it. |
| `[ui] Belmont Minimap's MODS tab is there; adding BelmontUI's MODS tab after it` | The wait ended normally. |
| `[ui] MODS tab not added to '<bar>': <why>` | Building the tab failed; the game's menu is untouched. |
| `[ui] MODS tab not added: no Settings screen in this scene to copy rows from` | This scene has no Settings screen to copy rows from yet. |
| `[ui] <guid>: row '<label>' not added: ...` | One row failed; the others still show. |
| `[ui] <guid>: '<label>' failed: ...` | A button's action threw. |
| `[ui] <guid>: save failed: ...` | Writing a config file failed. |
| `[ui] watcher failed: ...` | An unexpected error in the polling loop. Please report it. |

For more detail, set `LogLevels = All` in `BepInEx\config\BepInEx.cfg` (section `[Logging.Disk]`) to see the debug lines too, such as skipped config entries and tab moves.

## "There is no MODS tab"

1. Is `BelmontUI.dll` in `BepInEx\plugins\BelmontUI\`? Does the log have `Belmont UI loaded`?
2. Is BepInEx 6 IL2CPP installed and working? Other mods loading is a good sign.
3. Have you opened the pause menu (in game) or **Settings** (title screen)? The tab is added when the menu exists, not at start-up.
4. With Belmont Minimap installed, is its MODS tab there? BelmontUI waits for it, up to 20 seconds with the menu closed.
5. Search the log for `not added`. The reason after the colon says which part of the menu was not found. After a game update this usually means BelmontUI needs an update too.

## "The tab is there but my mod isn't on it"

1. Does the log have `[ui] <your guid>: settings page ... registered`? If not, your registration never ran.
2. Does your mod's log say the library is missing? The chainloader check runs in `Load()`; make sure your plugin has the soft `BepInDependency` attribute, so BelmontUI loads first.
3. Look for an `ArgumentException` from a row: a bad row stops the whole `Register` call. See [validation errors](api#validation-errors).

## "My mod doesn't load without BelmontUI"

The log shows `FileNotFoundException: Could not load file or assembly 'BelmontUI'`. A `BelmontUi.*` type is mentioned outside the isolated glue class: in the plugin class, in a field, in a method signature, or in a method that also runs when the library is absent. Move it into the glue class. A `try/catch` cannot fix this, because the exception happens while the method is compiled. See [the soft-dependency pattern](getting-started#the-soft-dependency-pattern).

## "B closes the whole menu instead of going back to the list"

Look for the `back actions are off` line. If it is missing or the list is empty, the game's back action has a name BelmontUI doesn't recognise (it looks for Close, Cancel or Back). Use the **Back** row meanwhile and report the line with your game version.

## A setting doesn't stick

- Values apply at once but are written to disk when you leave the sub-menu or close the menu. Quitting the game from inside the sub-menu by other means may skip the save.
- A Choice row whose entry holds a value not in its list shows the first option. Check that the values you pass match what `Config.Bind` stores.

## Compatibility

| With | Status |
|---|---|
| Belmont Minimap | Supported. Its MODS tab comes first; BelmontUI's MODS tab follows it. |
| Other mods that add tabs | Supported. BelmontUI's MODS tab moves itself to the end and never reorders other tabs. |
| Mods without BelmontUI support | Unaffected. |
| Game updates | May stop the tab from appearing until BelmontUI is updated. The game's menus keep working. |
