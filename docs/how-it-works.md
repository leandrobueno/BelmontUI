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

BelmontUI's MODS tab is appended to the bar, never inserted in front of another mod's tab. On every poll BelmontUI checks three things and fixes only its own tab:

- it is the last entry of the bar's tab array;
- its toggle is the last sibling in the toggle row;
- its toggle is the last one in the toggle group.

If another mod added a tab after it, BelmontUI moves its own tab back to the end and logs `[ui] tab moved to the end of '<bar>' (after '<tab>')` at debug level. If another mod rewrote the tab array without it, it is put back: `[ui] MODS tab put back into '<bar>' (another mod rewrote its tabs)`.

The other toggles on the bar are narrowed by n/(n+1) so one more fits.

## Belmont Minimap coexistence

Belmont Minimap (`local.belmont.minimap`) adds a MODS tab only to a pause-menu bar that still has exactly the five native tabs. So when Minimap is installed, BelmontUI leaves the bar alone until Minimap's `TabContent_MODS` tab is there, then adds its own after it. If Minimap's tab never comes, BelmontUI gives up waiting 20 seconds after the bar initialised with the menu closed.

Whether Minimap is installed is decided on the first poll, not at load: BelmontUI loads before Minimap, so the plugin list is incomplete during its own `Load()`.

## Cloning the native widgets

