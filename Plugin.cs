using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace BelmontUi;

// A library for other mods: a "Mods Settings" tab in the game's own Settings menu, built from the
// game's settings widgets, where each mod's settings appear once declared. On its own it only adds
// the tab, which then says that no mod has registered settings. Mods soft-depend on this plugin
// (see README.md).
[BepInPlugin(Guid, "Belmont UI", Version)]
public class BelmontUiPlugin : BasePlugin
{
    public const string Guid = "leandrobueno.belmontui";
    public const string Version = "0.1.9";

    internal static ManualLogSource Logger;

    public override void Load()
    {
        Logger = Log;
        AddComponent<BelmontUiWatcher>();
        Log.LogInfo("Belmont UI loaded");
    }
}
