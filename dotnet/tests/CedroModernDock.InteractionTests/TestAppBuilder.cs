using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CedroModernDock.InteractionTests.TestAppBuilder))]

namespace CedroModernDock.InteractionTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
