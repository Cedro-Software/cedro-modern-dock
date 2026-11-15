namespace CedroModernDock.Tests;

using System.IO;
using System.Text.Json;
using CedroModernDock.Core.Themes;

public class ThemeParserTest : IDisposable
{
    [Theory]
    [InlineData("bad\0.png")]
    [InlineData("bad\n.png")]
    [InlineData("bad<name.png")]
    [InlineData("bad>name.png")]
    [InlineData("bad\"name.png")]
    [InlineData("bad|name.png")]
    [InlineData("bad?name.png")]
    [InlineData("bad*name.png")]
    [InlineData("folder. /image.png")]
    [InlineData("folder./image.png")]
    public void DropsInvalidWindowsImagePathsAndResolveFileDoesNotThrow(string path)
    {
        string jsonPath = JsonSerializer.Serialize(path);
        var theme = Parse($"\"dock\": {{ \"image\": {{ \"path\": {jsonPath} }} }}, " +
            $"\"indicator\": {{ \"style\": \"image\", \"image\": {jsonPath} }}, " +
            $"\"separator\": {{ \"style\": \"image\", \"image\": {jsonPath}, \"imageVertical\": {jsonPath} }}", _tempDir);

        Assert.Null(theme.Manifest.Dock!.Image);
        Assert.Null(theme.Manifest.Indicator!.Image);
        Assert.Equal("dot", theme.Manifest.Indicator.Style);
        Assert.Null(theme.Manifest.Separator!.Image);
        Assert.Null(theme.Manifest.Separator.ImageVertical);
        Assert.Equal("line", theme.Manifest.Separator.Style);
        Assert.Contains(theme.Warnings, w => w.StartsWith("dock.image.path:"));
        Assert.Null(theme.ResolveFile(path));
    }

    [Fact]
    public void ResolveFileReturnsNullWhenTheThemeDirectoryCannotBeNormalized()
    {
        var theme = Parse("", "bad\0directory");
        Assert.Null(theme.ResolveFile("image.png"));
    }

    [Theory]
    [InlineData("trailing.")]
    [InlineData("trailing\n")]
    public void RejectsIdsThatAliasOrContainInvalidWindowsDirectoryNames(string id)
    {
        Assert.False(ThemeParser.IsValidId(id));
        Assert.Throws<ThemeFormatException>(() => ThemeParser.Parse(
            $"{{ \"id\": {JsonSerializer.Serialize(id)}, \"name\": \"Invalid\" }}", null, false));
    }

    private readonly string _tempDir;

    public ThemeParserTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CedroRepoTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private static LoadedTheme Parse(string body, string? dir = null)
        => ThemeParser.Parse("{ \"id\": \"test-theme\", \"name\": \"Test\"" + (body.Length > 0 ? ", " + body : "") + " }", dir, isBuiltIn: false);

    [Fact]
    public void ParsesMinimalTheme()
    {
        var t = Parse("");
        Assert.Equal("test-theme", t.Id);
        Assert.Equal("Test", t.Name);
        Assert.Empty(t.Warnings);
        Assert.False(t.IsBuiltIn);
        Assert.Null(t.Manifest.Dock);
    }

