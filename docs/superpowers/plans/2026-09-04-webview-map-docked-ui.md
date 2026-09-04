# WebView Map with Docked UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Map page's illustrated-map-only rendering with a real map (MapLibre GL JS against the free OpenFreeMap style, hosted in a native WebView via `gree/unity-webview`), with the existing floating UI panels docked to the screen edges so nothing needs to overlap the WebView.

**Architecture:** Three layers - (1) docked UI Toolkit top/bottom bars in `HomeUiController` that measure their own resolved height, (2) `WebViewMapView`, a `MonoBehaviour` owning the native WebView's lifecycle/margins/JS bridge, behind a small `IWebViewBridge` seam so the surrounding logic is testable without a real WebView, (3) a bundled MapLibre HTML page that owns pan/zoom/tile-loading/marker-rendering entirely.

**Tech Stack:** Unity 6000.3.16f1, UI Toolkit + Unity App UI, C# MonoBehaviour+Coroutine async style, `gree/unity-webview` (`net.gree.unity-webview`, free/MIT, git package dependency), MapLibre GL JS 4.x against `https://tiles.openfreemap.org/styles/liberty` (free, no API key).

**Spec:** `docs/superpowers/specs/2026-09-04-webview-map-docked-ui-design.md`

## Global Constraints

- No API key or paid service anywhere in this feature - OpenFreeMap requires none.
- `gree/unity-webview` is a native `View` overlay with no texture-render path on Android - never attempt to render it "under" or "through" UI Toolkit content; only docking (via `SetMargins`) and `SetActive`/visibility toggling are valid ways to avoid overlap.
- Landmark ids used anywhere in this feature must match `PrototypeIds`/`PrototypeUiCatalog.asset`'s existing three landmarks exactly: `independence-palace`, `central-post-office`, `notre-dame-basilica`.
- `WebViewMapView` must expose the same `IsAvailable` / `AvailabilityChanged` fallback contract the (kept-as-is) `feature/raster-tile-map` branch established for `RasterMapView`, so `HomeUiController` has one fallback pattern, not two.
- Every step that touches `HomeUiController.cs` must preserve the existing illustrated-map code path as the fallback branch - never delete it.

---

### Task 1: Recover the Geo services layer

**Files:**
- Create: `Assets/_Project/Scripts/Services/Geo/GeoPoint.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/GeoMath.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/GeoToMapProjection.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/LandmarkGeoCatalog.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/DeviceLocationService.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/DeviceStepCounterService.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/RealLandmarkMapProvider.cs`
- Create: `Assets/_Project/Scripts/Services/Geo/RealWalkMetricsProvider.cs`
- Create: `Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs`
- Create: matching `.meta` files for every file above, plus `Assets/_Project/Scripts/Services/Geo.meta`

**Interfaces:**
- Produces: `GeoPoint` (`lat`, `lon`, `Equals`), `GeoMath.HaversineMeters(GeoPoint, GeoPoint)`, `GeoMath.BearingDegrees(GeoPoint, GeoPoint)`, `GeoToMapProjection` (3-anchor calibrated projection, `ProjectClamped(GeoPoint) -> Vector2`), `LandmarkGeoCatalog` (`ScriptableObject`, `List<LandmarkGeoData> landmarks`, `Find(string id) -> LandmarkGeoData`, `LandmarkGeoData.Location -> GeoPoint`), `DeviceLocationService` (`MonoBehaviour`, `static readonly GeoPoint DefaultCenter = new GeoPoint(10.7798, 106.6997)`, `bool HasFix`, `GeoPoint Current`, `event Action<GeoPoint> OnLocationUpdated`, `void Activate()`), `RealLandmarkMapProvider : ILandmarkMapProvider`, `RealWalkMetricsProvider : IWalkMetricsProvider`.

This entire task recovers code proven working on `feature/raster-tile-map` (commit `ab84abf`) - none of it is renderer-specific, all of it is reused as-is for the WebView map. `git show` reads a file from another branch without checking it out or touching the current working tree.

- [ ] **Step 1: Recover every Geo service file and its `.meta` from `feature/raster-tile-map`**

Run from the repo root:

```bash
mkdir -p Assets/_Project/Scripts/Services/Geo
for f in GeoPoint GeoMath GeoToMapProjection LandmarkGeoCatalog DeviceLocationService DeviceStepCounterService RealLandmarkMapProvider RealWalkMetricsProvider; do
  git show feature/raster-tile-map:Assets/_Project/Scripts/Services/Geo/$f.cs > Assets/_Project/Scripts/Services/Geo/$f.cs
  git show feature/raster-tile-map:Assets/_Project/Scripts/Services/Geo/$f.cs.meta > Assets/_Project/Scripts/Services/Geo/$f.cs.meta
done
git show feature/raster-tile-map:Assets/_Project/Scripts/Services/Geo.meta > Assets/_Project/Scripts/Services/Geo.meta
git show feature/raster-tile-map:Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs > Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs
git show feature/raster-tile-map:Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs.meta > Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs.meta
```

- [ ] **Step 2: Run the recovered EditMode tests and verify they pass**

