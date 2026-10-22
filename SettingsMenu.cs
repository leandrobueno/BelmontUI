using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace BelmontUi;

/// <summary>
/// One mod's settings on the MODS tab. The tab lists every registered mod (its
/// <see cref="Title"/>, its <see cref="Summary"/> and a Settings button); the button opens this
/// menu's rows as a sub-menu, in the order they were added.
/// </summary>
/// <remarks>
/// You get one from <see cref="Settings.Register(string, string, Action{SettingsMenu}, string)"/> or <see cref="Settings.RegisterFromConfig(string, string, ConfigFile, Action{SettingsMenu}, string)"/>
/// and add rows with the builder methods (<see cref="Toggle(ConfigEntryBase, string, string)"/>, <see cref="Slider(ConfigEntryBase, string, float, float, float, string, string)"/>, ...), which
/// return the menu so calls can be chained. Each row is bound to a BepInEx config entry: when the
/// player changes the control, the new value is written to the entry at once, and the entry's config
/// file is saved when the player leaves the sub-menu or closes the menu.
/// <para>
/// Every text can be a plain <c>string</c>, shown as given in every language, or a
/// <see cref="LocalizedText"/> with one version per language: each builder method has an overload
/// for that, and <see cref="LocalizedTitle"/> / <see cref="LocalizedSummary"/> do it for the title and
/// summary. The tab shows the version for the game's current language and rewrites it when the player
/// changes language. The library's own texts (Settings, Back, the reset row) come in all of the game's
/// languages.
/// </para>
/// </remarks>
public sealed class SettingsMenu
{
    internal readonly List<SettingRow> RowList = new();
    private LocalizedText _title;
    private LocalizedText _summary;

    /// <summary>The mod's plugin GUID, as passed to <see cref="Settings.Register(string, string, Action{SettingsMenu}, string)"/>.</summary>
    public string Id { get; }

    /// <summary>
    /// The mod's name: on its row of the list and at the top of its sub-menu. Can be changed at any
    /// time; the tab picks the new text up on its next refresh. Empty or null falls back to <see cref="Id"/>.
    /// Reading it gives the name in the game's current language when it was set as a
    /// <see cref="LocalizedTitle"/>; setting it gives every language the same name.
    /// </summary>
    public string Title
    {
        get => _title.Current;
        set => LocalizedTitle = value;
    }

    /// <summary>
    /// The mod's name in every language it was given in (see <see cref="Title"/>). Null or empty
    /// falls back to <see cref="Id"/>.
    /// </summary>
    public LocalizedText LocalizedTitle
    {
        get => _title;
        set
        {
            _title = value == null || value.IsEmpty ? new LocalizedText(Id) : value;
            Settings.Revision++;
        }
    }

    /// <summary>
    /// One line describing the mod, shown in the middle of its row on the list (optional; null or
    /// empty shows nothing). Can be changed at any time. Reading it gives the line in the game's
    /// current language; setting it gives every language the same line.
    /// </summary>
    public string Summary
    {
        get => LocalizedText.Show(_summary);
        set => LocalizedSummary = value;
    }

    /// <summary>The summary in every language it was given in (see <see cref="Summary"/>); null shows nothing.</summary>
    public LocalizedText LocalizedSummary
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

