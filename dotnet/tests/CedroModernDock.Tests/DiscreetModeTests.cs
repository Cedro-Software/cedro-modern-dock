namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Persistence;

public class DiscreetModeTests
{
    [Fact]
    public void WaitsForTheConfiguredUninterruptedIdleDelay()
    {
        var state = new DiscreetModeState();
        Assert.False(state.Update(true, false, 300, TimeSpan.Zero));
        Assert.False(state.Update(true, false, 300, TimeSpan.FromMilliseconds(299)));
        Assert.True(state.Update(true, false, 300, TimeSpan.FromMilliseconds(300)));
        Assert.True(state.Update(true, false, 300, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void InteractionWakesTheDockAndRestartsTheIdleDelay()
    {
        var state = new DiscreetModeState();
        state.Update(true, false, 300, TimeSpan.Zero);
        Assert.True(state.Update(true, false, 300, TimeSpan.FromSeconds(1)));
        Assert.False(state.Update(true, true, 300, TimeSpan.FromSeconds(1)));
        Assert.False(state.Update(true, false, 300, TimeSpan.FromSeconds(2)));
        Assert.False(state.Update(true, false, 300, TimeSpan.FromMilliseconds(2299)));
        Assert.True(state.Update(true, false, 300, TimeSpan.FromMilliseconds(2300)));
    }

    [Fact]
    public void DisablingTheModeResetsAnyPendingIdleTime()
    {
        var state = new DiscreetModeState();
        state.Update(true, false, 300, TimeSpan.Zero);
        Assert.False(state.Update(false, false, 300, TimeSpan.FromSeconds(1)));
        Assert.False(state.Update(true, false, 300, TimeSpan.FromSeconds(2)));
        Assert.True(state.Update(true, false, 300, TimeSpan.FromMilliseconds(2300)));
        state.Reset();
        Assert.False(state.Update(true, false, 300, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ZeroDelayStillKeepsAnInteractingDockAwake()
    {
        var state = new DiscreetModeState();
        Assert.False(state.Update(true, true, 0, TimeSpan.Zero));
        Assert.True(state.Update(true, false, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(450, 450)]
    [InlineData(2000, 1000)]
    public void AppearanceSettingsPersistAndClampTheDelay(int requested, int expected)
    {
        var repository = new MemoryDock();
        var appearance = new DockAppearanceService(new DockService(repository));
        Assert.False(appearance.GetDiscreetMode());
        Assert.Equal(300, appearance.GetDiscreetModeDelay());
        appearance.SetDiscreetMode(true);
        appearance.SetDiscreetModeDelay(requested);
        Assert.True(repository.Model.DiscreetMode);
        Assert.Equal(expected, appearance.GetDiscreetModeDelay());
        repository.Model.DiscreetModeDelay = requested;
        Assert.Equal(expected, appearance.GetDiscreetModeDelay());
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(45, 45)]
    [InlineData(100, 50)]
    public void AppearanceSettingsPersistAndClampTheOpacity(int requested, int expected)
    {
        var repository = new MemoryDock();
        var appearance = new DockAppearanceService(new DockService(repository));
        Assert.Equal(15, appearance.GetDiscreetModeOpacity());
        appearance.SetDiscreetModeOpacity(requested);
        Assert.Equal(expected, repository.Model.DiscreetModeOpacity);
        Assert.Equal(expected, appearance.GetDiscreetModeOpacity());
        repository.Model.DiscreetModeOpacity = requested;
        Assert.Equal(expected, appearance.GetDiscreetModeOpacity());
    }

    [Theory]
    [InlineData("{}", false, 300, 15)]
    [InlineData("{\"autoHide\":true,\"autoHideDelay\":450}", true, 450, 15)]
    [InlineData("{\"autoHide\":true,\"discreetModeOpacity\":40}", true, 300, 40)]
    public void LoadsCompatibleConfigurationAndRoundTripsTheMode(string json, bool enabled, int delay, int opacity)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CedroDiscreetTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "config.json");
            File.WriteAllText(path, json);
            var repository = new JsonDockRepository(path);
            var model = repository.Load();
            Assert.Equal(enabled, model.DiscreetMode);
            Assert.Equal(delay, model.DiscreetModeDelay);
            Assert.Equal(opacity, model.DiscreetModeOpacity);
            model.DiscreetMode = true;
            model.DiscreetModeDelay = 650;
            model.DiscreetModeOpacity = 45;
            repository.Save(model);
            var loaded = repository.Load();
            Assert.True(loaded.DiscreetMode);
            Assert.Equal(650, loaded.DiscreetModeDelay);
            Assert.Equal(45, loaded.DiscreetModeOpacity);
            Assert.Contains("\"autoHide\"", File.ReadAllText(path));
            Assert.Contains("\"discreetModeOpacity\"", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class MemoryDock : IDockRepository
    {
        public DockModel Model { get; } = new();
        public DockModel Load() => Model;
        public void Save(DockModel model) { }
    }
}
