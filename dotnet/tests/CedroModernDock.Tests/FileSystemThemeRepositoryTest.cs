namespace CedroModernDock.Tests;

using System.IO;
using System.IO.Compression;
using System.Text;
using CedroModernDock.Core.Themes;
using CedroModernDock.Infrastructure.Windows.Persistence;

public class FileSystemThemeRepositoryTest : IDisposable
{
    private readonly string _tempDir;
    private readonly string _themesDir;
    private readonly FileSystemThemeRepository _repository;

    public FileSystemThemeRepositoryTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CedroRepoTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _themesDir = Path.Combine(_tempDir, "Themes");
        _repository = new FileSystemThemeRepository(_themesDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private static string Manifest(string id, string name = "Test") =>
        $"{{ \"schemaVersion\": 1, \"id\": \"{id}\", \"name\": \"{name}\", \"indicator\": {{ \"style\": \"image\", \"image\": \"img/dot.png\" }} }}";

    private string MakeSourceFolder(string id, string name = "Test")
    {
        string dir = Path.Combine(_tempDir, "src-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Path.Combine(dir, "img"));
        File.WriteAllText(Path.Combine(dir, "theme.json"), Manifest(id, name));
        File.WriteAllText(Path.Combine(dir, "img", "dot.png"), "png");
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "hello");
        return dir;
    }

    private string MakeZip(params (string Name, string Content)[] entries)
    {
        string path = Path.Combine(_tempDir, "pkg-" + Guid.NewGuid().ToString("N")[..6] + FileSystemThemeRepository.PackageExtension);
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = zip.CreateEntry(name);
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            s.Write(bytes, 0, bytes.Length);
        }
        return path;
    }

    private void AssertNoStagingFolders()
    {
        if (!Directory.Exists(_themesDir)) return;
        Assert.Empty(Directory.GetDirectories(_themesDir, ".staging-*"));
    }

