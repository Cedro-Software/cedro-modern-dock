namespace CedroModernDock.Tests;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>Direct port of DockItemActionServiceTest.java</summary>
public class DockItemActionServiceTest
{
    [Fact]
    public void ExecutesTheOriginalShortcutTargetWithItsArgumentsAndWorkingDirectory()
    {
        var launcher = new RecordingProgramLauncher();
        var service = new DockItemActionService(launcher, null!, null!);
        var item = new DockProgramItemModel("Editor", @"C:\tools\Editor.exe")
        {
            LaunchPath = @"C:\tools\Update.exe",
            Arguments = "--processStart Editor.exe --process-start-args=\"a file.txt\"",
            WorkingDirectory = @"C:\working folder"
        };

        Assert.True(service.Execute(item, () => { }));

        Assert.Equal(item.LaunchPath, launcher.Path);
        Assert.Equal(item.Arguments, launcher.Arguments);
        Assert.Equal(item.WorkingDirectory, launcher.WorkingDirectory);
    }

    private sealed class RecordingProgramLauncher : IProgramLauncher
    {
        public string? Path { get; private set; }
        public string? Arguments { get; private set; }
        public string? WorkingDirectory { get; private set; }

        public bool Launch(string executablePath, string label, string arguments = "", string? workingDirectory = null)
        {
            Path = executablePath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            return true;
        }
    }

    [Fact]
    public void ExecutesProgramItemsThroughProgramLauncher()
    {
        var capture = new InvocationCapture();
        var service = new DockItemActionService(
            LambdaProgramLauncher(capture),
            LambdaFolderLauncher(capture),
            LambdaWindowsModuleLauncher(capture)
        );

        service.Execute(new DockProgramItemModel("Editor", @"C:\tools\editor.exe"), capture.OpenSettings);

        Assert.Equal("program", capture.Kind);
        Assert.Equal(@"C:\tools\editor.exe", capture.Value);
        Assert.Equal("Editor", capture.Label);
    }

    [Fact]
    public void ExecutesFolderItemsThroughFolderLauncher()
    {
        var capture = new InvocationCapture();
        var service = new DockItemActionService(
            LambdaProgramLauncher(capture),
            LambdaFolderLauncher(capture),
            LambdaWindowsModuleLauncher(capture)
        );

        service.Execute(new DockFolderItemModel("Projects", @"C:\Users\Arthur Rodrigues\Projects"), capture.OpenSettings);

        Assert.Equal("folder", capture.Kind);
        Assert.Equal(@"C:\Users\Arthur Rodrigues\Projects", capture.Value);
        Assert.Equal("Projects", capture.Label);
    }

    [Fact]
    public void ExecutesWindowsModuleItemsThroughWindowsModuleLauncher()
    {
        var capture = new InvocationCapture();
        var service = new DockItemActionService(
            LambdaProgramLauncher(capture),
            LambdaFolderLauncher(capture),
            LambdaWindowsModuleLauncher(capture)
        );

        service.Execute(new DockWindowsModuleItemModel("Control Panel", "ctrlpnl"), capture.OpenSettings);

        Assert.Equal("module", capture.Kind);
        Assert.Equal("ctrlpnl", capture.Value);
        Assert.Equal("Control Panel", capture.Label);
    }

    [Fact]
    public void ExecutesStartMenuModuleThroughWindowsModuleLauncher()
    {
        var capture = new InvocationCapture();
        var service = new DockItemActionService(
            LambdaProgramLauncher(capture),
            LambdaFolderLauncher(capture),
            LambdaWindowsModuleLauncher(capture)
        );

        service.Execute(new DockWindowsModuleItemModel("Start Menu", "start"), capture.OpenSettings);

        Assert.Equal("module", capture.Kind);
        Assert.Equal("start", capture.Value);
        Assert.Equal("Start Menu", capture.Label);
    }

    [Fact]
    public void ExecutesSettingsItemsThroughSettingsAction()
    {
        var capture = new InvocationCapture();
        var service = new DockItemActionService(
            LambdaProgramLauncher(capture),
            LambdaFolderLauncher(capture),
            LambdaWindowsModuleLauncher(capture)
        );

        service.Execute(new DockSettingsItemModel(), capture.OpenSettings);

        Assert.Equal("settings", capture.Kind);
    }

    // Small adapter wrappers so lambdas can be passed as interface parameters
    // (C# interfaces are not delegates, unlike Java functional interfaces).
    private static IProgramLauncher LambdaProgramLauncher(InvocationCapture c) =>
        new ProgramLauncherImpl((path, label) => c.Record("program", path, label));
    private static IFolderLauncher LambdaFolderLauncher(InvocationCapture c) =>
        new FolderLauncherImpl((path, label) => c.Record("folder", path, label));
    private static IWindowsModuleLauncher LambdaWindowsModuleLauncher(InvocationCapture c) =>
        new WindowsModuleLauncherImpl((module, label) => c.Record("module", module, label));

    private sealed class ProgramLauncherImpl(Action<string, string> fn) : IProgramLauncher
    {
        public bool Launch(string executablePath, string label, string arguments = "", string? workingDirectory = null)
        {
            fn(executablePath, label);
            return true;
        }
    }
    private sealed class FolderLauncherImpl(Action<string, string> fn) : IFolderLauncher
    {
        public void Launch(string folderPath, string label) => fn(folderPath, label);
    }
    private sealed class WindowsModuleLauncherImpl(Action<string, string> fn) : IWindowsModuleLauncher
    {
        public void Launch(string module, string label) => fn(module, label);
    }

    private sealed class InvocationCapture
    {
        public string Kind { get; private set; } = "";
        public string Value { get; private set; } = "";
        public string Label { get; private set; } = "";

        public void Record(string kind, string value, string label)
        {
            Kind = kind;
            Value = value;
            Label = label;
        }

        public void OpenSettings() => Kind = "settings";
    }
}
