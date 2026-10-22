using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using Doozy.Runtime.UIManager;
using Doozy.Runtime.UIManager.Animators;
using Doozy.Runtime.UIManager.Components;
using Doozy.Runtime.UIManager.Containers;
using FMODUnity;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Utilities.Localization;
using Object = UnityEngine.Object;

namespace BelmontUi;

// BelmontUI's MODS tab in one tab bar (see ITabHost). Its page content is copied from a native
// Settings tab (scroll and navigation) and its rows from native rows (toggle, slider, list
// stepper) whose game binding is stripped and whose control is driven against a BepInEx
// ConfigEntry instead. On the pause menu the page sits in a shell copied from a native pause-menu
// page, so it shows and hides like its neighbours. Values apply at once; config files are saved
// when the tab loses focus or the menu closes.
internal sealed class ModsTab
{
    // The object name stays "ModsSettingsTab": Belmont Minimap's tab is "TabContent_MODS", and the
    // watcher tells the two MODS tabs apart by name.
    private const string TabName = "ModsSettingsTab";
    private const string ContentHolderName = "BelmontUiContent";
    private const float FallbackRowHeight = 60f;
    private const int LongTextLength = 40;
    private const float SummaryLargest = 0.68f;     // description font size, as a part of the label's
    private const float SummarySmallest = 0.52f;
    private const float RowPadding = 16f;           // above and below a two-line description (~10 px at 1080p)
    private const float ColumnLimit = 0.36f;        // descriptions start at most this far in (part of the row)
    private const float ColumnGap = 0.025f;
    private const float SummaryLineSpacing = -15f;   // a little tighter than the font's 1.5-em lines
    private const string TwoLines = "Ag\nAg";
    private const float ToggleDumpDelay = 1f;
    private const float ReleaseSeconds = 0.15f;      // how long a pressed button shows its pressed look
    private const float TopPadding = 0.075f;         // pause menu: room above the page title, of the page height
    private const float BottomPadding = 0.075f;      // pause menu: the rows end this far above the page bottom (frame line)
    private static readonly List<ModsTab> LiveTabs = new();

    // What stays on a pause-menu page root when it becomes our shell: geometry, canvas and the
    // container that the tab bar shows and hides. Everything else is that page's own logic.
    private static readonly HashSet<string> ShellComponents = new()
    {
        "UnityEngine.Transform", "UnityEngine.RectTransform", "UnityEngine.CanvasRenderer", "UnityEngine.CanvasGroup",
        "UnityEngine.Canvas", "UnityEngine.UI.GraphicRaycaster", "UnityEngine.UI.Image", "UnityEngine.UI.RawImage",
        "UnityEngine.UI.LayoutElement", "Doozy.Runtime.UIManager.Containers.UIContainer",
    };

    // Managed delegates handed to Unity events, kept so the GC doesn't collect them.
    private static readonly List<Delegate> KeepAlive = new();
    // Button rows of every live tab: their button follows the row's selection state each frame.
    private static readonly List<(ModsTab Tab, BoundRow Row)> LiveButtons = new();

    // A copy of a native menu button, kept across scenes for the title screen (which has none).
    private static GameObject _cachedButton;

    private sealed class Template
    {
        internal Component Binding;
        internal GameObject Row => Binding.gameObject;
        internal EventReference Sfx;
        // Sibling-index path from the row root to the selectable the native navigation group
        // moves to for this row; null when the group doesn't reference the row.
        internal List<int> NavPath;
    }

    private sealed class Templates
    {
        internal Template Bool, DropBool, Float, Enum;
        internal GameObject Button;                  // a native menu button (System tab), or the cached copy
        internal Rect Control = new(0.80f, 0f, 0.18f, 1f);   // where rows have their control, normalised in the row
        internal EventReference ResetSfx, ApplySfx;  // the Settings screen's own sounds
        internal EventReference OpenSfx, CloseSfx, SubTabSfx;
    }

    private sealed class BoundRow
    {
        internal SettingRow Row;
        internal SettingsMenu Page;
        internal UIToggle Toggle;
        internal UISlider Slider;
        internal UIListStepper Stepper;
        internal UISelectable NavSelectable;   // what Up/Down lands on: the native row's choice
        internal TMP_Text ValueLabel;           // slider rows: written by us, in the row's format
        internal int ValueLabelFrames;          // slider rows: frames shown since the row last appeared
        internal GameObject Template;           // the native row this one was copied from
        internal GameObject Object;
        internal bool Live;
        internal UIButton Button;               // button rows: the copied game button (decorative)
        internal float PressedUntil;            // button rows: pressed look until then
        internal TMP_Text LabelText;            // the row's name label
        internal TMP_Text HintLabel;            // list rows: the description line
        internal TMP_Text[] ButtonTexts;        // button rows: the caption(s) on the copied button
        internal TMP_Text SummaryText;          // list rows: the summary
        internal SettingsMenu Opens;            // list rows: the menu their button opens
        internal RectTransform SummaryRect;     // list rows: the summary text box
        internal float SummaryCenter;
        internal bool SummaryFitted;
        internal float SummarySizeMax;          // description font sizes, from the label's
        internal float SummarySizeMin;
        internal int ShownFrames;               // frames the row has been on screen in a row
        internal float BaseHeight;              // the row's layout height as copied (one line)
        internal float LabelBaseY = float.NaN;  // label: its y when re-anchored on the row's middle
        internal RectTransform ButtonRect;      // button rows: the copied button and its anchor at base height
        internal float ButtonAnchorY;
        internal UISelectable Selectable => NavSelectable ?? (UISelectable)Toggle ?? (UISelectable)Slider ?? Stepper;
    }

    private readonly ITabHost _host;
    private readonly UITab_BS _tab;
    private readonly GameObject _tabObject;
    private readonly UIToggle _toggle;
    private readonly GameObject _toggleObject;
    private readonly UIToggle _referenceToggle;
    private readonly Transform _toggleParent;
    private readonly GameObject _staging;
    private readonly Transform _content;
    private readonly VerticalNavigationGroup _navigation;
    private readonly Templates _templates;
    private readonly List<BoundRow> _rows = new();
    private readonly List<GameObject> _rowObjects = new();
    private readonly float _addedAt;
    private bool _buttonLogged;
    private int _builtRevision = -1;
    private bool _refreshing;
    private bool _toggleWired;
    private bool _toggleLogged;
    private bool _toggleDumped;
    private bool _animatorsOff;
    private int _toggleSyncs;
    private int _toggleUnstable;
    private bool _pageLogged;
    private bool _imagesDumped;
    private bool _shownLogged;
    private int _fittedCount = -1;
    private string _textLanguage = GameLanguage.Code;
    private TMP_Text _emptyHeader;
    private readonly List<(TMP_Text Text, SettingRow Row)> _headers = new();
    private readonly Dictionary<IntPtr, SettingRow> _rowOf = new();     // row object -> its row (VisibleWhen)

    // Binding rows: the row listening for an input, and what it has seen so far.
    private BoundRow _capture;
    private bool _captureArmed;          // every control was up since listening started
    private string _captureHeld;         // the first control pressed, while it is held
    private float _captureLastInput;
    private bool _captureWaitRelease;    // after listening: the menu stays blocked until every control is up
    private float _captureEndedAt;
    private BindingDevices _captureDevices;
    private const float ReleaseWaitSeconds = 2f;
    private readonly List<InputAction> _blockedForCapture = new();
    private string _glyphPrefix;

    internal bool Alive => _host.Alive && _tabObject != null && _toggleObject != null;

    private ModsTab(ITabHost host, UITab_BS tab, GameObject tabObject, UIToggle toggle, GameObject toggleObject, UIToggle referenceToggle,
        Transform toggleParent, GameObject staging, Transform content, VerticalNavigationGroup navigation, Templates templates)
    {
        _host = host;
        _tab = tab;
        _tabObject = tabObject;
        _toggle = toggle;
        _toggleObject = toggleObject;
        _referenceToggle = referenceToggle;
        _toggleParent = toggleParent;
        _staging = staging;
        _content = content;
        _navigation = navigation;
        _templates = templates;
        _addedAt = Time.unscaledTime;
    }

