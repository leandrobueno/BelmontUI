using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine.InputSystem;

namespace BelmontUi;

// Belmont Minimap puts a MODS tab of its own on the pause menu, for its settings. With the
// library's one option on (HideMinimapTab), that tab is hidden and its settings are shown on this
// library's tab instead, as a "Belmont Minimap" page.
//
// The page is that mod's own list of menu rows (its entries, labels, hints, ranges and choices),
// read by reflection; if a newer version changes that list, the page is built from its config
// entries instead. The entries stay that mod's: it notices every change itself and saves its own
// file. Its tab is not removed, only made inactive the way the game makes a tab unavailable
// (UITabView.ChangeTabActivity), which the tab bar's own navigation skips, with its button hidden.
//
// Decided once per game start: rows built after the menus have been used can come out wrong, and
// the tab must never be hidden without the page that replaces it.
internal static class MinimapBridge
{
    internal const string Guid = "local.belmont.minimap";
    private const string TabName = "TabContent_MODS";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _started;
    private static bool _hide;
    private static bool _logged;

    private sealed class Row
    {
        internal int Group;
        internal ConfigEntryBase Entry;
        internal LocalizedText Label, Hint;
        internal float Min, Max, Step;
        internal LocalizedText[] Choices;
        internal string[] Values;
    }

