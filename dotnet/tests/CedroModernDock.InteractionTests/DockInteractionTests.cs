using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CedroModernDock.Controls;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;
using CedroModernDock.Views;

namespace CedroModernDock.InteractionTests;

public class DockInteractionTests
{
    [AvaloniaFact]
    public void HoverRendersTheEntireIconOutsideTheOriginalButtonBounds()
    {
        using var fixture = new DockFixture(iconSize: 57);
        using var icon = new WriteableBitmap(new PixelSize(57, 57), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var pixels = icon.Lock())
        {
            var data = new byte[pixels.RowBytes * pixels.Size.Height];
            for (int y = 0; y < pixels.Size.Height; y++)
                for (int x = 0; x < pixels.Size.Width; x++)
                {
                    int offset = y * pixels.RowBytes + x * 4;
                    data[offset + 2] = data[offset + 3] = 255;
                }
            Marshal.Copy(data, 0, pixels.Address, data.Length);
        }
        fixture.ViewModel.Items[0].Icon = icon;
        fixture.ViewModel.DockPadding = 0;
        var button = fixture.Window.GetVisualDescendants().OfType<DockItemButton>().First();
        foreach (double zoom in new[] { 1.25, 1.8 })
        {
            fixture.ViewModel.Style.HoverZoom = zoom;
            // Refresh the headroom binding without starting native dock services.
            fixture.ViewModel.IconsSize = 58;
            fixture.ViewModel.IconsSize = 57;
            fixture.Window.UpdateLayout();
            Pump(50);
            fixture.Window.MouseMove(CenterInWindow(button, fixture.Window));
            Assert.True(button.IsPointerOver);
            Pump(400);
            Assert.Equal(zoom, Scale(button), precision: 3);
            using var frame = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(fixture.Window.ClientSize.Width),
                (int)Math.Ceiling(fixture.Window.ClientSize.Height)));
            frame.Render(fixture.Window);
            using var stream = new MemoryStream();
            frame.Save(stream);
            stream.Position = 0;
            using var rendered = new System.Drawing.Bitmap(stream);
            int minX = rendered.Width, minY = rendered.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < rendered.Height; y++)
                for (int x = 0; x < rendered.Width; x++)
                {
                    var pixel = rendered.GetPixel(x, y);
                    if (pixel.R > 240 && pixel.G < 15 && pixel.B < 15)
                    {
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
                }
            string ancestors = string.Join("; ", ((Control)button.Content!).GetVisualAncestors()
                .Select(v => $"{v.GetType().Name} {v.Bounds.Size} ClipToBounds={v.ClipToBounds} Clip={v.Clip}"));
            int expected = (int)Math.Floor(57 * zoom) - 1;
            Assert.True(maxX - minX + 1 >= expected && maxY - minY + 1 >= expected,
                $"Clipped {zoom}x icon: {maxX - minX + 1}x{maxY - minY + 1}, expected at least {expected}. {ancestors}");
        }
    }

    [AvaloniaFact]
    public void FastHoverDoesNotReplayAfterThePointerLeaves()
    {
        using var fixture = new DockFixture();
        foreach (double zoom in new[] { 1.3, 1.8 })
        {
            fixture.ViewModel.Style.HoverZoom = zoom;
            var buttons = fixture.Window.GetVisualDescendants().OfType<DockItemButton>().ToArray();
            foreach (var button in buttons)
            {
                fixture.Window.MouseMove(CenterInWindow(button, fixture.Window));
                Pump(30);
            }
            fixture.Window.MouseMove(new Point(-50, -50));
            var previous = buttons.Select(Scale).ToArray();
            for (int frame = 0; frame < 35; frame++)
            {
                Pump(15);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Assert.False(buttons[i].IsPointerOver);
                    double current = Scale(buttons[i]);
                    Assert.True(current <= previous[i] + 0.002, $"Icon {i} enlarged after pointer exit: {previous[i]} -> {current}");
                    previous[i] = current;
                }
            }
            Assert.All(buttons, button => Assert.Equal(1, Scale(button), precision: 3));
        }
    }

    [AvaloniaFact]
    public void HoverKeepsTheButtonHitAreaFixedAndPressStillClicks()
    {
        using var fixture = new DockFixture();
        var button = fixture.Window.GetVisualDescendants().OfType<DockItemButton>().First();
        var center = CenterInWindow(button, fixture.Window);
        fixture.Window.MouseMove(center);
        Pump(220);
        Assert.True(Scale(button) > 1.25);
        Assert.Equal(Matrix.Identity, button.RenderTransform?.Value ?? Matrix.Identity);
        var outside = button.TranslatePoint(new Point(-2, button.Bounds.Height / 2), fixture.Window)!.Value;
        fixture.Window.MouseMove(outside);
        Assert.False(button.IsPointerOver);
        bool clicked = false;
        button.Click += (_, _) => clicked = true;
        fixture.Window.MouseDown(center, MouseButton.Left);
        Pump(220);
        Assert.InRange(Scale(button), 1.16, 1.18);
        fixture.Window.MouseUp(center, MouseButton.Left);
        Assert.True(clicked);
    }

    [AvaloniaFact]
    public void QuitIsAbsentFromIconMenusButAvailableOnTheDockBackground()
    {
        using var fixture = new DockFixture();
        var icon = fixture.Window.GetVisualDescendants().OfType<DockItemButton>().First();
        icon.RaiseEvent(new ContextRequestedEventArgs());
        var iconMenu = CurrentMenu(fixture.Window);
        Assert.DoesNotContain(iconMenu.Items.OfType<MenuItem>(), i => Equals(i.Header, "Quit Cedro Dock"));
        var bar = fixture.Window.FindControl<Panel>("DockBar")!;
        foreach (object item in new object[]
        {
            new DockItemViewModel(new DockSettingsItemModel(), "Settings", fixture.ViewModel.LaunchCommand),
            new DockItemViewModel(new DockSeparatorItemModel(), "Separator", fixture.ViewModel.LaunchCommand),
            new RunningAppViewModel("unused.exe", "Running app")
        })
        {
            var source = new Border { DataContext = item, Width = 20, Height = 20 };
            bar.Children.Add(source);
            source.RaiseEvent(new ContextRequestedEventArgs());
            Assert.DoesNotContain(CurrentMenu(fixture.Window).Items.OfType<MenuItem>(), i => Equals(i.Header, "Quit Cedro Dock"));
            bar.Children.Remove(source);
        }
        bar.RaiseEvent(new ContextRequestedEventArgs());
        var backgroundMenu = CurrentMenu(fixture.Window);
        Assert.False(iconMenu.IsOpen);
        Assert.Contains(backgroundMenu.Items.OfType<MenuItem>(), i => Equals(i.Header, "Quit Cedro Dock"));
    }

    [AvaloniaFact]
    public void DesktopClicksDismissTheMenuAndReleaseTheObserver()
    {
        using var fixture = new DockFixture();
        var menu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Item" } } };
        menu.Open(fixture.Window);
        fixture.Window.UpdateLayout();
        Action<PixelPoint>? click = null;
        var observer = new Observer();
        using var dismissal = new DesktopContextMenuDismissal(menu, callback => { click = callback; return observer; });
        click!(menu.PointToScreen(new Point(20, 20)));
        Dispatcher.UIThread.RunJobs();
        Assert.True(menu.IsOpen);
        click(new PixelPoint(-1000, -1000));
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen);
        Assert.True(observer.IsDisposed);
    }

    [AvaloniaFact]
    public void ClickingAnOpenSubmenuDoesNotDismissItsParent()
    {
        using var fixture = new DockFixture();
        var child = new MenuItem { Header = "Child" };
        var parent = new MenuItem { Header = "Themes", ItemsSource = new[] { child } };
        var menu = new ContextMenu { ItemsSource = new[] { parent } };
        menu.Open(fixture.Window);
        parent.IsSubMenuOpen = true;
        fixture.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Action<PixelPoint>? click = null;
        using var dismissal = new DesktopContextMenuDismissal(menu, callback => { click = callback; return new Observer(); });
        var popup = Assert.Single(parent.GetVisualDescendants().OfType<Popup>());
        var host = Assert.IsAssignableFrom<Control>(popup.Host);
        Assert.True(host.Bounds.Width > 0);
        click!(host.PointToScreen(new Point(host.Bounds.Width / 2, host.Bounds.Height / 2)));
        Dispatcher.UIThread.RunJobs();
        Assert.True(menu.IsOpen);
        Assert.True(parent.IsSubMenuOpen);
        menu.Close();
    }

    [AvaloniaFact]
    public void QueuedClicksFromAClosedMenuDoNotDismissItsReplacement()
    {
        using var fixture = new DockFixture();
        var oldMenu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Old" } } };
        oldMenu.Open(fixture.Window);
        Action<PixelPoint>? click = null;
        using var dismissal = new DesktopContextMenuDismissal(oldMenu, callback => { click = callback; return new Observer(); });
        click!(new PixelPoint(-1000, -1000));
        oldMenu.Close();
        var newMenu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "New" } } };
        newMenu.Open(fixture.Window);
        Dispatcher.UIThread.RunJobs();
        Assert.True(newMenu.IsOpen);
        newMenu.Close();
    }

    private static ContextMenu CurrentMenu(MainWindow window) => (ContextMenu)typeof(MainWindow)
        .GetField("_dockContextMenu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static double Scale(DockItemButton button) => ((Control)button.Content!).RenderTransform!.Value.M11;

    private static Point CenterInWindow(Control control, Window window) => control.TranslatePoint(
        new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

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

    private sealed class Observer : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class DockFixture : IDisposable
    {
        public MainWindowViewModel ViewModel { get; } = new() { ExitAction = () => { } };
        public MainWindow Window { get; }

        public DockFixture(int iconSize = 48)
        {
            ViewModel.IconsSize = iconSize;
            ViewModel.Style.IconSize = iconSize;
            for (int i = 0; i < 4; i++)
                ViewModel.Items.Add(new DockItemViewModel(new DockProgramItemModel($"Item {i}", "unused.exe"),
                    $"Item {i}", ViewModel.LaunchCommand) { Style = ViewModel.Style, IconSize = iconSize, Icon = IconLoader.LoadFromAsset("Assets/icons/cedro/logo_32.png") });
            Window = new HeadlessDockWindow { DataContext = ViewModel };
            Window.Show();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    // Skip desktop attachment and native preview/watchers in this isolated UI process.
    private sealed class HeadlessDockWindow : MainWindow
    {
        protected override void OnOpened(EventArgs e) { }
    }
}
