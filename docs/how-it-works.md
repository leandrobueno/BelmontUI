---
title: How it works
nav_order: 5
---

# How it works
{: .no_toc }

BelmontUI ships no assets. Everything on the tab is found at run time and copied from the game's own menus, so it looks, sounds and navigates like the native Settings pages.

1. TOC
{:toc}

## Finding the menus

A watcher component checks every 30 frames, about half a second at 60 fps. The game rebuilds its menus for each scene, so the work repeats per scene.

- **In game**, where a `GameMenuManager` exists, the tab goes into the pause menu's top bar.
- **On the title screen**, where there is no pause menu, the tab goes into each Settings screen, next to CONTROLS, GAMEPLAY and the others.

Every lookup is by component type, never by object name.

## Tab placement: always last

The MODS SETTINGS tab is appended to the bar, never inserted in front of another mod's tab. On every poll BelmontUI checks three things and fixes only its own tab:

- it is the last entry of the bar's tab array;
- its toggle is the last sibling in the toggle row;
- its toggle is the last one in the toggle group.

If another mod added a tab after it, BelmontUI moves its own tab back to the end and logs `[ui] tab moved to the end of '<bar>' (after '<tab>')` at debug level. If another mod rewrote the tab array without it, it is put back: `[ui] Mods Settings tab put back into '<bar>' (another mod rewrote its tabs)`.

The other toggles on the bar are narrowed by n/(n+1) so one more fits.

## Belmont Minimap coexistence

Belmont Minimap (`local.belmont.minimap`) adds a MODS tab only to a pause-menu bar that still has exactly the five native tabs. So when Minimap is installed, BelmontUI leaves the bar alone until Minimap's `TabContent_MODS` tab is there, then adds its own after it. If Minimap's tab never comes, BelmontUI gives up waiting 20 seconds after the bar initialised with the menu closed.

Whether Minimap is installed is decided on the first poll, not at load: BelmontUI loads before Minimap, so the plugin list is incomplete during its own `Load()`.

## Cloning the native widgets

1. **The page** is a copy of the Settings screen's GAMEPLAY tab, or any settings tab when that is missing. Its scroll area and navigation group stay; its rows and settings logic go. The title becomes "MODS SETTINGS" and the long note is cleared. On the pause menu, the content moves into a shell copied from the last native pause-menu page, so it shows and hides like its neighbours.
2. **The top-bar toggle** is a copy of the bar's last visible toggle, relabelled. On every poll it is matched to a native toggle (sizes, layout, text settings, images) while both are off, so it stays identical.
3. **The rows** are copies of native rows: the checkbox row for toggles, the slider row for sliders, the list row for choices and keys. The game's binding component is stripped and the Doozy control is driven against your `ConfigEntry`. Localizers are stripped so your texts stay. Controller navigation uses the same selectable the native page uses for that kind of row.
4. **Buttons** are a copy of the System page's framed button, placed where other rows have their control. Before the pause menu has been opened once there is nothing to copy, so on the title screen a button row shows a checkbox that works the same.
5. **The mod list**: one row per registered mod, sorted by title (case-insensitive), with the summary and a **Settings** button. Each mod's rows and a **Back** row form its sub-menu. While a sub-menu is open, the game's Close / Cancel / Back input actions are switched off, so B goes back to the list instead of closing the menu.
6. **Sounds**: value changes play the native row's change sound; opening, closing and reset play the Settings screen's sounds.

## Where saves happen

- A control writes its `ConfigEntry` immediately when it changes.
- Config files are written to disk when the player leaves a sub-menu, when the tab loses focus, when the menu closes, and when `ResetAll()` or `Save()` is called.
- On the title screen's Settings screen, the tab reports no pending changes, so the game's apply / discard flow never asks about it.

Each mod's values go to its own `BepInEx\config\<guid>.cfg`. BelmontUI has no config file.

## What breaks on game updates, and how it fails

The tab depends on the game's menu components: the Settings tab view and its rows, the pause menu's tab view, and the Doozy UI controls. A game update that renames or restructures them can stop the tab from being built.

BelmontUI fails safe:

- Every step is guarded. A failure logs `[ui] Mods Settings tab not added to '<bar>': <reason>` and leaves the game's menu as it was. You lose the tab, not the menu.
- A single row that fails logs `[ui] <guid>: row '<label>' not added: ...` and the other rows still show.
- Mods keep working either way; their settings stay editable in their `.cfg` files.
- Because the public API uses only plain .NET types, a rebuilt BelmontUI after a game update does not force consumer mods to rebuild.
