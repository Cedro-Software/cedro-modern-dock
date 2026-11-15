namespace CedroModernDock.Core.Themes;

using System.Text.Json.Serialization;

/// <summary>
/// Contents of a theme's <c>theme.json</c>. Every visual property is optional:
/// a property left out keeps the user's own setting (dock color, transparency,
/// rounding, padding) or the Cedro default. See docs/THEMES.md for the format.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Format version understood by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("website")] public string? Website { get; set; }

    [JsonPropertyName("dock")] public ThemeDockSection? Dock { get; set; }
    [JsonPropertyName("icons")] public ThemeIconsSection? Icons { get; set; }
    [JsonPropertyName("indicator")] public ThemeIndicatorSection? Indicator { get; set; }
    [JsonPropertyName("separator")] public ThemeSeparatorSection? Separator { get; set; }
    [JsonPropertyName("tooltip")] public ThemeTooltipSection? Tooltip { get; set; }
    [JsonPropertyName("menu")] public ThemeMenuSection? Menu { get; set; }
}

public sealed class ThemeDockSection
{
    /// <summary>Solid background color. Ignored when <see cref="Gradient"/> is set.</summary>
    [JsonPropertyName("background")] public string? Background { get; set; }
    /// <summary>2+ colors for a linear gradient background.</summary>
    [JsonPropertyName("gradient")] public List<string>? Gradient { get; set; }
    /// <summary>Gradient direction in degrees (0 = left to right, 90 = top to bottom).</summary>
    [JsonPropertyName("gradientAngle")] public double? GradientAngle { get; set; }
    /// <summary>Background opacity 0..1. Left out: the user's transparency slider applies.</summary>
    [JsonPropertyName("opacity")] public double? Opacity { get; set; }
    [JsonPropertyName("cornerRadius")] public int? CornerRadius { get; set; }
    [JsonPropertyName("padding")] public int? Padding { get; set; }
    [JsonPropertyName("borderColor")] public string? BorderColor { get; set; }
    [JsonPropertyName("borderThickness")] public double? BorderThickness { get; set; }
    /// <summary>Thin inner line along the top edge (glass reflection).</summary>
    [JsonPropertyName("highlight")] public string? Highlight { get; set; }
    [JsonPropertyName("shadow")] public ThemeShadow? Shadow { get; set; }
    /// <summary>Skin image stretched with 9-slice scaling (RocketDock / ObjectDock style).</summary>
    [JsonPropertyName("image")] public ThemeImage? Image { get; set; }
}

public sealed class ThemeShadow
{
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("blur")] public double? Blur { get; set; }
    [JsonPropertyName("spread")] public double? Spread { get; set; }
    [JsonPropertyName("offsetX")] public double? OffsetX { get; set; }
    [JsonPropertyName("offsetY")] public double? OffsetY { get; set; }
}

public sealed class ThemeImage
{
    /// <summary>Path relative to the theme folder.</summary>
    [JsonPropertyName("path")] public string? Path { get; set; }
    /// <summary>Non-stretched borders in image pixels: [left, top, right, bottom].</summary>
    [JsonPropertyName("slice")] public List<double>? Slice { get; set; }
    [JsonPropertyName("opacity")] public double? Opacity { get; set; }
}

public sealed class ThemeIconsSection
{
    /// <summary>Hover magnification, 1.0 (none) to 1.8.</summary>
    [JsonPropertyName("hoverZoom")] public double? HoverZoom { get; set; }
}

public sealed class ThemeIndicatorSection
{
    /// <summary>dot | line | pill | glow | image | none</summary>
    [JsonPropertyName("style")] public string? Style { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("size")] public double? Size { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
}

public sealed class ThemeSeparatorSection
{
    /// <summary>classic | line | dots | image | space</summary>
    [JsonPropertyName("style")] public string? Style { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("thickness")] public double? Thickness { get; set; }
    /// <summary>Image for a horizontal dock (a vertical bar).</summary>
    [JsonPropertyName("image")] public string? Image { get; set; }
    /// <summary>Image for a vertical dock (a horizontal bar). Defaults to <see cref="Image"/>.</summary>
    [JsonPropertyName("imageVertical")] public string? ImageVertical { get; set; }
}

public sealed class ThemeTooltipSection
{
    [JsonPropertyName("background")] public string? Background { get; set; }
    [JsonPropertyName("foreground")] public string? Foreground { get; set; }
    [JsonPropertyName("borderColor")] public string? BorderColor { get; set; }
    [JsonPropertyName("cornerRadius")] public int? CornerRadius { get; set; }
}

public sealed class ThemeMenuSection
{
    [JsonPropertyName("background")] public string? Background { get; set; }
    [JsonPropertyName("foreground")] public string? Foreground { get; set; }
    [JsonPropertyName("secondaryForeground")] public string? SecondaryForeground { get; set; }
    [JsonPropertyName("hover")] public string? Hover { get; set; }
    [JsonPropertyName("accent")] public string? Accent { get; set; }
    [JsonPropertyName("borderColor")] public string? BorderColor { get; set; }
    [JsonPropertyName("danger")] public string? Danger { get; set; }
    [JsonPropertyName("cornerRadius")] public int? CornerRadius { get; set; }
}
