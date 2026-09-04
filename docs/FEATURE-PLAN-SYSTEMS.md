# Feature list to code — your scope (excludes Map, Walk-tracking, AR, and 3D)

## Context

Two teammates own AR & 3D (Vuforia recognition, 3D companion models/animation, AR companion spawn, AR Photo capture/compositing). Map and walk-distance tracking are separately scoped (`docs/MAP-WALK-PROVIDER-INTEGRATION.md`) and already excluded per your earlier request. What's left is the **systems/data/UI layer**: the gameplay rules, save data, and screens that already exist as a working prototype (`CompanionProgressionService`, `UiPrototypeRuntime`, `HomeUiController`/`WalkUiController`, 17+7 passing tests) but have real gaps once real AR/3D content gets plugged in by your teammates, plus the connective work that lets all three of you build in parallel without blocking each other.

The project already has exactly this kind of boundary for Map/Walk: `IWalkMetricsProvider` / `ILandmarkMapProvider` (in `Assets/_Project/Scripts/UI/Comprehensive/UiContracts.cs`, mocked by `MockIntegrationProviders.cs`). The single highest-value thing on this list is doing the same for AR/3D, so your two teammates have a stable contract to build against instead of reaching into your UI code directly.

---

## 1. Define the AR/3D integration contract — `MUST`, do first

**What**: A small set of interfaces/hook points (mirroring `IWalkMetricsProvider`/`ILandmarkMapProvider`) that your teammates' AR/3D code calls into or implements, so their work and yours can proceed independently.

**Why**: Right now the AR/3D touchpoints are informal — a button (`SimulateImageTargetRecognition()`) and a couple of flat placeholder `Image`s. Without an explicit contract, your teammates will end up editing `HomeUiController`/`WalkUiController` directly, which is exactly the coupling `IWalkMetricsProvider` was designed to avoid for Map/Walk.

**Concrete pieces to define** (in `UiContracts.cs`, alongside the existing provider interfaces):
- **Target recognition hook**: `WalkUiController.SimulateImageTargetRecognition()` already *is* the exact call your teammates need — when Vuforia reports a target found, they should call this method (or a renamed equivalent, e.g. `OnImageTargetRecognized()`) instead of you wiring a fake button. Document this as the seam; no real interface object needed, just a method they call.
- **Companion visual query**: an interface like `ICompanionVisualState { string SpeciesId; GrowthStage Stage; float Scale; }` (or just expose `CompanionProgressionService.StageFor`/`PlaceholderScaleFor` + the unlocked companion id list, which already exist) that their spawn/render code reads to know *what* to instantiate and *how big* — so they never need to touch your save data directly.
- **AR Photo hand-off**: define the shape of what their capture code gives you — e.g. a `Texture2D` (or raw PNG bytes) — and you own encoding it to disk and calling `UiPrototypeRuntime.SaveArPhoto(path)` (already exists) plus the Journey-linking in #3 below. Write this as a one-paragraph contract (in `docs/` or a code comment) so their capture code has a clear function signature to call.
- **Companion tap-reaction trigger** (optional): if they want your UI to react to an AR tap (e.g. show a toast), define a simple `event Action<string> CompanionTapped` they can raise — otherwise this stays entirely inside their AR scene and needs nothing from you.

**Where this lives**: `Assets/_Project/Scripts/UI/Comprehensive/UiContracts.cs` for interfaces/DTOs, `docs/` for a short written contract doc (same spirit as `MAP-WALK-PROVIDER-INTEGRATION.md`) so it's discoverable without reading code.

---

## 2. Make Landmark → companion rewards data-driven — `MUST`

**What**: Remove the hardcoded "only Central Post Office can unlock Rabbit" logic so any Landmark your teammates wire a working Image Target for can carry its own reward, without another code change.

**Why**: Once your teammates add real Vuforia targets for Independence Palace and/or Notre-Dame Basilica, those Landmarks need to be able to grant a Stamp (and optionally unlock a companion) too — currently only Central Post Office can.

**Current state**: `CompanionProgressionService.CompleteLandmarkMemory` (`Assets/_Project/Scripts/Services/CompanionProgressionService.cs`) hardcodes the stamp-id format and the Rabbit-unlock check to `PrototypeIds.CentralPostOffice` specifically. `LandmarkUiData` (`UiContracts.cs`) also has no reward field at all — only `imageTargetReady: bool`.

**To build**:
- Add a `companionRewardId` (string, empty = no reward) field to `LandmarkUiData` and populate it in `ARWalkingUiPrototypeSetup.CreateCatalog()`.
- Change `CompleteLandmarkMemory` to look up the reward from that field instead of comparing `landmarkId == PrototypeIds.CentralPostOffice`.
- Keep the stamp-id format landmark-agnostic (`landmarkId + "-stamp"` already is the fallback — just make it the only rule).
- Update `Assets/_Project/Tests/EditMode/UiPrototypeEditModeTests.cs` to assert the *configured* companion unlocks per landmark, not just Rabbit/Central Post Office.

---

## 3. AR Photo save + Journey linking (data/UI side only) — `SHOULD`

**What**: The non-AR half of AR Photo — once a teammate's capture code hands you a composited frame, you encode/save it and link it to the right Journey entry; Journey then displays the real photo instead of a placeholder.

**Why**: Journey is supposed to show "optional AR Photo" *per visited Landmark* — a per-entry link, not a global pool. The capture/compositing itself (camera + 3D companion) is your teammates' work; wiring where the result goes is yours.

