namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Domain;

/// <summary>
/// Composition root record bundling all application services and adapters.
/// Direct port of the Java AppServices record. App.axaml.cs constructs this
/// with concrete infrastructure adapters and injects it into the UI.
/// </summary>
public sealed record AppServices(
    DockService DockService,
    DockAppearanceService AppearanceService,
    DockPositioningService PositioningService,
    DockItemActionService ItemActionService,
    WindowPreviewService WindowPreviewService,
    IIconGateway IconGateway,
    LocalizationService LocalizationService,
    ThemeService ThemeService
);
