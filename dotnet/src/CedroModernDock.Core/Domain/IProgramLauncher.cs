namespace CedroModernDock.Core.Domain;

/// <summary>Port for launching executable programs (.exe).</summary>
public interface IProgramLauncher
{
    /// <summary>
    /// Launches the program. Returns true when the launch was attempted
    /// successfully, false when the executable could not be launched
    /// (e.g. it no longer exists).
    /// </summary>
    bool Launch(string executablePath, string label, string arguments = "", string? workingDirectory = null);
}
