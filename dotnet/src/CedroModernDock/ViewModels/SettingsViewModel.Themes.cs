using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CedroModernDock.Core.Themes;

namespace CedroModernDock.ViewModels;

/// <summary>One card of the theme gallery, with a miniature dock preview.</summary>
public sealed class ThemeCardViewModel : ViewModelBase
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public string Byline { get; }
    public bool IsBuiltIn { get; }
    public bool IsUserTheme => !IsBuiltIn;
    public string? Warnings { get; }
    public bool HasWarnings => Warnings != null;

    // Miniature dock
    public IBrush PreviewBackground { get; }
    public IBrush PreviewBorderBrush { get; }
    public Thickness PreviewBorderThickness { get; }
    public CornerRadius PreviewCornerRadius { get; }
    public BoxShadows PreviewShadow { get; }
    public IBrush? PreviewHighlight { get; }
    public bool HasPreviewHighlight => PreviewHighlight != null;
    public IBrush PreviewIndicator { get; }
    public CornerRadius PreviewIndicatorRadius { get; }
    public double PreviewIndicatorWidth { get; }
    public double PreviewIndicatorHeight { get; }
    public bool PreviewHasIndicator { get; }
    public IBrush PreviewAccent { get; }

    private bool _isActive;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

    public ThemeCardViewModel(LoadedTheme theme, DockSurface surface, bool isActive, string builtInLabel)
    {
        var m = theme.Manifest;
        Id = theme.Id;
        Name = theme.Name;
        Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description;
        IsBuiltIn = theme.IsBuiltIn;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Author)) parts.Add(m.Author!);
        if (!string.IsNullOrWhiteSpace(m.Version)) parts.Add("v" + m.Version);
        if (theme.IsBuiltIn) parts.Add(builtInLabel);
        Byline = string.Join(" · ", parts);
        Warnings = theme.Warnings.Count > 0 ? string.Join("\n", theme.Warnings) : null;
        _isActive = isActive;

        // The preview is ~40% of a real dock: scale radius and shadow alike.
        const double k = 0.45;
        PreviewBackground = surface.Background;
        PreviewBorderBrush = surface.BorderBrush;
        PreviewBorderThickness = new Thickness(Math.Min(surface.BorderThickness, 2));
        PreviewCornerRadius = new CornerRadius(Math.Round(surface.CornerRadius * k + (surface.CornerRadius > 0 ? 1 : 0)));
        PreviewShadow = surface.Shadow.Count > 0
            ? new BoxShadows(new BoxShadow
            {
                Blur = surface.Shadow[0].Blur * k, OffsetY = surface.Shadow[0].OffsetY * k,
                Spread = surface.Shadow[0].Spread * k, Color = surface.Shadow[0].Color
            })
            : default;
        PreviewHighlight = surface.Highlight;

        var ind = m.Indicator;
        string style = ind?.Style ?? "dot";
        double size = Math.Max(2.5, (ind?.Size ?? 4) * 0.75);
        PreviewHasIndicator = style is not "none";
        PreviewIndicator = new SolidColorBrush(ThemeRenderer.ParseOr(ind?.Color, Colors.White));
        (PreviewIndicatorWidth, PreviewIndicatorHeight) = style switch
        {
            "line" => (10.0, Math.Max(2, size * 0.6)),
            "pill" => (size * 3, size),
            _ => (size, size)
        };
        PreviewIndicatorRadius = new CornerRadius(Math.Min(PreviewIndicatorWidth, PreviewIndicatorHeight) / 2);
        PreviewAccent = new SolidColorBrush(surface.Accent);
    }
}

public partial class SettingsViewModel
{
    public ObservableCollection<ThemeCardViewModel> ThemeCards { get; } = new();

    private string? _themeErrors;
    /// <summary>Installed themes that could not be loaded (shown under the gallery).</summary>
    public string? ThemeErrors
    {
        get => _themeErrors;
        private set { if (SetProperty(ref _themeErrors, value)) OnPropertyChanged(nameof(HasThemeErrors)); }
    }
    public bool HasThemeErrors => ThemeErrors != null;

    private string? _themeOverrideNotice;
    /// <summary>Appearance page notice listing the settings the active theme imposes.</summary>
    public string? ThemeOverrideNotice
    {
        get => _themeOverrideNotice;
        private set { if (SetProperty(ref _themeOverrideNotice, value)) OnPropertyChanged(nameof(HasThemeOverrideNotice)); }
    }
    public bool HasThemeOverrideNotice => ThemeOverrideNotice != null;

    private string _activeThemeName = "";
    public string ActiveThemeName { get => _activeThemeName; private set => SetProperty(ref _activeThemeName, value); }

    public string ThemesFolder => _appServices.ThemeService.ThemesDirectory;

    // Appearance settings the active theme leaves to the user (others are disabled).
    private bool _canEditDockColor = true, _canEditTransparency = true, _canEditRounding = true, _canEditPadding = true;
    public bool CanEditDockColor { get => _canEditDockColor; private set => SetProperty(ref _canEditDockColor, value); }
    public bool CanEditTransparency { get => _canEditTransparency; private set => SetProperty(ref _canEditTransparency, value); }
    public bool CanEditRounding { get => _canEditRounding; private set => SetProperty(ref _canEditRounding, value); }
    public bool CanEditPadding { get => _canEditPadding; private set => SetProperty(ref _canEditPadding, value); }

