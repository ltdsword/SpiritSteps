using System.Linq;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;

namespace ARWalking.Tests.EditMode
{
    public sealed class LandmarkGeoCatalogAssetEditModeTests
    {
        [Test]
        public void RealAsset_PreservesThreeCalibrationAnchorsAndAddsLandmark81()
        {
            var catalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
            Assert.That(catalog, Is.Not.Null, "Assets/_Project/Resources/UI/LandmarkGeoCatalog.asset is missing.");

            var calibrationIds = new[] { PrototypeIds.IndependencePalace, PrototypeIds.CentralPostOffice, PrototypeIds.NotreDameBasilica };
            Assert.That(catalog.landmarks.Count, Is.EqualTo(4));
            Assert.That(catalog.landmarks.Count(entry => entry.isMapCalibrationAnchor), Is.EqualTo(3));
            foreach (var id in calibrationIds)
            {
                var entry = catalog.Find(id);
                Assert.That(entry, Is.Not.Null, $"missing catalog entry for {id}");
                Assert.That(entry.isMapCalibrationAnchor, Is.True, $"{id} must remain one of the original map calibration anchors");
                Assert.That(entry.latitude, Is.Not.EqualTo(0).Within(0.0001), $"{id} has a placeholder (0,0) coordinate");
            }

            var landmark81 = catalog.Find(PrototypeIds.Landmark81);
            Assert.That(landmark81, Is.Not.Null);
            Assert.That(landmark81.latitude, Is.Not.EqualTo(0).Within(0.0001));
            Assert.That(landmark81.isMapCalibrationAnchor, Is.False, "Landmark 81 is additive and must not replace an original calibration anchor.");
        }
    }
}