**Current state**: `UiPrototypeRuntime.SaveArPhoto()` only ever fabricates a fake filename — never receives or writes real image data. `JourneyEntryData` (`PlayerSaveData.cs`) has no photo field; `PlayerSaveData.savedPhotoPaths` is a flat list disconnected from `journeys`. `HomeUiController.BuildJourneyList()`/`BuildJourneyDetail()` always show the same bundled placeholder image (`_assets.journeyOne`) regardless of what was captured.

**To build**:
- Add `photoPath` (nullable/empty string) to `JourneyEntryData`.
- Extend `SaveArPhoto` (or add an overload) to accept real image bytes/a `Texture2D` from the teammate's capture code, encode to PNG, write to `Application.persistentDataPath`, and associate the resulting path with the current Landmark's journey entry.
- In `HomeUiController`, load and display the real file (`Texture2D` + `LoadImage`) when `photoPath` is set, falling back to today's placeholder when it's empty (so older/photo-less entries still render).

---

## 4. Cultural content data pipeline — `MUST` (mechanism; the copy itself is a writing task, not code)

**What**: Decide how final History/Architecture/Did-You-Know text gets into the game, then wire it in.

**Why**: `docs/UI-strategy.md`: *"The historical text is prototype copy and requires cultural review before release."* Right now there's exactly one place this text lives, and it gets silently overwritten every time anyone regenerates the prototype.

**Current state**: `ARWalkingUiPrototypeSetup.CreateCatalog()` hardcodes placeholder paragraphs directly in C#. Running `Tools/AR Walking/Build Animal Companion Prototype` **overwrites** `PrototypeUiCatalog.asset` from that hardcoded source every time — so if someone hand-edits the `.asset` in the Inspector with real content, the next teammate who runs the setup tool wipes it out.

**To build**: Pick one:
- **(a)** Keep content in code (`CreateCatalog()`), and simply update the hardcoded strings once real copy is written — simplest, but the tool must not be re-run carelessly afterward, or content should be marked "only overwrite from code once."
- **(b)** Move Landmark/Companion/Food text out of the generator entirely so `PrototypeUiCatalog.asset` becomes the single source of truth, editable directly in the Inspector without re-running any tool — safer for a non-engineer to update copy later, slightly more setup work now (change `CreateCatalog()` to only fill fields if empty, or drop catalog population from the generator altogether).

This is a decision to make once, not a big build — flag it to whoever owns content updates.

---

## 5. Bilingual EN/VI text — `OPTIONAL`

**What**: Finish (or remove) the unused localization stub so UI copy can show in Vietnamese.

**Why**: The game is themed around Vietnamese landmarks/culture, and `UiStrings.cs` already exists half-built, suggesting it was intended — but nothing requires it.

**Current state**: `UiStrings.Get(key)` (`Assets/_Project/Scripts/UI/Comprehensive/UiStrings.cs`) covers 6 keys and is **never called** anywhere — every screen uses raw English literals directly. It's dead scaffolding today.

**To build (if wanted)**: Wire `UiStrings` through every UI string, add a Vietnamese dictionary, and add a language toggle (natural fit in the existing Settings overlay). If bilingual support isn't actually planned, delete the stub instead of leaving unused code around.

---

## 6. Non-companion UI sound effects — `OPTIONAL`

**What**: Basic SFX for button taps, reward/coin chimes, Stamp collected — UI-layer sounds only, not companion Idle/Eat/reaction sounds (those are tied to the 3D animation events your teammates own).

**Why**: Not mentioned in any scope doc — `Assets/_Project/Audio` exists but is empty — flagging only as discretionary polish, not planned scope.

---

## Explicitly not on your list (owned by the AR/3D teammates)

3D companion models & animation (Idle/Eat/reaction), Vuforia Image Target setup and recognition, AR companion spawn/scale/tap-reaction in the AR scene, World-Space vs Screen-Space placement of the memory panel (today's Screen-Space UI Toolkit panel already satisfies the documented fallback — they can keep using it as-is, or move it into World Space if they want the fuller AR feel), AR Photo capture/compositing itself, Camera runtime permission (Vuforia typically manages this itself), Accessories (3D-model-dependent), AR Memory Fragment Hunt.

Also excluded per your earlier request: `IWalkMetricsProvider`/`ILandmarkMapProvider` real implementations (GPS distance, GPS landmark proximity) — covered separately in `docs/MAP-WALK-PROVIDER-INTEGRATION.md`.

---

## Suggested order

1. **#1 Integration contract** — write this first so your teammates aren't blocked waiting to know what to call.
2. **#2 Data-driven Landmark rewards** — small, self-contained, unblocks them wiring a 2nd/3rd Landmark.
3. **#4 Content pipeline decision** — quick decision, prevents wasted content-writing work later.
4. **#3 AR Photo save/Journey linking** — do once a teammate has a capture function ready to call, per the contract from #1.
5. **#5, #6** — optional, fit in as time allows.

## Verification

- `Tools/AR Walking/Tests/Run Edit Mode` / `Run Play Mode` should keep passing throughout; extend the EditMode reward tests for #2 specifically (assert per-landmark unlock behavior, not just the current Rabbit/Central-Post-Office case).
- #1's contract should be validated by literally having a teammate call it from a stub (even a temporary test button) before they've built real Vuforia/3D content — if they can trigger your existing flow through the documented seam, the boundary is correct.
