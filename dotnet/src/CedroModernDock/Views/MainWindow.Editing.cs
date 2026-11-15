using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CedroModernDock.Controls;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

/// <summary>
/// Dock editing directly from the dock: right-click menus (dock background and
/// items) and drag-and-drop reordering of pinned items while icons are unlocked.
/// </summary>
public partial class MainWindow
{
    private MainWindowViewModel? DockVm => DataContext as MainWindowViewModel;

    // ---------------------------------------------------------------------
    // Context menus
    // ---------------------------------------------------------------------

    private void OnDockContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var vm = DockVm;
        if (vm == null || sender is not Control anchor) return;
        e.Handled = true;
        HidePreview();

        var entries = new List<object>();
        var dataContext = (e.Source as StyledElement)?.DataContext;

        if (dataContext is DockItemViewModel { IsSeparator: false } item)
        {
            entries.Add(MakeHeader(item.Icon, item.Label, DescribeItem(vm, item)));
            entries.AddRange(BuildItemEntries(vm, item));
            entries.Add(new Separator());
        }
        else if (dataContext is DockItemViewModel separator)
        {
            entries.Add(MakeItem(vm.T("contextMenu.removeSeparator", "Remove this separator"), Glyphs.Remove,
                () => vm.RemoveItem(separator), danger: true));
            entries.Add(new Separator());
        }
        else if (dataContext is RunningAppViewModel running)
        {
            entries.Add(MakeHeader(running.Icon, running.Label, vm.T("contextMenu.running", "Running, not pinned")));
            entries.Add(MakeItem(vm.T("contextMenu.pin", "Pin to dock"), Glyphs.Pin,
                () => vm.PinRunningApp(running.ExecutablePath, running.Label)));
            entries.Add(MakeItem(vm.T("contextMenu.newWindow", "New window"), Glyphs.Launch,
                () => LaunchExecutable(running.ExecutablePath)));
            entries.Add(MakeItem(vm.T("contextMenu.closeAll", "Close all windows"), Glyphs.CloseWindows,
                () => CloseAllWindows(running.ExecutablePath, running.Label), danger: true));
            entries.Add(new Separator());
        }

        // Where a separator added from this menu goes: at the clicked spot.
        int insertIndex = e.TryGetPosition(PinnedItems, out var clickPos)
            ? InsertionIndexAt(vm, clickPos, exclude: null)
            : int.MaxValue;
        entries.AddRange(BuildDockEntries(vm, insertIndex));

