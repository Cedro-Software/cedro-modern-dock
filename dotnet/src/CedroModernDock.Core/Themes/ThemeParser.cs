namespace CedroModernDock.Core.Themes;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Raised when a theme.json is missing required data or is malformed.</summary>
public sealed class ThemeFormatException : Exception
{
    public ThemeFormatException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A validated theme ready to be rendered.</summary>
/// <param name="Manifest">Parsed and sanitized theme.json.</param>
/// <param name="Directory">Theme folder (null for built-in themes): image paths resolve against it.</param>
/// <param name="IsBuiltIn">True for themes shipped inside the application.</param>
/// <param name="Warnings">Non-fatal problems (ignored values) found while loading.</param>
public sealed record LoadedTheme(ThemeManifest Manifest, string? Directory, bool IsBuiltIn, IReadOnlyList<string> Warnings)
{
    public string Id => Manifest.Id;
    public string Name => Manifest.Name;

    /// <summary>
    /// Absolute path of a file referenced by the theme, or null when the theme
    /// is built-in, the path is unsafe (absolute, escapes the folder) or missing.
    /// </summary>
    public string? ResolveFile(string? relativePath)
    {
        if (Directory == null || string.IsNullOrWhiteSpace(relativePath)) return null;
        if (!ThemeParser.IsSafeRelativePath(relativePath)) return null;
        string root = Path.GetFullPath(Directory);
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(full) ? full : null;
    }
}

/// <summary>
/// Parses and validates theme.json. Invalid optional values are dropped with a
/// warning (the theme still loads); missing id/name or broken JSON reject it.
/// Numeric values are clamped to ranges the dock can render sensibly.
/// </summary>
public static class ThemeParser
{
    public const string ManifestFileName = "theme.json";

    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9._-]{1,63}$", RegexOptions.Compiled);
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif" };

    public static readonly IReadOnlySet<string> IndicatorStyles = new HashSet<string> { "dot", "line", "pill", "glow", "image", "none" };
    public static readonly IReadOnlySet<string> SeparatorStyles = new HashSet<string> { "classic", "line", "dots", "image", "space" };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static bool IsValidId(string? id) => id != null && IdPattern.IsMatch(id);

    public static bool IsSafeRelativePath(string path)
    {
        if (Path.IsPathRooted(path) || path.Contains(':')) return false;
        var segments = path.Replace('\\', '/').Split('/');
        return segments.All(s => s != ".." && s.Length > 0);
    }

    public static LoadedTheme Parse(string json, string? directory, bool isBuiltIn)
    {
        ThemeManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ThemeManifest>(json, Options);
        }
        catch (JsonException e)
        {
            throw new ThemeFormatException($"theme.json is not valid JSON: {e.Message}", e);
        }
        if (manifest == null) throw new ThemeFormatException("theme.json is empty.");

        if (!IsValidId(manifest.Id))
            throw new ThemeFormatException(
                $"Invalid theme id '{manifest.Id}': use 2-64 lowercase letters, digits, '.', '_' or '-'.");
        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new ThemeFormatException("The theme has no name.");
        if (manifest.SchemaVersion > ThemeManifest.CurrentSchemaVersion)
            throw new ThemeFormatException(
                $"The theme needs a newer Cedro Modern Dock (format v{manifest.SchemaVersion}).");