    // Once, when the watcher knows which plugins are loaded and before any tab is built.
    internal static void Start()
    {
        if (_started)
            return;
        _started = true;
        if (!IL2CPPChainloader.Instance.Plugins.TryGetValue(Guid, out var info) || info?.Instance == null)
            return;

        // The library's own page, which only exists for this option.
        Settings.Register(BelmontUiPlugin.Guid, new LocalizedText("Belmont UI"), menu =>
            menu.Toggle(BelmontUiPlugin.HideMinimapTab, Texts.HideMinimapTab, Texts.HideMinimapTabHint),
            Texts.OwnSummary);

        if (!BelmontUiPlugin.HideMinimapTab.Value)
            return;
        try
        {
            RegisterPage(info.Instance);
            _hide = true;
            var others = OtherProviders();
            BelmontUiPlugin.Logger.LogInfo("[ui] Belmont Minimap: its MODS tab will be hidden, its settings are on the 'Belmont Minimap' page" +
                (others.Count > 0 ? $"; hidden with it: the pages of {string.Join(", ", others)}" : ""));
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] Belmont Minimap's settings can't be shown here, so its MODS tab stays: {e}");
        }
    }

    // Every poll of a pause menu bar that has this library's tab.
    internal static void Apply(ITabHost host)
    {
        if (!_hide || !host.IsPauseMenu || !host.Initialized)
            return;
        foreach (var tab in host.Tabs())
        {
            if (tab.gameObject.name != TabName)
                continue;
            bool changed = false;
            if (tab.IsActive())
            {
                // Not while the player is in the menu, possibly on that very tab.
                if (host.MenuShowing)
                    return;
                host.SetTabActive(tab, false);
                changed = true;
            }
            var toggle = tab.tabToggle;
            if (toggle != null && toggle.gameObject.activeSelf)
            {
                toggle.gameObject.SetActive(false);
                changed = true;
            }
            if (changed && !_logged)
            {
                _logged = true;
                BelmontUiPlugin.Logger.LogInfo($"[ui] Belmont Minimap's MODS tab hidden on '{host.Name}'");
            }
        }
    }

    private static void RegisterPage(object plugin)
    {
        var assembly = plugin.GetType().Assembly;
        var catalog = assembly.GetType("BelmontMinimap.MenuCatalog");
        List<Row> rows = null;
        try
        {
            rows = ReadMenuRows(plugin);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] Belmont Minimap's menu rows can't be read ({e.Message}); building its page from its config entries");
        }

        var summary = Caption(catalog?.GetField("MinimapSummary", Any)?.GetValue(null));
        if (rows == null || rows.Count == 0)
        {
            var config = (plugin as BepInEx.Unity.IL2CPP.BasePlugin)?.Config
                ?? throw new InvalidOperationException("no config file");
            Settings.RegisterFromConfig(Guid, new LocalizedText("Belmont Minimap"), config, null, summary);
            return;
        }

        var groups = (catalog?.GetField("Groups", Any)?.GetValue(null) as IEnumerable)?.Cast<object>().Select(Caption).ToArray()
            ?? Array.Empty<LocalizedText>();
        var keys = rows.Where(r => r.Entry.SettingType == typeof(Key)).Select(r => r.Entry).ToArray();
        Settings.Register(Guid, new LocalizedText("Belmont Minimap"), menu =>
        {
            int group = -1;
            foreach (var row in rows)
            {
                if (row.Group != group)
                {
                    group = row.Group;
                    var title = group >= 0 && group < groups.Length ? groups[group] : null;
                    if (title != null && !title.IsEmpty)
                        menu.Header(title);
                }
                var type = row.Entry.SettingType;
                if (type == typeof(bool))
                {
                    menu.Toggle(row.Entry, row.Label, row.Hint);
                }
                else if (type == typeof(Key))
                {
                    var entry = row.Entry;
                    menu.Binding(entry, row.Label, new BindingOptions
                    {
                        AllowUnbind = true,
                        // That mod's own rule: one key, one of its shortcuts.
                        Validate = key => keys.Any(other => other != entry && other.BoxedValue.ToString() == key) ? Texts.AlreadyAssigned : null,
                    }, Texts.KeyHintUnbind);
                }
                else if (type.IsEnum)
                {
                    string[] values = row.Values ?? Enum.GetNames(type);
                    menu.Choice(row.Entry, row.Label, values, row.Choices != null && row.Choices.Length == values.Length ? row.Choices : null, row.Hint);
                }
                else if (type == typeof(float) || type == typeof(double))
                {
                    menu.Slider(row.Entry, row.Label, row.Min, row.Max, row.Step, "{0:0.##}", row.Hint);
                }
            }
            menu.ResetButton();
        }, summary);
    }

    // Plugin.MenuRows: a list of MenuOption records (Group, Entry, Label, Hint, Min, Max, Step,
    // Choices, Values, IsAvailable), with Caption records (Zh, En) for the texts.
    private static List<Row> ReadMenuRows(object plugin)
    {
        var list = plugin.GetType().GetField("MenuRows", Any)?.GetValue(plugin) as IEnumerable
            ?? throw new InvalidOperationException("no MenuRows");
        var rows = new List<Row>();
        foreach (var option in list)
        {
            if (option == null)
                continue;
            if (Get(option, "IsAvailable") is bool available && !available)
                continue;
            var entry = Get(option, "Entry") as ConfigEntryBase ?? throw new InvalidOperationException("a row without an entry");
            var row = new Row
            {
                Group = Convert.ToInt32(Get(option, "Group")),
                Entry = entry,
                Label = Caption(Get(option, "Label")),
                Hint = Caption(Get(option, "Hint")),
                Min = Convert.ToSingle(Get(option, "Min")),
                Max = Convert.ToSingle(Get(option, "Max")),
                Step = Convert.ToSingle(Get(option, "Step")),
                Choices = (Get(option, "Choices") as IEnumerable)?.Cast<object>().Select(Caption).ToArray(),
                Values = (Get(option, "Values") as IEnumerable)?.Cast<object>().Select(v => v?.ToString() ?? "").ToArray(),
            };
            if (row.Label == null || row.Label.IsEmpty)
                row.Label = new LocalizedText(entry.Definition.Key);
            rows.Add(row);
        }
        return rows;
    }

    private static object Get(object target, string member)
    {
        var type = target.GetType();
        var property = type.GetProperty(member, Any);
        if (property != null)
            return property.GetValue(target);
        return type.GetField(member, Any)?.GetValue(target);
    }

    // That mod's texts come in English and Simplified Chinese.
    private static LocalizedText Caption(object caption)
    {
        if (caption == null)
            return null;
        string en = Get(caption, "En") as string;
        string zh = Get(caption, "Zh") as string;
        if (string.IsNullOrEmpty(en))
            return string.IsNullOrEmpty(zh) ? null : new LocalizedText(zh);
        var text = new LocalizedText(en);
        if (!string.IsNullOrEmpty(zh) && zh != en)
            text["zh-CN"] = zh;
        return text;
    }

    // Mods whose pages live on Belmont Minimap's tab (its provider contract): hidden with it.
    private static List<string> OtherProviders()
    {
        var names = new List<string>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.GetType("BelmontModSettings.ProviderV2") != null || assembly.GetType("BelmontModSettings.ProviderV1") != null)
                    names.Add(assembly.GetName().Name);
            }
            catch
            {
            }
        }
        return names;
    }
}
