# Cedro Modern Dock themes

A theme changes the whole look of the dock in one click: background (color,
gradient or a skin image), border, shadow, hover zoom, running indicator,
separators, tooltips and right-click menus. Anyone can make one, in the
spirit of RocketDock and ObjectDock skins.

A theme is a folder containing a `theme.json` file and, optionally, images.
Every property is optional: whatever a theme leaves out keeps the user's own
setting or the Cedro default.

## Contents

1. [Quick start](#quick-start)
2. [Where themes live](#where-themes-live)
3. [File format](#file-format)
4. [Colors](#colors)
5. [Property reference](#property-reference)
6. [Image skins (9-slice)](#image-skins-9-slice)
7. [Validation and warnings](#validation-and-warnings)
8. [Packaging and sharing](#packaging-and-sharing)
9. [Complete annotated example](#complete-annotated-example)
10. [Tips for good-looking themes](#tips-for-good-looking-themes)

## Quick start

The fastest way: open **Settings > Themes**, set the dock up the way you
like, then click **Create from my settings**. Cedro writes a new theme
folder from the current look and opens it, ready to edit.

By hand:

1. In **Settings > Themes**, click **Open themes folder**. It opens
   `%APPDATA%\CedroModernDock\Themes` (this guide is copied there too).
2. Create a folder named after your theme id, for example `midnight`.
3. Inside it, create `theme.json`:

   ```json
   {
     "schemaVersion": 1,
     "id": "midnight",
     "name": "Midnight",
     "author": "Your name",
     "dock": {
       "background": "#E6101826",
       "cornerRadius": 16,
       "borderColor": "#33FFFFFF"
     },
     "indicator": { "style": "glow", "color": "#FF6FB8FF", "size": 6 }
   }
   ```

4. Back in **Settings > Themes**, click **Refresh**. Your theme appears in
   the gallery; click it to apply it.

Edit the file and click **Refresh** again to see your changes (images are
reloaded too).

## Where themes live

| What | Location |
|------|----------|
| Installed themes | `%APPDATA%\CedroModernDock\Themes\<id>\theme.json` |
| Images | Anywhere inside the theme folder, referenced by relative path |
| This guide | `%APPDATA%\CedroModernDock\Themes\THEMES.md` |

Each sub-folder of the themes folder that contains a `theme.json` is loaded.
Installing a package always puts the theme in a folder named after its `id`,
so name your folder like the id.

Built-in themes are part of the application and cannot be edited or deleted.
Their ids are reserved: `cedro`, `givre`, `ardoise`, `cristal`, `acrylique`,
`corail`, `hologramme`, `braise`, `noyer`, `velours`, `aurore`. The `cedro`
theme is the original look: it takes every value from the user's settings.

## File format

`theme.json` is a JSON object. The parser is lenient:

- `//` and `/* */` comments are allowed, and so are trailing commas.
- Property names are case-insensitive (`cornerRadius` = `CornerRadius`).
- Unknown properties are ignored.

But a value of the wrong JSON type makes the whole file invalid (for example
a number written as a string, `"cornerRadius": "12"`, or a decimal where an
integer is expected, `"cornerRadius": 12.5`). Integer properties are marked
`int` in the tables below.

### Metadata

| Property | Type | Required | Meaning |
|----------|------|----------|---------|
| `schemaVersion` | int | no (default `1`) | Format version. This build understands `1`; a higher value is rejected ("needs a newer Cedro Modern Dock"). |
| `id` | string | **yes** | Unique id, also the install folder name. 2 to 64 characters: lowercase letters `a-z`, digits, `.`, `_`, `-`; must start with a letter or digit (regex `^[a-z0-9][a-z0-9._-]{1,63}$`). |
| `name` | string | **yes** | Display name in the gallery. Trimmed, cut to 60 characters. |
| `author` | string | no | Cut to 60 characters. |
| `version` | string | no | Free text, for example `1.2.0`. Cut to 20 characters. |
| `description` | string | no | Cut to 300 characters. |
| `website` | string | no | Must be an absolute `http://` or `https://` URL, otherwise dropped with a warning. |

The visual sections are `dock`, `icons`, `indicator`, `separator`,
`tooltip` and `menu`. Each one, and each property inside it, is optional.

## Colors

Every color property accepts:

| Form | Example | Notes |
|------|---------|-------|
| `#RGB` | `#4CF` | Each digit doubled; opaque. |
| `#ARGB` | `#84CF` | Alpha first. |
| `#RRGGBB` | `#44CCFF` | Opaque. |
| `#AARRGGBB` | `#8044CCFF` | **Alpha first** (not CSS `#RRGGBBAA`). |
| `rgb(r, g, b)` | `rgb(68, 204, 255)` | 0..255 each; opaque. |
| `rgba(r, g, b, a)` | `rgba(68, 204, 255, 0.5)` | `a` from 0 to 1. |
| `r, g, b` | `68, 204, 255` | Same form as `config.json`; opaque. |

Named colors (`white`, `transparent`...) are not supported: use
`#FFFFFFFF` or `#00000000`. An invalid color is dropped with a warning and
the default applies.

## Property reference

"Default" is what the dock uses when the property is left out. Values
outside the given range are clamped to it.

### `dock`

The bar itself. It is drawn in layers, bottom to top: background (color or
gradient) with border and shadow, then the skin image, then the highlight
line, then the icons.

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `background` | color | | user's dock color | Solid background. Ignored when `gradient` is set. |
| `gradient` | color[] | 2+ colors | none | Linear gradient, stops evenly spaced. Fewer than 2 valid colors: ignored with a warning. |
| `gradientAngle` | number | -360..360 | `90` | Gradient direction in degrees: `0` = left to right, `90` = top to bottom. |
| `opacity` | number | 0..1 | see below | Opacity of the background, multiplied with the colors' own alpha. |
| `cornerRadius` | int | 0..60 | user's rounding | Corner radius of the bar. |
| `padding` | int | 0..40 | user's inner margin | Space between the bar edge and the icons. |
| `borderColor` | color | | no border | Border color. |
| `borderThickness` | number | 0..6 | `1` | Border width. Only used when `borderColor` is set. |
| `highlight` | color | | none | 1 px line along the inside top edge (glass reflection). |
| `shadow` | object | | none | Drop shadow, see below. |
| `image` | object | | none | 9-slice skin image, see [Image skins](#image-skins-9-slice). |

**Transparency rules.** The user's transparency slider only applies to
backgrounds the theme leaves open:

- Theme sets neither `background` nor `gradient`: the user's dock color is
  used, with the user's transparency slider (or `opacity` if set).
- Theme sets `background` or `gradient`: the theme owns the alpha. The
  colors' own alpha is used as-is and the slider no longer applies, unless
  `opacity` is set, in which case `opacity` multiplies the colors' alpha.

`cornerRadius` and `padding` fall back to the user's settings when left out.
Settings > Themes tells the user which of their settings the active theme
overrides.

#### `dock.shadow`

The shadow is drawn only when `color` is set (a shadow without a valid
color is dropped).

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `color` | color | | (required) | Shadow color, usually black with low alpha. |
| `blur` | number | 0..40 | `16` | Blur radius. |
| `spread` | number | -20..20 | `0` | Grows (or shrinks) the shadow. |
| `offsetX` | number | -20..20 | `0` | Horizontal offset. |
| `offsetY` | number | -20..20 | `4` | Vertical offset. |

#### `dock.image`

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `path` | string | | (required) | Image file, relative to the theme folder. Without a valid path the whole `image` object is dropped. |
| `slice` | number[4] | each 0..512 | a third of the image's smaller side | `[left, top, right, bottom]` borders in image pixels that are not stretched. Any other shape: ignored with a warning. |
| `opacity` | number | 0..1 | `1` | Opacity of the skin image. |

### `icons`

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `hoverZoom` | number | 1.0..1.8 | `1.3` | Icon magnification on hover. `1.0` disables the zoom. |

### `indicator`

The mark under (or beside) the icons of running applications.

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `style` | string | `dot`, `line`, `pill`, `glow`, `image`, `none` | `dot` | Shape, see below. |
| `color` | color | | `#FFFFFFFF` | Color of the shape styles. |
| `size` | number | 2..16 | `4` | Base size in pixels. |
| `image` | string | | none | Image file for `style: "image"`. |

| Style | Drawn as |
|-------|----------|
| `dot` | Circle, `size` x `size`. |
| `line` | Rounded bar, 42% of the icon size wide, `max(2, size x 0.6)` tall. |
| `pill` | Capsule, `size x 3.2` wide, `size` tall. |
| `glow` | Circle of `size` with a soft halo of the same color. |
| `image` | `indicator.image`, fitted (aspect ratio kept) in a box 50% of the icon size wide and `max(4, size x 1.5)` tall. |
| `none` | Nothing; no space is reserved under the icons. |

`style: "image"` without a valid `image` path falls back to `dot` with a
warning. Draw indicator images about twice the size they will be displayed
at so they stay sharp on high-DPI screens.

### `separator`

The divider between groups of icons and before running applications.

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `style` | string | `classic`, `line`, `dots`, `image`, `space` | `classic` | Look, see below. |
| `color` | color | | `#55FFFFFF` | Color of `line` and `dots`. |
| `thickness` | number | 1..8 | `1` | Line width, or dot size minus 2. |
| `image` | string | | none | Image for a horizontal dock (a vertical bar). |
| `imageVertical` | string | | `image` | Image for a vertical dock (a horizontal bar). |

| Style | Drawn as |
|-------|----------|
| `classic` | The original Cedro separator images. `color`, `thickness` and the images are ignored. |
| `line` | Straight line, 72% of the icon size long, `thickness` wide. |
| `dots` | Three dots of `thickness + 2` pixels. |
| `image` | Your image, fitted (aspect ratio kept) in a slot 22 px wide (along the dock) and as tall as the icons. |
| `space` | An empty gap. |

`style: "image"` without a valid `image` path falls back to `line` with a
warning.

### `tooltip`

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `background` | color | | `menu.background` | Tooltip background. |
| `foreground` | color | | `menu.foreground` | Tooltip text. |
| `borderColor` | color | | `menu.borderColor` | Tooltip border. |
| `cornerRadius` | int | 0..20 | `7` | Tooltip corner radius. |

### `menu`

The right-click menus of the dock and its icons (sub-menus included).

| Property | Type | Range | Default | Meaning |
|----------|------|-------|---------|---------|
| `background` | color | | `#FF1C1C20` (dark) | Menu background. Always drawn opaque: its alpha is ignored, so text behind the menu never shows through. The same applies to `tooltip.background`. |
| `foreground` | color | | `#FFF0F0F0` | Item text. |
| `secondaryForeground` | color | | `foreground` at 60% alpha | Secondary text, shortcuts, sub-menu chevrons. |
| `hover` | color | | `foreground` at 12% alpha | Hovered item background. |
| `accent` | color | | `#FF4C9AFF` | Accent color, used for the dashed outline of the dock in edit mode (icons unlocked). |
| `borderColor` | color | | `foreground` at 14% alpha | Menu border. |
| `danger` | color | | `#FFFF6B6B` | Text of destructive items. |
| `cornerRadius` | int | 0..20 | `10` | Menu corner radius. Items use `radius - 4`, at most 8. |

The menu separator line and disabled text are derived from `foreground`.

## Image skins (9-slice)

`dock.image` draws the bar from a picture, like RocketDock and ObjectDock
skins. The image is cut into nine parts by the four `slice` values
(`[left, top, right, bottom]`, in image pixels):

```
        left            right
       |<-->|          |<-->|
       +----+----------+----+  ---
       | 1  |    2     | 3  |   top
       +----+----------+----+  ---
       |    |          |    |
       | 4  |    5     | 6  |
       |    |          |    |
       +----+----------+----+  ---
       | 7  |    8     | 9  |   bottom
       +----+----------+----+  ---
```

- Corners **1 3 7 9** are drawn at their original size, never stretched.
- Edges **2 8** stretch horizontally, **4 6** stretch vertically.
- The center **5** stretches both ways.

So whatever the number of icons, the rounded corners and the borders keep
their shape. Rules:

- Put everything that must not distort (rounded corners, rims, a front lip,
  highlights near the corners) inside the slice borders. With a corner
  radius of 14 px, a slice of 20 leaves a safe margin.
- Keep the stretched parts (2, 4, 5, 6, 8) uniform along the stretch
  direction: a vertical gradient is fine in 2, 5 and 8; avoid details there.
- One image pixel is drawn as one device-independent pixel (for an image
  saved at 96 DPI, the usual default). If the dock is
  smaller than `left + right` (or `top + bottom`), the corners are scaled
  down to fit.
- Slices larger than the image are clamped to it. Without `slice`, a third
  of the image's smaller side is used on all four sides.
- The skin is drawn **over** the background layer. To show only the skin,
  set `"background": "#00000000"`; otherwise the user's dock color is
  painted underneath it. Transparent pixels of the image let the desktop
  show through.
- `cornerRadius` does not clip the image; match it to your artwork, since it
  is still used for the border, the shadow and the edit-mode outline.
- `highlight` and `borderColor` are drawn too, so leave them out if the
  artwork already has its rim.

A small image is enough: 100 to 200 px wide and as tall as needed for the
corners plus a few pixels of center.

### Image files

- Formats: `png`, `jpg`, `jpeg`, `bmp`, `webp`, `gif` (PNG recommended:
  it keeps transparency).
- Paths are relative to the theme folder: `background.png`,
  `images/indicator.png`. Absolute paths, drive letters and `..` are
  rejected with a warning.
- A path that is valid but points to a missing or unreadable file is
  silently skipped: the skin is not drawn, an image separator leaves an
  empty gap, an image indicator is replaced by a plain bar in
  `indicator.color`. No warning is shown for this case, so double-check
  your file names.

## Validation and warnings

When themes are loaded, two kinds of problems are reported in
**Settings > Themes**:

- **Errors** reject the theme: it does not appear in the gallery and is
  listed under "Some installed themes could not be loaded". Causes: invalid
  JSON (including a wrong value type), missing or invalid `id`, missing
  `name`, `schemaVersion` above 1, `theme.json` over 256 KB, or an `id`
  already used by another theme (built-in or installed).
- **Warnings** keep the theme: the faulty value is dropped and its default
  applies. They are shown on the theme's card. Causes: invalid color,
  unknown `style`, gradient with fewer than 2 colors, malformed `slice`,
  unsafe or unsupported image path, invalid `website`, image style without
  an image.

Out-of-range numbers are clamped silently (no warning).

## Packaging and sharing

A package is a zip file renamed to `.cedrotheme` (a plain `.zip` works
too). Users install it with **Settings > Themes > Import a theme...**, which
also accepts a `theme.json` file directly (its folder is copied).
**Export** on a theme card creates a package for you.

Package layout, either:

```
midnight.cedrotheme              midnight.cedrotheme
  theme.json                       midnight/
  background.png        or           theme.json
  indicator.png                      background.png
                                     indicator.png
```

`theme.json` must be at the root of the zip or inside one top-level folder.

Limits and rules:

- At most 200 entries and 25 MB uncompressed; `theme.json` at most 256 KB.
- Only these file types are extracted: `.json`, `.png`, `.jpg`, `.jpeg`,
  `.bmp`, `.webp`, `.gif`, `.txt`, `.md`. Everything else is skipped, so a
  `README.md` or `LICENSE.txt` is welcome, executables are not.
- Entries with unsafe paths (absolute, `..`) are skipped.
- The package is validated before anything is written; the theme is
  installed to `Themes\<id>` and activated.
- Installing a theme with the **same id** as an installed one replaces it
  (that is how updates work: keep the id, bump `version`).
- Packages using a built-in id are refused.

To create a package by hand on Windows: right-click the theme folder >
Compress to ZIP file, then rename `midnight.zip` to `midnight.cedrotheme`.

## Complete annotated example

Comments are allowed in `theme.json`, so this file is valid as-is (with its
images next to it).

```jsonc
{
  "schemaVersion": 1,
  "id": "aurora-glass",            // folder name and unique id
  "name": "Aurora Glass",
  "author": "Jane Doe",
  "version": "1.0.0",
  "description": "Frosted glass with a northern-lights gradient.",
  "website": "https://example.com/aurora-glass",

  "dock": {
    // Gradient from top to bottom; the colors' alpha is used and the
    // user's transparency slider no longer applies.
    "gradient": ["#B31B2A4A", "#991A3D5C", "#B3123048"],
    "gradientAngle": 90,
    // "opacity": 0.9,             // would multiply the alpha above
    "cornerRadius": 18,
    "padding": 10,
    "borderColor": "#40FFFFFF",
    "borderThickness": 1,
    "highlight": "#80FFFFFF",      // glass reflection on the top edge
    "shadow": {
      "color": "#59000000",
      "blur": 18,
      "offsetY": 6
    }
    // A skin image would go here:
    // "image": { "path": "background.png", "slice": [20, 20, 20, 20] }
  },

  "icons": {
    "hoverZoom": 1.45
  },

  "indicator": {
    "style": "glow",               // dot | line | pill | glow | image | none
    "color": "#FF7CF5D0",
    "size": 5
  },

  "separator": {
    "style": "dots",               // classic | line | dots | image | space
    "color": "#80FFFFFF",
    "thickness": 2
  },

  "tooltip": {
    "background": "#F0122033",
    "foreground": "#FFEAF6FF",
    "borderColor": "#407CF5D0",
    "cornerRadius": 8
  },

  "menu": {
    "background": "#FF101A2A",     // menus are always drawn opaque
    "foreground": "#FFEAF2FA",
    "secondaryForeground": "#FF9FB3C8",
    "hover": "#2E7CF5D0",
    "accent": "#FF7CF5D0",
    "borderColor": "#33FFFFFF",
    "danger": "#FFFF7A7A",
    "cornerRadius": 10
  }
}
```

A complete image-skin sample (9-slice background, image indicator, image
separators) is available in the repository under
`docs/themes/sample-glass-shelf`, with a ready-to-import
`sample-glass-shelf.cedrotheme`.

## Tips for good-looking themes

- **Menu contrast.** Menus and tooltips sit on top of any window or
  wallpaper, so their backgrounds are always drawn opaque. Aim for strong
  contrast between `foreground` and `background` (dark text on light, or
  light text on dark). Check `secondaryForeground`, `hover` and `danger`
  against the background too.
- **Shadow vs headroom.** The dock window keeps transparent space around
  the bar, on every side, for the larger of the hover zoom growth and the
  shadow extent (`blur + spread + max(|offsetX|, |offsetY|)` pixels). A huge
  shadow pushes the bar away from the screen edge; blur 12 to 20 and offset
  4 to 6 usually look best.
- **Hover zoom vs headroom.** The zoom growth is
  `iconSize x (hoverZoom - 1) / 2` per side. Very high zoom on large icons makes
  the dock take more room.
- **Test on light and dark wallpapers.** Translucent themes change a lot
  with the wallpaper. A thin `borderColor` or `highlight` keeps the bar's
  shape visible on both.
- **Test both orientations.** Docks can sit on any screen edge; provide
  `separator.imageVertical` when your separator image is directional.
- **Test small and large icons.** Indicators and separators scale with the
  icon size; skin corners do not.
- **Let users keep control.** Leave out `cornerRadius`, `padding` or
  `opacity` if your design works with any value: users keep their sliders.
- **Indicator visibility.** The indicator must read against the bar and
  the wallpaper: a bright `glow` on dark docks, a dark `dot` on light ones.
- **Keep packages small.** A few PNGs of a few KB each are enough;
  optimize them before sharing.
- **Edit, Refresh, repeat.** Click **Refresh** in Settings > Themes after
  each change; warnings on the card tell you exactly which value was
  ignored.
