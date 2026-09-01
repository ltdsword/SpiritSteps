# AGENTS.md — AR Walking (Animal Companion Prototype)

Orientation doc for future Claude Code / agent sessions in this repo. Read this first; it points at the deeper docs and explains how the pieces fit together.

## What this project is

A **local-only, walking-based animal companion prototype** for a school "3D location-based companion game" project (final project, ~12-day scope), inspired by Pikmin Bloom. Core loop: walk → distance grants Growth EXP to unlocked companions (Dog/Cat/Rabbit) + Coins → spend Coins on Food to boost one companion → walk toward Landmarks (Independence Palace, Central Post Office, Notre-Dame Basilica in Ho Chi Minh City District 1) → scan a (currently simulated) Vuforia Image Target → view a short cultural memory → collect a Stamp → optionally unlock a companion → record a Journey entry.

There is **no backend, multiplayer, or account system**. All progress lives in `player-save.json` under `Application.persistentDataPath` (see `LocalPlayerSaveStore`). GPS, walk metrics, and AR/Vuforia are still integration boundaries — the checked-in code uses deterministic mock providers so the whole loop is demoable without a phone.

Read `docs/UI-strategy.md` first for the product/UI contract (12 screens, 4 tabs, progression rules). `docs/Features_3D_Game.md` (Vietnamese) is the fuller game-design brief. `docs/AR_Pet_Walking_Tech_Architecture_UPDATED.md` and `docs/AR_Pet_Walking_Final_Presentation_UPDATED.md` are presentation-deck source content, not code specs — treat them as background, not ground truth for implementation details (the UI-strategy doc and the code itself are ground truth). `docs/MAP-WALK-PROVIDER-INTEGRATION.md` documents the exact contract a real GPS/AR integration must satisfy to replace the mocks; `docs/AR-3D-INTEGRATION-CONTRACT.md` is the equivalent contract for the AR & 3D teammates. `docs/FEATURE-PLAN-SYSTEMS.md` is the standing feature-gap plan for the systems/UI/data layer (excludes Map, Walk-tracking, AR, and 3D — those are two teammates' + the provider-integration doc's territory) — check it before proposing new work in that area, since it already tracks what's done vs. still open.

`docs/vietnamese-memory-explorer-2/` is an **archived Next.js/React mockup** (not part of the Unity build) kept only as a visual/UX reference. Do not modify it or wire it into the Unity project. `UI-examples/` (Pikmin Bloom assets, zipped + extracted) is reference art only, not imported into the project.

## Engine / stack

- Unity **6000.3.16f1** (Unity 6), C#.
- UI is built entirely in code with **UI Toolkit** (`UIDocument` + `VisualElement`), styled by one stylesheet `Assets/_Project/Resources/UI/ARWalking.uss`, using the **Unity App UI** package (`Unity.AppUI.UI`) for the root `Panel` (theme/scale) — not uGUI/Canvas. Two scenes host it: `Home.unity` (main app, `HomeUiController`) and `Walk.unity` (AR/Landmark memory + AR Photo, `WalkUiController`).
- No prefabs/inspector wiring drive the UI — everything is built imperatively in `Render()`/`Build*()` methods every time the route changes, from data in two `ScriptableObject`s: `PrototypeUiCatalog` (text/game data) and `PrototypeUiAssets` (texture references).

## Folder map

```
Assets/_Project/
  Scripts/
    UI/Comprehensive/   — UiContracts (enums/DTOs/interfaces), UiPrototypeRuntime (app singleton),
                           HomeUiController, WalkUiController, UiRouteCatalog/UiNavigationStack,
                           PrototypeUiCatalog, PrototypeUiAssets, IllustratedMapManipulator,
                           UiSafeAreaSimulation, UiStrings
    UI/                 — SceneNavigator, SafeAreaFitter (legacy uGUI-era helpers — see Known Issues)
    Services/           — LocalPlayerSaveStore, PlayerSaveData, CompanionProgressionService,
                           MockIntegrationProviders (Deterministic* + IntegrationProviderContract)
    Core/               — Bootstrap (Boot scene → Home)
    Editor/             — ARWalkingUiPrototypeSetup (regenerates catalog/assets/nav graph/scenes),
                           ARWalkingTestAutomation, UiVisualValidationCapture
  Resources/UI/         — generated ScriptableObjects + PanelSettings + ARWalking.uss (Resources.Load'd at runtime)
  Tests/EditMode|PlayMode/
  Scenes/               — Boot, Home, Walk
docs/                   — design/architecture docs (see above)
UI-examples/            — Pikmin Bloom reference assets (not imported)
TestArtifacts/          — test + visual-capture output (gitignored-ish; see UI_VISUAL_CAPTURE below)
```

