# AR & 3D Integration Contract

## Ownership boundary

This mirrors `MAP-WALK-PROVIDER-INTEGRATION.md`: the systems/UI code (`Assets/_Project/Scripts/UI`, `Assets/_Project/Scripts/Services`) owns save data, reward math, and navigation. The AR/3D code owns Vuforia recognition, 3D companion models/animation, the AR scene, and photo capture. Neither side should reach into the other's implementation details - use the hooks below.

## Target recognition → memory flow

Call `WalkUiController.OnImageTargetRecognized()` (an `WalkUiController` instance is on the `AppUIRoot` GameObject in the `Walk` scene) from your Vuforia `OnTargetFound` handler once an Image Target is recognized. This advances the existing History → Architecture → Did You Know flow exactly as the current "Simulate recognition" debug button does - in fact `OnImageTargetRecognized()` just calls the same method that button calls, so the demo fallback keeps working even after Vuforia is wired in.

```csharp
public void OnImageTargetRecognized() => SimulateImageTargetRecognition();
```

There is currently no "target lost" handling - if your Vuforia integration needs one (e.g. to pause the flow when the player's camera drifts off the target mid-scan), that's new UI-side work; ping the systems side rather than adding it directly to the AR scene.

## What to spawn: `UiPrototypeRuntime.GetCompanionVisualState(string companionId)`

Returns a `CompanionVisualState` (species id, `unlocked`, `GrowthStage`, and the placeholder model `scale` for that stage - 0.70/0.85/1.00). Use this to decide what to instantiate and how big to scale it, instead of reading `PlayerSaveData` directly:

```csharp
var state = UiPrototypeRuntime.Instance.GetCompanionVisualState(PrototypeIds.Dog);
// state.unlocked, state.stage, state.scale
```

The currently-unlocked companion ids are also available via `UiPrototypeRuntime.Instance.SaveData.companions` if you need the full list (e.g. to decide which companion appears in a given AR scene).

## Tap reaction hook

If you want a UI reaction (e.g. a toast) when the player taps the AR companion, call:

```csharp
UiPrototypeRuntime.Instance.NotifyCompanionTapped(companionId);
```

`WalkUiController` already subscribes to this and shows a toast ("<Name> reacted!") - you don't need to build any UI for it, just raise the event after your tap-reaction animation plays. If you don't need a UI reaction, you can ignore this entirely; a tap can stay fully inside the AR scene.

## AR Photo hand-off

Once your capture code has composited the real-world camera feed with the 3D companion into a frame, hand it to:

```csharp
string savedPath = UiPrototypeRuntime.Instance.SaveArPhoto(pngBytes); // byte[], already PNG-encoded
```

This writes the file to `Application.persistentDataPath`, records it in the player's save data, and links it to the Journey entry for whichever Landmark is currently selected (`UiPrototypeRuntime.SelectedLandmarkIndex`) - so it shows up automatically in that Landmark's Journey detail screen. You do not need to touch `PlayerSaveData` or `JourneyEntryData` directly.

(There's also a `SaveArPhoto(string path = null)` overload used by the current mock "Save photo path" button - that one fabricates a fake path for demo purposes and doesn't require real image bytes. Once your real capture path is ready, prefer the `byte[]` overload.)

## Landmark AR readiness

`LandmarkUiData.imageTargetReady` (in `PrototypeUiCatalog.asset`, one entry per Landmark) controls whether the "Open simulated Image Target" button appears on that Landmark's detail screen. Set it to `true` once you have a working Vuforia target for that Landmark. `LandmarkUiData.companionRewardId` is a companion id (or empty) - set on whichever Landmark(s) should unlock a companion on completion; this is no longer hardcoded to any specific Landmark, so any of the three (or a future one) can carry a reward.

## Screen-Space vs World-Space memory panel

The History/Architecture/Did-You-Know panel currently renders as a Screen-Space UI Toolkit overlay (`WalkUiController`'s `ar-guide` element) layered on top of your AR camera view. This already satisfies the documented fallback ("Screen Space popup is acceptable if World Space UI is hard to read"). You're free to move it into World Space anchored near the spawned companion if you want the fuller AR feel - that's your call, not a required change on the systems side.
