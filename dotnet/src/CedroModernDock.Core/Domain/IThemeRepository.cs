namespace CedroModernDock.Core.Domain;

using CedroModernDock.Core.Themes;

/// <summary>Storage of user-installed (third-party) themes.</summary>
public interface IThemeRepository
{
    /// <summary>Folder holding one sub-folder per installed theme.</summary>
    string ThemesDirectory { get; }

    /// <summary>Loads every valid installed theme. Broken ones are reported in <paramref name="errors"/>.</summary>
    IReadOnlyList<LoadedTheme> LoadAll(out IReadOnlyList<string> errors);

    /// <summary>
    /// Installs a theme from a .zip / .cedrotheme archive or from a folder
    /// containing theme.json. An installed theme with the same id is replaced.
    /// Throws <see cref="ThemeFormatException"/> when the package is invalid.
    /// </summary>
    LoadedTheme Install(string packagePath, Func<string, bool>? isReservedId = null);

    /// <summary>Removes an installed theme. Returns false when it does not exist.</summary>
    bool Delete(string themeId);

    /// <summary>Writes <paramref name="manifest"/> as a new installed theme and returns it.</summary>
    LoadedTheme Save(ThemeManifest manifest);

    /// <summary>Packs an installed theme into a shareable .cedrotheme archive.</summary>
    void Export(string themeId, string destinationFile);
}
