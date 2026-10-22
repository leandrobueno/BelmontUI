using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Switch;
using UnityEngine.InputSystem.XInput;
using Object = UnityEngine.Object;

namespace BelmontUi;

// How one control of a binding is shown. Gamepad controls use the game's own button icons: TMP's
// default sprite asset (RedWine_GlobalSpriteAsset) falls back to every glyph set, and the game's
// prompts name a glyph as a device prefix plus the Input System control path ("xbox_buttonSouth",
// "ps5_dpad/up"), which is the format bindings are stored in. The prefix is the game's own
// (InputTagTextPreprocessor.GetPrefix) for the device the game last saw used
// (InputDeviceWatcherService), else the current gamepad. Keys show as names: the game's keyboard glyphs cover only a few keys.
internal static class ControlGlyphs
{
    private static InputDeviceWatcherService _watcher;
    private static float _nextLookup;
    private static bool _gamePrefixFailed;

    // "xbox_", "ps5_", "ps4_" or "switch_", for the gamepad the game shows prompts for.
    internal static string Prefix
    {
        get
        {
            try
            {
                var pad = CurrentGamepad();
                return GamePrefix(pad) ?? PrefixOf(pad);
            }
            catch
            {
                return "xbox_";
            }
        }
    }

    internal static bool PlayStation => Prefix.StartsWith("ps", StringComparison.Ordinal);

    // Rich text for one control: the game's icon for a gamepad control, else the name.
    internal static string Display(string control) =>
        ControlBinding.IsGamepad(control) ? $"<sprite name=\"{Prefix}{control}\">" : Name(control);

    // A short name for one control, in the current controller's style.
    internal static string Name(string control)
    {
        if (string.IsNullOrEmpty(control))
            return "";
        if (control == "Mouse4" || control == "Mouse5")
            return "Mouse " + control.Substring(5);
        if (ControlBinding.IsGamepad(control))
            return PlayStation ? PlayStationName(control) : XboxName(control);
        return KeyName(control);
    }

    // Input System Key names as a player reads them: "Digit1" -> "1", "LeftShift" -> "Shift",
    // "Numpad1" -> "Num 1", "PageUp" -> "Page Up", "Semicolon" -> ";".
    private static string KeyName(string key) => key switch
    {
        "LeftShift" => "Shift",
        "LeftCtrl" => "Ctrl",
        "LeftAlt" => "Alt",
        "RightAlt" => "Alt Gr",
        "UpArrow" => "Up",
        "DownArrow" => "Down",
        "LeftArrow" => "Left",
        "RightArrow" => "Right",
        "Backquote" => "`",
        "Minus" => "-",
        "Equals" => "=",
        "LeftBracket" => "[",
        "RightBracket" => "]",
        "Semicolon" => ";",
        "Quote" => "'",
        "Comma" => ",",
        "Period" => ".",
        "Slash" => "/",
        "Backslash" => "\\",
        "NumpadEnter" => "Num Enter",
        "NumpadPlus" => "Num +",
        "NumpadMinus" => "Num -",
        "NumpadMultiply" => "Num *",
        "NumpadDivide" => "Num /",
        "NumpadPeriod" => "Num .",
        _ when key.StartsWith("Digit", StringComparison.Ordinal) => key.Substring(5),
        _ when key.StartsWith("Numpad", StringComparison.Ordinal) => "Num " + key.Substring(6),
        _ => SplitWords(key),
    };

    // "PageUp" -> "Page Up", "RightShift" -> "Right Shift"; single letters and F-keys stay.
    private static string SplitWords(string key)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < key.Length; i++)
        {
            if (i > 0 && char.IsUpper(key[i]) && char.IsLower(key[i - 1]))
                text.Append(' ');
            text.Append(key[i]);
        }
        return text.ToString();
    }

    private static Gamepad CurrentGamepad()
    {
        var watcher = Watcher();
        var device = watcher != null ? watcher.LastUsedDevice : null;
        return device?.TryCast<Gamepad>() ?? Gamepad.current;
    }

    // The game keeps one InputDeviceWatcherService (in the Settings scene); looked up again at
    // most once a second while missing.
    private static InputDeviceWatcherService Watcher()
    {
        if (_watcher != null)
            return _watcher;
        if (Time.unscaledTime < _nextLookup)
            return null;
        _nextLookup = Time.unscaledTime + 1f;
        _watcher = Object.FindObjectOfType<InputDeviceWatcherService>();
        return _watcher;
    }

    // The game's own choice (InputTagTextPreprocessor.GetPrefix, Steam Deck included), for a gamepad
    // binding on this pad; null if it can't be called, and then never tried again.
    private static string GamePrefix(Gamepad pad)
    {
        if (_gamePrefixFailed || pad == null)
            return null;
        try
        {
            var binding = new InputBinding();
            binding.path = "<Gamepad>/buttonSouth";
            string prefix = InputTagTextPreprocessor.GetPrefix(pad, binding);
            return string.IsNullOrEmpty(prefix) ? null : prefix;
        }
        catch (Exception e)
        {
            _gamePrefixFailed = true;
            BelmontUiPlugin.Logger.LogWarning($"[ui] the game's icon prefix is not available, using BelmontUI's own: {e.Message}");
            return null;
        }
    }

    // BelmontUI's copy of that choice: DualSense before DualShock, which it derives from; anything unknown shows Xbox buttons, as in
    // the game (a PlayStation pad through Steam Input appears as XInput).
    private static string PrefixOf(Gamepad pad)
    {
        if (pad == null || pad.TryCast<XInputController>() != null)
            return "xbox_";
        if (pad.TryCast<DualSenseGamepadHID>() != null)
            return "ps5_";
        if (pad.TryCast<DualShockGamepad>() != null)
            return "ps4_";
        if (pad.TryCast<SwitchProControllerHID>() != null)
            return "switch_";
        return "xbox_";
    }

    private static string XboxName(string control) => control switch
    {
        "buttonSouth" => "A",
        "buttonEast" => "B",
        "buttonWest" => "X",
        "buttonNorth" => "Y",
        "leftShoulder" => "LB",
        "rightShoulder" => "RB",
        "leftTrigger" => "LT",
        "rightTrigger" => "RT",
        "leftStickPress" => "LS",
        "rightStickPress" => "RS",
        "select" => "View",
        "start" => "Menu",
        "dpad/up" => "D-pad Up",
        "dpad/down" => "D-pad Down",
        "dpad/left" => "D-pad Left",
        "dpad/right" => "D-pad Right",
        _ => control,
    };

    private static string PlayStationName(string control) => control switch
    {
        "buttonSouth" => "Cross",
        "buttonEast" => "Circle",
        "buttonWest" => "Square",
        "buttonNorth" => "Triangle",
        "leftShoulder" => "L1",
        "rightShoulder" => "R1",
        "leftTrigger" => "L2",
        "rightTrigger" => "R2",
        "leftStickPress" => "L3",
        "rightStickPress" => "R3",
        "select" => "Share",
        "start" => "Options",
        _ => XboxName(control),
    };
}
