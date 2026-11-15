using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>Resolves dropped programs and preserves shortcut launch details.</summary>
public static class ShortcutResolver
{
    public sealed record ResolvedShortcut(string ExecutablePath, string Arguments = "", string? WorkingDirectory = null);

    /// <summary>
    /// Returns the .exe path for an .exe file or a .lnk shortcut pointing to an
    /// .exe; null for anything else (documents, .url links, broken shortcuts).
    /// </summary>
    public static string? ResolveExecutable(string path)
        => Resolve(path)?.ExecutablePath;

    public static ResolvedShortcut? Resolve(string path)
    {
        if (!File.Exists(path)) return null;
        string ext = Path.GetExtension(path);

        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return new ResolvedShortcut(path);

        if (!ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        // MSI "advertised" shortcuts (e.g. VLC, Office) point to an icon in
        // C:\Windows\Installer; the real target comes from Windows Installer.
        string? msiTarget = ResolveMsiShortcut(path);
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return msiTarget == null ? null : new ResolvedShortcut(msiTarget);
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(path);
                try
                {
                    string target = msiTarget ?? Environment.ExpandEnvironmentVariables((string)shortcut.TargetPath ?? "");
                    string arguments = (string)shortcut.Arguments ?? "";
                    string directory = Environment.ExpandEnvironmentVariables((string)shortcut.WorkingDirectory ?? "");
                    return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target)
                        ? new ResolvedShortcut(target, arguments, string.IsNullOrWhiteSpace(directory) ? null : directory)
                        : null;
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch
        {
            return msiTarget == null ? null : new ResolvedShortcut(msiTarget);
        }
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiGetShortcutTarget(string szShortcutPath,
        StringBuilder szProductCode, StringBuilder? szFeatureId, StringBuilder szComponentCode);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern int MsiGetComponentPath(string szProduct, string szComponent,
        StringBuilder? lpPathBuf, ref uint pcchBuf);

    private static string? ResolveMsiShortcut(string lnkPath)
    {
        try
        {
            var product = new StringBuilder(39);
            var feature = new StringBuilder(39);
            var component = new StringBuilder(39);
            if (MsiGetShortcutTarget(lnkPath, product, feature, component) != 0)
                return null; // not an advertised shortcut

            uint len = 1024;
            var buffer = new StringBuilder((int)len);
            int state = MsiGetComponentPath(product.ToString(), component.ToString(), buffer, ref len);
            const int INSTALLSTATE_LOCAL = 3;
            if (state != INSTALLSTATE_LOCAL) return null;

            string target = buffer.ToString();
            return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target)
                ? target
                : null;
        }
        catch
        {
            return null;
        }
    }
}
