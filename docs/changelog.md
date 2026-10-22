---
title: Changelog
nav_order: 7
---

# Changelog

Every version from 0.1.0 to 0.2.2 was built on 2026-10-19, 0.3.0 on 2026-10-21; each later one followed an in-game test of the one before. 0.1.9 was the first state the author confirmed has no visual issues. 0.2.2 was tested in game with the mod list, sub-menus and Back working. 0.3.0 was tested in game in English, French, Japanese and Russian, with live language changes, on the pause menu and the title screen. 0.4.0 (built 2026-10-22) is the current version, tested in game in English, German and Japanese: binding rows with a gamepad (Xbox and PlayStation icons) and the keyboard, combinations, cancel and refusals, headers, rows shown on a condition and two-line descriptions.

| Version | Git commit |
|---|---|
| 0.4.0 | not committed yet |
| 0.3.0 | `887f461`, `53497ab` (row alignment) |
| 0.2.2 | `0d1b056` (includes 0.2.0 and 0.2.1) |
| 0.1.9 | `b917e54` (includes 0.1.0 to 0.1.8) |

## 0.4.0

- **Binding rows** (`SettingsMenu.Binding`): the player selects the row and presses a button, key or combination, as on the game's Controls screen. Hold one control and press another for a combination (`rightShoulder+leftShoulder`). The row shows the binding with the game's own controller icons, Xbox or PlayStation as the game chooses, switching when the controller changes; keys show as names. B, Esc or Backspace cancel, as on the game's Controls screen, with a 10-second timeout as a backstop. The mod decides the devices and can allow, reserve or refuse controls with a reason shown on the row. "Press new button" and "Already assigned" are the game's own texts.
- `ControlBinding`: the stored format (Input System control names, `held+pressed`) with `Parse`, `Make`, `IsHeld`, `WasPressedThisFrame`, `Describe` and `Display`, so a mod reads its bindings the same way.
- **Section headers** (`SettingsMenu.Header`): a title line grouping the rows after it.
- **Rows shown on a condition** (`SettingsMenu.VisibleWhen`): a row shows only while its condition holds, re-checked whenever a setting changes; the layout closes the gap, Up / Down skip hidden rows, and the selection moves off a row that disappears.
- **Descriptions on every row**: a row's hint is now shown next to its label (it was only kept before), in a column that starts after the page's longest label, up to two lines, the font shrinking a little before the text is cut. A row whose description takes two lines grows by one line plus a little padding above and below, so the text stays inside the row's dark strip; one-line rows keep the game's height. The mod list's summaries use the same column.
- Rows redraw when an entry changes elsewhere (another mod's handler, a reset), and creating a `LocalizedText` no longer rebuilds the tab.
- New texts in all eleven languages. The 0.3 API is unchanged.

## 0.3.0

- The tab is called **MODS** on the pause menu and on the title screen's Settings, the same in every language (a player's suggestion). It is still always the last tab, and still comes after Belmont Minimap's own MODS tab.
- BelmontUI's own texts are translated into the game's eleven languages: Settings, Back, the reset row (name, button, hint), the empty-list line and On / Off. Settings, Back and the reset row's name and button are the game's own words, taken from its string tables.
- The tab follows the game's language, including a change while the game runs (the texts of the rows already built are rewritten).
- `LocalizedText`: a text with one version per language. Overloads taking it on `Register`, `RegisterFromConfig` and every builder method, plus `SettingsMenu.LocalizedTitle` / `LocalizedSummary`, `Settings.Language` and `Settings.LanguageChanged`.
- Backward compatible: the string methods are unchanged, so mods built against 0.2 load and behave as before; their default reset row is translated as well.
- Fixed: on the pause menu the rows sat at the left edge of the page instead of centred like the game's settings rows (in every version since 0.1.2). The copied settings content now keeps the Settings page's side margins inside the pause-menu page.
- Fixed: a slider's value showed its minimum in the slider's own format ("0.2" for 100%) until it was first moved. The copied slider writes its label when it first runs, after BelmontUI did; BelmontUI now writes it again for the first frames a slider row is shown.

## 0.2.2

- The summary on the mod list reads in full. It is first placed from a quarter of the row to the button, then moved to just after the mod's name the first frame the row is drawn. In 0.2.1 the name was measured before the row had ever been shown, came out several times too wide, and the summary was cut to "Cast up to...".

## 0.2.1

- The first opening of the tab shows only the mod list. In 0.2.0 it also showed most rows of every sub-menu under the list: a focus step from 0.1.3 that re-activates faded objects also re-activated the rows the list had hidden.
- The summary starts right after the mod's name and uses the width up to the button.

## 0.2.0

- The tab opens on a **list of registered mods**: title, one-line summary and a **Settings** button each. Each mod's settings are a sub-menu titled with the mod's name, with the Settings screen's open and close sounds.
- **Back** with B, Esc, Backspace or the Back row. While a sub-menu is open, the game's Close / Cancel / Back input actions are suspended, so B doesn't close the pause menu.
- API: `SettingsPage` is renamed `SettingsMenu`; the builder methods are unchanged. `SettingsMenu.Summary`, and an optional `summary` parameter on `Register` and `RegisterFromConfig`.
- XML documentation for the whole public API. The build writes `BelmontUI.xml` next to the dll, so IDEs show the descriptions.
- An object-style API (public row objects) was tried during 0.2.0 and made internal again before release. The builder methods are the only way to add rows.

## 0.1.9

- Fixes the blank page of 0.1.8: rows outside the visible band are hidden one by one instead of masking their zero-height container.

## 0.1.8

- The button is sized to the row and centred on its bar.
- The selection can no longer get stuck on the decorative button.
- The pause-menu page ends above the frame line and scrolls with the selection and the mouse wheel.

## 0.1.7

- Button rows show a copy of the System page's button and run at once.
- Rows redraw after **Restore defaults**.
- The pause-menu page has room above its title.
- API: `Button` and `ResetButton` take an optional `text` for the caption.

## 0.1.6

- When the page shows, every row starts in the Normal look, except the one the game selects. Before, copied rows could all look selected.

## 0.1.5

- List rows are copied from the plainest native list row, which removes a white bar the first template carried.

## 0.1.4

- Controller navigation reaches every row, including list rows.
- The Belmont Minimap wait now works: Minimap detection moved from load time to the first poll.
- The tab toggle matches the native ones exactly.
- Slider value labels stay on one line.

## 0.1.3

- The tab is kept last in the tab array, the toggle row and the toggle group, on every poll.

## 0.1.2

- Waits for Belmont Minimap's MODS tab before adding its own.
- The pause-menu page lives in a shell copied from a native pause-menu page, fixing a blank page.
- The tab toggle is matched to the native one down to its children.

## 0.1.1

- The tab moved to the pause menu's top bar; the title screen keeps it in Settings.
- Toggles fitted and aligned, the copied page's title and note fixed, no white box on list rows, slider values in the row's format.
- The tab is put back when another mod rewrites the tab array.

## 0.1.0

- First version: a Mods Settings tab in the Settings screen, with toggle, slider, int slider, list, key and button rows, and menus derived from `Config.Bind` metadata.