Trigger the EditMode suite (via the Unity Test Runner API, same pattern used throughout this project's sessions) filtered to `GeoProviderEditModeTests`, or the full EditMode suite. Expected: all `GeoProviderEditModeTests` tests pass, no compile errors. If `DeviceLocationService.DefaultCenter` isn't `(10.7798, 106.6997)`, stop - the recovery didn't match what step 1 expects and Task 2's catalog coordinates below assume this exact value.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Services/Geo Assets/_Project/Scripts/Services/Geo.meta Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs Assets/_Project/Tests/EditMode/GeoProviderEditModeTests.cs.meta
git commit -m "feat: recover the real GPS/location provider layer for the webview map"
```

---

### Task 2: Author the real landmark coordinate catalog

**Files:**
- Create: `Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset`
- Create: `Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset.meta`
- Test: `Assets/_Project/Tests/EditMode/LandmarkGeoCatalogAssetEditModeTests.cs`

**Interfaces:**
- Consumes: `LandmarkGeoCatalog`, `LandmarkGeoData` (Task 1).
- Produces: a loadable `Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog")` asset with exactly the three landmarks this prototype has (`independence-palace`, `central-post-office`, `notre-dame-basilica`), each with real-world WGS84 coordinates and `isMapCalibrationAnchor = true` (all three - `RealLandmarkMapProvider`'s calibration needs exactly three non-collinear anchors, and this prototype only has three landmarks total).

This asset never existed even on `feature/raster-tile-map` (only the `LandmarkGeoCatalog.cs` *script* was committed there - `RealLandmarkMapProvider` silently fell back to the deterministic mock provider the whole time because `Resources.Load` returned null). The WebView map needs real coordinates to place pins, so this is now required, not optional.

- [ ] **Step 1: Write the failing test**

```csharp
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;

namespace ARWalking.Tests.EditMode
{
    public sealed class LandmarkGeoCatalogAssetEditModeTests
    {
        [Test]
        public void RealAsset_HasExactlyThreeCalibratedLandmarksMatchingPrototypeIds()
        {
            var catalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
            Assert.That(catalog, Is.Not.Null, "Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset is missing.");

            var expectedIds = new[] { PrototypeIds.IndependencePalace, PrototypeIds.CentralPostOffice, PrototypeIds.NotreDameBasilica };
            Assert.That(catalog.landmarks.Count, Is.EqualTo(3));
            foreach (var id in expectedIds)
            {
                var entry = catalog.Find(id);
                Assert.That(entry, Is.Not.Null, $"missing catalog entry for {id}");
                Assert.That(entry.isMapCalibrationAnchor, Is.True, $"{id} must be a calibration anchor - all three landmarks in this prototype must be");
                Assert.That(entry.latitude, Is.Not.EqualTo(0).Within(0.0001), $"{id} has a placeholder (0,0) coordinate");
            }
        }
    }
}
```

- [ ] **Step 2: Run it to confirm it fails**

Expected: FAIL with "Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset is missing." (the asset doesn't exist yet).

- [ ] **Step 3: Author the asset**

Write the file directly - this is the same YAML shape Unity itself would serialize for a `ScriptableObject` with a `List<LandmarkGeoData>` field, referencing `LandmarkGeoCatalog.cs`'s script GUID recovered in Task 1 (`22ca5c155fa4ef0449fdef90e22b67f1` - confirm this against the `.meta` file Task 1 actually recovered; if it differs, use the real one). Coordinates are real-world WGS84 for these three District 1, Ho Chi Minh City landmarks (Central Post Office matches `DeviceLocationService.DefaultCenter` exactly, as it already did as the informal "center of the demo area"):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 22ca5c155fa4ef0449fdef90e22b67f1, type: 3}
  m_Name: LandmarkGeoCatalog
  m_EditorClassIdentifier: ARWalking.Runtime::ARWalking.UI.LandmarkGeoCatalog
  landmarks:
  - id: independence-palace
    latitude: 10.7770
    longitude: 106.6953
    unlockRadiusMeters: 100
    isMapCalibrationAnchor: 1
  - id: central-post-office
    latitude: 10.7798
    longitude: 106.6997
    unlockRadiusMeters: 100
    isMapCalibrationAnchor: 1
  - id: notre-dame-basilica
    latitude: 10.7798
    longitude: 106.6990
    unlockRadiusMeters: 100
    isMapCalibrationAnchor: 1
```

Write the matching `.asset.meta` (any fresh valid GUID for the asset itself, not the script - use `Unity_RunCommand` with `AssetDatabase` to reimport/generate it if hand-authoring the meta is awkward, or generate a random 32-hex-char GUID by hand):

```yaml
fileFormatVersion: 2
guid: 9f1e6a2c4b7d4f0e8a3c5d6b7e8f9a0b
labels: []
NativeAssetType:
  MainObjectFullClassName: ARWalking.UI.LandmarkGeoCatalog
```

If Unity complains about the `NativeAssetType` block on import (it's optional metadata, not load-bearing), delete that block - what matters is `guid` matching what other assets/scripts reference (nothing references this asset's own guid yet, so any valid, unique GUID works).

- [ ] **Step 4: Run the test again and confirm it passes**

Also run the full EditMode suite to confirm no regressions and that the asset actually loads without a console error on domain reload.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset.meta Assets/_Project/Tests/EditMode/LandmarkGeoCatalogAssetEditModeTests.cs Assets/_Project/Tests/EditMode/LandmarkGeoCatalogAssetEditModeTests.cs.meta
git commit -m "feat: author real-world coordinates for the three prototype landmarks"
```

---

### Task 3: Add the gree/unity-webview package and the Android hardware-acceleration fix

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs`
- Create: `Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs.meta`

**Interfaces:**
- Produces: the `Gree.UnityWebView` namespace (`WebViewObject` and friends) becomes available to any script in the project; no other task depends on a specific symbol from this one beyond the package being resolved.

- [ ] **Step 1: Add the package dependency**

Edit `Packages/manifest.json`, adding this entry to `"dependencies"` (alphabetical position doesn't matter to Unity, but keep the file readable):

```json
"net.gree.unity-webview": "https://github.com/gree/unity-webview.git?path=/dist/package",
```

- [ ] **Step 2: Let Unity resolve the package and confirm no compile errors**

Trigger a domain reload / package resolve (e.g. via `Unity_RunCommand` calling `UnityEditor.PackageManager.Client.Resolve()`, or simply let the Editor pick up the manifest change). Confirm the Console has no errors and `Gree.UnityWebView.WebViewObject` is resolvable (e.g. compile a throwaway `using Gree.UnityWebView;` script and check it compiles).

- [ ] **Step 3: Port the hardware-acceleration fix**

Create `Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs`:

```csharp
#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace ARWalking.EditorTools
{
    // Root cause: gree/unity-webview's own IPostGenerateGradleAndroidProject step
    // (net.gree.unity-webview's UnityWebViewPostprocessBuild) only patches the
    // FIRST <activity> in the manifest that has a MAIN/LAUNCHER intent-filter.
    // Unity 6's Android template always emits two such activities --
    // UnityPlayerActivity (legacy, android:enabled="false") and
    // UnityPlayerGameActivity (android:enabled="true", the one that actually
    // launches when "Activity" is set to GameActivity in Player Settings) -- with
    // UnityPlayerActivity listed first. gree's script ends up setting
    // android:hardwareAccelerated="true" on the disabled legacy activity while
    // leaving the real, enabled GameActivity at "false" (Unity's own generated
    // value, which silently overrides Assets/Plugins/Android/AndroidManifest.xml
    // during manifest generation). A window without hardware acceleration forces
    // every view it hosts -- including a WebViewObject's embedded native WebView
    // -- into software compositing, so a WebGL canvas (MapLibre GL JS) can't get
    // a working GL context and renders solid black, even though ordinary DOM/CSS
    // content in the same page still paints fine. This step runs after gree's
    // (higher callbackOrder) and forces hardwareAccelerated="true" on every
    // <activity>, so whichever one Unity actually enables ends up correct.
    // Diagnosed and fixed in the PedometerPrototype reference project; ported
    // here as-is.
    public class ForceHardwareAcceleratedActivities : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            const string androidNs = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument();
            using (var reader = new XmlTextReader(manifestPath)) doc.Load(reader);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("android", androidNs);

            bool changed = false;
            foreach (XmlElement activity in doc.SelectNodes("/manifest/application/activity", nsMgr))
            {
                if (activity.GetAttribute("hardwareAccelerated", androidNs) != "true")
                {
                    activity.SetAttribute("hardwareAccelerated", androidNs, "true");
                    changed = true;
                }
            }

            if (changed)
            {
                using var writer = new XmlTextWriter(manifestPath, new System.Text.UTF8Encoding(false));
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
                Debug.Log($"[ForceHardwareAcceleratedActivities] Forced hardwareAccelerated=true on all activities in {manifestPath}");
            }
        }
    }
}
#endif
```

- [ ] **Step 4: Verify it compiles in the Editor**

Confirm no Console errors after the domain reload triggered by adding this script. This class's actual effect can only be verified during an Android build (Task 11's manual checklist), not via a unit test - it operates on generated Gradle project files that don't exist until a build runs.

- [ ] **Step 5: Commit**

```bash
git add Packages/manifest.json Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs Assets/_Project/Scripts/Editor/ForceHardwareAcceleratedActivities.cs.meta
git commit -m "feat: add gree/unity-webview dependency and the Unity 6 hardware-acceleration fix"
```

---

### Task 4: WebViewMapMargins (pure margin-calculation function)

**Files:**
- Create: `Assets/_Project/Scripts/Services/Map/WebViewMapMargins.cs`
- Create: `Assets/_Project/Scripts/Services/Map/WebViewMapMargins.cs.meta`
- Create: `Assets/_Project/Scripts/Services/Map.meta` (only if the `Services/Map` folder doesn't already exist)
- Test: `Assets/_Project/Tests/EditMode/WebViewMapMarginsEditModeTests.cs`

**Interfaces:**
- Produces: `WebViewMapMargins.Compute(float topBarBottomEdgeScreenPx, float bottomBarTopEdgeScreenPx, int screenWidth, int screenHeight) -> (int left, int top, int right, int bottom)`.

`WebViewObject.SetMargins` takes raw Android screen pixels. `HomeUiController`'s docked bars live inside the existing `_safeRoot` element (see `HomeUiController.ApplySafeArea`), which already converts `Screen.safeArea` into UI Toolkit panel-space padding - so a bar's *resolved* screen-space edge (panel-space value divided by the same `panelHeight / Screen.height` scale `ApplySafeArea` already computes) already reflects the safe area correctly. This function only does the last step: turn "where the bars' edges land in real screen pixels" into "how much margin the WebView needs on each side" - deliberately kept free of `UnityEngine.Screen`/`VisualElement` so it's testable with plain numbers.

- [ ] **Step 1: Write the failing tests**

```csharp
using ARWalking.UI;
using NUnit.Framework;

