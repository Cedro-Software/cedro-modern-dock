using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Main dock ViewModel. Holds the dock items collection, appearance settings,
/// and the running-app indicator watcher. Direct port of DockController's
/// state and update logic, expressed as MVVM bindings.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices? _appServices;
    private CancellationTokenSource? _indicatorCts;

    private int _iconsSize = 48;
    private int _spacing = 5;
    private int _borderRounding = 12;
    private int _dockPadding = 10;
    private IBrush _dockBackground = new SolidColorBrush(Color.FromArgb(158, 0, 0, 0));
    private string _statusText = "";

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    /// <summary>Running-but-unpinned programs shown right of the separator (taskbar-like).</summary>
    public ObservableCollection<RunningAppViewModel> RunningApps { get; } = new();

    private bool _hasRunningApps;
    public bool HasRunningApps
    {
        get => _hasRunningApps;
        private set
        {
            if (SetProperty(ref _hasRunningApps, value))
            {
                OnPropertyChanged(nameof(ShowHorizontalSeparator));
                OnPropertyChanged(nameof(ShowVerticalSeparator));
            }
        }
    }

    private bool _isVerticalDock;
    public bool IsVerticalDock
    {
        get => _isVerticalDock;
        set
        {
            if (SetProperty(ref _isVerticalDock, value))
            {
                OnPropertyChanged(nameof(DockOrientation));
                OnPropertyChanged(nameof(ShowHorizontalSeparator));
                OnPropertyChanged(nameof(ShowVerticalSeparator));
            }
        }
    }

    /// <summary>Layout direction of the dock items (horizontal by default, vertical when enabled).</summary>
    public Avalonia.Layout.Orientation DockOrientation
        => IsVerticalDock ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal;

    /// <summary>Separator is only shown between pinned items and displayed unpinned running apps.</summary>
    public bool ShowHorizontalSeparator => HasRunningApps && !IsVerticalDock;

    /// <summary>Separator is only shown between pinned items and displayed unpinned running apps.</summary>
    public bool ShowVerticalSeparator => HasRunningApps && IsVerticalDock;

    /// <summary>Hover zoom factor of dock items; must match scale(1.3) in MainWindow.axaml.</summary>
    public const double HoverZoomScale = 1.3;

    public int IconsSize
    {
        get => _iconsSize;
        set
        {
            if (SetProperty(ref _iconsSize, value))
            {
                OnPropertyChanged(nameof(ZoomHeadroom));
                OnPropertyChanged(nameof(ZoomHeadroomThickness));
            }
        }
    }

    /// <summary>
    /// Transparent space kept around the dock bar so a hover-zoomed icon can grow
    /// past the bar's edge instead of being clipped by the window (which is sized
    /// to its content). Sized from the icon's growth per side (the button's own
    /// padding absorbs the rounding), so the zoom never depends on the dock padding.
    /// </summary>
    public int ZoomHeadroom => (int)Math.Round(IconsSize * (HoverZoomScale - 1) / 2);
    public Thickness ZoomHeadroomThickness => new Thickness(ZoomHeadroom);
    public int Spacing { get => _spacing; set => SetProperty(ref _spacing, value); }
    public int BorderRounding
    {
        get => _borderRounding;
        set
        {
            if (SetProperty(ref _borderRounding, value))
                OnPropertyChanged(nameof(DockCornerRadius));
        }
    }
    public CornerRadius DockCornerRadius => new CornerRadius(BorderRounding);
    public int DockPadding
    {
        get => _dockPadding;
        set
        {
            if (SetProperty(ref _dockPadding, value))
                OnPropertyChanged(nameof(DockPaddingThickness));
        }
    }
    public Thickness DockPaddingThickness => new Thickness(DockPadding);
    public IBrush DockBackground { get => _dockBackground; set => SetProperty(ref _dockBackground, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public ICommand LaunchCommand { get; }

    /// <summary>Set by MainWindow — opens the settings window with this window as owner.</summary>
    public Action? OpenSettingsAction { get; set; }

    /// <summary>Set by MainWindow — re-anchors the dock after dock content/settings change.</summary>
    public Action? RepositionAction { get; set; }

    /// <summary>Set by MainWindow — dismisses the window-preview popup (dock refresh).</summary>
    public Action? PreviewDismissAction { get; set; }

    public MainWindowViewModel()
    {
        LaunchCommand = new RelayCommand(_ => { });
    }

    public MainWindowViewModel(AppServices appServices)
    {
        _appServices = appServices;
        LaunchCommand = new RelayCommand(param => ExecuteItem(param));
        _appServices.LocalizationService.AddListener(UpdateDockUI);
    }

    public void Initialize()
    {
        if (_appServices == null) return;
        ApplyAppearance();
        UpdateDockUI();
        StartIndicatorWatcher();
    }

    public void UpdateDockUI()
    {
        if (_appServices == null) return;
        IsVerticalDock = _appServices.AppearanceService.GetVerticalDock();
        // The tint must be set BEFORE items are created: CreateItemViewModel
        // tints each icon immediately, and a stale value would make every
        // pinned item lag one color selection behind.
        var appearance = _appServices.AppearanceService;
        IconTinter.ActiveColor = appearance.GetTintIcons()
            ? ParseRgbColor(appearance.GetTintColorRGB())
            : null;
        Items.Clear();
        var dock = _appServices.DockService.GetDock();
        var loc = _appServices.LocalizationService;

        foreach (var item in dock.Items)
        {
            var vm = CreateItemViewModel(item, loc);
            if (vm != null)
            {
                vm.IconSize = _appServices.AppearanceService.GetIconsSize();
                Items.Add(vm);
            }
        }
        ApplyAppearance();
        RepositionAction?.Invoke();
        PreviewDismissAction?.Invoke();
    }
    // --- continued below ---

    private DockItemViewModel? CreateItemViewModel(DockItem item, LocalizationService loc)
    {
        string label = loc.DockItemLabel(item);

        if (item is DockSettingsItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        if (item is DockWindowsModuleItemModel moduleItem)
        {
            var icon = IconLoader.LoadWindowsModuleIcon(moduleItem.Module);
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        if (item is DockProgramItemModel programItem)
        {
            string? iconPath = _appServices!.IconGateway.ResolveProgramIcon(programItem.ExecutablePath);
            var icon = IconLoader.LoadFromFile(iconPath);
            var itemVm = new DockItemViewModel(item, label, LaunchCommand,
                showIndicator: true, executablePath: programItem.ExecutablePath)
            {
                Icon = IconTinter.Apply(icon)
            };

            // The icon may not be cached yet (first run with a new item). Extract it in the
            // background, then push the resulting bitmap into the ViewModel so the dock
            // updates without requiring a restart.
            if (icon == null)
            {
                string exe = programItem.ExecutablePath;
                _ = Task.Run(() =>
                {
                    _appServices.IconGateway.CacheProgramIcon(exe);
                    string? cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    var loaded = IconLoader.LoadFromFile(cached);
                    if (loaded != null)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => itemVm.Icon = IconTinter.Apply(loaded));
                });
            }

            return itemVm;
        }

        if (item is DockFolderItemModel folderItem)
        {
            string? iconPath = _appServices!.IconGateway.ResolveFolderIcon(folderItem.FolderPath);
            var icon = IconLoader.LoadFromFile(iconPath);
            if (icon == null)
            {
                _ = Task.Run(() => _appServices.IconGateway.CacheFolderIcon(folderItem.FolderPath));
                icon = IconLoader.LoadFromAsset("Assets/icons/folder.png");
            }
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        return null;
    }

    private void ApplyAppearance()
    {
        if (_appServices == null) return;
        var appearance = _appServices.AppearanceService;

        IconsSize = appearance.GetIconsSize();
        foreach (var app in RunningApps)
            app.IconSize = IconsSize;
        Spacing = appearance.GetSpacingBetweenIcons();
        BorderRounding = appearance.GetDockBorderRounding();
        DockPadding = appearance.GetDockPadding();

        // Re-tint the persistent running-apps VMs with the current tint
        // (set in UpdateDockUI before items are created; pinned items are
        // recreated on every refresh so they are tinted at creation).
        foreach (var app in RunningApps)
            app.Icon = IconTinter.Apply(app.OriginalIcon);

        string colorRgb = appearance.GetDockColorRGB();
        double transparency = appearance.GetDockTransparencyPercentage() / 100.0;
        byte alpha = (byte)(transparency * 255);
        var parts = colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        DockBackground = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    private static Color ParseRgbColor(string rgb)
    {
        var parts = rgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        return Color.FromRgb(r, g, b);
    }

    private void ExecuteItem(object? param)
    {
        if (param is DockItemViewModel itemVm && _appServices != null)
        {
            if (itemVm.Item is DockProgramItemModel)
            {
                itemVm.StartBounce();
            }

            bool launched = _appServices.ItemActionService.Execute(
                itemVm.Item, () => OpenSettingsAction?.Invoke());

            if (!launched && itemVm.Item is DockProgramItemModel programItem)
            {
                itemVm.StopBounce();
                var loc = _appServices.LocalizationService;
                System.Windows.Forms.MessageBox.Show(
                    loc.Text("dialog.programNotFound.message",
                        programItem.Label, programItem.ExecutablePath),
                    loc.Text("dialog.programNotFound.title"),
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }
    }

    private void StartIndicatorWatcher()
    {
        _indicatorCts?.Cancel();
        _indicatorCts = new CancellationTokenSource();
        var token = _indicatorCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    RefreshIndicators();
                    RefreshRunningApps();
                    await Task.Delay(1200, token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* keep watching */ }
            }
        }, token);
    }

    private void RefreshIndicators()
    {
        if (_appServices == null) return;
        var programItems = Items.Where(i => i.ShowIndicator && i.ExecutablePath != null).ToList();
        foreach (var item in programItems)
        {
            bool isOpen = _appServices.WindowPreviewService.HasOpenWindows(item.ExecutablePath);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => item.IsRunning = isOpen);
        }
    }

    private readonly Dictionary<string, RunningAppViewModel> _runningAppsByPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Syncs the running-apps section: taskbar-visible windows, grouped by executable,
    /// minus programs already pinned in the dock. Only touches the UI when the set
    /// actually changed, so the preview popup never flickers.
    /// </summary>
    private void RefreshRunningApps()
    {
        if (_appServices == null) return;

        // Feature toggle: only show unpinned running apps when enabled.
        if (!_appServices.AppearanceService.GetShowUnpinnedRunningApps())
        {
            if (RunningApps.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    RunningApps.Clear();
                    _runningAppsByPath.Clear();
                    HasRunningApps = false;
                    RepositionAction?.Invoke();
                    PreviewDismissAction?.Invoke();
                });
            }
            return;
        }

        List<string> pinnedPaths = Items
            .Where(i => i.Item is DockProgramItemModel)
            .Select(i => ((DockProgramItemModel)i.Item).ExecutablePath)
            .ToList();

        List<CedroModernDock.Core.Domain.RunningWindowInfo> windows;
        try
        {
            windows = _appServices.WindowPreviewService.FindTaskbarWindows();
        }
        catch
        {
            return;
        }

        var desired = windows
            .GroupBy(w => w.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Where(g => !IsPinned(g.Key, pinnedPaths))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_appServices == null) return;

            bool changed = false;

            // Remove apps that closed or got pinned.
            foreach (var path in _runningAppsByPath.Keys.ToList())
            {
                if (!desired.ContainsKey(path) || IsPinned(path, pinnedPaths))
                {
                    RunningApps.Remove(_runningAppsByPath[path]);
                    _runningAppsByPath.Remove(path);
                    changed = true;
                }
            }

            // Add newly opened apps.
            foreach (var (path, win) in desired)
            {
                if (_runningAppsByPath.ContainsKey(path)) continue;
                string label = System.IO.Path.GetFileNameWithoutExtension(path);
                var vm = new RunningAppViewModel(path, label)
                {
                    IconSize = _appServices.AppearanceService.GetIconsSize()
                };
                LoadRunningIcon(vm, win.Handle);
                _runningAppsByPath[path] = vm;
                RunningApps.Add(vm);
                changed = true;
            }

            if (changed)
            {
                HasRunningApps = RunningApps.Count > 0;
                RepositionAction?.Invoke();
                PreviewDismissAction?.Invoke();
            }
        });
    }

    private static bool IsPinned(string executablePath, List<string> pinnedPaths)
    {
        string file = System.IO.Path.GetFileName(executablePath);
        foreach (var pinned in pinnedPaths)
        {
            if (string.Equals(pinned, executablePath, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(System.IO.Path.GetFileName(pinned), file, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void LoadRunningIcon(RunningAppViewModel vm, IntPtr windowHandle)
    {
        if (_appServices == null) return;
        string exe = vm.ExecutablePath;

        // The actual Windows Settings app (SystemSettings.exe) carries no icon
        // in its EXE and its UWP frame exposes none either, so its dock icon is
        // hardcoded to the bundled Windows Settings asset.
        if (string.Equals(System.IO.Path.GetFileName(exe),
                "SystemSettings.exe", StringComparison.OrdinalIgnoreCase))
        {
            var settingsIcon = IconLoader.LoadFromAsset("Assets/icons/windows_settings.png");
            if (settingsIcon != null)
            {
                vm.OriginalIcon = settingsIcon;
                vm.Icon = IconTinter.Apply(settingsIcon);
                return;
            }
        }

        var icon = IconLoader.LoadFromFile(_appServices.IconGateway.ResolveProgramIcon(exe));
        if (icon != null)
        {
            vm.OriginalIcon = icon;
            vm.Icon = IconTinter.Apply(icon);
            return;
        }
        // Not cached yet — extract in the background and push when ready.
        // Modern/UWP apps (Settings, etc.) carry no icon resource in the EXE
        // and their frame window exposes none either; fall back to the running
        // window's icon, then to the AppX package manifest logo.
        _ = Task.Run(() =>
        {
            try
            {
                _appServices.IconGateway.CacheProgramIcon(exe);
                string? cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                var loaded = IconLoader.LoadFromFile(cached);
                if (loaded == null && windowHandle != IntPtr.Zero)
                {
                    WindowsIconExtractor.ExtractAndCacheWindowIcon(exe, windowHandle);
                    cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    loaded = IconLoader.LoadFromFile(cached);
                }
                if (loaded == null)
                {
                    WindowsIconExtractor.ExtractAndCacheAppxIcon(exe);
                    cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    loaded = IconLoader.LoadFromFile(cached);
                }
                if (loaded != null)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        vm.OriginalIcon = loaded;
                        vm.Icon = IconTinter.Apply(loaded);
                    });
            }
            catch { /* best-effort */ }
        });
    }

    public void Shutdown()
    {
        if (_appServices != null)
            _appServices.LocalizationService.RemoveListener(UpdateDockUI);
        _indicatorCts?.Cancel();
        _indicatorCts?.Dispose();
        _indicatorCts = null;
    }
}
