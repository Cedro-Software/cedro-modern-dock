namespace CedroModernDock.Infrastructure.Windows.Persistence;

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Themes;

/// <summary>
/// Third-party themes stored as folders under %APPDATA%\CedroModernDock\Themes,
/// one folder per theme, each with a theme.json and optional images.
/// Packages (.zip / .cedrotheme) are validated before anything is written:
/// only data files are extracted, paths cannot escape the theme folder and
/// the archive size is bounded.
/// </summary>
public sealed class FileSystemThemeRepository : IThemeRepository
{
    public const string PackageExtension = ".cedrotheme";

    private const int MaxEntries = 200;
    private const long MaxTotalBytes = 25L * 1024 * 1024;
    private const long MaxManifestBytes = 256 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif", ".txt", ".md"
    };

    public string ThemesDirectory { get; }

    public FileSystemThemeRepository() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CedroModernDock", "Themes")) { }

    public FileSystemThemeRepository(string themesDirectory)
    {
        ThemesDirectory = themesDirectory;
    }

    public IReadOnlyList<LoadedTheme> LoadAll(out IReadOnlyList<string> errors)
    {
        var themes = new List<LoadedTheme>();
        var errorList = new List<string>();
        errors = errorList;
        if (!Directory.Exists(ThemesDirectory)) return themes;

        foreach (var dir in Directory.EnumerateDirectories(ThemesDirectory))
        {
            // Hidden work folders (.staging-*, .previous-*) are never themes.
            if (Path.GetFileName(dir).StartsWith('.')) continue;
            string manifestPath = Path.Combine(dir, ThemeParser.ManifestFileName);
            if (!File.Exists(manifestPath)) continue;
            try
            {
                if (new FileInfo(manifestPath).Length > MaxManifestBytes)
                    throw new ThemeFormatException("theme.json is too large.");
                themes.Add(ThemeParser.Parse(File.ReadAllText(manifestPath), dir, isBuiltIn: false));
            }
            catch (Exception e) when (e is ThemeFormatException or IOException or UnauthorizedAccessException)
            {
                errorList.Add($"{Path.GetFileName(dir)}: {e.Message}");
            }
        }
        return themes;
    }

    public LoadedTheme Install(string packagePath, Func<string, bool>? isReservedId = null)
    {
        if (Directory.Exists(packagePath))
            return InstallFromFolder(packagePath, isReservedId);
        if (File.Exists(packagePath) && Path.GetFileName(packagePath)
                .Equals(ThemeParser.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            return InstallFromFolder(Path.GetDirectoryName(packagePath)!, isReservedId);
        if (File.Exists(packagePath))
            return InstallFromArchive(packagePath, isReservedId);
        throw new ThemeFormatException($"'{packagePath}' does not exist.");
    }

    private LoadedTheme InstallFromFolder(string sourceDir, Func<string, bool>? isReservedId)
    {
        string manifestPath = Path.Combine(sourceDir, ThemeParser.ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new ThemeFormatException("No theme.json found in this folder.");
        if (new FileInfo(manifestPath).Length > MaxManifestBytes)
            throw new ThemeFormatException("theme.json is too large.");
        var probe = ThemeParser.Parse(File.ReadAllText(manifestPath), sourceDir, isBuiltIn: false);
        CheckReserved(probe.Id, isReservedId);

        string target = TargetFor(probe.Id);
        if (Path.GetFullPath(sourceDir).TrimEnd('\\') == Path.GetFullPath(target).TrimEnd('\\'))
            return probe; // already in place

        var files = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f)))
            .ToList();
        if (files.Count > MaxEntries || files.Sum(f => new FileInfo(f).Length) > MaxTotalBytes)
            throw new ThemeFormatException("The theme folder is too large.");

        string staging = StagingDir();
        try
        {
            foreach (var file in files)
            {
                string rel = Path.GetRelativePath(sourceDir, file);
                string dest = Path.Combine(staging, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest);
            }
            return Commit(staging, target);
        }
        finally
        {
            TryDelete(staging);
        }
    }

    private LoadedTheme InstallFromArchive(string archivePath, Func<string, bool>? isReservedId)
    {
        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException e)
        {
            throw new ThemeFormatException("The file is not a valid theme package (zip).", e);
        }

        using (zip)
        {
            if (zip.Entries.Count > MaxEntries)
                throw new ThemeFormatException("The theme package contains too many files.");
            if (zip.Entries.Sum(e => e.Length) > MaxTotalBytes)
                throw new ThemeFormatException("The theme package is too large.");

            // theme.json at the root, or inside a single top-level folder.
            var manifestEntry = zip.Entries
                .Where(e => e.Name.Equals(ThemeParser.ManifestFileName, StringComparison.OrdinalIgnoreCase))
                .Select(e => (Entry: e, Depth: e.FullName.Replace('\\', '/').Count(c => c == '/')))
                .Where(x => x.Depth <= 1)
                .OrderBy(x => x.Depth)
                .Select(x => x.Entry)
                .FirstOrDefault()
                ?? throw new ThemeFormatException("No theme.json found in the package.");
            if (manifestEntry.Length > MaxManifestBytes)
                throw new ThemeFormatException("theme.json is too large.");

            string prefix = manifestEntry.FullName.Replace('\\', '/');
            prefix = prefix[..^manifestEntry.Name.Length];

            string json;
            using (var reader = new StreamReader(manifestEntry.Open()))
                json = reader.ReadToEnd();
            var probe = ThemeParser.Parse(json, null, isBuiltIn: false);
            CheckReserved(probe.Id, isReservedId);

            string staging = StagingDir();
            try
            {
                string stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                long budget = MaxTotalBytes; // enforced on the bytes actually written
                foreach (var entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.EndsWith('/')) continue;
                    string rel = name[prefix.Length..];
                    if (!ThemeParser.IsSafeRelativePath(rel)) continue;
                    if (!AllowedExtensions.Contains(Path.GetExtension(rel))) continue;
                    string dest = Path.GetFullPath(Path.Combine(staging, rel));
                    if (!dest.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    budget -= CopyBounded(entry, dest, budget);
                }
                return Commit(staging, TargetFor(probe.Id));
            }
            finally
            {
                TryDelete(staging);
            }
        }
    }

    /// <summary>Validates the staged copy, then swaps it into place.</summary>
    private LoadedTheme Commit(string staging, string target)
    {
        ThemeParser.Parse(File.ReadAllText(Path.Combine(staging, ThemeParser.ManifestFileName)), staging, false);
        Directory.CreateDirectory(ThemesDirectory);
        // Keep the previous version aside until the new one is in place.
        string? previous = null;
        if (Directory.Exists(target))
        {
            previous = Path.Combine(ThemesDirectory, ".previous-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.Move(target, previous);
        }
        try
        {
            Directory.Move(staging, target);
        }
        catch
        {
            if (previous != null && !Directory.Exists(target)) Directory.Move(previous, target);
            throw;
        }
        if (previous != null) TryDelete(previous);
        return ThemeParser.Parse(File.ReadAllText(Path.Combine(target, ThemeParser.ManifestFileName)), target, false);
    }

    public bool Delete(string themeId)
    {
        if (!ThemeParser.IsValidId(themeId)) return false;
        string target = TargetFor(themeId);
        if (!Directory.Exists(target)) return false;
        Directory.Delete(target, recursive: true);
        return true;
    }

    public LoadedTheme Save(ThemeManifest manifest)
    {
        if (!ThemeParser.IsValidId(manifest.Id))
            throw new ThemeFormatException($"Invalid theme id '{manifest.Id}'.");
        string target = TargetFor(manifest.Id);
        Directory.CreateDirectory(target);
        string json = JsonSerializer.Serialize(manifest, ThemeParser.WriteOptions);
        File.WriteAllText(Path.Combine(target, ThemeParser.ManifestFileName), json);
        return ThemeParser.Parse(json, target, isBuiltIn: false);
    }

    public void Export(string themeId, string destinationFile)
    {
        string source = TargetFor(themeId);
        if (!Directory.Exists(source))
            throw new ThemeFormatException($"Theme '{themeId}' is not installed.");
        if (File.Exists(destinationFile)) File.Delete(destinationFile);
        ZipFile.CreateFromDirectory(source, destinationFile, CompressionLevel.Optimal, includeBaseDirectory: true);
    }

    private string TargetFor(string themeId)
    {
        string target = Path.GetFullPath(Path.Combine(ThemesDirectory, themeId));
        string root = Path.GetFullPath(ThemesDirectory).TrimEnd('\\') + "\\";
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ThemeFormatException($"Invalid theme id '{themeId}'.");
        return target;
    }

    private static void CheckReserved(string id, Func<string, bool>? isReservedId)
    {
        if (isReservedId?.Invoke(id) == true)
            throw new ThemeFormatException($"The id '{id}' is reserved by a built-in theme.");
    }

    private string StagingDir()
    {
        string dir = Path.Combine(ThemesDirectory, ".staging-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Extracts an entry, refusing to write more than <paramref name="budget"/> bytes.</summary>
    private static long CopyBounded(ZipArchiveEntry entry, string dest, long budget)
    {
        using var input = entry.Open();
        using var output = File.Create(dest);
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            written += read;
            if (written > budget)
                throw new ThemeFormatException("The theme package is too large.");
            output.Write(buffer, 0, read);
        }
        return written;
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