    public string TabItems => L("settings.tab.items", "Dock items");

    public void RefreshThemes()
    {
        var service = _appServices.ThemeService;
        var active = service.GetActiveTheme();
        ActiveThemeName = active.Name;
        string builtIn = L("settings.themes.builtIn", "Built-in");

        ThemeCards.Clear();
        foreach (var theme in service.GetThemes())
        {
            var surface = ThemeRenderer.BuildSurface(theme, _appServices.AppearanceService);
            ThemeCards.Add(new ThemeCardViewModel(theme, surface, theme.Id == active.Id, builtIn));
        }
        ThemeErrors = service.LoadErrors.Count > 0 ? string.Join("\n", service.LoadErrors) : null;

        var dock = active.Manifest.Dock;
        bool ownsColor = dock?.Background != null || dock?.Gradient != null;
        CanEditDockColor = !ownsColor;
        CanEditTransparency = !(ownsColor || dock?.Opacity != null);
        CanEditRounding = dock?.CornerRadius == null;
        CanEditPadding = dock?.Padding == null;
        var imposed = new List<string>();
        if (!CanEditDockColor) imposed.Add(L("settings.themes.override.color", "background color"));
        if (!CanEditTransparency) imposed.Add(L("settings.themes.override.transparency", "transparency"));
        if (!CanEditRounding) imposed.Add(L("settings.themes.override.rounding", "rounding"));
        if (!CanEditPadding) imposed.Add(L("settings.themes.override.padding", "inner margin"));
        ThemeOverrideNotice = imposed.Count == 0
            ? null
            : string.Format(L("settings.themes.override.notice",
                "The \"{0}\" theme sets: {1}. Choose the Cedro theme to customize everything."),
                active.Name, string.Join(", ", imposed));
    }

    public void ApplyTheme(string id) => _appServices.ThemeService.SetActiveTheme(id);

    /// <summary>Installs a package and activates it. Returns an error message, or null.</summary>
    public string? InstallTheme(string path)
    {
        try
        {
            ThemeRenderer.ClearCache();
            var theme = _appServices.ThemeService.Install(path);
            _appServices.ThemeService.SetActiveTheme(theme.Id);
            return null;
        }
        catch (Exception e) when (e is ThemeFormatException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return e.Message;
        }
    }

    public string? DeleteTheme(string id)
    {
        try
        {
            return _appServices.ThemeService.Delete(id) ? null
                : L("settings.themes.delete.unavailable", "The theme is no longer available. Refresh the theme list and try again.");
        }
        catch (Exception e) when (e is ThemeFormatException or IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    public string? ExportTheme(string id, string destination)
    {
        try
        {
            _appServices.ThemeService.Export(id, destination);
            return null;
        }
        catch (Exception e) when (e is ThemeFormatException or IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    public string CreateThemeFromCurrent(string name)
    {
        // No author: the Windows account name must not end up in shared themes.
        var theme = _appServices.ThemeService.CreateFromCurrent(name, author: null);
        _appServices.ThemeService.SetActiveTheme(theme.Id);
        return theme.Directory ?? ThemesFolder;
    }

    public void ReloadThemes()
    {
        ThemeRenderer.ClearCache();
        _appServices.ThemeService.Reload();
    }

    private void OnThemesChanged()
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) RefreshThemes();
        else Avalonia.Threading.Dispatcher.UIThread.Post(RefreshThemes);
    }

    // --- Theme page texts ---
    public string TabThemes => L("settings.tab.themes", "Themes");
    public string ThemesSubtitle => L("settings.themes.subtitle",
        "Change the whole look of the dock in one click. Themes made by other people can be imported as .cedrotheme or .zip files.");
    public string ImportThemeText => L("settings.themes.import", "Import a theme...");
    public string CreateThemeText => L("settings.themes.create", "Create from my settings");
    public string OpenThemesFolderText => L("settings.themes.openFolder", "Open themes folder");
    public string ReloadThemesText => L("settings.themes.reload", "Refresh");
    public string ExportThemeText => L("settings.themes.export", "Export");
    public string DeleteThemeText => L("settings.themes.delete", "Delete");
    public string ActiveBadgeText => L("settings.themes.active", "Active");
    public string ThemeErrorsTitle => L("settings.themes.errors", "Some installed themes could not be loaded:");
    public string AuthorsTitle => L("settings.themes.authors.title", "Make your own theme");
    public string AuthorsHelper => L("settings.themes.authors.helper",
        "A theme is a folder with a theme.json file (colors, gradient, rounding, shadow, indicator, separators, menus) and optional images, such as a 9-slice dock skin. \"Create from my settings\" writes a ready-to-edit theme in the themes folder, along with the THEMES.md guide. Zip the folder and rename it .cedrotheme to share it.");
}
