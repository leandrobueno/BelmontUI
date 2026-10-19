using System;
using BepInEx.Configuration;

namespace BelmontUi;

internal enum RowKind
{
    Toggle,
    Int,
    Float,
    Choice,
    Key,
    Button,
}

// One line of a mod's settings: a setting, or a button.
internal sealed class SettingRow
{
    internal RowKind Kind;
    internal ConfigEntryBase Entry;
    internal string Label, Hint;
    internal float Min, Max, Step;                  // Int and Float
    internal string Format;                         // Float: a string.Format pattern, for logs and fallbacks
    internal string[] Values, Choices;              // Choice and Key: what the entry holds, what the user sees
    internal Action OnPress;                        // Button
    internal string Text;                           // Button: the caption on the button itself
    internal bool IsReset;                          // Button: restores defaults (plays the reset sound)

    internal bool IsSetting => Kind != RowKind.Button;

    internal bool GetBool() => Entry.BoxedValue is bool b && b;

    internal void SetBool(bool value) => Entry.BoxedValue = value;

    internal float GetNumber() => Convert.ToSingle(Entry.BoxedValue);

    // On the step grid and rounded, so the config file doesn't get 0.70000005.
    internal void SetNumber(float value)
    {
        value = Math.Clamp(value, Min, Max);
        if (Step > 0f)
            value = Min + MathF.Round((value - Min) / Step) * Step;
        value = Math.Clamp((float)Math.Round(value, 4), Min, Max);
        IConvertible number = Kind == RowKind.Int ? (int)MathF.Round(value) : value;
        Entry.BoxedValue = Convert.ChangeType(number, Entry.SettingType);
    }

    // Index into Values of the entry's current value, or 0 when it holds something else.
    internal int GetChoice()
    {
        int index = Array.IndexOf(Values, Entry.BoxedValue?.ToString() ?? "");
        return index < 0 ? 0 : index;
    }

    // String entries hold the value itself; enum entries hold the enum member of that name.
    internal void SetChoice(int index)
    {
        if (Values.Length == 0)
            return;
        string value = Values[Math.Clamp(index, 0, Values.Length - 1)];
        Entry.BoxedValue = Entry.SettingType.IsEnum ? Enum.Parse(Entry.SettingType, value) : value;
    }
}
