using System;
using System.Collections.Generic;

namespace BelmontUi;

/// <summary>Which devices a binding row listens to.</summary>
[Flags]
public enum BindingDevices
{
    /// <summary>Gamepad buttons, triggers, stick presses and the d-pad.</summary>
    Gamepad = 1,

    /// <summary>Keyboard keys and the mouse side buttons (Mouse4, Mouse5).</summary>
    Keyboard = 2,

    /// <summary>Either: the first input the player gives decides.</summary>
    Both = Gamepad | Keyboard,
}

/// <summary>
/// What a binding row (<see cref="SettingsMenu.Binding(BepInEx.Configuration.ConfigEntryBase, LocalizedText, BindingOptions, LocalizedText)"/>)
/// accepts. Every property is optional; a new instance accepts any single control or combination on the gamepad.
/// </summary>
/// <example>
/// <code>
/// menu.Binding(Plugin.WheelButton, "Wheel button", new BindingOptions
/// {
///     Devices = BindingDevices.Gamepad,
///     Reserved = new[] { "start", "select" },
///     Validate = binding => binding.Contains("buttonSouth") ? new LocalizedText("A is the jump button") : null,
/// });
/// </code>
/// </example>
public sealed class BindingOptions
{
    /// <summary>The devices the row listens to. Default: <see cref="BindingDevices.Gamepad"/>.</summary>
    public BindingDevices Devices { get; set; } = BindingDevices.Gamepad;

    /// <summary>Whether holding one control and pressing another stores both as a combination. Default: true.</summary>
    public bool AllowCombinations { get; set; } = true;

    /// <summary>
    /// The only controls the player may pick (names as in <see cref="ControlBinding"/>), each control of a
    /// combination checked on its own. Null or empty allows every control of the chosen devices.
    /// </summary>
    public IReadOnlyCollection<string> Allowed { get; set; }

    /// <summary>
    /// Controls the player may not pick (names as in <see cref="ControlBinding"/>), each control of a
    /// combination checked on its own. Picking one shows <see cref="ReservedReason"/> on the row and
    /// keeps listening.
    /// </summary>
    public IReadOnlyCollection<string> Reserved { get; set; }

    /// <summary>The text shown when a reserved or not allowed control is picked. Null uses the library's "This button can't be used here." in the player's language.</summary>
    public LocalizedText ReservedReason { get; set; }

    /// <summary>
    /// A last check on the complete binding (for example <c>"rightShoulder+leftShoulder"</c>): return null to
    /// accept it, or the reason to show on the row, and the row keeps listening for another input.
    /// Runs on Unity's main thread.
    /// </summary>
    public Func<string, LocalizedText> Validate { get; set; }

    /// <summary>
    /// Seconds without any input after which listening stops and the old binding stays (B, Esc and
    /// Backspace cancel at once). Default: 10.
    /// </summary>
    public float TimeoutSeconds { get; set; } = 10f;
}
