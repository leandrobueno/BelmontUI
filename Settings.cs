using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace BelmontUi;

/// <summary>
/// Entry point of the library: declare a mod's settings once, in your plugin's <c>Load()</c>, and they
/// appear on the <b>MODS</b> tab (pause menu top bar; the title screen's Settings screen).
/// </summary>
/// <remarks>
/// The tab lists every registered mod with its title, summary and a Settings button that opens the
/// mod's rows as a sub-menu. Call these methods only from code that runs when BelmontUI is installed
/// (check <c>IL2CPPChainloader.Instance.Plugins.ContainsKey("leandrobueno.belmontui")</c> first and keep
/// every <c>BelmontUi.*</c> reference in a separate class), so your mod still loads without it.
/// </remarks>
public static class Settings
{
    private static readonly Dictionary<string, SettingsMenu> ByGuid = new();
    private static readonly List<SettingsMenu> PageList = new();

    // Bumped on every change, so a tab built from an older state rebuilds itself.
    internal static int Revision;

    /// <summary>Registers a mod's settings menu and fills it with the builder methods.</summary>
    /// <param name="guid">Your plugin's GUID. It identifies the menu: registering the same GUID again replaces the earlier menu.</param>
    /// <param name="title">The mod's name, shown on its row of the list and at the top of its sub-menu. Null or empty uses <paramref name="guid"/>.</param>
    /// <param name="build">
    /// Adds the rows, in display order, through the builder methods of the menu it receives
    /// (<see cref="SettingsMenu.Toggle(ConfigEntryBase, string, string)"/>, <see cref="SettingsMenu.Slider(ConfigEntryBase, string, float, float, float, string, string)"/>, ...). Runs once, right away.
    /// Null registers an empty menu.
    /// </param>
    /// <param name="summary">One line describing the mod, shown in the middle of its row on the list (optional).</param>
    /// <returns>The menu, for later changes (<see cref="SettingsMenu.Title"/>, <see cref="SettingsMenu.Summary"/>, more rows).</returns>
    /// <exception cref="ArgumentException"><paramref name="guid"/> is empty, or a row added by <paramref name="build"/> is invalid (the message names the row).</exception>
    /// <example>
    /// <code>
    /// BelmontUi.Settings.Register(Plugin.Guid, "My Mod", menu =>
    /// {
    ///     menu.Toggle(Plugin.Enabled, "Enabled");
    ///     menu.Slider(Plugin.Opacity, "Opacity", 0.2f, 1f, 0.05f, "{0:0%}");
    ///     menu.ResetButton();
    /// }, summary: "What my mod does, in one line.");
    /// </code>
    /// </example>
    public static SettingsMenu Register(string guid, string title, Action<SettingsMenu> build, string summary = null) =>
        Register(guid, (LocalizedText)title, build, (LocalizedText)summary);

    /// <summary>Registers a mod's settings menu with its title and summary in several languages; otherwise the same as <see cref="Register(string, string, Action{SettingsMenu}, string)"/>.</summary>
    /// <param name="guid">Your plugin's GUID. Registering the same GUID again replaces the earlier menu.</param>
    /// <param name="title">The mod's name, per language. Null or empty uses <paramref name="guid"/>.</param>
    /// <param name="build">Adds the rows, in display order, through the builder methods. Runs once, right away. Null registers an empty menu.</param>
    /// <param name="summary">One line describing the mod, per language (optional).</param>
    /// <returns>The menu, for later changes.</returns>
    /// <exception cref="ArgumentException"><paramref name="guid"/> is empty, or a row added by <paramref name="build"/> is invalid (the message names the row).</exception>
    /// <example>
    /// <code>
    /// BelmontUi.Settings.Register(Plugin.Guid, "My Mod", menu =>
    /// {
    ///     menu.Toggle(Plugin.Enabled, new LocalizedText("Enabled") { ["fr"] = "Activé", ["de"] = "Aktiviert" });
    ///     menu.ResetButton();
    /// }, summary: new LocalizedText("What my mod does.") { ["fr"] = "Ce que fait mon mod." });
    /// </code>
    /// </example>
    public static SettingsMenu Register(string guid, LocalizedText title, Action<SettingsMenu> build, LocalizedText summary = null)
    {
        if (string.IsNullOrEmpty(guid))
            throw new ArgumentException("A menu needs the mod's GUID", nameof(guid));
        var page = new SettingsMenu(guid, title, summary);
        build?.Invoke(page);

        if (ByGuid.TryGetValue(guid, out var old))
            PageList.Remove(old);
        ByGuid[guid] = page;
        PageList.Add(page);
        Revision++;
        BelmontUiPlugin.Logger.LogInfo(
            $"[ui] {guid}: settings page '{page.Title}' registered ({page.Rows.Count(r => r.IsSetting)} settings)");
        return page;
    }

