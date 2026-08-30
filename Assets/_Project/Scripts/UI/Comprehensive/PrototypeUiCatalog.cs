using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    [CreateAssetMenu(fileName = "PrototypeUiCatalog", menuName = "AR Walking/UI Prototype Catalog")]
    public sealed class PrototypeUiCatalog : ScriptableObject
    {
        public List<SpiritUiData> spirits = new List<SpiritUiData>();
        public List<SeedlingUiData> seedlings = new List<SeedlingUiData>();
        public List<WalkUiData> walks = new List<WalkUiData>();
        public List<LandmarkUiData> landmarks = new List<LandmarkUiData>();
        public List<JourneyUiData> journeys = new List<JourneyUiData>();
        public List<PhotoUiData> photographs = new List<PhotoUiData>();
        public List<CollectibleUiData> collectibles = new List<CollectibleUiData>();
        public IllustratedMapUiData map = new IllustratedMapUiData();
        public List<MapMarkerUiData> markers = new List<MapMarkerUiData>();
    }

    public sealed class StaticUiDataProvider : IUiDataProvider
    {
        readonly PrototypeUiCatalog _catalog;

        public StaticUiDataProvider(PrototypeUiCatalog catalog) { _catalog = catalog; }
        public IReadOnlyList<SpiritUiData> Spirits => _catalog.spirits;
        public IReadOnlyList<SeedlingUiData> Seedlings => _catalog.seedlings;
        public IReadOnlyList<WalkUiData> Walks => _catalog.walks;
        public IReadOnlyList<LandmarkUiData> Landmarks => _catalog.landmarks;
        public IReadOnlyList<JourneyUiData> Journeys => _catalog.journeys;
        public IReadOnlyList<PhotoUiData> Photographs => _catalog.photographs;
        public IReadOnlyList<CollectibleUiData> Collectibles => _catalog.collectibles;
    }

    public sealed class StaticMapDataProvider : IMapDataProvider
    {
        readonly PrototypeUiCatalog _catalog;

        public StaticMapDataProvider(PrototypeUiCatalog catalog) { _catalog = catalog; }
        public IllustratedMapUiData Map => _catalog.map;
        public IReadOnlyList<MapMarkerUiData> Markers => _catalog.markers;
    }
}