    private string[] AllFilesUnderTemp() => Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories);

    [Fact]
    public void LoadAllOnMissingFolderReturnsNothing()
    {
        Assert.Empty(_repository.LoadAll(out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void InstallsFromFolder()
    {
        string src = MakeSourceFolder("my-theme");
        File.WriteAllText(Path.Combine(src, "evil.exe"), "MZ");

        var theme = _repository.Install(src);

        string target = Path.Combine(_themesDir, "my-theme");
        Assert.Equal("my-theme", theme.Id);
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(theme.Directory!));
        Assert.True(File.Exists(Path.Combine(target, "theme.json")));
        Assert.True(File.Exists(Path.Combine(target, "img", "dot.png")));
        Assert.True(File.Exists(Path.Combine(target, "readme.txt")));
        Assert.False(File.Exists(Path.Combine(target, "evil.exe")));
        Assert.NotNull(theme.ResolveFile(theme.Manifest.Indicator!.Image));
        Assert.True(Directory.Exists(src)); // source left untouched
        AssertNoStagingFolders();
    }

    [Fact]
    public void InstallsFromThemeJsonFilePath()
    {
        string src = MakeSourceFolder("from-file");
        var theme = _repository.Install(Path.Combine(src, "theme.json"));
        Assert.Equal("from-file", theme.Id);
        Assert.True(File.Exists(Path.Combine(_themesDir, "from-file", "img", "dot.png")));
    }

    [Fact]
    public void InstallsZipWithThemeJsonAtRoot()
    {
        string zip = MakeZip(("theme.json", Manifest("zip-root")), ("img/dot.png", "png"), ("notes.md", "# hi"));

        var theme = _repository.Install(zip);

        string target = Path.Combine(_themesDir, "zip-root");
        Assert.Equal("zip-root", theme.Id);
        Assert.True(File.Exists(Path.Combine(target, "theme.json")));
        Assert.True(File.Exists(Path.Combine(target, "img", "dot.png")));
        Assert.True(File.Exists(Path.Combine(target, "notes.md")));
        Assert.Empty(theme.Warnings);
        AssertNoStagingFolders();
    }

    [Fact]
    public void InstallsZipWithThemeJsonInsideOneTopLevelFolder()
    {
        string zip = MakeZip(
            ("MyTheme/theme.json", Manifest("zip-nested")),
            ("MyTheme/img/dot.png", "png"),
            ("other/ignored.png", "x"),
            ("toplevel.png", "x"));

        var theme = _repository.Install(zip);

        string target = Path.Combine(_themesDir, "zip-nested");
        Assert.Equal("zip-nested", theme.Id);
        Assert.True(File.Exists(Path.Combine(target, "theme.json")));
        Assert.True(File.Exists(Path.Combine(target, "img", "dot.png")));
        Assert.False(Directory.Exists(Path.Combine(target, "MyTheme")));
        Assert.False(File.Exists(Path.Combine(target, "toplevel.png")));
        Assert.False(Directory.Exists(Path.Combine(target, "other")));
    }

    [Fact]
    public void ThemeJsonNestedTooDeepIsNotFound()
    {
        string zip = MakeZip(("a/b/theme.json", Manifest("deep")));
        Assert.Throws<ThemeFormatException>(() => _repository.Install(zip));
        Assert.False(Directory.Exists(Path.Combine(_themesDir, "deep")));
        AssertNoStagingFolders();
    }

    [Fact]
    public void ZipSlipEntriesAreIgnored()
    {
        string zip = MakeZip(
            ("x/theme.json", Manifest("slip")),
            ("x/../../evil.png", "evil"),
            ("x/../evil2.png", "evil"),
            ("x/..\\..\\evil3.png", "evil"),
            ("x/sub/../../../evil4.png", "evil"),
            ("../evil.json", "evil"),
            ("../../evil5.json", "evil"),
            ("x/C:/evil6.png", "evil"),
            ("x/ok.png", "ok"));

        var theme = _repository.Install(zip);

        Assert.Equal("slip", theme.Id);
        string target = Path.GetFullPath(Path.Combine(_themesDir, "slip"));
        Assert.True(File.Exists(Path.Combine(target, "ok.png")));
        // Nothing named evil* exists anywhere: not outside the theme folder, not inside it.
        Assert.DoesNotContain(AllFilesUnderTemp(), f => Path.GetFileName(f).StartsWith("evil"));
        // Every file of the theme lives under its folder.
        foreach (var f in Directory.GetFiles(_themesDir, "*", SearchOption.AllDirectories))
            Assert.StartsWith(target + Path.DirectorySeparatorChar, Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase);
        AssertNoStagingFolders();
    }

    [Fact]
    public void ZipSlipWithRootManifestIsIgnored()
    {
        string zip = MakeZip(("theme.json", Manifest("slip-root")), ("../evil.json", "evil"), ("a/../../evil.png", "evil"));
        _repository.Install(zip);
        Assert.DoesNotContain(AllFilesUnderTemp(), f => Path.GetFileName(f).StartsWith("evil"));
    }

    [Fact]
    public void DisallowedExtensionsAreNotExtracted()
    {
        string zip = MakeZip(
            ("theme.json", Manifest("ext")),
            ("tool.exe", "MZ"),
            ("lib.dll", "MZ"),
            ("script.ps1", "x"),
            ("run.bat", "x"),
            ("img/dot.png", "png"),
            ("img/hidden.png.exe", "MZ"));

        _repository.Install(zip);

        string target = Path.Combine(_themesDir, "ext");
        var names = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
        Assert.Contains("theme.json", names);
        Assert.Contains("dot.png", names);
        Assert.DoesNotContain("tool.exe", names);
        Assert.DoesNotContain("lib.dll", names);
        Assert.DoesNotContain("script.ps1", names);
        Assert.DoesNotContain("run.bat", names);
        Assert.DoesNotContain("hidden.png.exe", names);
    }

    [Fact]
    public void SameIdReplacesPreviousInstall()
    {
        _repository.Install(MakeZip(("theme.json", Manifest("dup", "First")), ("img/dot.png", "png"), ("old.png", "old")));
        var second = _repository.Install(MakeZip(("theme.json", Manifest("dup", "Second")), ("img/dot.png", "png")));

        Assert.Equal("Second", second.Name);
        string target = Path.Combine(_themesDir, "dup");
        Assert.False(File.Exists(Path.Combine(target, "old.png")));
        var all = _repository.LoadAll(out var errors);
        Assert.Single(all);
        Assert.Equal("Second", all[0].Name);
        Assert.Empty(errors);
    }

    [Fact]
    public void ReservedIdIsRejectedWithoutWritingAnything()
    {
        string zip = MakeZip(("theme.json", Manifest("cedro")), ("img/dot.png", "png"));
        string folder = MakeSourceFolder("cedro");
        Func<string, bool> reserved = id => id == "cedro";

        Assert.Throws<ThemeFormatException>(() => _repository.Install(zip, reserved));
        Assert.Throws<ThemeFormatException>(() => _repository.Install(folder, reserved));

        Assert.True(!Directory.Exists(_themesDir) || !Directory.EnumerateFileSystemEntries(_themesDir).Any());
    }

    [Fact]
    public void ReservedIdRejectionKeepsAnExistingInstall()
    {
        _repository.Install(MakeZip(("theme.json", Manifest("keep", "Original")), ("img/dot.png", "png")));
        Assert.Throws<ThemeFormatException>(() =>
            _repository.Install(MakeZip(("theme.json", Manifest("keep", "Other"))), id => id == "keep"));
        Assert.Equal("Original", _repository.LoadAll(out _).Single().Name);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"id\": \"Bad Id\", \"name\": \"X\" }")]
    [InlineData("{ \"id\": \"no-name\" }")]
    [InlineData("{ \"schemaVersion\": 99, \"id\": \"future\", \"name\": \"X\" }")]
    public void InvalidZipManifestThrowsAndLeavesNoStagingFolder(string manifest)
    {
        Directory.CreateDirectory(_themesDir);
        string zip = MakeZip(("theme.json", manifest), ("img/dot.png", "png"));
        Assert.Throws<ThemeFormatException>(() => _repository.Install(zip));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_themesDir));
    }

    [Fact]
    public void InvalidFolderManifestThrowsAndLeavesNoStagingFolder()
    {
        Directory.CreateDirectory(_themesDir);
        string src = MakeSourceFolder("ok");
        File.WriteAllText(Path.Combine(src, "theme.json"), "{ broken");
        Assert.Throws<ThemeFormatException>(() => _repository.Install(src));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_themesDir));
    }

    [Fact]
    public void NonZipFileThrowsThemeFormatException()
    {
        Directory.CreateDirectory(_themesDir);
        string path = Path.Combine(_tempDir, "fake.cedrotheme");
        File.WriteAllText(path, "this is not a zip");
        Assert.Throws<ThemeFormatException>(() => _repository.Install(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_themesDir));
    }

    [Fact]
    public void ZipWithoutManifestThrows()
    {
        Directory.CreateDirectory(_themesDir);
        Assert.Throws<ThemeFormatException>(() => _repository.Install(MakeZip(("img/dot.png", "png"))));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_themesDir));
    }

    [Fact]
    public void FolderWithoutManifestThrows()
    {
        string dir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(dir);
        Assert.Throws<ThemeFormatException>(() => _repository.Install(dir));
    }

    [Fact]
    public void MissingPathThrows()
    {
        Assert.Throws<ThemeFormatException>(() => _repository.Install(Path.Combine(_tempDir, "nope.zip")));
    }

    [Fact]
    public void TooManyEntriesIsRejected()
    {
        var entries = new List<(string, string)> { ("theme.json", Manifest("big")) };
        for (int i = 0; i < 200; i++) entries.Add(($"f{i}.txt", "x"));
        Directory.CreateDirectory(_themesDir);
        Assert.Throws<ThemeFormatException>(() => _repository.Install(MakeZip(entries.ToArray())));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_themesDir));
    }

    [Fact]
    public void DeleteRemovesInstalledTheme()
    {
        _repository.Install(MakeSourceFolder("to-delete"));
        Assert.True(_repository.Delete("to-delete"));
        Assert.False(Directory.Exists(Path.Combine(_themesDir, "to-delete")));
        Assert.False(_repository.Delete("to-delete"));
        Assert.Empty(_repository.LoadAll(out _));
    }

    [Fact]
    public void DeleteRefusesInvalidIds()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "victim"));
        Directory.CreateDirectory(_themesDir);
        Assert.False(_repository.Delete("../victim"));
        Assert.False(_repository.Delete(".."));
        Assert.False(_repository.Delete(""));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "victim")));
        Assert.True(Directory.Exists(_themesDir));
    }

    [Fact]
    public void SaveAndLoadAllRoundTrip()
    {
        var manifest = new ThemeManifest
        {
            Id = "saved",
            Name = "Saved Théme",
            Author = "Me",
            Dock = new ThemeDockSection { Background = "#FF102030", Opacity = 0.5, CornerRadius = 12, Gradient = new() { "#FF000000", "#FFFFFFFF" } },
            Indicator = new ThemeIndicatorSection { Style = "pill", Color = "#FFFF0000", Size = 5 },
            Menu = new ThemeMenuSection { Accent = "#FF00FF00" }
        };

        var saved = _repository.Save(manifest);
        Assert.Equal("saved", saved.Id);
        Assert.Empty(saved.Warnings);

        var all = _repository.LoadAll(out var errors);
        Assert.Empty(errors);
        var loaded = Assert.Single(all);
        Assert.Equal("saved", loaded.Id);
        Assert.Equal("Saved Théme", loaded.Name);
        Assert.Equal("Me", loaded.Manifest.Author);
        Assert.Equal("#FF102030", loaded.Manifest.Dock!.Background);
        Assert.Equal(0.5, loaded.Manifest.Dock.Opacity);
        Assert.Equal(12, loaded.Manifest.Dock.CornerRadius);
        Assert.Equal(new List<string> { "#FF000000", "#FFFFFFFF" }, loaded.Manifest.Dock.Gradient);
        Assert.Equal("pill", loaded.Manifest.Indicator!.Style);
        Assert.Equal(5, loaded.Manifest.Indicator.Size);
        Assert.Equal("#FF00FF00", loaded.Manifest.Menu!.Accent);
        Assert.Null(loaded.Manifest.Tooltip);
        Assert.False(loaded.IsBuiltIn);
        Assert.Equal(Path.GetFullPath(Path.Combine(_themesDir, "saved")), Path.GetFullPath(loaded.Directory!));

        // Nulls are not written; non-ASCII text is kept readable.
        string json = File.ReadAllText(Path.Combine(_themesDir, "saved", "theme.json"));
        Assert.DoesNotContain("null", json);
        Assert.Contains("Théme", json);
    }

    [Fact]
    public void SaveRejectsInvalidId()
    {
        Assert.Throws<ThemeFormatException>(() => _repository.Save(new ThemeManifest { Id = "../x", Name = "X" }));
        Assert.Throws<ThemeFormatException>(() => _repository.Save(new ThemeManifest { Id = "", Name = "X" }));
    }

    [Fact]
    public void ExportProducesAPackageThatInstallAccepts()
    {
        _repository.Install(MakeSourceFolder("exported", "Exported"));
        string package = Path.Combine(_tempDir, "out" + FileSystemThemeRepository.PackageExtension);
        File.WriteAllText(package, "stale"); // overwritten

        _repository.Export("exported", package);

        using (var zip = ZipFile.OpenRead(package))
            Assert.Contains(zip.Entries, e => e.FullName.Replace('\\', '/') == "exported/theme.json");

        Assert.True(_repository.Delete("exported"));
        var reinstalled = _repository.Install(package);
        Assert.Equal("exported", reinstalled.Id);
        Assert.Equal("Exported", reinstalled.Name);
        Assert.True(File.Exists(Path.Combine(_themesDir, "exported", "img", "dot.png")));
        Assert.NotNull(reinstalled.ResolveFile("img/dot.png"));
    }

    [Fact]
    public void ExportOfUnknownThemeThrows()
    {
        Assert.Throws<ThemeFormatException>(() => _repository.Export("nope", Path.Combine(_tempDir, "x.zip")));
    }

    [Fact]
    public void LoadAllReportsBrokenThemes()
    {
        _repository.Install(MakeSourceFolder("good", "Good"));
        string broken = Path.Combine(_themesDir, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "theme.json"), "{ nope");
        string noName = Path.Combine(_themesDir, "noname");
        Directory.CreateDirectory(noName);
        File.WriteAllText(Path.Combine(noName, "theme.json"), "{ \"id\": \"noname\" }");
        Directory.CreateDirectory(Path.Combine(_themesDir, "no-manifest")); // ignored silently
        File.WriteAllText(Path.Combine(_themesDir, "THEMES.md"), "# guide"); // loose file ignored

        var themes = _repository.LoadAll(out var errors);

        Assert.Equal("good", Assert.Single(themes).Id);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.StartsWith("broken:"));
        Assert.Contains(errors, e => e.StartsWith("noname:"));
    }

    [Fact]
    public void LoadAllRejectsOversizedManifest()
    {
        string dir = Path.Combine(_themesDir, "huge");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.json"),
            "{ \"id\": \"huge\", \"name\": \"H\", \"description\": \"" + new string('x', 300 * 1024) + "\" }");
        Assert.Empty(_repository.LoadAll(out var errors));
        Assert.Contains(errors, e => e.StartsWith("huge:") && e.Contains("too large"));
    }

    [Fact]
    public void InstallingFromTheInstalledFolderIsANoOp()
    {
        var first = _repository.Install(MakeSourceFolder("in-place"));
        var again = _repository.Install(first.Directory!);
        Assert.Equal("in-place", again.Id);
        Assert.True(File.Exists(Path.Combine(_themesDir, "in-place", "theme.json")));
    }
}
