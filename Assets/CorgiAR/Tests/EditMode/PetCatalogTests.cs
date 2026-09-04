using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CorgiAR.Tests
{
    public sealed class PetCatalogTests
    {
        [Test]
        public void Catalog_Has17UniquePets()
        {
            Assert.AreEqual(17, PetCatalog.Entries.Length);
            Assert.AreEqual(17, PetCatalog.Entries.Select(p => p.Id).Distinct().Count(),
                "pet ids must be unique");
            Assert.AreEqual(5, PetCatalog.Entries.Count(p => p.Family == PetFamily.DogKit));
            Assert.AreEqual(12, PetCatalog.Entries.Count(p => p.Family == PetFamily.UltimateAnimated));
        }

        [Test]
        public void Catalog_EverySourceModelExists()
        {
            foreach (PetEntry pet in PetCatalog.Entries)
                Assert.IsTrue(File.Exists(pet.SourcePrefabPath),
                    $"{pet.Id}: missing source model at {pet.SourcePrefabPath}");
        }

        [Test]
        public void Catalog_EveryEntryHasControllerThumbnailAndPositiveScale()
        {
            foreach (PetEntry pet in PetCatalog.Entries)
            {
                Assert.IsNotEmpty(pet.OverrideControllerPath, pet.Id);
                Assert.IsNotEmpty(pet.ThumbnailPath, pet.Id);
                Assert.IsNotEmpty(pet.DisplayName, pet.Id);
                Assert.IsTrue(float.IsFinite(pet.Scale) && pet.Scale > 0f,
                    $"{pet.Id}: scale {pet.Scale} must be a positive finite number");
            }

            // Dog Kit models are already the right size; only the UAA set is scaled down.
            Assert.IsTrue(PetCatalog.Entries.Where(p => p.Family == PetFamily.DogKit).All(p => p.Scale == 1f));
            Assert.IsTrue(PetCatalog.Entries.Where(p => p.Family == PetFamily.UltimateAnimated).All(p => p.Scale < 1f));
        }
    }
}
