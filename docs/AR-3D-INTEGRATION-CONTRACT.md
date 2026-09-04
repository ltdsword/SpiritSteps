# AR & 3D Integration Contract

## Ownership boundary

The systems/UI code (`Assets/_Project/Scripts/UI`, `Assets/_Project/Scripts/Services`) owns
save data, reward math, and navigation. The AR/3D code (`Assets/CorgiAR`, ported from the
`CorgiAR` companion feature, plus the shared `Assets/ShibaFeeding/Scripts` feeding pieces it
depends on) owns AR Foundation placement/locomotion/interaction, the 3D companion models and
animation, and photo capture. Neither side should reach into the other's implementation
details - use the hooks below.

**Assembly direction is one-way.** `Assets/_Project/Scripts/ARWalking.Runtime.asmdef` is
`autoReferenced: true`, so `Assets/CorgiAR/Scripts` (plain `Assembly-CSharp`, no asmdef of its
own) can call into `ARWalking.UI` types - and does, in `ArPhotoCapture` and
`PetArContextBinder`. The reverse is not possible: ARWalking code cannot reference CorgiAR
types. The only join between the two sides is the companion id **string** - `PrototypeIds` in
`UiContracts.cs` is kept in sync by hand with `CorgiAR.PetCatalog.Entries` ids.

## One AR scene for every AR feature

Every AR-facing feature - Home/Map's "AR Photo", Companion's "View in AR", Feed's post-purchase
reward view, Walk's pet tap, and Landmark AR Memory - loads the same scene, `PetAr.unity`. There
is no per-feature AR scene. The only thing that differs between entry points is the context
passed right before the scene loads.

### Entering: `UiPrototypeRuntime.EnterPetAr`

```csharp
public void EnterPetAr(string petId, bool isPhotoMode,
    PendingPetInteraction interaction = PendingPetInteraction.None, string landmarkId = null)
```

This sets `PetArSceneContext` (plain static fields - they only need to survive one scene load)
and calls `SceneManager.LoadScene("PetAr")`:

- `petId` - which `CorgiAR.PetCatalog` entry to bind (`PetBinder.Bind`).
- `isPhotoMode` - whether the Capture button is shown.
- `interaction` - `PendingPetInteraction.Feed` nudges the player toward the food button after a
  Shop purchase; `None` for the plain place/view/interact/animate flows.
- `landmarkId` - non-null only for the Landmark AR Memory flow; turns on the
  History/Architecture/Did-You-Know overlay and the Collect Stamp action.

### Reading the context: `CorgiAR.PetArContextBinder`

On scene load, `PetArContextBinder` (on the `CorgiARCompanion` prefab root, runs after
`PetBinder`'s own `Start()` via `[DefaultExecutionOrder(1000)]`) reads `PetArSceneContext` and
applies it: binds the requested pet, shows/hides the Capture button
(`CorgiArHud.SetPhotoModeEnabled`), and toasts a feeding nudge when `interaction == Feed`.

### Leaving: `UiPrototypeRuntime.ReturnFromPetAr`

Pops the `UiRoute.PetAr` entry and loads `"Home"`. The AR-side Back button (drawn by
`WalkUiController`'s UI Toolkit overlay - see below) calls this; it is the only exit control
PetAr has, since CorgiAR's own uGUI HUD was originally a standalone sandbox scene with no
"return to app" concept.

## What to spawn: `UiPrototypeRuntime.GetCompanionVisualState(string companionId)`

Returns a `CompanionVisualState` (species id, `unlocked`, `GrowthStage`, and the placeholder
model `scale` for that stage - 0.70/0.85/1.00). `PetBinder`/`PetArContextBinder` bind by id
directly rather than reading this, but anything that needs to *decide* whether/how large to
show a companion outside PetAr (e.g. a future in-scene preview) should use this instead of
reading `PlayerSaveData` directly:

```csharp
var state = UiPrototypeRuntime.Instance.GetCompanionVisualState(PrototypeIds.Corgi);
// state.unlocked, state.stage, state.scale
```

## Tap reaction hook

`DogInteractionController`'s tap/pet reactions can raise a UI toast by calling:

```csharp
UiPrototypeRuntime.Instance.NotifyCompanionTapped(companionId);
```

`WalkUiController` already subscribes to this and shows a toast ("<Name> reacted!"). A tap can
also stay fully inside the AR scene without raising this at all.

## AR Photo hand-off

`CorgiAR.ArPhotoCapture.Capture()` hides the HUD for one frame, grabs the screen, and hands the
PNG bytes to:

```csharp
string savedPath = UiPrototypeRuntime.Instance.SaveArPhoto(pngBytes); // byte[], already PNG-encoded
```

This writes the file to `Application.persistentDataPath`, records it in the player's save data,
and links it to a Journey entry:

- If `PetArSceneContext.LandmarkId` is set (Landmark AR Memory flow), the photo attaches to that
  Landmark's existing Journey entry - same as before.
- Otherwise (Photo/Feed/Companion/Walk flows) it finds-or-creates a Journey entry for
  `PetArSceneContext.PetId` + today's date (`JourneyEntryData.companionId`), so a second photo of
  the same pet on the same day updates that entry instead of creating a duplicate.

`JourneyEntryData` carries exactly one of `landmarkId` or `companionId`, never both -
`HomeUiController`'s Journey list/detail screens branch on whichever is set.

## Landmark AR readiness

`LandmarkUiData.imageTargetReady` (in `PrototypeUiCatalog.asset`, one entry per Landmark)
controls whether the "Open AR Memory" button appears on that Landmark's detail screen.
`LandmarkUiData.companionRewardId` is a companion id (or empty) unlocked on completion - not
hardcoded to any specific Landmark or companion.

## Screen-Space memory overlay + Back button

`WalkUiController` (a `UIDocument` + this script, living in the `PetAr` scene) renders a UI
Toolkit overlay layered on top of the AR camera and CorgiAR's uGUI HUD Canvas
(`UIDocument.sortingOrder = 100`, above the HUD Canvas's default sorting order). It always draws
a top-left Back button (the only exit control PetAr has); it additionally draws the
History/Architecture/Did-You-Know guide and Collect Stamp action only when
`PetArSceneContext.LandmarkId` is set. Screen-Space is intentional here, not a fallback - see
`docs/UI-strategy.md`.

## Companion roster

Companion ids are the CorgiAR `PetCatalog` ids directly (`corgi`, `uaa_fox`, ...) - see
`PrototypeIds` in `UiContracts.cs`. Unlock thresholds live in
`Assets/_Project/Scripts/Services/CompanionRoster.cs` (walking-distance km per companion, or
`float.PositiveInfinity` for the one companion reserved as a Landmark reward); this is the
single source of truth `PlayerSaveData` seeding and `CompanionProgressionService.CompleteWalk`
both read from. `PrototypeUiAssets.companions` is a positional `Texture2D[]` loaded from
`Assets/CorgiAR/UI/Pets/<id>.png` in the same order as `PrototypeUiCatalog.companions` - keep
both in sync via `Tools/AR Walking/Build Animal Companion Prototype`
(`ARWalkingUiPrototypeSetup.cs`) rather than hand-editing the catalog asset.
