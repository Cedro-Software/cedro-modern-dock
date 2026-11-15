using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Look shared by every dock item (hover zoom, running
/// indicator, separators, tooltip placement). One instance is owned by the
/// dock and referenced by all item view models, so a change updates the
/// items in place without rebuilding them.
/// </summary>
public sealed class DockVisualStyle : ViewModelBase
{
    // --- Hover zoom ---------------------------------------------------------

    private double _hoverZoom = 1.3;
    public double HoverZoom
    {
        get => _hoverZoom;
        set
        {
            if (SetProperty(ref _hoverZoom, value))
            {
                OnPropertyChanged(nameof(HoverTransform));
                OnPropertyChanged(nameof(PressedTransform));
                UpdateTooltipOffsets();
            }
        }
    }

    public TransformOperations HoverTransform => Scale(HoverZoom);

    /// <summary>Slight press-down feedback relative to the hover size.</summary>
    public TransformOperations PressedTransform => Scale(Math.Max(1.0, HoverZoom * 0.9));

    private static TransformOperations Scale(double factor)
        => TransformOperations.Parse($"scale({factor.ToString("0.###", CultureInfo.InvariantCulture)})");

    // --- Layout context -----------------------------------------------------

    private int _iconSize = 48;
    public int IconSize
    {
        get => _iconSize;
        set { if (SetProperty(ref _iconSize, value)) { UpdateIndicator(); UpdateSeparator(); UpdateTooltipOffsets(); } }
    }

    private bool _isVerticalDock;
    public bool IsVerticalDock
    {
        get => _isVerticalDock;
        set { if (SetProperty(ref _isVerticalDock, value)) { UpdateSeparator(); UpdateTooltipOffsets(); } }
    }

    // --- Running indicator --------------------------------------------------

    private string _indicatorStyle = "dot";
    private double _indicatorSize = 4;
    private Color _indicatorColor = Colors.White;
    private Bitmap? _indicatorImage;

    public void SetIndicator(string style, double size, Color color, Bitmap? image)
    {
        _indicatorStyle = style;
        _indicatorSize = size;
        _indicatorColor = color;
        _indicatorImage = image;
        UpdateIndicator();
    }

    public bool IndicatorIsShape { get; private set; }
    public bool IndicatorIsImage { get; private set; }
    public Bitmap? IndicatorImage => _indicatorImage;
    public double IndicatorWidth { get; private set; }
    public double IndicatorHeight { get; private set; }
    public CornerRadius IndicatorCornerRadius { get; private set; }
    public IBrush IndicatorBrush { get; private set; } = Brushes.White;
    public BoxShadows IndicatorGlow { get; private set; }
    /// <summary>Height reserved under every icon so running and idle icons stay aligned.</summary>
    public double IndicatorSlotHeight { get; private set; }

    private void UpdateIndicator()
    {
        double s = _indicatorSize;
        IndicatorIsImage = _indicatorStyle == "image" && _indicatorImage != null;
        IndicatorIsShape = !IndicatorIsImage && _indicatorStyle != "none";
        IndicatorBrush = new SolidColorBrush(_indicatorColor);
        IndicatorGlow = default;
        switch (_indicatorStyle)
        {
            case "line":
                IndicatorWidth = Math.Round(IconSize * 0.42);
                IndicatorHeight = Math.Max(2, Math.Round(s * 0.6));
                break;
            case "pill":
                IndicatorWidth = Math.Round(s * 3.2);
                IndicatorHeight = s;
                break;
            case "glow":
                IndicatorWidth = s;
                IndicatorHeight = s;
                IndicatorGlow = new BoxShadows(new BoxShadow
                {
                    Blur = s * 2.2, Spread = s * 0.35, Color = _indicatorColor
                });
                break;
            case "image":
                IndicatorWidth = Math.Round(IconSize * 0.5);
                IndicatorHeight = Math.Max(4, s * 1.5);
                break;
            case "none":
                IndicatorWidth = 0;
                IndicatorHeight = 0;
                break;
            default: // dot
                IndicatorWidth = s;
                IndicatorHeight = s;
                break;
        }
        IndicatorCornerRadius = new CornerRadius(Math.Min(IndicatorWidth, IndicatorHeight) / 2);
        IndicatorSlotHeight = _indicatorStyle == "none" ? 0 : IndicatorHeight;
        RaiseAll(nameof(IndicatorIsShape), nameof(IndicatorIsImage), nameof(IndicatorImage),
            nameof(IndicatorWidth), nameof(IndicatorHeight), nameof(IndicatorCornerRadius),
            nameof(IndicatorBrush), nameof(IndicatorGlow), nameof(IndicatorSlotHeight));
    }

    // --- Separators ---------------------------------------------------------

    private string _separatorStyle = "classic";
    private Color _separatorColor = Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
    private double _separatorThickness = 1;
    private Bitmap? _separatorImage;
    private Bitmap? _separatorImageVertical;

    public void SetSeparator(string style, Color color, double thickness, Bitmap? image, Bitmap? imageVertical)
    {
        _separatorStyle = style;
        _separatorColor = color;
        _separatorThickness = thickness;
        _separatorImage = image;
        _separatorImageVertical = imageVertical ?? image;
        UpdateSeparator();
    }

    public double SeparatorSlotWidth { get; private set; }
    public double SeparatorSlotHeight { get; private set; }
    public bool SeparatorIsImage { get; private set; }
    public bool SeparatorIsLine { get; private set; }
    public bool SeparatorIsDots { get; private set; }
    public Bitmap? SeparatorImage { get; private set; }
    public IBrush SeparatorBrush { get; private set; } = Brushes.White;
    public double SeparatorLineWidth { get; private set; }
    public double SeparatorLineHeight { get; private set; }
    public double SeparatorDotSize { get; private set; }
    public Avalonia.Layout.Orientation SeparatorDotsOrientation { get; private set; }

    private void UpdateSeparator()
    {
        bool v = IsVerticalDock;
        double across = IconSize; // extent along the dock's cross axis
        SeparatorIsImage = _separatorStyle is "classic" or "image";
        SeparatorIsLine = _separatorStyle == "line";
        SeparatorIsDots = _separatorStyle == "dots";
        SeparatorImage = SeparatorIsImage ? (v ? _separatorImageVertical : _separatorImage) : null;
        SeparatorBrush = new SolidColorBrush(_separatorColor);
        double t = _separatorThickness;
        double along = _separatorStyle switch
        {
            "classic" or "image" => 22,
            "line" => t + 14,
            "dots" => t + 15,
            _ => 18 // space
        };
        SeparatorSlotWidth = v ? across : along;
        SeparatorSlotHeight = v ? along : across;
        double lineLength = Math.Round(IconSize * 0.72);
        SeparatorLineWidth = v ? lineLength : t;
        SeparatorLineHeight = v ? t : lineLength;
        SeparatorDotSize = t + 2;
        SeparatorDotsOrientation = v ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical;
        RaiseAll(nameof(SeparatorSlotWidth), nameof(SeparatorSlotHeight), nameof(SeparatorIsImage),
            nameof(SeparatorIsLine), nameof(SeparatorIsDots), nameof(SeparatorImage), nameof(SeparatorBrush),
            nameof(SeparatorLineWidth), nameof(SeparatorLineHeight), nameof(SeparatorDotSize),
            nameof(SeparatorDotsOrientation));
    }

    // --- Tooltips -----------------------------------------------------------

    private PlacementMode _tooltipPlacement = PlacementMode.Bottom;
    /// <summary>Tooltips open on the screen side away from the dock's edge.</summary>
    public PlacementMode TooltipPlacement
    {
        get => _tooltipPlacement;
        set { if (SetProperty(ref _tooltipPlacement, value)) UpdateTooltipOffsets(); }
    }

    public double TooltipHorizontalOffset { get; private set; }
    public double TooltipVerticalOffset { get; private set; }

    private void UpdateTooltipOffsets()
    {
        // Clear the hover-zoomed icon, which grows past the button's bounds.
        double gap = Math.Ceiling(IconSize * (HoverZoom - 1) / 2) + 6;
        (TooltipHorizontalOffset, TooltipVerticalOffset) = TooltipPlacement switch
        {
            PlacementMode.Top => (0.0, -gap),
            PlacementMode.Left => (-gap, 0.0),
            PlacementMode.Right => (gap, 0.0),
            _ => (0.0, gap)
        };
        RaiseAll(nameof(TooltipHorizontalOffset), nameof(TooltipVerticalOffset));
    }

    private void RaiseAll(params string[] names)
    {
        foreach (var n in names) OnPropertyChanged(n);
    }

    public DockVisualStyle()
    {
        UpdateIndicator();
        UpdateSeparator();
        UpdateTooltipOffsets();
    }
}
