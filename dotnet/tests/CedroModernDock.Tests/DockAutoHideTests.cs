namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Persistence;

public class DockAutoHideTests
{
    private class MockScreenBoundsProvider : IScreenBoundsProvider
    {
        private readonly ScreenBounds _bounds;
        public MockScreenBoundsProvider(ScreenBounds bounds) => _bounds = bounds;
        public ScreenBounds GetPrimaryScreenBounds() => _bounds;
    }

    private class MockDockRepository : IDockRepository
    {
        public DockModel Dock { get; set; } = new();
        public DockModel Load() => Dock;
        public void Save(DockModel dock) => Dock = dock;
    }

    [Fact]
    public void ResolvesHiddenPosition_ForHorizontalDockAtBottom()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = false;
        repo.Dock.VerticalAnchor = DockVerticalAnchor.DOWN;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        var (hx, hy) = positioningService.ResolveHiddenPosition(windowWidth: 500, windowHeight: 60, shownX: 710, shownY: 1010);

        Assert.Equal(710, hx);
        Assert.Equal(1080 - 2, hy); // 1078 leaves 2px sliver
    }

    [Fact]
    public void ResolvesHiddenPosition_ForHorizontalDockAtTop()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = false;
        repo.Dock.VerticalAnchor = DockVerticalAnchor.TOP;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        var (hx, hy) = positioningService.ResolveHiddenPosition(windowWidth: 500, windowHeight: 60, shownX: 710, shownY: 10);

        Assert.Equal(710, hx);
        Assert.Equal(0 - 60 + 2, hy); // -58 leaves 2px sliver
    }

    [Fact]
    public void ResolvesHiddenPosition_ForVerticalDockAtRight()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = true;
        repo.Dock.HorizontalAnchor = DockHorizontalAnchor.RIGHT;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        var (hx, hy) = positioningService.ResolveHiddenPosition(windowWidth: 60, windowHeight: 500, shownX: 1850, shownY: 290);

        Assert.Equal(1920 - 2, hx); // 1918 leaves 2px sliver
        Assert.Equal(290, hy);
    }

    [Fact]
    public void ResolvesHiddenPosition_ForVerticalDockAtLeft()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = true;
        repo.Dock.HorizontalAnchor = DockHorizontalAnchor.LEFT;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        var (hx, hy) = positioningService.ResolveHiddenPosition(windowWidth: 60, windowHeight: 500, shownX: 10, shownY: 290);

        Assert.Equal(0 - 60 + 2, hx); // -58 leaves 2px sliver
        Assert.Equal(290, hy);
    }

    [Fact]
    public void DetectsCursorInTriggerZone_BottomEdge()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = false;
        repo.Dock.VerticalAnchor = DockVerticalAnchor.DOWN;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        // Touching the bottom edge (1076 to 1080)
        Assert.True(positioningService.IsCursorInTriggerZone(960, 1079, 500, 60, 710, 1010));
        Assert.True(positioningService.IsCursorInTriggerZone(960, 1076, 500, 60, 710, 1010));

        // Away from bottom edge and away from dock
        Assert.False(positioningService.IsCursorInTriggerZone(960, 500, 500, 60, 710, 1010));
    }

    [Fact]
    public void DetectsCursorInTriggerZone_TopEdge()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = false;
        repo.Dock.VerticalAnchor = DockVerticalAnchor.TOP;

        var bounds = new ScreenBounds(0, 0, 1920, 1080);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        Assert.True(positioningService.IsCursorInTriggerZone(960, 2, 500, 60, 710, 10));
        Assert.False(positioningService.IsCursorInTriggerZone(960, 500, 500, 60, 710, 10));
    }

    [Fact]
    public void DetectsCursorInTriggerZone_HoverAtRestingPlace()
    {
        var repo = new MockDockRepository();
        repo.Dock.VerticalDock = false;
        repo.Dock.VerticalAnchor = DockVerticalAnchor.TOP;

        var bounds = new ScreenBounds(0, 0, 1600, 900);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        // Dock is resting at (661, 25) with width 500, height 80
        // Hovering right over the resting place (e.g. 700, 50) MUST trigger
        Assert.True(positioningService.IsCursorInTriggerZone(700, 50, 500, 80, 661, 25));

        // Hovering near the resting place
        Assert.True(positioningService.IsCursorInTriggerZone(660, 20, 500, 80, 661, 25));

        // Hovering far away from the resting place and edge
        Assert.False(positioningService.IsCursorInTriggerZone(700, 500, 500, 80, 661, 25));
    }

    [Fact]
    public void ResolvesHiddenPosition_InDynamicMode_ClosestToTop()
    {
        var repo = new MockDockRepository();
        repo.Dock.PositioningMode = DockPositioningMode.DYNAMIC;

        var bounds = new ScreenBounds(0, 0, 1600, 900);
        var positioningService = new DockPositioningService(new DockService(repo), new MockScreenBoundsProvider(bounds));

        // User placed dock near top (661, 25)
        var (hx, hy) = positioningService.ResolveHiddenPosition(500, 80, 661, 25);
        Assert.Equal(661, hx);
        Assert.Equal(0 - 80 + 2, hy); // -78 leaves 2px sliver at top
    }

    [Fact]
    public void SavesAndLoadsAutoHideSettings()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CedroAutoHideTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string configPath = Path.Combine(tempDir, "config.json");
            var repository = new JsonDockRepository(configPath);

            var model = new DockModel();
            model.AutoHide = true;
            model.AutoHideDelay = 450;

            repository.Save(model);
            DockModel loadedModel = repository.Load();

            Assert.True(loadedModel.AutoHide);
            Assert.Equal(450, loadedModel.AutoHideDelay);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
