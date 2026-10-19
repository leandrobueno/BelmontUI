using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace BelmontUi;

// One mod's section of the Mods Settings tab: its title and rows. Built through Settings.Register.
//
// Only plain .NET types here (ConfigEntryBase, strings, numbers, delegates), so a consumer's
// surface doesn't move when the interop assemblies are regenerated.
public sealed class SettingsPage
{
    internal readonly List<SettingRow> Rows = new();

    public string Id { get; }
    public string Title { get; set; }

    internal SettingsPage(string id, string title)
    {
        Id = id;
        Title = title ?? id;
    }

    public SettingsPage Toggle(ConfigEntryBase entry, string label, string hint = null) =>
        Add(new SettingRow { Kind = RowKind.Toggle, Entry = entry, Label = label, Hint = hint });

    // A float (or double) entry on a slider. step is the slider's increment; format is a
    // string.Format pattern for the value ("{0:0%}", "{0:0.00}x"), used where the game's slider
    // doesn't show the value itself.
    public SettingsPage Slider(ConfigEntryBase entry, string label, float min, float max, float step, string format = "{0:0.##}",
        string hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Float, Entry = entry, Label = label, Hint = hint,
            Min = min, Max = max, Step = step <= 0f ? 0f : step, Format = format ?? "{0:0.##}",
        });

    // An int entry on a whole-number slider.
    public SettingsPage IntSlider(ConfigEntryBase entry, string label, int min, int max, int step = 1, string hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Int, Entry = entry, Label = label, Hint = hint,
            Min = min, Max = max, Step = step <= 0 ? 1 : step, Format = "{0}",
        });

    // values are what the entry holds (a string, or an enum member's name); labels are what the
    // user sees for each, defaulting to the values themselves.
    public SettingsPage Choice(ConfigEntryBase entry, string label, string[] values, string[] labels = null, string hint = null)
    {
        if (values == null || values.Length == 0)
            throw new ArgumentException("A choice needs at least one value", nameof(values));
        if (labels != null && labels.Length != values.Length)
            throw new ArgumentException("One label per value", nameof(labels));
        return Add(new SettingRow
        {
            Kind = RowKind.Choice, Entry = entry, Label = label, Hint = hint,
            Values = values, Choices = labels ?? values,
        });
    }

    // A key picked from a list of Input System Key names ("F3", "Digit1", "Q"; also Mouse4 and
    // Mouse5), shown as "F3", "1", "Mouse 4".
    public SettingsPage Key(ConfigEntryBase entry, string label, string[] keys, string hint = null)
    {
        if (keys == null || keys.Length == 0)
            throw new ArgumentException("A key setting needs at least one key", nameof(keys));
        return Add(new SettingRow
        {
            Kind = RowKind.Key, Entry = entry, Label = label, Hint = hint,
            Values = keys, Choices = keys.Select(KeyName).ToArray(),
        });
    }

    // A row with a button in the game's style: label on the left, the button where the other
    // rows have their control. text is the caption on the button (the label when not given).
    public SettingsPage Button(string label, Action onPress, string hint = null, string text = null) =>
        Add(new SettingRow { Kind = RowKind.Button, Label = label, Hint = hint, OnPress = onPress, Text = text ?? label });

    // "Restore defaults": every setting of this page back to its default, then saved.
    public SettingsPage ResetButton(string label = "Restore defaults", string hint = "Every setting of this mod back to its default.", string text = "Reset") =>
        Add(new SettingRow { Kind = RowKind.Button, Label = label, Hint = hint, OnPress = ResetAll, Text = text, IsReset = true });

    public void ResetAll()
    {
        foreach (var row in Rows)
        {
            if (row.IsSetting)
                row.Entry.BoxedValue = row.Entry.DefaultValue;
        }
        Save();
        BelmontUiPlugin.Logger.LogInfo($"[ui] {Id}: defaults restored");
    }

    public void Save()
    {
        foreach (var file in Rows.Where(r => r.IsSetting).Select(r => r.Entry.ConfigFile).Distinct())
            file?.Save();
    }

    internal static string KeyName(string name) =>
        name == null ? ""
        : name.StartsWith("Digit", StringComparison.Ordinal) ? name.Substring(5)
        : name.StartsWith("Mouse", StringComparison.Ordinal) ? "Mouse " + name.Substring(5)
        : name;

    private SettingsPage Add(SettingRow row)
    {
        if (row.IsSetting && row.Entry == null)
            throw new ArgumentNullException("entry", $"Setting '{row.Label}' has no config entry");
        if (string.IsNullOrEmpty(row.Label))
            throw new ArgumentException("A row needs a label");
        Rows.Add(row);
        Settings.Revision++;
        return this;
    }
}
