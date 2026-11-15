using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace CedroModernDock.Controls;

/// <summary>Animates icon content without changing the button's pointer hit area.</summary>
public sealed class DockItemButton : Button
{
    static DockItemButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<DockItemButton>(false);
    }

    public static readonly StyledProperty<double> HoverZoomProperty =
        AvaloniaProperty.Register<DockItemButton, double>(nameof(HoverZoom), 1.3);

    private readonly ScaleTransform _scale = new(1, 1)
    {
        Transitions = new Transitions
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new CubicEaseOut() },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new CubicEaseOut() }
        }
    };

    protected override Type StyleKeyOverride => typeof(Button);

    public double HoverZoom
    {
        get => GetValue(HoverZoomProperty);
        set => SetValue(HoverZoomProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            if (change.OldValue is Control oldContent && ReferenceEquals(oldContent.RenderTransform, _scale))
            {
                oldContent.ClearValue(RenderTransformProperty);
                oldContent.ClearValue(IsHitTestVisibleProperty);
            }
            if (Content is Control content)
            {
                content.RenderTransform = _scale;
                content.RenderTransformOrigin = RelativePoint.Center;
                content.IsHitTestVisible = false;
            }
        }
        if (change.Property == IsPointerOverProperty || change.Property == IsPressedProperty || change.Property == HoverZoomProperty)
        {
            double zoom = IsPressed ? Math.Max(1, HoverZoom * 0.9) : IsPointerOver ? HoverZoom : 1;
            _scale.ScaleX = zoom;
            _scale.ScaleY = zoom;
        }
    }
}
