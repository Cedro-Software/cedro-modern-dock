using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Data;
using Avalonia.Media;

namespace CedroModernDock.Controls;

/// <summary>
/// Line icons (16x16, stroked) used by the dock menus and settings buttons.
/// Drawn as simple geometry so they follow the menu text color.
/// </summary>
public static class Glyphs
{
    public const string Rename = "M3 13 L3.6 10.4 L10.6 3.4 A1.4 1.4 0 0 1 12.6 5.4 L5.6 12.4 Z M9.6 4.4 L11.6 6.4";
    public const string OpenLocation = "M2.5 4.5 A1 1 0 0 1 3.5 3.5 H6.5 L8 5 H12.5 A1 1 0 0 1 13.5 6 V12 A1 1 0 0 1 12.5 13 H3.5 A1 1 0 0 1 2.5 12 Z";
    public const string Retarget = "M2.5 8 H12.5 M9.5 5 L12.5 8 L9.5 11 M2.5 4 V12";
    public const string Separator = "M8 2.5 V13.5 M5 5.5 L3 8 L5 10.5 M11 5.5 L13 8 L11 10.5";
    public const string Remove = "M3 4.5 H13 M6.5 4.5 V3 H9.5 V4.5 M4.5 4.5 L5.2 13 H10.8 L11.5 4.5 M7 7 V10.5 M9 7 V10.5";
    public const string AddProgram = "M3.5 2.5 H12.5 A1 1 0 0 1 13.5 3.5 V12.5 A1 1 0 0 1 12.5 13.5 H3.5 A1 1 0 0 1 2.5 12.5 V3.5 A1 1 0 0 1 3.5 2.5 Z M8 5.5 V10.5 M5.5 8 H10.5";
    public const string AddFolder = "M2.5 4.5 A1 1 0 0 1 3.5 3.5 H6.5 L8 5 H12.5 A1 1 0 0 1 13.5 6 V12 A1 1 0 0 1 12.5 13 H3.5 A1 1 0 0 1 2.5 12 Z M8 7 V11 M6 9 H10";
    public const string AddModule = "M2.5 2.5 H7 V7 H2.5 Z M9 2.5 H13.5 V7 H9 Z M2.5 9 H7 V13.5 H2.5 Z M11.25 9 V13.5 M9 11.25 H13.5";
    public const string Lock = "M4.5 7.5 H11.5 A1 1 0 0 1 12.5 8.5 V13 A1 1 0 0 1 11.5 14 H4.5 A1 1 0 0 1 3.5 13 V8.5 A1 1 0 0 1 4.5 7.5 Z M5.5 7.5 V5 A2.5 2.5 0 0 1 10.5 5 V7.5";
    public const string Unlock = "M4.5 7.5 H11.5 A1 1 0 0 1 12.5 8.5 V13 A1 1 0 0 1 11.5 14 H4.5 A1 1 0 0 1 3.5 13 V8.5 A1 1 0 0 1 4.5 7.5 Z M5.5 7.5 V5 A2.5 2.5 0 0 1 10.3 4";
    public const string Settings = "M8 5.6 A2.4 2.4 0 1 1 8 10.4 A2.4 2.4 0 1 1 8 5.6 Z M8 1.5 V3.5 M8 12.5 V14.5 M1.5 8 H3.5 M12.5 8 H14.5 M3.4 3.4 L4.8 4.8 M11.2 11.2 L12.6 12.6 M3.4 12.6 L4.8 11.2 M11.2 4.8 L12.6 3.4";
    public const string Pin = "M6 2.5 H10 M7 2.5 V6.5 L4.5 9 H11.5 L9 6.5 V2.5 M8 9 V14";
    public const string CloseWindows = "M4 4 L12 12 M12 4 L4 12";
    public const string Launch = "M9 2.5 H13.5 V7 M13.5 2.5 L7.5 8.5 M12 9.5 V12.5 A1 1 0 0 1 11 13.5 H3.5 A1 1 0 0 1 2.5 12.5 V5 A1 1 0 0 1 3.5 4 H6.5";
    public const string Exit = "M8 2 V7.5 M5 4 A5 5 0 1 0 11 4";
    public const string Position = "M8 2 V14 M2 8 H14 M6 4 L8 2 L10 4 M6 12 L8 14 L10 12 M4 6 L2 8 L4 10 M12 6 L14 8 L12 10";
    public const string ArrowUp = "M8 13 V3 M4 7 L8 3 L12 7";
    public const string ArrowDown = "M8 3 V13 M4 9 L8 13 L12 9";
    public const string Check = "M3 8.5 L6.5 12 L13 4.5";

    /// <summary>Menu icon: stroke bound to the owning MenuItem's foreground.</summary>
    public static Control MenuIcon(string data)
    {
        var path = new Path { Data = Geometry.Parse(data), Classes = { "menuIcon" } };
        path.Bind(Shape.StrokeProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(MenuItem) }
        });
        return path;
    }
}
