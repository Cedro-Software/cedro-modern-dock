using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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

    /// <summary>Look shared by all dock items (zoom, indicator, separators, tooltips).</summary>
    public DockVisualStyle Style { get; } = new();

    public int IconsSize
    {
        get => _iconsSize;
        set
        {
            if (SetProperty(ref _iconsSize, value))
                RaiseHeadroom();
        }
    }

    private void RaiseHeadroom()
    {
        OnPropertyChanged(nameof(ZoomHeadroom));
        OnPropertyChanged(nameof(ZoomHeadroomThickness));
    }

    /// <summary>
    /// Transparent space kept around the dock bar so a hover-zoomed icon can grow
    /// past the bar's edge instead of being clipped by the window (which is sized
    /// to its content). Sized from the icon's growth per side (the button's own
    /// padding absorbs the rounding), so the zoom never depends on the dock padding.
    /// </summary>
    public int ZoomHeadroom => (int)Math.Round(IconsSize * (Style.HoverZoom - 1) / 2);
    public Thickness ZoomHeadroomThickness => new Thickness(ZoomHeadroom);

    /// <summary>Accent color of the dashed edit-mode outline (icons unlocked).</summary>
    public IBrush AccentBrush { get; } = new SolidColorBrush(Color.FromRgb(0x4C, 0x9A, 0xFF));

    /// <summary>Colors handed to the window-preview popup so it matches the dock.</summary>
    public string PreviewColorRgb { get; private set; } = "0, 0, 0";
    public double PreviewOpacity { get; private set; } = 0.3;
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
        // Classic separator images, the same assets the dock used before.
        Style.SetSeparator("classic", Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), 1,
            IconLoader.LoadFromAsset("Assets/icons/open-softwares-separator-1.png"),
            IconLoader.LoadFromAsset("Assets/icons/open-softwares-separator-2.png"));
    }

    /// <summary>Set by the App — quits the application (dock menu "Quit").</summary>
    public Action? ExitAction { get; set; }

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
                vm.Style = Style;
                Items.Add(vm);
            }
        }
        IconsLocked = _appServices.DockService.GetIconsLocked();
        ApplyAppearance();
        RepositionAction?.Invoke();
        PreviewDismissAction?.Invoke();
    }

    // --- Dock editing (context menus, drag reorder) ---

    private bool _iconsLocked = true;
    /// <summary>When false, pinned icons can be reordered by drag and drop.</summary>
    public bool IconsLocked
    {
        get => _iconsLocked;
        private set
        {
            if (SetProperty(ref _iconsLocked, value))
                OnPropertyChanged(nameof(IconsUnlocked));
        }
    }

    /// <summary>Edit mode: the dock shows a dashed accent outline.</summary>
    public bool IconsUnlocked => !IconsLocked;

    /// <summary>Returns the translated text, or the fallback when the key is missing.</summary>
    public string T(string key, string fallback)
        => _appServices?.LocalizationService.TextOr(key, fallback) ?? fallback;

    public void SetIconsLocked(bool locked)
    {
        if (_appServices == null) return;
        _appServices.DockService.SetIconsLocked(locked);
        IconsLocked = locked;
    }

    private int IndexOfModel(DockItemViewModel vm)
        => _appServices?.DockService.GetItems().IndexOf(vm.Item) ?? -1;

    /// <summary>Inserts a separator after <paramref name="after"/>.</summary>
    public void AddSeparator(DockItemViewModel after)
    {
        int index = IndexOfModel(after);
        AddSeparatorAt(index >= 0 ? index + 1 : int.MaxValue);
    }

    /// <summary>Inserts a separator at <paramref name="index"/> (clamped; MaxValue = end).</summary>
    public void AddSeparatorAt(int index)
    {
        if (_appServices == null) return;
        _appServices.DockService.InsertItem(index, new DockSeparatorItemModel());
        UpdateDockUI();
    }

    /// <summary>
    /// Adds files dropped on the dock at <paramref name="index"/>: folders become
    /// folder items, .exe files and shortcuts to .exe files become program items.
    /// Returns the number of items added.
    /// </summary>
    public int AddDroppedPaths(IEnumerable<string> paths, int index)
    {
        if (_appServices == null) return 0;
        int added = 0;
        foreach (var raw in paths)
        {
            DockItem? item = null;
            if (Directory.Exists(raw))
            {
                item = CreateFolderItem(raw);
            }
            else
            {
                string? exe = ShortcutResolver.ResolveExecutable(raw);
                if (exe != null)
                {
                    var sel = ProgramSelectionResolver.Resolve(exe);
                    _appServices.IconGateway.CacheProgramIcon(sel.ExecutablePath);
                    // Keep the shortcut's name (e.g. "Visual Studio Code" rather than "Code").
                    string label = raw.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(raw)
                        : sel.Label;
                    item = new DockProgramItemModel(label, sel.ExecutablePath);
                }
            }
            if (item == null) continue;
            _appServices.DockService.InsertItem(index == int.MaxValue ? index : index + added, item);
            added++;
        }
        if (added > 0) UpdateDockUI();
        return added;
    }

    private DockFolderItemModel CreateFolderItem(string path)
    {
        var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (string.IsNullOrWhiteSpace(label))
            label = path;
        _appServices!.IconGateway.CacheFolderIcon(path);
        return new DockFolderItemModel(label, path);
    }

    public void RemoveItem(DockItemViewModel vm)
    {
        if (_appServices == null || !vm.CanRemove) return;
        int index = IndexOfModel(vm);
        if (index < 0) return;
        _appServices.DockService.RemoveItem(index);
        UpdateDockUI();
    }

    public void RenameItem(DockItemViewModel vm, string newLabel)
    {
        if (_appServices == null || !vm.CanEdit) return;
        newLabel = newLabel.Trim();
        if (newLabel.Length == 0) return;
        vm.Item.Label = newLabel;
        _appServices.DockService.SaveChanges();
        UpdateDockUI();
    }

    /// <summary>Changes the target of a program (.exe) or folder item.</summary>
    public void RetargetItem(DockItemViewModel vm, string newPath)
    {
        if (_appServices == null || !vm.CanEdit || string.IsNullOrWhiteSpace(newPath)) return;
        if (vm.Item is DockProgramItemModel)
        {
            var sel = ProgramSelectionResolver.Resolve(newPath);
            _appServices.IconGateway.CacheProgramIcon(sel.ExecutablePath);
            vm.Item.Path = sel.ExecutablePath;
        }
        else
        {
            vm.Item.Path = newPath;
        }
        _appServices.DockService.SaveChanges();
        UpdateDockUI();
    }

    /// <summary>
    /// Persists a drag-reorder already applied to <see cref="Items"/>: the item
    /// moved from <paramref name="fromIndex"/> to final position <paramref name="toIndex"/>.
    /// </summary>
    public void CommitMove(int fromIndex, int toIndex)
    {
        if (_appServices == null || fromIndex == toIndex) return;
        // DockModel.MoveItem takes a gap index (0..Count), not a final position.
        _appServices.DockService.MoveItem(fromIndex, toIndex > fromIndex ? toIndex + 1 : toIndex);
    }

    public void OpenSettings() => OpenSettingsAction?.Invoke();

    public void AddProgram(string path)
    {
        if (_appServices == null) return;
        var sel = ProgramSelectionResolver.Resolve(path);
        _appServices.IconGateway.CacheProgramIcon(sel.ExecutablePath);
        _appServices.DockService.AddItem(new DockProgramItemModel(sel.Label, sel.ExecutablePath));
        UpdateDockUI();
    }

    public void AddFolder(string path)
    {
        if (_appServices == null) return;
        _appServices.DockService.AddItem(CreateFolderItem(path));
        UpdateDockUI();
    }

    /// <summary>Pins a running-but-unpinned app to the dock.</summary>
    public void PinRunningApp(string executablePath, string label)
    {
        if (_appServices == null) return;
        _appServices.IconGateway.CacheProgramIcon(executablePath);
        _appServices.DockService.AddItem(new DockProgramItemModel(label, executablePath));
        UpdateDockUI();
    }

    private DockItemViewModel? CreateItemViewModel(DockItem item, LocalizationService loc)
    {
        string label = loc.DockItemLabel(item);

        if (item is DockSeparatorItemModel)
            return new DockItemViewModel(item, label, LaunchCommand);

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
        var color = ParseRgbColor(colorRgb);
        DockBackground = new SolidColorBrush(Color.FromArgb((byte)(transparency * 255), color.R, color.G, color.B));
        PreviewColorRgb = colorRgb;
        PreviewOpacity = transparency;

        Style.IconSize = IconsSize;
        Style.IsVerticalDock = IsVerticalDock;
        Style.TooltipPlacement = ResolveTooltipPlacement();
        RaiseHeadroom();
    }

    /// <summary>Tooltips open toward the screen center, away from the edge the dock sits on.</summary>
    private Avalonia.Controls.PlacementMode ResolveTooltipPlacement()
    {
        if (_appServices == null) return Avalonia.Controls.PlacementMode.Top;
        var pos = _appServices.PositioningService;
        if (IsVerticalDock)
            return pos.GetHorizontalAnchor() == DockHorizontalAnchor.RIGHT
                ? Avalonia.Controls.PlacementMode.Left
                : Avalonia.Controls.PlacementMode.Right;
        return pos.GetVerticalAnchor() == DockVerticalAnchor.TOP
            ? Avalonia.Controls.PlacementMode.Bottom
            : Avalonia.Controls.PlacementMode.Top;
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
            bool launched = _appServices.ItemActionService.Execute(
                itemVm.Item, () => OpenSettingsAction?.Invoke());

            if (!launched && itemVm.Item is DockProgramItemModel programItem)
            {
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
                    IconSize = _appServices.AppearanceService.GetIconsSize(),
                    Style = Style
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
        {
            _appServices.LocalizationService.RemoveListener(UpdateDockUI);
        }
        _indicatorCts?.Cancel();
        _indicatorCts?.Dispose();
        _indicatorCts = null;
    }
}
