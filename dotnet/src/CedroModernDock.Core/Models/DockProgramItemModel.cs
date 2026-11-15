namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Direct port of DockProgramItemModel. The serialized "path" property
/// holds the executable path; the ExecutablePath accessor is a non-serialized
/// convenience alias (matching the Java @JsonIgnore getExecutablePath).
/// </summary>
public class DockProgramItemModel : DockItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";

    public string Arguments { get; set; } = "";
    public string? WorkingDirectory { get; set; }

    /// <summary>Shortcut launch target when it differs from the executable tracked by the dock.</summary>
    public string? LaunchPath { get; set; }

    [JsonIgnore]
    public string ExecutablePath => Path;

    [JsonIgnore]
    public DockItemType Type => DockItemType.PROGRAM;

    public DockProgramItemModel() { }

    public DockProgramItemModel(string label, string exePath)
    {
        Label = label;
        Path = exePath;
    }
}
