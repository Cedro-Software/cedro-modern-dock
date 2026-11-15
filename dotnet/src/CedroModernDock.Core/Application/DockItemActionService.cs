namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>Direct port of DockItemActionService.</summary>
public class DockItemActionService
{
    private readonly IFolderLauncher _folderLauncher;
    private readonly IProgramLauncher _programLauncher;
    private readonly IWindowsModuleLauncher _windowsModuleLauncher;

    public DockItemActionService(
        IProgramLauncher programLauncher,
        IFolderLauncher folderLauncher,
        IWindowsModuleLauncher windowsModuleLauncher)
    {
        _programLauncher = programLauncher;
        _folderLauncher = folderLauncher;
        _windowsModuleLauncher = windowsModuleLauncher;
    }

    /// <summary>
    /// Executes the dock item action. Returns true when the item was handled
    /// (or launched), false when a program item could not be launched.
    /// </summary>
    public bool Execute(DockItem item, Action openSettingsAction)
    {
        if (item is DockProgramItemModel programItem)
        {
            return _programLauncher.Launch(programItem.LaunchPath ?? programItem.ExecutablePath,
                programItem.Label, programItem.Arguments, programItem.WorkingDirectory);
        }

        if (item is DockFolderItemModel folderItem)
        {
            _folderLauncher.Launch(folderItem.FolderPath, folderItem.Label);
            return true;
        }

        if (item is DockWindowsModuleItemModel windowsModuleItem)
        {
            _windowsModuleLauncher.Launch(windowsModuleItem.Module, windowsModuleItem.Label);
            return true;
        }

        if (item is DockSettingsItemModel)
        {
            openSettingsAction();
        }

        return true;
    }
}
