namespace Zephiel.Web.Infrastructure;

/// <summary>
/// Curated display-font choices for the storefront (the ZEPHIEL wordmark + big headings).
/// The chosen font is stored in the <c>appearance.display_font</c> setting; the layout loads the
/// matching Google Fonts file and sets the <c>--display-font</c> CSS variable that the
/// <c>font-bodoni</c> Tailwind utility resolves to. Admin → Settings previews these before saving.
/// </summary>
public static class DisplayFonts
{
    public record Font(string Value, string Label, string GoogleParam, string Stack);

    public const string Default = "Playfair Display";

    public static readonly IReadOnlyList<Font> All = new List<Font>
    {
        new("Playfair Display",   "Playfair Display — soft luxe serif",   "Playfair+Display:ital,wght@0,400..900;1,400..700",                 "'Playfair Display', Georgia, serif"),
        new("Cormorant Garamond", "Cormorant Garamond — light & classic", "Cormorant+Garamond:ital,wght@0,300;0,400;0,500;0,600;1,300;1,400", "'Cormorant Garamond', Georgia, serif"),
        new("Cinzel",             "Cinzel — engraved Roman capitals",     "Cinzel:wght@400;500;600;700",                                      "'Cinzel', Georgia, serif"),
        new("Fraunces",           "Fraunces — modern editorial serif",    "Fraunces:ital,opsz,wght@0,9..144,300..700;1,9..144,300..700",      "'Fraunces', Georgia, serif"),
        new("Bodoni Moda",        "Bodoni Moda — high-contrast Didone",    "Bodoni+Moda:ital,opsz,wght@0,6..96,400..900;1,6..96,400..700",     "'Bodoni Moda', Georgia, serif"),
        new("Marcellus",          "Marcellus — refined capitals",         "Marcellus",                                                        "'Marcellus', Georgia, serif"),
        new("EB Garamond",        "EB Garamond — timeless book serif",    "EB+Garamond:ital,wght@0,400..800;1,400..700",                      "'EB Garamond', Georgia, serif"),
        new("Libre Baskerville",  "Libre Baskerville — traditional",      "Libre+Baskerville:ital,wght@0,400;0,700;1,400",                    "'Libre Baskerville', Georgia, serif"),
    };

    /// <summary>The stored value → the font (falls back to the default when unknown/blank).</summary>
    public static Font Get(string? value) => All.FirstOrDefault(f => f.Value == value) ?? All[0];

    /// <summary>(Value, Label) pairs for the Admin dropdown / value validation.</summary>
    public static (string Value, string Label)[] Options => All.Select(f => (f.Value, f.Label)).ToArray();

    /// <summary>css2 link that loads a single chosen font (storefront).</summary>
    public static string GoogleLinkOne(Font f) =>
        "https://fonts.googleapis.com/css2?family=" + f.GoogleParam + "&display=swap";

    /// <summary>css2 link that loads every option at once (Admin preview).</summary>
    public static string GoogleLinkAll() =>
        "https://fonts.googleapis.com/css2?" + string.Join("&", All.Select(f => "family=" + f.GoogleParam)) + "&display=swap";
}
