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
        set
        {
            if (SetProperty(ref _isRunning, value) && value)
            {
                StopBounce();
            }
        }
    }

    private double _bounceOffsetY;
    private Avalonia.Threading.DispatcherTimer? _bounceTimer;
    private DateTime _bounceStartTime;

    /// <summary>Vertical offset in pixels for the app-launch bounce micro-interaction.</summary>
    public double BounceOffsetY
    {
        get => _bounceOffsetY;
        private set => SetProperty(ref _bounceOffsetY, value);
    }

    public void StartBounce()
    {
        StopBounce();
        _bounceStartTime = DateTime.UtcNow;
        _bounceTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _bounceTimer.Tick += OnBounceTick;
        _bounceTimer.Start();
    }

    public void StopBounce()
    {
        if (_bounceTimer != null)
        {
            _bounceTimer.Stop();
            _bounceTimer.Tick -= OnBounceTick;
            _bounceTimer = null;
        }
        BounceOffsetY = 0.0;
    }

    private void OnBounceTick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.UtcNow - _bounceStartTime).TotalSeconds;

        // Stop bouncing after 8 seconds timeout or when the program has open windows
        if (elapsed > 8.0 || (elapsed > 0.8 && IsRunning))
        {
            StopBounce();
            return;
        }

        // Crisp, springy parabolic bounce cycle (0.45s per bounce)
        const double period = 0.45;
        double phase = (elapsed % period) / period; // 0.0 to 1.0

        // Parabolic arc: reaches apex (-15px) at phase 0.5, hits ground at 1.0
        const double maxApex = -15.0;
        BounceOffsetY = 4 * maxApex * phase * (1.0 - phase);
    }

    /// <summary>True if this item should show a running-app indicator (program items only).</summary>
    public bool ShowIndicator { get; }

    /// <summary>The icon render size in pixels (mirrors the dock's IconsSize setting).</summary>
    public int IconSize { get; set; } = 48;

    /// <summary>The executable path for running-indicator polling (program items only).</summary>
    public string? ExecutablePath { get; }

    /// <summary>Command invoked when the dock item is clicked.</summary>
    public ICommand ClickCommand { get; }

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