        var menu = new ContextMenu { ItemsSource = entries };
        menu.Open(anchor);
    }

    private IEnumerable<object> BuildItemEntries(MainWindowViewModel vm, DockItemViewModel item)
    {
        bool isProgram = item.Item is DockProgramItemModel;
        if (isProgram && item.IsRunning)
            yield return MakeItem(vm.T("contextMenu.newWindow", "New window"), Glyphs.Launch,
                () => item.ClickCommand.Execute(item));
        if (item.CanEdit)
        {
            yield return MakeItem(vm.T("contextMenu.rename", "Rename..."), Glyphs.Rename, () => _ = RenameItemAsync(vm, item));
            yield return MakeItem(vm.T("contextMenu.changeTarget", "Change location..."), Glyphs.Retarget, () => _ = RetargetItemAsync(vm, item));
            yield return MakeItem(vm.T("contextMenu.openLocation", "Open file location"), Glyphs.OpenLocation, () => OpenItemLocation(item));
        }
        yield return MakeItem(vm.T("contextMenu.addSeparatorAfter", "Add a separator after"), Glyphs.Separator, () => vm.AddSeparator(item));
        if (isProgram && item.IsRunning && item.ExecutablePath != null)
            yield return MakeItem(vm.T("contextMenu.closeAll", "Close all windows"), Glyphs.CloseWindows,
                () => CloseAllWindows(item.ExecutablePath, item.Label), danger: true);
        if (item.CanRemove)
            yield return MakeItem(vm.T("contextMenu.removeItem", "Remove from dock"), Glyphs.Remove, () => vm.RemoveItem(item), danger: true);
    }

    private IEnumerable<object> BuildDockEntries(MainWindowViewModel vm, int insertIndex)
    {
        yield return MakeItem(vm.T("contextMenu.addProgram", "Add a program..."), Glyphs.AddProgram, () => _ = AddProgramAsync(vm));
        yield return MakeItem(vm.T("contextMenu.addFolder", "Add a folder..."), Glyphs.AddFolder, () => _ = AddFolderAsync(vm));
        yield return MakeItem(vm.T("contextMenu.addSeparatorHere", "Add a separator here"), Glyphs.Separator, () => vm.AddSeparatorAt(insertIndex));
        yield return new Separator();
        yield return BuildThemeSubmenu(vm);
        yield return MakeItem(
            vm.IconsLocked
                ? vm.T("contextMenu.unlockIcons", "Unlock icons (move with the mouse)")
                : vm.T("contextMenu.lockIcons", "Lock icons"),
            vm.IconsLocked ? Glyphs.Unlock : Glyphs.Lock,
            () => vm.SetIconsLocked(!vm.IconsLocked));
        yield return new Separator();
        yield return MakeItem(vm.T("contextMenu.settings", "Settings..."), Glyphs.Settings, vm.OpenSettings);
        if (vm.ExitAction != null)
            yield return MakeItem(vm.T("contextMenu.exit", "Quit Cedro Dock"), Glyphs.Exit, vm.ExitAction);
    }

    /// <summary>"Theme" sub-menu: every installed theme, the active one checked.</summary>
    private MenuItem BuildThemeSubmenu(MainWindowViewModel vm)
    {
        var parent = new MenuItem
        {
            Header = vm.T("contextMenu.theme", "Theme"),
            Icon = Glyphs.MenuIcon(Glyphs.Theme)
        };
        var themes = _appServices?.ThemeService;
        if (themes == null) return parent;
        string activeId = themes.GetActiveTheme().Id;
        var children = new List<object>();
        foreach (var theme in themes.GetThemes())
        {
            var entry = new MenuItem
            {
                Header = theme.Name,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = theme.Id == activeId,
                GroupName = "dockTheme"
            };
            string id = theme.Id;
            entry.Click += (_, _) => themes.SetActiveTheme(id);
            children.Add(entry);
        }
        children.Add(new Separator());
        children.Add(MakeItem(vm.T("contextMenu.manageThemes", "Manage themes..."), Glyphs.Settings, vm.OpenThemesSettings));
        parent.ItemsSource = children;
        return parent;
    }

    private static MenuItem MakeItem(string header, string glyph, Action action, bool danger = false)
    {
        var menuItem = new MenuItem { Header = header, Icon = Glyphs.MenuIcon(glyph) };
        if (danger) menuItem.Classes.Add("danger");
        menuItem.Click += (_, _) => action();
        return menuItem;
    }

    /// <summary>Non-clickable first row: the item's icon, name and a short description.</summary>
    private static MenuItem MakeHeader(Avalonia.Media.IImage? icon, string title, string? subtitle)
    {
        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 13,
            MaxWidth = 220, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = new TextBlock
            {
                Text = subtitle, FontSize = 11, MaxWidth = 220,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            sub.Bind(TextBlock.ForegroundProperty, sub.GetResourceObservable("CedroMenuSecondary"));
            text.Children.Add(sub);
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (icon != null)
            row.Children.Add(new Image { Source = icon, Width = 28, Height = 28 });
        row.Children.Add(text);
        var header = new MenuItem { Header = row };
        header.Classes.Add("menuHeader");
        return header;
    }

    private static string? DescribeItem(MainWindowViewModel vm, DockItemViewModel item) => item.Item switch
    {
        DockProgramItemModel p => item.IsRunning
            ? vm.T("contextMenu.runningProgram", "Running") + " · " + Path.GetFileName(p.ExecutablePath)
            : Path.GetFileName(p.ExecutablePath),
        DockFolderItemModel f => f.FolderPath,
        _ => null
    };

    private static void LaunchExecutable(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* best-effort */ }
    }

    /// <summary>Asks every window of the program to close (WM_CLOSE, like the taskbar).</summary>
    private void CloseAllWindows(string executablePath, string label)
    {
        if (_appServices == null) return;
        var service = _appServices.WindowPreviewService;
        _ = Task.Run(() =>
        {
            try
            {
                foreach (var window in service.LoadPreview(new DockProgramItemModel(label, executablePath)))
                    service.Close(window);
            }
            catch { /* best-effort */ }
        });
    }

    private async Task RenameItemAsync(MainWindowViewModel vm, DockItemViewModel item)
    {
        string? newLabel = await PromptTextAsync(
            vm.T("dialog.rename.title", "Rename"),
            vm.T("dialog.rename.message", "Display name:"),
            item.Item.Label,
            vm.T("dialog.ok", "OK"),
            vm.T("dialog.cancel", "Cancel"));
        if (newLabel != null)
            vm.RenameItem(item, newLabel);
    }

    private async Task RetargetItemAsync(MainWindowViewModel vm, DockItemViewModel item)
    {
        var loc = vm;
        IStorageFolder? start = null;
        // The pickers below are async; the item VM may be recreated meanwhile,
        // but it keeps a reference to the same model, which is what we edit.
        try
        {
            string? dir = item.Item is DockFolderItemModel
                ? item.Item.Path
                : Path.GetDirectoryName(item.Item.Path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                start = await StorageProvider.TryGetFolderFromPathAsync(dir);
        }
        catch { /* the start folder is only a convenience */ }

        string? newPath = null;
        if (item.Item is DockFolderItemModel)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = loc.T("dialog.directoryChooser.title", "Choose a folder"),
                SuggestedStartLocation = start
            });
            if (folders.Count > 0) newPath = folders[0].Path.LocalPath;
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = loc.T("dialog.fileChooser.executableTitle", "Choose an .exe"),
                SuggestedStartLocation = start,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(loc.T("dialog.fileChooser.executableFilter", "Executable"))
                    {
                        Patterns = new[] { "*.exe" }
                    }
                }
            });
            if (files.Count > 0) newPath = files[0].Path.LocalPath;
        }

        if (newPath != null)
            vm.RetargetItem(item, newPath);
    }

    private async Task AddProgramAsync(MainWindowViewModel vm)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = vm.T("dialog.fileChooser.executableTitle", "Choose an .exe"),
            FileTypeFilter = new[]
            {
                new FilePickerFileType(vm.T("dialog.fileChooser.executableFilter", "Executable"))
                {
                    Patterns = new[] { "*.exe" }
                }
            }
        });
        if (files.Count > 0)
            vm.AddProgram(files[0].Path.LocalPath);
    }

    private async Task AddFolderAsync(MainWindowViewModel vm)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = vm.T("dialog.directoryChooser.title", "Choose a folder")
        });
        if (folders.Count > 0)
            vm.AddFolder(folders[0].Path.LocalPath);
    }

    private static void OpenItemLocation(DockItemViewModel item)
    {
        try
        {
            string path = item.Item.Path;
            if (item.Item is DockFolderItemModel && Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch { /* best-effort */ }
    }

    /// <summary>Small modal text prompt. Returns null when cancelled.</summary>
    private async Task<string?> PromptTextAsync(string title, string message, string initial,
        string okText, string cancelText)
        => await Dialogs.PromptAsync(this, title, message, initial, okText, cancelText);

    // ---------------------------------------------------------------------
    // Drag-and-drop reordering (only while icons are unlocked)
    // ---------------------------------------------------------------------

    private const double DragThreshold = 6;
    private DockItemViewModel? _dragItem;
    private Point _dragStartPoint;
    private int _dragOriginalIndex = -1;
    private bool _isDragging;

    private void SetupDragReorder()
    {
        // Tunnel handlers run before the item Button sees the pointer, so a
        // press can become a drag without breaking normal clicks.
        PinnedItems.AddHandler(PointerPressedEvent, OnPinnedPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PinnedItems.AddHandler(PointerMovedEvent, OnPinnedPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        PinnedItems.AddHandler(PointerReleasedEvent, OnPinnedPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPinnedPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isDragging) EndDrag(e.Pointer);
        ResetDrag();

        var vm = DockVm;
        if (vm == null || vm.IconsLocked) return;
        if (!e.GetCurrentPoint(PinnedItems).Properties.IsLeftButtonPressed) return;
        if ((e.Source as StyledElement)?.DataContext is not DockItemViewModel item) return;

        _dragItem = item;
        _dragStartPoint = e.GetPosition(PinnedItems);
        _dragOriginalIndex = vm.Items.IndexOf(item);
    }

    private void OnPinnedPointerMoved(object? sender, PointerEventArgs e)
    {
        var vm = DockVm;
        if (_dragItem == null || vm == null) return;

        if (!e.GetCurrentPoint(PinnedItems).Properties.IsLeftButtonPressed)
        {
            if (_isDragging) EndDrag(e.Pointer);
            ResetDrag();
            return;
        }

        var pos = e.GetPosition(PinnedItems);
        if (!_isDragging)
        {
            var delta = pos - _dragStartPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                return;
            _isDragging = true;
            // Taking the capture cancels the Button's pressed state: no click.
            e.Pointer.Capture(PinnedItems);
            HidePreview();
            _dragItem.Opacity = 0.5;
        }

        int target = InsertionIndexAt(vm, pos, _dragItem);
        int current = vm.Items.IndexOf(_dragItem);
        if (current >= 0 && target != current)
            vm.Items.Move(current, target);
        e.Handled = true;
    }

    private void OnPinnedPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag(e.Pointer);
            e.Handled = true;
        }
        ResetDrag();
    }

    private void EndDrag(IPointer pointer)
    {
        var vm = DockVm;
        if (_dragItem != null)
        {
            _dragItem.Opacity = 1.0;
            if (vm != null)
            {
                int finalIndex = vm.Items.IndexOf(_dragItem);
                if (_dragOriginalIndex >= 0 && finalIndex >= 0)
                    vm.CommitMove(_dragOriginalIndex, finalIndex);
            }
        }
        if (ReferenceEquals(pointer.Captured, PinnedItems))
            pointer.Capture(null);
    }

    /// <summary>
    /// Index (in the pinned items) where something placed at <paramref name="pos"/>
    /// (PinnedItems coordinates) goes: the number of items, other than
    /// <paramref name="exclude"/>, whose center lies before that point.
    /// </summary>
    private int InsertionIndexAt(MainWindowViewModel vm, Point pos, DockItemViewModel? exclude)
    {
        int index = 0;
        double p = vm.IsVerticalDock ? pos.Y : pos.X;
        for (int i = 0; i < vm.Items.Count; i++)
        {
            if (ReferenceEquals(vm.Items[i], exclude)) continue;
            if (PinnedItems.ContainerFromIndex(i) is not Control container) continue;
            var center = container.TranslatePoint(
                new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), PinnedItems);
            if (center == null) continue;
            double c = vm.IsVerticalDock ? center.Value.Y : center.Value.X;
            if (c < p) index++;
        }
        return index;
    }

    // ---------------------------------------------------------------------
    // Files dropped from the desktop / Explorer
    // ---------------------------------------------------------------------

    private void SetupFileDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        DragDrop.SetAllowDrop(DockBar, true);
        DockBar.AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        DockBar.AddHandler(DragDrop.DropEvent, OnFileDrop);
    }

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        var vm = DockVm;
        if (vm == null) return;
        e.Handled = true;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        var paths = new List<string>();
        foreach (var file in files)
        {
            string? local = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(local)) paths.Add(local);
        }
        if (paths.Count == 0) return;

        int index = InsertionIndexAt(vm, e.GetPosition(PinnedItems), exclude: null);
        vm.AddDroppedPaths(paths, index);
    }

    private void ResetDrag()
    {
        _dragItem = null;
        _dragOriginalIndex = -1;
        _isDragging = false;
    }
}