namespace ARWalking.Tests.EditMode
{
    public sealed class WebViewMapMarginsEditModeTests
    {
        [Test]
        public void Compute_TypicalBars_ReturnsTopAndBottomMarginsOnly()
        {
            var (left, top, right, bottom) = WebViewMapMargins.Compute(
                topBarBottomEdgeScreenPx: 220f, bottomBarTopEdgeScreenPx: 1800f,
                screenWidth: 1080, screenHeight: 2200);

            Assert.That(left, Is.EqualTo(0));
            Assert.That(right, Is.EqualTo(0));
            Assert.That(top, Is.EqualTo(220));
            Assert.That(bottom, Is.EqualTo(400)); // 2200 - 1800
        }

        [Test]
        public void Compute_ClampsToScreenBounds_NeverNegativeOrOverflowing()
        {
            var (_, top, _, bottom) = WebViewMapMargins.Compute(
                topBarBottomEdgeScreenPx: -50f, bottomBarTopEdgeScreenPx: 5000f,
                screenWidth: 1080, screenHeight: 2200);

            Assert.That(top, Is.EqualTo(0));
            Assert.That(bottom, Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Expected: FAIL with "WebViewMapMargins does not exist" / compile error.

- [ ] **Step 3: Implement**

```csharp
namespace ARWalking.UI
{
    /// <summary>Converts the docked top/bottom bars' resolved screen-space edges into the raw-pixel margins
    /// WebViewObject.SetMargins needs to shrink the native WebView to exactly the rectangle left between them.
    /// Deliberately a pure function (no UnityEngine.Screen/VisualElement) so it's testable with plain numbers -
    /// see WebViewMapMargins.cs's doc comment for why the inputs are already screen-space, not panel-space.</summary>
    public static class WebViewMapMargins
    {
        public static (int left, int top, int right, int bottom) Compute(
            float topBarBottomEdgeScreenPx, float bottomBarTopEdgeScreenPx, int screenWidth, int screenHeight)
        {
            var top = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(topBarBottomEdgeScreenPx), 0, screenHeight);
            var bottom = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(screenHeight - bottomBarTopEdgeScreenPx), 0, screenHeight);
            return (0, top, 0, bottom);
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Services/Map/WebViewMapMargins.cs Assets/_Project/Scripts/Services/Map/WebViewMapMargins.cs.meta Assets/_Project/Tests/EditMode/WebViewMapMarginsEditModeTests.cs Assets/_Project/Tests/EditMode/WebViewMapMarginsEditModeTests.cs.meta
git commit -m "feat: add pure WebView margin calculation for docked map layout"
```

---

### Task 5: IWebViewBridge seam and the production gree/unity-webview adapter

**Files:**
- Create: `Assets/_Project/Scripts/Services/Map/IWebViewBridge.cs`
- Create: `Assets/_Project/Scripts/Services/Map/IWebViewBridge.cs.meta`
- Create: `Assets/_Project/Scripts/Services/Map/GreeWebViewBridge.cs`
- Create: `Assets/_Project/Scripts/Services/Map/GreeWebViewBridge.cs.meta`

**Interfaces:**
- Produces: `IWebViewBridge` (`void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)`, `bool IsInitialized`, `void SetMargins(int left, int top, int right, int bottom)`, `void SetVisibility(bool visible)`, `void LoadURL(string url)`, `void EvaluateJS(string js)`), `GreeWebViewBridge : IWebViewBridge` (constructor takes the hosting `GameObject`).

`WebViewObject` (from `gree/unity-webview`) is a real native plugin - not something to fake convincingly in a test, and not something to unit-test here. This seam exists so `WebViewMapView`'s *surrounding* logic (message parsing, JSON building, margin/visibility calls) can be tested against a fake in Task 7, without ever touching the real plugin in CI.

- [ ] **Step 1: Define the interface**

```csharp
using System;

namespace ARWalking.UI
{
    /// <summary>Seam over "the thing that hosts a native WebView" so WebViewMapView's own logic (message
    /// parsing, JSON building, margin/visibility calls) is testable without a real WebViewObject - which is a
    /// genuine native plugin, not something a test double can convincingly stand in for. GreeWebViewBridge is
    /// the only production implementation.</summary>
    public interface IWebViewBridge
    {
        void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded);
        bool IsInitialized { get; }
        void SetMargins(int left, int top, int right, int bottom);
        void SetVisibility(bool visible);
        void LoadURL(string url);
        void EvaluateJS(string js);
    }
}
```

- [ ] **Step 2: Implement the production adapter**

```csharp
using System;
using Gree.UnityWebView;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>Thin adapter over the real net.gree.unity-webview WebViewObject. Adds itself as a component on
    /// the given host GameObject (WebViewObject is itself a MonoBehaviour the plugin expects to live on some
    /// GameObject) - callers never touch WebViewObject directly, only this interface.</summary>
    public sealed class GreeWebViewBridge : IWebViewBridge
    {
        readonly WebViewObject _webViewObject;

        public GreeWebViewBridge(GameObject host)
        {
            _webViewObject = host.AddComponent<WebViewObject>();
        }

        public bool IsInitialized => _webViewObject.IsInitialized();

        public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
        {
            _webViewObject.Init(
                cb: msg => onMessage(msg),
                err: msg => onError(msg),
                httpErr: msg => onError(msg),
                ld: _ => onLoaded(null));
        }

        public void SetMargins(int left, int top, int right, int bottom) => _webViewObject.SetMargins(left, top, right, bottom);
        public void SetVisibility(bool visible) => _webViewObject.SetVisibility(visible);
        public void LoadURL(string url) => _webViewObject.LoadURL(url);
        public void EvaluateJS(string js) => _webViewObject.EvaluateJS(js);
    }
}
```

- [ ] **Step 3: Confirm it compiles**

No test for this file specifically (it's a thin, untestable-without-a-real-WebView adapter, same rationale as `MapTilerTileSource` on the raster branch having no dedicated unit test) - just confirm the Editor compiles it cleanly against the package added in Task 3.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Services/Map/IWebViewBridge.cs Assets/_Project/Scripts/Services/Map/IWebViewBridge.cs.meta Assets/_Project/Scripts/Services/Map/GreeWebViewBridge.cs Assets/_Project/Scripts/Services/Map/GreeWebViewBridge.cs.meta
git commit -m "feat: add IWebViewBridge seam and the gree/unity-webview production adapter"
```

---

### Task 6: Bundled MapLibre HTML page

**Files:**
- Create: `Assets/StreamingAssets/spiritsteps_map.html`

**Interfaces:**
- Produces: a page exposing `window.mapBridge.update(jsonString)` (called via `EvaluateJS`), where `jsonString` has the shape `{"player":{"lat":N,"lon":N},"markers":[{"id":"...","label":"...","lat":N,"lon":N}]}`. Reports landmark taps to native code via `Unity.call("marker," + id)`.

Adapted from `PedometerPrototype`'s `Assets/StreamingAssets/openfreemap_map.html`: same MapLibre/OpenFreeMap setup; drops the POI-radius-circle layer (not part of this design - landmark unlock radius isn't visualized on the map itself); markers carry an id/label instead of an id/radius; adds a recenter control and MapLibre's built-in zoom control, both pure map-native UI needing no C# round-trip beyond the state already pushed.

- [ ] **Step 1: Write the file**

```html
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8" />
<meta name="viewport" content="initial-scale=1,maximum-scale=1,user-scalable=no" />
<script src="https://unpkg.com/maplibre-gl@4/dist/maplibre-gl.js"></script>
<link href="https://unpkg.com/maplibre-gl@4/dist/maplibre-gl.css" rel="stylesheet" />
<style>
  html, body, #map { margin: 0; padding: 0; width: 100%; height: 100%; }
  .player-dot, .landmark-pin {
    border-radius: 50%; border: 2px solid white; box-shadow: 0 0 4px rgba(0,0,0,0.6); cursor: pointer;
  }
  .player-dot { width: 16px; height: 16px; background: #3399ff; cursor: default; }
  .landmark-pin { width: 26px; height: 26px; background: #ff4d4d; }
  .recenter-button {
    position: absolute; right: 10px; bottom: 90px; z-index: 1;
    width: 36px; height: 36px; border-radius: 6px; border: none; background: white;
    box-shadow: 0 1px 4px rgba(0,0,0,0.3); font-size: 18px; cursor: pointer;
  }
</style>
</head>
<body>
<div id="map"></div>
<button class="recenter-button" id="recenter-btn" title="Recenter">&#8853;</button>
<script>
  // Bundled into the app via Assets/StreamingAssets, loaded into a native WebView by WebViewMapView.cs
  // (net.gree.unity-webview). Bridge contract:
  //   JS  -> native: Unity.call("marker," + landmarkId)     (a landmark pin was tapped)
  //   native -> JS : window.mapBridge.update(jsonString)    (WebViewMapView.Render, via EvaluateJS)

  const map = new maplibregl.Map({
    container: 'map',
    style: 'https://tiles.openfreemap.org/styles/liberty',
    center: [0, 0],
    zoom: 2
  });
  map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');

  let mapLoaded = false;
  let pendingState = null;
  let lastState = null;
  let playerMarker = null;
  const landmarkMarkers = {};

  map.on('load', () => {
    mapLoaded = true;
    if (pendingState) { applyState(pendingState); pendingState = null; }
  });

  document.getElementById('recenter-btn').addEventListener('click', () => {
    if (lastState) map.flyTo({ center: [lastState.player.lon, lastState.player.lat], zoom: 17 });
  });

  function makeMarkerEl(className) {
    const el = document.createElement('div');
    el.className = className;
    return el;
  }

  function applyState(state) {
    lastState = state;

    if (!playerMarker) {
      playerMarker = new maplibregl.Marker({ element: makeMarkerEl('player-dot') })
        .setLngLat([state.player.lon, state.player.lat]).addTo(map);
      map.jumpTo({ center: [state.player.lon, state.player.lat], zoom: 17 });
    } else {
      playerMarker.setLngLat([state.player.lon, state.player.lat]);
    }

    state.markers.forEach(function (marker) {
      if (!landmarkMarkers[marker.id]) {
        const el = makeMarkerEl('landmark-pin');
        el.title = marker.label;
        el.addEventListener('click', (e) => {
          e.stopPropagation();
          if (window.Unity) Unity.call('marker,' + marker.id);
        });
        landmarkMarkers[marker.id] = new maplibregl.Marker({ element: el })
          .setLngLat([marker.lon, marker.lat]).addTo(map);
      } else {
        landmarkMarkers[marker.id].setLngLat([marker.lon, marker.lat]);
      }
    });
  }

  window.mapBridge = {
    update: function (json) {
      const state = JSON.parse(json);
      if (!mapLoaded) { pendingState = state; return; }
      applyState(state);
    }
  };
</script>
</body>
</html>
```

- [ ] **Step 2: No automated test for this file**

It's markup/JS, not C# - there's nothing an EditMode/PlayMode test can exercise here. Task 11's manual checklist covers it (map loads, markers appear, tap-a-pin fires the bridge, recenter works).

- [ ] **Step 3: Commit**

```bash
git add Assets/StreamingAssets/spiritsteps_map.html
git commit -m "feat: add the bundled MapLibre/OpenFreeMap page for the WebView map"
```

---

### Task 7: WebViewMapView (the bridge MonoBehaviour)

**Files:**
- Create: `Assets/_Project/Scripts/UI/Comprehensive/WebViewMapMarker.cs`
- Create: `Assets/_Project/Scripts/UI/Comprehensive/WebViewMapMarker.cs.meta`
- Create: `Assets/_Project/Scripts/UI/Comprehensive/WebViewMapView.cs`
- Create: `Assets/_Project/Scripts/UI/Comprehensive/WebViewMapView.cs.meta`
- Test: `Assets/_Project/Tests/PlayMode/WebViewMapViewPlayModeTests.cs`

**Interfaces:**
- Consumes: `IWebViewBridge` (Task 5), `GeoPoint` (Task 1).
- Produces: `WebViewMapMarker` (readonly struct: `string id`, `string label`, `GeoPoint location`), `WebViewMapView` (`MonoBehaviour`): `void Initialize(IWebViewBridge bridge)`, `void SetMargins(int left, int top, int right, int bottom)`, `void Render(GeoPoint player, IReadOnlyList<WebViewMapMarker> markers)`, `void SetActive(bool active)`, `bool IsAvailable`, `event Action AvailabilityChanged`, `event Action<string> OnMarkerTapped`.

- [ ] **Step 1: Write `WebViewMapMarker`**

```csharp
namespace ARWalking.UI
{
    public readonly struct WebViewMapMarker
    {
        public readonly string id;
        public readonly string label;
        public readonly GeoPoint location;

        public WebViewMapMarker(string id, string label, GeoPoint location)
        {
            this.id = id;
            this.label = label;
            this.location = location;
        }
    }
}
```

- [ ] **Step 2: Write the failing PlayMode tests**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ARWalking.Tests.PlayMode
{
    public sealed class WebViewMapViewPlayModeTests
    {
        sealed class FakeWebViewBridge : IWebViewBridge
        {
            public Action<string> OnMessage;
            public Action<string> OnError;
            public Action<string> OnLoaded;
            public bool IsInitialized { get; set; } = true;
            public bool Visible;
            public string LastLoadedUrl;
            public string LastEvaluatedJs;
            public int LeftMargin, TopMargin, RightMargin, BottomMargin;

            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
            {
                OnMessage = onMessage; OnError = onError; OnLoaded = onLoaded;
            }
            public void SetMargins(int left, int top, int right, int bottom)
            {
                LeftMargin = left; TopMargin = top; RightMargin = right; BottomMargin = bottom;
            }
            public void SetVisibility(bool visible) => Visible = visible;
            public void LoadURL(string url) => LastLoadedUrl = url;
            public void EvaluateJS(string js) => LastEvaluatedJs = js;
        }

        GameObject _host;
        WebViewMapView _view;
        FakeWebViewBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("webview-map-view-test-host");
            _view = _host.AddComponent<WebViewMapView>();
            _bridge = new FakeWebViewBridge();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_host);

        [UnityTest]
        public IEnumerator Initialize_StagesAndLoadsTheBundledHtmlPage()
        {
            _view.Initialize(_bridge);
            yield return null; yield return null;
            _bridge.OnLoaded?.Invoke(null);
            yield return null;

            Assert.That(_bridge.LastLoadedUrl, Does.Contain("spiritsteps_map.html"));
        }

        [UnityTest]
        public IEnumerator OnMarkerTapped_ParsesBridgeMessage_RaisesEventWithLandmarkId()
        {
            _view.Initialize(_bridge);
            yield return null;
            string tappedId = null;
            _view.OnMarkerTapped += id => tappedId = id;

            _bridge.OnMessage("marker,central-post-office");

            Assert.That(tappedId, Is.EqualTo("central-post-office"));
        }

        [UnityTest]
        public IEnumerator Render_BeforePageReady_QueuesState_FlushesOnLoad()
        {
            _view.Initialize(_bridge);
            yield return null;

            var player = new GeoPoint(10.7798, 106.6997);
            _view.Render(player, new List<WebViewMapMarker> { new WebViewMapMarker("central-post-office", "Central Post Office", player) });
            Assert.That(_bridge.LastEvaluatedJs, Is.Null, "must not push state before the page reports it's loaded");

            _bridge.OnLoaded?.Invoke(null);
            yield return null;

            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("mapBridge.update"));
            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("central-post-office"));
            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("10.7798"));
        }

        [UnityTest]
        public IEnumerator SetMargins_ForwardsDirectlyToTheBridge()
        {
            _view.Initialize(_bridge);
            yield return null;

            _view.SetMargins(0, 220, 0, 400);

            Assert.That((_bridge.LeftMargin, _bridge.TopMargin, _bridge.RightMargin, _bridge.BottomMargin), Is.EqualTo((0, 220, 0, 400)));
        }

        [UnityTest]
        public IEnumerator SetActive_ForwardsToBridgeVisibility()
        {
            _view.Initialize(_bridge);
            yield return null;

            _view.SetActive(false);
            Assert.That(_bridge.Visible, Is.False);
            _view.SetActive(true);
            Assert.That(_bridge.Visible, Is.True);
        }

        [UnityTest]
        public IEnumerator BridgeReportsError_BecomesUnavailable()
        {
            var becameUnavailable = false;
            _view.AvailabilityChanged += () => becameUnavailable = !_view.IsAvailable;
            _view.Initialize(_bridge);
            yield return null;

            _bridge.OnError("simulated: WebView init failed");

            Assert.That(_view.IsAvailable, Is.False);
            Assert.That(becameUnavailable, Is.True);
        }
    }
}
```

- [ ] **Step 3: Run to confirm failure**

Expected: FAIL with compile errors (`WebViewMapView` doesn't exist yet).

- [ ] **Step 4: Implement `WebViewMapView`**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ARWalking.UI
{
    /// <summary>Owns a native WebView (via an injected IWebViewBridge - GreeWebViewBridge in production)
    /// hosting Assets/StreamingAssets/spiritsteps_map.html. Positioned in raw screen pixels via SetMargins,
    /// independent of Unity's UI Toolkit layout - the docked top/bottom bars in HomeUiController compute those
    /// margins (see WebViewMapMargins) and this class only ever forwards them to the bridge.</summary>
    public sealed class WebViewMapView : MonoBehaviour
    {
        const string HtmlFileName = "spiritsteps_map.html";

        public bool IsAvailable { get; private set; } = true;
        public event Action AvailabilityChanged;
        public event Action<string> OnMarkerTapped;

        IWebViewBridge _bridge;
        bool _pageReady;
        string _pendingStateJson;

        public void Initialize(IWebViewBridge bridge)
        {
            _bridge = bridge;
            StartCoroutine(SetUp());
        }

        public void SetMargins(int left, int top, int right, int bottom) => _bridge?.SetMargins(left, top, right, bottom);
        public void SetActive(bool active) => _bridge?.SetVisibility(active);

        public void Render(GeoPoint player, IReadOnlyList<WebViewMapMarker> markers)
        {
            var json = BuildStateJson(player, markers);
            if (!_pageReady) { _pendingStateJson = json; return; }
            PushState(json);
        }

        IEnumerator SetUp()
        {
            _bridge.Init(OnMessageFromBridge, OnBridgeError, _ => OnPageLoaded());

            while (!_bridge.IsInitialized) yield return null;

            string stagedUrl = null;
            Exception stagingError = null;
            yield return StageHtmlAsset(url => stagedUrl = url, e => stagingError = e);
            if (stagingError != null)
            {
                OnBridgeError($"failed to stage {HtmlFileName}: {stagingError.Message}");
                yield break;
            }

            _bridge.LoadURL(stagedUrl);
        }

        // Cross-platform StreamingAssets loading, following gree/unity-webview's own documented sample pattern:
        // on Android, streamingAssetsPath is a jar:// URL that needs UnityWebRequest; elsewhere it's a plain
        // file path.
        IEnumerator StageHtmlAsset(Action<string> onStaged, Action<Exception> onError)
        {
            string src = Path.Combine(Application.streamingAssetsPath, HtmlFileName);
            string dst = Path.Combine(Application.temporaryCachePath, HtmlFileName);
            byte[] bytes;

            if (src.Contains("://"))
            {
                using var req = UnityWebRequest.Get(src);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError(new Exception(req.error));
                    yield break;
                }
                bytes = req.downloadHandler.data;
            }
            else
            {
                bytes = File.ReadAllBytes(src);
            }

            File.WriteAllBytes(dst, bytes);
            onStaged("file://" + dst.Replace(" ", "%20"));
        }

        void OnPageLoaded()
        {
            _pageReady = true;
            if (_pendingStateJson != null) { PushState(_pendingStateJson); _pendingStateJson = null; }
        }

        void OnBridgeError(string message)
        {
            Debug.LogWarning($"[WebViewMapView] {message}");
            if (!IsAvailable) return;
            IsAvailable = false;
            AvailabilityChanged?.Invoke();
        }

        void OnMessageFromBridge(string message)
        {
            var parts = message.Split(',');
            if (parts.Length != 2 || parts[0] != "marker") return;
            OnMarkerTapped?.Invoke(parts[1]);
        }

        void PushState(string json) => _bridge.EvaluateJS("window.mapBridge && window.mapBridge.update(" + JsStringLiteral(json) + ");");

        static string BuildStateJson(GeoPoint player, IReadOnlyList<WebViewMapMarker> markers)
        {
            var sb = new StringBuilder();
            sb.Append("{\"player\":{\"lat\":").Append(player.lat.ToString("F6", CultureInfo.InvariantCulture))
              .Append(",\"lon\":").Append(player.lon.ToString("F6", CultureInfo.InvariantCulture)).Append("},\"markers\":[");
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(Escape(marker.id)).Append("\",\"label\":\"").Append(Escape(marker.label))
                  .Append("\",\"lat\":").Append(marker.location.lat.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(",\"lon\":").Append(marker.location.lon.ToString("F6", CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string JsStringLiteral(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
```

- [ ] **Step 5: Run tests, confirm pass**

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/Comprehensive/WebViewMapMarker.cs Assets/_Project/Scripts/UI/Comprehensive/WebViewMapMarker.cs.meta Assets/_Project/Scripts/UI/Comprehensive/WebViewMapView.cs Assets/_Project/Scripts/UI/Comprehensive/WebViewMapView.cs.meta Assets/_Project/Tests/PlayMode/WebViewMapViewPlayModeTests.cs Assets/_Project/Tests/PlayMode/WebViewMapViewPlayModeTests.cs.meta
git commit -m "feat: add WebViewMapView bridging the bundled map page to C#"
```

---

### Task 8: Wire real providers and WebViewMapView into UiPrototypeRuntime

**Files:**
- Modify: `Assets/_Project/Scripts/UI/Comprehensive/UiPrototypeRuntime.cs`
- Modify: `Assets/_Project/Tests/PlayMode/UiPrototypePlayModeTests.cs`

**Interfaces:**
- Consumes: `WebViewMapView`, `IWebViewBridge`, `GreeWebViewBridge` (Tasks 5, 7); `RealWalkMetricsProvider`, `RealLandmarkMapProvider`, `DeviceLocationService`, `LandmarkGeoCatalog`, `GeoToMapProjection` (Task 1).
- Produces: `UiPrototypeRuntime.MapView` (`WebViewMapView`), `.GeoCatalog` (`LandmarkGeoCatalog`), `.LocationService` (`DeviceLocationService`), `static IWebViewBridge TestWebViewBridgeOverride`.

- [ ] **Step 1: Add the new properties, test override, and real-provider wiring**

In `UiPrototypeRuntime.cs`, mirroring the exact pattern `feature/raster-tile-map` used for `RasterMapView` (real providers behind the same `Test*Override` convention, a calibrated `GeoToMapProjection` built from the catalog's three anchor landmarks, and every failure path degrading gracefully instead of throwing out of `Awake()`):

```csharp
public static IWebViewBridge TestWebViewBridgeOverride { get; set; }

public IWalkMetricsProvider WalkProvider { get; private set; }
public ILandmarkMapProvider LandmarkMapProvider { get; private set; }
public WebViewMapView MapView { get; private set; }
public LandmarkGeoCatalog GeoCatalog { get; private set; }
public DeviceLocationService LocationService => SharedLocationService;
```

In `Awake()`, replace:

```csharp
WalkProvider = TestWalkProviderOverride ?? new DeterministicWalkMetricsProvider();
LandmarkMapProvider = TestMapProviderOverride ?? new DeterministicLandmarkMapProvider();
```

with:

```csharp
WalkProvider = TestWalkProviderOverride ?? BuildRealWalkProvider();
LandmarkMapProvider = TestMapProviderOverride ?? BuildRealMapProvider(catalog);
GeoCatalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
InitializeMapView();
```

Add these members (same file):

```csharp
DeviceLocationService _sharedLocationService;
DeviceLocationService SharedLocationService => _sharedLocationService ??= gameObject.AddComponent<DeviceLocationService>();

IWalkMetricsProvider BuildRealWalkProvider() =>
    new RealWalkMetricsProvider(SharedLocationService, gameObject.AddComponent<DeviceStepCounterService>());

ILandmarkMapProvider BuildRealMapProvider(PrototypeUiCatalog catalog)
{
    var geoCatalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
    var projection = BuildCalibratedProjection(geoCatalog, catalog);
    if (geoCatalog == null || projection == null)
    {
        Debug.LogWarning("LandmarkGeoCatalog is missing or under-calibrated (needs 3 non-collinear " +
            "isMapCalibrationAnchor landmarks whose id matches a PrototypeUiCatalog marker's targetId). " +
            "Falling back to the deterministic map provider.");
        return new DeterministicLandmarkMapProvider();
    }
    return new RealLandmarkMapProvider(SharedLocationService, geoCatalog, projection);
}

static GeoToMapProjection BuildCalibratedProjection(LandmarkGeoCatalog geoCatalog, PrototypeUiCatalog uiCatalog)
{
    if (geoCatalog == null) return null;
    var anchors = new List<(GeoPoint geo, Vector2 map)>();
    foreach (var landmark in geoCatalog.landmarks)
    {
        if (!landmark.isMapCalibrationAnchor) continue;
        MapMarkerUiData marker = null;
        foreach (var candidate in uiCatalog.markers)
            if (candidate.targetId == landmark.id) { marker = candidate; break; }
        if (marker == null) continue;
        anchors.Add((landmark.Location, marker.normalizedPosition));
    }
    if (anchors.Count != 3) return null;
    try { return new GeoToMapProjection(anchors[0].geo, anchors[0].map, anchors[1].geo, anchors[1].map, anchors[2].geo, anchors[2].map); }
    catch (ArgumentException) { return null; }
}

void InitializeMapView()
{
    // WebViewMapView's Initialize starts a coroutine and touches Application.streamingAssetsPath - fine to
    // call from Awake(), unlike RasterMapView on the prior branch, this constructor creates no VisualElement.
    MapView = gameObject.AddComponent<WebViewMapView>();
    try
    {
        var bridge = TestWebViewBridgeOverride ?? new GreeWebViewBridge(gameObject);
        MapView.Initialize(bridge);
    }
    catch (Exception e)
    {
        // A failure here must never abort the rest of Awake() - SaveData/InitialLoadResult below this call are
        // unrelated to the map and the whole app must not break because the map couldn't start.
        Debug.LogWarning($"WebView map failed to initialize ({e.Message}); falling back to the static illustrated map.");
    }
}
```

Add `using System.Collections.Generic;` if not already present (it already is, per the existing `using` block).

- [ ] **Step 2: Reset the new test override in `ClearTestOverrides`**

```csharp
public static void ClearTestOverrides()
{
    TestSavePathOverride = null;
    TestWalkProviderOverride = null;
    TestMapProviderOverride = null;
    TestWebViewBridgeOverride = null;
}
```

- [ ] **Step 3: Pin overrides in `UiPrototypePlayModeTests.cs`'s `SetUp`**

Real providers now being the default (same lesson as the raster-tile-map branch: `FourTabsWalkResultCompanionDetailAndFeedWork` and friends assumed deterministic providers). Add a `DeterministicFailingWebViewBridge : IWebViewBridge` nested class (mirrors the existing `DeterministicFailingRasterMapTileSource` pattern for this suite - all methods no-op except `Init`, which never calls `onLoaded` or `onMessage`, and `IsInitialized` returns `true` immediately since these tests don't exercise the map itself):

```csharp
sealed class DeterministicFailingWebViewBridge : IWebViewBridge
{
    public bool IsInitialized => true;
    public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded) { }
    public void SetMargins(int left, int top, int right, int bottom) { }
    public void SetVisibility(bool visible) { }
    public void LoadURL(string url) { }
    public void EvaluateJS(string js) { }
}
```

In `SetUp()`, alongside the existing `TestWalkProviderOverride`/`TestMapProviderOverride` pins, add:

```csharp
UiPrototypeRuntime.TestWebViewBridgeOverride = new DeterministicFailingWebViewBridge();
```

- [ ] **Step 4: Run the full EditMode + PlayMode suites, confirm no regressions**

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/Comprehensive/UiPrototypeRuntime.cs Assets/_Project/Tests/PlayMode/UiPrototypePlayModeTests.cs
git commit -m "feat: wire real providers and WebViewMapView into UiPrototypeRuntime"
```

---

### Task 9: Docked USS styles

**Files:**
- Modify: `Assets/_Project/Resources/UI/ARWalking.uss`

**Interfaces:**
- Produces: `.map-top-bar`, `.map-bottom-bar` classes for Task 10 to use.

- [ ] **Step 1: Replace the floating-overlay rules with docked-bar rules**

Replace the `.map-top-overlay`, `.map-stats`, and `.map-controls` rules (currently all `position: absolute`) with:

```css
.map-top-bar {
    flex-direction: row;
    align-items: flex-start;
    padding: 20px 22px 12px 22px;
    background-color: rgb(217, 234, 210);
}

.map-top-bar .compact-card {
    flex-grow: 1;
    margin-right: 12px;
}

.map-bottom-bar {
    flex-direction: row;
    align-items: center;
    padding: 12px 22px 18px 22px;
    background-color: rgb(217, 234, 210);
}

.map-bottom-bar .metric {
    margin-right: 10px;
}

.map-bottom-bar .action-button {
    flex-grow: 1;
    margin-top: 0;
}

.map-bottom-bar .icon-button {
    margin-left: 10px;
}
```

Keep `background-color: rgb(217, 234, 210)` (the existing `.map-viewport` background) on both bars - it's the same illustrated-map-page background color, so the docked bars visually match the fallback illustrated map's palette instead of introducing a new color. Leave `.map-page` and `.map-marker` untouched (`.map-page` still applies to both the real-map and fallback paths; `.map-marker` is still used by the illustrated-map fallback's marker buttons).

Do **not** delete `.map-viewport`, `.map-canvas`, `.map-image`, or `.map-controls` yet - Task 10 keeps the illustrated-map fallback branch using them as-is; only the *live* WebView path stops using them.

- [ ] **Step 2: No automated test** - USS has no test surface; Task 10's PlayMode tests exercise which elements/classes get built, and Task 11's manual checklist covers the visual result.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Resources/UI/ARWalking.uss
git commit -m "feat: add docked top/bottom bar styles for the webview map layout"
```

---

### Task 10: Rewrite `HomeUiController.BuildMap()` and wire visibility/marker-tap

**Files:**
- Modify: `Assets/_Project/Scripts/UI/Comprehensive/HomeUiController.cs`
- Test: `Assets/_Project/Tests/PlayMode/WebViewMapDockingPlayModeTests.cs`

**Interfaces:**
- Consumes: `WebViewMapView`, `WebViewMapMarker`, `WebViewMapMargins` (Tasks 4, 7), `GeoPoint`, `DeviceLocationService`, `LandmarkGeoCatalog` (Task 1).

This is the task that actually makes the Map page use the real map. `BuildMap()` branches on `_runtime.MapView != null && _runtime.MapView.IsAvailable`: the existing illustrated-map code becomes the `else` branch, completely unchanged; the new branch builds the docked bars and attaches the WebView.

- [ ] **Step 1: Write the failing PlayMode tests**

```csharp
using System;
using System.Collections;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    public sealed class WebViewMapDockingPlayModeTests
    {
        sealed class RecordingWebViewBridge : IWebViewBridge
        {
            public int LeftMargin, TopMargin, RightMargin, BottomMargin;
            public bool Visible = true;
            public string LastMessageHandlerProbe;
            Action<string> _onMessage;

            public bool IsInitialized => true;
            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
            {
                _onMessage = onMessage;
                onLoaded(null); // page "ready" immediately - these tests don't need real HTML staging
            }
            public void SetMargins(int left, int top, int right, int bottom)
            {
                LeftMargin = left; TopMargin = top; RightMargin = right; BottomMargin = bottom;
            }
            public void SetVisibility(bool visible) => Visible = visible;
            public void LoadURL(string url) { }
            public void EvaluateJS(string js) { }
            public void SimulateMarkerTap(string landmarkId) => _onMessage?.Invoke("marker," + landmarkId);
        }

        string _savePath;
        RecordingWebViewBridge _bridge;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UiPrototypeRuntime.Instance != null) { UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject); yield return null; }
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ar-walking-webview-dock-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            _bridge = new RecordingWebViewBridge();
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestWebViewBridgeOverride = _bridge;
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (UiPrototypeRuntime.Instance != null) UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
            yield return null;
            UiPrototypeRuntime.ClearTestOverrides();
        }

        static IEnumerator WaitForScene(string name)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        HomeUiController CreateProfile()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            home.CompleteSetup("Docking Test");
            return home;
        }

        [UnityTest]
        public IEnumerator MapPage_UsesDockedBars_NotAbsoluteFloatingOverlay()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q(className: "map-top-bar"), Is.Not.Null);
            Assert.That(root.Q(className: "map-bottom-bar"), Is.Not.Null);
            Assert.That(root.Q(className: "map-top-overlay"), Is.Null, "the old floating overlay must not be built when the webview map is available");
        }

        [UnityTest]
        public IEnumerator MapPage_ComputesAndAppliesWebViewMargins()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            Assert.That(_bridge.TopMargin, Is.GreaterThan(0));
            Assert.That(_bridge.BottomMargin, Is.GreaterThan(0));
            Assert.That(_bridge.LeftMargin, Is.EqualTo(0));
            Assert.That(_bridge.RightMargin, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator MarkerTapped_NavigatesToLandmarkDetail()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            _bridge.SimulateMarkerTap(PrototypeIds.CentralPostOffice);
            yield return null;

            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.LandmarkDetail));
        }

        [UnityTest]
        public IEnumerator OpeningSettingsOverlay_HidesWebView_ClosingRestoresIt()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;
            Assert.That(_bridge.Visible, Is.True);

            home.ShowOverlay(UiOverlay.Settings);
            yield return null;
            Assert.That(_bridge.Visible, Is.False);

            // Closing goes through IAppNavigator.CloseOverlay directly (confirmed against HomeUiController's
            // actual settings-modal "Close" button, which wires to _runtime.Navigator.CloseOverlay - calling it
            // directly via the public UiPrototypeRuntime.Instance is more robust than querying for the button
            // by name, since ActionWithIcon doesn't assign it one).
            UiPrototypeRuntime.Instance.Navigator.CloseOverlay();
            yield return null;
            Assert.That(_bridge.Visible, Is.True);
        }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Expected: FAIL - `.map-top-bar`/`.map-bottom-bar` don't exist yet in `BuildMap()`, margins are never set, `OnMarkerTapped` is never wired, and overlay open/close never touches `MapView.SetActive`.

- [ ] **Step 3: Add a `SyncMapViewVisibility` helper and hook it into overlay/navigation events**

Near `OnNavigationChanged` in `HomeUiController.cs`:

```csharp
void OnNavigationChanged() { Render(); RenderOverlay(); SyncMapViewVisibility(); }

void SyncMapViewVisibility()
{
    if (_runtime.MapView == null) return;
    var onMapWithNoOverlay = _runtime.Navigator.CurrentRoute == UiRoute.HomeMap && _runtime.Navigator.CurrentOverlay == null;
    _runtime.MapView.SetActive(onMapWithNoOverlay);
}
```

Call `SyncMapViewVisibility()` at the end of `Start()` too (after the initial `Render()`/`ResetToSetup()` call), so the very first frame's visibility is correct before any navigation event has fired.

- [ ] **Step 4: Rewrite `BuildMap()`**

Replace the existing `BuildMap()` body with a branch: the real-map path is new; the `else` is the *exact* existing body, unchanged (down to variable names), just moved under the `else`.

```csharp
void BuildMap()
{
    var page = Page("map-page", true);
    if (_runtime.MapView != null && _runtime.MapView.IsAvailable) BuildRealMap(page);
    else BuildIllustratedMapFallback(page);
}

void BuildRealMap(VisualElement page)
{
    var top = new VisualElement { name = "map-top-bar" };
    top.AddToClassList("map-top-bar");
    var greeting = Card("compact-card", "glass-card");
    greeting.Add(Eyebrow("LOCAL-ONLY PROFILE"));
    greeting.Add(Title("Hello, " + _runtime.SaveData.displayName));
    greeting.Add(Body("District 1, Ho Chi Minh City"));
    top.Add(greeting);
    top.Add(IconAction(_assets.iconSettings, "SET", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
    page.Add(top);

    var bottom = new VisualElement { name = "map-bottom-bar" };
    bottom.AddToClassList("map-bottom-bar");
    bottom.Add(Metric(_runtime.SaveData.coins.ToString(), "Coins"));
    bottom.Add(Metric(_runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " km", "total distance"));
    bottom.Add(ActionWithIcon(_assets.iconSteps, "Start a walk", BeginWalk, "primary-action", "compact-action"));
    bottom.Add(ActionWithIcon(_assets.iconAr, "AR Photo", () =>
    {
        _pickingPetForPhoto = true;
        SelectRoot(UiRootTab.Companions);
    }, "secondary-action", "compact-action"));
    bottom.Add(IconAction(_assets.iconHelp, "?", () => ShowToast("Tap a landmark pin to view its distance and AR availability."), "map-help-button"));
    page.Add(bottom);

    _runtime.LocationService.Activate();
    _runtime.MapView.OnMarkerTapped -= OnRealMapMarkerTapped; // avoid a duplicate subscription if BuildMap runs again
    _runtime.MapView.OnMarkerTapped += OnRealMapMarkerTapped;

    page.RegisterCallback<GeometryChangedEvent>(_ => ApplyRealMapMargins(top, bottom));
    ApplyRealMapMargins(top, bottom);
    RenderRealMapMarkers();
}

void ApplyRealMapMargins(VisualElement topBar, VisualElement bottomBar)
{
    var panelHeight = _document.rootVisualElement.resolvedStyle.height;
    if (float.IsNaN(panelHeight) || panelHeight <= 0f) return;
    var scale = Screen.height / panelHeight;

    var topEdgeScreenPx = topBar.worldBound.yMax * scale;
    var bottomEdgeScreenPx = bottomBar.worldBound.yMin * scale;
    var (left, top, right, bottom) = WebViewMapMargins.Compute(topEdgeScreenPx, bottomEdgeScreenPx, Screen.width, Screen.height);
    _runtime.MapView.SetMargins(left, top, right, bottom);
}

void RenderRealMapMarkers()
{
    if (!_runtime.LocationService.HasFix) return;
    var markers = new List<WebViewMapMarker>();
    foreach (var marker in _mapData.Markers)
    {
        if (marker.type != MapMarkerType.Landmark) continue;
        var landmark = _runtime.GeoCatalog?.Find(marker.targetId);
        if (landmark == null) continue;
        markers.Add(new WebViewMapMarker(marker.targetId, marker.label, landmark.Location));
    }
    _runtime.MapView.Render(_runtime.LocationService.Current, markers);
}

void OnRealMapMarkerTapped(string landmarkId)
{
    _runtime.SelectedLandmarkIndex = FindLandmarkIndex(landmarkId);
    Navigate(UiRoute.LandmarkDetail);
}

void BuildIllustratedMapFallback(VisualElement page)
{
    var viewport = new VisualElement { name = "illustrated-map-viewport" };
    viewport.AddToClassList("map-viewport");
    var canvas = new VisualElement { name = "illustrated-map-canvas" };
    canvas.AddToClassList("map-canvas");
    canvas.Add(Image(_assets != null ? _assets.illustratedMap : null, "map-image"));
    viewport.Add(canvas);
    var manipulator = new IllustratedMapManipulator(canvas, _mapData.Map.minimumZoom, _mapData.Map.maximumZoom);
    viewport.AddManipulator(manipulator);
    foreach (var marker in _mapData.Markers)
    {
        var captured = marker;
        var markerIcon = marker.type == MapMarkerType.Player ? _assets.iconLocation : _assets.iconMap;
        var button = IconAction(markerIcon, marker.type == MapMarkerType.Player ? "YOU" : "PIN", () => OpenMarker(captured), "marker-" + marker.id);
        button.AddToClassList("map-marker");
        button.AddToClassList(marker.type == MapMarkerType.Player ? "marker-player" : "marker-landmark");
        button.name = "marker-" + marker.id;
        button.tooltip = marker.label;
        button.style.left = Length.Percent(marker.normalizedPosition.x * 100f);
        button.style.top = Length.Percent(marker.normalizedPosition.y * 100f);
        canvas.Add(button);
    }
    page.Insert(0, viewport);
    var top = new VisualElement { name = "map-top-overlay" };
    top.AddToClassList("map-top-overlay");
    var greeting = Card("compact-card", "glass-card");
    greeting.Add(Eyebrow("LOCAL-ONLY PROFILE"));
    greeting.Add(Title("Hello, " + _runtime.SaveData.displayName));
    greeting.Add(Body("District 1, Ho Chi Minh City"));
    top.Add(greeting);
    top.Add(IconAction(_assets.iconSettings, "SET", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
    page.Add(top);
    var stats = Card("map-stats", "glass-card");
    stats.Add(Metric(_runtime.SaveData.coins.ToString(), "Coins"));
    stats.Add(Metric(_runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " km", "total distance"));
    stats.Add(ActionWithIcon(_assets.iconSteps, "Start a walk", BeginWalk, "primary-action", "compact-action"));
    stats.Add(ActionWithIcon(_assets.iconAr, "AR Photo", () =>
    {
        _pickingPetForPhoto = true;
        SelectRoot(UiRootTab.Companions);
    }, "secondary-action", "compact-action"));
    page.Add(stats);
    var controls = new VisualElement(); controls.AddToClassList("map-controls");
    controls.Add(IconAction(_assets.iconLocation, "GPS", () => ShowToast("Location permission is requested here when the real map provider is connected."), "location-button"));
    controls.Add(IconAction(_assets.iconCompass, "CTR", manipulator.Recenter, "recenter-button"));
    controls.Add(IconAction(_assets.iconHelp, "?", () => ShowToast("Tap a Landmark pin to view its distance and AR availability."), "map-help-button"));
    page.Add(controls);
}
```

Note `BuildIllustratedMapFallback` takes `page` as a parameter now (was a captured local before) - `page` was already built via `Page("map-page", true)` in the new top-level `BuildMap()`, so this is purely a signature change from the old code's implicit closure over `page`, not a behavior change.

- [ ] **Step 5: Update `Update()` to re-render markers/player position on each GPS fix while on the Map route**

Find the existing `Update()` method's raster-branch-era map-refresh block (if this project's `main` doesn't have one yet, add a small block near the top of `Update()`, guarded the same way `ApplySafeArea` already is):

```csharp
if (_runtime.Navigator.CurrentRoute == UiRoute.HomeMap && _runtime.MapView != null && _runtime.MapView.IsAvailable)
    RenderRealMapMarkers();
```

Keep this cheap: `RenderRealMapMarkers()` already no-ops via its `HasFix` guard when there's nothing new to show, and `WebViewMapView.Render` only pushes JS when the page is ready - calling it every frame is the same pattern `DeviceLocationService.OnLocationUpdated` callers already tolerate elsewhere in this codebase, not a new performance concern (unlike the raster branch's tile-fetch cost, MapLibre owns everything past this one small JSON push).

- [ ] **Step 6: Unsubscribe in `OnDisable`**

```csharp
void OnDisable()
{
    if (_runtime != null && _runtime.Navigator != null) _runtime.Navigator.Changed -= OnNavigationChanged;
    if (_runtime != null && _runtime.MapView != null) _runtime.MapView.OnMarkerTapped -= OnRealMapMarkerTapped;
}
```

- [ ] **Step 7: Run the new PlayMode tests and the full suite, confirm pass with no regressions**

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/UI/Comprehensive/HomeUiController.cs Assets/_Project/Tests/PlayMode/WebViewMapDockingPlayModeTests.cs Assets/_Project/Tests/PlayMode/WebViewMapDockingPlayModeTests.cs.meta
git commit -m "feat: dock the map page's panels and attach the real webview map"
```

---

### Task 11: Manual verification and finishing

**Files:** none (verification only).

- [ ] **Step 1: Full automated suite, one last time**

Run EditMode and PlayMode in full; expect the same pre-existing, unrelated 3 `CorgiAR.Tests.*` failures as every prior branch this session and nothing else.

- [ ] **Step 2: Editor Play-mode manual check (Windows, via WebView2)**

This is the fast iteration loop the design specifically called out - use it before reaching for an Android build:
- Map page shows real OpenFreeMap tiles, not a blank/black rectangle (if black: the hardware-acceleration fix from Task 3 didn't take effect - check the generated manifest, though note that fix is Android-build-only and won't apply to the Windows Editor's WebView2 host at all, so a black canvas in the *Editor* specifically points elsewhere).
- Top and bottom bars sit at the screen edges with no gap or overlap against the map.
- Player marker appears once `DeviceLocationService`'s editor simulation provides a fix (arrow keys nudge it, per `DeviceLocationService.RunEditorSimulation`).
- Landmark pins appear at the three real coordinates from Task 2 and are tappable, opening Landmark Detail.
- Recenter button re-centers on the player.
- Pinch-zoom/pan/rotate feel smooth (native MapLibre gestures).
- Opening Settings/Permissions/a toast hides the WebView; closing restores it.

- [ ] **Step 3: Android on-device check**

Build and deploy (same `BuildPipeline.BuildPlayer` + `BuildOptions.AutoRunPlayer` pattern used throughout this project's sessions; verify success via APK existence + `adb shell pidof`, not the build wrapper's own pass/fail, which has previously misreported warnings-only builds as failed). Repeat the Step 2 checklist on-device, plus:
- Confirm the map is not solid black (the specific failure mode Task 3's fix targets, and the one thing Editor testing can't verify).
- Confirm real GPS permission prompts and fixes work (`adb logcat`, same workflow used on the raster-tile-map branch).
- Confirm the WebView's margins track the docked bars correctly on a real notched device (safe-area top inset).

- [ ] **Step 4: If any issue is found, fix it, add/adjust a test where feasible, commit, and re-verify** - do not consider this plan done until Steps 2 and 3 both pass clean.

- [ ] **Step 5: Finish the branch**

Per `superpowers:executing-plans`, invoke `superpowers:finishing-a-development-branch` once every prior step is green.
