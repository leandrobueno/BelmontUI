using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace BelmontUi;

/// <summary>
/// One mod's settings on the Mods Settings tab. The tab lists every registered mod (its
/// <see cref="Title"/>, its <see cref="Summary"/> and a Settings button); the button opens this
/// menu's rows as a sub-menu, in the order they were added.
/// </summary>
/// <remarks>
/// You get one from <see cref="Settings.Register"/> or <see cref="Settings.RegisterFromConfig"/>
/// and add rows with the builder methods (<see cref="Toggle"/>, <see cref="Slider"/>, ...), which
/// return the menu so calls can be chained. Each row is bound to a BepInEx config entry: when the
/// player changes the control, the new value is written to the entry at once, and the entry's config
/// file is saved when the player leaves the sub-menu or closes the menu.
/// <para>
/// Labels, hints, summaries and button captions are plain strings shown as given; the library
/// translates nothing. A mod that wants its menu in the player's language passes its own translations.
/// </para>
/// </remarks>
public sealed class SettingsMenu
{
    internal readonly List<SettingRow> RowList = new();
    private string _title;
    private string _summary;

    /// <summary>The mod's plugin GUID, as passed to <see cref="Settings.Register"/>.</summary>
    public string Id { get; }

    /// <summary>
    /// The mod's name: on its row of the list and at the top of its sub-menu. Can be changed at any
    /// time; the tab picks the new text up on its next refresh. Empty or null falls back to <see cref="Id"/>.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = string.IsNullOrEmpty(value) ? Id : value;
            Settings.Revision++;
        }
    }

    /// <summary>
    /// One line describing the mod, shown in the middle of its row on the list (optional; null or
    /// empty shows nothing). Can be changed at any time.
    /// </summary>
    public string Summary
    {
        get => _summary;
        set
        {
            _summary = value;
            Settings.Revision++;
        }
    }

    // The rows in the order they were added; read by the tab when it builds.
    internal IReadOnlyList<SettingRow> Rows => RowList.AsReadOnly();

    internal SettingsMenu(string id, string title, string summary)
    {
        Id = id;
        _title = string.IsNullOrEmpty(title) ? id : title;
        _summary = summary;
    }

    // Every builder method ends here: the row is checked (an ArgumentException names the row and
    // what is missing or of the wrong type), stored, and the tab rebuilds on its next poll.
    internal SettingsMenu Add(SettingRow row)
    {
        if (row == null)
            throw new ArgumentNullException(nameof(row));
        row.Validate();
        RowList.Add(row);
        Settings.Revision++;
        return this;
    }

    /// <summary>Adds an On / Off row, the game's checkbox row.</summary>
    /// <param name="entry">A <c>bool</c> config entry; checked means <c>true</c>.</param>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="hint">A longer description. The game's checkbox row has no place to show it, so it is kept for future use.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, or the entry is null or not a <c>bool</c>.</exception>
    public SettingsMenu Toggle(ConfigEntryBase entry, string label, string hint = null) =>
        Add(new SettingRow { Kind = RowKind.Toggle, BindsTo = entry, Label = label, Hint = hint });

    /// <summary>Adds a slider for a decimal number, the game's slider row with the value shown next to it.</summary>
    /// <param name="entry">A <c>float</c> or <c>double</c> config entry.</param>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="min">The lowest value the slider can reach.</param>
    /// <param name="max">The highest value; must be greater than <paramref name="min"/>.</param>
    /// <param name="step">
    /// The increment of one Left / Right press, and the grid values are rounded to (from
    /// <paramref name="min"/>), so the config file gets 0.75 and not 0.7500001. 0 lets the slider move freely.
    /// </param>
    /// <param name="format">
    /// A <see cref="string.Format(string, object)"/> pattern for the value shown next to the slider,
    /// with the value as <c>{0}</c>: <c>"{0:0%}"</c> shows 0.75 as "75%", <c>"{0:0.00}x"</c> as "0.75x",
    /// <c>"{0:0}"</c> as "1".
    /// </param>
    /// <param name="hint">A longer description. The game's slider row has no place to show it, so it is kept for future use.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a float / double, or <paramref name="max"/> is not greater than <paramref name="min"/>.</exception>
    public SettingsMenu Slider(ConfigEntryBase entry, string label, float min, float max, float step, string format = "{0:0.##}",
        string hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Slider, BindsTo = entry, Label = label, Hint = hint,
            Min = min, Max = max, Step = step, Format = format,
        });

    /// <summary>Adds a slider for a whole number.</summary>
    /// <param name="entry">An <c>int</c> config entry (<c>long</c>, <c>short</c> and <c>byte</c> work too).</param>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value; must be greater than <paramref name="min"/>.</param>
    /// <param name="step">The increment of one Left / Right press; at least 1.</param>
    /// <param name="hint">A longer description. The game's slider row has no place to show it, so it is kept for future use.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a whole-number type, or <paramref name="max"/> is not greater than <paramref name="min"/>.</exception>
    public SettingsMenu IntSlider(ConfigEntryBase entry, string label, int min, int max, int step = 1, string hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.IntSlider, BindsTo = entry, Label = label, Hint = hint,
            Min = min, Max = max, Step = step,
        });

    /// <summary>Adds a row that cycles through a list of options with Left / Right, the game's list row.</summary>
    /// <param name="entry">A <c>string</c> or <c>enum</c> config entry.</param>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="values">
    /// What the entry holds for each option, in display order: the exact strings for a string entry,
    /// or the enum member names for an enum entry (<c>"Always"</c>, <c>"WhileHeld"</c>). An entry holding
    /// something not in the list shows the first option until it is changed.
    /// </param>
    /// <param name="labels">
    /// What the player sees for each option, one per value in the same order (<c>"Always"</c>,
    /// <c>"While holding the wheel button"</c>). Null shows the values themselves.
    /// </param>
    /// <param name="hint">A longer description, shown where the game's list row has its description line.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or of another type, <paramref name="values"/> is empty or names a missing enum member, or <paramref name="labels"/> has a different length.</exception>
    public SettingsMenu Choice(ConfigEntryBase entry, string label, string[] values, string[] labels = null, string hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Choice, BindsTo = entry, Label = label, Hint = hint,
            Values = values, Labels = labels,
        });

    /// <summary>Adds a row that picks a keyboard key (or mouse side button) from a list, with Left / Right.</summary>
    /// <param name="entry">A <c>string</c> config entry holding the key's name.</param>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="keys">
    /// The keys the player can pick from, as Input System <c>Key</c> names (<c>"F3"</c>, <c>"Digit1"</c>,
    /// <c>"Q"</c>), plus <c>"Mouse4"</c> and <c>"Mouse5"</c> for the mouse side buttons. The row shows them
    /// as "F3", "1", "Q", "Mouse 4". Offer only keys the game and your mod don't already use there.
    /// </param>
    /// <param name="hint">A longer description, shown where the game's list row has its description line.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a string, or <paramref name="keys"/> is empty.</exception>
    public SettingsMenu Key(ConfigEntryBase entry, string label, string[] keys, string hint = null) =>
        Add(new SettingRow { Kind = RowKind.Key, BindsTo = entry, Label = label, Hint = hint, Keys = keys });

    /// <summary>
    /// Adds a row with a button in the game's style: the label on the left, a copy of the pause menu's
    /// System-tab button where the other rows have their control.
    /// </summary>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="onPress">
    /// What pressing it does (A, Enter or a click). It runs at once, on Unity's main thread; the rows
    /// re-read their entries afterwards, so values it changes show immediately.
    /// </param>
    /// <param name="hint">A longer description, kept for future use.</param>
    /// <param name="text">The caption on the button; null uses <paramref name="label"/>.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <remarks>On the title screen, before the pause menu has been opened once, there is no button to copy yet; the row then shows a checkbox and works the same.</remarks>
    /// <exception cref="ArgumentException">The label is empty or <paramref name="onPress"/> is null.</exception>
    public SettingsMenu Button(string label, Action onPress, string hint = null, string text = null) =>
        Add(new SettingRow { Kind = RowKind.Button, Label = label, Hint = hint, OnPress = onPress, ButtonText = text });

    /// <summary>
    /// Adds a "Restore defaults" row with a "Reset" button: every entry of this menu goes back to its
    /// default value and is saved (<see cref="ResetAll"/>), with the Settings screen's reset sound.
    /// </summary>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="hint">A longer description, kept for future use.</param>
    /// <param name="text">The caption on the button.</param>
    /// <returns>This menu, for chaining.</returns>
    public SettingsMenu ResetButton(string label = "Restore defaults", string hint = "Every setting of this mod back to its default.", string text = "Reset") =>
        Add(new SettingRow { Kind = RowKind.Button, Label = label, Hint = hint, OnPress = ResetAll, ButtonText = text, Sound = ButtonSound.Reset });

    /// <summary>
    /// Puts every entry bound to this menu's rows back to its default value (the one given to
    /// <c>Config.Bind</c>) and saves the config files. The tab shows the new values at once.
    /// </summary>
    public void ResetAll()
    {
        foreach (var row in RowList)
        {
            if (row.IsSetting)
                row.Entry.BoxedValue = row.Entry.DefaultValue;
        }
        Save();
        BelmontUiPlugin.Logger.LogInfo($"[ui] {Id}: defaults restored");
    }

    /// <summary>
    /// Saves the config files of the entries bound to this menu's rows. The tab already saves when the
    /// player leaves the sub-menu or closes the menu; call this to save at another moment.
    /// </summary>
    public void Save()
    {
        foreach (var file in RowList.Where(r => r.IsSetting).Select(r => r.Entry.ConfigFile).Distinct())
            file?.Save();
    }

    internal static string KeyName(string name) =>
        name == null ? ""
        : name.StartsWith("Digit", StringComparison.Ordinal) ? name.Substring(5)
        : name.StartsWith("Mouse", StringComparison.Ordinal) ? "Mouse " + name.Substring(5)
        : name;
}
