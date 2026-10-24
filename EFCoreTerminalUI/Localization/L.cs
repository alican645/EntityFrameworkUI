using System.Globalization;

namespace EFCoreTerminalUI.Localization;

public sealed record Language(string Code, string NativeName, Strings Strings);

/// <summary>Etkin dil ve desteklenen diller.</summary>
public static class L
{
    public static readonly IReadOnlyList<Language> Languages =
    [
        new("en", "English", new Strings()),
        new("tr", "Türkçe", Translations.Turkish),
        new("de", "Deutsch", Translations.German),
        new("es", "Español", Translations.Spanish),
        new("fr", "Français", Translations.French),
    ];

    public static Language Current { get; private set; } = Languages[0];

    /// <summary>Etkin dildeki metinler.</summary>
    public static Strings T => Current.Strings;

    public static Language? Find(string? code) =>
        code is null ? null : Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sistem diline (CurrentUICulture / LANG) en yakın desteklenen dil, yoksa İngilizce.</summary>
    public static Language Detect()
    {
        var fromCulture = Find(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        if (fromCulture is not null) return fromCulture;

        // Invariant culture: macOS/Linux'ta LC_ALL / LANG değişkenine bak (ör. tr_TR.UTF-8)
        var env = Environment.GetEnvironmentVariable("LC_ALL") ?? Environment.GetEnvironmentVariable("LANG") ?? "";
        return Find(env.Split('_', '.', '-')[0]) ?? Languages[0];
    }

    public static void Set(Language language) => Current = language;
}
