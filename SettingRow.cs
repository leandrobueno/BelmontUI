using System;
using System.Linq;
using BepInEx.Configuration;

namespace BelmontUi;

// What a row shows and changes.
internal enum RowKind
{
    Toggle,      // a bool entry: the game's checkbox row
    Slider,      // a float (or double) entry: the game's slider row
    IntSlider,   // an int entry: a whole-number slider
    Choice,      // a string or enum entry: the game's list row (Left / Right)
    Key,         // a string entry holding an Input System key name, as a list of keys
    Button,      // an action: a row with a button in the game's style
}

internal enum ButtonSound
{
    Apply,
    Reset,
    None,
}

// One line of a mod's settings menu, as the builder methods of SettingsMenu declare it. The
// MODS tab reads it when it builds (and rebuilds after a menu changes), and writes the
// chosen value straight to BindsTo. Internal: mods use the builder methods.
internal sealed class SettingRow
{
    // The texts in every language the mod gave; Label, Hint, Labels and ButtonText are them in
    // the game's current language, read when the tab builds and again on a language change.
    public LocalizedText LabelText { get; set; }
    public LocalizedText HintText { get; set; }
    public RowKind Kind { get; set; }

    // The BepInEx config entry the row changes. Not used by Button rows.
    public ConfigEntryBase BindsTo { get; set; }

    // Slider and IntSlider: range and increment (Slider: 0 = continuous; IntSlider: at least 1),
    // and a string.Format pattern for the value shown next to the slider ("{0:0%}", "{0:0.00}x").
    public float Min { get; set; }
    public float Max { get; set; }
    public float Step { get; set; }
    public string Format { get; set; }

    // Choice: what the entry holds (a string, or an enum member's name), and what the player sees
    // for each (the values themselves when not given).
    public string[] Values { get; set; }
    public LocalizedText[] LabelTexts { get; set; }

    // Key: Input System Key names ("F3", "Digit1", "Q"; also Mouse4 and Mouse5).
    public string[] Keys { get; set; }

    // Button: what pressing it does, and the caption on the button (the label when not given).
    public Action OnPress { get; set; }
    public LocalizedText ButtonLabel { get; set; }

    internal ButtonSound Sound = ButtonSound.Apply;

    public SettingRow() { }

    public SettingRow(LocalizedText label, RowKind kind, ConfigEntryBase bindsTo = null)
    {
        LabelText = label;
        Kind = kind;
        BindsTo = bindsTo;
    }

    // ---- for the tab ----

    internal string Label => LocalizedText.Show(LabelText);
    internal string Hint => LocalizedText.Show(HintText);
    internal string[] Labels => LabelTexts?.Select(t => LocalizedText.Show(t) ?? "").ToArray();
    internal string ButtonText => LocalizedText.Show(ButtonLabel);
    internal ConfigEntryBase Entry => BindsTo;
    internal bool IsSetting => Kind != RowKind.Button;
    internal string[] OptionValues => Kind == RowKind.Key ? Keys : Values;
    internal string[] Choices => Kind == RowKind.Key ? Keys.Select(SettingsMenu.KeyName).ToArray() : Labels ?? Values;
    internal string Text => string.IsNullOrEmpty(ButtonText) ? Label : ButtonText;
    internal float EffectiveStep => Kind == RowKind.IntSlider ? Math.Max(1f, MathF.Round(Step)) : Math.Max(0f, Step);
    internal string EffectiveFormat => !string.IsNullOrEmpty(Format) ? Format : Kind == RowKind.IntSlider ? "{0}" : "{0:0.##}";