    /// <summary>
    /// Registers a mod's settings menu built from its config file's own metadata, with no row code.
    /// </summary>
    /// <param name="guid">Your plugin's GUID; registering the same GUID again replaces the earlier menu.</param>
    /// <param name="title">The mod's name, shown on the list and at the top of its sub-menu.</param>
    /// <param name="config">Your plugin's <c>Config</c>. Every entry already bound becomes a row, in the file's order:
    /// <c>bool</c> a toggle; a number with an <c>AcceptableValueRange</c> a slider; a string with an
    /// <c>AcceptableValueList</c>, or an enum, a list row. Free text and numbers without a range are left
    /// out (logged at debug level). Labels come from the keys (<c>HudOpacity</c> becomes "Hud opacity"),
    /// hints from the descriptions.</param>
    /// <param name="extra">Optional: adds more rows after the generated ones, with the builder methods.</param>
    /// <param name="summary">One line describing the mod, shown on its row of the list (optional).</param>
    /// <returns>The menu.</returns>
    /// <remarks>A "Restore defaults" row is added at the end unless <paramref name="extra"/> added a button row of its own.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
    public static SettingsMenu RegisterFromConfig(string guid, string title, ConfigFile config, Action<SettingsMenu> extra = null,
        string summary = null) =>
        RegisterFromConfig(guid, (LocalizedText)title, config, extra, (LocalizedText)summary);

    /// <summary>
    /// Registers a menu built from a config file, with the title and summary in several languages;
    /// otherwise the same as <see cref="RegisterFromConfig(string, string, ConfigFile, Action{SettingsMenu}, string)"/>.
    /// The generated rows' labels and hints come from the config file and are not translated; rows added
    /// by <paramref name="extra"/> can be.
    /// </summary>
    /// <param name="guid">Your plugin's GUID; registering the same GUID again replaces the earlier menu.</param>
    /// <param name="title">The mod's name, per language.</param>
    /// <param name="config">Your plugin's <c>Config</c>.</param>
    /// <param name="extra">Optional: adds more rows after the generated ones.</param>
    /// <param name="summary">One line describing the mod, per language (optional).</param>
    /// <returns>The menu.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
    public static SettingsMenu RegisterFromConfig(string guid, LocalizedText title, ConfigFile config, Action<SettingsMenu> extra = null,
        LocalizedText summary = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        return Register(guid, title, page =>
        {
            foreach (var pair in config)
                AddFromConfig(page, pair.Value);
            extra?.Invoke(page);
            if (!page.Rows.Any(r => !r.IsSetting))
                page.ResetButton();
        }, summary);
    }

    /// <summary>The menu registered under a plugin GUID.</summary>
    /// <param name="guid">The plugin GUID passed to <see cref="Register(string, string, Action{SettingsMenu}, string)"/> or <see cref="RegisterFromConfig(string, string, ConfigFile, Action{SettingsMenu}, string)"/>.</param>
    /// <returns>The menu, or null when nothing is registered under that GUID.</returns>
    public static SettingsMenu Get(string guid) =>
        guid != null && ByGuid.TryGetValue(guid, out var page) ? page : null;

    /// <summary>Removes a mod's menu from the tab; nothing happens when the GUID isn't registered.</summary>
    /// <param name="guid">The plugin GUID the menu was registered under.</param>
    public static void Unregister(string guid)
    {
        if (guid == null || !ByGuid.Remove(guid, out var page))
            return;
        PageList.Remove(page);
        Revision++;
    }

    /// <summary>
    /// The game's current language, as the game's locale code (lower case): <c>"en"</c>, <c>"fr"</c>,
    /// <c>"de"</c>, <c>"es"</c>, <c>"it"</c>, <c>"pt-br"</c>, <c>"ru"</c>, <c>"ja"</c>, <c>"ko"</c>,
    /// <c>"zh-cn"</c> or <c>"zh-tw"</c>. <c>"en"</c> until the game's localization has started. Compare it
    /// ignoring case; <see cref="LocalizedText"/> does.
    /// </summary>
    public static string Language => GameLanguage.Code;

