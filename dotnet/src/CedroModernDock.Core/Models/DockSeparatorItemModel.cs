namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Visual separator placed between pinned dock items. It has no target and
/// performs no action when clicked.
/// </summary>
public class DockSeparatorItemModel : DockItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";

    [JsonIgnore]
    public DockItemType Type => DockItemType.SEPARATOR;
}
