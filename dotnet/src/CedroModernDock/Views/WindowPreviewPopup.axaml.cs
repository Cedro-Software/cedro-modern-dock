using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

/// <summary>
/// Popup listing a program item's open windows as live DWM thumbnails.
/// Each thumbnail lives in its own native top-level window (ThumbnailWindow):
/// DWM thumbnails composite above the host window's content and ignore window
/// regions, so rounded corners are done via DWMWA corner preference on those
/// native windows. The Avalonia window draws only the rows/titles.
/// </summary>
public partial class WindowPreviewPopup : Window
{
    private const int ThumbWidth = 144;
    private const int ThumbHeight = 81;
    // DWM clips live thumbnails at the system's fixed corner radius (~8px), so
    // the Avalonia rows must use the same radius for a consistent look.
    private const int MaxRounding = 8;
    // 75% of the popup width (144 thumbnail + 2*6 row padding + 2*4 panel margin).
    private const int TitleMaxWidth = 123;

    // Fast fade-in/out applied to the popup window and to each native thumbnail
    // (driven via layered-window alpha so the DWM previews fade in lock-step).
    private const double FadeMilliseconds = 120;
    private const double FadeTickMilliseconds = 12;

    private readonly List<PreviewRow> _rows = new();
    private IReadOnlyList<IntPtr> _sourceHandles = Array.Empty<IntPtr>();
    private string _appLabel = "";
    private string _colorRgb = "0, 0, 0, ";
    private double _transparency = 0.3;
    private int _rounding = 12;
    private IntPtr _hwnd;
    private bool _regPostQueued;
    private Button? _positionAnchor;
    private Visual? _positionDockBar;
    private bool _repositionPending;
    private bool _verticalDock;
    private DockHorizontalAnchor _horizontalAnchor = DockHorizontalAnchor.LEFT;
    private double _fadeValue = 1;
    private bool _fadeIn;
    private bool _fadeFinishHide;
    private bool _fadeRunning;
    private IPointer? _pressedPointer;
    private int _hoveredRowIndex = -1;
    private readonly DispatcherTimer _closeDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer _fadeTimer = new() { Interval = TimeSpan.FromMilliseconds(FadeTickMilliseconds) };

    /// <summary>
    /// One popup row: the Avalonia row area, its native thumbnail window and its
    /// native close button, plus the physical screen rect used to decide when
    /// the close button should be visible.
    /// </summary>
    private sealed class PreviewRow
    {
        public Border Row = null!;
        public Border Area = null!;
        public ThumbnailWindow? Thumb;
        public PreviewCloseButtonWindow? Close;
        public (int X, int Y, int Width, int Height) ScreenRect;
    }

    public WindowPreviewPopup()
    {
        InitializeComponent();
        _fadeTimer.Tick += (_, _) => OnFadeTick();
        // When the pointer leaves a row (or its close button), the close button
        // must not vanish while the cursor is still over the thumbnail, so the
        // hide is deferred and re-checked against the cursor position.
        _closeDebounceTimer.Tick += (_, _) => UpdateCloseButtons();
        // Position only once the window has a real layout size: before the first
        // Show the content tree is not attached (DesiredSize is 0), and after
        // BuildRows the measure is invalidated, so positioning in ShowFor would
        // anchor the popup to the item's edge instead of centering it.
        LayoutUpdated += (_, _) =>
        {
            if (!_repositionPending) return;
            _repositionPending = false;
            if (_positionAnchor is { } anchor && _positionDockBar is { } dockBar)
                PositionNear(anchor, dockBar);
        };
    }

    /// <summary>Invoked with the row's source HWND when a thumbnail row is clicked.</summary>
    public Action<IntPtr>? ThumbnailClicked { get; set; }

    /// <summary>Invoked with the row's source HWND when its close button is clicked.</summary>
    public Action<IntPtr>? CloseRequested { get; set; }

    /// <summary>Invoked when the pointer enters/exits the popup (keeps it open on hover).</summary>
    public Action? PointerEnteredCallback { get; set; }
    public Action? PointerExitedCallback { get; set; }