## Runtime architecture

- `UiPrototypeRuntime` is a `DontDestroyOnLoad` singleton created via `RuntimeInitializeOnLoadMethod`. It owns navigation (`UiNavigationStack`), the save file (`LocalPlayerSaveStore` → `PlayerSaveData`), reward logic (`CompanionProgressionService`), and the two integration providers (`IWalkMetricsProvider`, `ILandmarkMapProvider`). `HomeUiController`/`WalkUiController` are thin: they call into `UiPrototypeRuntime` and rebuild their `VisualElement` tree on every `Navigator.Changed` event.
- Navigation is an enum-driven stack (`UiRoute`, `UiRootTab`, `UiOverlay`) in `UiRouteCatalog`/`UiNavigationStack` — not Unity's `NavigationScreen` at runtime (that App UI nav-graph asset, `ARWalkingNavigation.asset`, is generated for App UI tooling/editing convenience but the actual screen switch is the hand-rolled stack).
- `CompanionProgressionService.CompleteWalk` is the **single reward authority** (Growth EXP, Coins, Cat unlock at 1km). Providers must never compute rewards themselves — see `docs/MAP-WALK-PROVIDER-INTEGRATION.md`.
- Save/load: `PlayerSaveData` is `JsonUtility`-serialized to `player-save.json`. Corrupt/unsupported-schema files are renamed to a timestamped `.bak` and treated as "missing" (fresh onboarding). `RepairCollections()` backfills the three companions if a save predates a schema change.

## Editor tooling (Tools/AR Walking menu)

