using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CedroModernDock.Controls;

/// <summary>
/// Draws a bitmap with 9-slice scaling: the four corners keep their size, the
/// edges stretch along one axis and the center stretches both ways. This is
/// how classic dock skins (RocketDock, ObjectDock) draw their background.
/// </summary>
public sealed class NineSliceImage : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<NineSliceImage, Bitmap?>(nameof(Source));

    /// <summary>Non-stretched borders, in source image units (left, top, right, bottom).</summary>
    public static readonly StyledProperty<Thickness> SliceProperty =
        AvaloniaProperty.Register<NineSliceImage, Thickness>(nameof(Slice));

    static NineSliceImage()
    {
        AffectsRender<NineSliceImage>(SourceProperty, SliceProperty);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Thickness Slice
    {
        get => GetValue(SliceProperty);
        set => SetValue(SliceProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var source = Source;
        if (source == null) return;
        var src = source.Size;
        var dst = Bounds.Size;
        if (src.Width <= 0 || src.Height <= 0 || dst.Width <= 0 || dst.Height <= 0) return;

        // Clamp the slices so they never overlap, neither in the source nor
        // in a destination smaller than the image corners.
        double l = Math.Clamp(Slice.Left, 0, src.Width), r = Math.Clamp(Slice.Right, 0, src.Width - l);
        double t = Math.Clamp(Slice.Top, 0, src.Height), b = Math.Clamp(Slice.Bottom, 0, src.Height - t);
        double scaleX = l + r > dst.Width ? dst.Width / (l + r) : 1;
        double scaleY = t + b > dst.Height ? dst.Height / (t + b) : 1;
        double dl = l * scaleX, dr = r * scaleX, dt = t * scaleY, db = b * scaleY;

        double[] sx = { 0, l, src.Width - r, src.Width };
        double[] sy = { 0, t, src.Height - b, src.Height };
        double[] dx = { 0, dl, dst.Width - dr, dst.Width };
        double[] dy = { 0, dt, dst.Height - db, dst.Height };

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var s = new Rect(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                var d = new Rect(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]);
                if (s.Width <= 0 || s.Height <= 0 || d.Width <= 0 || d.Height <= 0) continue;
                context.DrawImage(source, s, d);
            }
        }
    }
}
