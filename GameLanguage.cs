using System;
using UnityEngine.Localization.Settings;

namespace BelmontUi;

// The game's selected locale (Unity Localization), read on the watcher's poll. On a change every
// tab rewrites its texts in the new language (ModsTab.RefreshLanguage), and
// Settings.LanguageChanged is raised. Read only once the game's localization has finished
// starting, so the poll never makes it load synchronously.
internal static class GameLanguage
{
    internal static string Code { get; private set; } = "en";

    private static bool _logged;

    internal static void Poll()
    {
        string code;
        try
        {
            if (LocalizationSettings.GetInstanceDontCreateDefault() == null || !LocalizationSettings.InitializationOperation.IsDone)
                return;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null)
                return;
            code = locale.Identifier.Code;
        }
        catch (Exception e)
        {
            if (!_logged)
            {
                _logged = true;
                BelmontUiPlugin.Logger.LogWarning($"[ui] game language not readable, texts stay in {Code}: {e.Message}");
            }
            return;
        }
        if (string.IsNullOrEmpty(code) || string.Equals(code, Code, StringComparison.OrdinalIgnoreCase))
            return;
        string before = Code;
        Code = code;
        BelmontUiPlugin.Logger.LogInfo($"[ui] game language: {before} -> {code}");
        Settings.RaiseLanguageChanged(code);
    }
}