    [Fact]
    public void ToleratesCommentsTrailingCommasAndCaseInsensitiveNames()
    {
        var t = ThemeParser.Parse("{ // comment\n \"ID\": \"abc\", \"Name\": \"X\", }", null, true);
        Assert.Equal("abc", t.Id);
        Assert.True(t.IsBuiltIn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]                // too short
    [InlineData("Upper")]
    [InlineData("-leading")]
    [InlineData("has space")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\\\b")]
    public void RejectsInvalidId(string id)
    {
        var ex = Assert.Throws<ThemeFormatException>(() =>
            ThemeParser.Parse($"{{ \"id\": \"{id}\", \"name\": \"X\" }}", null, false));
        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void RejectsTooLongId()
    {
        string id = new('a', 65);
        Assert.Throws<ThemeFormatException>(() => ThemeParser.Parse($"{{ \"id\": \"{id}\", \"name\": \"X\" }}", null, false));
        Assert.Equal(new string('a', 64), ThemeParser.Parse($"{{ \"id\": \"{new string('a', 64)}\", \"name\": \"X\" }}", null, false).Id);
    }

    [Fact]
    public void RejectsMissingId()
    {
        Assert.Throws<ThemeFormatException>(() => ThemeParser.Parse("{ \"name\": \"X\" }", null, false));
    }

    [Theory]
    [InlineData("{ \"id\": \"abc\" }")]
    [InlineData("{ \"id\": \"abc\", \"name\": \"\" }")]
    [InlineData("{ \"id\": \"abc\", \"name\": \"   \" }")]
    public void RejectsMissingName(string json)
    {
        var ex = Assert.Throws<ThemeFormatException>(() => ThemeParser.Parse(json, null, false));
        Assert.Contains("name", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"id\": \"abc\", ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ \"id\": 42, \"name\": \"X\" }")]
    [InlineData("{ \"id\": \"abc\", \"name\": \"X\", \"dock\": { \"opacity\": \"high\" } }")]
    public void RejectsBadJson(string json)
    {
        Assert.Throws<ThemeFormatException>(() => ThemeParser.Parse(json, null, false));
    }

    [Fact]
    public void RejectsNewerSchemaVersion()
    {
        var ex = Assert.Throws<ThemeFormatException>(() =>
            ThemeParser.Parse($"{{ \"schemaVersion\": {ThemeManifest.CurrentSchemaVersion + 1}, \"id\": \"abc\", \"name\": \"X\" }}", null, false));
        Assert.Contains("newer", ex.Message);
        // Current version is accepted.
        ThemeParser.Parse($"{{ \"schemaVersion\": {ThemeManifest.CurrentSchemaVersion}, \"id\": \"abc\", \"name\": \"X\" }}", null, false);
    }

    [Fact]
    public void ClampsNumericRanges()
    {
        var t = Parse("""
            "dock": {
              "background": "#000",
              "gradientAngle": 999, "opacity": 5, "cornerRadius": 500, "padding": -3,
              "borderThickness": 50,
              "shadow": { "color": "#000", "blur": 100, "spread": -100, "offsetX": 99, "offsetY": -99 },
              "image": { "path": "skin.png", "opacity": -1 }
            },
            "icons": { "hoverZoom": 9 },
            "indicator": { "size": 0 },
            "separator": { "thickness": 100 },
            "tooltip": { "cornerRadius": 99 },
            "menu": { "cornerRadius": -5 }
            """);
        var d = t.Manifest.Dock!;
        Assert.Equal(360, d.GradientAngle);
        Assert.Equal(1, d.Opacity);
        Assert.Equal(60, d.CornerRadius);
        Assert.Equal(0, d.Padding);
        Assert.Equal(6, d.BorderThickness);
        Assert.Equal(40, d.Shadow!.Blur);
        Assert.Equal(-20, d.Shadow.Spread);
        Assert.Equal(20, d.Shadow.OffsetX);
        Assert.Equal(-20, d.Shadow.OffsetY);
        Assert.Equal(0, d.Image!.Opacity);
        Assert.Equal(1.8, t.Manifest.Icons!.HoverZoom);
        Assert.Equal(2, t.Manifest.Indicator!.Size);
        Assert.Equal(8, t.Manifest.Separator!.Thickness);
        Assert.Equal(20, t.Manifest.Tooltip!.CornerRadius);
        Assert.Equal(0, t.Manifest.Menu!.CornerRadius);
        Assert.Empty(t.Warnings); // clamping is silent
    }

    [Fact]
    public void LowerHoverZoomIsClampedToOne()
    {
        Assert.Equal(1.0, Parse("\"icons\": { \"hoverZoom\": 0.2 }").Manifest.Icons!.HoverZoom);
    }

    [Fact]
    public void NormalizesColorsToAarrggbb()
    {
        var t = Parse("\"dock\": { \"background\": \"rgb(255, 0, 0)\", \"borderColor\": \"#8000\" }");
        Assert.Equal("#FFFF0000", t.Manifest.Dock!.Background);
        Assert.Equal("#88000000", t.Manifest.Dock.BorderColor);
        Assert.Empty(t.Warnings);
    }

    [Fact]
    public void DropsInvalidColorsWithWarnings()
    {
        var t = Parse("""
            "dock": { "background": "blue", "borderColor": "#12345", "highlight": "#FFF" },
            "tooltip": { "foreground": "rgb(1,2)" },
            "menu": { "accent": "zzz", "hover": "#1FFFFFFF" }
            """);
        Assert.Null(t.Manifest.Dock!.Background);
        Assert.Null(t.Manifest.Dock.BorderColor);
        Assert.Equal("#FFFFFFFF", t.Manifest.Dock.Highlight);
        Assert.Null(t.Manifest.Tooltip!.Foreground);
        Assert.Null(t.Manifest.Menu!.Accent);
        Assert.Equal("#1FFFFFFF", t.Manifest.Menu.Hover);
        Assert.Equal(4, t.Warnings.Count);
        Assert.Contains(t.Warnings, w => w.Contains("dock.background"));
        Assert.Contains(t.Warnings, w => w.Contains("dock.borderColor"));
        Assert.Contains(t.Warnings, w => w.Contains("tooltip.foreground"));
        Assert.Contains(t.Warnings, w => w.Contains("menu.accent"));
    }

    [Fact]
    public void ShadowWithoutValidColorIsDropped()
    {
        var t = Parse("\"dock\": { \"shadow\": { \"color\": \"nope\", \"blur\": 4 } }");
        Assert.Null(t.Manifest.Dock!.Shadow);
        Assert.Single(t.Warnings);
    }

    [Fact]
    public void DropsUnknownKeywordsWithWarnings()
    {
        var t = Parse("\"indicator\": { \"style\": \"sparkle\" }, \"separator\": { \"style\": \"zigzag\" }");
        Assert.Null(t.Manifest.Indicator!.Style);
        Assert.Null(t.Manifest.Separator!.Style);
        Assert.Equal(2, t.Warnings.Count);
        Assert.Contains(t.Warnings, w => w.Contains("indicator.style"));
        Assert.Contains(t.Warnings, w => w.Contains("separator.style"));
    }

    [Fact]
    public void KeywordsAreTrimmedAndLowercased()
    {
        var t = Parse("\"indicator\": { \"style\": \" PILL \" }, \"separator\": { \"style\": \"Dots\" }");
        Assert.Equal("pill", t.Manifest.Indicator!.Style);
        Assert.Equal("dots", t.Manifest.Separator!.Style);
        Assert.Empty(t.Warnings);
    }

    [Theory]
    [InlineData("../x.png")]
    [InlineData("a/../../x.png")]
    [InlineData("..\\\\x.png")]
    [InlineData("C:\\\\x.png")]
    [InlineData("C:x.png")]
    [InlineData("/x.png")]
    [InlineData("\\\\\\\\server\\\\share\\\\x.png")]
    [InlineData("x.exe")]
    [InlineData("x.png.exe")]
    [InlineData("x")]
    [InlineData("dir//x.png")]
    public void DropsUnsafeOrNonImagePathsWithWarnings(string path)
    {
        var t = Parse($"\"indicator\": {{ \"image\": \"{path}\" }}, \"separator\": {{ \"image\": \"{path}\", \"imageVertical\": \"{path}\" }}, \"dock\": {{ \"image\": {{ \"path\": \"{path}\" }} }}");
        Assert.Null(t.Manifest.Indicator!.Image);
        Assert.Null(t.Manifest.Separator!.Image);
        Assert.Null(t.Manifest.Separator.ImageVertical);
        Assert.Null(t.Manifest.Dock!.Image);
        Assert.Equal(4, t.Warnings.Count);
    }

    [Fact]
    public void KeepsSafeImagePathsAndNormalizesSeparators()
    {
        var t = Parse("\"indicator\": { \"style\": \"image\", \"image\": \"img\\\\dot.PNG\" }, \"dock\": { \"image\": { \"path\": \"skin.webp\", \"slice\": [1, 2, 3, 4] } }");
        Assert.Equal("img/dot.PNG", t.Manifest.Indicator!.Image);
        Assert.Equal("image", t.Manifest.Indicator.Style);
        Assert.Equal("skin.webp", t.Manifest.Dock!.Image!.Path);
        Assert.Equal(new List<double> { 1, 2, 3, 4 }, t.Manifest.Dock.Image.Slice);
        Assert.Empty(t.Warnings);
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("[1, 2, 3, 4, 5]")]
    [InlineData("[1, -2, 3, 4]")]
    [InlineData("[1, 2, 3, 513]")]
    public void DropsInvalidSlice(string slice)
    {
        var t = Parse($"\"dock\": {{ \"image\": {{ \"path\": \"skin.png\", \"slice\": {slice} }} }}");
        Assert.Null(t.Manifest.Dock!.Image!.Slice);
        Assert.Equal("skin.png", t.Manifest.Dock.Image.Path);
        Assert.Single(t.Warnings);
    }

    [Fact]
    public void GradientWithLessThanTwoValidColorsIsDropped()
    {
        var t = Parse("\"dock\": { \"gradient\": [\"#000\", \"nope\"] }");
        Assert.Null(t.Manifest.Dock!.Gradient);
        Assert.Equal(2, t.Warnings.Count); // invalid color + too few colors
        Assert.Contains(t.Warnings, w => w.Contains("at least 2"));

        var single = Parse("\"dock\": { \"gradient\": [\"#000\"] }");
        Assert.Null(single.Manifest.Dock!.Gradient);
        Assert.Single(single.Warnings);

        var empty = Parse("\"dock\": { \"gradient\": [] }");
        Assert.Null(empty.Manifest.Dock!.Gradient);
    }

    [Fact]
    public void ValidGradientIsKeptAndNormalized()
    {
        var t = Parse("\"dock\": { \"gradient\": [\"#000\", \"bad\", \"rgb(255,255,255)\"] }");
        Assert.Equal(new List<string> { "#FF000000", "#FFFFFFFF" }, t.Manifest.Dock!.Gradient);
        Assert.Single(t.Warnings);
    }

    [Fact]
    public void ImageIndicatorWithoutImageFallsBackToDot()
    {
        var t = Parse("\"indicator\": { \"style\": \"image\" }");
        Assert.Equal("dot", t.Manifest.Indicator!.Style);
        Assert.Single(t.Warnings);

        var bad = Parse("\"indicator\": { \"style\": \"image\", \"image\": \"../x.png\" }");
        Assert.Equal("dot", bad.Manifest.Indicator!.Style);
        Assert.Equal(2, bad.Warnings.Count);
    }

    [Fact]
    public void ImageSeparatorWithoutImageFallsBackToLine()
    {
        var t = Parse("\"separator\": { \"style\": \"image\", \"image\": \"x.exe\" }");
        Assert.Equal("line", t.Manifest.Separator!.Style);
        Assert.Equal(2, t.Warnings.Count);
    }

    [Fact]
    public void DockImageWithoutPathIsDropped()
    {
        var t = Parse("\"dock\": { \"image\": { \"opacity\": 0.5 } }");
        Assert.Null(t.Manifest.Dock!.Image);
    }

    [Fact]
    public void WebsiteMustBeHttp()
    {
        Assert.Equal("https://example.com/", Parse("\"website\": \"https://example.com/\"").Manifest.Website);
        foreach (var bad in new[] { "javascript:alert(1)", "file:///C:/x", "not a url", "ftp://x.org" })
        {
            var t = Parse($"\"website\": \"{bad}\"");
            Assert.Null(t.Manifest.Website);
            Assert.Single(t.Warnings);
        }
    }

    [Fact]
    public void TruncatesLongTextFields()
    {
        var t = Parse($"\"author\": \"{new string('a', 100)}\", \"version\": \"{new string('1', 30)}\", \"description\": \"{new string('d', 400)}\"")
            ;
        Assert.Equal(60, t.Manifest.Author!.Length);
        Assert.Equal(20, t.Manifest.Version!.Length);
        Assert.Equal(300, t.Manifest.Description!.Length);
        var longName = ThemeParser.Parse($"{{ \"id\": \"abc\", \"name\": \"  {new string('n', 80)}  \" }}", null, false);
        Assert.Equal(60, longName.Name.Length);
    }

    [Fact]
    public void ResolveFileFindsFilesInsideTheThemeFolder()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "a.png"), "x");
        var t = Parse("", _tempDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "img", "a.png")), t.ResolveFile("img/a.png"));
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "img", "a.png")), t.ResolveFile("img\\a.png"));
        Assert.Null(t.ResolveFile("img/missing.png"));
        Assert.Null(t.ResolveFile(null));
        Assert.Null(t.ResolveFile(" "));
    }

    [Fact]
    public void ResolveFileRefusesEscapes()
    {
        string themeDir = Path.Combine(_tempDir, "theme");
        Directory.CreateDirectory(themeDir);
        File.WriteAllText(Path.Combine(_tempDir, "outside.png"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "theme-sibling.png"), "x");
        var t = Parse("", themeDir);

        Assert.Null(t.ResolveFile("../outside.png"));
        Assert.Null(t.ResolveFile("..\\outside.png"));
        Assert.Null(t.ResolveFile("sub/../../outside.png"));
        Assert.Null(t.ResolveFile(Path.Combine(_tempDir, "outside.png")));
        Assert.Null(t.ResolveFile("../theme-sibling.png"));
        Assert.Null(t.ResolveFile("C:outside.png"));
    }

    [Fact]
    public void ResolveFileReturnsNullForBuiltInThemes()
    {
        var t = ThemeParser.Parse("{ \"id\": \"abc\", \"name\": \"X\" }", null, true);
        Assert.Null(t.ResolveFile("x.png"));
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a/b/c.png", true)]
    [InlineData("a\\b.png", true)]
    [InlineData("../a.png", false)]
    [InlineData("a/../b.png", false)]
    [InlineData("/a.png", false)]
    [InlineData("C:\\a.png", false)]
    [InlineData("C:a.png", false)]
    [InlineData("a//b.png", false)]
    [InlineData("a/", false)]
    public void IsSafeRelativePath(string path, bool expected)
    {
        Assert.Equal(expected, ThemeParser.IsSafeRelativePath(path));
    }
}
