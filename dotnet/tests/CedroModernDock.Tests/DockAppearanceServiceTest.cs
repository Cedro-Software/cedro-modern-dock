namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

public class DockAppearanceServiceTest
{
    [Fact]
    public void DefaultDockPaddingIsTen()
    {
        var repository = new InMemoryDockRepository();
        var dockService = new DockService(repository);
        var appearanceService = new DockAppearanceService(dockService);

        Assert.Equal(10, appearanceService.GetDockPadding());
    }

    [Fact]
    public void SetDockPaddingUpdatesAndClampsValue()
    {
        var repository = new InMemoryDockRepository();
        var dockService = new DockService(repository);
        var appearanceService = new DockAppearanceService(dockService);

        appearanceService.SetDockPadding(4);
        Assert.Equal(4, appearanceService.GetDockPadding());
        Assert.Equal(4, repository.SavedModel!.DockPadding);

        // Clamp negative to 0
        appearanceService.SetDockPadding(-5);
        Assert.Equal(0, appearanceService.GetDockPadding());

        // Clamp above 30 to 30
        appearanceService.SetDockPadding(50);
        Assert.Equal(30, appearanceService.GetDockPadding());
    }

    private sealed class InMemoryDockRepository : IDockRepository
    {
        private readonly DockModel _model = new();
        public DockModel? SavedModel { get; private set; }

        public DockModel Load()
        {
            SavedModel ??= _model;
            return _model;
        }

        public void Save(DockModel model) => SavedModel = model;
    }
}