    // Adds the tab to one tab bar, with the page and rows copied from a Settings screen. Null,
    // with the reason logged, when the menus don't look like what this was written against;
    // nothing of theirs is touched in that case.
    internal static ModsTab TryAdd(ITabHost host, SettingsTabView settings, Transform buttonSource)
    {
        GameObject staging = null;
        GameObject tabObject = null;
        GameObject toggleObject = null;
        try
        {
            var hostTabs = host.Tabs();
            if (hostTabs.Count == 0)
                return Fail(host, "the tab bar has no tabs");
            foreach (var existing in hostTabs)
            {
                if (existing.gameObject.name == TabName)
                    return Fail(host, "a BelmontUI MODS tab is already there");
            }

            // The tab bar's last visible tab: its toggle is the pattern for ours, and on the
            // pause menu its page root is the shell ours goes into.
            UITab_BS reference = null;
            foreach (var candidate in hostTabs)
            {
                if (candidate.tabToggle != null && (reference == null || candidate.tabToggle.gameObject.activeInHierarchy))
                    reference = candidate;
            }
            if (reference == null)
                return Fail(host, "no tab has a toggle in the top bar");
            var referenceToggle = reference.tabToggle;

            var settingsTabs = settings.tabs;
            if (settingsTabs == null || settingsTabs.Length == 0)
                return Fail(host, "the Settings screen has no tabs to copy from");
            var pageSource = PickSource(settingsTabs);
            if (pageSource == null)
                return Fail(host, "no native settings tab to copy the page from");
            var templates = FindTemplates(settingsTabs);
            if (templates.Bool == null && templates.DropBool == null)
                return Fail(host, "no toggle row found to copy");
            templates.ResetSfx = settings.resetSingleSettingEvent;
            templates.ApplySfx = settings.applyChangesEvent;
            templates.OpenSfx = settings.openTabViewEvent;
            templates.CloseSfx = settings.closeTabViewEvent;
            templates.SubTabSfx = settings.switchSubTabEvent;
            templates.Button = PickButton(buttonSource);

            // Copies are made under an inactive object, so nothing of theirs runs before the
            // game's components are stripped off them.
            staging = new GameObject("BelmontUiStaging");
            staging.SetActive(false);
            staging.transform.SetParent(host.Transform, false);

            // The page: a copy of the native settings tab without its view component and rows.
            var settingsCopy = Object.Instantiate(pageSource.gameObject, staging.transform);
            settingsCopy.name = TabName;
            tabObject = settingsCopy;
            foreach (var old in settingsCopy.GetComponents<SettingsViewBase>())
                Object.DestroyImmediate(old);

            Transform content = null;
            foreach (var rowObject in RowObjects(settingsCopy))
            {
                content ??= rowObject.transform.parent;
                Object.DestroyImmediate(rowObject);
            }
            if (content == null)
                return Fail(host, $"tab '{pageSource.gameObject.name}' has no setting rows");

            var navigation = settingsCopy.GetComponentInChildren<VerticalNavigationGroup>(true);
            if (navigation == null)
                BelmontUiPlugin.Logger.LogWarning("[ui] MODS tab: no navigation group in the copied page, Up/Down may not work");

            string pageTitle = CaptionOf(pageSource.tabToggle);
            var titleText = FixPageTexts(settingsCopy, pageTitle);

            // On the pause menu the content moves into a shell copied from a native pause-menu
            // page, so the bar's show / hide flow and the page geometry are the native ones.
            if (host.IsPauseMenu)
                tabObject = BuildShell(reference.gameObject, settingsCopy, staging.transform);

            var tab = host.AddTabComponent(tabObject);
            tab.tabContainer = tabObject.GetComponent<UIContainer>() ?? tabObject.GetComponentInChildren<UIContainer>(true);
            tab.subTabs = new Il2CppReferenceArray<UITab_BS>(0);
            tab.onTabFocused ??= new UnityEvent();
            tab.onTabUnfocused ??= new UnityEvent();

            // The top-bar toggle: a copy of the reference tab's, with its own caption.
            toggleObject = Object.Instantiate(referenceToggle.gameObject, staging.transform);
            toggleObject.name = TabName + "Toggle";
            foreach (var localizer in toggleObject.GetComponentsInChildren<LocalizeStringEvent>(true))
                Object.DestroyImmediate(localizer);
            foreach (var text in toggleObject.GetComponentsInChildren<TMP_Text>(true))
                text.text = Texts.TabCaption;
            var toggle = toggleObject.GetComponent<UIToggle>();
            if (toggle == null)
                return Fail(host, "the copied tab toggle has no UIToggle");
            tab.tabToggle = toggle;
            var uiTab = toggle.TryCast<UITab>();
            if (uiTab != null)
                uiTab.targetContainer = tab.tabContainer;

            // Into place, after the native tabs.
            var toggleParent = referenceToggle.transform.parent;
            toggleObject.transform.SetParent(toggleParent, false);
            toggleObject.transform.SetAsLastSibling();
            tabObject.transform.SetParent(reference.transform.parent, false);
            tabObject.transform.SetAsLastSibling();

            SettleToggle(toggle);

            var modsTab = new ModsTab(host, tab, tabObject, toggle, toggleObject, referenceToggle, toggleParent, staging, content, navigation, templates);
            modsTab._titleText = titleText;
            modsTab._listTitle = titleText != null ? titleText.text : null;
            modsTab._upperTitles = IsUpper(pageTitle);
            LiveTabs.Add(modsTab);
            modsTab.Rebuild();
            modsTab.FitToggles();
            modsTab.EnsureRegistered(first: true);

            Listen(tab.onTabFocused, modsTab.OnFocused);
            Listen(tab.onTabUnfocused, modsTab.OnUnfocused);
            Listen(host.OnCloseView, modsTab.OnUnfocused);

            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] MODS tab added to '{host.Name}' (page from '{pageSource.gameObject.name}'" +
                $"{(host.IsPauseMenu ? $" in a shell from '{reference.gameObject.name}'" : "")}, toggle from " +
                $"'{referenceToggle.gameObject.name}' under '{toggleParent.name}', " +
                $"{(host.Initialized ? "after" : "before")} the tab bar initialised)");
            return modsTab;
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogError($"[ui] MODS tab not added to '{host.Name}': {e}");
            SafeDestroy(toggleObject);
            SafeDestroy(tabObject);
            SafeDestroy(staging);
            return null;
        }
    }

    // Off, with the off visuals: the copy keeps whatever state its source was showing, and a
    // value set without callbacks leaves the visuals as they are. So: on without callbacks, off
    // with them (instant), then the selectable state refreshed.
    private static void SettleToggle(UIToggle toggle)
    {
        try
        {
            toggle.InitializeToggle();
            UIToggleExtensions.SetIsOn(toggle, true, false, false);
            UIToggleExtensions.SetIsOn(toggle, false, false, true);
            toggle.RefreshState();
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] toggle settle failed: {e.Message}");
        }
    }

    // Every poll: still registered and last (Belmont Minimap rewrites the pause menu's tab
    // array), in line with the native toggles, rows up to date and settled.
    internal void Tick()
    {
        EnsureRegistered(first: false);
        if (_builtRevision != Settings.Revision)
            Rebuild();
        RefreshLanguage();
        SettleRows();
    }

    // A language change rewrites the texts of the rows already built instead of rebuilding them:
    // rows copied after the game's Settings screen has been used come out wrong (blank
    // checkmarks, slider labels at their minimum), since the native rows they copy are no longer
    // in their initial state. Then every text of the tab is laid out again: the game's texts
    // follow a language through BaseFont, whose fallback fonts it swaps, and a text picks up the
    // new fallback only when it is laid out again (native texts are, as their localizer rewrites
    // them; ours that keep their content, like "MODS" or a mod's English label, would not be).
    private void RefreshLanguage()
    {
        if (_textLanguage == GameLanguage.Code)
            return;
        _textLanguage = GameLanguage.Code;
        foreach (var bound in _rows)
        {
            try
            {
                Retext(bound);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {bound.Page.Id}: '{bound.Row.Label}' not retranslated: {e}");
            }
        }
        try
        {
            if (_emptyHeader != null)
                _emptyHeader.text = Texts.NoMods.Current;
            foreach (var (text, row) in _headers)
            {
                if (text != null)
                    text.text = row.Label;
            }
            if (_titleText != null && _openPage != null)
                _titleText.text = _upperTitles ? _openPage.Title.ToUpperInvariant() : _openPage.Title;
            if (_toggleObject != null)
            {
                foreach (var text in _toggleObject.GetComponentsInChildren<TMP_Text>(true))
                    text.ForceMeshUpdate(true, true);
            }
            if (_tabObject != null)
            {
                foreach (var text in _tabObject.GetComponentsInChildren<TMP_Text>(true))
                    text.ForceMeshUpdate(true, true);
            }
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] text refresh after the language change failed: {e.Message}");
        }
        BelmontUiPlugin.Logger.LogInfo($"[ui] MODS tab: texts in {_textLanguage} ({_rows.Count} rows)");
    }

    private void Retext(BoundRow bound)
    {
        var row = bound.Row;
        SetLabel(bound.LabelText, bound.Opens != null ? bound.Opens.Title : row.Label);
        if (bound.HintLabel != null)
            bound.HintLabel.text = row.Hint ?? "";
        if (bound.ButtonTexts != null)
        {
            foreach (var text in bound.ButtonTexts)
            {
                if (text != null)
                    text.text = row.Text;
            }
        }
        if (bound.SummaryText != null && bound.Opens != null)
        {
            bound.SummaryText.text = bound.Opens.Summary ?? "";
            bound.SummaryFitted = false;
        }
        if (bound.SummaryText != null && bound.Opens == null && bound != _capture)
            bound.SummaryText.text = row.Hint ?? "";
        bound.SummaryFitted = false;
        if (bound.Stepper != null)
        {
            FillOptions(bound.Stepper, row.Kind == RowKind.Toggle || row.IsAction
                ? new[] { Texts.Off.Current, Texts.On.Current }
                : row.Choices);
            Apply(bound);
        }
    }

    // Takes the tab out again (in game, the Settings screen keeps its native tabs).
    internal void Remove()
    {
        try
        {
            var tabs = _host.Tabs().Where(t => t.Pointer != _tab.Pointer).ToList();
            _host.SetTabs(tabs);
            if (_host.Initialized)
                _host.UpdateActiveTabCount();
            UnfitToggles();
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] MODS tab removal from '{_host.Name}' failed: {e}");
        }
        SafeDestroy(_toggleObject);
        SafeDestroy(_tabObject);
        SafeDestroy(_staging);
        BelmontUiPlugin.Logger.LogInfo($"[ui] MODS tab removed from '{_host.Name}'");
    }

    // ---- button rows ----

    // A native menu button to copy: on the pause menu, the System page's (SETTINGS, TUTORIALS,
    // ...), the one with the fewest game scripts on it; a copy is kept for the title screen.
    private static GameObject PickButton(Transform source)
    {
        if (source != null)
        {
            var candidates = new List<(UIButton Button, int Scripts)>();
            foreach (var button in source.GetComponentsInChildren<UIButton>(true))
            {
                if (button.GetComponentInChildren<TMP_Text>(true) == null)
                    continue;
                bool framed = button.GetComponentsInChildren<Image>(true).Any(i => i.sprite != null);
                if (!framed)
                    continue;
                candidates.Add((button, GameScripts(button.gameObject).Count));
            }
            if (candidates.Count > 0)
            {
                var ordered = candidates.OrderBy(c => c.Scripts).ToList();
                var pick = ordered[0].Button;
                BelmontUiPlugin.Logger.LogInfo(
                    $"[ui] button template: '{pick.gameObject.name}' ({string.Join(", ", ordered.Select(c => $"{c.Button.gameObject.name} {c.Scripts} script(s)"))})");
                try
                {
                    if (_cachedButton == null)
                    {
                        var holder = new GameObject("BelmontUiButtonCache");
                        holder.SetActive(false);
                        Object.DontDestroyOnLoad(holder);
                        _cachedButton = Object.Instantiate(pick.gameObject, holder.transform);
                        _cachedButton.name = pick.gameObject.name;
                        CleanButton(_cachedButton);
                    }
                }
                catch (Exception e)
                {
                    BelmontUiPlugin.Logger.LogDebug($"[ui] button cache failed: {e.Message}");
                }
                return pick.gameObject;
            }
            BelmontUiPlugin.Logger.LogWarning("[ui] no framed button on the System page; button rows keep a checkbox");
        }
        if (_cachedButton != null)
            BelmontUiPlugin.Logger.LogInfo($"[ui] button template: the copy of '{_cachedButton.name}' kept from the pause menu");
        else
            BelmontUiPlugin.Logger.LogInfo("[ui] no button template yet (the pause menu hasn't been seen); button rows keep a checkbox here");
        return _cachedButton;
    }

    // Components that are the game's own logic (not Unity, Doozy or TextMesh Pro).
    private static List<Behaviour> GameScripts(GameObject root)
    {
        var scripts = new List<Behaviour>();
        foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null)
                continue;
            string type = behaviour.GetIl2CppType().FullName ?? "";
            if (type.StartsWith("UnityEngine", StringComparison.Ordinal) || type.StartsWith("Doozy", StringComparison.Ordinal) ||
                type.StartsWith("TMPro", StringComparison.Ordinal))
                continue;
            scripts.Add(behaviour);
        }
        return scripts;
    }

    // A copied button that can never act on its own: no game scripts, no localizers, no click
    // listeners, no navigation, no raycasts (the row underneath takes the clicks).
    private static UIButton CleanButton(GameObject copy)
    {
        foreach (var script in GameScripts(copy))
        {
            try
            {
                Object.DestroyImmediate(script);
            }
            catch (Exception e)
            {
                script.enabled = false;
                BelmontUiPlugin.Logger.LogDebug($"[ui] button script '{script.GetIl2CppType().Name}' kept, disabled: {e.Message}");
            }
        }
        foreach (var localizer in copy.GetComponentsInChildren<LocalizeStringEvent>(true))
            Object.DestroyImmediate(localizer);
        var button = copy.GetComponent<UIButton>() ?? copy.GetComponentInChildren<UIButton>(true);
        if (button == null)
            return null;
        Silence(button.onClickEvent);
        Silence(button.onSubmitEvent);
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        return button;
    }

    // The listeners the game saved on the button (open Settings, quit to the menu ...) are
    // switched off; the events are read-only, so they can't simply be replaced.
    private static void Silence(UnityEvent unityEvent)
    {
        if (unityEvent == null)
            return;
        unityEvent.RemoveAllListeners();
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
    }

    // The button goes where the other rows have their control, at its native size (no taller
    // than the row); the row's own checkbox there is switched off.
    private void AddButtonVisual(GameObject rowObject, BoundRow bound)
    {
        var template = _templates.Button;
        if (template == null)
            return;
        var rowRect = rowObject.transform.TryCast<RectTransform>();
        if (rowRect == null)
            return;

        var hidden = new List<string>();
        var region = _templates.Control;
        var rowSize = rowRect.rect.size;
        foreach (var graphic in rowObject.GetComponentsInChildren<Graphic>(true))
        {
            string kind = graphic.GetIl2CppType().Name;
            if (kind.StartsWith("TMP", StringComparison.Ordinal) || kind == "TextMeshProUGUI")
                continue;
            var image = graphic.TryCast<Image>();
            if (image != null && image.sprite != null && image.sprite.name.StartsWith("T_CommonUI_BarSelector", StringComparison.Ordinal))
                continue;
            var rect = graphic.rectTransform;
            if (rowSize.x <= 1f || rect.rect.width > rowSize.x * 0.5f)
                continue;
            var center = (Vector2)rowRect.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            float x = (center.x - rowRect.rect.xMin) / rowSize.x;
            if (x < region.xMin || x > region.xMax || !graphic.enabled)
                continue;
            graphic.enabled = false;
            hidden.Add(graphic.name);
        }

        var copy = Object.Instantiate(template, rowObject.transform);
        copy.name = "BelmontUiButton";
        copy.SetActive(true);
        var button = CleanButton(copy);
        if (button == null)
        {
            Object.DestroyImmediate(copy);
            return;
        }
        bound.ButtonTexts = copy.GetComponentsInChildren<TMP_Text>(true).ToArray();
        foreach (var text in bound.ButtonTexts)
            text.text = bound.Row.Text;

        // Native size, scaled as a whole (frame, ornaments and caption keep their proportions)
        // to half again the height of the row's bar, like the Settings buttons on Belmont
        // Minimap's rows, and no wider than the control region; centred on the bar.
        var templateRect = template.transform.TryCast<RectTransform>();
        var buttonRect = copy.transform.TryCast<RectTransform>();
        var native = templateRect != null && templateRect.rect.width > 1f ? templateRect.rect.size : buttonRect.sizeDelta;
        var (barCenter, barHeight) = RowBar(rowObject, rowRect);
        float targetHeight = barHeight > 1f ? barHeight * 1.5f : rowSize.y * 0.75f;
        float scale = native.y > 1f ? targetHeight / native.y : 1f;
        float maxWidth = rowSize.x * region.width;
        if (native.x * scale > maxWidth && native.x > 1f)
            scale = maxWidth / native.x;
        var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(region.center.x, barCenter);
        bound.ButtonRect = buttonRect;
        bound.ButtonAnchorY = barCenter;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = native;
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.localScale = new Vector3(scale, scale, 1f);
        buttonRect.SetAsLastSibling();
        bound.Button = button;
        if (!_buttonLogged)
        {
            _buttonLogged = true;
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] button '{template.name}': native {native}, row {rowSize}, bar height {barHeight:0.#} at {barCenter:0.###} of the row, " +
                $"scale {scale:0.###} (shown {native * scale}), x {region.center.x:0.###}; hidden under it [{string.Join(", ", hidden)}]");
        }
    }

    // The row's bar (the dark strip behind label and control): its vertical centre as a fraction
    // of the row's height, and its height. (0.5, 0) when the row has none.
    private static (float Center, float Height) RowBar(GameObject rowObject, RectTransform rowRect)
    {
        foreach (var image in rowObject.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || !image.sprite.name.StartsWith("T_CommonUI_BarSelector_Background", StringComparison.Ordinal))
                continue;
            var rect = image.rectTransform;
            var bottom = rowRect.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMin, 0f))).y;
            var top = rowRect.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f))).y;
            if (rowRect.rect.height <= 1f || top <= bottom)
                break;
            return ((((top + bottom) / 2f) - rowRect.rect.yMin) / rowRect.rect.height, top - bottom);
        }
        return (0.5f, 0f);
    }

    // The action runs at once; the row control (toggle or list) is put back a moment later,
    // while the button shows its pressed look.
    private void Press(BoundRow bound)
    {
        var row = bound.Row;
        if (row.Kind == RowKind.Binding)
        {
            StartCapture(bound);
            return;
        }
        bound.PressedUntil = Time.unscaledTime + ReleaseSeconds;
        BelmontUiPlugin.Logger.LogInfo($"[ui] {bound.Page.Id}: '{row.Label}' pressed (selection {SelectionName()})");
        try
        {
            row.OnPress?.Invoke();
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogError($"[ui] {bound.Page.Id}: '{row.Label}' failed: {e}");
        }
        if (row.Sound != ButtonSound.None)
        {
            var sfx = row.Sound == ButtonSound.Reset && !_templates.ResetSfx.IsNull ? _templates.ResetSfx : _templates.ApplySfx;
            if (sfx.IsNull && _templates.Bool != null)
                sfx = _templates.Bool.Sfx;
            Play(sfx);
        }
        try
        {
            bound.Button?.SetState(UISelectionState.Pressed);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] pressed look failed: {e.Message}");
        }
        RefreshValues();
    }

    private void Release(BoundRow bound)
    {
        _refreshing = true;
        try
        {
            if (bound.Toggle != null && bound.Toggle.isOn)
                UIToggleExtensions.SetIsOn(bound.Toggle, false, false, true);
            if (bound.Stepper != null && bound.Stepper.index != 0)
                bound.Stepper.SetIndex(0, true, true);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': release failed: {e.Message}");
        }
        finally
        {
            _refreshing = false;
        }
        SyncButtonState(bound);

        // The controller must keep its place: whatever took the selection during the press,
        // the row gets it back, and the navigation links are rebuilt.
        string before = SelectionName();
        var eventSystem = EventSystem.current;
        var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        var rowSelectable = bound.Selectable;
        if (eventSystem != null && rowSelectable != null && (current == null || !current.activeInHierarchy || !OwnsSelection(current) || IsButtonPart(current)))
            eventSystem.SetSelectedGameObject(rowSelectable.gameObject);
        UpdateNavigation();
        BelmontUiPlugin.Logger.LogInfo($"[ui] {bound.Page.Id}: '{bound.Row.Label}' released (selection {before} -> {SelectionName()})");
    }

    // The button looks highlighted or normal with its row. Never "Selected": the button isn't a
    // navigation target (no links), and a selected look on it is what could hand it the
    // EventSystem's selection, after which Up / Down had nowhere to go.
    private static void SyncButtonState(BoundRow bound)
    {
        var row = (UISelectable)bound.Toggle ?? bound.Stepper;
        if (bound.Button == null || row == null)
            return;
        var state = row.currentUISelectionState;
        if (state == UISelectionState.Pressed || state == UISelectionState.Selected)
            state = UISelectionState.Highlighted;
        if (bound.Button.currentUISelectionState != state)
            bound.Button.SetState(state);
    }

    private static string SelectionName()
    {
        var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return current == null ? "none" : $"'{current.name}'{(current.activeInHierarchy ? "" : " (inactive)")}";
    }

    // The object is one of our rows' selectables (or under one).
    private bool OwnsSelection(GameObject selected)
    {
        foreach (var bound in _rows)
        {
            if (bound.Object != null && IsUnder(selected.transform, bound.Object.transform))
                return true;
        }
        return false;
    }

    private bool IsButtonPart(GameObject selected)
    {
        foreach (var bound in _rows)
        {
            if (bound.Button != null && IsUnder(selected.transform, bound.Button.transform))
                return true;
        }
        return false;
    }

    // ---- scrolling ----

    // The settings page fitted the Settings screen; in the pause menu, with room above the title,
    // its last rows ran past the frame. The rows hang below a zero-height container
    // ('ContentContainer', 3140 x 0), so a mask on it hid everything (0.1.8). Instead the visible
    // band runs from the top of the rows' area down to just above the frame line; rows outside it
    // are hidden by scaling them to zero (the layout keeps its spacing), and the rows' layout
    // object scrolls so the selected row stays inside the band (mouse wheel too).
    private RectTransform _viewport;
    private float _contentBaseY;
    private float _scroll;
    private bool _scrollReady;
    private GameObject _lastSelected;
    private float _bandTop, _bandBottom;                       // in the container's local space
    private readonly Dictionary<IntPtr, Vector3> _hiddenScale = new();

    private void SetUpScrolling()
    {
        try
        {
            var page = _tabObject.transform.TryCast<RectTransform>();
            _viewport = _content.parent != null ? _content.parent.TryCast<RectTransform>() : null;
            var content = _content.TryCast<RectTransform>();
            if (page == null || _viewport == null || content == null)
                return;
            if (_viewport.GetComponent<LayoutGroup>() != null)
            {
                BelmontUiPlugin.Logger.LogWarning($"[ui] no scrolling: '{_viewport.name}' lays out '{content.name}' itself");
                return;
            }

            float limit = page.rect.yMin + page.rect.height * BottomPadding;
            _bandTop = _viewport.rect.yMax;
            _bandBottom = _viewport.InverseTransformPoint(page.TransformPoint(new Vector3(0f, limit, 0f))).y;
            if (_bandBottom >= _bandTop)
            {
                BelmontUiPlugin.Logger.LogWarning($"[ui] no scrolling: the rows start below the frame line ({_bandTop:0} <= {_bandBottom:0})");
                return;
            }
            _contentBaseY = content.anchoredPosition.y;
            _scroll = 0f;
            _scrollReady = true;
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] scrolling: rows under '{_viewport.name}' {_viewport.rect.size}, visible band {_bandBottom:0}..{_bandTop:0} " +
                $"(page {page.rect.yMin:0}..{page.rect.yMax:0}, frame line at {limit:0})");
        }
        catch (Exception e)
        {
            _scrollReady = false;
            BelmontUiPlugin.Logger.LogWarning($"[ui] scrolling not set up: {e.Message}");
        }
    }

    private void SetScroll(float value)
    {
        var content = _content.TryCast<RectTransform>();
        _scroll = Mathf.Max(0f, value);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, _contentBaseY + _scroll);
    }

    // Top and bottom of a row in the container's space, from its layout position and rect (not
    // its scale, which is zero while it is hidden).
    private (float Top, float Bottom) Extent(RectTransform row)
    {
        var content = _content.TryCast<RectTransform>();
        float baseY = content.localPosition.y;
        float scale = content.localScale.y;
        float y = row.localPosition.y;
        return (baseY + scale * (y + row.rect.yMax), baseY + scale * (y + row.rect.yMin));
    }

    // Rows (and headers) fully inside the band show; the others are scaled to zero.
    // Every row clipped to zero gets its scale back, so the next layout pass sees all shown rows at
    // full height (before a mode or visibility change re-clips them).
    private void ResetClip()
    {
        if (_hiddenScale.Count == 0)
            return;
        foreach (var rowObject in _rowObjects)
        {
            var rect = rowObject != null ? rowObject.transform.TryCast<RectTransform>() : null;
            if (rect != null && _hiddenScale.TryGetValue(rect.Pointer, out var scale))
                rect.localScale = scale;
        }
        _hiddenScale.Clear();
    }

    private void ApplyClip()
    {
        if (!_scrollReady)
            return;
        foreach (var rowObject in _rowObjects)
        {
            // Rows switched off have no layout position yet; scaling one to zero would keep it
            // collapsed (the layout counts child scale) once it is shown again.
            var rect = rowObject != null && rowObject.activeSelf ? rowObject.transform.TryCast<RectTransform>() : null;
            if (rect == null)
                continue;
            var (top, bottom) = Extent(rect);
            bool visible = bottom >= _bandBottom - 1f && top <= _bandTop + 1f;
            bool hidden = _hiddenScale.TryGetValue(rect.Pointer, out var scale);
            if (visible && hidden)
            {
                rect.localScale = scale;
                _hiddenScale.Remove(rect.Pointer);
            }
            else if (!visible && !hidden)
            {
                _hiddenScale[rect.Pointer] = rect.localScale;
                rect.localScale = Vector3.zero;
            }
        }
    }

    // Per frame while the tab has the focus: the selection stays off the decorative buttons,
    // the selected row stays in view, the mouse wheel scrolls, rows outside the band hide.
    private void FrameUpdate()
    {
        if (_waitBackRelease && !BackOrConfirmHeld())
        {
            _waitBackRelease = false;
            UnblockBack();
        }
        if (!_tab.IsFocused())
        {
            if (_capture != null)
                EndCapture(null, "the tab lost the focus");
            return;
        }
        CaptureFrame();
        RefreshGlyphs();
        if (_pendingOpen != null)
        {
            var page = _pendingOpen;
            _pendingOpen = null;
            OpenPage(page);
        }
        if (_pendingClose)
        {
            _pendingClose = false;
            ClosePage(silent: false);
        }
        else if (_openPage != null && _capture == null && !_captureWaitRelease && BackPressed())
        {
            ClosePage(silent: false);
        }
        RewriteValueLabels();
        RefreshChanged();
        FitSummaries();
        var eventSystem = EventSystem.current;
        var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (current != null && IsButtonPart(current))
        {
            foreach (var bound in _rows)
            {
                if (bound.Button != null && IsUnder(current.transform, bound.Button.transform) && bound.Selectable != null)
                {
                    eventSystem.SetSelectedGameObject(bound.Selectable.gameObject);
                    BelmontUiPlugin.Logger.LogInfo($"[ui] '{bound.Row.Label}': selection was on its button, given back to the row");
                    current = bound.Selectable.gameObject;
                    break;
                }
            }
        }
        if (!_scrollReady)
            return;

        if (current != null && current != _lastSelected)
        {
            _lastSelected = current;
            ScrollIntoView(current);
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
                ScrollBy(-Mathf.Sign(wheel) * RowStep());
        }
        ApplyClip();
    }

    private void ScrollIntoView(GameObject selected)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var bound = _rows[i];
            if (bound.Object == null || !IsUnder(selected.transform, bound.Object.transform))
                continue;
            if (bound == FirstVisibleRow())
            {
                SetScroll(0f);   // the first row: back to the top
                return;
            }
            var (top, bottom) = Extent(bound.Object.transform.TryCast<RectTransform>());
            if (bottom < _bandBottom)
                SetScroll(_scroll + (_bandBottom - bottom));
            else if (top > _bandTop)
                SetScroll(_scroll - (top - _bandTop));
            return;
        }
    }

    // One row's height, for the mouse wheel.
    private float RowStep()
    {
        foreach (var bound in _rows)
        {
            var rect = bound.Object != null ? bound.Object.transform.TryCast<RectTransform>() : null;
            if (rect != null && rect.rect.height > 1f)
                return rect.rect.height;
        }
        return FallbackRowHeight;
    }

    // Never past the last row (down) or the top (up: SetScroll clamps at 0).
    private void ScrollBy(float delta)
    {
        if (delta > 0f && _rowObjects.Count > 0)
        {
            var last = _rowObjects[_rowObjects.Count - 1]?.transform.TryCast<RectTransform>();
            if (last != null)
            {
                var (_, bottom) = Extent(last);
                delta = Mathf.Min(delta, Mathf.Max(0f, _bandBottom - bottom));
            }
        }
        if (Mathf.Abs(delta) > 0.01f)
            SetScroll(_scroll + delta);
    }

    // ---- the pause-menu shell ----

    // A copy of a native pause-menu page root, emptied, keeping only geometry, canvas and
    // container components, with the settings page's content moved in.
    private static GameObject BuildShell(GameObject nativePage, GameObject settingsCopy, Transform staging)
    {
        var shell = Object.Instantiate(nativePage, staging);
        shell.name = TabName;
        for (int i = shell.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(shell.transform.GetChild(i).gameObject);

        var kept = new List<string>();
        var removed = new List<string>();
        // Two passes: a component can refuse to go while another still requires it.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var component in shell.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                string type = component.GetIl2CppType().FullName;
                if (ShellComponents.Contains(type))
                    continue;
                try
                {
                    Object.DestroyImmediate(component);
                    if (!removed.Contains(type))
                        removed.Add(type);
                }
                catch (Exception e)
                {
                    if (pass == 1)
                        BelmontUiPlugin.Logger.LogWarning($"[ui] shell keeps '{type}': {e.Message}");
                }
            }
        }
        foreach (var component in shell.GetComponents<Component>())
        {
            if (component != null)
                kept.Add(component.GetIl2CppType().Name);
        }

        // The content was laid out inside the Settings page root, which is inset from its parent
        // (300 units each side at 4K); the shell fills the pause menu's page area. The content goes
        // into a holder with the page root's horizontal geometry, so the rows are as wide and as
        // centred as the game's settings rows. Vertically the holder fills the shell: the pause
        // menu's own top padding and row band are applied to it later.
        var holder = new GameObject(ContentHolderName);
        holder.layer = shell.layer;
        var holderRect = holder.AddComponent<RectTransform>();
        holder.transform.SetParent(shell.transform, false);
        var source = settingsCopy.transform.TryCast<RectTransform>();
        if (source != null)
        {
            holderRect.anchorMin = new Vector2(source.anchorMin.x, 0f);
            holderRect.anchorMax = new Vector2(source.anchorMax.x, 1f);
            holderRect.pivot = new Vector2(source.pivot.x, 0.5f);
            holderRect.sizeDelta = new Vector2(source.sizeDelta.x, 0f);
            holderRect.anchoredPosition = new Vector2(source.anchoredPosition.x, 0f);
        }
        else
        {
            holderRect.anchorMin = Vector2.zero;
            holderRect.anchorMax = Vector2.one;
            holderRect.sizeDelta = Vector2.zero;
        }

        var children = new List<Transform>();
        for (int i = 0; i < settingsCopy.transform.childCount; i++)
            children.Add(settingsCopy.transform.GetChild(i));
        foreach (var child in children)
        {
            child.SetParent(holder.transform, false);
            child.SetAsLastSibling();
        }
        Object.DestroyImmediate(settingsCopy);

        BelmontUiPlugin.Logger.LogInfo(
            $"[ui] page shell from '{nativePage.name}': kept [{string.Join(", ", kept)}], removed [{string.Join(", ", removed)}], " +
            $"{children.Count} content object(s) moved in, horizontal inset {holderRect.sizeDelta.x:0} " +
            $"(anchors x {holderRect.anchorMin.x:0.##}-{holderRect.anchorMax.x:0.##})");
        return shell;
    }

    // ---- registration in the tab bar ----

    private void EnsureRegistered(bool first)
    {
        try
        {
            var tabs = _host.Tabs();
            int index = tabs.FindIndex(t => t.Pointer == _tab.Pointer);
            bool listed = index >= 0;
            bool last = listed && index == tabs.Count - 1;
            if (!last)
            {
                // Always the last entry, never in front of another mod's tab: appended when
                // missing, moved back to the end when another mod added a tab after ours.
                string after = tabs.Count > 0 ? tabs[tabs.Count - 1].gameObject.name : "";
                if (listed)
                {
                    tabs.RemoveAt(index);
                    after = tabs[tabs.Count - 1].gameObject.name;
                }
                tabs.Add(_tab);
                _host.SetTabs(tabs);
                if (_host.Initialized)
                {
                    // The bar already set its tabs up, so this one is set up by hand: its index,
                    // and its toggle switching to it.
                    _tab.Initialize(_host.View, null, tabs.Count - 1);
                    _tab.SetActive(true);
                    if (!_toggleWired)
                    {
                        var host = _host;
                        var tab = _tab;
                        Listen(_toggle.OnValueChangedCallback, (bool on) =>
                        {
                            if (on)
                                host.GoTo(tab);
                        });
                        _toggleWired = true;
                    }
                    _host.UpdateActiveTabCount();
                }
                else
                {
                    // The bar's own initialisation will bind the toggle with the others.
                    _toggleWired = true;
                }
                if (!first && !listed)
                    BelmontUiPlugin.Logger.LogInfo($"[ui] MODS tab put back into '{_host.Name}' (another mod rewrote its tabs)");
                else if (listed)
                    BelmontUiPlugin.Logger.LogDebug($"[ui] tab moved to the end of '{_host.Name}' (after '{after}')");
            }

            // Last in the toggle row and in the toggle group as well.
            if (_toggleObject.transform.parent != _toggleParent)
                _toggleObject.transform.SetParent(_toggleParent, false);
            if (_toggleObject.transform.GetSiblingIndex() != _toggleParent.childCount - 1)
            {
                string after = _toggleParent.GetChild(_toggleParent.childCount - 1).name;
                _toggleObject.transform.SetAsLastSibling();
                BelmontUiPlugin.Logger.LogDebug($"[ui] tab toggle moved to the end of '{_toggleParent.name}' (after '{after}')");
            }
            var group = NativeGroup();
            if (group != null)
            {
                if (!_toggle.inToggleGroup)
                {
                    _toggle.AddToToggleGroup(group);
                }
                else
                {
                    var toggles = group.toggles;
                    if (toggles != null && toggles.Count > 0 && toggles[toggles.Count - 1].Pointer != _toggle.Pointer)
                    {
                        _toggle.RemoveFromToggleGroup();
                        _toggle.AddToToggleGroup(group);
                        BelmontUiPlugin.Logger.LogDebug($"[ui] tab toggle moved to the end of the toggle group of '{_host.Name}'");
                    }
                }
            }

            if (NativeToggles().Count != _fittedCount)
                FitToggles();
            SyncToggleGeometry();
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogError($"[ui] MODS tab check on '{_host.Name}' failed: {e}");
        }
    }

    private UIToggleGroup NativeGroup()
    {
        foreach (var rect in NativeToggles())
        {
            var group = rect.GetComponent<UIToggle>()?.toggleGroup;
            if (group != null)
                return group;
        }
        return null;
    }

    // The bar's other toggles, in order, ours excluded.
    private List<RectTransform> NativeToggles()
    {
        var toggles = new List<RectTransform>();
        for (int i = 0; i < _toggleParent.childCount; i++)
        {
            var child = _toggleParent.GetChild(i);
            if (child.gameObject == _toggleObject || !child.gameObject.activeSelf)
                continue;
            if (child.GetComponent<UIToggle>() == null)
                continue;
            var rect = child.TryCast<RectTransform>();
            if (rect != null)
                toggles.Add(rect);
        }
        return toggles;
    }

    // ---- the toggle's look ----

    // Our toggle looks like an unselected native one, down to its children: the reference tab's
    // toggle when it is off, else any native toggle that is off. Its value and selectable state
    // are matched first; then every rect, layout element, caption setting, image and active flag
    // under it, by hierarchy position (ours is a copy, so the hierarchies agree). The first sync
    // logs the roots side by side; a second later both trees are dumped. If the children keep
    // drifting back, Doozy's animators under ours are switched off and the state copied once more.
    private void SyncToggleGeometry()
    {
        var natives = NativeToggles();
        if (natives.Count == 0 || _tab.IsFocused())
            return;
        var ours = _toggleObject.transform.TryCast<RectTransform>();
        if (ours == null)
            return;

        RectTransform reference = null;
        var preferred = _referenceToggle != null ? _referenceToggle.transform.TryCast<RectTransform>() : null;
        if (preferred != null && natives.Any(n => n.Pointer == preferred.Pointer) && !_referenceToggle.isOn)
            reference = preferred;
        if (reference == null)
        {
            foreach (var rect in natives)
            {
                var toggle = rect.GetComponent<UIToggle>();
                if (toggle != null && !toggle.isOn)
                    reference = rect;
            }
        }
        if (reference == null)
            return;
        var referenceToggle = reference.GetComponent<UIToggle>();

        // Value and state first: a toggle that thinks it is on or selected draws itself raised.
        if (_toggle.isOn)
        {
            SettleToggle(_toggle);
            BelmontUiPlugin.Logger.LogDebug("[ui] tab toggle was on while its tab isn't focused; settled off");
        }
        string nativeState = referenceToggle != null ? referenceToggle.currentUISelectionState.ToString() : "?";
        string ourState = _toggle.currentUISelectionState.ToString();
        if (referenceToggle != null && nativeState != ourState)
        {
            _toggle.SetState(referenceToggle.currentUISelectionState);
            BelmontUiPlugin.Logger.LogDebug($"[ui] tab toggle state {ourState} -> {nativeState}");
        }

        if (!_toggleLogged)
        {
            _toggleLogged = true;
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] toggle before sync: native {Describe(reference)} on {referenceToggle?.isOn} state {nativeState} | " +
                $"ours {Describe(ours)} on {_toggle.isOn} state {ourState}");
        }

        bool layoutGroup = _toggleParent.GetComponent<HorizontalLayoutGroup>() != null;
        var last = natives[natives.Count - 1];
        float x = ours.anchoredPosition.x;
        if (!layoutGroup)
        {
            float slot = natives.Count >= 2
                ? last.anchoredPosition.x - natives[natives.Count - 2].anchoredPosition.x
                : last.rect.width;
            x = last.anchoredPosition.x + slot;
        }

        int changed = MirrorTree(reference, ours, x);
        _toggleSyncs++;
        if (changed > 0 && _toggleSyncs > 1)
            _toggleUnstable++;

        if (!_toggleDumped && Time.unscaledTime - _addedAt > ToggleDumpDelay && _toggleObject.activeInHierarchy)
        {
            _toggleDumped = true;
            BelmontUiPlugin.Logger.LogInfo($"[ui] toggle tree, native '{reference.name}':\n{DumpTree(reference)}");
            BelmontUiPlugin.Logger.LogInfo($"[ui] toggle tree, ours (after sync, {changed} value(s) changed):\n{DumpTree(ours)}");
        }

        // Something keeps putting the copy back: its animators. Off they go, and the state is
        // copied once more.
        if (_toggleUnstable >= 2 && !_animatorsOff)
        {
            _animatorsOff = true;
            int off = DisableAnimators(_toggleObject);
            int again = MirrorTree(reference, ours, x);
            BelmontUiPlugin.Logger.LogInfo($"[ui] tab toggle kept drifting from the native look: {off} animator(s) switched off, {again} value(s) re-copied");
        }
    }

    // Copies rects, layout elements, text settings, image states and active flags from one
    // hierarchy onto its twin. Returns how many values differed.
    private static int MirrorTree(RectTransform reference, RectTransform ours, float rootX)
    {
        int changed = 0;
        foreach (var rect in reference.GetComponentsInChildren<RectTransform>(true))
        {
            var twin = FindTwin(reference, rect, ours)?.TryCast<RectTransform>();
            if (twin == null)
                continue;
            bool root = rect.Pointer == reference.Pointer;
            var position = root ? new Vector2(rootX, rect.anchoredPosition.y) : rect.anchoredPosition;
            if (twin.anchorMin != rect.anchorMin) { twin.anchorMin = rect.anchorMin; changed++; }
            if (twin.anchorMax != rect.anchorMax) { twin.anchorMax = rect.anchorMax; changed++; }
            if (twin.pivot != rect.pivot) { twin.pivot = rect.pivot; changed++; }
            if (twin.sizeDelta != rect.sizeDelta) { twin.sizeDelta = rect.sizeDelta; changed++; }
            if (twin.localScale != rect.localScale) { twin.localScale = rect.localScale; changed++; }
            if (twin.localRotation != rect.localRotation) { twin.localRotation = rect.localRotation; changed++; }
            if (twin.anchoredPosition != position) { twin.anchoredPosition = position; changed++; }
            if (!root && twin.gameObject.activeSelf != rect.gameObject.activeSelf)
            {
                twin.gameObject.SetActive(rect.gameObject.activeSelf);
                changed++;
            }
            var element = rect.GetComponent<LayoutElement>();
            var twinElement = twin.GetComponent<LayoutElement>();
            if (element != null && twinElement != null)
            {
                if (twinElement.preferredWidth != element.preferredWidth) { twinElement.preferredWidth = element.preferredWidth; changed++; }
                if (twinElement.preferredHeight != element.preferredHeight) { twinElement.preferredHeight = element.preferredHeight; changed++; }
                if (twinElement.minWidth != element.minWidth) { twinElement.minWidth = element.minWidth; changed++; }
                if (twinElement.minHeight != element.minHeight) { twinElement.minHeight = element.minHeight; changed++; }
                if (twinElement.flexibleWidth != element.flexibleWidth) { twinElement.flexibleWidth = element.flexibleWidth; changed++; }
                if (twinElement.flexibleHeight != element.flexibleHeight) { twinElement.flexibleHeight = element.flexibleHeight; changed++; }
            }
        }
        foreach (var text in reference.GetComponentsInChildren<TMP_Text>(true))
        {
            var twin = FindTwin(reference, text.transform, ours)?.GetComponent<TMP_Text>();
            if (twin == null)
                continue;
            if (twin.enableAutoSizing != text.enableAutoSizing) { twin.enableAutoSizing = text.enableAutoSizing; changed++; }
            if (twin.fontSizeMin != text.fontSizeMin) { twin.fontSizeMin = text.fontSizeMin; changed++; }
            if (twin.fontSizeMax != text.fontSizeMax) { twin.fontSizeMax = text.fontSizeMax; changed++; }
            if (twin.fontSize != text.fontSize) { twin.fontSize = text.fontSize; changed++; }
            if (twin.fontStyle != text.fontStyle) { twin.fontStyle = text.fontStyle; changed++; }
            if (twin.alignment != text.alignment) { twin.alignment = text.alignment; changed++; }
            if (twin.margin != text.margin) { twin.margin = text.margin; changed++; }
            if (twin.characterSpacing != text.characterSpacing) { twin.characterSpacing = text.characterSpacing; changed++; }
            if (twin.color != text.color) { twin.color = text.color; changed++; }
        }
        changed += MirrorImages(reference.gameObject, ours.gameObject);
        return changed;
    }

    // Only the animators that move and scale things: colour animators stay on, so the tab still
    // lights up when selected or hovered, like the native ones.
    private static int DisableAnimators(GameObject root)
    {
        var off = new List<string>();
        var kept = new List<string>();
        foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null)
                continue;
            string type = behaviour.GetIl2CppType().FullName ?? "";
            if (!type.StartsWith("Doozy.Runtime.UIManager.Animators", StringComparison.Ordinal) &&
                !type.StartsWith("Doozy.Runtime.Reactor.Animators", StringComparison.Ordinal))
                continue;
            string name = $"{behaviour.GetIl2CppType().Name} on '{behaviour.name}'";
            if (type.Contains("ColorAnimator"))
            {
                behaviour.enabled = true;
                kept.Add(name);
                continue;
            }
            behaviour.enabled = false;
            off.Add(name);
        }
        BelmontUiPlugin.Logger.LogInfo($"[ui] tab toggle animators off: [{string.Join(", ", off)}], kept on: [{string.Join(", ", kept)}]");
        return off.Count;
    }

    private static string Describe(RectTransform rect)
    {
        var text = rect.GetComponentInChildren<TMP_Text>(true);
        var element = rect.GetComponent<LayoutElement>();
        return $"'{rect.name}' pos {rect.anchoredPosition} size {rect.sizeDelta} rect {rect.rect.size} scale {rect.localScale} " +
            $"anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot}" +
            (element != null ? $" layout pref {element.preferredWidth}x{element.preferredHeight} min {element.minWidth}x{element.minHeight}" : "") +
            (text != null ? $" | caption '{text.name}' size {text.fontSize} auto {text.enableAutoSizing} ({text.fontSizeMin}-{text.fontSizeMax}) " +
                $"rect {text.rectTransform.rect.size} pos {text.rectTransform.anchoredPosition} margin {text.margin} scale {text.rectTransform.localScale}" : "");
    }

    // Every transform under a root: path, active, position, size, scale, plus text and image facts.
    private static string DumpTree(Transform root)
    {
        var sb = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var rect = t.TryCast<RectTransform>();
            sb.Append("    ").Append(t.Pointer == root.Pointer ? "(root)" : Path(t, root));
            sb.Append(t.gameObject.activeSelf ? "" : " (off)");
            if (rect != null)
                sb.Append($" pos {rect.anchoredPosition} size {rect.sizeDelta} rect {rect.rect.size} scale {rect.localScale} anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot}");
            var text = t.GetComponent<TMP_Text>();
            if (text != null)
                sb.Append($" | text '{text.text}' size {text.fontSize} auto {text.enableAutoSizing} align {text.alignment} margin {text.margin}");
            var image = t.GetComponent<Image>();
            if (image != null)
                sb.Append($" | image {(image.enabled ? "on" : "off")} {image.color} sprite {(image.sprite != null ? image.sprite.name : "none")} type {image.type}");
            var raw = t.GetComponent<RawImage>();
            if (raw != null)
                sb.Append($" | raw image {(raw.enabled ? "on" : "off")} {raw.color}");
            var group = t.GetComponent<CanvasGroup>();
            if (group != null)
                sb.Append($" | group alpha {group.alpha:0.##}");
            var components = t.GetComponents<Behaviour>();
            var names = new List<string>();
            foreach (var c in components)
            {
                if (c == null)
                    continue;
                string name = c.GetIl2CppType().Name;
                if (name.Contains("Animator") || name.Contains("Selectable") || name.Contains("Toggle") || name.Contains("Container") || name.Contains("Stepper") || name.Contains("Dropdown") || name.Contains("Localize"))
                    names.Add(name + (c.enabled ? "" : "(disabled)"));
            }
            if (names.Count > 0)
                sb.Append($" | {string.Join(", ", names)}");
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    // One more tab has to fit in the same bar: every toggle, ours included, gets n/(n+1) of its
    // width (and caption size when the caption isn't auto-sized).
    private void FitToggles()
    {
        var natives = NativeToggles();
        int count = natives.Count;
        if (count == 0)
            return;
        float factor = _fittedCount < 0 ? count / (count + 1f) : (_fittedCount + 1f) / (count + 1f);
        _fittedCount = count;
        if (Mathf.Approximately(factor, 1f))
            return;
        Scale(natives, factor);
    }

    private void UnfitToggles()
    {
        if (_fittedCount < 0)
            return;
        var natives = NativeToggles();
        if (natives.Count > 0)
            Scale(natives, (natives.Count + 1f) / natives.Count, nativesOnly: true);
        _fittedCount = -1;
    }

    private void Scale(List<RectTransform> natives, float factor, bool nativesOnly = false)
    {
        var all = new List<RectTransform>(natives);
        var ours = _toggleObject != null ? _toggleObject.transform.TryCast<RectTransform>() : null;
        if (!nativesOnly && ours != null)
            all.Add(ours);

        var layout = _toggleParent.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing *= factor;
            foreach (var rect in all)
            {
                var element = rect.GetComponent<LayoutElement>();
                if (element != null)
                {
                    if (element.preferredWidth > 0f)
                        element.preferredWidth *= factor;
                    if (element.minWidth > 0f)
                        element.minWidth *= factor;
                }
                else if (!layout.childControlWidth)
                {
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x * factor, rect.sizeDelta.y);
                }
            }
        }
        else
        {
            // Hand-placed: the same span, one more slot in it.
            var first = natives[0];
            var last = natives[natives.Count - 1];
            float slot = natives.Count >= 2 ? (last.anchoredPosition.x - first.anchoredPosition.x) / (natives.Count - 1) : last.rect.width;
            float newSlot = slot * factor;
            for (int i = 0; i < all.Count; i++)
            {
                var rect = all[i];
                rect.anchoredPosition = new Vector2(first.anchoredPosition.x + i * newSlot, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x * factor, rect.sizeDelta.y);
            }
        }

        foreach (var rect in all)
        {
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.enableAutoSizing)
                    text.fontSize *= factor;
            }
        }
        BelmontUiPlugin.Logger.LogInfo($"[ui] tab bar '{_toggleParent.name}': {all.Count} toggles scaled by {factor:0.###} " +
            $"({(layout != null ? $"layout group, spacing now {layout.spacing:0.#}" : "hand-placed")})");
    }

    // ---- rows ----

    private void Rebuild()
    {
        LiveButtons.RemoveAll(e => e.Tab == this);
        if (_capture != null)
            EndCapture(null, "the tab was rebuilt");
        _headers.Clear();
        _rowOf.Clear();
        foreach (var rowObject in _rowObjects)
            SafeDestroy(rowObject);
        _rowObjects.Clear();
        _rows.Clear();
        _owner.Clear();
        _imagesDumped = false;

        // Where the selection was, so a rebuild while the tab is in use keeps it on a live row.
        int selected = _rows.FindIndex(b => b.Selectable != null && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == b.Selectable.gameObject);

        var pages = Settings.Snapshot();
        // A menu registered again is a new object: the open one is found again by its GUID.
        if (_openPage != null)
            _openPage = pages.FirstOrDefault(p => p.Id == _openPage.Id);
        if (_openPage == null)
        {
            _waitBackRelease = false;
            UnblockBack();
        }

        _textLanguage = GameLanguage.Code;
        _emptyHeader = pages.Length == 0 ? AddHeader(Texts.NoMods.Current, null) : null;
        foreach (var page in pages)
        {
            try
            {
                AddModRow(page);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {page.Id}: list row not added: {e}");
            }
        }
        foreach (var page in pages)
        {
            foreach (var row in page.Rows)
            {
                try
                {
                    AddRow(page, row, page);
                }
                catch (Exception e)
                {
                    BelmontUiPlugin.Logger.LogError($"[ui] {page.Id}: row '{row.Label}' not added: {e}");
                }
            }
            try
            {
                AddBackRow(page);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {page.Id}: Back row not added: {e}");
            }
        }

        ApplyMode();
        _builtRevision = Settings.Revision;
        WatchConfigFiles(pages);
        if (_tab.IsFocused())
        {
            var row = selected >= 0 && selected < _rows.Count && _rows[selected].Object != null && _rows[selected].Object.activeSelf
                ? _rows[selected]
                : FirstVisibleRow();
            Select(row);
        }
        BelmontUiPlugin.Logger.LogInfo(
            $"[ui] MODS tab: {pages.Length} mod(s) [{string.Join(", ", pages.Select(p => p.Title))}], {_rows.Count} rows in all");
    }

    // The navigation group's list and the tab's default selection, from the shown rows' selectables.
    private void UpdateNavigation()
    {
        var selectables = new List<UISelectable>();
        foreach (var row in _rows)
        {
            var selectable = row.Selectable;
            if (selectable == null || row.Object == null || !row.Object.activeSelf)
                continue;
            selectable.interactable = true;
            selectables.Add(selectable);
        }
        if (_navigation != null)
        {
            _navigation.selectables = new Il2CppReferenceArray<UISelectable>(selectables.ToArray());
            _navigation.UpdateNavigation();
        }
        _tab.defaultSelection = selectables.Count > 0 ? selectables[0].gameObject : null;
    }

    // A label on its own line, copied from a row's name label.
    private TMP_Text AddHeader(string title, SettingsMenu owner)
    {
        var source = _templates.Bool ?? _templates.DropBool;
        var template = source.Binding.TryCast<UIBoolSettingBinding>()?.nameLabel ?? source.Binding.TryCast<UIDropdownBoolSettingBinding>()?.nameLabel;
        if (template == null)
            return null;
        var labelObject = Object.Instantiate(template.gameObject, _staging.transform);
        labelObject.name = "Header";
        foreach (var localizer in labelObject.GetComponentsInChildren<LocalizeStringEvent>(true))
            Object.DestroyImmediate(localizer);
        var label = labelObject.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.text = title;
            label.fontStyle = FontStyles.Bold;
        }
        var rowTransform = source.Row.transform.TryCast<RectTransform>();
        float height = rowTransform != null && rowTransform.rect.height > 1f ? rowTransform.rect.height : FallbackRowHeight;
        var element = labelObject.GetComponent<LayoutElement>() ?? labelObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        labelObject.transform.SetParent(_content, false);
        labelObject.transform.SetAsLastSibling();
        _rowObjects.Add(labelObject);
        if (owner != null)
            _owner[labelObject.Pointer] = owner;
        return label;
    }

    // owner: the sub-menu the row belongs to; null for the list.
    private void AddRow(SettingsMenu page, SettingRow row, SettingsMenu owner)
    {
        var bound = new BoundRow { Row = row, Page = page };
        GameObject rowObject;
        switch (row.Kind)
        {
            case RowKind.Header:
                var header = AddHeader(row.Label, owner);
                _headers.Add((header, row));
                if (header != null)
                    _rowOf[header.gameObject.Pointer] = row;
                return;
            case RowKind.Toggle:
            case RowKind.Button:
            case RowKind.Binding:
                rowObject = _templates.Bool != null ? CopyToggleRow(row, bound) : CopyDropBoolRow(row, bound);
                if (row.IsAction)
                {
                    AddButtonVisual(rowObject, bound);
                    LiveButtons.Add((this, bound));
                }
                break;
            case RowKind.Slider:
            case RowKind.IntSlider:
                if (_templates.Float == null)
                {
                    Skip(page, row, "the game has no slider row to copy");
                    return;
                }
                rowObject = CopySliderRow(row, bound);
                break;
            default:
                if (_templates.Enum == null)
                {
                    Skip(page, row, "the game has no list row to copy");
                    return;
                }
                rowObject = CopyListRow(row, bound);
                break;
        }
        bound.Object = rowObject;
        rowObject.transform.SetParent(_content, false);
        rowObject.transform.SetAsLastSibling();
        _rowObjects.Add(rowObject);
        _rows.Add(bound);
        _rowOf[rowObject.Pointer] = row;
        // The hint shows next to the label (a mod's list row has its summary instead).
        if (owner != null && !string.IsNullOrEmpty(row.Hint))
            AddSummary(bound, row.Hint);
        if (owner != null)
            _owner[rowObject.Pointer] = owner;
    }

    private GameObject CopyToggleRow(SettingRow row, BoundRow bound)
    {
        var template = _templates.Bool;
        bound.Template = template.Row;
        var rowObject = Object.Instantiate(template.Row, _staging.transform);
        var binding = rowObject.GetComponent<UIBoolSettingBinding>();
        var label = binding.nameLabel;
        var toggle = binding.toggle;
        HideIndicator(binding.unsavedChangesIndicator);
        Object.DestroyImmediate(binding);
        Strip(rowObject);
        SetLabel(label, row.Label);
        bound.LabelText = label;
        toggle ??= rowObject.GetComponentInChildren<UIToggle>(true);
        if (toggle == null)
            throw new InvalidOperationException("the copied toggle row has no UIToggle");
        bound.Toggle = toggle;
        bound.NavSelectable = NavSelectable(template, rowObject);
        var sfx = template.Sfx;
        Apply(bound);
        Listen(toggle.OnValueChangedCallback, (bool on) =>
        {
            if (_refreshing)
                return;
            if (row.IsAction)
            {
                // Any press, on or off: the row is put back off when the button is released.
                if (bound.PressedUntil <= 0f)
                    Press(bound);
                return;
            }
            row.SetBool(on);
            Play(sfx);
        });
        return rowObject;
    }

    // Without a toggle row to copy, a bool is an Off / On list.
    private GameObject CopyDropBoolRow(SettingRow row, BoundRow bound)
    {
        var template = _templates.DropBool;
        bound.Template = template.Row;
        var rowObject = Object.Instantiate(template.Row, _staging.transform);
        var binding = rowObject.GetComponent<UIDropdownBoolSettingBinding>();
        var label = binding.nameLabel;
        var stepper = binding._listStepper;
        var description = binding.descriptionDisplay;
        HideIndicator(binding.unsavedChangesIndicator);
        Object.DestroyImmediate(binding);
        bound.HintLabel = SetDescription(description, row.Hint);
        Strip(rowObject);
        SetLabel(label, row.Label);
        bound.LabelText = label;
        stepper ??= rowObject.GetComponentInChildren<UIListStepper>(true);
        if (stepper == null)
            throw new InvalidOperationException("the copied list row has no UIListStepper");
        bound.Stepper = stepper;
        bound.NavSelectable = NavSelectable(template, rowObject);
        FillOptions(stepper, new[] { Texts.Off.Current, Texts.On.Current });
        HideDropdownBackground(stepper);
        HideStrayGraphics(rowObject, stepper);
        var sfx = template.Sfx;
        Apply(bound);
        Listen(stepper.OnIndexChanged, (int index) =>
        {
            if (_refreshing)
                return;
            if (row.IsAction)
            {
                if (bound.PressedUntil <= 0f)
                    Press(bound);
                return;
            }
            row.SetBool(index == 1);
            Play(sfx);
        });
        return rowObject;
    }

    private GameObject CopySliderRow(SettingRow row, BoundRow bound)
    {
        var template = _templates.Float;
        bound.Template = template.Row;
        var rowObject = Object.Instantiate(template.Row, _staging.transform);
        var binding = rowObject.GetComponent<UIFloatSettingBinding>();
        var label = binding.nameLabel;
        var slider = binding.slider;
        HideIndicator(binding.unsavedChangesIndicator);
        Object.DestroyImmediate(binding);
        Strip(rowObject);
        SetLabel(label, row.Label);
        bound.LabelText = label;
        slider ??= rowObject.GetComponentInChildren<UISlider>(true);
        if (slider == null)
            throw new InvalidOperationException("the copied slider row has no UISlider");
        bound.Slider = slider;
        bound.NavSelectable = NavSelectable(template, rowObject);
        // The value label is ours: the slider would write "1.0" where the row wants "100%", and
        // the formatted text must stay on one line.
        bound.ValueLabel = slider.ValueLabel;
        slider.ValueLabel = null;
        if (bound.ValueLabel != null)
        {
            bound.ValueLabel.enableWordWrapping = false;
            bound.ValueLabel.overflowMode = TextOverflowModes.Overflow;
            bound.ValueLabel.enableAutoSizing = false;
        }
        slider.wholeNumbers = row.Kind == RowKind.IntSlider;
        slider.minValue = row.Min;
        slider.maxValue = row.Max;
        if (row.EffectiveStep > 0f)
        {
            slider.overrideStepSize = true;
            slider.stepSizeOverrideValue = row.EffectiveStep;
        }
        var sfx = template.Sfx;
        Apply(bound);
        Listen(slider.OnValueChanged, (float value) =>
        {
            if (_refreshing)
                return;
            row.SetNumber(value);
            WriteValueLabel(bound);
            Play(sfx);
        });
        return rowObject;
    }

    private GameObject CopyListRow(SettingRow row, BoundRow bound)
    {
        var template = _templates.Enum;
        bound.Template = template.Row;
        var rowObject = Object.Instantiate(template.Row, _staging.transform);
        var binding = rowObject.GetComponent<UIDropdownEnumSettingBinding>();
        var label = binding.nameLabel;
        var stepper = binding._listStepper;
        var description = binding.descriptionDisplay;
        HideIndicator(binding.unsavedChangesIndicator);
        Object.DestroyImmediate(binding);
        bound.HintLabel = SetDescription(description, row.Hint);
        Strip(rowObject);
        SetLabel(label, row.Label);
        bound.LabelText = label;
        stepper ??= rowObject.GetComponentInChildren<UIListStepper>(true);
        if (stepper == null)
            throw new InvalidOperationException("the copied list row has no UIListStepper");
        bound.Stepper = stepper;
        bound.NavSelectable = NavSelectable(template, rowObject);
        FillOptions(stepper, row.Choices);
        HideDropdownBackground(stepper);
        HideStrayGraphics(rowObject, stepper);
        var sfx = template.Sfx;
        Apply(bound);
        Listen(stepper.OnIndexChanged, (int index) =>
        {
            if (_refreshing)
                return;
            row.SetChoice(index);
            Play(sfx);
        });
        return rowObject;
    }

    // The selectable the native navigation group moves to for this kind of row, found on the
    // copy at the same hierarchy position. Null means: use the control itself.
    private static UISelectable NavSelectable(Template template, GameObject rowObject)
    {
        if (template.NavPath == null)
            return null;
        var twin = rowObject.transform;
        foreach (int index in template.NavPath)
        {
            if (index >= twin.childCount)
                return null;
            twin = twin.GetChild(index);
        }
        var selectable = twin.GetComponent<UISelectable>();
        if (selectable != null)
            selectable.interactable = true;
        return selectable;
    }

    // The control shows the entry's value. The change callbacks fire (Doozy only redraws a
    // toggle or list when they do: a value set without them left the old checkmark and caption
    // on screen after Restore defaults), and our own listeners ignore them while refreshing.
    private void Apply(BoundRow bound)
    {
        var row = bound.Row;
        if (row.IsAction && bound.PressedUntil > 0f)
            return;   // released (and put back off) a moment after the press
        if (row.Kind == RowKind.Binding && bound != _capture)
            ShowBinding(bound);
        _refreshing = true;
        try
        {
            if (bound.Toggle != null)
            {
                bool on = !row.IsAction && row.GetBool();
                if (bound.Toggle.isOn != on)
                    UIToggleExtensions.SetIsOn(bound.Toggle, on, false, true);
            }
            else if (bound.Slider != null)
            {
                bound.Slider.SetValue(row.GetNumber(), true);
                WriteValueLabel(bound);
            }
            else if (bound.Stepper != null)
            {
                int index = row.IsAction ? 0 : row.Kind == RowKind.Toggle ? (row.GetBool() ? 1 : 0) : row.GetChoice();
                bound.Stepper.SetIndex(index, true, true);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    // A copied slider writes its own label (the minimum, in its own format) when it first runs,
    // which is on a frame after its row appears, so ours is written again for the first frames
    // a slider row is shown.
    private void RewriteValueLabels()
    {
        foreach (var bound in _rows)
        {
            if (bound.ValueLabel == null || bound.Object == null)
                continue;
            if (!bound.Object.activeInHierarchy)
            {
                bound.ValueLabelFrames = 0;
                continue;
            }
            if (bound.ValueLabelFrames >= 3)
                continue;
            bound.ValueLabelFrames++;
            WriteValueLabel(bound);
        }
    }

    private static void WriteValueLabel(BoundRow bound)
    {
        if (bound.ValueLabel == null)
            return;
        try
        {
            bound.ValueLabel.text = string.Format(bound.Row.EffectiveFormat, bound.Row.GetNumber());
        }
        catch (FormatException)
        {
            bound.ValueLabel.text = bound.Row.GetNumber().ToString("0.##");
        }
    }

    // Values can change elsewhere (another menu, a reset), so they're re-read when the tab shows.
    private void RefreshValues()
    {
        foreach (var bound in _rows)
        {
            try
            {
                Apply(bound);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {bound.Page.Id}: '{bound.Row.Label}' refresh failed: {e}");
            }
        }
    }

    // Once a copied row is live: plain graphics outside its control stay off, the dropdown's list
    // template stays hidden, and the first list row is dumped next to its native row, image by
    // image. (Image states are no longer mirrored from the native row: they carry whatever
    // selection look that row was last left in, which showed as the ornament on every copy.)
    private void SettleRows()
    {
        foreach (var bound in _rows)
        {
            if (bound.Live || bound.Object == null || bound.Template == null || !bound.Object.activeInHierarchy)
                continue;
            bound.Live = true;
            try
            {
                if (bound.Stepper != null)
                    HideStrayGraphics(bound.Object, bound.Stepper);
                if (bound.Stepper != null && bound.Stepper.dropdown != null && bound.Stepper.dropdown.template != null)
                    bound.Stepper.dropdown.template.gameObject.SetActive(false);
                if (!_imagesDumped && bound.Stepper != null)
                {
                    _imagesDumped = true;
                    BelmontUiPlugin.Logger.LogInfo($"[ui] list row images, native '{bound.Template.name}':\n{DumpImages(bound.Template.transform)}");
                    BelmontUiPlugin.Logger.LogInfo($"[ui] list row images, ours '{bound.Row.Label}':\n{DumpImages(bound.Object.transform)}");
                }
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': settle failed: {e.Message}");
            }
        }
    }

    private static string DumpImages(Transform root)
    {
        var sb = new StringBuilder();
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            string kind = graphic.GetIl2CppType().Name;
            if (kind == "TextMeshProUGUI")
                continue;
            var image = graphic.TryCast<Image>();
            sb.Append("    ").Append(Path(graphic.transform, root))
                .Append(graphic.gameObject.activeInHierarchy ? "" : " (inactive)")
                .Append($" {kind} {(graphic.enabled ? "on" : "off")} {graphic.color}")
                .Append(image != null ? $" sprite {(image.sprite != null ? image.sprite.name : "none")} type {image.type}" : "")
                .Append($" rect {graphic.rectTransform.rect.size}")
                .Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    private static int MirrorImages(GameObject template, GameObject copy)
    {
        int changed = 0;
        foreach (var image in template.GetComponentsInChildren<Image>(true))
        {
            var twin = FindTwin(template.transform, image.transform, copy.transform);
            var copyImage = twin?.GetComponent<Image>();
            if (copyImage == null)
                continue;
            if (copyImage.enabled != image.enabled)
            {
                copyImage.enabled = image.enabled;
                changed++;
            }
            if (copyImage.color != image.color)
            {
                copyImage.color = image.color;
                changed++;
            }
            if (twin.gameObject.activeSelf != image.gameObject.activeSelf)
            {
                twin.gameObject.SetActive(image.gameObject.activeSelf);
                changed++;
            }
        }
        return changed;
    }

    // The transform at the same place of the copy's hierarchy.
    private static Transform FindTwin(Transform templateRoot, Transform target, Transform copyRoot)
    {
        var path = PathIndices(templateRoot, target);
        if (path == null)
            return null;
        var twin = copyRoot;
        foreach (int index in path)
        {
            if (index >= twin.childCount)
                return null;
            twin = twin.GetChild(index);
        }
        return twin;
    }

    // Sibling indices from root down to target; null when target isn't under root.
    private static List<int> PathIndices(Transform root, Transform target)
    {
        var path = new List<int>();
        var current = target;
        while (current != null && current.Pointer != root.Pointer)
        {
            path.Add(current.GetSiblingIndex());
            current = current.parent;
        }
        if (current == null)
            return null;
        path.Reverse();
        return path;
    }

    // ---- the list and the sub-menus ----

    // The tab opens on a list of the registered mods (title, summary, a Settings button); a
    // button opens that mod's rows as a sub-menu with its title at the top. B, Esc, Backspace or
    // the Back row at the end return to the list, with that mod's row selected. All rows are
    // built once per rebuild; the mode only switches which are active (the layout skips the rest).
    private SettingsMenu _openPage;           // null: the list
    private SettingsMenu _pendingOpen;
    private bool _pendingClose;
    private bool _waitBackRelease;
    private TMP_Text _titleText;
    private string _listTitle;
    private bool _upperTitles;   // the native page titles are upper case, so a mod's title is too
    private readonly Dictionary<IntPtr, SettingsMenu> _owner = new();   // row object -> its menu (absent: the list)
    private readonly List<InputAction> _blockedBack = new();
    private bool _blockLogged;

    private static readonly string[] BackWords = { "Close", "Cancel", "Back" };

    // A mod's row on the list: its title, its summary in the middle, a Settings button.
    private void AddModRow(SettingsMenu page)
    {
        var row = new SettingRow
        {
            LabelText = page.LocalizedTitle, Kind = RowKind.Button, ButtonLabel = Texts.Settings, Sound = ButtonSound.None,
            OnPress = () => _pendingOpen = page,
        };
        int before = _rows.Count;
        AddRow(page, row, null);
        if (_rows.Count == before)
            return;
        var bound = _rows[_rows.Count - 1];
        bound.Opens = page;
        AddSummary(bound, page.Summary);
    }

    // The last row of every sub-menu, for the mouse (B, Esc and Backspace do the same).
    private void AddBackRow(SettingsMenu page)
    {
        var row = new SettingRow
        {
            LabelText = Texts.Back, Kind = RowKind.Button, ButtonLabel = Texts.Back, Sound = ButtonSound.None,
            OnPress = () => _pendingClose = true,
        };
        AddRow(page, row, page);
    }

    // A row's description (a mod's summary on the list, a setting's hint in a sub-menu): a copy of
    // the row's label, smaller, in a column that starts after the page's longest label and ends
    // before the controls, up to two lines, centred on the label. FitSummaries places it once the
    // row's labels are drawn and can be measured.
    private void AddSummary(BoundRow bound, string summary)
    {
        if (string.IsNullOrEmpty(summary) || bound.LabelText == null || bound.Object == null)
            return;
        try
        {
            var rowRect = bound.Object.transform.TryCast<RectTransform>();
            var labelRect = bound.LabelText.rectTransform;
            float center = 0.5f;
            if (rowRect.rect.height > 1f)
            {
                float y = rowRect.InverseTransformPoint(labelRect.TransformPoint(new Vector3(0f, labelRect.rect.center.y, 0f))).y;
                center = (y - rowRect.rect.yMin) / rowRect.rect.height;
            }
            var copy = Object.Instantiate(bound.LabelText.gameObject, bound.Object.transform);
            copy.name = "Summary";
            foreach (var localizer in copy.GetComponentsInChildren<LocalizeStringEvent>(true))
                Object.DestroyImmediate(localizer);
            var text = copy.GetComponent<TMP_Text>();
            float labelSize = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            bound.SummarySizeMax = labelSize * SummaryLargest;
            bound.SummarySizeMin = labelSize * SummarySmallest;
            text.text = summary;
            bound.SummaryText = text;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = false;
            text.fontSize = bound.SummarySizeMax;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.lineSpacing = SummaryLineSpacing;
            foreach (var fitter in copy.GetComponentsInChildren<ContentSizeFitter>(true))
                Object.DestroyImmediate(fitter);

            // Hidden until FitSummaries has measured the labels and placed it.
            var rect = copy.transform.TryCast<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.zero;
            text.margin = Vector4.zero;
            var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            bound.SummaryRect = rect;
            bound.SummaryCenter = center;
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': summary not shown: {e.Message}");
        }
    }

    // The description from start (a fraction of the row width) to just before the controls: the
    // largest size from 75 % to 55 % of the label's at which the text takes at most two lines, else
    // the smallest size with "..." after the second line.
    private void PlaceSummary(BoundRow bound, float start)
    {
        var rect = bound.SummaryRect;
        var text = bound.SummaryText;
        var rowRect = bound.Object.transform.TryCast<RectTransform>();
        float rowWidth = rowRect.rect.width;
        float right = Mathf.Max(0.45f, _templates.Control.xMin - 0.02f);
        start = Mathf.Clamp(start, 0.08f, right - 0.15f);
        float width = (right - start) * rowWidth;

        // Two lines measured as two lines (line spacing included): two single lines fall short and
        // the second line was cut.
        float sizeMax = bound.SummarySizeMax, sizeMin = bound.SummarySizeMin;
        float size = sizeMin;
        float step = Mathf.Max(0.5f, (sizeMax - sizeMin) / 8f);
        for (float candidate = sizeMax; candidate >= sizeMin - 0.01f; candidate -= step)
        {
            text.fontSize = candidate;
            float twoLines = text.GetPreferredValues(TwoLines, width, 0f).y;
            if (text.GetPreferredValues(text.text, width, 0f).y <= twoLines * 1.02f)
            {
                size = candidate;
                break;
            }
        }
        text.fontSize = size;
        float height = text.GetPreferredValues(TwoLines, width, 0f).y * 1.08f;
        rect.anchorMin = rect.anchorMax = new Vector2((start + right) / 2f, bound.SummaryCenter);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // A description on two lines makes its row taller by one line (see GrowRow); one line keeps
        // the game's height.
        float oneLine = text.GetPreferredValues("Ag", width, 0f).y;
        float needed = text.GetPreferredValues(text.text, width, 0f).y;
        GrowRow(bound, needed > oneLine * 1.5f ? Mathf.Min(needed, height) - oneLine + 2f * RowPadding : 0f);
    }

    private int _rowsResized;

    // A row whose description takes two lines grows by the extra line: its layout height (the row
    // and its Background), which the dark strip and the selection ornaments follow (they stretch with
    // the row or sit on its middle). The label, which the game anchors to the top on toggle and slider
    // rows, and BelmontUI's own button and description are re-anchored so they stay centred.
    private void GrowRow(BoundRow bound, float extra)
    {
        var row = bound.Object;
        var element = row != null ? row.GetComponent<LayoutElement>() : null;
        if (element == null)
            return;
        if (bound.BaseHeight <= 0f)
            bound.BaseHeight = element.preferredHeight > 1f ? element.preferredHeight : row.transform.TryCast<RectTransform>().rect.height;
        float baseHeight = bound.BaseHeight;
        float height = baseHeight + Mathf.Max(0f, extra);
        if (Mathf.Abs(element.preferredHeight - height) < 0.5f)
            return;
        element.preferredHeight = height;
        element.minHeight = height;
        var background = row.transform.Find("Background");
        var inner = background != null ? background.GetComponent<LayoutElement>() : null;
        if (inner != null)
        {
            inner.preferredHeight = height;
            inner.minHeight = height;
        }

        var label = bound.LabelText != null ? bound.LabelText.rectTransform : null;
        if (label != null && Mathf.Approximately(label.anchorMin.y, label.anchorMax.y))
        {
            if (float.IsNaN(bound.LabelBaseY))
                bound.LabelBaseY = label.anchoredPosition.y + (label.anchorMin.y - 0.5f) * baseHeight;
            label.anchorMin = new Vector2(label.anchorMin.x, 0.5f);
            label.anchorMax = new Vector2(label.anchorMax.x, 0.5f);
            label.anchoredPosition = new Vector2(label.anchoredPosition.x, bound.LabelBaseY);
        }
        if (bound.ButtonRect != null)
        {
            float y = 0.5f + (bound.ButtonAnchorY - 0.5f) * baseHeight / height;
            bound.ButtonRect.anchorMin = new Vector2(bound.ButtonRect.anchorMin.x, y);
            bound.ButtonRect.anchorMax = new Vector2(bound.ButtonRect.anchorMax.x, y);
        }
        if (bound.SummaryRect != null)
        {
            float y = 0.5f + (bound.SummaryCenter - 0.5f) * baseHeight / height;
            bound.SummaryRect.anchorMin = new Vector2(bound.SummaryRect.anchorMin.x, y);
            bound.SummaryRect.anchorMax = new Vector2(bound.SummaryRect.anchorMax.x, y);
        }
        _rowsResized++;
    }

    // The column for the descriptions of the rows on screen (the list, or the open sub-menu): just
    // after the longest label, the same on every row. Labels are measured from their rendered text
    // once the rows have been drawn for a couple of frames (measured earlier, a label came out
    // several times too wide).
    private void FitSummaries()
    {
        List<BoundRow> shown = null;
        bool pending = false;
        foreach (var bound in _rows)
        {
            if (bound.Object == null || !bound.Object.activeInHierarchy)
            {
                if (bound.Object != null)
                    bound.ShownFrames = 0;
                continue;
            }
            bound.ShownFrames++;
            (shown ??= new List<BoundRow>()).Add(bound);
            if (bound.SummaryRect != null && !bound.SummaryFitted)
                pending = true;
        }
        if (!pending || shown.Any(b => b.ShownFrames < 3))
            return;
        try
        {
            float longest = 0f, rowWidth = 0f;
            string longestLabel = null;
            var ends = new Dictionary<BoundRow, float>();
            foreach (var bound in shown)
            {
                var label = bound.LabelText;
                // Rows scrolled out of the band are scaled to zero and can't be measured.
                if (label == null || bound.Object.transform.localScale.x < 0.01f)
                    continue;
                label.ForceMeshUpdate();
                var bounds = label.textBounds;
                var rowRect = bound.Object.transform.TryCast<RectTransform>();
                rowWidth = rowRect.rect.width;
                if (rowWidth <= 1f || bounds.size.x <= 1f)
                    continue;
                float nameEnd = rowRect.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(bounds.max.x, 0f, 0f))).x;
                float end = (nameEnd - rowRect.rect.xMin) / rowWidth;
                if (end < 0.6f)
                {
                    ends[bound] = end;
                    if (end > longest)
                    {
                        longest = end;
                        longestLabel = bound.Row.Label;
                    }
                }
            }
            // One column for the page, but no further in than ColumnLimit: a row whose label is
            // longer starts its description just after its own label.
            float column = Mathf.Min(longest > 0f ? longest : 0.25f, ColumnLimit) + ColumnGap;
            _rowsResized = 0;
            foreach (var bound in shown)
            {
                if (bound.SummaryRect == null)
                    continue;
                float start = ends.TryGetValue(bound, out var own) ? Mathf.Max(column, own + ColumnGap) : column;
                PlaceSummary(bound, start);
                bound.SummaryFitted = true;
            }
            if (_rowsResized > 0)
            {
                ResetClip();
                var content = _content.TryCast<RectTransform>();
                if (content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                if (_scrollReady)
                {
                    _lastSelected = null;
                    ApplyClip();
                }
                BelmontUiPlugin.Logger.LogInfo($"[ui] descriptions: {_rowsResized} row(s) resized for two-line descriptions");
            }
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] descriptions: longest label '{longestLabel}' ends at {longest:0.###} of a {rowWidth:0}-unit row, column from {column:0.###}");
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] descriptions not placed: {e.Message}");
        }
    }

    // Shows the rows of the current mode, retitles the page, rebuilds navigation and scroll.
    private void ApplyMode()
    {
        foreach (var rowObject in _rowObjects)
        {
            if (rowObject == null)
                continue;
            _owner.TryGetValue(rowObject.Pointer, out var owner);
            bool show = owner == _openPage && RowShown(rowObject);
            if (rowObject.activeSelf != show)
                rowObject.SetActive(show);
        }
        if (_titleText != null && _listTitle != null)
            _titleText.text = _openPage == null ? _listTitle : _upperTitles ? _openPage.Title.ToUpperInvariant() : _openPage.Title;
        ResetClip();
        var content = _content.TryCast<RectTransform>();
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        UpdateNavigation();
        if (_scrollReady)
        {
            SetScroll(0f);
            _lastSelected = null;
            ApplyClip();
        }
    }

    private BoundRow FirstVisibleRow()
    {
        foreach (var bound in _rows)
        {
            if (bound.Object != null && bound.Object.activeSelf && bound.Selectable != null)
                return bound;
        }
        return null;
    }

    private static void Select(BoundRow bound)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem != null && bound?.Selectable != null)
            eventSystem.SetSelectedGameObject(bound.Selectable.gameObject);
    }

    private void OpenPage(SettingsMenu page)
    {
        if (_openPage != null || page == null)
            return;
        _openPage = page;
        ApplyMode();
        BlockBack();
        Play(!_templates.OpenSfx.IsNull ? _templates.OpenSfx : _templates.SubTabSfx);
        Select(FirstVisibleRow());
        NormalizeRows(log: false);
        BelmontUiPlugin.Logger.LogInfo($"[ui] '{page.Title}' opened ({page.Rows.Count} rows, selection {SelectionName()})");
    }

    // silent: the tab lost focus or the menu closed; no sound, no reselection, inputs back at once.
    private void ClosePage(bool silent)
    {
        var page = _openPage;
        if (page == null)
            return;
        try
        {
            page.Save();
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogError($"[ui] {page.Id}: save failed: {e}");
        }
        _openPage = null;
        ApplyMode();
        if (silent)
        {
            _waitBackRelease = false;
            UnblockBack();
            BelmontUiPlugin.Logger.LogInfo($"[ui] '{page.Title}' closed with the tab");
            return;
        }
        Play(!_templates.CloseSfx.IsNull ? _templates.CloseSfx : _templates.SubTabSfx);
        Select(_rows.FirstOrDefault(r => r.Opens == page && r.Object != null && r.Object.activeSelf) ?? FirstVisibleRow());
        NormalizeRows(log: false);
        // The game's own Close / Cancel come back once the key that closed the sub-menu is up,
        // so the same press can't also close the pause menu.
        _waitBackRelease = true;
        BelmontUiPlugin.Logger.LogInfo($"[ui] '{page.Title}' closed (selection {SelectionName()})");
    }

    private static bool BackPressed()
    {
        var pad = Gamepad.current;
        var keyboard = Keyboard.current;
        return (pad != null && pad.buttonEast.wasPressedThisFrame) ||
            (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame));
    }

    private static bool BackOrConfirmHeld()
    {
        var pad = Gamepad.current;
        var keyboard = Keyboard.current;
        return (pad != null && (pad.buttonEast.isPressed || pad.buttonSouth.isPressed)) ||
            (keyboard != null && (keyboard.escapeKey.isPressed || keyboard.backspaceKey.isPressed || keyboard.enterKey.isPressed ||
                                  keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed)) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    // ---- entries changed elsewhere ----

    // The config files the menus' rows are bound to. A change to any of their entries, by anyone (a
    // mod's SettingChanged handler moving other entries, a reset, a reload), is redrawn on the rows
    // that show it the next frame the tab has the focus.
    private static readonly HashSet<ConfigFile> WatchedFiles = new();
    private static readonly HashSet<ConfigEntryBase> ChangedEntries = new();

    private static void WatchConfigFiles(IEnumerable<SettingsMenu> pages)
    {
        foreach (var row in pages.SelectMany(p => p.Rows))
        {
            var file = row.IsSetting ? row.Entry?.ConfigFile : null;
            if (file != null && WatchedFiles.Add(file))
                file.SettingChanged += (_, e) =>
                {
                    if (e?.ChangedSetting != null)
                        ChangedEntries.Add(e.ChangedSetting);
                };
        }
    }

    private void RefreshChanged()
    {
        if (ChangedEntries.Count == 0)
            return;
        UpdateVisibility();
        foreach (var bound in _rows)
        {
            if (bound == _capture || bound.Row.Entry == null || !ChangedEntries.Contains(bound.Row.Entry))
                continue;
            try
            {
                Apply(bound);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': redraw after a change failed: {e.Message}");
            }
        }
        ChangedEntries.Clear();
    }

    // ---- rows shown on a condition ----

    private bool RowShown(GameObject rowObject) =>
        !_rowOf.TryGetValue(rowObject.Pointer, out var row) || row.IsVisible();

    // After a setting changed: rows of the open sub-menu whose VisibleWhen changed are shown or hidden,
    // the layout closes or opens the gap, navigation is rebuilt, and a selection on a row that went
    // away moves to the nearest row still shown.
    private void UpdateVisibility()
    {
        if (_openPage == null)
            return;
        var eventSystem = EventSystem.current;
        var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        int selectedIndex = selected == null ? -1 : _rows.FindIndex(b => b.Object != null && IsUnder(selected.transform, b.Object.transform));
        int changed = 0;
        foreach (var rowObject in _rowObjects)
        {
            if (rowObject == null || !_owner.TryGetValue(rowObject.Pointer, out var owner) || owner != _openPage)
                continue;
            bool show = RowShown(rowObject);
            if (rowObject.activeSelf == show)
                continue;
            rowObject.SetActive(show);
            changed++;
        }
        if (changed == 0)
            return;
        ResetClip();
        var content = _content.TryCast<RectTransform>();
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        UpdateNavigation();
        if (selectedIndex >= 0 && (_rows[selectedIndex].Object == null || !_rows[selectedIndex].Object.activeSelf))
            Select(NearestShownRow(selectedIndex));
        if (_scrollReady)
        {
            _lastSelected = null;   // scrolled back into view next frame
            ApplyClip();
        }
        BelmontUiPlugin.Logger.LogInfo($"[ui] '{_openPage.Title}': {changed} row(s) shown or hidden by their conditions");
    }

    // The closest row still on screen below the given one, else above it.
    private BoundRow NearestShownRow(int index)
    {
        for (int distance = 1; distance < _rows.Count; distance++)
        {
            foreach (int i in new[] { index + distance, index - distance })
            {
                if (i < 0 || i >= _rows.Count)
                    continue;
                var bound = _rows[i];
                if (bound.Object != null && bound.Object.activeSelf && bound.Selectable != null)
                    return bound;
            }
        }
        return FirstVisibleRow();
    }

    // ---- binding rows ----

    // Pressing a binding row: its line says "Press new button", the button shows "...", and every
    // input action the game has on is switched off, so nothing else reacts to what is pressed. The
    // controls are read straight from the devices.
    private void StartCapture(BoundRow bound)
    {
        if (_capture != null)
            EndCapture(null, "another row started listening");
        _capture = bound;
        _captureArmed = false;
        _captureHeld = null;
        _captureLastInput = Time.unscaledTime;
        bound.PressedUntil = float.MaxValue;
        BlockCapture();
        ShowBinding(bound);
        Play(_templates.Bool != null ? _templates.Bool.Sfx : _templates.ApplySfx);
        BelmontUiPlugin.Logger.LogInfo($"[ui] {bound.Page.Id}: '{bound.Row.Label}' listening ({bound.Row.Options.Devices}, was '{bound.Row.GetText()}')");
    }

    private void CaptureFrame()
    {
        float now = Time.unscaledTime;
        if (_capture == null)
        {
            // The menu gets its input back once the row's devices are let go, or after a moment in
            // any case (a key or button the Input System still reports down must not lock the menu).
            if (_captureWaitRelease)
            {
                string held = Held(_captureDevices);
                if (held == null || now - _captureEndedAt > ReleaseWaitSeconds)
                {
                    if (held != null)
                        BelmontUiPlugin.Logger.LogInfo($"[ui] binding: menu input back although '{held}' still reads as held");
                    _captureWaitRelease = false;
                    UnblockCapture();
                }
            }
            return;
        }
        var options = _capture.Row.Options;
        if (!_captureArmed)
        {
            // The press that opened the row (A, Enter, a click) and a refused input must be let go
            // first; controls still down after a moment are ignored (only new presses count).
            string held = Held(options.Devices);
            if (held == null || now - _captureLastInput > ReleaseWaitSeconds)
            {
                if (held != null)
                    BelmontUiPlugin.Logger.LogInfo($"[ui] binding: listening although '{held}' still reads as held");
                _captureArmed = true;
                _captureLastInput = now;
            }
            return;
        }
        // Cancel as on the game's Controls screen (its UI/Close action): B, Esc or Backspace.
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        if ((keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            EndCapture(null, "cancelled");
            return;
        }
        if (_captureHeld == null && now - _captureLastInput > Math.Max(1f, options.TimeoutSeconds))
        {
            EndCapture(null, "no input in time");
            return;
        }

        var presses = PressedThisFrame(options.Devices);
        string pressed = presses.FirstOrDefault(p => p != _captureHeld);
        if (_captureHeld == null)
        {
            if (pressed == null)
                return;
            _captureLastInput = now;
            if (!options.AllowCombinations)
            {
                Offer(pressed);
                return;
            }
            // Two controls down in the same frame (a quick Shift+G): the modifier, or the one listed
            // first, is the held one.
            var second = presses.Skip(1).FirstOrDefault(p => ControlBinding.IsGamepad(p) == ControlBinding.IsGamepad(pressed));
            if (second != null)
            {
                bool swap = IsModifier(second) && !IsModifier(pressed);
                Offer(swap ? ControlBinding.Make(second, pressed) : ControlBinding.Make(pressed, second));
                return;
            }
            _captureHeld = pressed;
            return;
        }
        if (pressed != null && pressed != _captureHeld && ControlBinding.IsGamepad(pressed) == ControlBinding.IsGamepad(_captureHeld))
        {
            Offer(ControlBinding.Make(_captureHeld, pressed));
            return;
        }
        if (ControlBinding.Button(_captureHeld)?.isPressed != true)
            Offer(_captureHeld);
    }

    // A complete input: stored when it passes the row's checks, else the reason shows on the row's
    // line and the row keeps listening once everything is let go.
    private void Offer(string binding)
    {
        var bound = _capture;
        var options = bound.Row.Options;
        LocalizedText reason = null;
        foreach (var part in ControlBinding.Parse(binding))
        {
            bool allowed = options.Allowed == null || options.Allowed.Count == 0 || options.Allowed.Contains(part);
            bool reserved = options.Reserved != null && options.Reserved.Contains(part);
            if (!allowed || reserved)
            {
                reason = options.ReservedReason ?? Texts.AlreadyAssigned;
                break;
            }
        }
        if (reason == null && options.Validate != null)
        {
            try
            {
                reason = options.Validate(binding);
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {bound.Page.Id}: '{bound.Row.Label}' check of '{binding}' failed: {e}");
                reason = Texts.AlreadyAssigned;
            }
        }
        if (reason != null)
        {
            BelmontUiPlugin.Logger.LogInfo($"[ui] {bound.Page.Id}: '{bound.Row.Label}' refused '{binding}': {reason.Current}");
            if (bound.SummaryText != null)
                bound.SummaryText.text = reason.Current;
            _captureHeld = null;
            _captureArmed = false;
            _captureLastInput = Time.unscaledTime;
            return;
        }
        bound.Row.SetText(binding);
        EndCapture(binding, "bound");
    }

    private void EndCapture(string binding, string why)
    {
        var bound = _capture;
        _capture = null;
        _captureHeld = null;
        if (bound == null)
            return;
        bound.PressedUntil = Time.unscaledTime;
        _captureEndedAt = Time.unscaledTime;
        _captureDevices = bound.Row.Options.Devices;
        _captureWaitRelease = _blockedForCapture.Count > 0;
        if (!_captureWaitRelease)
            UnblockCapture();
        try
        {
            if (bound.SummaryText != null)
                bound.SummaryText.text = bound.Row.Hint ?? "";
            ShowBinding(bound);
            if (binding != null)
                Play(_templates.ApplySfx.IsNull && _templates.Bool != null ? _templates.Bool.Sfx : _templates.ApplySfx);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': binding display failed: {e.Message}");
        }
        BelmontUiPlugin.Logger.LogInfo($"[ui] {bound.Page.Id}: '{bound.Row.Label}' {why}: '{bound.Row.GetText()}'");
        // The mod's SettingChanged handlers may have changed this entry back or others with it.
        RefreshValues();
    }

    // The button caption: the binding (icons for the controller in use), or "..." while listening,
    // with "Press new button" on the row's line.
    private void ShowBinding(BoundRow bound)
    {
        bool listening = bound == _capture;
        if (bound.ButtonTexts != null)
        {
            string caption = listening ? "..." : bound.Row.Text;
            foreach (var text in bound.ButtonTexts)
            {
                if (text != null)
                {
                    text.richText = true;
                    text.text = caption;
                }
            }
        }
        if (listening && bound.SummaryText != null)
            bound.SummaryText.text = Texts.PressButton.Current;
    }

    // The captions follow the controller in use (Xbox or PlayStation icons).
    private void RefreshGlyphs()
    {
        string prefix = ControlGlyphs.Prefix;
        if (prefix == _glyphPrefix)
            return;
        bool first = _glyphPrefix == null;
        _glyphPrefix = prefix;
        if (!first)
            BelmontUiPlugin.Logger.LogInfo($"[ui] binding rows: controller icons now '{prefix}'");
        foreach (var bound in _rows)
        {
            if (bound.Row.Kind == RowKind.Binding)
                ShowBinding(bound);
        }
    }

    // The controls of the given devices pressed this frame, modifiers first. B, Esc and Backspace
    // cancel instead, as on the game's Controls screen, so they are never bound.
    private static List<string> PressedThisFrame(BindingDevices devices)
    {
        var pressed = new List<string>();
        if ((devices & BindingDevices.Gamepad) != 0 && Gamepad.current != null)
        {
            foreach (var control in ControlBinding.GamepadControls)
            {
                if (control != "buttonEast" && ControlBinding.Button(control)?.wasPressedThisFrame == true)
                    pressed.Add(control);
            }
        }
        if ((devices & BindingDevices.Keyboard) != 0)
        {
            foreach (var control in ControlBinding.MouseControls)
            {
                if (ControlBinding.Button(control)?.wasPressedThisFrame == true)
                    pressed.Add(control);
            }
            // Every key, every frame while listening: "any key" stays pressed while Shift is held,
            // so it doesn't show the G of a Shift+G.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed)
            {
                foreach (var key in keyboard.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape && key.keyCode != Key.Backspace)
                        pressed.Add(key.keyCode.ToString());
                }
            }
        }
        return pressed.OrderBy(p => IsModifier(p) ? 0 : 1).ToList();
    }

    private static bool IsModifier(string control) =>
        control is "LeftShift" or "RightShift" or "LeftCtrl" or "RightCtrl" or "LeftAlt" or "RightAlt";

    // The first control of the given devices held now (the confirm inputs A, Enter, Space and the
    // left mouse button always count), or null.
    private static string Held(BindingDevices devices)
    {
        if (Gamepad.current != null)
        {
            foreach (var control in ControlBinding.GamepadControls)
            {
                if (((devices & BindingDevices.Gamepad) != 0 || control == "buttonSouth") && ControlBinding.Button(control)?.isPressed == true)
                    return control;
            }
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed)
        {
            foreach (var key in keyboard.allKeys)
            {
                if (key == null || !key.isPressed)
                    continue;
                var code = key.keyCode;
                if ((devices & BindingDevices.Keyboard) != 0 || code == Key.Enter || code == Key.NumpadEnter || code == Key.Space)
                    return code.ToString();
            }
        }
        if ((devices & BindingDevices.Keyboard) != 0)
        {
            foreach (var control in ControlBinding.MouseControls)
            {
                if (ControlBinding.Button(control)?.isPressed == true)
                    return control;
            }
        }
        return Mouse.current != null && Mouse.current.leftButton.isPressed ? "mouse left button" : null;
    }

    // Every input action the game has on goes off while a row listens (and until its input is let go),
    // so the press doesn't also navigate, switch tabs or close the menu. Only actions that were on
    // are touched, and all of them come back: switching off every action of a map makes the map read
    // as off too, so the map's state can't decide (unlike BlockBack, which leaves the maps on).
    private void BlockCapture()
    {
        if (_blockedForCapture.Count > 0)
            return;
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<InputActionAsset>()))
        {
            var asset = obj?.TryCast<InputActionAsset>();
            if (asset == null)
                continue;
            foreach (var map in asset.actionMaps)
            {
                if (map == null || !map.enabled)
                    continue;
                foreach (var action in map.actions)
                {
                    if (action == null || !action.enabled)
                        continue;
                    action.Disable();
                    _blockedForCapture.Add(action);
                }
            }
        }
        BelmontUiPlugin.Logger.LogDebug($"[ui] binding: {_blockedForCapture.Count} input action(s) off while listening");
    }

    private void UnblockCapture()
    {
        foreach (var action in _blockedForCapture)
        {
            try
            {
                if (!_blockedBack.Contains(action))
                    action.Enable();
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogDebug($"[ui] input action not restored: {e.Message}");
            }
        }
        _blockedForCapture.Clear();
    }

    // While a sub-menu is open, the game's enabled Close / Cancel / Back actions are off, so B
    // goes back to the list instead of closing the menu. Only actions that were on are touched.
    private void BlockBack()
    {
        if (_blockedBack.Count > 0)
            return;
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<InputActionAsset>()))
        {
            var asset = obj?.TryCast<InputActionAsset>();
            if (asset == null)
                continue;
            foreach (var map in asset.actionMaps)
            {
                if (map == null || !map.enabled)
                    continue;
                foreach (var action in map.actions)
                {
                    if (action == null || !action.enabled || !BackWords.Any(w => action.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;
                    action.Disable();
                    _blockedBack.Add(action);
                }
            }
        }
        if (!_blockLogged)
        {
            _blockLogged = true;
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] sub-menu: the game's back actions are off while it is open: [{string.Join(", ", _blockedBack.Select(a => $"{a.actionMap?.name}/{a.name}"))}]");
        }
    }

    // Not while their map is off (menu closed): the game turns them on with the map.
    private void UnblockBack()
    {
        foreach (var action in _blockedBack)
        {
            try
            {
                if (action.actionMap != null && action.actionMap.enabled)
                    action.Enable();
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogDebug($"[ui] back action not restored: {e.Message}");
            }
        }
        _blockedBack.Clear();
    }

    private void OnUnfocused()
    {
        if (_capture != null)
            EndCapture(null, "the tab lost the focus");
        _captureWaitRelease = false;
        UnblockCapture();
        SaveAll();
        _pendingOpen = null;
        _pendingClose = false;
        if (_openPage != null)
        {
            ClosePage(silent: true);
        }
        else
        {
            _waitBackRelease = false;
            UnblockBack();
        }
    }

    // ---- focus ----

    private void OnFocused()
    {
        if (_builtRevision != Settings.Revision)
            Rebuild();
        RefreshLanguage();
        ForceVisible();
        RefreshValues();
        // The mode's rows only (the list, or the open sub-menu), with navigation computed from live
        // objects now that everything shows.
        ApplyMode();
        bool first = !_pageLogged;
        if (first)
        {
            _pageLogged = true;
            if (_host.IsPauseMenu)
            {
                AddTopPadding();
                SetUpScrolling();
            }
            LogPageState("first focus");
        }
        // Every row in the normal look, except the one the game selects. Again a frame later,
        // in case the game picks its selection or the animators replay after this callback.
        NormalizeRows(log: first);
        if (_scrollReady)
        {
            SetScroll(0f);
            _lastSelected = null;
            ApplyClip();
        }
        Deferred.Add((this, Time.frameCount));
    }

    // The copies start in the look their native row was last left in (selected, highlighted),
    // which showed as the selection ornament on every row until the cursor had passed over it.
    // Every selectable under our rows that isn't the EventSystem's current selection is cleared
    // to Normal at once, and every selection animator under it plays its Normal state.
    private int NormalizeRows(bool log)
    {
        var eventSystem = EventSystem.current;
        var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        int reset = 0;
        var states = log ? new List<string>() : null;
        foreach (var bound in _rows)
        {
            if (bound.Object == null)
                continue;
            try
            {
                bool selectedRow = false;
                foreach (var selectable in bound.Object.GetComponentsInChildren<UISelectable>(true))
                {
                    if (current != null && selectable.gameObject.Pointer == current.Pointer)
                    {
                        selectedRow = true;
                        continue;
                    }
                    if (states != null && bound.Selectable != null && selectable.Pointer == bound.Selectable.Pointer)
                        states.Add($"{bound.Row.Label} {selectable.currentUISelectionState}");
                    selectable.InstantClearState();
                    if (selectable.currentUISelectionState != UISelectionState.Normal)
                    {
                        selectable.SetState(UISelectionState.Normal);
                        reset++;
                    }
                }
                if (selectedRow)
                {
                    states?.Add($"{bound.Row.Label} (selected by the game)");
                    continue;
                }
                foreach (var animator in bound.Object.GetComponentsInChildren<BaseUISelectableAnimator>(true))
                {
                    if (animator.enabled && animator.gameObject.activeInHierarchy)
                        animator.Play(UISelectionState.Normal);
                }
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': normal state failed: {e.Message}");
            }
        }
        if (states != null)
            BelmontUiPlugin.Logger.LogInfo(
                $"[ui] row states on first focus (selected: {(current != null ? current.name : "none")}): {string.Join(", ", states)}; {reset} set to Normal");
        return reset;
    }

    private static readonly List<(ModsTab Tab, int Frame)> Deferred = new();

    // Every frame, from the watcher: button rows (release after a press, otherwise follow the
    // row's selection look), and the second normalisation after a focus.
    internal static void RunFrameWork()
    {
        for (int i = LiveTabs.Count - 1; i >= 0; i--)
        {
            var live = LiveTabs[i];
            if (!live.Alive)
            {
                LiveTabs.RemoveAt(i);
                continue;
            }
            try
            {
                live.FrameUpdate();
            }
            catch (Exception e)
            {
                LiveTabs.RemoveAt(i);
                BelmontUiPlugin.Logger.LogWarning($"[ui] MODS tab per-frame upkeep stopped: {e.Message}");
            }
        }
        for (int i = LiveButtons.Count - 1; i >= 0; i--)
        {
            var (tab, bound) = LiveButtons[i];
            if (!tab.Alive || bound.Object == null)
            {
                LiveButtons.RemoveAt(i);
                continue;
            }
            try
            {
                if (bound.PressedUntil > 0f)
                {
                    if (Time.unscaledTime >= bound.PressedUntil)
                    {
                        bound.PressedUntil = 0f;
                        tab.Release(bound);
                    }
                    continue;
                }
                if (bound.Object.activeInHierarchy)
                    SyncButtonState(bound);
            }
            catch (Exception e)
            {
                LiveButtons.RemoveAt(i);
                BelmontUiPlugin.Logger.LogDebug($"[ui] '{bound.Row.Label}': button upkeep failed: {e.Message}");
            }
        }
        for (int i = Deferred.Count - 1; i >= 0; i--)
        {
            if (Time.frameCount <= Deferred[i].Frame)
                continue;
            var tab = Deferred[i].Tab;
            Deferred.RemoveAt(i);
            if (!tab.Alive)
                continue;
            int reset = tab.NormalizeRows(log: false);
            if (reset > 0)
                BelmontUiPlugin.Logger.LogDebug($"[ui] {reset} row selectable(s) set to Normal a frame after focus");
        }
    }

    // On the Settings screen the page sits under a higher tab bar; in the pause menu its title
    // came out right under the bar's ornament line. Everything on the page moves down by a share
    // of the page height: items placed from the top move, stretched ones (the scroll view) lose
    // that much at the top and keep their bottom, so the rows still scroll.
    private void AddTopPadding()
    {
        try
        {
            var page = _tabObject.transform.TryCast<RectTransform>();
            if (page == null || page.rect.height <= 1f)
                return;
            float pad = page.rect.height * TopPadding;
            // The content sits in the shell's holder (see BuildShell), or right under the page.
            var holder = page.Find(ContentHolderName) ?? page;
            var moved = new List<string>();
            for (int i = 0; i < holder.childCount; i++)
            {
                var child = holder.GetChild(i).TryCast<RectTransform>();
                if (child == null)
                    continue;
                if (!Mathf.Approximately(child.anchorMin.y, child.anchorMax.y))
                    child.offsetMax = new Vector2(child.offsetMax.x, child.offsetMax.y - pad);
                else
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y - pad);
                moved.Add(child.name);
            }
            BelmontUiPlugin.Logger.LogInfo($"[ui] page content moved down {pad:0} of {page.rect.height:0} units: [{string.Join(", ", moved)}]");
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] top padding failed: {e.Message}");
        }
    }

    // The settings view component that faded the copied content in is gone, and the content may
    // have been copied while its source was hidden: whatever is at alpha 0 or disabled under our
    // page is brought back when the tab shows.
    private void ForceVisible()
    {
        try
        {
            int changed = 0;
            if (!_tabObject.activeSelf)
            {
                _tabObject.SetActive(true);
                changed++;
            }
            foreach (var group in _tabObject.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (group.alpha < 1f || !group.interactable || !group.blocksRaycasts)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                    changed++;
                }
                // Never under the rows: the list and the sub-menus hide rows by deactivating them,
                // and row roots carry canvas groups (0.2.0 showed every sub-menu row on the list).
                if (!group.gameObject.activeSelf && !IsUnder(group.transform, _content))
                {
                    group.gameObject.SetActive(true);
                    changed++;
                }
            }
            var container = _tab.tabContainer;
            if (container != null && !container.isVisible && !container.isShowing)
            {
                container.InstantShow();
                changed++;
            }
            if (changed > 0)
            {
                string line = $"[ui] page shown by hand: {changed} change(s) (canvas groups / container)";
                if (_shownLogged)
                    BelmontUiPlugin.Logger.LogDebug(line);
                else
                    BelmontUiPlugin.Logger.LogInfo(line);
                _shownLogged = true;
            }
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] page show failed: {e}");
        }
    }

    // One picture of the page for LogOutput.log: where it is, how big, what is faded or disabled,
    // and what the navigation links to.
    private void LogPageState(string when)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append($"[ui] page state ({when}) on '{_host.Name}': '{_tabObject.name}' active {_tabObject.activeSelf}/{_tabObject.activeInHierarchy}, ");
            sb.Append($"{_rows.Count} rows, content '{_content.name}' has {_content.childCount} children, active {_content.gameObject.activeInHierarchy}");
            var rect = _tabObject.transform.TryCast<RectTransform>();
            if (rect != null)
                sb.Append($"\n  rect: anchors {rect.anchorMin}-{rect.anchorMax} pos {rect.anchoredPosition} size {rect.sizeDelta} rect {rect.rect.size} world {rect.position} scale {rect.lossyScale}");
            var chain = new List<string>();
            for (var t = _tabObject.transform.parent; t != null; t = t.parent)
                chain.Add($"{t.name}{(t.gameObject.activeSelf ? "" : "(off)")}");
            sb.Append($"\n  parents: {string.Join(" < ", chain)}");
            var container = _tab.tabContainer;
            if (container != null)
                sb.Append($"\n  container: '{container.name}' state {container.visibilityState} visible {container.isVisible}");
            sb.Append($"\n  default selection: {(_tab.defaultSelection != null ? _tab.defaultSelection.name : "none")}");
            if (_navigation != null)
            {
                var list = _navigation.selectables;
                sb.Append($"\n  navigation group '{_navigation.name}' on '{_navigation.gameObject.name}': {(list != null ? list.Length : 0)} selectables, wrap {_navigation.wrapAround}");
                if (list != null)
                {
                    foreach (var s in list)
                    {
                        if (s == null)
                            continue;
                        var nav = s.navigation;
                        sb.Append($"\n    {s.GetIl2CppType().Name} '{s.name}' interactable {s.interactable} enabled {s.enabled} active {s.gameObject.activeInHierarchy} " +
                            $"mode {nav.mode} up {(nav.selectOnUp != null ? nav.selectOnUp.name : "-")} down {(nav.selectOnDown != null ? nav.selectOnDown.name : "-")}");
                    }
                }
            }
            BelmontUiPlugin.Logger.LogInfo(sb.ToString());
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogWarning($"[ui] page state log failed: {e.Message}");
        }
    }

    private static string Path(Transform target, Transform root)
    {
        var names = new List<string>();
        for (var t = target; t != null && t.Pointer != root.Pointer; t = t.parent)
            names.Insert(0, t.name);
        return names.Count == 0 ? "(root)" : string.Join("/", names);
    }

    private void SaveAll()
    {
        foreach (var page in Settings.Snapshot())
        {
            try
            {
                page.Save();
            }
            catch (Exception e)
            {
                BelmontUiPlugin.Logger.LogError($"[ui] {page.Id}: save failed: {e}");
            }
        }
    }

    // ---- helpers ----

    // Prefer the Gameplay tab: plain rows, nothing special on its page.
    private static SettingsViewBase PickSource(Il2CppArrayBase<SettingTab> tabs)
    {
        SettingsViewBase any = null;
        foreach (var tab in tabs)
        {
            var view = tab?.TryCast<SettingsViewBase>();
            if (view == null || RowObjects(view.gameObject).Count == 0)
                continue;
            if (view.TryCast<GameplaySettingsView>() != null)
                return view;
            any ??= view;
        }
        return any;
    }

    // One native row of each kind, from whichever Settings tab has it, with the sound it plays
    // and the selectable its tab's navigation group moves to for it.
    private static Templates FindTemplates(Il2CppArrayBase<SettingTab> tabs)
    {
        var templates = new Templates();
        foreach (var tab in tabs)
        {
            if (tab == null)
                continue;
            if (templates.Bool == null)
            {
                var binding = tab.GetComponentInChildren<UIBoolSettingBinding>(true);
                if (binding != null)
                    templates.Bool = Describe(tab, binding, binding.onValueChangedSFX, "toggle");
            }
            if (templates.DropBool == null)
            {
                var binding = tab.GetComponentInChildren<UIDropdownBoolSettingBinding>(true);
                if (binding != null)
                    templates.DropBool = Describe(tab, binding, binding.onValueChangedSFX, "list bool");
            }
            if (templates.Float == null)
            {
                var binding = tab.GetComponentInChildren<UIFloatSettingBinding>(true);
                if (binding != null)
                    templates.Float = Describe(tab, binding, binding.onValueChangedSFX, "slider");
            }
        }
        var list = PickListTemplate(tabs);
        if (list.Binding != null)
            templates.Enum = Describe(list.Tab, list.Binding, list.Binding.onValueChangedSFX, "list");
        if (list.Binding != null)
            templates.Control = ControlRegion(list.Binding);
        if (templates.Enum != null && templates.Enum.Sfx.IsNull && templates.DropBool != null)
            templates.Enum.Sfx = templates.DropBool.Sfx;
        return templates;
    }

    // The plainest list row of all Settings tabs: the fewest graphics outside its stepper control
    // (arrows, caption). The first one found was the player outline colour row, whose colour
    // swatch showed as a white bar on every copy. Ties go to the Gameplay and Video tabs.
    private static (SettingTab Tab, UIDropdownEnumSettingBinding Binding) PickListTemplate(Il2CppArrayBase<SettingTab> tabs)
    {
        var candidates = new List<(SettingTab Tab, UIDropdownEnumSettingBinding Binding, int Stray, int Rank)>();
        foreach (var tab in tabs)
        {
            if (tab == null)
                continue;
            int rank = tab.TryCast<GameplaySettingsView>() != null || tab.TryCast<VideoSettingsView>() != null ? 0 : 1;
            foreach (var binding in tab.GetComponentsInChildren<UIDropdownEnumSettingBinding>(true))
            {
                var stepper = binding._listStepper ?? binding.GetComponentInChildren<UIListStepper>(true);
                if (stepper == null || stepper.dropdown == null)
                    continue;
                candidates.Add((tab, binding, StrayGraphics(binding.gameObject, stepper).Count, rank));
            }
        }
        if (candidates.Count == 0)
            return (null, null);
        var ordered = candidates.OrderBy(c => c.Stray).ThenBy(c => c.Rank).ToList();
        var pick = ordered[0];
        BelmontUiPlugin.Logger.LogInfo(
            $"[ui] list row template: '{pick.Binding.gameObject.name}' in '{pick.Tab.gameObject.name}', {pick.Stray} graphic(s) outside its control " +
            $"(of {candidates.Count} candidates: {string.Join(", ", ordered.Take(6).Select(c => $"{c.Binding.gameObject.name} {c.Stray}"))})");
        return (pick.Tab, pick.Binding);
    }

    // Where a list row has its control (arrows and value), as a fraction of the row's width:
    // button rows put their button there, so it lines up with the other rows' controls.
    private static Rect ControlRegion(UIDropdownEnumSettingBinding binding)
    {
        var fallback = new Rect(0.80f, 0f, 0.18f, 1f);
        try
        {
            var stepper = binding._listStepper ?? binding.GetComponentInChildren<UIListStepper>(true);
            var control = stepper?.dropdown?.transform.parent?.TryCast<RectTransform>();
            var row = binding.transform.TryCast<RectTransform>();
            if (control == null || row == null || row.rect.width <= 1f)
                return fallback;
            var min = (Vector2)row.InverseTransformPoint(control.TransformPoint(new Vector3(control.rect.xMin, control.rect.yMin, 0f)));
            var max = (Vector2)row.InverseTransformPoint(control.TransformPoint(new Vector3(control.rect.xMax, control.rect.yMax, 0f)));
            float x0 = (min.x - row.rect.xMin) / row.rect.width;
            float x1 = (max.x - row.rect.xMin) / row.rect.width;
            if (x1 <= x0 || x0 < 0f || x1 > 1.01f)
                return fallback;
            BelmontUiPlugin.Logger.LogInfo($"[ui] row control region: {x0:0.###}-{x1:0.###} of the row width (from '{binding.gameObject.name}')");
            return new Rect(x0, 0f, x1 - x0, 1f);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] control region failed: {e.Message}");
            return fallback;
        }
    }

    // Graphics of a list row that are neither text nor part of its stepper control (the parent
    // of the dropdown and arrows): a colour swatch, a preview, decorations of that one setting.
    // The selection bar and the unsaved-changes dot count too, but every row has those, so the
    // ranking is unaffected.
    private static List<Graphic> StrayGraphics(GameObject row, UIListStepper stepper)
    {
        var stray = new List<Graphic>();
        var control = stepper.dropdown != null ? stepper.dropdown.transform.parent : null;
        foreach (var graphic in row.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetIl2CppType().Name.StartsWith("TMP", StringComparison.Ordinal) || graphic.GetIl2CppType().Name == "TextMeshProUGUI")
                continue;
            if (control != null && IsUnder(graphic.transform, control))
                continue;
            stray.Add(graphic);
        }
        return stray;
    }

    private static bool IsUnder(Transform target, Transform root)
    {
        for (var t = target; t != null; t = t.parent)
        {
            if (t.Pointer == root.Pointer)
                return true;
        }
        return false;
    }

    // Whatever template was picked: plain graphics (no sprite, no texture) outside the stepper
    // control are switched off. That is the swatch kind of thing; the selection bar and its
    // corner pieces have sprites and stay.
    private static int HideStrayGraphics(GameObject row, UIListStepper stepper)
    {
        int hidden = 0;
        foreach (var graphic in StrayGraphics(row, stepper))
        {
            var image = graphic.TryCast<Image>();
            var raw = graphic.TryCast<RawImage>();
            bool plain = (image != null && image.sprite == null) || (raw != null && raw.texture == null);
            if (!plain || !graphic.enabled)
                continue;
            graphic.enabled = false;
            hidden++;
        }
        return hidden;
    }

    private static Template Describe(SettingTab tab, Component binding, EventReference sfx, string kind)
    {
        var template = new Template { Binding = binding, Sfx = sfx };
        var view = tab.TryCast<SettingsViewBase>();
        var navigation = view != null && view.navigationGroup != null ? view.navigationGroup : tab.GetComponentInChildren<VerticalNavigationGroup>(true);
        UISelectable target = null;
        var list = navigation != null ? navigation.selectables : null;
        if (list != null)
        {
            foreach (var selectable in list)
            {
                if (selectable == null)
                    continue;
                var path = PathIndices(binding.transform, selectable.transform);
                if (path != null)
                {
                    target = selectable;
                    template.NavPath = path;
                    break;
                }
            }
        }

        // What the row offers and what the game picked, once per kind.
        var sb = new StringBuilder();
        sb.Append($"[ui] native {kind} row '{binding.gameObject.name}' in '{tab.gameObject.name}': navigation group " +
            $"{(navigation != null ? $"'{navigation.gameObject.name}' with {(list != null ? list.Length : 0)} selectables" : "none")}, " +
            $"it moves to {(target != null ? $"'{target.name}' ({target.GetIl2CppType().Name}, path {string.Join("/", template.NavPath)})" : "nothing in this row")}");
        foreach (var selectable in binding.GetComponentsInChildren<Selectable>(true))
        {
            var nav = selectable.navigation;
            sb.Append($"\n    {selectable.GetIl2CppType().Name} '{Path(selectable.transform, binding.transform)}' interactable {selectable.interactable} " +
                $"enabled {selectable.enabled} active {selectable.gameObject.activeSelf} mode {nav.mode}");
        }
        BelmontUiPlugin.Logger.LogInfo(sb.ToString());
        return template;
    }

    // The game objects of the setting rows under an object: those carrying a setting binding.
    private static List<GameObject> RowObjects(GameObject root)
    {
        var rows = new List<GameObject>();
        var seen = new HashSet<int>();
        void Add(Component binding)
        {
            if (binding != null && seen.Add(binding.gameObject.GetInstanceID()))
                rows.Add(binding.gameObject);
        }
        foreach (var b in root.GetComponentsInChildren<UIBoolSettingBinding>(true)) Add(b);
        foreach (var b in root.GetComponentsInChildren<UIDropdownBoolSettingBinding>(true)) Add(b);
        foreach (var b in root.GetComponentsInChildren<UIDropdownEnumSettingBinding>(true)) Add(b);
        foreach (var b in root.GetComponentsInChildren<UIFloatSettingBinding>(true)) Add(b);
        foreach (var b in root.GetComponentsInChildren<UIIntSettingBinding>(true)) Add(b);
        foreach (var b in root.GetComponentsInChildren<UITogglegroupEnumSettingBinding>(true)) Add(b);
        return rows;
    }

    // The copied page's own texts: the tab title becomes ours, the long note (the Gameplay tab's
    // balance warning) goes, and no localizer gets to put them back.
    private static TMP_Text FixPageTexts(GameObject tabObject, string sourceTitle)
    {
        string title = Texts.TabCaption;
        int titles = 0, cleared = 0;
        TMP_Text titleText = null;
        foreach (var text in tabObject.GetComponentsInChildren<TMP_Text>(true))
        {
            foreach (var localizer in text.GetComponents<LocalizeStringEvent>())
                Object.DestroyImmediate(localizer);
            foreach (var preprocessor in text.GetComponents<InputTagTextPreprocessor>())
                Object.DestroyImmediate(preprocessor);
            string value = (text.text ?? "").Trim();
            if (value.Length == 0)
                continue;
            if (sourceTitle.Length > 0 && string.Equals(value, sourceTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                text.text = title;
                titleText ??= text;
                titles++;
            }
            else if (value.Length > LongTextLength)
            {
                text.text = "";
                cleared++;
            }
        }
        BelmontUiPlugin.Logger.LogDebug($"[ui] page texts: {titles} title(s) renamed, {cleared} note(s) cleared");
        return titleText;
    }

    // Localizers would overwrite our texts and dropdown options.
    private static void Strip(GameObject rowObject)
    {
        foreach (var localizer in rowObject.GetComponentsInChildren<LocalizeDropdown>(true))
            Object.DestroyImmediate(localizer);
        foreach (var localizer in rowObject.GetComponentsInChildren<LocalizeStringEvent>(true))
            Object.DestroyImmediate(localizer);
        foreach (var preprocessor in rowObject.GetComponentsInChildren<InputTagTextPreprocessor>(true))
            Object.DestroyImmediate(preprocessor);
    }

    // The game shows this when a value differs from the saved one; ours never do.
    private static void HideIndicator(UIContainer indicator)
    {
        if (indicator != null)
            indicator.gameObject.SetActive(false);
    }

    private static void SetLabel(TMP_Text label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private static TMP_Text SetDescription(UILocalizedString description, string hint)
    {
        if (description == null)
            return null;
        var text = description.textDisplay;
        Object.DestroyImmediate(description);
        if (text != null)
            text.text = hint ?? "";
        return text;
    }

    private static void FillOptions(UIListStepper stepper, string[] labels)
    {
        var dropdown = stepper.dropdown;
        if (dropdown == null)
            throw new InvalidOperationException("the copied list row has no dropdown");
        dropdown.ClearOptions();
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var label in labels)
            options.Add(label);
        dropdown.AddOptions(options);
        dropdown.RefreshShownValue();
    }

    // Plain images (no sprite) under the row's dropdown are switched off; the arrows have sprites
    // and stay. (The white box seen in game survived this, so SettleRows dumps the images.)
    private static void HideDropdownBackground(UIListStepper stepper)
    {
        var dropdown = stepper.dropdown;
        if (dropdown == null)
            return;
        int hidden = 0;
        foreach (var image in dropdown.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null && image.enabled)
            {
                image.enabled = false;
                hidden++;
            }
        }
        if (hidden > 0)
            BelmontUiPlugin.Logger.LogDebug($"[ui] list row: {hidden} plain image(s) under the dropdown switched off");
    }

    private static string CaptionOf(UIToggle toggle)
    {
        var text = toggle != null ? toggle.GetComponentInChildren<TMP_Text>(true) : null;
        return text != null && text.text != null ? text.text.Trim() : "";
    }

    // True for "GAMEPLAY", false for "Gameplay" (and for texts without letters).
    private static bool IsUpper(string sample) =>
        sample.Length > 0 && sample == sample.ToUpperInvariant() && sample != sample.ToLowerInvariant();

    private static void Play(EventReference sfx)
    {
        try
        {
            if (!sfx.IsNull)
                RuntimeManager.PlayOneShot(sfx, Vector3.zero);
        }
        catch (Exception e)
        {
            BelmontUiPlugin.Logger.LogDebug($"[ui] sound failed: {e.Message}");
        }
    }

    private static void Listen(UnityEvent unityEvent, Action action)
    {
        if (unityEvent == null)
            return;
        KeepAlive.Add(action);
        unityEvent.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));
    }

    private static void Listen<T>(UnityEvent<T> unityEvent, Action<T> action)
    {
        if (unityEvent == null)
            return;
        KeepAlive.Add(action);
        unityEvent.AddListener(DelegateSupport.ConvertDelegate<UnityAction<T>>(action));
    }

    private static void Skip(SettingsMenu page, SettingRow row, string why) =>
        BelmontUiPlugin.Logger.LogWarning($"[ui] {page.Id}: '{row.Label}' not shown: {why}");

    private static ModsTab Fail(ITabHost host, string why)
    {
        BelmontUiPlugin.Logger.LogWarning($"[ui] MODS tab not added to '{host.Name}': {why}");
        return null;
    }

    private static void SafeDestroy(GameObject gameObject)
    {
        try
        {
            if (gameObject != null)
                Object.Destroy(gameObject);
        }
        catch
        {
        }
    }
}
