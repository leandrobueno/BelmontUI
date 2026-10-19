---
title: Home
layout: home
nav_order: 1
---

# BelmontUI

BelmontUI is a library for **Castlevania: Belmont's Curse** mods running on BepInEx 6 (IL2CPP). It adds a **MODS SETTINGS** tab to the game's pause menu, and to the Settings screen on the title screen. Each mod that registers settings gets a row on that tab with its title, a one-line summary and a **Settings** button. The button opens the mod's own sub-menu, built from the game's own toggles, sliders, list steppers and buttons.

A mod declares its settings once, in `Load()`, with a few lines of C#:

```csharp
BelmontUi.Settings.Register(Plugin.Guid, "My Mod", menu =>
{
    menu.Toggle(Plugin.Enabled, "Enabled");
    menu.Slider(Plugin.Opacity, "Opacity", 0.2f, 1f, 0.05f, "{0:0%}");
    menu.ResetButton();
}, summary: "What my mod does, in one line.");
```

The dependency is optional. A mod that uses BelmontUI still loads and works without it, minus its entry on the tab.

## Documentation map

| Page | For | What it covers |
|---|---|---|
| [Getting started](getting-started) | players and mod authors | Installing, requirements, the soft-dependency pattern with a full example |
| [API reference](api) | mod authors | Every public member, what each row kind looks like, `RegisterFromConfig` rules |
| [Recipes](recipes) | mod authors | Copy-paste examples for common menus |
| [How it works](how-it-works) | the curious, maintainers | Tab placement, widget cloning, game updates, saving |
| [Troubleshooting](troubleshooting) | players and mod authors | Log lines, the "no tab" checklist, compatibility |
| [Changelog](changelog) | everyone | Versions 0.1.0 to the current one |

## For AI coding agents

The repository's [AGENTS.md](https://github.com/leandrobueno/BelmontUI/blob/main/AGENTS.md) lets Claude, Codex, Cursor and similar agents add BelmontUI support to a mod without reading the source. A machine-readable index of this site is at [llms.txt](llms.txt).

## License

MIT. See [LICENSE](https://github.com/leandrobueno/BelmontUI/blob/main/LICENSE).
