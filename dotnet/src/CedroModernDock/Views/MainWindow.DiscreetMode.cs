using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Threading;
using CedroModernDock.Core.Application;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

public partial class MainWindow
{
    private readonly DispatcherTimer _discreetPollTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DiscreetModeState _discreetState = new();
    private readonly Stopwatch _discreetClock = Stopwatch.StartNew();
    private readonly ScaleTransform _discreetScale = new(1, 1)
    {
        Transitions = new Transitions
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new BackEaseOut() },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new BackEaseOut() }
        }
    };
    private bool _isDiscreetIdle;
    private IntPtr _discreetWindowHandle;

    private void ConfigureDiscreetMode()
    {
        bool enabled = _appServices?.AppearanceService.GetDiscreetMode() == true;
        if (enabled)
        {
            if (!_discreetPollTimer.IsEnabled) _discreetPollTimer.Start();
        }
        else
        {
            _discreetPollTimer.Stop();
            WakeDiscreetMode();
        }
    }

    public void WakeDiscreetMode()
    {
        _discreetState.Reset();
        SetDiscreetIdle(false);
    }

    private void OnDiscreetPollTick(object? sender, EventArgs e)
    {
        if (_appServices == null) return;
        // A failed cursor query must not fade a dock that might be in use.
        if (!User32.GetCursorPos(out var cursor)) return;
        SetDiscreetIdle(_discreetState.Update(_appServices.AppearanceService.GetDiscreetMode(), IsDiscreetInteractionActive(cursor),
            _appServices.AppearanceService.GetDiscreetModeDelay(), _discreetClock.Elapsed));
    }

    private bool IsDiscreetInteractionActive(POINT cursor)
        => IsPointerNearDock(cursor) || IsPointerOverPreview()
            || SettingsWindow.IsAnyOpen || _dockContextMenu?.IsOpen == true
            || _isDragging || OwnedWindows.Any(w => w.IsVisible);

    private bool IsPointerNearDock(POINT cursor)
    {
        const int padding = 24;
        if (_discreetWindowHandle != IntPtr.Zero && User32.GetWindowRect(_discreetWindowHandle, out var rect))
            return cursor.X >= rect.Left - padding && cursor.X <= rect.Right + padding
                && cursor.Y >= rect.Top - padding && cursor.Y <= rect.Bottom + padding;
        double scale = RenderScaling > 0 ? RenderScaling : 1;
        return cursor.X >= Position.X - padding && cursor.X <= Position.X + ClientSize.Width * scale + padding
            && cursor.Y >= Position.Y - padding && cursor.Y <= Position.Y + ClientSize.Height * scale + padding;
    }

    private void SetDiscreetIdle(bool idle)
    {
        if (_isDiscreetIdle == idle) return;
        _isDiscreetIdle = idle;
        if (idle) HidePreview();
        DockBar.Opacity = idle ? (_appServices?.AppearanceService.GetDiscreetModeOpacity() ?? 15) / 100.0 : 1;
        _discreetScale.ScaleX = idle ? 0.96 : 1;
        _discreetScale.ScaleY = idle ? 0.96 : 1;
    }
}
