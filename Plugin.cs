using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace BelmontUi;

/// <summary>
/// The BelmontUI plugin itself. It adds the MODS tab; other mods put their settings on it
/// through <see cref="Settings"/>. On its own it only adds the tab, which then says that no mod has
/// registered settings.
/// </summary>
/// <remarks>Mods depend on it softly: <c>[BepInDependency("leandrobueno.belmontui", BepInDependency.DependencyFlags.SoftDependency)]</c>,
/// written with the GUID string so the attribute doesn't need this assembly at run time.</remarks>
[BepInPlugin(Guid, "Belmont UI", Version)]
public class BelmontUiPlugin : BasePlugin
{
    /// <summary>
    /// The plugin GUID, <c>"leandrobueno.belmontui"</c>. Use it in your <c>BepInDependency</c> attribute and in the
    /// <c>IL2CPPChainloader.Instance.Plugins.ContainsKey</c> check that guards every call into this library.
    /// </summary>
    public const string Guid = "leandrobueno.belmontui";

    /// <summary>The plugin version.</summary>
    public const string Version = "0.3.0";

    internal static ManualLogSource Logger;

    /// <summary>Called by BepInEx when the plugin loads: starts the component that adds the tab.</summary>
    public override void Load()
    {
        Logger = Log;
        AddComponent<BelmontUiWatcher>();
        Log.LogInfo("Belmont UI loaded");
    }
}
