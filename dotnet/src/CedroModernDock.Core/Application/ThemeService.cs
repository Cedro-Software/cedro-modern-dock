namespace CedroModernDock.Core.Application;

using System.Reflection;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Themes;

/// <summary>
/// Catalog of dock themes: built-in ones (embedded in this assembly) plus
/// third-party themes installed in the user's themes folder. Tracks the
/// active theme, persisted in config.json as <c>themeId</c>.
/// </summary>
public class ThemeService
{
    /// <summary>The original Cedro look: every value comes from the user's settings.</summary>
    public const string DefaultThemeId = "cedro";

    private readonly DockService _dockService;
    private readonly IThemeRepository _repository;
    private readonly List<LoadedTheme> _builtIn;
    private List<LoadedTheme> _themes = new();

    /// <summary>Raised after the active theme or the catalog changed.</summary>
    public event Action? ThemesChanged;

    /// <summary>Problems found during the last scan (one line per broken theme).</summary>
    public IReadOnlyList<string> LoadErrors { get; private set; } = Array.Empty<string>();

    public ThemeService(DockService dockService, IThemeRepository repository)
    {
        _dockService = dockService;
        _repository = repository;
        _builtIn = LoadBuiltInThemes();
        Reload();
    }

    public string ThemesDirectory => _repository.ThemesDirectory;

    public IReadOnlyList<LoadedTheme> GetThemes() => _themes;

    public LoadedTheme GetActiveTheme()
    {
        string id = _dockService.GetDock().ThemeId;
        return Find(id) ?? Find(DefaultThemeId) ?? _themes[0];
    }

    public LoadedTheme? Find(string id)
        => _themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public void SetActiveTheme(string id)
    {
        if (Find(id) == null) return;
        _dockService.GetDock().ThemeId = id;
        _dockService.SaveChanges();
        ThemesChanged?.Invoke();
    }

    /// <summary>Rescans the themes folder (e.g. after the user dropped a folder in it).</summary>
    public void Reload()
    {
        var user = _repository.LoadAll(out var errors);
        var list = new List<LoadedTheme>(_builtIn);
        var errorList = new List<string>(errors);
        foreach (var theme in user.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (list.Any(t => string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase)))
            {
                errorList.Add($"{theme.Name}: id '{theme.Id}' is already used by another theme.");
                continue;
            }
            list.Add(theme);
        }
        _themes = list;
        LoadErrors = errorList;
        ThemesChanged?.Invoke();
    }

    public LoadedTheme Install(string packagePath)
    {
        // Built-in ids are reserved: reject before anything is written.
        var installed = _repository.Install(packagePath, IsReservedId);
        Reload();
        return Find(installed.Id) ?? installed;
    }

    private bool IsReservedId(string id)
        => _builtIn.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool Delete(string id)
    {
        var theme = Find(id);
        if (theme == null || theme.IsBuiltIn) return false;
        bool wasActive = string.Equals(GetActiveTheme().Id, id, StringComparison.OrdinalIgnoreCase);
        bool deleted = _repository.Delete(id);
        if (wasActive)
        {
            _dockService.GetDock().ThemeId = DefaultThemeId;
            _dockService.SaveChanges();
        }
        Reload();
        return deleted;
    }

    public void Export(string id, string destinationFile) => _repository.Export(id, destinationFile);

    /// <summary>
    /// Writes the theme format guide (THEMES.md, embedded in this assembly) into
    /// the themes folder, so authors find the documentation next to their themes.
    /// </summary>
    public void EnsureAuthorGuide()
    {
        Directory.CreateDirectory(ThemesDirectory);
        string target = Path.Combine(ThemesDirectory, "THEMES.md");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CedroModernDock.Core.THEMES.md");
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();
        if (!File.Exists(target) || File.ReadAllText(target) != content)
            File.WriteAllText(target, content);
    }

    /// <summary>
    /// Creates an editable theme from the current dock settings and the active
    /// theme, so theme authors start from what they see. Returns the new theme.
    /// </summary>
    public LoadedTheme CreateFromCurrent(string name, string? author)
    {
        var dock = _dockService.GetDock();
        var active = GetActiveTheme().Manifest;
        string baseId = Slug(name);
        string id = baseId;
        for (int i = 2; Find(id) != null; i++) id = $"{baseId}-{i}";

        var color = ThemeColor.TryParse(dock.DockColorRGB, out var c) ? c : ThemeColor.FromRgb(0, 0, 0);
        var manifest = new ThemeManifest
        {
            Id = id,
            Name = name,
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            Version = "1.0.0",
            Description = "",
            Dock = new ThemeDockSection
            {
                Background = active.Dock?.Gradient == null ? active.Dock?.Background ?? color.ToHex() : null,
                Gradient = active.Dock?.Gradient,
                GradientAngle = active.Dock?.GradientAngle,
                // Keep the look the user sees: a theme-owned background has no
                // slider transparency; the user's color comes with the slider value.
                Opacity = active.Dock?.Opacity
                          ?? (active.Dock?.Background != null || active.Dock?.Gradient != null
                              ? null
                              : dock.DockTransparency),
                CornerRadius = active.Dock?.CornerRadius ?? dock.DockBorderRounding,
                Padding = active.Dock?.Padding ?? dock.DockPadding,
                BorderColor = active.Dock?.BorderColor,
                BorderThickness = active.Dock?.BorderThickness,
                Highlight = active.Dock?.Highlight,
                Shadow = active.Dock?.Shadow
            },
            Icons = active.Icons,
            // Image-based parts reference files of the source theme: not copied.
            Indicator = active.Indicator is { Style: "image" } ? null : active.Indicator,
            Separator = active.Separator is { Style: "image" } ? null : active.Separator,
            Tooltip = active.Tooltip,
            Menu = active.Menu
        };
        var saved = _repository.Save(manifest);
        try { EnsureAuthorGuide(); } catch (IOException) { /* the guide is a convenience */ }
        Reload();
        return Find(saved.Id) ?? saved;
    }

    public static string Slug(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                         != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        string slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > 48) slug = slug[..48].Trim('-');
        return ThemeParser.IsValidId(slug) ? slug : "my-theme";
    }

    private static List<LoadedTheme> LoadBuiltInThemes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var themes = new List<LoadedTheme>();
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Themes.BuiltIn.") && n.EndsWith(".json"))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            themes.Add(ThemeParser.Parse(reader.ReadToEnd(), null, isBuiltIn: true));
        }
        return themes;
    }
}
