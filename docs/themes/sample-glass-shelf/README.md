# Glass Shelf (sample)

A sample third-party theme for Cedro Modern Dock that draws the dock from
images instead of colors, like classic RocketDock / ObjectDock skins.

| File | Size | Used by |
|------|------|---------|
| `background.png` | 120 x 60 | `dock.image`, 9-slice with `slice: [20, 20, 20, 20]` |
| `indicator.png` | 32 x 16 | `indicator.style: "image"` |
| `separator.png` | 8 x 64 | `separator.image` (horizontal dock: a vertical bar) |
| `separator-vertical.png` | 64 x 8 | `separator.imageVertical` (vertical dock) |

`dock.background` is fully transparent (`#00000000`) so only the skin is
visible: without it the user's dock color would be painted under the image.

## Install

- import `sample-glass-shelf.cedrotheme` from
  Settings > Themes > Import a theme..., or
- copy this folder to `%APPDATA%\CedroModernDock\Themes\` and click Refresh
  in Settings > Themes.

## Make your own

Copy this folder, change `id` and `name` in `theme.json`, replace the
images, then refresh the gallery. The full format is documented in
`THEMES.md` (in the themes folder, or `docs/THEMES.md` in the repository).
