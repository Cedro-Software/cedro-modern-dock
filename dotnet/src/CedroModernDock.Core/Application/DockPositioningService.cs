namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Models;

/// <summary>
/// Direct port of DockPositioningService, with the JavaFX Screen dependency
/// abstracted behind IScreenBoundsProvider so the Core stays platform-agnostic.
/// </summary>
public class DockPositioningService
{
    private readonly DockService _dockService;
    private readonly IScreenBoundsProvider? _screenBoundsProvider;

    public DockPositioningService(DockService dockService, IScreenBoundsProvider? screenBoundsProvider = null)
    {
        _dockService = dockService;
        _screenBoundsProvider = screenBoundsProvider;
    }

    public DockPositioningMode GetPositioningMode() => _dockService.GetDock().PositioningMode;

    public void SetPositioningMode(DockPositioningMode positioningMode)
    {
        _dockService.GetDock().PositioningMode = positioningMode;
        _dockService.SaveChanges();
    }

    public DockVerticalAnchor GetVerticalAnchor() => _dockService.GetDock().VerticalAnchor;

    public void SetVerticalAnchor(DockVerticalAnchor verticalAnchor)
    {
        _dockService.GetDock().VerticalAnchor = verticalAnchor;
        _dockService.SaveChanges();
    }

    public DockHorizontalAnchor GetHorizontalAnchor() => _dockService.GetDock().HorizontalAnchor;

    public void SetHorizontalAnchor(DockHorizontalAnchor horizontalAnchor)
    {
        _dockService.GetDock().HorizontalAnchor = horizontalAnchor;
        _dockService.SaveChanges();
    }

    public int GetScreenEdgeSpacing() => _dockService.GetDock().TopSpacing;

    public void SetScreenEdgeSpacing(int spacing)
    {
        SetTopSpacing(spacing);
        SetLeftSpacing(spacing);
        SetRightSpacing(spacing);
        SetBottomSpacing(spacing);
    }

    public int GetTopSpacing() => _dockService.GetDock().TopSpacing;

    public void SetTopSpacing(int spacing)
    {
        _dockService.GetDock().TopSpacing = Math.Max(0, spacing);
        _dockService.SaveChanges();
    }

    public int GetLeftSpacing() => _dockService.GetDock().LeftSpacing;

    public void SetLeftSpacing(int spacing)
    {
        _dockService.GetDock().LeftSpacing = Math.Max(0, spacing);
        _dockService.SaveChanges();
    }

    public int GetRightSpacing() => _dockService.GetDock().RightSpacing;

    public void SetRightSpacing(int spacing)
    {
        _dockService.GetDock().RightSpacing = Math.Max(0, spacing);
        _dockService.SaveChanges();
    }

    public int GetBottomSpacing() => _dockService.GetDock().BottomSpacing;

    public void SetBottomSpacing(int spacing)
    {
        _dockService.GetDock().BottomSpacing = Math.Max(0, spacing);
        _dockService.SaveChanges();
    }

    public bool GetAutoHide() => _dockService.GetDock().AutoHide;

    public void SetAutoHide(bool value)
    {
        _dockService.GetDock().AutoHide = value;
        _dockService.SaveChanges();
    }

    public int GetAutoHideDelay() => _dockService.GetDock().AutoHideDelay;

    public void SetAutoHideDelay(int value)
    {
        _dockService.GetDock().AutoHideDelay = Math.Clamp(value, 0, 2000);
        _dockService.SaveChanges();
    }

    public enum DockEdge { Top, Bottom, Left, Right }

    public DockEdge DetermineDockEdge(
        ScreenBounds bounds, DockModel dock, double windowWidth, double windowHeight, double shownX, double shownY)
    {
        if (dock.PositioningMode == DockPositioningMode.DYNAMIC)
        {
            double centerX = shownX + (windowWidth / 2.0);
            double centerY = shownY + (windowHeight / 2.0);

            double distToTop = Math.Abs(centerY - bounds.MinY);
            double distToBottom = Math.Abs(bounds.MaxY - centerY);
            double distToLeft = Math.Abs(centerX - bounds.MinX);
            double distToRight = Math.Abs(bounds.MaxX - centerX);

            double minDist = Math.Min(Math.Min(distToTop, distToBottom), Math.Min(distToLeft, distToRight));

            if (minDist == distToTop) return DockEdge.Top;
            if (minDist == distToBottom) return DockEdge.Bottom;
            if (minDist == distToLeft) return DockEdge.Left;
            return DockEdge.Right;
        }

        if (dock.VerticalDock)
        {
            return dock.HorizontalAnchor == DockHorizontalAnchor.RIGHT ? DockEdge.Right : DockEdge.Left;
        }
        else
        {
            return dock.VerticalAnchor == DockVerticalAnchor.DOWN ? DockEdge.Bottom : DockEdge.Top;
        }
    }

