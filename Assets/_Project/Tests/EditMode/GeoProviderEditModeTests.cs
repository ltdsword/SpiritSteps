using System;
using System.Collections.Generic;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;

namespace ARWalking.Tests.EditMode
{
    /// <summary>
    /// Covers the pure geo math and the real IWalkMetricsProvider/ILandmarkMapProvider wiring added for
    /// docs/MAP-WALK-PROVIDER-INTEGRATION.md. GPS/step hardware itself is not exercised here (there is none in
    /// Edit Mode) -- see the merge checklist in that doc for the required on-device Play Mode verification.
    /// </summary>
    public sealed class GeoProviderEditModeTests
    {
        [Test]
        public void HaversineMeters_OneDegreeOfLatitude_IsAboutOneHundredElevenKilometres()
        {
            var a = new GeoPoint(10.0, 106.0);
            var b = new GeoPoint(11.0, 106.0);
            var distance = GeoMath.HaversineMeters(a, b);
            Assert.That(distance, Is.EqualTo(111195.0).Within(500.0));
        }

        [Test]
        public void HaversineMeters_SamePoint_IsZero()
        {
            var p = new GeoPoint(10.7798, 106.6997);
            Assert.That(GeoMath.HaversineMeters(p, p), Is.EqualTo(0.0).Within(0.001));
        }

        [Test]
        public void BearingDegrees_DuePointsAreCardinal()
        {
            var origin = new GeoPoint(10.0, 106.0);
            Assert.That(GeoMath.BearingDegrees(origin, new GeoPoint(11.0, 106.0)), Is.EqualTo(0.0).Within(1.0));
            Assert.That(GeoMath.BearingDegrees(origin, new GeoPoint(10.0, 107.0)), Is.EqualTo(90.0).Within(1.0));
            Assert.That(GeoMath.BearingDegrees(origin, new GeoPoint(9.0, 106.0)), Is.EqualTo(180.0).Within(1.0));
            Assert.That(GeoMath.BearingDegrees(origin, new GeoPoint(10.0, 105.0)), Is.EqualTo(270.0).Within(1.0));
        }

        [Test]
        public void GeoToMapProjection_RoundTripsItsOwnCalibrationAnchors()
        {
            var geo0 = new GeoPoint(10.777498, 106.695347);
            var geo1 = new GeoPoint(10.779802, 106.699604);
            var geo2 = new GeoPoint(10.779666, 106.699000);
            var map0 = new Vector2(.32f, .45f);
            var map1 = new Vector2(.62f, .42f);
            var map2 = new Vector2(.55f, .36f);

            var projection = new GeoToMapProjection(geo0, map0, geo1, map1, geo2, map2);

            Assert.That(projection.Project(geo0), Is.EqualTo(map0).Using(ApproximatelyVector2));
            Assert.That(projection.Project(geo1), Is.EqualTo(map1).Using(ApproximatelyVector2));
            Assert.That(projection.Project(geo2), Is.EqualTo(map2).Using(ApproximatelyVector2));
        }

        [Test]
        public void GeoToMapProjection_CollinearAnchors_Throws()
        {
            var a = new GeoPoint(10.0, 106.0);
            var b = new GeoPoint(10.5, 106.0);
            var c = new GeoPoint(11.0, 106.0); // same longitude as a and b -> collinear
            Assert.Throws<ArgumentException>(() =>
                new GeoToMapProjection(a, new Vector2(0, 0), b, new Vector2(0, .5f), c, new Vector2(0, 1f)));
        }

        [Test]
        public void LandmarkGeoCatalog_FindsById_AndReturnsNullForUnknown()
        {
            var catalog = ScriptableObject.CreateInstance<LandmarkGeoCatalog>();
            try
            {
                catalog.landmarks = new List<LandmarkGeoData>
                {
                    new LandmarkGeoData { id = PrototypeIds.CentralPostOffice, latitude = 10.7798, longitude = 106.6997, unlockRadiusMeters = 100f }
                };
                Assert.That(catalog.Find(PrototypeIds.CentralPostOffice), Is.Not.Null);
                Assert.That(catalog.Find("not-a-real-id"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void DeviceLocationService_DefaultCenter_MatchesEditorSimulationStart()
        {
            var host = new GameObject("device-location-default-center-test-host");
            try
            {
                var location = host.AddComponent<DeviceLocationService>();
                location.Activate();
                Assert.That(location.Current, Is.EqualTo(DeviceLocationService.DefaultCenter));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void RealProviders_SatisfyTheSameContractAsTheDeterministicOnes()
        {
            var host = new GameObject("geo-provider-test-host");
            var geoCatalog = ScriptableObject.CreateInstance<LandmarkGeoCatalog>();
            try
            {
                geoCatalog.landmarks = new List<LandmarkGeoData>
                {
                    new LandmarkGeoData { id = PrototypeIds.CentralPostOffice, latitude = 10.7798, longitude = 106.6997, unlockRadiusMeters = 100f }
                };
                var location = host.AddComponent<DeviceLocationService>();
                var stepCounter = host.AddComponent<DeviceStepCounterService>();
                var projection = new GeoToMapProjection(
                    new GeoPoint(10.0, 106.0), new Vector2(0f, 0f),
                    new GeoPoint(11.0, 106.0), new Vector2(0f, 1f),
                    new GeoPoint(10.0, 107.0), new Vector2(1f, 0f));

                VerifyWalkProvider(new RealWalkMetricsProvider(location, stepCounter));
                VerifyMapProvider(new RealLandmarkMapProvider(location, geoCatalog, projection));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(geoCatalog);
            }
        }

        static void VerifyWalkProvider(IWalkMetricsProvider provider)
        {
            provider.StartWalk();
            Assert.That(provider.IsWalking, Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.GetLiveMetrics()), Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.StopWalk()), Is.True);
            Assert.That(provider.IsWalking, Is.False);
        }

        static void VerifyMapProvider(ILandmarkMapProvider provider)
        {
            Assert.That(IntegrationProviderContract.IsValid(provider.GetMapState()), Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.GetLandmarkProximity(PrototypeIds.CentralPostOffice)), Is.True);
        }

        sealed class Vector2ApproxComparer : System.Collections.Generic.IComparer<Vector2>
        {
            public int Compare(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.001f ? 0 : 1;
        }

        static readonly Vector2ApproxComparer ApproximatelyVector2 = new Vector2ApproxComparer();
    }
}
