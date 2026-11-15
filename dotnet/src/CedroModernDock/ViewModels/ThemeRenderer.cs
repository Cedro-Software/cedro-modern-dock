using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Themes;

namespace CedroModernDock.ViewModels;

/// <summary>Everything the dock bar needs to draw itself for a theme.</summary>
public sealed record DockSurface(
    IBrush Background,
    Color BaseColor,
    double Opacity,
    int CornerRadius,
    int Padding,
    IBrush BorderBrush,
    double BorderThickness,
    IBrush? Highlight,
    BoxShadows Shadow,
    double ShadowExtent,
    Bitmap? Skin,
    Thickness SkinSlice,
    double SkinOpacity,
    Color Accent);

/// <summary>
/// Converts a <see cref="LoadedTheme"/> into Avalonia brushes. Values the theme
/// leaves out fall back to the user's appearance settings (Cedro behavior).
/// </summary>
public static class ThemeRenderer
{
    private static readonly Dictionary<string, Bitmap?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);

    public static Color ToColor(ThemeColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static Color ParseOr(string? text, Color fallback)
        => ThemeColor.TryParse(text, out var c) ? ToColor(c) : fallback;

    public static DockSurface BuildSurface(LoadedTheme theme, DockAppearanceService appearance)
    {
        var dock = theme.Manifest.Dock;
        var userColor = ParseOr(appearance.GetDockColorRGB(), Colors.Black);
        bool themeOwnsBackground = dock?.Background != null || dock?.Gradient != null;
        // A theme that defines its colors defines their alpha too; the user's
        // transparency slider only drives backgrounds the theme leaves open.
        double opacity = dock?.Opacity
                         ?? (themeOwnsBackground ? 1.0 : appearance.GetDockTransparencyPercentage() / 100.0);

        IBrush background;
        Color baseColor;
        if (dock?.Gradient is { Count: >= 2 } stops)
        {
            var colors = stops.Select(s => ParseOr(s, Colors.Black)).ToList();
            baseColor = colors[colors.Count / 2];
            double angle = (dock.GradientAngle ?? 90) * Math.PI / 180;
            double dx = Math.Cos(angle) / 2, dy = Math.Sin(angle) / 2;
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative),
                Opacity = opacity
            };
            for (int i = 0; i < colors.Count; i++)
                gradient.GradientStops.Add(new GradientStop(colors[i], (double)i / (colors.Count - 1)));
            background = gradient;
        }
        else
        {
            baseColor = dock?.Background != null ? ParseOr(dock.Background, userColor) : userColor;
            background = new SolidColorBrush(baseColor, opacity);
        }

        int radius = dock?.CornerRadius ?? appearance.GetDockBorderRounding();
        int padding = dock?.Padding ?? appearance.GetDockPadding();
        double borderThickness = dock?.BorderColor != null ? dock.BorderThickness ?? 1 : 0;
        IBrush borderBrush = dock?.BorderColor != null
            ? new SolidColorBrush(ParseOr(dock.BorderColor, Colors.Transparent))
            : Brushes.Transparent;
        IBrush? highlight = dock?.Highlight != null ? new SolidColorBrush(ParseOr(dock.Highlight, Colors.Transparent)) : null;

        BoxShadows shadow = default;
        double extent = 0;
        if (dock?.Shadow is { Color: not null } s)
        {
            var bs = new BoxShadow
            {
                OffsetX = s.OffsetX ?? 0,
                OffsetY = s.OffsetY ?? 4,
                Blur = s.Blur ?? 16,
                Spread = s.Spread ?? 0,
                Color = ParseOr(s.Color, Colors.Transparent)
            };
            shadow = new BoxShadows(bs);
            extent = Math.Ceiling(bs.Blur + Math.Max(0, bs.Spread) + Math.Max(Math.Abs(bs.OffsetX), Math.Abs(bs.OffsetY)));
        }

        Bitmap? skin = LoadBitmap(theme.ResolveFile(dock?.Image?.Path));
        var slice = default(Thickness);
        if (skin != null)
        {
            var sl = dock!.Image!.Slice;
            slice = sl is { Count: 4 }
                ? new Thickness(sl[0], sl[1], sl[2], sl[3])
                : new Thickness(Math.Floor(Math.Min(skin.Size.Width, skin.Size.Height) / 3));
        }

        var accent = ParseOr(theme.Manifest.Menu?.Accent, Color.FromRgb(0x4C, 0x9A, 0xFF));
        return new DockSurface(background, baseColor, opacity, radius, padding, borderBrush, borderThickness,
            highlight, shadow, extent, skin, slice, dock?.Image?.Opacity ?? 1.0, accent);
    }

    /// <summary>Pushes hover zoom, indicator and separator settings into the shared item style.</summary>
    public static void ApplyItemStyle(LoadedTheme theme, DockVisualStyle style)
    {
        var m = theme.Manifest;
        style.HoverZoom = m.Icons?.HoverZoom ?? 1.3;

        var ind = m.Indicator;
        var indicatorImage = LoadBitmap(theme.ResolveFile(ind?.Image));
        string indicatorStyle = ind?.Style ?? "dot";
        // Image missing or unreadable: same fallback as the parser.
        if (indicatorStyle == "image" && indicatorImage == null) indicatorStyle = "dot";
        style.SetIndicator(
            indicatorStyle,
            ind?.Size ?? 4,
            ParseOr(ind?.Color, Colors.White),
            indicatorImage);

        var sep = m.Separator;
        string sepStyle = sep?.Style ?? "classic";
        Bitmap? image, imageVertical;
        if (sepStyle == "classic")
        {
            image = IconLoader.LoadFromAsset("Assets/icons/open-softwares-separator-1.png");
            imageVertical = IconLoader.LoadFromAsset("Assets/icons/open-softwares-separator-2.png");
        }
        else
        {
            image = LoadBitmap(theme.ResolveFile(sep?.Image));
            imageVertical = LoadBitmap(theme.ResolveFile(sep?.ImageVertical)) ?? image;
            if (sepStyle == "image" && image == null) sepStyle = "line";
        }
        style.SetSeparator(sepStyle, ParseOr(sep?.Color, Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            sep?.Thickness ?? 1, image, imageVertical);
    }

    /// <summary>
    /// Publishes the theme's menu and tooltip colors as application resources.
    /// Cedro styles read the Cedro* keys; the Fluent keys cover sub-menus.
    /// </summary>
    public static void ApplyPopupResources(LoadedTheme theme)
    {
        var res = Avalonia.Application.Current?.Resources;
        if (res == null) return;
        var menu = theme.Manifest.Menu;
        var tip = theme.Manifest.Tooltip;

        // Menus and tooltips stay opaque whatever the theme says: text showing
        // through from the windows behind hurts readability.
        var bg = Readable(ParseOr(menu?.Background, Color.Parse("#F51C1C20")));
        var fg = ParseOr(menu?.Foreground, Color.Parse("#FFF0F0F0"));
        var secondary = ParseOr(menu?.SecondaryForeground, Color.FromArgb(0x99, fg.R, fg.G, fg.B));
        var hover = ParseOr(menu?.Hover, Color.FromArgb(0x1F, fg.R, fg.G, fg.B));
        var accent = ParseOr(menu?.Accent, Color.Parse("#FF4C9AFF"));
        var border = ParseOr(menu?.BorderColor, Color.FromArgb(0x24, fg.R, fg.G, fg.B));
        var danger = ParseOr(menu?.Danger, Color.Parse("#FFFF6B6B"));
        int radius = menu?.CornerRadius ?? 10;

        res["CedroMenuBackground"] = new SolidColorBrush(bg);
        res["CedroMenuForeground"] = new SolidColorBrush(fg);
        res["CedroMenuSecondary"] = new SolidColorBrush(secondary);
        res["CedroMenuHover"] = new SolidColorBrush(hover);
        res["CedroMenuAccent"] = new SolidColorBrush(accent);
        res["CedroMenuBorder"] = new SolidColorBrush(border);
        res["CedroMenuDanger"] = new SolidColorBrush(danger);
        res["CedroMenuDangerHover"] = new SolidColorBrush(Color.FromArgb(0x26, danger.R, danger.G, danger.B));
        res["CedroMenuSeparator"] = new SolidColorBrush(Color.FromArgb(0x1F, fg.R, fg.G, fg.B));
        res["CedroMenuCornerRadius"] = new CornerRadius(radius);
        res["CedroMenuItemCornerRadius"] = new CornerRadius(Math.Max(0, Math.Min(radius - 4, 8)));

        // Fluent menu resources (item hover/pressed states, sub-menu popups).
        res["MenuFlyoutPresenterBackground"] = new SolidColorBrush(bg);
        res["MenuFlyoutPresenterBorderBrush"] = new SolidColorBrush(border);
        res["MenuFlyoutItemBackgroundPointerOver"] = new SolidColorBrush(hover);
        res["MenuFlyoutItemBackgroundPressed"] = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, hover.A * 1.6), hover.R, hover.G, hover.B));
        foreach (var key in new[] { "MenuFlyoutItemForeground", "MenuFlyoutItemForegroundPointerOver", "MenuFlyoutItemForegroundPressed" })
            res[key] = new SolidColorBrush(fg);
        foreach (var key in new[] { "MenuFlyoutSubItemChevron", "MenuFlyoutSubItemChevronPointerOver", "MenuFlyoutSubItemChevronPressed", "MenuFlyoutSubItemChevronSubMenuOpened",
                     "MenuFlyoutItemKeyboardAcceleratorTextForeground", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed" })
            res[key] = new SolidColorBrush(secondary);
        res["MenuFlyoutItemForegroundDisabled"] = new SolidColorBrush(Color.FromArgb(0x66, fg.R, fg.G, fg.B));

        res["CedroTooltipBackground"] = new SolidColorBrush(Readable(ParseOr(tip?.Background, bg)));
        res["CedroTooltipForeground"] = new SolidColorBrush(ParseOr(tip?.Foreground, fg));
        res["CedroTooltipBorder"] = new SolidColorBrush(ParseOr(tip?.BorderColor, border));
        res["CedroTooltipCornerRadius"] = new CornerRadius(tip?.CornerRadius ?? 7);
    }

    /// <summary>
    /// Popup backgrounds are drawn fully opaque: on Windows, even a 98% alpha
    /// lets roughly 12% of the windows behind bleed through the menu text.
    /// </summary>
    private static Color Readable(Color c) => Color.FromRgb(c.R, c.G, c.B);

    private static Bitmap? LoadBitmap(string? path)
    {
        if (path == null) return null;
        if (BitmapCache.TryGetValue(path, out var cached)) return cached;
        Bitmap? bitmap = null;
        try { bitmap = new Bitmap(path); }
        catch { /* unreadable image: the theme part falls back */ }
        BitmapCache[path] = bitmap;
        return bitmap;
    }

    /// <summary>Drops cached images (themes reinstalled or edited on disk).</summary>
    public static void ClearCache() => BitmapCache.Clear();
}
