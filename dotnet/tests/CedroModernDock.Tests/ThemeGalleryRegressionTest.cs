namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;

public class ThemeGalleryRegressionTest : IDisposable, IDockRepository
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "CedroGalleryTest_" + Guid.NewGuid().ToString("N"));
    private readonly DockModel _dock = new();

    [Fact]
    public void InvalidImageThemeCanBeInstalledShownInTheGalleryAndAppliedWithFallbacks()
    {
        string source = Path.Combine(_tempDir, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "theme.json"), """
            { "id": "bad-images", "name": "Bad images",
              "dock": { "image": { "path": "bad\u0000.png" } },
              "indicator": { "style": "image", "image": "bad\u0000.png" },
              "separator": { "style": "image", "image": "bad\u0000.png" } }
            """);
        var dock = new DockService(this);
        var themes = new ThemeService(dock, new FileSystemThemeRepository(Path.Combine(_tempDir, "Themes")));
        var appearance = new DockAppearanceService(dock);
        var services = new AppServices(dock, appearance, new DockPositioningService(dock),
            null!, null!, null!, new LocalizationService(dock), themes);
        var settings = new SettingsViewModel(services, () => { }, _ => { });

        Assert.Null(settings.InstallTheme(source));
        settings.RefreshThemes();

        var card = Assert.Single(settings.ThemeCards, t => t.Id == "bad-images");
        Assert.True(card.HasWarnings);
        Assert.True(card.IsActive);
        var surface = ThemeRenderer.BuildSurface(themes.GetActiveTheme(), appearance);
        Assert.Null(surface.Skin);
        var style = new DockVisualStyle();
        ThemeRenderer.ApplyItemStyle(themes.GetActiveTheme(), style);
        Assert.True(style.IndicatorIsShape);
        Assert.False(style.IndicatorIsImage);
        Assert.True(style.SeparatorIsLine);
    }

    [Fact]
    public void DeletingAThemeWhoseManifestWasEditedReturnsAnErrorWithoutDeletingTheFolder()
    {
        var dock = new DockService(this);
        var themes = new ThemeService(dock, new FileSystemThemeRepository(Path.Combine(_tempDir, "Themes")));
        var theme = themes.CreateFromCurrent("Mine", null);
        themes.SetActiveTheme(theme.Id);
        var services = new AppServices(dock, new DockAppearanceService(dock), new DockPositioningService(dock),
            null!, null!, null!, new LocalizationService(dock), themes);
        var settings = new SettingsViewModel(services, () => { }, _ => { });
        File.WriteAllText(Path.Combine(theme.Directory!, "theme.json"), """{ "id": "changed", "name": "Changed" }""");

        string? error = settings.DeleteTheme(theme.Id);

        Assert.NotNull(error);
        Assert.Contains("does not match", error);
        Assert.True(Directory.Exists(theme.Directory));
        Assert.Equal(theme.Id, _dock.ThemeId);
    }

    DockModel IDockRepository.Load() => _dock;
    void IDockRepository.Save(DockModel model) { }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }
}
