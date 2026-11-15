namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Infrastructure.Windows.Adapters;

/// <summary>Direct port of DefaultProgramLauncherTest.java</summary>
public class WindowsProgramLauncherTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesShortcutCommandLineAndWorkingDirectoryIncludingForElevation(bool elevated)
    {
        const string arguments = "--profile-directory=\"Profile 2\" \"C:\\some folder\\file.txt\" \"\"";
        var command = WindowsProgramLauncher.ResolveLaunchCommand(@"C:\tools\Editor.exe", arguments, @"C:\working folder");

        var startInfo = WindowsProgramLauncher.CreateStartInfo(command, elevated);

        Assert.Equal(@"C:\tools\Editor.exe", startInfo.FileName);
        Assert.Equal(arguments, startInfo.Arguments);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Equal(@"C:\working folder", startInfo.WorkingDirectory);
        Assert.Equal(elevated, startInfo.UseShellExecute);
        Assert.Equal(elevated ? "runas" : "", startInfo.Verb);
    }

    private string _tempDir = null!;

    public WindowsProgramLauncherTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CedroLauncherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void LaunchesRegularProgramsDirectly()
    {
        string executable = Path.Combine(_tempDir, "Editor.exe");
        File.WriteAllText(executable, "");

        var command = WindowsProgramLauncher.ResolveLaunchCommand(executable);

        Assert.Equal(executable, command.ExecutablePath);
        Assert.Empty(command.Arguments);
        CleanupTempDir();
    }

    [Theory]
    [InlineData("")]
    [InlineData("--some-option=\"a value\"")]
    public void LaunchesDiscordThroughUpdateExecutable(string arguments)
    {
        string installDir = Path.Combine(_tempDir, "Discord");
        Directory.CreateDirectory(installDir);
        string updateExecutable = Path.Combine(installDir, "Update.exe");
        File.WriteAllText(updateExecutable, "");
        string versionDirectory = Path.Combine(installDir, "app-1.0.9230");
        Directory.CreateDirectory(versionDirectory);
        string discordExecutable = Path.Combine(versionDirectory, "Discord.exe");
        File.WriteAllText(discordExecutable, "");

        var command = WindowsProgramLauncher.ResolveLaunchCommand(discordExecutable, arguments, versionDirectory);

        Assert.Equal(updateExecutable, command.ExecutablePath);
        var expected = string.IsNullOrEmpty(arguments)
            ? new[] { "--processStart", "Discord.exe" }
            : new[] { "--processStart", "Discord.exe", "--process-start-args", arguments };
        Assert.Equal(expected, command.Arguments);
        Assert.Equal(versionDirectory, command.WorkingDirectory);
        Assert.Empty(command.RawArguments);
        CleanupTempDir();
    }

    [Fact]
    public void LaunchingMissingExecutableDoesNotThrow()
    {
        var launcher = new WindowsProgramLauncher();
        string missing = Path.Combine(_tempDir, "Missing.exe");

        bool launched = launcher.Launch(missing, "Missing");

        Assert.False(launched);
        CleanupTempDir();
    }

    private void CleanupTempDir()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