    /// <summary>
    /// Populates rows and positions the popup next to <paramref name="anchor"/>,
    /// outside the visible <paramref name="dockBar"/>.
    /// </summary>
    public void ShowFor(IReadOnlyList<WindowInfo> windows, string appLabel,
        string colorRgb, int rounding, double transparency, Button anchor, Visual dockBar,
        bool verticalDock = false, DockHorizontalAnchor horizontalAnchor = DockHorizontalAnchor.LEFT)
    {
        _verticalDock = verticalDock;
        _horizontalAnchor = horizontalAnchor;
        _appLabel = appLabel;
        _colorRgb = colorRgb;
        _rounding = rounding;
        _transparency = transparency;
        _sourceHandles = new List<IntPtr>(windows.Select(w => w.Handle));

        bool wasVisible = IsVisible;
        _positionAnchor = anchor;
        _positionDockBar = dockBar;
        _repositionPending = true;
        // Cancel any in-flight fade so the new preview starts from a clean,
        // fully opaque state (e.g. re-showing right after a hide began).
        CancelFade();
        BuildRows(windows);
        if (wasVisible) PositionNear(anchor, dockBar);
        if (!wasVisible)
        {
            // Fade in from transparent on first show.
            _fadeValue = 0;
            Opacity = 0;
        }
        Show();
        if (!wasVisible)
        {
            StartFade(true, finishHide: false);
        }
        else if (!_regPostQueued)
        {
            _regPostQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _regPostQueued = false;
                RegisterThumbnails();
            }, DispatcherPriority.Loaded);
        }
    }

    private void BuildRows(IReadOnlyList<WindowInfo> windows)
    {
        ClearRows();
        _rows.Clear();

        var parts = _colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        byte alpha = (byte)(_transparency * 255);
        var rowBrush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        int rounding = Math.Min(_rounding, MaxRounding);
        var textBrush = WindowTitleFormatter.IsDarkBackground(_colorRgb)
            ? (IBrush)new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
            : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));

        foreach (var window in windows)
        {
            // Placeholder sizing the row; the real thumbnail is a native
            // ThumbnailWindow positioned over this area at screen coordinates.
            var thumbArea = new Border { Width = ThumbWidth, Height = ThumbHeight, Background = Brushes.Transparent };
            var title = new TextBlock
            {
                Text = WindowTitleFormatter.Format(window.Title, _appLabel),
                Foreground = textBrush,
                FontSize = 12,
                // Cap the title at 75% of the popup width: long titles must
                // never widen the popup beyond the previews it contains.
                MaxWidth = TitleMaxWidth,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
            var content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(thumbArea);
            content.Children.Add(title);

            var row = new Border
            {
                CornerRadius = new CornerRadius(rounding),
                // Same background and transparency as the dock itself.
                Background = rowBrush,
                Padding = new Thickness(6, 6, 6, 0),
                Child = content
            };
            RowsPanel.Children.Add(row);
            var previewRow = new PreviewRow { Row = row, Area = thumbArea };
            row.PointerPressed += (_, e) =>
            {
                // The press implicitly captures the pointer; when the popup
                // hides right after (click-to-activate), the capture must be
                // released or the dock stops receiving hover until clicked.
                _pressedPointer = e.Pointer;
                ThumbnailClicked?.Invoke(window.Handle);
            };
            // Hovering a row reveals its close button; leaving defers to the
            // cursor-position check in UpdateCloseButtons so the button does not
            // vanish while the cursor is over the thumbnail or the button itself.
            row.PointerEntered += (_, _) => ShowCloseButton(previewRow);
            row.PointerExited += (_, _) => ScheduleCloseButtonCheck();
            _rows.Add(previewRow);
        }
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            row.Thumb?.Dispose();
            row.Close?.Dispose();
        }
        _rows.Clear();
        RowsPanel.Children.Clear();
        _hoveredRowIndex = -1;
    }

    private void PositionNear(Button anchor, Visual dockBar)
    {
        Measure(Size.Infinity);
        double scale = RenderScaling;
        int w = (int)(DesiredSize.Width * scale);
        int h = (int)(DesiredSize.Height * scale);

        var anchorCenter = anchor.PointToScreen(new Point(anchor.Bounds.Width / 2, anchor.Bounds.Height / 2));

        var screens = Screens;
        var screen = screens.ScreenFromPoint(anchorCenter);
        if (screen is null && screens.All.Count > 0)
            screen = screens.All[0];

        // The popup must sit outside the visible dock bar (not the dock window,
        // whose transparent zoom headroom surrounds the bar). With a small dock
        // padding the hover-zoomed button grows past the bar's edge, so the
        // popup also clears it (DIPs). The button's layout rect is read through
        // its parent so the hover RenderTransform (possibly mid-animation) does
        // not skew it.
        double clearBelow = 0, clearAbove = 0, clearLeft = 0, clearRight = 0;
        if (anchor.GetVisualParent() is { } parent && parent.TransformToVisual(dockBar) is { } toBar)
        {
            var rect = anchor.Bounds.TransformToAABB(toBar);
            double growX = rect.Width * (MainWindowViewModel.HoverZoomScale - 1) / 2;
            double growY = rect.Height * (MainWindowViewModel.HoverZoomScale - 1) / 2;
            clearAbove = Math.Max(0, growY - rect.Top);
            clearBelow = Math.Max(0, rect.Bottom + growY - dockBar.Bounds.Height);
            clearLeft = Math.Max(0, growX - rect.Left);
            clearRight = Math.Max(0, rect.Right + growX - dockBar.Bounds.Width);
        }
        var barTopLeft = dockBar.PointToScreen(new Point(0, 0));
        var barBottomRight = dockBar.PointToScreen(new Point(dockBar.Bounds.Width, dockBar.Bounds.Height));

        // Place below the dock by default; above when the dock is in the lower half.
        int popupX = anchorCenter.X - w / 2;
        int popupY = barBottomRight.Y + (int)(clearBelow * scale) + 4;
        if (screen is not null)
        {
            var work = screen.WorkingArea;
            if (_verticalDock)
            {
                // Vertical dock: popup to the left/right of the dock, flush
                // against the bar's outer edge with the same 4px gap horizontal
                // mode uses below the dock. Dock on the left edge -> popup on
                // the right; dock on the right edge -> popup on the left.
                int popupRightX = barBottomRight.X + (int)(clearRight * scale) + 4;
                int popupLeftX = barTopLeft.X - (int)(clearLeft * scale) - w - 4;

                bool placeRight = _horizontalAnchor != DockHorizontalAnchor.RIGHT;
                int px = placeRight ? popupRightX : popupLeftX;
                int py = anchorCenter.Y - h / 2;

                if (placeRight && px + w > work.Right)
                    px = popupLeftX;
                else if (!placeRight && px < work.X)
                    px = popupRightX;

                popupX = Math.Max(work.X + 4, Math.Min(px, work.Right - w - 4));
                popupY = Math.Max(work.Y + 4, Math.Min(py, work.Bottom - h - 4));
            }
            else
            {
                if (popupY + h > work.Bottom)
                    popupY = barTopLeft.Y - (int)(clearAbove * scale) - h - 4;

                popupX = Math.Max(work.X + 4, Math.Min(popupX, work.Right - w - 4));
                popupY = Math.Max(work.Y + 4, popupY);
            }
        }
        Position = new PixelPoint(popupX, popupY);
    }

    /// <summary>
    /// Single registration path: layout must be final before creating the native
    /// thumbnail windows (they are positioned at the row areas' screen coords),
    /// so defer to Loaded priority. Source HWNDs were stored by ShowFor.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        // Never steal focus: same extended styles as DockWindowBehavior.ApplyExtendedStyles.
        if (_hwnd != IntPtr.Zero)
        {
            IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
            int exStyle = exStylePtr.ToInt32();
            exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
            exStyle &= ~Win32Constants.WS_EX_APPWINDOW;
            User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        Dispatcher.UIThread.Post(RegisterThumbnails, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Creates one native ThumbnailWindow per row, positioned over the row's
    /// thumbnail area at physical screen coordinates. Rounded corners are the
    /// DWM corner preference applied by ThumbnailWindow itself.
    /// </summary>
    private void RegisterThumbnails()
    {
        if (_hwnd == IntPtr.Zero) return;
        double scale = RenderScaling;

        for (int i = 0; i < _rows.Count && i < _sourceHandles.Count; i++)
        {
            if (_rows[i].Thumb != null) continue;
            var area = _rows[i].Area;
            var matrix = area.TransformToVisual(this);
            if (matrix == null) continue;
            var bounds = area.Bounds.TransformToAABB(matrix.Value);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;

            IntPtr sourceHwnd = _sourceHandles[i];
            if (sourceHwnd == IntPtr.Zero) continue;

            // Physical pixels on screen: window-relative bounds + window position.
            int px = (int)(Position.X + bounds.X * scale);
            int py = (int)(Position.Y + bounds.Y * scale);
            int pw = (int)Math.Ceiling(bounds.Width * scale);
            int ph = (int)Math.Ceiling(bounds.Height * scale);

            ThumbnailWindow thumb;
            try
            {
                // Same rounding rule as the Avalonia rows: follow the dock
                // rounding, capped at the DWM maximum, with no minimum.
                int rounding = Math.Min(_rounding, MaxRounding);
                thumb = new ThumbnailWindow(sourceHwnd, px, py, pw, ph, rounding);
                if (_fadeValue < 1)
                    thumb.SetOpacity((byte)Math.Clamp(_fadeValue * 255, 0, 255));
            }
            catch (Exception)
            {
                continue;
            }
            var row = _rows[i];
            row.Thumb = thumb;

            // The close button overlays the thumbnail's top-right corner. It is
            // hidden until the row is hovered; clicking it requests closing the
            // source window. It lives in its own native window so it renders
            // above the DWM thumbnail (see PreviewCloseButtonWindow).
            var close = new PreviewCloseButtonWindow(px + pw - PreviewCloseButtonWindow.Size - 2, py + 2);
            close.Clicked += () => CloseRequested?.Invoke(sourceHwnd);
            close.PointerExited += () => ScheduleCloseButtonCheck();
            row.Close = close;

            // Hover keep-alive uses the whole row's screen rect (thumbnail +
            // title), so the close button stays while the cursor is anywhere
            // over the row, not just the thumbnail pixels. PointToScreen gives
            // the physical screen position directly; using Bounds with a
            // TransformToVisual matrix would double-count the row's offset
            // within the panel for every row below the first.
            var topLeft = row.Row.PointToScreen(new Point(0, 0));
            row.ScreenRect = (
                topLeft.X,
                topLeft.Y,
                (int)Math.Ceiling(row.Row.Bounds.Width * scale),
                (int)Math.Ceiling(row.Row.Bounds.Height * scale));
        }

        // After a rebuild the pointer may already be over a row (e.g. when a
        // window was closed and the popup re-shows): sync the close buttons to
        // the cursor so the hovered row's button is visible immediately.
        UpdateCloseButtons();
    }

    /// <summary>
    /// Shows a row's close button and hides every other row's, remembering which
    /// row is hovered so a delayed check does not re-hide the active one.
    /// </summary>
    private void ShowCloseButton(PreviewRow row)
    {
        _closeDebounceTimer.Stop();
        _hoveredRowIndex = _rows.IndexOf(row);
        UpdateCloseButtons();
    }

    /// <summary>Defers the close-button visibility check (see UpdateCloseButtons).</summary>
    private void ScheduleCloseButtonCheck()
    {
        _closeDebounceTimer.Stop();
        _closeDebounceTimer.Start();
    }

    /// <summary>
    /// Keeps the hovered row's close button visible while the cursor is over the
    /// row's thumbnail or its close button, and hides the others. Runs when a row
    /// is entered or exited (the native thumbnail and close button are separate
    /// top-level windows, so leaving a row onto either fires PointerExited).
    /// </summary>
    private void UpdateCloseButtons()
    {
        _closeDebounceTimer.Stop();
        if (!IsVisible || _rows.Count == 0) return;
        if (!User32.GetCursorPos(out POINT pt))
        {
            HideAllCloseButtons();
            return;
        }

        // The pointer can be over the hovered row's thumbnail, over its close
        // button (a separate native window), or on a different row entirely.
        int activeRow = -1;
        if (_hoveredRowIndex >= 0 && _hoveredRowIndex < _rows.Count)
        {
            var hovered = _rows[_hoveredRowIndex];
            if (IsPointOverRow(hovered, pt.X, pt.Y))
                activeRow = _hoveredRowIndex;
        }
        if (activeRow < 0)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (IsPointOverRow(_rows[i], pt.X, pt.Y))
                {
                    activeRow = i;
                    break;
                }
            }
        }
        _hoveredRowIndex = activeRow;

        for (int i = 0; i < _rows.Count; i++)
        {
            var close = _rows[i].Close;
            if (close == null) continue;
            if (i == activeRow) close.Show();
            else close.Hide();
        }
    }

    private bool IsPointOverRow(PreviewRow row, int x, int y)
    {
        var (rx, ry, rw, rh) = row.ScreenRect;
        bool overThumb = x >= rx && x < rx + rw && y >= ry && y < ry + rh;
        return overThumb || (row.Close != null && row.Close.ContainsPoint(x, y));
    }

    private void HideAllCloseButtons()
    {
        foreach (var row in _rows)
            row.Close?.Hide();
    }

    /// <summary>
    /// True when the physical screen point is over the popup, over any native
    /// thumbnail window, or over any close button window (used to keep the popup
    /// open while hovering thumbnails/buttons, which are separate top-level windows).
    /// </summary>
    public bool IsPointOverPopup(int x, int y)
    {
        int w = (int)(Width * RenderScaling);
        int h = (int)(Height * RenderScaling);
        if (x >= Position.X && x < Position.X + w && y >= Position.Y && y < Position.Y + h)
            return true;
        foreach (var row in _rows)
        {
            if (row.Thumb != null && row.Thumb.ContainsPoint(x, y))
                return true;
            if (row.Close != null && row.Close.ContainsPoint(x, y))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when the shown rows already target exactly these source windows,
    /// so re-hovering the same item can keep the live thumbnails untouched.
    /// </summary>
    public bool MatchesSource(IReadOnlyList<WindowInfo> windows)
    {
        if (!IsVisible || windows.Count != _sourceHandles.Count) return false;
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].Handle != _sourceHandles[i]) return false;
        }
        return true;
    }

    public void HidePopup()
    {
        // Fade out, then tear down the rows and hide the window. If the pointer
        // returns during the fade (hover), CancelHide re-shows instead.
        if (!IsVisible) return;
        ReleasePressedCapture();
        StartFade(false, finishHide: true);
    }

    /// <summary>
    /// Hides immediately without fading. Used when a thumbnail is clicked to
    /// activate a window: the preview must vanish at once (taskbar-style),
    /// not linger half-faded above the newly opened window.
    /// </summary>
    public void HideNow()
    {
        if (!IsVisible) return;
        if (_fadeRunning)
        {
            _fadeTimer.Stop();
            _fadeRunning = false;
            _fadeFinishHide = false;
        }
        ReleasePressedCapture();
        ClearRows();
        Hide();
        Opacity = 1;
        _fadeValue = 1;
    }

    private void ReleasePressedCapture()
    {
        if (_pressedPointer is null) return;
        _pressedPointer.Capture(null);
        _pressedPointer = null;
    }

    private void StartFade(bool fadeIn, bool finishHide)
    {
        _fadeIn = fadeIn;
        _fadeFinishHide = finishHide;
        _fadeRunning = true;
        if (!_fadeTimer.IsEnabled)
            _fadeTimer.Start();
    }

    /// <summary>Reverses an in-progress fade-out (pointer returned over the popup).</summary>
    public void CancelHide()
    {
        if (!_fadeRunning || _fadeIn) return;
        StartFade(true, finishHide: false);
    }

    /// <summary>
    /// Stops any running fade and snaps the popup to full opacity. Called when
    /// the popup is re-shown for a different item, so a pending fade-out cannot
    /// hide the freshly built preview.
    /// </summary>
    private void CancelFade()
    {
        if (!_fadeRunning) return;
        _fadeTimer.Stop();
        _fadeRunning = false;
        _fadeFinishHide = false;
        _fadeValue = 1;
        Opacity = 1;
    }

    private void OnFadeTick()
    {
        double step = FadeTickMilliseconds / FadeMilliseconds;
        _fadeValue = _fadeIn ? Math.Min(1, _fadeValue + step) : Math.Max(0, _fadeValue - step);
        ApplyFadeOpacity(_fadeValue);
        if ((_fadeIn && _fadeValue >= 1) || (!_fadeIn && _fadeValue <= 0))
        {
            _fadeTimer.Stop();
            _fadeRunning = false;
            if (!_fadeIn && _fadeFinishHide)
                FinishHide();
        }
    }

    private void ApplyFadeOpacity(double value)
    {
        Opacity = value;
        byte alpha = (byte)Math.Clamp(value * 255, 0, 255);
        foreach (var row in _rows)
            row.Thumb?.SetOpacity(alpha);
    }

    private void FinishHide()
    {
        _fadeValue = 0;
        ReleasePressedCapture();
        ClearRows();
        Hide();
        Opacity = 1;
        _fadeValue = 1;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e) => PointerEnteredCallback?.Invoke();

    private void OnPointerExited(object? sender, PointerEventArgs e) => PointerExitedCallback?.Invoke();
}
