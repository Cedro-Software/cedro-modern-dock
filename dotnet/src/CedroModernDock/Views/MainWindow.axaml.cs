using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class MainWindow : Window
{
    private DockWindowBehavior? _dockBehavior;
    private AppServices? _appServices;
    private WindowPreviewPopup? _previewPopup;
    private Button? _hoveredButton;
    private Button? _previewAnchor;
    private int _previewRequestId;
    // Identity of the item whose preview is currently shown. _hoveredButton is
    // nulled while the pointer is over the popup, so the close handler cannot
    // rely on it; these are captured when the preview is requested.
    private string _previewExecutablePath = "";
    private string _previewLabel = "";
    private readonly DispatcherTimer _previewHideDebounce = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer _previewCloseRefresh = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _positionPersistTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public MainWindow()
    {
        InitializeComponent();
        PositionChanged += OnDockPositionChanged;
        _positionPersistTimer.Tick += (_, _) =>
        {
            _positionPersistTimer.Stop();
            PersistDockPosition();
        };
        // The popup hides only when the pointer is neither over the popup nor
        // over a native thumbnail window (thumbnails are separate top-level
        // windows, so leaving the popup onto a thumbnail fires PointerExited).
        _previewHideDebounce.Tick += (_, _) =>
        {
            _previewHideDebounce.Stop();
            if (IsPointerOverPreview())
                _previewHideDebounce.Start();
            else
                HidePreview();
        };
        // After a close request the window may take a moment to disappear (it
        // can show a save prompt), so the row refresh is deferred until it has.
        _previewCloseRefresh.Tick += (_, _) =>
        {
            _previewCloseRefresh.Stop();
            RefreshPreviewAfterClose();
        };
    }

    private bool IsPointerOverPreview()
    {
        if (_previewPopup is not { IsVisible: true } popup) return false;
        if (!User32.GetCursorPos(out POINT pt)) return false;
        return popup.IsPointOverPopup(pt.X, pt.Y);
    }

    /// <summary>Receives the composed application services from the App composition root.</summary>
    public void SetAppServices(AppServices appServices) => _appServices = appServices;

    /// <summary>
    /// Once the native window is created and shown, grab its HWND and apply
    /// the dock-specific Win32 behavior (no-activate, no-taskbar, Win+D defense).
    /// Also applies the saved dock position from the positioning service.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        IPlatformHandle? handle = this.TryGetPlatformHandle();
        if (handle is null)
        {
            UpdateStatus("ERROR: Could not obtain native window handle");
            return;
        }

        _dockBehavior = new DockWindowBehavior(handle.Handle, UpdateStatus);
        _dockBehavior.Apply();

        // Initialize the dock ViewModel (loads items, starts indicator watcher).
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenSettingsAction = () => OpenSettings(vm);
            vm.RepositionAction = () => ApplyDockPosition();
            vm.PreviewDismissAction = HidePreview;
            vm.Initialize();
        }

        ApplyDockPosition(force: true);

        // Static anchors must use the finalized window size, which SizeToContent
        // only produces after the first layout pass. Re-apply once layout settles
        // and whenever the dock content resizes the window.
        if (_appServices?.PositioningService.IsDynamicPositioning() == false)
        {
            SizeChanged += OnDockSizeChanged;
            Dispatcher.UIThread.Post(() => ApplyDockPosition(), DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Applies the dock position from the positioning service. In STATIC mode
    /// this re-anchors the dock to the current screen edge; in DYNAMIC mode the
    /// saved position is only applied once at startup (force) so the user's
    /// drags are never overridden by subsequent refreshes.
    /// </summary>
    private void ApplyDockPosition(bool force = false)
    {
        if (_appServices == null) return;
        if (!force && _appServices.PositioningService.IsDynamicPositioning()) return;
        // Anchors and the saved position refer to the visible bar, which sits
        // inside the window's zoom headroom.
        double headroom = DockBar.Margin.Left;
        var (x, y) = _appServices.PositioningService.ResolvePosition(
            Width - 2 * headroom, Height - 2 * headroom);
        int offset = ZoomHeadroomPixels;
        Position = new PixelPoint((int)x - offset, (int)y - offset);
    }

    private int ZoomHeadroomPixels => (int)Math.Round(DockBar.Margin.Left * RenderScaling);

    /// <summary>
    /// Screen position (physical px) of the visible dock bar: the window
    /// position plus the transparent zoom headroom around the bar. Persisted
    /// dock positions use it, so they do not depend on the headroom.
    /// </summary>
    public PixelPoint DockBarPosition
    {
        get
        {
            int offset = ZoomHeadroomPixels;
            return new PixelPoint(Position.X + offset, Position.Y + offset);
        }
    }

    private void OnDockSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_appServices?.PositioningService.IsDynamicPositioning() == false)
            ApplyDockPosition();
    }

    private void OnItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || _appServices == null) return;
        if (button.DataContext is not DockItemViewModel vm || vm.Item is not DockProgramItemModel item)
            return;

        _hoveredButton = button;
        _previewHideDebounce.Stop();
        int requestId = ++_previewRequestId;
        var programItem = item;
        string label = vm.Label;
        _previewExecutablePath = programItem.ExecutablePath;
        _previewLabel = label;

        Task.Run(() =>
        {
            List<WindowInfo> windows;
            try
            {
                windows = _appServices.WindowPreviewService.LoadPreview(programItem);
            }
            catch (Exception)
            {
                windows = new List<WindowInfo>();
            }
            Dispatcher.UIThread.Post(() => OnPreviewLoaded(requestId, button, label, windows));
        });
    }

    private void OnRunningAppPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || _appServices == null) return;
        if (button.DataContext is not RunningAppViewModel vm) return;

        _hoveredButton = button;
        _previewHideDebounce.Stop();
        int requestId = ++_previewRequestId;
        string executablePath = vm.ExecutablePath;
        string label = vm.Label;
        _previewExecutablePath = executablePath;
        _previewLabel = label;

        Task.Run(() =>
        {
            List<WindowInfo> windows;
            try
            {
                // Build a throwaway program item to reuse the preview loader.
                windows = _appServices.WindowPreviewService.LoadPreview(
                    new DockProgramItemModel(label, executablePath));
            }
            catch (Exception)
            {
                windows = new List<WindowInfo>();
            }
            Dispatcher.UIThread.Post(() => OnPreviewLoaded(requestId, button, label, windows));
        });
    }

    /// <summary>
    /// Clicking an unpinned running app activates its window when it has exactly
    /// one open window (taskbar-style); with multiple windows nothing happens —
    /// the preview popup still offers per-window control via hover.
    /// </summary>
    private void OnRunningAppClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || _appServices == null) return;
        if (button.DataContext is not RunningAppViewModel vm) return;

        string executablePath = vm.ExecutablePath;
        List<WindowInfo> windows;
        try
        {
            windows = _appServices.WindowPreviewService.LoadPreview(
                new DockProgramItemModel(vm.Label, executablePath));
        }
        catch (Exception)
        {
            return;
        }
        if (windows.Count != 1) return;

        ++_previewRequestId;
        _previewPopup?.HideNow();
        _hoveredButton = null;
        _appServices.WindowPreviewService.Activate(windows[0]);
    }

    private void OnPreviewLoaded(int requestId, Button button, string label,
        List<WindowInfo> windows)
    {
        if (requestId != _previewRequestId || _hoveredButton != button) return;
        if (windows.Count == 0)
        {
            if (_previewPopup?.IsVisible == true)
                HidePreview();
            return;
        }
        if (_appServices == null) return;
        _previewAnchor = button;

        var appearance = _appServices.AppearanceService;
        if (_previewPopup == null)
        {
            _previewPopup = new WindowPreviewPopup();
            _previewPopup.ThumbnailClicked = hwnd => OnPopupThumbnailClicked(hwnd);
            _previewPopup.CloseRequested = hwnd => OnPopupCloseRequested(hwnd);
            _previewPopup.PointerEnteredCallback = OnPopupPointerEntered;
            _previewPopup.PointerExitedCallback = OnPopupPointerExited;
        }
        // Re-hovering the same item (e.g. after visiting the popup) must not
        // tear down and rebuild the live thumbnails: that races with the hide
        // debounce and can leave rows blank. Keep the already-shown popup.
        if (_previewPopup.IsVisible && _previewPopup.MatchesSource(windows))
        {
            _previewPopup.CancelHide();
            return;
        }
        _previewPopup.ShowFor(windows, label,
            appearance.GetDockColorRGB(), appearance.GetDockBorderRounding(),
            appearance.GetDockTransparencyPercentage() / 100.0, button, DockBar,
            verticalDock: _appServices.AppearanceService.GetVerticalDock(),
            horizontalAnchor: _appServices.PositioningService.GetHorizontalAnchor());
    }

    private void OnItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (_hoveredButton == sender) _hoveredButton = null;
        _previewHideDebounce.Stop();
        // During fast icon-to-icon moves the previous icon's Exit can be
        // delivered AFTER the next icon's Enter. Re-arming the hide timer then
        // hides the popup 80ms later (the cursor is over the new icon, not the
        // popup) and the requestId bump drops the pending preview load — the
        // popup would never show again until the pointer leaves and re-enters.
        if (_hoveredButton == null)
            _previewHideDebounce.Start();
    }

    private void HidePreview()
    {
        ++_previewRequestId;
        _previewPopup?.HidePopup();
        _hoveredButton = null;
    }

    private void OnPopupPointerEntered()
    {
        _previewHideDebounce.Stop();
        // Pointer returned during a fade-out: reverse it instead of hiding.
        _previewPopup?.CancelHide();
    }

    private void OnPopupPointerExited()
    {
        _previewHideDebounce.Stop();
        // Returning to a dock item (pointer now over the button) fires this
        // after OnItemPointerEntered already re-stopped the hide timer. Don't
        // re-arm it in that case: the item's own pointer handling owns the
        // hide decision, and re-arming here hides the popup 90ms later even
        // though the cursor is over the very item that shows the preview.
        if (_hoveredButton == null)
            _previewHideDebounce.Start();
    }

    private void OnPopupThumbnailClicked(IntPtr sourceHwnd)
    {
        // Click-to-activate: hide instantly (no fade), release the pointer
        // capture held by the pressed row, then bring the window forward.
        ++_previewRequestId;
        _previewPopup?.HideNow();
        _hoveredButton = null;
        if (sourceHwnd != IntPtr.Zero)
            _appServices?.WindowPreviewService.Activate(new WindowInfo(sourceHwnd, ""));
    }

    /// <summary>
    /// Close button clicked: ask the source window to close (WM_CLOSE, the same
    /// message the taskbar sends) and defer a row refresh so the popup reflects
    /// the window actually disappearing.
    /// </summary>
    private void OnPopupCloseRequested(IntPtr sourceHwnd)
    {
        if (sourceHwnd == IntPtr.Zero || _appServices == null) return;
        _appServices.WindowPreviewService.Close(new WindowInfo(sourceHwnd, ""));
        _previewCloseRefresh.Stop();
        _previewCloseRefresh.Start();
    }

    /// <summary>
    /// Re-queries the shown item's open windows after a close request: if any
    /// remain, the popup rows are rebuilt without the closed window; if the
    /// closed window was the last one, the whole popup hides. Runs on the UI
    /// thread with the source query executed off it.
    /// </summary>
    private void RefreshPreviewAfterClose()
    {
        if (_appServices == null || _previewPopup == null) return;
        if (_previewPopup.IsVisible != true) return;

        string executablePath = _previewExecutablePath;
        if (string.IsNullOrEmpty(executablePath)) return;

        int requestId = ++_previewRequestId;
        string label = _previewLabel;
        Task.Run(() =>
        {
            List<WindowInfo> windows;
            try
            {
                windows = _appServices.WindowPreviewService.LoadPreview(
                    new DockProgramItemModel(label, executablePath));
            }
            catch (Exception)
            {
                windows = new List<WindowInfo>();
            }
            Dispatcher.UIThread.Post(() => OnPreviewRefreshed(requestId, label, windows));
        });
    }

    private void OnPreviewRefreshed(int requestId, string label, List<WindowInfo> windows)
    {
        if (requestId != _previewRequestId || _previewPopup is not { IsVisible: true }) return;
        if (_previewPopup == null) return;
        if (windows.Count == 0)
        {
            ++_previewRequestId;
            _previewPopup.HideNow();
            _hoveredButton = null;
            return;
        }
        if (_appServices == null) return;
        // Rebuild the popup without the closed window. The pointer is over the
        // popup, so it stays open; CancelHide guards against an in-flight fade.
        var anchor = _previewAnchor ?? _hoveredButton;
        if (anchor == null) return;
        _previewPopup.CancelHide();
        var appearance = _appServices.AppearanceService;
        _previewPopup.ShowFor(windows, label,
            appearance.GetDockColorRGB(), appearance.GetDockBorderRounding(),
            appearance.GetDockTransparencyPercentage() / 100.0, anchor, DockBar,
            verticalDock: _appServices.AppearanceService.GetVerticalDock(),
            horizontalAnchor: _appServices.PositioningService.GetHorizontalAnchor());
    }

    /// <summary>Allows dragging the borderless dock window (DYNAMIC mode only).</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't start a window drag when pressing a dock item button — the
        // button must receive the click. Drag only when pressing the dock
        // background itself.
        if (!e.Pointer.IsPrimary)
            return;
        if (e.Source is Visual source && source.FindAncestorOfType<Button>() != null)
            return;
        // The zoom headroom around the bar is transparent: pressing it must not
        // drag the dock.
        if (!new Rect(DockBar.Bounds.Size).Contains(e.GetPosition(DockBar)))
            return;
        // In STATIC mode the dock is anchored to the screen and must not be
        // dragged; dragging is only meaningful in DYNAMIC mode.
        if (_appServices == null || !_appServices.PositioningService.IsDynamicPositioning())
            return;
        BeginMoveDrag(e);
    }

    /// <summary>
    /// PositionChanged fires for every move — including each WM_MOVE delivered
    /// while the OS move-drag loop runs. The drag blocks the UI thread, so a
    /// DispatcherTimer restart here only ticks after the drag ends, persisting
    /// the final position exactly once instead of on every pixel of the drag.
    /// </summary>
    private void OnDockPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_appServices == null) return;
        if (!_appServices.PositioningService.IsDynamicPositioning()) return;
        _positionPersistTimer.Stop();
        _positionPersistTimer.Start();
    }

    private void PersistDockPosition()
    {
        if (_appServices == null) return;
        if (!_appServices.PositioningService.IsDynamicPositioning()) return;
        var position = DockBarPosition;
        _appServices.DockService.SetDockPosition(position.X, position.Y);
    }

    private void UpdateStatus(string status)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.StatusText = status;
    }

    /// <summary>Opens the settings window. Port of App.java openSettingsWindow().</summary>
    private void OpenSettings(MainWindowViewModel vm)
    {
        if (_appServices == null) return;
        SettingsWindow.Open(
            _appServices,
            this,
            dockRefreshAction: vm.UpdateDockUI,
            positioningModeChangeAction: mode => HandlePositioningModeChange(mode)
        );
    }

    /// <summary>Port of App.java handlePositioningModeChange().</summary>
    private void HandlePositioningModeChange(DockPositioningMode mode)
    {
        if (_appServices == null) return;
        var currentMode = _appServices.PositioningService.GetPositioningMode();
        if (currentMode == DockPositioningMode.STATIC && mode == DockPositioningMode.DYNAMIC)
        {
            var position = DockBarPosition;
            _appServices.DockService.SetDockPosition(position.X, position.Y);
        }
        _appServices.PositioningService.SetPositioningMode(mode);
    }

    protected override void OnClosed(EventArgs e)
    {
        PositionChanged -= OnDockPositionChanged;
        _positionPersistTimer.Stop();
        HidePreview();
        if (DataContext is MainWindowViewModel vm)
            vm.Shutdown();
        _dockBehavior?.Dispose();
        _dockBehavior = null;
        base.OnClosed(e);
    }
}