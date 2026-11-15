namespace CedroModernDock.ViewModels;

using System.Windows.Input;
using Avalonia.Media.Imaging;
using CedroModernDock.Core.Models;

/// <summary>
/// ViewModel for a single dock item. Holds the icon bitmap, localized label,
/// running-app indicator state, and click-to-launch command.
/// </summary>
public class DockItemViewModel : ViewModelBase
{
    private Bitmap? _icon;
    private bool _isRunning;

    /// <summary>The underlying dock item model.</summary>
    public DockItem Item { get; }

    /// <summary>Localized display label (shown in tooltip).</summary>
    public string Label { get; }

    /// <summary>The icon bitmap to render (loaded from cache or assets).</summary>
    public Bitmap? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>True when the program has at least one open window (drives indicator dot).</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    /// <summary>True if this item should show a running-app indicator (program items only).</summary>
    public bool ShowIndicator { get; }

    /// <summary>The icon render size in pixels (mirrors the dock's IconsSize setting).</summary>
    public int IconSize { get; set; } = 48;

    /// <summary>The executable path for running-indicator polling (program items only).</summary>
    public string? ExecutablePath { get; }

    /// <summary>Command invoked when the dock item is clicked.</summary>
    public ICommand ClickCommand { get; }

    /// <summary>True for a separator item (rendered as a divider, not a button).</summary>
    public bool IsSeparator => Item is DockSeparatorItemModel;

    /// <summary>True for items rendered as a clickable icon button.</summary>
    public bool IsButton => !IsSeparator;

    /// <summary>Look shared by all dock items (set by the dock).</summary>
    public DockVisualStyle Style { get; set; } = new();

    /// <summary>Program and folder items can be renamed and retargeted.</summary>
    public bool CanEdit => Item is DockProgramItemModel || Item is DockFolderItemModel;

    /// <summary>The settings item must stay in the dock (same rule as the settings window).</summary>
    public bool CanRemove => Item is not DockSettingsItemModel;

    private double _opacity = 1.0;
    /// <summary>Lowered while the item is being dragged.</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    public DockItemViewModel(
        DockItem item,
        string label,
        ICommand clickCommand,
        bool showIndicator = false,
        string? executablePath = null)
    {
        Item = item;
        Label = label;
        ClickCommand = clickCommand;
        ShowIndicator = showIndicator;
        ExecutablePath = executablePath;
    }
}
