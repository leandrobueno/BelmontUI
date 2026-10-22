using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BelmontUi;

/// <summary>
/// The text format of a button or key binding, as a <see cref="SettingsMenu.Binding(BepInEx.Configuration.ConfigEntryBase, LocalizedText, BindingOptions, LocalizedText)"/>
/// row stores it in a <c>string</c> config entry, and helpers to read, show and test one.
/// </summary>
/// <remarks>
/// <para>
/// A binding is one control, or two joined with <c>"+"</c> for a combination: hold the first, then
/// press the second (<c>"rightShoulder+leftShoulder"</c>). An empty string means unbound.
/// </para>
/// <para>
/// Gamepad controls are Input System control names on <c>Gamepad</c>: <c>buttonNorth</c>,
/// <c>buttonSouth</c>, <c>buttonWest</c>, <c>buttonEast</c>, <c>leftShoulder</c>, <c>rightShoulder</c>,
/// <c>leftTrigger</c>, <c>rightTrigger</c>, <c>leftStickPress</c>, <c>rightStickPress</c>, <c>select</c>,
/// <c>start</c>, <c>dpad/up</c>, <c>dpad/down</c>, <c>dpad/left</c>, <c>dpad/right</c>. Keyboard controls
/// are Input System <c>Key</c> names (<c>Digit1</c>, <c>F3</c>, <c>Q</c>), plus <c>Mouse4</c> and
/// <c>Mouse5</c> for the mouse side buttons (back and forward).
/// </para>
/// <para>The methods that read input use the Input System directly, so they work whatever the game's own input actions are doing; call them from Unity's main thread.</para>
/// </remarks>
public static class ControlBinding
{
    /// <summary>Joins the two controls of a combination: <c>"held+pressed"</c>.</summary>
    public const string Separator = "+";

    /// <summary>The gamepad control names a binding can hold, in the order a capture checks them.</summary>
    public static readonly IReadOnlyList<string> GamepadControls = new[]
    {
        "buttonSouth", "buttonEast", "buttonWest", "buttonNorth",
        "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
        "leftStickPress", "rightStickPress", "select", "start",
        "dpad/up", "dpad/down", "dpad/left", "dpad/right",
    };

    /// <summary>The mouse side buttons, which keyboard bindings can also hold.</summary>
    public static readonly IReadOnlyList<string> MouseControls = new[] { "Mouse4", "Mouse5" };

    /// <summary>Splits a binding into its controls: none for an empty binding, one, or two for a combination (held first).</summary>
    /// <param name="binding">A stored binding, for example <c>"rightShoulder+leftShoulder"</c>; null counts as empty.</param>
    /// <returns>The control names, trimmed; never null.</returns>
    public static string[] Parse(string binding) =>
        string.IsNullOrWhiteSpace(binding)
            ? Array.Empty<string>()
            : binding.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

    /// <summary>Makes a binding from one control, or from two for a combination.</summary>
    /// <param name="held">The control (for a combination, the one held first).</param>
    /// <param name="pressed">For a combination, the control pressed while <paramref name="held"/> is held; null or empty for a single control.</param>
    /// <returns>The binding text, for example <c>"rightShoulder+leftShoulder"</c>.</returns>
    public static string Make(string held, string pressed = null) =>
        string.IsNullOrEmpty(pressed) ? held ?? "" : $"{held}{Separator}{pressed}";

    /// <summary>True for a gamepad control name of <see cref="GamepadControls"/>.</summary>
    /// <param name="control">One control name (not a combination).</param>
    /// <returns>Whether the name is a gamepad control.</returns>
    public static bool IsGamepad(string control) => control != null && GamepadControls.Contains(control);

    /// <summary>True for an Input System <c>Key</c> name or <c>Mouse4</c> / <c>Mouse5</c>.</summary>
    /// <param name="control">One control name (not a combination).</param>
    /// <returns>Whether the name is a keyboard key or mouse side button.</returns>
    public static bool IsKeyboard(string control) =>
        control != null && (MouseControls.Contains(control) || (Enum.TryParse<Key>(control, out var key) && key != Key.None));

    /// <summary>True when every control of the binding is a known gamepad, keyboard or mouse control and there are at most two.</summary>
    /// <param name="binding">A stored binding.</param>
    /// <returns>Whether the binding can be read; an empty binding is valid (unbound).</returns>
    public static bool IsValid(string binding)
    {
        var parts = Parse(binding);
        return parts.Length <= 2 && parts.All(p => IsGamepad(p) || IsKeyboard(p));
    }

    /// <summary>True while every control of the binding is held (on the current gamepad, keyboard and mouse).</summary>
    /// <param name="binding">A stored binding; an empty one is never held.</param>
    /// <returns>Whether the binding is held now.</returns>
    public static bool IsHeld(string binding)
    {
        var parts = Parse(binding);
        return parts.Length > 0 && parts.All(p => Button(p)?.isPressed == true);
    }

    /// <summary>
    /// True on the frame the binding is completed: its control was pressed this frame or, for a
    /// combination, its second control was pressed this frame while the first one is held.
    /// </summary>
    /// <param name="binding">A stored binding; an empty one never fires.</param>
    /// <returns>Whether the binding was pressed this frame.</returns>
    public static bool WasPressedThisFrame(string binding)
    {
        var parts = Parse(binding);
        if (parts.Length == 0)
            return false;
        var last = Button(parts[parts.Length - 1]);
        if (last == null || !last.wasPressedThisFrame)
            return false;
        return parts.Length == 1 || Button(parts[0])?.isPressed == true;
    }

    /// <summary>
    /// The binding as plain text for the current controller style: "RB + LB" or "R1 + L1" for the gamepad
    /// (whichever the game shows), key names for the keyboard ("1", "F3", "Mouse 4"), "-" when unbound.
    /// </summary>
    /// <param name="binding">A stored binding.</param>
    /// <returns>The readable text.</returns>
    public static string Describe(string binding)
    {
        var parts = Parse(binding);
        return parts.Length == 0 ? "-" : string.Join(" + ", parts.Select(p => ControlGlyphs.Name(p)));
    }

    /// <summary>
    /// The binding as rich text for a TextMeshPro label: the game's own button icons for the current
    /// controller (Xbox or PlayStation, chosen like the game's own prompts) joined with " + ", and key
    /// names for the keyboard. Falls back to <see cref="Describe"/> where the game has no icon.
    /// </summary>
    /// <param name="binding">A stored binding.</param>
    /// <returns>Text with TMP sprite tags where icons exist.</returns>
    public static string Display(string binding)
    {
        var parts = Parse(binding);
        return parts.Length == 0 ? "-" : string.Join(" + ", parts.Select(ControlGlyphs.Display));
    }

    // The button control behind one control name on the current devices, or null.
    internal static ButtonControl Button(string control)
    {
        if (string.IsNullOrEmpty(control))
            return null;
        if (control == "Mouse4")
            return Mouse.current?.backButton;
        if (control == "Mouse5")
            return Mouse.current?.forwardButton;
        if (IsGamepad(control))
        {
            var pad = Gamepad.current;
            return pad?.TryGetChildControl(control)?.TryCast<ButtonControl>();
        }
        if (Enum.TryParse<Key>(control, out var key) && key != Key.None)
        {
            var keyboard = Keyboard.current;
            return keyboard != null ? keyboard[key] : null;
        }
        return null;
    }
}
