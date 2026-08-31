# AGENTS.md — AR Walking (Animal Companion Prototype)

Orientation doc for future Claude Code / agent sessions in this repo. Read this first; it points at the deeper docs and explains how the pieces fit together.

## What this project is

A **local-only, walking-based animal companion prototype** for a school "3D location-based companion game" project (final project, ~12-day scope), inspired by Pikmin Bloom. Core loop: walk → distance grants Growth EXP to unlocked companions (Dog/Cat/Rabbit) + Coins → spend Coins on Food to boost one companion → walk toward Landmarks (Independence Palace, Central Post Office, Notre-Dame Basilica in Ho Chi Minh City District 1) → scan a (currently simulated) Vuforia Image Target → view a short cultural memory → collect a Stamp → optionally unlock a companion → record a Journey entry.

There is **no backend, multiplayer, or account system**. All progress lives in `player-save.json` under `Application.persistentDataPath` (see `LocalPlayerSaveStore`). GPS, walk metrics, and AR/Vuforia are still integration boundaries — the checked-in code uses deterministic mock providers so the whole loop is demoable without a phone.

Read `docs/UI-strategy.md` first for the product/UI contract (12 screens, 4 tabs, progression rules). `docs/Features_3D_Game.md` (Vietnamese) is the fuller game-design brief. `docs/AR_Pet_Walking_Tech_Architecture_UPDATED.md` and `docs/AR_Pet_Walking_Final_Presentation_UPDATED.md` are presentation-deck source content, not code specs — treat them as background, not ground truth for implementation details (the UI-strategy doc and the code itself are ground truth). `docs/MAP-WALK-PROVIDER-INTEGRATION.md` documents the exact contract a real GPS/AR integration must satisfy to replace the mocks.

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

- **`Tools/AR Walking/Build Animal Companion Prototype`** (`ARWalkingUiPrototypeSetup.Build`) is the source of truth for `PrototypeUiCatalog.asset`, `PrototypeUiAssets.asset`, `ARWalkingPanelSettings.asset`, `ARWalkingNavigation.asset`, texture import settings, and the `Home`/`Walk` scene wiring (destroys legacy `Canvas`/`UIController`, ensures `AppUIRoot` with the right controller). **Re-run this after changing catalog/asset-library code** (`CreateCatalog`/`CreateAssetLibrary` in that file) — hand-editing the generated `.asset` files will be overwritten next time someone runs it, and the two can silently drift if you edit one without the other.
- **`Tools/AR Walking/Tests/Run Edit Mode`** / **`Run Play Mode`** — runs the two test assemblies (`ARWalking.EditModeTests`, `ARWalking.PlayModeTests`) and writes a plain-text report to `TestArtifacts/Results/{EditMode,PlayMode}.txt`.
- **`Tools/AR Walking/Visual Checks/Capture 720x1600 | 1080x2400 | 1440x3200`** — must be run **while already in Play Mode**. Waits 16 editor frames (toggling a simulated safe-area inset at frame 8) then `ScreenCapture.CaptureScreenshot`s to `TestArtifacts/UI/Home_{w}x{h}_SafeArea.png`, logging `UI_VISUAL_CAPTURE <path> actual=<w>x<h> safeInset=<px>` to the console. **The first capture after entering Play Mode can report a mismatched `actual=` resolution** (the custom Game View size doesn't always apply before the first frame) — if `actual` doesn't match the requested size, run the same capture menu item a second time rather than trusting the first screenshot.
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

## Still-open item (not a code defect)

**Untracked stray folders in the working tree**, worth a deliberate keep/delete decision rather than leaving them: `Assets/_Recovery/0.unity` (Unity crash-recovery scene) and `Assets/UI Toolkit/UnityThemes` (default boilerplate from creating a UI Toolkit asset once, unused by the App UI-based runtime UI). Both showed up as untracked in `git status` at the start of the 2026-09-01 session and haven't been addressed.
