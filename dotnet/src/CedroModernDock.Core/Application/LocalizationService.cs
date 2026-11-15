namespace CedroModernDock.Core.Application;

using System.Text;
using CedroModernDock.Core.Models;

/// <summary>
/// Direct port of LocalizationService. Loads language bundles from embedded
/// .properties files (Java ResourceBundle format) and provides localized text
/// with {0}/{1} style format arguments (equivalent to Java MessageFormat).
/// </summary>
public class LocalizationService
{
    private readonly DockService _dockService;
    private readonly List<Action> _listeners = new();
    private IReadOnlyDictionary<string, string> _bundle;

    public LocalizationService(DockService dockService)
    {
        _dockService = dockService;
        _bundle = LoadBundle(GetCurrentLanguage());
    }

    public SupportedLanguage GetCurrentLanguage()
    {
        var language = _dockService.GetDock().Language;
        return language;
    }

    public void SetLanguage(SupportedLanguage language)
    {
        if (language == GetCurrentLanguage())
            return;

        _dockService.GetDock().Language = language;
        _dockService.SaveChanges();
        _bundle = LoadBundle(language);
        NotifyListeners();
    }

    /// <summary>
    /// Like <see cref="Text"/>, but returns <paramref name="fallback"/> when the
    /// key is missing from the active language bundle (keys added after the
    /// original translations were made).
    /// </summary>
    public string TextOr(string key, string fallback)
        => _bundle.TryGetValue(key, out var value) ? value : fallback;

    public string Text(string key, params object[] arguments)
    {
        string pattern = _bundle.TryGetValue(key, out var value) ? value : key;
        if (arguments == null || arguments.Length == 0)
            return pattern;
        return string.Format(pattern, arguments);
    }

    public string LanguageDisplayName(SupportedLanguage language) => language.NativeDisplayName();

    public string DockItemLabel(DockItem item)
    {
        if (item == null)
            return "";

        if (item is DockSettingsItemModel)
            return Text("dockItem.settings");

        if (item is DockSeparatorItemModel)
            return TextOr("dockItem.separator", "Separator");

        if (item is DockWindowsModuleItemModel windowsModuleItem)
        {
            return windowsModuleItem.Module switch
            {
                "start" => Text("windowsModule.startMenu"),
                "mypc" => Text("windowsModule.myComputer"),
                "trash" => Text("windowsModule.recycleBin"),
                "ctrlpnl" => Text("windowsModule.controlPanel"),
                "pconfig" => Text("windowsModule.settings"),
                _ => item.Label
            };
        }

        return item.Label;
    }

    public void AddListener(Action listener) => _listeners.Add(listener);

    public void RemoveListener(Action listener) => _listeners.Remove(listener);

    /// <summary>
    /// Static helper for bootstrap contexts (before services are wired up),
    /// e.g. the single-instance dialog. Direct port of bootstrapText.
    /// </summary>
    public static string BootstrapText(SupportedLanguage language, string key, params object[] arguments)
    {
        var bundle = LoadBundle(language);
        string pattern = bundle.TryGetValue(key, out var value) ? value : key;
        if (arguments == null || arguments.Length == 0)
            return pattern;
        return string.Format(pattern, arguments);
    }

    private void NotifyListeners()
    {
        foreach (var listener in _listeners)
            listener();
    }

    private static IReadOnlyDictionary<string, string> LoadBundle(SupportedLanguage language) =>
        PropertiesBundle.Load(language.BundleSuffix());
}
