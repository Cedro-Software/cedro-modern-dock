using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Controls;

/// <summary>Closes menus on outside desktop clicks even when their owner never activates.</summary>
public sealed class DesktopContextMenuDismissal : IDisposable
{
    private readonly ContextMenu _menu;
    private readonly IDisposable? _observer;
    private bool _disposed;

    public DesktopContextMenuDismissal(ContextMenu menu,
        Func<Action<PixelPoint>, IDisposable?>? observeClicks = null)
    {
        _menu = menu;
        _menu.Closed += OnClosed;
        if (observeClicks != null)
            _observer = observeClicks(OnDesktopClick);
        else if (TopLevel.GetTopLevel(menu)?.TryGetPlatformHandle() != null)
            _observer = DesktopMouseClickObserver.TryCreate((x, y) => OnDesktopClick(new PixelPoint(x, y)));
    }

    private void OnDesktopClick(PixelPoint point)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed && _menu.IsOpen && !ContainsClick(point)) _menu.Close();
        }, DispatcherPriority.Input);
    }

    private bool ContainsClick(PixelPoint point)
    {
        if (Contains(_menu, point)) return true;
        foreach (var item in _menu.GetLogicalDescendants().OfType<MenuItem>().Where(i => i.IsSubMenuOpen))
            foreach (var popup in item.GetVisualDescendants().OfType<Popup>())
                if (popup.IsOpen && popup.Host is Control host && Contains(host, point)) return true;
        return false;
    }

    private static bool Contains(Control control, PixelPoint point)
    {
        var root = TopLevel.GetTopLevel(control);
        var local = root?.TranslatePoint(root.PointToClient(point), control);
        return control.IsVisible && local is { } p && new Rect(control.Bounds.Size).Contains(p);
    }

    private void OnClosed(object? sender, RoutedEventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _menu.Closed -= OnClosed;
        _observer?.Dispose();
    }
}
