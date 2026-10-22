using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BelmontUi;

/// <summary>
/// A text with one version per language, for a menu's title, summary, row labels, hints, button
/// captions and choice names. The tab shows the version for the game's current language
/// (<see cref="Settings.Language"/>) and switches when the player changes language.
/// </summary>
/// <remarks>
/// <para>
/// Languages are the game's locale codes: <c>"en"</c>, <c>"fr"</c>, <c>"de"</c>, <c>"es"</c>,
/// <c>"it"</c>, <c>"pt-BR"</c>, <c>"ru"</c>, <c>"ja"</c>, <c>"ko"</c>, <c>"zh-CN"</c>, <c>"zh-TW"</c>.
/// Case and <c>_</c> / <c>-</c> don't matter, and a code without region (<c>"pt"</c>) covers every region of that language.
/// </para>
/// <para>
/// Which version shows: the exact language, then its code without region (<c>"pt"</c> for
/// <c>"pt-BR"</c>), then English (<c>"en"</c>), then the plain text given to the constructor, then
/// any version there is. Chinese Simplified and Traditional never stand in for each other: the
/// game's two Chinese fonts don't hold the same characters.
/// </para>
/// <para>
/// A plain <c>string</c> converts to a <see cref="LocalizedText"/> that shows the same text in every
/// language, and so does a <c>Dictionary&lt;string, string&gt;</c> of locale codes to texts. The game's
/// fonts hold only the characters its own texts use (a few thousand for Chinese and Japanese, about a
/// thousand for Korean), so a character outside them shows as a blank box.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var label = new LocalizedText("Opacity")
/// {
///     ["fr"] = "Opacité",
///     ["de"] = "Deckkraft",
///     ["ja"] = "不透明度",
/// };
/// menu.Slider(Plugin.Opacity, label, 0.2f, 1f, 0.05f, "{0:0%}");
/// </code>
/// </example>
public sealed class LocalizedText : IEnumerable<KeyValuePair<string, string>>
{
    private readonly Dictionary<string, string> _byLocale = new();

    /// <summary>An empty text; add versions with the indexer or <see cref="Add"/>.</summary>
    public LocalizedText()
    {
    }

    /// <summary>A text shown as given wherever no version for the current language was added.</summary>
    /// <param name="text">The plain text, usually the English one.</param>
    public LocalizedText(string text)
    {
        Text = text;
    }

    /// <summary>A text with one version per language.</summary>
    /// <param name="texts">Locale code to text, for example <c>{ ["en"] = "Opacity", ["fr"] = "Opacité" }</c>. Null adds nothing.</param>
    public LocalizedText(IDictionary<string, string> texts) : this(null, texts)
    {
    }

    /// <summary>A plain text plus one version per language.</summary>
    /// <param name="text">The plain text, shown when neither the current language nor English was added.</param>
    /// <param name="texts">Locale code to text. Null adds nothing.</param>
    public LocalizedText(string text, IDictionary<string, string> texts)
    {
        Text = text;
        if (texts == null)
            return;
        foreach (var pair in texts)
            Set(pair.Key, pair.Value);
    }

    /// <summary>The plain text given to the constructor (null when none was).</summary>
    public string Text { get; }

    /// <summary>The version for one language, exactly as added (no fallback); null when there is none. Setting it adds or replaces that version, and the tab shows the change on its next refresh.</summary>
    /// <param name="locale">A locale code such as <c>"fr"</c> or <c>"zh-CN"</c>.</param>
    public string this[string locale]
    {
        get => locale != null && _byLocale.TryGetValue(Normalize(locale), out var text) ? text : null;
        set => Set(locale, value);
    }

    /// <summary>Adds or replaces the version for one language. With this method, a collection initializer works: <c>new LocalizedText { { "en", "Opacity" }, { "fr", "Opacité" } }</c>.</summary>
    /// <param name="locale">A locale code such as <c>"fr"</c> or <c>"zh-CN"</c>.</param>
    /// <param name="text">The text in that language. Null or empty removes that version.</param>
    /// <returns>This text, for chaining.</returns>
    public LocalizedText Add(string locale, string text)
    {
        Set(locale, text);
        return this;
    }

    /// <summary>The locale codes this text has a version for, in lower case.</summary>
    public IEnumerable<string> Locales => _byLocale.Keys;

    /// <summary>The text to show for a language, with the fallbacks described on <see cref="LocalizedText"/>.</summary>
    /// <param name="locale">A locale code; null resolves as English.</param>
    /// <returns>The text, or <c>""</c> when the text is empty.</returns>
    public string Resolve(string locale)
    {
        string code = Normalize(locale ?? "en");
        if (_byLocale.TryGetValue(code, out var text))
            return text;
        int dash = code.IndexOf('-');
        if (dash > 0 && code.Substring(0, dash) != "zh" && _byLocale.TryGetValue(code.Substring(0, dash), out text))
            return text;
        if (_byLocale.TryGetValue("en", out text))
            return text;
        if (!string.IsNullOrEmpty(Text))
            return Text;
        return _byLocale.Values.FirstOrDefault() ?? "";
    }

    /// <summary>The text in the game's current language (<see cref="Settings.Language"/>).</summary>
    public string Current => Resolve(Settings.Language);

    /// <summary>True when the text has neither a plain text nor any version.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text) && _byLocale.Count == 0;

    /// <summary>The text in the game's current language.</summary>
    /// <returns><see cref="Current"/>.</returns>
    public override string ToString() => Current;

    /// <summary>A text shown the same in every language; null stays null.</summary>
    /// <param name="text">The text.</param>
    public static implicit operator LocalizedText(string text) => text == null ? null : new LocalizedText(text);

    /// <summary>A text with one version per language, from locale code to text; null stays null.</summary>
    /// <param name="texts">Locale code to text.</param>
    public static implicit operator LocalizedText(Dictionary<string, string> texts) => texts == null ? null : new LocalizedText(texts);

    /// <summary>The versions, by lower-case locale code.</summary>
    /// <returns>An enumerator over (locale, text) pairs.</returns>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _byLocale.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // A null text stays null, so callers can resolve optional texts without checks.
    internal static string Show(LocalizedText text) => text?.Current;

    internal static string Normalize(string locale) => (locale ?? "").Trim().Replace('_', '-').ToLowerInvariant();

    private void Set(string locale, string text)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("A translation needs a locale code such as \"fr\" or \"zh-CN\"", nameof(locale));
        string code = Normalize(locale);
        if (string.IsNullOrEmpty(text))
            _byLocale.Remove(code);
        else
            _byLocale[code] = text;
        Settings.Revision++;
    }
}