    /// <summary>
    /// Raised on Unity's main thread when the player changes the game's language, with the new
    /// locale code. The tab already shows <see cref="LocalizedText"/>s in the new language; use this
    /// for texts your mod builds itself.
    /// </summary>
    public static event Action<string> LanguageChanged;

    // The tabs notice the new language themselves (ModsTab.RefreshLanguage) and rewrite their texts.
    internal static void RaiseLanguageChanged(string code)
    {
        var handlers = LanguageChanged;
        if (handlers == null)
            return;
        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(code);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] a LanguageChanged handler failed: {e}");
            }
        }
    }

    // By title, case-insensitive, as the list shows them.
    internal static SettingsMenu[] Snapshot() =>
        PageList.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase).ToArray();

    private static void AddFromConfig(SettingsMenu page, ConfigEntryBase entry)
    {
        string label = Humanize(entry.Definition.Key);
        string hint = entry.Description?.Description;
        var acceptable = entry.Description?.AcceptableValues;
        var type = entry.SettingType;

        if (type == typeof(bool))
        {
            page.Toggle(entry, label, hint);
        }
        else if (type.IsEnum)
        {
            string[] values = Enum.GetNames(type);
            string[] listed = ListValues(acceptable);
            if (listed != null)
                values = values.Where(listed.Contains).ToArray();
            if (values.Length > 0)
                page.Choice(entry, label, values, values.Select(Humanize).ToArray(), hint);
        }
        else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            if (TryRange(acceptable, out float min, out float max))
                page.IntSlider(entry, label, (int)min, (int)max, Math.Max(1, (int)((max - min) / 100f)), hint);
            else
                Skip(page, entry, "no AcceptableValueRange");
        }
        else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            if (TryRange(acceptable, out float min, out float max))
                page.Slider(entry, label, min, max, StepFor(max - min), "{0:0.##}", hint);
            else
                Skip(page, entry, "no AcceptableValueRange");
        }
        else if (type == typeof(string))
        {
            string[] values = ListValues(acceptable);
            if (values != null && values.Length > 0)
                page.Choice(entry, label, values, null, hint);
            else
                Skip(page, entry, "free text");
        }
        else
        {
            Skip(page, entry, type.Name);
        }
    }

    private static void Skip(SettingsMenu page, ConfigEntryBase entry, string why) =>
        BelmontUiPlugin.Logger.LogDebug($"[ui] {page.Id}: {entry.Definition.Section}.{entry.Definition.Key} not on the page ({why})");

    private static bool TryRange(AcceptableValueBase acceptable, out float min, out float max)
    {
        min = max = 0f;
        var type = acceptable?.GetType();
        if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>))
            return false;
        min = Convert.ToSingle(type.GetProperty("MinValue")?.GetValue(acceptable));
        max = Convert.ToSingle(type.GetProperty("MaxValue")?.GetValue(acceptable));
        return max > min;
    }

    private static string[] ListValues(AcceptableValueBase acceptable)
    {
        var type = acceptable?.GetType();
        if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
            return null;
        return (type.GetProperty("AcceptableValues")?.GetValue(acceptable) as Array)?.Cast<object>()
            .Select(v => v?.ToString() ?? "").ToArray();
    }

    // A step that gives 20 to 100 positions over the range, on a round number.
    private static float StepFor(float span)
    {
        float rough = span / 50f;
        float magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(rough)));
        float unit = rough / magnitude;
        return (unit < 2f ? 1f : unit < 5f ? 2f : 5f) * magnitude;
    }

    // "HudOffsetX" -> "Hud offset X", "KeepHomeCard" -> "Keep home card".
    private static string Humanize(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "";
        var text = new StringBuilder();
        for (int i = 0; i < key.Length; i++)
        {
            char c = key[i];
            bool wordStart = i > 0 && char.IsUpper(c) && (char.IsLower(key[i - 1]) ||
                (i + 1 < key.Length && char.IsLower(key[i + 1]) && !char.IsLower(key[i - 1])));
            if (c == '_' || c == ' ')
            {
                text.Append(' ');
                continue;
            }
            if (wordStart)
            {
                text.Append(' ');
                bool singleLetter = i + 1 >= key.Length || !char.IsLower(key[i + 1]);
                text.Append(singleLetter ? c : char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }
        return text.ToString();
    }
}
