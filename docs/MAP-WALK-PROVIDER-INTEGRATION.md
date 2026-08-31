# Map and Walk Provider Integration

## Ownership boundary

The companion progression, save service, and UI depend only on `IWalkMetricsProvider` and `ILandmarkMapProvider`. A real GPS, route, step, or map implementation should replace the deterministic provider instances in `UiPrototypeRuntime` without changing reward logic.

## Walking provider

Implement `IWalkMetricsProvider`:

```csharp
public interface IWalkMetricsProvider
{
    bool IsWalking { get; }
    void StartWalk();
    WalkMetrics GetLiveMetrics();
    WalkMetrics StopWalk();
}
```

Contract:

- `distanceKilometres`: non-negative kilometres.
- `elapsedSeconds`: non-negative elapsed seconds, excluding paused time if pause is later supported.
- `hasSteps`: `true` only when a trustworthy step count is available.
- `steps`: non-negative integer steps; ignored when `hasSteps` is `false`.
- `StartWalk` begins a fresh measurement session.
- `GetLiveMetrics` returns a snapshot and must not consume or reset the session.
- `StopWalk` ends the session and returns the final snapshot.

Progression uses only distance. Do not calculate Coins or Growth EXP in the provider. `CompanionProgressionService.CompleteWalk` is the single reward authority.

## Landmark map provider

Implement `ILandmarkMapProvider`:

```csharp
public interface ILandmarkMapProvider
{
    LandmarkMapState GetMapState();
    LandmarkProximity GetLandmarkProximity(string landmarkId);
}
```

Contract:

- Landmark IDs are stable strings from `PrototypeIds`: `independence-palace`, `central-post-office`, and `notre-dame-basilica`.
- `playerNormalizedPosition` is in inclusive map coordinates 0–1 when `hasPlayerPosition` is true.
- `mapHeadingDegrees` is a heading in degrees.
- `distanceMetres` is a non-negative straight-line distance in metres.
- `directionDegrees` is degrees or a consistently documented normalized bearing convertible to degrees. The checked-in interface uses degrees.
- `isWithinUnlockRadius` is the provider's proximity decision. The UI does not recalculate the radius.

## Wiring the real providers

In the composition point where `UiPrototypeRuntime` is created, assign real implementations instead of `DeterministicWalkMetricsProvider` and `DeterministicLandmarkMapProvider`. Keep provider construction outside `HomeUiController` and `WalkUiController` so UI tests remain deterministic.

During automated tests, `UiPrototypeRuntime.TestWalkProviderOverride` and `TestMapProviderOverride` can inject provider doubles before the runtime is created. Production code should not set these fields.

Run the shared checks in `IntegrationProviderContract` against live snapshots during development. The Edit Mode suite also contains friend-provider stubs demonstrating the expected lifecycle and units.

## Merge checklist

1. Preserve all stable Landmark IDs.
2. Confirm kilometre/metre conversions at the adapter boundary.
3. Confirm `StopWalk` cannot return negative or accumulated data from an earlier walk.
4. Verify the optional-step path on a device with and without activity permission.
5. Verify location and camera permissions are requested only when their features open.
6. Run Edit Mode provider contract tests and the Play Mode Map → Walk → Result flow.
7. Confirm rewards remain identical when swapping mock and real providers for the same final metrics.
