namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Infrastructure.Windows.Native;

public class ShortcutResolverTest
{
    [Fact]
    public void ResolvesShortcutArgumentsAndWorkingDirectoryWithoutChangingQuoting()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CedroShortcutTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string exe = Path.Combine(dir, "test tool.exe");
            string link = Path.Combine(dir, "profile.lnk");
            const string arguments = "--profile-directory=\"Profile 2\" \"a file.txt\" \"\"";
            File.WriteAllText(exe, "");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(link);
                try
                {
                    shortcut.TargetPath = exe;
                    shortcut.Arguments = arguments;
                    shortcut.WorkingDirectory = dir;
                    shortcut.Save();
                }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); }
            }
            finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }

            var resolved = Assert.IsType<ShortcutResolver.ResolvedShortcut>(ShortcutResolver.Resolve(link));

            Assert.Equal(exe, resolved.ExecutablePath);
            Assert.Equal(arguments, resolved.Arguments);
            Assert.Equal(dir, resolved.WorkingDirectory);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void ReturnsTheExecutableForAnExeFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CedroShortcutTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string exe = Path.Combine(dir, "tool.exe");
            File.WriteAllText(exe, "");
            Assert.Equal(exe, ShortcutResolver.ResolveExecutable(exe));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReturnsNullForAFileThatIsNotAProgram()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CedroShortcutTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string document = Path.Combine(dir, "notes.txt");
            File.WriteAllText(document, "");
            Assert.Null(ShortcutResolver.ResolveExecutable(document));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReturnsNullForAMissingFile()
    {
        Assert.Null(ShortcutResolver.ResolveExecutable(@"C:\does\not\exist\tool.exe"));
    }
}
