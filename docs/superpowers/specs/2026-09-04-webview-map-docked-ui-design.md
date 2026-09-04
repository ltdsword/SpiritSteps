# WebView Map with Docked UI - Design

## History

This is the third architecture attempted for the Map page's real map rendering:

1. **Custom vector-tile compositor** (mesh/shader based). Mobile GPU overdraw
   made it unusably slow on-device; abandoned after real-device testing.
2. **Raster-tile compositor** (`feature/raster-tile-map`, kept as-is, not
   merged). A UI-Toolkit DOM-tile-grid of MapTiler PNG tiles, hand-rolled
   pan/zoom/prefetch/eviction. Got tiles on screen, but repeatedly needed
   real-device testing to catch bugs a description can't (a float32
   precision bug that silently corrupted tile indices at real zoom levels,
   a wrong MapTiler URL shape, a tile-availability flag that gave up on the
   whole map after a single stray failure, and finally a genuine crash from
   two compounding causes: superseded network requests never cancelled
   during fast pan/zoom, and evicted tile textures never `Destroy()`ed -
   both root-caused and fixed, but the pattern - reimplementing a mature
   map library's pan/zoom/tile-loading/eviction logic by hand - kept
   producing exactly the class of bug real map libraries solved a decade
   ago).
3. **Native WebView hosting a real map library (this design)**. A previous
   session found that `gree/unity-webview` (the free, open-source WebView
   plugin fitting this project's budget) renders as a native Android `View`
   overlay with no texture-render path on Android - it always paints above
   Unity's own UI Toolkit surface, so anything UI Toolkit draws "on top" of
   the map is actually hidden behind it. That blocked this approach before.
   The fix isn't a different plugin - it's not needing anything to overlap
   the WebView in the first place: dock the existing floating panels to the
   screen edges instead, and size the WebView to exactly the rectangle left
   over.

   A separate prototype (`D:\Unity\Projects\PedometerPrototype`,
   `Assets/Prototype_MapPOI`) independently reached the same plugin choice
   and validates the rest of this design end-to-end: OpenFreeMap (free, no
   API key - MapLibre GL JS against `https://tiles.openfreemap.org/styles/
   liberty`) hosted via `gree/unity-webview`, a JS↔C# bridge, and -
   critically - `WebViewObject.SetMargins(left, top, right, bottom)`, which
   shrinks the native WebView to a sub-rectangle of the screen in raw
   pixels. That's the exact mechanism docking needs. The prototype also
   already diagnosed and fixed a Unity 6-specific Android bug (see
   "Android hardware-acceleration fix" below) that would otherwise make the
   map render solid black.

Net effect versus the raster-tile approach: pan, zoom, tile loading/caching
and label rendering are no longer this project's problem - MapLibre GL JS
(a real, maintained map library) owns all of it. What's left to build is
UI layout (docking) and a thin bridge (state in, marker-tap-and-recenter
out).

## Architecture

Three layers, replacing the raster-tile branch's compositor layer with a
WebView:

1. **Docked UI layer** (UI Toolkit, `HomeUiController.BuildMap`). The
   existing floating top bar (greeting + settings) and bottom bar (stats +
   actions) expand to the screen edges - full width, safe-area-padded -
   instead of floating with margins over a full-bleed map. They sit in
   normal document flow, above and below an empty middle region reserved
   for the map.
2. **WebView bridge layer** (`WebViewMapView`, a `MonoBehaviour`). Owns the
   `WebViewObject` lifecycle, stages the bundled HTML into a loadable URL,
   measures the docked bars' resolved height plus `Screen.safeArea` and
   calls `SetMargins` so the native view exactly fills the leftover
   rectangle, pushes player/marker state to JS, receives marker-tap events
   back, and hides/shows the native view when a modal/toast opens over the
   Map page (since a modal is UI Toolkit content that would otherwise be
   hidden behind the WebView too).
3. **Map page** (`Assets/StreamingAssets/spiritsteps_map.html`). A bundled
   MapLibre GL JS page against the OpenFreeMap `liberty` style. Owns pan,
   zoom, rotation, tile loading/caching, and marker/label rendering
   entirely - none of that is reimplemented in C#. Reports marker taps back
   through the bridge; recenter is a JS-side button using the last state
   already pushed from C# (no separate browser geolocation permission).

## Components

**Recovered from `feature/raster-tile-map` via `git show` (renderer-
agnostic, already proven working):**
- `Assets/_Project/Scripts/Services/Geo/GeoPoint.cs`
- `Assets/_Project/Scripts/Services/Geo/GeoMath.cs`
- `Assets/_Project/Scripts/Services/Geo/DeviceLocationService.cs`
- `Assets/_Project/Scripts/Services/Geo/LandmarkGeoCatalog.cs`
- `Assets/_Project/Scripts/Services/Geo/DeviceStepCounterService.cs`
- `Assets/_Project/Scripts/Services/Geo/RealLandmarkMapProvider.cs`
- `Assets/_Project/Scripts/Services/Geo/RealWalkMetricsProvider.cs`

**New:**
- `Assets/_Project/Scripts/Services/Map/WebViewMapMargins.cs` - a pure,
  static function: given the top bar's and bottom bar's resolved heights
  and `Screen.safeArea`, returns the `(left, top, right, bottom)` pixel
  margins to pass to `SetMargins`. Kept pure and separate from the
  MonoBehaviour specifically so it's EditMode-testable without a real
  screen/WebView.
- `Assets/_Project/Scripts/UI/Comprehensive/WebViewMapView.cs` - the bridge
  MonoBehaviour: `Initialize()`, `SetMargins(int left, int top, int right,
  int bottom)`, `Render(GeoPoint player, IReadOnlyList<MapMarkerUiData>
  markers)`, `event Action<string> OnMarkerTapped`, `SetActive(bool)`,
  `bool IsAvailable` + `event Action AvailabilityChanged` (mirrors the
  raster branch's fallback contract, so `HomeUiController` doesn't need a
  second pattern for "renderer failed, show the illustrated map instead").
- `Assets/StreamingAssets/spiritsteps_map.html` - adapted from the
  prototype's `openfreemap_map.html`: same MapLibre/OpenFreeMap setup,
  plus (a) marker-tap detection that reports a landmark id through the
  bridge instead of raw lat/lon, (b) a recenter control that re-centers on
  the last state pushed from C#, (c) MapLibre's built-in zoom control.
- `Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs` -
  ported from the prototype (see "Android hardware-acceleration fix"
  below), adapted comment to reference this project.

**Modified:**
- `Packages/manifest.json` - add `"net.gree.unity-webview":
  "https://github.com/gree/unity-webview.git?path=/dist/package"`.
- `Assets/_Project/Scripts/UI/Comprehensive/HomeUiController.cs` -
  `BuildMap()` rewritten: drop the illustrated-map viewport, its
  manipulator, and the per-marker button loop entirely (markers now live
  in JS); build the docked top/bottom bars; after layout, measure them and
  call `webViewMapView.SetMargins(...)` (via `WebViewMapMargins`); wire
  `OnMarkerTapped` to the existing `OpenMarker` flow; wire
  `ShowOverlay`/toast display to `webViewMapView.SetActive(false)` and
  restore on dismiss.
- `Assets/_Project/Resources/UI/ARWalking.uss` - replace `.map-top-overlay`
  / `.map-stats` / `.map-controls` (all `position: absolute`) with
  `.map-top-bar` / `.map-bottom-bar` (flex, full width, in document flow,
  safe-area-aware padding). The floating GPS/recenter/help column is
  dropped: recenter and zoom move into the WebView, and "help" becomes a
  compact icon button folded into the bottom bar.

## Android hardware-acceleration fix

Unity 6's Android build template emits two `<activity>` entries with a
MAIN/LAUNCHER intent-filter: the legacy `UnityPlayerActivity` (first in the
manifest, `android:enabled="false"`) and `UnityPlayerGameActivity` (the one
actually enabled and launched). `gree/unity-webview`'s own
`IPostGenerateGradleAndroidProject` step only patches the *first* matching
activity, so it sets `hardwareAccelerated="true"` on the disabled legacy
activity while the real, enabled one is left at Unity's generated default
of `false`. A window without hardware acceleration forces every view it
hosts - including a `WebViewObject`'s embedded native WebView - into
software compositing, so MapLibre's WebGL canvas can't get a working GL
context and renders solid black (ordinary DOM/CSS content in the same page
still paints fine, which is what makes this confusing to diagnose from a
screenshot alone).

`ForceHardwareAcceleratedActivities` is a second post-processor
(`callbackOrder => 10`, running after gree's) that forces
`hardwareAccelerated="true"` on every `<activity>` in the generated
manifest, so whichever one Unity actually enables ends up correct
regardless of ordering. Already diagnosed and fixed in the
`PedometerPrototype` reference project; ported here as-is.

## Data flow

- GPS fix (`DeviceLocationService`) → `HomeUiController` → `WebViewMapView
  .Render(player, markers)` → `EvaluateJS("window.mapBridge.update(" +
  json + ")")`. First render also centers/zooms the map on the player.
- Landmark pin tap (JS) → `Unity.call("marker," + landmarkId)` → parsed in
  `WebViewMapView` → `OnMarkerTapped` → `HomeUiController.OpenMarker(id)`.
- Recenter tap (JS) → purely internal to the page: re-centers on the last
  state `mapBridge.update` received. No bridge round-trip, no separate
  geolocation permission prompt.
- Overlay/modal open (`ShowOverlay`, toast) → `webViewMapView
  .SetActive(false)` (hides the native view so the modal isn't hidden
  behind it) → on dismiss → `SetActive(true)` + a fresh `SetMargins` call,
  since bar content (e.g. greeting text length) could have changed its
  resolved height while hidden.
- Viewport geometry change (bar resize, safe-area change) → recompute and
  re-apply margins via `WebViewMapMargins`.

## Error handling

- WebView init or HTML-staging failure → `WebViewMapView.IsAvailable`
  becomes `false`, `AvailabilityChanged` fires, `HomeUiController` falls
  back to the existing static illustrated map - the same contract the
  raster-tile branch already established, so no new fallback pattern.
- No GPS fix yet → the map still renders (centered on
  `DeviceLocationService.DefaultCenter`), just without a player marker
  until a fix arrives - matches existing `DeviceLocationService` behavior.
- Location permission denied → same as today: `DeviceLocationService`
  logs a warning and stops; the map still shows, centered on the default
  point, with no player marker.

## Testing strategy

- **EditMode:** geo math (already covered by the recovered
  `GeoProviderEditModeTests`), plus new tests for `WebViewMapMargins` as a
  pure function (given bar heights + a safe-area rect, assert the expected
  `(left, top, right, bottom)`).
- **PlayMode:** `WebViewObject` is a real native plugin - not something to
  fake convincingly in a test double, and not something to unit-test here.
  PlayMode tests target the logic around it instead: `HomeUiController`'s
  wiring (marker-tap → `OpenMarker`, overlay-open → `SetActive(false)`),
  using a small test seam on `WebViewMapView` (an injectable interface for
  the bridge send/receive, mirroring the `IRasterMapTileSource` test-seam
  pattern from the raster branch) so these tests don't require a real
  WebView.
- **Manual verification** (now possible in the Windows Editor via
  WebView2, not just on-device - a real iteration-speed win over the
  raster branch, which required a full Android build/deploy cycle for
  every check): map loads and shows OpenFreeMap tiles; player marker
  appears once a fix arrives and updates on subsequent fixes; landmark
  markers appear and tapping one opens its detail screen; recenter button
  works; pinch-zoom/pan/rotate feel smooth; opening Settings/Permissions/a
  toast correctly hides the WebView and restores it on dismiss; top/bottom
  bars sit correctly inside the safe area on a notched device; WebView
  fills exactly the remaining rectangle with no gap or overlap against the
  docked bars.