        var warnings = new List<string>();
        Sanitize(manifest, warnings);
        return new LoadedTheme(manifest, directory, isBuiltIn, warnings);
    }

    private static void Sanitize(ThemeManifest m, List<string> w)
    {
        m.Name = Truncate(m.Name.Trim(), 60)!; // non-null: checked by Parse
        m.Author = Truncate(m.Author?.Trim(), 60);
        m.Version = Truncate(m.Version?.Trim(), 20);
        m.Description = Truncate(m.Description?.Trim(), 300);
        if (m.Website != null
            && !(Uri.TryCreate(m.Website, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
        {
            w.Add("website must be an http(s) URL; ignored.");
            m.Website = null;
        }

        if (m.Dock is { } d)
        {
            d.Background = Color(d.Background, "dock.background", w);
            if (d.Gradient != null)
            {
                d.Gradient = d.Gradient.Select(c => Color(c, "dock.gradient", w)).OfType<string>().ToList();
                if (d.Gradient.Count < 2)
                {
                    w.Add("dock.gradient needs at least 2 colors; ignored.");
                    d.Gradient = null;
                }
            }
            d.GradientAngle = Clamp(d.GradientAngle, -360, 360);
            d.Opacity = Clamp(d.Opacity, 0, 1);
            d.CornerRadius = ClampInt(d.CornerRadius, 0, 60);
            d.Padding = ClampInt(d.Padding, 0, 40);
            d.BorderColor = Color(d.BorderColor, "dock.borderColor", w);
            d.BorderThickness = Clamp(d.BorderThickness, 0, 6);
            d.Highlight = Color(d.Highlight, "dock.highlight", w);
            if (d.Shadow is { } s)
            {
                s.Color = Color(s.Color, "dock.shadow.color", w);
                s.Blur = Clamp(s.Blur, 0, 40);
                s.Spread = Clamp(s.Spread, -20, 20);
                s.OffsetX = Clamp(s.OffsetX, -20, 20);
                s.OffsetY = Clamp(s.OffsetY, -20, 20);
                if (s.Color == null) d.Shadow = null;
            }
            if (d.Image is { } img)
            {
                img.Path = ImagePath(img.Path, "dock.image.path", w);
                img.Opacity = Clamp(img.Opacity, 0, 1);
                if (img.Slice != null && (img.Slice.Count != 4 || img.Slice.Any(v => v < 0 || v > 512)))
                {
                    w.Add("dock.image.slice must be [left, top, right, bottom] in 0..512; ignored.");
                    img.Slice = null;
                }
                if (img.Path == null) d.Image = null;
            }
        }

        if (m.Icons is { } icons)
            icons.HoverZoom = Clamp(icons.HoverZoom, 1.0, 1.8);

        if (m.Indicator is { } ind)
        {
            ind.Style = Keyword(ind.Style, IndicatorStyles, "indicator.style", w);
            ind.Color = Color(ind.Color, "indicator.color", w);
            ind.Size = Clamp(ind.Size, 2, 16);
            ind.Image = ImagePath(ind.Image, "indicator.image", w);
            if (ind.Style == "image" && ind.Image == null)
            {
                w.Add("indicator.style is 'image' but no valid indicator.image; using 'dot'.");
                ind.Style = "dot";
            }
        }

        if (m.Separator is { } sep)
        {
            sep.Style = Keyword(sep.Style, SeparatorStyles, "separator.style", w);
            sep.Color = Color(sep.Color, "separator.color", w);
            sep.Thickness = Clamp(sep.Thickness, 1, 8);
            sep.Image = ImagePath(sep.Image, "separator.image", w);
            sep.ImageVertical = ImagePath(sep.ImageVertical, "separator.imageVertical", w);
            if (sep.Style == "image" && sep.Image == null)
            {
                w.Add("separator.style is 'image' but no valid separator.image; using 'line'.");
                sep.Style = "line";
            }
        }

        if (m.Tooltip is { } t)
        {
            t.Background = Color(t.Background, "tooltip.background", w);
            t.Foreground = Color(t.Foreground, "tooltip.foreground", w);
            t.BorderColor = Color(t.BorderColor, "tooltip.borderColor", w);
            t.CornerRadius = ClampInt(t.CornerRadius, 0, 20);
        }

        if (m.Menu is { } menu)
        {
            menu.Background = Color(menu.Background, "menu.background", w);
            menu.Foreground = Color(menu.Foreground, "menu.foreground", w);
            menu.SecondaryForeground = Color(menu.SecondaryForeground, "menu.secondaryForeground", w);
            menu.Hover = Color(menu.Hover, "menu.hover", w);
            menu.Accent = Color(menu.Accent, "menu.accent", w);
            menu.BorderColor = Color(menu.BorderColor, "menu.borderColor", w);
            menu.Danger = Color(menu.Danger, "menu.danger", w);
            menu.CornerRadius = ClampInt(menu.CornerRadius, 0, 20);
        }
    }

    private static string? Color(string? value, string field, List<string> w)
    {
        if (value == null) return null;
        if (ThemeColor.TryParse(value, out var c)) return c.ToHex();
        w.Add($"{field}: '{value}' is not a color; ignored.");
        return null;
    }

    private static string? Keyword(string? value, IReadOnlySet<string> allowed, string field, List<string> w)
    {
        if (value == null) return null;
        string v = value.Trim().ToLowerInvariant();
        if (allowed.Contains(v)) return v;
        w.Add($"{field}: '{value}' is unknown (expected {string.Join(", ", allowed)}); ignored.");
        return null;
    }

    private static string? ImagePath(string? value, string field, List<string> w)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!IsSafeRelativePath(value))
        {
            w.Add($"{field}: '{value}' must be a path inside the theme folder; ignored.");
            return null;
        }
        if (!ImageExtensions.Contains(Path.GetExtension(value).ToLowerInvariant()))
        {
            w.Add($"{field}: '{value}' is not a supported image (png, jpg, bmp, webp, gif); ignored.");
            return null;
        }
        return value.Replace('\\', '/');
    }

    private static double? Clamp(double? v, double min, double max)
        => v.HasValue ? Math.Clamp(v.Value, min, max) : null;

    private static int? ClampInt(int? v, int min, int max)
        => v.HasValue ? Math.Clamp(v.Value, min, max) : null;

    private static string? Truncate(string? s, int max)
        => s == null ? null : s.Length <= max ? s : s[..max];
}
