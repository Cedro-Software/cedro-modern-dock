namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Persistence;

/// <summary>Direct port of JsonDockRepositoryTest.java</summary>
public class JsonDockRepositoryTest
{
    private string _tempDir = null!;

    public JsonDockRepositoryTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CedroRepoTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SavesAndLoadsFolderItems()
    {
        string configPath = Path.Combine(_tempDir, "config.json");
        var repository = new JsonDockRepository(configPath);

        var model = new DockModel();
        model.AddItem(new DockFolderItemModel("Projects", @"C:\Users\Arthur Rodrigues\Projects"));

        repository.Save(model);
        DockModel loadedModel = repository.Load();

        Assert.Single(loadedModel.Items);

        DockItem loadedItem = loadedModel.Items[0];
        var folderItem = Assert.IsType<DockFolderItemModel>(loadedItem);
        Assert.Equal(DockItemType.FOLDER, folderItem.Type);
        Assert.Equal("Projects", folderItem.Label);
        Assert.Equal(@"C:\Users\Arthur Rodrigues\Projects", folderItem.FolderPath);
        CleanupTempDir();
    }

    [Fact]
    public void SavesAndLoadsSelectedLanguage()
    {
        string configPath = Path.Combine(_tempDir, "config.json");
        var repository = new JsonDockRepository(configPath);

        var model = new DockModel();
        model.Language = SupportedLanguage.PT_BR;

        repository.Save(model);
        DockModel loadedModel = repository.Load();

        Assert.Equal(SupportedLanguage.PT_BR, loadedModel.Language);
        CleanupTempDir();
    }

    [Fact]
    public void SavesAndLoadsDockPosition()
    {
        string configPath = Path.Combine(_tempDir, "config.json");
        var repository = new JsonDockRepository(configPath);

        var model = new DockModel();
        model.SetDockPosition(718.5, 28.2);

        repository.Save(model);
        DockModel loadedModel = repository.Load();

        Assert.Equal(718.5, loadedModel.DockPositionX);
        Assert.Equal(28.2, loadedModel.DockPositionY);
        CleanupTempDir();
    }

    [Fact]
    public void SavesAndLoadsDockPadding()
    {
        string configPath = Path.Combine(_tempDir, "config.json");
        var repository = new JsonDockRepository(configPath);

        var model = new DockModel();
        model.DockPadding = 4;

        repository.Save(model);
        DockModel loadedModel = repository.Load();

        Assert.Equal(4, loadedModel.DockPadding);
        CleanupTempDir();
    }

    [Fact]
    public void FirstRunFlagsDefaultCreationOnlyOnce()
    {
        string configPath = Path.Combine(_tempDir, "config.json");
        var repository = new JsonDockRepository(configPath);

        repository.Load();
        Assert.True(repository.WasDefaultCreated);

        var repository2 = new JsonDockRepository(configPath);
        repository2.Load();
        Assert.False(repository2.WasDefaultCreated);
        CleanupTempDir();
    }

    [Fact]
    public void LoadsExistingJavaConfigJsonWithoutDataLoss()
    {
        // Uses the real config.json from the original Java project root to prove
        // format compatibility — existing users' configs must load as-is.
        string configPath = "config_compat_test.json";
        if (!File.Exists(configPath))
        {
            // Skip if the file wasn't copied (e.g. different working dir).
            return;
        }

        var repository = new JsonDockRepository(configPath);
        DockModel loadedModel = repository.Load();

        // The sample config has 13 items (settings + 2 windows modules + 10 programs).
        Assert.True(loadedModel.Items.Count >= 10, $"Expected >=10 items, got {loadedModel.Items.Count}");

        // Verify the settings item is deserialized to the right type.
        Assert.Contains(loadedModel.Items, i => i is DockSettingsItemModel);

        // Verify a windows module item preserved its module field.
        var recycleBin = loadedModel.Items.OfType<DockWindowsModuleItemModel>()
            .FirstOrDefault(m => m.Module == "trash");
        Assert.NotNull(recycleBin);

        // Verify a program item preserved its executable path.
        var chrome = loadedModel.Items.OfType<DockProgramItemModel>()
            .FirstOrDefault(p => p.Label == "chrome");
        Assert.NotNull(chrome);
        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe", chrome!.ExecutablePath);

        // Verify appearance settings survived the round-trip.
        Assert.Equal(27, loadedModel.IconsSize);
        Assert.Equal(0.62, loadedModel.DockTransparency, precision: 2);
    }

    private void CleanupTempDir()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }
}
