using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(fileName = "PoiPresentationCatalog", menuName = "POI/Presentation Catalog")]
    public sealed class PoiPresentationCatalog : ScriptableObject
    {
        public string markerAddress = "Poi/Marker";
        public string[] iconAddresses = new string[PoiEnums.IconCount];
        public Sprite[] placeholderSprites = new Sprite[PoiEnums.IconCount];
        public Material groundMaterial;
        public Color groundColor = new Color(0.16f, 0.22f, 0.16f, 1f);
        public Color playerColor = new Color(0.9f, 0.75f, 0.2f, 1f);
    }
}
