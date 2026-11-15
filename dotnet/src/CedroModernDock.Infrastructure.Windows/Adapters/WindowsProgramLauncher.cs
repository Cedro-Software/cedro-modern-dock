namespace CedroModernDock.Infrastructure.Windows.Adapters;

using System.Diagnostics;
using CedroModernDock.Core.Domain;

/// <summary>
/// Direct port of DefaultProgramLauncher.java. Launches executables,
/// with special handling for Discord (Squirrel-installed apps) and
/// automatic elevation (UAC) when the standard launch fails with error=740.
/// </summary>
public class WindowsProgramLauncher : IProgramLauncher
{
    public bool Launch(string executablePath, string label, string arguments = "", string? workingDirectory = null)
    {
        Debug.WriteLine($"{label} Clicked");

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Debug.WriteLine($"Executable path not defined for: {label}");
            return false;
        }

        try
        {
            return ExecuteAndHandleElevation(executablePath, label, arguments, workingDirectory);
        }
        catch (Exception e)
        {
            // A failed launch must never take the dock down: elevation
            // (error 740) is handled inside; any other failure (missing
            // file, bad path, invalid exe) is logged and reported as a
            // failed launch.
            Debug.WriteLine($"Failed to open: {label}");
            Debug.WriteLine($"Path: {executablePath}");
            Debug.WriteLine($"Error: {e.Message}");
            return false;
        }
    }

    private bool ExecuteAndHandleElevation(string path, string label, string arguments, string? workingDirectory)
    {
        var launchCommand = ResolveLaunchCommand(path, arguments, workingDirectory);
        if (!File.Exists(launchCommand.ExecutablePath))
        {
            Debug.WriteLine($"Executable not found: {launchCommand.ExecutablePath}");
            return false;
        }

        try
        {
            Process.Start(CreateStartInfo(launchCommand));
            Debug.WriteLine($"Executing: {label}");
            return true;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // error=740 means elevation is required (ERROR_ELEVATION_REQUIRED)
            if (e.NativeErrorCode == 740)
            {
                Debug.WriteLine("Standard execution failed. Requesting elevation...");
                Process.Start(CreateStartInfo(launchCommand, elevated: true));
                Debug.WriteLine($"(Elevated) Executing: {label}");
                return true;
            }

            Debug.WriteLine("Error trying to execute program");
            return false;
        }
    }

    /// <summary>
    /// Resolves the actual launch command for an executable, handling
    /// Discord's Squirrel installer pattern (app runs via Update.exe --processStart).
    /// </summary>
    public static LaunchCommand ResolveLaunchCommand(string executablePath, string arguments = "", string? workingDirectory = null)
    {
        string normalizedPath = Path.GetFullPath(executablePath);
        if (IsDiscordExecutable(normalizedPath))
        {
            string? versionDirectory = Path.GetDirectoryName(normalizedPath);
            string? installDirectory = versionDirectory != null
                ? Path.GetDirectoryName(versionDirectory) : null;

            if (installDirectory != null)
            {
                string updateExecutable = Path.Combine(installDirectory, "Update.exe");
                if (File.Exists(updateExecutable))
                {
                    var updaterArguments = new List<string> { "--processStart", Path.GetFileName(normalizedPath) };
                    if (!string.IsNullOrEmpty(arguments))
                    {
                        updaterArguments.Add("--process-start-args");
                        updaterArguments.Add(arguments);
                    }
                    return new LaunchCommand(updateExecutable, updaterArguments.ToArray(), WorkingDirectory: workingDirectory);
                }
            }
        }

        return new LaunchCommand(normalizedPath, Array.Empty<string>(), arguments ?? "", workingDirectory);
    }

    private static bool IsDiscordExecutable(string executablePath)
    {
        string? fileName = Path.GetFileName(executablePath);
        string? versionDirectory = Path.GetDirectoryName(executablePath);
        string? installDirectory = versionDirectory != null
            ? Path.GetDirectoryName(versionDirectory) : null;

        if (fileName == null || versionDirectory == null || installDirectory == null)
            return false;

        return string.Equals(fileName, "Discord.exe", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(versionDirectory)?.StartsWith("app-", StringComparison.OrdinalIgnoreCase) == true
            && string.Equals(Path.GetFileName(installDirectory), "Discord", StringComparison.OrdinalIgnoreCase);
    }

    public static ProcessStartInfo CreateStartInfo(LaunchCommand command, bool elevated = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            UseShellExecute = elevated,
            Verb = elevated ? "runas" : "",
            WorkingDirectory = command.WorkingDirectory ?? ""
        };
        // Shortcut arguments are already a Windows command line; splitting them
        // would alter quoting, empty arguments and paths containing spaces.
        if (!string.IsNullOrEmpty(command.RawArguments))
            startInfo.Arguments = command.RawArguments;
        else
            foreach (var arg in command.Arguments)
                startInfo.ArgumentList.Add(arg);
        return startInfo;
    }

    /// <summary>Direct port of the Java LaunchCommand record.</summary>
    public sealed record LaunchCommand(string ExecutablePath, string[] Arguments,
        string RawArguments = "", string? WorkingDirectory = null);
}
