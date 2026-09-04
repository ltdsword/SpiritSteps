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
