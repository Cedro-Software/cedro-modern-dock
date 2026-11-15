namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Core.Themes;
using CedroModernDock.Infrastructure.Windows.Persistence;

public class ThemeServiceTest : IDisposable
{
    private static readonly string[] BuiltInIds =
    {
        "cedro", "givre", "ardoise", "cristal", "acrylique", "corail",
        "hologramme", "braise", "noyer", "velours", "aurore"
    };

    private readonly string _tempDir;
    private readonly string _themesDir;
    private readonly InMemoryDockRepository _dockRepository = new();
    private readonly DockService _dockService;
    private readonly FileSystemThemeRepository _themeRepository;

    public ThemeServiceTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CedroRepoTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _themesDir = Path.Combine(_tempDir, "Themes");
        _dockService = new DockService(_dockRepository);
        _themeRepository = new FileSystemThemeRepository(_themesDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private ThemeService NewService() => new(_dockService, _themeRepository);

    private string MakeThemeFolder(string id, string name)
    {
        string dir = Path.Combine(_tempDir, "src-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"),
            $"{{ \"id\": \"{id}\", \"name\": \"{name}\", \"dock\": {{ \"background\": \"#336699\" }} }}");
        return dir;
    }

    [Fact]
    public void LoadsAllBuiltInThemesWithoutWarningsCedroFirst()
    {
        var service = NewService();
        var themes = service.GetThemes();

        Assert.Equal(11, themes.Count);
        Assert.Equal("cedro", themes[0].Id);
        Assert.Equal(BuiltInIds.OrderBy(x => x), themes.Select(t => t.Id).OrderBy(x => x));
        Assert.All(themes, t =>
        {
            Assert.True(t.IsBuiltIn);
            Assert.Null(t.Directory);
            Assert.True(t.Warnings.Count == 0, $"{t.Id}: {string.Join(" | ", t.Warnings)}");
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
        });
        Assert.Empty(service.LoadErrors);
    }

    [Fact]
    public void DefaultActiveThemeIsCedro()
    {
        Assert.Equal("cedro", NewService().GetActiveTheme().Id);
        Assert.Equal(ThemeService.DefaultThemeId, new DockModel().ThemeId);
    }

    [Fact]
    public void GetActiveThemeFallsBackToCedroForUnknownId()
    {
        _dockService.GetDock().ThemeId = "does-not-exist";
        Assert.Equal("cedro", NewService().GetActiveTheme().Id);
    }

    [Fact]
    public void FindIsCaseInsensitive()
    {
        var service = NewService();
        Assert.Equal("givre", service.Find("GIVRE")?.Id);
        Assert.Null(service.Find("nope"));
    }

    [Fact]
    public void SetActiveThemePersistsAndRaisesThemesChanged()
    {
        var service = NewService();
        int raised = 0;
        service.ThemesChanged += () => raised++;

        service.SetActiveTheme("givre");

        Assert.Equal(1, raised);
        Assert.Equal("givre", service.GetActiveTheme().Id);
        Assert.Equal("givre", _dockRepository.SavedModel!.ThemeId);
    }

    [Fact]
    public void SetActiveThemeIgnoresUnknownIds()
    {
        var service = NewService();
        int raised = 0;
        service.ThemesChanged += () => raised++;

        service.SetActiveTheme("unknown-theme");

        Assert.Equal(0, raised);
        Assert.Equal("cedro", _dockService.GetDock().ThemeId);
    }

    [Fact]
    public void InstallAddsUserThemeAfterBuiltIns()
    {
        var service = NewService();
        int raised = 0;
        service.ThemesChanged += () => raised++;

        var installed = service.Install(MakeThemeFolder("user-theme", "User"));

        Assert.Equal("user-theme", installed.Id);
        Assert.False(installed.IsBuiltIn);
        Assert.True(raised >= 1);
        Assert.Equal(12, service.GetThemes().Count);
        Assert.Equal("user-theme", service.GetThemes()[^1].Id);
        Assert.Equal("cedro", service.GetThemes()[0].Id);
    }

    [Fact]
    public void UserThemesAreSortedByName()
    {
        var service = NewService();
        service.Install(MakeThemeFolder("zz-first", "Alpha"));
        service.Install(MakeThemeFolder("aa-second", "Beta"));
        var user = service.GetThemes().Where(t => !t.IsBuiltIn).Select(t => t.Id).ToList();
        Assert.Equal(new[] { "zz-first", "aa-second" }, user);
    }

