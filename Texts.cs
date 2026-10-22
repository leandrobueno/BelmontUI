namespace BelmontUi;

// The library's own texts in the game's eleven languages. Where the game has the same term, the
// text is copied from its string tables (table/key noted), so it reads like the native menus;
// the others were written for BelmontUI. The tab caption, "MODS", is the same in every language.
// Every character is in the game's font for that language (checked against the font atlases).
internal static class Texts
{
    internal const string TabCaption = "MODS";

    // UI_System/System_Settings_Button
    internal static readonly LocalizedText Settings = new("Settings")
    {
        ["de"] = "Einstellungen",
        ["es"] = "Ajustes",
        ["fr"] = "Options",
        ["it"] = "Impostazioni",
        ["ja"] = "設定",
        ["ko"] = "설정",
        ["pt-BR"] = "Configurações",
        ["ru"] = "Настройки",
        ["zh-CN"] = "设置",
        ["zh-TW"] = "設定",
    };

    // UI_Common/Common_Back
    internal static readonly LocalizedText Back = new("Back")
    {
        ["de"] = "Zurück",
        ["es"] = "Atrás",
        ["fr"] = "Retour",
        ["it"] = "Indietro",
        ["ja"] = "戻る",
        ["ko"] = "뒤로",
        ["pt-BR"] = "Voltar",
        ["ru"] = "Назад",
        ["zh-CN"] = "返回",
        ["zh-TW"] = "返回",
    };

    // The reset row's label. English keeps 0.2's "Restore defaults"; the others are
    // UI_Settings/Settings_DefaultSettings_Title, the title of the game's own reset dialog.
    internal static readonly LocalizedText RestoreDefaults = new("Restore defaults")
    {
        ["de"] = "Standard",
        ["es"] = "Ajustes por defecto",
        ["fr"] = "Options par défaut",
        ["it"] = "Impostazioni predefinite",
        ["ja"] = "初期設定",
        ["ko"] = "초기 설정",
        ["pt-BR"] = "Configurações padrão",
        ["ru"] = "Настройки по умолчанию",
        ["zh-CN"] = "默认设置",
        ["zh-TW"] = "初始設定",
    };

    // The reset row's button. English keeps 0.2's "Reset"; the others are
    // UI_Settings/Settings_DefaultSettings_Confirm, the confirm button of that dialog.
    internal static readonly LocalizedText Reset = new("Reset")
    {
        ["de"] = "Zurücksetzen",
        ["es"] = "Restaurar",
        ["fr"] = "Restaurer",
        ["it"] = "Ripristina",
        ["ja"] = "初期設定に戻す",
        ["ko"] = "초기 설정으로 되돌리기",
        ["pt-BR"] = "Restaurar",
        ["ru"] = "Восстановить",
        ["zh-CN"] = "恢复默认设置",
        ["zh-TW"] = "恢復初始設定",
    };

    // Written for BelmontUI (the reset row's hint; not shown by the game's row today).
    internal static readonly LocalizedText ResetHint = new("Every setting of this mod back to its default.")
    {
        ["de"] = "Setzt alle Einstellungen dieses Mods auf ihren Standardwert zurück.",
        ["es"] = "Restablece todos los ajustes de este mod a su valor por defecto.",
        ["fr"] = "Remet toutes les options de ce mod à leur valeur par défaut.",
        ["it"] = "Riporta tutte le impostazioni di questa mod al valore predefinito.",
        ["ja"] = "このMODのすべての設定を初期設定に戻します。",
        ["ko"] = "이 모드의 모든 설정을 초기 설정으로 되돌립니다.",
        ["pt-BR"] = "Restaura todas as configurações deste mod para o padrão.",
        ["ru"] = "Возвращает все настройки этого мода к значениям по умолчанию.",
        ["zh-CN"] = "将此模组的所有设置恢复为默认值。",
        ["zh-TW"] = "將此模組的所有設定恢復為初始值。",
    };

    // Written for BelmontUI: the tab's only line when no mod registered a menu.
    internal static readonly LocalizedText NoMods = new("No mod has registered settings.")
    {
        ["de"] = "Kein Mod hat Einstellungen registriert.",
        ["es"] = "Ningún mod ha registrado ajustes.",
        ["fr"] = "Aucun mod n'a enregistré d'options.",
        ["it"] = "Nessuna mod ha registrato impostazioni.",
        ["ja"] = "設定を登録したMODはありません。",
        ["ko"] = "설정을 등록한 모드가 없습니다.",
        ["pt-BR"] = "Nenhum mod registrou configurações.",
        ["ru"] = "Ни один мод не зарегистрировал настройки.",
        ["zh-CN"] = "没有模组提供设置。",
        ["zh-TW"] = "沒有模組提供設定。",
    };

    // Written for BelmontUI: the two options of an On / Off row shown as a list row (only when
    // the Settings screen has no checkbox row to copy).
    internal static readonly LocalizedText Off = new("Off")
    {
        ["de"] = "Aus",
        ["es"] = "Desactivado",
        ["fr"] = "Désactivé",
        ["it"] = "Disattivato",
        ["ja"] = "オフ",
        ["ko"] = "끄기",
        ["pt-BR"] = "Desativado",
        ["ru"] = "Выкл.",
        ["zh-CN"] = "关闭",
        ["zh-TW"] = "關閉",
    };

    internal static readonly LocalizedText On = new("On")
    {
        ["de"] = "An",
        ["es"] = "Activado",
        ["fr"] = "Activé",
        ["it"] = "Attivato",
        ["ja"] = "オン",
        ["ko"] = "켜기",
        ["pt-BR"] = "Ativado",
        ["ru"] = "Вкл.",
        ["zh-CN"] = "开启",
        ["zh-TW"] = "開啟",
    };
}
