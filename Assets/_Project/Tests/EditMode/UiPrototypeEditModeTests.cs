using System;
using System.Linq;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;

namespace ARWalking.Tests.EditMode
{
    public sealed class UiPrototypeEditModeTests
    {
        [Test]
        public void RouteCatalogContainsExactlyThirteenSupportedScreens()
        {
            Assert.That(UiRouteCatalog.All.Count, Is.EqualTo(13));
            Assert.That(UiRouteCatalog.All.Distinct().Count(), Is.EqualTo(13));
            Assert.That(UiRouteCatalog.All, Does.Contain(UiRoute.HomeMap));
            Assert.That(UiRouteCatalog.All, Does.Contain(UiRoute.ArPhoto));
            Assert.That(UiRouteCatalog.All, Does.Contain(UiRoute.JourneyDetail));
        }

        [Test]
        public void NavigationRootsAndBackStackAreDeterministic()
        {
            var navigation = new UiNavigationStack();
            navigation.SwitchRoot(UiRootTab.Garden);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.SeedlingGrowth));
            navigation.Push(UiRoute.HatchReveal);
            Assert.That(navigation.CanGoBack, Is.True);
            Assert.That(navigation.Back(), Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.SeedlingGrowth));
            Assert.That(navigation.Back(), Is.False);
        }

        [Test]
        public void OverlayConsumesBackBeforeScreenRoute()
        {
            var navigation = new UiNavigationStack();
            navigation.Push(UiRoute.ActiveWalk);
            navigation.ShowOverlay(UiOverlay.Settings);
            Assert.That(navigation.Back(), Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.ActiveWalk));
            Assert.That(navigation.CurrentOverlay, Is.Null);
        }

        [Test]
        public void MockCatalogIsCompleteAndValid()
        {
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.spirits.Count, Is.EqualTo(3));
            Assert.That(catalog.seedlings.Count, Is.EqualTo(3));
            Assert.That(catalog.landmarks.Count, Is.EqualTo(4));
            Assert.That(catalog.journeys.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(catalog.photographs.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(catalog.seedlings.All(seed => seed.requiredSteps > 0 && seed.Progress >= 0f && seed.Progress <= 1f), Is.True);
            Assert.That(catalog.spirits.Count(spirit => spirit.isSelected), Is.EqualTo(1));
        }

        [Test]
        public void MapMarkersUseOnlySupportedNormalizedTypes()
        {
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            Assert.That(catalog.markers, Is.Not.Empty);
            Assert.That(catalog.markers.All(marker => marker.normalizedPosition.x >= 0f && marker.normalizedPosition.x <= 1f), Is.True);
            Assert.That(catalog.markers.All(marker => marker.normalizedPosition.y >= 0f && marker.normalizedPosition.y <= 1f), Is.True);
            Assert.That(catalog.markers.Any(marker => marker.type == MapMarkerType.PlayerSpirit), Is.True);
            Assert.That(catalog.markers.Any(marker => marker.type == MapMarkerType.Landmark), Is.True);
            Assert.That(catalog.markers.Any(marker => marker.type == MapMarkerType.ArDiscoveryHint), Is.True);
        }

        [Test]
        public void PublicUiTypesContainNoRemovedCombatConcepts()
        {
            var forbidden = new[] { "mushroom", "battle", "attack", "hitpoint", "teamselection" };
            var publicNames = typeof(UiRoute).Assembly.GetExportedTypes().Select(type => type.FullName ?? type.Name).ToArray();
            foreach (var word in forbidden)
                Assert.That(publicNames.Any(name => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0), Is.False, word);

            foreach (var route in UiRouteCatalog.All)
                foreach (var word in forbidden)
                    Assert.That(route.ToString().IndexOf(word, StringComparison.OrdinalIgnoreCase), Is.LessThan(0), route.ToString());
        }

        [Test]
        public void RequiredRuntimeAssetsExist()
        {
            var library = Resources.Load<PrototypeUiAssets>("UI/PrototypeUiAssets");
            Assert.That(library, Is.Not.Null);
            Assert.That(library.illustratedMap, Is.Not.Null);
            Assert.That(library.illustratedMap.width, Is.EqualTo(2048));
            Assert.That(library.illustratedMap.height, Is.EqualTo(2048));
            Assert.That(library.spirits.Length, Is.EqualTo(3));
            Assert.That(library.landmarks.Length, Is.EqualTo(4));
        }
    }
}
