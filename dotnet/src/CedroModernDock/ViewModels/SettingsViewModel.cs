using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;

namespace CedroModernDock.ViewModels;

/// <summary>One row in the Settings dock-items list: display label + icon.</summary>
public sealed record DockItemListEntry(string Label, Bitmap? Icon);

/// <summary>Alignment combo-box entry: localized label + enum value.</summary>
public sealed record AnchorOption(string Label, DockVerticalAnchor Value);

/// <summary>Alignment combo-box entry: localized label + enum value.</summary>
public sealed record HorizontalAnchorOption(string Label, DockHorizontalAnchor Value);

/// <summary>ViewModel for the Settings window. Port of SettingsController.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppServices _appServices;
    private readonly Action _dockRefreshAction;
    private readonly Action<DockPositioningMode> _positioningModeChangeAction;
    private readonly Action _applyLocalizedTexts;
    private bool _localizationRegistered;
    private bool _isInitialized;

    private SupportedLanguage _selectedLanguage;

    // Icon management
    private int _selectedItemIndex = -1;

    // Appearance
    private int _iconSize;
    private int _iconSpacing;
    private int _transparency;
    private int _borderRounding;
    private int _dockPadding;
    private Color _dockColor = Colors.Black;
    private bool _tintIcons;
    private Color _tintColor = Color.FromRgb(0, 80, 140); // 7th preset by default

    // Positioning
    private bool _isStaticMode = true;
    private DockVerticalAnchor _verticalAnchor = DockVerticalAnchor.TOP;
    private DockHorizontalAnchor _horizontalAnchor = DockHorizontalAnchor.MIDDLE;
    private int _topSpacing, _leftSpacing, _rightSpacing, _bottomSpacing;

    public ObservableCollection<DockItemListEntry> ItemEntries { get; } = new();
    public SupportedLanguage[] Languages => Enum.GetValues<SupportedLanguage>();
    public string[] LanguageNames => Languages.Select(l => l.NativeDisplayName()).ToArray();

    public IReadOnlyList<AnchorOption> VerticalAnchorOptions => new[]
    {
        new AnchorOption(T("settings.positioning.choice.top"), DockVerticalAnchor.TOP),
        new AnchorOption(T("settings.positioning.choice.middle"), DockVerticalAnchor.MIDDLE),
        new AnchorOption(T("settings.positioning.choice.down"), DockVerticalAnchor.DOWN)
    };

    public IReadOnlyList<HorizontalAnchorOption> HorizontalAnchorOptions => new[]
    {
        new HorizontalAnchorOption(T("settings.positioning.choice.left"), DockHorizontalAnchor.LEFT),
        new HorizontalAnchorOption(T("settings.positioning.choice.middle"), DockHorizontalAnchor.MIDDLE),
        new HorizontalAnchorOption(T("settings.positioning.choice.right"), DockHorizontalAnchor.RIGHT)
    };

    public int SelectedLanguageIndex
    {
        get => Array.IndexOf(Languages, _selectedLanguage);
        set
        {
            if (value < 0 || value >= Languages.Length) return;
            SelectedLanguage = Languages[value];
        }
    }

    public int SelectedItemIndex
    {
        get => _selectedItemIndex;
        set { SetProperty(ref _selectedItemIndex, value); UpdateButtonStates(); }
    }

    public int IconSize { get => _iconSize; set => SetProperty(ref _iconSize, value); }
    public int IconSpacing { get => _iconSpacing; set => SetProperty(ref _iconSpacing, value); }
    public int Transparency { get => _transparency; set => SetProperty(ref _transparency, value); }
    public int BorderRounding { get => _borderRounding; set => SetProperty(ref _borderRounding, value); }
    public int DockPadding { get => _dockPadding; set => SetProperty(ref _dockPadding, value); }

    /// <summary>Quick-pick preset colors for the dock background.</summary>
    public IReadOnlyList<IBrush> PresetColors { get; } = new[]
    {
        new SolidColorBrush(Color.FromRgb(0, 0, 0)),      // black
        new SolidColorBrush(Color.FromRgb(30, 30, 30)),   // dark gray
        new SolidColorBrush(Color.FromRgb(60, 60, 60)),   // gray
        new SolidColorBrush(Color.FromRgb(120, 120, 120)),// light gray
        new SolidColorBrush(Color.FromRgb(255, 255, 255)),// white
        new SolidColorBrush(Color.FromRgb(20, 50, 90)),   // navy
        new SolidColorBrush(Color.FromRgb(0, 80, 140)),   // blue
        new SolidColorBrush(Color.FromRgb(0, 100, 80)),   // teal
        new SolidColorBrush(Color.FromRgb(60, 90, 20)),   // olive
        new SolidColorBrush(Color.FromRgb(120, 70, 0)),   // brown
        new SolidColorBrush(Color.FromRgb(140, 30, 40)),  // dark red
        new SolidColorBrush(Color.FromRgb(90, 40, 90))    // purple
    };

    public Color DockColor { get => _dockColor; set => SetProperty(ref _dockColor, value); }
    public IBrush DockColorBrush => new SolidColorBrush(DockColor);

    /// <summary>Enables the iOS-style color tint over all dock icons.</summary>
    public bool TintIcons { get => _tintIcons; set => SetProperty(ref _tintIcons, value); }
    public Color TintColor { get => _tintColor; set => SetProperty(ref _tintColor, value); }
    public IBrush TintColorBrush => new SolidColorBrush(TintColor);

    public bool IsStaticMode { get => _isStaticMode; set => SetProperty(ref _isStaticMode, value); }
    public DockVerticalAnchor VerticalAnchor { get => _verticalAnchor; set => SetProperty(ref _verticalAnchor, value); }
    public DockHorizontalAnchor HorizontalAnchor { get => _horizontalAnchor; set => SetProperty(ref _horizontalAnchor, value); }

    /// <summary>Selected alignment combo entry (localized label + value).</summary>
    public AnchorOption? SelectedVerticalOption
    {
        get => VerticalAnchorOptions.FirstOrDefault(o => o.Value == VerticalAnchor);
        set
        {
            if (value != null && value.Value != VerticalAnchor)
                VerticalAnchor = value.Value;
        }
    }

    /// <summary>Selected alignment combo entry (localized label + value).</summary>
    public HorizontalAnchorOption? SelectedHorizontalOption
    {
        get => HorizontalAnchorOptions.FirstOrDefault(o => o.Value == HorizontalAnchor);
        set
        {
            if (value != null && value.Value != HorizontalAnchor)
                HorizontalAnchor = value.Value;
        }
    }
    public int TopSpacing { get => _topSpacing; set => SetProperty(ref _topSpacing, value); }
    public int LeftSpacing { get => _leftSpacing; set => SetProperty(ref _leftSpacing, value); }
    public int RightSpacing { get => _rightSpacing; set => SetProperty(ref _rightSpacing, value); }
    public int BottomSpacing { get => _bottomSpacing; set => SetProperty(ref _bottomSpacing, value); }

    // Button enabled states
    public bool CanRemove { get; private set; }
    public bool CanMoveUp { get; private set; }
    public bool CanMoveDown { get; private set; }

    public SupportedLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
                OnPropertyChanged(nameof(SelectedLanguageIndex));
        }
    }

    // Localized text properties
    public string WindowTitle => T("settings.window.title");
    public string PageTitle => T("settings.page.title");
    public string PageSubtitle => T("settings.page.subtitle");
    public string LanguageLabel => T("settings.language.label");
    public string TabIcons => T("settings.tab.icons");
    public string TabIconsCustomization => T("settings.tab.iconsCustomization");
    public string TabDockCustomization => T("settings.tab.dockCustomization");
    public string TabDockPositioning => T("settings.tab.dockPositioning");
    public string TabGeneral => T("settings.tab.general");
    // --- continued below ---
    public string ItemsTitle => T("settings.icons.items.title");
    public string ItemsHelper => T("settings.icons.items.helper");
    public string ActionsTitle => T("settings.icons.actions.title");
    public string ActionsHelper => T("settings.icons.actions.helper");
    public string MoveUpText => T("settings.icons.moveUp");
    public string MoveDownText => T("settings.icons.moveDown");
    public string AddProgramText => T("settings.icons.addProgram");
    public string AddFolderText => T("settings.icons.addFolder");
    public string AddModuleText => T("settings.icons.addWindowsModule");
    public string RemoveText => T("settings.icons.removeSelected");
    public string IconSizeTitle => T("settings.iconsCustomization.size.title");
    public string IconSizeHelper => T("settings.iconsCustomization.size.helper");
    public string SpacingTitle => T("settings.iconsCustomization.spacing.title");
    public string SpacingHelper => T("settings.iconsCustomization.spacing.helper");
    public string TintIconsTitle => T("settings.iconsCustomization.tint.title");
    public string TintColorHelper => T("settings.iconsCustomization.tint.helper");
    public string TransparencyTitle => T("settings.dockCustomization.transparency.title");
    public string TransparencyHelper => T("settings.dockCustomization.transparency.helper");
    public string RoundingTitle => T("settings.dockCustomization.rounding.title");
    public string RoundingHelper => T("settings.dockCustomization.rounding.helper");
    public string DockPaddingTitle => T("settings.dockCustomization.padding.title");
    public string DockPaddingHelper => T("settings.dockCustomization.padding.helper");
    public string BgColorTitle => T("settings.dockCustomization.background.title");
    public string BgColorHelper => T("settings.dockCustomization.background.helper");
    public string PosModeTitle => T("settings.positioning.mode.title");
    public string PosModeHelper => T("settings.positioning.mode.helper");
    public string StaticText => T("settings.positioning.mode.static");
    public string DynamicText => T("settings.positioning.mode.dynamic");
    public string AlignmentTitle => T("settings.positioning.alignment.title");
    public string VerticalText => T("settings.positioning.alignment.vertical");
    public string HorizontalText => T("settings.positioning.alignment.horizontal");
    public string ScreenSpacingTitle => T("settings.positioning.spacing.title");
    public string TopSpacingLabel => T("settings.positioning.spacing.top");
    public string LeftSpacingLabel => T("settings.positioning.spacing.left");
    public string RightSpacingLabel => T("settings.positioning.spacing.right");
    public string BottomSpacingLabel => T("settings.positioning.spacing.down");
    public string DynamicPosTitle => T("settings.positioning.dynamic.title");
    public string DynamicPosHelper => T("settings.positioning.dynamic.helper");
    public string VersionText => T("settings.general.version");
    public string RepoText => T("settings.general.repository");
    public string ContactText => T("settings.general.contact");
    public string OpenSourceText => T("settings.general.openSource");
    public string AcknowledgementsText => T("settings.general.acknowledgements");
    public string StartWithWindowsText => T("settings.general.startWithWindows");
    public string ShowUnpinnedRunningAppsText => T("settings.general.showUnpinnedRunningApps");
    public string ArrangeVerticalText => T("settings.general.arrangeVertical");
    public string CustomColorText => T("settings.customColor");

    private bool _isAutoStartEnabled;
    public bool IsAutoStartEnabled
    {
        get => _isAutoStartEnabled;
        set => SetProperty(ref _isAutoStartEnabled, value);
    }

    private bool _showUnpinnedRunningApps = true;
    public bool ShowUnpinnedRunningApps
    {
        get => _showUnpinnedRunningApps;
        set => SetProperty(ref _showUnpinnedRunningApps, value);
    }

    private bool _isVerticalDock;
    public bool IsVerticalDock
    {
        get => _isVerticalDock;
        set => SetProperty(ref _isVerticalDock, value);
    }

    public SettingsViewModel(AppServices appServices, Action dockRefreshAction,
        Action<DockPositioningMode> positioningModeChangeAction)
    {
        _appServices = appServices;
        _dockRefreshAction = dockRefreshAction;
        _positioningModeChangeAction = positioningModeChangeAction;
        _applyLocalizedTexts = () => { RefreshAllProperties(); RefreshItemLabels(); };
    }

    public void Initialize()
    {
        _appServices.LocalizationService.AddListener(_applyLocalizedTexts);
        _localizationRegistered = true;

        var app = _appServices.AppearanceService;
        IconSize = app.GetIconsSize();
        IconSpacing = app.GetSpacingBetweenIcons();
        Transparency = app.GetDockTransparencyPercentage();
        BorderRounding = app.GetDockBorderRounding();
        DockPadding = app.GetDockPadding();
        DockColor = ParseRgbColor(app.GetDockColorRGB());
        TintIcons = app.GetTintIcons();
        TintColor = ParseRgbColor(app.GetTintColorRGB());

        var pos = _appServices.PositioningService;
        IsStaticMode = pos.GetPositioningMode() == DockPositioningMode.STATIC;
        VerticalAnchor = pos.GetVerticalAnchor();
        HorizontalAnchor = pos.GetHorizontalAnchor();
        TopSpacing = pos.GetTopSpacing();
        LeftSpacing = pos.GetLeftSpacing();
        RightSpacing = pos.GetRightSpacing();
        BottomSpacing = pos.GetBottomSpacing();

        RefreshItemLabels();
        SelectedLanguage = _appServices.LocalizationService.GetCurrentLanguage();
        IsAutoStartEnabled = Infrastructure.Windows.Adapters.AutoStartHelper.IsAutoStartEnabled();
        ShowUnpinnedRunningApps = app.GetShowUnpinnedRunningApps();
        IsVerticalDock = app.GetVerticalDock();
        _isInitialized = true;
    }

    /// <summary>Routes property changes to the appropriate service method (after Initialize).</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_isInitialized) return;

        switch (e.PropertyName)
        {
            case nameof(IconSize): OnIconSizeChanged(); break;
            case nameof(IconSpacing): OnIconSpacingChanged(); break;
            case nameof(Transparency): OnTransparencyChanged(); break;
            case nameof(BorderRounding): OnBorderRoundingChanged(); break;
            case nameof(DockPadding): OnDockPaddingChanged(); break;
            case nameof(DockColor): OnDockColorChanged(); OnPropertyChanged(nameof(DockColorBrush)); break;
            case nameof(TintIcons): OnTintIconsChanged(); break;
            case nameof(TintColor): OnTintColorChanged(); OnPropertyChanged(nameof(TintColorBrush)); break;
            case nameof(SelectedLanguage): OnLanguageChanged(SelectedLanguage); break;
            case nameof(IsAutoStartEnabled): OnAutoStartChanged(); break;
            case nameof(ShowUnpinnedRunningApps): OnShowUnpinnedRunningAppsChanged(); break;
            case nameof(IsVerticalDock): OnVerticalDockChanged(); break;
            case nameof(IsStaticMode): OnPositioningModeChanged(); break;
            case nameof(VerticalAnchor): OnVerticalAnchorChanged(); break;
            case nameof(HorizontalAnchor): OnHorizontalAnchorChanged(); break;
            case nameof(TopSpacing): OnTopSpacingChanged(); break;
            case nameof(LeftSpacing): OnLeftSpacingChanged(); break;
            case nameof(RightSpacing): OnRightSpacingChanged(); break;
            case nameof(BottomSpacing): OnBottomSpacingChanged(); break;
        }
    }

    private string T(string key) => _appServices.LocalizationService.Text(key);

    private static Color ParseRgbColor(string rgb)
    {
        var parts = rgb.Trim().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        return Color.FromRgb(r, g, b);
    }

    private static string ColorToRgb(Color c) => $"{c.R}, {c.G}, {c.B}, ";
    // --- commands below ---

    public void OnIconSizeChanged() { _appServices.AppearanceService.SetIconsSize(IconSize); _dockRefreshAction(); }
    public void OnIconSpacingChanged() { _appServices.AppearanceService.SetSpacingBetweenIcons(IconSpacing); _dockRefreshAction(); }
    public void OnTransparencyChanged() { _appServices.AppearanceService.SetDockTransparencyPercentage(Transparency); _dockRefreshAction(); }
    public void OnBorderRoundingChanged() { _appServices.AppearanceService.SetDockBorderRounding(BorderRounding); _dockRefreshAction(); }
    public void OnDockPaddingChanged() { _appServices.AppearanceService.SetDockPadding(DockPadding); _dockRefreshAction(); }
    public void OnDockColorChanged() { _appServices.AppearanceService.SetDockColorRGB(ColorToRgb(DockColor)); _dockRefreshAction(); }
    public void OnTintIconsChanged() { _appServices.AppearanceService.SetTintIcons(TintIcons); _dockRefreshAction(); }
    public void OnTintColorChanged() { _appServices.AppearanceService.SetTintColorRGB(ColorToRgb(TintColor)); _dockRefreshAction(); }
    public void OnLanguageChanged(SupportedLanguage lang) { _appServices.LocalizationService.SetLanguage(lang); _dockRefreshAction(); }

    public void OnAutoStartChanged()
    {
        if (IsAutoStartEnabled)
            Infrastructure.Windows.Adapters.AutoStartHelper.EnableAutoStart();
        else
            Infrastructure.Windows.Adapters.AutoStartHelper.DisableAutoStart();
    }

    public void OnShowUnpinnedRunningAppsChanged()
    {
        _appServices.AppearanceService.SetShowUnpinnedRunningApps(ShowUnpinnedRunningApps);
        _dockRefreshAction();
    }

    public void OnVerticalDockChanged()
    {
        _appServices.AppearanceService.SetVerticalDock(IsVerticalDock);
        _dockRefreshAction();
    }
    public void OnPositioningModeChanged()
    {
        var mode = IsStaticMode ? DockPositioningMode.STATIC : DockPositioningMode.DYNAMIC;
        _positioningModeChangeAction(mode);
        _dockRefreshAction();
    }
    public void OnVerticalAnchorChanged() { _appServices.PositioningService.SetVerticalAnchor(VerticalAnchor); _dockRefreshAction(); }
    public void OnHorizontalAnchorChanged() { _appServices.PositioningService.SetHorizontalAnchor(HorizontalAnchor); _dockRefreshAction(); }
    public void OnTopSpacingChanged() { _appServices.PositioningService.SetTopSpacing(TopSpacing); _dockRefreshAction(); }
    public void OnLeftSpacingChanged() { _appServices.PositioningService.SetLeftSpacing(LeftSpacing); _dockRefreshAction(); }
    public void OnRightSpacingChanged() { _appServices.PositioningService.SetRightSpacing(RightSpacing); _dockRefreshAction(); }
    public void OnBottomSpacingChanged() { _appServices.PositioningService.SetBottomSpacing(BottomSpacing); _dockRefreshAction(); }

    public async Task AddProgramAsync(Window window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = T("dialog.fileChooser.executableTitle"),
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType(T("dialog.fileChooser.executableFilter")) { Patterns = new[] { "*.exe" } } }
        });
        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        var sel = ProgramSelectionResolver.Resolve(path);
        _appServices.IconGateway.CacheProgramIcon(sel.ExecutablePath);
        _appServices.DockService.AddItem(new DockProgramItemModel(sel.Label, sel.ExecutablePath));
        RefreshItemLabels();
        _dockRefreshAction();
    }

    public async Task AddFolderAsync(Window window)
    {
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = T("dialog.directoryChooser.title")
        });
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        // GetFileName returns "" for paths ending in a separator (e.g. root
        // drives); fall back to the raw path so the list never shows a blank row.
        var label = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
        if (string.IsNullOrWhiteSpace(label))
            label = path;
        _appServices.IconGateway.CacheFolderIcon(path);
        _appServices.DockService.AddItem(new DockFolderItemModel(label, path));
        RefreshItemLabels();
        _dockRefreshAction();
    }

    public void RemoveSelected()
    {
        if (SelectedItemIndex < 0) return;
        _appServices.DockService.RemoveItem(SelectedItemIndex);
        SelectedItemIndex = -1;
        RefreshItemLabels();
        _dockRefreshAction();
    }

    public void MoveItemUp()
    {
        if (SelectedItemIndex <= 0) return;
        _appServices.DockService.SwapItems(SelectedItemIndex, SelectedItemIndex - 1);
        int newIndex = SelectedItemIndex - 1;
        RefreshItemLabels();
        // Apply the selection after the rebuild: setting it before
        // RefreshItemLabels would be wiped by the collection Clear.
        SelectedItemIndex = newIndex;
        _dockRefreshAction();
    }

    public void MoveItemDown()
    {
        if (SelectedItemIndex < 0 || SelectedItemIndex >= ItemEntries.Count - 1) return;
        _appServices.DockService.SwapItems(SelectedItemIndex, SelectedItemIndex + 1);
        int newIndex = SelectedItemIndex + 1;
        RefreshItemLabels();
        SelectedItemIndex = newIndex;
        _dockRefreshAction();
    }

    /// <summary>
    /// Moves the item at <paramref name="fromIndex"/> into the gap at
    /// <paramref name="toIndex"/> (drag-reorder). The target is clamped to the
    /// list bounds and the selection follows the moved item.
    /// </summary>
    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= ItemEntries.Count) return;
        toIndex = Math.Clamp(toIndex, 0, ItemEntries.Count);
        if (fromIndex == toIndex) return;
        _appServices.DockService.MoveItem(fromIndex, toIndex);
        // DockModel.MoveItem leaves the item at index toIndex - 1 when moving
        // down (the gap shifts), so mirror that here for the selection.
        int newIndex = toIndex > fromIndex ? toIndex - 1 : toIndex;
        RefreshItemLabels();
        SelectedItemIndex = newIndex;
        _dockRefreshAction();
    }

    public void RefreshItemLabels()
    {
        ItemEntries.Clear();
        foreach (var item in _appServices.DockService.GetItems())
        {
            string label = _appServices.LocalizationService.DockItemLabel(item);
            ItemEntries.Add(new DockItemListEntry(label, ResolveItemIcon(item)));
        }
        UpdateButtonStates();
    }

    private Bitmap? ResolveItemIcon(DockItem item)
    {
        if (item is DockSettingsItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return icon ?? IconLoader.LoadFromAsset("Assets/icons/folder.png");
        }

        if (item is DockWindowsModuleItemModel moduleItem)
        {
            return IconLoader.LoadWindowsModuleIcon(moduleItem.Module)
                ?? IconLoader.LoadFromAsset("Assets/icons/folder.png");
        }

        if (item is DockProgramItemModel programItem)
        {
            return IconLoader.LoadFromFile(_appServices.IconGateway.ResolveProgramIcon(programItem.ExecutablePath));
        }

        if (item is DockFolderItemModel folderItem)
        {
            return IconLoader.LoadFromFile(_appServices.IconGateway.ResolveFolderIcon(folderItem.FolderPath))
                ?? IconLoader.LoadFromAsset("Assets/icons/folder.png");
        }

        return null;
    }

    private void UpdateButtonStates()
    {
        var items = _appServices.DockService.GetItems();
        CanRemove = SelectedItemIndex >= 0 && SelectedItemIndex < items.Count
            && items[SelectedItemIndex] is not DockSettingsItemModel;
        CanMoveUp = SelectedItemIndex > 0;
        CanMoveDown = SelectedItemIndex >= 0 && SelectedItemIndex < ItemEntries.Count - 1;
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    private void RefreshAllProperties()
    {
        // Force all computed properties to re-notify (localized texts).
        foreach (var prop in GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.GetMethod != null && p.GetIndexParameters().Length == 0))
            OnPropertyChanged(prop.Name);
        OnPropertyChanged(nameof(Languages));
        OnPropertyChanged(nameof(VerticalAnchorOptions));
        OnPropertyChanged(nameof(HorizontalAnchorOptions));
        OnPropertyChanged(nameof(SelectedVerticalOption));
        OnPropertyChanged(nameof(SelectedHorizontalOption));
    }

    public void Shutdown()
    {
        if (_localizationRegistered)
        {
            _appServices.LocalizationService.RemoveListener(_applyLocalizedTexts);
            _localizationRegistered = false;
        }
    }
}
