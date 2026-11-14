namespace CedroModernDock.Tests;

using CedroModernDock.Core.Themes;

public class ThemeColorTest
{
    [Theory]
    [InlineData("#F80", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("#8F80", 0x88, 0xFF, 0x88, 0x00)]
    [InlineData("#FF8800", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("#80FF8800", 0x80, 0xFF, 0x88, 0x00)]
    [InlineData("#ff8800", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("  #FF8800  ", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("rgb(255, 136, 0)", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("RGB(255,136,0)", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("rgba(255, 136, 0, 0.5)", 128, 0xFF, 0x88, 0x00)]
    [InlineData("rgba(255, 136, 0, 0)", 0, 0xFF, 0x88, 0x00)]
    [InlineData("rgba(255, 136, 0, 1)", 255, 0xFF, 0x88, 0x00)]
    [InlineData("rgba(255, 136, 0, 7)", 255, 0xFF, 0x88, 0x00)]   // alpha clamped to 1
    [InlineData("rgba(255, 136, 0)", 255, 0xFF, 0x88, 0x00)]      // alpha optional
    [InlineData("255, 136, 0, ", 0xFF, 0xFF, 0x88, 0x00)]         // config.json legacy form
    [InlineData("255, 136, 0", 0xFF, 0xFF, 0x88, 0x00)]
    [InlineData("0, 0, 0, ", 0xFF, 0, 0, 0)]
    public void ParsesSupportedForms(string text, int a, int r, int g, int b)
    {
        Assert.True(ThemeColor.TryParse(text, out var c));
        Assert.Equal(new ThemeColor((byte)a, (byte)r, (byte)g, (byte)b), c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("#GGG")]
    [InlineData("#XYZXYZ")]
    [InlineData("red")]
    [InlineData("rgb(256, 0, 0)")]
    [InlineData("rgb(-1, 0, 0)")]
    [InlineData("rgb(1, 2)")]
    [InlineData("rgb(1, 2, 3, 4)")]
    [InlineData("rgb(1, 2, 3")]
    [InlineData("rgba(1, 2, 3, x)")]
    [InlineData("rgba(1, 2, 3, 0.5, 9)")]
    [InlineData("1, 2")]
    [InlineData("1, 2, 3, 4")]
    [InlineData("a, b, c")]
    [InlineData("1.5, 2, 3")]
    public void RejectsGarbage(string? text)
    {
        Assert.False(ThemeColor.TryParse(text, out _));
    }

    /// <summary>
    /// BUG (ThemeColor.cs, TryParseHex): uint.TryParse is called with NumberStyles.HexNumber,
    /// which includes AllowLeadingWhite/AllowTrailingWhite, so whitespace inside the hex body
    /// is accepted after the #RGB expansion: "# FFF" becomes "  FFFFFF" and parses as
    /// #00FFFFFF (fully transparent). Expected: rejected.
    /// </summary>
    [Theory]
    [InlineData("# FFF")]
    [InlineData("# FFFFFFF")]
    public void BUG_RejectsWhitespaceInsideHex(string text)
    {
        Assert.False(ThemeColor.TryParse(text, out _));
    }

    [Fact]
    public void ParseThrowsOnInvalidColor()
    {
        Assert.Throws<FormatException>(() => ThemeColor.Parse("nope"));
        Assert.Equal(ThemeColor.FromRgb(1, 2, 3), ThemeColor.Parse("#010203"));
    }

    [Fact]
    public void ToHexIsAlwaysAarrggbbAndRoundTrips()
    {
        var c = new ThemeColor(0x12, 0xAB, 0xCD, 0xEF);
        Assert.Equal("#12ABCDEF", c.ToHex());
        Assert.Equal(c, ThemeColor.Parse(c.ToHex()));
        Assert.Equal("#FFFF8800", ThemeColor.Parse("#F80").ToHex());
    }

    [Fact]
    public void AlphaHelpersAndLuminance()
    {
        var c = ThemeColor.FromRgb(255, 255, 255);
        Assert.Equal(255, c.A);
        Assert.Equal(10, c.WithAlpha(10).A);
        Assert.Equal(128, c.ScaleAlpha(0.5).A);
        Assert.Equal(255, c.ScaleAlpha(3).A);
        Assert.Equal(0, c.ScaleAlpha(-1).A);
        Assert.Equal(1.0, c.Luminance, precision: 6);
        Assert.Equal(0.0, ThemeColor.FromRgb(0, 0, 0).Luminance, precision: 6);
    }
}
