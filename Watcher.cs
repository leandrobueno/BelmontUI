using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace BelmontUi;

// The game rebuilds its menus per scene, so every half second this looks for a place for the MODS
// tab: the pause menu's tab bar when the scene has one (in game), else the Settings
// screen's (title screen). A menu where adding failed is left alone until the next scene, so a
// game update that moves things around costs the tab, not the menu. Tabs already added are
// checked every poll, because Belmont Minimap rewrites the pause menu's tab array.
//
// Belmont Minimap adds its own MODS tab to the pause menu bar, and only when that bar still has
// exactly the five native tabs (it snapshots them and refuses any change until it has committed
// its sixth, which it does once the menu has been opened and closed). So with that mod installed,
// the bar is left alone until its MODS tab is there, or until a timeout with the menu closed, in
// case that mod never adds it.
/// <summary>The library's Unity component, which finds the game's menus and adds the MODS tab. Not for direct use.</summary>
public class BelmontUiWatcher : MonoBehaviour
{
    private const int PollFrames = 30;
    private const string MinimapGuid = "local.belmont.minimap";
    private const string MinimapTabName = "TabContent_MODS";
    private const float MinimapWaitSeconds = 20f;

    /// <summary>Required by Il2CppInterop for a component injected with <c>AddComponent</c>; not for direct use.</summary>
    /// <param name="pointer">The native object pointer.</param>
    public BelmontUiWatcher(IntPtr pointer) : base(pointer) { }

    // Decided on the first poll, not at load: this plugin loads before Belmont Minimap.
    private bool? _minimapPresent;

    private readonly Dictionary<int, ModsTab> _tabs = new();      // by the host view's instance id
    private readonly Dictionary<int, float> _waitingSince = new();
    private readonly HashSet<int> _failed = new();
    private readonly HashSet<int> _waitLogged = new();
    private int _sceneHandle;

    private void Update()
    {
        try
        {
            ModsTab.RunFrameWork();

            if (Time.frameCount % PollFrames != 0)
                return;

            GameLanguage.Poll();

            if (_minimapPresent == null)
            {
                var plugins = IL2CPPChainloader.Instance.Plugins;
                _minimapPresent = plugins.ContainsKey(MinimapGuid) ||
                    plugins.Keys.Any(k => k.IndexOf("minimap", StringComparison.OrdinalIgnoreCase) >= 0);
                BelmontUiPlugin.Logger.LogInfo(
                    $"[ui] Belmont Minimap {(_minimapPresent.Value ? "is" : "is not")} installed ({plugins.Count} plugins loaded)");
            }

            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene != _sceneHandle)
            {
                _sceneHandle = scene;
                _failed.Clear();
                _waitingSince.Clear();
                _waitLogged.Clear();
            }

            var settingsViews = FindAll<SettingsTabView>();
            var gameMenus = FindAll<GameMenuManager>();

            // A settings view to copy the page and rows from.
            SettingsTabView source = settingsViews.Count > 0 ? settingsViews[0] : null;

            bool pauseMenu = false;
            foreach (var menu in gameMenus)
            {
                var bar = menu.gameMenuTabView;
                if (bar == null || bar.gameObject == null)
                    continue;
                pauseMenu = true;
                int id = bar.GetInstanceID();
                if (!_tabs.ContainsKey(id) && !_failed.Contains(id) && ShouldWaitForMinimap(id, bar, menu))
                    continue;
                var captured = menu;
                var systemPage = menu.menuTab != null ? menu.menuTab.transform : null;
                Ensure(id, () => new TabHost<BasicTabView, BasicTab>(bar, true, () => captured.IsShowing()), source, systemPage);
            }

            if (pauseMenu)
            {
                // In game the tab is on the pause menu only: the Settings screen keeps its native tabs.
                foreach (var view in settingsViews)
                    Drop(view.GetInstanceID());
            }
            else
            {
                foreach (var view in settingsViews)
                    Ensure(view.GetInstanceID(), () => new TabHost<SettingsTabView, SettingTab>(view, false), view);
            }

            var gone = new List<int>();
            foreach (var pair in _tabs)
            {
                if (!pair.Value.Alive)
                    gone.Add(pair.Key);
            }
            foreach (int id in gone)
                _tabs.Remove(id);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogError($"[ui] watcher failed: {e}");
        }
    }

    // True while Belmont Minimap is installed but hasn't put its MODS tab on this bar yet. The
    // clock starts when the bar is first seen initialised and only runs out with the menu closed,
    // since that mod waits for an initialised, closed menu itself.
    [HideFromIl2Cpp]
    private bool ShouldWaitForMinimap(int id, BasicTabView bar, GameMenuManager menu)
    {
        if (_minimapPresent != true)
            return false;
        var tabs = bar.tabs;
        int count = 0;
        if (tabs != null)
        {
            foreach (var tab in tabs)
            {
                if (tab == null)
                    continue;
                count++;
                if (tab.gameObject.name == MinimapTabName)
                {
                    BelmontUiPlugin.Logger.LogInfo("[ui] Belmont Minimap's MODS tab is there; adding BelmontUI's MODS tab after it");
                    return false;
                }
            }
        }

        if (_waitLogged.Add(id))
            BelmontUiPlugin.Logger.LogInfo("[ui] Belmont Minimap is installed: waiting for its MODS tab before adding BelmontUI's MODS tab");

        bool initialised = bar._initialized;
        bool showing = menu.IsShowing();
        if (!initialised)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] waiting for Minimap: bar not initialised yet ({count} tabs)");
            return true;
        }
        if (!_waitingSince.TryGetValue(id, out float since))
            _waitingSince[id] = since = Time.unscaledTime;
        float waited = Time.unscaledTime - since;
        if (waited < MinimapWaitSeconds || showing)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] waiting for Minimap: {waited:0}s, {count} tabs, menu {(showing ? "open" : "closed")}");
            return true;
        }
        BelmontUiPlugin.Logger.LogWarning(
            $"[ui] Belmont Minimap didn't add its MODS tab within {MinimapWaitSeconds:0}s; adding BelmontUI's MODS tab now (that mod may then skip its tab)");
        return false;
    }

    [HideFromIl2Cpp]
    private void Ensure(int id, Func<ITabHost> host, SettingsTabView source, Transform buttonSource = null)
    {
        if (_failed.Contains(id))
            return;
        if (_tabs.TryGetValue(id, out var existing))
        {
            if (existing.Alive)
            {
                existing.Tick();
                return;
            }
            _tabs.Remove(id);
        }
        if (source == null)
        {
            BelmontUiPlugin.Logger.LogWarning("[ui] MODS tab not added: no Settings screen in this scene to copy rows from");
            _failed.Add(id);
            return;
        }
        var tab = ModsTab.TryAdd(host(), source, buttonSource);
        if (tab != null)
            _tabs[id] = tab;
        else
            _failed.Add(id);
    }

    [HideFromIl2Cpp]
    private void Drop(int id)
    {
        if (!_tabs.TryGetValue(id, out var tab))
            return;
        tab.Remove();
        _tabs.Remove(id);
    }

    // Instances in loaded scenes; prefab assets that are also in memory have no scene.
    private static List<T> FindAll<T>() where T : Component
    {
        var found = new List<T>();
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()))
        {
            var component = obj?.TryCast<T>();
            if (component == null || component.gameObject == null)
                continue;
            if (!component.gameObject.scene.IsValid())
                continue;
            found.Add(component);
        }
        return found;
    }
}
