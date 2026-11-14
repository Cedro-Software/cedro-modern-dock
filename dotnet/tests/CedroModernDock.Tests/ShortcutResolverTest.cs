namespace CedroModernDock.Tests;

using System.IO;
using CedroModernDock.Infrastructure.Windows.Native;

public class ShortcutResolverTest
{
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
