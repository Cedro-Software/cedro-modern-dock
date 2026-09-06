using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Adapters;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;
using CedroModernDock.Views;

namespace CedroModernDock;

public partial class App : Application
{
    private static AppServices? _appServices;
    private static MainWindow? _mainWindow;
    private static MainWindowViewModel? _mainViewModel;
    private static IClassicDesktopStyleApplicationLifetime? _desktop;

    /// <summary>
    /// Active singleton instance of the Settings menu window, if open.
    /// </summary>
    public static SettingsWindow? SettingsMenu => SettingsWindow.Instance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Keep the app alive via tray icon — the window is never closed, only hidden.
            _desktop = desktop;

            // Composition root — wire concrete Windows adapters into the application services.
            var appServices = CreateServices();
            _appServices = appServices;

            // Wire tray icon events (Avalonia TrayIcon events can't be set via XAML string attributes).
            WireTrayIcon(appServices.LocalizationService);

            var viewModel = new MainWindowViewModel(appServices);
            _mainViewModel = viewModel;

            var mainWindow = new MainWindow { DataContext = viewModel };
            mainWindow.SetAppServices(appServices);
            _mainWindow = mainWindow;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MemoryStream? _trayIconStream;

    private void WireTrayIcon(LocalizationService loc)
    {
        // Resolve the embedded Avalonia asset via AssetLoader.Open() with an
        // avares:// URI (same pattern as IconLoader.LoadFromAsset).  WindowIcon(string)
        // treats the argument as a file path on disk, which fails at runtime.
        //
        // Note: WindowIcon(Stream) may hold the stream until the icon is used,
        // so the stream must stay alive for the lifetime of the tray icon.
        _trayIconStream = new MemoryStream();
        using (var source = AssetLoader.Open(
            new Uri("avares://CedroModernDock/Assets/icons/cedro/logo_32.png")))
        {
            source.CopyTo(_trayIconStream);
        }
        _trayIconStream.Position = 0;

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(_trayIconStream),
            ToolTipText = "Cedro Modern Dock",
            Menu = new NativeMenu()
        };

        var settingsItem = new NativeMenuItem { Header = loc.Text("tray.openSettings") };
        var exitItem = new NativeMenuItem { Header = loc.Text("tray.exit") };

        // In Avalonia 11.3, NativeMenuItem uses Command property for click handling.
        settingsItem.Command = new ViewModels.RelayCommand(_ => OpenSettingsFromTray());
        exitItem.Command = new ViewModels.RelayCommand(_ => OnTrayExit());

        trayIcon.Menu.Items.Add(settingsItem);
        trayIcon.Menu.Items.Add(new NativeMenuItemSeparator());
        trayIcon.Menu.Items.Add(exitItem);

        // Also handle a direct click on the tray icon.
        trayIcon.Clicked += (_, _) => OpenSettingsFromTray();

        // Register the tray icon with the application.
        var icons = new TrayIcons();
        icons.Add(trayIcon);
        TrayIcon.SetIcons(this, icons);
    }

    // --- Tray icon event handlers ---

    private static void OnTrayIconClick()
    {
        // Left-click on tray icon → open settings (same as Java SystemTrayManager).
        OpenSettingsFromTray();
    }

    private static void OnTrayOpenSettings()
    {
        OpenSettingsFromTray();
    }

    private static void OnTrayExit()
    {
        _mainViewModel?.Shutdown();
        _desktop?.Shutdown();
    }

    private static void OpenSettingsFromTray()
    {
        if (_appServices == null || _mainWindow == null || _mainViewModel == null) return;
        if (!_mainWindow.IsVisible)
            _mainWindow.Show();
        SettingsWindow.Open(
            _appServices,
            _mainWindow,
            _mainViewModel.UpdateDockUI,
            mode => HandlePositioningModeChange(mode)
        );
    }

    private static void HandlePositioningModeChange(DockPositioningMode mode)
    {
        if (_appServices == null || _mainWindow == null) return;
        var currentMode = _appServices.PositioningService.GetPositioningMode();
        if (currentMode == DockPositioningMode.STATIC && mode == DockPositioningMode.DYNAMIC)
        {
            _appServices.DockService.SetDockPosition(_mainWindow.Position.X, _mainWindow.Position.Y);
        }
        _appServices.PositioningService.SetPositioningMode(mode);
    }

    /// <summary>
    /// Composes all application services with their Windows-specific adapters.
    /// Direct port of App.java createServices().
    /// </summary>
    private static AppServices CreateServices()
    {
        var repository = new JsonDockRepository();
        var dockService = new DockService(repository);

        // First run (no config yet, so a default was created): register the app
        // for auto-start so "Start with Windows" is enabled by default. The
        // Settings checkbox reads the registry, so this makes it checked.
        if (repository.WasDefaultCreated)
            AutoStartHelper.EnableAutoStart();

        var screenBoundsProvider = new WindowsScreenBoundsProvider();

        return new AppServices(
            DockService: dockService,
            AppearanceService: new DockAppearanceService(dockService),
            PositioningService: new DockPositioningService(dockService, screenBoundsProvider),
            ItemActionService: new DockItemActionService(
                new WindowsProgramLauncher(),
                new WindowsFolderLauncher(),
                new WindowsModuleLauncher()
            ),
            WindowPreviewService: new WindowPreviewService(new Win32WindowQueryGateway()),
            IconGateway: new CachedWindowsIconGateway(),
            LocalizationService: new LocalizationService(dockService)
        );
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}