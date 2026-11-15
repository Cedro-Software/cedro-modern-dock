using Avalonia.Data.Converters;

namespace CedroModernDock.ViewModels;

public static class Converters
{
    /// <summary>true -> 1, false -> 0 (fades an element while keeping its layout slot).</summary>
    public static readonly IValueConverter BoolToOpacity =
        new FuncValueConverter<bool, double>(b => b ? 1.0 : 0.0);
}
