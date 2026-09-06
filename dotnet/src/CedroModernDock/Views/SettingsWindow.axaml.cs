using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow : Window
{
    private static readonly object _syncLock = new();
    private static SettingsWindow? _instance;
    private static bool _isOpening;

    /// <summary>
    /// Gets the current singleton instance of the settings menu window, if one is open.
    /// </summary>
    public static SettingsWindow? Instance
    {
        get
        {
            lock (_syncLock)
            {
                return _instance;
            }
        }
    }

    private const double DragThresholdPixels = 4;

    private SettingsViewModel? _vm;
    private AppServices? _appServices;
    private Action? _dockRefreshAction;

    private Point _pressPoint;
    private int _dragSourceIndex = -1;
    private bool _dragInProgress;

    public SettingsWindow()
    {
        InitializeComponent();
        // ListBoxItem marks PointerPressed as handled while it updates the
        // selection, which would skip a normal (bubble) handler on the ListBox.
        // Subscribe with handledEventsToo so the drag gesture still sees presses.
        ItemsList.AddHandler(
            InputElement.PointerPressedEvent,
            OnItemsPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public static void Open(AppServices appServices, Window owner,
        Action dockRefreshAction, Action<DockPositioningMode> positioningModeChangeAction)
    {
        lock (_syncLock)
        {
            // Only one settings window at a time: focus the existing one.
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
                _instance.Show();
                _instance.Activate();
                _instance.Focus();
                return;
            }

            // Prevent duplicate creation if rapid clicks occur while the window is still being created/opened.
            if (_isOpening)
            {
                return;
            }

            _isOpening = true;
        }

        try
        {
            var vm = new SettingsViewModel(appServices, dockRefreshAction, positioningModeChangeAction);
            var window = new SettingsWindow
            {
                DataContext = vm,
                _vm = vm,
                _appServices = appServices,
                _dockRefreshAction = dockRefreshAction
            };

            lock (_syncLock)
            {
                _instance = window;
            }

            window.Closed += (_, _) =>
            {
                lock (_syncLock)
                {
                    if (_instance == window)
                        _instance = null;
                }
            };

            vm.Initialize();
            window.Show(owner);
        }
        catch
        {
            lock (_syncLock)
            {
                _instance = null;
            }
            throw;
        }
        finally
        {
            lock (_syncLock)
            {
                _isOpening = false;
            }
        }
    }

    private SettingsViewModel Vm => _vm ??= (DataContext as SettingsViewModel)!;

    private void OnClosed(object? sender, EventArgs e) => Vm?.Shutdown();

    private async void OnAddProgram(object? sender, RoutedEventArgs e) => await Vm?.AddProgramAsync(this)!;
    private async void OnAddFolder(object? sender, RoutedEventArgs e) => await Vm?.AddFolderAsync(this)!;

    private async void OnAddModule(object? sender, RoutedEventArgs e)
    {
        if (_appServices != null && _dockRefreshAction != null)
        {
            await AddWindowsModulesWindow.Open(_appServices, _dockRefreshAction, this);
            // Refresh the dock-items list after the modal closes: adding a
            // module must show up immediately, not only after reopening.
            Vm?.RefreshItemLabels();
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Vm?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e) => Vm?.MoveItemUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e) => Vm?.MoveItemDown();

    // --- Drag-to-reorder the dock items list ---

    private void OnItemsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // The pressed row must be resolved from the event source, not
        // SelectedIndex: ListBoxItem handles the press for selection (marking
        // the event handled), so our ListBox-level handler runs afterwards via
        // handledEventsToo and the selection binding may not be committed yet.
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>() is { } container)
        {
            _dragSourceIndex = ItemsList.IndexFromContainer(container);
            if (_dragSourceIndex < 0) return;
            _pressPoint = e.GetPosition(ItemsList);
            _dragInProgress = false;
        }
    }

    private async void OnItemsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSourceIndex < 0 || _dragInProgress) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var current = e.GetPosition(ItemsList);
        double dx = current.X - _pressPoint.X;
        double dy = current.Y - _pressPoint.Y;
        if (Math.Abs(dx) < DragThresholdPixels && Math.Abs(dy) < DragThresholdPixels)
            return;

        // Only reordering within this list is allowed (no cross-window drops).
        _dragInProgress = true;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(_dragSourceIndex.ToString()));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        _dragInProgress = false;
        _dragSourceIndex = -1;
        HideDropIndicator();
    }

    private void OnItemsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // No drag was ever started: clear the pending press state.
        _dragSourceIndex = -1;
        _dragInProgress = false;
    }

    private void OnItemsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragSourceIndex = -1;
        _dragInProgress = false;
        HideDropIndicator();
    }

    private void OnItemsDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.Text))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Move;
        ShowDropIndicatorAt(e.GetPosition(ItemsList));
        e.Handled = true;
    }

    private void OnItemsDragLeave(object? sender, DragEventArgs e) => HideDropIndicator();

    private void OnItemsDrop(object? sender, DragEventArgs e)
    {
        HideDropIndicator();
        string? sourceText = e.DataTransfer.TryGetText();
        if (sourceText is null || !int.TryParse(sourceText, out int fromIndex)) return;

        var (gapIndex, _) = ResolveDropGap(e.GetPosition(ItemsList));
        Vm?.MoveItem(fromIndex, gapIndex);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>
    /// Resolves the pointer position inside the (possibly scrolled) list to a
    /// "gap index" (0..Count) plus the screen Y of that gap. Works with
    /// virtualized containers: it walks the realized rows, uses each container's
    /// true index (so scrolled items keep their real position), and splits each
    /// row at its vertical midpoint to decide before/after.
    /// </summary>
    private (int GapIndex, double GapY) ResolveDropGap(Point position)
    {
        int count = Vm?.ItemEntries.Count ?? 0;
        if (count == 0) return (0, 0);

        // Collect realized rows: index + viewport-relative top/height.
        var rows = new List<(int Index, double Top, double Height)>();
        foreach (var container in ItemsList.GetRealizedContainers())
        {
            if (container is not Control c) continue;
            int index = ItemsList.IndexFromContainer(c);
            if (index < 0) continue;
            if (c.TranslatePoint(new Point(0, 0), ItemsList) is not Point topLeft) continue;
            double height = c.Bounds.Height;
            if (height <= 0) continue;
            rows.Add((index, topLeft.Y, height));
        }
        if (rows.Count == 0) return (0, 0);
        rows.Sort((a, b) => a.Index.CompareTo(b.Index));

        // Above the first realized row: gap at its index (its top line).
        var first = rows[0];
        if (position.Y < first.Top)
            return (first.Index, first.Top);

        // Inside a realized row: split at the midpoint.
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            double bottom = row.Top + row.Height;
            if (position.Y <= bottom)
            {
                double mid = row.Top + row.Height / 2;
                return position.Y <= mid
                    ? (row.Index, row.Top)
                    : (row.Index + 1, bottom);
            }
        }

        // Below the last realized row: gap right after it.
        var last = rows[^1];
        return (Math.Min(last.Index + 1, count), last.Top + last.Height);
    }

    private void ShowDropIndicatorAt(Point position)
    {
        if (DropIndicator is null) return;
        var (_, gapY) = ResolveDropGap(position);
        DropIndicator.Margin = new Thickness(0, gapY, 0, 0);
        DropIndicator.IsVisible = true;
    }

    private void HideDropIndicator()
    {
        if (DropIndicator is null) return;
        DropIndicator.IsVisible = false;
    }

    private void OnAcknowledgements(object? sender, RoutedEventArgs e)
    {
        if (_appServices != null)
            AcknowledgementsWindow.Open(this, _appServices.LocalizationService);
    }

    private void OnPresetColorPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ISolidColorBrush brush })
            Vm.DockColor = brush.Color;
    }

    private void OnTintPresetColorPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ISolidColorBrush brush })
            Vm.TintColor = brush.Color;
    }

    private void OnCustomTintColor(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        var dialog = new Window
        {
            Title = vm.TintIconsTitle,
            Width = 340,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Color.Parse("#1E1E1E")),
            Content = new ColorView
            {
                Color = vm.TintColor,
                Margin = new Thickness(16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            }
        };
        dialog.Closed += (_, _) =>
        {
            if (dialog.Content is ColorView picker)
                vm.TintColor = picker.Color;
        };
        dialog.ShowDialog(this);
    }

    private void OnCustomColor(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        var dialog = new Window
        {
            Title = vm.BgColorTitle,
            Width = 340,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Color.Parse("#1E1E1E")),
            Content = new ColorView
            {
                Color = vm.DockColor,
                Margin = new Thickness(16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            }
        };
        dialog.Closed += (_, _) =>
        {
            if (dialog.Content is ColorView picker)
                vm.DockColor = picker.Color;
        };
        dialog.ShowDialog(this);
    }
}
