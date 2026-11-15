using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CedroModernDock.Controls;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;
using CedroModernDock.Views;
using Avalonia.VisualTree;

namespace CedroModernDock.InteractionTests;

public class DiscreetModeInteractionTests
{
    [AvaloniaFact]
    public void SettingsExposeTheModeAndKeepItAwakeRegardlessOfTheOpeningRoute()
    {
        using var fixture = new Fixture();
        fixture.SetIdle(true);
        SettingsWindow.Open(fixture.Services, fixture.Window, fixture.ViewModel.UpdateDockUI, _ => { }, SettingsViewModel.PageGeneral);
        var settings = (SettingsWindow)typeof(SettingsWindow).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try
        {
            Assert.True(SettingsWindow.IsAnyOpen);
            Assert.True(fixture.IsInteracting());
            var toggle = settings.FindControl<ToggleSwitch>("DiscreetModeToggle")!;
            var opacitySlider = settings.FindControl<Slider>("DiscreetModeOpacitySlider")!;
            Assert.False(opacitySlider.IsEffectivelyVisible);
            Assert.Equal(15, opacitySlider.Minimum);
            Assert.Equal(50, opacitySlider.Maximum);
            Assert.Equal(15, opacitySlider.Value);
            toggle.IsChecked = true;
            settings.FindControl<Slider>("DiscreetModeDelaySlider")!.Value = 600;
            opacitySlider.Value = 40;
            Dispatcher.UIThread.RunJobs();
            Assert.True(opacitySlider.IsEffectivelyVisible);
            Assert.True(fixture.Services.AppearanceService.GetDiscreetMode());
            Assert.Equal(600, fixture.Services.AppearanceService.GetDiscreetModeDelay());
            Assert.Equal(40, fixture.Services.AppearanceService.GetDiscreetModeOpacity());
            Pump(450);
            Assert.Equal(1, fixture.Window.FindControl<Panel>("DockBar")!.Opacity, precision: 3);
        }
        finally { settings.Close(); }
        Assert.False(SettingsWindow.IsAnyOpen);
        SettingsWindow.Open(fixture.Services, fixture.Window, fixture.ViewModel.UpdateDockUI, _ => { }, SettingsViewModel.PageGeneral);
        settings = (SettingsWindow)typeof(SettingsWindow).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try { Assert.Equal(40, settings.FindControl<Slider>("DiscreetModeOpacitySlider")!.Value); }
        finally { settings.Close(); }
    }

    [AvaloniaFact]
    public void IdleAppearanceDoesNotMoveTheDockOrAnimateIndividualIcons()
    {
        using var fixture = new Fixture();
        var position = fixture.Window.Position;
        var bar = fixture.Window.FindControl<Panel>("DockBar")!;
        foreach (int opacity in new[] { 15, 30, 50 })
        {
            fixture.Services.AppearanceService.SetDiscreetModeOpacity(opacity);
            fixture.SetIdle(true);
            Pump(500);
            Assert.Equal(opacity / 100.0, bar.Opacity, precision: 3);
            Assert.Equal(0.96, bar.RenderTransform!.Value.M11, precision: 3);
            Assert.Equal(position, fixture.Window.Position);
            foreach (var icon in fixture.Window.GetVisualDescendants().OfType<DockItemButton>())
                Assert.Equal(1, ((Control)icon.Content!).RenderTransform!.Value.M11, precision: 3);
            fixture.Window.WakeDiscreetMode();
            Pump(500);
            Assert.Equal(1, bar.Opacity, precision: 3);
            Assert.Equal(1, bar.RenderTransform!.Value.M11, precision: 3);
        }
    }

    [AvaloniaFact]
    public void AnOpenDockMenuKeepsDiscreetModeAwake()
    {
        using var fixture = new Fixture();
        Assert.False(fixture.IsInteracting());
        fixture.Window.FindControl<Panel>("DockBar")!.RaiseEvent(new ContextRequestedEventArgs());
        Assert.True(fixture.IsInteracting());
        var menu = (ContextMenu)typeof(MainWindow).GetField("_dockContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Window)!;
        menu.Close();
        Assert.False(fixture.IsInteracting());
    }

    private static void Pump(int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds)
        {
            Thread.Sleep(8);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private sealed class Fixture : IDisposable, IDockRepository
    {
        private readonly DockModel _model = new();
        private readonly string _themesDirectory = Path.Combine(Path.GetTempPath(), "CedroDiscreetUi_" + Guid.NewGuid().ToString("N"));
        public AppServices Services { get; }
        public MainWindowViewModel ViewModel { get; }
        public MainWindow Window { get; }

        public Fixture()
        {
            _model.AddItem(new DockSettingsItemModel());
            var dock = new DockService(this);
            Services = new AppServices(dock, new DockAppearanceService(dock), new DockPositioningService(dock),
                null!, null!, null!, new LocalizationService(dock), new ThemeService(dock, new FileSystemThemeRepository(_themesDirectory)));
            ViewModel = new MainWindowViewModel(Services);
            Window = new TestWindow { DataContext = ViewModel };
            Window.SetAppServices(Services);
            ViewModel.UpdateDockUI();
            Window.Show();
            Window.UpdateLayout();
        }

        public void SetIdle(bool idle) => typeof(MainWindow).GetMethod("SetDiscreetIdle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Window, new object[] { idle });
        public bool IsInteracting() => (bool)typeof(MainWindow).GetMethod("IsDiscreetInteractionActive", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Window, new object[] { new POINT { X = -10000, Y = -10000 } })!;
        public DockModel Load() => _model;
        public void Save(DockModel model) { }
        public void Dispose()
        {
            Window.Close();
            if (Directory.Exists(_themesDirectory)) Directory.Delete(_themesDirectory, recursive: true);
        }
    }

    private sealed class TestWindow : MainWindow
    {
        protected override void OnOpened(EventArgs e) { }
    }
}
