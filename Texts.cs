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

    // UI_Settings/Settings_Control_KeyBindingNew: a binding row while it listens.
    internal static readonly LocalizedText PressButton = new("Press new button")
    {
        ["de"] = "Neue Taste drücken",
        ["es"] = "Pulsa cualquier botón",
        ["fr"] = "Choisir une autre touche",
        ["it"] = "Premi nuovo tasto",
        ["ja"] = "新しいボタンを入力してください",
        ["ko"] = "새롭게 지정할 버튼을 입력해 주세요",
        ["pt-BR"] = "Pressione o novo botão",
        ["ru"] = "Нажмите другую кнопку",
        ["zh-CN"] = "请输入新的按键。",
        ["zh-TW"] = "請輸入新的配置",
    };

    // UI_Settings/Settings_Control_KeyBindingUsed: a binding row's default reason for a reserved control.
    internal static readonly LocalizedText AlreadyAssigned = new("Already assigned")
    {
        ["de"] = "Wird bereits verwendet",
        ["es"] = "Asignación ya en uso",
        ["fr"] = "Déjà utilisée",
        ["it"] = "Già in uso",
        ["ja"] = "重複しています",
        ["ko"] = "중복된 항목입니다",
        ["pt-BR"] = "Atribuição feita",
        ["ru"] = "Уже привязано",
        ["zh-CN"] = "已被占用。",
        ["zh-TW"] = "已重複",
    };

    // Written for BelmontUI: a binding row's default hint.
    internal static readonly LocalizedText BindingHint = new("Select, then press a button. Hold one and press another for a combination.")
    {
        ["de"] = "Auswählen, dann eine Taste drücken. Für eine Kombination eine Taste halten und eine weitere drücken.",
        ["es"] = "Selecciona y pulsa un botón. Para una combinación, mantén uno y pulsa otro.",
        ["fr"] = "Sélectionnez, puis appuyez sur une touche. Pour une combinaison, maintenez-en une et appuyez sur une autre.",
        ["it"] = "Seleziona, poi premi un tasto. Per una combinazione, tieni premuto un tasto e premine un altro.",
        ["ja"] = "選択してボタンを押してください。組み合わせは、ボタンを押したまま別のボタンを押します。",
        ["ko"] = "선택한 후 버튼을 누르세요. 조합은 버튼 하나를 누른 채 다른 버튼을 누르세요.",
        ["pt-BR"] = "Selecione e pressione um botão. Para uma combinação, segure um e pressione outro.",
        ["ru"] = "Выберите и нажмите кнопку. Для сочетания удерживайте одну и нажмите другую.",
        ["zh-CN"] = "选中后按下按键。组合键：按住一个键再按另一个键。",
        ["zh-TW"] = "選擇後按下按鍵。組合鍵：按住一個鍵再按另一個鍵。",
    };

    // Written for BelmontUI: the default hint of a binding row without combinations.
    internal static readonly LocalizedText BindingHintSingle = new("Select, then press a button.")
    {
        ["de"] = "Auswählen, dann eine Taste drücken.",
        ["es"] = "Selecciona y pulsa un botón.",
        ["fr"] = "Sélectionnez, puis appuyez sur une touche.",
        ["it"] = "Seleziona, poi premi un tasto.",
        ["ja"] = "選択してボタンを押してください。",
        ["ko"] = "선택한 후 버튼을 누르세요.",
        ["pt-BR"] = "Selecione e pressione um botão.",
        ["ru"] = "Выберите и нажмите кнопку.",
        ["zh-CN"] = "选中后按下按键。",
        ["zh-TW"] = "選擇後按下按鍵。",
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
