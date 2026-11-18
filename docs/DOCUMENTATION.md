# Cedro Modern Dock — Living Documentation

> **For agents:** this file is the primary orientation guide for AI coding
> agents working in this repository. Read it before changing code to learn how
> the project is organized, which business rules it enforces, and which
> pitfalls to avoid. It describes the current state of the project only.

## Maintenance Rules

Every agent that changes this repository must keep this file in sync:

1. **Update on every commit.** Before committing a change that affects
   behavior, structure, architecture, data flow, integrations, or business
   rules, update the affected sections of this file and include it in the
   same commit. Commits that only touch formatting, comments, or other docs
   with no semantic impact need no update.
2. **Current state only.** Describe the system as it exists today, in the
   present tense. No history, no changelog, no "before/after", and no words
   such as "previously", "now", "no longer", "removed", "changed", or
   "replaced".
3. **No future plans.** Document only what exists in the code; no roadmap,
   "planned", or TODO items presented as features.
4. **Delete, don't annotate.** When a feature, rule, or component goes away,
   delete its description entirely. When behavior changes, replace the old
   description with the new one.
5. **Verify every statement.** Each claim must be traceable to the code,
   tests, or configuration. If something cannot be verified, omit it.
6. **Rewrite only what the change touches**, but always describe the whole
   system, not just the latest change. Running the update twice on the same
   code must produce no diff.
7. **No volatile details.** Leave out exact line numbers, local environment
   settings, minor dependency versions, and transient states.
8. **No README or CONTRIBUTING duplication.** `README.md` presents the project
   and `docs/CONTRIBUTING.md` explains how to build and run it; this file
   explains what the system does and why, its rules, architecture, and
   pitfalls. Summarize and link to code instead of copying it.
9. **Keep the section order:** Overview, Business Rules, Architecture,
   Gotchas & Pitfalls. Write in English.

## Overview

Cedro Modern Dock is a Windows dock: a translucent, rounded launcher bar that
hosts pinned shortcuts (programs, folders, and built-in Windows modules),
shows which of those programs are running, mirrors running-but-unpinned apps
taskbar-style, and offers live window previews on hover. It is a .NET 9 /
Avalonia 11 desktop application (MVVM, CommunityToolkit.Mvvm) with a 21-language
UI and a fully customizable appearance, including built-in and third-party
dock themes.

## Business Rules

1. All configuration persists to `%APPDATA%\CedroModernDock\config.json` as a
   single `DockModel`. The JSON shape is compatible with the original
   Java/Jackson format (camelCase keys, polymorphic `@type` item
   discrimination); unknown properties are ignored, and a missing or corrupt
   file is replaced by a fresh default config.
2. The app is single-instance: a second launch shows a localized warning and
   exits.
3. Dock items are one of five kinds: settings item, Windows module
   (Start Menu, My Computer, Recycle Bin, Control Panel, Windows Settings),
   program (.exe), folder, or separator. A newly created config contains the settings
   item, which cannot be removed; other items can be reordered and removed.
   In the Settings dock-items list, items can be dragged to reorder them (a
   drag handle icon on each row indicates this); dropping into a gap moves the
   item there and persists immediately. Context menus on the dock support
   editing items and adding separators; unlocked icons can be dragged to
   reorder them directly on the dock. The open Settings list follows item
   additions, removals, edits, and moves. Selection follows the item identity
   and clears when that item is deleted; removal and movement resolve the
   current model position rather than trusting a cached row index.
