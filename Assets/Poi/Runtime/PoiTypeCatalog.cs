using System;
using UnityEngine;

namespace Poi
{
    [Serializable]
    public struct PoiTypeRow
    {
        public PoiType type;
        public string displayName;
        public IconKey icon;
    }

    [CreateAssetMenu(fileName = "PoiTypeCatalog", menuName = "POI/Type Catalog")]
    public sealed class PoiTypeCatalog : ScriptableObject
    {
        public PoiTypeRow[] rows = new PoiTypeRow[PoiEnums.TypeCount];
    }
}
