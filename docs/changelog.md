---
title: Changelog
nav_order: 7
---

# Changelog

Versions 0.1.0 to 0.2.0 were built on 2026-10-19, each 0.1.x after an in-game test of the one before. 0.1.9 is the first version committed to git, and the first one the author confirmed has no visual issues.

## 0.2.0

- The tab opens on a **list of registered mods**: title, one-line summary and a **Settings** button each. Each mod's settings are a sub-menu with a Back action (B, Esc, Backspace, or the Back row).
- `SettingsPage` is renamed `SettingsMenu`; the builder methods are unchanged.
- XML documentation for the whole public API, shipped as `BelmontUI.xml`.
- `Summary` on menus, and an optional `summary` parameter on `Register` and `RegisterFromConfig`.

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