- **`Tools/AR Walking/Build Animal Companion Prototype`** (`ARWalkingUiPrototypeSetup.Build`) is the source of truth for `PrototypeUiAssets.asset` (textures/icons), `ARWalkingPanelSettings.asset`, `ARWalkingNavigation.asset`, texture import settings, and the `Home`/`Walk` scene wiring (destroys legacy `Canvas`/`UIController`, ensures `AppUIRoot` with the right controller) — re-run after changing `CreateAssetLibrary()`, since those overwrite unconditionally every run. **`PrototypeUiCatalog.asset` (companions/foods/landmarks/map/markers text data) is the one exception**: `CreateCatalog()` only populates it the *first* time the asset is created — once it has content, re-running the tool logs `"PrototypeUiCatalog already has content; leaving ... untouched"` and leaves it alone, so hand-edited copy (e.g. real cultural text replacing the placeholder paragraphs) survives future runs. Delete `PrototypeUiCatalog.asset` first if you actually want it regenerated from `CreateCatalog()`'s hardcoded source.
- **`Tools/AR Walking/Tests/Run Edit Mode`** / **`Run Play Mode`** — runs the two test assemblies (`ARWalking.EditModeTests`, `ARWalking.PlayModeTests`) and writes a plain-text report to `TestArtifacts/Results/{EditMode,PlayMode}.txt`.
- **`Tools/AR Walking/Visual Checks/Capture 720x1600 | 1080x2400 | 1440x3200`** — must be run **while already in Play Mode**. Waits 16 editor frames (toggling a simulated safe-area inset at frame 8) then `ScreenCapture.CaptureScreenshot`s to `TestArtifacts/UI/Home_{w}x{h}_SafeArea.png`, logging `UI_VISUAL_CAPTURE <path> actual=<w>x<h> safeInset=<px>` to the console. **The first capture after entering Play Mode can report a mismatched `actual=` resolution** (the custom Game View size doesn't always apply before the first frame) — if `actual` doesn't match the requested size, run the same capture menu item a second time rather than trusting the first screenshot. **This can persist for the whole Play session, not just the first capture** (seen 2026-09-02: two separate captures in one session both logged `actual=960x2658` against a requested `1080x2400`) — if a second attempt doesn't fix it, stop Play Mode and re-enter rather than repeatedly retrying capture in the same session; don't draw conclusions (e.g. "content is clipped/overflowing") from a screenshot whose logged `actual=` size disagrees with the requested size.
- `Tools/AR Walking/Visual Checks/Clear Safe Area Simulation` resets the simulated inset toggled by the capture flow.

## Testing with Unity MCP (`unity-mcp` server)

This project has the `unity-mcp` MCP server configured (global scope, `~/.claude.json`). It requires Unity Editor + its relay process (`relay_win.exe`) running, and is only loaded into a Claude Code session that started **after** the server was configured — restart Claude Code if `mcp__unity-mcp__*` tools aren't listed.

Useful tools: `Unity_ManageEditor` (Play/Pause/Stop/GetState), `Unity_ManageScene` (load/hierarchy — `Path` is the *folder*, not the `.unity` file, e.g. `Path: "Assets/_Project/Scenes"`, `Name: "Walk"`), `Unity_ReadConsole`/`Unity_GetConsoleLogs`, `Unity_ManageGameObject` (`get_component` on a chatty component like `UIDocument` can exceed the tool's output cap — prefer `get_components` sparingly or grep the saved overflow file it points you to), `Unity_ManageMenuItem` (execute the `Tools/AR Walking/...` items above), `Unity_RunCommand` (compiles + executes an ad-hoc `CommandScript : IRunCommand` in the Editor).

**Known gotcha (found during a 2026-09-01 defect-review session): calling `Unity_RunCommand` to *mutate live Play-mode state* (e.g. `UiPrototypeRuntime.Instance.Navigator.SwitchRoot(...)`) reliably left the Home scene rendering a blank cream screen on every subsequent frame** — the `HomeUiController`'s cached `VisualElement` references (`_panel`, `_safeRoot`) appear to desync from the live `UIDocument` panel after the command's compile step, and `Navigator.Changed` no longer produces visible output even though the underlying game state (route, save data) keeps updating correctly. `Time.frameCount` was also observed reset to a very low number after a couple of these calls, consistent with something reloading Play state. This reproduced consistently (Home Map → blank; back to Home Map → still blank) and is almost certainly a `RunCommand`-during-Play artifact, **not a product bug** — the project's own `Assets/_Project/Tests/PlayMode/UiPrototypePlayModeTests.cs` exercises the identical `SelectRoot`/navigation path in-process (no compile boundary) and passes. **When testing this UI live: prefer taking a screenshot via the `Tools/AR Walking/Visual Checks/Capture ...` menu item right after entering Play Mode (before any `Unity_RunCommand` call), and avoid interleaving `Unity_RunCommand` state-mutation calls with screenshot capture in the same Play session** if you need more than one clean screen capture — stop and re-enter Play Mode between them instead.

## Issues found in review, fixed 2026-09-01

1. **Map markers didn't visually differentiate player vs. Landmark type** — `HomeUiController.BuildMap()` was passing `"marker-" + marker.id` as the `name` argument of `IconAction(...)` instead of adding it as a CSS class, so `.marker-landmark` (blue) and `.marker-player` (darker green, 70px) in `ARWalking.uss` were dead code; every marker rendered identically. **Fixed** by adding `button.AddToClassList(marker.type == MapMarkerType.Player ? "marker-player" : "marker-landmark")` alongside the existing `map-marker` class; confirmed visually (Landmark pins now blue, player marker now larger/darker green).
2. **Every "inner" screen lost its intended page padding** — `HomeUiController.Page(string name, bool showNavigation)` only ever did `page.AddToClassList("page")`; the `name` argument (`"content-page"`, `"map-page"`, `"onboarding-setup-page"`) was used solely as the element's `name`, so `.content-page`'s 32px/20px padding never applied to any screen built via `ScreenWithHeader(...)` (Active Walk, Walk Result, Companion Collection, Companion Detail, Shop, Landmark Detail, Journey List, Journey Detail). **Fixed** by adding `page.AddToClassList(name)` inside `Page(...)`, and renaming the onboarding call site from `"onboarding-setup-page"` to `"onboarding-page"` so it now actually matches the `.onboarding-page` CSS selector (it previously didn't match even by name).
3. **Selected bottom-nav tab didn't recolor its icon** — `.selected-nav #nav-icon` set the same black tint as the unselected state, so only the pill background changed on selection. **Fixed** by changing `.selected-nav #nav-icon`'s tint to `rgb(71, 132, 83)`, matching `.selected-nav`'s text color.
4. **Icon semantics mismatch on Shop (star) and Companions (seedling) tabs** — left as-is per product decision: the Pikmin Bloom-derived 14-icon reference set has no dedicated shop/animal icon and every icon is already assigned elsewhere, so there's no better swap available without new art. Tabs are still labeled with text, so this is cosmetic. Revisit once real shop/animal iconography exists.
5. **Two dead legacy scripts** — `Assets/_Project/Scripts/UI/SafeAreaFitter.cs` and `SceneNavigator.cs` were confirmed unreferenced (not attached in `Boot`/`Home`/`Walk` scene hierarchies, no `.prefab` files exist in the project, no text/script references anywhere in `Assets/`) and **deleted** via `Unity_DeleteScript` (moved to OS trash, recoverable).

Regression check after all five fixes: `Tools/AR Walking/Tests/Run Edit Mode` (17/17 passed) and `Run Play Mode` (7/7 passed), console clean.

## Team split & systems-layer features added 2026-09-01

The team is split three ways: Map/Walk (real GPS + step tracking, contract in `docs/MAP-WALK-PROVIDER-INTEGRATION.md`), AR & 3D (Vuforia, 3D companion models/animation, AR scene, photo capture, contract in `docs/AR-3D-INTEGRATION-CONTRACT.md`), and everything else — the systems/data/UI layer this repo's owner is responsible for (tracked in `docs/FEATURE-PLAN-SYSTEMS.md`). To let the AR/3D teammates build against a stable contract instead of editing UI code directly, this session added:

- **`LandmarkUiData.companionRewardId`** (`UiContracts.cs`) — which companion (if any) a Landmark's AR Memory unlocks, now data (set per-entry in `PrototypeUiCatalog.asset`/`CreateCatalog()`) instead of hardcoded to Central Post Office/Rabbit. `CompanionProgressionService.CompleteLandmarkMemory(landmarkId, companionRewardId, utcNow)` takes it as a parameter; `LandmarkRewardDto.rabbitUnlocked` (bool) was replaced with `unlockedCompanionId` (string, empty = none) accordingly.
- **`UiPrototypeRuntime.GetCompanionVisualState(companionId)`** — returns a `CompanionVisualState` DTO (unlocked, `GrowthStage`, placeholder scale) so AR/3D spawn code can ask "what should I render" without touching `PlayerSaveData`.
- **`UiPrototypeRuntime.CompanionTapped` event / `NotifyCompanionTapped(id)`** — AR/3D code raises this on a tap-reaction; `WalkUiController` already subscribes and shows a toast, so the hook is demonstrably wired end-to-end today even with no AR scene yet.
- **`WalkUiController.OnImageTargetRecognized()`** — the named integration point for a real Vuforia `OnTargetFound` handler to call; it's just an alias for the existing `SimulateImageTargetRecognition()`, so the "Simulate recognition" debug button keeps working as a demo fallback after Vuforia is wired in.
- **`UiPrototypeRuntime.SaveArPhoto(byte[] pngBytes)`** (new overload alongside the existing mock `SaveArPhoto(string path = null)`) — writes real AR Photo bytes to `Application.persistentDataPath` and links the resulting path to whichever Landmark's Journey entry is currently selected, via a new `JourneyEntryData.photoPath` field. `HomeUiController.JourneyImage(journey)` loads and caches that file for the Journey list/detail screens, falling back to the bundled placeholder when no photo was saved.

Verification: `Tools/AR Walking/Tests/Run Edit Mode` (18/18) and `Run Play Mode` (8/8) pass, including new tests for per-landmark data-driven rewards (`LandmarkRewardIsDataDrivenPerLandmarkNotHardcodedToOneId`), photo→Journey linking, and `GetCompanionVisualState`.

## Dog placeholder replaced with a real rendered icon (2026-09-02)

The user imported `Assets/Bublisher/3D Stylized Animated Dogs Kit/` (5 dog breeds: FBX + prefab + animator controller each, ~104 MB, committed in full since it's real content the AR/3D teammates need, not a build byproduct). Two things came out of wiring it in:

- **The kit's shared material (`.../Materials/3D Stylized Animated Dogs Kit.mat`) used the Built-in `Standard` shader, which renders solid magenta under this project's URP pipeline.** Converted it in place to `Universal Render Pipeline/Lit` (remapping `_MainTex`→`_BaseMap`, `_BumpMap`, `_MetallicGlossMap`) so all five prefabs render correctly. This was a real bug the AR/3D teammates would have hit the first time they dragged any of these prefabs into a scene — worth telling them it's already fixed.
- **`PrototypeUiAssets.companions[0]` (the flat 2D Dog placeholder used by the UI Toolkit screens) now points at `Assets/_Project/Art/UI/ReferenceTemp/Spirits/dog-corgi.png`**, a 1024×1024 transparent-background icon rendered from the Corgi prefab via a one-off Editor script (temp additive scene + orthographic camera + directional/fill lights + `RenderTexture` readback, cleaned up immediately after). This is a 2D icon, not a 3D scene — there's still no 3D rendering pipeline in this repo's scope; Cat and Rabbit still use the archived plant artwork per `docs/UI-strategy.md`'s Artwork policy until similar source art exists for them.

Regenerate the icon (different breed, different framing) by re-running the render script pattern in the session that made this change, or by hand in the Editor: instantiate the prefab, frame it in an orthographic camera at roughly `orthographicSize = boundsExtentsMagnitude * 0.5` for a fill ratio matching the other Spirits/ art, render to a transparent `RenderTexture`, `EncodeToPNG`.

## Activity Dashboard added (2026-09-02)

The user pointed at `docs/images/dashboard.png`/`dashboard_button.png` and the Pikmin Bloom "Lifelog" reference (`UI-examples/UI-pikmin/lifelog-group/PB_Lifelog_Screenshot_2.jpg`) and asked for a walking-activity dashboard screen. This is data the systems/UI layer can own end-to-end (it only consumes the distance/steps numbers `IWalkMetricsProvider` already reports, the same boundary `docs/MAP-WALK-PROVIDER-INTEGRATION.md` already defines) without touching the Map/Walk teammate's real GPS work:

- **`PlayerSaveData.dailyActivity`** (`List<DailyActivityData>`) records one entry per UTC calendar day (`dateIso` "yyyy-MM-dd", `distanceKilometres`, `hasSteps`, `steps`), accumulated by `CompanionProgressionService.CompleteWalk` on every completed walk (a new `CompleteWalk(metrics, companionsUnlockedBeforeWalk, utcNow)` overload takes the timestamp explicitly, mirroring `CompleteLandmarkMemory`'s existing pattern, so tests stay deterministic).
- **`CompanionProgressionService.GetWeeklyActivity(utcNow)`** returns a `WeeklyActivityDto`: the Monday-Sunday week containing `utcNow` (7 `DayActivity` entries with `isToday`/`isFuture` flags), today's distance/steps, and a `weeklyAverageKilometres` averaged over Monday..today only (not zero-padded across the unfinished week). `DailyGoalKilometres` (5 km) is the shared constant behind both the progress bar and the chart's bar heights.
- **`UiRoute.ActivityDashboard`** (`HomeUiController.BuildActivityDashboard`) is a new pushed screen: today's progress toward the daily goal via the existing `.progress-track`/`.progress-fill` classes, plus a 7-bar Mon-Sun chart (`.weekly-chart-*` classes in `ARWalking.uss`) and a "Weekly average" pill. Reached via a new `activity-dashboard-button` (steps icon) on Home Map's header, next to Settings.
- Deliberately **not** a circular progress ring like the reference art — UI Toolkit has no native conic-gradient/arc fill, and pulling in App UI's `CircularProgress` component (which needs a custom shader material, `Hidden/App UI/CircularProgress`) was judged an unverified build-safety risk for a decorative element, inconsistent with the rest of this codebase's plain-`VisualElement` approach. The horizontal `.progress-track`/`.progress-fill` bar conveys the same "today vs. goal" information safely.

Verification: `Tools/AR Walking/Tests/Run Edit Mode` (20/20) and `Run Play Mode` (9/9) pass, including new tests for daily-activity accumulation, Monday-alignment/week-to-date averaging, and the new screen's navigation.

## Still-open item (not a code defect)

**Untracked stray folders in the working tree**, worth a deliberate keep/delete decision rather than leaving them: `Assets/_Recovery/0.unity` (Unity crash-recovery scene) and `Assets/UI Toolkit/UnityThemes` (default boilerplate from creating a UI Toolkit asset once, unused by the App UI-based runtime UI). Both showed up as untracked in `git status` at the start of the 2026-09-01 session and haven't been addressed.