4. The dock window never appears in the taskbar or Alt-Tab
   (`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, no `WS_EX_APPWINDOW`). It is an
   owned window of the desktop (parented to Progman or the WorkerW hosting the
   desktop icons), so Win+D keeps it visible, normal windows cover it, and it
   is never part of the minimize-all cycle.
5. Running-but-unpinned apps are listed after a separator, mirroring the
   taskbar: visible, non-tool, non-cloaked windows grouped by executable,
   excluding programs already pinned in the dock and the dock's own windows
   (any window whose owning process is the dock executable — e.g. the Settings
   window — is filtered out). The section is gated by the "Show running
   programs that aren't pinned on the Dock" setting, and the separator renders
   only while the section is shown (in both orientations).
6. For UWP/AppX apps the visible top-level window belongs to
   `ApplicationFrameHost.exe`; the entry is resolved to the real app via the
   child CoreWindow's executable so grouping, labels, and icons target the
   actual app. The resolution is cached per frame HWND so minimizing the app
   (which reparents the CoreWindow away) does not flip the entry back to
   `ApplicationFrameHost.exe`. Preview rows for such apps target the
   ApplicationFrameHost frame — the cloaked CoreWindow cannot be shown or
   activated — and IME helper windows (class `IME`/`MSCTFIME UI`) are excluded
   from open-window enumeration. The Windows Settings app (`SystemSettings.exe`)
   carries no icon in its EXE or frame, so its running-app icon is hardcoded to
   the bundled `windows_settings.png` asset.
 7. Hovering a running program opens a preview popup with one live DWM
   thumbnail per window; clicking a thumbnail activates that window. Hovering
   an individual thumbnail reveals a close button overlaid on its top-right
   corner (taskbar-style); clicking it posts WM_CLOSE to that window, and the
   popup re-queries the program's windows so the closed row is dropped or the
   popup hides when the last window closes. Clicking an unpinned running app's
   icon directly activates its window only when the app has exactly one open
   window; with several windows the click does nothing (the preview popup
   remains the way to pick one). The popup sits 4 px outside the visible bar
   (below/above it in horizontal mode, beside it in vertical mode with the
   side chosen by the horizontal anchor), pushed further out where the
   hovered, zoomed item extends past the bar's edge.
8. Icon tinting is an iOS-style duotone: when enabled, every dock icon is
   recolored as `tint color × luminance` per pixel (alpha preserved), so
   icons keep their shape and shading but lose their original hue. The color
   comes from 12 presets or a custom picker; the default is the 7th preset
   (blue).
9. Program/folder icons resolve in order: cached PNG extracted from the exe
   resource (`PrivateExtractIcons`, 256 px), then the running window's own
   icon (`WM_GETICON`/class icon), then the AppX manifest logo
   (`Square44x44Logo` plus scale variants). Results are cached as PNG files
   under `%APPDATA%\CedroModernDock\iconsCache`, keyed by SHA-256 of the
   source path.
10. Positioning is either static (screen anchors + per-edge spacings) or
    dynamic (free drag by pressing the bar background; the dragged position
    is persisted). Anchors, spacings, and the persisted position all refer to
    the visible dock bar, not to the larger dock window around it. Switching
    from static to dynamic seeds the dynamic position with the bar's current
    screen position.
11. Auto-start is registered in
    `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` under the name
    `CedroModernDock`, pointing at the running exe path. On first run (when
    `config.json` is created because none exists) the app registers itself
    automatically, so the "Start with Windows" option is enabled by default;
    the Settings checkbox reads and toggles this registry entry directly.
12. The UI is localized through 21 property bundles; the English bundle loads
    first and any key missing from the selected language silently falls back
    to English.
13. The dock bar is a translucent rounded panel whose color, transparency,
    corner rounding, and inner padding (space between the bar's edge and the
    icons: 0–30, default 10, clamped on both read and write so out-of-range
    `config.json` values are tolerated) are configurable; icons, spacing, and
    orientation (horizontal/vertical) are configurable as well. Hovering an
    item zooms it to 1.3× by default (themes can set 1.0–1.8×), and the zoomed icon may extend past the bar's edge,
    so the zoom is never cut regardless of the padding.
14. Clicking a pinned program launches it; when the executable is missing (or
    the launch fails for any other reason) the dock shows a localized warning
    dialog with the program's label and path — a failed launch never crashes
    the dock. Dropped `.lnk` files preserve the executable launch target,
    argument string, and working directory. The executable used for icons
    and running-window matching can differ from the launch target for
    Squirrel shortcuts. These optional launch details persist in the item;
    configs without them use empty arguments and the default working
    directory. Changing a program's target clears its shortcut launch details.
15. Themes select the dock surface, hover zoom, indicators, separators, menus,
    and tooltips. Omitted values fall back to user settings or Cedro defaults;
    appearance controls imposed by the theme are disabled. `themeId` persists
    in config.json and defaults to `cedro`. User themes live under
    `%APPDATA%\CedroModernDock\Themes\<id>` and can be imported from a folder,
    `.zip`, or `.cedrotheme` package. Folder names must match manifest ids,
    ignoring case on Windows; mismatches are reported and excluded from the
    gallery. Deletion and export revalidate identity before touching files.
16. Invalid optional theme image paths produce warnings and fall back to the
    default visual. Relative paths reject control characters, Windows-invalid
    filename characters, traversal, and segments ending in dots or spaces;
    failed path normalization returns no image. Package extraction accepts
    data files only, limits entry counts and bytes, rejects duplicate paths,
    and validates the staged manifest's id, reserved-id status and size before
    replacing an installed theme. A failed deletion keeps the active theme
    configuration intact.
17. Dock item context menus omit the Quit action; quitting remains available
    from the dock background menu and the tray icon. Only one dock context
    menu is open at a time. Outside clicks, including clicks on the desktop
    or another application, dismiss it without consuming the click. Clicks
    inside an open submenu keep the parent menu open.
18. Hover and press feedback scale the icon content while the button's input
    area stays fixed. Each axis uses a 300 ms local scalar transition with
    CubicEaseOut easing; leaving an icon interrupts its enlargement and returns
    it to normal without replay. The Ardoise theme uses 1.25× magnification.
    Dock item buttons disable bounds clipping so enlarged icons remain visible.
19. Discreet mode is disabled by default and is configured in General. When
    enabled, the dock keeps its normal desktop attachment and, after an idle delay of
    0–1000 ms (default 300 ms), fades to a configurable opacity of 15–50%
    (default 15%) and scales to 96% size. The idle-delay and opacity controls
    appear while the mode is enabled, and their values persist in config.json.
    It remains visible and interactive at the same position. Opacity changes
    take 300 ms with CubicEaseOut; whole-bar scaling takes 350 ms with
    BackEaseOut. Approaching the dock restores its normal appearance. Open
    Settings, dock context menus, previews, owned dialogs, and dragging keep
    the dock awake. The preferences persist as `autoHide`, `autoHideDelay`,
    and `discreetModeOpacity` (integer percentage, clamped on read and write).
    These keys describe discreet mode, not movement off screen. Individual
    icon hover and press animations are independent of this mode.

## Architecture

### Components

- **CedroModernDock.Core** — platform-agnostic domain and application layer:
  models (`DockModel` and item models, anchors, positioning mode), services
  (`DockService`, `DockAppearanceService`, `DockPositioningService`,
  `DockItemActionService`, `WindowPreviewService`, `LocalizationService`,
  `ProgramSelectionResolver`, `WindowTitleFormatter`, `ThemeService`),
  the monotonic idle state `DiscreetModeState` and discreet-mode preferences
  in `DockAppearanceService`,
  validated theme models and `ThemeParser`, port interfaces
  (`IIconGateway`, `IWindowQueryGateway`, launchers, `IDockRepository`,
  `IScreenBoundsProvider`, `IThemeRepository`), and the `AppServices` composition record.
- **CedroModernDock.Infrastructure.Windows** — Windows-specific behavior:
  P/Invoke layer (`Win32Interop`, `ShellInterop`, `DwmThumbnailInterop`),
  window enumeration (`Win32WindowQuery`), desktop attachment and minimize
  defense (`DockWindowBehavior`), the menu-scoped passive mouse-button observer
  (`DesktopMouseClickObserver`), native DWM thumbnail windows
  (`ThumbnailWindow`) and their overlay close buttons
  (`PreviewCloseButtonWindow`), icon extraction (`WindowsIconExtractor`), adapters
   (`CachedWindowsIconGateway`, `Win32WindowQueryGateway`, launchers,
   `Win32WindowsInputSender`,
  `AutoStartHelper`, `WindowsScreenBoundsProvider`, `SingleInstanceGuard`),
  and persistence (`JsonDockRepository`, `FileSystemThemeRepository`).
- **CedroModernDock** — Avalonia UI: `MainWindow` (dock bar, hover handling,
  drag positioning), `WindowPreviewPopup` (rows of native thumbnails, fade
  in/out), `SettingsWindow` (six pages: Dock Items, Icons Customization, Dock
  Customization, Themes, Dock Positioning, General), with a language selector fixed
  at the sidebar footer and synchronized with the selector in General;
  `AddWindowsModulesWindow`,
  `AcknowledgementsWindow`; ViewModels including `MainWindowViewModel`,
  `SettingsViewModel`, `RunningAppViewModel`, `DockItemViewModel`;
  `IconTinter` (runtime icon recoloring), `IconLoader`, `ThemeRenderer` (dock
  surfaces, item styles and popup resources), `DockItemButton` (content-only
  hover/press scaling), `DesktopContextMenuDismissal` (desktop click dismissal),
  and `NineSliceImage` (skin
  rendering); composition root in
  `App.axaml.cs`.
- **CedroModernDock.Tests** — unit tests covering JSON round-trips
  (including configs that lack newer keys such as `dockPadding`, which load
  with defaults), dock appearance settings (padding clamping), window
  querying, program launching, shortcut arguments and working directories,
  theme parsing, package installation, folder identity, gallery fallbacks,
  and Settings selection synchronization during dock-item editing and
  reordering (`DockService.MoveItem`). The legacy Java-format compatibility test reads
  `config_compat_test.json` from the test working directory; that file is not
  in the repository, so the test returns early and passes unless it is
  supplied.
- **CedroModernDock.InteractionTests** — headless UI tests in a separate
  process, using Avalonia and Skia to exercise fast pointer sweeps, press
  feedback, fixed hit areas, menu contents, submenu hit testing, and dismissal
  observer lifetime, discreet-mode settings, and idle/wake appearance. They
  use simulated clicks and skip native dock attachment.

### Data Flow

- **Discreet mode**: while enabled, a 50 ms UI timer reads the native cursor
  position and the dock's physical window bounds, with a 24 px approach
  margin. Menu, preview, Settings, dialog, and drag state suspend the idle
  delay. `DiscreetModeState` uses monotonic elapsed time; the UI applies
  opacity and whole-bar scale without changing window geometry or icon
  animations. Desktop attachment and window stacking are independent of the
  mode. Closing the dock stops polling.
- **Theme changes**: `ThemeService` combines embedded themes with validated
  user themes and emits `ThemesChanged` after selection or catalog changes.
  The dock applies `ThemeRenderer` output and Settings rebuilds its gallery.
  `FileSystemThemeRepository` checks ids on load and before delete/export;
  staged installations are validated before the existing folder is swapped.
- **Context-menu dismissal**: the dock's no-activate window supplements
  Avalonia light dismissal with a passive Windows mouse hook while a menu is
  open. The hook queues button-down coordinates to the UI dispatcher, which
  checks the menu and open submenu bounds in screen coordinates. Closing the
  menu or dock disposes the observer, and callbacks from disposed sessions
  cannot close replacement menus.

- **Startup**: `Program.Main` installs a crash logger, acquires the
  single-instance guard, then boots Avalonia. `App` composes `AppServices`
  (repository → `DockService`; all Windows adapters wired), creates
  `MainWindowViewModel`, and runs `Initialize`: appearance is applied, the
  dock UI is built from the persisted items, and the indicator watcher starts
  polling every 1.2 s.
- **Settings changes**: a `SettingsViewModel` handler calls the matching
  service setter (which persists immediately via `SaveChanges`), then the dock
  refresh action runs `MainWindowViewModel.UpdateDockUI`: items are rebuilt,
  appearance is re-applied, and the dock repositions. Drag-reorder in the
  Dock Items list (rows show a drag-handle icon and a move cursor) posts the
  source index through `DragDrop.DoDragDropAsync` and drops into a gap:
  `SettingsViewModel.MoveItem` calls `DockService.MoveItem` (remove-then-insert
  semantics), re-selects the moved item, refreshes the list, and triggers the
  dock refresh. The landing gap and a 2 px insertion indicator are resolved
  from the realized containers under the pointer, so scrolled items keep their
  real position.
- **Item synchronization**: `DockService.SaveItemChanges` persists item
  mutations and emits `ItemsChanged`. The open `SettingsViewModel` refreshes
  its rows while preserving the selected model reference, and unsubscribes
  when the window closes. Appearance-only saves do not emit this event.
- **Shortcut launch**: `ShortcutResolver` reads `.lnk` targets, arguments,
  and working directories using Windows Script Host, with Windows Installer
  target resolution for advertised shortcuts. `DockItemActionService` passes
  the saved launch details to `WindowsProgramLauncher`. Raw shortcut arguments
  retain their Windows quoting; generated Squirrel arguments use
  `ProcessStartInfo.ArgumentList`. UAC retries use the same command and
  working directory through the `runas` shell verb.
- **Running apps**: the watcher enumerates taskbar windows, groups them by
  executable, filters pinned programs, and diffs against the existing
  `RunningAppViewModel` set; icons load in the background through the
  resolve chain, the tint is applied on assignment, and persistent VMs keep
  the untinted bitmap so the tint can be re-applied on appearance changes.
- **Window previews**: hovering a program loads its open windows, the popup
  builds one row per window, positions itself relative to the dock, fades in,
  and registers a native `ThumbnailWindow` per row at the row's screen
  coordinates; clicking activates the window; leaving fades it out and tears
  the thumbnails down. Each row also gets a hidden native `PreviewCloseButtonWindow`
  overlaid on its thumbnail's top-right corner; hovering the row fades it in
  (shown/hidden via a cursor-position check, since the thumbnail and button are
  separate top-level windows), and clicking it posts `WM_CLOSE` to the window
  before the popup re-queries the program and rebuilds or hides.
- **Icons**: `WindowsIconExtractor` extracts 256 px PNGs into the icon cache,
  keyed by SHA-256 of the exe/folder path; the folder path uses the shell
  image list, UWP apps use the AppX manifest asset.

### Integrations

- Win32 APIs: window styles and subclassing, `SetParent` to the desktop
  hierarchy, DWM thumbnails and cloaking, shell image lists, `PrivateExtractIcons`,
  AppX manifest XML parsing.
- Windows Registry for auto-start.
- Avalonia 11.3 (Fluent theme, `Avalonia.Controls.ColorPicker` for the custom
  color dialogs), CommunityToolkit.Mvvm, System.Text.Json,
  System.Drawing.Common (icon tinting and HICON conversion).
- WiX Toolset v4 produces a per-machine MSI (`dotnet/installer/`): a
  single-file self-contained publish bundled with the Skia native libraries,
  installed to `Program Files`, desktop and Start menu shortcuts,
  `ARPPRODUCTICON` set to the app icon, a `MajorUpgrade` so a new release
  replaces the files of previous versions while `config.json` (in
  `%APPDATA%`) is preserved, a GPL-3.0 license page, and a finish-dialog
  checkbox (checked by default) that launches the dock unelevated after the
  install. The WiXUI wizard dialogs are English; the Windows Installer's
  built-in dialog text is localized to the OS language.
- Code signing via SignPath Foundation (OSS program), driven by the GitHub
  Actions workflow `.github/workflows/sign-installer.yml` (triggered by `v*`
  tags or manually). A `windows-latest` runner installs WiX 4.0.5 and its UI
  and Util extensions, runs `dotnet/installer/build.ps1`, uploads the unsigned
  MSI as a GitHub artifact, and submits it to the SignPath project
  `cedro-modern-dock` using the `msi-in-zip` artifact configuration. That
  configuration Authenticode-signs `CedroModernDock.exe` inside the MSI and
  the MSI itself; the bundled third-party native libraries (Skia, HarfBuzz,
  ANGLE) are not signed because the Foundation only allows signing binaries
  built from the project's own source. The workflow authenticates with the
  repository secret `SIGNPATH_API_TOKEN`; the signing policy is `test-signing`
  by default (`release-signing` requires the production certificate). The
  Foundation requires the attribution "Free code signing provided by
  SignPath.io, certificate by SignPath Foundation" and signed binaries with
  product name and version metadata; `build.ps1` passes `Version`, `Product`
  ("Cedro Modern Dock") and `Company` ("Cedro") to `dotnet publish`, so the
  executable's file metadata carries the installer version.

## Gotchas & Pitfalls

- **Animate dock content independently of pointer styles**: rapidly replacing
  a style-triggered `RenderTransform` transition can replay enlargement after
  pointer exit. `DockItemButton` drives per-axis `ScaleTransform` transitions
  with local target values and keeps its transformed content out of hit testing.
- **A no-activate dock does not reliably emit deactivation on desktop clicks**:
  its menus need the menu-scoped desktop click observer in addition to Avalonia
  light dismissal. Native callbacks only queue work and always forward input.

- **Avalonia cannot bind a string to an image source at runtime**: `Source`
  must be a literal asset path or a `Bitmap`. `DockVisualStyle` supplies
  orientation-specific separator bitmaps shared by pinned separators and the
  separator preceding running apps.
- **Shortcut launch details belong to the shortcut, not just its EXE**:
  profiles, file arguments, and working directories are persisted explicitly.
  An item containing only an executable path cannot reconstruct these details;
  re-import the `.lnk` to capture them.
- **Icon tint state must be set before dock items are created**: in
  `UpdateDockUI`, `IconTinter.ActiveColor` is assigned before the item loop;
  otherwise every pinned icon lags one selection behind. Persistent
  `RunningAppViewModel` instances re-tint from their stored `OriginalIcon`.
- **Win+D cannot be defeated by subclassing alone**: the reliable mechanism is
  parenting the dock to the desktop window (Progman, or the WorkerW hosting
  `SHELLDLL_DefView`); the `SC_MINIMIZE`/`SIZE_MINIMIZED` subclass remains as
  defense in depth.
- **A window owned by the desktop is excluded from `EnumWindows`**: window
  enumeration helpers must also search children; the dock's own window is
  invisible to top-level-only tooling.
- **Never `DestroyIcon` shared icons**: `WM_GETICON`/class icons belong to the
  window; only icons from `PrivateExtractIcons` or `ImageList_GetIcon` are
  owned and destroyed.
- **UWP apps expose no icon through the normal paths**: the frame window
  (`ApplicationFrameHost`) has no icon resource and answers `WM_GETICON` with
  zero; the icon lives in the package manifest. The package root is found by
  walking up from the app exe until `AppxManifest.xml` exists (e.g. Windows
  Settings ships from `C:\Windows\ImmersiveControlPanel`), and the asset is
  `Square44x44Logo` (or `Square150x150Logo`/`Square71x71Logo`/`<Logo>`), with
  `.scale-N` / `_scale-N` variants preferred in descending size.
- **Avalonia `ComboBox` has no `SelectedValuePath`/`DisplayMemberBinding`**
  (WPF-isms): use an `ItemTemplate` and bind `SelectedItem` to an option
  record carrying a localized label and the enum value.
- **Avalonia 11.3 drag-and-drop uses `DataTransfer`/`DataTransferItem`, not the
  older `DataObject` API**: start a drag with `DragDrop.DoDragDropAsync(e, data,
  effects)` where `data` is a `DataTransfer` populated via
  `DataTransferItem.CreateText(...)`/`Add(...)`; check formats with
  `e.DataTransfer.Contains(DataFormat.Text)` and read text with
  `TryGetText()`. `DragOver` must set `e.DragEffects`, and the target element
  needs `DragDrop.AllowDrop="True"`.
- **`ColorView` comes from the `Avalonia.Controls.ColorPicker` package**, which
  is otherwise only a transitive dependency of Avalonia.Diagnostics
  (Debug-only); it must be referenced explicitly so Release builds compile.
- **Single-file publish leaves native libraries** (`libSkiaSharp.dll`,
  `libHarfBuzzSharp.dll`, `av_libglesv2.dll`) beside the exe; distribute the
  whole publish folder (e.g. as a zip).
- **Windows Smart App Control can block freshly built DLLs** with
  `FileLoadException 0x800711C7` for both the app and `testhost`; the file must
  be allowed from Windows Security → Protection history.
- **The preview popup fades via layered-window alpha**, and thumbnails are
  separate top-level windows: hover-keep-alive checks must include the native
  thumbnail windows, and placement clamps use `PixelRect`'s `X/Y` (there is no
  `Left`/`Top`).
- **The preview close button must be a native window**: DWM thumbnails
  composite above the Avalonia popup content, so an Avalonia control could
  never render over them. `PreviewCloseButtonWindow` is a circular top-level
  window: it is clipped to an ellipse with `SetWindowRgn`, made translucent via
  whole-window alpha (`SetLayeredWindowAttributes` `LWA_ALPHA`), and drawn in
  `WM_PAINT`; a per-window timer fades the alpha in/out on `Show`/`Hide`.
  Its hover/click is tracked with `WM_MOUSEMOVE`/`WM_MOUSELEAVE`, and because
  it is a separate window it must also be included in `IsPointOverPopup` and in
  the popup's cursor-position-based show/hide logic.
- **Localization property files are UTF-8 without BOM**; console output may
  garble accented characters (display-only). The properties bundle loads
  `en_US` first, so missing keys fall back to English silently — new keys
  should be added to all 21 bundles to keep translations complete.
- **The dock repositions dynamically** (drag or layout change): external
  tooling must query the window position at capture time rather than caching
  it.
- **The dock window is larger than the visible bar**: the window is
  `SizeToContent`, so nothing can draw past its edge; the bar carries a
  transparent margin (`MainWindowViewModel.ZoomHeadroom`, derived from the icon
  size, the active theme's `DockVisualStyle.HoverZoom`, and its shadow extent)
  where zoomed icons overflow. Window position/size and
  bar position/size therefore differ: static positioning passes the bar size
  and offsets the window by the headroom, persisted positions go through
  `MainWindow.DockBarPosition`, and the preview popup measures against
  `DockBar`. The headroom still belongs to the window, so clicks on it do not
  reach what lies beneath (the dock only ignores them for dragging). The
  headroom is rounded, not ceiled: `1.3 - 1` is inexact in floating point and
  `Math.Ceiling` would add a stray pixel.
- **The Fluent theme sets `ClipToBounds` on `ItemsControl`**: the dock's item
  lists override it to `False`; otherwise zoomed icons are cut at the bar's
  content edge (inside the padding). `DockItemButton` also overrides the
  inherited button clipping default: its enlarged content draws beyond the
  button's fixed bounds while its pointer hit area stays unchanged.