    /// <summary>
    /// Computes the dock's retracted position when auto-hidden (leaving a 2px sliver at the screen edge).
    /// </summary>
    public (double X, double Y) ResolveHiddenPosition(
        double windowWidth, double windowHeight, double shownX, double shownY)
    {
        if (_screenBoundsProvider == null)
            return (shownX, shownY);

        var bounds = _screenBoundsProvider.GetPrimaryScreenBounds();
        var dock = _dockService.GetDock();
        var edge = DetermineDockEdge(bounds, dock, windowWidth, windowHeight, shownX, shownY);

        return edge switch
        {
            DockEdge.Right => (SnapToPixel(bounds.MaxX - 2), SnapToPixel(shownY)),
            DockEdge.Left => (SnapToPixel(bounds.MinX - windowWidth + 2), SnapToPixel(shownY)),
            DockEdge.Bottom => (SnapToPixel(shownX), SnapToPixel(bounds.MaxY - 2)),
            _ => (SnapToPixel(shownX), SnapToPixel(bounds.MinY - windowHeight + 2))
        };
    }

    /// <summary>
    /// Determines whether the mouse cursor is touching the screen edge, moving in the corridor,
    /// or hovering over the dock's resting area.
    /// </summary>
    public bool IsCursorInTriggerZone(
        double cursorX, double cursorY, double windowWidth, double windowHeight, double shownX, double shownY)
    {
        // 1. Direct hit on the dock's resting area ("the place of dock") with generous padding
        const double hoverPadding = 12.0;
        if (cursorX >= shownX - hoverPadding && cursorX <= shownX + windowWidth + hoverPadding &&
            cursorY >= shownY - hoverPadding && cursorY <= shownY + windowHeight + hoverPadding)
        {
            return true;
        }

        if (_screenBoundsProvider == null)
            return false;

        var bounds = _screenBoundsProvider.GetPrimaryScreenBounds();
        var dock = _dockService.GetDock();
        var edge = DetermineDockEdge(bounds, dock, windowWidth, windowHeight, shownX, shownY);

        const double edgeThreshold = 10.0;

        switch (edge)
        {
            case DockEdge.Top:
                bool inTopHorizontal = cursorX >= shownX - 16 && cursorX <= shownX + windowWidth + 16;
                bool inTopCorridor = inTopHorizontal && cursorY <= shownY + windowHeight + hoverPadding;
                bool atTopScreenEdge = cursorY <= bounds.MinY + edgeThreshold;
                return inTopCorridor || atTopScreenEdge;

            case DockEdge.Bottom:
                bool inBottomHorizontal = cursorX >= shownX - 16 && cursorX <= shownX + windowWidth + 16;
                bool inBottomCorridor = inBottomHorizontal && cursorY >= shownY - hoverPadding;
                bool atBottomScreenEdge = cursorY >= bounds.MaxY - edgeThreshold;
                return inBottomCorridor || atBottomScreenEdge;

            case DockEdge.Left:
                bool inLeftVertical = cursorY >= shownY - 16 && cursorY <= shownY + windowHeight + 16;
                bool inLeftCorridor = inLeftVertical && cursorX <= shownX + windowWidth + hoverPadding;
                bool atLeftScreenEdge = cursorX <= bounds.MinX + edgeThreshold;
                return inLeftCorridor || atLeftScreenEdge;

            case DockEdge.Right:
                bool inRightVertical = cursorY >= shownY - 16 && cursorY <= shownY + windowHeight + 16;
                bool inRightCorridor = inRightVertical && cursorX >= shownX - hoverPadding;
                bool atRightScreenEdge = cursorX >= bounds.MaxX - edgeThreshold;
                return inRightCorridor || atRightScreenEdge;

            default:
                return false;
        }
    }

    public bool IsDynamicPositioning() => GetPositioningMode() == DockPositioningMode.DYNAMIC;

    /// <summary>
    /// Computes the dock's screen position based on the current positioning mode.
    /// Returns (x, y) in device pixels.
    /// </summary>
    public (double X, double Y) ResolvePosition(double windowWidth, double windowHeight)
    {
        DockModel dock = _dockService.GetDock();

        if (dock.PositioningMode == DockPositioningMode.DYNAMIC)
            return (SnapToPixel(dock.DockPositionX), SnapToPixel(dock.DockPositionY));

        if (_screenBoundsProvider == null)
            return (0, 0);

        var bounds = _screenBoundsProvider.GetPrimaryScreenBounds();
        double x = SnapToPixel(ResolveHorizontalPosition(bounds, windowWidth, dock));
        double y = SnapToPixel(ResolveVerticalPosition(bounds, windowHeight, dock));
        return (x, y);
    }

    public static double SnapToPixel(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    private static double ResolveHorizontalPosition(
        ScreenBounds bounds, double windowWidth, DockModel dock)
    {
        return dock.HorizontalAnchor switch
        {
            DockHorizontalAnchor.LEFT => bounds.MinX + dock.LeftSpacing,
            DockHorizontalAnchor.MIDDLE => bounds.MinX + ((bounds.Width - windowWidth) / 2),
            DockHorizontalAnchor.RIGHT => bounds.MaxX - windowWidth - dock.RightSpacing,
            _ => bounds.MinX
        };
    }

    private static double ResolveVerticalPosition(
        ScreenBounds bounds, double windowHeight, DockModel dock)
    {
        return dock.VerticalAnchor switch
        {
            DockVerticalAnchor.TOP => bounds.MinY + dock.TopSpacing,
            DockVerticalAnchor.MIDDLE => bounds.MinY + ((bounds.Height - windowHeight) / 2),
            DockVerticalAnchor.DOWN => bounds.MaxY - windowHeight - dock.BottomSpacing,
            _ => bounds.MinY
        };
    }
}
