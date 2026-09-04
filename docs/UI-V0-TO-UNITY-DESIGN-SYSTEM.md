# Bạn Bước UI Design System — v0.dev to Unity UI Toolkit

This document is the visual contract for the runtime UI. The archived React application in
`docs/vietnamese-memory-explorer-2` remains a read-only reference; the production UI is built
with Unity UI Toolkit in `HomeUiController`, `WalkUiController`, and `ARWalking.uss`.

## Product character

- Warm, optimistic, companion-led, and distinctly mobile.
- A map or photograph is the visual stage; compact floating controls and soft cards sit above it.
- Information is presented as friendly pills, illustrated wells, passport cards, and bottom sheets.
- The interface should feel polished without looking corporate or mechanically “dashboard-like.”

## Core palette

| Role | Unity RGB | Usage |
|---|---:|---|
| Warm background | `249, 246, 235` | Main page background |
| Card | `255, 253, 247` | Cards, sheets, floating navigation |
| Deep ink | `42, 63, 49` | Display text and icons |
| Muted ink | `105, 120, 109` | Supporting labels |
| Primary green | `84, 190, 107` | Primary actions and active state |
| Soft green | `232, 245, 229` | Selected surfaces and progress tracks |
| Blossom pink | `250, 218, 224` | Photo and reward actions |
| Sun yellow | `250, 220, 116` | Coins, highlights, celebration |
| Sky blue | `196, 229, 242` | Landmark and information surfaces |
| Destructive | `203, 84, 76` | Reset/end-walk emphasis |
| Border | `225, 226, 211` | Low-contrast card outlines |

## Typography

- Display: Baloo 2, bold. Use for page titles, important values, companion names, and buttons.
- Body: Nunito, bold in this Unity version because variable `font-weight` is unsupported in USS.
- Large numbers should dominate their card; supporting units and labels stay compact.
- Avoid all-caps paragraphs. Uppercase is reserved for small eyebrows and transient AR modes.

## Geometry and depth

- Runtime scaling uses `ScaleWithScreenSize` at a 720×1600 logical reference resolution. A
  1080×2400 validation capture therefore renders at exactly 1.5×. App UI DPI auto-correction must
  stay disabled so these proportions do not vary
  between the Editor and phones with missing or inaccurate DPI metadata.
- Reference design radius is 20 px on a 390 px-wide canvas. At the 1080 reference width, use
  roughly 48–64 px card radii, 32–44 px inner radii, and fully rounded pills.
- Main horizontal page gutter: 42–46 px.
- Use 1–3 px warm gray/green borders and a second offset/tinted layer for elevation.
- Unity 6000.3 does not support `box-shadow` or CSS `font-weight`; do not add either to USS.
- Interactive elements scale to 0.96–0.98 while pressed.

## Screen proportion contract

The authored USS values map to the 720px-wide logical screen. These are the target rendered ratios
used to judge whether the interface is too small or too crowded:

| Element | Authored size | Share of 720px width | 390px-equivalent |
|---|---:|---:|---:|
| Body text | 26 px | 3.6% | 14 px |
| Subtitle | 34 px | 4.7% | 18 px |
| Page title | 55–58 px | 7.6–8.1% | 30–31 px |
| Primary/icon button height | 82 px | 11.4% | 44 px |
| Bottom navigation height | 128 px | 17.8% | 69 px |
| Bottom navigation label | 33 px | 4.6% | 18 px |
| Navigation icon | 40 px | 5.6% | 22 px |
| Standard card padding | 30 px | 4.2% | 16 px |
| Landmark pin | 78 px | 10.8% | 42 px |

Long body copy should remain around 45–70 characters per line. Buttons must be at least the
equivalent of 44×44 px, labels may wrap but not clip, and the floating navigation plus safe-area
inset must remain below 12% of the 2400px validation-capture height.

## Shared components