    internal SettingsMenu(string id, LocalizedText title, LocalizedText summary)
    {
        Id = id;
        _title = title == null || title.IsEmpty ? new LocalizedText(id) : title;
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
        Toggle(entry, (LocalizedText)label, hint);

    /// <summary>Adds an On / Off row with its texts in several languages; otherwise the same as <see cref="Toggle(ConfigEntryBase, string, string)"/>.</summary>
    /// <param name="entry">A <c>bool</c> config entry; checked means <c>true</c>.</param>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="hint">A longer description, per language (kept for future use).</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, or the entry is null or not a <c>bool</c>.</exception>
    public SettingsMenu Toggle(ConfigEntryBase entry, LocalizedText label, LocalizedText hint = null) =>
        Add(new SettingRow { Kind = RowKind.Toggle, BindsTo = entry, LabelText = label, HintText = hint });

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
        Slider(entry, (LocalizedText)label, min, max, step, format, hint);

    /// <summary>Adds a decimal slider with its texts in several languages; otherwise the same as <see cref="Slider(ConfigEntryBase, string, float, float, float, string, string)"/>.</summary>
    /// <param name="entry">A <c>float</c> or <c>double</c> config entry.</param>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="min">The lowest value the slider can reach.</param>
    /// <param name="max">The highest value; must be greater than <paramref name="min"/>.</param>
    /// <param name="step">The increment of one Left / Right press; 0 lets the slider move freely.</param>
    /// <param name="format">A <see cref="string.Format(string, object)"/> pattern for the value shown next to the slider, the same in every language.</param>
    /// <param name="hint">A longer description, per language (kept for future use).</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a float / double, or <paramref name="max"/> is not greater than <paramref name="min"/>.</exception>
    public SettingsMenu Slider(ConfigEntryBase entry, LocalizedText label, float min, float max, float step, string format = "{0:0.##}",
        LocalizedText hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Slider, BindsTo = entry, LabelText = label, HintText = hint,
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
        IntSlider(entry, (LocalizedText)label, min, max, step, hint);

    /// <summary>Adds a whole-number slider with its texts in several languages; otherwise the same as <see cref="IntSlider(ConfigEntryBase, string, int, int, int, string)"/>.</summary>
    /// <param name="entry">An <c>int</c> config entry (<c>long</c>, <c>short</c> and <c>byte</c> work too).</param>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value; must be greater than <paramref name="min"/>.</param>
    /// <param name="step">The increment of one Left / Right press; at least 1.</param>
    /// <param name="hint">A longer description, per language (kept for future use).</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a whole-number type, or <paramref name="max"/> is not greater than <paramref name="min"/>.</exception>
    public SettingsMenu IntSlider(ConfigEntryBase entry, LocalizedText label, int min, int max, int step = 1, LocalizedText hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.IntSlider, BindsTo = entry, LabelText = label, HintText = hint,
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
        Choice(entry, (LocalizedText)label, values, labels?.Select(l => (LocalizedText)(l ?? "")).ToArray(), hint);

    /// <summary>Adds a list row with its texts, option names included, in several languages; otherwise the same as <see cref="Choice(ConfigEntryBase, string, string[], string[], string)"/>.</summary>
    /// <param name="entry">A <c>string</c> or <c>enum</c> config entry.</param>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="values">What the entry holds for each option, in display order. Never translated: they are what the config file stores.</param>
    /// <param name="labels">What the player sees for each option, per language, one per value in the same order. Null shows the values themselves.</param>
    /// <param name="hint">A longer description, per language, shown where the game's list row has its description line.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or of another type, <paramref name="values"/> is empty or names a missing enum member, or <paramref name="labels"/> has a different length.</exception>
    public SettingsMenu Choice(ConfigEntryBase entry, LocalizedText label, string[] values, LocalizedText[] labels = null, LocalizedText hint = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Choice, BindsTo = entry, LabelText = label, HintText = hint,
            Values = values, LabelTexts = labels,
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
        Key(entry, (LocalizedText)label, keys, hint);

    /// <summary>Adds a key row with its texts in several languages; otherwise the same as <see cref="Key(ConfigEntryBase, string, string[], string)"/>.</summary>
    /// <param name="entry">A <c>string</c> config entry holding the key's name.</param>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="keys">The keys the player can pick from, as Input System <c>Key</c> names. Key names are not translated.</param>
    /// <param name="hint">A longer description, per language, shown where the game's list row has its description line.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty, the entry is null or not a string, or <paramref name="keys"/> is empty.</exception>
    public SettingsMenu Key(ConfigEntryBase entry, LocalizedText label, string[] keys, LocalizedText hint = null) =>
        Add(new SettingRow { Kind = RowKind.Key, BindsTo = entry, LabelText = label, HintText = hint, Keys = keys });

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
        Button((LocalizedText)label, onPress, hint, text);

    /// <summary>Adds a button row with its texts in several languages; otherwise the same as <see cref="Button(string, Action, string, string)"/>.</summary>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="onPress">What pressing it does. It runs at once, on Unity's main thread.</param>
    /// <param name="hint">A longer description, per language (kept for future use).</param>
    /// <param name="text">The caption on the button, per language; null uses <paramref name="label"/>.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty or <paramref name="onPress"/> is null.</exception>
    public SettingsMenu Button(LocalizedText label, Action onPress, LocalizedText hint = null, LocalizedText text = null) =>
        Add(new SettingRow { Kind = RowKind.Button, LabelText = label, HintText = hint, OnPress = onPress, ButtonLabel = text });

    /// <summary>
    /// Adds a "Restore defaults" row with a "Reset" button: every entry of this menu goes back to its
    /// default value and is saved (<see cref="ResetAll"/>), with the Settings screen's reset sound.
    /// </summary>
    /// <param name="label">The row's name, shown on the left.</param>
    /// <param name="hint">A longer description, kept for future use.</param>
    /// <param name="text">The caption on the button.</param>
    /// <returns>This menu, for chaining.</returns>
    /// <remarks>
    /// Left at their defaults, the three texts come in every language of the game; the row's name and
    /// the button then use the game's own words from its "Default settings" dialog. A text you pass is
    /// shown as given.
    /// </remarks>
    public SettingsMenu ResetButton(string label = DefaultResetLabel, string hint = DefaultResetHint, string text = DefaultResetText) =>
        ResetButton(label == DefaultResetLabel ? Texts.RestoreDefaults : label,
            hint == DefaultResetHint ? Texts.ResetHint : hint,
            text == DefaultResetText ? Texts.Reset : (LocalizedText)text);

    /// <summary>Adds the reset row of <see cref="ResetButton(string, string, string)"/> with your own texts in several languages.</summary>
    /// <param name="label">The row's name, per language.</param>
    /// <param name="hint">A longer description, per language (kept for future use); null uses the library's.</param>
    /// <param name="text">The caption on the button, per language; null uses the library's "Reset".</param>
    /// <returns>This menu, for chaining.</returns>
    /// <exception cref="ArgumentException">The label is empty.</exception>
    public SettingsMenu ResetButton(LocalizedText label, LocalizedText hint = null, LocalizedText text = null) =>
        Add(new SettingRow
        {
            Kind = RowKind.Button, LabelText = label, HintText = hint ?? Texts.ResetHint, OnPress = ResetAll,
            ButtonLabel = text ?? Texts.Reset, Sound = ButtonSound.Reset,
        });

    // The 0.2 defaults. Mods compiled against 0.2 pass these strings themselves (C# puts default
    // values at the call site), so they are recognised and replaced by the translated texts.
    private const string DefaultResetLabel = "Restore defaults";
    private const string DefaultResetHint = "Every setting of this mod back to its default.";
    private const string DefaultResetText = "Reset";

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