    [Fact]
    public void InstallingABuiltInIdFails()
    {
        var service = NewService();
        foreach (var id in new[] { "cedro", "aurore" })
            Assert.Throws<ThemeFormatException>(() => service.Install(MakeThemeFolder(id, "Fake")));
        Assert.True(!Directory.Exists(_themesDir) || !Directory.EnumerateFileSystemEntries(_themesDir).Any());
        Assert.Equal(11, service.GetThemes().Count);
    }

    [Fact]
    public void UserThemeOnDiskWithBuiltInIdIsReportedAndSkipped()
    {
        string dir = Path.Combine(_themesDir, "cedro");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"), "{ \"id\": \"cedro\", \"name\": \"Impostor\" }");

        var service = NewService();

        Assert.Equal(11, service.GetThemes().Count);
        Assert.Equal("Cedro", service.Find("cedro")!.Name);
        Assert.True(service.Find("cedro")!.IsBuiltIn);
        Assert.Contains(service.LoadErrors, e => e.Contains("Impostor") && e.Contains("cedro"));
    }

    [Fact]
    public void LoadErrorsIncludeBrokenUserThemes()
    {
        string dir = Path.Combine(_themesDir, "broken");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"), "{");
        var service = NewService();
        Assert.Contains(service.LoadErrors, e => e.StartsWith("broken:"));
        Assert.Equal(11, service.GetThemes().Count);
    }

    [Fact]
    public void ReloadPicksUpThemesDroppedIntoTheFolder()
    {
        var service = NewService();
        _themeRepository.Install(MakeThemeFolder("dropped", "Dropped"));
        Assert.Null(service.Find("dropped"));
        service.Reload();
        Assert.NotNull(service.Find("dropped"));
    }

    [Fact]
    public void DeletingTheActiveUserThemeResetsToCedro()
    {
        var service = NewService();
        service.Install(MakeThemeFolder("mine", "Mine"));
        service.SetActiveTheme("mine");
        Assert.Equal("mine", service.GetActiveTheme().Id);

        Assert.True(service.Delete("mine"));

        Assert.Equal("cedro", _dockService.GetDock().ThemeId);
        Assert.Equal("cedro", _dockRepository.SavedModel!.ThemeId);
        Assert.Equal("cedro", service.GetActiveTheme().Id);
        Assert.Null(service.Find("mine"));
        Assert.False(Directory.Exists(Path.Combine(_themesDir, "mine")));
    }

    [Fact]
    public void DeletingAnInactiveUserThemeKeepsTheActiveTheme()
    {
        var service = NewService();
        service.Install(MakeThemeFolder("other", "Other"));
        service.SetActiveTheme("givre");

        Assert.True(service.Delete("other"));

        Assert.Equal("givre", service.GetActiveTheme().Id);
    }

    [Fact]
    public void BuiltInThemesCannotBeDeleted()
    {
        var service = NewService();
        service.SetActiveTheme("givre");
        foreach (var id in BuiltInIds)
            Assert.False(service.Delete(id));
        Assert.Equal(11, service.GetThemes().Count);
        Assert.Equal("givre", service.GetActiveTheme().Id);
        Assert.False(service.Delete("unknown"));
    }

    [Fact]
    public void CreateFromCurrentCreatesUniqueSluggedIds()
    {
        var service = NewService();

        var first = service.CreateFromCurrent("Mon Thème", "Moi");
        var second = service.CreateFromCurrent("Mon Thème", null);

        Assert.Equal("mon-theme", first.Id);
        Assert.Equal("Mon Thème", first.Name);
        Assert.Equal("Moi", first.Manifest.Author);
        Assert.Equal("mon-theme-2", second.Id);
        Assert.Null(second.Manifest.Author);
        Assert.Empty(first.Warnings);
        Assert.True(File.Exists(Path.Combine(_themesDir, "mon-theme", "theme.json")));
        Assert.True(File.Exists(Path.Combine(_themesDir, "mon-theme-2", "theme.json")));
        Assert.NotNull(service.Find("mon-theme"));
        Assert.NotNull(service.Find("mon-theme-2"));
        Assert.Equal("mon-theme-3", service.CreateFromCurrent("Mon Thème", null).Id);
    }

    [Fact]
    public void CreateFromCurrentAvoidsBuiltInIds()
    {
        var created = NewService().CreateFromCurrent("Cedro", null);
        Assert.Equal("cedro-2", created.Id);
    }

    [Fact]
    public void CreateFromCurrentCopiesDockSettingsWhenActiveThemeLeavesThemOpen()
    {
        var dock = _dockService.GetDock();
        dock.DockColorRGB = "10, 20, 30, ";
        dock.DockBorderRounding = 17;
        dock.DockPadding = 6;
        dock.DockTransparency = 0.4;

        var created = NewService().CreateFromCurrent("Copy", null); // active: cedro (no dock section)

        var d = created.Manifest.Dock!;
        Assert.Equal("#FF0A141E", d.Background);
        Assert.Equal(17, d.CornerRadius);
        Assert.Equal(6, d.Padding);
        Assert.Equal(0.4, d.Opacity!.Value, precision: 6);
        Assert.Equal("classic", created.Manifest.Separator?.Style);
    }

    [Fact]
    public void CreateFromCurrentCopiesTheActiveThemesValues()
    {
        var service = NewService();
        foreach (var source in service.GetThemes().ToList())
        {
            service.SetActiveTheme(source.Id);
            var created = service.CreateFromCurrent("From " + source.Id, null);
            Assert.True(created.Warnings.Count == 0, $"{source.Id}: {string.Join(" | ", created.Warnings)}");
            Assert.Equal(source.Manifest.Menu?.Accent, created.Manifest.Menu?.Accent);
            Assert.Equal(source.Manifest.Icons?.HoverZoom, created.Manifest.Icons?.HoverZoom);
            if (source.Manifest.Dock?.Gradient is { } g)
            {
                Assert.Equal(g, created.Manifest.Dock!.Gradient);
                Assert.Null(created.Manifest.Dock.Background);
            }
            if (source.Manifest.Indicator?.Style == "image") Assert.Null(created.Manifest.Indicator);
        }
    }

    [Theory]
    [InlineData("Mon Thème", "mon-theme")]
    [InlineData("  Été à Paris !  ", "ete-a-paris")]
    [InlineData("Ça_va / Très bien", "ca-va-tres-bien")]
    [InlineData("ABC 123", "abc-123")]
    [InlineData("Noël--2026", "noel-2026")]
    [InlineData("", "my-theme")]
    [InlineData("   ", "my-theme")]
    [InlineData("!!!", "my-theme")]
    [InlineData("x", "my-theme")]           // single char is not a valid id
    [InlineData("日本語", "my-theme")]
    public void SlugHandlesAccentsAndEmptyInput(string name, string expected)
    {
        Assert.Equal(expected, ThemeService.Slug(name));
    }

    [Fact]
    public void SlugTruncatesLongNamesToAValidId()
    {
        string slug = ThemeService.Slug(new string('a', 47) + " bcd " + new string('e', 30));
        Assert.True(slug.Length <= 48);
        Assert.False(slug.EndsWith('-'));
        Assert.True(ThemeParser.IsValidId(slug));
        Assert.Equal(new string('a', 47), slug);
    }

    [Fact]
    public void EnsureAuthorGuideWritesThemesMd()
    {
        var service = NewService();
        service.EnsureAuthorGuide();

        string guide = Path.Combine(_themesDir, "THEMES.md");
        Assert.True(File.Exists(guide));
        string content = File.ReadAllText(guide);
        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("theme", content, StringComparison.OrdinalIgnoreCase);

        // Overwrites a stale copy, and the guide does not break theme loading.
        File.WriteAllText(guide, "stale");
        service.EnsureAuthorGuide();
        Assert.Equal(content, File.ReadAllText(guide));
        service.Reload();
        Assert.Empty(service.LoadErrors);
    }

    [Fact]
    public void ExportDelegatesToRepository()
    {
        var service = NewService();
        service.Install(MakeThemeFolder("exp", "Exp"));
        string package = Path.Combine(_tempDir, "exp.cedrotheme");
        service.Export("exp", package);
        Assert.True(File.Exists(package));
        Assert.True(service.Delete("exp"));
        Assert.Equal("exp", service.Install(package).Id);
    }

    private sealed class InMemoryDockRepository : IDockRepository
    {
        private readonly DockModel _model = new();
        public DockModel? SavedModel { get; private set; }

        public DockModel Load()
        {
            SavedModel ??= _model;
            return _model;
        }

        public void Save(DockModel model) => SavedModel = model;
    }
}
