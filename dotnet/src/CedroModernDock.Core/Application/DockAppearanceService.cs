namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Models;

/// <summary>Direct port of DockAppearanceService.</summary>
public class DockAppearanceService
{
    private readonly DockService _dockService;

    public DockAppearanceService(DockService dockService)
    {
        _dockService = dockService;
    }

    public DockModel GetDock() => _dockService.GetDock();

    public int GetIconsSize() => GetDock().IconsSize;

    public void SetIconsSize(int iconsSize)
    {
        GetDock().IconsSize = iconsSize;
        _dockService.SaveChanges();
    }

    public int GetSpacingBetweenIcons() => GetDock().SpacingBetweenIcons;

    public void SetSpacingBetweenIcons(int spacingValue)
    {
        GetDock().SpacingBetweenIcons = spacingValue;
        _dockService.SaveChanges();
    }

    public int GetDockTransparencyPercentage() => (int)(GetDock().DockTransparency * 100);

    public void SetDockTransparencyPercentage(int value)
    {
        GetDock().DockTransparency = (double)value / 100;
        _dockService.SaveChanges();
    }

    public int GetDockBorderRounding() => GetDock().DockBorderRounding;

    public void SetDockBorderRounding(int value)
    {
        GetDock().DockBorderRounding = value;
        _dockService.SaveChanges();
    }

    public int GetDockPadding() => GetDock().DockPadding;

    public void SetDockPadding(int value)
    {
        GetDock().DockPadding = Math.Clamp(value, 0, 30);
        _dockService.SaveChanges();
    }

    public string GetDockColorRGB() => GetDock().DockColorRGB;

    public void SetDockColorRGB(string value)
    {
        GetDock().DockColorRGB = value;
        _dockService.SaveChanges();
    }

    public DockTheme GetDockTheme()
    {
        DockModel dock = GetDock();
        return new DockTheme(dock.DockColorRGB, dock.DockTransparency, dock.DockBorderRounding);
    }

    public bool GetShowUnpinnedRunningApps() => GetDock().ShowUnpinnedRunningApps;

    public void SetShowUnpinnedRunningApps(bool value)
    {
        GetDock().ShowUnpinnedRunningApps = value;
        _dockService.SaveChanges();
    }

    public bool GetVerticalDock() => GetDock().VerticalDock;

    public void SetVerticalDock(bool value)
    {
        GetDock().VerticalDock = value;
        _dockService.SaveChanges();
    }

    public bool GetTintIcons() => GetDock().TintIcons;

    public void SetTintIcons(bool value)
    {
        GetDock().TintIcons = value;
        _dockService.SaveChanges();
    }

    public string GetTintColorRGB() => GetDock().TintColorRGB;

    public void SetTintColorRGB(string value)
    {
        GetDock().TintColorRGB = value;
        _dockService.SaveChanges();
    }
}
