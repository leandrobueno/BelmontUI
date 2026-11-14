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

    // Written for BelmontUI: after "Press new button" on a row that can be given B, where a tap
    // of B is the new button. {0} is the controller's icon of that button.
    internal static readonly LocalizedText HoldToCancel = new("Hold {0} to cancel")
    {
        ["de"] = "{0} halten zum Abbrechen",
        ["es"] = "Mantén {0} para cancelar",
        ["fr"] = "Maintenir {0} pour annuler",
        ["it"] = "Tieni premuto {0} per annullare",
        ["ja"] = "{0}長押しでキャンセル",
        ["ko"] = "{0} 길게 눌러 취소",
        ["pt-BR"] = "Segure {0} para cancelar",
        ["ru"] = "Удерживайте {0} для отмены",
        ["zh-CN"] = "长按 {0} 取消",
        ["zh-TW"] = "長按 {0} 取消",
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
    // A Key entry's row that Delete can clear.
    internal static readonly LocalizedText KeyHintUnbind = new("Select, then press a key. Delete clears it.")
    {
        ["de"] = "Auswählen, dann eine Taste drücken. Delete löscht die Belegung.",
        ["es"] = "Selecciona y pulsa una tecla. Delete la borra.",
        ["fr"] = "Sélectionnez, puis appuyez sur une touche. Delete l'efface.",
        ["it"] = "Seleziona, poi premi un tasto. Delete lo cancella.",
        ["ja"] = "選択してからキーを押します。Deleteで解除します。",
        ["ko"] = "선택한 다음 키를 누르세요. Delete로 해제합니다.",
        ["pt-BR"] = "Selecione e aperte uma tecla. Delete a remove.",
        ["ru"] = "Выберите и нажмите клавишу. Delete сбрасывает её.",
        ["zh-CN"] = "选中后按下一个键。按 Delete 可清除。",
        ["zh-TW"] = "選中後按下一個鍵。按 Delete 可清除。",
    };

    // The library's own page, shown only with Belmont Minimap installed (MinimapBridge).
    internal static readonly LocalizedText OwnSummary = new("Options of the MODS tab itself.")
    {
        ["de"] = "Optionen des MODS-Tabs selbst.",
        ["es"] = "Opciones de la propia pestaña MODS.",
        ["fr"] = "Options de l'onglet MODS lui-même.",
        ["it"] = "Opzioni della scheda MODS stessa.",
        ["ja"] = "MODSタブ自体のオプション。",
        ["ko"] = "MODS 메뉴 자체의 옵션입니다.",
        ["pt-BR"] = "Opções da própria aba MODS.",
        ["ru"] = "Параметры самой вкладки MODS.",
        ["zh-CN"] = "MODS 标签页自身的选项。",
        ["zh-TW"] = "MODS 分頁本身的選項。",
    };

    internal static readonly LocalizedText HideMinimapTab = new("Hide Belmont Minimap's MODS tab")
    {
        ["de"] = "MODS-Tab von Belmont Minimap ausblenden",
        ["es"] = "Ocultar la pestaña MODS de Belmont Minimap",
        ["fr"] = "Masquer l'onglet MODS de Belmont Minimap",
        ["it"] = "Nascondi la scheda MODS di Belmont Minimap",
        ["ja"] = "Belmont MinimapのMODSタブを隠す",
        ["ko"] = "Belmont Minimap의 MODS 메뉴 숨기기",
        ["pt-BR"] = "Ocultar a aba MODS do Belmont Minimap",
        ["ru"] = "Скрыть вкладку MODS мода Belmont Minimap",
        ["zh-CN"] = "隐藏 Belmont Minimap 的 MODS 标签页",
        ["zh-TW"] = "隱藏 Belmont Minimap 的 MODS 分頁",
    };

    internal static readonly LocalizedText HideMinimapTabHint = new("Its settings move here, under Belmont Minimap; pages other mods add to its tab are hidden too. Applies at the next game start.")
    {
        ["de"] = "Seine Einstellungen stehen dann hier unter Belmont Minimap; Seiten anderer Mods in seinem Tab werden mit ausgeblendet. Gilt ab dem nächsten Spielstart.",
        ["es"] = "Sus ajustes pasan aquí, a Belmont Minimap; las páginas de otros mods en su pestaña también se ocultan. Se aplica al iniciar el juego de nuevo.",
        ["fr"] = "Ses options passent ici, sous Belmont Minimap ; les pages d'autres mods dans son onglet sont masquées aussi. S'applique au prochain lancement du jeu.",
        ["it"] = "Le sue impostazioni passano qui, sotto Belmont Minimap; anche le pagine di altre mod nella sua scheda vengono nascoste. Si applica al prossimo avvio del gioco.",
        ["ja"] = "その設定はここのBelmont Minimapに移り、他のMODがそのタブに追加したページも隠れます。次回のゲーム起動時に反映されます。",
        ["ko"] = "해당 설정은 여기의 Belmont Minimap으로 옮겨지고, 다른 모드가 그 메뉴에 추가한 페이지도 숨겨집니다. 다음 게임 시작 시 적용됩니다.",
        ["pt-BR"] = "As configurações dele passam para cá, em Belmont Minimap; páginas de outros mods naquela aba também são ocultadas. Vale a partir do próximo início do jogo.",
        ["ru"] = "Его настройки переносятся сюда, в раздел Belmont Minimap; страницы других модов на той вкладке тоже скрываются. Действует со следующего запуска игры.",
        ["zh-CN"] = "其设置将移到这里的 Belmont Minimap 中,其他模组加入该标签页的页面也会隐藏。下次启动游戏时生效。",
        ["zh-TW"] = "其設定將移到這裡的 Belmont Minimap 中，其他模組新增到該分頁的頁面也會隱藏。下次啟動遊戲時生效。",
    };

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
