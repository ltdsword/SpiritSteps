using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ARWalking.UI
{
    [CreateAssetMenu(fileName = "PrototypeUiCatalog", menuName = "AR Walking/UI Prototype Catalog")]
    public sealed class PrototypeUiCatalog : ScriptableObject
    {
        [FormerlySerializedAs("spirits")]
        public List<CompanionUiData> companions = new List<CompanionUiData>();
        public List<FoodUiData> foods = new List<FoodUiData>();
        public List<LandmarkUiData> landmarks = new List<LandmarkUiData>();
        public IllustratedMapUiData map = new IllustratedMapUiData();
        public List<MapMarkerUiData> markers = new List<MapMarkerUiData>();
    }

    public sealed class StaticUiDataProvider : IUiDataProvider
    {
        readonly PrototypeUiCatalog _catalog;
        public StaticUiDataProvider(PrototypeUiCatalog catalog) { _catalog = catalog; }
        public IReadOnlyList<CompanionUiData> Companions => _catalog.companions;
        public IReadOnlyList<FoodUiData> Foods => _catalog.foods;
        public IReadOnlyList<LandmarkUiData> Landmarks => _catalog.landmarks;
    }

    public sealed class StaticMapDataProvider : IMapDataProvider
    {
        readonly PrototypeUiCatalog _catalog;
        public StaticMapDataProvider(PrototypeUiCatalog catalog) { _catalog = catalog; }
        public IllustratedMapUiData Map => _catalog.map;
        public IReadOnlyList<MapMarkerUiData> Markers => _catalog.markers;
    }
}
