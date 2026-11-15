using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CedroModernDock.Controls;

/// <summary>
/// The Cedro context-menu template draws its shadow in a transparent margin
/// around the menu. That only works when the popup window gets per-pixel
/// transparency; when the system refuses it (remote sessions, some GPU
/// drivers), the margin would show as an opaque frame. This handler checks the
/// popup once it is open and switches the menu to a margin-less look.
/// </summary>
public static class MenuTransparency
{
    public static void Register()
    {
        ContextMenu.OpenedEvent.AddClassHandler<ContextMenu>(OnOpened);
    }

    private static void OnOpened(ContextMenu menu, RoutedEventArgs e)
    {
        bool transparent = menu.GetVisualRoot() is TopLevel root
                           && root.ActualTransparencyLevel != WindowTransparencyLevel.None;
        menu.Classes.Set("opaquePopup", !transparent);
    }
}
