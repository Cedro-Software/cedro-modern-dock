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

        if (dataContext is DockItemViewModel item)
        {
            entries.AddRange(BuildItemEntries(vm, item));
            entries.Add(new Separator());
        }
        else if (dataContext is RunningAppViewModel running)
        {
            entries.Add(MakeItem(vm.T("contextMenu.pin", "Pin to dock"), () =>
            {
                vm.PinRunningApp(running.ExecutablePath, running.Label);
            }));
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
        if (item.CanEdit)
        {
            yield return MakeItem(vm.T("contextMenu.rename", "Rename..."), () => _ = RenameItemAsync(vm, item));
            yield return MakeItem(vm.T("contextMenu.changeTarget", "Change location..."), () => _ = RetargetItemAsync(vm, item));
            yield return MakeItem(vm.T("contextMenu.openLocation", "Open file location"), () => OpenItemLocation(item));
        }
        yield return MakeItem(vm.T("contextMenu.addSeparatorAfter", "Add a separator after"), () => vm.AddSeparator(item));
        if (item.CanRemove)
            yield return MakeItem(vm.T("contextMenu.removeItem", "Remove from dock"), () => vm.RemoveItem(item));
    }

    private IEnumerable<object> BuildDockEntries(MainWindowViewModel vm, int insertIndex)
    {
        yield return MakeItem(vm.T("contextMenu.addProgram", "Add a program..."), () => _ = AddProgramAsync(vm));
        yield return MakeItem(vm.T("contextMenu.addFolder", "Add a folder..."), () => _ = AddFolderAsync(vm));
        yield return MakeItem(vm.T("contextMenu.addSeparatorHere", "Add a separator here"), () => vm.AddSeparatorAt(insertIndex));
        yield return new Separator();
        yield return MakeItem(
            vm.IconsLocked
                ? vm.T("contextMenu.unlockIcons", "Unlock icons (move with the mouse)")
                : vm.T("contextMenu.lockIcons", "Lock icons"),
            () => vm.SetIconsLocked(!vm.IconsLocked));
        yield return new Separator();
        yield return MakeItem(vm.T("contextMenu.settings", "Settings..."), vm.OpenSettings);
    }

    private static MenuItem MakeItem(string header, Action action)
    {
        var menuItem = new MenuItem { Header = header };
        menuItem.Click += (_, _) => action();
        return menuItem;
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
    {
        var textBox = new TextBox { Text = initial, MinWidth = 280 };
        var ok = new Button { Content = okText, IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = cancelText, IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = message },
                    textBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { ok, cancel }
                    }
                }
            }
        };

        ok.Click += (_, _) => dialog.Close(textBox.Text ?? "");
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Opened += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        return await dialog.ShowDialog<string?>(this);
    }

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
