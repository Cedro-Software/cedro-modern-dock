namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;

public class SettingsItemSynchronizationTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RemovingSelectedItemFromDockClearsSelectionWithoutRemovingItsNeighbor(int selectedIndex)
    {
        using var fixture = new Fixture();
        fixture.Settings.SelectedItemIndex = selectedIndex;

        fixture.Dock.RemoveItem(selectedIndex);
        fixture.Settings.RemoveSelected();

        Assert.Equal(-1, fixture.Settings.SelectedItemIndex);
        Assert.False(fixture.Settings.CanRemove);
        Assert.Equal(2, fixture.Dock.GetItems().Count);
        Assert.Equal(fixture.Dock.GetItems(), fixture.Settings.ItemEntries.Select(e => e.Item));
    }

    [Fact]
    public void SelectionFollowsTheSameItemThroughExternalInsertionsAndMoves()
    {
        using var fixture = new Fixture();
        var selected = fixture.Dock.GetItems()[1];
        fixture.Settings.SelectedItemIndex = 1;

        fixture.Dock.InsertItem(0, new DockSeparatorItemModel());
        Assert.Equal(2, fixture.Settings.SelectedItemIndex);
        fixture.Dock.MoveItem(2, 0);
        Assert.Equal(0, fixture.Settings.SelectedItemIndex);

        fixture.Settings.RemoveSelected();
        Assert.DoesNotContain(selected, fixture.Dock.GetItems());
        Assert.Equal(3, fixture.Dock.GetItems().Count);
    }

    [Fact]
    public void ExternalEditsRefreshTheSelectedRow()
    {
        using var fixture = new Fixture();
        fixture.Settings.SelectedItemIndex = 1;
        fixture.Dock.GetItems()[1].Label = "Renamed";

        fixture.Dock.SaveItemChanges();

        Assert.Equal("Renamed", fixture.Settings.ItemEntries[1].Label);
        Assert.Equal(1, fixture.Settings.SelectedItemIndex);
    }

    [Fact]
    public void SettingsItemCannotBeRemovedEvenByCallingTheActionDirectly()
    {
        using var fixture = new Fixture();
        var settingsItem = new DockSettingsItemModel();
        fixture.Dock.InsertItem(0, settingsItem);
        fixture.Settings.SelectedItemIndex = 0;

        fixture.Settings.RemoveSelected();

        Assert.False(fixture.Settings.CanRemove);
        Assert.Contains(settingsItem, fixture.Dock.GetItems());
    }

    [Fact]
    public void ShutdownUnsubscribesFromItemChanges()
    {
        using var fixture = new Fixture();
        fixture.Settings.Shutdown();

        fixture.Dock.AddItem(new DockSeparatorItemModel());

        Assert.Equal(3, fixture.Settings.ItemEntries.Count);
    }

    [Fact]
    public void RetargetingAProgramClearsShortcutLaunchDetailsAndRefreshesSettings()
    {
        using var fixture = new Fixture();
        var program = (DockProgramItemModel)fixture.Dock.GetItems()[0];
        program.LaunchPath = @"C:\tools\Update.exe";
        program.Arguments = "--profile-directory=\"Profile 2\"";
        program.WorkingDirectory = @"C:\working folder";
        fixture.Settings.SelectedItemIndex = 0;
        var dockVm = new MainWindowViewModel(fixture.Services);
        var itemVm = new DockItemViewModel(program, program.Label, dockVm.LaunchCommand);
        try
        {
            dockVm.RetargetItem(itemVm, @"C:\tools\Replacement.exe");

            Assert.Equal(@"C:\tools\Replacement.exe", program.ExecutablePath);
            Assert.Equal("", program.Arguments);
            Assert.Null(program.WorkingDirectory);
            Assert.Null(program.LaunchPath);
            Assert.Equal(program.ExecutablePath, fixture.Settings.ItemEntries[0].Detail);
            Assert.Equal(0, fixture.Settings.SelectedItemIndex);
        }
        finally { dockVm.Shutdown(); }
    }

    private sealed class Fixture : IDockRepository, IIconGateway, IDisposable
    {
        private readonly DockModel _model = new();
        private readonly string _themesDirectory = Path.Combine(Path.GetTempPath(), "CedroSettingsTest_" + Guid.NewGuid().ToString("N"));
        public DockService Dock { get; }
        public SettingsViewModel Settings { get; }
        public AppServices Services { get; }

        public Fixture()
        {
            foreach (string label in new[] { "A", "B", "C" })
                _model.AddItem(new DockProgramItemModel(label, $@"C:\tools\{label}.exe"));
            Dock = new DockService(this);
            // These services are not used by the item-list operations under test.
            Services = new AppServices(Dock, new DockAppearanceService(Dock),
                new DockPositioningService(Dock), null!, null!, this, new LocalizationService(Dock),
                new ThemeService(Dock, new FileSystemThemeRepository(_themesDirectory)));
            Settings = new SettingsViewModel(Services, () => { }, _ => { });
            Settings.Initialize();
        }

        public DockModel Load() => _model;
        public void Save(DockModel model) { }
        public string? ResolveProgramIcon(string path) => null;
        public string? ResolveFolderIcon(string path) => null;
        public void CacheProgramIcon(string path) { }
        public void CacheFolderIcon(string path) { }
        public void Dispose()
        {
            Settings.Shutdown();
            if (Directory.Exists(_themesDirectory)) Directory.Delete(_themesDirectory, recursive: true);
        }
    }
}
