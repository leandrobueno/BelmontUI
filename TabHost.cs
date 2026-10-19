using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Events;

namespace BelmontUi;

// The tab bar the Mods Settings tab lives in: the pause menu's (MAP, EQUIPMENT, TAROT, JOURNEY,
// SYSTEM; BasicTab tabs) when there is one, else the Settings screen's (CONTROLS ... LANGUAGE;
// SettingTab tabs) on the title screen. Both are the game's UITabView<T>; this hides the T.
internal interface ITabHost
{
    string Name { get; }
    bool Alive { get; }
    bool Initialized { get; }
    bool IsPauseMenu { get; }
    bool MenuShowing { get; }
    Transform Transform { get; }
    UnityEvent OnCloseView { get; }
    UITabViewBase View { get; }
    List<UITab_BS> Tabs();
    void SetTabs(List<UITab_BS> tabs);
    UITab_BS AddTabComponent(GameObject page);
    void GoTo(UITab_BS tab);
    void UpdateActiveTabCount();
}

internal sealed class TabHost<TView, TTab> : ITabHost
    where TView : UITabView<TTab>
    where TTab : UITab_BS
{
    private readonly TView _view;
    private readonly Func<bool> _showing;

    internal TabHost(TView view, bool isPauseMenu, Func<bool> showing = null)
    {
        _view = view;
        IsPauseMenu = isPauseMenu;
        _showing = showing;
    }

    public string Name => _view.gameObject.name;
    public bool Alive => _view != null && _view.gameObject != null;
    public bool Initialized => _view._initialized;
    public bool IsPauseMenu { get; }
    public bool MenuShowing => _showing != null ? _showing() : _view._isOpen;
    public Transform Transform => _view.transform;
    public UnityEvent OnCloseView => _view.onCloseTabView;
    public UITabViewBase View => _view;

    public List<UITab_BS> Tabs()
    {
        var list = new List<UITab_BS>();
        var tabs = _view.tabs;
        if (tabs == null)
            return list;
        foreach (var tab in tabs)
        {
            if (tab != null)
                list.Add(tab);
        }
        return list;
    }

    public void SetTabs(List<UITab_BS> tabs)
    {
        var typed = new TTab[tabs.Count];
        for (int i = 0; i < tabs.Count; i++)
            typed[i] = tabs[i].TryCast<TTab>();
        _view.tabs = new Il2CppReferenceArray<TTab>(typed);
    }

    public UITab_BS AddTabComponent(GameObject page) => page.AddComponent<TTab>();

    public void GoTo(UITab_BS tab)
    {
        var typed = tab.TryCast<TTab>();
        if (typed != null)
            _view.GoToTab(typed);
    }

    public void UpdateActiveTabCount() => _view.UpdateActiveTabCount();
}
