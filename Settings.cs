using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace BelmontUi;

// Entry point: declare a mod's settings once, in Load(), and they appear as a section of the
// Mods Settings tab in the game's Settings menu (pause menu and title screen).
public static class Settings
{
    private static readonly Dictionary<string, SettingsPage> ByGuid = new();
    private static readonly List<SettingsPage> PageList = new();

    // Bumped on every change, so a tab built from an older state rebuilds itself.
    internal static int Revision;

    public static SettingsPage Register(string guid, string title, Action<SettingsPage> build)
    {
        if (string.IsNullOrEmpty(guid))
            throw new ArgumentException("A page needs the mod's GUID", nameof(guid));
        var page = new SettingsPage(guid, title);
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

    // Rows from the config file's own metadata: bool entries become toggles, numbers with an
    // AcceptableValueRange sliders, strings with an AcceptableValueList and enums choices.
    // Labels come from the keys ("HudOpacity" -> "Hud opacity"), hints from the descriptions.
    // Entries that don't fit (free text, numbers without a range) are left out. extra can add
    // rows; a Restore defaults button is added unless extra added a button.
    public static SettingsPage RegisterFromConfig(string guid, string title, ConfigFile config, Action<SettingsPage> extra = null)
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
        });
    }

    public static SettingsPage Get(string guid) =>
        guid != null && ByGuid.TryGetValue(guid, out var page) ? page : null;

    public static void Unregister(string guid)
    {
        if (guid == null || !ByGuid.Remove(guid, out var page))
            return;
        PageList.Remove(page);
        Revision++;
    }

    internal static SettingsPage[] Snapshot() => PageList.ToArray();

    private static void AddFromConfig(SettingsPage page, ConfigEntryBase entry)
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

    private static void Skip(SettingsPage page, ConfigEntryBase entry, string why) =>
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