1. **The page** is a copy of the Settings screen's GAMEPLAY tab, or any settings tab when that is missing. Its scroll area and navigation group stay; its rows and settings logic go. The title becomes "MODS" and the long note is cleared. On the pause menu, the content moves into a shell copied from the last native pause-menu page, so it shows and hides like its neighbours.
2. **The top-bar toggle** is a copy of the bar's last visible toggle, relabelled. On every poll it is matched to a native toggle (sizes, layout, text settings, images) while both are off, so it stays identical.
3. **The rows** are copies of native rows: the checkbox row for toggles, the slider row for sliders, the list row for choices and keys. The game's binding component is stripped and the Doozy control is driven against your `ConfigEntry`. Localizers are stripped so your texts stay. Controller navigation uses the same selectable the native page uses for that kind of row.
4. **Buttons** are a copy of the System page's framed button, placed where other rows have their control. Before the pause menu has been opened once there is nothing to copy, so on the title screen a button row shows a checkbox that works the same.
5. **The mod list**: a copy of the checkbox row per registered mod, with the title as its label, the summary as a smaller copy of the label, and a copied System-tab button captioned **Settings**. See [the mod list and sub-menus](#the-mod-list-and-sub-menus).
6. **Binding rows** are button rows whose caption is the binding as TMP rich text (see [binding rows](#binding-rows)).
7. **Headers** are a copy of a row's label, in bold, one row tall, with no control.
8. **Descriptions**: a row's hint is a smaller copy of its label (see [descriptions](#descriptions)).
9. **Sounds**: value changes play the native row's change sound; opening, closing and reset play the Settings screen's sounds.

## The mod list and sub-menus

On every rebuild, BelmontUI builds all rows at once: one list row per registered mod, sorted by title (case-insensitive), then each mod's rows followed by a **Back** row. The list and the sub-menus only switch which rows are active, so moving between them doesn't rebuild anything. The log gets `[ui] MODS tab: <n> mod(s) [<titles>], <m> rows in all`. With no mod registered, the page shows one line: "No mod has registered settings."

- **The summary** is placed like a row's description, in the column after the longest name of the list (see [descriptions](#descriptions)), once the list row has been drawn, because a name measured before it is shown comes out too wide.
- **Opening.** Pressing a list row's **Settings** button opens the sub-menu on the next frame. The page title becomes the mod's title and the first row is selected. The Settings screen's open sound plays, and the log gets `[ui] '<title>' opened (...)`.
- **Back.** B, Esc, Backspace or the **Back** row close the sub-menu. The mod's config files are saved, its list row is selected again, the close sound plays, and the log gets `[ui] '<title>' closed (...)`.
- **The game's back actions.** While a sub-menu is open, every enabled input action whose name contains Close, Cancel or Back is switched off, in every enabled action map. Without that, B would also close the pause menu or the Settings screen. They are listed once in the log as `[ui] sub-menu: the game's back actions are off while it is open: [...]`. They come back once the key that closed the sub-menu is released, and at once when the tab loses focus or the menu closes.
- **Leaving the tab.** If the tab loses focus or the menu closes while a sub-menu is open, the sub-menu closes silently and saves, and the tab opens on the list next time.

## Binding rows

- **Listening.** Pressing a binding row puts "Press new button" (the game's own text) on its line and "..." on its button, and switches off every input action the game has on, so the press doesn't also navigate, switch tabs or close the menu. Exactly the actions it switched off are switched on again afterwards. The log gets `[ui] <guid>: '<label>' listening (<devices>, was '<binding>')`.
- **Reading.** The controls are read straight from the devices (`Gamepad.current`, every key of `Keyboard.current`, the mouse's back and forward buttons), not through the game's actions. The press that opened the row must be let go first; a control the Input System still reports as down after 2 seconds is ignored.
- **Combinations.** A control pressed and released alone is stored as it is. A second control pressed while the first is held stores both, held first: `rightShoulder+leftShoulder`. Two controls pressed in the same frame (a quick Shift+G) make a combination too, with the modifier key as the held one. Both controls must be on the same kind of device.
- **Cancel and timeout.** B, Esc or Backspace cancel, like the game's Controls screen, so those three can't be bound by pressing them. `TimeoutSeconds` (10 by default) without input cancels too. The old binding stays either way.
- **Checks.** A control outside `Allowed`, in `Reserved`, or refused by `Validate` shows the reason on the row's line, and the row listens again once everything is let go. A `Validate` that throws refuses the binding and logs the error.
- **Afterwards.** The binding is written to the entry, the log gets `[ui] <guid>: '<label>' bound: '<binding>'` (or `cancelled`, `no input in time`), and the menu gets its input back once the controls are let go, or after 2 seconds in any case.

### Controller icons

The game draws its prompts as TMP sprite tags: a device prefix plus the Input System control path, such as `<sprite name="xbox_buttonSouth">` or `<sprite name="ps5_dpad/up">`, from the default sprite asset (`RedWine_GlobalSpriteAsset`), which falls back to every glyph set. Bindings are stored in the same control names, so a row's caption is the prefix plus each control. The prefix is the game's own choice (`InputTagTextPreprocessor.GetPrefix`) for the device the game last saw used (`InputDeviceWatcherService.LastUsedDevice`); if that can't be called, it comes from the device type (XInput: `xbox_`, DualSense: `ps5_`, DualShock: `ps4_`, Switch: `switch_`). When the prefix changes, the rows are drawn again and the log gets `[ui] binding rows: controller icons now '<prefix>'`. Keys show as names, since the game's keyboard glyphs cover only a few keys. `ControlBinding.Display` returns the same rich text for your own labels.

## Descriptions

A row's hint sits between its label and its control, in a column that starts just after the longest label of the page (no further right than about 39 % of the row's width), the same on every row. It is fitted to two lines: the font starts at 68 % of the label's size and shrinks to 52 % before the text is cut with "...".

A description on two lines makes its row taller: the row's layout height, and that of its `Background`, grow by the extra line plus 16 units of padding above and below (about 10 pixels at 1080p). The dark strip stretches with the row and the selection ornaments sit on its middle; the label, which the game anchors to the top on toggle and slider rows, is re-anchored to the middle with the control and the description. One-line rows keep the game's height. The log gets `[ui] descriptions: <n> row(s) resized for two-line descriptions`.

## Rows that react to other settings

The tab listens to `SettingChanged` on every config file bound to it. When an entry changes from anywhere (the player, another mod's handler, a reset), its rows are drawn again and every `VisibleWhen` condition of the open sub-menu is checked. A row whose condition turns false is switched off, so the layout closes the gap and Up / Down skip it; if it was selected, the selection moves to the nearest row still shown. The log gets `[ui] '<title>': <n> row(s) shown or hidden by their conditions`.

## Where saves happen

- A control writes its `ConfigEntry` immediately when it changes.
- Config files are written to disk when the player leaves a mod's sub-menu (that mod's files), when the tab loses focus, when the menu closes, and when `ResetAll()` or `Save()` is called.
- On the title screen's Settings screen, the tab reports no pending changes, so the game's apply / discard flow never asks about it.

Each mod's values go to its own `BepInEx\config\<guid>.cfg`. BelmontUI has no config file.

## What breaks on game updates, and how it fails

The tab depends on the game's menu components: the Settings tab view and its rows, the pause menu's tab view, and the Doozy UI controls. A game update that renames or restructures them can stop the tab from being built.

BelmontUI fails safe:

- Every step is guarded. A failure logs `[ui] MODS tab not added to '<bar>': <reason>` and leaves the game's menu as it was. You lose the tab, not the menu.
- A single row that fails logs `[ui] <guid>: row '<label>' not added: ...` and the other rows still show.
- Mods keep working either way; their settings stay editable in their `.cfg` files.
- Because the public API uses only plain .NET types, a rebuilt BelmontUI after a game update does not force consumer mods to rebuild.
