namespace CedroModernDock.Core.Themes;

using System.Globalization;

/// <summary>
/// UI-framework independent ARGB color parsed from theme files. Accepted forms:
/// <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c>, <c>rgb(r, g, b)</c>,
/// <c>rgba(r, g, b, a)</c> (a in 0..1) and the config.json form <c>r, g, b</c>.
/// </summary>
public readonly record struct ThemeColor(byte A, byte R, byte G, byte B)
{
    public static ThemeColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public ThemeColor WithAlpha(byte a) => this with { A = a };

    /// <summary>Multiplies the alpha channel by <paramref name="factor"/> (0..1).</summary>
    public ThemeColor ScaleAlpha(double factor)
        => this with { A = (byte)Math.Round(Math.Clamp(factor, 0, 1) * A) };

    /// <summary>Relative luminance (0 = black, 1 = white), ignoring alpha.</summary>
    public double Luminance => (0.2126 * R + 0.7152 * G + 0.0722 * B) / 255.0;

    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public static bool TryParse(string? text, out ThemeColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();

        if (s.StartsWith('#'))
            return TryParseHex(s[1..], out color);

        string lower = s.ToLowerInvariant();
        if (lower.StartsWith("rgba(") && lower.EndsWith(')'))
            return TryParseComponents(s[5..^1], allowAlpha: true, alphaIsFraction: true, out color);
        if (lower.StartsWith("rgb(") && lower.EndsWith(')'))
            return TryParseComponents(s[4..^1], allowAlpha: false, alphaIsFraction: false, out color);

        // config.json legacy form "r, g, b, " (trailing comma tolerated).
        return TryParseComponents(s, allowAlpha: false, alphaIsFraction: false, out color);
    }

    public static ThemeColor Parse(string text)
        => TryParse(text, out var c) ? c : throw new FormatException($"Invalid color: '{text}'");

    private static bool TryParseHex(string hex, out ThemeColor color)
    {
        color = default;
        // HexNumber tolerates surrounding spaces: "# FFF" must not parse.
        if (hex.Length == 0 || !hex.All(Uri.IsHexDigit)) return false;
        if (hex.Length is 3 or 4)
            hex = string.Concat(hex.Select(ch => new string(ch, 2)));
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8) return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return false;
        color = new ThemeColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    private static bool TryParseComponents(string body, bool allowAlpha, bool alphaIsFraction, out ThemeColor color)
    {
        color = default;
        var parts = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts.Length > (allowAlpha ? 4 : 3)) return false;
        var rgb = new byte[3];
        for (int i = 0; i < 3; i++)
            if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out rgb[i]))
                return false;
        byte a = 255;
        if (parts.Length == 4)
        {
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double av))
                return false;
            a = (byte)Math.Round(Math.Clamp(alphaIsFraction ? av : av / 255.0, 0, 1) * 255);
        }
        color = new ThemeColor(a, rgb[0], rgb[1], rgb[2]);
        return true;
    }
}