    // The same rules for the builder methods and for Add: a row that would show wrong or throw
    // in the tab is refused when it is declared, with what is missing.
    internal void Validate()
    {
        if (LabelText == null || LabelText.IsEmpty)
            throw new ArgumentException("A row needs a Label");
        string name = $"Row '{Label}' ({Kind})";
        if (Kind == RowKind.Button)
        {
            if (OnPress == null)
                throw new ArgumentException($"{name} needs OnPress, the action the button runs");
            return;
        }
        if (BindsTo == null)
            throw new ArgumentException($"{name} needs BindsTo, the ConfigEntry it changes");
        var type = BindsTo.SettingType;
        switch (Kind)
        {
            case RowKind.Toggle:
                if (type != typeof(bool))
                    throw new ArgumentException($"{name} needs a bool entry; '{BindsTo.Definition.Key}' holds {type.Name}");
                break;
            case RowKind.Slider:
                if (type != typeof(float) && type != typeof(double) && type != typeof(decimal))
                    throw new ArgumentException($"{name} needs a float or double entry; '{BindsTo.Definition.Key}' holds {type.Name} (use IntSlider for whole numbers)");
                if (!(Max > Min))
                    throw new ArgumentException($"{name} needs Max greater than Min ({Min} .. {Max})");
                break;
            case RowKind.IntSlider:
                if (type != typeof(int) && type != typeof(long) && type != typeof(short) && type != typeof(byte))
                    throw new ArgumentException($"{name} needs an int entry; '{BindsTo.Definition.Key}' holds {type.Name}");
                if (!(Max > Min))
                    throw new ArgumentException($"{name} needs Max greater than Min ({Min} .. {Max})");
                break;
            case RowKind.Choice:
                if (Values == null || Values.Length == 0)
                    throw new ArgumentException($"{name} needs Values, at least one");
                if (LabelTexts != null && LabelTexts.Length != Values.Length)
                    throw new ArgumentException($"{name} needs one Label per Value ({LabelTexts.Length} labels, {Values.Length} values)");
                if (type.IsEnum)
                {
                    var missing = Values.Where(v => !Enum.IsDefined(type, v)).ToArray();
                    if (missing.Length > 0)
                        throw new ArgumentException($"{name}: {type.Name} has no member {string.Join(", ", missing)}");
                }
                else if (type != typeof(string))
                {
                    throw new ArgumentException($"{name} needs a string or enum entry; '{BindsTo.Definition.Key}' holds {type.Name}");
                }
                break;
            case RowKind.Key:
                if (Keys == null || Keys.Length == 0)
                    throw new ArgumentException($"{name} needs Keys, at least one key name");
                if (type != typeof(string))
                    throw new ArgumentException($"{name} needs a string entry; '{BindsTo.Definition.Key}' holds {type.Name}");
                break;
            default:
                throw new ArgumentException($"{name}: unknown kind");
        }
    }

    internal bool GetBool() => Entry.BoxedValue is bool b && b;

    internal void SetBool(bool value) => Entry.BoxedValue = value;

    internal float GetNumber() => Convert.ToSingle(Entry.BoxedValue);

    // On the step grid and rounded, so the config file doesn't get 0.70000005.
    internal void SetNumber(float value)
    {
        float step = EffectiveStep;
        value = Math.Clamp(value, Min, Max);
        if (step > 0f)
            value = Min + MathF.Round((value - Min) / step) * step;
        value = Math.Clamp((float)Math.Round(value, 4), Min, Max);
        IConvertible number = Kind == RowKind.IntSlider ? (int)MathF.Round(value) : value;
        Entry.BoxedValue = Convert.ChangeType(number, Entry.SettingType);
    }

    // Index into the option values of the entry's current value, or 0 when it holds something else.
    internal int GetChoice()
    {
        int index = Array.IndexOf(OptionValues, Entry.BoxedValue?.ToString() ?? "");
        return index < 0 ? 0 : index;
    }

    // String entries hold the value itself; enum entries hold the enum member of that name.
    internal void SetChoice(int index)
    {
        var values = OptionValues;
        if (values.Length == 0)
            return;
        string value = values[Math.Clamp(index, 0, values.Length - 1)];
        Entry.BoxedValue = Entry.SettingType.IsEnum ? Enum.Parse(Entry.SettingType, value) : value;
    }
}