- **Top status bar:** coin pill, distance/activity pill, circular profile/settings button.
- **Bottom navigation:** floating cream capsule with four equal tabs and Lucide-style line icons.
- **Primary button:** green full-width pill with white icon and display text.
- **Blossom button:** pink pill for photo, collection, and celebration actions.
- **Secondary button:** warm cream or soft-green pill with ink-colored icon/text.
- **Metric tile:** large display value over a compact muted label.
- **Illustrated card:** rounded art well paired with concise copy and a clear action.
- **Bottom sheet:** rounded top corners, drag handle, eyebrow, story content, and footer actions.

## Screen anatomy

- **Map:** floating status bar; centered location pill; teardrop landmark pins with distance chips;
  companion portrait at the player position; recenter/photo rail; walk card above navigation.
- **Companions:** title and AR Photo pill; one featured companion with stage dots and growth bar;
  compact owned grid; dashed locked rows under “Yet to meet.”
- **Shop:** title and coin pill; food rows with colored art wells, bilingual-style description,
  growth reward, and green price pill; companion picker as a bottom sheet.
- **Journey:** three summary tiles; stamp-passport grid; large photo timeline cards with overlaid date.
- **Landmark:** large hero art with distance pill; three differently tinted cultural story sections;
  stamp status and a single AR Memory call to action.
- **Walk result:** celebratory green surface, three metrics, growth rewards, and one collect action.
- **Activity:** circular daily goal, step/distance summary, rounded seven-day bars, average pill.
- **AR Memory:** dark translucent top controls, scanning frame, guide pill, story bottom sheet, dots,
  and a full-screen collected state.

## Runtime constraints

- Keep navigation, save data, rewards, mock/provider boundaries, and AR scene contracts unchanged.
- Icon color is controlled by `Image.tintColor` in C#, including SVG `VectorImage` content.
- Keep real CorgiAR companion thumbnails and current cultural/landmark artwork.
- The CorgiAR uGUI interaction HUD is teammate-owned. UI Toolkit owns the always-present exit and
  Landmark Memory overlays layered above it.
- All layouts must support the configured 1080×2400 reference and simulated safe areas.

## Art-well image fitting

Every photographic asset (companion, food, landmark, journey) sits in a colored "well" element that
owns the shape and the fit. The well needs `border-radius`, `overflow: hidden`, and `position: relative`.
The child `Image` must be `position: absolute; left: 0; right: 0; top: 0; bottom: 0;` (no `width`/`height`)
and use `ScaleMode.ScaleAndCrop`. This is the "always fill the frame" rule: if the art is smaller than
the frame, it zooms in to cover it; if it's bigger, it crops to fit — never leave a gap and never let it
overflow the frame's edges.

Two things bit this repeatedly before landing on the absolute-fill pattern above, so don't regress to
either:

- **`ScaleMode.ScaleToFit` on a well-bound image.** Because USS `border-radius` is an absolute pixel
  value, a gap between an undersized "contain"-scaled image and its rounded/circular well exposes the
  well's own background around the art, which reads as the artwork itself having gone round or
  blob-shaped instead of staying a clean rectangle.
- **`width: 100%; height: 100%;` on the image instead of absolute-fill insets.** Percentage sizing on a
  UI Toolkit `Image` does not reliably fill its parent in this Unity version — it can leave an
  unpredictable gap on the right/bottom edge even when the well itself is sized correctly. Absolute
  positioning with all four insets is the only technique confirmed to fill the frame exactly; every
  other correctly-filling image in this file (`map-image`, `landmark-hero`, `journey-memory-image`)
  already used this technique, which is what gave it away.
- **A well whose `overflow` is anything but `hidden`.** `border-radius` on the image itself does
  nothing for the drawn texture unless an ancestor establishes a clip with `overflow: hidden` — a
  parent with `overflow: visible` (e.g. one that also hosts a pulse/glow effect meant to bleed outside
  its bounds, like the map's player marker) silently defeats the image's own radius. Give the clipped
  photo its own inner well so the bleeding effect can stay a sibling with `overflow: visible` instead of
  a shared ancestor.

Apply this to every art well, including backdrop/halo-style ones: `player-map-marker` now holds a
`player-avatar-well` sibling to the pulse ring so the avatar fills and clips correctly while the ring
still bleeds outward, and `featured-portrait-well` and `starter-reveal`/`reveal-companion` fill and crop
to their well rather than floating inset with a visible gap.
