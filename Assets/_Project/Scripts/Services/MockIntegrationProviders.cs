using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    public sealed class DeterministicWalkMetricsProvider : IWalkMetricsProvider
    {
        readonly WalkMetrics _live;
        readonly WalkMetrics _result;
        public bool IsWalking { get; private set; }

        public DeterministicWalkMetricsProvider(WalkMetrics result = null)
        {
            _result = Clone(result ?? new WalkMetrics { distanceKilometres = 1f, hasSteps = true, steps = 1320, elapsedSeconds = 900f });
            _live = new WalkMetrics { distanceKilometres = 0.65f, hasSteps = _result.hasSteps, steps = _result.hasSteps ? 858 : 0, elapsedSeconds = 540f };
        }

        public void StartWalk() { IsWalking = true; }
        public WalkMetrics GetLiveMetrics() => IsWalking ? Clone(_live) : new WalkMetrics();
        public WalkMetrics StopWalk()
        {
            if (!IsWalking) throw new InvalidOperationException("A walk must be started before it can be stopped.");
            IsWalking = false;
            return Clone(_result);
        }

        static WalkMetrics Clone(WalkMetrics value) => new WalkMetrics
        {
            distanceKilometres = value.distanceKilometres,
            hasSteps = value.hasSteps,
            steps = value.steps,
            elapsedSeconds = value.elapsedSeconds
        };
    }

    public sealed class DeterministicLandmarkMapProvider : ILandmarkMapProvider
    {
        readonly Dictionary<string, LandmarkProximity> _landmarks = new Dictionary<string, LandmarkProximity>
        {
            { PrototypeIds.IndependencePalace, Proximity(PrototypeIds.IndependencePalace, 620f, 245f, false) },
            { PrototypeIds.CentralPostOffice, Proximity(PrototypeIds.CentralPostOffice, 80f, 35f, true) },
            { PrototypeIds.NotreDameBasilica, Proximity(PrototypeIds.NotreDameBasilica, 210f, 20f, false) }
        };

        public LandmarkMapState GetMapState() => new LandmarkMapState
        {
            hasPlayerPosition = true,
            playerNormalizedPosition = new Vector2(0.48f, 0.58f),
            mapHeadingDegrees = 0f
        };

        public LandmarkProximity GetLandmarkProximity(string landmarkId)
        {
            if (!_landmarks.TryGetValue(landmarkId, out var value))
                return Proximity(landmarkId, float.PositiveInfinity, 0f, false);
            return Proximity(value.landmarkId, value.distanceMetres, value.directionDegrees, value.isWithinUnlockRadius);
        }

        static LandmarkProximity Proximity(string id, float metres, float direction, bool unlocked) => new LandmarkProximity
        {
            landmarkId = id, distanceMetres = metres, directionDegrees = direction, isWithinUnlockRadius = unlocked
        };
    }

    public static class IntegrationProviderContract
    {
        public static bool IsValid(WalkMetrics metrics)
        {
            return metrics != null && metrics.distanceKilometres >= 0f && metrics.elapsedSeconds >= 0f &&
                   (!metrics.hasSteps || metrics.steps >= 0);
        }

        public static bool IsValid(LandmarkMapState state)
        {
            return state != null && (!state.hasPlayerPosition ||
                (state.playerNormalizedPosition.x >= 0f && state.playerNormalizedPosition.x <= 1f &&
                 state.playerNormalizedPosition.y >= 0f && state.playerNormalizedPosition.y <= 1f));
        }

        public static bool IsValid(LandmarkProximity proximity)
        {
            return proximity != null && !string.IsNullOrWhiteSpace(proximity.landmarkId) &&
                   proximity.distanceMetres >= 0f && proximity.directionDegrees >= -360f && proximity.directionDegrees <= 360f;
        }
    }
}
